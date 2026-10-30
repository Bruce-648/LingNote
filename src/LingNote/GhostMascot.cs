using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace LingNote;
public sealed class GhostMascot : FrameworkElement
{
    private static readonly BitmapSource Logo=LoadLogo();
    private readonly DispatcherTimer timer=new() {Interval=TimeSpan.FromMilliseconds(33)};
    private readonly Stopwatch clock=new();
    private bool blinking,floating;
    public bool IsBlinking=>blinking;
    public bool IsFloating=>floating;
    public GhostMascot()
    {
        Width=32;Height=38;IsHitTestVisible=false;SnapsToDevicePixels=true;UseLayoutRounding=true;RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);timer.Tick+=(_,_)=>InvalidateVisual();
        IsVisibleChanged+=(_,_)=>UpdateTimer();Loaded+=(_,_)=>UpdateTimer();Unloaded+=(_,_)=>timer.Stop();
    }
    public void Animate(bool blink,bool bob=false)
    {
        if(blinking==blink&&floating==bob)return;
        blinking=blink;floating=bob;clock.Restart();UpdateTimer();InvalidateVisual();
    }
    private void UpdateTimer(){if(IsVisible&&(blinking||floating)){clock.Start();timer.Start();}else{timer.Stop();clock.Stop();}}
    private static BitmapSource LoadLogo(){var image=new BitmapImage(new Uri("pack://application:,,,/LingNote;component/Assets/app-logo.png"));image.Freeze();return image;}
    protected override void OnRender(DrawingContext dc)
    {
        var size=Math.Min(ActualWidth,ActualHeight-6);if(size<=0)return;
        var seconds=clock.Elapsed.TotalSeconds;var bob=floating?Math.Sin(seconds*Math.PI*1.1)*2.5:0;
        var dpi=VisualTreeHelper.GetDpi(this);
        var x=Math.Round((ActualWidth-size)/2*dpi.DpiScaleX)/dpi.DpiScaleX;var y=Math.Round(((ActualHeight-size)/2+bob)*dpi.DpiScaleY)/dpi.DpiScaleY;
        dc.PushTransform(new TranslateTransform(x,y));dc.DrawImage(Logo,new Rect(0,0,size,size));dc.PushTransform(new ScaleTransform(size/1024,size/1024));
        var phase=seconds%1.9;var openness=blinking&&phase>1.68?Math.Clamp(Math.Abs(phase-1.79)/.11,.06,1):1;
        if(openness<1){Eye(dc,548,409,74,117,11,openness);Eye(dc,805,431,68,112,9,openness);}dc.Pop();dc.Pop();
    }
    private static void Eye(DrawingContext dc,double x,double y,double rx,double ry,double angle,double openness)
    {
        dc.PushTransform(new RotateTransform(angle,x,y));dc.DrawEllipse(Brushes.Black,null,new Point(x,y),rx+9,ry+9);
        dc.DrawEllipse(Brushes.WhiteSmoke,null,new Point(x,y),rx,ry*openness);dc.Pop();
    }
}
