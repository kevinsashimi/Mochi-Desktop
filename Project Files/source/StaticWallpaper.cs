using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MochiDesktop {
    static class QuietCoveArt {
        public static Bitmap Load(){
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Mochi.Wallpaper.QuietCove"))
            using(Bitmap original=new Bitmap(stream))return new Bitmap(original);
        }
        // Keep the complete new painting and its proportions. Source pixels
        // are not enlarged. At 3840 x 2160 the full painting fills the display.
        public static RectangleF Fit(Size canvas,Size art){
            float scale=Math.Min(1,Math.Min(canvas.Width/(float)art.Width,canvas.Height/(float)art.Height));
            float width=art.Width*scale,height=art.Height*scale;
            return new RectangleF((canvas.Width-width)/2,(canvas.Height-height)/2,width,height);
        }
        public static void Paint(Graphics graphics,Size size,Bitmap art){
            graphics.PageUnit=GraphicsUnit.Pixel;
            using(LinearGradientBrush surround=new LinearGradientBrush(new Rectangle(Point.Empty,size),Color.FromArgb(9,66,86),Color.FromArgb(5,31,46),90))
                graphics.FillRectangle(surround,new Rectangle(Point.Empty,size));
            graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
            RectangleF fit=Fit(size,art.Size);
            if(fit.Size==art.Size)graphics.DrawImageUnscaled(art,(int)fit.X,(int)fit.Y);
            else graphics.DrawImage(art,fit);
        }
    }

    // A still image painted only on window creation/exposure. The two-second
    // shell check repairs desktop changes; it never advances or draws frames.
    // Windows' wallpaper configuration and desktop items remain untouched.
    sealed class StaticWallpaper : IDisposable {
        readonly Timer shellCheck=new Timer{Interval=2000};
        readonly List<StaticWallpaperSurface> surfaces=new List<StaticWallpaperSurface>();
        DesktopWallpaperHost desktop;Bitmap art;string displays="",lastError="";
        bool enabled,disposed;DateTime retryAfter=DateTime.MinValue;
        public string Status {get;private set;}
        internal bool IsEnabled {get{return enabled;}}
        internal bool HasArtwork {get{return art!=null;}}
        public StaticWallpaper(){Status="Off — your usual wallpaper is showing.";shellCheck.Tick+=CheckDesktop;}
        public void SetEnabled(bool value){
            if(disposed || enabled==value)return;
            enabled=value;
            if(!value){shellCheck.Stop();Clear();ReleaseArt();Status="Off — your usual wallpaper is showing.";return;}
            retryAfter=DateTime.MinValue;lastError="";
            CheckDesktop(null,EventArgs.Empty);shellCheck.Start();
        }
        void CheckDesktop(object sender,EventArgs args){
            if(!enabled || disposed || DateTime.UtcNow<retryAfter)return;
            try {
                WallpaperNative.MonitorArea[] monitors=WallpaperNative.Monitors();
                StringBuilder key=new StringBuilder();foreach(WallpaperNative.MonitorArea monitor in monitors)key.Append(monitor.Bounds);
                bool rebuild=!WallpaperNative.HostAlive(desktop) || displays!=key.ToString();
                foreach(StaticWallpaperSurface surface in surfaces)if(!WallpaperNative.IsWindow(surface.Handle) || !WallpaperNative.IsWindow(surface.DesktopHandle))rebuild=true;
                if(rebuild){
                    Clear();desktop=WallpaperNative.FindWallpaperHost();
                    if(desktop==null){Status="Waiting for the Windows desktop. Your usual wallpaper is still showing.";retryAfter=DateTime.UtcNow.AddSeconds(30);return;}
                    if(art==null)art=QuietCoveArt.Load();
                    displays=key.ToString();
                    using(WallpaperNative.DpiScope scope=new WallpaperNative.DpiScope(desktop.Parent)){
                        foreach(WallpaperNative.MonitorArea monitor in monitors)
                            surfaces.Add(new StaticWallpaperSurface(desktop.Parent,monitor.Bounds,art,true,desktop.Icons,desktop.Layered));
                    }
                }
                foreach(StaticWallpaperSurface surface in surfaces){
                    surface.EnsurePlacement(desktop);
                    if(surface.PaintError!=null)throw new InvalidOperationException(surface.PaintError);
                }
                Status="Quiet Cove is showing on "+surfaces.Count+" display"+(surfaces.Count==1?"":"s")+".\r\nA still image — no animated scene.";
            } catch(Exception error){
                lastError=WallpaperNative.FailureDetails(error);Clear();
                Status="Quiet Cove couldn't be displayed. Your usual wallpaper is still available.\r\n"+lastError;
                retryAfter=DateTime.UtcNow.AddSeconds(30);
            }
        }
        public string GetStatus(){
            string path=Path.Combine(Program.DataDirectory,"wallpaper-status.txt");
            StringBuilder report=new StringBuilder("Mochi "+AppVersion.Text+"\r\n"+DateTime.Now.ToString("O")+"\r\n"+Status+"\r\n");
            report.AppendLine("Wallpaper: Quiet Cove (static). Native artwork: 3840 x 2160. Full-screen 1:1 pixels on a 3840 x 2160 display; proportional reduction for smaller screens.");
            foreach(StaticWallpaperSurface surface in surfaces)report.AppendLine("Display: "+surface.ScreenBounds+"; paint requests: "+surface.PaintCount);
            if(desktop!=null)report.AppendLine("Desktop: "+desktop.Description);
            if(lastError!="")report.AppendLine("Last error: "+lastError);
            try{File.WriteAllText(path,report.ToString());return Status+"\r\n\r\nArtwork: 3840 × 2160 pixels. Fills a 4K UHD screen without stretching or zooming. Smaller screens fit the complete painting; other aspect ratios or larger screens may have a blue surround.\r\nDiagnostic report: "+path;}catch{return Status;}
        }
        void Clear(){foreach(StaticWallpaperSurface surface in surfaces)surface.Dispose();surfaces.Clear();desktop=null;displays="";}
        void ReleaseArt(){if(art!=null){art.Dispose();art=null;}}
        public void Dispose(){if(disposed)return;disposed=true;enabled=false;shellCheck.Stop();shellCheck.Dispose();Clear();ReleaseArt();}
    }

    sealed class StaticWallpaperSurface : NativeWindow,IDisposable {
        readonly Bitmap art;WallpaperLayerWindow layer;bool disposed;
        public readonly Rectangle ScreenBounds;
        public int PaintCount {get;private set;}
        public string PaintError {get;private set;}
        public IntPtr DesktopHandle {get{return layer==null?Handle:layer.Handle;}}
        public StaticWallpaperSurface(IntPtr parent,Rectangle bounds,Bitmap image,bool desktopCoordinates=false,IntPtr icons=default(IntPtr),bool layered=false){
            ScreenBounds=bounds;art=image;
            try{
                WallpaperNative.EnsureClass();Native.P origin=new Native.P{x=bounds.X,y=bounds.Y};
                if(desktopCoordinates && !WallpaperNative.ScreenToClient(parent,ref origin))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                IntPtr canvasParent=parent;
                if(layered){layer=new WallpaperLayerWindow(parent,origin,bounds.Size);canvasParent=layer.Handle;origin=new Native.P();}
                // GDI paints an opaque canvas. WS_EX_TRANSPARENT here can let
                // the parent erase it; input passes through via WM_NCHITTEST.
                IntPtr window=WallpaperNative.CreateWindowEx(0x08000080,WallpaperNative.CanvasClass,"Mochi Quiet Cove",0x40000000|0x04000000|0x02000000,
                    origin.x,origin.y,bounds.Width,bounds.Height,canvasParent,IntPtr.Zero,WallpaperNative.GetModuleHandle(null),IntPtr.Zero);
                if(window==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Could not create the static wallpaper window.");
                AssignHandle(window);
                if(!WallpaperNative.SetWindowPos(window,IntPtr.Zero,0,0,0,0,0x1|0x2|0x10|0x40))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                InvalidateRect(window,IntPtr.Zero,false);UpdateWindow(window);
                if(layer!=null)layer.ShowBelow(icons);
            }catch{Dispose();throw;}
        }
        public void EnsurePlacement(DesktopWallpaperHost host){
            if(WallpaperNative.GetParent(DesktopHandle)!=host.Parent)throw new InvalidOperationException("The desktop changed; Quiet Cove will reconnect.");
            if(host.Layered){
                if(!WallpaperNative.IsBelow(DesktopHandle,host.Icons) || !WallpaperNative.IsBelow(host.Wallpaper,DesktopHandle))layer.ShowBelow(host.Icons);
                if(!WallpaperNative.IsBelow(host.Wallpaper,DesktopHandle))throw new InvalidOperationException("The desktop background moved; Quiet Cove will reconnect.");
            }
        }
        protected override void WndProc(ref Message message){
            if(message.Msg==0x84){message.Result=new IntPtr(-1);return;}
            if(message.Msg==0x21){message.Result=new IntPtr(3);return;}
            if(message.Msg==0x14){message.Result=new IntPtr(1);return;}
            if(message.Msg==0x0f){
                PaintData data;IntPtr dc=BeginPaint(Handle,out data);
                try{
                    if(dc==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    using(Graphics graphics=Graphics.FromHdc(dc))QuietCoveArt.Paint(graphics,ScreenBounds.Size,art);
                    PaintCount++;
                }catch(Exception error){PaintError=error.Message;}
                finally{EndPaint(Handle,ref data);}
                message.Result=IntPtr.Zero;return;
            }
            base.WndProc(ref message);
        }
        public void Dispose(){
            if(disposed)return;disposed=true;IntPtr window=Handle;
            if(window!=IntPtr.Zero){ReleaseHandle();if(WallpaperNative.IsWindow(window))WallpaperNative.DestroyWindow(window);}
            if(layer!=null){layer.Dispose();layer=null;}
        }
        [StructLayout(LayoutKind.Sequential)] struct PaintData {
            public IntPtr DC;public int Erase;public WallpaperNative.Rect Paint;public int Restore,Update;
            [MarshalAs(UnmanagedType.ByValArray,SizeConst=32)]public byte[] Reserved;
        }
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr BeginPaint(IntPtr window,out PaintData data);
        [DllImport("user32.dll")] static extern bool EndPaint(IntPtr window,ref PaintData data);
        [DllImport("user32.dll")] static extern bool InvalidateRect(IntPtr window,IntPtr rect,bool erase);
        [DllImport("user32.dll")] static extern bool UpdateWindow(IntPtr window);
    }
}
