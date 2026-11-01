using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace LingNote;
public static class UiIcons
{
    public static Image Image(string name,int size=18,bool white=false,Brush? tint=null)
    {
        var path=name switch {
            "checks"=>"M3,6 L5,8 8,4 M11,6 L21,6 M3,14 L5,16 8,12 M11,14 L21,14 M11,21 L21,21",
            "monitor"=>"M5,3 L19,3 Q21,3 21,5 L21,15 Q21,17 19,17 L5,17 Q3,17 3,15 L3,5 Q3,3 5,3 Z M12,17 L12,21 M8,21 L16,21",
            "region"=>"M8,3 L5,3 Q3,3 3,5 L3,8 M11,3 L13,3 M16,3 L19,3 Q21,3 21,5 L21,8 M21,11 L21,13 M21,16 L21,19 Q21,21 19,21 L16,21 M13,21 L11,21 M8,21 L5,21 Q3,21 3,19 L3,16 M3,13 L3,11",
            "search"=>"M16,10 A6,6 0 1 1 4,10 A6,6 0 1 1 16,10 M15,15 L21,21",
            "back"=>"M14,5 L7,12 14,19 M7,12 L21,12",
            "capture"=>"M8,3 L3,3 3,8 M16,3 L21,3 21,8 M3,16 L3,21 8,21 M16,21 L21,21 21,16 M8,12 L11,15 17,9",
            "mic"=>"M9,5 A3,3 0 0 1 15,5 L15,12 A3,3 0 0 1 9,12 Z M5,11 L5,12 A7,7 0 0 0 19,12 L19,11 M12,19 L12,23",
            "link"=>"M9,15 L15,9 M7,14 L5,16 C1,21 7,25 11,21 L14,18 M10,6 L13,3 C18,-1 24,5 20,10 L17,13",
            "database"=>"M3,6 C3,1 21,1 21,6 C21,11 3,11 3,6 Z M3,6 L3,18 C3,23 21,23 21,18 L21,6 M3,12 C3,17 21,17 21,12",
            "keyboard"=>"M3,5 L21,5 21,19 3,19 Z M6,9 L7,9 M11,9 L12,9 M16,9 L17,9 M6,12 L7,12 M11,12 L12,12 M16,12 L17,12 M8,16 L16,16",
            "palette"=>"M21,12 C21,2 6,0 3,9 C0,18 10,23 13,21 C16,19 11,17 15,15 C18,14 21,16 21,12 Z M7,8 L8,8 M12,5 L13,5 M17,8 L18,8",
            "info"=>"M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 M12,11 L12,17 M12,7 L12,8",
            "sliders"=>"M3,6 L8,6 M12,6 L21,6 M3,17 L13,17 M17,17 L21,17 M12,6 A2,2 0 1 1 8,6 A2,2 0 1 1 12,6 M17,17 A2,2 0 1 1 13,17 A2,2 0 1 1 17,17",
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
        var brush=tint??(white?Brushes.White:new SolidColorBrush(Color.FromRgb(82,82,82)));
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
