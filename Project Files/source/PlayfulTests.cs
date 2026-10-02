using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Linq;

namespace MochiDesktop {
    sealed partial class WindowsDesktopIcons {
        // A native-style COM object built from unmanaged vtables, not a managed
        // class implementing our imported interfaces. This exercises the actual
        // CLR COM marshaler without connecting to or moving real desktop icons.
        sealed class ViewInteropFixture : IDisposable {
            const int NoInterface = unchecked((int)0x80004002);
            static readonly Guid UnknownId = new Guid("00000000-0000-0000-C000-000000000046");
            static readonly Guid BrowserId = new Guid("000214E2-0000-0000-C000-000000000046");
            static readonly Guid ViewId = new Guid("000214E3-0000-0000-C000-000000000046");
            static readonly Guid FolderId = new Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE");
            static readonly Guid OleWindowId = new Guid("00000114-0000-0000-C000-000000000046");
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Query(IntPtr self, ref Guid iid, out IntPtr value);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint Reference(IntPtr self);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int PointerResult(IntPtr self, out IntPtr value);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int ModeResult(IntPtr self, out uint value);
            readonly List<IntPtr> allocations = new List<IntPtr>();
            readonly List<Delegate> callbacks = new List<Delegate>();
            public readonly IntPtr Browser, View, Folder;
            public int BrowserReferences = 1, ViewReferences = 1, OleWindowQueries, WindowCalls;
            public int ViewError, WindowError;
            public bool ExposeFolder = true;

            public ViewInteropFixture() {
                Browser = Allocate(16); View = Allocate(5); Folder = Allocate(17);
                foreach (IntPtr pointer in new[] { Browser, View, Folder }) {
                    Slot(pointer, 0, new Query(QueryInterface));
                    Slot(pointer, 1, new Reference(AddReference));
                    Slot(pointer, 2, new Reference(ReleaseReference));
                }
                // IShellBrowser::QueryActiveShellView is slot 15 in the Windows SDK.
                Slot(Browser, 15, new PointerResult(delegate(IntPtr self, out IntPtr value) {
                    value = IntPtr.Zero; if (ViewError != 0) return ViewError;
                    value = View; AddReference(View); return 0;
                }));
                Slot(View, 3, new PointerResult(delegate(IntPtr self, out IntPtr value) {
                    WindowCalls++; value = new IntPtr(0x1234); return WindowError;
                }));
                Slot(Folder, 3, new ModeResult(delegate(IntPtr self, out uint value) { value = 1; return 0; }));
            }
            IntPtr Allocate(int slots) {
                IntPtr table = Marshal.AllocHGlobal(slots * IntPtr.Size), pointer = Marshal.AllocHGlobal(IntPtr.Size);
                allocations.Add(table); allocations.Add(pointer);
                for (int n = 0; n < slots; n++) Marshal.WriteIntPtr(table, n * IntPtr.Size, IntPtr.Zero);
                Marshal.WriteIntPtr(pointer, table); return pointer;
            }
            void Slot(IntPtr pointer, int slot, Delegate callback) {
                callbacks.Add(callback);
                Marshal.WriteIntPtr(Marshal.ReadIntPtr(pointer), slot * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(callback));
            }
            int QueryInterface(IntPtr self, ref Guid iid, out IntPtr value) {
                value = IntPtr.Zero;
                if (iid == OleWindowId) { OleWindowQueries++; return NoInterface; }
                if (iid == UnknownId) value = self == Browser ? Browser : View;
                else if (self == Browser && iid == BrowserId) value = Browser;
                else if (self != Browser && iid == ViewId) value = View;
                else if (self != Browser && iid == FolderId && ExposeFolder) value = Folder;
                if (value == IntPtr.Zero) return NoInterface;
                AddReference(value); return 0;
            }
            uint AddReference(IntPtr self) { return (uint)(self == Browser ? ++BrowserReferences : ++ViewReferences); }
            uint ReleaseReference(IntPtr self) { return (uint)(self == Browser ? --BrowserReferences : --ViewReferences); }
            public void RejectLegacyCast() {
                Guid iid = OleWindowId; IntPtr ignored;
                int hr = Marshal.QueryInterface(View, ref iid, out ignored);
                PlayfulTests.Check(hr == NoInterface && ignored == IntPtr.Zero, "fixture must reproduce the legacy IOleWindow E_NOINTERFACE");
                OleWindowQueries = 0;
            }
            public void Dispose() {
                foreach (IntPtr pointer in allocations) Marshal.FreeHGlobal(pointer);
                GC.KeepAlive(callbacks);
            }
        }

        public static void VerifyViewInterop() {
            for (int scenario = 0; scenario < 4; scenario++) using (ViewInteropFixture fixture = new ViewInteropFixture()) {
                fixture.RejectLegacyCast();
                if (scenario == 1) fixture.ViewError = unchecked((int)0x80004002);
                if (scenario == 2) fixture.ExposeFolder = false;
                if (scenario == 3) fixture.WindowError = unchecked((int)0x80004005);
                using (WindowsDesktopIcons client = new WindowsDesktopIcons()) {
                    client.browser = Marshal.GetObjectForIUnknown(fixture.Browser);
                    bool available;
                    try { available = client.ReadActiveView(); }
                    catch (Exception error) { if (!ShellFailure(error)) throw; client.problem = client.Failure(error); available = false; }
                    if (scenario == 0) {
                        uint mode;
                        PlayfulTests.Check(available && client.viewWindow == new IntPtr(0x1234) && fixture.WindowCalls == 1,
                            "IShellView.GetWindow must work without separately exposing IOleWindow");
                        PlayfulTests.Check(client.folderView.GetCurrentViewMode(out mode) == 0 && mode == 1, "IFolderView was not queried from the view correctly");
                    } else {
                        string expected = scenario == 1 ? "Getting the active IShellView" : scenario == 2 ? "Querying IFolderView" : "IShellView.GetWindow";
                        PlayfulTests.Check(!available && client.Problem.Contains(expected) && client.Problem.Contains("0x8000400"),
                            "native failure lost the exact interface stage: " + client.Problem);
                    }
                    PlayfulTests.Check(fixture.OleWindowQueries == 0, "desktop connection still requests the optional IOleWindow identity");
                }
                PlayfulTests.Check(fixture.BrowserReferences == 1 && fixture.ViewReferences == 1,
                    "COM view connection leaked references: " + fixture.BrowserReferences + ", " + fixture.ViewReferences);
            }
        }
    }

