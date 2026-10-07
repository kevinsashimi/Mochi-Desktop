using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MochiDesktop {
    sealed class DesktopWallpaperHost {
        public readonly IntPtr Parent,Icons,Wallpaper;
        public readonly bool Layered;
        public DesktopWallpaperHost(IntPtr parent,IntPtr icons,IntPtr wallpaper,bool layered){Parent=parent;Icons=icons;Wallpaper=wallpaper;Layered=layered;}
        public string Description {get{return Layered?"Windows layered desktop":"Classic Windows desktop";}}
    }
    // Windows 11's raised desktop requires a layered sibling between DefView and
    // WorkerW. WS_EX_LAYERED cannot share a class with CS_OWNDC, so this lightweight
    // container presents the static painted child without altering Explorer styles.
    sealed class WallpaperLayerWindow : NativeWindow,IDisposable {
        public WallpaperLayerWindow(IntPtr parent,Native.P origin,Size size){
            IntPtr window=WallpaperNative.CreateWindowEx(0x08000000|0x80|0x20|0x80000,WallpaperNative.LayerClass,"Mochi static wallpaper layer",0x40000000|0x04000000|0x02000000,
                origin.x,origin.y,size.Width,size.Height,parent,IntPtr.Zero,WallpaperNative.GetModuleHandle(null),IntPtr.Zero);
            if(window==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Could not create the wallpaper's desktop layer.");
            AssignHandle(window);
            if(!WallpaperNative.SetLayeredWindowAttributes(window,0,255,2)){int error=Marshal.GetLastWin32Error();Dispose();throw new System.ComponentModel.Win32Exception(error,"Could not enable the wallpaper's desktop layer.");}
        }
        public void ShowBelow(IntPtr icons){if(!WallpaperNative.SetWindowPos(Handle,icons,0,0,0,0,0x1|0x2|0x10|0x40))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Could not place the wallpaper below the desktop icons.");}
        protected override void WndProc(ref Message m){
            if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}
            if(m.Msg==0x21){m.Result=new IntPtr(3);return;}
            if(m.Msg==0x14){m.Result=new IntPtr(1);return;}
            base.WndProc(ref m);
        }
        public void Dispose(){IntPtr window=Handle;if(window!=IntPtr.Zero){ReleaseHandle();if(WallpaperNative.IsWindow(window))WallpaperNative.DestroyWindow(window);}}
    }

    static class WallpaperNative {
        internal const string CanvasClass="MochiStaticWallpaperCanvas";
        internal const string LayerClass="MochiStaticWallpaperLayer";
        static readonly WindowProc CanvasProcedure=DefWindowProc;static bool registered;
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct WindowClass {
            public uint Size,Style;public WindowProc Procedure;public int ClassExtra,WindowExtra;public IntPtr Instance,Icon,Cursor,Background;
            public string Menu,Name;public IntPtr SmallIcon;
        }
        internal delegate IntPtr WindowProc(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
        internal static void EnsureClass(){
            if(registered)return;
            RegisterCanvasClass(CanvasClass,false);RegisterCanvasClass(LayerClass,false);registered=true;
        }
        internal static void RegisterCanvasClass(string name,bool ownDC=false){
            WindowClass c=new WindowClass();c.Size=(uint)Marshal.SizeOf(typeof(WindowClass));c.Style=ownDC?0x20u:0;c.Procedure=CanvasProcedure;c.Instance=GetModuleHandle(null);c.Name=name;
            if(RegisterClassEx(ref c)==0 && Marshal.GetLastWin32Error()!=1410)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        internal static DesktopWallpaperHost FindWallpaperHost(){
            IntPtr progman=FindWindow("Progman",null);
            if(progman==IntPtr.Zero || GetShellWindow()==IntPtr.Zero)return null;
            // Ask Explorer to prepare its wallpaper layer before choosing a host.
            // A cached blank WorkerW is not proof it is the active wallpaper layer.
            IntPtr result;
            SendMessageTimeout(progman,0x052c,new IntPtr(0x0d),new IntPtr(1),2,750,out result);
            DesktopWallpaperHost host=FindExistingHost(progman);if(host!=null)return host;
            SendMessageTimeout(progman,0x052c,IntPtr.Zero,IntPtr.Zero,2,750,out result);
            host=FindExistingHost(progman);if(host!=null)return host;
            SendMessageTimeout(progman,0x052c,new IntPtr(0x0d),IntPtr.Zero,2,750,out result);
            SendMessageTimeout(progman,0x052c,new IntPtr(0x0d),new IntPtr(1),2,750,out result);
            return FindExistingHost(progman);
        }
        internal static bool HostAlive(DesktopWallpaperHost host){
            if(host==null || !IsWindow(host.Parent) || !IsWindowVisible(host.Parent) || !IsWindow(host.Icons) || !IsWindow(host.Wallpaper))return false;
            if(host.Layered)return GetParent(host.Icons)==host.Parent && GetParent(host.Wallpaper)==host.Parent && IsRaised(host.Parent,host.Icons);
            return !IsRaised(GetParent(host.Icons),host.Icons);
        }
        static bool IsRaised(IntPtr parent,IntPtr icons){return (GetWindowLong(parent,-20)&0x00200000)!=0 || (GetWindowLong(icons,-20)&0x80000)!=0;}
        internal static DesktopWallpaperHost FindExistingHost(IntPtr progman){
            uint process;GetWindowThreadProcessId(progman,out process);
            IntPtr direct=FindWindowEx(progman,IntPtr.Zero,"SHELLDLL_DefView",null);
            // Prefer Progman's actual icon view over unrelated/stale top-level WorkerWs.
            if(direct!=IntPtr.Zero)return SelectHost(progman,direct,process);
            DesktopWallpaperHost host=null;
            EnumWindows(delegate(IntPtr candidate,IntPtr param){
                if(ClassName(candidate)!="WorkerW" || !IsWindowVisible(candidate))return true;
                uint owner;GetWindowThreadProcessId(candidate,out owner);if(owner!=process)return true;
                IntPtr icons=FindWindowEx(candidate,IntPtr.Zero,"SHELLDLL_DefView",null);
                if(icons==IntPtr.Zero)return true;
                host=SelectHost(candidate,icons,process);return false;
            },IntPtr.Zero);
            return host;
        }
        static DesktopWallpaperHost SelectHost(IntPtr iconHost,IntPtr icons,uint process){
            bool raised=IsRaised(iconHost,icons);
            for(IntPtr child=FindWindowEx(iconHost,IntPtr.Zero,"WorkerW",null);child!=IntPtr.Zero;child=FindWindowEx(iconHost,child,"WorkerW",null)){
                if(!IsBlankWorker(child,process) || !IsBelow(child,icons))continue;
                // On raised desktops WorkerW paints Windows' own background. Our
                // layered window belongs beside it, not inside/underneath it.
                return raised?new DesktopWallpaperHost(iconHost,icons,child,true):new DesktopWallpaperHost(child,icons,child,false);
            }
            if(raised)return null; // Never fall back below a raised/opaque desktop.
            IntPtr sibling=FindWindowEx(IntPtr.Zero,iconHost,"WorkerW",null);
            return IsBlankWorker(sibling,process)?new DesktopWallpaperHost(sibling,icons,sibling,false):null;
        }
        internal static bool IsBelow(IntPtr window,IntPtr other){
            if(window==IntPtr.Zero || other==IntPtr.Zero || GetParent(window)!=GetParent(other))return false;
            for(IntPtr next=GetWindow(other,2);next!=IntPtr.Zero;next=GetWindow(next,2))if(next==window)return true;
            return false;
        }
        static bool IsBlankWorker(IntPtr window,uint shellProcess){
            if(window==IntPtr.Zero || !IsWindowVisible(window) || FindWindowEx(window,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero)return false;
            uint process;GetWindowThreadProcessId(window,out process);return process==shellProcess;
        }
        internal static string Describe(IntPtr window){
            Rect rect;GetWindowRect(window,out rect);
            return "0x"+window.ToInt64().ToString("x")+" "+ClassName(window)+" parent=0x"+GetParent(window).ToInt64().ToString("x")+
                " visible="+IsWindowVisible(window)+" style=0x"+GetWindowLong(window,-16).ToString("x8")+" ex=0x"+GetWindowLong(window,-20).ToString("x8")+" bounds="+rect.Value;
        }
        internal static string DesktopDescription(){
            StringBuilder text=new StringBuilder("Desktop windows (no titles or filenames):\r\n");
            EnumWindows(delegate(IntPtr candidate,IntPtr param){
                string kind=ClassName(candidate);if(kind!="Progman" && kind!="WorkerW")return true;
                text.AppendLine(Describe(candidate));DescribeChildren(text,candidate,0);return true;
            },IntPtr.Zero);return text.ToString();
        }
        static void DescribeChildren(StringBuilder text,IntPtr parent,int depth){
            if(depth>2)return;int count=0;
            for(IntPtr child=GetWindow(parent,5);child!=IntPtr.Zero && count++<64;child=GetWindow(child,2)){
                string kind=ClassName(child);
                if(kind=="WorkerW" || kind=="SHELLDLL_DefView" || kind=="SysListView32" || kind==CanvasClass || kind==LayerClass){text.Append(' ',(depth+1)*2);text.AppendLine(Describe(child));DescribeChildren(text,child,depth+1);}
            }
        }
        internal struct MonitorArea {public Rectangle Bounds,Work;}
        [StructLayout(LayoutKind.Sequential)] internal struct Rect {public int Left,Top,Right,Bottom;public Rectangle Value{get{return Rectangle.FromLTRB(Left,Top,Right,Bottom);}}}
        [StructLayout(LayoutKind.Sequential)] struct MonitorInfo {public int Size;public Rect Bounds,Work;public uint Flags;}
        delegate bool MonitorCallback(IntPtr monitor,IntPtr dc,ref Rect bounds,IntPtr param);
        internal static MonitorArea[] Monitors(){
            List<MonitorArea> result=new List<MonitorArea>();
            // Per-monitor aware enumeration returns physical pixels even when Mochi
            // itself is system-DPI aware and the monitors use different scaling.
            using(DpiScope scope=new DpiScope(new IntPtr(-4),true)){
                EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,delegate(IntPtr monitor,IntPtr dc,ref Rect rect,IntPtr param){
                    MonitorInfo info=new MonitorInfo();info.Size=Marshal.SizeOf(typeof(MonitorInfo));
                    if(GetMonitorInfo(monitor,ref info))result.Add(new MonitorArea{Bounds=info.Bounds.Value,Work=info.Work.Value});return true;
                },IntPtr.Zero);
            }
            return result.ToArray();
        }
        internal static string ClassName(IntPtr window){StringBuilder text=new StringBuilder(256);GetClassName(window,text,text.Capacity);return text.ToString();}
        internal static string FailureDetails(Exception error){
            System.ComponentModel.Win32Exception native=error as System.ComponentModel.Win32Exception;
            if(native==null)return error.Message;
            return error.Message+" Win32 "+native.NativeErrorCode+" (0x"+native.NativeErrorCode.ToString("X8")+"): "+new System.ComponentModel.Win32Exception(native.NativeErrorCode).Message;
        }
        internal static bool HasDesktopManifest(){
            // Check the native process manifest, not an ordinary managed resource.
            // Windows needs this compatibility declaration to allow layered children.
            try{
                IntPtr module=GetModuleHandle(null),resource=FindResource(module,new IntPtr(1),new IntPtr(24));
                if(resource==IntPtr.Zero)return false;
                uint length=SizeofResource(module,resource);if(length==0 || length>1048576)return false;
                IntPtr data=LockResource(LoadResource(module,resource));if(data==IntPtr.Zero)return false;
                byte[] bytes=new byte[(int)length];Marshal.Copy(data,bytes,0,bytes.Length);
                System.Xml.Linq.XDocument document=System.Xml.Linq.XDocument.Parse(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
                System.Xml.Linq.XNamespace ns="urn:schemas-microsoft-com:compatibility.v1";
                foreach(System.Xml.Linq.XElement os in document.Descendants(ns+"supportedOS"))
                    if(string.Equals((string)os.Attribute("Id"),"{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}",StringComparison.OrdinalIgnoreCase))return true;
                return false;
            }catch{return false;}
        }
        internal sealed class DpiScope : IDisposable {
            IntPtr previous;
            public DpiScope(IntPtr value,bool direct=false){
                try {IntPtr awareness=direct?value:GetWindowDpiAwarenessContext(value);if(awareness!=IntPtr.Zero)previous=SetThreadDpiAwarenessContext(awareness);}catch(EntryPointNotFoundException){}
            }
            public void Dispose(){if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous);}
        }
        [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr window);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindResource(IntPtr module,IntPtr name,IntPtr type);
        [DllImport("kernel32.dll")] static extern uint SizeofResource(IntPtr module,IntPtr resource);
        [DllImport("kernel32.dll")] static extern IntPtr LoadResource(IntPtr module,IntPtr resource);
        [DllImport("kernel32.dll")] static extern IntPtr LockResource(IntPtr resource);
        [DllImport("user32.dll",SetLastError=true)] internal static extern bool SetLayeredWindowAttributes(IntPtr window,uint key,byte alpha,uint flags);
        [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll",SetLastError=true)] internal static extern int SetWindowLong(IntPtr window,int index,int value);
        delegate bool EnumCallback(IntPtr window,IntPtr param);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern ushort RegisterClassEx(ref WindowClass c);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern IntPtr CreateWindowEx(int exStyle,string className,string title,int style,int x,int y,int w,int h,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
        [DllImport("user32.dll")] internal static extern bool DestroyWindow(IntPtr window);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr DefWindowProc(IntPtr window,uint message,IntPtr wp,IntPtr lp);
        [DllImport("user32.dll",SetLastError=true)] internal static extern bool ScreenToClient(IntPtr window,ref Native.P point);
        [DllImport("user32.dll",SetLastError=true)] internal static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int w,int h,uint flags);
        [DllImport("user32.dll")] internal static extern bool ValidateRect(IntPtr window,IntPtr rect);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumCallback callback,IntPtr param);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string name,string title);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string name,string title);
        [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wp,IntPtr lp,uint flags,uint timeout,out IntPtr result);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder text,int size);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window,out Rect rect);
        [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr window,out Rect rect);
        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorCallback callback,IntPtr param);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
        [DllImport("user32.dll")] static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    }
}
