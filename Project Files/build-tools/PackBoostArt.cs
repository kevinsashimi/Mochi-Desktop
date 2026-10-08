using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Build-time packing only: the artist's complete poses are scaled and registered,
// never repainted, warped, keyed or composited with another pose.
class PackBoostArt {
    static int Main(string[] args){
        if(args.Length!=2){Console.Error.WriteLine("PackBoostArt source.png boost-poses.png");return 1;}
        using(Bitmap source=new Bitmap(args[0]))using(Bitmap sheet=new Bitmap(768,624,PixelFormat.Format32bppArgb))using(Graphics g=Graphics.FromImage(sheet)){
            if(source.Width!=1536 || source.Height!=1024)throw new Exception("Unexpected source dimensions");
            int[] rows={0,376,700,1024};
            g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
            // One scale for all twelve drawings preserves the relative fin strokes.
            const float scale=.49f;
            for(int row=0;row<3;row++)for(int col=0;col<4;col++){
                Rectangle cell=Rectangle.FromLTRB(col*384,rows[row],(col+1)*384,rows[row+1]);
                int left=cell.Right,top=cell.Bottom,right=cell.Left,bottom=cell.Top;
                for(int y=cell.Top;y<cell.Bottom;y++)for(int x=cell.Left;x<cell.Right;x++)if(source.GetPixel(x,y).A>24){
                    left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x+1);bottom=Math.Max(bottom,y+1);
                }
                if(right-left<300 || bottom-top<180)throw new Exception("Missing pose "+row+","+col);
                Rectangle crop=Rectangle.FromLTRB(Math.Max(cell.Left,left-2),Math.Max(cell.Top,top-2),Math.Min(cell.Right,right+2),Math.Min(cell.Bottom,bottom+2));
                RectangleF destination=new RectangleF(col*192+96-crop.Width*scale/2,row*208+108-crop.Height*scale/2,crop.Width*scale,crop.Height*scale);
                g.DrawImage(source,destination,crop,GraphicsUnit.Pixel);
                Console.WriteLine("Pose "+row+","+col+": "+crop+" -> "+destination);
            }
            sheet.Save(args[1],ImageFormat.Png);
        }
        return 0;
    }
}
