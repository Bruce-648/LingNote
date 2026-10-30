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
    public ActionPopup(FrameworkElement? anchor,bool atPointer=false,double width=184)
    {
        popup=new Popup {PlacementTarget=anchor,Placement=atPointer?PlacementMode.MousePoint:PlacementMode.Bottom,StaysOpen=false,AllowsTransparency=true,PopupAnimation=PopupAnimation.None};
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
    public void Open()=>popup.IsOpen=true;
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
    private TextPrompt(string title,string value,bool allowEmpty,string[]? choices)
    {
        Width=400;SizeToContent=SizeToContent.Height;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel {Margin=new Thickness(24)};panel.Children.Add(new TextBlock {Text=title,FontSize=20,FontWeight=FontWeights.SemiBold});
        input=new TextBox {Text=value,Margin=new Thickness(0,20,0,14),Padding=new Thickness(10)};panel.Children.Add(input);
        if(choices is {Length:>0}) {var topics=new WrapPanel();foreach(var choice in choices){var pick=new Button {Content=choice,Padding=new Thickness(9,6,9,6),FontSize=12};pick.Click+=(_,_)=>input.Text=choice;topics.Children.Add(pick);}panel.Children.Add(new ScrollViewer {Content=topics,MaxHeight=130,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,0,0,10)});}
        if(allowEmpty)panel.Children.Add(new TextBlock {Text="选择已有主题，或输入新主题；留空移至未分区",FontSize=11,Foreground=Brushes.Gray,Margin=new Thickness(0,0,0,14)});
        var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};var cancel=new Button {Content="取消",IsCancel=true};var save=new Button {Content=title=="创建主题"?"创建主题":title=="新建分区"?"创建分区":"保存",IsDefault=true,Style=(Style)Application.Current.FindResource("Primary")};cancel.Click+=(_,_)=>DialogResult=false;save.Click+=(_,_)=>{if(!allowEmpty&&string.IsNullOrWhiteSpace(input.Text))return;result=input.Text.Trim();DialogResult=true;};buttons.Children.Add(cancel);buttons.Children.Add(save);panel.Children.Add(buttons);
        Content=new Border {Background=Brushes.White,CornerRadius=new CornerRadius(14),BorderBrush=Brushes.LightGray,BorderThickness=new Thickness(1),Child=panel};Loaded+=(_,_)=>input.Focus();
    }
    public static string? Ask(Window owner,string title,string value,bool allowEmpty=false,string[]? choices=null) {var dialog=new TextPrompt(title,value,allowEmpty,choices){Owner=owner};return dialog.ShowDialog()==true?dialog.result:null;}
}
