using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.IO;

namespace MochiDesktop {
    sealed class EdgeWatch {
        double last=Double.NaN,exposure;
        public bool Due {get{return exposure>=25;}}
        public void Reset(double now){last=now;exposure=0;}
        public static bool Near(Point p,Size size,Rectangle area){
            double w=Math.Max(0,area.Width-size.Width),h=Math.Max(0,area.Height-size.Height);
            double mx=Math.Min(90,w*.18),my=Math.Min(90,h*.18);
            return (mx>0 && (p.X-area.Left<mx || area.Right-size.Width-p.X<mx)) ||
                   (my>0 && (p.Y-area.Top<my || area.Bottom-size.Height-p.Y<my));
        }
        public void Update(Point p,Size size,Rectangle area,double now){
            double dt=Double.IsNaN(last)?0:Math.Max(0,Math.Min(1,now-last));last=now;
            // Brief visits to open water decay the recent history instead of erasing it.
            exposure=Near(p,size,area)?Math.Min(30,exposure+dt):Math.Max(0,exposure-dt*2);
        }
    }

    enum SwimStyle { Straight, Curved, Mixed }

    sealed class SwimPath {
        sealed class Curve {
            public PointF A,B,C,D;public bool Straight;
            public Curve(PointF a,PointF b,PointF c,PointF d,bool straight=false){A=a;B=b;C=c;D=d;Straight=straight;}
            public static Curve Line(PointF a,PointF d){return new Curve(a,Lerp(a,d,1.0/3),Lerp(a,d,2.0/3),d,true);}
            public PointF At(double u){double v=1-u;return new PointF((float)(v*v*v*A.X+3*v*v*u*B.X+3*v*u*u*C.X+u*u*u*D.X),(float)(v*v*v*A.Y+3*v*v*u*B.Y+3*v*u*u*C.Y+u*u*u*D.Y));}
            public PointF Tangent(double u){double v=1-u;return new PointF((float)(3*v*v*(B.X-A.X)+6*v*u*(C.X-B.X)+3*u*u*(D.X-C.X)),(float)(3*v*v*(B.Y-A.Y)+6*v*u*(C.Y-B.Y)+3*u*u*(D.Y-C.Y)));}
            public bool Fits(RectangleF bounds){foreach(PointF p in new[]{A,B,C,D})if(p.X<bounds.Left-.001 || p.X>bounds.Right+.001 || p.Y<bounds.Top-.001 || p.Y>bounds.Bottom+.001)return false;return true;}
        }
        public readonly Point Start,End;
        public readonly SwimStyle Style;
        public readonly double Duration,Length;
        public readonly bool Escaping;
        readonly Curve[] curves;
        readonly double[] distances;
        const int SamplesPerCurve=96;
        public int Segments {get{return curves.Length;}}
        public bool StraightSegment(int index){return curves[index].Straight;}
        public PointF SegmentHeading(int index,double u){return curves[index].Tangent(u);}
        static PointF Lerp(PointF a,PointF b,double u){return new PointF((float)(a.X+(b.X-a.X)*u),(float)(a.Y+(b.Y-a.Y)*u));}
        static double Distance(PointF a,PointF b){double x=a.X-b.X,y=a.Y-b.Y;return Math.Sqrt(x*x+y*y);}
        public SwimPath(Point start,Size size,Rectangle area,Random random,bool escape,Point? destination=null){
            RectangleF room=new RectangleF(area.Left,area.Top,Math.Max(0,area.Width-size.Width),Math.Max(0,area.Height-size.Height));
            Start=Motion.Clamp(start,size,area);Escaping=escape;Style=(SwimStyle)random.Next(3);
            PointF target=Start;bool found=false;
            if(destination.HasValue){target=Motion.Clamp(destination.Value,size,area);found=true;}
            else if(escape){
                target=new PointF(room.Left+room.Width*(float)(.3+random.NextDouble()*.4),room.Top+room.Height*(float)(.3+random.NextDouble()*.4));
                double length=Distance(Start,target);
                if(length>600)target=Lerp(Start,target,600/length);
                found=true;
            }else for(int i=0;i<48;i++){
                double angle=random.NextDouble()*Math.PI*2,distance=140+random.NextDouble()*280;
                PointF candidate=new PointF(Start.X+(float)(Math.Cos(angle)*distance),Start.Y+(float)(Math.Sin(angle)*distance));
                // Reject unavailable headings instead of clamping them into edge-parallel trips.
                if(candidate.X>=room.Left && candidate.X<=room.Right && candidate.Y>=room.Top && candidate.Y<=room.Bottom){target=candidate;found=true;break;}
            }
            if(!found)target=new PointF(room.Left+(float)random.NextDouble()*room.Width,room.Top+(float)random.NextDouble()*room.Height);
            End=Motion.Clamp(Point.Round(target),size,area);
            double dx=End.X-Start.X,dy=End.Y-Start.Y,chord=Distance(Start,End);
            double bend=(random.Next(2)==0?-1:1)*Math.Min(110,chord*(.24+random.NextDouble()*.2));
            double joinAt=.42+random.NextDouble()*.16;bool straightFirst=random.Next(2)==0;
            Curve[] candidateCurves=null;
            for(int attempt=0;attempt<32;attempt++){
                double offset=attempt==31?0:bend*Math.Pow(.72,attempt);
                float bx=chord>0?(float)(-dy/chord*offset):0,by=chord>0?(float)(dx/chord*offset):0;
                if(Style==SwimStyle.Straight)candidateCurves=new[]{Curve.Line(Start,End)};
                else if(Style==SwimStyle.Curved){
                    PointF b=Lerp(Start,End,.26),c=Lerp(Start,End,.74);
                    candidateCurves=new[]{new Curve(Start,new PointF(b.X+bx,b.Y+by),new PointF(c.X+bx,c.Y+by),End)};
                }else{
                    PointF mid=Lerp(Start,End,joinAt);mid=new PointF(mid.X+bx,mid.Y+by);
                    if(straightFirst)candidateCurves=new[]{Curve.Line(Start,mid),new Curve(mid,Lerp(Start,mid,1.5),Lerp(mid,End,.70),End)};
                    else candidateCurves=new[]{new Curve(Start,Lerp(Start,mid,.30),Lerp(End,mid,1.5),mid),Curve.Line(mid,End)};
                }
                bool fits=true;foreach(Curve curve in candidateCurves)if(!curve.Fits(room))fits=false;
                if(fits)break;
            }
            curves=candidateCurves;
            // Distance-based sampling keeps the speed continuous when straight and curved parts join.
            distances=new double[curves.Length*SamplesPerCurve+1];PointF previous=Start;
            for(int i=1;i<distances.Length;i++){PointF point=AtParameter(i/(double)(distances.Length-1));distances[i]=distances[i-1]+Distance(previous,point);previous=point;}
            Length=distances[distances.Length-1];Duration=Math.Max(2.8,Length/(48+random.NextDouble()*14));
        }
        public static double Progress(double t){t=Math.Max(0,Math.Min(1,t));return t*t*t*(t*(t*6-15)+10);}
        double Parameter(double progress){
            if(Length<.00001)return 0;
            double wanted=Math.Max(0,Math.Min(1,progress))*Length;int lo=0,hi=distances.Length-1;
            while(hi-lo>1){int mid=(lo+hi)/2;if(distances[mid]<wanted)lo=mid;else hi=mid;}
            double span=distances[hi]-distances[lo],fraction=span>.000001?(wanted-distances[lo])/span:0;
            return (lo+fraction)/(distances.Length-1);
        }
        PointF AtParameter(double parameter){double p=Math.Max(0,Math.Min(1,parameter))*curves.Length;int segment=Math.Min(curves.Length-1,(int)p);return curves[segment].At(p-segment);}
        public PointF Position(double t){return AtParameter(Parameter(Progress(t)));}
        public PointF Heading(double t){double p=Parameter(Progress(t))*curves.Length;int segment=Math.Min(curves.Length-1,(int)p);return curves[segment].Tangent(p-segment);}
    }

