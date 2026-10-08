using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MochiDesktop {
    enum BoostStyle { RocketGrin, Wheee, WinkAndGiggle }

    sealed class BoostAnimation {
        public readonly BoostStyle Style;
        public readonly double Started;
        // The grin settles while the short .34-second impulse is already braking.
        // This is visual only: it never delays steering or changes the recharge.
        public const double Duration=.76;
        public BoostAnimation(BoostStyle style,double now){Style=style;Started=now;}
        public bool Finished(double now){return now>=Started+Duration;}
        public int Frame(double now){
            double elapsed=Math.Max(0,now-Started);
            return elapsed<.07?0:elapsed<.20?1:elapsed<.44?2:3;
        }
        public static BoostStyle Pick(Random random,int previous){
            // The next expression is a surprise, but cannot repeat the last one.
            int next=random.Next(previous<0?3:2);
            if(previous>=0 && next>=previous)next++;
            return (BoostStyle)next;
        }
    }

    static class BoostAnimationRenderer {
        public static Bitmap Draw(Atlas atlas,BoostAnimation clip,double now,int size,string bubble,bool right,double heading,PointF offset){
            Size window=Renderer.WindowSize(size);Bitmap result=new Bitmap(window.Width,window.Height,PixelFormat.Format32bppArgb);
            double elapsed=Math.Max(0,now-clip.Started),progress=Math.Min(1,elapsed/BoostAnimation.Duration);
            double energy=Math.Sin(Math.PI*progress),kick=Math.Sin(elapsed*Math.PI*2/.24)*energy;
            // Heading is clockwise from up. Mirror before pitching so neither side
            // becomes belly-up, or collapses to a line, on a diagonal/vertical dash.
            double radians=heading*Math.PI/180,dx=Math.Sin(radians),dy=-Math.Cos(radians);
            double pitch=Math.Atan2(dy,Math.Abs(dx))*180/Math.PI*(right?1:-1);
            Rectangle body=Renderer.SpriteRect(size);
            float radius=Math.Min((window.Height-105)/2f,(window.Width-8)/2f);
            float unit=Math.Min(size/192f,radius/(atlas.BoostRadius*1.045f));
            Bitmap sprite=atlas.Boost[(int)clip.Style,clip.Frame(now)];
            using(Graphics g=Graphics.FromImage(result)){
                g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                GraphicsState state=g.Save();
                // Reserve the same circular footprint at every pitch, so steering
                // never causes a zoom or clips the fins into the status/speech.
                g.TranslateTransform(body.Left+body.Width/2f+offset.X,(window.Height+55)/2f+offset.Y);
                g.RotateTransform((float)(pitch+kick*1.5*(right?-1:1)));g.ScaleTransform(right?-unit:unit,unit);
                float stretch=1+(float)energy*.025f;
                g.ScaleTransform(stretch,1/stretch);
                using(Matrix stroke=new Matrix(1,(float)kick*.018f,0,1,0,0))g.MultiplyTransform(stroke);
                g.DrawImage(sprite,new Rectangle(-96,-104,192,208));g.Restore(state);
                Renderer.DrawBubble(g,window,bubble);
            }
            return result;
        }
    }
}
