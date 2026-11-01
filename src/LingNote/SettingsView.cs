using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LingNote;
public sealed class SettingsView : UserControl
{
    private readonly Preferences prefs;
    private readonly TextBox address=new(),model=new(),chunk=new();
    private readonly ComboBox thinking=new(),generationMode=new(),thinkingProtocol=new(),timeout=new();
    private readonly PasswordBox key=new() {Padding=new Thickness(10)};
    private readonly CheckBox stream=new() {Content="流式生成笔记（若服务不兼容可关闭）",Margin=new Thickness(0,12,0,12)};
    private readonly TextBlock status=new() {FontSize=14,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};
    private readonly Button download=new() {Content="下载模型（约 466 MB）"};
    private CancellationTokenSource? downloadCts;
    public bool CanDetach {get;private set;}
    public string? PendingInstaller {get;private set;}
    public string? PendingInstallerHash {get;private set;}
    public Action<string>? ChangeDataDirectory {get;set;}
    public SettingsView(Preferences preferences)
    {
        prefs=preferences;
        var modelStatus=new TextBlock {Visibility=Visibility.Collapsed,Text="模型已就绪，可离线转写",TextWrapping=TextWrapping.Wrap,Foreground=System.Windows.Media.Brushes.Gray,Margin=new Thickness(0,12,0,0)};
        var models=new WrapPanel {Margin=new Thickness(0,12,0,0)};var import=new Button {Content="导入模型",Margin=new Thickness(0,0,8,0)};var cancel=new Button {Content="取消下载",Margin=new Thickness(0),Visibility=Visibility.Collapsed};download.Margin=new Thickness(0,0,8,0);download.Content="下载模型";download.Style=(Style)FindResource("Primary");download.Visibility=Transcriber.ModelAvailable?Visibility.Collapsed:Visibility.Visible;models.Children.Add(download);models.Children.Add(import);models.Children.Add(cancel);
        var progressStyle=new Style(typeof(TextBlock));var emptyProgress=new Trigger {Property=TextBlock.TextProperty,Value=""};emptyProgress.Setters.Add(new Setter(VisibilityProperty,Visibility.Collapsed));progressStyle.Triggers.Add(emptyProgress);
        var modelProgress=new TextBlock {Style=progressStyle,TextWrapping=TextWrapping.Wrap,FontSize=12,Foreground=System.Windows.Media.Brushes.Gray,Margin=new Thickness(0,12,0,0)};
        var progressBar=new ProgressBar {Height=3,IsIndeterminate=true,Visibility=Visibility.Collapsed,Margin=new Thickness(0,8,0,0)};
        address.Text=prefs.BaseUrl;
        model.Text=prefs.Model;
        try {key.Password=prefs.GetKey();}catch {status.Text="已保存的密钥无法读取，请重新填写。";}
        
        generationMode.Items.Add("课堂模式 · 按需精编");generationMode.Items.Add("精编模式 · 录音后自动生成");
        generationMode.SelectedIndex=prefs.GenerationMode=="direct"?1:0;
        foreach(var option in new[]{"自动识别","思考强度（reasoning_effort）","思考开关（enable_thinking）","不发送"})thinkingProtocol.Items.Add(option);
        var protocols=new[]{"auto","effort","toggle","disabled"};
        thinkingProtocol.SelectedIndex=Array.IndexOf(protocols,prefs.ThinkingProtocol);if(thinkingProtocol.SelectedIndex<0)thinkingProtocol.SelectedIndex=0;
        
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
        var buttons=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,16,0,0)};var save=new Button {Content="保存",Style=(Style)FindResource("Primary")};var test=new Button {Content="测试连接"};buttons.Children.Add(save);buttons.Children.Add(test);status.Style=progressStyle;
        var advanced=new StackPanel();
        stream.Content="流式生成";stream.IsChecked=prefs.Stream;chunk.Text=prefs.ChunkCharacters.ToString();
        var folder=new Button {Content="打开数据文件夹",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(4,12,4,4)};folder.Click+=(_,_)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Paths.Root){UseShellExecute=true});
        bool dirty=false;address.TextChanged+=(_,_)=>dirty=true;model.TextChanged+=(_,_)=>dirty=true;key.PasswordChanged+=(_,_)=>dirty=true;chunk.TextChanged+=(_,_)=>dirty=true;stream.Checked+=(_,_)=>dirty=true;stream.Unchecked+=(_,_)=>dirty=true;thinking.SelectionChanged+=(_,_)=>{if(!rebuilding)dirty=true;};thinkingProtocol.SelectionChanged+=(_,_)=>dirty=true;generationMode.SelectionChanged+=(_,_)=>dirty=true;
        save.Click+=(_,_)=> { try {Save(); dirty=false; status.Text="已保存，下次打开会自动读取。";} catch(Exception ex) {status.Text=ex.Message;} };
        test.Click+=async(_,_)=> { try {Save(); dirty=false; test.IsEnabled=false; status.Text="正在测试连接…"; var text=await new NoteService().Request(prefs,"只回复：连接成功。","测试",null,CancellationToken.None); status.Text="连接成功："+text[..Math.Min(100,text.Length)];} catch(Exception ex) {status.Text=ex.Message;} finally {test.IsEnabled=true;} };
        download.Click+=async(_,_)=> { modelStatus.Visibility=Visibility.Collapsed;downloadCts=new(); cancel.Visibility=progressBar.Visibility=Visibility.Visible; download.IsEnabled=false; import.IsEnabled=false; try {await Transcriber.Download(new Progress<string>(s=>modelProgress.Text=s),downloadCts.Token);ModelSucceeded();UpdateModelState();} catch(OperationCanceledException) {modelProgress.Text="已取消下载，可重新下载。";} catch(Exception ex) {modelProgress.Text="下载失败："+ex.Message+" 可以重试，或导入已下载的模型文件。";} finally {download.IsEnabled=true; import.IsEnabled=true; downloadCts.Dispose(); downloadCts=null;cancel.Visibility=progressBar.Visibility=Visibility.Collapsed;UpdateModelState();} };
        cancel.Click+=(_,_)=>downloadCts?.Cancel();
        import.Click+=async(_,_)=> { modelStatus.Visibility=Visibility.Collapsed;var dialog=new Microsoft.Win32.OpenFileDialog {Filter="Whisper GGML 模型|ggml-small.bin;*.bin"}; if(dialog.ShowDialog(Owner??Window.GetWindow(this))!=true) return; import.IsEnabled=download.IsEnabled=false;try {await using(var input=File.OpenRead(dialog.FileName)) {if(!Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(input)).Equals(Transcriber.SmallSha256,StringComparison.OrdinalIgnoreCase))throw new IOException("请选择完整的官方 Whisper small 多语言 GGML 模型。");} Directory.CreateDirectory(Paths.Models); if(!Path.GetFullPath(dialog.FileName).Equals(Path.GetFullPath(Paths.Model),StringComparison.OrdinalIgnoreCase)) File.Copy(dialog.FileName,Paths.Model,true);ModelSucceeded();UpdateModelState();} catch(Exception ex) {modelProgress.Text=ex.Message;} finally {import.IsEnabled=download.IsEnabled=true;} };
        void ModelSucceeded(){modelProgress.Text="";modelStatus.Visibility=Visibility.Visible;}
        void UpdateModelState(){var ready=Transcriber.ModelAvailable;download.Visibility=ready?Visibility.Collapsed:Visibility.Visible;}
        var updates=new UpdatePanel((file,hash)=>{Save();dirty=false;PendingInstaller=file;PendingInstallerHash=hash;Close();});
        var shell=new Grid();shell.SetResourceReference(Panel.BackgroundProperty,"Surface");
        shell.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(232)});shell.ColumnDefinitions.Add(new ColumnDefinition());
        var nav=new StackPanel {Margin=new Thickness(22,18,12,18)};
        var back=new Button {Content=SettingsIcons.Label("back","返回主界面"),Height=38,FontSize=13,Foreground=(System.Windows.Media.Brush)FindResource("TextSecondary"),Background=new SolidColorBrush(Color.FromRgb(235,238,244)),HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,8,12,8),Margin=new Thickness(0,0,0,20),ToolTip="返回 LingNote，保留当前笔记"};back.Click+=(_,_)=>BackRequested?.Invoke();nav.Children.Add(back);
        nav.Children.Add(new TextBlock {Text="设置",FontSize=24,FontWeight=FontWeights.Bold,Margin=new Thickness(10,0,0,12)});
        var search=new TextBox {Height=34,Padding=new Thickness(12,6,12,6),Text="",Style=(Style)FindResource("SettingsInput"),ToolTip="搜索设置",Margin=new Thickness(0,0,0,14)};System.Windows.Automation.AutomationProperties.SetName(search,"搜索设置");search.Margin=new Thickness(0);search.Background=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(237,240,246));var searchRow=new Grid {Margin=new Thickness(0,0,0,14)};searchRow.Children.Add(search);var placeholder=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(12,0,0,0),IsHitTestVisible=false,VerticalAlignment=VerticalAlignment.Center};placeholder.Children.Add(SettingsIcons.Image("search",16));placeholder.Children.Add(new TextBlock {Text="搜索设置",Foreground=System.Windows.Media.Brushes.Gray,Margin=new Thickness(7,0,0,0)});searchRow.Children.Add(placeholder);search.TextChanged+=(_,_)=>placeholder.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;nav.Children.Add(searchRow);
        var navBorder=new Border {Child=nav};navBorder.SetResourceReference(Border.BackgroundProperty,"SidebarSurface");shell.Children.Add(navBorder);
        var page=new StackPanel {Margin=new Thickness(32,28,32,28),MaxWidth=900};var scroll=new ScrollViewer {Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetColumn(scroll,1);shell.Children.Add(scroll);Content=shell;
        foreach(var field in new[]{address,model}){field.Style=(Style)FindResource("SettingsInput");field.FontSize=14;field.Height=34;field.Padding=new Thickness(12,5,12,5);}
        key.FontSize=14;key.Height=34;key.Padding=new Thickness(12,5,44,5);
        var keyRow=new Grid();keyRow.Children.Add(key);var reveal=new TextBox {Style=(Style)FindResource("SettingsInput"),Height=34,Padding=new Thickness(12,5,44,5),Visibility=Visibility.Collapsed};keyRow.Children.Add(reveal);
        var eye=new Button {Content=SettingsIcons.Image("eye",18),Width=34,Height=32,Padding=new Thickness(7),HorizontalAlignment=HorizontalAlignment.Right,Background=System.Windows.Media.Brushes.Transparent,Margin=new Thickness(1),ToolTip="显示或隐藏 API Key"};
        bool showing=false;eye.Click+=(_,_)=>{showing=!showing;if(showing)reveal.Text=key.Password;else{key.Password=reveal.Text;reveal.Clear();}key.Visibility=showing?Visibility.Collapsed:Visibility.Visible;reveal.Visibility=showing?Visibility.Visible:Visibility.Collapsed;};reveal.TextChanged+=(_,_)=>{if(showing)key.Password=reveal.Text;};keyRow.Children.Add(eye);
        var api=new StackPanel();api.Children.Add(FormRow("接口地址",address));var modelChoice=new ComboBox {Style=(Style)FindResource("SettingsEditableChoice"),IsEditable=true,Height=34,Padding=new Thickness(10,4,10,4),Background=System.Windows.Media.Brushes.White,BorderBrush=(System.Windows.Media.Brush)FindResource("Line")};modelChoice.Items.Add(prefs.Model);modelChoice.SetBinding(ComboBox.TextProperty,new System.Windows.Data.Binding("Text") {Source=model,Mode=System.Windows.Data.BindingMode.TwoWay,UpdateSourceTrigger=System.Windows.Data.UpdateSourceTrigger.PropertyChanged});api.Children.Add(FormRow("模型",modelChoice));api.Children.Add(FormRow("API Key",keyRow));api.Children.Add(buttons);api.Children.Add(status);
        buttons.HorizontalAlignment=HorizontalAlignment.Right;buttons.Margin=new Thickness(0,14,0,0);save.Width=test.Width=112;save.Height=test.Height=34;save.Margin=new Thickness(0,0,12,0);test.Margin=new Thickness(0);
        var apiCard=Card("接口配置","link",api);
        stream.Content=null;stream.Margin=new Thickness(0);stream.Style=(Style)FindResource("SettingsToggle");
        timeout.Style=(Style)FindResource("SettingsChoice");timeout.Width=110;foreach(var seconds in new[]{30,60,120,300,600})timeout.Items.Add(seconds+" 秒");timeout.SelectedIndex=Array.IndexOf(new[]{30,60,120,300,600},prefs.RequestTimeoutSeconds);if(timeout.SelectedIndex<0)timeout.SelectedIndex=4;
        timeout.SelectionChanged+=(_,_)=>{dirty=true;};
        advanced.Children.Add(SettingRow("请求超时时间","单次请求的最长等待时间。",timeout));advanced.Children.Add(SettingRow("流式输出（SSE）","以流式方式接收 AI 回复，获得更快的响应体验。",stream));
        var advancedCard=Card("请求与高级选项","sliders",advanced);
        generationMode.Style=thinking.Style=(Style)FindResource("SettingsChoice");generationMode.Width=260;thinking.Width=110;
        var general=new StackPanel();general.Children.Add(SettingRow("笔记模式","选择新录音的笔记整理方式",generationMode));general.Children.Add(SettingRow("思考模式","设置 AI 整理时的思考强度",thinking));chunk.Style=(Style)FindResource("SettingsInput");chunk.Width=110;chunk.Height=34;chunk.Padding=new Thickness(12,5,12,5);general.Children.Add(SettingRow("每段整理字数","每段 2,000–20,000 字",chunk));
        var modelBody=new StackPanel();models.Margin=new Thickness(0);foreach(var button in new[]{download,import,cancel}){button.Height=34;button.MinWidth=112;button.Padding=new Thickness(14,6,14,6);button.Margin=new Thickness(8,0,0,0);}
        modelBody.Children.Add(SettingRow("Whisper small",Transcriber.ModelAvailable?"模型已就绪 · 在本地完成语音转写":"尚未安装 · 下载或导入模型后即可本地转写",models));modelBody.Children.Add(modelStatus);modelBody.Children.Add(modelProgress);modelBody.Children.Add(progressBar);
        modelStatus.Text="Whisper small · 本地模型已就绪";var modelCard=Card("本地语音模型","mic",modelBody);
        var updateCard=Card("","restore",updates);
        var storage=new StackPanel();folder.HorizontalAlignment=HorizontalAlignment.Right;folder.Width=144;folder.Height=34;folder.Margin=new Thickness(0);folder.Padding=new Thickness(12,6,12,6);
        var dataLocation=new TextBlock {Text=Paths.Root,TextWrapping=TextWrapping.Wrap,FontSize=13,Foreground=(Brush)FindResource("TextSecondary")};
        var changeData=new Button {Content="更改文件夹",Width=112,Margin=new Thickness(8,0,0,0)};
        var storageStatus=new TextBlock {TextWrapping=TextWrapping.Wrap,FontSize=13,Foreground=(Brush)FindResource("TextSecondary"),Margin=new Thickness(0,10,0,0),Visibility=Visibility.Collapsed};
        changeData.Click+=(_,_)=>{try{if(downloadCts!=null)throw new IOException("请等待模型下载结束后再更改数据文件夹。");var picker=new Microsoft.Win32.OpenFolderDialog {Title="选择录音与笔记文件夹（空文件夹，将复制现有数据）",InitialDirectory=Paths.Root};if(picker.ShowDialog(Owner??Window.GetWindow(this))!=true)return;Save();dirty=false;if(ChangeDataDirectory==null)throw new IOException("请从主界面打开设置后更改数据文件夹。");ChangeDataDirectory(picker.FolderName);dataLocation.Text=Paths.Root;storageStatus.Text="保存位置已更改，原文件夹中的数据已保留。";}catch(Exception ex){storageStatus.Text="更改失败："+ex.Message;}storageStatus.Visibility=Visibility.Visible;};
        var dataActions=new WrapPanel();dataActions.Children.Add(folder);dataActions.Children.Add(changeData);storage.Children.Add(SettingRow("录音与笔记",dataLocation,dataActions));storage.Children.Add(storageStatus);
        // Show the default directory being edited, not the active theme override.
        var location=new TextBlock {Text=string.IsNullOrWhiteSpace(prefs.ScreenshotDirectory)?CaptureService.DefaultDirectory:prefs.ScreenshotDirectory,TextWrapping=TextWrapping.Wrap,FontSize=13,Foreground=(System.Windows.Media.Brush)FindResource("TextSecondary")};
        var choose=new Button {Content="更改文件夹",Width=112,Height=34,Padding=new Thickness(12,6,12,6),Margin=new Thickness(0)};
        choose.Click+=(_,_)=>{var dialog=new Microsoft.Win32.OpenFolderDialog {Title="选择默认截屏保存文件夹"};if(dialog.ShowDialog(Owner??Window.GetWindow(this))==true){prefs.ScreenshotDirectory=dialog.FolderName;prefs.Save();location.Text=prefs.ScreenshotDirectory;}};
        var reset=new Button {Content="恢复默认",Width=112,Height=34,Padding=new Thickness(12,6,12,6),Margin=new Thickness(8,0,0,0)};reset.Click+=(_,_)=>{prefs.ScreenshotDirectory="";prefs.Save();location.Text=CaptureService.DefaultDirectory;};
        var openScreenshots=new Button {Content="打开文件夹",Width=112,Margin=new Thickness(0,0,8,0)};openScreenshots.Click+=(_,_)=>{try{CaptureService.OpenDirectory(CaptureService.DirectoryFor(prefs,""));}catch(Exception ex){storageStatus.Text="无法打开截屏文件夹："+ex.Message;storageStatus.Visibility=Visibility.Visible;}};
        var directoryActions=new WrapPanel();directoryActions.Children.Add(openScreenshots);directoryActions.Children.Add(choose);directoryActions.Children.Add(reset);storage.Children.Add(SettingRow("截屏保存位置",location,directoryActions));
        storage.Children.Add(new TextBlock {Text="主题单独指定文件夹后，截屏将优先保存到主题文件夹。",TextWrapping=TextWrapping.Wrap,FontSize=13,Foreground=(System.Windows.Media.Brush)FindResource("TextSecondary"),Margin=new Thickness(0,14,0,2)});
        var shortcuts=new StackPanel();shortcuts.Children.Add(SettingRow("截取窗口","保存前台窗口的完整画面",KeyCaps("Ctrl","Shift","1")));shortcuts.Children.Add(SettingRow("截取区域","拖动选择需要保存的范围",KeyCaps("Ctrl","Shift","2")));shortcuts.Children.Add(SettingRow("取消截屏","退出区域选择并返回原界面",KeyCaps("Esc")));
        Action? refreshNavigation=null;
        var appearance=new StackPanel();appearance.Children.Add(SettingRow("界面模式","浅色界面 · 配色立即生效并自动保存",LightPreview()));
        var accentOptions=new (string Name,string Hex)[]{("蓝色","#1263F1"),("石墨","#334155"),("靛蓝","#4338CA"),("紫色","#6D28D9"),("兰紫","#7E22CE"),("玫红","#BE185D"),("红色","#BE123C"),("橙色","#C2410C"),("琥珀","#A16207"),("绿色","#15803D"),("青绿","#047857"),("湖蓝","#0E7490")};
        var backgroundOptions=new (string Name,string Hex)[]{("纯白","#FFFFFF"),("雾灰","#F5F6F8"),("暖米","#F8F5EF"),("浅绿","#F1F6F3"),("浅蓝","#F0F5FA"),("浅紫","#F7F2F8")};
        void ApplyAppearance(){prefs.Save();AppTheme.Apply(prefs);RefreshColorChoices(appearance);refreshNavigation?.Invoke();}
        appearance.Children.Add(ColorChoices("强调色","用于主按钮、开关和选中项",accentOptions,()=>prefs.ThemeAccent,color=>{prefs.ThemeAccent=color;ApplyAppearance();}));
        appearance.Children.Add(ColorChoices("页面底色","卡片保持白色，阅读区域更清晰",backgroundOptions,()=>prefs.ThemeBackground,color=>{prefs.ThemeBackground=color;ApplyAppearance();}));
        var restoreTheme=new Button {Content="恢复默认配色",HorizontalAlignment=HorizontalAlignment.Right,MinWidth=132,Margin=new Thickness(0,14,0,0)};
        restoreTheme.Click+=(_,_)=>{prefs.ThemeAccent="#1263F1";prefs.ThemeBackground="#FFFFFF";ApplyAppearance();RefreshColorChoices(appearance);};appearance.Children.Add(restoreTheme);
        var about=new StackPanel();var brand=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,4,0,20)};brand.Children.Add(new Image {Source=new BitmapImage(new Uri("pack://application:,,,/LingNote;component/Assets/app-logo.png")),Width=48,Height=48,Margin=new Thickness(0,0,14,0)});var brandText=new StackPanel {VerticalAlignment=VerticalAlignment.Center};brandText.Children.Add(new TextBlock {Text="LingNote",FontSize=25,FontWeight=FontWeights.SemiBold});brandText.Children.Add(new TextBlock {Text="让录音成为清晰的知识笔记",FontSize=13,Foreground=(System.Windows.Media.Brush)FindResource("TextSecondary"),Margin=new Thickness(0,4,0,0)});brand.Children.Add(brandText);about.Children.Add(brand);about.Children.Add(SettingRow("当前版本","已安装的 LingNote 版本",KeyCaps(UpdateService.DisplayVersion)));about.Children.Add(new TextBlock {Text="本地录音、语音转写与 AI 笔记。支持课堂模式与精编模式，帮助你记录、整理和回顾知识。",TextWrapping=TextWrapping.Wrap,FontSize=14,Foreground=(System.Windows.Media.Brush)FindResource("TextSecondary"),LineHeight=23,Margin=new Thickness(0,18,0,4)});
        var cards=new Dictionary<string,List<Border>> {
            ["常规"]=[Card("录音与笔记","settings",general)], ["AI 连接"]=[apiCard,advancedCard,modelCard,updateCard],
            ["本地语音模型"]=[modelCard], ["应用更新"]=[updateCard], ["存储与数据"]=[Card("保存位置","database",storage)],
            ["快捷键"]=[Card("键盘快捷键","keyboard",shortcuts)],
            ["外观"]=[Card("视觉风格","palette",appearance)],
            ["关于"]=[Card("","info",about)]};
        var icons=new[]{"settings","link","mic","restore","database","keyboard","palette","info"};var navButtons=new List<Button>();string current="常规";
        void ShowPage(string category){current=category;page.Children.Clear();foreach(var card in cards[category]){if(card.Parent is Panel parent)parent.Children.Remove(card);page.Children.Add(card);}foreach(var button in navButtons){bool selected=(string)button.Tag==category;button.Background=selected?(Brush)FindResource("AccentSelection"):Brushes.Transparent;button.Foreground=selected?(Brush)FindResource("Accent"):Brushes.Black;var content=(StackPanel)button.Content;((Border)content.Children[0]).Background=selected?(System.Windows.Media.Brush)FindResource("Accent"):System.Windows.Media.Brushes.Transparent;var image=(Image)content.Children[1];image.Source=SettingsIcons.Image(icons[navButtons.IndexOf(button)],18,false,selected?(System.Windows.Media.Brush)FindResource("Accent"):null).Source;}scroll.ScrollToTop();}
        int iconIndex=0;foreach(var category in cards.Keys){var button=new Button {Tag=category,Content=SettingsIcons.Label(icons[iconIndex++],category),Height=38,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,8,7),Margin=new Thickness(0,2,0,2),Background=System.Windows.Media.Brushes.Transparent};var navContent=(StackPanel)button.Content;navContent.Children.Insert(0,new Border {Width=3,Height=24,CornerRadius=new CornerRadius(2),Margin=new Thickness(-12,-2,10,-2)});button.Click+=(_,_)=>ShowPage(category);button.MouseEnter+=(_,_)=>{if(current!=category)button.Background=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238,242,249));};button.MouseLeave+=(_,_)=>{if(current!=category)button.Background=System.Windows.Media.Brushes.Transparent;};nav.Children.Add(button);navButtons.Add(button);}
        search.TextChanged+=(_,_)=>{var query=search.Text.Trim();foreach(var button in navButtons){var category=(string)button.Tag;var keywords=category switch {"AI 连接"=>"接口 地址 模型 API Key 密钥 超时 流式 SSE", "常规"=>"笔记 模式 思考 字数", "本地语音模型"=>"Whisper 导入 下载 转写", "存储与数据"=>"截屏 截图 文件夹 路径 download", _=>category};button.Visibility=(category+keywords).Contains(query,StringComparison.OrdinalIgnoreCase)?Visibility.Visible:Visibility.Collapsed;}var first=navButtons.FirstOrDefault(b=>b.Visibility==Visibility.Visible);if(first!=null)ShowPage((string)first.Tag);else{page.Children.Clear();page.Children.Add(new TextBlock {Text="没有找到相关设置",FontSize=22});}};
        refreshNavigation=()=>ShowPage(current);
        ShowPage("常规");
        Closing+=(_,e)=> { if(updates.IsDownloading){updates.Cancel();e.Cancel=true;status.Text="正在取消更新下载，请稍后关闭。";return;} if(downloadCts==null && dirty) {try {Save();dirty=false;}catch(Exception ex){e.Cancel=true;status.Text=ex.Message;}} if(downloadCts!=null) {downloadCts.Cancel(); e.Cancel=true; status.Text="正在取消下载，请稍后关闭。";} CanDetach=!e.Cancel; };
    }
    public Window? Owner {get;set;}
    public event Action? BackRequested;
    private event EventHandler<System.ComponentModel.CancelEventArgs>? Closing;
    public event EventHandler? Closed;
    private bool detached;
    public void Close()
    {
        if(detached)return;
        var args=new System.ComponentModel.CancelEventArgs();Closing?.Invoke(this,args);CanDetach=!args.Cancel;
        if(CanDetach){detached=true;Closed?.Invoke(this,EventArgs.Empty);}
    }
    private Border Card(string title,string icon,UIElement body)
    {
        NormalizeActionButtons(body);var panel=new StackPanel();if(title.Length>0){var heading=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,10)};heading.Children.Add(SettingsIcons.Image(icon,21));heading.Children.Add(new TextBlock {Text=title,FontSize=17,FontWeight=FontWeights.SemiBold,Margin=new Thickness(10,0,0,0)});panel.Children.Add(heading);}panel.Children.Add(body);
        return new Border {Background=Brushes.White,CornerRadius=new CornerRadius(14),BorderBrush=(System.Windows.Media.Brush)FindResource("Line"),BorderThickness=new Thickness(1),Padding=new Thickness(20,18,20,18),Margin=new Thickness(0,0,0,16),Child=panel};
    }
    private static Grid FormRow(string title,FrameworkElement field)
    {
        var row=new Grid {Margin=new Thickness(0,4,0,6)};row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(116)});row.ColumnDefinitions.Add(new ColumnDefinition());row.Children.Add(new TextBlock {Text=title,VerticalAlignment=VerticalAlignment.Center});Grid.SetColumn(field,1);row.Children.Add(field);System.Windows.Automation.AutomationProperties.SetName(field,title);return row;
    }
    private Border SettingRow(string title,string description,FrameworkElement control)=>SettingRow(title,new TextBlock {Text=description,FontSize=13,Foreground=(Brush)FindResource("TextSecondary"),TextWrapping=TextWrapping.Wrap,LineHeight=20},control);
    private Border SettingRow(string title,FrameworkElement description,FrameworkElement control)
    {
        var row=new Grid {MinHeight=70};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        var label=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,12,24,12)};label.Children.Add(new TextBlock {Text=title,FontSize=14,FontWeight=FontWeights.Medium,TextWrapping=TextWrapping.Wrap});description.Margin=new Thickness(0,5,0,0);label.Children.Add(description);row.Children.Add(label);
        Grid.SetColumn(control,1);control.VerticalAlignment=VerticalAlignment.Center;control.HorizontalAlignment=HorizontalAlignment.Right;row.Children.Add(control);System.Windows.Automation.AutomationProperties.SetName(control,title);
        return new Border {BorderBrush=(Brush)FindResource("Line"),BorderThickness=new Thickness(0,0,0,1),Child=row};
    }
    private static void NormalizeActionButtons(DependencyObject root)
    {
        if(root is Button {Content:string} button){button.ClearValue(HeightProperty);button.MinHeight=36;button.Padding=new Thickness(14,6,14,6);button.VerticalContentAlignment=VerticalAlignment.Center;}
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())NormalizeActionButtons(child);
    }
    private FrameworkElement ColorChoices(string title,string description,(string Name,string Hex)[] choices,Func<string> current,Action<string> select)
    {
        var root=new StackPanel {Margin=new Thickness(0,14,0,14)};root.Children.Add(new TextBlock {Text=title,FontSize=14,FontWeight=FontWeights.Medium});root.Children.Add(new TextBlock {Text=description,FontSize=13,Foreground=(Brush)FindResource("TextSecondary"),Margin=new Thickness(0,5,0,12)});
        var colors=new WrapPanel();root.Children.Add(colors);var options=new List<(Button Button,Border Frame,TextBlock Mark,string Color)>();
        void Refresh(){foreach(var option in options){bool chosen=string.Equals(option.Color,current(),StringComparison.OrdinalIgnoreCase);option.Frame.BorderBrush=chosen?(Brush)FindResource("Accent"):Brushes.Transparent;option.Mark.Visibility=chosen?Visibility.Visible:Visibility.Hidden;System.Windows.Automation.AutomationProperties.SetItemStatus(option.Button,chosen?"已选中":"未选中");}}
        foreach(var choice in choices){var color=(Color)ColorConverter.ConvertFromString(choice.Hex);var swatch=new Border {Width=34,Height=28,CornerRadius=new CornerRadius(6),Background=new SolidColorBrush(color),BorderBrush=new SolidColorBrush(Color.FromRgb(210,215,224)),BorderThickness=new Thickness(1)};var mark=new TextBlock {Text="✓",FontSize=16,FontWeight=FontWeights.SemiBold,Foreground=color.R+color.G+color.B>600?Brushes.Black:Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};swatch.Child=mark;
            var content=new StackPanel();content.Children.Add(swatch);content.Children.Add(new TextBlock {Text=choice.Name,FontSize=12,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,6,0,0)});var frame=new Border {Child=content,CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(2),Padding=new Thickness(6)};
            var button=new Button {Content=frame,Padding=new Thickness(0),Margin=new Thickness(0,0,8,8),Background=Brushes.Transparent,ToolTip=choice.Name+" "+choice.Hex};System.Windows.Automation.AutomationProperties.SetName(button,title+"："+choice.Name);button.Click+=(_,_)=>{select(choice.Hex);Refresh();};colors.Children.Add(button);options.Add((button,frame,mark,choice.Hex));}
        root.Tag=(Action)Refresh;Refresh();return root;
    }
    private static void RefreshColorChoices(Panel panel){foreach(var child in panel.Children.OfType<FrameworkElement>())if(child.Tag is Action refresh)refresh();}
    private static FrameworkElement KeyCaps(params string[] keys)
    {
        var panel=new StackPanel {Orientation=Orientation.Horizontal};foreach(var value in keys)panel.Children.Add(new Border {CornerRadius=new CornerRadius(6),Background=new SolidColorBrush(Color.FromRgb(245,246,248)),BorderBrush=new SolidColorBrush(Color.FromRgb(228,231,236)),BorderThickness=new Thickness(1),Padding=new Thickness(9,5,9,5),Margin=new Thickness(5,0,0,0),Child=new TextBlock {Text=value,FontSize=13,Foreground=new SolidColorBrush(Color.FromRgb(70,77,88))}});return panel;
    }
    private static FrameworkElement Swatch(string color,string text)
    {
        var panel=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};panel.Children.Add(new Border {Width=17,Height=17,CornerRadius=new CornerRadius(9),Background=(Brush)new BrushConverter().ConvertFromString(color)!,BorderBrush=Brushes.LightGray,BorderThickness=new Thickness(1),Margin=new Thickness(0,0,8,0)});panel.Children.Add(new TextBlock {Text=text+"  "+color,FontSize=13,Foreground=Brushes.DimGray,VerticalAlignment=VerticalAlignment.Center});return panel;
    }
    private static FrameworkElement LightPreview()
    {
        var preview=new Grid {Width=116,Height=65,Background=Brushes.White};preview.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(28)});preview.ColumnDefinitions.Add(new ColumnDefinition());preview.Children.Add(new Border {Background=new SolidColorBrush(Color.FromRgb(241,243,247))});var lines=new StackPanel {Margin=new Thickness(9,12,9,0)};Grid.SetColumn(lines,1);foreach(var width in new[]{53d,38d,46d}){var line=new Border {Height=4,Width=width,Background=Brushes.LightGray,HorizontalAlignment=HorizontalAlignment.Left,CornerRadius=new CornerRadius(2),Margin=new Thickness(0,0,0,7)};if(width==38)line.SetResourceReference(Border.BackgroundProperty,"Accent");lines.Children.Add(line);}preview.Children.Add(lines);var frame=new Border {CornerRadius=new CornerRadius(9),BorderThickness=new Thickness(2),Padding=new Thickness(4),Child=preview,Margin=new Thickness(0,8,0,8)};frame.SetResourceReference(Border.BorderBrushProperty,"Accent");return frame;
    }
    private void Save() { _=NoteService.Endpoint(address.Text); if(!int.TryParse(chunk.Text,out var length) || length<2000 || length>20000) throw new ArgumentException("文字长度应为 2000 到 20000。"); prefs.BaseUrl=address.Text.Trim(); prefs.Model=model.Text.Trim(); if(!string.IsNullOrWhiteSpace(key.Password))prefs.SetKey(key.Password); prefs.GenerationMode=generationMode.SelectedIndex==1?"direct":"classroom"; prefs.ThinkingProtocol=new[]{"auto","effort","toggle","disabled"}[Math.Clamp(thinkingProtocol.SelectedIndex,0,3)]; prefs.ThinkingMode=thinking.SelectedItem?.ToString() switch {"Off"=>"none","Low"=>"low","Medium"=>"medium","XHigh"=>"xhigh","Max"=>"max",_=>"high"}; prefs.RequestTimeoutSeconds=new[]{30,60,120,300,600}[Math.Clamp(timeout.SelectedIndex,0,4)]; prefs.Stream=stream.IsChecked==true; prefs.ChunkCharacters=length; prefs.SaveApi(); }
}
