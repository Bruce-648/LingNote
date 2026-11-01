using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Diagnostics;
namespace LingNote;
public sealed class ActionPopup
{
    private readonly Popup popup;
    private readonly StackPanel panel=new();
    private bool closedOnAnchorPress;
    public bool IsOpen=>popup.IsOpen;
    public ActionPopup(FrameworkElement? anchor,bool atPointer=false,double width=184)
    {
        popup=new Popup {PlacementTarget=anchor,Placement=atPointer?PlacementMode.MousePoint:PlacementMode.Bottom,StaysOpen=false,AllowsTransparency=true,PopupAnimation=PopupAnimation.None};
        popup.Closed+=(_,_)=>{
            if(anchor==null||System.Windows.Input.Mouse.LeftButton!=System.Windows.Input.MouseButtonState.Pressed)return;
            var point=System.Windows.Input.Mouse.GetPosition(anchor);
            closedOnAnchorPress=new Rect(0,0,anchor.ActualWidth,anchor.ActualHeight).Contains(point);
        };
        popup.Child=new Border {Width=width,Margin=new Thickness(5),Padding=new Thickness(4),Background=Brushes.White,CornerRadius=new CornerRadius(10),BorderBrush=new SolidColorBrush(Color.FromRgb(235,235,235)),BorderThickness=new Thickness(1),Effect=new DropShadowEffect {BlurRadius=10,ShadowDepth=2,Opacity=.13},Child=panel};
    }
    public void Add(string text,string icon,Action action,bool enabled=true)
    {
        var row=new StackPanel {Orientation=Orientation.Horizontal}; row.Children.Add(UiIcons.Image(icon,16,false));row.Children.Add(new TextBlock {Text=text,Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center});
        var button=new Button {Content=row,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(9,7,9,7),Margin=new Thickness(0,2,0,2),Background=Brushes.White,Foreground=new SolidColorBrush(Color.FromRgb(23,23,23)),FontFamily=(FontFamily)Application.Current.FindResource("AppFont"),FontSize=12,IsEnabled=enabled};
        button.MouseEnter+=(_,_)=>button.Background=new SolidColorBrush(Color.FromRgb(245,245,245)); button.MouseLeave+=(_,_)=>button.Background=Brushes.White;
        button.Click+=(_,_)=> {popup.IsOpen=false;Dismiss?.Invoke();action();}; panel.Children.Add(button);
    }
    public void Separator()=>panel.Children.Add(new Border {Height=1,Background=new SolidColorBrush(Color.FromRgb(235,235,235)),Margin=new Thickness(5,3,5,3)});
    public Action? Dismiss {get;set;}
    public UIElement DetachContent() {var content=popup.Child;popup.Child=null;return content;}
    public void Open(){closedOnAnchorPress=false;popup.IsOpen=true;}
    public void Close()=>popup.IsOpen=false;
    public void Toggle()
    {
        // Popup dismisses on the anchor's mouse-down, before Button.Click.
        // Consume that click instead of immediately reopening the same menu.
        if(closedOnAnchorPress){closedOnAnchorPress=false;return;}
        popup.IsOpen=!popup.IsOpen;
    }
    public void AddShortcut(string text,string icon,string shortcut,Action action)
    {
        var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(23)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        row.Children.Add(UiIcons.Image(icon,18,false,new SolidColorBrush(Color.FromRgb(48,55,66))));
        var label=new TextBlock {Text=text,FontSize=13,FontWeight=FontWeights.Medium,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(label,1);row.Children.Add(label);
        var keys=new TextBlock {Text=shortcut,FontSize=12,Foreground=new SolidColorBrush(Color.FromRgb(116,125,141)),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,0,0,0)};Grid.SetColumn(keys,2);row.Children.Add(keys);
        var button=new Button {Content=row,HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(6,7,6,7),Margin=new Thickness(0,1,0,1),Background=Brushes.Transparent};
        button.MouseEnter+=(_,_)=>button.Background=new SolidColorBrush(Color.FromRgb(244,246,250));button.MouseLeave+=(_,_)=>button.Background=Brushes.Transparent;
        button.Click+=(_,_)=>{popup.IsOpen=false;action();};panel.Children.Add(button);
    }
}
public sealed class ConfirmWindow : Window
{
    private ConfirmWindow(string title,string message,string action)
    {
        Width=400;SizeToContent=SizeToContent.Height;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;WindowStartupLocation=WindowStartupLocation.CenterOwner;ShowInTaskbar=false;
        var panel=new StackPanel {Margin=new Thickness(26)};panel.Children.Add(new TextBlock {Text=title,FontSize=20,FontWeight=FontWeights.SemiBold});panel.Children.Add(new TextBlock {Text=message,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,22),Foreground=Brushes.Gray});
        var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};var cancel=new Button {Content="取消",Background=new SolidColorBrush(Color.FromRgb(245,245,245)),Foreground=Brushes.Black,IsCancel=true};var ok=new Button {Content=action,Foreground=Brushes.White,Background=new SolidColorBrush(Color.FromRgb(220,38,38))};cancel.Click+=(_,_)=>DialogResult=false;ok.Click+=(_,_)=>DialogResult=true;buttons.Children.Add(cancel);buttons.Children.Add(ok);panel.Children.Add(buttons);
        Content=new Border {CornerRadius=new CornerRadius(14),Background=Brushes.White,BorderBrush=Brushes.LightGray,BorderThickness=new Thickness(1),Child=panel};
    }
    public static bool Ask(Window owner,string title,string text,string action="删除")=>new ConfirmWindow(title,text,action) {Owner=owner}.ShowDialog()==true;
}
public sealed class PlaybackWindow : Window
{
    public PlaybackWindow(Course course)
    {
        Title="录音片段";Width=400;Height=440;WindowStartupLocation=WindowStartupLocation.CenterOwner;var panel=new StackPanel {Margin=new Thickness(20)};
        foreach(var part in course.Parts) {var button=new Button {Content=TimeSpan.FromSeconds(part.Offset).ToString("hh\\:mm\\:ss")+"  ·  "+part.Duration.ToString("F0")+" 秒"};button.Click+=(_,_)=>Process.Start(new ProcessStartInfo(part.File){UseShellExecute=true});panel.Children.Add(button);}
        Content=new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    }
}

