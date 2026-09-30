using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

namespace MochiDesktop {
    enum ReactionKind { CheekNuzzle, HappyWiggle, CozySway, HighFlipper, BarrelRoll, Backflip, HappyHops, FinDance, FlipperHug, NoseBoop, BubbleSurf }

    sealed class ReactionRotation {
        public int Next {get;private set;}
        readonly bool play;
        static readonly ReactionKind[] PetKinds={ReactionKind.CheekNuzzle,ReactionKind.HappyWiggle,ReactionKind.CozySway,ReactionKind.HighFlipper,ReactionKind.FlipperHug,ReactionKind.NoseBoop};
        static readonly ReactionKind[] PlayKinds={ReactionKind.BarrelRoll,ReactionKind.Backflip,ReactionKind.HappyHops,ReactionKind.FinDance,ReactionKind.BubbleSurf};
        public ReactionRotation(int next,bool forPlay){play=forPlay;int count=play?PlayKinds.Length:PetKinds.Length;Next=((next%count)+count)%count;}
        public ReactionKind Take(){ReactionKind[] kinds=play?PlayKinds:PetKinds;ReactionKind kind=kinds[Next];Next=(Next+1)%kinds.Length;return kind;}
        public static ReactionKind PickRandom(Random random,bool forPlay){ReactionKind[] kinds=forPlay?PlayKinds:PetKinds;return kinds[random.Next(kinds.Length)];}
    }

    sealed class ReactionPose {
        public bool Feeding;
        public int Row,Frame;
        public int IllustratedRow=-1;
        public float Angle,X,Y,Scale=1,StretchX=1,StretchY=1;
        public double Time,Progress;
    }

