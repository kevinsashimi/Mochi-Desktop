using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace MochiDesktop {
    enum MischiefKind { FinGrab, GentleBite }
    enum MischiefStage { Approach, Pickup, Carry, Drop, Giggle, Finished }

    sealed class PlayfulPlan {
        public DesktopItem Item;
        public MischiefKind Kind;
        public bool FaceRight;
        public Rectangle Area;
        public Point HoldOffset, DropPoint;
        public SwimPath Approach, Carry;
        public double ApproachDuration, CarryDuration;
        public const int DropHeight = 18;

        public static Point Attachment(int size, MischiefKind kind, bool right) {
            Rectangle body = Renderer.SpriteRect(size);
            int x = body.Left + (int)Math.Round(size * (kind == MischiefKind.FinGrab ? .34 : .27));
            if (right) x = Renderer.WindowSize(size).Width - x;
            return new Point(x - 16, body.Top + (int)Math.Round(body.Height * (kind == MischiefKind.FinGrab ? .75 : .63)) - 16);
        }

        public static PlayfulPlan Create(DesktopLayout layout, DesktopItem item, Point from, int size, Rectangle area, Random random, MischiefKind kind) {
            Size window = Renderer.WindowSize(size);
            if (!area.Contains(new Rectangle(item.Position, item.Cell)) || !area.Contains(new Rectangle(from, window))) return null;
            for (int attempt = 0; attempt < 80; attempt++) {
                double angle = random.NextDouble() * Math.PI * 2;
                double distance = 120 + random.NextDouble() * Math.Min(850, Math.Max(area.Width, area.Height) * .7);
                bool right = Math.Cos(angle) >= 0;
                Point offset = Attachment(size, kind, right);
                Size footprint = new Size(Math.Max(window.Width, offset.X + item.Cell.Width), Math.Max(window.Height, offset.Y + item.Cell.Height + DropHeight));
                if (footprint.Width > area.Width || footprint.Height > area.Height) return null;
                Point pickup = Motion.Clamp(new Point(item.Position.X - offset.X, item.Position.Y - offset.Y), footprint, area);
                Point end = new Point(pickup.X + (int)Math.Round(Math.Cos(angle) * distance), pickup.Y + (int)Math.Round(Math.Sin(angle) * distance));
                if (!area.Contains(new Rectangle(end, footprint))) continue;
                Point drop = new Point(end.X + offset.X, end.Y + offset.Y + DropHeight);
                if (!layout.Free(item.Id, drop, item.Cell, area) || Distance(drop, item.Position) < 96) continue;
                SwimPath approach = new SwimPath(from, window, area, random, false, pickup);
                SwimPath carry = new SwimPath(pickup, footprint, area, random, false, end);
                return new PlayfulPlan {
                    Item = item, Kind = kind, FaceRight = right, Area = area, HoldOffset = offset, DropPoint = drop,
                    Approach = approach, Carry = carry,
                    ApproachDuration = Math.Max(.5, Math.Min(12, approach.Length / 130 + .6)),
                    CarryDuration = Math.Max(3.2, Math.Min(15, carry.Length / 85 + 1))
                };
            }
            return null;
        }
        public static double Distance(Point a, Point b) { double x = (double)a.X - b.X, y = (double)a.Y - b.Y; return Math.Sqrt(x * x + y * y); }
    }

    sealed class PlayfulTrip : IDisposable {
        public readonly PlayfulPlan Plan;
        public readonly Bitmap Icon;
        public readonly string[] Lines;
        public readonly int Giggle;
        public MischiefStage Stage { get; private set; }
        public Point Position { get; private set; }
        public Point IconPosition { get; private set; }
        public double Started { get; private set; }
        public bool Committed { get; private set; }
        public Point FinalIconPosition { get; private set; }
        public string StopReason { get; private set; }
        public Rectangle ActualIconBounds { get { return new Rectangle(lastActual, Plan.Item.Cell); } }
        readonly IDesktopIcons desktop;
        Point lastActual;
        double nextMove, nextValidation;
        bool touched, disposed;
        public PlayfulTrip(PlayfulPlan plan, IDesktopIcons icons, Bitmap picture, double now, string[] lines, int giggle) {
            Plan = plan; desktop = icons; Icon = picture; Lines = lines; Giggle = giggle;
            Stage = MischiefStage.Approach; Started = now; Position = plan.Approach.Start;
            lastActual = IconPosition = plan.Item.Position;
        }
        public string Speech {
            get { return Stage == MischiefStage.Approach || Stage == MischiefStage.Pickup ? Lines[0] : Stage == MischiefStage.Carry ? Lines[1] : Stage == MischiefStage.Drop ? Lines[2] : Lines[3]; }
        }
        void Enter(MischiefStage stage, double now) { Stage = stage; Started = now; nextMove = 0; }
        bool Stop(string reason) { StopReason = reason; return false; }
        bool Move(Point destination, double now, bool final) {
            IconPosition = destination;
            if (!final && now < nextMove) return true;
            nextMove = now + .05; // Limit Explorer calls to 20 Hz; the character still redraws at 60 Hz.
            Point actual;
            if (!desktop.TryMove(Plan.Item.Id, lastActual, destination, out actual))
                return Stop("Windows could not move the selected icon. " + desktop.Problem);
            touched |= actual != Plan.Item.Position; lastActual = actual;
            // Explorer may snap to its existing grid. Keep that setting and validate the returned position.
            return Plan.Area.Contains(new Rectangle(actual, Plan.Item.Cell)) || Stop("Windows placed the icon outside the planned screen area.");
        }
        bool StillOurs() {
            DesktopLayout current = desktop.Read();
            DesktopItem item = current.Find(Plan.Item.Id);
            if (current.Status != DesktopIconStatus.Available) return Stop(DesktopDescription(current));
            return item != null && item.Position == lastActual && item.Cell == Plan.Item.Cell || Stop("The selected icon was moved, removed, or resized during the trip.");
        }
        internal static string DesktopDescription(DesktopLayout layout) {
            if (!String.IsNullOrEmpty(layout.Detail)) return layout.Detail;
            if (layout.Status == DesktopIconStatus.AutoArrange) return "Turn off Desktop > View > Auto arrange icons.";
            if (layout.Status == DesktopIconStatus.HiddenIcons) return "Turn on Desktop > View > Show desktop icons.";
            return "Windows Explorer's desktop icon view is unavailable.";
        }
        public bool Advance(double now) {
            double elapsed = Math.Max(0, now - Started);
            if (Stage < MischiefStage.Giggle && now >= nextValidation) {
                nextValidation = now + .3;
                if (!StillOurs()) return false;
            }
            if (Stage == MischiefStage.Approach) {
                Position = Point.Round(Plan.Approach.Position(elapsed / Plan.ApproachDuration));
                if (elapsed >= Plan.ApproachDuration) { Position = Plan.Approach.End; Enter(MischiefStage.Pickup, now); }
            } else if (Stage == MischiefStage.Pickup) {
                Point hold = new Point(Position.X + Plan.HoldOffset.X, Position.Y + Plan.HoldOffset.Y);
                if (!Move(Motion.Travel(Plan.Item.Position, hold, elapsed / 1.1), now, elapsed >= 1.1)) return false;
                if (elapsed >= 1.1) Enter(MischiefStage.Carry, now);
            } else if (Stage == MischiefStage.Carry) {
                Position = Point.Round(Plan.Carry.Position(elapsed / Plan.CarryDuration));
                if (!Move(new Point(Position.X + Plan.HoldOffset.X, Position.Y + Plan.HoldOffset.Y), now, elapsed >= Plan.CarryDuration)) return false;
                if (elapsed >= Plan.CarryDuration) { Position = Plan.Carry.End; Enter(MischiefStage.Drop, now); }
            } else if (Stage == MischiefStage.Drop) {
                DesktopLayout current = desktop.Read();
                if (current.Status != DesktopIconStatus.Available) return Stop(DesktopDescription(current));
                if (!current.Free(Plan.Item.Id, Plan.DropPoint, Plan.Item.Cell, Plan.Area)) return Stop("The planned drop spot is now occupied.");
                Point hold = new Point(Position.X + Plan.HoldOffset.X, Position.Y + Plan.HoldOffset.Y);
                if (!Move(Motion.Travel(hold, Plan.DropPoint, elapsed / .65), now, elapsed >= .65)) return false;
                if (elapsed >= .65) {
                    // Check the actual snapped drop cell against a fresh layout, not the initial snapshot.
                    current = desktop.Read();
                    if (current.Status != DesktopIconStatus.Available) return Stop(DesktopDescription(current));
                    if (!current.Free(Plan.Item.Id, lastActual, Plan.Item.Cell, Plan.Area)) return Stop("Windows snapped the drop onto an occupied icon cell.");
                    Committed = true; FinalIconPosition = lastActual; Enter(MischiefStage.Giggle, now);
                }
            } else if (Stage == MischiefStage.Giggle && elapsed >= 2.8) { Stage = MischiefStage.Finished; return false; }
            return Stage != MischiefStage.Finished;
        }
        public void Dispose() {
            if (disposed) return; disposed = true;
            try {
                if (touched && !Committed) {
                    DesktopLayout current = desktop.Read();
                    DesktopItem item = current.Find(Plan.Item.Id);
                    // Restore only our own unfinished move, and never overwrite a user's new arrangement.
                    if (current.Status == DesktopIconStatus.Available && item != null && item.Position == lastActual && item.Cell == Plan.Item.Cell &&
                        current.Free(Plan.Item.Id, Plan.Item.Position, Plan.Item.Cell, Plan.Area)) {
                        Point ignored; desktop.TryMove(Plan.Item.Id, lastActual, Plan.Item.Position, out ignored);
                    }
                }
            } finally { if (Icon != null) Icon.Dispose(); desktop.Dispose(); }
        }
    }

    static class PlayfulRenderer {
        public static Bitmap Draw(Atlas atlas, PlayfulTrip trip, double now, int size) {
            double time = Math.Max(0, now - trip.Started);
            PlayfulPlan plan = trip.Plan;
            if (trip.Stage == MischiefStage.Approach) {
                PointF heading = plan.Approach.Heading(time / plan.ApproachDuration);
                int direction = Motion.Direction(heading.X, heading.Y);
                return GazeRenderer.Draw(atlas, new GazePose { Direction = direction, Time = time }, size, trip.Speech, true);
            }
            Size window = Renderer.WindowSize(size); Rectangle body = Renderer.SpriteRect(size);
            Bitmap result = new Bitmap(window.Width, window.Height, PixelFormat.Format32bppArgb);
            bool holding = trip.Stage == MischiefStage.Pickup || trip.Stage == MischiefStage.Carry || trip.Stage == MischiefStage.Drop;
            int frame = trip.Stage == MischiefStage.Pickup ? Math.Min(3, (int)(time / .25)) : trip.Stage == MischiefStage.Drop ? Math.Min(7, 4 + (int)(time / .18)) : 3 + ((int)(time / .6) % 2);
            Bitmap sprite = plan.Kind == MischiefKind.FinGrab ? atlas.Illustrated[1, frame] : atlas.Feeding[0, trip.Stage == MischiefStage.Pickup && time < .65 ? 1 : trip.Stage == MischiefStage.Drop ? 3 : 2];
            if (!holding) sprite = trip.Giggle == 0 ? atlas.Feeding[0, 4 + (int)(time / .14) % 4] : atlas.Illustrated[0, 2 + (int)(time / .3) % 4];
            float bob = holding ? (float)Math.Sin(time * 4) * 1.2f : (float)(-Math.Abs(Math.Sin(time * 10)) * 3);
            float tilt = holding ? (float)Math.Sin(time * 3) * 1.2f : (float)Math.Sin(time * 12) * 2;
            using (Graphics g = Graphics.FromImage(result)) {
                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                DrawBody(g, sprite, body, window, plan.FaceRight, bob, tilt, false, plan.Kind);
                // Carry the original, upright icon; mirroring Mochi never mirrors its artwork.
                if (holding && trip.Icon != null) {
                    g.DrawImage(trip.Icon, new Rectangle(trip.IconPosition.X - trip.Position.X, trip.IconPosition.Y - trip.Position.Y, 32, 32));
                    // Foreground fins/lip from the same pose wrap around the cargo. These
                    // small occlusion masks are inside the body, not a second moving pose.
                    if (trip.Stage != MischiefStage.Pickup || time >= .75)
                        DrawBody(g, sprite, body, window, plan.FaceRight, bob, tilt, true, plan.Kind);
                }
                if (!holding) {
                    using (Pen ink = new Pen(Color.FromArgb(180, 163, 121, 194), 2))
                        for (int i = 0; i < 3; i++) {
                            float x = window.Width / 2f + (i - 1) * 18, y = 97 + (float)Math.Sin(time * 5 + i) * 4;
                            g.DrawLine(ink, x, y, x + (i - 1) * 3, y - 6);
                        }
                }
                Renderer.DrawBubble(g, window, trip.Speech);
            }
            return result;
        }
        static void DrawBody(Graphics g, Bitmap sprite, Rectangle body, Size window, bool right, float bob, float tilt, bool foreground, MischiefKind kind) {
            GraphicsState state = g.Save();
            if (right) { g.TranslateTransform(window.Width, 0); g.ScaleTransform(-1, 1); }
            g.TranslateTransform(body.Left + body.Width / 2f, body.Top + body.Height / 2f + bob);
            g.RotateTransform(tilt); g.ScaleTransform(body.Width / 192f, body.Height / 208f); g.TranslateTransform(-96, -104);
            if (foreground) using (GraphicsPath mask = new GraphicsPath()) {
                if (kind == MischiefKind.FinGrab) { mask.AddEllipse(26, 133, 29, 20); mask.AddEllipse(73, 136, 33, 22); }
                else mask.AddEllipse(35, 117, 36, 7);
                g.SetClip(mask, CombineMode.Intersect); g.DrawImageUnscaled(sprite, 0, 0);
            } else g.DrawImageUnscaled(sprite, 0, 0);
            g.Restore(state);
        }
    }

    sealed partial class Companion {
        static readonly string[] GrabLines = { "Psst... this one\nneeds a little holiday!", "Tiny fins.\nBig borrowing plans.", "I'll just grab this...\nvery innocently.", "A little flipper\nfive-finger discount!" };
        static readonly string[] BiteLines = { "Just a gentle nibble.\nNot a real snack!", "Nom! This icon\nis coming with me.", "I promise I won't\neat your homework!", "Open wide...\nfor a tiny adventure!" };
        static readonly string[] CarryLines = { "Nothing to sea here!", "Your icon booked\na shark taxi.", "Borrowing this!\nHee hee...", "Operation: sneaky\nlittle swim!", "Taking the scenic\nshortcut. Literally." };
        static readonly string[] MischiefDropLines = { "Special delivery!", "Boop! New parking.", "This looks like\na very good spot.", "One tiny desktop\nmakeover!" };
        static readonly string[] GiggleLines = { "Hee hee!\nWho moved that?", "I'm an interior\nde-fin-er!", "Oops. My fins\nslipped. Twice.", "You saw nothing.\nEspecially my grin.", "A little chaos.\nAs a treat!", "Same icon.\nMore adventure!" };
        int lastMischiefPickup = -1, lastMischiefCarry = -1, lastMischiefDrop = -1, lastMischiefGiggle = -1;
        double nextPlayful, playfulLastTick;
        string lastPlayfulItem;
        string playfulNote = "No prank attempted yet.";
        bool playfulRequested;
        PlayfulTrip playful;
        // A fake implementation is injected by timer tests; production always uses Explorer.
        Func<IDesktopIcons> desktopFactory = delegate { return new WindowsDesktopIcons(); };

        void SchedulePlayful() { nextPlayful = prefs.PlayfulMode ? Now + random.Next(180, 301) : Double.PositiveInfinity; }
        void ApplyPlayfulSettings(bool wasEnabled, bool tryNow) {
            if (wasEnabled != prefs.PlayfulMode) SchedulePlayful();
            if (tryNow && prefs.PlayfulMode) { nextPlayful = Now + 1; automaticReadyAt = nextPlayful; playfulRequested = true; actionUntil = Now; }
            else if (!prefs.PlayfulMode) playfulRequested = false;
        }
        void RecordPlayfulStatus(string message) {
            playfulNote = message;
            WritePlayfulStatus("Last result: " + message);
        }
        void WritePlayfulStatus(string message) {
            if (simulation) return;
            try { File.WriteAllText(Path.Combine(Program.DataDirectory, "playful-status.txt"),
                "Mochi " + typeof(Companion).Assembly.GetName().Version + "\r\n" + DateTimeOffset.Now.ToString("o") + "\r\n" + message + "\r\n"); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        string GetPlayfulStatus() {
            string wait = !prefs.PlayfulMode ? "Off. Enable Playful Mode and Save to start the clock." :
                playful != null ? "A prank is in progress." : Now>=nextPlayful ?
                "Queued. Waiting for earlier animations and the 3-second pause." :
                "Next attempt in " + Math.Ceiling(nextPlayful - Now) + " seconds (after current controls or animation finish).";
            string regular = idleActivityActive ? "Playing an automatic " + (feeding!=null ? "feeding" : reaction.IsPlay ? "play" : "petting") + " animation." :
                Now>=nextIdleActivity ? "Queued; keeps its place until Mochi is ready." : "Next turn in " + Math.Ceiling(nextIdleActivity-Now) + " seconds.";
            string current;
            using (IDesktopIcons desktop = desktopFactory()) {
                DesktopLayout layout = desktop.Read();
                Rectangle area = Screen.FromRectangle(Bounds).WorkingArea;
                int local = layout.Items.FindAll(delegate(DesktopItem item) { return area.Contains(new Rectangle(item.Position, item.Cell)); }).Count;
                current = layout.Status != DesktopIconStatus.Available ? PlayfulTrip.DesktopDescription(layout) :
                    layout.Items.Count == 0 ? "No readable desktop icons were found." :
                    local == 0 ? "There are no fully visible icons on Mochi's monitor. Move Mochi to a monitor with desktop icons." :
                    local + " icon(s) on Mochi's monitor; " + layout.Items.Count + " across the desktop. A free drop spot and readable artwork are also needed.";
            }
            string report = "Playful Mode: " + wait + "\r\n\r\nFeeding / petting / play: " + regular +
                "\r\nThe two clocks run independently; due animations take turns with a 3-second pause." +
                "\r\n\r\nDesktop check: " + current + "\r\n\r\nLast result: " + playfulNote;
            WritePlayfulStatus(report); return report;
        }
        bool CannotStartPlayful(string reason, bool requested) {
            RecordPlayfulStatus(reason + " Will check again in 30 seconds.");
            if (requested && !simulation) {
                modal = true;
                try { MessageBox.Show(reason + "\n\nMochi will check again in 30 seconds. Settings > Playful status shows the latest result.",
                    "Mochi needs a little help", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                finally { modal = false; }
            }
            return false;
        }
        void CancelPlayful(string reason = null) {
            if (playful == null) return;
            PlayfulTrip trip = playful; playful = null;
            trip.Dispose(); bubble = ""; bubbleUntil = 0; row = frame = 0; actionStart = Now; actionUntil = 0;
            RecordPlayfulStatus(reason ?? (trip.Committed ? "Prank completed; the icon was dropped at its new position." : "Interrupted by Mochi's controls; an unfinished move is restored when possible."));
            SchedulePlayful(); Schedule(); PauseAutomaticActivities(); edgeWatch.Reset(Now);
        }
        bool BeginPlayful() {
            // A skipped attempt is not a prank: recheck promptly instead of silently
            // making the user wait another full 3-5 minutes after fixing the desktop.
            nextPlayful = Now + 30;
            bool requested = playfulRequested; playfulRequested = false;
            IDesktopIcons desktop = desktopFactory();
            bool keep = false;
            try {
                DesktopLayout layout = desktop.Read();
                if (layout.Status != DesktopIconStatus.Available) return CannotStartPlayful(PlayfulTrip.DesktopDescription(layout), requested);
                Rectangle area = Screen.FromRectangle(Bounds).WorkingArea;
                List<DesktopItem> choices = layout.Items.FindAll(delegate(DesktopItem item) { return area.Contains(new Rectangle(item.Position, item.Cell)); });
                if (choices.Count == 0) return CannotStartPlayful(layout.Items.Count == 0 ? "No readable desktop icons were found." :
                    "There are no fully visible icons on Mochi's monitor. Move Mochi to a monitor with desktop icons.", requested);
                int start = random.Next(choices.Count);
                bool pictureFailed = false; string pictureProblem = "";
                for (int n = 0; n < choices.Count * 2; n++) {
                    DesktopItem item = choices[(start + n) % choices.Count];
                    // Prefer a different icon, but allow the previous one if it is
                    // the only item with a usable route or picture.
                    if (n < choices.Count && choices.Count > 1 && item.Id == lastPlayfulItem) continue;
                    if (n >= choices.Count && item.Id != lastPlayfulItem) continue;
                    MischiefKind kind = (MischiefKind)random.Next(2);
                    PlayfulPlan plan = PlayfulPlan.Create(layout, item, Location, prefs.Size, area, random, kind);
                    if (plan == null) continue;
                    Bitmap picture = desktop.Picture(item.Id);
                    if (picture == null) { pictureFailed = true; pictureProblem = desktop.Problem; continue; }
                    string[] lines = { PickLine(kind == MischiefKind.FinGrab ? GrabLines : BiteLines, ref lastMischiefPickup), PickLine(CarryLines, ref lastMischiefCarry), PickLine(MischiefDropLines, ref lastMischiefDrop), PickLine(GiggleLines, ref lastMischiefGiggle) };
                    playful = new PlayfulTrip(plan, desktop, picture, Now, lines, random.Next(2)); keep = true;
                    playfulLastTick = Now; lastPlayfulItem = item.Id; gaze.Reset(); swimming = guidedSwim = false;
                    startupHintAt = -1; bubble = ""; edgeWatch.Reset(Now);
                    RecordPlayfulStatus("Prank started: " + (kind == MischiefKind.FinGrab ? "fin-grab." : "gentle bite.")); Render(); return true;
                }
                return CannotStartPlayful(pictureFailed ? "The selected icon artwork could not be loaded. " + pictureProblem :
                    "No free drop spot and on-screen route could be found. Try a clearer area or a smaller Mochi.", requested);
            } finally { if (!keep) desktop.Dispose(); }
        }
        bool AdvancePlayful(double now) {
            if (playful == null) return false;
            if (!prefs.PlayfulMode) { CancelPlayful("Playful Mode was turned off."); return true; }
            if (now - playfulLastTick > 2) { CancelPlayful("The app paused for more than two seconds; the trip was cancelled."); return true; }
            if (!simulation && MouseInterruptsPlayful(Cursor.Position, Control.MouseButtons)) { CancelPlayful("Mochi or the selected icon was clicked during the trip."); return true; }
            playfulLastTick = now;
            bool active = playful.Advance(now);
            Location = playful.Position; Face(playful.Plan.FaceRight);
            if (!active) { CancelPlayful(playful.StopReason); Save(); }
            Render(); return true;
        }
        bool MouseInterruptsPlayful(Point cursor, MouseButtons buttons) {
            return playful != null && buttons != MouseButtons.None && (Bounds.Contains(cursor) || playful.ActualIconBounds.Contains(cursor));
        }
    }
}
