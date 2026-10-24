using System;
using System.ComponentModel;
using System.IO;

namespace MochiDesktop {
    static class BackgroundPlacementTests {
        internal static void Check(bool condition,string message){if(!condition)throw new Exception("Background placement: "+message);}
        public static int Run(string path){
            try{
                Companion.VerifyBackgroundPlacement();
                Companion.VerifyNativePresentationRecovery();
                File.WriteAllText(path,
                    "PASS: startup access-denied failure does not interrupt the welcome, rendering or timer; periodic invalid-window failure retries and recovers.\r\n"+
                    "PASS: one-second retry cadence after success and failure; sustained failures and long timer gaps do not cause a busy loop; manual mode suspends background placement.\r\n"+
                    "PASS: missing, closing and disposed handles are skipped; unrelated exceptions remain visible; real native placement failure is contained; successful native placement preserves bounds, non-topmost behavior and focus.\r\n"+
                    "PASS: transient bitmap, Manual guide, and wake placement failures preserve the last frame, use bounded retries, and recover; unrelated failures remain visible.\r\n");
                return 0;
            }catch(Exception error){File.WriteAllText(path,"FAIL: "+error);return 1;}
        }
    }

    sealed partial class Companion {
        internal static void VerifyBackgroundPlacement(){
            int attempts=0;
            using(Companion pet=new Companion(true)){
                pet.backgroundPlacement=delegate(IntPtr window){
                    attempts++;
                    if(attempts==1)throw new Win32Exception(5,"Access is denied");
                    if(attempts==2)throw new Win32Exception(1400,"Invalid window handle");
                };
                pet.KeepBehindApps();
                BackgroundPlacementTests.Check(attempts==0 && !pet.IsHandleCreated,"placement created a premature window");
                IntPtr handle=pet.Handle;
                // Execute the same handler wired to the real Shown event, with no saved
                // preferences, update requests or real desktop icon manipulation.
                pet.CompanionShown(pet,EventArgs.Empty);
                BackgroundPlacementTests.Check(attempts==1 && pet.timer.Enabled && pet.startupHintAt==4 && pet.canvas!=null,
                    "access denied interrupted startup");
                pet.timer.Stop();pet.modal=true;
                for(int i=0;i<100;i++){pet.simulatedTime=i/100.0;pet.Tick();}
                BackgroundPlacementTests.Check(attempts==1,"startup failure retried before one second");
                pet.simulatedTime=1;pet.Tick();
                BackgroundPlacementTests.Check(attempts==2 && pet.nextBackgroundCheck==2,"periodic failure lost its retry");
                pet.simulatedTime=2;pet.Tick();
                BackgroundPlacementTests.Check(attempts==3 && pet.nextBackgroundCheck==3,"retry did not recover");
                pet.simulatedTime=2.99;pet.Tick();
                BackgroundPlacementTests.Check(attempts==3,"successful placement changed the normal cadence");

                pet.backgroundPlacement=delegate(IntPtr window){attempts++;throw new Win32Exception(5);};
                for(int second=3;second<13;second++){
                    pet.simulatedTime=second;
                    for(int tick=0;tick<60;tick++)pet.Tick();
                }
                BackgroundPlacementTests.Check(attempts==13,"sustained denial caused a busy loop or stopped retries");
                pet.simulatedTime=100;pet.Tick();pet.Tick();
                BackgroundPlacementTests.Check(attempts==14 && pet.nextBackgroundCheck==101,"suspension caused a retry burst");

                pet.modal=false;pet.StartManualSwimming();pet.simulatedTime=102;pet.Tick();
                BackgroundPlacementTests.Check(attempts==14 && pet.manualSwim!=null,"placement interrupted manual mode");
                pet.StopManualSwimming();pet.modal=true;pet.Tick();
                BackgroundPlacementTests.Check(attempts==15,"placement did not resume after manual mode");

                // Exercise the real Win32 failure path, independently of injected errors.
                bool nativeRejected=false,nativeSucceeded=false;
                pet.backgroundPlacement=delegate(IntPtr window){
                    try{Native.KeepBehindApps(new IntPtr(-12345));}
                    catch(Win32Exception){nativeRejected=true;throw;}
                };
                pet.KeepBehindApps();
                BackgroundPlacementTests.Check(nativeRejected,"native invalid-window fixture did not fail");
                pet.backgroundPlacement=delegate(IntPtr window){Native.KeepBehindApps(window);nativeSucceeded=true;};
                System.Drawing.Rectangle bounds=pet.Bounds;IntPtr foreground=Native.GetForegroundWindow();
                pet.KeepBehindApps();
                BackgroundPlacementTests.Check(nativeSucceeded && pet.Bounds==bounds && !pet.TopMost && Native.GetForegroundWindow()==foreground,
                    "native placement changed position, size, topmost state or focus");

                pet.backgroundPlacement=delegate(IntPtr window){throw new InvalidOperationException("unrelated failure");};
                bool propagated=false;
                try{pet.KeepBehindApps();}catch(InvalidOperationException){propagated=true;}
                BackgroundPlacementTests.Check(propagated,"unrelated exception was swallowed");
                pet.closing=true;pet.KeepBehindApps();
            }
            using(Companion pet=new Companion(true)){
                IntPtr handle=pet.Handle;
                pet.backgroundPlacement=delegate(IntPtr window){throw new Exception("disposed window was placed");};
                pet.Dispose();pet.KeepBehindApps();
                BackgroundPlacementTests.Check(!pet.IsHandleCreated,"placement recreated a disposed handle");
            }
        }

