#if MOCHI_TESTS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace MochiDesktop {
    static class SwimContinuityTests {
        public static int Run(string report){
            List<string> results=new List<string>();
            try{
                Companion.VerifySwimContinuity(results);
                Companion.VerifyWindowGeometryOwnership(results);
                File.WriteAllLines(report,results.ToArray());return 0;
            }catch(Exception error){results.Add("FAIL: "+error);File.WriteAllLines(report,results.ToArray());return 1;}
        }
    }
    sealed partial class Companion {
        [DllImport("user32.dll",EntryPoint="SetWindowPos",SetLastError=true)]
        static extern bool TryMoveTestWindow(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        internal static void VerifyWindowGeometryOwnership(List<string> results){
            Point away=new Point(-10000,-10000);
            foreach(int size in new[]{144,176,224})using(Companion pet=new Companion(true)){
                pet.prefs.Size=size;pet.PetLocation=new Point(650,300);
                pet.nextIdleActivity=pet.nextSwim=Double.PositiveInfinity;
                IntPtr window=pet.Handle;pet.Render();
                Rectangle visible=pet.Bounds;Point position=pet.PetLocation;
                foreach(uint flags in new uint[]{0x15,0x16,0x14}){
                    CheckSwim(TryMoveTestWindow(window,IntPtr.Zero,0,0,1920,1080,flags),"native geometry fixture failed");
                    CheckSwim(pet.Bounds==visible && pet.PetLocation==position,"external move/resize displaced the displayed pet");
                }
                pet.BeginSwim(true);
                Point origin=pet.PetLocation;
                for(int frame=1;frame<=30;frame++){
                    CheckSwim(TryMoveTestWindow(window,IntPtr.Zero,0,0,1920,1080,0x14),"native geometry fixture failed during swimming");
                    pet.AdvanceFrame(frame/60.0,away);
                    CheckSwim(pet.Location==pet.PetLocation && pet.Size==pet.PetSize,"native and model geometry diverged during normal swimming");
                    CheckSwim(PlayfulPlan.Distance(origin,pet.PetLocation)<100,"swim jumped to the desktop origin");
                }
                pet.swimming=false;pet.MoveDragged(new Point(420,220));pet.Render();
                CheckSwim(pet.Location==new Point(420,220),"geometry ownership blocked dragging");
                pet.prefs.Size=size==144?224:144;pet.Render();
                CheckSwim(pet.Location==pet.PetLocation && pet.Size==pet.PetSize,"geometry ownership blocked a size preference");
                pet.StartManualSwimming();pet.manualSwim.Input.Set(System.Windows.Forms.Keys.Right,true);
                pet.Advance(pet.Now+.03,away);pet.StopManualSwimming();
                CheckSwim(pet.Location==pet.PetLocation && pet.Size==pet.PetSize,"geometry ownership blocked manual swimming");
            }
            results.Add("PASS: native moves to (0,0), oversized native bounds and combined move/resize requests cannot relocate or resize a visible idle/swimming pet; normal frame presentation, dragging, size changes and Manual mode still work at every pet size.");
            foreach(Rectangle area in new[]{new Rectangle(0,0,1280,1024),new Rectangle(-1920,-200,1920,1080)})
            using(Companion pet=new Companion(true)){
                Point position=new Point(area.Left+500,area.Top+250);pet.PetLocation=position;
                pet.nextIdleActivity=pet.nextSwim=Double.PositiveInfinity;
                IntPtr window=pet.Handle;pet.Render();
                CleanupTrip trip=new CleanupTrip(position,pet.prefs.Size,area,new Random(9),OceanLitter.Bottle,0,0);
                pet.cleanup=trip;pet.Render();
                CheckSwim(pet.Bounds==area && pet.PetLocation==position && pet.PetSize!=area.Size,"cleanup did not keep separate character and scene geometry");
                Bitmap before=pet.canvas,hit=pet.cleanupHitFrame;
                pet.bitmapPresentation=delegate{throw new System.ComponentModel.Win32Exception(5,"Transient end-of-cleanup upload failure");};
                pet.CancelCleanup();
                CheckSwim(pet.cleanup==null && pet.PetLocation==position && pet.Bounds==area && Object.ReferenceEquals(before,pet.canvas) && Object.ReferenceEquals(hit,pet.cleanupHitFrame),"failed cleanup handoff changed the old frame, position or hit mask");
                pet.StartSwimPath(new SwimPath(position,pet.PetSize,area,new Random(4),false),area,false);
                pet.AdvanceFrame(.015,away);
                CheckSwim(PlayfulPlan.Distance(position,pet.PetLocation)<=2 && pet.PetLocation!=area.Location,"next swim adopted the cleanup surface origin or size");
                pet.bitmapPresentation=Native.SetBitmap;
                pet.AdvanceFrame(.3,away);
                CheckSwim(pet.Location==pet.PetLocation && pet.Size==pet.PetSize && pet.cleanupHitFrame==null,"cleanup handoff did not recover atomically");
            }
            results.Add("PASS: cleanup starts at the character's existing position, and a failed cleanup-to-swimming frame retains the old scene/hit mask while logical position and footprint remain independent; retry restores the pet-sized window on zero/negative-coordinate screens.");
        }
        static void CheckSwim(bool ok,string message){if(!ok)throw new Exception("Swim continuity: "+message);}
        internal static void VerifySwimContinuity(List<string> results){
            Point away=new Point(-10000,-10000);
            Rectangle[] areas={new Rectangle(0,0,1920,1080),new Rectangle(-1920,-200,1920,1080),new Rectangle(0,0,900,1500)};
            foreach(Rectangle area in areas)foreach(int size in new[]{144,176,224})foreach(bool guided in new[]{false,true}){
                using(Companion pet=new Companion(true)){
                    pet.prefs.Size=size;pet.Size=Renderer.WindowSize(size);
                    pet.PetLocation=new Point(area.Left+70,area.Top+70);
                    pet.nextIdleActivity=pet.nextPlayful=pet.nextSwim=Double.PositiveInfinity;
                    SwimPath path=new SwimPath(pet.Location,pet.Size,area,new Random(size),false,new Point(area.Right-pet.Width-80,area.Bottom-pet.Height-80));
                    pet.StartSwimPath(path,area,guided);
                    double now=0,elapsed=0;
                    for(int frame=0;frame<60;frame++){
                        now+=1.0/60;elapsed+=1.0/60;pet.AdvanceFrame(now,away);
                        CheckSwim(pet.Location==Point.Round(path.Position(elapsed/pet.travelDuration)),"normal frame timing changed");
                    }
                    foreach(double gap in new[]{.12,.5,1.5,10,300,3600}){
                        Point before=pet.Location;now+=gap;elapsed+=.05;
                        pet.AdvanceFrame(now,away);
                        CheckSwim(pet.swimming && pet.swimPath==path && pet.Location==Point.Round(path.Position(elapsed/pet.travelDuration)),"missed frames skipped ahead or replaced the route");
                        double maxStep=path.Length/pet.travelDuration*1.875*.05+2;
                        CheckSwim(PlayfulPlan.Distance(before,pet.Location)<=maxStep,"resume jumped beyond one bounded movement step");
                        CheckSwim(area.Contains(pet.Bounds),"delayed frame changed screens");
                        for(int frame=0;frame<10;frame++){
                            now+=1.0/60;elapsed+=1.0/60;pet.AdvanceFrame(now,away);
                            CheckSwim(pet.Location==Point.Round(path.Position(elapsed/pet.travelDuration)),"deferred time was replayed on later frames");
                        }
                    }
                    Point paused=pet.Location;pet.modal=true;now+=30;pet.AdvanceFrame(now,away);pet.modal=false;
                    CheckSwim(pet.Location==paused,"paused UI moved Mochi");
                    now+=1.0/60;elapsed+=1.0/60;pet.AdvanceFrame(now,away);
                    CheckSwim(pet.Location==Point.Round(path.Position(elapsed/pet.travelDuration)),"modal pause caught up afterward");
                    for(int frame=0;pet.swimming && frame<10000;frame++){now+=1.0/60;pet.AdvanceFrame(now,away);}
                    CheckSwim(!pet.swimming && pet.Location==path.End,"bounded movement never finished or changed the destination");
                    pet.StartSwimPath(new SwimPath(pet.Location,pet.Size,area,new Random(8),false),area,guided);
                    CheckSwim(pet.lastSwimFrame==pet.Now,"new route inherited the old delayed clock");
                    Point restart=pet.Location;pet.AdvanceFrame(pet.Now+1.0/60,away);
                    CheckSwim(PlayfulPlan.Distance(restart,pet.Location)<=2,"new route teleported on its first frame");
                }
            }
            results.Add("PASS: normal/guided swims across all sizes and landscape/portrait/negative-coordinate screens survive 120ms to one-hour frame gaps with bounded movement, no catch-up, unchanged routes, exact arrivals, modal pauses and fresh clocks on subsequent swims.");
            foreach(int fps in new[]{20,30,60,120})using(Companion pet=new Companion(true)){
                pet.PetLocation=new Point(300,250);pet.nextIdleActivity=pet.nextSwim=Double.PositiveInfinity;
                Rectangle area=new Rectangle(0,0,1920,1080);SwimPath path=new SwimPath(pet.Location,pet.Size,area,new Random(27),false,new Point(1200,500));
                pet.StartSwimPath(path,area,false);double duration=pet.travelDuration;
                int count=(int)Math.Ceiling(duration*fps);
                for(int i=1;i<=count;i++){
                    double now=i/(double)fps;pet.AdvanceFrame(now,away);
                    CheckSwim(pet.Location==Point.Round(path.Position(now/duration)),"normal speed/easing changed at "+fps+" fps");
                }
                CheckSwim(!pet.swimming && pet.Location==path.End,"normal-rate arrival changed");
            }
            results.Add("PASS: 20/30/60/120 fps retain the original time-based speed, acceleration, deceleration and arrival time.");
        }
    }
}
#endif
