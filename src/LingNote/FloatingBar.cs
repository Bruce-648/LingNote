using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LingNote;
public sealed class FloatingBar : Window
{
    private readonly TextBlock time=new() {FontSize=19,FontFamily=new FontFamily("Consolas"),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(6,0,10,0)};
    private readonly Button pause=new() {Width=42,Height=40,Padding=new Thickness(12),ToolTip="暂停／继续"};
    private readonly Waveform meter=new() {Width=76,Height=30,Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center};
    private readonly GhostMascot ghost=new() {Width=42,Height=48};
    private readonly System.Windows.Shapes.Ellipse compactDot=new() {Width=10,Height=10,Fill=new SolidColorBrush(Color.FromRgb(255,48,56)),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,4,6,0)};
    private readonly Button collapse;
    private readonly StackPanel controls;
    private readonly Border bubble;
    private readonly Preferences prefs;
    private bool compact,transitioning,previousPaused;
    private Point? pressStart;
    public bool IsCompact=>compact;
    public FloatingBar(Preferences config,Action toggle,Action finish,Action restore)
    {
        prefs=config;Title="LingNote 录音控制";Width=430;Height=96;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;ShowActivated=false;
        UseLayoutRounding=true;SnapsToDevicePixels=true;
        RenderOptions.SetEdgeMode(this,EdgeMode.Unspecified);
        TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);
        var root=new Grid();Content=root;
        controls=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
        pause.Content=UiIcons.Image("pause",18);
        var stop=new Button {Content=UiIcons.Image("stop",18),Width=42,Height=40,Padding=new Thickness(12),ToolTip="结束并整理"};
        var open=new Button {Content=UiIcons.Image("open",18),Width=42,Height=40,Padding=new Thickness(12),ToolTip="返回主窗口"};
        System.Windows.Automation.AutomationProperties.SetName(pause,"暂停或继续");System.Windows.Automation.AutomationProperties.SetName(stop,"结束并整理");System.Windows.Automation.AutomationProperties.SetName(open,"返回主窗口");
        controls.Children.Add(new System.Windows.Shapes.Ellipse {Width=14,Height=14,Fill=(Brush)FindResource("RecordingRed"),Margin=new Thickness(8,0,12,0),VerticalAlignment=VerticalAlignment.Center});controls.Children.Add(time);controls.Children.Add(meter);controls.Children.Add(pause);controls.Children.Add(stop);controls.Children.Add(open);
        var surface=new Grid {ClipToBounds=true};surface.Children.Add(controls);
        var compactContent=new Grid();compactContent.Children.Add(ghost);compactContent.Children.Add(compactDot);
        bubble=new Border {Background=Brushes.Transparent,Child=compactContent,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Opacity=0,Visibility=Visibility.Hidden,Cursor=Cursors.Hand,ToolTip="点击展开录音控制；拖动可移动"};surface.Children.Add(bubble);
        root.Children.Add(new FloatingSurface {Margin=new Thickness(4,28,4,4)});
        root.Children.Add(new Border {BorderThickness=new Thickness(1),Child=surface,Margin=new Thickness(4,28,4,4)});
        collapse=new Button {Content=UiIcons.Image("minimize",14),Width=24,Height=24,Padding=new Thickness(0),Margin=new Thickness(0,0,7,0),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Background=Brushes.White,Opacity=0,IsHitTestVisible=false,ToolTip="收起悬浮条"};
        var template=new ControlTemplate(typeof(Button));
        var circle=new FrameworkElementFactory(typeof(Grid));
        circle.SetValue(Panel.BackgroundProperty,Brushes.Transparent);
        circle.AppendChild(new FrameworkElementFactory(typeof(FloatingSurface)));
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty,HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty,VerticalAlignment.Center);
        circle.AppendChild(presenter);template.VisualTree=circle;collapse.Template=template;
        System.Windows.Automation.AutomationProperties.SetName(collapse,"收起悬浮条");root.Children.Add(collapse);
        MouseEnter+=(_,_)=>RevealCollapse(true);MouseLeave+=(_,_)=>RevealCollapse(false);
        collapse.Click+=(_,_)=>SetCompact(true);pause.Click+=(_,_)=>toggle();stop.Click+=(_,_)=>finish();open.Click+=(_,_)=>restore();
        var menu=new ContextMenu();void Add(string text,Action action){var item=new MenuItem {Header=text};item.Click+=(_,_)=>action();menu.Items.Add(item);}
        Add("展开录音控制",()=>SetCompact(false));Add("暂停／继续",toggle);Add("结束并整理",finish);Add("返回主窗口",restore);bubble.ContextMenu=menu;
        PreviewMouseLeftButtonDown+=(_,e)=>{
            if(FindButton(e.OriginalSource as DependencyObject)!=null||transitioning)return;
            pressStart=PointToScreen(e.GetPosition(this));CaptureMouse();e.Handled=true;
        };
        PreviewMouseMove+=(_,e)=>{
            if(pressStart is not Point start||e.LeftButton!=MouseButtonState.Pressed)return;
            var delta=PointToScreen(e.GetPosition(this))-start;
            if(Math.Abs(delta.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(delta.Y)<SystemParameters.MinimumVerticalDragDistance)return;
            pressStart=null;ReleaseMouseCapture();try {DragMove();SavePosition();}catch(InvalidOperationException){}e.Handled=true;
        };
        PreviewMouseLeftButtonUp+=(_,e)=>{
            if(pressStart==null)return;pressStart=null;ReleaseMouseCapture();if(compact)SetCompact(false);e.Handled=true;
        };
        LostMouseCapture+=(_,_)=>pressStart=null;
        Loaded+=(_,_)=>{var area=SystemParameters.WorkArea;Left=prefs.FloatingLeft<0?area.Right-Width-24:Math.Clamp(prefs.FloatingLeft,area.Left,Math.Max(area.Left,area.Right-Width));Top=Math.Clamp(prefs.FloatingTop,area.Top,Math.Max(area.Top,area.Bottom-Height));};
    }
    private void RevealCollapse(bool show){show=show&&!compact&&!transitioning;collapse.IsHitTestVisible=show;collapse.BeginAnimation(OpacityProperty,new DoubleAnimation(show?1:0,TimeSpan.FromMilliseconds(100)));}
    public void SetCompact(bool value)
    {
        if(transitioning||compact==value)return;transitioning=true;compact=value;RevealCollapse(false);
        var targetWidth=value?80:430;var area=SystemParameters.WorkArea;var targetLeft=Math.Clamp(Left+ActualWidth-targetWidth,area.Left,Math.Max(area.Left,area.Right-targetWidth));
        bubble.Visibility=Visibility.Visible;controls.IsHitTestVisible=false;
        var duration=TimeSpan.FromMilliseconds(200);var easing=new CubicEase {EasingMode=EasingMode.EaseInOut};
        controls.BeginAnimation(OpacityProperty,new DoubleAnimation(value?0:1,duration){EasingFunction=easing});
        bubble.BeginAnimation(OpacityProperty,new DoubleAnimation(value?1:0,duration){EasingFunction=easing});
        var width=new DoubleAnimation(targetWidth,duration){EasingFunction=easing};
        width.Completed+=(_,_)=>{Width=targetWidth;Left=targetLeft;BeginAnimation(WidthProperty,null);BeginAnimation(LeftProperty,null);controls.Visibility=value?Visibility.Hidden:Visibility.Visible;controls.IsHitTestVisible=!value;bubble.Visibility=value?Visibility.Visible:Visibility.Hidden;transitioning=false;RevealCollapse(IsMouseOver);SavePosition();};
        controls.Visibility=Visibility.Visible;BeginAnimation(LeftProperty,new DoubleAnimation(targetLeft,duration){EasingFunction=easing});BeginAnimation(WidthProperty,width);
    }
    private void SavePosition(){prefs.FloatingLeft=Left;prefs.FloatingTop=Top;prefs.Save();}
    private static Button? FindButton(DependencyObject? element){while(element!=null){if(element is Button b)return b;element=VisualTreeHelper.GetParent(element);}return null;}
    public void Update(double seconds,float level,bool paused)
    {
        time.Text=TimeSpan.FromSeconds(seconds).ToString("hh\\:mm\\:ss");if(!paused)meter.Push(level);
        compactDot.Fill=(Brush)FindResource(paused?"TextMuted":"RecordingRed");ghost.Animate(!paused);if(previousPaused!=paused){pause.Content=UiIcons.Image(paused?"play":"pause",18);previousPaused=paused;}ToolTip=paused?"已暂停":"录音中";
    }
}
