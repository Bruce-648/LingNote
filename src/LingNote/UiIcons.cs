using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace LingNote;
public static class UiIcons
{
    public static Image Image(string name,int size=18,bool white=false)
    {
        var path=name switch {
            "eye"=>"M2,12 C7,3 17,3 22,12 C17,21 7,21 2,12 Z M15,12 A3,3 0 1 1 9,12 A3,3 0 1 1 15,12",
            "copy"=>"M8,8 L20,8 20,21 8,21 Z M5,16 L3,16 3,3 15,3 15,5",
            "export"=>"M12,16 L12,2 M7,7 L12,2 17,7 M4,12 L4,21 20,21 20,12",
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
            "folder"=>"M3,9 L3,6 Q3,4 5,4 L10,4 12,7 19,7 Q21,7 21,9 L21,18 Q21,20 19,20 L5,20 Q3,20 3,18 Z M3,9 L21,9",
            "folder-open"=>"M3,19 L3,6 Q3,4 5,4 L10,4 12,7 19,7 Q21,7 21,9 L21,11 M3,20 L6.5,12.5 Q7,11 8.5,11 L22,11 18.5,19 Q18,20 17,20 Z",
            "restore"=>"M5,10 L5,4 M5,10 L11,10 M5,10 C9,2 20,5 20,13 C20,21 9,23 5,17",
            "open"=>"M14,4 L21,4 21,11 M21,4 L11,14 M10,5 L4,5 4,20 19,20 19,14",
            "close"=>"M6,6 L18,18 M18,6 L6,18",
            "settings"=>"",
            _=>"M5,12 L19,12"
        };
        var brush=white?Brushes.White:new SolidColorBrush(Color.FromRgb(82,82,82));
        var pen=new Pen(brush,name is "folder" or "folder-open"?1.8:1.7) {StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
        var group=new DrawingGroup();group.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(0,0,24,24))));
        if(name=="settings") {
            // Native outlined gear: consistent stroke and no raster downsampling.
            var gear=new StreamGeometry();using(var context=gear.Open()) {
                bool first=true;
                for(int tooth=0;tooth<8;tooth++)foreach(var point in new[]{(-22.5,7.8),(-13d,7.8),(-10d,10d),(10d,10d),(13d,7.8),(22.5,7.8)}) {
                    double angle=(tooth*45+point.Item1)*Math.PI/180;
                    var position=new Point(12+Math.Cos(angle)*point.Item2,12+Math.Sin(angle)*point.Item2);
                    if(first){context.BeginFigure(position,false,true);first=false;}else context.LineTo(position,true,false);
                }
            }
            gear.Freeze();group.Children.Add(new GeometryDrawing(null,pen,gear));group.Children.Add(new GeometryDrawing(null,pen,new EllipseGeometry(new Point(12,12),3.1,3.1)));
        }
        else if(name=="more") {foreach(var x in new[]{6d,12d,18d})group.Children.Add(new GeometryDrawing(brush,null,new EllipseGeometry(new Point(x,12),1.3,1.3)));}
        else group.Children.Add(new GeometryDrawing(name is "play" or "stop"?brush:null,pen,Geometry.Parse(path)));group.Freeze();
        return new Image {Source=new DrawingImage(group),Width=size,Height=size,VerticalAlignment=VerticalAlignment.Center};
    }
    public static object TopicFolder(string text,bool expanded)
    {
        var panel=new StackPanel {Orientation=Orientation.Horizontal};
        var folder=Image(expanded?"folder-open":"folder",16);folder.Margin=new Thickness(0,0,8,0);panel.Children.Add(folder);
        panel.Children.Add(new TextBlock {Text=text,VerticalAlignment=VerticalAlignment.Center});return panel;
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
