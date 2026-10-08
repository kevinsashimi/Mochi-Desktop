using System;
using System.Collections.Generic;
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
            Check(input.TakeBoostPress() && !input.TakeBoostPress(),"Space did not produce one boost request");
            input.Set(Keys.Space,true);Check(!input.TakeBoostPress(),"held Space repeated a boost");
            input.Set(Keys.Space,false);input.Set(Keys.Space,true);Check(input.TakeBoostPress(),"fresh Space press did not rearm");
            input.Clear();Check(!input.TakeBoostPress(),"focus loss retained a queued boost");
        }
        static void BoostChecks(){
            Rectangle area=new Rectangle(-3000,-2000,6000,4000);Size size=Renderer.WindowSize(176);float previous=0;
            foreach(int fps in new[]{30,60,120}){
                ManualSwimMotion boost=new ManualSwimMotion(Point.Empty,size,area,0),normal=new ManualSwimMotion(Point.Empty,size,area,0);
                boost.Input.Set(Keys.D,true);normal.Input.Set(Keys.D,true);
                Check(boost.TryBoost(0),"first boost not ready");
                for(int i=1;i<=fps*2;i++){boost.Advance(i/(double)fps);normal.Advance(i/(double)fps);}
                Check(boost.Position.X-normal.Position.X>150 && boost.Position.X-normal.Position.X<225 && Math.Abs(boost.Velocity.X-240)<.1,"boost was not a short, faster swim");
                if(fps>30)Check(Math.Abs(boost.Position.X-previous)<.06,"boost distance depends on timer cadence");previous=boost.Position.X;
                Check(!boost.TryBoost(4.999999) && boost.TryBoost(5),"five-second cooldown boundary");
            }
            foreach(bool right in new[]{false,true}){
                ManualSwimMotion boost=new ManualSwimMotion(Point.Empty,size,area,0,right);
                Check(boost.TryBoost(0),"resting boost was unavailable");
                for(int i=1;i<=120;i++)boost.Advance(i/60.0);
                Check(Math.Abs(boost.Position.X)>270 && Math.Abs(boost.Position.X)<290 && (boost.Position.X>0)==right && boost.Velocity==PointF.Empty,"resting dash direction or stopping distance");
            }
            ManualSwimMotion reentry=new ManualSwimMotion(Point.Empty,size,area,1,true,5);
            Check(!reentry.TryBoost(4.999) && reentry.TryBoost(5),"re-entering reset the cooldown");
            ManualSwimMotion delayed=new ManualSwimMotion(Point.Empty,size,area,0);delayed.TryBoost(0);delayed.Advance(60);
            Check(Math.Abs(delayed.Position.X)<20 && delayed.BoostUntil<60,"suspension replayed an old dash");
            foreach(Rectangle screen in new[]{new Rectangle(0,0,1920,1040),new Rectangle(-1920,-300,1920,1080),new Rectangle(1920,100,1080,1880)})foreach(int pixels in new[]{144,176,224}){
                size=Renderer.WindowSize(pixels);
                for(int n=0;n<8;n++){
                    ManualSwimMotion boost=new ManualSwimMotion(new Point(screen.Left+(screen.Width-size.Width)/2,screen.Top+(screen.Height-size.Height)/2),size,screen,0);
                    if(n==0 || n==4 || n==5)boost.Input.Set(Keys.A,true);
                    if(n==1 || n==6 || n==7)boost.Input.Set(Keys.D,true);
                    if(n==2 || n==4 || n==6)boost.Input.Set(Keys.W,true);
                    if(n==3 || n==5 || n==7)boost.Input.Set(Keys.S,true);
                    Check(boost.TryBoost(0),"directional boost failed");
                    for(int i=1;i<=300;i++){boost.Advance(i/60.0);Check(screen.Contains(new Rectangle(Point.Round(boost.Position),size)),"boost left the working area");}
                }
            }
            ManualSwimMotion wall=new ManualSwimMotion(new Point(area.Left,area.Top),Renderer.WindowSize(176),area,0,false);
            Check(!wall.TryBoost(0) && wall.BoostReadyAt==0,"wall wasted a charge");wall.Input.Set(Keys.D,true);Check(wall.TryBoost(0),"turning away from the wall failed");
        }
        static void WakeChecks(){
            Rectangle area=new Rectangle(-1920,-300,1920,1080);ManualBoostWake wake=new ManualBoostWake(area,new Random(47));
            PointF position=new PointF(-1000,100);double now=0;
            for(int i=0;i<35;i++){
                PointF from=position;position.X+=9;now+=.01;wake.Advance(now,from,position,new PointF(720,0),176,true);
                Rectangle bounds=wake.Bounds(now);Check(wake.Count>0 && wake.Count<=72 && area.Contains(bounds) && bounds.Width<500 && bounds.Height<220,"wake clipping or resource bound");
                using(Bitmap frame=wake.Draw(now,bounds)){
                    bool visible=false;for(int y=0;y<frame.Height && !visible;y++)for(int x=0;x<frame.Width;x++)if(frame.GetPixel(x,y).A>40){visible=true;break;}
                    Check(visible && frame.GetPixel(0,0).A==0,"bubble wake invisible or opaque rectangle");
                }
            }
            int count=wake.Count;wake.Advance(now+.1,position,position,PointF.Empty,176,true);Check(wake.Count<=count,"stationary edge generated new bubbles");
            wake.Advance(now+2,position,position,PointF.Empty,176,false);Check(wake.Count==0 && wake.Bounds(now+2).IsEmpty,"wake failed to fade away");
            using(Atlas atlas=new Atlas())foreach(int pixels in new[]{144,176,224})for(int direction=0;direction<16;direction++){
                using(Bitmap frame=BoostAnimationRenderer.Draw(atlas,new BoostAnimation(BoostStyle.RocketGrin,0),.17,pixels,"Tiny shark.\nTurbo fins!",direction<=8,direction*22.5,PointF.Empty)){
                    ManualBoostRenderer.DrawStatus(frame,.17,.34,5);
                    Check(frame.Size==Renderer.WindowSize(pixels) && frame.GetPixel(0,0).A==0,"boost pose geometry");
                }
            }
        }
        static void IllustrationChecks(){
            Random random=new Random(347);int previous=-1;HashSet<BoostStyle> styles=new HashSet<BoostStyle>();
            for(int i=0;i<300;i++){
                BoostStyle style=BoostAnimation.Pick(random,previous);Check((int)style!=previous,"boost expression immediately repeated");styles.Add(style);previous=(int)style;
            }
            Check(styles.Count==3,"missing boost expression");
            using(Atlas atlas=new Atlas())foreach(BoostStyle style in Enum.GetValues(typeof(BoostStyle))){
                BoostAnimation clip=new BoostAnimation(style,10);HashSet<int> visited=new HashSet<int>();
                for(int i=0;i<76;i++)visited.Add(clip.Frame(10+i*.01));
                Check(visited.Count==4 && !clip.Finished(10.759) && clip.Finished(10.76),"boost illustrated timeline or settle boundary");
                for(int cell=0;cell<4;cell++){
                    Rectangle bounds=atlas.OpaqueBounds(atlas.Boost[(int)style,cell]);
                    Check(bounds.Width>155 && bounds.Height>100 && bounds.Left>=3 && bounds.Right<=189 && bounds.Top>30 && bounds.Bottom<180,"boost sprite empty or cut off");
                }
                foreach(int pixels in new[]{144,176,224})foreach(double elapsed in new[]{0,.12,.30,.52,.74}){
                    int baseline;
                    using(Bitmap horizontal=BoostAnimationRenderer.Draw(atlas,clip,10+elapsed,pixels,"",false,270,PointF.Empty))baseline=VisiblePixels(horizontal);
                    Check(baseline>pixels*pixels*.18,"empty boost body");
                    for(int direction=0;direction<16;direction++){
                        using(Bitmap frame=BoostAnimationRenderer.Draw(atlas,clip,10+elapsed,pixels,"",direction<=8,direction*22.5,new PointF(.49f,-.49f))){
                            int opaque=0;
                            for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++)if(frame.GetPixel(x,y).A>20){
                                opaque++;
                                Check(x>2 && x<frame.Width-3 && y>=77 && y<frame.Height-23,"boost fin overlaps speech, status or window edge: "+style+" size "+pixels+" heading "+direction+" time "+elapsed+" pixel "+x+","+y);
                            }
                            Check(opaque>baseline*.96,"boost flattened or lost its body");
                        }
                    }
                }
                foreach(int pixels in new[]{144,176,224})foreach(double elapsed in new[]{.03,.12,.30,.52}){
                    using(Bitmap left=BoostAnimationRenderer.Draw(atlas,clip,10+elapsed,pixels,"",false,270,PointF.Empty))
                    using(Bitmap right=BoostAnimationRenderer.Draw(atlas,clip,10+elapsed,pixels,"",true,90,PointF.Empty)){
                        right.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        // GDI+ can round mirrored subpixels differently; the complete
                        // silhouettes must nevertheless occupy the same footprint.
                        Rectangle a=atlas.OpaqueBounds(left),b=atlas.OpaqueBounds(right);
                        Check(Math.Abs(a.Left-b.Left)<=2 && Math.Abs(a.Top-b.Top)<=2 && Math.Abs(a.Width-b.Width)<=2 && Math.Abs(a.Height-b.Height)<=2,"left/right boost geometry differs");
                    }
                }
            }
        }
        static int VisiblePixels(Bitmap frame){int count=0;for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++)if(frame.GetPixel(x,y).A>20)count++;return count;}
        static void GuideChecks(){
            Point mouse=new Point(200,100);ManualGuideVisibility guide=new ManualGuideVisibility(0,mouse);
            guide.Advance(100,mouse,false);Check(guide.Opacity==1,"controls faded before first use");
            guide.Advance(100,mouse,true);guide.Advance(102.99,mouse,true);Check(guide.Opacity==1,"controls did not leave reading time");
            guide.Advance(103.75,mouse,true);Check(Math.Abs(guide.Opacity-.5)<.001,"controls did not fade gradually");
            guide.Advance(104.5,mouse,true);Check(guide.Opacity==0,"controls never disappeared");
            guide.Advance(300,mouse,true);Check(guide.Opacity==0,"held keys or stationary mouse restored controls");
            mouse.X++;guide.Advance(301,mouse,true);guide.Advance(301.09,mouse,true);
            Check(guide.Opacity>.4 && guide.Opacity<.6,"mouse recall did not fade in");
            guide.Advance(301.18,mouse,true);Check(guide.Opacity==1,"mouse did not restore controls");
            guide.Advance(305.5,mouse,false);Check(guide.Opacity==0,"recalled controls did not fade again");
            using(Atlas atlas=new Atlas())foreach(int width in new[]{320,800,1920,3840})using(Bitmap card=ManualGuideRenderer.Draw(new Size(width,1080),atlas,true)){
                Check(card.Width<=width && card.GetPixel(0,0).A==0 && card.GetPixel(card.Width/2,4).A>200,"controls card size or transparency");
                // Every separate instruction row must contain legible dark ink.
                int x=card.Width>=420?111:20;
                for(int line=0;line<3;line++){
                    int ink=0;
                    for(int y=49+line*27;y<72+line*27;y++)for(int column=x;column<card.Width-15;column++){
                        Color c=card.GetPixel(column,y);if(c.A>150 && c.R<90 && c.G<140)ink++;
                    }
                    Check(ink>140,"missing stacked instruction line");
                }
                Rectangle top=ManualGuideRenderer.Place(new Size(width,1080),card.Size,Rectangle.Empty,Rectangle.Empty);
                Rectangle body=new Rectangle(width/2-90,0,180,160);
                Rectangle bottom=ManualGuideRenderer.Place(new Size(width,1080),card.Size,body,top);
                Check(bottom.Top>top.Top && !bottom.IntersectsWith(body),"card did not leave Mochi's path at the top");
                Check(ManualGuideRenderer.Place(new Size(width,1080),card.Size,body,bottom)==bottom,"card moved repeatedly with a stationary Mochi");
                body.Y=980;Rectangle recalled=ManualGuideRenderer.Place(new Size(width,1080),card.Size,body,bottom);
                Check(recalled==top,"card did not leave the bottom edge clear");
            }
        }
        static void BodyBoundsChecks(){
            using(Atlas atlas=new Atlas())foreach(Rectangle area in new[]{new Rectangle(0,0,1920,1080),new Rectangle(-1920,-300,1920,1080),new Rectangle(1920,100,1080,1920)})foreach(int size in new[]{144,176,224}){
                Size window=Renderer.WindowSize(size);
                using(Bitmap art=GazeRenderer.Draw(atlas,new GazePose{Direction=0,Time=.2},size,"",true)){
                    Rectangle body=ManualBodyGeometry.Measure(art);
                    Check(body.Top>78 && body.Width<window.Width,"body geometry includes invisible padding");
                    foreach(Keys key in new[]{Keys.Left,Keys.Up,Keys.Right,Keys.Down}){
                        ManualSwimMotion motion=new ManualSwimMotion(area.Location,window,area,0);
                        motion.SetBodyBounds(body);motion.Input.Set(key,true);
                        for(int i=1;i<=240;i++){
                            motion.Advance(i*.05);Rectangle visible=body;visible.Offset(Point.Round(motion.Position));
                            Check(area.Contains(visible),"visible body left its monitor");
                        }
                        Rectangle final=body;final.Offset(Point.Round(motion.Position));
                        Check(key==Keys.Left?final.Left==area.Left:key==Keys.Right?final.Right==area.Right:key==Keys.Up?final.Top==area.Top:final.Bottom==area.Bottom,"transparent padding blocked a screen edge");
                        Check(!area.Contains(new Rectangle(Point.Round(motion.Position),window)),"the window padding still limits swimming");
                        Check(!motion.TryBoost(13),"blocked visible edge wasted a boost");
                    }
                }
            }
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
                Input();MotionChecks();BoostChecks();WakeChecks();IllustrationChecks();GuideChecks();BodyBoundsChecks();Companion.VerifyManualSwimming();Companion.VerifyManualBoost();Companion.VerifyManualEdges();
                File.WriteAllText(path,
                    "PASS: WASD and arrow keys; held-key repeats, independent aliases, opposite keys and equal-speed mixed diagonals.\r\n"+
                    "PASS: smooth time-based motion at 30/60/120 Hz; release settling; long-frame guard; all sizes inside landscape, portrait and negative-coordinate monitors.\r\n"+
                    "PASS: menu entry, animated steering and retained facing; paused roaming; due Idle/Playful clocks preserved with three-second handoff; safe interruption, notices and disposal.\r\n"+
                    "PASS: short boosts in eight directions; Space edge-triggering; five-second boundary and cross-session recharge; equal distance at 30/60/120 Hz; resting dashes, wall protection and suspension safety.\r\n"+
                    "PASS: bounded transparent bubble wake, screen clipping, stationary and expiry behavior; all boost poses and sizes; silent/spoken variety; cooldown feedback and complete effect cleanup.\r\n"+
                    "PASS: 12 dedicated illustrated boost frames; three expressions without immediate repeats; launch/kick/settle timeline; every heading, expression and size keeps a full body clear of speech/status; left/right geometry; actual timer selects, renders and cleans up the new artwork.\r\n"+
                    "PASS: full-monitor visible-body bounds without speech/window padding; all four edges and corners, three sizes, normal and boosted movement, negative-coordinate and portrait monitors; edge-safe cooldown feedback and safe exit.\r\n"+
                    "PASS: stacked controls at narrow/desktop/4K widths; visible until first use; three-second reading time and smooth fade; held keys do not reset fading; mouse motion restores controls and starts a fresh fade.\r\n");return 0;
            }catch(Exception error){File.WriteAllText(path,"FAIL: "+error);return 1;}
        }
    }

    sealed partial class Companion {
        public static void VerifyManualEdges(){
            Rectangle screen=Screen.PrimaryScreen.Bounds;Point cursor=new Point(-10000,-10000);
            foreach(int size in new[]{144,176,224})foreach(bool boosted in new[]{false,true})for(int direction=0;direction<8;direction++)using(Companion pet=new Companion(true)){
                pet.prefs.Size=size;pet.Size=Renderer.WindowSize(size);
                bool left=direction==0 || direction==4 || direction==6,right=direction==1 || direction==5 || direction==7;
                bool up=direction==2 || direction==4 || direction==5,down=direction==3 || direction==6 || direction==7;
                pet.Location=new Point(right?screen.Right-pet.Width-50:screen.Left+50,down?screen.Bottom-pet.Height-50:screen.Top+50);
                pet.StartManualSwimming();CheckManualEdge(pet.manualSwim.Area==screen,"manual swim still excludes the taskbar strip");
                if(left)pet.manualSwim.Input.Set(Keys.A,true);if(right)pet.manualSwim.Input.Set(Keys.D,true);
                if(up)pet.manualSwim.Input.Set(Keys.W,true);if(down)pet.manualSwim.Input.Set(Keys.S,true);
                if(boosted)pet.manualSwim.Input.Set(Keys.Space,true);
                for(int i=0;i<72;i++){
                    pet.Advance(pet.Now+.025,cursor);Rectangle body=pet.manualBodyBounds;body.Offset(pet.Location);
                    CheckManualEdge(screen.Contains(body),"actual animated body crossed the screen");
                }
                Rectangle visible=pet.manualBodyBounds;visible.Offset(pet.Location);
                CheckManualEdge((!left || visible.Left-screen.Left<=2) && (!right || screen.Right-visible.Right<=2) && (!up || visible.Top-screen.Top<=2) && (!down || screen.Bottom-visible.Bottom<=2),"actual sprite stopped before an edge: size="+size+" boost="+boosted+" direction="+direction+" body="+visible+" screen="+screen);
                using(Bitmap frame=pet.RenderFrame())for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++)if(frame.GetPixel(x,y).A>60)
                    CheckManualEdge(screen.Contains(new Point(pet.Left+x,pet.Top+y)),"visible feedback was cut off by an edge");
                pet.StopManualSwimming();CheckManualEdge(pet.manualBody==null && Screen.PrimaryScreen.WorkingArea.Contains(pet.Bounds),"exit retained a body buffer or offscreen normal window");
            }
        }
        static void CheckManualEdge(bool ok,string message){ManualSwimTests.Check(ok,message);}
        public static void VerifyManualBoost(){
            Point away=new Point(-10000,-10000);Rectangle area=Screen.PrimaryScreen.WorkingArea;Point start=new Point(area.Left+300,area.Top+200);
            using(Companion pet=new Companion(true)){
                pet.Location=start;pet.prefs.Roam=false;pet.prefs.PlayfulMode=true;pet.StartManualSwimming();pet.nextIdleActivity=pet.nextPlayful=1;
                pet.manualSwim.Input.Set(Keys.D,true);pet.manualSwim.Input.Set(Keys.Space,true);pet.Advance(.01,away);
                ManualSwimTests.Check(pet.manualSwim.BoostReadyAt==5.01 && pet.nextManualBoostAt==5.01,"Space did not start boost");
                ManualSwimTests.Check(pet.manualBoostAnimation!=null && pet.manualBoostAnimation.Started==.01,"accepted Space did not start new illustrated animation");
                BoostAnimation firstClip=pet.manualBoostAnimation;
                using(Bitmap actual=pet.RenderFrame())using(Bitmap expected=BoostAnimationRenderer.Draw(pet.atlas,firstClip,pet.Now,pet.prefs.Size,pet.bubble,true,90,pet.swimFraction)){
                    ManualBoostRenderer.DrawStatus(expected,pet.Now,pet.manualSwim.BoostUntil,pet.manualSwim.BoostReadyAt);
                    FacingTests.Same(actual,expected,"dedicated boost artwork through the real render path");
                }
                pet.manualSwim.Input.Set(Keys.Space,false);pet.manualSwim.Input.Set(Keys.Space,true);pet.Advance(.02,away);
                ManualSwimTests.Check(pet.manualBoostAnimation==firstClip,"cooldown rejection restarted the animation");
                string speech=pet.bubble;
                for(int i=0;i<24;i++)pet.Advance(pet.Now+.02,away);
                ManualSwimTests.Check(pet.manualWake.Count>0 && pet.manualSwim.BoostUntil<pet.Now && pet.nextIdleActivity==1 && pet.nextPlayful==1,"boost wake missing or activity clocks changed");
                ManualSwimTests.Check(pet.manualBoostAnimation==firstClip,"joyful settle ended with the impulse");
                pet.manualSwim.Input.Set(Keys.D,false);
                for(int i=0;i<280;i++){pet.manualSwim.Input.Set(Keys.Space,true);pet.Advance(pet.Now+.02,away);}
                ManualSwimTests.Check(pet.manualSwim.BoostReadyAt==5.01,"held Space repeated when cooldown expired");
                ManualSwimTests.Check(pet.manualBoostAnimation==null,"boost illustration never settled to regular swimming");
                pet.manualSwim.Input.Set(Keys.Space,false);pet.manualSwim.Input.Set(Keys.Space,true);pet.Advance(pet.Now+.01,away);
                double ready=pet.nextManualBoostAt;ManualSwimTests.Check(ready>10 && pet.nextIdleActivity==1 && pet.nextPlayful==1,"fresh boost altered pending timers");
                pet.StopManualSwimming();pet.Location=start;pet.StartManualSwimming();pet.manualSwim.Input.Set(Keys.Space,true);pet.Advance(pet.Now+.01,away);
                ManualSwimTests.Check(pet.nextManualBoostAt==ready && pet.manualSwim.BoostUntil<pet.Now,"mode re-entry bypassed cooldown");
                int spoken=0,silent=0,last=-1,lastStyle=pet.lastBoostStyle;HashSet<int> seenStyles=new HashSet<int>();
                for(int i=0;i<60;i++){
                    pet.StopManualSwimming();pet.Location=start;pet.StartManualSwimming();pet.manualSwim.Input.Set(Keys.Space,true);pet.Advance(Math.Max(pet.Now+.01,pet.nextManualBoostAt),away);
                    ManualSwimTests.Check(pet.manualBoostAnimation!=null && pet.lastBoostStyle!=lastStyle,"actual boost expression immediately repeated");
                    lastStyle=pet.lastBoostStyle;seenStyles.Add(lastStyle);
                    if(pet.bubble.Length>0){spoken++;ManualSwimTests.Check(pet.lastBoostLine!=last,"spoken boost lines repeated consecutively");last=pet.lastBoostLine;}else silent++;
                }
                ManualSwimTests.Check(spoken>10 && silent>10,"boost did not include both speech and quiet moments");
                ManualSwimTests.Check(seenStyles.Count==3,"actual timer never chose an expression");
                pet.StopManualSwimming();ManualSwimTests.Check(pet.manualBoostAnimation==null && pet.manualWake==null && pet.manualWakeWindow==null,"exit retained boost effects");
            }
        }
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
