using System;
using System.ComponentModel;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MochiDesktop {
    // Native group-placement fixture; production cleanup uses the companion alone.
    sealed class BackgroundTestWindow : NativeWindow,IDisposable {
        public BackgroundTestWindow(){CreateHandle(new CreateParams{Caption="Mochi placement test",ClassName="STATIC",Style=unchecked((int)0x80000000),ExStyle=0x80000|0x80|0x20|0x08000000,Width=1,Height=1});}
        public void Present(Bitmap frame,Point position){Native.SetBitmap(Handle,frame,position);}
        public void Hide(){Native.ShowWindow(Handle,0);}
        public void Dispose(){if(Handle!=IntPtr.Zero)DestroyHandle();}
        protected override void WndProc(ref Message m){Native.PreserveBackgroundOrder(ref m);base.WndProc(ref m);}
    }

    static class BackgroundPlacementTests {
        internal static void Check(bool condition,string message){if(!condition)throw new Exception("Background placement: "+message);}
        public static int Run(string path){
            try{
                Companion.VerifyBackgroundPlacement();
                Companion.VerifyNativePresentationRecovery();
                Companion.VerifyBackgroundRefresh();
                VerifyWindowGroup();VerifyEventSubscription();VerifyTransientDesktopMenus();
                File.WriteAllText(path,
                    "PASS: startup access-denied failure does not interrupt the welcome, rendering or timer; periodic invalid-window failure retries and recovers.\r\n"+
                    "PASS: one-second retry cadence after success and failure; sustained failures and long timer gaps do not cause a busy loop; manual mode suspends background placement.\r\n"+
                    "PASS: missing, closing and disposed handles are skipped; unrelated exceptions remain visible; real native placement failure is contained; successful native placement preserves bounds, non-topmost behavior and focus.\r\n"+
                    "PASS: transient bitmap, Manual guide, and wake placement failures preserve the last frame, use bounded retries, and recover; unrelated failures remain visible.\r\n"+
                    "PASS: shell/menu refresh requests restore placement on the next tick, coalesce bursts, respect failure backoff and Manual mode.\r\n"+
                    "PASS: native shell/menu event subscription receives notifications and releases callbacks on disposal.\r\n"+
                    "PASS: transient desktop menus and hidden utility windows do not move or hide a correctly placed scene; delayed shell changes are rechecked throughout menu settling.\r\n"+
                    "PASS: native grouped placement preserves bin/pet/cargo order, bounds, focus and non-topmost styles through repeated placement and foreground changes.\r\n");
                return 0;
            }catch(Exception error){File.WriteAllText(path,"FAIL: "+error);return 1;}
        }
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,System.Text.StringBuilder text,int count);
        static string WindowOrder(IntPtr first){
            string result="";int n=0;
            for(IntPtr w=GetWindow(first,0);w!=IntPtr.Zero && n++<30;w=GetWindow(w,2)){
                System.Text.StringBuilder title=new System.Text.StringBuilder(256);GetWindowText(w,title,256);
                result+=" / "+w+" owner="+GetWindow(w,4)+" style="+GetWindowLong(w,-20)+" "+title;
            }return result;
        }
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        static IntPtr NextVisible(IntPtr window){
            do{window=GetWindow(window,2);}while(window!=IntPtr.Zero && !IsWindowVisible(window));return window;
        }
        [DllImport("user32.dll")] static extern void NotifyWinEvent(uint kind,IntPtr window,int objectId,int childId);
        static void VerifyEventSubscription(){
            int calls=0;
            using(Form app=new Form())using(Native.BackgroundEvents events=new Native.BackgroundEvents(delegate{calls++;})){
                IntPtr handle=app.Handle;NotifyWinEvent(7,handle,0,0);
                for(int i=0;i<100 && calls==0;i++){Application.DoEvents();System.Threading.Thread.Sleep(2);}
                Check(calls>0,"native menu-end event did not request refresh");
                events.Dispose();int before=calls;NotifyWinEvent(7,handle,0,0);
                for(int i=0;i<10;i++){Application.DoEvents();System.Threading.Thread.Sleep(2);}
                Check(calls==before,"disposed event hooks still called back");
            }
        }
        sealed class PlacementProbe : System.Windows.Forms.NativeWindow,IDisposable {
            public int Moves;
            public PlacementProbe(string kind="STATIC"){
                IntPtr window=WallpaperNative.CreateWindowEx(0x80|0x08000000,kind,"Placement probe",unchecked((int)0x80000000),
                    10,10,32,32,IntPtr.Zero,IntPtr.Zero,WallpaperNative.GetModuleHandle(null),IntPtr.Zero);
                if(window==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());AssignHandle(window);
            }
            protected override void WndProc(ref System.Windows.Forms.Message message){
                if(message.Msg==0x46 && message.LParam!=IntPtr.Zero){
                    Native.WindowPosition p=(Native.WindowPosition)Marshal.PtrToStructure(message.LParam,typeof(Native.WindowPosition));
                    if((p.flags&4)==0)Moves++;
                }
                base.WndProc(ref message);
            }
            public void Dispose(){IntPtr window=Handle;if(window!=IntPtr.Zero){ReleaseHandle();WallpaperNative.DestroyWindow(window);}}
        }
        static void VerifyTransientDesktopMenus(){
            using(Form desktop=new Form{Text="Desktop menu fixture"})using(Form app=new Form{Text="Normal app fixture"})
            using(PlacementProbe back=new PlacementProbe())using(PlacementProbe front=new PlacementProbe())
            using(PlacementProbe menu=new PlacementProbe("#32768"))using(PlacementProbe hidden=new PlacementProbe()){
                desktop.Show();app.Show();Native.KeepBackgroundGroupAt(desktop.Handle,back.Handle,front.Handle);
                int backMoves=back.Moves,frontMoves=front.Moves;
                for(int n=0;n<20;n++){
                    // Explorer's native context menu can temporarily sit just above
                    // the desktop, or between the lowest app and the companion.
                    WallpaperNative.SetWindowPos(menu.Handle,n%2==0?back.Handle:app.Handle,0,0,0,0,0x53);
                    WallpaperNative.SetWindowPos(hidden.Handle,back.Handle,0,0,0,0,0x13);
                    Native.KeepBackgroundGroupAt(desktop.Handle,back.Handle,front.Handle);
                    Check(IsWindowVisible(back.Handle) && IsWindowVisible(front.Handle),"menu hid the scene");
                    Native.ShowWindow(menu.Handle,0);Native.KeepBackgroundGroupAt(desktop.Handle,back.Handle,front.Handle);
                }
                Check(back.Moves==backMoves && front.Moves==frontMoves,"menu churn unnecessarily repositioned the scene");
            }
        }
        static void VerifyWindowGroup(){
            using(Form desktop=new Form{Text="Desktop placement fixture"})
            using(Form app=new Form{Text="Foreground placement fixture"})
            using(Companion pet=new Companion(true))
            using(BackgroundTestWindow bin=new BackgroundTestWindow())using(BackgroundTestWindow cargo=new BackgroundTestWindow())
            using(Bitmap pixel=new Bitmap(32,32)){
                desktop.Show();app.Show();app.Activate();
                IntPtr handle=pet.Handle;using(Bitmap frame=new Bitmap(pet.Width,pet.Height))Native.SetBitmap(handle,frame,pet.Location);
                using(Graphics g=Graphics.FromImage(pixel))g.Clear(Color.Turquoise);
                bin.Present(pixel,new Point(200,200));cargo.Present(pixel,new Point(240,200));
                Rectangle bounds=pet.Bounds;IntPtr focus=Native.GetForegroundWindow();
                for(int i=0;i<20;i++){
                    Native.KeepBackgroundGroupAt(desktop.Handle,bin.Handle,handle,cargo.Handle);
                    Check(NextVisible(bin.Handle)==desktop.Handle && NextVisible(handle)==bin.Handle && NextVisible(cargo.Handle)==handle,"group changed layer order at "+i+": desktop="+desktop.Handle+", bin="+bin.Handle+" next="+GetWindow(bin.Handle,2)+", pet="+handle+" next="+GetWindow(handle,2)+", cargo="+cargo.Handle+" next="+GetWindow(cargo.Handle,2)+", app="+app.Handle+WindowOrder(handle));
                    Check((GetWindowLong(handle,-20)&8)==0 && (GetWindowLong(bin.Handle,-20)&8)==0 && (GetWindowLong(cargo.Handle,-20)&8)==0,"group entered topmost band");
                    Check(pet.Bounds==bounds && Native.GetForegroundWindow()==focus,"group moved or stole focus");
                    Check(NextVisible(app.Handle)==cargo.Handle,"scene covered an ordinary application");
                    IntPtr owner=GetWindow(handle,4);Check(owner==IntPtr.Zero || !IsWindowVisible(owner),"taskbar owner became visible");
                    // A menu owner can request activation without reordering one member.
                    Native.SetForegroundWindow(handle);Native.SetForegroundWindow(focus);
                }
                cargo.Hide();Native.KeepBackgroundGroupAt(desktop.Handle,bin.Handle,handle);
                Check(NextVisible(handle)==bin.Handle && NextVisible(bin.Handle)==desktop.Handle,"hidden cargo broke ordering");
                Native.KeepBackgroundGroupAt(IntPtr.Zero,bin.Handle,handle);
                Check(NextVisible(handle)==bin.Handle && NextVisible(bin.Handle)==IntPtr.Zero,"no-Explorer fallback changed order"+WindowOrder(handle));
            }
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
                    catch(System.ComponentModel.Win32Exception){nativeRejected=true;throw;}
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

        internal static void VerifyBackgroundRefresh(){
            using(Companion pet=new Companion(true)){
                IntPtr handle=pet.Handle;pet.modal=true;int calls=0;
                pet.backgroundPlacement=delegate{calls++;};pet.KeepBehindApps();
                pet.simulatedTime=.05;for(int n=0;n<40;n++)pet.RequestBackgroundRefresh();pet.Tick();
                BackgroundPlacementTests.Check(calls==2,"shell event waited for the one-second poll or produced a burst");
                pet.Tick();BackgroundPlacementTests.Check(calls==2,"refresh request was not consumed");
                pet.simulatedTime=.08;pet.Tick();BackgroundPlacementTests.Check(calls==3,"delayed Explorer change was not rechecked during settling");
                calls--;
                pet.backgroundPlacement=delegate{calls++;throw new Win32Exception(5);};
                pet.simulatedTime=.1;pet.RequestBackgroundRefresh();pet.Tick();
                for(int n=1;n<100;n++){pet.simulatedTime=.1+n/100.0;pet.RequestBackgroundRefresh();pet.Tick();}
                BackgroundPlacementTests.Check(calls==3,"event burst bypassed access-denied backoff");
                pet.backgroundPlacement=delegate{calls++;};pet.simulatedTime=1.11;pet.Tick();
                BackgroundPlacementTests.Check(calls==4,"event recovery did not retry");
                pet.modal=false;pet.StartManualSwimming();pet.RequestBackgroundRefresh();pet.simulatedTime=1.2;pet.Tick();
                BackgroundPlacementTests.Check(calls==4,"event disturbed Manual mode");
                pet.StopManualSwimming();pet.Tick();BackgroundPlacementTests.Check(calls==5,"pending refresh lost after Manual mode");
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
