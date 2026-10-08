using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace MochiDesktop {
    static class Destination {
        public static Point Place(Point click,int size,Rectangle workArea){
            Rectangle body=Renderer.SpriteRect(size);
            return Motion.Clamp(new Point(click.X-body.Left-body.Width/2,click.Y-body.Top-body.Height/2),Renderer.WindowSize(size),workArea);
        }
    }

    sealed class DestinationPicker : IDisposable {
        readonly List<DestinationOverlay> overlays=new List<DestinationOverlay>();
        readonly Action<Point?> completed;readonly Atlas atlas;readonly bool faceRight;
        bool disposed,finished,shown;
        public DestinationPicker(Atlas art,bool right,Action<Point?> callback){atlas=art;faceRight=right;completed=callback;}
        public void Show(){
            DestinationOverlay active=null;
            foreach(Screen screen in Screen.AllScreens){
                DestinationOverlay overlay=new DestinationOverlay(screen.Bounds,atlas,faceRight,Finish);
                overlay.Deactivate+=delegate{CheckFocusLater(overlay);};
                overlay.FormClosed+=delegate{if(!disposed)Finish(null);};
                overlays.Add(overlay);overlay.Show();
                if(screen.Bounds.Contains(Cursor.Position))active=overlay;
            }
            if(active==null)active=overlays[0];active.Activate();shown=true;
        }
        void CheckFocusLater(DestinationOverlay source){
            if(!shown || disposed || !source.IsHandleCreated)return;
            try{source.BeginInvoke((Action)delegate{
                if(disposed || !shown)return;
                foreach(DestinationOverlay overlay in overlays)if(overlay.ContainsFocus)return;
                Finish(null);
            });}catch(InvalidOperationException){}
        }
        void Finish(Point? point){if(finished || disposed)return;finished=true;Dispose();completed(point);}
        public void Dispose(){if(disposed)return;disposed=true;shown=false;foreach(DestinationOverlay overlay in overlays){overlay.Close();overlay.Dispose();}overlays.Clear();}
    }

    sealed class DestinationOverlay : Form {
        readonly Atlas atlas;readonly bool faceRight;readonly Action<Point?> completed;
        MouseButtons pressed=MouseButtons.None;bool finished;
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams {get{CreateParams p=base.CreateParams;p.ExStyle|=0x80000|0x80;return p;}}
        public DestinationOverlay(Rectangle screen,Atlas art,bool right,Action<Point?> callback){
            atlas=art;faceRight=right;completed=callback;Text="Pick a spot for Mochi";
            FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;Bounds=screen;
            Cursor=Cursors.Cross;KeyPreview=true;SetStyle(ControlStyles.StandardDoubleClick,false);
        }
        protected override void OnShown(EventArgs e){base.OnShown(e);using(Bitmap page=DrawOverlay(Size,atlas,faceRight))Native.SetBitmap(Handle,page,Location);}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);pressed=e.Button;}
        protected override void OnMouseUp(MouseEventArgs e){
            base.OnMouseUp(e);if(finished || pressed!=e.Button || (e.Button!=MouseButtons.Left && e.Button!=MouseButtons.Right))return;
            pressed=MouseButtons.None;finished=true;
            // Consume both halves of the selection click before removing the overlay.
            Point? choice=e.Button==MouseButtons.Left?(Point?)PointToScreen(e.Location):null;
            completed(choice);
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
            if(keyData==Keys.Escape){if(!finished){finished=true;completed(null);}return true;}
            return base.ProcessCmdKey(ref msg,keyData);
        }
        public static void VerifyInput(Atlas atlas){
            int calls=0;Point? chosen=null;
            using(DestinationOverlay form=new DestinationOverlay(new Rectangle(-900,100,1000,700),atlas,false,delegate(Point? p){calls++;chosen=p;})){
                form.OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,300,300,0));
                if(calls!=0)throw new Exception("Opening menu release selected a destination");
                form.OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,300,300,0));form.OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,300,300,0));
                form.OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,400,400,0));
                if(calls!=1 || chosen!=new Point(-600,400))throw new Exception("Picker selected twice or used wrong screen coordinates");
            }
            foreach(bool escape in new[]{false,true}){
                calls=0;chosen=Point.Empty;
                using(DestinationOverlay form=new DestinationOverlay(new Rectangle(0,0,1000,700),atlas,true,delegate(Point? p){calls++;chosen=p;})){
                    if(escape){Message message=new Message();form.ProcessCmdKey(ref message,Keys.Escape);form.ProcessCmdKey(ref message,Keys.Escape);}
                    else {form.OnMouseDown(new MouseEventArgs(MouseButtons.Right,1,50,50,0));form.OnMouseUp(new MouseEventArgs(MouseButtons.Right,1,50,50,0));}
                    if(calls!=1 || chosen.HasValue)throw new Exception("Escape/right-click must cancel exactly once");
                }
            }
        }
        public static Bitmap DrawOverlay(Size size,Atlas atlas,bool right,string heading="Where should I swim?",string instructions="Click a spot and I'll swim over!",string exitHint="Esc or right-click to cancel"){
            Bitmap page=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(page)){
                // A nonzero alpha across the screen receives the click without passing it to an app.
                g.Clear(Color.FromArgb(22,10,39,56));g.SmoothingMode=SmoothingMode.AntiAlias;
                int width=Math.Min(500,size.Width-24),height=116,x=(size.Width-width)/2,y=Math.Min(28,Math.Max(0,size.Height-height));
                RectangleF card=new RectangleF(x,y,width,height);
                using(GraphicsPath shape=new GraphicsPath()){
                    float d=26;shape.AddArc(card.Left,card.Top,d,d,180,90);shape.AddArc(card.Right-d,card.Top,d,d,270,90);shape.AddArc(card.Right-d,card.Bottom-d,d,d,0,90);shape.AddArc(card.Left,card.Bottom-d,d,d,90,90);shape.CloseFigure();
                    using(Brush fill=new SolidBrush(Color.FromArgb(250,243,251,255)))using(Pen border=new Pen(Color.FromArgb(255,128,186,211),1)){g.FillPath(fill,shape);g.DrawPath(border,shape);}
                }
                GraphicsState pose=g.Save();g.TranslateTransform(x+58,y+59);if(right)g.ScaleTransform(-1,1);g.DrawImage(atlas.Frames[0,0],new Rectangle(-45,-49,90,98));g.Restore(pose);
                using(Font title=new Font("Segoe UI Semibold",19,FontStyle.Regular,GraphicsUnit.Pixel))
                using(Font text=new Font("Segoe UI",15,FontStyle.Regular,GraphicsUnit.Pixel))
                using(Font hint=new Font("Segoe UI",13,FontStyle.Regular,GraphicsUnit.Pixel))
                using(Brush ink=new SolidBrush(Color.FromArgb(29,64,83)))using(Brush quiet=new SolidBrush(Color.FromArgb(78,111,128))){
                    g.DrawString(heading,title,ink,new RectangleF(x+111,y+16,width-125,29));
                    g.DrawString(instructions,text,ink,new RectangleF(x+111,y+49,width-125,26));
                    g.DrawString(exitHint,hint,quiet,new RectangleF(x+111,y+81,width-125,24));
                }
            }
            return page;
        }
    }

    sealed partial class Companion {
        const string DestinationPrompt="Pick a spot!\nI'll swim over to you.";
        static readonly string[] SwimOverLines={
            "On my way!",
            "Tiny fins,\nbig mission!",
            "Bubble delivery!\nComing through!",
            "Next stop:\nright by you!",
            "Wiggle engines:\nfull speed!",
            "Your shark taxi\nis on the way!",
            "Just keep swimming...\nto you!",
            "Taking the scenic\nsplash route!"
        };
        static readonly string[] ArrivalLines={
            "Made it!\nYour tiny swim buddy.",
            "Special delivery:\none happy shark!",
            "Parked with\nporpoise!",
            "Your daily dose\nof vitamin sea!",
            "Boop! I'm here!",
            "This spot is\nfin-tastic!",
            "One small swim,\none big hello!",
            "Room for a little\nwhale shark?"
        };
        int lastSwimOverLine=-1,lastArrivalLine=-1;
        bool choosingDestination,guidedSwim;Rectangle swimBounds;
        DestinationPicker destinationPicker;
        void StopPicking(){
            if(!choosingDestination && destinationPicker==null)return;
            choosingDestination=false;DestinationPicker picker=destinationPicker;destinationPicker=null;
            if(picker!=null)picker.Dispose();bubble="";bubbleUntil=0;actionUntil=0;row=0;
        }
        void ChooseDestination(){
            CancelInteraction();down=false;dragging=false;Capture=false;Act(0,DestinationPrompt,4);
            choosingDestination=true;bubbleUntil=Double.PositiveInfinity;actionUntil=Double.PositiveInfinity;Render();
            if(simulation)return;
            try{destinationPicker=new DestinationPicker(atlas,faceRight,CompleteDestination);destinationPicker.Show();}
            catch{StopPicking();Act(0,"Please try again.\nMy map got soggy!",3);}
        }
        void CompleteDestination(Point? click){
            if(!choosingDestination)return;StopPicking();edgeWatch.Reset(Now);
            if(!click.HasValue){Act(0,"No rush.\nI'll wait right here.",2.5);return;}
            Point point=click.Value;Rectangle destinationArea=Screen.FromPoint(point).WorkingArea;
            Point target=Destination.Place(point,prefs.Size,destinationArea);
            Rectangle routeArea=Rectangle.Union(Screen.FromRectangle(Bounds).WorkingArea,destinationArea);
            BeginDestinationSwim(target,routeArea);
        }
        void BeginDestinationSwim(Point target,Rectangle routeArea){
            StopManualSwimming();
            CancelPlayful();
            CancelRegularAnimation();
            StopPicking();startupHintAt=-1;gaze.Reset();feeding=null;reaction=null;down=false;dragging=false;
            SwimPath path=new SwimPath(Location,Size,routeArea,random,false,target);
            if(path.Length<.5){Act(0,"I'm already here!",2.5);return;}
            StartSwimPath(path,routeArea,true);
        }
        void StartSwimPath(SwimPath path,Rectangle bounds,bool guided){
            swimPath=path;swimBounds=bounds;guidedSwim=guided;
            swimFrom=Location;swimTo=path.End;travelStart=Now;
            travelDuration=guided?Math.Max(.8,Math.Min(12,path.Length/130+1)):path.Duration;swimFraction=PointF.Empty;
            FaceTravel(swimTo.X-swimFrom.X);swimGaze.Reset();PointF heading=path.Heading(0);swimGaze.Update(heading.X,heading.Y,Now);
            row=faceRight?1:2;actionStart=Now;actionUntil=0;swimming=true;bubble=guided?PickLine(SwimOverLines,ref lastSwimOverLine):"";bubbleUntil=Now+3;
            if(guided)edgeWatch.Reset(Now);Render();
        }
        public static void VerifyDestination(){
            Point away=new Point(-10000,-10000);
            using(Companion pet=new Companion(true)){
                pet.prefs.Roam=false;Rectangle area=Screen.PrimaryScreen.WorkingArea;
                Point start=new Point(area.Left+(area.Width-pet.Width)/2,area.Top+(area.Height-pet.Height)/2);pet.Location=start;
                int menuCount=0;foreach(ToolStripItem item in pet.menu.Items)if(item.Text=="Come here")menuCount++;
                if(menuCount!=1)throw new Exception("Come here menu action missing or duplicated");
                pet.Pet();pet.ChooseDestination();
                if(!pet.choosingDestination || pet.reaction!=null || pet.swimming || pet.bubble!=DestinationPrompt)throw new Exception("Destination prompt did not pause other activities");
                pet.nextIdleActivity=pet.Now;double pending=pet.nextIdleActivity;pet.nextSwim=pet.Now;pet.Advance(pet.Now+600,away);
                if(!pet.choosingDestination || pet.Location!=start || pet.swimming || pet.reaction!=null || pet.feeding!=null || pet.bubble!=DestinationPrompt)throw new Exception("Destination selection was interrupted");
                pet.CompleteDestination(null);
                if(pet.choosingDestination || pet.swimming || pet.Location!=start || pet.nextIdleActivity!=pending || pet.automaticReadyAt<pet.Now+3)throw new Exception("Destination cancellation changed position, erased a pending turn, or skipped the handoff pause");
                foreach(Point click in new[]{new Point(area.Left+1,area.Top+1),new Point(area.Right-1,area.Bottom-1),new Point(area.Left+area.Width/2,area.Top+area.Height/2)}){
                    pet.Location=start;pet.ChooseDestination();int last=pet.lastSurprise;pet.CompleteDestination(click);
                    Point target=Destination.Place(click,pet.prefs.Size,area);
                    if(!pet.swimming || !pet.guidedSwim || pet.Location!=start || pet.swimTo!=target || pet.prefs.Roam)throw new Exception("Requested swim teleported, ignored target, or changed roaming preference");
                    double end=pet.travelStart+pet.travelDuration;pet.Advance(pet.travelStart+pet.travelDuration*.5,click);
                    if(!pet.swimming || pet.Location==start || pet.Location==target || pet.lastSurprise!=last)throw new Exception("Destination swim did not animate or triggered a click reaction");
                    pet.Advance(end,away);
                    if(pet.swimming || pet.Location!=target || !area.Contains(pet.Bounds) || pet.lastSurprise!=last)throw new Exception("Destination swim did not arrive safely");
                    if(target.X!=start.X && pet.faceRight!=(target.X>start.X))throw new Exception("Destination swim lost facing");
                }
                pet.ChooseDestination();pet.StartFeeding(FeedKind.SnackToss,false);
                if(pet.choosingDestination || pet.feeding==null)throw new Exception("Manual reaction failed to cancel picker");
                pet.ChooseDestination();pet.CancelInteraction();if(pet.choosingDestination)throw new Exception("Controls failed to cancel picker");
                Point crossStart=new Point(-1300,200),crossEnd=new Point(900,500);Rectangle crossArea=new Rectangle(-1920,-200,3840,1400);
                pet.Location=crossStart;pet.BeginDestinationSwim(crossEnd,crossArea);
                pet.Advance(pet.travelStart+pet.travelDuration*.25,away);
                if(pet.Left>=0)throw new Exception("Cross-monitor swim jumped to the current monitor");
                pet.Advance(pet.travelStart+pet.travelDuration,away);
                if(pet.Location!=crossEnd || pet.swimming)throw new Exception("Cross-monitor swim did not finish at destination");
            }
            Random random=new Random(212);
            foreach(Rectangle area in new[]{new Rectangle(0,0,1920,1040),new Rectangle(-1600,-300,1600,1200),new Rectangle(1920,-200,1080,1880)})foreach(int size in new[]{144,176,224}){
                Size window=Renderer.WindowSize(size);
                for(int i=0;i<100;i++){
                    Point click=new Point(random.Next(area.Left-100,area.Right+100),random.Next(area.Top-100,area.Bottom+100));Point target=Destination.Place(click,size,area);
                    if(!area.Contains(new Rectangle(target,window)))throw new Exception("Destination placement outside work area");
                    Point from=new Point(area.Left+(area.Width-window.Width)/2,area.Top+(area.Height-window.Height)/2);
                    SwimPath route=new SwimPath(from,window,area,random,false,target);
                    if(Point.Round(route.Position(0))!=from || Point.Round(route.Position(1))!=target)throw new Exception("Explicit route endpoints");
                    for(int n=0;n<=100;n++){PointF p=route.Position(n/100.0);if(p.X<area.Left-.01 || p.Y<area.Top-.01 || p.X+window.Width>area.Right+.01 || p.Y+window.Height>area.Bottom+.01)throw new Exception("Explicit route leaves monitor");}
                }
            }
            Rectangle union=new Rectangle(-1920,-200,3840,1400);Size medium=Renderer.WindowSize(176);Point a=new Point(-1300,200),b=new Point(900,500);
            SwimPath cross=new SwimPath(a,medium,union,random,false,b);
            if(Point.Round(cross.Position(0))!=a || Point.Round(cross.Position(1))!=b)throw new Exception("Cross-monitor route endpoints");
            using(Atlas atlas=new Atlas())using(Bitmap overlay=DestinationOverlay.DrawOverlay(new Size(1000,700),atlas,false)){
                if(overlay.GetPixel(1,699).A==0 || overlay.GetPixel(500,55).A<240)throw new Exception("Picker click shield or prompt missing");
                DestinationOverlay.VerifyInput(atlas);
            }
        }
    }
}
