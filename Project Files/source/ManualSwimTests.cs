using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MochiDesktop {
    static class ManualSwimTests {
        internal static void Check(bool ok,string message){if(!ok)throw new Exception("Manual mode: "+message);}
        static void Input(){
            ManualSwimInput input=new ManualSwimInput();
            Keys[] keys={Keys.W,Keys.Up,Keys.A,Keys.Left,Keys.S,Keys.Down,Keys.D,Keys.Right};
            PointF[] directions={new PointF(0,-1),new PointF(-1,0),new PointF(0,1),new PointF(1,0)};
            for(int i=0;i<keys.Length;i++){
                input.Set(keys[i],true);input.Set(keys[i],true);
                Check(input.Direction==directions[i/2],"key direction or auto-repeat");
                input.Set(keys[i],false);Check(input.Direction==PointF.Empty,"release left a key held");
            }
            input.Set(Keys.W,true);input.Set(Keys.Up,true);input.Set(Keys.W,false);
            Check(input.Direction==new PointF(0,-1),"releasing one alias cancelled the other");
            input.Set(Keys.S,true);Check(input.Direction==PointF.Empty,"opposite keys did not cancel");
            input.Set(Keys.S,false);input.Set(Keys.Right,true);
            Check(Math.Abs(input.Direction.X-.70710678)<.00001 && Math.Abs(input.Direction.Y+.70710678)<.00001,"mixed-key diagonal must be normalized");
            input.Clear();input.Set(Keys.Space,true);Check(input.Direction==PointF.Empty,"unrelated key moved Mochi");
        }
        static void MotionChecks(){
            Rectangle large=new Rectangle(-3000,-2000,6000,4000);Size size=Renderer.WindowSize(176);PointF previous=PointF.Empty;
            foreach(int fps in new[]{30,60,120}){
                ManualSwimMotion motion=new ManualSwimMotion(Point.Empty,size,large,0);motion.Input.Set(Keys.Right,true);
                for(int i=1;i<=fps*2;i++)motion.Advance(i/(double)fps);
                Check(Math.Abs(motion.Position.X-456)<.03 && Math.Abs(motion.Position.Y)<.001,"swim speed depends on frame rate");
                if(fps>30)Check(Math.Abs(motion.Position.X-previous.X)<.03,"timer cadence changed travel distance");previous=motion.Position;
                motion.Input.Clear();for(int i=1;i<=fps;i++)motion.Advance(2+i/(double)fps);
                Check(motion.Velocity==PointF.Empty && motion.Position.X<481,"key release did not settle promptly");
                PointF stopped=motion.Position;motion.Advance(4);Check(motion.Position==stopped,"idle steering drifted");
                motion.Input.Set(Keys.Left,true);motion.Advance(3600);
                Check(Math.Abs(motion.Position.X-stopped.X)<13,"long suspension caused a teleport");
            }
            foreach(Rectangle area in new[]{new Rectangle(0,0,1920,1040),new Rectangle(-1920,-300,1920,1080),new Rectangle(1920,100,1080,1880)})foreach(int pixels in new[]{144,176,224}){
                size=Renderer.WindowSize(pixels);ManualSwimMotion motion=new ManualSwimMotion(area.Location,size,area,0);double t=0;
                foreach(Keys key in new[]{Keys.Up,Keys.Left,Keys.Down,Keys.Right,Keys.Up,Keys.Left}){
                    motion.Input.Clear();motion.Input.Set(key,true);
                    for(int i=0;i<600;i++){
                        motion.Advance(t+=.04);
                        Check(area.Contains(new Rectangle(Point.Round(motion.Position),size)),"swam outside a working area");
                    }
                }
                Check(Point.Round(motion.Position)==area.Location,"did not reach screen edges");
            }
        }
        public static int Run(string path){
            try {
                Input();MotionChecks();Companion.VerifyManualSwimming();
                File.WriteAllText(path,
                    "PASS: WASD and arrow keys; held-key repeats, independent aliases, opposite keys and equal-speed mixed diagonals.\r\n"+
                    "PASS: smooth time-based motion at 30/60/120 Hz; release settling; long-frame guard; all sizes inside landscape, portrait and negative-coordinate monitors.\r\n"+
                    "PASS: menu entry, animated steering and retained facing; paused roaming; due Idle/Playful clocks preserved with three-second handoff; safe interruption, notices and disposal.\r\n");return 0;
            }catch(Exception error){File.WriteAllText(path,"FAIL: "+error);return 1;}
        }
    }

    sealed partial class Companion {
        public static void VerifyManualSwimming(){
            Point away=new Point(-10000,-10000);Rectangle area=Screen.PrimaryScreen.WorkingArea;
            foreach(bool playfulMode in new[]{false,true})foreach(bool roam in new[]{false,true})using(Companion pet=new Companion(true)){
                pet.Location=new Point(area.Left+300,area.Top+200);pet.prefs.Roam=roam;pet.prefs.PlayfulMode=playfulMode;
                PlayfulDesktopFixture desktop=new PlayfulDesktopFixture(new Point(area.Left+650,area.Top+450));pet.desktopFactory=desktop.Open;
                int count=0;foreach(ToolStripItem item in pet.menu.Items)if(item.Text=="Manual mode")count++;
                ManualSwimTests.Check(count==1,"menu action missing or duplicated");
                pet.Pet();pet.StartManualSwimming();pet.nextIdleActivity=1;pet.nextPlayful=2;pet.nextSwim=0;
                ManualSwimTests.Check(pet.manualSwim!=null && pet.reaction==null && pet.feeding==null && !pet.swimming,"manual control did not take priority");
                Point start=pet.Location;pet.Advance(600,away);
                ManualSwimTests.Check(pet.Location==start && pet.manualSwim!=null && pet.reaction==null && pet.feeding==null && pet.playful==null && desktop.Reads==0,"waiting for input allowed an automatic activity");
                pet.releaseWelcomePending=true;pet.releaseWelcomeAt=0;
                ManualSwimTests.Check(!pet.CanShowReleaseWelcome(),"welcome interrupted steering");
                bool noticed=false;pet.pendingUpdateNotice=delegate{noticed=true;};pet.PumpUpdateNotice();
                ManualSwimTests.Check(!noticed && pet.pendingUpdateNotice!=null,"update interrupted steering");pet.pendingUpdateNotice=null;pet.releaseWelcomePending=false;
                pet.manualSwim.Input.Set(Keys.D,true);
                for(int i=0;i<60;i++)pet.Advance(pet.Now+1.0/60,away);
                ManualSwimTests.Check(pet.Left>start.X+200 && pet.faceRight && pet.manualMoving && pet.prefs.Roam==roam,"steering failed or changed roaming preference");
                using(Bitmap first=pet.RenderFrame()){
                    pet.Advance(pet.Now+.04,away);using(Bitmap second=pet.RenderFrame()){
                        bool differs=false;for(int y=0;y<first.Height && !differs;y+=2)for(int x=0;x<first.Width;x+=2)if(first.GetPixel(x,y)!=second.GetPixel(x,y)){differs=true;break;}
                        ManualSwimTests.Check(differs,"swimming pose was static");
                    }
                }
                pet.manualSwim.Input.Set(Keys.D,false);pet.manualSwim.Input.Set(Keys.A,true);
                for(int i=0;i<60;i++)pet.Advance(pet.Now+1.0/60,away);
                ManualSwimTests.Check(!pet.faceRight,"reversing did not change facing");
                pet.manualSwim.Input.Clear();for(int i=0;i<60;i++)pet.Advance(pet.Now+1.0/60,away);
                ManualSwimTests.Check(!pet.manualMoving && !pet.faceRight && pet.nextIdleActivity==1 && pet.nextPlayful==2,"release drifted or changed activity clocks");
                ManualSwimInput oldInput=pet.manualSwim.Input;pet.StopManualSwimming();double ended=pet.Now;
                ManualSwimTests.Check(oldInput.Direction==PointF.Empty && pet.manualSwim==null && !pet.faceRight && pet.prefs.Roam==roam,"exit retained keys or changed preferences/facing");
                pet.Advance(ended+2.99,away);ManualSwimTests.Check(pet.feeding==null && pet.playful==null,"handoff pause too short");
                pet.Advance(ended+3,away);ManualSwimTests.Check(pet.feeding!=null && pet.playful==null && pet.nextPlayful==2,"oldest idle turn lost on exit");
                pet.Advance(pet.feeding.Started+pet.feeding.Duration,away);
                pet.Advance(pet.Now+3,away);
                ManualSwimTests.Check((pet.playful!=null)==playfulMode,"queued prank did not respect Playful Mode");
                // Starting keyboard control mid-prank follows the existing safe cancellation path.
                pet.StartManualSwimming();ManualSwimTests.Check(pet.playful==null,"prank survived keyboard takeover");
                pet.ChooseDestination();ManualSwimTests.Check(pet.manualSwim==null && pet.choosingDestination,"Come here failed to replace steering");
                pet.StartManualSwimming();ManualSwimTests.Check(!pet.choosingDestination,"picker survived keyboard takeover");
                pet.Feed();ManualSwimTests.Check(pet.manualSwim==null && pet.feeding!=null,"manual feeding failed to stop steering");
                pet.StartManualSwimming();pet.BeginSwim(true);ManualSwimTests.Check(pet.manualSwim==null && pet.swimming,"requested swim failed to stop steering");
                pet.StartManualSwimming();pet.CancelInteraction();ManualSwimTests.Check(pet.manualSwim==null,"opening controls failed to stop steering");
                pet.StartManualSwimming();oldInput=pet.manualSwim.Input;oldInput.Set(Keys.W,true);pet.Dispose();
                ManualSwimTests.Check(oldInput.Direction==PointF.Empty && pet.manualSwim==null,"disposal left steering active");
            }
        }
    }
}