    static class SmoothMotionTests {
        static void Check(bool ok,string message){if(!ok)throw new Exception("Smooth motion: "+message);}
        public static void Run(Atlas atlas){
            Random random=new Random(917);Size size=Renderer.WindowSize(176);
            Rectangle[] screens={new Rectangle(0,0,1920,1040),new Rectangle(-1920,-250,1920,1080),new Rectangle(0,0,900,1500),new Rectangle(-400,0,300,330)};
            HashSet<int> directions=new HashSet<int>();
            foreach(Rectangle area in screens)for(int i=0;i<500;i++){
                Point start=Motion.Clamp(new Point(random.Next(area.Left,area.Right),random.Next(area.Top,area.Bottom)),size,area);
                SwimPath path=new SwimPath(start,size,area,random,i%3==0);
                Check(Point.Round(path.Position(0))==start && Point.Round(path.Position(1))==path.End,"path endpoints");
                for(int n=0;n<=100;n++){
                    PointF p=path.Position(n/100.0);
                    Check(p.X>=area.Left-.01 && p.Y>=area.Top-.01 && p.X+size.Width<=area.Right+.01 && p.Y+size.Height<=area.Bottom+.01,"curved path leaves work area");
                    PointF heading=path.Heading(n/100.0);Check(!Single.IsNaN(heading.X) && !Single.IsNaN(heading.Y),"invalid heading");
                }
            }
            Rectangle large=new Rectangle(-1500,-800,3000,1800);Point middle=new Point(-size.Width/2,100-size.Height/2);
            HashSet<SwimStyle> styles=new HashSet<SwimStyle>();HashSet<bool> mixedOrders=new HashSet<bool>();
            for(int i=0;i<2000;i++){
                SwimPath p=new SwimPath(middle,size,large,random,false);directions.Add(Motion.Direction(p.End.X-middle.X,p.End.Y-middle.Y));styles.Add(p.Style);
                double dx=p.End.X-middle.X,dy=p.End.Y-middle.Y,chord=Math.Sqrt(dx*dx+dy*dy),deviation=0;
                for(int n=0;n<=100;n++){PointF sample=p.Position(n/100.0);deviation=Math.Max(deviation,Math.Abs(dx*(sample.Y-middle.Y)-dy*(sample.X-middle.X))/chord);}
                if(p.Style==SwimStyle.Straight)Check(deviation<.01,"straight route is not straight");
                else Check(deviation>10,"curved/mixed route has no visible bend");
                if(p.Style==SwimStyle.Mixed){
                    Check(p.Segments==2 && p.StraightSegment(0)!=p.StraightSegment(1),"mixed route lacks one straight and one curved part");mixedOrders.Add(p.StraightSegment(0));
                    PointF a=p.SegmentHeading(0,1),b=p.SegmentHeading(1,0);
                    double dot=(a.X*b.X+a.Y*b.Y)/Math.Sqrt((a.X*a.X+a.Y*a.Y)*(b.X*b.X+b.Y*b.Y));
                    Check(dot>.99999,"mixed route turns abruptly at join");
                }
                Check(p.Length>=chord-.05,"distance sampling shortened route");
            }
            Check(directions.Count==16,"not all swimming directions reachable");
            Check(styles.Count==3 && mixedOrders.Count==2,"missing swim style or mixed segment order");
            Check(SwimPath.Progress(.001)<.000000011 && 1-SwimPath.Progress(.999)<.000000011,"abrupt launch or landing");
            Rectangle screen=screens[0];
            foreach(Point start in new[]{new Point(0,0),new Point(1670,0),new Point(0,759),new Point(1670,759),new Point(0,400),new Point(1670,400),new Point(800,0),new Point(800,759)})for(int i=0;i<40;i++){
                Point origin=Motion.Clamp(start,size,screen);SwimPath p=new SwimPath(origin,size,screen,random,true);
                Check(!EdgeWatch.Near(p.End,size,screen),"escape destination stays near edge");
                if(origin.X==0)Check(p.End.X>origin.X,"escape not inward from left");
                if(origin.X==screen.Right-size.Width)Check(p.End.X<origin.X,"escape not inward from right");
                if(origin.Y==0)Check(p.End.Y>origin.Y,"escape not inward from top");
                if(origin.Y==screen.Bottom-size.Height)Check(p.End.Y<origin.Y,"escape not inward from bottom");
            }
            EdgeWatch watch=new EdgeWatch();watch.Reset(0);
            for(int i=1;i<25;i++)watch.Update(Point.Empty,size,screen,i);Check(!watch.Due,"border escape too early");
            watch.Update(Point.Empty,size,screen,25);Check(watch.Due,"border linger not detected");
            watch.Update(new Point(600,400),size,screen,26);Check(!watch.Due,"open water fails to decay edge history");
            watch.Reset(30);Check(!watch.Due,"manual reset failed");

            foreach(int sz in new[]{144,176,224})for(int d=0;d<16;d++)foreach(double time in new[]{.1,.45,.9,1.4}){
                GazePose pose=new GazePose{Direction=d,Time=time};
                using(Bitmap b=GazeRenderer.Draw(atlas,pose,sz,"",true)){
                    for(int x=0;x<b.Width;x++)Check(b.GetPixel(x,0).A==0 && b.GetPixel(x,b.Height-1).A==0,"swimming clips vertically");
                    for(int y=0;y<b.Height;y++)Check(b.GetPixel(0,y).A==0 && b.GetPixel(b.Width-1,y).A==0,"swimming clips horizontally");
                }
            }
            ArtifactTests.Run(atlas);
        }
    }

