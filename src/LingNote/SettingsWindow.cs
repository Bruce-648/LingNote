using System.Windows;
using System.Windows.Controls;
using System.IO;

namespace LingNote;
public sealed class SettingsWindow : Window
{
    private readonly Preferences prefs;
    private readonly TextBox address=new(),model=new(),chunk=new();
    private readonly PasswordBox key=new() {Padding=new Thickness(10)};
    private readonly CheckBox stream=new() {Content="流式生成笔记（若服务不兼容可关闭）",Margin=new Thickness(0,12,0,12)};
    private readonly TextBlock status=new() {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,10)};
    private readonly Button download=new() {Content="下载模型（约 466 MB）"};
    private CancellationTokenSource? downloadCts;
    public string? PendingInstaller {get;private set;}
    public string? PendingInstallerHash {get;private set;}
    public SettingsWindow(Preferences preferences)
    {
        prefs=preferences; Title="LingNote · 设置"; Width=480; Height=650;MinHeight=400;ResizeMode=ResizeMode.CanResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new StackPanel {Margin=new Thickness(24)};Content=new ScrollViewer {Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        root.Children.Add(new TextBlock {Text="本地语音模型",FontSize=16,FontWeight=FontWeights.SemiBold});
        var modelStatus=new TextBlock {Visibility=Visibility.Collapsed,Text="模型已就绪，可离线转写",TextWrapping=TextWrapping.Wrap,Foreground=System.Windows.Media.Brushes.Gray,Margin=new Thickness(0,12,0,0)};root.Children.Add(modelStatus);
        var models=new WrapPanel {Margin=new Thickness(0,12,0,0)};var import=new Button {Content="导入模型",Margin=new Thickness(0,0,8,0)};var cancel=new Button {Content="取消下载",Margin=new Thickness(0),Visibility=Visibility.Collapsed};download.Margin=new Thickness(0,0,8,0);download.Content="下载模型";download.Style=(Style)FindResource("Primary");download.Visibility=Transcriber.ModelAvailable?Visibility.Collapsed:Visibility.Visible;models.Children.Add(download);models.Children.Add(import);models.Children.Add(cancel);root.Children.Add(models);
        var progressStyle=new Style(typeof(TextBlock));var emptyProgress=new Trigger {Property=TextBlock.TextProperty,Value=""};emptyProgress.Setters.Add(new Setter(VisibilityProperty,Visibility.Collapsed));progressStyle.Triggers.Add(emptyProgress);
        var modelProgress=new TextBlock {Style=progressStyle,TextWrapping=TextWrapping.Wrap,FontSize=12,Foreground=System.Windows.Media.Brushes.Gray,Margin=new Thickness(0,12,0,0)};root.Children.Add(modelProgress);
        var progressBar=new ProgressBar {Height=3,IsIndeterminate=true,Visibility=Visibility.Collapsed,Margin=new Thickness(0,8,0,0)};root.Children.Add(progressBar);
        root.Children.Add(new System.Windows.Controls.Separator {Margin=new Thickness(0,12,0,12)});
        root.Children.Add(new TextBlock {Text="AI 设置",FontSize=21,FontWeight=FontWeights.SemiBold});
        Label(root,"接口地址",address);address.Text=prefs.BaseUrl;
        Label(root,"模型",model);model.Text=prefs.Model;
        Label(root,"API Key",key);try {key.Password=prefs.GetKey();}catch {status.Text="已保存的密钥无法读取，请重新填写。";}
        var buttons=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,16,0,0)};var save=new Button {Content="保存",Style=(Style)FindResource("Primary")};var test=new Button {Content="测试连接"};buttons.Children.Add(save);buttons.Children.Add(test);root.Children.Add(buttons);root.Children.Add(status);
        var advanced=new StackPanel {Margin=new Thickness(0,12,0,0)};var expander=new Expander {Header="高级设置",Content=advanced,Margin=new Thickness(0,8,0,0)};root.Children.Add(expander);
        stream.Content="流式生成";stream.IsChecked=prefs.Stream;advanced.Children.Add(stream);Label(advanced,"每段整理字数",chunk);chunk.Text=prefs.ChunkCharacters.ToString();
        var folder=new Button {Content="打开数据文件夹",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(4,12,4,4)};folder.Click+=(_,_)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Paths.Root){UseShellExecute=true});advanced.Children.Add(folder);
        bool dirty=false;address.TextChanged+=(_,_)=>dirty=true;model.TextChanged+=(_,_)=>dirty=true;key.PasswordChanged+=(_,_)=>dirty=true;chunk.TextChanged+=(_,_)=>dirty=true;stream.Checked+=(_,_)=>dirty=true;stream.Unchecked+=(_,_)=>dirty=true;
        save.Click+=(_,_)=> { try {Save(); dirty=false; status.Text="已保存，下次打开会自动读取。";} catch(Exception ex) {status.Text=ex.Message;} };
        test.Click+=async(_,_)=> { try {Save(); dirty=false; test.IsEnabled=false; status.Text="正在测试连接…"; var text=await new NoteService().Request(prefs,"只回复：连接成功。","测试",null,CancellationToken.None); status.Text="连接成功："+text[..Math.Min(100,text.Length)];} catch(Exception ex) {status.Text=ex.Message;} finally {test.IsEnabled=true;} };
        download.Click+=async(_,_)=> { modelStatus.Visibility=Visibility.Collapsed;downloadCts=new(); cancel.Visibility=progressBar.Visibility=Visibility.Visible; download.IsEnabled=false; import.IsEnabled=false; try {await Transcriber.Download(new Progress<string>(s=>modelProgress.Text=s),downloadCts.Token);ModelSucceeded();UpdateModelState();} catch(OperationCanceledException) {modelProgress.Text="已取消下载，可重新下载。";} catch(Exception ex) {modelProgress.Text="下载失败："+ex.Message+" 可以重试，或导入已下载的模型文件。";} finally {download.IsEnabled=true; import.IsEnabled=true; downloadCts.Dispose(); downloadCts=null;cancel.Visibility=progressBar.Visibility=Visibility.Collapsed;UpdateModelState();} };
        cancel.Click+=(_,_)=>downloadCts?.Cancel();
        import.Click+=async(_,_)=> { modelStatus.Visibility=Visibility.Collapsed;var dialog=new Microsoft.Win32.OpenFileDialog {Filter="Whisper GGML 模型|ggml-small.bin;*.bin"}; if(dialog.ShowDialog(this)!=true) return; import.IsEnabled=download.IsEnabled=false;try {await using(var input=File.OpenRead(dialog.FileName)) {if(!Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(input)).Equals(Transcriber.SmallSha256,StringComparison.OrdinalIgnoreCase))throw new IOException("请选择完整的官方 Whisper small 多语言 GGML 模型。");} Directory.CreateDirectory(Paths.Models); if(!Path.GetFullPath(dialog.FileName).Equals(Path.GetFullPath(Paths.Model),StringComparison.OrdinalIgnoreCase)) File.Copy(dialog.FileName,Paths.Model,true);ModelSucceeded();UpdateModelState();} catch(Exception ex) {modelProgress.Text=ex.Message;} finally {import.IsEnabled=download.IsEnabled=true;} };
        void ModelSucceeded(){modelProgress.Text="";modelStatus.Visibility=Visibility.Visible;}
        void UpdateModelState(){var ready=Transcriber.ModelAvailable;download.Visibility=ready?Visibility.Collapsed:Visibility.Visible;}
        root.Children.Add(new Separator {Margin=new Thickness(0,20,0,16)});
        var updates=new UpdatePanel((file,hash)=>{Save();dirty=false;PendingInstaller=file;PendingInstallerHash=hash;Close();});root.Children.Add(updates);
        Closing+=(_,e)=> { if(updates.IsDownloading){updates.Cancel();e.Cancel=true;status.Text="正在取消更新下载，请稍后关闭。";return;} if(downloadCts==null && dirty) {try {Save();dirty=false;}catch(Exception ex){e.Cancel=true;status.Text=ex.Message;}} if(downloadCts!=null) {downloadCts.Cancel(); e.Cancel=true; status.Text="正在取消下载，请稍后关闭。";} };
    }
    private static void Label(Panel root,string label,Control field) {root.Children.Add(new TextBlock {Text=label,Margin=new Thickness(0,15,0,6),FontSize=12}); root.Children.Add(field);}
    private void Save() { _=NoteService.Endpoint(address.Text); if(!int.TryParse(chunk.Text,out var length) || length<2000 || length>20000) throw new ArgumentException("文字长度应为 2000 到 20000。"); prefs.BaseUrl=address.Text.Trim(); prefs.Model=model.Text.Trim(); if(!string.IsNullOrWhiteSpace(key.Password))prefs.SetKey(key.Password); prefs.Stream=stream.IsChecked==true; prefs.ChunkCharacters=length; prefs.SaveApi(); }
    private void UpdateModel()=>status.Text=Transcriber.ModelAvailable?"本地模型已就绪，可以离线转写。":"尚未安装模型。你可以先录音，安装模型后再整理。";
}