    sealed class ReactionSequence {
        public readonly ReactionKind Kind;
        public readonly double Started;
        public readonly bool FaceRight;
        public bool IsPlay {get{return (Kind>=ReactionKind.BarrelRoll && Kind<=ReactionKind.FinDance) || Kind==ReactionKind.BubbleSurf;}}
        public double Duration {get{return Kind==ReactionKind.CozySway?4.4:Kind==ReactionKind.FinDance?4.6:Kind==ReactionKind.BarrelRoll || Kind==ReactionKind.Backflip?3.2:4.0;}}
        static readonly int[][] IllustratedTiming={
            new[]{240,260,340,460,340,260,240,340},
            new[]{200,250,300,500,500,300,250,300},
            new[]{180,180,180,180,180,180,180,500},
            new[]{180,160,160,200,160,160,180,500}
        };
        public ReactionSequence(ReactionKind kind,double started,bool faceRight=false){Kind=kind;Started=started;FaceRight=faceRight;}
        public bool Finished(double now){return now>=Started+Duration;}
        static double Clamp(double v){return Math.Max(0,Math.Min(1,v));}
        static int IllustratedFrame(int row,double elapsed){double ms=Math.Max(0,elapsed-.35)*1000;for(int i=0;i<7;i++){if(ms<IllustratedTiming[row][i])return i;ms-=IllustratedTiming[row][i];}return 7;}
        public string Speech(double now){
            if(now<Started || Finished(now))return "";
            bool finish=now-Started>Duration-.75;
            switch(Kind){
                case ReactionKind.CheekNuzzle:return finish?"One more little pat?":"Ooh, that's the spot.";
                case ReactionKind.HappyWiggle:return finish?"You make my fins happy!":"Can't stop the happy wiggles!";
                case ReactionKind.CozySway:return finish?"My favorite place is here.":"Just floating here with you.";
                case ReactionKind.HighFlipper:return finish?"Best buddies!":"High flipper!";
                case ReactionKind.FlipperHug:return finish?"A little hug, just for you.":"Sending you a flipper hug!";
                case ReactionKind.BarrelRoll:return finish?"Round of a-paws? A-fins?":"Watch my roly-poly!";
                case ReactionKind.Backflip:return finish?"Ta-da! Stuck the splash.":"Tiny shark. Big flip!";
                case ReactionKind.HappyHops:return finish?"Two hops for my best friend!":"Boing... and boing!";
                case ReactionKind.NoseBoop:return finish?"Boop received!":"A little nose boop?";
                case ReactionKind.BubbleSurf:return finish?"Best wave ever!":"Catch a bubble\nand ride with me!";
                default:return finish?"Fin-tastic dance partner!":"You bring the beat. I bring fins.";
            }
        }
        public ReactionPose Sample(double now){
            double elapsed=Math.Max(0,now-Started),u=Clamp((elapsed-.35)/(Duration-.9));
            double envelope=Math.Pow(Math.Sin(Math.PI*u),2);
            ReactionPose p=new ReactionPose{Time=elapsed,Progress=Clamp(elapsed/Duration)};
            p.Frame=Atlas.FrameAt(0,elapsed*1000);
            switch(Kind){
                case ReactionKind.CheekNuzzle:
                    p.IllustratedRow=0;p.Frame=IllustratedFrame(0,elapsed);p.Y=(float)(-2*envelope);break;
                case ReactionKind.FlipperHug:
                    p.IllustratedRow=1;p.Frame=IllustratedFrame(1,elapsed);p.Y=(float)(-2*envelope);break;
                case ReactionKind.HappyWiggle:
                    p.Feeding=true;p.Frame=u>0 && u<1?4+((int)(elapsed/.15)%4):0;
                    p.Angle=(float)(Math.Sin(u*Math.PI*10)*5*envelope);p.X=(float)(Math.Sin(u*Math.PI*10)*4*envelope);
                    p.Y=(float)(-4*Math.Pow(Math.Sin(u*Math.PI*5),2)*envelope);break;
                case ReactionKind.CozySway:
                    p.Feeding=true;p.Frame=u>.1 && u<.88?3:0;
                    p.Angle=(float)(Math.Sin(u*Math.PI*4)*5*envelope);p.Y=(float)(-5*Math.Sin(u*Math.PI*3)*envelope);
                    p.StretchX=1+(float)(.018*envelope);p.StretchY=1-(float)(.025*envelope);break;
                case ReactionKind.HighFlipper:
                    p.Row=3;p.Frame=u<=0 || u>=1?0:new[]{0,1,2,1,2,3}[Math.Min(5,(int)(u*6))];
                    p.Angle=(float)(-7*envelope);p.Y=(float)(-7*envelope);break;
                case ReactionKind.BarrelRoll:
                    p.IllustratedRow=2;p.Frame=IllustratedFrame(2,elapsed);
                    p.X=(float)(Math.Sin(u*Math.PI*2)*3*envelope);p.Y=(float)(-3*envelope);break;
                case ReactionKind.Backflip:
                    p.IllustratedRow=3;p.Frame=IllustratedFrame(3,elapsed);p.Y=(float)(-10*envelope);break;
                case ReactionKind.HappyHops:
                    double hop=u>=1?0:(u*2)%1,lift=Math.Pow(Math.Sin(Math.PI*hop),2);
                    p.Row=4;p.Frame=u<=0 || u>=1?0:hop<.16?0:hop<.38?1:hop<.7?2:3;
                    p.Y=(float)(-18*lift);p.StretchY=1+(float)(.045*lift);p.StretchX=1-(float)(.025*lift);
                    p.Angle=(float)(Math.Sin(u*Math.PI*4)*4);break;
                case ReactionKind.FinDance:
                    p.Row=3;p.Frame=u<=0 || u>=1?0:new[]{1,2,1,3}[(int)(elapsed/.18)%4];
                    p.Angle=(float)(Math.Sin(u*Math.PI*8)*11*envelope);p.X=(float)(Math.Sin(u*Math.PI*8)*10*envelope);
                    p.Y=(float)(-6*Math.Pow(Math.Sin(u*Math.PI*8),2)*envelope);break;
                case ReactionKind.NoseBoop:
                    p.Feeding=true;p.Frame=u>.3 && u<.65?3:0;
                    p.X=(float)(-13*envelope);p.Y=(float)(-3*envelope);
                    p.Angle=(float)(4*Math.Sin(u*Math.PI*2)*envelope);
                    p.StretchX=1+(float)(.035*envelope);p.StretchY=1-(float)(.025*envelope);break;
                case ReactionKind.BubbleSurf:
                    p.Row=2;p.Frame=Atlas.FrameAt(2,elapsed*1000);
                    p.X=(float)(-10*Math.Sin(u*Math.PI*3)*envelope);
                    p.Y=(float)((-11-5*Math.Sin(u*Math.PI*6))*envelope);
                    p.Angle=(float)(8*Math.Sin(u*Math.PI*4)*envelope);break;
            }
            return p;
        }
    }

