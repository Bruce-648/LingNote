using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LingNote;
// The generated PNG remains the source of each silhouette. Display masks use
// consistent optical bounds, stroke weight and opacity without aspect stretching.
public static class SettingsIcons
{
    private static readonly Dictionary<string,BitmapSource> images=new();
    private static BitmapSource Source(string name)
    {
        if(images.TryGetValue(name,out var cached))return cached;
        var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.UriSource=new Uri($"pack://application:,,,/LingNote;component/Assets/Settings/{name}.png");bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=256;bitmap.EndInit();bitmap.Freeze();
        var rgba=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);int width=rgba.PixelWidth,height=rgba.PixelHeight;var pixels=new byte[width*height*4];rgba.CopyPixels(pixels,width*4,0);
        int left=width,top=height,right=-1,bottom=-1;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(pixels[(y*width+x)*4+3]>=64){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
        if(right<left)throw new InvalidOperationException("设置图标为空："+name);
        const int side=256;var shape=new bool[side*side];double scale=216d/Math.Max(right-left+1,bottom-top+1),cx=(left+right)/2d,cy=(top+bottom)/2d;
        // Uniform scaling retains the microphone's upright proportions and round
        // circles; equal transparent margins alone cannot normalize stroke weight.
        for(int y=0;y<side;y++)for(int x=0;x<side;x++) {
            int sx=(int)Math.Round((x-(side-1)/2d)/scale+cx),sy=(int)Math.Round((y-(side-1)/2d)/scale+cy);
            shape[y*side+x]=sx>=0&&sy>=0&&sx<width&&sy<height&&pixels[(sy*width+sx)*4+3]>=64;
        }
        int area=shape.Count(v=>v),axis=0,diagonal=0;
        for(int y=1;y<side;y++)for(int x=1;x<side;x++) {
            bool value=shape[y*side+x];if(value!=shape[y*side+x-1])axis++;if(value!=shape[(y-1)*side+x])axis++;
            if(value!=shape[(y-1)*side+x-1])diagonal++;if(x+1<side&&value!=shape[(y-1)*side+x+1])diagonal++;
        }
        double perimeter=Math.PI/8*(axis+diagonal/Math.Sqrt(2)),stroke=2*area/Math.Max(1,perimeter);
        // At an 18px slot this yields approximately a 1.3px dark outline. Apply
        // one offset to the source boundaries instead of stretching the glyph.
        double offset=Math.Clamp((18.5-stroke)/2,-7,7);
        var inside=Distance(shape,side,false);var outside=Distance(shape,side,true);var mask=new byte[side*side*4];
        left=top=side;right=bottom=-1;
        for(int y=0;y<side;y++)for(int x=0;x<side;x++) {
            int i=y*side+x;double distance=shape[i]?inside[i]-.5:-outside[i]+.5;byte alpha=(byte)Math.Clamp((distance+offset+.5)*255,0,255);
            int index=i*4;mask[index]=mask[index+1]=mask[index+2]=255;mask[index+3]=alpha;
            if(alpha>=128){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
        }
        var normalized=BitmapSource.Create(side,side,96,96,PixelFormats.Bgra32,null,mask,side*4);normalized.Freeze();
        int padding=Math.Max(3,(int)(Math.Max(right-left+1,bottom-top+1)*.07));left=Math.Max(0,left-padding);top=Math.Max(0,top-padding);right=Math.Min(side-1,right+padding);bottom=Math.Min(side-1,bottom+padding);
        var source=new CroppedBitmap(normalized,new Int32Rect(left,top,right-left+1,bottom-top+1));source.Freeze();images[name]=source;return source;
    }
    private static double[] Distance(bool[] shape,int side,bool target)
    {
        var values=shape.Select(value=>value==target?0d:10000d).ToArray();double diagonal=Math.Sqrt(2);
        for(int y=0;y<side;y++)for(int x=0;x<side;x++){int i=y*side+x;if(x>0)values[i]=Math.Min(values[i],values[i-1]+1);if(y>0){values[i]=Math.Min(values[i],values[i-side]+1);if(x>0)values[i]=Math.Min(values[i],values[i-side-1]+diagonal);if(x+1<side)values[i]=Math.Min(values[i],values[i-side+1]+diagonal);}}
        for(int y=side-1;y>=0;y--)for(int x=side-1;x>=0;x--){int i=y*side+x;if(x+1<side)values[i]=Math.Min(values[i],values[i+1]+1);if(y+1<side){values[i]=Math.Min(values[i],values[i+side]+1);if(x>0)values[i]=Math.Min(values[i],values[i+side-1]+diagonal);if(x+1<side)values[i]=Math.Min(values[i],values[i+side+1]+diagonal);}}
        return values;
    }
    public static Image Image(string name,int size=18,bool white=false,Brush? tint=null)
    {
        var drawing=new DrawingGroup {OpacityMask=new ImageBrush(Source(name)) {Stretch=Stretch.Uniform}};
        drawing.Children.Add(new GeometryDrawing(tint??(white?Brushes.White:new SolidColorBrush(Color.FromRgb(34,38,44))),null,new RectangleGeometry(new Rect(0,0,24,24))));drawing.Freeze();
        var image=new Image {Source=new DrawingImage(drawing),Width=size,Height=size,VerticalAlignment=VerticalAlignment.Center};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);return image;
    }
    public static object Label(string name,string text)
    {
        var panel=new StackPanel {Orientation=Orientation.Horizontal};panel.Children.Add(Image(name));panel.Children.Add(new TextBlock {Text=text,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(7,0,0,0)});return panel;
    }
}
