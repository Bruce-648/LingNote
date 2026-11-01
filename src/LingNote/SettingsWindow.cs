using System.Windows;
using System.Windows.Controls;
using System.IO;

namespace LingNote;
public sealed class SettingsWindow : Window
{
    private readonly Preferences prefs;
    private readonly TextBox address=new(),model=new(),chunk=new();
    private readonly ComboBox thinking=new(),generationMode=new(),thinkingProtocol=new();
    private readonly PasswordBox key=new() {Padding=new Thickness(10)};
    private readonly CheckBox stream=new() {Content="流式生成笔记（若服务不兼容可关闭）",Margin=new Thickness(0,12,0,12)};
    private readonly TextBlock status=new() {FontSize=14,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};
    private readonly Button download=new() {Content="下载模型（约 466 MB）"};
    private CancellationTokenSource? downloadCts;
    public bool CanDetach {get;private set;}
    public string? PendingInstaller {get;private set;}
    public string? PendingInstallerHash {get;private set;}
    public SettingsWindow(Preferences preferences)
    {
        prefs=preferences; Title="LingNote · 设置"; Width=1100; Height=950;MinHeight=400;ResizeMode=ResizeMode.CanResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new StackPanel {Margin=new Thickness(32,24,32,26)};Content=new ScrollViewer {Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        root.Children.Add(new TextBlock {Text="本地语音模型",FontSize=14,FontWeight=FontWeights.SemiBold});
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
        Label(root,"笔记模式",generationMode);
        generationMode.Items.Add("课堂模式（默认，按需精编）");generationMode.Items.Add("精编模式（录音后生成精编笔记）");
        generationMode.SelectedIndex=prefs.GenerationMode=="direct"?1:0;
        foreach(var option in new[]{"自动识别","思考强度（reasoning_effort）","思考开关（enable_thinking）","不发送"})thinkingProtocol.Items.Add(option);
        var protocols=new[]{"auto","effort","toggle","disabled"};
        thinkingProtocol.SelectedIndex=Array.IndexOf(protocols,prefs.ThinkingProtocol);if(thinkingProtocol.SelectedIndex<0)thinkingProtocol.SelectedIndex=0;
        Label(root,"思考模式",thinking);
        bool rebuilding=false;string pendingMode=prefs.ThinkingMode;
        void UpdateThinking()
        {
            rebuilding=true;
            string protocol;try{protocol=NoteService.ResolveThinkingProtocol(address.Text,model.Text,protocols[Math.Clamp(thinkingProtocol.SelectedIndex,0,3)]);}catch(ArgumentException){protocol="disabled";}
            var canDisableGemini=model.Text.Trim().StartsWith("gemini-2.5-",StringComparison.OrdinalIgnoreCase)&&!model.Text.Trim().StartsWith("gemini-2.5-pro",StringComparison.OrdinalIgnoreCase);
            var labels=protocol=="toggle"?new[]{"Off","On"}:protocol=="gemini"?(canDisableGemini?new[]{"Off","Low","Medium","High"}:new[]{"Low","Medium","High"}):protocol is "openai" or "effort"?new[]{"Off","Low","Medium","High","XHigh","Max"}:new[]{"Off","Low","High","Max"};
            var choices=protocol=="toggle"?new[]{"none","high"}:protocol=="gemini"?(canDisableGemini?new[]{"none","low","medium","high"}:new[]{"low","medium","high"}):protocol is "openai" or "effort"?new[]{"none","low","medium","high","xhigh","max"}:new[]{"none","low","high","max"};
            thinking.Items.Clear();foreach(var option in labels)thinking.Items.Add(option);
            thinking.SelectedIndex=Array.IndexOf(choices,protocol=="toggle"&&pendingMode!="none"?"high":pendingMode);if(thinking.SelectedIndex<0)thinking.SelectedIndex=Array.IndexOf(choices,"high");
            thinking.IsEnabled=protocol!="disabled";rebuilding=false;
        }
        UpdateThinking();
        thinking.SelectionChanged+=(_,_)=>{if(!rebuilding&&thinking.SelectedItem is string value)pendingMode=value switch{"Off"=>"none","Low"=>"low","Medium"=>"medium","XHigh"=>"xhigh","Max"=>"max",_=>"high"};};
        thinkingProtocol.SelectionChanged+=(_,_)=>UpdateThinking();address.TextChanged+=(_,_)=>UpdateThinking();model.TextChanged+=(_,_)=>UpdateThinking();
        var buttons=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,16,0,0)};var save=new Button {Content="保存",Style=(Style)FindResource("Primary")};var test=new Button {Content="测试连接"};buttons.Children.Add(save);buttons.Children.Add(test);root.Children.Add(buttons);root.Children.Add(status);status.Style=progressStyle;
        var advanced=new StackPanel {Margin=new Thickness(0,12,0,0)};var expander=new Expander {Header="高级设置",Content=advanced,Margin=new Thickness(0,8,0,0)};root.Children.Add(expander);
        stream.Content="流式生成";stream.IsChecked=prefs.Stream;advanced.Children.Add(stream);Label(advanced,"每段整理字数",chunk);chunk.Text=prefs.ChunkCharacters.ToString();
        var folder=new Button {Content="打开数据文件夹",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(4,12,4,4)};folder.Click+=(_,_)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Paths.Root){UseShellExecute=true});advanced.Children.Add(folder);
        bool dirty=false;address.TextChanged+=(_,_)=>dirty=true;model.TextChanged+=(_,_)=>dirty=true;key.PasswordChanged+=(_,_)=>dirty=true;chunk.TextChanged+=(_,_)=>dirty=true;stream.Checked+=(_,_)=>dirty=true;stream.Unchecked+=(_,_)=>dirty=true;thinking.SelectionChanged+=(_,_)=>{if(!rebuilding)dirty=true;};thinkingProtocol.SelectionChanged+=(_,_)=>dirty=true;generationMode.SelectionChanged+=(_,_)=>dirty=true;
        save.Click+=(_,_)=> { try {Save(); dirty=false; status.Text="已保存，下次打开会自动读取。";} catch(Exception ex) {status.Text=ex.Message;} };
        test.Click+=async(_,_)=> { try {Save(); dirty=false; test.IsEnabled=false; status.Text="正在测试连接…"; var text=await new NoteService().Request(prefs,"只回复：连接成功。","测试",null,CancellationToken.None); status.Text="连接成功："+text[..Math.Min(100,text.Length)];} catch(Exception ex) {status.Text=ex.Message;} finally {test.IsEnabled=true;} };
        download.Click+=async(_,_)=> { modelStatus.Visibility=Visibility.Collapsed;downloadCts=new(); cancel.Visibility=progressBar.Visibility=Visibility.Visible; download.IsEnabled=false; import.IsEnabled=false; try {await Transcriber.Download(new Progress<string>(s=>modelProgress.Text=s),downloadCts.Token);ModelSucceeded();UpdateModelState();} catch(OperationCanceledException) {modelProgress.Text="已取消下载，可重新下载。";} catch(Exception ex) {modelProgress.Text="下载失败："+ex.Message+" 可以重试，或导入已下载的模型文件。";} finally {download.IsEnabled=true; import.IsEnabled=true; downloadCts.Dispose(); downloadCts=null;cancel.Visibility=progressBar.Visibility=Visibility.Collapsed;UpdateModelState();} };
        cancel.Click+=(_,_)=>downloadCts?.Cancel();
        import.Click+=async(_,_)=> { modelStatus.Visibility=Visibility.Collapsed;var dialog=new Microsoft.Win32.OpenFileDialog {Filter="Whisper GGML 模型|ggml-small.bin;*.bin"}; if(dialog.ShowDialog(this)!=true) return; import.IsEnabled=download.IsEnabled=false;try {await using(var input=File.OpenRead(dialog.FileName)) {if(!Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(input)).Equals(Transcriber.SmallSha256,StringComparison.OrdinalIgnoreCase))throw new IOException("请选择完整的官方 Whisper small 多语言 GGML 模型。");} Directory.CreateDirectory(Paths.Models); if(!Path.GetFullPath(dialog.FileName).Equals(Path.GetFullPath(Paths.Model),StringComparison.OrdinalIgnoreCase)) File.Copy(dialog.FileName,Paths.Model,true);ModelSucceeded();UpdateModelState();} catch(Exception ex) {modelProgress.Text=ex.Message;} finally {import.IsEnabled=download.IsEnabled=true;} };
        void ModelSucceeded(){modelProgress.Text="";modelStatus.Visibility=Visibility.Visible;}
        void UpdateModelState(){var ready=Transcriber.ModelAvailable;download.Visibility=ready?Visibility.Collapsed:Visibility.Visible;}
        root.Children.Add(new Separator {Margin=new Thickness(0,20,0,16)});
        var updates=new UpdatePanel((file,hash)=>{Save();dirty=false;PendingInstaller=file;PendingInstallerHash=hash;Close();});root.Children.Add(updates);
        var existing=root.Children.Cast<UIElement>().ToList();
        var aiHeading=existing.OfType<TextBlock>().Single(t=>t.Text=="AI 设置");aiHeading.Text="AI 连接";aiHeading.FontSize=24;
        var modelHeading=existing.OfType<TextBlock>().Single(t=>t.Text=="本地语音模型");modelHeading.FontSize=24;
        var aiStart=existing.IndexOf(aiHeading);var modeLabel=existing.OfType<TextBlock>().Single(t=>t.Text=="笔记模式");var modeStart=existing.IndexOf(modeLabel);
        var thinkingLabel=existing.OfType<TextBlock>().Single(t=>t.Text=="思考模式");
        root.Children.Clear();
        root.Children.Add(new TextBlock {Text="设置",FontSize=32,FontWeight=FontWeights.Bold});
        root.Children.Add(new TextBlock {Text="管理 AI 连接与本地转写。",Foreground=(System.Windows.Media.Brush)FindResource("TextSecondary"),FontSize=17,Margin=new Thickness(0,4,0,28)});
        for(int i=aiStart;i<modeStart;i++)root.Children.Add(existing[i]);
        // The password remains in PasswordBox; the transient reveal field never persists.
        root.Children.Remove(key);
        var keyRow=new Grid {MaxWidth=800,HorizontalAlignment=HorizontalAlignment.Stretch};key.Padding=new Thickness(14,8,52,8);keyRow.Children.Add(key);
        var reveal=new TextBox {Style=(Style)FindResource("SettingsInput"),Visibility=Visibility.Collapsed,Padding=new Thickness(18,10,54,10)};keyRow.Children.Add(reveal);
        var eye=new Button {Content=UiIcons.Image("eye",22),ToolTip="显示或隐藏 API Key",Width=48,Height=48,Padding=new Thickness(12),HorizontalAlignment=HorizontalAlignment.Right,Background=System.Windows.Media.Brushes.Transparent,Margin=new Thickness(1)};
        bool showing=false;eye.Click+=(_,_)=>{showing=!showing;if(showing)reveal.Text=key.Password;else{key.Password=reveal.Text;reveal.Clear();}key.Visibility=showing?Visibility.Collapsed:Visibility.Visible;reveal.Visibility=showing?Visibility.Visible:Visibility.Collapsed;};
        reveal.TextChanged+=(_,_)=>{if(showing)key.Password=reveal.Text;};keyRow.Children.Add(eye);root.Children.Add(keyRow);
        root.SizeChanged+=(_,_)=>{foreach(var field in new FrameworkElement[]{address,model,keyRow}){field.Width=Math.Min(800,root.ActualWidth);field.HorizontalAlignment=HorizontalAlignment.Left;}};
        root.Children.Add(buttons);root.Children.Add(status);status.Style=progressStyle;
        root.Children.Add(new Separator {Margin=new Thickness(0,24,0,26)});
        var modelRow=new DockPanel();DockPanel.SetDock(models,Dock.Right);modelRow.Children.Add(models);
        var description=new StackPanel();description.Children.Add(modelHeading);description.Children.Add(new TextBlock {Text=Transcriber.ModelAvailable?"Whisper small · 本地转写":"Whisper small · 尚未安装",Foreground=(System.Windows.Media.Brush)FindResource("TextSecondary"),Margin=new Thickness(0,6,0,0)});modelRow.Children.Add(description);root.Children.Add(modelRow);
        root.Children.Add(modelStatus);root.Children.Add(modelProgress);root.Children.Add(progressBar);
        root.Children.Add(new Separator {Margin=new Thickness(0,26,0,16)});
        // The protocol selector stays out of the visual tree. Existing saved protocol
        // choices continue to work, while automatic detection remains the default.
        advanced.Children.Clear();
        generationMode.Style=thinking.Style=(Style)FindResource("SettingsChoice");
        generationMode.Width=286;thinking.Width=126;
        advanced.Children.Add(SettingRow("笔记模式","选择录音结束后的笔记整理方式",generationMode));
        advanced.Children.Add(SettingRow("思考模式","设置 AI 整理时的思考强度",thinking));
        stream.Content=null;stream.Margin=new Thickness(0);stream.Style=(Style)FindResource("SettingsToggle");
        System.Windows.Automation.AutomationProperties.SetName(stream,"流式生成");
        advanced.Children.Add(SettingRow("流式生成","生成过程中逐步显示笔记内容",stream));
        chunk.Style=(Style)FindResource("SettingsInput");chunk.Width=100;chunk.Height=34;chunk.FontSize=14;chunk.Padding=new Thickness(10,5,10,5);chunk.HorizontalContentAlignment=HorizontalAlignment.Right;
        advanced.Children.Add(SettingRow("每段整理字数","每段 2,000–20,000 字",chunk));
        folder.Margin=new Thickness(0);folder.Padding=new Thickness(12,6,12,6);
        advanced.Children.Add(SettingRow("数据文件夹","查看本地保存的笔记、录音与模型",folder));
        root.Children.Add(expander);root.Children.Add(new Separator {Margin=new Thickness(0,16,0,26)});root.Children.Add(updates);
        foreach(var field in new[]{address,model}){field.Style=(Style)FindResource("SettingsInput");field.MaxWidth=800;field.HorizontalAlignment=HorizontalAlignment.Stretch;}
        foreach(var label in root.Children.OfType<TextBlock>().Where(t=>t!=aiHeading&&t.FontSize<20)){label.FontSize=14;}
        buttons.Margin=new Thickness(0,18,0,0);save.Width=108;test.Width=112;save.Height=test.Height=44;save.Margin=new Thickness(0,0,18,0);test.Margin=new Thickness(0);
        Closing+=(_,e)=> { if(updates.IsDownloading){updates.Cancel();e.Cancel=true;status.Text="正在取消更新下载，请稍后关闭。";return;} if(downloadCts==null && dirty) {try {Save();dirty=false;}catch(Exception ex){e.Cancel=true;status.Text=ex.Message;}} if(downloadCts!=null) {downloadCts.Cancel(); e.Cancel=true; status.Text="正在取消下载，请稍后关闭。";} CanDetach=!e.Cancel; };
    }
    private Border SettingRow(string title,string description,FrameworkElement control)
    {
        var row=new Grid {MinHeight=66,Margin=new Thickness(0,2,0,2)};
        row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
        row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        var label=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,10,20,10)};
        label.Children.Add(new TextBlock {Text=title,FontSize=14,FontWeight=FontWeights.Medium});
        label.Children.Add(new TextBlock {Text=description,FontSize=13,Foreground=(System.Windows.Media.Brush)FindResource("TextSecondary"),Margin=new Thickness(0,3,0,0),TextWrapping=TextWrapping.Wrap});
        row.Children.Add(label);Grid.SetColumn(control,1);control.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(control);
        System.Windows.Automation.AutomationProperties.SetName(control,title);
        return new Border {BorderBrush=(System.Windows.Media.Brush)FindResource("Line"),BorderThickness=new Thickness(0,0,0,1),Child=row};
    }
    private static void Label(Panel root,string label,Control field) {root.Children.Add(new TextBlock {Text=label,Margin=new Thickness(0,15,0,6),FontSize=14}); root.Children.Add(field);}
    private void Save() { _=NoteService.Endpoint(address.Text); if(!int.TryParse(chunk.Text,out var length) || length<2000 || length>20000) throw new ArgumentException("文字长度应为 2000 到 20000。"); prefs.BaseUrl=address.Text.Trim(); prefs.Model=model.Text.Trim(); if(!string.IsNullOrWhiteSpace(key.Password))prefs.SetKey(key.Password); prefs.GenerationMode=generationMode.SelectedIndex==1?"direct":"classroom"; prefs.ThinkingProtocol=new[]{"auto","effort","toggle","disabled"}[Math.Clamp(thinkingProtocol.SelectedIndex,0,3)]; prefs.ThinkingMode=thinking.SelectedItem?.ToString() switch {"Off"=>"none","Low"=>"low","Medium"=>"medium","XHigh"=>"xhigh","Max"=>"max",_=>"high"}; prefs.Stream=stream.IsChecked==true; prefs.ChunkCharacters=length; prefs.SaveApi(); }
    private void UpdateModel()=>status.Text=Transcriber.ModelAvailable?"本地模型已就绪，可以离线转写。":"尚未安装模型。你可以先录音，安装模型后再整理。";
}