    static class ReactionRenderer {
        static Bitmap Sprite(Atlas atlas,ReactionPose pose){return pose.IllustratedRow>=0?atlas.Illustrated[pose.IllustratedRow,pose.Frame]:pose.Feeding?atlas.Feeding[pose.Row,pose.Frame]:atlas.Frames[pose.Row,pose.Frame];}
        public static Bitmap Draw(Atlas atlas,ReactionSequence clip,double now,int size){
            ReactionPose pose=clip.Sample(now);Size window=Renderer.WindowSize(size);
            Bitmap result=new Bitmap(window.Width,window.Height,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(result)){
                g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                if(clip.Kind==ReactionKind.BubbleSurf)DrawSpecialEffect(g,window,pose,clip.Kind,size);
                Bitmap sprite=Sprite(atlas,pose);
                float unit=size/192f,cx=window.Width/2f+pose.X*unit,cy=78+104*unit+pose.Y*unit;
                float scale=pose.Scale;Rectangle visible=atlas.OpaqueBounds(sprite);
                RectangleF bounds=TransformedBounds(visible,pose,unit,scale);
                // Fit the complete visible body inside the pet window, leaving the speech area clear.
                float fit=Math.Min(1,Math.Min((window.Width-16)/bounds.Width,(window.Height-86)/bounds.Height));
                scale*=fit;bounds=TransformedBounds(visible,pose,unit,scale);
                cx=Math.Max(8-bounds.Left,Math.Min(window.Width-8-bounds.Right,cx));
                cy=Math.Max(78-bounds.Top,Math.Min(window.Height-8-bounds.Bottom,cy));
                GraphicsState state=g.Save();g.TranslateTransform(cx,cy);g.RotateTransform(pose.Angle);
                g.ScaleTransform(unit*scale*pose.StretchX,unit*scale*pose.StretchY);
                g.DrawImage(sprite,new Rectangle(-96,-104,192,208));g.Restore(state);
                if(clip.Kind==ReactionKind.NoseBoop)DrawSpecialEffect(g,window,pose,clip.Kind,size);
                DrawCelebration(g,window,pose,clip.IsPlay);
            }
            // Mirror the completed body/effects pixels exactly, including stretched and rotated poses.
            // Text is added afterward so every future reaction inherits both readable directions.
            if(clip.FaceRight)result.RotateFlip(RotateFlipType.RotateNoneFlipX);
            using(Graphics g=Graphics.FromImage(result)){g.SmoothingMode=SmoothingMode.AntiAlias;Renderer.DrawBubble(g,window,clip.Speech(now));}
            return result;
        }
        static void DrawSpecialEffect(Graphics g,Size window,ReactionPose pose,ReactionKind kind,int size){
            double u=Math.Max(0,Math.Min(1,(pose.Time-.35)/3.1)),fade=Math.Pow(Math.Sin(Math.PI*u),2);
            if(fade<.001)return;
            Rectangle body=Renderer.SpriteRect(size);float unit=size/192f;
            if(kind==ReactionKind.NoseBoop){
                float x=body.Left+body.Width*.09f-13*(float)fade*unit,y=body.Top+body.Height*.44f;
                using(Pen ring=new Pen(Color.FromArgb((int)(210*fade),239,153,181),2)){
                    float radius=(float)(5+7*Math.Pow(Math.Sin(u*Math.PI*2),2))*unit;
                    g.DrawEllipse(ring,x-radius,y-radius,radius*2,radius*2);
                    for(int i=0;i<3;i++){double a=(i-1)*.7+Math.PI;g.DrawLine(ring,x+(float)Math.Cos(a)*(radius+4),y+(float)Math.Sin(a)*(radius+4),x+(float)Math.Cos(a)*(radius+8),y+(float)Math.Sin(a)*(radius+8));}
                }
            }else if(kind==ReactionKind.BubbleSurf){
                using(Pen ring=new Pen(Color.FromArgb((int)(185*fade),92,185,220),1.6f))
                using(Brush fill=new SolidBrush(Color.FromArgb((int)(36*fade),143,220,242))){
                    for(int i=0;i<5;i++){
                        float x=window.Width/2f+(i-2)*22*unit+(float)Math.Sin(pose.Time*2+i)*7*unit;
                        float y=body.Top+body.Height*.76f+(float)Math.Sin(pose.Time*3+i)*4*unit;
                        float radius=(12+(i%2)*4)*unit;
                        g.FillEllipse(fill,x-radius,y-radius,radius*2,radius*2);g.DrawEllipse(ring,x-radius,y-radius,radius*2,radius*2);
                        g.DrawArc(ring,x-radius*.65f,y-radius*.65f,radius*1.3f,radius*1.3f,205,55);
                    }
                }
            }
        }
        static RectangleF TransformedBounds(Rectangle r,ReactionPose pose,float unit,float scale){
            float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
            double radians=pose.Angle*Math.PI/180,c=Math.Cos(radians),s=Math.Sin(radians);
            foreach(int x in new[]{r.Left,r.Right})foreach(int y in new[]{r.Top,r.Bottom}){
                float px=(x-96)*pose.StretchX*unit*scale,py=(y-104)*pose.StretchY*unit*scale;
                float rx=(float)(px*c-py*s),ry=(float)(px*s+py*c);
                minX=Math.Min(minX,rx);maxX=Math.Max(maxX,rx);minY=Math.Min(minY,ry);maxY=Math.Max(maxY,ry);
            }
            return RectangleF.FromLTRB(minX,minY,maxX,maxY);
        }
        static void DrawCelebration(Graphics g,Size window,ReactionPose pose,bool play){
            for(int i=0;i<3;i++){
                double age=(pose.Time-(play?2.7:.65)-i*.28)/1.1;
                if(age<=0 || age>=1)continue;
                float x=i==0?29:i==1?window.Width-31:window.Width/2f+38;
                float y=125-(float)age*31+i*7,sz=play?5:6;
                int alpha=(int)(210*Math.Sin(age*Math.PI));
                using(SolidBrush ink=new SolidBrush(play?Color.FromArgb(alpha,76,169,202):Color.FromArgb(alpha,233,124,156))){
                    if(play){PointF[] points={new PointF(x,y-sz),new PointF(x+sz*.3f,y-sz*.3f),new PointF(x+sz,y),new PointF(x+sz*.3f,y+sz*.3f),new PointF(x,y+sz),new PointF(x-sz*.3f,y+sz*.3f),new PointF(x-sz,y),new PointF(x-sz*.3f,y-sz*.3f)};g.FillPolygon(ink,points);}
                    else using(GraphicsPath heart=new GraphicsPath()){
                        heart.AddBezier(x,y+sz,x-sz*1.8f,y-sz*.1f,x-sz*.6f,y-sz*1.5f,x,y-sz*.5f);
                        heart.AddBezier(x,y-sz*.5f,x+sz*.6f,y-sz*1.5f,x+sz*1.8f,y-sz*.1f,x,y+sz);g.FillPath(ink,heart);
                    }
                }
            }
        }
    }

