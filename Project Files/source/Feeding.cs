using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace MochiDesktop {
    enum FeedKind { GulpAndWiggle, SnackChase, TummyPat, HappyRoll, SnackToss }

    sealed class FeedRotation {
        public int Next { get; private set; }
        static readonly int Count=Enum.GetValues(typeof(FeedKind)).Length;
        public FeedRotation(int next){Next=((next%Count)+Count)%Count;}
        public FeedKind Take(){FeedKind result=(FeedKind)Next;Next=(Next+1)%Count;return result;}
    }

    sealed class FeedPose {
        public int Supplement=0, Frame, SwimRow=2;
        public bool Flip, SnackVisible;
        public float SnackProgress, SnackAngle, SnackLift;
        public double Time;
        public FeedSequence Sequence;
    }

    sealed class FeedSequence {
        public readonly FeedKind Kind;
        public readonly Point Origin, Destination;
        public readonly bool FaceRight;
        public readonly double Started;
        public double Lead {get{return Kind==FeedKind.SnackChase?2.2:0;}}
        public double Reaction {get{return Kind==FeedKind.TummyPat?2.4:Kind==FeedKind.HappyRoll?2.4:1.45;}}
        public double Duration {get{return Lead+2.05+Reaction+0.25;}}
        public FeedSequence(FeedKind kind,double started,Point origin,Size window,Rectangle workArea,bool? faceRight=null){
            Kind=kind;Started=started;Origin=origin;Destination=origin;FaceRight=faceRight==true;
            if(kind==FeedKind.SnackChase){
                int leftRoom=origin.X-workArea.Left,rightRoom=workArea.Right-window.Width-origin.X;
                FaceRight=faceRight??(leftRoom<120 && rightRoom>leftRoom);
                int dx=(FaceRight?1:-1)*Math.Min(155,Math.Max(0,FaceRight?rightRoom:leftRoom));
                Destination=Motion.Clamp(new Point(origin.X+dx,origin.Y-12),window,workArea);
            }
        }
        public bool Finished(double now){return now>=Started+Duration;}
        public string Speech(double now){
            double t=now-Started;
            if(t<0 || Finished(now))return "";
            if(t<Lead)return "Come here, little snack!";
            if(t<Lead+1.2)return Kind==FeedKind.SnackToss?"Toss it! I'll catch!":"Ooh, a tiny treat!";
            if(t<Lead+2.05)return "Nom nom...";
            switch(Kind){
                case FeedKind.SnackChase:return "Caught it! So tasty.";
                case FeedKind.TummyPat:return "Happy little tummy.";
                case FeedKind.HappyRoll:return "A roly-poly thank you!";
                case FeedKind.SnackToss:return "One tiny snack.\nOne perfect catch!";
                default:return "That deserves a wiggle!";
            }
        }
        public Point Position(double now){
            if(Kind!=FeedKind.SnackChase)return Origin;
            return Motion.Travel(Origin,Destination,Math.Max(0,now-Started)/Lead);
        }
        public FeedPose Sample(double now){
            double t=Math.Max(0,now-Started);FeedPose p=new FeedPose{Time=t,Sequence=this};p.Flip=FaceRight;
            if(t<Lead){
                p.Supplement=-1;p.SwimRow=FaceRight?1:2;p.Frame=Atlas.FrameAt(p.SwimRow,t*1000);
                p.SnackVisible=true;p.SnackProgress=0.15f+(float)(Math.Sin(t*5)*0.045);p.SnackAngle=(float)(Math.Sin(t*6)*13);return p;
            }
            double eat=t-Lead;
            p.SnackVisible=eat<1.20;
            double u=Math.Max(0,Math.Min(1,eat/1.20)),leadProgress=Lead>0?.15+Math.Sin(Lead*5)*.045:0;
            p.SnackProgress=(float)(leadProgress+(1-leadProgress)*Motion.Ease(u));
            p.SnackAngle=(float)(Math.Sin(eat*9)*12*(1-u)+(Lead>0?Math.Sin(Lead*6)*13*(1-Motion.Ease(eat/.18)):0));
            if(Kind==FeedKind.SnackToss){p.SnackLift=(float)(Math.Sin(Math.PI*u)*.22);p.SnackAngle=(float)(360*Motion.Ease(u));}
            if(eat<0.95)p.Frame=0;
            else if(eat<1.25)p.Frame=1;
            else if(eat<1.61)p.Frame=2;
            else if(eat<2.05)p.Frame=3;
            else {
                double reaction=Math.Max(0,Math.Min(0.9999,(eat-2.05)/Reaction));
                if(Kind==FeedKind.TummyPat){p.Supplement=1;p.Frame=Math.Min(7,(int)(reaction*8));}
                else if(Kind==FeedKind.HappyRoll){p.Supplement=2;p.Frame=Math.Min(7,(int)(reaction*8));}
                else {p.Supplement=0;p.Frame=4+Math.Min(3,(int)(reaction*4));}
            }
            return p;
        }
    }

    static class FeedRenderer {
        static Bitmap Sprite(Atlas atlas,FeedPose pose){return pose.Supplement<0?atlas.Frames[2,pose.Frame]:atlas.Feeding[pose.Supplement,pose.Frame];}
        public static Bitmap Draw(Atlas atlas,FeedPose pose,int size,string bubble=""){
            Size ws=Renderer.WindowSize(size);Bitmap result=new Bitmap(ws.Width,ws.Height,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(result)){
                g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                GraphicsState facing=g.Save();
                if(pose.Flip){g.TranslateTransform(ws.Width,0);g.ScaleTransform(-1,1);}
                Rectangle body=Renderer.SpriteRect(size);
                Bitmap sprite=Sprite(atlas,pose);
                GraphicsState bodyState=g.Save();float bob=(float)Math.Sin(pose.Time*Math.PI*2/2.2)*1.5f;
                g.TranslateTransform(0,bob);
                g.DrawImage(sprite,body);
                g.Restore(bodyState);
                if(pose.SnackVisible){
                    // The treat approaches the existing mouth. Only the last few pixels shrink during the gulp.
                    float mouthX=body.Left+body.Width*0.265f,mouthY=body.Top+body.Height*0.590f+bob;
                    float side=-1,progress=pose.SnackProgress;
                    float x=mouthX+side*(1-progress)*body.Width*0.34f,y=mouthY-((1-progress)*0.18f+pose.SnackLift)*body.Height;
                    float snackSize=Math.Max(17,size*0.14f)*(1-Math.Max(0,progress-0.80f)*3.8f);
                    x=Math.Max(snackSize/2+3,Math.Min(ws.Width-snackSize/2-3,x));
                    GraphicsState state=g.Save();g.TranslateTransform(x,y);g.RotateTransform(pose.SnackAngle);
                    g.DrawImage(atlas.Snack,new RectangleF(-snackSize/2,-snackSize/2,snackSize,snackSize));g.Restore(state);
                }
                g.Restore(facing);Renderer.DrawBubble(g,ws,bubble);
            }
            return result;
        }
    }

    static class FeedingPreview {
        public static void Write(string directory){
            Directory.CreateDirectory(directory);
            using(Atlas atlas=new Atlas()){
                foreach(FeedKind kind in Enum.GetValues(typeof(FeedKind))){
                    string folder=Path.Combine(directory,kind.ToString());Directory.CreateDirectory(folder);
                    FeedSequence clip=new FeedSequence(kind,0,new Point(170,90),Renderer.WindowSize(176),new Rectangle(0,0,780,480));
                    for(int i=0;i<Math.Ceiling(clip.Duration*20);i++){
                        double t=i/20.0;using(Bitmap sprite=FeedRenderer.Draw(atlas,clip.Sample(t),176,clip.Speech(t)))
                        using(Bitmap canvas=new Bitmap(640,400,PixelFormat.Format32bppArgb))
                        using(Graphics g=Graphics.FromImage(canvas)){
                            g.Clear(Color.FromArgb(233,244,250));Point pos=clip.Position(t);g.DrawImageUnscaled(sprite,pos);
                            canvas.Save(Path.Combine(folder,i.ToString("D3")+".png"),ImageFormat.Png);
                        }
                    }
                }
            }
        }
    }

    static class FeedingTests {
        static void Check(bool ok,string message){if(!ok)throw new Exception("Feeding: "+message);}
        public static void Run(Atlas atlas,string directory){
            FeedRotation rotation=new FeedRotation(0);
            for(int i=0;i<20;i++)Check((int)rotation.Take()==i%5,"rotation order at"+i);
            Check(new FeedRotation(-1).Next==4 && new FeedRotation(123).Next==3,"invalid counter normalization");
            string path=Path.Combine(directory,"feeding-settings-test.xml");
            Preferences prefs=new Preferences();prefs.NextFeed=2;prefs.Size=144;prefs.Frequency=2;prefs.Roam=false;prefs.Save(path);
            Preferences restored=Preferences.Load(path);Check(restored.NextFeed==2 && restored.Size==144 && restored.Frequency==2 && !restored.Roam,"persist without resetting user preferences");
            Check(new FeedRotation(restored.NextFeed).Take()==FeedKind.TummyPat,"resume reaction after restart");
            System.Xml.Linq.XElement old=new System.Xml.Linq.XElement("Mochi",new System.Xml.Linq.XElement("Size",224));old.Save(path);
            Check(Preferences.Load(path).NextFeed==0 && Preferences.Load(path).Size==224,"backwards-compatible settings");
            Rectangle monitor=new Rectangle(-1920,-200,1920,1080);
            foreach(int size in new[]{144,176,224}){
                Size window=Renderer.WindowSize(size);
                foreach(FeedKind kind in Enum.GetValues(typeof(FeedKind))){
                    FeedSequence clip=new FeedSequence(kind,10,new Point(-900,160),window,monitor);
                    Check(!clip.Finished(10+clip.Duration-0.001) && clip.Finished(10+clip.Duration),"finish boundary");
                    Check(clip.Sample(10).SnackVisible,"snack starts visible");
                    Check(!clip.Sample(10+clip.Lead+1.25).SnackVisible,"snack swallowed before closed mouth");
                    Check(clip.Speech(9)=="" && clip.Speech(10+clip.Duration)=="","speech lifetime");
                    Check(clip.Speech(10+clip.Lead+1.4)=="Nom nom...","gulp speech timing");
                    for(double t=0;t<clip.Duration;t+=0.12){FeedPose pose=clip.Sample(10+t);Check(pose.Frame>=0 && pose.Frame<8 && pose.Supplement>=-1 && pose.Supplement<3,"frame range");}
                    double[] samples={0,clip.Lead+1.05,clip.Lead+1.40,clip.Lead+1.80,clip.Lead+2.1,clip.Lead+2.7,clip.Duration-0.05};
                    foreach(double t in samples)using(Bitmap frame=FeedRenderer.Draw(atlas,clip.Sample(10+t),size,clip.Speech(10+t))){
                        Check(frame.GetPixel(frame.Width/2,18).A>200,"feeding speech bubble missing");
                        for(int x=0;x<frame.Width;x++){Check(frame.GetPixel(x,0).A==0 && frame.GetPixel(x,frame.Height-1).A==0,"vertical clipping");}
                        for(int y=0;y<frame.Height;y++){Check(frame.GetPixel(0,y).A==0 && frame.GetPixel(frame.Width-1,y).A==0,"horizontal clipping");}
                    }
                }
                foreach(int x in new[]{monitor.Left,monitor.Right-window.Width}){
                    FeedSequence chase=new FeedSequence(FeedKind.SnackChase,0,new Point(x,monitor.Top),window,monitor);
                    Check(monitor.Contains(new Rectangle(chase.Destination,window)),"chase destination offscreen");
                    Check(chase.FaceRight==(x==monitor.Left),"chase turns away from screen edge");
                    for(double t=0;t<chase.Duration;t+=0.1)Check(monitor.Contains(new Rectangle(Motion.Clamp(chase.Position(t),window,monitor),window)),"chase offscreen");
                }
            }
            for(int r=0;r<3;r++)for(int c=0;c<8;c++){
                bool found=false;for(int y=0;y<208;y+=4)for(int x=0;x<192;x+=4)if(atlas.Feeding[r,c].GetPixel(x,y).A>200)found=true;
                Check(found,"empty supplemental pose");
            }
        }
    }
}
