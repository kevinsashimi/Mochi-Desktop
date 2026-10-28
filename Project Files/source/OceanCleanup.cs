using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace MochiDesktop {
    enum OceanLitter { Bottle, Bag, Net, Can }
    enum OceanBin { PearlClam, CoralBasket, BuoyBarrel, TreasureChest, NautilusPot, SubmarinePod }
    enum CleanupStage { Drift, React, Courage, Approach, Pickup, Carry, Deposit, Celebrate, Finished }

    // Pure animation state. Litter is never represented by a file or a Shell item.
    sealed class CleanupTrip {
        public const int LitterSize=84, OpenNetSize=120, CargoSize=44, BinSize=112;
        public const double AverageSwimSpeed=140;
        public readonly OceanLitter Kind;
        public readonly int Emotion, Size, Variant;
        public readonly OceanBin BinKind;
        public readonly MischiefKind Grip;
        public readonly Rectangle Area;
        public double Born { get; private set; }
        public CleanupStage Stage { get; private set; }
        public double Started { get; private set; }
        public Point Position { get; private set; }
        public PointF Litter { get; private set; }
        public Point Bin { get; private set; }
        public double BinShown { get; private set; }
        public bool Right { get; private set; }
        public SwimPath Route { get; private set; }
        public double Duration { get; private set; }
        public double Expires { get; private set; }
        readonly Random random;
        readonly Point driftOrigin;
        readonly double phase;
        readonly int voice;
        PointF approachLitter,pickupFrom,depositFrom;double pickupAngle;
        public CleanupTrip(Point from,int size,Rectangle area,Random rng,OceanLitter kind,int emotion,double now,int variant=-1,int binKind=-1,Point? previousBin=null,MischiefKind? grip=null) {
            Size=size;Area=area;random=rng;Kind=kind;Emotion=emotion;Born=Started=now;Expires=now+30;Position=from;
            Variant=variant<0?rng.Next(3):variant;BinKind=(OceanBin)(binKind<0?rng.Next(6):binKind);BinShown=-1;
            Grip=grip??(rng.Next(2)==0?MischiefKind.GentleBite:MischiefKind.FinGrab);
            voice=rng.Next(3);phase=rng.NextDouble()*Math.PI*2;
            driftOrigin=new Point(area.Left+area.Width/4+rng.Next(Math.Max(1,area.Width/2)),
                area.Top+area.Height/4+rng.Next(Math.Max(1,area.Height/2)));
            Litter=Drift(now);Bin=PickBin(from,previousBin);Right=Litter.X>from.X+Renderer.WindowSize(size).Width/2;
        }
        Point PickBin(Point from,Point? previous) {
            Rectangle bounds=Area;bounds.Inflate(-12,-12);
            Point best=Motion.Clamp(bounds.Location,new Size(BinSize,BinSize),bounds);double bestScore=Double.NegativeInfinity;
            Rectangle pet=new Rectangle(from,Renderer.WindowSize(Size));pet.Inflate(20,20);
            Rectangle litter=new Rectangle(Point.Round(Litter),new Size(SpawnPixels,SpawnPixels));litter.Inflate(16,16);
            // Bounded rejection sampling reaches the whole working area while
            // keeping the receiving bin distinct from the pet and floating litter.
            for(int attempt=0;attempt<48;attempt++){
                Point candidate=new Point(bounds.Left+random.Next(Math.Max(1,bounds.Width-BinSize+1)),bounds.Top+random.Next(Math.Max(1,bounds.Height-BinSize+1)));
                Rectangle bin=new Rectangle(candidate,new Size(BinSize,BinSize));
                double distance=PlayfulPlan.Distance(candidate,Point.Round(Litter));
                double score=distance-(bin.IntersectsWith(pet)?10000:0)-(bin.IntersectsWith(litter)?10000:0);
                if(previous.HasValue)score-=Math.Max(0,160-PlayfulPlan.Distance(candidate,previous.Value))*20;
                if(score>bestScore){best=candidate;bestScore=score;}
                if(!bin.IntersectsWith(pet) && !bin.IntersectsWith(litter) && distance>=150 && (!previous.HasValue || PlayfulPlan.Distance(candidate,previous.Value)>=140))return candidate;
            }
            return best;
        }
        public PointF BinMouth {
            get {
                // Receiving openings of the six packed illustrations, in tile coordinates.
                PointF[] openings={new PointF(.50f,.60f),new PointF(.50f,.31f),new PointF(.50f,.25f),
                    new PointF(.52f,.53f),new PointF(.67f,.32f),new PointF(.54f,.43f)};
                PointF p=openings[(int)BinKind];return new PointF(Bin.X+p.X*BinSize,Bin.Y+p.Y*BinSize);
            }
        }
        internal void PlaceBin(Point point){Bin=Motion.Clamp(point,new Size(BinSize,BinSize),Area);}
        public int SpawnPixels { get { return Kind==OceanLitter.Net?OpenNetSize:LitterSize; } }
        public double LitterAngle(double now){return Stage<CleanupStage.Pickup?Math.Sin((now-Born)*1.3)*8:Stage==CleanupStage.Pickup?pickupAngle*(1-GatherProgress(now)):0;}
        public Point PropPosition(double now){
            PointF at=Litter;if(Stage==CleanupStage.Carry)at.Y+=(float)Math.Sin((now-Started)*4);
            Point p=Point.Round(at);int padding=(OpenNetSize-LitterPixels(now))/2;p.Offset(-padding,-padding);return p;
        }
        public double GatherProgress(double now){return Stage<CleanupStage.Pickup?0:Stage==CleanupStage.Pickup?Motion.Ease((now-Started)/1.2):1;}
        public int LitterPixels(double now) {
            double t=Math.Max(0,now-Started);
            if(Stage==CleanupStage.Pickup)return SpawnPixels-(int)((SpawnPixels-CargoSize)*Motion.Ease(t/1.2));
            if(Stage==CleanupStage.Deposit)return Math.Max(5,CargoSize-(int)((CargoSize-5)*Motion.Ease(t/1.4)));
            return Stage==CleanupStage.Carry?CargoSize:SpawnPixels;
        }
        PointF Drift(double now) {
            double t=now-Born;
            Point p=new Point(driftOrigin.X+(int)(Math.Sin(t*.22+phase)*90),driftOrigin.Y+(int)(Math.Sin(t*.4+phase)*25));
            return Motion.Clamp(p,new Size(SpawnPixels,SpawnPixels),Area);
        }
        public Point HoldOffset { get {
            // Use the same attachment as desktop-icon carrying. The whole upright
            // litter sprite stays visible, without item-specific bite positioning.
            Point p=PlayfulPlan.Attachment(Size,Grip,Right);p.Offset(-(CargoSize-32)/2,-(CargoSize-32)/2);return p;
        } }
        public PointF Hold { get { Point p=HoldOffset;return new PointF(Position.X+p.X,Position.Y+p.Y); } }
        void Enter(CleanupStage stage,double now){Stage=stage;Started=now;if(stage==CleanupStage.Courage)BinShown=now;}
        void PlanCarry(double now) {
            // Dock beside the bin, with space for the whole body. Only the cargo
            // travels into its opening; aligning the grip itself put Mochi inside it.
            Size window=Renderer.WindowSize(Size);Rectangle body=Renderer.SpriteRect(Size);
            Point end=Position;bool facing=Right;double best=Double.PositiveInfinity;
            for(int side=0;side<4;side++){
                Right=side==0 || (side>=2 && BinMouth.X>Position.X+window.Width/2);
                Point offset=HoldOffset;
                int x=side==0?Bin.X-12-body.Right:side==1?Bin.X+BinSize+12-body.Left:(int)BinMouth.X-window.Width/2;
                int y=side==2?Bin.Y-12-body.Bottom:side==3?Bin.Y+BinSize+12-body.Top:(int)BinMouth.Y-CargoSize/2-offset.Y;
                Point candidate=Motion.Clamp(new Point(x,y),window,Area);
                Rectangle occupied=body;occupied.Offset(candidate);occupied.Inflate(6,6);
                Rectangle overlap=Rectangle.Intersect(occupied,new Rectangle(Bin,new Size(BinSize,BinSize)));
                double score=overlap.Width*overlap.Height*10000.0+PlayfulPlan.Distance(Position,candidate)+(side>=2?200:0);
                if(score<best){best=score;end=candidate;facing=Right;}
            }
            Right=facing;
            Route=new SwimPath(Position,Renderer.WindowSize(Size),Area,random,false,end);
            Duration=Math.Max(.001,Route.Length/AverageSwimSpeed);Expires+=Duration;Enter(CleanupStage.Carry,now);Litter=Hold;
        }
        public void Pause(double seconds){
            seconds=Math.Max(0,seconds);Born+=seconds;Started+=seconds;Expires+=seconds;if(BinShown>=0)BinShown+=seconds;
        }
        public double Progress(double now){return Math.Max(0,Math.Min(1,(now-Started)/Duration));}
        // Share normal swimming's gentle launch/landing, keeping position and
        // heading on the same eased distance through each cleanup route.
        public double SwimDistance(double now){return Route.Length*SwimPath.Progress(Progress(now));}
        public bool Advance(double now) {
            if(now>Expires){Stage=CleanupStage.Finished;return false;}
            double t=Math.Max(0,now-Started);
            if(Stage<CleanupStage.Approach)Litter=Drift(now);
            if(Stage==CleanupStage.Drift && t>=3.5)Enter(CleanupStage.React,now);
            else if(Stage==CleanupStage.React && t>=4)Enter(CleanupStage.Courage,now);
            else if(Stage==CleanupStage.Courage && t>=3.5){
                Right=BinMouth.X>Litter.X;Point offset=HoldOffset;
                approachLitter=Litter;
                Point end=Motion.Clamp(new Point((int)approachLitter.X+(SpawnPixels-CargoSize)/2-offset.X,(int)approachLitter.Y+(SpawnPixels-CargoSize)/2-offset.Y),Renderer.WindowSize(Size),Area);
                Route=new SwimPath(Position,Renderer.WindowSize(Size),Area,random,false,end);
                // Preserve the faster journey time while easing both ends.
                // Long routes receive their full duration without a speedup cap.
                Duration=Math.Max(.001,Route.Length/AverageSwimSpeed);Expires+=Duration;Enter(CleanupStage.Approach,now);
            }else if(Stage==CleanupStage.Approach){
                Position=Point.Round(Route.PositionAtDistance(SwimDistance(now)));
                // Let the drifting litter settle where the grip will arrive. A
                // longer, normal-speed route must not chase an outdated forecast.
                Litter=Mix(Drift(now),approachLitter,SwimPath.Progress(Progress(now)));
                if(now>=Started+Duration){pickupFrom=Litter;pickupAngle=LitterAngle(now);Enter(CleanupStage.Pickup,now);}
            }else if(Stage==CleanupStage.Pickup){
                Litter=Mix(pickupFrom,Hold,Motion.Ease(t/1.2));
                if(t>=1.2)PlanCarry(now);
            }else if(Stage==CleanupStage.Carry){
                Position=Point.Round(Route.PositionAtDistance(SwimDistance(now)));Litter=Hold;
                if(now>=Started+Duration){depositFrom=Hold;Enter(CleanupStage.Deposit,now);}
            }else if(Stage==CleanupStage.Deposit){
                double p=Motion.Ease(t/1.4);PointF center=Mix(new PointF(depositFrom.X+CargoSize/2,depositFrom.Y+CargoSize/2),BinMouth,p);
                int pixels=LitterPixels(now);Litter=new PointF(center.X-pixels/2f,center.Y-pixels/2f-(float)(Math.Sin(p*Math.PI)*22));
                if(t>=1.4)Enter(CleanupStage.Celebrate,now);
            }else if(Stage==CleanupStage.Celebrate && t>=3)Stage=CleanupStage.Finished;
            return Stage!=CleanupStage.Finished;
        }
        static PointF Mix(PointF a,PointF b,double p){return new PointF((float)(a.X+(b.X-a.X)*p),(float)(a.Y+(b.Y-a.Y)*p));}
        public string Speech(double now) {
            if(Stage==CleanupStage.Drift)return "";
            if(Stage==CleanupStage.React)return Emotion==0?"Hey! My ocean isn't\na rubbish drawer!":Emotion==1?"This doesn't belong\nwith my ocean friends.":"Someone's home\nisn't a place for this...";
            if(Stage==CleanupStage.Courage)return new[]{"I'm small. But I can\nmake a little difference.","Deep breath. Brave fins.\nI've got this.","Feeling sad means I care.\nNow let's do something."}[voice];
            if(Stage==CleanupStage.Approach)return Kind==OceanLitter.Net?"Easy now... no little\nfriends left tangled!":"Hang on, ocean.\nI'm on my way!";
            if(Stage==CleanupStage.Pickup && Kind==OceanLitter.Net)return Grip==MischiefKind.GentleBite?"Gather the loose ends...\nOne gentle grip.":"Fold it with care...\nNo tangles left behind.";
            if(Stage==CleanupStage.Pickup)return Grip==MischiefKind.GentleBite?"Gentle grip.\nDefinitely not a snack!":"A little fin hug...\njust for the journey.";
            if(Stage==CleanupStage.Carry)return new[]{"One less piece adrift.\nOne safer swim.","Precious cargo:\na cleaner tomorrow.","Tiny fins. Big feelings.\nStill swimming."}[voice];
            if(Stage==CleanupStage.Deposit)return "Into the Ocean Bin.\nThe sea can breathe.";
            if(Stage==CleanupStage.Celebrate)return Kind==OceanLitter.Bag?"A safer swim for turtles.":
                Kind==OceanLitter.Net?"One less net. More free fins.":
                Kind==OceanLitter.Bottle?"A little cleaner. A little safer.":"That's better, ocean.";
            return "";
        }
    }

    // Separate from the legacy sad/bowed-head and playful atlas loops. Each
    // reaction uses complete, head-registered poses and keeps a single opaque body.
    static class CleanupReaction {
        public static int Frame(int emotion,double elapsed) {
            double t=Math.Max(0,Math.Min(4,elapsed));
            if(emotion==2){
                // One unhurried blink; never rotate, lower, or enlarge the head.
                if(t<1.55)return 0;
                if(t<1.67)return 1;
                if(t<1.83)return 2;
                if(t<1.95)return 1;
                if(t<2.10)return 3;
                return 0;
            }
            double phase=t%2;
            if(emotion==0)return phase<.35?0:phase<.55?1:phase<1.0?2:phase<1.20?1:phase<1.45?3:0;
            return phase<.40?0:phase<.70?1:phase<1.0?2:phase<1.30?3:0;
        }
        public static Bitmap Draw(Atlas atlas,int emotion,double elapsed,int size,bool right,string speech) {
            double t=Math.Max(0,Math.Min(4,elapsed));
            double envelope=Math.Pow(Math.Sin(Math.PI*t/4),2);
            // Sadness stays level at exactly the normal rendering scale. The other
            // two reactions breathe between their actual illustrated fin gestures.
            float breath=emotion==2?0:(float)(Math.Sin(t*Math.PI*2/2.4)*envelope);
            float sway=emotion==2?0:(float)(Math.Sin(t*Math.PI*2/2)*envelope);
            Size window=Renderer.WindowSize(size);Rectangle body=Renderer.SpriteRect(size);
            Bitmap result=new Bitmap(window.Width,window.Height,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(result)){
                g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                GraphicsState state=g.Save();
                if(right){g.TranslateTransform(window.Width,0);g.ScaleTransform(-1,1);}
                g.TranslateTransform(body.Left+body.Width/2f+sway*.6f,body.Top+body.Height/2f+breath*.7f);
                g.ScaleTransform(1+breath*.003f,1-breath*.002f);
                g.DrawImage(atlas.Cleanup[emotion,Frame(emotion,t)],new RectangleF(-body.Width/2f,-body.Height/2f,body.Width,body.Height));
                g.Restore(state);Renderer.DrawBubble(g,window,speech);
            }
            return result;
        }
    }

    static class CleanupArt {
        public static Rectangle SceneBounds(CleanupTrip trip,double now){
            // Keep the native surface stationary for the entire trip. Moving or
            // resizing it to follow the union of the objects can clip old/new
            // pixels while the desktop compositor updates its window shape.
            return trip.Area;
        }
        public static Bitmap Scene(Atlas atlas,CleanupTrip trip,double now,Bitmap pet,Rectangle bounds,Bitmap buffer=null){
            // One bitmap and one UpdateLayeredWindow call publish all three
            // objects together. No overlapping HWND can erase, cover or lag
            // behind another during pickup, crossing the bin, or disposal.
            Bitmap result=buffer??new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb);
            try{
                using(Graphics g=Graphics.FromImage(result)){
                    g.Clear(Color.Transparent);
                    if(trip.Stage>=CleanupStage.Courage)using(Bitmap bin=Bin(atlas,trip.BinKind,CleanupTrip.BinSize,now-trip.BinShown))
                        g.DrawImageUnscaled(bin,trip.Bin.X-bounds.X,trip.Bin.Y-bounds.Y);
                    g.DrawImageUnscaled(pet,trip.Position.X-bounds.X,trip.Position.Y-bounds.Y);
                    if(trip.Stage<CleanupStage.Celebrate)using(Bitmap litter=Prop(atlas,trip,now)){
                        Point at=trip.PropPosition(now);g.DrawImageUnscaled(litter,at.X-bounds.X,at.Y-bounds.Y);
                    }
                }
                return result;
            }catch{if(buffer==null)result.Dispose();throw;}
        }
        public static Bitmap Prop(Atlas atlas,CleanupTrip trip,double now){
            // Stable drawing tile for all litter poses; this is composited into
            // the scene, never presented as an independent native window.
            Bitmap result=new Bitmap(CleanupTrip.OpenNetSize,CleanupTrip.OpenNetSize,PixelFormat.Format32bppArgb);
            int size=trip.LitterPixels(now),padding=(CleanupTrip.OpenNetSize-size)/2;
            using(Bitmap litter=Litter(atlas,trip.Kind,trip.Variant,size,trip.LitterAngle(now),now-trip.Born,trip.GatherProgress(now)))
            using(Graphics g=Graphics.FromImage(result)){
                if(trip.Stage==CleanupStage.Deposit)DrawOpacity(g,litter,new Rectangle(padding,padding,size,size),(float)Math.Max(0,1-(now-trip.Started)/1.4));
                else g.DrawImageUnscaled(litter,padding,padding);
            }
            return result;
        }
        // Compact illustrated sprites have a light rim and dark contour, so they
        // remain legible over both light and dark desktop backgrounds.
        public static Bitmap Litter(Atlas atlas,OceanLitter kind,int variant,int size,double angle=0,double reveal=-1,double gathered=1) {
            Bitmap b=new Bitmap(size,size,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(b)){
                g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                DrawArrival(g,size,reveal);
                g.TranslateTransform(size/2f,size/2f);g.RotateTransform((float)angle);
                RectangleF target=new RectangleF(-size/2f,-size/2f,size,size);
                if(kind==OceanLitter.Net && gathered<1){
                    gathered=Math.Max(0,gathered);
                    // Narrow the open mesh toward its middle, then crossfade into
                    // the matching gathered bundle as the pickup closes the grip.
                    float width=(float)(size*(1-.45*gathered));
                    DrawOpacity(g,atlas.OpenNets[variant],new RectangleF(-width/2,-size/2f,width,size),(float)(1-gathered));
                    if(gathered>0)DrawOpacity(g,atlas.Litter[(int)kind,variant],target,(float)gathered);
                }else g.DrawImage(atlas.Litter[(int)kind,variant],target);
            }return b;
        }
        static void DrawOpacity(Graphics g,Bitmap image,RectangleF target,float alpha){
            using(ImageAttributes attributes=new ImageAttributes()){
                ColorMatrix matrix=new ColorMatrix();matrix.Matrix33=alpha;attributes.SetColorMatrix(matrix);
                g.DrawImage(image,Rectangle.Round(target),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);
            }
        }
        public static Bitmap Bin(Atlas atlas,OceanBin kind,int size,double reveal=-1) {
            Bitmap b=new Bitmap(size,size,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(b)){
                g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                DrawArrival(g,size,reveal);g.DrawImage(atlas.OceanBins[(int)kind],new Rectangle(0,0,size,size));
            }return b;
        }
        static void DrawArrival(Graphics g,int size,double elapsed) {
            if(elapsed<0 || elapsed>=2.4)return;
            double wave=(elapsed%1.2)/1.2,fade=(1-wave)*(1-elapsed/2.4);
            float radius=(float)(size*(.30+.17*wave)),center=size/2f;
            using(Pen edge=new Pen(Color.FromArgb((int)(170*fade),26,118,155),3))
            using(Pen light=new Pen(Color.FromArgb((int)(235*fade),220,255,255),1.5f)){
                g.DrawEllipse(edge,center-radius,center-radius,radius*2,radius*2);
                g.DrawEllipse(light,center-radius,center-radius,radius*2,radius*2);
            }
        }
        public static Bitmap Pet(Atlas atlas,CleanupTrip trip,double now) {
            double t=now-trip.Started;int size=trip.Size;
            if(trip.Stage==CleanupStage.Approach){PointF h=trip.Route.HeadingAtDistance(trip.SwimDistance(now));return GazeRenderer.Draw(atlas,new GazePose{Direction=Motion.Direction(h.X,h.Y),Time=t},size,trip.Speech(now),true);}
            if(trip.Stage==CleanupStage.React)return CleanupReaction.Draw(atlas,trip.Emotion,t,size,trip.Right,trip.Speech(now));
            if(trip.Stage<CleanupStage.Pickup || trip.Stage>=CleanupStage.Celebrate){
                int row=trip.Stage==CleanupStage.Celebrate?6:0;
                return Renderer.Draw(atlas,row,Atlas.FrameAt(row,t*1000),size,trip.Speech(now),trip.Right);
            }
            Size window=Renderer.WindowSize(size);Bitmap result=new Bitmap(window.Width,window.Height,PixelFormat.Format32bppArgb);
            int frame=trip.Stage==CleanupStage.Pickup?Math.Min(3,(int)(t/.3)):trip.Stage==CleanupStage.Deposit?Math.Min(7,4+(int)(t/.35)):3+(int)(t/.6)%2;
            // Match the desktop-icon pickup: open wide, then close into the
            // gentle carrying grip. The complete litter stays visible on top.
            Bitmap sprite=trip.Grip==MischiefKind.FinGrab?atlas.Illustrated[1,frame]:atlas.Feeding[0,trip.Stage==CleanupStage.Pickup && t<.65?1:trip.Stage==CleanupStage.Deposit?3:2];
            using(Graphics g=Graphics.FromImage(result)){
                g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                Rectangle body=Renderer.SpriteRect(size);float bob=(float)Math.Sin(t*4);
                PlayfulRenderer.DrawBody(g,sprite,body,window,trip.Right,bob,0,false,trip.Grip);
                Renderer.DrawBubble(g,window,trip.Speech(now));
            }return result;
        }
    }

    sealed partial class Companion {
        CleanupTrip cleanup;Bitmap cleanupHitFrame,cleanupBackBuffer;Point cleanupHitPosition;
        double nextCleanupInputAttempt;
        double cleanupMenuOpenedAt=-1;
        void PlaceBackgroundWindows(IntPtr pet){
            Native.KeepBackgroundGroup(pet);
        }
        double nextCleanup,cleanupLastTick;int lastLitter=-1,lastOceanBin=-1;
        readonly int[] lastLitterVariant={-1,-1,-1,-1};Point? lastOceanBinPosition;
        void ScheduleCleanup(){nextCleanup=prefs.OceanCleanup?Now+random.Next(240,421):Double.PositiveInfinity;}
        bool BeginCleanup(bool requested=false){
            if(down || modal || manualSwim!=null)return false;
            if(requested)CancelInteraction();
            Rectangle area=Screen.FromRectangle(PetBounds).WorkingArea;
            if(area.Width<PetSize.Width || area.Height<PetSize.Height){ScheduleCleanup();return false;}
            CancelRegularAnimation();gaze.Reset();swimming=false;guidedSwim=false;startupHintAt=-1;bubble="";
            int kind=random.Next(lastLitter<0?4:3);if(lastLitter>=0 && kind>=lastLitter)kind++;lastLitter=kind;
            int variant=random.Next(lastLitterVariant[kind]<0?3:2);if(lastLitterVariant[kind]>=0 && variant>=lastLitterVariant[kind])variant++;
            int bin=random.Next(lastOceanBin<0?6:5);if(lastOceanBin>=0 && bin>=lastOceanBin)bin++;
            cleanup=new CleanupTrip(PetLocation,prefs.Size,area,random,(OceanLitter)kind,random.Next(3),Now,variant,bin,lastOceanBinPosition);
            lastLitterVariant[kind]=variant;lastOceanBin=bin;lastOceanBinPosition=cleanup.Bin;
            cleanupLastTick=Now;Render();return true;
        }
        void CancelCleanup(){
            if(cleanup==null)return;
            Point position=cleanup.Position;
            cleanup=null;cleanupMenuOpenedAt=-1;
            if(cleanupBackBuffer!=null){cleanupBackBuffer.Dispose();cleanupBackBuffer=null;}
            UpdateCleanupInput();
            // Change model coordinates now; publish native bounds and replacement
            // pixels together in Render. A failed upload retains the old scene
            // and its matching hit mask until that same transition can retry.
            PetLocation=position;
            row=frame=0;actionStart=Now;actionUntil=0;ScheduleCleanup();Schedule();PauseAutomaticActivities();edgeWatch.Reset(Now);
            if(!closing && !resourcesDisposed)Render(); // Settings can pause ticks immediately after cancellation.
        }
        bool AdvanceCleanup(double now){
            if(cleanup==null)return false;
            if(now-cleanupLastTick>2){CancelCleanup();Render();return true;}
            cleanupLastTick=now;
            bool active=cleanup.Advance(now);
            PetLocation=cleanup.Position;Face(cleanup.Right);
            if(!active){CancelCleanup();Save();Render(now);}
            else PresentCleanup(now);
            return true;
        }
        void PresentCleanup(double now){
            Render(now);RefreshBackgroundIfDue();
        }
        bool CleanupHit(Point screen){
            if(cleanupHitFrame==null)return false;
            Point p=new Point(screen.X-cleanupHitPosition.X,screen.Y-cleanupHitPosition.Y);
            return p.X>=0 && p.Y>=0 && p.X<cleanupHitFrame.Width && p.Y<cleanupHitFrame.Height && cleanupHitFrame.GetPixel(p.X,p.Y).A>=20;
        }
        void UpdateCleanupInput(){
            if(!IsHandleCreated || Now<nextCleanupInputAttempt)return;
            // HTTRANSPARENT alone only searches windows on the same UI thread.
            // Layered WS_EX_TRANSPARENT also lets the actual desktop/other apps
            // receive input. Re-evaluate at the timer's normal 15ms cadence so
            // hovering Mochi restores his ordinary interactive window promptly.
            try{Native.SetClickThrough(Handle,cleanupHitFrame!=null && !down && !menu.Visible && !CleanupHit(Cursor.Position));nextCleanupInputAttempt=0;}
            catch(System.ComponentModel.Win32Exception){nextCleanupInputAttempt=Now+NativeRetryDelay;}
        }
    }
}