    static class ArtifactTests {
        static void Check(bool ok,string message){if(!ok)throw new Exception("Rendering artifacts: "+message);}
        static void SinglePose(Bitmap image,string label){
            int red=0,blue=0,mixed=0;
            // Inspect the body core, away from the intentionally separate snack, hearts, and sparkles.
            int cx=image.Width/2,cy=78+(image.Height-90)/2;
            for(int y=cy-16;y<=cy+16;y++)for(int x=cx-16;x<=cx+16;x++){
                Color c=image.GetPixel(x,y);if(c.A<180 || c.G>8)continue;
                if(c.R>240 && c.B<8)red++;else if(c.B>240 && c.R<8)blue++;else if(c.R>24 && c.B>24)mixed++;
            }
            Check(mixed==0 && ((red>50)^(blue>50)),"multiple poses overlaid in "+label+" (red="+red+", blue="+blue+", mixed="+mixed+")");
        }
        public static void Run(Atlas atlas){
            // Contrasting diagnostic art exposes both overlapping outlines and blended interiors.
            Bitmap[][,] sheets={atlas.Frames,atlas.Feeding,atlas.Illustrated};
            List<Bitmap> saved=new List<Bitmap>();
            using(Bitmap red=new Bitmap(192,208))using(Bitmap blue=new Bitmap(192,208)){
                using(Graphics g=Graphics.FromImage(red))g.FillRectangle(Brushes.Red,28,28,136,152);
                using(Graphics g=Graphics.FromImage(blue))g.FillRectangle(Brushes.Blue,28,28,136,152);
                try{
                    foreach(Bitmap[,] sheet in sheets)for(int r=0;r<sheet.GetLength(0);r++)for(int c=0;c<8;c++){
                        saved.Add(sheet[r,c]);sheet[r,c]=(r+c)%2==0?red:blue;
                    }
                    foreach(int size in new[]{144,176,224}){
                        GazeMotion turn=new GazeMotion();
                        for(int i=0;i<160;i++){
                            double t=i/60.0,angle=i*Math.PI/40;
                            turn.Update(Math.Sin(angle),-Math.Cos(angle),t);GazePose pose=turn.Sample(t);
                            foreach(bool swimming in new[]{false,true})using(Bitmap frame=GazeRenderer.Draw(atlas,pose,size,"",swimming)){
                                SinglePose(frame,swimming?"swimming turn":"cursor turn");
                                Rectangle body=Renderer.SpriteRect(size);int cx=frame.Width/2,cy=body.Top+body.Height/2;
                                for(int y=cy-22;y<=cy+22;y++)for(int x=cx-38;x<=cx+38;x++)
                                    Check(frame.GetPixel(x,y).A==255,"a seam cuts through the opaque swimming body");
                            }
                        }
                        for(int row=0;row<9;row++)for(double t=0;t<1.5;t+=.037)
                            using(Bitmap frame=Renderer.DrawAnimated(atlas,row,t,size,""))SinglePose(frame,"base animation "+row);
                        foreach(ReactionKind kind in Enum.GetValues(typeof(ReactionKind))){
                            ReactionSequence clip=new ReactionSequence(kind,0);
                            for(double t=0;t<clip.Duration;t+=.037)using(Bitmap frame=ReactionRenderer.Draw(atlas,clip,t,size))SinglePose(frame,kind.ToString());
                        }
                        foreach(FeedKind kind in Enum.GetValues(typeof(FeedKind)))foreach(int x in new[]{0,500}){
                            FeedSequence clip=new FeedSequence(kind,0,new Point(x,120),Renderer.WindowSize(size),new Rectangle(0,0,1100,800));
                            for(double t=0;t<clip.Duration;t+=.037)using(Bitmap frame=FeedRenderer.Draw(atlas,clip.Sample(t),size))SinglePose(frame,kind.ToString());
                        }
                    }
                }finally{
                    int i=0;foreach(Bitmap[,] sheet in sheets)for(int r=0;r<sheet.GetLength(0);r++)for(int c=0;c<8;c++)sheet[r,c]=saved[i++];
                }
            }
        }
    }

