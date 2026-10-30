using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LingNote;
public sealed class UpdatePanel : StackPanel
{
    private readonly TextBlock status=new() {TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Gray,Margin=new Thickness(0,10,0,0),FontSize=12};
    private readonly TextBox notes=new() {IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MaxHeight=140,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Visibility=Visibility.Collapsed,Margin=new Thickness(0,12,0,0)};
    private readonly Button check=new() {Content="检查更新",Margin=new Thickness(0,12,8,0)};
    private readonly Button action=new() {Content="下载更新",Visibility=Visibility.Collapsed,Margin=new Thickness(0,12,8,0)};
    private readonly Button cancel=new() {Content="取消下载",Visibility=Visibility.Collapsed,Margin=new Thickness(0,12,0,0)};
    private CancellationTokenSource? cancellation;
    private AppRelease? release;
    private string? installer;
    public bool IsDownloading {get;private set;}
    public UpdatePanel(Action<string,string> install)
    {
        Children.Add(new TextBlock {Text="应用更新",FontSize=16,FontWeight=FontWeights.SemiBold});
        Children.Add(new TextBlock {Text="当前版本 "+UpdateService.DisplayVersion,Foreground=Brushes.Gray,Margin=new Thickness(0,12,0,0),FontSize=12});
        var options=UpdatePreferences.Load();var auto=new CheckBox {Content="启动时自动检查更新",IsChecked=options.AutoCheck,Margin=new Thickness(0,12,0,0)};Children.Add(auto);
        auto.Checked+=(_,_)=>{options.AutoCheck=true;options.Save();};auto.Unchecked+=(_,_)=>{options.AutoCheck=false;options.Save();};
        var buttons=new WrapPanel();buttons.Children.Add(check);buttons.Children.Add(action);buttons.Children.Add(cancel);Children.Add(buttons);Children.Add(status);Children.Add(notes);
        check.Click+=async(_,_)=>{
            check.IsEnabled=false;action.Visibility=Visibility.Collapsed;notes.Visibility=Visibility.Collapsed;installer=null;release=null;status.Text="正在检查 GitHub 上的新版本…";cancellation=new();
            try {using var service=new UpdateService();release=await service.Latest(cancellation.Token);options.LastCheck=DateTimeOffset.UtcNow;options.Save();
                if(release==null)status.Text="暂未找到正式发布版本，请稍后再试。";
                else if(release.Version<=UpdateService.CurrentVersion)status.Text="当前已是最新版本。";
                else {status.Text=$"发现新版本 {release.Version.ToString(3)} · {release.Size/1048576d:F1} MB";notes.Text=string.IsNullOrWhiteSpace(release.Notes)?"此版本暂无更新说明。":release.Notes;notes.Visibility=Visibility.Visible;action.Content="下载更新";action.Visibility=Visibility.Visible;}
            }catch(OperationCanceledException){status.Text="检查超时或已取消，请重试。";}catch(System.Net.Http.HttpRequestException){status.Text="检查失败：无法连接 GitHub，请检查网络或代理后重试。";}catch(Exception ex){status.Text="检查失败："+ex.Message;}finally{check.IsEnabled=true;cancellation.Dispose();cancellation=null;}
        };
        action.Click+=async(_,_)=>{
            if(release==null)return;
            if(installer!=null){try{action.IsEnabled=false;await UpdateService.Verify(installer,release.Sha256,CancellationToken.None);install(installer,release.Sha256);}catch(Exception ex){status.Text=ex.Message;}finally{action.IsEnabled=true;}return;}
            IsDownloading=true;check.IsEnabled=action.IsEnabled=false;cancel.Visibility=Visibility.Visible;cancellation=new();
            try{using var service=new UpdateService();installer=await service.Download(release,Path.Combine(Paths.Root,"updates"),new Progress<string>(text=>status.Text=text),cancellation.Token);action.Content="安装并重启";}
            catch(OperationCanceledException){status.Text="下载已取消，可以重试。";}catch(Exception ex){status.Text="下载失败："+ex.Message;}
            finally{IsDownloading=false;check.IsEnabled=action.IsEnabled=true;cancel.Visibility=Visibility.Collapsed;cancellation.Dispose();cancellation=null;}
        };
        cancel.Click+=(_,_)=>Cancel();Unloaded+=(_,_)=>Cancel();
    }
    public void Cancel()=>cancellation?.Cancel();
}
