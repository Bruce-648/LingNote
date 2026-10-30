using System.Windows;
using System.Windows.Media;
namespace LingNote;
public sealed class Waveform : FrameworkElement
{
    private readonly Queue<double> samples=new();
    public void Push(double level) {samples.Enqueue(Math.Clamp(level,0,1));while(samples.Count>60)samples.Dequeue();InvalidateVisual();}
    public void Clear() {samples.Clear();InvalidateVisual();}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);int count=Math.Max(1,(int)(ActualWidth/6));var values=samples.TakeLast(count).ToArray();
        var brush=new SolidColorBrush(Color.FromRgb(115,115,115));
        for(int i=0;i<count;i++) {int n=i-count+values.Length;double v=n<0?0:values[n];double h=Math.Max(3,Math.Sqrt(v)*Math.Max(3,ActualHeight-4));dc.DrawRoundedRectangle(brush,null,new Rect(i*6,(ActualHeight-h)/2,2.5,h),1.25,1.25);}
    }
}