    static class SmoothPreview {
        public static void Write(string directory){
            Directory.CreateDirectory(directory);
            using(Atlas atlas=new Atlas())using(Font font=new Font("Segoe UI",10))using(Brush ink=new SolidBrush(Color.FromArgb(29,64,83))){
                string[] labels={"Up","Up-right","Right","Down-right","Down","Down-left","Left","Up-left"};
                for(int i=0;i<90;i++)using(Bitmap page=new Bitmap(1000,620))using(Graphics g=Graphics.FromImage(page)){
                    g.Clear(Color.FromArgb(233,244,250));
                    for(int d=0;d<8;d++){
                        int x=d%4*250,y=d/4*310;
                        g.DrawString(labels[d],font,ink,x+16,y+6);
                        GazePose pose=new GazePose{Direction=d*2,Time=i/30.0};
                        using(Bitmap pet=GazeRenderer.Draw(atlas,pose,176,"",true))g.DrawImageUnscaled(pet,x,y+20);
                    }
                    page.Save(Path.Combine(directory,i.ToString("D3")+".png"),ImageFormat.Png);
                }
                using(Bitmap page=new Bitmap(1000,650))using(Graphics g=Graphics.FromImage(page))using(Pen normal=new Pen(Color.FromArgb(70,110,177,206),2))using(Pen escape=new Pen(Color.FromArgb(231,144,82),3)){
                    g.Clear(Color.FromArgb(233,244,250));g.DrawString("Curved routes in every direction; orange routes leave screen edges",font,ink,15,10);
                    Rectangle area=new Rectangle(25,40,950,590);Size size=Renderer.WindowSize(144);Random random=new Random(82);
                    for(int i=0;i<40;i++){
                        bool leave=i>=32;Point start=leave?new Point(i%2==0?area.Left:area.Right-size.Width,i%3==0?area.Top:area.Bottom-size.Height):new Point(375,220);
                        SwimPath path=new SwimPath(start,size,area,random,leave);PointF[] points=new PointF[61];
                        for(int n=0;n<points.Length;n++){PointF p=path.Position(n/60.0);points[n]=new PointF(p.X+size.Width/2,p.Y+size.Height/2);}
                        g.DrawLines(leave?escape:normal,points);g.FillEllipse(ink,points[60].X-2,points[60].Y-2,4,4);
                    }
                    page.Save(Path.Combine(directory,"routes.png"),ImageFormat.Png);
                }
            }
        }
    }
}
