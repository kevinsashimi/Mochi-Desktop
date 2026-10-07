using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MochiDesktop {
    static class StaticWallpaperTests {
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        public static int Run(string path){
            List<string> report=new List<string>();
            try {
                Check(WallpaperNative.HasDesktopManifest(),"missing Windows compatibility manifest");
                string fixture=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)),"static-preferences.xml");
                Check(!new Preferences().StaticWallpaper,"fresh installs must start with wallpaper off");
                File.WriteAllText(fixture,"<Mochi><OceanWallpaper>true</OceanWallpaper><Size>224</Size><PlayfulMode>true</PlayfulMode><Roam>false</Roam></Mochi>");
                Preferences p=Preferences.Load(fixture);
                Check(!p.StaticWallpaper && p.PlayfulMode && !p.Roam && p.Size==224,"legacy live wallpaper must not opt into new wallpaper");
                using(SettingsDialog form=new SettingsDialog(p)){
                    form.Show();Application.DoEvents();
                    Check(!((CheckBox)form.Controls.Find("StaticWallpaperToggle",true)[0]).Checked,"Settings default must be off");
                    ((CheckBox)form.Controls.Find("StaticWallpaperToggle",true)[0]).Checked=true;
                    ((Button)form.CancelButton).PerformClick();Check(!p.StaticWallpaper,"Cancel applied wallpaper");
                }
                using(SettingsDialog form=new SettingsDialog(p)){
                    form.Show();Application.DoEvents();
                    ((CheckBox)form.Controls.Find("StaticWallpaperToggle",true)[0]).Checked=true;
                    ((Button)form.AcceptButton).PerformClick();Check(p.StaticWallpaper,"Save did not apply wallpaper");
                }
                p.Save(fixture);p=Preferences.Load(fixture);
                Check(p.StaticWallpaper && p.PlayfulMode && !p.Roam && p.Size==224,"opt-in or other settings lost on restart");
                p.StaticWallpaper=false;p.Save(fixture);Check(!Preferences.Load(fixture).StaticWallpaper,"opt-out lost on restart");
                report.Add("PASS: fresh installs and legacy live-wallpaper settings default off; Save, Cancel, opt-in/out persistence and other preferences.");
                Assembly assembly=Assembly.GetExecutingAssembly();
                foreach(string name in assembly.GetManifestResourceNames())Check(!name.StartsWith("Mochi.Ocean."),"old live artwork/shader is still embedded");
                Check(assembly.GetType("MochiDesktop.OceanRenderer")==null && assembly.GetType("MochiDesktop.OceanMotion")==null,"live renderer remains compiled");
                using(Bitmap art=QuietCoveArt.Load()){
                    Check(art.Width==3840 && art.Height==2160,"unexpected new artwork dimensions");
                    Check(QuietCoveArt.Fit(art.Size,art.Size)==new RectangleF(PointF.Empty,art.Size),"4K must fill the display at 1:1 without surround");
                    foreach(Size size in new[]{new Size(3840,2160),new Size(1920,1080),new Size(3440,1440),new Size(1080,1920),new Size(7680,2160),new Size(640,480)}){
                        RectangleF fit=QuietCoveArt.Fit(size,art.Size);
                        Check(fit.Width<=art.Width+.01 && fit.Height<=art.Height+.01,"art enlarged");
                        Check(Math.Abs(fit.Width/fit.Height-art.Width/(float)art.Height)<.001,"art stretched");
                        Check(fit.X>=0 && fit.Y>=0 && fit.Right<=size.Width+.01 && fit.Bottom<=size.Height+.01,"art cropped");
                    }
                }
                report.Add("PASS: new Quiet Cove resource; live artwork/renderer absent; full 3840 x 2160 painting at 1:1 with no surround; proportional ultrawide, portrait and smaller layouts without enlargement.");
                using(StaticWallpaper wallpaper=new StaticWallpaper()){
                    Check(!wallpaper.IsEnabled && !wallpaper.HasArtwork,"disabled wallpaper loads artwork or enables work");
                    wallpaper.SetEnabled(false);Check(wallpaper.Status.StartsWith("Off"),"default wallpaper is not off");
                    wallpaper.SetEnabled(true);wallpaper.SetEnabled(false);
                    Check(!wallpaper.IsEnabled && !wallpaper.HasArtwork && wallpaper.Status.StartsWith("Off"),"off must cancel startup and release artwork");
                }
                report.Add("PASS: disabled wallpaper loads no artwork; turning off cancels startup and releases its resources.");
                VerifyHosts();
                report.Add("PASS: controlled native fixtures select classic and raised desktop hosts, reject unsafe hosts, and detect host changes/destruction.");
                string failure=WallpaperNative.FailureDetails(new System.ComponentModel.Win32Exception(5,"Could not create wallpaper."));
                Check(failure.Contains("Win32 5 (0x00000005)"),"native error code hidden");
                report.Add("PASS: native Windows compatibility manifest and actionable Win32 diagnostic errors.");
                File.WriteAllLines(path,report.ToArray());return 0;
            }catch(Exception error){report.Add("FAIL: "+error);File.WriteAllLines(path,report.ToArray());return 1;}
        }
        static IntPtr Fixture(string name,IntPtr parent){
            WallpaperNative.RegisterCanvasClass(name,false);
            IntPtr window=WallpaperNative.CreateWindowEx(0x08000080,name,"Mochi wallpaper test fixture",unchecked((int)(parent==IntPtr.Zero?0x90000000:0x50000000)),0,0,100,100,parent,IntPtr.Zero,WallpaperNative.GetModuleHandle(null),IntPtr.Zero);
            Check(window!=IntPtr.Zero,"native desktop fixture creation");return window;
        }
        static void VerifyHosts(){
            IntPtr root=Fixture("MochiStaticTestDesktop",IntPtr.Zero),sibling=IntPtr.Zero;
            try{
                IntPtr view=Fixture("SHELLDLL_DefView",root);DesktopWallpaperHost found;
                sibling=Fixture("WorkerW",IntPtr.Zero);
                WallpaperNative.SetWindowPos(sibling,root,0,0,0,0,0x1|0x2|0x10);
                found=WallpaperNative.FindExistingHost(root);
                Check(found!=null && found.Parent==sibling && found.Icons==view && !found.Layered,"blank sibling behind icons must be selected");
                IntPtr wrong=Fixture("SHELLDLL_DefView",sibling);
                Check(WallpaperNative.FindExistingHost(root)==null,"an icon-bearing WorkerW must never receive wallpaper");
                WallpaperNative.DestroyWindow(wrong);WallpaperNative.DestroyWindow(sibling);sibling=IntPtr.Zero;
                IntPtr child=Fixture("WorkerW",root);
                WallpaperNative.SetWindowPos(child,view,0,0,0,0,0x1|0x2|0x10);
                found=WallpaperNative.FindExistingHost(root);
                Check(found!=null && found.Parent==child && found.Icons==view && !found.Layered,"blank child behind icon view must be selected");
                Check(WallpaperNative.HostAlive(found),"valid desktop host rejected");
                WallpaperNative.SetWindowLong(root,-20,WallpaperNative.GetWindowLong(root,-20)|0x00200000);
                Check(!WallpaperNative.HostAlive(found),"switch to raised desktop must rebuild attachment");
                sibling=Fixture("WorkerW",IntPtr.Zero);
                WallpaperNative.SetWindowPos(sibling,root,0,0,0,0,0x1|0x2|0x10);
                found=WallpaperNative.FindExistingHost(root);
                Check(found!=null && found.Parent==root && found.Wallpaper==child && found.Icons==view && found.Layered,"raised desktop needs a layered sibling, not a stale top-level or nested WorkerW");
                Check(WallpaperNative.HostAlive(found),"raised desktop rejected");
                WallpaperNative.DestroyWindow(child);Check(!WallpaperNative.HostAlive(found),"destroyed desktop must trigger recovery");
                Check(WallpaperNative.FindExistingHost(root)==null,"raised desktop must not fall back beneath an opaque desktop");
            }finally{if(sibling!=IntPtr.Zero)WallpaperNative.DestroyWindow(sibling);WallpaperNative.DestroyWindow(root);}
        }
        // Exercise native GDI presentation in controlled windows, never real desktop items.
        public static int Preview(string directory){
            Directory.CreateDirectory(directory);List<string> report=new List<string>();
            try {
                VerifyLayeredPresentation(directory,report);
                using(Bitmap art=QuietCoveArt.Load())foreach(Size size in new[]{new Size(1920,1080),new Size(3840,2160),new Size(3440,1440),new Size(1080,1920)}){
                    using(Form parent=new Form{Text="Quiet Cove verification",FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Location=Point.Empty,ClientSize=size,AutoScaleMode=AutoScaleMode.None}){
                        parent.Show();Application.DoEvents();WallpaperNative.SetWindowLong(parent.Handle,-16,GetWindowLong(parent.Handle,-16)|0x02000000);IntPtr foreground=GetForegroundWindow(),child;
                        using(StaticWallpaperSurface surface=new StaticWallpaperSurface(parent.Handle,new Rectangle(Point.Empty,size),art)){
                            child=surface.Handle;Application.DoEvents();
                            WallpaperNative.Rect actual;
                            Check(GetParent(child)==parent.Handle && WallpaperNative.GetClientRect(child,out actual) && actual.Value.Size==size,"native child dimensions/attachment");
                            Check((GetWindowLong(child,-20)&0x08000000)==0x08000000,"no-activate/click-through styles missing");
                            Check(SendMessage(child,0x84,IntPtr.Zero,IntPtr.Zero)==new IntPtr(-1) && SendMessage(child,0x21,IntPtr.Zero,IntPtr.Zero)==new IntPtr(3),"wallpaper blocks input");
                            Check(GetForegroundWindow()==foreground,"wallpaper stole focus");
                            Check(surface.PaintCount>0 && surface.PaintError==null,"GDI did not paint: "+surface.PaintError);
                            int paints=surface.PaintCount;Stopwatch wait=Stopwatch.StartNew();
                            while(wait.ElapsedMilliseconds<350){Application.DoEvents();Thread.Sleep(10);}
                            Check(surface.PaintCount==paints,"static wallpaper keeps repainting");
                            using(Bitmap capture=new Bitmap(size.Width,size.Height)){
                                using(Graphics graphics=Graphics.FromImage(capture))graphics.CopyFromScreen(Point.Empty,Point.Empty,size);
                                if(size==art.Size){
                                    for(int y=0;y<size.Height;y+=17)for(int x=0;x<size.Width;x+=17)
                                        Check(capture.GetPixel(x,y).ToArgb()==art.GetPixel(x,y).ToArgb(),"4K desktop must preserve source pixels without resampling");
                                    Check(capture.GetPixel(size.Width-1,size.Height-1).ToArgb()==art.GetPixel(size.Width-1,size.Height-1).ToArgb(),"4K edge must contain artwork");
                                }
                                capture.Save(Path.Combine(directory,"quiet-cove-desktop-"+size.Width+"x"+size.Height+".png"),ImageFormat.Png);
                                using(Bitmap expected=new Bitmap(size.Width,size.Height)){
                                    using(Graphics graphics=Graphics.FromImage(expected))QuietCoveArt.Paint(graphics,size,art);
                                    foreach(Point point in new[]{new Point(size.Width/2,size.Height/2),new Point(8,8),new Point(size.Width-8,size.Height-8)})
                                        Check(capture.GetPixel(point.X,point.Y).ToArgb()==expected.GetPixel(point.X,point.Y).ToArgb(),"native wallpaper differs at "+point+": "+capture.GetPixel(point.X,point.Y)+" vs "+expected.GetPixel(point.X,point.Y));
                                }
                            }
                        }
                        Check(!WallpaperNative.IsWindow(child),"wallpaper window leaked");parent.Close();Application.DoEvents();
                    }
                    report.Add("PASS: "+size+" native static pixels, no continuous repaints, child attachment, click-through, focus preservation and cleanup.");
                }
                File.WriteAllLines(Path.Combine(directory,"render-results.txt"),report.ToArray());return 0;
            }catch(Exception error){report.Add("FAIL: "+error);File.WriteAllLines(Path.Combine(directory,"render-results.txt"),report.ToArray());return 1;}
        }
        static void VerifyLayeredPresentation(string directory,List<string> report){
            Size size=new Size(640,480);
            using(Bitmap art=QuietCoveArt.Load())
            using(Form parent=new Form{FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Location=new Point(40,40),ClientSize=size,BackColor=Color.Magenta})
            using(Panel background=new Panel{Bounds=new Rectangle(Point.Empty,size),BackColor=Color.Magenta})
            using(Panel icons=new Panel{Bounds=new Rectangle(10,10,50,50),BackColor=Color.Lime}){
                parent.Controls.Add(background);parent.Controls.Add(icons);icons.BringToFront();parent.Show();Application.DoEvents();
                IntPtr focus=GetForegroundWindow(),canvas,container;
                DesktopWallpaperHost host=new DesktopWallpaperHost(parent.Handle,icons.Handle,background.Handle,true);
                using(StaticWallpaperSurface surface=new StaticWallpaperSurface(parent.Handle,new Rectangle(parent.PointToScreen(Point.Empty),size),art,true,icons.Handle,true)){
                    canvas=surface.Handle;container=surface.DesktopHandle;surface.EnsurePlacement(host);
                    Check(container!=canvas && GetParent(canvas)==container && GetParent(container)==parent.Handle,"layered wallpaper hierarchy");
                    Check((GetWindowLong(container,-20)&0x80000)!=0,"raised desktop needs layered presentation");
                    Check((GetClassLong(container,-26)&0x20)==0,"layered window must not use CS_OWNDC");
                    Check(SendMessage(container,0x84,IntPtr.Zero,IntPtr.Zero)==new IntPtr(-1) && SendMessage(container,0x21,IntPtr.Zero,IntPtr.Zero)==new IntPtr(3),"layered container must pass input without activation");
                    Check(GetForegroundWindow()==focus,"layered wallpaper stole focus");
                    Application.DoEvents();Application.DoEvents();
                    using(Bitmap screenshot=new Bitmap(size.Width,size.Height)){
                        using(Graphics g=Graphics.FromImage(screenshot))g.CopyFromScreen(parent.PointToScreen(Point.Empty),Point.Empty,size);
                        screenshot.Save(Path.Combine(directory,"wallpaper-layered-desktop.png"));
                        Color wallpaper=screenshot.GetPixel(320,240),icon=screenshot.GetPixel(20,20);
                        Check(wallpaper.B>wallpaper.R && wallpaper.G>wallpaper.R && wallpaper.ToArgb()!=Color.Magenta.ToArgb(),"wallpaper is hidden behind the opaque wallpaper: "+wallpaper);
                        Check(icon.ToArgb()==Color.Lime.ToArgb(),"desktop icon was covered by the wallpaper: "+icon);
                    }
                    // Recover if Windows inserts its wallpaper above our own window.
                    WallpaperNative.SetWindowPos(background.Handle,icons.Handle,0,0,0,0,0x1|0x2|0x10);
                    surface.EnsurePlacement(host);
                    Check(WallpaperNative.IsBelow(container,icons.Handle) && WallpaperNative.IsBelow(background.Handle,container),"wallpaper did not recover its position between icons and background");
                }
                Check(!WallpaperNative.IsWindow(canvas) && !WallpaperNative.IsWindow(container),"layered wallpaper windows leaked");
                Check(WallpaperNative.IsWindow(icons.Handle) && WallpaperNative.IsWindow(background.Handle),"desktop fixture was modified on shutdown");
                background.Invalidate();parent.Refresh();Application.DoEvents();
                using(Bitmap restored=new Bitmap(1,1)){
                    using(Graphics g=Graphics.FromImage(restored))g.CopyFromScreen(parent.PointToScreen(new Point(320,240)),Point.Empty,new Size(1,1));
                    Check(restored.GetPixel(0,0).ToArgb()==Color.Magenta.ToArgb(),"usual wallpaper was not revealed on shutdown");
                }
                parent.Close();
            }
            report.Add("PASS: layered wallpaper is visibly above an opaque background and below icons; repairs z-order, passes input, preserves focus, and reveals original wallpaper on disposal (controlled fixture, not Explorer).");
        }
        [DllImport("user32.dll")] static extern uint GetClassLong(IntPtr window,int index);
        [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wp,IntPtr lp);
    }
}
