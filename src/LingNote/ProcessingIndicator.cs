using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace LingNote;
public sealed class ProcessingIndicator : StackPanel
{
    private readonly GhostMascot ghost=new() {Width=28,Height=36,Margin=new Thickness(0,0,7,0)};
    private readonly TextBlock dots=new() {Width=22,VerticalAlignment=VerticalAlignment.Center,Foreground=Brushes.Gray};
    private readonly DispatcherTimer timer=new() {Interval=TimeSpan.FromMilliseconds(380)};
    private int phase;
    public bool IsRunning {get;private set;}
    public ProcessingIndicator()
    {
        Orientation=Orientation.Horizontal;Visibility=Visibility.Collapsed;
        Children.Add(ghost);Children.Add(new TextBlock {Text="正在处理",Foreground=Brushes.Gray,FontSize=12,VerticalAlignment=VerticalAlignment.Center});Children.Add(dots);
        System.Windows.Automation.AutomationProperties.SetName(this,"正在处理");
        timer.Tick+=(_,_)=>{phase=(phase+1)%4;dots.Text=new string('.',phase);};
        IsVisibleChanged+=(_,_)=>UpdateTimer();Unloaded+=(_,_)=>timer.Stop();
    }
    public void SetRunning(bool running)
    {
        if(IsRunning==running)return;IsRunning=running;phase=0;dots.Text="";
        Visibility=running?Visibility.Visible:Visibility.Collapsed;ghost.Animate(running,running);UpdateTimer();
    }
    private void UpdateTimer(){if(IsRunning&&IsVisible)timer.Start();else timer.Stop();}
}
