using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace MochiDesktop {
    sealed class ManualBoostWake {
        sealed class Bubble {
            public PointF Origin,Drift;public double Born,Life,Phase;public float Radius;public bool Heart;
            public PointF Position(double now){
                double age=Math.Max(0,now-Born);
                return new PointF(Origin.X+Drift.X*(float)age+(float)Math.Sin(age*5+Phase)*3,
                    Origin.Y+Drift.Y*(float)age-(float)(age*age*9));
            }
        }
        readonly List<Bubble> bubbles=new List<Bubble>();
        readonly Rectangle area;readonly Random random;
        public int Count {get{return bubbles.Count;}}
        public ManualBoostWake(Rectangle bounds,Random rng){area=bounds;random=rng;}
        public void Advance(double now,PointF from,PointF to,PointF velocity,int size,bool boosting){
            bubbles.RemoveAll(delegate(Bubble b){return now-b.Born>=b.Life;});
            double dx=to.X-from.X,dy=to.Y-from.Y,distance=Math.Sqrt(dx*dx+dy*dy);
            if(!boosting || distance<.2)return;
            double speed=Math.Sqrt(velocity.X*velocity.X+velocity.Y*velocity.Y);
            if(speed<1)return;
            float hx=(float)(velocity.X/speed),hy=(float)(velocity.Y/speed),unit=size/176f;
            Rectangle body=Renderer.SpriteRect(size);
            int groups=Math.Max(1,Math.Min(4,(int)Math.Ceiling(distance/10)));
            for(int n=0;n<groups;n++)for(int side=0;side<2;side++){
                float along=(n+.5f)/groups,splay=(float)(random.NextDouble()-.5)*22*unit;
                PointF tail=new PointF(from.X+(float)dx*along+body.Left+body.Width*.5f-hx*size*.32f-hy*splay,
                    from.Y+(float)dy*along+body.Top+body.Height*.53f-hy*size*.32f+hx*splay);
                bubbles.Add(new Bubble{Origin=tail,Drift=new PointF(-hx*18+(float)(random.NextDouble()-.5)*12,-hy*18-15),
                    Born=now,Life=.85+random.NextDouble()*.5,Phase=random.NextDouble()*Math.PI*2,
                    Radius=(3+(float)random.NextDouble()*7)*unit,Heart=random.Next(13)==0});
            }
            // At most one brief wake remains; memory stays bounded at low timer rates too.
            if(bubbles.Count>72)bubbles.RemoveRange(0,bubbles.Count-72);
        }
        public Rectangle Bounds(double now){
            if(bubbles.Count==0)return Rectangle.Empty;
            Rectangle bounds=Rectangle.Empty;
            foreach(Bubble b in bubbles){
                PointF p=b.Position(now);int radius=(int)Math.Ceiling(b.Radius*1.6)+4;
                Rectangle r=new Rectangle((int)Math.Floor(p.X)-radius,(int)Math.Floor(p.Y)-radius,radius*2,radius*2);
                bounds=bounds.IsEmpty?r:Rectangle.Union(bounds,r);
            }
            bounds=Rectangle.Intersect(bounds,area);return bounds.Width<=0 || bounds.Height<=0?Rectangle.Empty:bounds;
        }
        public Bitmap Draw(double now,Rectangle bounds){
            Bitmap frame=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(frame)){
                g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.TranslateTransform(-bounds.Left,-bounds.Top);
                foreach(Bubble b in bubbles){
                    double life=Math.Max(0,Math.Min(1,(now-b.Born)/b.Life)),fade=(1-life)*(1-life);
                    float radius=b.Radius*(float)(.7+life*.7);PointF p=b.Position(now);
                    using(Brush tint=new SolidBrush(Color.FromArgb((int)(45*fade),151,233,250)))
                    using(Pen rim=new Pen(Color.FromArgb((int)(195*fade),b.Heart?253:138,b.Heart?192:224,255),1.4f))
                    using(Pen shine=new Pen(Color.FromArgb((int)(240*fade),246,255,255),1.2f)){
                        if(b.Heart){
                            using(GraphicsPath heart=new GraphicsPath()){
                                heart.AddBezier(p.X,p.Y-radius*.45f,p.X-radius*.5f,p.Y-radius*1.25f,p.X-radius*1.5f,p.Y-radius*.5f,p.X-radius*.75f,p.Y+radius*.25f);
                                heart.AddBezier(p.X-radius*.75f,p.Y+radius*.25f,p.X-radius*.4f,p.Y+radius*.7f,p.X,p.Y+radius,p.X,p.Y+radius);
                                heart.AddBezier(p.X,p.Y+radius,p.X+radius*.4f,p.Y+radius*.7f,p.X+radius*.75f,p.Y+radius*.25f,p.X+radius*.75f,p.Y+radius*.25f);
                                heart.AddBezier(p.X+radius*.75f,p.Y+radius*.25f,p.X+radius*1.5f,p.Y-radius*.5f,p.X+radius*.5f,p.Y-radius*1.25f,p.X,p.Y-radius*.45f);
                                heart.CloseFigure();g.FillPath(tint,heart);g.DrawPath(rim,heart);
                            }
                        }else {
                            RectangleF ring=new RectangleF(p.X-radius,p.Y-radius,radius*2,radius*2);
                            g.FillEllipse(tint,ring);g.DrawEllipse(rim,ring);
                            g.DrawArc(shine,p.X-radius*.65f,p.Y-radius*.65f,radius*1.3f,radius*1.3f,190,70);
                            if(b.Radius>7){
                                float x=p.X+radius*.58f,y=p.Y-radius*.58f,r=radius*.22f;
                                g.DrawLine(shine,x-r,y,x+r,y);g.DrawLine(shine,x,y-r,x,y+r);
                            }
                        }
                    }
                }
            }
            return frame;
        }
    }

    // A tightly sized transparent wake, rather than repainting a whole 4K overlay
    // every frame. It cannot receive clicks or keyboard focus.
    sealed class ManualWakeWindow : NativeWindow,IDisposable {
        public bool Visible {get;private set;}
        public bool IsDisposed {get;private set;}
        public Rectangle Bounds {get;private set;}
        public ManualWakeWindow(string title="Mochi's bubble wake"){
            // A native surface has no Form activation/owner handoff on Show or Close.
            CreateHandle(new CreateParams{Caption=title,ClassName="STATIC",Style=unchecked((int)0x80000000),
                ExStyle=0x80000|0x80|0x20|0x08000000|0x8,Width=1,Height=1});
        }
        public void Present(Bitmap frame,Rectangle bounds,byte opacity=255){
            Bounds=bounds;Native.SetBitmap(Handle,frame,bounds.Location,opacity);
            if(!Visible){Native.ShowWindow(Handle,4);Visible=true;} // SW_SHOWNOACTIVATE
        }
        public void Hide(){if(Visible){Native.ShowWindow(Handle,0);Visible=false;}}
        public void Dispose(){if(IsDisposed)return;IsDisposed=true;Visible=false;DestroyHandle();}
        protected override void WndProc(ref Message m){
            if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}
            if(m.Msg==0x21){m.Result=new IntPtr(3);return;}
            base.WndProc(ref m);
        }
    }

    static class ManualBoostRenderer {
        public static void DrawStatus(Bitmap frame,double now,double until,double readyAt,Rectangle? viewport=null,Rectangle? body=null){
            float width=Math.Min(184,frame.Width-20),height=18,x=(frame.Width-width)/2,y=frame.Height-21;
            if(viewport.HasValue){
                Rectangle v=viewport.Value;width=Math.Min(width,v.Width-8);if(width<60 || v.Height<height+4)return;
                x=Math.Max(v.Left+4,Math.Min(v.Right-width-4,(frame.Width-width)/2));
                if(y+height>v.Bottom-2 && body.HasValue)y=body.Value.Top-height-5;
                y=Math.Max(v.Top+2,Math.Min(v.Bottom-height-2,y));
            }
            double remaining=Math.Max(0,readyAt-now);string label=now<until?"Bubble boost!":remaining>0?"Boost in "+Math.Ceiling(remaining)+"s":"Space: Boost!";
            using(Graphics g=Graphics.FromImage(frame)){
                g.SmoothingMode=SmoothingMode.AntiAlias;
                using(GraphicsPath pill=new GraphicsPath()){
                    pill.AddArc(x,y,height,height,90,180);pill.AddArc(x+width-height,y,height,height,270,180);pill.CloseFigure();
                    using(Brush paper=new SolidBrush(Color.FromArgb(235,241,252,255)))g.FillPath(paper,pill);
                    using(Pen rim=new Pen(Color.FromArgb(210,117,188,213)))g.DrawPath(rim,pill);
                    GraphicsState state=g.Save();g.SetClip(pill);
                    float progress=(float)(remaining>0?1-Math.Min(1,remaining/ManualSwimMotion.BoostCooldown):1);
                    using(Brush water=new SolidBrush(Color.FromArgb(210,91,203,228)))g.FillRectangle(water,x,y+height-3,width*progress,3);
                    g.Restore(state);
                }
                using(Font font=new Font("Segoe UI",11,FontStyle.Regular,GraphicsUnit.Pixel))
                using(Brush ink=new SolidBrush(Color.FromArgb(29,64,83)))
                using(StringFormat text=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
                    g.DrawString(label,font,ink,new RectangleF(x,y-1,width,height-1),text);
            }
        }
    }
}