public sealed class TextPrompt : Window
{
    private readonly TextBox input;
    private string? result;
    public string? SelectedFolder {get;private set;}
    private TextPrompt(string title,string value,bool allowEmpty,string[]? choices)
    {
        Width=400;SizeToContent=SizeToContent.Height;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel {Margin=new Thickness(24)};panel.Children.Add(new TextBlock {Text=title,FontSize=20,FontWeight=FontWeights.SemiBold});
        input=new TextBox {Text=value,Margin=new Thickness(0,20,0,14),Padding=new Thickness(10)};panel.Children.Add(input);
        if(title=="创建主题") {
            Width=520;var header=new DockPanel();var close=new Button {Content=UiIcons.Image("close",14),Width=28,Height=28,Padding=new Thickness(5),HorizontalAlignment=HorizontalAlignment.Right,Background=Brushes.Transparent,IsCancel=true};close.Click+=(_,_)=>DialogResult=false;DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);var heading=(UIElement)panel.Children[0];panel.Children.RemoveAt(0);header.Children.Add(heading);panel.Children.Insert(0,header);
            panel.Children.Add(new TextBlock {Text="截屏保存文件夹（可选）",Margin=new Thickness(0,0,0,8)});
            var description=new TextBlock {Text="在此电脑上选择文件夹",TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Gray,Margin=new Thickness(8,14,8,10)};
            var select=new Button {Content=UiIcons.Label("folder","添加"),HorizontalAlignment=HorizontalAlignment.Center,Padding=new Thickness(10,4,10,4)};select.Click+=(_,_)=>{var picker=new Microsoft.Win32.OpenFolderDialog {Title="选择此主题的截屏保存文件夹"};if(picker.ShowDialog(this)==true){SelectedFolder=picker.FolderName;description.Text=SelectedFolder;select.Content="更改文件夹";}};
            var box=new StackPanel();box.Children.Add(description);box.Children.Add(select);panel.Children.Add(new Border {Child=box,CornerRadius=new CornerRadius(10),BorderBrush=new SolidColorBrush(Color.FromRgb(235,235,235)),BorderThickness=new Thickness(1),Padding=new Thickness(12),Margin=new Thickness(0,0,0,20)});
        }
        if(choices is {Length:>0}) {var topics=new WrapPanel();foreach(var choice in choices){var pick=new Button {Content=choice,Padding=new Thickness(9,6,9,6),FontSize=12};pick.Click+=(_,_)=>input.Text=choice;topics.Children.Add(pick);}panel.Children.Add(new ScrollViewer {Content=topics,MaxHeight=130,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,0,0,10)});}
        if(allowEmpty)panel.Children.Add(new TextBlock {Text="选择已有主题，或输入新主题；留空移至未分区",FontSize=11,Foreground=Brushes.Gray,Margin=new Thickness(0,0,0,14)});
        var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};var cancel=new Button {Content="取消",IsCancel=true};var save=new Button {Content=title=="创建主题"?"创建主题":title=="新建分区"?"创建分区":"保存",IsDefault=true,Style=(Style)Application.Current.FindResource("Primary")};cancel.Click+=(_,_)=>DialogResult=false;save.Click+=(_,_)=>{if(!allowEmpty&&string.IsNullOrWhiteSpace(input.Text))return;result=input.Text.Trim();DialogResult=true;};buttons.Children.Add(cancel);buttons.Children.Add(save);panel.Children.Add(buttons);
        Content=new Border {Background=Brushes.White,CornerRadius=new CornerRadius(14),BorderBrush=Brushes.LightGray,BorderThickness=new Thickness(1),Child=panel};Loaded+=(_,_)=>input.Focus();
    }
    public static (string Name,string? Folder)? CreateTopic(Window owner){var dialog=new TextPrompt("创建主题","",false,null){Owner=owner};return dialog.ShowDialog()==true?(dialog.result!,dialog.SelectedFolder):null;}
    public static string? Ask(Window owner,string title,string value,bool allowEmpty=false,string[]? choices=null) {var dialog=new TextPrompt(title,value,allowEmpty,choices){Owner=owner};return dialog.ShowDialog()==true?dialog.result:null;}
}