    static class ReactionPreview {
        public static void Write(string directory){
            Directory.CreateDirectory(directory);
            using(Atlas atlas=new Atlas())foreach(ReactionKind kind in Enum.GetValues(typeof(ReactionKind))){
                ReactionSequence clip=new ReactionSequence(kind,0);string folder=Path.Combine(directory,kind.ToString());Directory.CreateDirectory(folder);
                for(int i=0;i<=Math.Ceiling(clip.Duration*20);i++)using(Bitmap pet=ReactionRenderer.Draw(atlas,clip,i/20.0,176))
                using(Bitmap page=new Bitmap(pet.Width,pet.Height))using(Graphics g=Graphics.FromImage(page)){
                    g.Clear(Color.FromArgb(233,244,250));g.DrawImageUnscaled(pet,0,0);page.Save(Path.Combine(folder,i.ToString("D3")+".png"),ImageFormat.Png);
                }
            }
        }
    }

    static class ReactionTests {
        static void Check(bool ok,string message){if(!ok)throw new Exception("Reactions: "+message);}
        public static void Run(Atlas atlas,string directory){
            foreach(bool play in new[]{false,true}){
                ReactionRotation cycle=new ReactionRotation(0,play);
                ReactionKind[] expected=play?new[]{ReactionKind.BarrelRoll,ReactionKind.Backflip,ReactionKind.HappyHops,ReactionKind.FinDance,ReactionKind.BubbleSurf}:new[]{ReactionKind.CheekNuzzle,ReactionKind.HappyWiggle,ReactionKind.CozySway,ReactionKind.HighFlipper,ReactionKind.FlipperHug,ReactionKind.NoseBoop};
                for(int i=0;i<20;i++)Check(cycle.Take()==expected[i%expected.Length],"independent rotation");
                int count=expected.Length;Check(new ReactionRotation(-1,play).Next==count-1 && new ReactionRotation(int.MaxValue,play).Next==int.MaxValue%count,"counter normalization");
            }
            string path=Path.Combine(directory,"reaction-settings-test.xml");
            new Preferences{NextPet=4,NextPlay=3,NextFeed=1,Size=224,Roam=false,Frequency=2}.Save(path);
            Preferences saved=Preferences.Load(path);
            Check(saved.NextPet==4 && saved.NextPlay==3 && saved.NextFeed==1 && saved.Size==224 && !saved.Roam && saved.Frequency==2,"persist independent cycles and preferences");
            File.WriteAllText(path,"<Mochi><Size>144</Size><NextFeed>2</NextFeed></Mochi>");saved=Preferences.Load(path);
            Check(saved.NextPet==0 && saved.NextPlay==0 && saved.NextFeed==2 && saved.Size==144,"old settings compatibility");
            foreach(ReactionKind kind in Enum.GetValues(typeof(ReactionKind))){
                ReactionSequence clip=new ReactionSequence(kind,10);
                Check(!clip.Finished(10+clip.Duration-.001) && clip.Finished(10+clip.Duration),"finish boundary");
                Check(clip.Speech(9)=="" && clip.Speech(10)!="" && clip.Speech(10+clip.Duration)=="","speech lifetime");
                ReactionPose end=clip.Sample(10+clip.Duration);
                Check(Math.Abs(end.Angle%360)<.001 && Math.Abs(end.X)<.001 && Math.Abs(end.Y)<.001 && Math.Abs(end.Scale-1)<.001,"settled endpoint");
                foreach(int size in new[]{144,176,224})using(Bitmap first=ReactionRenderer.Draw(atlas,clip,10,size)){
                    for(double t=.12;t<clip.Duration;t+=.12)using(Bitmap frame=ReactionRenderer.Draw(atlas,clip,10+t,size)){
                        Check(frame.GetPixel(frame.Width/2,18).A>200,"speech missing");
                        for(int x=0;x<frame.Width;x++)Check(frame.GetPixel(x,0).A==0 && frame.GetPixel(x,frame.Height-1).A==0,"vertical clipping: "+kind);
                        for(int y=0;y<frame.Height;y++)Check(frame.GetPixel(0,y).A==0 && frame.GetPixel(frame.Width-1,y).A==0,"horizontal clipping: "+kind);
                        if(t>.7 && t<1.5){int changed=0;for(int y=130;y<frame.Height;y+=3)for(int x=0;x<frame.Width;x+=3)if(first.GetPixel(x,y)!=frame.GetPixel(x,y))changed++;Check(changed>60,"inert reaction: "+kind);}
                    }
                }
                if(kind==ReactionKind.BarrelRoll || kind==ReactionKind.Backflip || kind==ReactionKind.CheekNuzzle || kind==ReactionKind.FlipperHug){
                    HashSet<int> visited=new HashSet<int>();int previous=0;
                    int expected=kind==ReactionKind.CheekNuzzle?0:kind==ReactionKind.FlipperHug?1:kind==ReactionKind.BarrelRoll?2:3;
                    for(double t=0;t<=clip.Duration;t+=.02){ReactionPose p=clip.Sample(10+t);Check(p.IllustratedRow==expected && p.Frame>=previous && p.Frame<8,"illustrated sequence order");Check(p.Angle==0 && p.Scale==1,"illustrated pose replaced by flat sprite spin");visited.Add(p.Frame);previous=p.Frame;}
                    Check(visited.Count==8 && previous==7,"missing illustrated pose");
                }
            }
            for(int row=0;row<4;row++)for(int col=0;col<8;col++){
                Bitmap b=atlas.Illustrated[row,col];int opaque=0;
                for(int y=0;y<b.Height;y+=4)for(int x=0;x<b.Width;x+=4)if(b.GetPixel(x,y).A>200)opaque++;
                Check(opaque>150,"empty illustrated cell");Rectangle bounds=atlas.OpaqueBounds(b);
                Check(bounds.Left>=4 && bounds.Top>=4 && bounds.Right<=188 && bounds.Bottom<=204,"illustrated cell edge");
            }
        }
    }
}
