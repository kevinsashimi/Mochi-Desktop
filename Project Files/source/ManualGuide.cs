using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MochiDesktop {
    sealed class ManualGuideVisibility {
        public double Opacity {get;private set;}
        public const double Hold=3,Fade=1.5,Recall=.18;
        bool used;Point pointer;
        double visibleUntil=Double.PositiveInfinity,transitionAt,from=1,target=1,duration;
        public ManualGuideVisibility(double now,Point cursor){Opacity=1;pointer=cursor;transitionAt=now;}
        double Sample(double now){return from+(target-from)*Motion.Ease(duration<=0?1:(now-transitionAt)/duration);}
        public void Advance(double now,Point cursor,bool controlsUsed){
            if(!used && controlsUsed){used=true;visibleUntil=now+Hold;}
            if(cursor!=pointer){
                pointer=cursor;
                if(used)visibleUntil=now+Hold;
                if(target!=1){from=Sample(now);target=1;transitionAt=now;duration=Recall;}
            }
            if(used && now>=visibleUntil && target!=0){from=Sample(visibleUntil);target=0;transitionAt=visibleUntil;duration=Fade;}
            Opacity=Sample(now);
        }
    }

    static class ManualGuideRenderer {
        public static readonly string[] Controls={"WASD / Arrows: Swim","Spacebar: Boost (5s cooldown)","Esc or right-click to exit"};
        public static Rectangle Place(Size screen,Size card,Rectangle body,Rectangle current){
            int x=(screen.Width-card.Width)/2;
            Rectangle top=new Rectangle(x,Math.Min(28,Math.Max(0,screen.Height-card.Height)),card.Width,card.Height);
            Rectangle bottom=new Rectangle(x,Math.Max(0,screen.Height-card.Height-28),card.Width,card.Height);
            if(current.IsEmpty)current=top;
            body.Inflate(12,12);
            if(!current.IntersectsWith(body))return current;
            Rectangle other=current.Top==top.Top?bottom:top;
            return other.IntersectsWith(body)?current:other;
        }
        public static Bitmap Draw(Size screen,Atlas atlas,bool right){
            int width=Math.Min(490,Math.Max(280,screen.Width-24));
            Bitmap page=new Bitmap(width,173,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(page)){
                g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;
                using(GraphicsPath card=Renderer.Round(new RectangleF(1,1,width-2,page.Height-2),17))
                using(Brush paper=new SolidBrush(Color.FromArgb(250,243,251,255)))
                using(Pen border=new Pen(Color.FromArgb(255,128,186,211))){g.FillPath(paper,card);g.DrawPath(border,card);}
                bool picture=width>=420;float x=picture?111:20;
                if(picture){
                    GraphicsState state=g.Save();g.TranslateTransform(58,78);if(right)g.ScaleTransform(-1,1);
                    g.DrawImage(atlas.Frames[0,0],new Rectangle(-45,-49,90,98));g.Restore(state);
                }
                using(Font title=new Font("Segoe UI Semibold",19,FontStyle.Regular,GraphicsUnit.Pixel))
                using(Font text=new Font("Segoe UI",15,FontStyle.Regular,GraphicsUnit.Pixel))
                using(Font hint=new Font("Segoe UI",12,FontStyle.Regular,GraphicsUnit.Pixel))
                using(Brush ink=new SolidBrush(Color.FromArgb(29,64,83)))using(Brush quiet=new SolidBrush(Color.FromArgb(78,111,128))){
                    g.DrawString("You're the captain!",title,ink,x,16);
                    for(int line=0;line<Controls.Length;line++)g.DrawString(Controls[line],text,ink,x,49+line*27);
                    g.DrawString("Move your mouse to see controls again.",hint,quiet,x,143);
                }
            }
            return page;
        }
    }

    static class ManualBodyGeometry {
        [ThreadStatic] static byte[] pixels;
        public static Rectangle Measure(Bitmap body){
            BitmapData data=body.LockBits(new Rectangle(Point.Empty,body.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try {
                int stride=Math.Abs(data.Stride),length=stride*data.Height;
                if(pixels==null || pixels.Length<length)pixels=new byte[length];
                if(data.Stride>=0)Marshal.Copy(data.Scan0,pixels,0,length);
                else for(int y=0;y<data.Height;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),pixels,y*stride,stride);
                int left=body.Width,top=body.Height,right=0,bottom=0;
                for(int y=0;y<body.Height;y++)for(int x=0;x<body.Width;x++)if(pixels[y*stride+x*4+3]>20){
                    left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x+1);bottom=Math.Max(bottom,y+1);
                }
                if(right<=left || bottom<=top)throw new InvalidOperationException("Mochi's swimming pose is empty.");
                return Rectangle.FromLTRB(left,top,right,bottom);
            }finally{body.UnlockBits(data);}
        }
    }
}
