using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace LingNote;
public static class UiIcons
{
    public static Image Image(string name,int size=18,bool white=false)
    {
        var path=name switch {
            "minimize"=>"M5,12 L19,12",
            "maximize"=>"M5,5 L19,5 19,19 5,19 Z",
            "sidebar"=>"M3,4 L21,4 21,20 3,20 Z M9,4 L9,20",
            "chevron"=>"M6,9 L12,15 18,9",
            "plus"=>"M12,5 L12,19 M5,12 L19,12",
            "play"=>"M8,5 L19,12 8,19 Z",
            "pause"=>"M8,5 L8,19 M16,5 L16,19",
            "stop"=>"M6,6 L18,6 18,18 6,18 Z",
            "trash"=>"M4,7 L20,7 M9,7 L9,4 15,4 15,7 M6,7 L7,20 17,20 18,7 M10,10 L10,17 M14,10 L14,17",
            "edit"=>"M4,20 L5,14 16,3 21,8 10,19 Z M14,5 L19,10",
            "folder"=>"M3,6 L10,6 12,9 21,9 21,20 3,20 Z",
            "restore"=>"M5,10 L5,4 M5,10 L11,10 M5,10 C9,2 20,5 20,13 C20,21 9,23 5,17",
            "open"=>"M14,4 L21,4 21,11 M21,4 L11,14 M10,5 L4,5 4,20 19,20 19,14",
            "close"=>"M6,6 L18,18 M18,6 L6,18",
            "settings"=>"M4,7 L20,7 M4,17 L20,17 M9,4 L9,10 M15,14 L15,20",
            _=>"M5,12 L19,12"
        };
        var brush=white?Brushes.White:new SolidColorBrush(Color.FromRgb(82,82,82));
        var pen=new Pen(brush,1.7) {StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
        var group=new DrawingGroup();group.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(0,0,24,24))));
        if(name=="more") {foreach(var x in new[]{6d,12d,18d})group.Children.Add(new GeometryDrawing(brush,null,new EllipseGeometry(new Point(x,12),1.3,1.3)));}
        else group.Children.Add(new GeometryDrawing(name is "play" or "stop"?brush:null,pen,Geometry.Parse(path)));group.Freeze();
        return new Image {Source=new DrawingImage(group),Width=size,Height=size,VerticalAlignment=VerticalAlignment.Center};
    }
    public static object Section(string text,bool collapsed)
    {
        var panel=new StackPanel {Orientation=Orientation.Horizontal};panel.Children.Add(new TextBlock {Text=text,FontSize=13,FontWeight=FontWeights.Medium,VerticalAlignment=VerticalAlignment.Center});var arrow=Image("chevron",13);arrow.Margin=new Thickness(6,0,0,0);if(collapsed)arrow.LayoutTransform=new RotateTransform(-90);panel.Children.Add(arrow);return panel;
    }
    public static object Dropdown(string text)
    {
        var panel=new StackPanel {Orientation=Orientation.Horizontal};panel.Children.Add(new TextBlock {Text=text,FontSize=12,VerticalAlignment=VerticalAlignment.Center});var arrow=Image("chevron",14);arrow.Margin=new Thickness(6,0,0,0);panel.Children.Add(arrow);return panel;
    }
    public static object Label(string name,string text,bool white=false)
    {
        var panel=new StackPanel {Orientation=Orientation.Horizontal};panel.Children.Add(Image(name,18,white));panel.Children.Add(new TextBlock {Text=text,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(7,0,0,0)});return panel;
    }
}
