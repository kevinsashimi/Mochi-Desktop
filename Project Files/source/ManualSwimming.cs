using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MochiDesktop {
    sealed class ManualSwimInput {
        readonly HashSet<Keys> held=new HashSet<Keys>();
        bool spaceHeld,boostPressed;
        public static bool Accepts(Keys key){
            return key==Keys.W || key==Keys.A || key==Keys.S || key==Keys.D ||
                key==Keys.Up || key==Keys.Left || key==Keys.Down || key==Keys.Right || key==Keys.Space;
        }
        public void Set(Keys key,bool pressed){
            if(key==Keys.Space){if(pressed && !spaceHeld)boostPressed=true;spaceHeld=pressed;return;}
            if(!Accepts(key))return;if(pressed)held.Add(key);else held.Remove(key);
        }
        public bool TakeBoostPress(){bool result=boostPressed;boostPressed=false;return result;}
        public void Clear(){held.Clear();spaceHeld=boostPressed=false;}
        public bool HasControls {get{return held.Count>0 || spaceHeld;}}
        public PointF Direction {
            get {
                int x=(held.Contains(Keys.D)||held.Contains(Keys.Right)?1:0)-(held.Contains(Keys.A)||held.Contains(Keys.Left)?1:0);
                int y=(held.Contains(Keys.S)||held.Contains(Keys.Down)?1:0)-(held.Contains(Keys.W)||held.Contains(Keys.Up)?1:0);
                double length=Math.Sqrt(x*x+y*y);
                return length==0?PointF.Empty:new PointF((float)(x/length),(float)(y/length));
            }
        }
    }

    sealed class ManualSwimMotion {
        public readonly ManualSwimInput Input=new ManualSwimInput();
        public PointF Position {get;private set;}
        public PointF Velocity {get;private set;}
        public PointF LastHeading {get;private set;}
        public double BoostUntil {get;private set;}
        public double BoostReadyAt {get;private set;}
        PointF boostHeading;
        readonly Rectangle area;Rectangle body;double last;
        public Rectangle Area {get{return area;}}
        const double Speed=240,Response=.10;
        public const double BoostDuration=.34,BoostCooldown=5;
        public ManualSwimMotion(Point start,Size size,Rectangle bounds,double now,bool right=true,double readyAt=0){
            body=new Rectangle(Point.Empty,size);area=bounds;Position=Motion.Clamp(start,size,bounds);last=now;
            LastHeading=new PointF(right?1:-1,0);BoostReadyAt=readyAt;BoostUntil=Double.NegativeInfinity;
        }
        public bool TryBoost(double now){
            if(now<BoostReadyAt)return false;
            PointF direction=Input.Direction;if(direction==PointF.Empty)direction=LastHeading;
            double left=area.Left-body.Left,top=area.Top-body.Top,right=Math.Max(left,area.Right-body.Right),bottom=Math.Max(top,area.Bottom-body.Bottom);
            bool canMove=direction.X>0 && Position.X<right-.5 || direction.X<0 && Position.X>left+.5 ||
                direction.Y>0 && Position.Y<bottom-.5 || direction.Y<0 && Position.Y>top+.5;
            if(!canMove)return false; // A wall does not waste a charge.
            boostHeading=LastHeading=direction;BoostUntil=now+BoostDuration;BoostReadyAt=now+BoostCooldown;
            Velocity=new PointF((float)(direction.X*Speed*1.6),(float)(direction.Y*Speed*1.6));last=now;return true;
        }
        public void SetBodyBounds(Rectangle visible){
            if(visible.Width<=0 || visible.Height<=0)throw new ArgumentException("The swimming body cannot be empty.");
            double x=Position.X,y=Position.Y;PointF direction=Input.Direction;
            // Keep a held edge aligned when fins breathe or a boost settles into
            // another pose. Changing the transparent margin must not open a gap.
            if(direction.X<0 && x<=area.Left-body.Left+.5)x=area.Left-visible.Left;
            else if(direction.X>0 && x>=area.Right-body.Right-.5)x=area.Right-visible.Right;
            if(direction.Y<0 && y<=area.Top-body.Top+.5)y=area.Top-visible.Top;
            else if(direction.Y>0 && y>=area.Bottom-body.Bottom-.5)y=area.Bottom-visible.Bottom;
            body=visible;Constrain(x,y,Velocity.X,Velocity.Y);
        }
        public void Advance(double now){
            // A delayed timer must not launch Mochi across the screen on resume.
            double dt=Math.Max(0,Math.Min(.05,now-last));last=now;
            if(dt==0)return;
            PointF direction=Input.Direction;if(direction!=PointF.Empty)LastHeading=direction;
            double boosted=Math.Max(0,Math.Min(dt,BoostUntil-(now-dt)));
            if(boosted>0)Step(direction==PointF.Empty?boostHeading:direction,Speed*3,boosted);
            if(boosted<dt)Step(direction,Speed,dt-boosted);
        }
        void Step(PointF direction,double speed,double dt){
            double decay=Math.Exp(-dt/Response);
            double tx=direction.X*speed,ty=direction.Y*speed;
            double x=Position.X+tx*dt+(Velocity.X-tx)*Response*(1-decay);
            double y=Position.Y+ty*dt+(Velocity.Y-ty)*Response*(1-decay);
            double vx=tx+(Velocity.X-tx)*decay,vy=ty+(Velocity.Y-ty)*decay;
            if(direction==PointF.Empty && vx*vx+vy*vy<1){vx=vy=0;}
            Constrain(x,y,vx,vy);
        }
        void Constrain(double x,double y,double vx,double vy){
            double left=area.Left-body.Left,top=area.Top-body.Top,right=Math.Max(left,area.Right-body.Right),bottom=Math.Max(top,area.Bottom-body.Bottom);
            if(x<=left || x>=right){x=Math.Max(left,Math.Min(right,x));if(x<=left && vx<0 || x>=right && vx>0)vx=0;}
            if(y<=top || y>=bottom){y=Math.Max(top,Math.Min(bottom,y));if(y<=top && vy<0 || y>=bottom && vy>0)vy=0;}
            Position=new PointF((float)x,(float)y);Velocity=new PointF((float)vx,(float)vy);
        }
    }

    // Keyboard messages belong only to this focused, temporary overlay. No global
    // keyboard hooks or hotkeys remain active during ordinary desktop use.
    sealed class ManualSwimOverlay : Form {
        readonly Atlas atlas;readonly bool faceRight;readonly ManualSwimInput input;readonly Action completed;
        readonly ManualGuideVisibility guide;
        Bitmap guideArt,guidePage;byte shownAlpha;double nextGuidePaint;Rectangle guideBounds;
        bool finished;MouseButtons pressed;
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams {get{CreateParams p=base.CreateParams;p.ExStyle|=0x80000|0x80;return p;}}
        public ManualSwimOverlay(Rectangle bounds,Atlas art,bool right,ManualSwimInput keys,Action finish,double now=0){
            atlas=art;faceRight=right;input=keys;completed=finish;Text="Manual mode - Mochi";
            guide=new ManualGuideVisibility(now,Cursor.Position);
            FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;Bounds=bounds;
            KeyPreview=true;SetStyle(ControlStyles.StandardDoubleClick,false);
        }
        protected override void OnShown(EventArgs e){
            base.OnShown(e);
            // A nearly invisible input shield remains focused even after the card
            // fades. It keeps Escape/right-click and mouse recall working safely.
            guidePage=new Bitmap(Width,Height);
            using(Graphics g=Graphics.FromImage(guidePage))g.Clear(Color.FromArgb(1,10,39,56));
            guideArt=ManualGuideRenderer.Draw(Size,atlas,faceRight);
            guideBounds=ManualGuideRenderer.Place(Size,guideArt.Size,Rectangle.Empty,Rectangle.Empty);
            PresentGuide(255);
        }
        public void AdvanceGuide(double now,Point cursor,Rectangle body){
            guide.Advance(now,cursor,input.HasControls);byte alpha=(byte)Math.Round(guide.Opacity*255);
            if(guidePage==null)return;
            body.Offset(-Left,-Top);Rectangle bounds=ManualGuideRenderer.Place(Size,guideArt.Size,body,guideBounds);
            bool moved=bounds!=guideBounds;
            if(moved){
                using(Graphics g=Graphics.FromImage(guidePage))using(Brush shield=new SolidBrush(Color.FromArgb(1,10,39,56))){
                    g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;g.FillRectangle(shield,guideBounds);
                }
                guideBounds=bounds;
            }
            if(!moved && (alpha==shownAlpha || now<nextGuidePaint))return;
            nextGuidePaint=now+1.0/30;
            PresentGuide(alpha);
        }
        public void KeepGuideBehind(IntPtr pet){
            // Activation can raise the focused input shield after the card was
            // shown. Keep the entire stack ordered without changing focus.
            Native.PlaceManualGuideBehind(Handle,pet);
        }
        void PresentGuide(byte alpha){
            shownAlpha=alpha;
            Rectangle bounds=guideBounds;
            using(Graphics g=Graphics.FromImage(guidePage)){
                g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                using(Brush shield=new SolidBrush(Color.FromArgb(1,10,39,56)))g.FillRectangle(shield,bounds);
                g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceOver;
                if(alpha>0)using(System.Drawing.Imaging.ImageAttributes blend=new System.Drawing.Imaging.ImageAttributes()){
                    System.Drawing.Imaging.ColorMatrix matrix=new System.Drawing.Imaging.ColorMatrix();matrix.Matrix33=alpha/255f;blend.SetColorMatrix(matrix);
                    g.DrawImage(guideArt,bounds,0,0,guideArt.Width,guideArt.Height,GraphicsUnit.Pixel,blend);
                }
            }
            Native.SetBitmap(Handle,guidePage,Location);
        }
        protected override bool IsInputKey(Keys keyData){return ManualSwimInput.Accepts(keyData&Keys.KeyCode) || base.IsInputKey(keyData);}
        protected override void OnKeyDown(KeyEventArgs e){
            if(ManualSwimInput.Accepts(e.KeyCode)){input.Set(e.KeyCode,true);e.Handled=e.SuppressKeyPress=true;}
            base.OnKeyDown(e);
        }
        protected override void OnKeyUp(KeyEventArgs e){
            if(ManualSwimInput.Accepts(e.KeyCode)){input.Set(e.KeyCode,false);e.Handled=e.SuppressKeyPress=true;}
            base.OnKeyUp(e);
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
            if(keyData==Keys.Escape){Finish();return true;}return base.ProcessCmdKey(ref msg,keyData);
        }
        protected override void OnMouseDown(MouseEventArgs e){pressed=e.Button;base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){
            base.OnMouseUp(e);bool exit=pressed==MouseButtons.Right && e.Button==MouseButtons.Right;pressed=MouseButtons.None;
            if(exit)Finish(); // Consume the complete click; do not open a desktop menu.
        }
        protected override void OnDeactivate(EventArgs e){base.OnDeactivate(e);CheckFocusLater();}
        public void CheckFocusLater(){
            // Menu dismissal and Activate can deliver a transient deactivation.
            // Check after those queued messages before deciding the user switched apps.
            if(finished || !IsHandleCreated)return;
            try{BeginInvoke((Action)delegate{if(!finished && !ContainsFocus)Finish();});}catch(InvalidOperationException){}
        }
        protected override void OnFormClosed(FormClosedEventArgs e){base.OnFormClosed(e);Finish();}
        void Finish(){if(finished)return;finished=true;input.Clear();completed();}
        public void Dismiss(){finished=true;input.Clear();Close();Dispose();}
        protected override void Dispose(bool disposing){
            if(disposing){if(guidePage!=null){guidePage.Dispose();guidePage=null;}if(guideArt!=null){guideArt.Dispose();guideArt=null;}}
            base.Dispose(disposing);
        }
    }

    sealed partial class Companion {
        ManualSwimMotion manualSwim;
        ManualSwimOverlay manualOverlay;
        bool manualMoving;
        Bitmap manualBody;
        Rectangle manualBodyBounds;
        double nextManualBoostAt;
        ManualBoostWake manualWake;
        ManualWakeWindow manualWakeWindow;
        BoostAnimation manualBoostAnimation;
        int lastBoostStyle=-1;
        int lastBoostLine=-1;
        static readonly string[] BoostLines={"Wheee!\nBubble-powered!","Tiny shark.\nTurbo fins!","Zoomies: activated!","Catch me if\nyou fin!","Fast and\nfin-tastic!","No brakes!\nOkay, tiny brakes.","Sea you later!","I am speed!\nVery cute speed."};
        void StartManualSwimming(){
            if(closing || modal)return;
            CancelInteraction();CloseReleaseWelcome();down=dragging=false;Capture=false;
            swimming=guidedSwim=false;startupHintAt=-1;gaze.Reset();swimGaze.Reset();edgeWatch.Reset(Now);
            Rectangle area=Screen.FromRectangle(Bounds).Bounds;
            manualSwim=new ManualSwimMotion(Location,Size,area,Now,faceRight,nextManualBoostAt);Location=Point.Round(manualSwim.Position);
            manualWake=new ManualBoostWake(area,simulation?new Random(502):new Random());
            row=frame=0;actionStart=Now;actionUntil=0;bubble="You're the captain!\nI'll bring the fins.";bubbleUntil=Now+3.5;
            RefreshManualBody();
            if(simulation)return;
            try {
                manualOverlay=new ManualSwimOverlay(area,atlas,faceRight,manualSwim.Input,StopManualSwimming,Now);
                manualOverlay.Show();TopMost=true;Render();manualOverlay.Activate();Native.RaiseForManualSwimming(Handle);
                manualOverlay.KeepGuideBehind(Handle);
                // Activation can fail if another application took focus during menu dismissal.
                if(manualOverlay!=null)manualOverlay.CheckFocusLater();
            }catch{StopManualSwimming();Act(0,"My fins need a moment.\nPlease try again!",3);}
        }
        void StopManualSwimming(){
            if(manualSwim==null)return;
            IntPtr foreground=simulation?IntPtr.Zero:Native.GetForegroundWindow();
            bool preserveFocus=foreground!=IntPtr.Zero && foreground!=Handle && (manualOverlay==null || foreground!=manualOverlay.Handle);
            manualSwim.Input.Clear();manualSwim=null;manualMoving=false;
            if(manualBody!=null){manualBody.Dispose();manualBody=null;}
            manualBoostAnimation=null;manualWake=null;if(manualWakeWindow!=null){manualWakeWindow.Dispose();manualWakeWindow=null;}
            ManualSwimOverlay overlay=manualOverlay;manualOverlay=null;if(overlay!=null)overlay.Dismiss();
            swimGaze.Reset();swimFraction=PointF.Empty;row=frame=0;actionStart=Now;actionUntil=0;bubble="";bubbleUntil=0;
            edgeWatch.Reset(Now);Schedule();PauseAutomaticActivities();
            Location=Motion.Clamp(Location,Size,Screen.FromRectangle(Bounds).WorkingArea);
            if(!simulation && IsHandleCreated && !IsDisposed){TopMost=false;Native.KeepBehindApps(Handle);Render();}
            // Closing a WinForms tool window can activate its previous sibling.
            // Preserve the app the user already switched to, if that happened here.
            if(preserveFocus && Native.GetForegroundWindow()==Handle)Native.SetForegroundWindow(foreground);
            Save();
        }
        void AdvanceManualSwimming(double now,Point cursor){
            if(manualSwim.Input.TakeBoostPress() && manualSwim.TryBoost(now)){
                nextManualBoostAt=manualSwim.BoostReadyAt;
                BoostStyle style=BoostAnimation.Pick(random,lastBoostStyle);lastBoostStyle=(int)style;
                manualBoostAnimation=new BoostAnimation(style,now);
                // Start in the accepted dash direction, even when Space reverses
                // the previous swim. Subsequent steering still eases with gaze.
                swimGaze.Reset();swimGaze.Update(manualSwim.LastHeading.X,manualSwim.LastHeading.Y,now);
                bubble="";bubbleUntil=0;
                if(random.NextDouble()<.45){bubble=PickLine(BoostLines,ref lastBoostLine);bubbleUntil=now+2.5;}
            }
            PointF previous=manualSwim.Position;
            manualSwim.Advance(now);PointF velocity=manualSwim.Velocity;
            manualMoving=velocity.X*velocity.X+velocity.Y*velocity.Y>=1;
            if(manualMoving){FaceTravel(velocity.X);swimGaze.Update(velocity.X,velocity.Y,now);}
            else {swimGaze.Reset();row=0;}
            if(now>bubbleUntil)bubble="";
            if(manualBoostAnimation!=null && manualBoostAnimation.Finished(now))manualBoostAnimation=null;
            RefreshManualBody();
            manualWake.Advance(now,previous,manualSwim.Position,manualSwim.Velocity,prefs.Size,now<=manualSwim.BoostUntil+.04);
            if(manualOverlay!=null){
                Rectangle visible=manualBodyBounds;visible.Offset(Location);
                manualOverlay.AdvanceGuide(now,cursor,visible);
                manualOverlay.KeepGuideBehind(Handle);
            }
            Render();
            if(!simulation)PresentManualWake(now);
        }
        void RefreshManualBody(){
            if(manualBody!=null)manualBody.Dispose();manualBody=DrawManualBody();
            manualBodyBounds=ManualBodyGeometry.Measure(manualBody);
            manualSwim.SetBodyBounds(manualBodyBounds);PointF position=manualSwim.Position;
            Location=Point.Round(position);swimFraction=new PointF(position.X-Left,position.Y-Top);
        }
        Bitmap DrawManualBody(){
            Bitmap frame;
            if(manualBoostAnimation!=null){
                double heading=swimGaze.Angle;
                // Use the same smoothed direction for the sprite's mirror and
                // pitch. Raw velocity can cross zero earlier during a reversal.
                double horizontal=Math.Sin(heading*Math.PI/180);
                bool right=Math.Abs(horizontal)<.02?faceRight:horizontal>0;
                frame=BoostAnimationRenderer.Draw(atlas,manualBoostAnimation,Now,prefs.Size,"",right,heading,PointF.Empty);
            }
            else if(manualMoving){frame=GazeRenderer.Draw(atlas,swimGaze.Sample(Now),prefs.Size,"",true);}
            else frame=Renderer.DrawAnimated(atlas,0,Now-actionStart,prefs.Size,"",faceRight);
            return frame;
        }
        Bitmap RenderManualFrame(){
            Bitmap frame=swimFraction==PointF.Empty?(Bitmap)manualBody.Clone():new Bitmap(manualBody.Width,manualBody.Height);
            Rectangle viewport=Rectangle.Intersect(new Rectangle(Point.Empty,frame.Size),new Rectangle(manualSwim.Area.Left-Left,manualSwim.Area.Top-Top,manualSwim.Area.Width,manualSwim.Area.Height));
            using(Graphics g=Graphics.FromImage(frame)){
                g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                if(swimFraction!=PointF.Empty)g.DrawImage(manualBody,swimFraction.X,swimFraction.Y);
                // Speech never reserves swimming space. Omit it where the usual
                // speech area is beyond the screen, instead of showing cut text.
                if(viewport.Contains(new Rectangle(12,8,frame.Width-24,66)))Renderer.DrawBubble(g,frame.Size,bubble);
            }
            ManualBoostRenderer.DrawStatus(frame,Now,manualSwim.BoostUntil,manualSwim.BoostReadyAt,viewport,manualBodyBounds);return frame;
        }
        void PresentManualWake(double now){
            Rectangle bounds=manualWake.Bounds(now);
            if(bounds.IsEmpty){if(manualWakeWindow!=null)manualWakeWindow.Hide();return;}
            if(manualWakeWindow==null)manualWakeWindow=new ManualWakeWindow();
            bool showing=!manualWakeWindow.Visible;
            using(Bitmap frame=manualWake.Draw(now,bounds))manualWakeWindow.Present(frame,bounds);
            // The layered bitmap update preserves z-order. Establish it once on
            // showing, rather than generating window-position messages every frame.
            if(showing){Native.RaiseForManualSwimming(manualWakeWindow.Handle);Native.RaiseForManualSwimming(Handle);}
        }
    }
}
