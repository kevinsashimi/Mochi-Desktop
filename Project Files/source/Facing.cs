using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace MochiDesktop {
    static class Spawn {
        public static Point PickLocation(Random random,Size window,Rectangle[] areas){
            // Weight each monitor by the available positions for the complete pet window.
            double total=0;foreach(Rectangle area in areas)if(area.Width>=window.Width && area.Height>=window.Height)total+=(double)(area.Width-window.Width+1)*(area.Height-window.Height+1);
            double pick=random.NextDouble()*total;
            foreach(Rectangle area in areas){
                int width=area.Width-window.Width+1,height=area.Height-window.Height+1;
                if(width<=0 || height<=0)continue;
                double weight=(double)width*height;
                if(pick<weight)return new Point(area.Left+random.Next(width),area.Top+random.Next(height));
                pick-=weight;
            }
            return Motion.Clamp(areas[0].Location,window,areas[0]);
        }
    }

    sealed class InteractionVariant {
        public FeedKind? Feed;public ReactionKind? Reaction;public bool Right;
        public string Key {get{return (Feed.HasValue?"feed:"+Feed.Value:"reaction:"+Reaction.Value)+":"+Right;}}
        public static InteractionVariant[] All(){
            List<InteractionVariant> variants=new List<InteractionVariant>();
            foreach(FeedKind kind in Enum.GetValues(typeof(FeedKind)))foreach(bool right in new[]{false,true})
                variants.Add(new InteractionVariant{Feed=kind,Right=right});
            foreach(ReactionKind kind in Enum.GetValues(typeof(ReactionKind)))foreach(bool right in new[]{false,true})
                variants.Add(new InteractionVariant{Reaction=kind,Right=right});
            return variants.ToArray();
        }
    }

    sealed partial class Companion {
        public static void VerifyFacing(){
            Point away=new Point(-10000,-10000);Random rng=new Random(442);
            Rectangle[] screens={new Rectangle(-1920,-200,1920,1040),new Rectangle(0,0,2560,1400),new Rectangle(2560,150,1080,1880)};
            HashSet<int> reached=new HashSet<int>();HashSet<Point> positions=new HashSet<Point>();
            foreach(int size in new[]{144,176,224})for(int i=0;i<1000;i++){
                Size window=Renderer.WindowSize(size);Point p=Spawn.PickLocation(rng,window,screens);bool inside=false;
                for(int n=0;n<screens.Length;n++)if(screens[n].Contains(new Rectangle(p,window))){inside=true;reached.Add(n);}
                if(!inside)throw new Exception("Random spawn outside a monitor working area");positions.Add(p);
            }
            if(reached.Count!=3 || positions.Count<2900)throw new Exception("Random spawn lacks monitor/position variety");
            using(Companion pet=new Companion(true)){
                pet.prefs.Roam=false;Rectangle area=Screen.PrimaryScreen.WorkingArea;
                Point middle=new Point(area.Left+(area.Width-pet.Width)/2,area.Top+(area.Height-pet.Height)/2);
                pet.Location=middle;
                foreach(bool right in new[]{false,true}){
                    pet.Face(right);pet.StartWelcome();double start=pet.Now;
                    Rectangle sprite=Renderer.SpriteRect(pet.prefs.Size);
                    Point nearby=new Point(pet.Left+sprite.Left+sprite.Width/2+(right?-130:130),pet.Top+sprite.Top+sprite.Height/2);
                    foreach(double elapsed in new[]{0.0,1.25,3.999,4.0,5.25,7.999}){
                        pet.Advance(start+elapsed,nearby);
                        if(pet.faceRight!=right || pet.row!=0 || pet.gaze.Active)throw new Exception("Greeting or hint left the selected idle pose");
                        using(Bitmap actual=pet.RenderFrame())using(Bitmap expected=Renderer.DrawAnimated(pet.atlas,0,pet.Now-pet.actionStart,pet.prefs.Size,pet.bubble,right))FacingTests.Same(actual,expected,"startup idle pose");
                    }
                    pet.Advance(start+8.001,away);
                    if(pet.faceRight!=right || pet.row!=0 || pet.bubble!="")throw new Exception("Greeting did not finish in the selected idle pose");
                }
                if(SurpriseVariants.Length!=2*(SurpriseFeeds.Length+SurpriseReactions.Length))throw new Exception("Every animation must have both facing variants");
                foreach(InteractionVariant variant in SurpriseVariants){
                    pet.Location=middle;
                    if(variant.Feed.HasValue)pet.StartFeeding(variant.Feed.Value,variant.Right);else pet.StartReaction(variant.Reaction.Value,variant.Right);
                    double end=pet.feeding!=null?pet.feeding.Started+pet.feeding.Duration:pet.reaction.Started+pet.reaction.Duration;
                    if(pet.faceRight!=variant.Right)throw new Exception("Reaction did not set facing: "+variant.Key);
                    pet.Advance(end,away);pet.Advance(end+1,away);
                    if(pet.feeding!=null || pet.reaction!=null || pet.faceRight!=variant.Right || pet.row!=0)throw new Exception("Facing lost after reaction: "+variant.Key);
                    using(Bitmap actual=pet.RenderFrame())using(Bitmap expected=Renderer.DrawAnimated(pet.atlas,0,pet.Now-pet.actionStart,pet.prefs.Size,pet.bubble,variant.Right))FacingTests.Same(actual,expected,"idle facing");
                }
                foreach(bool right in new[]{false,true}){
                    pet.Face(right);pet.Location=middle;pet.MoveDragged(new Point(middle.X+(right?-20:20),middle.Y));
                    if(pet.faceRight==right || pet.row!=(right?2:1))throw new Exception("Drag direction not applied");
                    pet.MoveDragged(middle); // Reverse while still on the same side of the original pointer position.
                    if(pet.faceRight!=right)throw new Exception("Drag reversal not applied");
                    pet.MoveDragged(new Point(middle.X,middle.Y+20));
                    if(pet.faceRight!=right)throw new Exception("Vertical drag changed side");
                    pet.down=true;pet.dragging=true;pet.OnUp(pet,new MouseEventArgs(MouseButtons.Left,1,0,0,0));
                    pet.Advance(pet.Now+2,away);
                    if(pet.faceRight!=right)throw new Exception("Drag release reset facing");
                    pet.Location=middle;Rectangle body=Renderer.SpriteRect(pet.prefs.Size);
                    Point center=new Point(pet.Left+body.Left+body.Width/2,pet.Top+body.Top+body.Height/2);
                    pet.Advance(pet.Now+1,new Point(center.X+(right?-130:130),center.Y));
                    if(!pet.gaze.Active)throw new Exception("Directional idle disabled cursor gaze");
                    pet.Advance(pet.Now+1,away);
                    if(pet.faceRight!=right || pet.gaze.Active)throw new Exception("Cursor gaze overwrote remembered side");
                }
                HashSet<bool> swims=new HashSet<bool>();
                for(int i=0;i<100;i++){
                    pet.Location=middle;pet.BeginSwim(true);
                    if(!pet.swimming)throw new Exception("Facing test swim failed to start");
                    Point destination=pet.swimTo,origin=pet.swimFrom;
                    bool expected=destination.X==origin.X?pet.faceRight:destination.X>origin.X;swims.Add(expected);
                    pet.Advance(pet.travelStart+pet.travelDuration,away);
                    if(pet.swimming || pet.faceRight!=expected)throw new Exception("Swim destination direction lost");
                }
                if(swims.Count!=2)throw new Exception("Swims never reached both sides");
                FacingTests.Run(pet.atlas);
            }
        }
    }

    static class FacingTests {
        public static void Same(Bitmap actual,Bitmap expected,string message){
            if(actual.Size!=expected.Size)throw new Exception(message+": dimensions");
            for(int y=0;y<actual.Height;y+=2)for(int x=0;x<actual.Width;x+=2)if(actual.GetPixel(x,y)!=expected.GetPixel(x,y))throw new Exception(message+": pixels");
        }
        static void Edges(Bitmap b,string message){
            for(int x=0;x<b.Width;x++)if(b.GetPixel(x,0).A!=0 || b.GetPixel(x,b.Height-1).A!=0)throw new Exception(message+": vertical clipping");
            for(int y=0;y<b.Height;y++)if(b.GetPixel(0,y).A!=0 || b.GetPixel(b.Width-1,y).A!=0)throw new Exception(message+": horizontal clipping");
        }
        static void Mirror(Bitmap left,Bitmap right,string message){
            for(int y=0;y<76;y+=2)for(int x=0;x<left.Width;x+=2)if(left.GetPixel(x,y)!=right.GetPixel(x,y))throw new Exception(message+": speech mirrored");
            // DrawImage's rectangle and transformed-center paths differ by one raster pixel.
            // Permit that registration difference, but compare the complete mirrored body.
            double best=Double.MaxValue;
            for(int offset=0;offset<=1;offset++){
                double error=0;int samples=0;
                for(int y=78;y<left.Height;y+=2)for(int x=1;x<left.Width;x+=2){
                    Color a=left.GetPixel(x,y),b=right.GetPixel(right.Width-1-x+offset,y);
                    error+=Math.Abs(a.A-b.A)+Math.Abs(a.R*a.A/255-b.R*b.A/255)+Math.Abs(a.G*a.A/255-b.G*b.A/255)+Math.Abs(a.B*a.A/255-b.B*b.A/255);samples++;
                }
                best=Math.Min(best,error/samples);
            }
            if(best>2)throw new Exception(message+": body did not mirror, error "+best);
        }
        public static void Run(Atlas atlas){
            foreach(int size in new[]{144,176,224}){
                foreach(int row in new[]{0,3,4,5,6,7,8})using(Bitmap left=Renderer.DrawAnimated(atlas,row,.9,size,"Hello Mochi!",false))using(Bitmap right=Renderer.DrawAnimated(atlas,row,.9,size,"Hello Mochi!",true)){Mirror(left,right,"idle/action");Edges(right,"right idle");}
                Rectangle area=new Rectangle(0,0,1920,1040);Size window=Renderer.WindowSize(size);
                foreach(int x in new[]{area.Left,area.Right-window.Width})foreach(bool right in new[]{false,true}){
                    FeedSequence chase=new FeedSequence(FeedKind.SnackChase,0,new Point(x,100),window,area,right);
                    if(chase.FaceRight!=right || !area.Contains(new Rectangle(chase.Destination,window)))throw new Exception("Explicit chase facing changed at edge");
                    if(right && chase.Destination.X<x || !right && chase.Destination.X>x)throw new Exception("Snack chase moves opposite selected facing");
                }
                foreach(FeedKind kind in Enum.GetValues(typeof(FeedKind))){
                    FeedSequence left=new FeedSequence(kind,0,new Point(700,400),window,area,false),right=new FeedSequence(kind,0,new Point(700,400),window,area,true);
                    for(double t=0;t<right.Duration;t+=.19)using(Bitmap l=FeedRenderer.Draw(atlas,left.Sample(t),size,left.Speech(t)))using(Bitmap r=FeedRenderer.Draw(atlas,right.Sample(t),size,right.Speech(t))){Edges(l,"left feed "+kind);Edges(r,"right feed "+kind);Mirror(l,r,"feed "+kind);}
                    if(left.FaceRight || !right.FaceRight)throw new Exception("Feeding must support both directions");
                }
                foreach(ReactionKind kind in Enum.GetValues(typeof(ReactionKind))){
                    ReactionSequence left=new ReactionSequence(kind,0,false),right=new ReactionSequence(kind,0,true);
                    for(double t=0;t<right.Duration;t+=.19)using(Bitmap l=ReactionRenderer.Draw(atlas,left,t,size))using(Bitmap r=ReactionRenderer.Draw(atlas,right,t,size)){Edges(l,"left reaction "+kind);Edges(r,"right reaction "+kind);Mirror(l,r,"reaction "+kind);}
                    if(left.FaceRight || !right.FaceRight)throw new Exception("Petting/play must support both directions");
                }
                using(Bitmap surface=new Bitmap(window.Width,window.Height))using(Graphics g=Graphics.FromImage(surface))using(Font font=new Font("Segoe UI",10.5f))using(StringFormat format=new StringFormat()){
                    string[] speech={"Toss it! I'll catch!","One tiny snack.\nOne perfect catch!","Boop received!","A little nose boop?","Best wave ever!","Catch a bubble\nand ride with me!"};
                    foreach(string message in speech){int fitted,lines;g.MeasureString(message,font,new SizeF(window.Width-44,48),format,out fitted,out lines);if(fitted<message.Length || lines>2)throw new Exception("New reaction speech clipped: "+message);}
                }
            }
        }
    }

    static class FacingPreview {
        public static void Write(string directory){
            Directory.CreateDirectory(directory);
            using(Atlas atlas=new Atlas()){
                foreach(InteractionVariant variant in InteractionVariant.All()){
                    string name=(variant.Feed.HasValue?variant.Feed.Value.ToString():variant.Reaction.Value.ToString())+(variant.Right?"-right":"-left");
                    string folder=Path.Combine(directory,name);Directory.CreateDirectory(folder);
                    FeedSequence feed=variant.Feed.HasValue?new FeedSequence(variant.Feed.Value,0,new Point(700,400),Renderer.WindowSize(176),new Rectangle(0,0,1920,1040),variant.Right):null;
                    ReactionSequence reaction=variant.Reaction.HasValue?new ReactionSequence(variant.Reaction.Value,0,variant.Right):null;
                    double duration=feed!=null?feed.Duration:reaction.Duration;
                    for(int i=0;i<=Math.Ceiling(duration*20);i++){
                        double t=i/20.0;
                        using(Bitmap frame=feed!=null?FeedRenderer.Draw(atlas,feed.Sample(t),176,feed.Speech(t)):ReactionRenderer.Draw(atlas,reaction,t,176))frame.Save(Path.Combine(folder,i.ToString("D3")+".png"),ImageFormat.Png);
                    }
                }
                foreach(bool right in new[]{false,true})using(Bitmap b=Renderer.DrawAnimated(atlas,0,.9,176,"",right))b.Save(Path.Combine(directory,"idle-"+right+".png"));
                foreach(bool right in new[]{false,true})using(Bitmap b=Renderer.DrawAnimated(atlas,0,.9,176,"Hi, I'm Mochi!\nYour whale shark pal.",right))b.Save(Path.Combine(directory,"intro-"+(right?"right":"left")+".png"));
            }
        }
    }
}
