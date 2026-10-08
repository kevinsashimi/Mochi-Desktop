using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MochiDesktop {
    sealed class ManualSwimInput {
        readonly HashSet<Keys> held=new HashSet<Keys>();
        public static bool Accepts(Keys key){
            return key==Keys.W || key==Keys.A || key==Keys.S || key==Keys.D ||
                key==Keys.Up || key==Keys.Left || key==Keys.Down || key==Keys.Right;
        }
        public void Set(Keys key,bool pressed){if(!Accepts(key))return;if(pressed)held.Add(key);else held.Remove(key);}
        public void Clear(){held.Clear();}
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
        readonly Rectangle area;readonly Size window;double last;
        const double Speed=240,Response=.10;
        public ManualSwimMotion(Point start,Size size,Rectangle bounds,double now){
            window=size;area=bounds;Position=Motion.Clamp(start,size,bounds);last=now;
        }
        public void Advance(double now){
            // A delayed timer must not launch Mochi across the screen on resume.
            double dt=Math.Max(0,Math.Min(.05,now-last));last=now;
            if(dt==0)return;
            PointF direction=Input.Direction;double decay=Math.Exp(-dt/Response);
            double tx=direction.X*Speed,ty=direction.Y*Speed;
            double x=Position.X+tx*dt+(Velocity.X-tx)*Response*(1-decay);
            double y=Position.Y+ty*dt+(Velocity.Y-ty)*Response*(1-decay);
            double vx=tx+(Velocity.X-tx)*decay,vy=ty+(Velocity.Y-ty)*decay;
            double right=Math.Max(area.Left,area.Right-window.Width),bottom=Math.Max(area.Top,area.Bottom-window.Height);
            if(x<=area.Left || x>=right){x=Math.Max(area.Left,Math.Min(right,x));vx=0;}
            if(y<=area.Top || y>=bottom){y=Math.Max(area.Top,Math.Min(bottom,y));vy=0;}
            if(direction==PointF.Empty && vx*vx+vy*vy<1){vx=vy=0;}
            Position=new PointF((float)x,(float)y);Velocity=new PointF((float)vx,(float)vy);
        }
    }

    // Keyboard messages belong only to this focused, temporary overlay. No global
    // keyboard hooks or hotkeys remain active during ordinary desktop use.
    sealed class ManualSwimOverlay : Form {
        readonly Atlas atlas;readonly bool faceRight;readonly ManualSwimInput input;readonly Action completed;
        bool finished;MouseButtons pressed;
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams {get{CreateParams p=base.CreateParams;p.ExStyle|=0x80000|0x80;return p;}}
        public ManualSwimOverlay(Rectangle bounds,Atlas art,bool right,ManualSwimInput keys,Action finish){
            atlas=art;faceRight=right;input=keys;completed=finish;Text="Manual mode - Mochi";
            FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;Bounds=bounds;
            KeyPreview=true;SetStyle(ControlStyles.StandardDoubleClick,false);
        }
        protected override void OnShown(EventArgs e){
            base.OnShown(e);
            using(Bitmap page=DestinationOverlay.DrawOverlay(Size,atlas,faceRight,"You're the captain!","Hold WASD or arrow keys to swim.","Esc or right-click to finish"))Native.SetBitmap(Handle,page,Location);
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
    }

    sealed partial class Companion {
        ManualSwimMotion manualSwim;
        ManualSwimOverlay manualOverlay;
        bool manualMoving;
        void StartManualSwimming(){
            if(closing || modal)return;
            CancelInteraction();CloseReleaseWelcome();down=dragging=false;Capture=false;
            swimming=guidedSwim=false;startupHintAt=-1;gaze.Reset();swimGaze.Reset();edgeWatch.Reset(Now);
            Rectangle area=Screen.FromRectangle(Bounds).WorkingArea;
            manualSwim=new ManualSwimMotion(Location,Size,area,Now);Location=Point.Round(manualSwim.Position);
            row=frame=0;actionStart=Now;actionUntil=0;bubble="You're the captain!\nI'll bring the fins.";bubbleUntil=Now+3.5;
            if(simulation)return;
            try {
                manualOverlay=new ManualSwimOverlay(area,atlas,faceRight,manualSwim.Input,StopManualSwimming);
                manualOverlay.Show();TopMost=true;Render();manualOverlay.Activate();Native.RaiseForManualSwimming(Handle);
                // Activation can fail if another application took focus during menu dismissal.
                if(manualOverlay!=null)manualOverlay.CheckFocusLater();
            }catch{StopManualSwimming();Act(0,"My fins need a moment.\nPlease try again!",3);}
        }
        void StopManualSwimming(){
            if(manualSwim==null)return;
            IntPtr foreground=simulation?IntPtr.Zero:Native.GetForegroundWindow();
            bool preserveFocus=foreground!=IntPtr.Zero && foreground!=Handle && (manualOverlay==null || foreground!=manualOverlay.Handle);
            manualSwim.Input.Clear();manualSwim=null;manualMoving=false;
            ManualSwimOverlay overlay=manualOverlay;manualOverlay=null;if(overlay!=null)overlay.Dismiss();
            swimGaze.Reset();swimFraction=PointF.Empty;row=frame=0;actionStart=Now;actionUntil=0;bubble="";bubbleUntil=0;
            edgeWatch.Reset(Now);Schedule();PauseAutomaticActivities();
            if(!simulation && IsHandleCreated && !IsDisposed){TopMost=false;Native.KeepBehindApps(Handle);Render();}
            // Closing a WinForms tool window can activate its previous sibling.
            // Preserve the app the user already switched to, if that happened here.
            if(preserveFocus && Native.GetForegroundWindow()==Handle)Native.SetForegroundWindow(foreground);
            Save();
        }
        void AdvanceManualSwimming(double now){
            manualSwim.Advance(now);PointF position=manualSwim.Position,velocity=manualSwim.Velocity;
            Location=Point.Round(position);swimFraction=new PointF(position.X-Left,position.Y-Top);
            manualMoving=velocity.X*velocity.X+velocity.Y*velocity.Y>=1;
            if(manualMoving){FaceTravel(velocity.X);swimGaze.Update(velocity.X,velocity.Y,now);}
            else {swimGaze.Reset();row=0;}
            if(now>bubbleUntil)bubble="";
            Render();
        }
    }
}
