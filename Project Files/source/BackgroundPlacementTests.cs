using System;
using System.ComponentModel;
using System.IO;

namespace MochiDesktop {
    static class BackgroundPlacementTests {
        internal static void Check(bool condition,string message){if(!condition)throw new Exception("Background placement: "+message);}
        public static int Run(string path){
            try{
                Companion.VerifyBackgroundPlacement();
                File.WriteAllText(path,
                    "PASS: startup access-denied failure does not interrupt the welcome, rendering or timer; periodic invalid-window failure retries and recovers.\r\n"+
                    "PASS: one-second retry cadence after success and failure; sustained failures and long timer gaps do not cause a busy loop; manual mode suspends background placement.\r\n"+
                    "PASS: missing, closing and disposed handles are skipped; unrelated exceptions remain visible; real native placement failure is contained; successful native placement preserves bounds, non-topmost behavior and focus.\r\n");
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
    }
}