        internal static void VerifyNativePresentationRecovery(){
            using(Companion pet=new Companion(true)){
                IntPtr handle=pet.Handle;pet.nextBackgroundCheck=Double.PositiveInfinity;
                int calls=0;
                pet.bitmapPresentation=delegate(IntPtr window,System.Drawing.Bitmap bitmap,System.Drawing.Point location,byte opacity){calls++;};
                pet.simulatedTime=0;pet.Render();
                System.Drawing.Bitmap retained=pet.canvas;
                BackgroundPlacementTests.Check(calls==1 && retained!=null,"initial bitmap presentation failed");
                calls=0;
                pet.bitmapPresentation=delegate(IntPtr window,System.Drawing.Bitmap bitmap,System.Drawing.Point location,byte opacity){
                    calls++;if(calls==1)throw new Win32Exception(5,"Access is denied");
                };
                pet.simulatedTime=.01;pet.Tick();
                BackgroundPlacementTests.Check(calls==1 && Object.ReferenceEquals(retained,pet.canvas),"failed bitmap replaced the last successful frame");
                for(int i=2;i<25;i++){pet.simulatedTime=i/100.0;pet.Tick();}
                BackgroundPlacementTests.Check(calls==1,"bitmap failure retried on the 15 ms timer");
                pet.simulatedTime=.26;pet.Tick();
                BackgroundPlacementTests.Check(calls==2 && !Object.ReferenceEquals(retained,pet.canvas),"bitmap presentation did not recover");
                pet.bitmapPresentation=delegate(IntPtr window,System.Drawing.Bitmap bitmap,System.Drawing.Point location,byte opacity){throw new InvalidOperationException("unrelated bitmap failure");};
                bool propagated=false;pet.simulatedTime=.27;
                try{pet.Tick();}catch(InvalidOperationException){propagated=true;}
                BackgroundPlacementTests.Check(propagated,"unrelated bitmap exception was swallowed");
                pet.bitmapPresentation=delegate{};
            }

            using(Companion pet=new Companion(true)){
                IntPtr handle=pet.Handle;pet.nextBackgroundCheck=Double.PositiveInfinity;pet.bitmapPresentation=delegate{};
                pet.StartManualSwimming();
                System.Drawing.Rectangle area=System.Windows.Forms.Screen.FromRectangle(pet.Bounds).Bounds;
                pet.manualOverlay=new ManualSwimOverlay(area,pet.atlas,pet.faceRight,pet.manualSwim.Input,delegate{},0);
                int calls=0;
                pet.manualGuidePlacement=delegate(ManualSwimOverlay overlay,IntPtr window){calls++;if(calls==1)throw new Win32Exception(1400,"Invalid window handle");};
                pet.simulatedTime=0;pet.Tick();
                pet.simulatedTime=.1;pet.Tick();pet.simulatedTime=.2;pet.Tick();
                BackgroundPlacementTests.Check(calls==1,"Manual guide failure retried too quickly");
                pet.simulatedTime=.25;pet.Tick();
                BackgroundPlacementTests.Check(calls==2,"Manual guide placement did not recover");
                pet.manualGuidePlacement=delegate{throw new InvalidOperationException("unrelated guide failure");};
                bool propagated=false;pet.simulatedTime=.26;
                try{pet.Tick();}catch(InvalidOperationException){propagated=true;}
                BackgroundPlacementTests.Check(propagated,"unrelated Manual guide exception was swallowed");
                pet.manualGuidePlacement=delegate{};
            }

            using(Companion pet=new Companion(true)){
                IntPtr handle=pet.Handle;pet.nextBackgroundCheck=Double.PositiveInfinity;pet.bitmapPresentation=delegate{};
                pet.StartManualSwimming();pet.simulateNativePresentation=true;
                System.Drawing.PointF from=new System.Drawing.PointF(pet.Left+pet.Width/2,pet.Top+pet.Height/2);
                pet.manualWake.Advance(0,from,new System.Drawing.PointF(from.X+12,from.Y),new System.Drawing.PointF(600,0),pet.prefs.Size,true);
                int presented=0,placed=0;
                pet.manualWakePresentation=delegate(System.Drawing.Bitmap bitmap,System.Drawing.Rectangle bounds){
                    presented++;if(presented==1)throw new Win32Exception(8,"Not enough memory");return presented==2;
                };
                pet.manualWakePlacement=delegate{placed++;if(placed==1)throw new Win32Exception(5,"Access is denied");};
                pet.simulatedTime=0;pet.Tick();pet.simulatedTime=.1;pet.Tick();pet.simulatedTime=.2;pet.Tick();
                BackgroundPlacementTests.Check(presented==1 && placed==0,"wake bitmap failure retried too quickly");
                pet.simulatedTime=.25;pet.Tick();
                BackgroundPlacementTests.Check(presented==2 && placed==1 && pet.manualWakeNeedsPlacement,"wake placement failure lost the pending placement");
                pet.simulatedTime=.4;pet.Tick();
                BackgroundPlacementTests.Check(presented==2 && placed==1,"wake placement failure retried too quickly");
                pet.simulatedTime=.5;pet.Tick();
                BackgroundPlacementTests.Check(presented==3 && placed==2 && !pet.manualWakeNeedsPlacement,"wake presentation or placement did not recover");
                pet.manualWakePresentation=delegate{throw new InvalidOperationException("unrelated wake failure");};
                bool propagated=false;pet.simulatedTime=.51;
                try{pet.Tick();}catch(InvalidOperationException){propagated=true;}
                BackgroundPlacementTests.Check(propagated,"unrelated wake exception was swallowed");
                pet.manualWakePresentation=delegate{return false;};pet.manualWakePlacement=delegate{};
            }
        }
    }
}
