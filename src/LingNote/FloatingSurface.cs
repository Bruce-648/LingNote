using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LingNote;

// Supersample only the small window chrome. Text, controls and the mascot stay
// in the live visual tree, so they retain their native display resolution.
internal sealed class FloatingSurface : FrameworkElement
{
    private const int Samples = 4;
    private static readonly Brush Outline = new SolidColorBrush(Color.FromRgb(224,231,240));
    private BitmapSource? frame;
    private int frameWidth, frameHeight;

    public FloatingSurface()
    {
        IsHitTestVisible=false;
        RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var dpi=VisualTreeHelper.GetDpi(this);
        var width=(int)Math.Round(ActualWidth*dpi.DpiScaleX);
        var height=(int)Math.Round(ActualHeight*dpi.DpiScaleY);
        if(width<=0||height<=0)return;
        if(frame==null||width!=frameWidth||height!=frameHeight)
        {
            var drawing=new DrawingVisual();
            using(var context=drawing.RenderOpen())
            {
                // Draw in physical pixels: the outline remains one pixel wide
                // at fractional display scales, with smooth circular corners.
                var radius=Math.Min(width,height)/2d;
                context.DrawRoundedRectangle(Brushes.White,new Pen(Outline,1),
                    new Rect(.5,.5,width-1,height-1),radius-.5,radius-.5);
            }
            var highResolution=new RenderTargetBitmap(width*Samples,height*Samples,
                96*Samples,96*Samples,PixelFormats.Pbgra32);
            highResolution.Render(drawing);highResolution.Freeze();
            // Filter to the monitor's actual pixel count once, then cache it.
            frame=new TransformedBitmap(highResolution,new ScaleTransform(1d/Samples,1d/Samples));
            frame.Freeze();frameWidth=width;frameHeight=height;
        }
        dc.DrawImage(frame,new Rect(0,0,width/dpi.DpiScaleX,height/dpi.DpiScaleY));
    }

    protected override void OnDpiChanged(DpiScale oldDpi,DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi,newDpi);frame=null;InvalidateVisual();
    }
}
