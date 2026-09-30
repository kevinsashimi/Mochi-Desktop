using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace MochiDesktop {
    sealed class GazePose {
        public int Direction;
        public double Time,Turn;
    }

    sealed class GazeMotion {
        public bool Active {get;private set;}
        public int Direction {get;private set;}
        public double Angle {get;private set;}
        double lastUpdate,turnLean;
        public static double Difference(double target,double current){return ((target-current+540)%360+360)%360-180;}
        public void Reset(){Active=false;turnLean=0;}
        public void Update(double dx,double dy,double now){
            double target=(Math.Atan2(dx,-dy)*180/Math.PI+360)%360;
            if(!Active){Active=true;Angle=target;Direction=Motion.Direction(dx,dy);turnLean=0;}
            else {
                double dt=Math.Max(0,Math.Min(.1,now-lastUpdate));
                double step=Difference(target,Angle)*(1-Math.Exp(-dt/.10));
                Angle=(Angle+step+360)%360;
                double lean=dt>0?Math.Max(-4,Math.Min(4,step/dt*.015)):0;
                turnLean+=(lean-turnLean)*(1-Math.Exp(-dt/.12));
                // A small dead band prevents flickering between neighboring poses at sector boundaries.
                int next=((int)Math.Floor((Angle+11.25)/22.5))%16;
                if(next!=Direction && Math.Abs(Difference(Angle,Direction*22.5))>12.75){
                    Direction=next;
                }
            }
            lastUpdate=now;
        }
        public GazePose Sample(double now){
            // One illustrated pose at a time. Banking, bobbing and steering remain continuous.
            return new GazePose{Direction=Direction,Turn=turnLean,Time=now};
        }
    }

    static class GazeRenderer {
        public static Bitmap Draw(Atlas atlas,GazePose pose,int size,string bubble,bool swimming=false,PointF offset=default(PointF)){
            Size window=Renderer.WindowSize(size);Bitmap result=new Bitmap(window.Width,window.Height,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(result)){
                g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                Rectangle body=Renderer.SpriteRect(size);float unit=size/176f;
                double breath=Math.Sin(pose.Time*Math.PI*2/(swimming?1.3:2.8)),sway=Math.Sin(pose.Time*Math.PI*2/(swimming?1.9:3.6));
                GraphicsState state=g.Save();
                g.TranslateTransform(body.Left+body.Width/2f+(float)sway*.8f*unit+offset.X,body.Top+body.Height/2f+(float)breath*2.3f*unit+offset.Y);
                g.RotateTransform((float)sway*(swimming?2.3f:1.6f)+(float)pose.Turn);
                g.ScaleTransform(1+(float)breath*.009f,1-(float)breath*.012f);
                Rectangle destination=new Rectangle(-body.Width/2,-body.Height/2,body.Width,body.Height);
                Bitmap sprite=atlas.Frames[9+pose.Direction/8,pose.Direction%8];
                if(swimming){
                    float side=(float)Math.Sin(pose.Direction*Math.PI/8);
                    float shear=(float)Math.Sin(pose.Time*Math.PI*2/.95)*.018f*side;
                    // A single continuous transform sways the tail without tile boundaries or overlapping images.
                    using(Matrix stroke=new Matrix(1,shear,0,1,0,-body.Width*.25f*side*shear))g.MultiplyTransform(stroke);
                }
                g.DrawImage(sprite,destination);
                g.Restore(state);Renderer.DrawBubble(g,window,bubble);
            }
            return result;
        }
    }

    static class GazePreview {
        public static void Write(string directory){
            Directory.CreateDirectory(directory);
            using(Atlas atlas=new Atlas())using(Font font=new Font("Segoe UI",10))using(Brush ink=new SolidBrush(Color.FromArgb(29,64,83))){
                GazeMotion[] motions=new GazeMotion[16];
                for(int d=0;d<16;d++){motions[d]=new GazeMotion();double a=d*Math.PI/8;motions[d].Update(Math.Sin(a),-Math.Cos(a),0);}
                for(int i=0;i<72;i++)using(Bitmap page=new Bitmap(1000,1160))using(Graphics g=Graphics.FromImage(page)){
                    g.Clear(Color.FromArgb(233,244,250));double t=i/20.0;
                    for(int d=0;d<16;d++){
                        int x=d%4*250,y=d/4*290;
                        g.DrawString((d*22.5).ToString("0.#")+" degrees",font,ink,x+16,y+12);
                        using(Bitmap pet=GazeRenderer.Draw(atlas,motions[d].Sample(t),176,""))g.DrawImageUnscaled(pet,x,y);
                    }
                    page.Save(Path.Combine(directory,i.ToString("D3")+".png"),ImageFormat.Png);
                }
                GazeMotion turn=new GazeMotion();turn.Update(0,-1,0);
                string turns=Path.Combine(directory,"turning");Directory.CreateDirectory(turns);
                for(int i=0;i<160;i++){
                    double t=i/20.0,a=t*Math.PI/4;turn.Update(Math.Sin(a),-Math.Cos(a),t);
                    using(Bitmap pet=GazeRenderer.Draw(atlas,turn.Sample(t),176,""))using(Bitmap page=new Bitmap(320,340))using(Graphics g=Graphics.FromImage(page)){
                        g.Clear(Color.FromArgb(233,244,250));g.DrawImageUnscaled(pet,35,15);
                        g.FillEllipse(ink,155+(float)Math.Sin(a)*110,188-(float)Math.Cos(a)*110,8,8);
                        page.Save(Path.Combine(turns,i.ToString("D3")+".png"),ImageFormat.Png);
                    }
                }
            }
        }
    }

    static class GazeTests {
        static void Check(bool ok,string message){if(!ok)throw new Exception("Gaze: "+message);}
        public static void Run(Atlas atlas){
            foreach(int size in new[]{144,176,224})for(int d=0;d<16;d++){
                double angle=d*Math.PI/8;GazeMotion motion=new GazeMotion();motion.Update(Math.Sin(angle),-Math.Cos(angle),0);
                using(Bitmap first=GazeRenderer.Draw(atlas,motion.Sample(0),size,"")){
                    foreach(double t in new[]{.23,.70,1.4,2.1,2.8}){
                        motion.Update(Math.Sin(angle),-Math.Cos(angle),t);
                        using(Bitmap next=GazeRenderer.Draw(atlas,motion.Sample(t),size,"")){
                            int changed=0;
                            for(int y=78;y<next.Height;y+=2)for(int x=0;x<next.Width;x+=2)if(first.GetPixel(x,y)!=next.GetPixel(x,y))changed++;
                            Check(changed>80,"still pose in direction "+d+" at size "+size);
                            for(int x=0;x<next.Width;x++)Check(next.GetPixel(x,0).A==0 && next.GetPixel(x,next.Height-1).A==0,"vertical clipping");
                            for(int y=0;y<next.Height;y++)Check(next.GetPixel(0,y).A==0 && next.GetPixel(next.Width-1,y).A==0,"horizontal clipping");
                        }
                    }
                }
                Check(motion.Direction==d,"steady gaze drifted");
            }
            foreach(int sign in new[]{-1,1}){
                GazeMotion turn=new GazeMotion();double start=sign==1?350:10,target=sign==1?10:350;
                turn.Update(Math.Sin(start*Math.PI/180),-Math.Cos(start*Math.PI/180),0);
                for(int i=1;i<=30;i++){
                    double before=turn.Angle;turn.Update(Math.Sin(target*Math.PI/180),-Math.Cos(target*Math.PI/180),i/30.0);
                    double step=GazeMotion.Difference(turn.Angle,before);
                    Check(Math.Abs(step)<6 && step*sign>=-0.001,"wraparound took a long or abrupt turn");
                }
                Check(Math.Abs(GazeMotion.Difference(target,turn.Angle))<.1,"turn never settled");
                turn.Update(0,1,1.033);Check(turn.Direction>=0 && turn.Direction<16,"reverse turn frame range");
                for(int i=32;i<=60;i++)turn.Update(0,1,i/30.0);
                Check(turn.Direction==8,"reverse turn never settled");
                turn.Reset();Check(!turn.Active,"gaze failed to reset");
            }
        }
    }
}
