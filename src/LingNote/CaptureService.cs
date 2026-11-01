using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LingNote;
public static class CaptureService
{
    [StructLayout(LayoutKind.Sequential)] public struct NativeRect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window,out NativeRect rect);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out NativeRect rect,int size);
    public static string DefaultDirectory {
        get {var home=AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);if(Path.GetFileName(home).Equals("app",StringComparison.OrdinalIgnoreCase))home=Path.GetDirectoryName(home)!;for(var candidate=new DirectoryInfo(home);candidate!=null;candidate=candidate.Parent)if(Directory.Exists(Path.Combine(candidate.FullName,"src","LingNote"))&&Directory.Exists(Path.Combine(candidate.FullName,"tests"))){home=candidate.FullName;break;}return Path.Combine(home,"download");}
    }
    public static string DirectoryFor(Preferences prefs,string topic)=>!string.IsNullOrWhiteSpace(topic)&&prefs.TopicScreenshotDirectories.TryGetValue(topic,out var custom)&&!string.IsNullOrWhiteSpace(custom)?custom:!string.IsNullOrWhiteSpace(prefs.ScreenshotDirectory)?prefs.ScreenshotDirectory:DefaultDirectory;
    public static System.Drawing.Rectangle WindowBounds(IntPtr window)
    {
        if(DwmGetWindowAttribute(window,9,out var rect,Marshal.SizeOf<NativeRect>())!=0&&!GetWindowRect(window,out rect))throw new IOException("无法获取当前窗口范围。");
        var bounds=System.Drawing.Rectangle.Intersect(System.Windows.Forms.SystemInformation.VirtualScreen,System.Drawing.Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom));
        if(bounds.Width<1||bounds.Height<1)throw new IOException("当前窗口不可截取。");return bounds;
    }
    public static BitmapSource Grab(System.Drawing.Rectangle bounds)
    {
        using var bitmap=new System.Drawing.Bitmap(bounds.Width,bounds.Height);using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen(bounds.Location,System.Drawing.Point.Empty,bounds.Size);
        using var buffer=new MemoryStream();bitmap.Save(buffer,System.Drawing.Imaging.ImageFormat.Png);buffer.Position=0;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=buffer;image.EndInit();image.Freeze();return image;
    }
    public static string Save(BitmapSource image,string directory)
    {
        Directory.CreateDirectory(directory);var file=Path.Combine(directory,"LingNote_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+"_"+Guid.NewGuid().ToString("N")[..6]+".png");
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var output=new FileStream(file,FileMode.CreateNew);encoder.Save(output);return file;
    }
}
internal sealed class CaptureOverlay : Window
{
    public BitmapSource? Result {get;private set;}
    public CaptureOverlay(BitmapSource image,System.Drawing.Rectangle screen)
    {
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;WindowStartupLocation=WindowStartupLocation.Manual;Cursor=Cursors.Cross;Background=Brushes.Black;
        // Set the native bounds after WPF creates the HWND, so mixed-DPI monitors
        // use physical pixels consistently with CopyFromScreen and the crop.
        SourceInitialized+=(_,_)=>SetWindowPos(new WindowInteropHelper(this).Handle,new IntPtr(-1),screen.X,screen.Y,screen.Width,screen.Height,0x0040);
        var root=new Grid();Content=root;root.Children.Add(new Image {Source=image,Stretch=Stretch.Fill});
        var canvas=new Canvas {Background=new SolidColorBrush(Color.FromArgb(55,0,0,0))};root.Children.Add(canvas);
        var selection=new System.Windows.Shapes.Rectangle {Stroke=Brushes.DodgerBlue,StrokeThickness=2,Fill=new SolidColorBrush(Color.FromArgb(15,255,255,255)),Visibility=Visibility.Collapsed};canvas.Children.Add(selection);
        var hint=new TextBlock {Text="拖动选择截屏区域 · Esc 取消",Background=Brushes.White,Foreground=Brushes.Black,Padding=new Thickness(12),Margin=new Thickness(20),VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Center,IsHitTestVisible=false};root.Children.Add(hint);
        Point? start=null;
        canvas.MouseLeftButtonDown+=(_,e)=>{start=e.GetPosition(canvas);canvas.CaptureMouse();selection.Visibility=Visibility.Visible;hint.Visibility=Visibility.Collapsed;};
        canvas.MouseMove+=(_,e)=>{if(start is not Point p)return;var q=e.GetPosition(canvas);Canvas.SetLeft(selection,Math.Min(p.X,q.X));Canvas.SetTop(selection,Math.Min(p.Y,q.Y));selection.Width=Math.Abs(q.X-p.X);selection.Height=Math.Abs(q.Y-p.Y);};
        canvas.MouseLeftButtonUp+=(_,e)=>{if(start is not Point p)return;canvas.ReleaseMouseCapture();var q=e.GetPosition(canvas);start=null;var sx=image.PixelWidth/canvas.ActualWidth;var sy=image.PixelHeight/canvas.ActualHeight;int x=Math.Clamp((int)(Math.Min(p.X,q.X)*sx),0,image.PixelWidth-1),y=Math.Clamp((int)(Math.Min(p.Y,q.Y)*sy),0,image.PixelHeight-1);int w=Math.Min(image.PixelWidth-x,(int)(Math.Abs(q.X-p.X)*sx)),h=Math.Min(image.PixelHeight-y,(int)(Math.Abs(q.Y-p.Y)*sy));if(w<2||h<2){selection.Visibility=Visibility.Collapsed;return;}Result=new CroppedBitmap(image,new Int32Rect(x,y,w,h));DialogResult=true;};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape)DialogResult=false;};
    }
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
}
public partial class MainWindow
{
    private bool capturing;
    private HwndSource? captureSource;
    private bool windowHotkey,regionHotkey;
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window,int id);
    private void InitializeCaptureHotkeys()
    {
        var handle=new WindowInteropHelper(this).Handle;captureSource=HwndSource.FromHwnd(handle);captureSource.AddHook(CaptureHook);
        windowHotkey=RegisterHotKey(handle,101,0x4006,0x31);regionHotkey=RegisterHotKey(handle,102,0x4006,0x32);
        if(!windowHotkey||!regionHotkey)Hint.Text="部分截屏快捷键已被占用，可使用悬浮条截屏按钮。";
        Closed+=(_,_)=>{if(windowHotkey)UnregisterHotKey(handle,101);if(regionHotkey)UnregisterHotKey(handle,102);captureSource.RemoveHook(CaptureHook);};
    }
    private IntPtr CaptureHook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message==0x0312&&wParam.ToInt32() is 101 or 102){handled=true;CaptureScreenshot(wParam.ToInt32()==102);}return IntPtr.Zero;
    }
    private async void CaptureScreenshot(bool region)
    {
        if(capturing)return;capturing=true;bool visible=floating.IsVisible;var foreground=CaptureService.GetForegroundWindow();
        if(foreground==new WindowInteropHelper(floating).Handle)foreground=new WindowInteropHelper(this).Handle;
        var directory=CaptureService.DirectoryFor(prefs,recording?.Topic??selected?.Topic??draftTopic);
        try {
            var bounds=region?System.Windows.Forms.SystemInformation.VirtualScreen:CaptureService.WindowBounds(foreground);
            if(visible)floating.Hide();await Task.Delay(160);
            var image=CaptureService.Grab(bounds);
            if(region){var overlay=new CaptureOverlay(image,bounds);if(overlay.ShowDialog()!=true)return;image=overlay.Result!;}
            var file=CaptureService.Save(image,directory);Hint.Text="截屏已保存："+file;tray.ShowBalloonTip(2200,"截屏已保存",file,System.Windows.Forms.ToolTipIcon.Info);
        }catch(Exception ex){Hint.Text="截屏失败："+ex.Message;tray.ShowBalloonTip(2500,"截屏失败",ex.Message,System.Windows.Forms.ToolTipIcon.Error);}
        finally {if(visible&&recording!=null)floating.Show();capturing=false;}
    }
}