    // These fixtures change in-memory coordinates only, including when run on a real desktop.
    sealed class PlayfulDesktopFixture {
        public readonly DesktopLayout Layout = new DesktopLayout { Status = DesktopIconStatus.Available };
        public int Moves, Disposals, Reads;
        public bool DenyMoves, Snap;
        public readonly HashSet<string> MissingPictures = new HashSet<string>();
        public readonly List<Point> Positions = new List<Point>();
        public PlayfulDesktopFixture(Point source) {
            Layout.Items.Add(new DesktopItem { Id = "fixture:borrowed", Position = source, Cell = new Size(72, 84) });
        }
        public IDesktopIcons Open() { return new FixtureDesktop(this); }
        public static Bitmap Picture() {
            Bitmap icon = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(icon)) {
                g.Clear(Color.Transparent);
                using (Brush tab = new SolidBrush(Color.FromArgb(247, 194, 81))) g.FillRectangle(tab, 2, 5, 12, 7);
                using (Brush folder = new SolidBrush(Color.FromArgb(239, 174, 53))) g.FillRectangle(folder, 2, 10, 28, 18);
                using (Brush mark = new SolidBrush(Color.FromArgb(65, 103, 151))) g.FillRectangle(mark, 6, 14, 5, 10);
            }
            return icon;
        }
        sealed class FixtureDesktop : IDesktopIcons {
            readonly PlayfulDesktopFixture state; bool disposed;
            public string Problem { get { return state.DenyMoves ? "Fixture rejected the position change." : ""; } }
            public FixtureDesktop(PlayfulDesktopFixture source) { state = source; }
            public DesktopLayout Read() {
                state.Reads++;
                DesktopLayout copy = new DesktopLayout { Status = disposed ? DesktopIconStatus.Unavailable : state.Layout.Status, Detail = state.Layout.Detail };
                foreach (DesktopItem item in state.Layout.Items) copy.Items.Add(new DesktopItem { Id = item.Id, Position = item.Position, Cell = item.Cell });
                return copy;
            }
            public Bitmap Picture(string id) { return state.Layout.Find(id) == null || state.MissingPictures.Contains(id) ? null : PlayfulDesktopFixture.Picture(); }
            public bool TryMove(string id, Point expected, Point destination, out Point actual) {
                actual = expected; DesktopItem item = state.Layout.Find(id);
                if (disposed || state.DenyMoves || state.Layout.Status != DesktopIconStatus.Available || item == null || item.Position != expected) return false;
                actual = state.Snap ? new Point((int)Math.Round(destination.X / 24.0) * 24, (int)Math.Round(destination.Y / 24.0) * 24) : destination;
                item.Position = actual; state.Moves++; state.Positions.Add(actual); return true;
            }
            public void Dispose() { if (!disposed) { disposed = true; state.Disposals++; } }
        }
    }

    static class PlayfulTests {
        public static void Check(bool condition, string message) { if (!condition) throw new Exception("Playful Mode: " + message); }
        static readonly string[] SampleLines = { "Psst... this one\nneeds a little holiday!", "Nothing to sea here!", "Special delivery!", "Hee hee!\nWho moved that?" };
        public static PlayfulTrip Trip(PlayfulDesktopFixture desktop, MischiefKind kind, bool? right = null, int size = 176, Rectangle? screen = null) {
            Rectangle area = screen ?? new Rectangle(0, 0, 1600, 1000);
            Point origin = new Point(area.Left + area.Width / 2 - 100, area.Top + 50);
            Random random = new Random(871);
            for (int attempt = 0; attempt < 100; attempt++) {
                DesktopLayout snapshot;
                using (IDesktopIcons reader = desktop.Open()) snapshot = reader.Read();
                PlayfulPlan plan = PlayfulPlan.Create(snapshot, snapshot.Items[0], origin, size, area, random, kind);
                if (plan != null && (!right.HasValue || plan.FaceRight == right.Value))
                    return new PlayfulTrip(plan, desktop.Open(), PlayfulDesktopFixture.Picture(), 0, SampleLines, attempt % 2);
            }
            throw new Exception("Fixture could not find a route.");
        }
        static double Until(PlayfulTrip trip, MischiefStage wanted, double now = 0) {
            for (int n = 0; n < 1500; n++, now += .05) {
                if (trip.Stage == wanted) return now;
                Check(trip.Advance(now), "trip stopped before " + wanted);
            }
            throw new Exception("Trip stalled.");
        }
        static void Settings(string directory) {
            string path = Path.Combine(directory, "playful-settings-test.xml");
            File.WriteAllText(path, "<Mochi><Size>224</Size><Roam>false</Roam><NextFeed>3</NextFeed></Mochi>");
            Preferences prefs = Preferences.Load(path);
            Check(!prefs.PlayfulMode && !prefs.Roam && prefs.Size == 224 && prefs.NextFeed == 3, "legacy settings or opt-in default");
            prefs.PlayfulMode = true; prefs.Save(path); prefs = Preferences.Load(path);
            Check(prefs.PlayfulMode && prefs.NextFeed == 3 && !prefs.Roam, "enabled mode did not persist independently of swimming");
            prefs.PlayfulMode = false; prefs.Save(path); Check(!Preferences.Load(path).PlayfulMode, "disabled mode did not persist");
            using (SettingsDialog form = new SettingsDialog(prefs)) {
                Control[] toggles = form.Controls.Find("PlayfulModeToggle", true);
                Check(toggles.Length == 1 && toggles[0] is CheckBox && !((CheckBox)toggles[0]).Checked, "settings-only toggle missing");
                foreach (Control control in form.Controls) Check(form.ClientRectangle.Contains(control.Bounds), "settings control outside panel: " + control.Text);
                ((CheckBox)toggles[0]).Checked = true;
                form.Show(); ((Button)form.AcceptButton).PerformClick();
                Check(form.DialogResult == DialogResult.OK && prefs.PlayfulMode, "settings Save did not apply toggle");
            }
            using (SettingsDialog form = new SettingsDialog(prefs)) {
                ((CheckBox)form.Controls.Find("PlayfulModeToggle", true)[0]).Checked = false;
                form.Show(); ((Button)form.CancelButton).PerformClick();
                Check(prefs.PlayfulMode, "settings Cancel changed the preference");
            }
            prefs.PlayfulMode = false;
            using (SettingsDialog form = new SettingsDialog(prefs)) {
                Button tryNow = (Button)form.Controls.Find("TryPlayfulNow", true)[0];
                Check(!tryNow.Enabled && !form.TryPlayfulRequested, "Try now must require opt-in");
                Check(form.Controls.Find("PlayfulStatus", true).Length == 1, "desktop status missing from Settings");
                ((CheckBox)form.Controls.Find("PlayfulModeToggle", true)[0]).Checked = true;
                form.Show(); tryNow.PerformClick();
                Check(form.DialogResult == DialogResult.OK && form.TryPlayfulRequested && prefs.PlayfulMode, "Save and try now did not apply the opt-in setting");
            }
        }
        static void Routes() {
            Random random = new Random(405); HashSet<SwimStyle> styles = new HashSet<SwimStyle>();
            HashSet<int> directions = new HashSet<int>(); HashSet<bool> sides = new HashSet<bool>();
            double shortest = Double.MaxValue, longest = 0; int count = 0;
            foreach (Rectangle area in new[] { new Rectangle(0, 0, 1920, 1040), new Rectangle(-1600, -300, 1600, 1200), new Rectangle(1920, -200, 1080, 1700) })
                foreach (int size in new[] { 144, 176, 224 }) foreach (MischiefKind kind in Enum.GetValues(typeof(MischiefKind)))
                    for (int i = 0; i < 70; i++) {
                        Point icon = i % 3 == 0 ? area.Location : new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
                        PlayfulDesktopFixture fixture = new PlayfulDesktopFixture(icon);
                        DesktopLayout layout; using (IDesktopIcons desktop = fixture.Open()) layout = desktop.Read();
                        Point from = Motion.Clamp(new Point(area.Left + area.Width / 2, area.Top + area.Height / 2), Renderer.WindowSize(size), area);
                        PlayfulPlan plan = PlayfulPlan.Create(layout, layout.Items[0], from, size, area, random, kind);
                        Check(plan != null, "could not reach an edge/negative-monitor icon"); count++;
                        sides.Add(plan.FaceRight); styles.Add(plan.Carry.Style); directions.Add(Motion.Direction(plan.Carry.End.X - plan.Carry.Start.X, plan.Carry.End.Y - plan.Carry.Start.Y));
                        shortest = Math.Min(shortest, plan.Carry.Length); longest = Math.Max(longest, plan.Carry.Length);
                        Check(plan.Approach.Start == from && plan.Approach.End == plan.Carry.Start, "approach/carry join teleports");
                        Check(layout.Free(plan.Item.Id, plan.DropPoint, plan.Item.Cell, area), "occupied or offscreen drop");
                        for (int frame = 0; frame <= 60; frame++) {
                            Point p = Point.Round(plan.Carry.Position(frame / 60.0));
                            Check(area.Contains(new Rectangle(p, Renderer.WindowSize(size))), "carrying character leaves work area");
                            Check(area.Contains(new Rectangle(new Point(p.X + plan.HoldOffset.X, p.Y + plan.HoldOffset.Y), plan.Item.Cell)), "carried icon/label leaves work area");
                        }
                    }
            Check(count == 1260 && styles.Count == 3 && directions.Count == 16 && sides.Count == 2, "missing route style, direction, or facing");
            Check(longest - shortest > 400, "travel distances do not vary");
            DesktopLayout small = new DesktopLayout { Status = DesktopIconStatus.Available };
            DesktopItem tiny = new DesktopItem { Id = "small", Position = Point.Empty, Cell = new Size(72, 84) }; small.Items.Add(tiny);
            Check(PlayfulPlan.Create(small, tiny, Point.Empty, 224, new Rectangle(0, 0, 200, 200), random, MischiefKind.FinGrab) == null, "undersized screen accepted");
        }
        static void OwnershipAndInterruptions() {
            Check(WindowsDesktopIcons.ShellFailure(new System.Reflection.TargetInvocationException(new System.Runtime.InteropServices.COMException())), "wrapped Explorer activation failure must be treated as unavailable");
            Check(!WindowsDesktopIcons.ShellFailure(new System.Reflection.TargetInvocationException(new InvalidOperationException())), "unexpected implementation error must not be hidden");
            foreach (MischiefKind kind in Enum.GetValues(typeof(MischiefKind))) foreach (bool right in new[] { false, true }) {
                PlayfulDesktopFixture desktop = new PlayfulDesktopFixture(new Point(750, 450));
                desktop.Layout.Items.Add(new DesktopItem { Id = "fixture:untouched", Position = new Point(20, 20), Cell = new Size(72, 84) });
                Point before = desktop.Layout.Items[0].Position;
                using (PlayfulTrip trip = Trip(desktop, kind, right)) {
                    double now = Until(trip, MischiefStage.Giggle);
                    Check(trip.Committed && desktop.Layout.Items[0].Position == trip.Plan.DropPoint && desktop.Layout.Items[0].Position != before, "drop did not commit the original item");
                    Check(desktop.Moves > 20 && desktop.Layout.Items[1].Position == new Point(20, 20), "carry did not animate or touched a different icon");
                    Check(!trip.Advance(now + 3), "giggle never finishes");
                    foreach (Point point in desktop.Positions) Check(trip.Plan.Area.Contains(new Rectangle(point, trip.Plan.Item.Cell)), "temporary icon position outside screen");
                }
                Check(desktop.Layout.Items[0].Position != before, "completed drop was undone on disposal");
            }
            foreach (string interruption in new[] { "cancel", "user move", "deleted", "blocked", "occupied", "resize", "unavailable", "denied" }) {
                PlayfulDesktopFixture desktop = new PlayfulDesktopFixture(new Point(750, 450));
                desktop.Layout.Items.Add(new DesktopItem { Id = "adjacent grid neighbor", Position = new Point(750, 534), Cell = new Size(72, 84) });
                Point original = desktop.Layout.Items[0].Position, external = new Point(1100, 100);
                using (PlayfulTrip trip = Trip(desktop, MischiefKind.FinGrab)) {
                    double now = Until(trip, MischiefStage.Carry); trip.Advance(now + .3);
                    Check(desktop.Moves > 0, "interruption fixture did not pick up icon");
                    if (interruption == "user move") desktop.Layout.Items[0].Position = external;
                    if (interruption == "deleted") { desktop.Layout.Items.Clear(); desktop.Layout.Items.Add(new DesktopItem { Id = "replacement at the same index", Position = external, Cell = new Size(72, 84) }); }
                    if (interruption == "blocked") desktop.Layout.Status = DesktopIconStatus.AutoArrange;
                    if (interruption == "denied") desktop.DenyMoves = true;
                    if (interruption == "unavailable") desktop.Layout.Status = DesktopIconStatus.Unavailable;
                    if (interruption == "resize") desktop.Layout.Items[0].Cell = new Size(160, 160);
                    if (interruption == "occupied") {
                        desktop.Layout.Items.Add(new DesktopItem { Id = "new arrival", Position = trip.Plan.DropPoint, Cell = trip.Plan.Item.Cell });
                        now = Until(trip, MischiefStage.Drop, now + .35);
                    }
                    if (interruption != "cancel") Check(!trip.Advance(now + .7), "did not abort after " + interruption);
                }
                if (interruption == "cancel" || interruption == "occupied") Check(desktop.Layout.Items[0].Position == original, "unfinished move not restored: " + interruption);
                if (interruption == "user move" || interruption == "deleted") Check(desktop.Layout.Items[0].Position == external, "overwrote user's move or replacement icon");
            }
            PlayfulDesktopFixture snapped = new PlayfulDesktopFixture(new Point(750, 450)) { Snap = true };
            using (PlayfulTrip trip = Trip(snapped, MischiefKind.GentleBite)) {
                Until(trip, MischiefStage.Giggle);
                Check(trip.Committed && trip.FinalIconPosition.X % 24 == 0 && trip.FinalIconPosition.Y % 24 == 0, "existing desktop grid not respected");
            }
        }
        static long BodyFingerprint(Bitmap image) {
            long value = 19;
            unchecked { for (int y = 80; y < image.Height; y += 4) for (int x = 0; x < image.Width; x += 4) value = value * 31 + image.GetPixel(x, y).ToArgb(); }
            return value;
        }
        static void Rendering() {
            using (Atlas atlas = new Atlas()) foreach (int size in new[] { 144, 176, 224 }) foreach (MischiefKind kind in Enum.GetValues(typeof(MischiefKind))) foreach (bool right in new[] { false, true }) {
                PlayfulDesktopFixture desktop = new PlayfulDesktopFixture(new Point(750, 450));
                using (PlayfulTrip trip = Trip(desktop, kind, right, size)) {
                    HashSet<long> frames = new HashSet<long>(); HashSet<MischiefStage> stages = new HashSet<MischiefStage>();
                    for (double now = 0; now < 45; now += .1) {
                        if (!trip.Advance(now)) break;
                        if (trip.Stage == MischiefStage.Pickup || trip.Stage == MischiefStage.Drop) continue; // Icon can enter/leave the pet window during transfer.
                        if (((int)Math.Round(now * 10)) % 4 != 0) continue;
                        using (Bitmap frame = PlayfulRenderer.Draw(atlas, trip, now, size)) {
                            Check(frame.Size == Renderer.WindowSize(size), "wrong frame dimensions");
                            Check(frame.GetPixel(0, 0).A == 0 && frame.GetPixel(frame.Width - 1, frame.Height - 1).A == 0, "opaque window corners");
                            for (int x = 0; x < frame.Width; x++) Check(frame.GetPixel(x, frame.Height - 1).A == 0, "clipped body at bottom");
                            frames.Add(BodyFingerprint(frame)); stages.Add(trip.Stage);
                        }
                    }
                    Check(frames.Count > 10 && stages.Contains(MischiefStage.Carry) && stages.Contains(MischiefStage.Giggle), "static or incomplete animation");
                }
            }
        }
        public static int Run(string path) {
            try {
                string directory = Path.GetDirectoryName(Path.GetFullPath(path)); Directory.CreateDirectory(directory);
                WindowsDesktopIcons.VerifyViewInterop(); Settings(directory); Routes(); OwnershipAndInterruptions(); Rendering(); Companion.VerifyPlayfulBehavior(); Companion.VerifyPlayfulScheduling(); Companion.VerifyActivityQueue();
                File.WriteAllText(path,
                    "PASS: settings-only opt-in toggle; Save/Cancel, persistence, old settings compatibility, status controls, and Save and try now.\r\n" +
                    "PASS: 1260 routes across sizes, edge icons, portrait and negative-coordinate monitors; all 16 directions, three path styles, varied distances, both facings, and on-screen cargo.\r\n" +
                    "PASS: native COM desktop-view handoff without IOleWindow; exact interface failure stages and reference cleanup; actual controller pickup/carry/drop/giggle; stable icon identity; untouched neighbors; cancellation restoration; user moves, deletion, grid snapping, occupied destinations, changed icon size, and Explorer unavailability.\r\n" +
                    "PASS: both grab/bite variants in both directions at all three sizes; changing rendered pixels, transparent corners, and unclipped body.\r\n" +
                    "PASS: real companion timer deadlines, repeated trips, speech variants, menu exclusion, interaction priorities, disabled mode, pause independence, and bounded pending turns; independent idle/prank clocks, oldest-due-first ordering, three-second handoffs, two hours of mixed categories without starvation, manual overrides, mode changes, 30-second retries, blocked reasons, mouse scope, and last-icon fallback.\r\n");
                return 0;
            } catch (Exception e) { File.WriteAllText(path, "FAIL: " + e); return 1; }
        }
    }

    sealed partial class Companion {
        static int QueueActivity(Companion pet) {
            int count=(pet.feeding!=null?1:0)+(pet.reaction!=null?1:0)+(pet.playful!=null?1:0)+(pet.swimming?1:0);
            PlayfulTests.Check(count<=1,"automatic animations overlap");
            return pet.playful!=null?4:pet.feeding!=null?1:pet.reaction!=null?(pet.reaction.IsPlay?3:2):pet.swimming?5:0;
        }
        public static void VerifyActivityQueue() {
            Rectangle area=Screen.PrimaryScreen.WorkingArea;Point away=new Point(-10000,-10000);
            // Either clock can become due during the other animation. Its exact
            // original deadline must survive both start and completion of that turn.
            for(int order=0;order<2;order++)using(Companion pet=new Companion(true)) {
                PlayfulDesktopFixture desktop=new PlayfulDesktopFixture(new Point(area.Left+650,area.Top+450));
                pet.desktopFactory=desktop.Open;pet.prefs.PlayfulMode=true;pet.prefs.Roam=false;
                pet.Location=new Point(area.Left+300,area.Top+100);
                pet.nextPlayful=order==0?0:1;pet.nextIdleActivity=order==0?1:0;
                pet.Advance(0,away);
                int first=order==0?4:1,second=order==0?1:4;
                PlayfulTests.Check(QueueActivity(pet)==first,"oldest due clock did not go first");
                double limit=pet.Now+60;
                while(QueueActivity(pet)==first && pet.Now<limit) {
                    pet.Advance(pet.Now+.05,away);
                    PlayfulTests.Check((order==0?pet.nextIdleActivity:pet.nextPlayful)==1,"busy animation reset the other clock");
                }
                PlayfulTests.Check(QueueActivity(pet)==0,"first queued turn did not finish");
                double ended=pet.Now,ownNext=order==0?pet.nextPlayful:pet.nextIdleActivity;
                PlayfulTests.Check(ownNext-ended>=180 && ownNext-ended<=300,"completed turn did not restart only its own clock");
                pet.prefs.Roam=true;pet.nextSwim=ended;
                pet.Advance(ended+2.99,away);
                PlayfulTests.Check(QueueActivity(pet)==0,"roaming or queued animation invaded the three-second pause");
                pet.Advance(ended+3,away);
                PlayfulTests.Check(QueueActivity(pet)==second,"second queued turn did not start after three seconds");
                PlayfulTests.Check((order==0?pet.nextPlayful:pet.nextIdleActivity)==ownNext,"starting the second turn reset the first clock");
                limit=pet.Now+60;
                while(QueueActivity(pet)==second && pet.Now<limit)pet.Advance(pet.Now+.05,away);
                PlayfulTests.Check((order==0?pet.nextPlayful:pet.nextIdleActivity)==ownNext,"finishing the second turn reset the first clock");
            }
            using(Companion pet=new Companion(true)) {
                PlayfulDesktopFixture desktop=new PlayfulDesktopFixture(new Point(area.Left+650,area.Top+450));
                pet.desktopFactory=desktop.Open;pet.prefs.PlayfulMode=true;pet.prefs.Roam=false;
                pet.Location=new Point(area.Left+300,area.Top+100);
                pet.prefs.NextFeed=3;pet.prefs.NextPet=4;pet.prefs.NextPlay=2;pet.SchedulePlayful();
                int[] counts=new int[5];int previous=0;double lastEnd=Double.NegativeInfinity;
                // Let the real random 3-5 minute clocks run for two simulated hours.
                for(double now=0;now<=7200;now+=.25) {
                    double idleBefore=pet.nextIdleActivity,prankBefore=pet.nextPlayful;
                    pet.Advance(now,away);int active=QueueActivity(pet);
                    if(previous!=0 && active!=previous)lastEnd=now;
                    if(active!=0 && active!=previous) {
                        PlayfulTests.Check(previous==0 && now-lastEnd>=3,"mixed cycle skipped its handoff pause");
                        counts[active]++;
                        if(idleBefore<=now && prankBefore<=now)
                            PlayfulTests.Check((active==4)==(prankBefore<=idleBefore),"newer deadline overtook an older queued turn");
                    }
                    if(previous==4 || active==4)PlayfulTests.Check(pet.nextIdleActivity==idleBefore,"prank starved the idle cycle");
                    if(previous>0 && previous<4 || active>0 && active<4)PlayfulTests.Check(pet.nextPlayful==prankBefore,"regular animation reset the prank clock");
                    previous=active;
                }
                PlayfulTests.Check(counts[1]>=6 && counts[2]>=6 && counts[3]>=6 && counts[4]>=18,
                    "mixed cycle starved a category: "+String.Join(",",Array.ConvertAll(counts,delegate(int count){return count.ToString();})));
                PlayfulTests.Check(pet.prefs.NextFeed==3 && pet.prefs.NextPet==4 && pet.prefs.NextPlay==2,"automatic queue changed manual menu rotations");
            }
            using(Companion pet=new Companion(true)) {
                PlayfulDesktopFixture desktop=new PlayfulDesktopFixture(new Point(area.Left+650,area.Top+450));
                pet.desktopFactory=desktop.Open;pet.prefs.PlayfulMode=true;pet.prefs.Roam=false;
                pet.Location=new Point(area.Left+300,area.Top+100);
                // A blocked prank must not prevent an equally due regular turn.
                desktop.Layout.Status=DesktopIconStatus.AutoArrange;pet.nextPlayful=pet.nextIdleActivity=0;
                pet.Advance(0,away);
                PlayfulTests.Check(pet.feeding!=null && pet.playful==null && pet.nextPlayful==30,"blocked prank prevented the regular cycle from running");
                pet.CancelInteraction();desktop.Layout.Status=DesktopIconStatus.Available;
                pet.nextIdleActivity=pet.Now;pet.nextPlayful=pet.Now+1;double idleDue=pet.nextIdleActivity,prankDue=pet.nextPlayful;
                pet.Pet();double end=pet.reaction.Started+pet.reaction.Duration;
                pet.Advance(end,away);pet.Advance(end+2.99,away);
                PlayfulTests.Check(QueueActivity(pet)==0 && pet.nextIdleActivity==idleDue && pet.nextPlayful==prankDue,"manual action erased queued deadlines or skipped the pause");
                pet.Advance(end+3,away);
                PlayfulTests.Check(pet.reaction!=null && !pet.reaction.IsPlay && pet.playful==null,"manual action did not hand off to the oldest queued turn");
                pet.prefs.PlayfulMode=false;pet.ApplyPlayfulSettings(true,false);
                PlayfulTests.Check(Double.IsPositiveInfinity(pet.nextPlayful) && pet.nextIdleActivity==idleDue,"disabling pranks erased the idle queue");
                end=pet.reaction.Started+pet.reaction.Duration;pet.Advance(end,away);pet.Advance(end+3,away);
                PlayfulTests.Check(pet.playful==null && QueueActivity(pet)==0,"disabled queued prank still ran");
                // After a long suspension, at most one pending turn per clock is
                // retained, rather than replaying every missed 3-5 minute interval.
                pet.prefs.PlayfulMode=true;pet.ApplyPlayfulSettings(false,false);pet.Advance(pet.Now+36000,away);
                int started=QueueActivity(pet),transitions=0;double until=pet.Now+100;
                PlayfulTests.Check(started!=0,"long gap lost both pending turns");
                for(double now=pet.Now+.1;now<until;now+=.1) {
                    pet.Advance(now,away);int active=QueueActivity(pet);
                    if(active!=0 && active!=started)transitions++;
                    started=active;
                }
                PlayfulTests.Check(transitions==1,"long gap replayed a backlog or lost the second queued turn");
            }
        }

        public static void VerifyPlayfulBehavior() {
            Point away = new Point(-10000, -10000);
            using (Companion pet = new Companion(true)) {
                Rectangle area = Screen.PrimaryScreen.WorkingArea;
                PlayfulDesktopFixture desktop = new PlayfulDesktopFixture(new Point(area.Left + area.Width / 2, area.Top + area.Height / 2));
                pet.desktopFactory = desktop.Open; pet.prefs.Roam = false; pet.nextIdleActivity = Double.PositiveInfinity;
                pet.Location = new Point(area.Left + 300, area.Top + 100);
                foreach (ToolStripItem item in pet.menu.Items) PlayfulTests.Check(item.Text.IndexOf("Playful Mode", StringComparison.OrdinalIgnoreCase) < 0, "mode leaked into context/tray menu");
                pet.Advance(400, away); PlayfulTests.Check(pet.playful == null && desktop.Reads == 0, "disabled mode accessed desktop");
                pet.prefs.PlayfulMode = true; HashSet<int> waits = new HashSet<int>();
                for (int i = 0; i < 1000; i++) { pet.SchedulePlayful(); double wait = pet.nextPlayful - pet.Now; PlayfulTests.Check(wait >= 180 && wait <= 300, "timer outside 3-5 minutes"); waits.Add((int)wait); }
                PlayfulTests.Check(waits.Count > 80, "timer lacks variation");
                double due = pet.nextPlayful;
                pet.Advance(due - .001, away); PlayfulTests.Check(pet.playful == null, "early mischief");
                pet.down = true; pet.Advance(due, away); PlayfulTests.Check(pet.playful == null, "interrupted held click"); pet.down = false;
                pet.modal = true; pet.Advance(due + 1, away); PlayfulTests.Check(pet.playful == null, "interrupted settings"); pet.modal = false;
                pet.Advance(due + 1.01, away); PlayfulTests.Check(pet.playful != null && !pet.prefs.Roam, "paused roaming incorrectly prevents opted-in mischief");
                int feed = pet.prefs.NextFeed, pat = pet.prefs.NextPet, play = pet.prefs.NextPlay;
                HashSet<MischiefKind> kinds = new HashSet<MischiefKind>(); HashSet<string> sayings = new HashSet<string>();
                for (int cycle = 0; cycle < 20; cycle++) {
                    if (pet.playful == null) { pet.nextIdleActivity = Double.PositiveInfinity; pet.Advance(pet.nextPlayful, away); }
                    PlayfulTests.Check(pet.playful != null, "repeated trip failed to start");
                    PlayfulTrip trip = pet.playful; kinds.Add(trip.Plan.Kind); foreach (string line in trip.Lines) sayings.Add(line);
                    double start = pet.Now;
                    while (pet.playful != null && pet.Now - start < 60) pet.Advance(pet.Now + .05, away);
                    PlayfulTests.Check(pet.playful == null && trip.Committed, "real timer failed to complete trip");
                    PlayfulTests.Check(pet.nextPlayful - pet.Now >= 180 && pet.nextPlayful - pet.Now <= 300, "completion did not schedule a fresh wait");
                    PlayfulTests.Check(pet.faceRight == trip.Plan.FaceRight && pet.prefs.NextFeed == feed && pet.prefs.NextPet == pat && pet.prefs.NextPlay == play, "lost facing or changed manual cycles");
                }
                PlayfulTests.Check(kinds.Count == 2 && sayings.Count >= 16, "missing animation/speech variants");
                foreach (string action in new[] { "pet", "feed", "swim", "destination", "disable", "long gap", "controls" }) {
                    pet.CancelInteraction(); pet.swimming = false; pet.actionUntil = 0; pet.prefs.PlayfulMode = true;
                    pet.nextPlayful = pet.Now; pet.nextIdleActivity = Double.PositiveInfinity; pet.Advance(Math.Max(pet.Now + .01, pet.automaticReadyAt), away);
                    PlayfulTests.Check(pet.playful != null, "interruption fixture did not start");
                    PlayfulTrip trip = pet.playful; double start = pet.Now;
                    while (trip.Stage != MischiefStage.Carry && pet.Now - start < 30) pet.Advance(pet.Now + .05, away);
                    PlayfulTests.Check(trip.Stage == MischiefStage.Carry, "never picked up icon before interruption");
                    if (action == "pet") pet.Pet();
                    else if (action == "feed") pet.Feed();
                    else if (action == "swim") pet.BeginSwim(true);
                    else if (action == "destination") pet.ChooseDestination();
                    else if (action == "disable") { pet.prefs.PlayfulMode = false; pet.Advance(pet.Now + .05, away); }
                    else if (action == "long gap") pet.Advance(pet.Now + 3600, away);
                    else pet.CancelInteraction();
                    PlayfulTests.Check(pet.playful == null && desktop.Layout.Find(trip.Plan.Item.Id).Position == trip.Plan.Item.Position, "interruption did not restore icon: " + action);
                    PlayfulTests.Check(pet.nextPlayful > pet.Now, "interruption queued immediate mischief");
                }
                pet.CancelInteraction(); pet.swimming = false; pet.actionUntil = 0; pet.prefs.PlayfulMode = true;
                desktop.Layout.Status = DesktopIconStatus.AutoArrange; pet.nextPlayful = pet.Now;
                pet.Advance(Math.Max(pet.Now + .01, pet.automaticReadyAt), away);
                PlayfulTests.Check(pet.playful == null && pet.nextPlayful - pet.Now == 30, "auto-arranged desktop not skipped/retried in 30 seconds");
            }
        }

        public static void VerifyPlayfulScheduling() {
            Point away = new Point(-10000, -10000);
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            // Exercise each real animation category over the same deadline.
            for (int category = 0; category < 4; category++) using (Companion pet = new Companion(true)) {
                PlayfulDesktopFixture desktop = new PlayfulDesktopFixture(new Point(area.Left + 650, area.Top + 450));
                pet.desktopFactory = desktop.Open; pet.prefs.PlayfulMode = true; pet.prefs.Roam = category == 3;
                pet.Location = new Point(area.Left + 300, area.Top + 100); pet.nextIdleActivity = Double.PositiveInfinity;
                pet.nextPlayful = 1;
                if (category == 0) pet.Feed();
                else if (category == 1) pet.Pet();
                else if (category == 2) pet.Play();
                else pet.BeginSwim(false);
                double end = category == 0 ? pet.feeding.Started + pet.feeding.Duration : category < 3 ?
                    pet.reaction.Started + pet.reaction.Duration : pet.travelStart + pet.travelDuration;
                PlayfulTests.Check(pet.nextPlayful == 1 && end > 1, "animation reset the pending prank deadline");
                pet.Advance(1, away);
                PlayfulTests.Check(pet.playful == null, "prank interrupted another animation");
                pet.Advance(end, away); pet.Advance(end + 2.99, away);
                PlayfulTests.Check(pet.playful == null && pet.nextPlayful == 1, "due prank skipped the 3-second pause or lost its deadline");
                pet.Advance(end + 3, away);
                PlayfulTests.Check(pet.playful != null && pet.feeding == null && pet.reaction == null && !pet.swimming,
                    "due prank did not start after category " + category);
            }
            using (Companion pet = new Companion(true)) {
                PlayfulDesktopFixture desktop = new PlayfulDesktopFixture(new Point(area.Left + 650, area.Top + 450));
                pet.desktopFactory = desktop.Open; pet.prefs.PlayfulMode = true; pet.prefs.Roam = false;
                pet.Location = new Point(area.Left + 300, area.Top + 100); pet.nextIdleActivity = Double.PositiveInfinity;
                pet.nextPlayful = 240;
                for (int i = 0; i < 10; i++) { pet.simulatedTime += 10; pet.CancelInteraction(); pet.ApplyPlayfulSettings(true, false); }
                PlayfulTests.Check(pet.nextPlayful == 240, "opening controls or saving unchanged settings reset the countdown");
                string status = pet.GetPlayfulStatus();
                PlayfulTests.Check(status.Contains("140 seconds") && status.Contains("1 icon(s)"), "status did not explain the countdown and local icons");
                PlayfulTests.Check(desktop.Moves == 0 && pet.nextPlayful == 240, "read-only status moved icons or reset the clock");
                pet.prefs.PlayfulMode = false; pet.ApplyPlayfulSettings(true, false);
                PlayfulTests.Check(Double.IsPositiveInfinity(pet.nextPlayful), "disabling mode left an active deadline");
                pet.prefs.PlayfulMode = true; pet.ApplyPlayfulSettings(false, false);
                PlayfulTests.Check(pet.nextPlayful - pet.Now >= 180 && pet.nextPlayful - pet.Now <= 300, "enabling mode did not start a fresh wait");
                foreach (DesktopIconStatus blocked in new[] { DesktopIconStatus.AutoArrange, DesktopIconStatus.HiddenIcons, DesktopIconStatus.Unavailable }) {
                    desktop.Layout.Status = blocked; desktop.Layout.Detail = blocked == DesktopIconStatus.Unavailable ? "Shell step returned 0x80004005." : "";
                    pet.ApplyPlayfulSettings(true, true); pet.Advance(pet.nextPlayful, away);
                    PlayfulTests.Check(pet.playful == null && pet.nextPlayful - pet.Now == 30, "blocked manual attempt did not retry promptly");
                    PlayfulTests.Check(pet.playfulNote.Contains(PlayfulTrip.DesktopDescription(desktop.Layout)), "blocked reason was lost");
                }
                desktop.Layout.Status = DesktopIconStatus.Available; desktop.Layout.Detail = "";
                pet.Advance(pet.nextPlayful - .01, away); PlayfulTests.Check(pet.playful == null, "retry happened early");
                pet.nextIdleActivity = pet.nextPlayful; pet.Advance(pet.nextPlayful, away);
                PlayfulTests.Check(pet.playful != null && pet.feeding == null, "idle category stole the due prank's priority");
                PlayfulTests.Check(!pet.MouseInterruptsPlayful(away, MouseButtons.Left), "an unrelated click cancels the whole prank");
                PlayfulTests.Check(pet.MouseInterruptsPlayful(desktop.Layout.Items[0].Position, MouseButtons.Left), "clicking the selected icon did not interrupt");
                PlayfulTests.Check(!pet.MouseInterruptsPlayful(desktop.Layout.Items[0].Position, MouseButtons.None), "hovering interrupted a prank");
                pet.CancelPlayful();
                desktop.Layout.Items.Add(new DesktopItem { Id = "fixture:unreadable", Position = new Point(area.Left + 850, area.Top + 500), Cell = new Size(72, 84) });
                desktop.MissingPictures.Add("fixture:unreadable"); pet.lastPlayfulItem = "fixture:borrowed";
                PlayfulTests.Check(pet.BeginPlayful() && pet.playful.Plan.Item.Id == "fixture:borrowed", "last-icon preference prevented the only usable icon from being selected");
                pet.CancelPlayful(); desktop.MissingPictures.Add("fixture:borrowed");
                PlayfulTests.Check(!pet.BeginPlayful() && pet.playfulNote.Contains("artwork"), "missing icon artwork failed silently");
                desktop.Layout.Items.Clear();
                PlayfulTests.Check(!pet.BeginPlayful() && pet.playfulNote.Contains("No readable desktop icons"), "empty desktop failed silently");
                desktop.Layout.Items.Add(new DesktopItem { Id = "fixture:other-monitor", Position = new Point(area.Right + 500, area.Top + 500), Cell = new Size(72, 84) });
                PlayfulTests.Check(!pet.BeginPlayful() && pet.playfulNote.Contains("Mochi's monitor"), "empty current monitor failed silently");
            }
            using (Companion pet = new Companion(true)) {
                PlayfulDesktopFixture desktop = new PlayfulDesktopFixture(new Point(area.Left + 20, area.Top + 20));
                pet.desktopFactory = desktop.Open; pet.prefs.PlayfulMode = true; pet.prefs.Roam = false;
                pet.Location = Motion.Clamp(new Point(area.Right - pet.Width - 20, area.Bottom - pet.Height - 20), pet.Size, area);
                Point start = pet.Location;
                pet.nextIdleActivity = Double.PositiveInfinity; pet.nextPlayful = 0;
                pet.Advance(0, away);
                PlayfulTests.Check(pet.playful != null, "an icon at the opposite end of the monitor was excluded by distance");
                PlayfulTrip trip = pet.playful;
                for (int step = 0; step < 300 && trip.Stage == MischiefStage.Approach; step++) pet.Advance(pet.Now + .05, away);
                PlayfulTests.Check(pet.playful == trip && trip.Stage == MischiefStage.Pickup && pet.Location == trip.Plan.Approach.End && pet.Location != start && desktop.Moves == 0,
                    "Mochi must swim across the monitor to the icon before moving it");
            }
        }
    }

    static class PlayfulPreview {
        public static void Write(string directory) {
            Directory.CreateDirectory(directory);
            using (Atlas atlas = new Atlas()) foreach (MischiefKind kind in Enum.GetValues(typeof(MischiefKind))) foreach (bool right in new[] { false, true }) {
                string folder = Path.Combine(directory, kind + (right ? "-right" : "-left")); Directory.CreateDirectory(folder);
                Rectangle area = new Rectangle(0, 0, 900, 600);
                PlayfulDesktopFixture desktop = new PlayfulDesktopFixture(new Point(450, 350));
                using (PlayfulTrip trip = PlayfulTests.Trip(desktop, kind, right, 176, area))
                using (Bitmap icon = PlayfulDesktopFixture.Picture())
                using (Font font = new Font("Segoe UI", 12))
                using (Brush ink = new SolidBrush(Color.FromArgb(29, 64, 83))) {
                    for (int frame = 0; frame < 900; frame++) {
                        double now = frame / 20.0;
                        if (!trip.Advance(now)) break;
                        using (Bitmap page = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb))
                        using (Graphics g = Graphics.FromImage(page))
                        using (Bitmap pet = PlayfulRenderer.Draw(atlas, trip, now, 176)) {
                            g.Clear(Color.FromArgb(233, 244, 250));
                            g.DrawString((kind == MischiefKind.FinGrab ? "A little fin-grab" : "A gentle icon nibble") + "  |  Playful Mode", font, ink, 18, 16);
                            Point position = desktop.Layout.Items[0].Position;
                            g.DrawImageUnscaled(icon, position);
                            g.DrawString("Ideas", font, ink, position.X - 2, position.Y + 34);
                            g.DrawImageUnscaled(pet, trip.Position);
                            page.Save(Path.Combine(folder, frame.ToString("D3") + ".png"), ImageFormat.Png);
                        }
                    }
                }
            }
        }
    }
}
