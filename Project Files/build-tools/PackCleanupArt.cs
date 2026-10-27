using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Register complete generated poses around the head, not changing fin bounds.
// No facial repainting, eye resizing, or compositing between poses.
class PackCleanupArt {
    sealed class Pose { public int Id,Count;public Rectangle Bounds; }
    static int Main(string[] args) {
        if(args.Length!=2){Console.Error.WriteLine("PackCleanupArt source.png output.png");return 1;}
        using(Bitmap source=new Bitmap(args[0]))using(Bitmap sheet=new Bitmap(768,624,PixelFormat.Format32bppArgb))using(Graphics g=Graphics.FromImage(sheet)){
            int width=source.Width,height=source.Height;
            if(width!=1448 || height!=1086)throw new Exception("Unexpected cleanup source dimensions");
            int[] labels=new int[width*height];List<Pose> poses=new List<Pose>();
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
                if(count>10000)poses.Add(new Pose{Id=id,Count=count,Bounds=Rectangle.FromLTRB(left,top,right,bottom)});
            }
            if(poses.Count!=12)throw new Exception("Expected twelve complete character components");
            poses.Sort(delegate(Pose a,Pose b){int ar=(a.Bounds.Top+a.Bounds.Height/2)/362,br=(b.Bounds.Top+b.Bounds.Height/2)/362;return ar==br?a.Bounds.Left.CompareTo(b.Bounds.Left):ar.CompareTo(br);});
            int[] headTop={97,430,762};const float scale=.5f;
            g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
            for(int i=0;i<12;i++){
                Pose pose=poses[i];int row=i/4,col=i%4;Rectangle crop=pose.Bounds;crop.Inflate(2,2);crop.Intersect(new Rectangle(0,0,width,height));
                using(Bitmap isolated=new Bitmap(crop.Width,crop.Height,PixelFormat.Format32bppArgb)){
                    // Isolate the component because one opened fin crosses its grid
                    // slot. Discard only disconnected/near-invisible generator dust.
                    for(int y=0;y<crop.Height;y++)for(int x=0;x<crop.Width;x++)
                        if(labels[(crop.Y+y)*width+crop.X+x]==pose.Id)isolated.SetPixel(x,y,source.GetPixel(crop.X+x,crop.Y+y));
                    RectangleF target=new RectangleF(col*192+10+(crop.Left-(14+362*col))*scale,
                        row*208+35+(crop.Top-headTop[row])*scale,crop.Width*scale,crop.Height*scale);
                    g.DrawImage(isolated,target,new Rectangle(0,0,isolated.Width,isolated.Height),GraphicsUnit.Pixel);
                    Console.WriteLine("Pose {0},{1}: {2} -> {3}",row,col,crop,target);
                }
            }
            sheet.Save(args[1],ImageFormat.Png);
        }
        return 0;
    }
}
