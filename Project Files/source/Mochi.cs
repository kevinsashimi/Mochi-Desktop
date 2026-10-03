using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Collections.Generic;

namespace MochiDesktop {
    static class Program {
        public static bool TestUi;
        public static EventWaitHandle SettingsRequest;
        public static string DataDirectory {
            get {
                string projectFiles=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Project Files");
                return Directory.Exists(projectFiles)?projectFiles:AppDomain.CurrentDomain.BaseDirectory;
            }
        }
        [STAThread] static int Main(string[] args) {
            if (args.Length == 2 && args[0] == "--apply-update") return UpdateInstaller.RunHelper(args[1]);
            try {
                Native.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                if (args.Length == 2 && args[0] == "--update-self-test") return UpdateTests.Run(args[1]);
                if (args.Length == 2 && args[0] == "--playful-self-test") return PlayfulTests.Run(args[1]);
                if (args.Length == 2 && args[0] == "--playful-preview") { PlayfulPreview.Write(args[1]); return 0; }
                if (args.Length == 2 && args[0] == "--desktop-icons-status") {
                    using (IDesktopIcons icons = new WindowsDesktopIcons()) {
                        DesktopLayout layout = icons.Read();
                        File.WriteAllText(args[1], "Mochi " + typeof(Companion).Assembly.GetName().Version + "\r\n" +
                            layout.Status + ": " + layout.Items.Count + " desktop icons. No positions changed.\r\n" + layout.Detail);
                    }
                    return 0;
                }
                if (args.Length >= 2 && args[0] == "--self-test") return Tests.Run(args[1]);
                if (args.Length >= 2 && args[0] == "--feed-preview") {FeedingPreview.Write(args[1]);return 0;}
                if (args.Length >= 2 && args[0] == "--gaze-preview") {GazePreview.Write(args[1]);return 0;}
                if (args.Length >= 2 && args[0] == "--reaction-preview") {ReactionPreview.Write(args[1]);return 0;}
                if (args.Length >= 2 && args[0] == "--smooth-preview") {SmoothPreview.Write(args[1]);return 0;}
                if (args.Length >= 2 && args[0] == "--facing-preview") {FacingPreview.Write(args[1]);return 0;}
                if (args.Length >= 2 && args[0] == "--preview") {
                    using (Atlas atlas = new Atlas()) using(Bitmap b = Renderer.Draw(atlas,0,0,192,"Hello! I'm Mochi.")) b.Save(args[1], ImageFormat.Png);
                    return 0;
                }
                TestUi=Array.IndexOf(args,"--test-ui")>=0;
                bool openSettings=Array.IndexOf(args,"--settings")>=0;
                bool first;
                using (Mutex mutex = new Mutex(true, "Local\\MochiDesktopCompanion", out first)) {
                    if (!first) { using(EventWaitHandle request=EventWaitHandle.OpenExisting("Local\\MochiDesktopSettings"))request.Set(); return 0; }
                    using(SettingsRequest=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\MochiDesktopSettings")) {
                        if(args.Length == 2 && args[0] == "--cleanup-update") UpdateInstaller.CleanupAfterRestart(args[1]);
                        if(openSettings)SettingsRequest.Set();
                        Application.Run(new Companion());
                    }
                    mutex.ReleaseMutex();
                }
                return 0;
            } catch(Exception ex) {
                try { File.WriteAllText(Path.Combine(DataDirectory,"error.log"), ex.ToString()); } catch {}
                MessageBox.Show("Mochi couldn't start: " + ex.Message, "Mochi Desktop", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }

    sealed class Preferences {
        public int Size = 176, Frequency = 1, X = int.MinValue, Y = int.MinValue, NextFeed, NextPet, NextPlay;
        public bool Roam = true, PlayfulMode;
        static string FilePath { get { return Path.Combine(Program.DataDirectory,"settings.xml"); } }
        public static Preferences Load(string filePath=null) {
            Preferences p = new Preferences();
            try {
                XElement x = XElement.Load(filePath??FilePath);
                p.Size = (int?)x.Element("Size") ?? 176;
                p.Frequency = (int?)x.Element("Frequency") ?? 1;
                p.X = (int?)x.Element("X") ?? int.MinValue;
                p.Y = (int?)x.Element("Y") ?? int.MinValue;
                p.Roam = (bool?)x.Element("Roam") ?? true;
                p.PlayfulMode = (bool?)x.Element("PlayfulMode") ?? false;
                p.NextFeed = new FeedRotation((int?)x.Element("NextFeed") ?? 0).Next;
                p.NextPet = new ReactionRotation((int?)x.Element("NextPet") ?? 0,false).Next;
                p.NextPlay = new ReactionRotation((int?)x.Element("NextPlay") ?? 0,true).Next;
                if(p.Size!=144 && p.Size!=176 && p.Size!=224) p.Size=176;
                p.Frequency=Math.Max(0,Math.Min(2,p.Frequency));
            } catch {}
            return p;
        }
        public void Save(string filePath=null) {
            try {
                string target=filePath??FilePath;
                XElement x = new XElement("Mochi",new XElement("Size",Size),new XElement("Frequency",Frequency),
                    new XElement("X",X),new XElement("Y",Y),new XElement("Roam",Roam),new XElement("PlayfulMode",PlayfulMode),new XElement("NextFeed",NextFeed),new XElement("NextPet",NextPet),new XElement("NextPlay",NextPlay));
                x.Save(target + ".tmp");
                if(File.Exists(target)) File.Replace(target+".tmp",target,null); else File.Move(target+".tmp",target);
            } catch { /* A read-only portable folder must not prevent quitting. */ }
        }
    }

    static class Startup {
        public static string Shortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"Mochi Desktop.lnk"); } }
        public static bool Enabled { get { return File.Exists(Shortcut); } }
        public static void SetEnabled(bool enabled) {
            if(!enabled) { if(File.Exists(Shortcut)) File.Delete(Shortcut); return; }
            object shell = null, link = null;
            try {
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                link=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{Shortcut});
                Set(link,"TargetPath",Application.ExecutablePath);
                Set(link,"WorkingDirectory",AppDomain.CurrentDomain.BaseDirectory);
                Set(link,"Description","Mochi, your little whale shark companion");
                Set(link,"IconLocation",Application.ExecutablePath+",0");
                link.GetType().InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
            } finally {
                if(link!=null) Marshal.FinalReleaseComObject(link);
                if(shell!=null) Marshal.FinalReleaseComObject(shell);
            }
        }
        static void Set(object o,string key,object value) { o.GetType().InvokeMember(key,BindingFlags.SetProperty,null,o,new object[]{value}); }
    }

    sealed class Atlas : IDisposable {
        public readonly Bitmap[,] Frames = new Bitmap[11,8];
        public readonly Bitmap[,] Feeding = new Bitmap[3,8];
        public readonly Bitmap[,] Illustrated = new Bitmap[4,8];
        public readonly Bitmap Snack;
        readonly Dictionary<Bitmap,Rectangle> opaqueBounds=new Dictionary<Bitmap,Rectangle>();
        public static readonly int[][] Durations = {
            new[]{280,110,110,140,140,320},new[]{120,120,120,120,120,120,120,220},new[]{120,120,120,120,120,120,120,220},
            new[]{140,140,140,280},new[]{140,140,140,140,280},new[]{140,140,140,140,140,140,140,240},
            new[]{150,150,150,150,150,260},new[]{120,120,120,120,120,220},new[]{150,150,150,150,150,280}
        };
        public Atlas() {
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Mochi.Atlas"))
            using(Bitmap source=new Bitmap(stream)) {
                if(source.Width!=1536 || source.Height!=2288) throw new InvalidDataException("Unexpected sprite sheet dimensions.");
                for(int r=0;r<11;r++) for(int c=0;c<8;c++) Frames[r,c]=source.Clone(new Rectangle(c*192,r*208,192,208),PixelFormat.Format32bppArgb);
            }
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Mochi.Feeding"))
            using(Bitmap source=new Bitmap(stream)){
                if(source.Width!=1536||source.Height!=624)throw new InvalidDataException("Unexpected feeding sheet dimensions.");
                for(int r=0;r<3;r++)for(int c=0;c<8;c++)Feeding[r,c]=source.Clone(new Rectangle(c*192,r*208,192,208),PixelFormat.Format32bppArgb);
            }
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Mochi.Snack"))using(Bitmap source=new Bitmap(stream))Snack=new Bitmap(source);
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Mochi.Illustrated"))using(Bitmap source=new Bitmap(stream)){
                if(source.Width!=1536 || source.Height!=832)throw new InvalidDataException("Unexpected illustrated interaction sheet dimensions.");
                for(int r=0;r<4;r++)for(int c=0;c<8;c++)Illustrated[r,c]=source.Clone(new Rectangle(c*192,r*208,192,208),PixelFormat.Format32bppArgb);
            }
        }
        public static int FrameAt(int row,double ms) {
            int[] d=Durations[row]; int sum=0; foreach(int x in d) sum+=x;
            double phase=Math.Max(0,ms)%sum;
            for(int i=0;i<d.Length;i++){if(phase<d[i])return i; phase-=d[i];} return 0;
        }
        public Rectangle OpaqueBounds(Bitmap bitmap){
            Rectangle cached;if(opaqueBounds.TryGetValue(bitmap,out cached))return cached;
            int left=bitmap.Width,top=bitmap.Height,right=0,bottom=0;
            for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++)if(bitmap.GetPixel(x,y).A>0){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x+1);bottom=Math.Max(bottom,y+1);}
            cached=right>left?Rectangle.FromLTRB(left,top,right,bottom):new Rectangle(0,0,bitmap.Width,bitmap.Height);opaqueBounds[bitmap]=cached;return cached;
        }
        public void Dispose(){opaqueBounds.Clear();foreach(Bitmap b in Frames)if(b!=null)b.Dispose();foreach(Bitmap b in Feeding)if(b!=null)b.Dispose();foreach(Bitmap b in Illustrated)if(b!=null)b.Dispose();if(Snack!=null)Snack.Dispose();}
    }

    static class Motion {
        public static double Ease(double t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        public static Point Clamp(Point p,Size size,Rectangle bounds) {
            return new Point(Math.Max(bounds.Left,Math.Min(p.X,bounds.Right-size.Width)),Math.Max(bounds.Top,Math.Min(p.Y,bounds.Bottom-size.Height)));
        }
        public static int Direction(double dx,double dy) {
            double a=Math.Atan2(dx,-dy)*180/Math.PI;
            return ((int)Math.Floor((a+360+11.25)/22.5))%16;
        }
        public static Point Travel(Point start,Point end,double t) {
            t=Math.Max(0,Math.Min(1,t)); double e=SwimPath.Progress(t),arc=Math.Sin(t*Math.PI);
            return new Point((int)Math.Round(start.X+(end.X-start.X)*e),(int)Math.Round(start.Y+(end.Y-start.Y)*e-arc*arc*8));
        }
    }

    static class Renderer {
        public static Size WindowSize(int spriteSize){return new Size(Math.Max(250,spriteSize+36),(int)(spriteSize*208.0/192)+90);}
        public static Rectangle SpriteRect(int spriteSize){Size w=WindowSize(spriteSize);return new Rectangle((w.Width-spriteSize)/2,78,spriteSize,(int)(spriteSize*208.0/192));}
        public static Bitmap Draw(Atlas atlas,int row,int frame,int size,string bubble,bool faceRight=false) {
            return DrawSprite(atlas.Frames[row,frame],size,bubble,0,faceRight);
        }
        public static Bitmap DrawAnimated(Atlas atlas,int row,double elapsed,int size,string bubble,bool faceRight=false){
            Bitmap sprite=atlas.Frames[row,Atlas.FrameAt(row,elapsed*1000)];
            return DrawSprite(sprite,size,bubble,elapsed,faceRight);
        }
        static Bitmap DrawSprite(Bitmap sprite,int size,string bubble,double time,bool faceRight){
            Size ws=WindowSize(size); Bitmap result=new Bitmap(ws.Width,ws.Height,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(result)) {
                g.Clear(Color.Transparent); g.SmoothingMode=SmoothingMode.AntiAlias;
                g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                Rectangle body=SpriteRect(size);double breath=Math.Sin(time*Math.PI*2/2.8),sway=Math.Sin(time*Math.PI*2/3.6);
                GraphicsState state=g.Save();
                if(faceRight){g.TranslateTransform(ws.Width,0);g.ScaleTransform(-1,1);}
                g.TranslateTransform(body.Left+body.Width/2f,body.Top+body.Height/2f+(float)breath*1.6f);
                g.RotateTransform((float)sway*.8f);g.ScaleTransform(1+(float)breath*.004f,1-(float)breath*.007f);
                g.DrawImage(sprite,new Rectangle(-body.Width/2,-body.Height/2,body.Width,body.Height));g.Restore(state);
                DrawBubble(g,ws,bubble);
            }
            return result;
        }
        public static void DrawBubble(Graphics g,Size ws,string bubble) {
                if(!String.IsNullOrEmpty(bubble)) {
                    RectangleF body=new RectangleF(12,8,ws.Width-24,57);
                    using(GraphicsPath path=Round(body,17)) using(SolidBrush bg=new SolidBrush(Color.FromArgb(250,245,252,255)))
                    using(Pen border=new Pen(Color.FromArgb(180,145,188,210),1)) {
                        g.FillPath(bg,path);g.DrawPath(border,path);
                        PointF[] tri={new PointF(ws.Width/2-7,64),new PointF(ws.Width/2,74),new PointF(ws.Width/2+7,64)};
                        g.FillPolygon(bg,tri);
                    }
                    using(Font font=new Font("Segoe UI",10.5f)) using(Brush ink=new SolidBrush(Color.FromArgb(29,64,83)))
                    using(StringFormat f=new StringFormat()) {
                        f.Alignment=StringAlignment.Center; f.LineAlignment=StringAlignment.Center;
                        g.DrawString(bubble,font,ink,new RectangleF(22,12,ws.Width-44,48),f);
                    }
                }
        }
        static GraphicsPath Round(RectangleF r,float radius){GraphicsPath p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    }

    sealed partial class Companion : Form {
        readonly Atlas atlas=new Atlas(); readonly Preferences prefs; readonly Random random;
        static readonly FeedKind[] SurpriseFeeds=(FeedKind[])Enum.GetValues(typeof(FeedKind));
        static readonly ReactionKind[] SurpriseReactions=(ReactionKind[])Enum.GetValues(typeof(ReactionKind));
        static readonly string[] Introductions={
            "Hi, I'm Mochi!\nYour whale shark pal.",
            "Hello! I'm Mochi.\nYour ocean buddy.",
            "I'm Mochi!\nSmall fins, big heart!"
        };
        const string StartupHint="Click to interact with me!\nRight-click for more.";
        static readonly string[] DragLines={"Wheee! Air swimming!","I'm a flying fish!\nShhh... close enough.","Tiny shark delivery!","First class, please!","Mind the fins!","Up, up, and a-splash!"};
        static readonly string[] DropLines={"A nice new spot!","Five-star parking!","New spot. Same shark.","Home sweet splash!","Landing: fin-tastic!","I meant to land here."};
        int lastDragLine=-1,lastDropLine=-1;
        double startupHintAt=-1;
        int lastSurprise=-1;
        bool faceRight;
        static readonly InteractionVariant[] SurpriseVariants=InteractionVariant.All();
        int nextIdleCategory; double nextIdleActivity, automaticReadyAt;
        bool idleActivityActive;
        readonly GazeMotion gaze=new GazeMotion(); readonly bool simulation;
        readonly GazeMotion swimGaze=new GazeMotion();readonly EdgeWatch edgeWatch=new EdgeWatch();
        SwimPath swimPath;PointF swimFraction;
        double simulatedTime,nextBackgroundCheck; bool resourcesDisposed;
        readonly Stopwatch clock=Stopwatch.StartNew(); readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        NotifyIcon tray; ContextMenuStrip menu; ToolStripMenuItem pauseItem; Icon trayIcon; Bitmap canvas;
        int row,frame; double actionStart,actionUntil,nextSwim,bubbleUntil,travelStart,travelDuration;
        string bubble=""; bool down,dragging,swimming,modal,closing; Point dragMouse,dragWindow,swimFrom,swimTo;
        FeedSequence feeding; ReactionSequence reaction;
        double Now {get{return simulation?simulatedTime:clock.Elapsed.TotalSeconds;}}
        protected override bool ShowWithoutActivation {get{return !Program.TestUi;}}
        protected override CreateParams CreateParams {get{CreateParams p=base.CreateParams;p.ExStyle|=0x80000;if(!Program.TestUi)p.ExStyle|=0x80|0x08000000;return p;}}
        public Companion(bool simulate=false) {
            simulation=simulate;prefs=simulate?new Preferences():Preferences.Load();
            random=simulate?new Random(37):new Random();
            Text="Mochi Desktop Companion"; AccessibleName="Mochi whale shark companion"; FormBorderStyle=FormBorderStyle.None;
            ShowInTaskbar=Program.TestUi; TopMost=false; StartPosition=FormStartPosition.Manual; Size=Renderer.WindowSize(prefs.Size);
            Rectangle[] areas=Array.ConvertAll(Screen.AllScreens,delegate(Screen screen){return screen.WorkingArea;});
            Location=Spawn.PickLocation(random,Size,areas);
            faceRight=random.Next(2)==1;
            BuildMenu();
            ScheduleIdleActivity();
            SchedulePlayful();
            if(simulation)return;
            using(Bitmap iconBmp=new Bitmap(atlas.Frames[0,0],new Size(48,48))) {
                IntPtr h=iconBmp.GetHicon(); try {using(Icon raw=Icon.FromHandle(h))trayIcon=(Icon)raw.Clone();}finally{Native.DestroyIcon(h);}
            }
            tray=new NotifyIcon{Icon=trayIcon,Text="Mochi - right-click for controls",Visible=true,ContextMenuStrip=menu};
            tray.MouseClick+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left)menu.Show(Cursor.Position);};
            timer.Interval=15; timer.Tick+=delegate{Tick();};
            Shown+=delegate{Native.KeepBehindApps(Handle);StartWelcome();timer.Start();Render();StartUpdateCheck(false);};
            SetStyle(ControlStyles.StandardDoubleClick,false);
            MouseDown+=OnDown;MouseMove+=OnMove;MouseUp+=OnUp;
            MouseCaptureChanged+=delegate{if(!Capture && down){down=false;dragging=false;Schedule();PauseAutomaticActivities();}};
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
        }
        void BuildMenu(){
            menu=new ContextMenuStrip{Font=new Font("Segoe UI",10),ShowImageMargin=false};
            menu.Items.Add("Pet Mochi",null,delegate{Pet();});
            menu.Items.Add("Feed a tiny snack",null,delegate{Feed();});
            menu.Items.Add("Play together",null,delegate{Play();});
            menu.Items.Add("Swim now",null,delegate{BeginSwim(true);});
            menu.Items.Add("Come here",null,delegate{if(simulation)ChooseDestination();else BeginInvoke((Action)ChooseDestination);});
            menu.Items.Add(new ToolStripSeparator());
            pauseItem=new ToolStripMenuItem("Pause swimming");pauseItem.CheckOnClick=false;
            pauseItem.Click+=delegate{prefs.Roam=!prefs.Roam;swimming=false;Schedule();Save();Act(3,prefs.Roam?"Let's explore a little.":"I'll stay right here.",2.5);};menu.Items.Add(pauseItem);
            menu.Items.Add("Settings...",null,delegate{OpenSettings();});
            menu.Items.Add("How to play",null,delegate{Help();});
            AddUpdateMenu();
            menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Quit Mochi",null,delegate{Close();});
            menu.Opening+=delegate{CancelInteraction();swimming=false;edgeWatch.Reset(Now);pauseItem.Checked=!prefs.Roam;};menu.Closed+=delegate{Schedule();PauseAutomaticActivities();edgeWatch.Reset(Now);};
        }
        void Schedule(){int[] lo={45,25,12},hi={90,55,25};nextSwim=Now+random.Next(lo[prefs.Frequency],hi[prefs.Frequency]+1);}
        void ScheduleIdleActivity(){nextIdleActivity=Now+random.Next(60,181);}
        void PauseAutomaticActivities(){automaticReadyAt=Math.Max(automaticReadyAt,Now+3);}
        void EndRegularAnimation(){
            // Only the automatic idle cycle owns this clock. Manual animations and
            // pranks must leave any pending idle deadline intact.
            if(idleActivityActive){idleActivityActive=false;ScheduleIdleActivity();}
            PauseAutomaticActivities();
        }
        void CancelRegularAnimation(){
            if(feeding==null && reaction==null)return;
            EndRegularAnimation();feeding=null;reaction=null;
        }
        bool StartDueActivity(double now){
            if(Program.TestUi || now<automaticReadyAt || (!simulation && Control.MouseButtons!=MouseButtons.None))return false;
            bool idleDue=now>=nextIdleActivity, playfulDue=prefs.PlayfulMode && now>=nextPlayful;
            // Absolute deadlines form a bounded queue: one turn per clock, oldest
            // first. A Settings "try now" request is an explicit user override.
            if(idleDue && (!playfulDue || (!playfulRequested && nextIdleActivity<nextPlayful))){StartIdleActivity();return true;}
            if(playfulDue && BeginPlayful())return true;
            if(idleDue && !modal){StartIdleActivity();return true;}
            return false;
        }
        void StartIdleActivity(){
            // Automatic reactions share the artwork, but leave the manual menu cycles alone.
            if(nextIdleCategory==0)StartFeeding(SurpriseFeeds[random.Next(SurpriseFeeds.Length)],null,true);
            else StartReaction(ReactionRotation.PickRandom(random,nextIdleCategory==2),null,true);
            nextIdleCategory=(nextIdleCategory+1)%3;
        }
        void StartWelcome(){Act(0,Introductions[random.Next(Introductions.Length)],4);startupHintAt=Now+4;}
        void Act(int r,string text,double duration){CancelPlayful();CancelRegularAnimation();StopPicking();guidedSwim=false;startupHintAt=-1;gaze.Reset();swimming=false;row=r;actionStart=Now;actionUntil=Now+duration;automaticReadyAt=Math.Max(automaticReadyAt,actionUntil+3);bubble=text;bubbleUntil=Now+Math.Max(3.5,duration);Schedule();}
        void Pet(){edgeWatch.Reset(Now);StartReaction(false);}
        void Play(){edgeWatch.Reset(Now);StartReaction(true);}
        void StartReaction(bool play){
            ReactionRotation cycle=new ReactionRotation(play?prefs.NextPlay:prefs.NextPet,play);ReactionKind kind=cycle.Take();
            if(play)prefs.NextPlay=cycle.Next;else prefs.NextPet=cycle.Next;
            StartReaction(kind);
        }
        void Face(bool right){faceRight=right;}
        void FaceTravel(double dx){if(Math.Abs(dx)>=1)faceRight=dx>0;}
        void StartReaction(ReactionKind kind,bool? right=null,bool automatic=false){
            CancelPlayful();
            CancelRegularAnimation();
            StopPicking();
            startupHintAt=-1;
            gaze.Reset();feeding=null;swimming=false;bubble="";bubbleUntil=0;
            reaction=new ReactionSequence(kind,Now,right??(random.Next(2)==1));Face(reaction.FaceRight);
            idleActivityActive=automatic;
            actionUntil=Now+reaction.Duration;Save();Schedule();Render();
        }
        void Feed(){
            edgeWatch.Reset(Now);
            FeedRotation rotation=new FeedRotation(prefs.NextFeed);FeedKind kind=rotation.Take();prefs.NextFeed=rotation.Next;
            StartFeeding(kind);
        }
        void StartFeeding(FeedKind kind,bool? right=null,bool automatic=false){
            CancelPlayful();
            CancelRegularAnimation();
            StopPicking();
            startupHintAt=-1;
            gaze.Reset();reaction=null;swimming=false;bubble="";bubbleUntil=0;
            feeding=new FeedSequence(kind,Now,Location,Size,Screen.FromRectangle(Bounds).WorkingArea,right??(random.Next(2)==1));Face(feeding.FaceRight);
            idleActivityActive=automatic;
            actionUntil=Now+feeding.Duration;Save();Schedule();Render();
        }
        void Interact(){
            edgeWatch.Reset(Now);
            // Draw uniformly from every animation, excluding only the previous surprise.
            int total=SurpriseVariants.Length;
            int pick=random.Next(lastSurprise<0?total:total-1);
            if(lastSurprise>=0 && pick>=lastSurprise)pick++;
            lastSurprise=pick;
            InteractionVariant variant=SurpriseVariants[pick];
            if(variant.Feed.HasValue)StartFeeding(variant.Feed.Value,variant.Right);
            else StartReaction(variant.Reaction.Value,variant.Right);
        }
        void CancelInteraction(){CancelPlayful();StopPicking();startupHintAt=-1;if(feeding!=null || reaction!=null){CancelRegularAnimation();row=0;frame=0;actionUntil=0;actionStart=Now;Schedule();}}
        void OnDown(object s,MouseEventArgs e){
            if(e.Button==MouseButtons.Left || e.Button==MouseButtons.Right)edgeWatch.Reset(Now);
            if(e.Button==MouseButtons.Right){CancelInteraction();swimming=false;menu.Show(Cursor.Position);return;}
            if(e.Button!=MouseButtons.Left)return;
            CancelInteraction();
            down=true;dragging=false;swimming=false;dragMouse=Cursor.Position;dragWindow=Location;if(!simulation)Capture=true;
        }
        string PickLine(string[] lines,ref int previous){
            int pick=random.Next(previous<0?lines.Length:lines.Length-1);
            if(previous>=0 && pick>=previous)pick++;
            previous=pick;return lines[pick];
        }
        void OnMove(object s,MouseEventArgs e){TrackDrag(Cursor.Position);}
        void TrackDrag(Point p){
            if(!down)return;int dx=p.X-dragMouse.X,dy=p.Y-dragMouse.Y;
            if(!dragging && Math.Abs(dx)+Math.Abs(dy)>5){CancelRegularAnimation();dragging=true;bubble=PickLine(DragLines,ref lastDragLine);bubbleUntil=Now+4;}
            if(dragging)MoveDragged(Motion.Clamp(new Point(dragWindow.X+dx,dragWindow.Y+dy),Size,Screen.FromPoint(p).WorkingArea));
        }
        void MoveDragged(Point position){FaceTravel(position.X-Left);Location=position;row=faceRight?1:2;}
        void OnUp(object s,MouseEventArgs e){
            if(e.Button!=MouseButtons.Left || !down)return;bool wasDragging=dragging;down=false;dragging=false;Capture=false;
            edgeWatch.Reset(Now);
            if(wasDragging){Act(3,PickLine(DropLines,ref lastDropLine),2.8);Save();}else Interact();
            Schedule();
        }
        void BeginSwim(bool requested){
            if(down || modal || (!requested&&!prefs.Roam))return;
            if(requested)CancelPlayful();
            CancelRegularAnimation();
            StopPicking();
            if(requested)startupHintAt=-1;
            gaze.Reset();feeding=null;reaction=null;
            Rectangle b=Screen.FromRectangle(Bounds).WorkingArea;
            swimPath=new SwimPath(Location,Size,b,random,edgeWatch.Due);Point target=swimPath.End;
            if(Math.Abs(target.X-Left)+Math.Abs(target.Y-Top)<15){Schedule();return;}
            if(swimPath.Escaping || requested)edgeWatch.Reset(Now);
            StartSwimPath(swimPath,b,false);
        }
        void Tick(){
            PumpUpdateNotice();
            if(closing)return;
            if(Program.SettingsRequest!=null && Program.SettingsRequest.WaitOne(0) && !modal){OpenSettings();return;}
            if(Now>=nextBackgroundCheck){Native.KeepBehindApps(Handle);nextBackgroundCheck=Now+1;}
            Advance(Now,Cursor.Position);
        }
        void Advance(double now,Point cursor){
            if(simulation)simulatedTime=now;
            if(closing || modal || menu.Visible)return;
            if(AdvancePlayful(now))return;
            if(choosingDestination){frame=Atlas.FrameAt(0,(now-actionStart)*1000);Render();return;}
            if(startupHintAt>=0 && now>=startupHintAt)Act(0,StartupHint,4);
            if(reaction!=null){
                if(!reaction.Finished(now)){Render();return;}
                EndRegularAnimation();reaction=null;row=0;frame=0;actionStart=now;actionUntil=now+.4;Schedule();
            }
            if(feeding!=null){
                Location=Motion.Clamp(feeding.Position(now),Size,Screen.FromRectangle(Bounds).WorkingArea);
                if(!feeding.Finished(now)){Render();return;}
                EndRegularAnimation();feeding=null;row=0;frame=0;actionStart=now;actionUntil=now+0.4;Schedule();Save();
            }
            if(now>bubbleUntil && bubble.Length>0){bubble="";}
            if(!down && prefs.Roam)edgeWatch.Update(Location,Size,Screen.FromRectangle(Bounds).WorkingArea,now);
            else edgeWatch.Reset(now);
            if(swimming){
                double t=(now-travelStart)/travelDuration;
                PointF position=swimPath.Position(t);Location=Motion.Clamp(Point.Round(position),Size,swimBounds);
                swimFraction=new PointF(position.X-Left,position.Y-Top);
                PointF heading=swimPath.Heading(t);swimGaze.Update(heading.X,heading.Y,now);
                if(now>=travelStart+travelDuration){
                    bool arrived=guidedSwim;swimming=false;guidedSwim=false;Location=swimTo;
                    PauseAutomaticActivities();
                    if(arrived)Act(0,PickLine(ArrivalLines,ref lastArrivalLine),2.5);else Schedule();Save();
                }
            }
            if(!swimming&&!down&&now>=actionUntil){
                if(StartDueActivity(now))return;
                Point c=cursor;Rectangle sprite=Renderer.SpriteRect(prefs.Size);Point center=new Point(Left+sprite.Left+sprite.Width/2,Top+sprite.Top+sprite.Height/2);
                double dx=c.X-center.X,dy=c.Y-center.Y,dist=Math.Sqrt(dx*dx+dy*dy);
                if(dist>65 && dist<390 && Screen.FromPoint(c).DeviceName==Screen.FromRectangle(Bounds).DeviceName){
                    gaze.Update(dx,dy,now);row=9+gaze.Direction/8;frame=gaze.Direction%8;
                }else {gaze.Reset();row=0;}
                // A parked pointer is not an interaction. Scheduled swims take priority over looking.
                if(prefs.Roam && !Program.TestUi && now>=automaticReadyAt && (now>=nextSwim || edgeWatch.Due))BeginSwim(false);
            }
            else gaze.Reset();
            if(row<9)frame=Atlas.FrameAt(row,(now-actionStart)*1000);
            Render();
        }
        Bitmap RenderFrame(){
            if(playful!=null)return PlayfulRenderer.Draw(atlas,playful,Now,prefs.Size);
            return reaction!=null?ReactionRenderer.Draw(atlas,reaction,Now,prefs.Size):feeding!=null?FeedRenderer.Draw(atlas,feeding.Sample(Now),prefs.Size,feeding.Speech(Now)):
                swimming?GazeRenderer.Draw(atlas,swimGaze.Sample(Now),prefs.Size,bubble,true,swimFraction):
                gaze.Active?GazeRenderer.Draw(atlas,gaze.Sample(Now),prefs.Size,bubble):
                row<9?Renderer.DrawAnimated(atlas,row,Now-actionStart,prefs.Size,bubble,faceRight && row!=1 && row!=2):Renderer.Draw(atlas,row,frame,prefs.Size,bubble);
        }
        void Render(){
            if(!IsHandleCreated)return;
            Bitmap next=RenderFrame();
            try{Native.SetBitmap(Handle,next,Location);}catch{next.Dispose();throw;}
            if(canvas!=null)canvas.Dispose();canvas=next;
        }
        void DisplayChanged(object sender,EventArgs e){if(IsDisposed)return;try{BeginInvoke((Action)delegate{CancelPlayful();StopPicking();swimming=false;guidedSwim=false;Location=Motion.Clamp(Location,Size,Screen.FromRectangle(Bounds).WorkingArea);Schedule();SchedulePlayful();});}catch(InvalidOperationException){} }
        void Save(){if(simulation)return;prefs.X=Left;prefs.Y=Top;prefs.Save();}
        void OpenSettings(){
            bool wasEnabled=prefs.PlayfulMode,tryPlayful=false;
            CancelInteraction();swimming=false;modal=true;
            try{using(SettingsDialog d=new SettingsDialog(prefs,GetPlayfulStatus)){if(d.ShowDialog()==DialogResult.OK){Size=Renderer.WindowSize(prefs.Size);Location=Motion.Clamp(Location,Size,Screen.FromRectangle(Bounds).WorkingArea);tryPlayful=d.TryPlayfulRequested;Save();}}}
            finally{modal=false;Schedule();PauseAutomaticActivities();ApplyPlayfulSettings(wasEnabled,tryPlayful);edgeWatch.Reset(Now);}
        }
        void Help(){swimming=false;modal=true;try{MessageBox.Show("Click Mochi for a random petting, play, or snack animation.\nDrag Mochi to move to another spot or monitor.\nMove your pointer nearby and Mochi will look toward it.\n\nRight-click Mochi or the tray icon to choose petting, feeding, play, swimming, settings, or Quit.\nClick the tray icon to open the controls.\nChoose Come here, then click a spot for Mochi to swim to.\nPress Escape or right-click to cancel choosing a spot.\n\nEvery 1-3 minutes, Mochi takes turns snacking, enjoying a pat, and playing (Idle Activities). Playful Mode has its own clock. Due animations wait their turn, with a 3-second pause between them.\n\nMochi occasionally swims within the current screen.\nSettings lets you pause swimming or change its frequency, size, and Windows startup.\n\nEnable Playful Mode in Settings for a little mischief: every 3-5 minutes, Mochi borrows a desktop icon and swims it to a new spot. Files stay in place. Turn off Auto arrange icons on your desktop to allow this.\n\nMochi runs locally. Only update checks and downloads use the internet. No account or microphone is needed.","Hello, I'm Mochi",MessageBoxButtons.OK,MessageBoxIcon.Information);}finally{modal=false;Schedule();PauseAutomaticActivities();}}
        protected override void WndProc(ref Message m){
            if(m.Msg==0x46 && m.LParam!=IntPtr.Zero){ // WM_WINDOWPOSCHANGING
                Native.WindowPosition position=(Native.WindowPosition)Marshal.PtrToStructure(m.LParam,typeof(Native.WindowPosition));
                if((position.flags&0x4)==0){ // A z-order change must keep the companion behind normal apps.
                    position.insertAfter=Native.BackgroundAnchor(Handle,Native.DesktopSurface());
                    Marshal.StructureToPtr(position,m.LParam,false);
                }
            }
            if(m.Msg==0x21){m.Result=new IntPtr(3);return;} // MA_NOACTIVATE: petting never steals typing focus.
            if(m.Msg==0x84 && canvas!=null && !down){
                long lp=m.LParam.ToInt64();Point p=PointToClient(new Point((short)(lp&0xffff),(short)((lp>>16)&0xffff)));
                if(p.X<0||p.Y<0||p.X>=canvas.Width||p.Y>=canvas.Height||canvas.GetPixel(p.X,p.Y).A<20){m.Result=new IntPtr(-1);return;}
            }
            base.WndProc(ref m);
        }
        protected override void OnFormClosing(FormClosingEventArgs e){closing=true;CancelPlayful();StopUpdateChecks();StopPicking();timer.Stop();Save();base.OnFormClosing(e);}
        protected override void Dispose(bool disposing){
            if(disposing && !resourcesDisposed){resourcesDisposed=true;CancelPlayful();StopUpdateChecks();StopPicking();Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;
                timer.Dispose();if(tray!=null){tray.Visible=false;tray.Dispose();}if(trayIcon!=null)trayIcon.Dispose();if(menu!=null)menu.Dispose();if(canvas!=null)canvas.Dispose();atlas.Dispose();}
            base.Dispose(disposing);
        }
        public static void VerifyWelcome(){
            using(Companion pet=new Companion(true)){
                pet.prefs.Roam=false;Point away=new Point(-10000,-10000);HashSet<string> selected=new HashSet<string>();
                for(int i=0;i<30;i++){
                    pet.StartWelcome();string intro=pet.bubble;selected.Add(intro);double due=pet.startupHintAt;
                    if(Array.IndexOf(Introductions,intro)<0)throw new Exception("Unknown startup introduction");
                    pet.Advance(due-.001,away);
                    if(pet.bubble!=intro || pet.swimming)throw new Exception("Startup introduction ended early");
                    pet.Advance(due,away);
                    if(pet.bubble!=StartupHint || pet.startupHintAt>=0)throw new Exception("Controls hint failed to follow introduction");
                    pet.Advance(pet.Now+3.999,away);
                    if(pet.bubble!=StartupHint)throw new Exception("Controls hint ended early");
                    pet.Advance(pet.Now+.002,away);
                    if(pet.bubble!="")throw new Exception("Controls hint failed to finish");
                }
                if(selected.Count!=Introductions.Length)throw new Exception("Startup introductions are not all reachable");
                foreach(int action in new[]{0,1,2,3,4}){
                    pet.StartWelcome();double due=pet.startupHintAt;pet.Advance(pet.Now+3,away);
                    if(action==0)pet.Pet();else if(action==1)pet.Feed();else if(action==2)pet.Play();
                    else if(action==3){pet.CancelInteraction();pet.modal=true;}else pet.BeginSwim(true);
                    ReactionSequence reaction=pet.reaction;FeedSequence feeding=pet.feeding;
                    pet.Advance(due+.01,away);pet.modal=false;
                    if(pet.startupHintAt>=0 || pet.bubble==StartupHint || pet.reaction!=reaction || pet.feeding!=feeding)throw new Exception("Startup hint interrupted a manual action");
                }
                pet.StartWelcome();double hintDue=pet.startupHintAt;
                pet.OnDown(pet,new MouseEventArgs(MouseButtons.Left,1,0,0,0));pet.OnUp(pet,new MouseEventArgs(MouseButtons.Left,1,0,0,0));pet.Advance(hintDue,away);
                if(pet.bubble==StartupHint || pet.startupHintAt>=0)throw new Exception("Startup hint took priority over a click");
                List<string> messages=new List<string>(Introductions);messages.Add(StartupHint);messages.AddRange(DragLines);messages.AddRange(DropLines);messages.AddRange(SwimOverLines);messages.AddRange(ArrivalLines);
                using(Bitmap surface=new Bitmap(280,300))using(Graphics g=Graphics.FromImage(surface))using(Font font=new Font("Segoe UI",10.5f))using(StringFormat format=new StringFormat()){
                    format.Alignment=StringAlignment.Center;format.LineAlignment=StringAlignment.Center;
                    foreach(int size in new[]{144,176,224})foreach(string message in messages){
                        int fitted,lines;g.MeasureString(message,font,new SizeF(Renderer.WindowSize(size).Width-44,48),format,out fitted,out lines);
                        if(fitted<message.Length || lines>2)throw new Exception("Companion message does not fit the bubble: "+message);
                    }
                }
            }
        }
        public static void VerifyBehavior(){
            using(Companion pet=new Companion(true)){
                pet.nextIdleActivity=Double.PositiveInfinity;
                Rectangle area=Screen.PrimaryScreen.WorkingArea;
                pet.Location=Motion.Clamp(new Point(area.Left+area.Width/2-pet.Width/2,area.Top+area.Height/2-pet.Height/2),pet.Size,area);
                pet.prefs.Frequency=2;
                Rectangle sprite=Renderer.SpriteRect(pet.prefs.Size);
                Point parked=new Point(pet.Left+sprite.Left+sprite.Width/2,pet.Top+sprite.Top+sprite.Height/2);
                // Exercise the actual timer path with an unmoving cursor on Mochi, then with it far away.
                foreach(Point pointer in new[]{parked,new Point(area.Right+1000,area.Bottom+1000)}){
                    for(int trip=0;trip<3;trip++){
                        pet.nextSwim=pet.Now;pet.Advance(pet.Now+0.033,pointer);
                        if(!pet.swimming)throw new Exception("Scheduled swim blocked by idle cursor");
                        double start=pet.travelStart,duration=pet.travelDuration;Point destination=pet.swimTo;
                        pet.Advance(start+duration*.5,pointer);
                        if(!pet.swimming || pet.Location==pet.swimFrom)throw new Exception("Swim interrupted by cursor");
                        pet.Advance(start+duration+.001,pointer);
                        if(pet.swimming || pet.Location!=destination)throw new Exception("Swim did not finish");
                        pet.Advance(pet.nextSwim-.001,pointer);
                        if(pet.swimming)throw new Exception("Swim rest interval skipped");
                    }
                }
                pet.prefs.Roam=false;pet.nextSwim=0;pet.Advance(pet.Now+30,parked);
                if(pet.swimming)throw new Exception("Pause ignored");
                pet.Location=Motion.Clamp(new Point(area.Left+area.Width/2-pet.Width/2,area.Top+area.Height/2-pet.Height/2),pet.Size,area);
                Point center=new Point(pet.Left+sprite.Left+sprite.Width/2,pet.Top+sprite.Top+sprite.Height/2);
                for(int direction=0;direction<16;direction++){
                    double angle=direction*Math.PI/8;
                    Point pointer=new Point(center.X+(int)(Math.Sin(angle)*130),center.Y-(int)(Math.Cos(angle)*130));
                    for(int i=0;i<30;i++)pet.Advance(pet.Now+.033,pointer);
                    if(!pet.gaze.Active || pet.gaze.Direction!=direction || pet.row<9)throw new Exception("Timer gaze direction failed: "+direction);
                }
                pet.Advance(pet.Now+.033,center);
                if(pet.gaze.Active || pet.row!=0)throw new Exception("Close pointer did not return to animated idle");
                pet.prefs.Roam=true;pet.down=true;pet.Advance(pet.Now+30,parked);
                if(pet.swimming)throw new Exception("Drag interrupted by automatic swim");
                pet.down=false;pet.modal=true;pet.Advance(pet.Now+30,parked);
                if(pet.swimming)throw new Exception("Settings interrupted by automatic swim");
                pet.modal=false;pet.Feed();pet.nextSwim=0;pet.Advance(pet.Now+1,parked);
                if(pet.swimming || pet.feeding==null)throw new Exception("Feeding interrupted by automatic swim");
                pet.CancelInteraction();pet.OnDown(pet,new MouseEventArgs(MouseButtons.Left,1,0,0,0));pet.nextSwim=0;pet.Advance(pet.Now+.1,parked);
                if(pet.swimming || pet.feeding!=null || pet.reaction!=null)throw new Exception("Holding the mouse triggered an action");
                pet.OnUp(pet,new MouseEventArgs(MouseButtons.Left,1,0,0,0));
                if(pet.feeding==null && pet.reaction==null)throw new Exception("Single click failed to react immediately");
                pet.prefs.NextPet=0;pet.prefs.NextPlay=0;
                for(int i=0;i<20;i++){
                    int feedCount=pet.prefs.NextFeed;
                    ReactionKind expectedPet=new[]{ReactionKind.CheekNuzzle,ReactionKind.HappyWiggle,ReactionKind.CozySway,ReactionKind.HighFlipper,ReactionKind.FlipperHug,ReactionKind.NoseBoop}[i%6];
                    pet.Pet();if(pet.reaction.Kind!=expectedPet || pet.prefs.NextFeed!=feedCount)throw new Exception("Pet cycle changed another cycle");
                    pet.nextSwim=0;pet.Advance(pet.Now+.5,parked);
                    if(pet.swimming || pet.reaction==null)throw new Exception("Petting interrupted by roaming");
                    int petCount=pet.prefs.NextPet;
                    pet.Play();if(pet.reaction.Kind!=(i%5==4?ReactionKind.BubbleSurf:(ReactionKind)(i%5+4)) || pet.prefs.NextPet!=petCount)throw new Exception("Play cycle changed petting cycle");
                    pet.Advance(pet.Now+.5,parked);if(pet.swimming || pet.reaction==null)throw new Exception("Play interrupted by roaming");
                    pet.Advance(pet.reaction.Started+pet.reaction.Duration,parked);
                    if(pet.reaction!=null || pet.row!=0)throw new Exception("Reaction did not return to idle");
                }
                pet.Pet();pet.Feed();if(pet.reaction!=null || pet.feeding==null)throw new Exception("Feed did not replace petting");
                pet.Play();if(pet.feeding!=null || pet.reaction==null)throw new Exception("Play did not replace feeding");
                pet.CancelInteraction();if(pet.reaction!=null || pet.feeding!=null)throw new Exception("Interaction cancellation failed");
                pet.Play();pet.BeginSwim(true);if(pet.reaction!=null)throw new Exception("Explicit swim did not replace play");
                int savedPet=pet.prefs.NextPet,savedPlay=pet.prefs.NextPlay,savedFeed=pet.prefs.NextFeed;
                HashSet<string> surprises=new HashSet<string>();string previousSurprise=null;
                for(int i=0;i<500;i++){
                    pet.OnDown(pet,new MouseEventArgs(MouseButtons.Left,i%2+1,0,0,0));
                    pet.OnUp(pet,new MouseEventArgs(MouseButtons.Left,i%2+1,0,0,0));
                    if(pet.down || (pet.feeding==null)==(pet.reaction==null))throw new Exception("Each single click must trigger exactly one immediate surprise");
                    string chosen=pet.feeding!=null?"feed:"+pet.feeding.Kind+":"+pet.feeding.FaceRight:"reaction:"+pet.reaction.Kind+":"+pet.reaction.FaceRight;
                    if(chosen==previousSurprise)throw new Exception("Immediate single-click surprise repeat");
                    surprises.Add(chosen);previousSurprise=chosen;
                    FeedSequence feed=pet.feeding;ReactionSequence reaction=pet.reaction;
                    pet.Advance(pet.Now+.4,parked);
                    if(pet.feeding!=feed || pet.reaction!=reaction || pet.swimming)throw new Exception("Pending single click or roaming interrupted surprise");
                    if(pet.prefs.NextPet!=savedPet || pet.prefs.NextPlay!=savedPlay || pet.prefs.NextFeed!=savedFeed)throw new Exception("Surprise changed a menu cycle");
                }
                foreach(InteractionVariant variant in SurpriseVariants)if(!surprises.Contains(variant.Key))throw new Exception("Unreachable directional surprise: "+variant.Key);
                int previousPick=pet.lastSurprise;
                pet.OnUp(pet,new MouseEventArgs(MouseButtons.Left,1,0,0,0));pet.OnUp(pet,new MouseEventArgs(MouseButtons.Right,1,0,0,0));
                if(pet.lastSurprise!=previousPick)throw new Exception("Unmatched release or right-click triggered a surprise");
                HashSet<string> dragLines=new HashSet<string>(),dropLines=new HashSet<string>();string previousDrag=null,previousDrop=null;
                for(int i=0;i<60;i++){
                    pet.OnDown(pet,new MouseEventArgs(MouseButtons.Left,1,0,0,0));Point pointer=pet.dragMouse;
                    pet.TrackDrag(new Point(pointer.X+20,pointer.Y+10));
                    if(!pet.dragging || pet.feeding!=null || pet.reaction!=null || pet.bubble==previousDrag)throw new Exception("Drag triggered a reaction or repeated speech");
                    dragLines.Add(pet.bubble);previousDrag=pet.bubble;
                    pet.OnUp(pet,new MouseEventArgs(MouseButtons.Left,1,0,0,0));
                    if(pet.feeding!=null || pet.reaction!=null || pet.lastSurprise!=previousPick || pet.bubble==previousDrop)throw new Exception("Drop triggered a click or repeated speech");
                    dropLines.Add(pet.bubble);previousDrop=pet.bubble;
                }
                if(dragLines.Count!=DragLines.Length || dropLines.Count!=DropLines.Length)throw new Exception("Drag/drop speech lacks variety");
                int calls=0;foreach(ToolStripItem item in pet.menu.Items)if(item.Text=="Come here")calls++;
                if(calls!=1)throw new Exception("Come here action missing or duplicated");
            }
        }
        public static void VerifyEdgeBehavior(){
            using(Companion pet=new Companion(true)){
                Rectangle area=Screen.PrimaryScreen.WorkingArea;
                pet.Location=Motion.Clamp(new Point(area.Left,area.Top),pet.Size,area);
                pet.prefs.Frequency=0;pet.nextSwim=10000;pet.nextIdleActivity=Double.PositiveInfinity;pet.edgeWatch.Reset(0);
                Point away=new Point(-10000,-10000);
                for(int i=1;i<=52 && !pet.swimming;i++)pet.Advance(i*.5,away);
                if(!pet.swimming || pet.swimPath==null || !pet.swimPath.Escaping)throw new Exception("Lingering at edge did not trigger inward swim");
                if(pet.Now<25 || pet.Now>26)throw new Exception("Border escape timing");
                if(pet.nextIdleActivity!=Double.PositiveInfinity)throw new Exception("Border escape reset idle activity timer");
                pet.Advance(pet.travelStart+pet.travelDuration,away);
                if(pet.swimming || EdgeWatch.Near(pet.Location,pet.Size,area))throw new Exception("Border escape failed to reach open water");
                pet.Location=new Point(area.Left,area.Top);pet.prefs.Roam=false;pet.nextSwim=0;
                for(int i=0;i<60;i++)pet.Advance(pet.Now+.5,away);
                if(pet.swimming)throw new Exception("Border escape ignored Pause swimming");
                pet.prefs.Roam=true;pet.nextSwim=10000;pet.down=true;
                for(int i=0;i<60;i++)pet.Advance(pet.Now+.5,away);
                if(pet.swimming || pet.edgeWatch.Due)throw new Exception("Border escape ignored held drag");
                pet.down=false;pet.modal=true;pet.Advance(pet.Now+30,away);
                if(pet.swimming)throw new Exception("Border escape interrupted settings");
                pet.modal=false;pet.Pet();ReactionSequence reaction=pet.reaction;
                pet.Advance(pet.Now+1,away);
                if(pet.swimming || pet.reaction!=reaction)throw new Exception("Border escape interrupted petting");
            }
        }
        public static void VerifyIdleBehavior(){
            using(Companion pet=new Companion(true)){
                pet.prefs.Roam=false;
                pet.prefs.NextFeed=2;pet.prefs.NextPet=4;pet.prefs.NextPlay=3;
                Point away=new Point(-10000,-10000);
                HashSet<FeedKind> feeds=new HashSet<FeedKind>();HashSet<ReactionKind> reactions=new HashSet<ReactionKind>();
                HashSet<int> waits=new HashSet<int>();
                // Advance the real timer path through 100 full cycles without waiting hours in real time.
                for(int i=0;i<300;i++){
                    double wait=pet.nextIdleActivity-pet.Now;
                    if(wait<60 || wait>180)throw new Exception("Idle wait outside 1-3 minutes");
                    waits.Add((int)Math.Round(wait));
                    double due=pet.nextIdleActivity;
                    pet.Advance(due-.001,away);
                    if(pet.feeding!=null || pet.reaction!=null)throw new Exception("Idle activity started early");
                    pet.Advance(due,away);
                    double end;
                    if(i%3==0){
                        if(pet.feeding==null || pet.reaction!=null)throw new Exception("Idle category should be feeding");
                        feeds.Add(pet.feeding.Kind);end=pet.feeding.Started+pet.feeding.Duration;
                    }else{
                        if(pet.feeding!=null || pet.reaction==null || pet.reaction.IsPlay!=(i%3==2))throw new Exception("Idle pet/play order changed");
                        reactions.Add(pet.reaction.Kind);end=pet.reaction.Started+pet.reaction.Duration;
                    }
                    if(pet.swimming || pet.prefs.NextFeed!=2 || pet.prefs.NextPet!=4 || pet.prefs.NextPlay!=3 || pet.lastSurprise!=-1)throw new Exception("Idle activity changed manual controls");
                    pet.Advance(end-.001,away);
                    if(pet.feeding==null && pet.reaction==null)throw new Exception("Idle animation ended early");
                    pet.Advance(end,away);
                    if(pet.feeding!=null || pet.reaction!=null || pet.nextIdleCategory!=(i+1)%3)throw new Exception("Idle animation failed to finish or category failed to advance");
                }
                if(feeds.Count!=5 || reactions.Count!=11 || waits.Count<20)throw new Exception("Idle animation or delay selection lacks variety");

                // A swim already underway finishes before a due reaction, including with the cursor parked on Mochi.
                pet.prefs.Roam=true;pet.actionUntil=0;pet.nextIdleActivity=pet.Now+1;
                double originalDue=pet.nextIdleActivity;
                pet.BeginSwim(false);
                if(!pet.swimming || pet.nextIdleActivity!=originalDue)throw new Exception("Automatic swim reset idle timer");
                double swimEnd=pet.travelStart+pet.travelDuration;
                pet.Advance(originalDue,new Point(pet.Left+pet.Width/2,pet.Top+pet.Height/2));
                if(!pet.swimming || pet.feeding!=null || pet.reaction!=null)throw new Exception("Idle activity interrupted swim");
                Point destination=pet.swimTo;
                pet.Advance(swimEnd,new Point(destination.X+pet.Width/2,destination.Y+pet.Height/2));
                if(pet.swimming || pet.feeding!=null || pet.nextIdleActivity!=originalDue)throw new Exception("Swim finish discarded a pending idle turn or skipped its pause");
                pet.Advance(swimEnd+2.99,away);
                if(pet.feeding!=null || pet.reaction!=null)throw new Exception("Idle activity skipped the 3-second swim handoff");
                pet.Advance(swimEnd+3,away);
                if(pet.swimming || pet.feeding==null || pet.nextIdleCategory!=1)throw new Exception("Swimming or parked pointer starved idle cycle");
                pet.Advance(pet.feeding.Started+pet.feeding.Duration,away);
                pet.prefs.Roam=false;
                Rectangle area=Screen.PrimaryScreen.WorkingArea;
                pet.Location=Motion.Clamp(new Point(area.Left+area.Width/2-pet.Width/2,area.Top+area.Height/2-pet.Height/2),pet.Size,area);
                Rectangle sprite=Renderer.SpriteRect(pet.prefs.Size);
                Point nearby=new Point(pet.Left+sprite.Left+sprite.Width/2+130,pet.Top+sprite.Top+sprite.Height/2);
                originalDue=pet.nextIdleActivity;pet.Advance(pet.Now+1,nearby);
                if(!pet.gaze.Active || pet.nextIdleActivity!=originalDue)throw new Exception("Looking reset idle timer");
                pet.Advance(originalDue,nearby);
                if(pet.reaction==null || pet.reaction.IsPlay || pet.nextIdleCategory!=2)throw new Exception("Looking blocked idle petting");

                // Manual requests win, while an already-due automatic turn keeps its place.
                pet.CancelInteraction();int category=pet.nextIdleCategory;
                pet.nextIdleActivity=pet.Now;
                pet.OnDown(pet,new MouseEventArgs(MouseButtons.Left,1,0,0,0));pet.OnUp(pet,new MouseEventArgs(MouseButtons.Left,1,0,0,0));
                if(pet.nextIdleCategory!=category || (pet.feeding==null && pet.reaction==null))throw new Exception("Idle cycle overrode click");
                pet.Pet();ReactionSequence manual=pet.reaction;
                pet.nextIdleActivity=pet.Now+.1;double pending=pet.nextIdleActivity;pet.Advance(pet.Now+.2,away);
                if(pet.reaction!=manual || pet.nextIdleCategory!=category)throw new Exception("Idle cycle interrupted manual reaction");
                pet.Advance(manual.Started+manual.Duration,away);
                if(pet.nextIdleActivity!=pending)throw new Exception("Manual reaction erased a pending idle turn");
                pet.OnDown(pet,new MouseEventArgs(MouseButtons.Left,1,0,0,0));pet.OnUp(pet,new MouseEventArgs(MouseButtons.Left,1,0,0,0));
                if(pet.nextIdleCategory!=category || pet.nextIdleActivity!=pending)throw new Exception("Single-click changed the pending idle category or deadline");
                pet.CancelInteraction();pet.BeginSwim(true);
                if(pet.nextIdleActivity!=pending || pet.nextIdleCategory!=category)throw new Exception("Manual swim erased a pending idle turn");
                pet.swimming=false;pet.nextIdleActivity=pet.Now+1;pet.down=true;pet.dragging=true;
                pending=pet.nextIdleActivity;
                pet.Advance(pet.Now+600,away);
                if(pet.feeding!=null || pet.reaction!=null || pet.nextIdleCategory!=category)throw new Exception("Idle cycle interrupted dragging");
                pet.OnUp(pet,new MouseEventArgs(MouseButtons.Left,1,0,0,0));
                if(pet.nextIdleActivity!=pending || pet.down)throw new Exception("Drag release changed the pending idle deadline");
                pet.modal=true;pet.Advance(pet.Now+600,away);
                if(pet.feeding!=null || pet.reaction!=null || pet.nextIdleCategory!=category)throw new Exception("Idle cycle interrupted controls");
                pet.modal=false;pet.ScheduleIdleActivity();

                // A long gap in timer ticks starts only one reaction, never a backlog of missed turns.
                pet.Advance(pet.Now+36000,away);
                if(pet.reaction==null || !pet.reaction.IsPlay || pet.nextIdleCategory!=0)throw new Exception("Long idle gap skipped category");
                pet.Advance(pet.reaction.Started+pet.reaction.Duration,away);
                pet.Advance(pet.Now+.5,away);
                if(pet.feeding!=null || pet.reaction!=null || pet.nextIdleActivity-pet.Now<59.5)throw new Exception("Long idle gap queued a burst of reactions");
            }
        }
    }

    sealed class SettingsDialog : Form {
        public bool TryPlayfulRequested { get; private set; }
        public SettingsDialog(Preferences prefs,Func<string> playfulStatus=null){
            Text="Mochi Settings";ClientSize=new Size(405,470);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterScreen;TopMost=false;
            BackColor=Color.FromArgb(245,250,253);Font=new Font("Segoe UI",10);
            Label title=new Label{Text="A little ocean on your desktop",Location=new Point(23,20),Size=new Size(365,30),Font=new Font("Segoe UI Semibold",14),ForeColor=Color.FromArgb(27,66,91)};
            CheckBox startup=new CheckBox{Text="Start with Windows",Checked=Startup.Enabled,Location=new Point(25,70),AutoSize=true};
            CheckBox roam=new CheckBox{Text="Swim around occasionally",Checked=prefs.Roam,Location=new Point(25,106),AutoSize=true};
            CheckBox playfulMode=new CheckBox{Text="Playful Mode (mischievous icons)",Name="PlayfulModeToggle",Checked=prefs.PlayfulMode,Location=new Point(25,142),AutoSize=true};
            Label playfulHint=new Label{Text="Every 3-5 minutes, borrow and rearrange an icon.\nFiles stay in place. Turn off desktop Auto arrange\nicons; turn off Align icons to grid for smooth swims.",Location=new Point(25,174),Size=new Size(355,56),ForeColor=Color.FromArgb(80,103,117),Font=new Font("Segoe UI",9)};
            Label sl=new Label{Text="Mochi's size",Location=new Point(25,246),AutoSize=true};
            ComboBox size=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(173,242),Width=199};size.Items.AddRange(new object[]{"Small","Medium","Large"});size.SelectedIndex=prefs.Size==144?0:prefs.Size==224?2:1;
            Label fl=new Label{Text="Swim frequency",Location=new Point(25,289),AutoSize=true};
            ComboBox freq=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(173,285),Width=199};freq.Items.AddRange(new object[]{"Calm (45-90 seconds)","Balanced (25-55 seconds)","Energetic (12-25 seconds)"});freq.SelectedIndex=prefs.Frequency;
            Label hint=new Label{Text="Turn off Start with Windows for manual startup.\nMochi stays on this computer and runs offline.",Location=new Point(25,332),Size=new Size(355,44),ForeColor=Color.FromArgb(80,103,117),Font=new Font("Segoe UI",9)};
            Button save=new Button{Text="Save",Location=new Point(272,418),Size=new Size(101,32)};Button cancel=new Button{Text="Cancel",Location=new Point(160,418),Size=new Size(101,32),DialogResult=DialogResult.Cancel};
            Button status=new Button{Text="Playful status",Name="PlayfulStatus",Location=new Point(25,380),Size=new Size(153,30)};
            status.Click+=delegate{MessageBox.Show(playfulStatus==null?"Open Settings from Mochi to check the desktop.":playfulStatus(),"Playful Mode status",MessageBoxButtons.OK,MessageBoxIcon.Information);};
            Button tryPlayful=new Button{Text="Save && try now",Name="TryPlayfulNow",Enabled=playfulMode.Checked,Location=new Point(190,380),Size=new Size(183,30)};
            playfulMode.CheckedChanged+=delegate{tryPlayful.Enabled=playfulMode.Checked;};
            tryPlayful.Click+=delegate{TryPlayfulRequested=true;save.PerformClick();};
            save.Click+=delegate{try{if(startup.Checked!=Startup.Enabled)Startup.SetEnabled(startup.Checked);prefs.Roam=roam.Checked;prefs.PlayfulMode=playfulMode.Checked;prefs.Size=new[]{144,176,224}[size.SelectedIndex];prefs.Frequency=freq.SelectedIndex;DialogResult=DialogResult.OK;Close();}catch(Exception ex){MessageBox.Show("Couldn't update Windows startup. Your other settings haven't changed.\n\n"+ex.Message,"Mochi Settings",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};
            Controls.AddRange(new Control[]{title,startup,roam,playfulMode,playfulHint,sl,size,fl,freq,hint,status,tryPlayful,save,cancel});AcceptButton=save;CancelButton=cancel;
        }
    }

    static class Native {
        [StructLayout(LayoutKind.Sequential)] public struct WindowPosition {public IntPtr window,insertAfter;public int x,y,width,height;public uint flags;}
        [StructLayout(LayoutKind.Sequential)] public struct P {public int x,y;public P(int a,int b){x=a;y=b;}}
        [StructLayout(LayoutKind.Sequential)] public struct S {public int w,h;public S(int a,int b){w=a;h=b;}}
        [StructLayout(LayoutKind.Sequential,Pack=1)] public struct Blend {public byte op,flags,alpha,format;}
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr window,IntPtr insertAfter,int x,int y,int width,int height,uint flags);
        delegate bool EnumWindowCallback(IntPtr window,IntPtr parameter);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowCallback callback,IntPtr parameter);
        [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string className,string title);
        public static IntPtr DesktopSurface(){
            IntPtr shell=GetShellWindow();
            if(shell==IntPtr.Zero)return IntPtr.Zero;
            // Explorer can host the desktop icons in Progman or a separate WorkerW.
            IntPtr desktop=shell;
            EnumWindows(delegate(IntPtr candidate,IntPtr parameter){
                if(IsWindowVisible(candidate) && FindWindowEx(candidate,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero){desktop=candidate;return false;}
                return true;
            },IntPtr.Zero);
            return desktop;
        }
        public static IntPtr BackgroundAnchor(IntPtr window,IntPtr desktop){
            if(desktop==IntPtr.Zero)return new IntPtr(1); // No Explorer desktop yet: HWND_BOTTOM.
            IntPtr previous=GetWindow(desktop,3); // GW_HWNDPREV: immediately above the desktop.
            if(previous==window)previous=GetWindow(window,3);
            // Never enter the topmost band, even if only topmost windows precede Explorer.
            if(previous==IntPtr.Zero || (GetWindowLong(previous,-20)&8)!=0)return new IntPtr(-2); // HWND_NOTOPMOST
            return previous;
        }
        public static void KeepBehindApps(IntPtr window){
            IntPtr desktop=DesktopSurface();
            if(desktop!=IntPtr.Zero && GetWindow(window,2)==desktop && (GetWindowLong(window,-20)&8)==0)return;
            if(!SetWindowPos(window,BackgroundAnchor(window,desktop),0,0,0,0,0x13)) // No move, resize, or activation.
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h,IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll",SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr dest,ref P location,ref S size,IntPtr src,ref P origin,int key,ref Blend blend,int flags);
        public static void SetBitmap(IntPtr window,Bitmap bitmap,Point location){
            IntPtr screen=GetDC(IntPtr.Zero),memory=CreateCompatibleDC(screen),handle=IntPtr.Zero,old=IntPtr.Zero;
            try{handle=bitmap.GetHbitmap(Color.FromArgb(0));old=SelectObject(memory,handle);P pos=new P(location.X,location.Y),zero=new P(0,0);S size=new S(bitmap.Width,bitmap.Height);Blend blend=new Blend{op=0,flags=0,alpha=255,format=1};
                if(!UpdateLayeredWindow(window,screen,ref pos,ref size,memory,ref zero,0,ref blend,2))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }finally{if(old!=IntPtr.Zero)SelectObject(memory,old);if(handle!=IntPtr.Zero)DeleteObject(handle);DeleteDC(memory);ReleaseDC(IntPtr.Zero,screen);}
        }
    }

    static class Tests {
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        public static int Run(string path){
            try{
                if(UpdateTests.Run(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)),"update-test-results.txt"))!=0)
                    throw new Exception("Updater tests failed; see update-test-results.txt.");
                if(PlayfulTests.Run(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)),"playful-test-results.txt"))!=0)
                    throw new Exception("Playful Mode tests failed; see playful-test-results.txt.");
                Check(Motion.Direction(0,-1)==0,"up cardinal");Check(Motion.Direction(1,0)==4,"right cardinal");Check(Motion.Direction(0,1)==8,"down cardinal");Check(Motion.Direction(-1,0)==12,"left cardinal");
                for(int i=0;i<16;i++){double a=i*Math.PI/8;Check(Motion.Direction(Math.Sin(a),-Math.Cos(a))==i,"direction mapping "+i);}
                Rectangle monitor=new Rectangle(-1920,-100,1920,1080);Size size=new Size(260,280);
                Random r=new Random(1234);for(int i=0;i<1000;i++){Point p=Motion.Clamp(new Point(r.Next(-4000,2000),r.Next(-2000,2000)),size,monitor);Check(monitor.Contains(new Rectangle(p,size)),"offscreen clamp");}
                Point from=new Point(10,20),to=new Point(310,100);Check(Motion.Travel(from,to,0)==from,"travel start");Check(Motion.Travel(from,to,1)==to,"travel end");
                using(Atlas a=new Atlas()){
                    for(int row=0;row<9;row++)for(int ms=0;ms<4000;ms+=17)Check(Atlas.FrameAt(row,ms)<Atlas.Durations[row].Length,"unused frame sampled");
                    for(int row=0;row<11;row++){int count=row<9?Atlas.Durations[row].Length:8;for(int c=0;c<count;c++){bool opaque=false;for(int y=0;y<208;y+=4)for(int x=0;x<192;x+=4)if(a.Frames[row,c].GetPixel(x,y).A>200)opaque=true;Check(opaque,"empty frame");}}
                    using(Bitmap b=Renderer.Draw(a,0,0,176,"Hello")){Check(b.GetPixel(0,0).A==0,"opaque window corner");Check(b.Size==Renderer.WindowSize(176),"render dimensions");}
                    FeedingTests.Run(a,Path.GetDirectoryName(Path.GetFullPath(path)));
                    GazeTests.Run(a);
                    ReactionTests.Run(a,Path.GetDirectoryName(Path.GetFullPath(path)));
                    SmoothMotionTests.Run(a);
                }
                Companion.VerifyBehavior();
                Companion.VerifyIdleBehavior();
                Companion.VerifyEdgeBehavior();
                Companion.VerifyWelcome();
                Companion.VerifyFacing();
                Companion.VerifyDestination();
                File.WriteAllText(path,"PASS: all 16 direction mappings; 1000 offscreen clamps including negative-coordinate monitor; travel endpoints; all animation frame bounds; 73 original + 24 feeding + 32 illustrated interaction cells; alpha-transparent window; render geometry.\r\nPASS: five-reaction feeding rotation; saved next reaction after restart; legacy settings default; malformed counter normalization; feed timelines and finish boundaries; snack disappears at gulp; screen-edge chase in both directions; timed feeding speech at all 3 sizes.\r\nPASS: continuous gaze motion in all 16 directions at all 3 sizes; no clipping; smooth wraparound and reverse turns; real timer path completes repeated swims with a parked or distant cursor; pause, drag, settings, feeding and held clicks retain priority.\r\nPASS: 6 petting and 5 play reactions; independent persisted rotations; old preferences retained; reaction speech lifetime; every illustrated frame used in sequence without synthetic sprite rotation; changing rendered frames and clear window edges at all 3 sizes; settle to idle; interruption and activity priorities through the actual timer path.\r\nPASS: 500 seeded single-click requests reach all 32 directional variants without immediate repeats, react immediately, preserve menu cycles, ignore right-button releases, and keep dragging separate; all drag/drop sayings reached without consecutive repeats.\r\nPASS: 100 full idle category cycles; all 16 routines selected; varied 60-180 second waits; no early starts; wait restarts after animation completion; swims finish before due activities; roaming and gaze do not reset the timer; paused swimming allows idle activities; manual clicks, reactions, dragging and controls retain priority; manual counters are preserved; long timer gaps do not queue a burst of reactions.\r\nPASS: single-pose rendering throughout feeding, reactions, idle and turns; opaque body has no strip seams at all 3 sizes; swimming renders in all 16 directions and 3 sizes without clipping; 2000 straight, curved, and mixed paths stay inside landscape, portrait and negative-coordinate screens; 16 heading sectors reached; all 3 swim styles and both mixed orders; continuous join headings; distance-based speed; smooth launch/landing; 320 inward edge escapes; 25-second border dwell; actual timer escape and manual/pause priorities.\r\nPASS: three random startup introductions; introduction precedes controls hint for 4 seconds each; manual actions and held clicks cancel the queued hint; text fits at every size.\r\nPASS: 32 directional variants; mirrored bodies and unmirrored speech at all 3 sizes; 3000 safe random spawns across three monitors; left/right idle startup poses throughout introductions and hints; lasting idle direction after every variant, 100 swims, drag reversals and release, and temporary cursor gaze.\r\nPASS: Come here picker pauses other activity, consumes the selection click, cancels safely, and guides a real animated swim even with roaming paused; 900 safe target placements and routes; cross-monitor endpoints; remembered arrival facing; no surprise reaction from destination selection.\r\n");return 0;
            }catch(Exception ex){File.WriteAllText(path,"FAIL: "+ex);return 1;}
        }
    }
}
