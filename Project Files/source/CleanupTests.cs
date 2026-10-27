#if MOCHI_TESTS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MochiDesktop {
    static class CleanupTests {
        internal static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        public static int Run(string path){
            List<string> log=new List<string>();
            try {
                string prefsPath=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)),"cleanup-settings.xml");
                File.WriteAllText(prefsPath,"<Mochi><Size>144</Size></Mochi>");
                Check(Preferences.Load(prefsPath).OceanCleanup,"Legacy settings should enable cleanup");
                Preferences prefs=new Preferences{OceanCleanup=false};prefs.Save(prefsPath);Check(!Preferences.Load(prefsPath).OceanCleanup,"Off preference lost");
                prefs.OceanCleanup=true;prefs.Save(prefsPath);Check(Preferences.Load(prefsPath).OceanCleanup,"On preference lost");
                log.Add("PASS: cleanup settings default, legacy migration, and on/off persistence.");
                Rectangle[] areas={new Rectangle(0,0,1920,1080),new Rectangle(-1080,-200,1080,1920),new Rectangle(0,0,640,480)};
                int count=0;
                foreach(Rectangle area in areas)foreach(int size in new[]{144,176,224})for(int kind=0;kind<4;kind++)for(int emotion=0;emotion<3;emotion++){
                    Random rng=new Random(count++);Point from=Motion.Clamp(new Point(area.Left+80,area.Top+50),Renderer.WindowSize(size),area);
                    CleanupTrip trip=new CleanupTrip(from,size,area,rng,(OceanLitter)kind,emotion,0);
                    HashSet<CleanupStage> stages=new HashSet<CleanupStage>();
                    for(double now=0;now<trip.Expires;now+=.08){
                        bool active=trip.Advance(now);stages.Add(trip.Stage);
                        Check(area.Contains(new Rectangle(trip.Position,Renderer.WindowSize(size))),"Pet escaped working area");
                        Check(area.Contains(new Rectangle(trip.Bin,new Size(CleanupTrip.BinSize,CleanupTrip.BinSize))),"Ocean Bin escaped working area");
                        if(trip.Stage<=CleanupStage.Approach)Check(area.Contains(new Rectangle(Point.Round(trip.Litter),new Size(trip.SpawnPixels,trip.SpawnPixels))),"Drifting litter escaped");
                        if(!active)break;
                    }
                    Check(stages.Count==9 && trip.Stage==CleanupStage.Finished,"Missing story stage or trip did not finish");
                }
                log.Add("PASS: 108 complete stories across all four litter types, three emotions, all sizes, small/portrait/negative-coordinate screens; bounded routes and finite lifetime.");
                VerifyGrips();log.Add("PASS: every litter type independently selects both mouth and flipper grips; all twelve litter designs remain fully visible and upright with both grips, both facings, all sizes and moving poses; open nets gather smoothly into matching bundles.");
                VerifyDelivery();log.Add("PASS: 3,888 deliveries keep Mochi clear of all six bins, including monitor edges, all sizes and grips; cargo reaches each opening and menu pauses preserve timing.");
                Rectangle screen=areas[0];VerifyOceanProps();
                log.Add("PASS: twelve distinct litter designs and six Ocean Bins; visible artwork with transparent edges; randomized safe bin positions across every screen quadrant and receiving openings inside each bin.");
                using(Atlas atlas=new Atlas())foreach(int size in new[]{144,176,224})for(int kind=0;kind<4;kind++)foreach(bool right in new[]{false,true}){
                    CleanupTrip trip=new CleanupTrip(new Point(650,300),size,screen,new Random(kind), (OceanLitter)kind,kind%3,0);
                    trip.PlaceBin(new Point(right?1700:20,80));
                    HashSet<CleanupStage> rendered=new HashSet<CleanupStage>();
                    for(double now=0;now<trip.Expires;now+=.1){
                        bool active=trip.Advance(now);
                        if(rendered.Add(trip.Stage))using(Bitmap b=CleanupArt.Pet(atlas,trip,now)){
                            Check(b.Size==Renderer.WindowSize(size),"Wrong canvas size");Check(b.GetPixel(0,0).A==0 && b.GetPixel(b.Width-1,b.Height-1).A==0,"Opaque frame corner");
                            using(Graphics g=Graphics.FromImage(b))using(Font f=new Font("Segoe UI",10.5f)){
                                string speech=trip.Speech(now);SizeF text=g.MeasureString(speech,f,b.Width-44);
                                Check(text.Height<=49,"Speech does not fit: "+speech);
                            }
                        }
                        if(trip.Stage==CleanupStage.Carry)Check(trip.Right==right,"Wrong carry facing");
                        if(!active)break;
                    }
                }
                log.Add("PASS: every story stage renders for all litter types, sizes and carry directions; transparent edges and readable speech.");
                VerifyReactionAnimation();
                log.Add("PASS: all three cleanup reactions animate in both directions and all sizes; sadness stays level between blinks with eyes no larger than the normal idle pose; anger/displeasure move continuously between fin gestures.");
                Companion.VerifyCleanup();log.Add("PASS: actual timer ordering, pause/off/manual priorities, cancellation, sleep gap, no burst scheduling, stable Ocean Bin destinations, design variety without immediate repeats, and zero Explorer access.");
                Companion.VerifyCleanupSurface();
                VerifySceneOverlap();log.Add("PASS: 432 overlapping scene frames preserve visible bin and carried litter pixels in one image across twelve litter designs, six bins, both grips and both facings.");
                VerifyCleanupSpeed();log.Add("PASS: 96 cleanup stories average 140 px/s with normal-swimming acceleration/deceleration on both approach and carry; gentle starts/stops, matching eased positions, exact arrivals and mid-route pauses across small, portrait, 4K and 8K screens, all route styles and long deliveries.");
                VerifyPickupContinuity();log.Add("PASS: all twelve litter designs retain visible pixels, position, size and tilt across the approach/pickup boundary; gathering stays visible on a fixed canvas throughout pickup.");
                VerifyLayeredPaintOwnership();log.Add("PASS: native WM_PAINT, WM_ERASEBKGND and WM_PRINTCLIENT requests cannot overwrite the companion scene; invalidation is validated without discarding the layered image.");
                log.Add("PASS: Mochi, bin and litter use one native window and one complete bitmap upload through approach, pickup, carry and disposal; hit testing excludes props and empty space.");
                log.Add("PASS: failed scene uploads retain all objects, coordinates and the hit mask together with bounded retries; placement recovers; cancellation restores normal pet bounds and releases the hit mask.");
                File.WriteAllLines(path,log);return 0;
            }catch(Exception e){log.Add("FAIL: "+e);File.WriteAllLines(path,log);return 1;}
        }
        static Bitmap PetWithLitter(Atlas atlas,CleanupTrip trip,double now){
            Bitmap pet=CleanupArt.Pet(atlas,trip,now);
            if(trip.Stage>=CleanupStage.Pickup && trip.Stage<=CleanupStage.Deposit)
                using(Bitmap prop=CleanupArt.Prop(atlas,trip,now))using(Graphics g=Graphics.FromImage(pet)){
                    Point at=trip.PropPosition(now);at.Offset(-trip.Position.X,-trip.Position.Y);g.DrawImageUnscaled(prop,at);
                }
            return pet;
        }
        static Color PixelAt(Bitmap bitmap,Point origin,Point screen){
            int x=screen.X-origin.X,y=screen.Y-origin.Y;
            return x<0 || y<0 || x>=bitmap.Width || y>=bitmap.Height?Color.Transparent:bitmap.GetPixel(x,y);
        }
        static Color Over(Color foreground,Color background){
            double a=foreground.A/255.0,b=background.A/255.0*(1-a),alpha=a+b;
            if(alpha==0)return Color.Transparent;
            return Color.FromArgb((int)Math.Round(alpha*255),(int)Math.Round((foreground.R*a+background.R*b)/alpha),
                (int)Math.Round((foreground.G*a+background.G*b)/alpha),(int)Math.Round((foreground.B*a+background.B*b)/alpha));
        }
        static bool SamePixel(Color a,Color b){return Math.Abs(a.A-b.A)<=2 && Math.Abs(a.R-b.R)<=3 && Math.Abs(a.G-b.G)<=3 && Math.Abs(a.B-b.B)<=3;}
        static void VerifySceneOverlap(){
            using(Atlas atlas=new Atlas())for(int kind=0;kind<4;kind++)for(int variant=0;variant<3;variant++)for(int grip=0;grip<2;grip++)for(int bin=0;bin<6;bin++){
                CleanupTrip trip=new CleanupTrip(new Point(650,300),176,new Rectangle(0,0,1920,1080),new Random(17),(OceanLitter)kind,0,0,variant,bin,null,(MischiefKind)grip);
                trip.PlaceBin(new Point(bin%2==0?1700:20,80));double now=0;
                while(trip.Stage!=CleanupStage.Carry && now<trip.Expires){now+=.05;trip.Advance(now);}
                for(int pose=0;pose<3;pose++){
                    now+=.12;trip.Advance(now);
                    // Deliberately cross a bin with the native pet rectangle,
                    // including its transparent corners, while holding litter.
                    trip.PlaceBin(new Point(trip.Position.X-55+pose*8,trip.Position.Y+90));
                    Rectangle bounds=CleanupArt.SceneBounds(trip,now);Point litterAt=trip.PropPosition(now);
                    using(Bitmap pet=CleanupArt.Pet(atlas,trip,now))using(Bitmap litter=CleanupArt.Prop(atlas,trip,now))
                    using(Bitmap basket=CleanupArt.Bin(atlas,trip.BinKind,CleanupTrip.BinSize,now-trip.BinShown))
                    using(Bitmap scene=CleanupArt.Scene(atlas,trip,now,pet,bounds)){
                        int binPixels=0,litterPixels=0;
                        for(int y=0;y<basket.Height;y+=2)for(int x=0;x<basket.Width;x+=2){
                            Point screen=new Point(trip.Bin.X+x,trip.Bin.Y+y);Color expected=basket.GetPixel(x,y);
                            if(expected.A>=200 && PixelAt(pet,trip.Position,screen).A==0 && PixelAt(litter,litterAt,screen).A==0){
                                Check(SamePixel(PixelAt(scene,bounds.Location,screen),expected),"Crossing Mochi erased a visible bin pixel");binPixels++;
                            }
                        }
                        for(int y=0;y<litter.Height;y+=2)for(int x=0;x<litter.Width;x+=2){
                            Color expected=litter.GetPixel(x,y);Point screen=new Point(litterAt.X+x,litterAt.Y+y);
                            if(expected.A>=100){
                                expected=Over(expected,Over(PixelAt(pet,trip.Position,screen),PixelAt(basket,trip.Bin,screen)));
                                Check(SamePixel(PixelAt(scene,bounds.Location,screen),expected),"Mochi erased a carried litter pixel");litterPixels++;
                            }
                        }
                        Check(binPixels>20 && litterPixels>20,"Overlap fixture lacks bin/litter pixels: "+kind+"/"+variant+"/"+grip+"/"+bin+" pixels="+binPixels+"/"+litterPixels);
                        Check(scene.GetPixel(0,0).A==0,"Scene introduced an opaque desktop background");
                    }
                }
            }
        }
        static void VerifyCleanupSpeed(){
            Rectangle[] areas={new Rectangle(0,0,640,480),new Rectangle(-1080,-200,1080,1920),new Rectangle(0,0,3840,2160),new Rectangle(-7680,-500,7680,4320)};
            int stories=0,softApproaches=0,softCarries=0;bool longTrip=false;HashSet<SwimStyle> styles=new HashSet<SwimStyle>();
            foreach(Rectangle area in areas)foreach(int size in new[]{144,176,224})for(int kind=0;kind<4;kind++)for(int grip=0;grip<2;grip++){
                Point start=Motion.Clamp(new Point(area.Left+30,area.Top+30),Renderer.WindowSize(size),area);
                CleanupTrip trip=new CleanupTrip(start,size,area,new Random(stories++),(OceanLitter)kind,0,0,0,0,null,(MischiefKind)grip);
                trip.PlaceBin(new Point(area.Right-CleanupTrip.BinSize-20,area.Bottom-CleanupTrip.BinSize-20));
                bool approach=false,carry=false,delivered=false;double now=0;
                while(now<trip.Expires && now<600){
                    now+=.1;trip.Advance(now);
                    if(trip.Stage==CleanupStage.Approach || trip.Stage==CleanupStage.Carry){
                        CleanupStage stage=trip.Stage;SwimPath route=trip.Route;
                        approach|=trip.Stage==CleanupStage.Approach;carry|=trip.Stage==CleanupStage.Carry;styles.Add(trip.Route.Style);
                        double mean=trip.Route.Length/trip.Duration;
                        // Measure long-route coverage by distance: increasing
                        // the requested pace should shorten the same journey.
                        longTrip|=trip.Route.Length>4000;
                        Check(route.Length<.1 || Math.Abs(mean-140)<.001,"Cleanup lost the 140 px/s average pace");
                        double started=trip.Started,duration=trip.Duration;Point[] positions=new Point[21];
                        for(int sample=0;sample<=20;sample++){
                            double fraction=sample/20.0;now=started+duration*fraction;trip.Advance(now);
                            positions[sample]=trip.Position;
                            Check(PlayfulPlan.Distance(trip.Position,Point.Round(route.Position(fraction)))<=1.5,"Cleanup motion differs from the normal swimming curve");
                            Check(area.Contains(new Rectangle(trip.Position,Renderer.WindowSize(size))),"Eased cleanup escaped working area");
                            if(sample==10){
                                Point before=trip.Position;trip.Pause(2.5);started+=2.5;now+=2.5;trip.Advance(now);
                                Check(trip.Position==before && trip.Stage==stage,"Mid-route menu pause changed cleanup position or stage");
                            }
                        }
                        Check(trip.Position==route.End && trip.Stage==(stage==CleanupStage.Approach?CleanupStage.Pickup:CleanupStage.Deposit),"Eased cleanup missed its destination or handoff");
                        if(route.Length>=80){
                            // Compare equal time windows on the actual rounded
                            // positions. A constant-speed route fails this check.
                            double launch=PlayfulPlan.Distance(positions[0],positions[2]);
                            double middle=PlayfulPlan.Distance(positions[9],positions[11]);
                            double landing=PlayfulPlan.Distance(positions[18],positions[20]);
                            Check(launch<middle*.25+2 && landing<middle*.25+2,"Cleanup lacks a gentle start or stop");
                            if(stage==CleanupStage.Approach)softApproaches++;else softCarries++;
                        }
                    }
                    delivered|=trip.Stage==CleanupStage.Celebrate;
                    if(trip.Stage==CleanupStage.Finished)break;
                }
                Check(approach && carry && delivered && trip.Stage==CleanupStage.Finished,"Eased delivery timed out");
            }
            Check(stories==96 && styles.Count==3 && longTrip && softApproaches>0 && softCarries>0,"Speed coverage missed long/curved routes or either eased travel leg");
        }
        static long AlphaMass(Bitmap frame){
            long alpha=0;for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++)alpha+=frame.GetPixel(x,y).A;return alpha;
        }
        [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll")] static extern bool InvalidateRect(IntPtr window,IntPtr rectangle,bool erase);
        [DllImport("user32.dll")] static extern bool UpdateWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool GetUpdateRect(IntPtr window,IntPtr rectangle,bool erase);
        static void VerifyLayeredPaintOwnership(){

            using(Companion prop=new Companion(true))
            using(Bitmap frame=new Bitmap(120,120)){
                using(Graphics g=Graphics.FromImage(frame))g.Clear(Color.Magenta);
                Native.SetBitmap(prop.Handle,frame,new Point(300,300));
                Native.KeepBackgroundGroup(prop.Handle);Check(Native.IsWindowVisible(prop.Handle),"Paint fixture did not become visible");
                // A native STATIC control paints its caption/background into this
                // DC on WM_PAINT/WM_PRINTCLIENT (reproduced in preview v8). Our
                // layered host must leave every pixel untouched on these requests.
                foreach(int message in new[]{0x14,0xF,0x318}){
                    using(Graphics g=Graphics.FromImage(frame)){
                        g.Clear(Color.Magenta);IntPtr dc=g.GetHdc();
                        try{SendMessage(prop.Handle,message,dc,IntPtr.Zero);}finally{g.ReleaseHdc(dc);}
                    }
                    for(int y=0;y<120;y++)for(int x=0;x<120;x++)Check(frame.GetPixel(x,y).ToArgb()==Color.Magenta.ToArgb(),"Default paint overwrote litter artwork for message "+message);
                }
                for(int attempt=0;attempt<10;attempt++){
                    InvalidateRect(prop.Handle,IntPtr.Zero,true);UpdateWindow(prop.Handle);
                    Check(!GetUpdateRect(prop.Handle,IntPtr.Zero,false) && Native.IsWindowVisible(prop.Handle),"Repaint invalidation discarded/replaced the layered image or kept its update region pending");
                }
            }
        }
        static void VerifyPickupContinuity(){
            using(Atlas atlas=new Atlas())for(int kind=0;kind<4;kind++)for(int variant=0;variant<3;variant++){
                CleanupTrip trip=new CleanupTrip(new Point(650,300),176,new Rectangle(0,0,1920,1080),new Random(17),(OceanLitter)kind,0,0,variant);
                double now=0;while(trip.Stage!=CleanupStage.Approach && now<trip.Expires){now+=.1;trip.Advance(now);}
                double end=trip.Started+trip.Duration;trip.Advance(end-.0001);
                Point at=trip.PropPosition(end-.0001);double angle=trip.LitterAngle(end-.0001);long alpha;
                using(Bitmap frame=CleanupArt.Prop(atlas,trip,end-.0001))alpha=AlphaMass(frame);
                trip.Advance(end);
                Check(trip.Stage==CleanupStage.Pickup && PlayfulPlan.Distance(at,trip.PropPosition(end))<1.5 && Math.Abs(angle-trip.LitterAngle(end))<.01,"Litter jumped or snapped tilt at pickup");
                using(Bitmap frame=CleanupArt.Prop(atlas,trip,end))Check(Math.Abs(AlphaMass(frame)-alpha)<alpha*.02,"Litter blinked at the pickup boundary");
                for(int sample=0;sample<=12;sample++){
                    now=end+sample*.095;trip.Advance(now);
                    using(Bitmap frame=CleanupArt.Prop(atlas,trip,now)){
                        int pixels=trip.LitterPixels(now);
                        Check(frame.Size==new Size(120,120) && AlphaMass(frame)>pixels*pixels*255*.08,"Pickup lost visible litter or resized its surface");
                    }
                }
            }
        }
        static CleanupTrip CarryTrip(int size,OceanLitter kind,int variant,MischiefKind grip,bool right,out double now){
            CleanupTrip trip=new CleanupTrip(new Point(650,300),size,new Rectangle(0,0,1920,1080),new Random(17),kind,2,0,variant,0,null,grip);
            trip.PlaceBin(new Point(right?1700:20,80));now=0;
            while(trip.Stage!=CleanupStage.Carry && now<trip.Expires){now+=.05;trip.Advance(now);}
            Check(trip.Stage==CleanupStage.Carry,"Carry fixture did not start");now+=.25;trip.Advance(now);return trip;
        }
        static void VerifyGrips(){
            foreach(OceanLitter kind in Enum.GetValues(typeof(OceanLitter))){
                int mouth=0;
                for(int seed=0;seed<200;seed++){
                    CleanupTrip trip=new CleanupTrip(new Point(650,300),176,new Rectangle(0,0,1920,1080),new Random(seed),kind,0,0);
                    if(trip.Grip==MischiefKind.GentleBite)mouth++;
                }
                Check(mouth>60 && mouth<140,"Grip is fixed or strongly biased for "+kind);
            }
            using(Atlas atlas=new Atlas()){
                foreach(int size in new[]{144,176,224})foreach(bool right in new[]{false,true})
                foreach(MischiefKind grip in Enum.GetValues(typeof(MischiefKind)))foreach(OceanLitter kind in Enum.GetValues(typeof(OceanLitter)))for(int variant=0;variant<3;variant++){
                    double now;CleanupTrip trip=CarryTrip(size,kind,variant,grip,right,out now);
                    using(Bitmap litter=CleanupArt.Litter(atlas,kind,variant,CleanupTrip.CargoSize))for(int pose=0;pose<4;pose++){
                        if(pose>0){now+=.3;trip.Advance(now);}
                        Check(trip.Stage==CleanupStage.Carry,"Carry fixture ended before pose checks");
                        using(Bitmap frame=PetWithLitter(atlas,trip,now)){
                            Point at=trip.HoldOffset;at.Y=(int)Math.Round(at.Y+Math.Sin((now-trip.Started)*4));
                            Check(new Rectangle(Point.Empty,frame.Size).Contains(new Rectangle(at,litter.Size)),"Cargo clipped by the pet canvas");
                            int compared=0;
                            // Every solid part of the original prop must survive the
                            // animated body, including bottle caps and fin-overlap areas.
                            // Artwork remains upright when Mochi changes direction.
                            for(int y=0;y<litter.Height;y++)for(int x=0;x<litter.Width;x++){
                                Color expected=litter.GetPixel(x,y);if(expected.A<250)continue;
                                Color actual=frame.GetPixel(at.X+x,at.Y+y);
                                Check(Math.Abs(actual.R-expected.R)+Math.Abs(actual.G-expected.G)+Math.Abs(actual.B-expected.B)<=18,
                                    "Litter was masked or distorted: "+kind+" / "+variant+" / "+grip+" / "+right+" / "+size+" / "+pose);
                                compared++;
                            }
                            Check(compared>50,"Cargo visibility check had too little opaque artwork");
                        }
                    }
                }
                for(int variant=0;variant<3;variant++){
                    using(Bitmap open=CleanupArt.Litter(atlas,OceanLitter.Net,variant,120,0,-1,0))
                    using(Bitmap midway=CleanupArt.Litter(atlas,OceanLitter.Net,variant,120,0,-1,.5))
                    using(Bitmap folded=CleanupArt.Litter(atlas,OceanLitter.Net,variant,120,0,-1,1)){
                        Check(!SamePixels(open,folded) && !SamePixels(open,midway) && !SamePixels(midway,folded),"Net pickup did not gather its mesh");CheckProp(open);
                    }
                }
            }
        }
        static void VerifyDelivery(){
            foreach(Rectangle area in new[]{new Rectangle(0,0,1920,1080),new Rectangle(-1080,-200,1080,1920),new Rectangle(0,0,640,480)})
            foreach(int size in new[]{144,176,224})for(int bin=0;bin<6;bin++)for(int kind=0;kind<4;kind++)for(int grip=0;grip<2;grip++)for(int spot=0;spot<9;spot++){
                Point start=Motion.Clamp(new Point(area.Left+50,area.Top+50),Renderer.WindowSize(size),area);
                CleanupTrip trip=new CleanupTrip(start,size,area,new Random(spot), (OceanLitter)kind,0,0,0,bin,null,(MischiefKind)grip);
                trip.PlaceBin(new Point(area.Left+(area.Width-CleanupTrip.BinSize)*(spot%3)/2,area.Top+(area.Height-CleanupTrip.BinSize)*(spot/3)/2));
                bool delivered=false,paused=false;
                for(double now=0;now<trip.Expires;now+=.1){
                    trip.Advance(now);
                    if(trip.Stage==CleanupStage.Carry && !paused){
                        Point before=trip.Position;double progress=trip.Progress(now);trip.Pause(100);now+=100;
                        Check(trip.Position==before && Math.Abs(trip.Progress(now)-progress)<.00001,"Menu pause jumped the delivery route");paused=true;
                    }
                    if(trip.Stage==CleanupStage.Deposit || trip.Stage==CleanupStage.Celebrate){
                        Rectangle body=Renderer.SpriteRect(size);body.Offset(trip.Position);body.Inflate(4,4);
                        Check(!body.IntersectsWith(new Rectangle(trip.Bin,new Size(CleanupTrip.BinSize,CleanupTrip.BinSize))),"Mochi overlaps bin at handoff: "+area+" / "+size+" / "+spot);
                    }
                    if(trip.Stage==CleanupStage.Celebrate){
                        PointF mouth=trip.BinMouth;
                        Check(Math.Abs(trip.Litter.X+2.5-mouth.X)<1 && Math.Abs(trip.Litter.Y+2.5-mouth.Y)<1,"Cargo missed bin opening");delivered=true;break;
                    }
                }
                Check(delivered && paused,"Delivery did not finish after menu pause");
            }
        }
        static void VerifyOceanProps() {
            Rectangle[] areas={new Rectangle(0,0,1920,1080),new Rectangle(-1080,-300,1080,1920),new Rectangle(0,0,640,480)};
            foreach(Rectangle area in areas){
                int[] quadrants=new int[4];
                for(int n=0;n<300;n++){
                    Point from=Motion.Clamp(new Point(n%2==0?area.Left+30:area.Right-30,n%4<2?area.Top+30:area.Bottom-30),Renderer.WindowSize(176),area);
                    CleanupTrip trip=new CleanupTrip(from,176,area,new Random(n),OceanLitter.Bottle,0,0);
                    Rectangle bin=new Rectangle(trip.Bin,new Size(CleanupTrip.BinSize,CleanupTrip.BinSize));
                    Check(area.Contains(bin) && bin.Contains(Point.Round(trip.BinMouth)),"Bin or receiving opening outside screen");
                    int q=(trip.Bin.X+CleanupTrip.BinSize/2>=area.Left+area.Width/2?1:0)+(trip.Bin.Y+CleanupTrip.BinSize/2>=area.Top+area.Height/2?2:0);quadrants[q]++;
                    if(area.Width>=1000)Check(!bin.IntersectsWith(new Rectangle(from,Renderer.WindowSize(176))),"Bin covered Mochi at spawn");
                }
                foreach(int count in quadrants)Check(count>5,"Bin placement did not reach all screen quadrants: "+area);
            }
            using(Atlas atlas=new Atlas()){
                for(int kind=0;kind<4;kind++)for(int variant=0;variant<3;variant++){
                    if(variant>0)Check(!SamePixels(atlas.Litter[kind,variant-1],atlas.Litter[kind,variant]),"Duplicate litter artwork");
                    foreach(int size in new[]{CleanupTrip.CargoSize,CleanupTrip.LitterSize})using(Bitmap b=CleanupArt.Litter(atlas,(OceanLitter)kind,variant,size,8))CheckProp(b);
                }
                for(int bin=0;bin<6;bin++){
                    if(bin>0)Check(!SamePixels(atlas.OceanBins[bin-1],atlas.OceanBins[bin]),"Duplicate Ocean Bin artwork");
                    using(Bitmap b=CleanupArt.Bin(atlas,(OceanBin)bin,CleanupTrip.BinSize,.5))CheckProp(b);
                }
            }
        }
        static void CheckProp(Bitmap frame) {
            int opaque=0;
            for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++){
                int alpha=frame.GetPixel(x,y).A;if(alpha>200)opaque++;
                if(x==0 || y==0 || x==frame.Width-1 || y==frame.Height-1)Check(alpha==0,"Prop clipped at canvas edge");
            }
            Check(opaque>frame.Width*frame.Height/6,"Prop too small or faint to see");
        }
        static bool SamePixels(Bitmap a,Bitmap b) {
            for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y))return false;
            return true;
        }
        static Rectangle EyeBounds(Bitmap pose) {
            // The nearer eye, excluding the mouth and blue forehead. Compare the
            // original idle artwork with the new open-eye sadness drawing.
            int left=192,top=208,right=0,bottom=0;
            for(int y=95;y<135;y++)for(int x=78;x<112;x++){
                Color pixel=pose.GetPixel(x,y);
                if(pixel.A>200 && pixel.R<55 && pixel.G<65 && pixel.B<70){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x+1);bottom=Math.Max(bottom,y+1);}
            }
            Check(right>left && bottom>top,"Eye landmarks not found");return Rectangle.FromLTRB(left,top,right,bottom);
        }
        static void VerifyReactionAnimation() {
            using(Atlas atlas=new Atlas()){
                Rectangle normal=EyeBounds(atlas.Frames[0,0]),sad=EyeBounds(atlas.Cleanup[2,0]);
                Check(sad.Width<=normal.Width+1 && sad.Height<=normal.Height+1,"Sadness enlarged Mochi's normal eyes");
                foreach(int size in new[]{144,176,224})foreach(bool right in new[]{false,true}){
                    using(Bitmap a=CleanupReaction.Draw(atlas,2,.4,size,right,null))
                    using(Bitmap b=CleanupReaction.Draw(atlas,2,2.5,size,right,null))
                    using(Bitmap blink=CleanupReaction.Draw(atlas,2,1.75,size,right,null)){
                        Check(SamePixels(a,b),"Sadness moved, bowed, or scaled the resting head");
                        Check(!SamePixels(a,blink),"Sadness blink is missing");
                    }
                    for(int emotion=0;emotion<2;emotion++){
                        using(Bitmap a=CleanupReaction.Draw(atlas,emotion,.10,size,right,null))
                        using(Bitmap b=CleanupReaction.Draw(atlas,emotion,.30,size,right,null))
                        using(Bitmap gesture=CleanupReaction.Draw(atlas,emotion,.85,size,right,null)){
                            Check(!SamePixels(a,b),"Reaction held static between gestures");
                            Check(!SamePixels(b,gesture),"Reaction fin gesture is missing");
                        }
                    }
                    for(int emotion=0;emotion<3;emotion++)for(double t=0;t<=4;t+=.05)using(Bitmap frame=CleanupReaction.Draw(atlas,emotion,t,size,right,null)){
                        for(int x=0;x<frame.Width;x++)Check(frame.GetPixel(x,0).A==0 && frame.GetPixel(x,frame.Height-1).A==0,"Reaction clipped vertically");
                        for(int y=0;y<frame.Height;y++)Check(frame.GetPixel(0,y).A==0 && frame.GetPixel(frame.Width-1,y).A==0,"Reaction clipped horizontally");
                    }
                }
            }
        }
        static void ReactionPreview(string path) {
            string frames=Path.Combine(path,"reaction-frames");Directory.CreateDirectory(frames);
            using(Atlas atlas=new Atlas())using(Font font=new Font("Segoe UI",12,FontStyle.Bold)){
                for(int n=0;n<80;n++)using(Bitmap sheet=new Bitmap(840,660))using(Graphics g=Graphics.FromImage(sheet)){
                    g.Clear(Color.FromArgb(227,244,245));
                    for(int row=0;row<2;row++)for(int emotion=0;emotion<3;emotion++){
                        int x=emotion*280,y=row*330;
                        g.DrawString(new[]{"Anger","Displeasure","Sadness"}[emotion]+(row==0?" / left":" / right"),font,Brushes.DarkSlateGray,x+18,y+12);
                        using(Bitmap frame=CleanupReaction.Draw(atlas,emotion,n/20.0,176,row==1,null))g.DrawImageUnscaled(frame,x+15,y+30);
                    }
                    sheet.Save(Path.Combine(frames,n.ToString("D3")+".png"),ImageFormat.Png);
                    if(n==8)sheet.Save(Path.Combine(path,"cleanup-reactions.png"),ImageFormat.Png);
                }
            }
        }
        static void GripPreview(string path){
            using(Atlas atlas=new Atlas())using(Bitmap sheet=new Bitmap(1120,1280))using(Graphics g=Graphics.FromImage(sheet))using(Font font=new Font("Segoe UI",12)){
                g.Clear(Color.FromArgb(227,244,245));
                for(int row=0;row<4;row++)for(int kind=0;kind<4;kind++){
                    double now;MischiefKind grip=row%2==0?MischiefKind.FinGrab:MischiefKind.GentleBite;
                    CleanupTrip trip=CarryTrip(176,(OceanLitter)kind,1,grip,row>=2,out now);
                    g.DrawString((OceanLitter)kind+" / "+(grip==MischiefKind.FinGrab?"flippers":"mouth"),font,Brushes.DarkSlateGray,kind*280+12,row*320+12);
                    using(Bitmap frame=PetWithLitter(atlas,trip,now))g.DrawImageUnscaled(frame,kind*280+15,row*320+32);
                }
                sheet.Save(Path.Combine(path,"random-carry-grips.png"),ImageFormat.Png);
            }
            using(Atlas atlas=new Atlas())using(Bitmap sheet=new Bitmap(800,530))using(Graphics g=Graphics.FromImage(sheet))using(Font font=new Font("Segoe UI",12)){
                g.Clear(Color.FromArgb(227,244,245));
                string[] labels={"Sprawled open","Gathering","Folding","Ready to carry"};
                for(int column=0;column<4;column++)g.DrawString(labels[column],font,Brushes.DarkSlateGray,column*200+15,12);
                for(int row=0;row<3;row++)for(int column=0;column<4;column++)using(Bitmap b=CleanupArt.Litter(atlas,OceanLitter.Net,row,120-column*20,0,-1,column/3.0))g.DrawImageUnscaled(b,column*200+40+column*10,row*160+45+column*10);
                sheet.Save(Path.Combine(path,"net-gathering.png"),ImageFormat.Png);
            }
        }
        static void DeliveryPreview(string path){
            using(Atlas atlas=new Atlas())using(Bitmap sheet=new Bitmap(1560,760))using(Graphics g=Graphics.FromImage(sheet))using(Font label=new Font("Segoe UI",13)){
                g.Clear(Color.FromArgb(227,244,245));
                for(int bin=0;bin<6;bin++){
                    CleanupTrip trip=new CleanupTrip(new Point(bin%2==0?150:900,150),176,new Rectangle(0,0,1280,900),new Random(17),(OceanLitter)(bin%4),2,0,bin%3,bin);
                    trip.PlaceBin(new Point(650,420));double now=0;
                    while(trip.Stage!=CleanupStage.Deposit && now<trip.Expires){now+=.05;trip.Advance(now);}
                    now+=.6;trip.Advance(now);
                    Rectangle region=Rectangle.Union(new Rectangle(trip.Position,Renderer.WindowSize(176)),new Rectangle(trip.Bin,new Size(CleanupTrip.BinSize,CleanupTrip.BinSize)));
                    int x=bin%3*520+(520-region.Width)/2-region.X,y=bin/3*380+45-region.Y;
                    g.DrawString(trip.BinKind.ToString(),label,Brushes.DarkSlateGray,bin%3*520+20,bin/3*380+12);
                    using(Bitmap b=CleanupArt.Bin(atlas,trip.BinKind,CleanupTrip.BinSize))g.DrawImageUnscaled(b,trip.Bin.X+x,trip.Bin.Y+y);
                    using(Bitmap b=CleanupArt.Pet(atlas,trip,now))g.DrawImageUnscaled(b,trip.Position.X+x,trip.Position.Y+y);
                    using(Bitmap b=CleanupArt.Prop(atlas,trip,now)){Point at=trip.PropPosition(now);g.DrawImageUnscaled(b,at.X+x,at.Y+y);}
                }
                sheet.Save(Path.Combine(path,"ocean-delivery.png"),ImageFormat.Png);
            }
        }
        public static void Preview(string path){
            Directory.CreateDirectory(path);
            ReactionPreview(path);DeliveryPreview(path);GripPreview(path);
            using(Atlas atlas=new Atlas())using(Bitmap sheet=new Bitmap(1360,860))using(Graphics g=Graphics.FromImage(sheet))using(Font title=new Font("Segoe UI",13,FontStyle.Bold))using(Brush ink=new SolidBrush(Color.FromArgb(35,76,92))){
                g.Clear(Color.FromArgb(227,244,245));
                CleanupTrip trip=new CleanupTrip(new Point(650,300),176,new Rectangle(0,0,1920,1080),new Random(17),OceanLitter.Bottle,2,0);
                HashSet<CleanupStage> seen=new HashSet<CleanupStage>();int cell=0;
                for(double now=0;now<trip.Expires;now+=.1){
                    bool active=trip.Advance(now);
                    if(trip.Stage<CleanupStage.Finished && seen.Add(trip.Stage)){
                        int x=cell%4*340,y=cell/4*430;cell++;
                        g.DrawString(trip.Stage.ToString(),title,ink,x+20,y+14);
                        using(Bitmap pet=PetWithLitter(atlas,trip,now))g.DrawImageUnscaled(pet,x+25,y+42);
                        if(trip.Stage<CleanupStage.Pickup)using(Bitmap litter=CleanupArt.Litter(atlas,trip.Kind,trip.Variant,trip.SpawnPixels,8,.5,0))g.DrawImageUnscaled(litter,x+245,y+290);
                        if(trip.Stage==CleanupStage.Deposit || trip.Stage==CleanupStage.Celebrate)using(Bitmap basket=CleanupArt.Bin(atlas,trip.BinKind,CleanupTrip.BinSize))g.DrawImageUnscaled(basket,x+215,y+300);
                    }
                    if(!active)break;
                }
                sheet.Save(Path.Combine(path,"cleanup-story.png"),ImageFormat.Png);
            }
            using(Atlas atlas=new Atlas())using(Bitmap sheet=new Bitmap(960,900))using(Graphics g=Graphics.FromImage(sheet))using(Font title=new Font("Segoe UI",14,FontStyle.Bold))using(Font text=new Font("Segoe UI",11)){
                g.Clear(Color.FromArgb(227,244,245));
                for(int kind=0;kind<4;kind++)g.DrawString(new[]{"Bottles","Plastic bags","Lost nets","Rubbish cans"}[kind],title,Brushes.DarkSlateGray,kind*240+45,18);
                for(int variant=0;variant<3;variant++)for(int kind=0;kind<4;kind++){
                    int x=kind*240,y=50+variant*130;
                    if(variant==1)using(Brush bg=new SolidBrush(Color.FromArgb(24,50,68)))g.FillRectangle(bg,x,y,240,130);
                    using(Bitmap b=CleanupArt.Litter(atlas,(OceanLitter)kind,variant,108,-5))g.DrawImageUnscaled(b,x+66,y+10);
                }
                g.DrawString("Mochi's Ocean Bins",title,Brushes.DarkSlateGray,35,465);
                string[] names={"Pearl Clam","Coral Basket","Buoy Barrel","Salvage Chest","Nautilus Pot","Submarine Pod"};
                for(int i=0;i<6;i++){
                    int x=i%3*320,y=510+i/3*185;
                    using(Bitmap b=CleanupArt.Bin(atlas,(OceanBin)i,128))g.DrawImageUnscaled(b,x+96,y);
                    g.DrawString(names[i],text,Brushes.DarkSlateGray,x+95,y+135);
                }
                sheet.Save(Path.Combine(path,"ocean-collection.png"),ImageFormat.Png);
            }
        }
    }
    sealed class CleanupSurfaceProbe : NativeWindow,IDisposable {
        public int Hides,SeparateMoves;public bool Uploading;
        public CleanupSurfaceProbe(IntPtr window){AssignHandle(window);}
        protected override void WndProc(ref Message message){
            if(message.Msg==0x18 && message.WParam==IntPtr.Zero)Hides++;
            if(message.Msg==0x46 && message.LParam!=IntPtr.Zero){
                Native.WindowPosition p=(Native.WindowPosition)Marshal.PtrToStructure(message.LParam,typeof(Native.WindowPosition));
                if((p.flags&0x80)!=0)Hides++;
                if(!Uploading && ((p.flags&1)==0 || (p.flags&2)==0))SeparateMoves++;
            }
            base.WndProc(ref message);
        }
        public void Dispose(){ReleaseHandle();}
    }
    sealed partial class Companion {
        internal static void VerifyCleanupSurface(){
            foreach(OceanLitter kind in Enum.GetValues(typeof(OceanLitter)))foreach(MischiefKind grip in Enum.GetValues(typeof(MischiefKind)))
            using(Companion pet=new Companion(true)){
                int size=new[]{144,176,224}[(int)kind%3];pet.prefs.Size=size;pet.Size=Renderer.WindowSize(size);
                IntPtr handle=pet.Handle;
                CleanupTrip trip=new CleanupTrip(new Point(650,300),size,new Rectangle(0,0,1920,1080),new Random(17),kind,0,0,0,0,null,grip);
                trip.PlaceBin(new Point(grip==MischiefKind.GentleBite?1700:20,80));pet.cleanup=trip;
                bool fail=false,failPlacement=true;
                int attempts=0,placementAttempts=0;double now=0;
                using(CleanupSurfaceProbe probe=new CleanupSurfaceProbe(handle)){
                    pet.bitmapPresentation=delegate(IntPtr window,Bitmap frame,Point position,byte opacity){
                        attempts++;
                        Rectangle expected=CleanupArt.SceneBounds(trip,now);
                        CleanupTests.Check(window==handle && position==expected.Location && frame.Size==expected.Size,"Scene is split between native uploads or misaligned");
                        if(fail)throw new System.ComponentModel.Win32Exception(5,"Temporary scene presentation failure");
                        probe.Uploading=true;
                        try{Native.SetBitmap(window,frame,position,opacity);}finally{probe.Uploading=false;}
                    };
                    pet.backgroundPlacement=delegate(IntPtr window){
                        placementAttempts++;
                        if(failPlacement)throw new System.ComponentModel.Win32Exception(5,"Temporary cleanup placement failure");
                        pet.PlaceBackgroundWindows(window);
                    };
                    Action present=delegate{pet.simulatedTime=now;pet.cleanupLastTick=now;pet.AdvanceCleanup(now);};
                    Action<double> pause=delegate(double seconds){trip.Pause(seconds);now+=seconds;};
                    while(trip.Stage!=CleanupStage.Approach && now<trip.Expires){now+=.1;trip.Advance(now);}
                    present();CleanupTests.Check(!Native.IsWindowVisible(handle),"Placement failure was not contained");
                    int placements=placementAttempts;pause(.05);present();
                    CleanupTests.Check(placementAttempts==placements,"Placement failure busy-looped");
                    failPlacement=false;pause(1.01);present();
                    Action visible=delegate{
                        WallpaperNative.Rect bounds;WallpaperNative.GetWindowRect(handle,out bounds);
                        CleanupTests.Check(pet.Handle==handle && Native.IsWindowVisible(handle) && bounds.Value==CleanupArt.SceneBounds(trip,now) && probe.Hides==0 && probe.SeparateMoves==0,"Scene hid or moved independently of its pixels: "+trip.Stage);
                        CleanupTests.Check(pet.cleanupHitFrame!=null && pet.cleanupHitPosition==trip.Position,"Hit mask lost its scene coordinates");
                        CleanupTests.Check(!pet.CleanupHit(trip.Bin),"Bin intercepted desktop input");
                        Func<Point,IntPtr> hit=delegate(Point point){return CleanupTests.SendMessage(handle,0x84,IntPtr.Zero,new IntPtr((point.X&0xffff)|((point.Y&0xffff)<<16)));};
                        CleanupTests.Check(hit(trip.Bin)==new IntPtr(-1),"Native hit test intercepted the bin");
                        bool clickable=false;
                        for(int y=0;y<pet.cleanupHitFrame.Height && !clickable;y+=4)for(int x=0;x<pet.cleanupHitFrame.Width && !clickable;x+=4)
                            if(pet.cleanupHitFrame.GetPixel(x,y).A>=20)clickable=hit(new Point(trip.Position.X+x,trip.Position.Y+y))==new IntPtr(1);
                        CleanupTests.Check(clickable,"Mochi lost mouse input in the expanded scene");
                    };
                    visible();now=trip.Started+trip.Duration-.10;trip.Advance(now);present();visible();
                    bool injected=false;
                    while(trip.Stage!=CleanupStage.Carry && now<trip.Expires){
                        now+=.025;trip.Advance(now);
                        if(trip.Stage==CleanupStage.Pickup && !injected){
                            injected=true;fail=true;Bitmap before=pet.canvas,hitBefore=pet.cleanupHitFrame;Point hitAt=pet.cleanupHitPosition;
                            int countBefore=attempts;
                            WallpaperNative.Rect oldBounds,newBounds;WallpaperNative.GetWindowRect(handle,out oldBounds);
                            present();WallpaperNative.GetWindowRect(handle,out newBounds);
                            CleanupTests.Check(Object.ReferenceEquals(before,pet.canvas) && Object.ReferenceEquals(hitBefore,pet.cleanupHitFrame) && pet.cleanupHitPosition==hitAt && oldBounds.Value==newBounds.Value,"Failed scene upload did not preserve all pixels, position and input together");
                            pause(.05);present();
                            CleanupTests.Check(attempts==countBefore+1,"Bitmap failure busy-looped");
                            fail=false;pause(.21);present();visible();
                            CleanupTests.Check(!Object.ReferenceEquals(before,pet.canvas),"Scene bitmap did not recover");
                        }else {int countBefore=attempts;present();visible();CleanupTests.Check(attempts==countBefore+1,"Frame did not use exactly one native upload");}
                    }
                    CleanupTests.Check(injected && trip.Stage==CleanupStage.Carry,"Pickup fixture did not finish");
                    for(int frame=0;frame<4;frame++){now+=.15;trip.Advance(now);present();visible();}
                    now=trip.Started+trip.Duration;trip.Advance(now);present();visible();
                    CleanupTests.Check(trip.Stage==CleanupStage.Deposit,"Deposit fixture did not arrive");
                    for(int frame=0;frame<26;frame++){now+=.05;trip.Advance(now);present();visible();}
                    now+=.2;trip.Advance(now);present();visible();
                    CleanupTests.Check(trip.Stage==CleanupStage.Celebrate,"Litter did not reach celebration");
                }
                Point final=trip.Position;pet.bitmapPresentation=Native.SetBitmap;pet.CancelCleanup();
                CleanupTests.Check(pet.cleanupHitFrame==null && pet.cleanup==null && pet.Location==final && pet.Size==Renderer.WindowSize(size) && pet.canvas.Size==Renderer.WindowSize(size),"Cleanup did not restore the normal pet bounds/frame or release its hit mask");
            }
        }
        internal static void VerifyCleanup(){
            using(Companion pet=new Companion(true)){
                pet.desktopFactory=delegate{throw new Exception("Ocean Cleanup must not access Explorer, its icons, or the Recycle Bin.");};
                pet.prefs.OceanCleanup=true;pet.ScheduleCleanup();double wait=pet.nextCleanup-pet.Now;
                CleanupTests.Check(wait>=240 && wait<=420,"Cleanup delay out of range");
                pet.nextIdleActivity=10000;pet.nextPlayful=10000;pet.nextSwim=10000;pet.nextCleanup=1;pet.actionUntil=0;
                pet.prefs.Roam=false;pet.Advance(2,new Point(-9000,-9000));CleanupTests.Check(pet.cleanup==null,"Pause allowed automatic cleanup");
                pet.prefs.Roam=true;pet.prefs.OceanCleanup=false;pet.Advance(3,new Point(-9000,-9000));CleanupTests.Check(pet.cleanup==null,"Off allowed cleanup");
                pet.prefs.OceanCleanup=true;pet.nextIdleActivity=0;pet.Advance(4,new Point(-9000,-9000));
                CleanupTests.Check(pet.cleanup==null && pet.feeding!=null,"Cleanup jumped older idle turn");
                pet.CancelInteraction();pet.nextIdleActivity=10000;pet.automaticReadyAt=0;pet.actionUntil=0;
                pet.Advance(5,new Point(-9000,-9000));CleanupTests.Check(pet.cleanup!=null,"Due cleanup did not start");
                double idle=pet.nextIdleActivity,playful=pet.nextPlayful;
                pet.StartFeeding((FeedKind)Enum.GetValues(typeof(FeedKind)).GetValue(0));CleanupTests.Check(pet.cleanup==null && pet.feeding!=null,"Manual feeding did not cancel");
                CleanupTests.Check(pet.nextIdleActivity==idle && pet.nextPlayful==playful,"Cleanup changed other clocks");
                pet.CancelInteraction();pet.BeginCleanup(true);pet.Advance(pet.Now+10,new Point(-9000,-9000));
                CleanupTests.Check(pet.cleanup==null && pet.nextCleanup-pet.Now>=240,"Suspend caused stale animation or burst");
                pet.BeginCleanup(true);Point bin=pet.cleanup.Bin;bool carried=false;
                for(int n=0;n<3000 && pet.cleanup!=null;n++){
                    CleanupTests.Check(pet.cleanup.Bin==bin,"Ocean Bin moved during the trip");
                    carried|=pet.cleanup.Stage==CleanupStage.Carry;
                    pet.Advance(pet.Now+.1,new Point(-9000,-9000));
                }
                CleanupTests.Check(carried && pet.cleanup==null,"Ocean Bin delivery did not finish without Explorer");
                pet.BeginCleanup(true);pet.Advance(pet.Now+.1,new Point(-9000,-9000));
                CleanupTrip pausedTrip=pet.cleanup;PointF pausedLitter=pausedTrip.Litter;
                pet.menu.Show(new Point(20,20));pet.Advance(pet.Now+10,new Point(-9000,-9000));pet.menu.Close();
                pet.Advance(pet.Now+.1,new Point(-9000,-9000));
                CleanupTests.Check(Object.ReferenceEquals(pausedTrip,pet.cleanup) && pet.cleanup.Stage==CleanupStage.Drift && Math.Abs(pet.cleanup.Litter.X-pausedLitter.X)<3,"Menu dismissal cancelled or jumped cleanup");
                pet.CancelInteraction();
                int previousType=-1,previousBin=-1;int[] previousVariants={-1,-1,-1,-1};
                HashSet<int> variants=new HashSet<int>(),bins=new HashSet<int>();
                for(int n=0;n<120;n++){
                    pet.BeginCleanup(true);CleanupTrip trip=pet.cleanup;int kind=(int)trip.Kind;
                    CleanupTests.Check(kind!=previousType && trip.Variant!=previousVariants[kind] && (int)trip.BinKind!=previousBin,"Cleanup immediately repeated a design");
                    variants.Add(kind*3+trip.Variant);bins.Add((int)trip.BinKind);
                    previousType=kind;previousVariants[kind]=trip.Variant;previousBin=(int)trip.BinKind;
                    pet.CancelInteraction();
                }
                CleanupTests.Check(variants.Count==12 && bins.Count==6,"Cleanup selection missed designs");
                pet.prefs.OceanCleanup=false;pet.prefs.Roam=false;pet.BeginCleanup(true);
                CleanupTests.Check(pet.cleanup!=null,"Explicit cleanup should work with automatic settings off");
                pet.CancelInteraction();
                for(int action=0;action<5;action++){
                    pet.BeginCleanup(true);
                    if(action==0)pet.Pet();
                    else if(action==1)pet.BeginSwim(true);
                    else if(action==2)pet.ChooseDestination();
                    else if(action==3)pet.StartManualSwimming();
                    else pet.OnDown(pet,new MouseEventArgs(MouseButtons.Left,1,0,0,0));
                    CleanupTests.Check(pet.cleanup==null,"Manual control did not cancel cleanup: "+action);
                    pet.down=false;pet.CancelInteraction();
                }
                CleanupTests.Check(pet.cleanup==null && pet.cleanupHitFrame==null,"Cleanup resources remain");
            }
        }
    }
}
#endif
