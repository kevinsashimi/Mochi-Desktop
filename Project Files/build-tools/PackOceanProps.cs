using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Extract complete illustrated objects; only alpha noise is discarded.
class PackOceanProps {
    sealed class Part { public int Id;public Rectangle Bounds; }
    static int Main(string[] args) {
        if(args.Length!=3){Console.Error.WriteLine("PackOceanProps litter|bins|nets source.png output.png");return 1;}
        bool bins=args[0]=="bins",nets=args[0]=="nets";int cols=bins||nets?3:4,rows=nets?1:bins?2:3,tile=bins||nets?160:128;
        using(Bitmap source=new Bitmap(args[1]))using(Bitmap sheet=new Bitmap(cols*tile,rows*tile,PixelFormat.Format32bppArgb))using(Graphics g=Graphics.FromImage(sheet)){
            int width=source.Width,height=source.Height;
            if(width!=1536 || height!=1024)throw new Exception("Unexpected prop source dimensions");
            int[] labels=new int[width*height];List<Part> parts=new List<Part>();
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)labels[y*width+x]=source.GetPixel(x,y).A>24?-1:0;
            int id=0;Queue<int> pending=new Queue<int>();
            for(int start=0;start<labels.Length;start++){
                if(labels[start]!=-1)continue;
                int count=0,left=width,top=height,right=0,bottom=0;labels[start]=++id;pending.Enqueue(start);
                while(pending.Count>0){
                    int index=pending.Dequeue(),x=index%width,y=index/width;count++;
                    left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x+1);bottom=Math.Max(bottom,y+1);
                    foreach(int neighbor in new[]{x>0?index-1:-1,x+1<width?index+1:-1,y>0?index-width:-1,y+1<height?index+width:-1})
                        if(neighbor>=0 && labels[neighbor]==-1){labels[neighbor]=id;pending.Enqueue(neighbor);}
                }
                if(count>10000)parts.Add(new Part{Id=id,Bounds=Rectangle.FromLTRB(left,top,right,bottom)});
            }
            if(parts.Count!=cols*rows)throw new Exception("Wrong number of complete props: "+parts.Count);
            parts.Sort(delegate(Part a,Part b){int ar=(a.Bounds.Top+a.Bounds.Height/2)*rows/height,br=(b.Bounds.Top+b.Bounds.Height/2)*rows/height;return ar==br?a.Bounds.Left.CompareTo(b.Bounds.Left):ar.CompareTo(br);});
            g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
            for(int i=0;i<parts.Count;i++){
                Part part=parts[i];Rectangle crop=part.Bounds;crop.Inflate(2,2);crop.Intersect(new Rectangle(0,0,width,height));
                using(Bitmap isolated=new Bitmap(crop.Width,crop.Height,PixelFormat.Format32bppArgb)){
                    for(int y=0;y<crop.Height;y++)for(int x=0;x<crop.Width;x++)
                        if(labels[(crop.Y+y)*width+crop.X+x]==part.Id)isolated.SetPixel(x,y,source.GetPixel(crop.X+x,crop.Y+y));
                    float scale=(tile-24f)/Math.Max(crop.Width,crop.Height);
                    RectangleF target=new RectangleF(i%cols*tile+(tile-crop.Width*scale)/2,i/cols*tile+(tile-crop.Height*scale)/2,crop.Width*scale,crop.Height*scale);
                    g.DrawImage(isolated,target,new Rectangle(0,0,isolated.Width,isolated.Height),GraphicsUnit.Pixel);
                    Console.WriteLine("Prop {0}: {1}",i,target);
                }
            }
            sheet.Save(args[2],ImageFormat.Png);
        }
        return 0;
    }
}
