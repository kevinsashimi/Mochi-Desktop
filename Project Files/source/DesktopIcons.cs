using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace MochiDesktop {
    enum DesktopIconStatus { Available, AutoArrange, HiddenIcons, Unavailable }

    sealed class DesktopItem {
        public string Id;
        public Point Position;
        public Size Cell;
    }

    sealed class DesktopLayout {
        public DesktopIconStatus Status;
        public string Detail = "";
        public readonly List<DesktopItem> Items = new List<DesktopItem>();
        public DesktopItem Find(string id) {
            return Items.Find(delegate(DesktopItem item) { return String.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase); });
        }
        public bool Free(string except, Point point, Size cell, Rectangle area) {
            Rectangle bounds = new Rectangle(point, cell);
            if (!area.Contains(bounds)) return false;
            // Grid cells may touch; their built-in spacing already includes the label.
            foreach (DesktopItem item in Items)
                if (!String.Equals(item.Id, except, StringComparison.OrdinalIgnoreCase) && bounds.IntersectsWith(new Rectangle(item.Position, item.Cell))) return false;
            return true;
        }
    }

    // Only icon positions are exposed. There is deliberately no filesystem move,
    // rename, delete, shell-execute, clipboard, or global desktop-setting operation.
    interface IDesktopIcons : IDisposable {
        string Problem { get; }
        DesktopLayout Read();
        Bitmap Picture(string id);
        bool TryMove(string id, Point expected, Point destination, out Point actual);
    }

    // Explorer's documented Shell folder-view interfaces marshal across processes.
    // No process-memory writes, injected code, or administrative access are needed.
    sealed partial class WindowsDesktopIcons : IDesktopIcons {
        object windows, desktop, browser, folder;
        IShellView view;
        IFolderView folderView;
        IntPtr viewWindow, root;
        bool disposed, connected;
        string operation = "Connecting to Windows Explorer", problem = "";
        public string Problem { get { return problem; } }

        bool Failed(string step, int hr) {
            if (hr == 0) return false;
            problem = step + " returned 0x" + unchecked((uint)hr).ToString("X8") + ".";
            return true;
        }
        string Failure(Exception error) {
            while (error is System.Reflection.TargetInvocationException && error.InnerException != null) error = error.InnerException;
            return operation + ": " + error.GetType().Name + " (0x" + unchecked((uint)error.HResult).ToString("X8") + ").";
        }

        bool Connect() {
            if (disposed) return false;
            if (connected) { if (IsWindow(viewWindow)) return true; problem = "The desktop view window is no longer available."; return false; }
            // A partially initialized COM view is not a usable connection.
            if (windows != null) return false;
            operation = "Creating ShellWindows";
            windows = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")));
            object location = 0, rootLocation = null;
            int ignored;
            operation = "Finding the desktop Shell window";
            desktop = ((IShellWindows)windows).FindWindowSW(ref location, ref rootLocation, 8, out ignored, 1); // SWC_DESKTOP, SWFO_NEEDDISPATCH
            if (desktop == null) { problem = "Windows Explorer did not return a desktop window."; return false; }
            Guid service = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837"), iid = typeof(IShellBrowser).GUID;
            operation = "Getting the desktop browser";
            if (Failed(operation, ((IServiceProvider)desktop).QueryService(ref service, ref iid, out browser))) return false;
            if (!ReadActiveView()) return false;
            if (!IsWindow(viewWindow)) { problem = "The desktop view window is no longer available."; return false; }
            iid = new Guid("000214E6-0000-0000-C000-000000000046"); // IShellFolder
            operation = "Getting the desktop folder";
            if (Failed(operation, folderView.GetFolder(ref iid, out folder))) return false;
            operation = "Getting the desktop folder identity";
            if (Failed(operation, SHGetIDListFromObject(folder, out root))) return false;
            connected = true;
            return true;
        }

        bool ReadActiveView() {
            operation = "Querying IShellBrowser on the desktop browser";
            IShellBrowser shellBrowser = (IShellBrowser)browser;
            operation = "Getting the active IShellView";
            if (Failed(operation, shellBrowser.QueryActiveShellView(out view))) return false;
            if (view == null) { problem = "Windows Explorer returned no active desktop view."; return false; }
            operation = "Querying IFolderView on the desktop view";
            folderView = (IFolderView)view;
            // GetWindow is part of IShellView's inherited vtable. Calling it through
            // that interface avoids asking the desktop proxy for a separate
            // IOleWindow identity, which can fail with E_NOINTERFACE.
            operation = "Getting the desktop window through IShellView.GetWindow";
            return !Failed(operation, view.GetWindow(out viewWindow));
        }

        DesktopIconStatus Status() {
            if (!Connect()) return DesktopIconStatus.Unavailable;
            if (!IsWindowVisible(viewWindow)) { problem = "The desktop icon view is hidden. Enable Desktop > View > Show desktop icons."; return DesktopIconStatus.HiddenIcons; }
            IntPtr list = FindWindowEx(viewWindow, IntPtr.Zero, "SysListView32", null);
            if (list == IntPtr.Zero) { problem = "Windows Explorer did not expose its desktop icon list."; return DesktopIconStatus.Unavailable; }
            if (!IsWindowVisible(list)) { problem = "Desktop icons are hidden. Enable Desktop > View > Show desktop icons."; return DesktopIconStatus.HiddenIcons; }
            operation = "Reading Auto arrange icons";
            int arrange = folderView.GetAutoArrange();
            if (arrange == 0) { problem = "Turn off Desktop > View > Auto arrange icons to let Mochi move an icon."; return DesktopIconStatus.AutoArrange; }
            if (arrange == 1) { problem = ""; return DesktopIconStatus.Available; }
            Failed(operation, arrange); return DesktopIconStatus.Unavailable;
        }

        string Identity(IntPtr relative) {
            IntPtr absolute = ILCombine(root, relative), name = IntPtr.Zero;
            if (absolute == IntPtr.Zero) return null;
            try {
                return SHGetNameFromIDList(absolute, 0x80028000, out name) == 0 ? Marshal.PtrToStringUni(name) : null; // SIGDN_DESKTOPABSOLUTEPARSING
            } finally { if (name != IntPtr.Zero) Marshal.FreeCoTaskMem(name); Marshal.FreeCoTaskMem(absolute); }
        }

        bool Position(IntPtr pidl, out Point screen) {
            NativePoint point;
            screen = Point.Empty;
            if (folderView.GetItemPosition(pidl, out point) != 0 || !ClientToScreen(viewWindow, ref point)) return false;
            screen = new Point(point.X, point.Y); return true;
        }

        // Item indexes can change during a trip. Resolve the original parsing identity
        // again before every operation; never reposition whatever now occupies an index.
        IntPtr Find(string id) {
            int count;
            if (folderView.ItemCount(2, out count) != 0 || count < 0 || count > 10000) return IntPtr.Zero; // SVGIO_ALLVIEW
            for (int i = 0; i < count; i++) {
                IntPtr item;
                if (folderView.Item(i, out item) != 0 || item == IntPtr.Zero) continue;
                bool keep = false;
                try { keep = String.Equals(Identity(item), id, StringComparison.OrdinalIgnoreCase); if (keep) return item; }
                finally { if (!keep) Marshal.FreeCoTaskMem(item); }
            }
            return IntPtr.Zero;
        }

        public DesktopLayout Read() {
            DesktopLayout result = new DesktopLayout { Status = DesktopIconStatus.Unavailable };
            try {
                result.Status = Status();
                result.Detail = problem;
                if (result.Status != DesktopIconStatus.Available) return result;
                NativePoint spacing; int count;
                operation = "Reading desktop icon spacing";
                if (Failed(operation, folderView.GetSpacing(out spacing))) return new DesktopLayout { Status = DesktopIconStatus.Unavailable, Detail = problem };
                if (spacing.X <= 0 || spacing.Y <= 0) return new DesktopLayout { Status = DesktopIconStatus.Unavailable, Detail = "Windows Explorer returned invalid icon spacing." };
                operation = "Counting desktop icons";
                if (Failed(operation, folderView.ItemCount(2, out count))) return new DesktopLayout { Status = DesktopIconStatus.Unavailable, Detail = problem };
                if (count < 0 || count > 10000) return new DesktopLayout { Status = DesktopIconStatus.Unavailable, Detail = "Windows Explorer returned an unsupported icon count." };
                operation = "Reading desktop icon identities and positions";
                for (int i = 0; i < count; i++) {
                    IntPtr item;
                    if (folderView.Item(i, out item) != 0 || item == IntPtr.Zero) continue;
                    try {
                        Point position; string id = Identity(item);
                        if (!String.IsNullOrEmpty(id) && Position(item, out position))
                            result.Items.Add(new DesktopItem { Id = id, Position = position, Cell = new Size(spacing.X, spacing.Y) });
                    } finally { Marshal.FreeCoTaskMem(item); }
                }
                if (count > 0 && result.Items.Count == 0) { result.Status = DesktopIconStatus.Unavailable; result.Detail = "Windows Explorer listed icons, but their identities or positions could not be read."; }
            } catch (Exception e) { if (!ShellFailure(e)) throw; problem = Failure(e); result = new DesktopLayout { Status = DesktopIconStatus.Unavailable, Detail = problem }; }
            return result;
        }

        public Bitmap Picture(string id) {
            try {
                if (Status() != DesktopIconStatus.Available) return null;
                operation = "Reading the selected icon artwork";
                IntPtr item = Find(id);
                if (item == IntPtr.Zero) return null;
                IntPtr absolute = IntPtr.Zero;
                ShellFileInfo info = new ShellFileInfo();
                try {
                    absolute = ILCombine(root, item);
                    if (absolute == IntPtr.Zero || SHGetFileInfo(absolute, 0, ref info, (uint)Marshal.SizeOf(typeof(ShellFileInfo)), 0x100 | 0x8 | 0x20) == IntPtr.Zero || info.Icon == IntPtr.Zero) return null;
                    using (Icon icon = Icon.FromHandle(info.Icon)) return icon.ToBitmap();
                } finally {
                    if (info.Icon != IntPtr.Zero) Native.DestroyIcon(info.Icon);
                    if (absolute != IntPtr.Zero) Marshal.FreeCoTaskMem(absolute);
                    Marshal.FreeCoTaskMem(item);
                }
            } catch (Exception e) { if (!ShellFailure(e)) throw; problem = Failure(e); return null; }
        }

        public bool TryMove(string id, Point expected, Point destination, out Point actual) {
            actual = expected;
            try {
                if (Status() != DesktopIconStatus.Available) return false;
                operation = "Moving the selected desktop icon";
                IntPtr item = Find(id);
                if (item == IntPtr.Zero) { problem = "The selected desktop icon is no longer available."; return false; }
                try {
                    Point current;
                    if (!Position(item, out current) || current != expected) return false; // Respect a user's move or desktop reflow.
                    NativePoint point = new NativePoint(destination.X, destination.Y);
                    if (!ScreenToClient(viewWindow, ref point)) return false;
                    // SVSI_POSITIONITEM | SVSI_NOSTATECHANGE | SVSI_NOTAKEFOCUS.
                    if (Failed(operation, folderView.SelectAndPositionItems(1, new[] { item }, new[] { point }, 0xC0000080))) return false;
                    return Position(item, out actual);
                } finally { Marshal.FreeCoTaskMem(item); }
            } catch (Exception e) { if (!ShellFailure(e)) throw; problem = Failure(e); return false; }
        }

        internal static bool ShellFailure(Exception e) {
            // COM activation failures can be wrapped by Activator on Mono and .NET.
            if (e is System.Reflection.TargetInvocationException && e.InnerException != null) return ShellFailure(e.InnerException);
            return e is COMException || e is InvalidCastException || e is NotImplementedException ||
                e is DllNotFoundException || e is EntryPointNotFoundException || e is PlatformNotSupportedException;
        }

        public void Dispose() {
            if (disposed) return; disposed = true;
            if (root != IntPtr.Zero) { Marshal.FreeCoTaskMem(root); root = IntPtr.Zero; }
            List<object> released = new List<object>();
            foreach (object value in new[] { folder, view, browser, desktop, windows }) {
                if (value == null || released.Exists(delegate(object prior) { return Object.ReferenceEquals(prior, value); })) continue;
                released.Add(value);
                if (Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
            }
            folderView = null; view = null; folder = browser = desktop = windows = null;
        }

        [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X, Y; public NativePoint(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct ShellFileInfo {
            public IntPtr Icon; public int IconIndex; public uint Attributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
        }
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr window, ref NativePoint point);
        [DllImport("shell32.dll")] static extern int SHGetIDListFromObject([MarshalAs(UnmanagedType.IUnknown)] object source, out IntPtr pidl);
        [DllImport("shell32.dll")] static extern IntPtr ILCombine(IntPtr root, IntPtr child);
        [DllImport("shell32.dll")] static extern int SHGetNameFromIDList(IntPtr pidl, uint kind, out IntPtr name);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFileInfo(IntPtr pidl, uint attributes, ref ShellFileInfo info, uint size, uint flags);

        [ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        interface IShellWindows {
            [DispId(0x60020008)] [return: MarshalAs(UnmanagedType.IDispatch)]
            object FindWindowSW([In] ref object location, [In] ref object root, int windowClass, out int window, int options);
        }
        [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IServiceProvider { [PreserveSig] int QueryService(ref Guid service, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object result); }
        // IShellView inherits these two IOleWindow slots after IUnknown. Only this
        // prefix is used; calls to the remaining IShellView methods are unnecessary.
        [ComImport, Guid("000214E3-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellView { [PreserveSig] int GetWindow(out IntPtr window); [PreserveSig] int ContextSensitiveHelp(bool enter); }
        [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellBrowser {
            [PreserveSig] int GetWindow(out IntPtr window);
            [PreserveSig] int ContextSensitiveHelp(bool enter);
            [PreserveSig] int InsertMenusSB(IntPtr menu, IntPtr widths);
            [PreserveSig] int SetMenuSB(IntPtr menu, IntPtr reserved, IntPtr active);
            [PreserveSig] int RemoveMenusSB(IntPtr menu);
            [PreserveSig] int SetStatusTextSB(IntPtr text);
            [PreserveSig] int EnableModelessSB(bool enable);
            [PreserveSig] int TranslateAcceleratorSB(IntPtr message, ushort id);
            [PreserveSig] int BrowseObject(IntPtr pidl, uint flags);
            [PreserveSig] int GetViewStateStream(uint mode, out IntPtr stream);
            [PreserveSig] int GetControlWindow(uint id, out IntPtr window);
            [PreserveSig] int SendControlMsg(uint id, uint message, IntPtr wparam, IntPtr lparam, out IntPtr result);
            [PreserveSig] int QueryActiveShellView(out IShellView view);
        }
        [ComImport, Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IFolderView {
            [PreserveSig] int GetCurrentViewMode(out uint mode);
            [PreserveSig] int SetCurrentViewMode(uint mode);
            [PreserveSig] int GetFolder(ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object folder);
            [PreserveSig] int Item(int index, out IntPtr pidl);
            [PreserveSig] int ItemCount(uint flags, out int count);
            [PreserveSig] int Items(uint flags, ref Guid iid, out IntPtr items);
            [PreserveSig] int GetSelectionMarkedItem(out int index);
            [PreserveSig] int GetFocusedItem(out int index);
            [PreserveSig] int GetItemPosition(IntPtr pidl, out NativePoint point);
            [PreserveSig] int GetSpacing(out NativePoint point);
            [PreserveSig] int GetDefaultSpacing(out NativePoint point);
            [PreserveSig] int GetAutoArrange();
            [PreserveSig] int SelectItem(int index, uint flags);
            [PreserveSig] int SelectAndPositionItems(uint count,
                [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] items,
                [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] NativePoint[] points, uint flags);
        }
    }
}
