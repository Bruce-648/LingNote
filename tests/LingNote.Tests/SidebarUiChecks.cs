using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Documents;
using LingNote;

internal static class SidebarUiChecks
{
    public static void DesignReview(bool keepOpen)
    {
        Exception? error=null;
        var thread=new Thread(()=>{try{
            Environment.SetEnvironmentVariable("LINGNOTE_DATA",Path.Combine(AppContext.BaseDirectory,"design-review-data",Guid.NewGuid().ToString("N")));
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            Paths.Init();var prefs=new Preferences();prefs.Topics.Add("计算机网络");prefs.Save();var store=new Store();
            var course=new Course {Title="计算机网络的性能分析",Topic="计算机网络",State="已完成",Seconds=1938,
                Note="# 速率与带宽\n\n速率是单位时间内传送的数据量，常用单位为 bit/s。\n\n## 常用单位\n\n| 单位 | 换算 |\n|---|---|\n| kbit/s | 10³ bit/s |\n| Mbit/s | 10⁶ bit/s |\n| Gbit/s | 10⁹ bit/s |\n\n## 传输时延\n\n发送数据需要一定的时间，传输时延可表示为：\n\n$ t = L / R $\n\n其中，L 为数据长度，R 为发送速率。",
                EnhancedNote="# 理解网络的性能\n\n## 速率、带宽与吞吐量\n\n- **速率**：单位时间内传送的数据量。\n- **带宽**：链路能够传输的最大数据量。\n- **吞吐量**：实际成功传输的数据量。\n\n> **补充**\n>\n> 实际吞吐量还受协议开销和网络拥塞影响，例如重传、首部开销、拥塞控制等。\n\n## 时延的组成\n\n- 发送时延：将数据发送到链路上的时间。\n- 传播时延：信号传播需要的时间。\n- 处理时延：中间设备处理数据所需的时间。\n- 排队时延：在路由器队列中等待发送的时间。",
                Parts=[new AudioPart {Done=true,Text="[00:00:00] 本节介绍速率、带宽与吞吐量。"}]};
            store.Save(course);store.Save(new Course {Title="网络的基本概念",Topic="计算机网络",State="已完成"});store.Save(new Course {Title="协议与分层",Topic="计算机网络",State="已完成"});
            foreach(var topic in new[]{"线性代数","高等数学","操作系统","数据库系统"})prefs.Topics.Add(topic);prefs.Save();
            var app=CreateApplication();var window=new MainWindow();window.Show();Pump();
            T Field<T>(string name)=>(T)typeof(MainWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.GetValue(window)!;
            void Invoke(string name,params object?[] parameters)=>typeof(MainWindow).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,parameters);
            void Check(bool passed,string label){if(!passed)throw new Exception(label);Console.WriteLine("PASS: "+label);}
            Directory.CreateDirectory("artifacts/ui-refactor");
            Invoke("ShowCourse",course);Wait(120);Snapshot(window,"artifacts/ui-refactor/01-notes.png");
            var quote=Field<RichTextBox>("EnhancedPreview").Document.Blocks.OfType<Section>().Single();
            Check(quote.Background!=null&&MarkdownView.ToMarkdown(Field<RichTextBox>("EnhancedPreview").Document).Contains("> **补充**"),"浅蓝补充区块显示并保留 Markdown 引用格式");
            Field<Button>("CopyButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(Clipboard.GetText().Contains("常用单位"),"复制按钮写入真实课堂笔记");
            Invoke("TranscriptTab_Click",window,new RoutedEventArgs());Check(new TextRange(Field<RichTextBox>("Preview").Document.ContentStart,Field<RichTextBox>("Preview").Document.ContentEnd).Text.Contains("[00:00:00]"),"原始转写保留时间戳");
            Invoke("Settings_Click",window,new RoutedEventArgs());Wait(120);
            Check(Field<ContentControl>("SettingsHost").IsVisible&&!Field<Grid>("Workspace").IsVisible,"设置嵌入主窗口并保留侧栏");Snapshot(window,"artifacts/ui-refactor/02-settings.png");
            Check(Field<Border>("Sidebar").Visibility==Visibility.Collapsed,"设置完整替换主侧栏");
            Visuals<Button>(window).Single(b=>b.Tag?.ToString()=="常规").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Wait(120);
            Check(!Visuals<TextBlock>(window).Any(t=>t.Text=="思考参数协议")&&Visuals<ComboBox>(window).Count()==2,"常规分类保留笔记模式与思考模式");
            Snapshot(window,"artifacts/ui-refactor/08-advanced-settings.png");
            var choice=Visuals<ComboBox>(window).Single(c=>c.Items.Contains("Low"));choice.IsDropDownOpen=true;Wait(180);
            var popup=(System.Windows.Controls.Primitives.Popup)choice.Template.FindName("PART_Popup",choice);var menu=(FrameworkElement)popup.Child;menu.UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth+menu.Margin.Left+menu.Margin.Right),(int)Math.Ceiling(menu.ActualHeight+menu.Margin.Top+menu.Margin.Bottom),96,96,PixelFormats.Pbgra32);bitmap.Render(menu);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create("artifacts/ui-refactor/09-settings-menu.png"))encoder.Save(output);
            choice.IsDropDownOpen=false;

            Invoke("New_Click",window,new RoutedEventArgs());Check(Field<Grid>("Workspace").IsVisible,"离开设置可恢复录音页面");
            Invoke("Start_Click",window,new RoutedEventArgs());Wait(600);Check(Field<Course?>("recording")!=null,"真实音频录制启动");Snapshot(window,"artifacts/ui-refactor/03-recording.png");
            Invoke("Pause_Click",window,new RoutedEventArgs());var recorder=Field<AudioRecorder>("recorder");var elapsed=recorder.Elapsed;Wait(300);Check(Math.Abs(recorder.Elapsed-elapsed)<.05,"暂停时真实计时停止");Snapshot(window,"artifacts/ui-refactor/04-paused.png");
            var floating=Field<FloatingBar>("floating");floating.Show();floating.Update(elapsed,.05f,true);Wait(150);Snapshot(floating,"artifacts/ui-refactor/05-floating-paused.png");floating.SetCompact(true);Wait(250);Snapshot(floating,"artifacts/ui-refactor/06-floating-compact.png");floating.SetCompact(false);Wait(250);floating.Hide();
            Invoke("Pause_Click",window,new RoutedEventArgs());Wait(100);Check(recorder.Elapsed>elapsed,"继续录音恢复计时");Invoke("Stop_Click",window,new RoutedEventArgs());var stopWait=System.Diagnostics.Stopwatch.StartNew();while((Field<Course?>("recording")!=null||Field<bool>("busy"))&&stopWait.ElapsedMilliseconds<10000)Pump();Check(Field<Course?>("recording")==null,"结束录音后停止采集并保留录音");
            Invoke("ShowCourse",course);window.Width=920;window.Height=660;Wait(150);Snapshot(window,"artifacts/ui-refactor/07-small-window.png");
            Check(Field<Grid>("ClassroomPane").ActualWidth>=200&&Field<Border>("EnhancedPane").ActualWidth>=200,"小窗口保留两栏最小阅读宽度");
            window.Width=1280;window.Height=860;Invoke("ShowCourse",course);Wait(150);
            if(keepOpen)Dispatcher.Run();else Invoke("Exit");
        }catch(Exception ex){error=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error!=null)throw new Exception("Design review failed",error);
    }
    public static void Preview()
    {
        var thread=new Thread(()=>{
            Environment.SetEnvironmentVariable("LINGNOTE_DATA",Path.Combine(AppContext.BaseDirectory,"preview-data"));Paths.Init();
            var app=CreateApplication();
            bool paused=false;FloatingBar? bar=null;
            bar=new FloatingBar(new Preferences(),()=>{paused=!paused;bar!.Update(12,0,paused);},()=>app.Shutdown(),()=>{});
            bar.ShowInTaskbar=true;bar.ShowActivated=true;bar.Show();bar.Activate();bar.Update(12,.1f,false);bar.SetCompact(true);
            Dispatcher.Run();
        });thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
    }
    private static Application CreateApplication()
    {
        var xml=System.Xml.Linq.XDocument.Load("src/LingNote/App.xaml");System.Xml.Linq.XNamespace ns="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var root=new System.Xml.Linq.XElement(ns+"ResourceDictionary",new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns+"x","http://schemas.microsoft.com/winfx/2006/xaml"),xml.Root!.Element(ns+"Application.Resources")!.Elements());
        return new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown,Resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Parse(root.ToString().Replace("pack://application:,,,/Assets/","pack://application:,,,/LingNote;component/Assets/"))};
    }
    public static void Run()
    {
        Exception? error=null;
        var thread=new Thread(()=> {try {CheckWindow();}catch(Exception ex){error=ex;}});
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(error!=null)throw new Exception("UI regression failed",error);
    }
    private static void CheckWindow()
    {
        Environment.SetEnvironmentVariable("LINGNOTE_DATA",Path.Combine(AppContext.BaseDirectory,"ui-test-data",Guid.NewGuid().ToString("N")));
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        Paths.Init();var prefs=new Preferences();var section=new LibrarySection {Name="测试分区"};prefs.Sections.Add(section);
        for(int i=1;i<=6;i++){prefs.Topics.Add("主题"+i);prefs.Topics.Add("分区主题"+i);prefs.TopicSections["分区主题"+i]=section.Id;}
        prefs.Save();var store=new Store();
        for(int i=1;i<=16;i++)store.Save(new Course {Title="未分区录音"+i,Note="# 历史笔记"});
        for(int i=1;i<=7;i++){store.Save(new Course {Title="主题内录音"+i,Topic="主题1"});store.Save(new Course {Title="分区录音"+i,SectionId=section.Id});}
        var app=CreateApplication();var window=new MainWindow();window.Show();Pump();
        T Field<T>(string name)=>(T)typeof(MainWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.GetValue(window)!;
        void Invoke(string name){var method=typeof(MainWindow).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!;method.Invoke(window,method.GetParameters().Length==0?null:[window,new RoutedEventArgs()]);}
        void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS: "+label);}
        void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();}
        window.WindowState=WindowState.Maximized;Wait(250);
        var screen=System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(window).Handle).WorkingArea;
        var topLeft=window.PointToScreen(new Point(0,0));var bottomRight=window.PointToScreen(new Point(window.ActualWidth,window.ActualHeight));
        Check(topLeft.X>=screen.Left-2&&topLeft.Y>=screen.Top-2&&bottomRight.X<=screen.Right+2&&bottomRight.Y<=screen.Bottom+2,"最大化窗口保持在当前显示器工作区内，不覆盖任务栏");
        window.WindowState=WindowState.Normal;Wait(150);Check(window.WindowState==WindowState.Normal&&window.ActualWidth<screen.Width,"最大化后可正常还原窗口");
        var history=Field<ListBox>("History");
        Check(history.Items.Count==15,"未分区默认只显示15条");Click(Field<Button>("UnassignedExpand"));Check(history.Items.Count==16,"展开未分区显示所有记录");
        Click(Field<Button>("UnassignedExpand"));Check(history.Items.Count==15,"未分区可以重新收起");
        Invoke("UnassignedToggle_Click");Check(Field<Button>("UnassignedExpand").Visibility==Visibility.Collapsed,"折叠未分区时隐藏展开入口");Invoke("UnassignedToggle_Click");
        var topics=Field<StackPanel>("EmptyTopics");
        Check(topics.Children.OfType<DockPanel>().Count()==5,"主题列表默认显示5个主题");
        Check(topics.Children.OfType<Border>().Count()==5,"主题内默认显示5条录音");
        Click(topics.Children.OfType<Button>().First());Check(topics.Children.OfType<Border>().Count()==7,"主题内录音可展开");
        Click(topics.Children.OfType<Button>().Last());Check(topics.Children.OfType<DockPanel>().Count()==6,"主题列表可展开");
        var sectionContent=Field<StackPanel>("CustomSections").Children.OfType<StackPanel>().Single();
        Check(sectionContent.Children.OfType<DockPanel>().Count()==5&&sectionContent.Children.OfType<Border>().Count()==5,"自定义分区主题及录音各显示5条");
        Click(sectionContent.Children.OfType<Button>().Last());sectionContent=Field<StackPanel>("CustomSections").Children.OfType<StackPanel>().Single();
        Check(sectionContent.Children.OfType<Border>().Count()==7,"自定义分区录音可展开");
        Invoke("TopicsToggle_Click");history.BringIntoView();Wait(150);
        VirtualizingPanel.SetIsVirtualizing(history,false);history.ScrollIntoView(history.Items[0]);history.UpdateLayout();window.UpdateLayout();Pump();
        var item=(ListBoxItem)history.ItemContainerGenerator.ContainerFromIndex(0);item.ApplyTemplate();item.UpdateLayout();Wait(100);var title=Visuals<TextBlock>(item).First(t=>t.Text==((Course)history.Items[0]).Title);
        var dock=Visuals<DockPanel>(item).Last();var titleCenter=title.TranslatePoint(new Point(0,title.ActualHeight/2),dock).Y;
        Check(Math.Abs(titleCenter-dock.ActualHeight/2)<1,"录音标题在行内垂直居中");
        Check(Visuals<Button>(item).Single().Visibility==Visibility.Hidden,"录音操作按钮默认隐藏");
        Directory.CreateDirectory("artifacts");Snapshot(window,"artifacts/sidebar-ui-check.png");
        var settings=new SettingsView(prefs){Owner=window};var settingsShell=new Window {Content=settings,Width=1100,Height=780};settingsShell.Show();Pump();Visuals<Button>(settings).Single(b=>b.Tag?.ToString()=="AI 连接").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();Snapshot(settingsShell,"artifacts/settings-ui-check.png");var updates=Visuals<UpdatePanel>(settings).Single();updates.BringIntoView();Wait(120);Check(Visuals<TextBlock>(updates).Any(t=>t.Text=="当前版本 "+UpdateService.DisplayVersion)&&Visuals<Button>(updates).Any(b=>b.Content?.ToString()=="检查更新"),"设置展示当前版本及检查更新入口");Snapshot(settingsShell,"artifacts/update-settings-ui-check.png");Visuals<Button>(settings).Single(b=>b.Tag?.ToString()=="常规").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();var thinking=(ComboBox)typeof(SettingsView).GetField("thinking",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(settings)!;var protocol=(ComboBox)typeof(SettingsView).GetField("thinkingProtocol",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(settings)!;Check(!protocol.IsVisible&&!Visuals<TextBlock>(settings).Any(t=>t.Text=="思考参数协议")&&Visuals<TextBlock>(settings).Any(t=>t.Text=="笔记模式"),"高级设置隐藏思考参数协议并保留笔记模式");var mode=Visuals<ComboBox>(settings).Single(c=>c.Items.Contains("精编模式 · 录音后自动生成"));Check(mode.SelectedIndex==0,"默认先生成课堂笔记");mode.SelectedIndex=1;var addressBox=(TextBox)typeof(SettingsView).GetField("address",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(settings)!;var modelBox=(TextBox)typeof(SettingsView).GetField("model",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(settings)!;addressBox.Text="https://api.openai.com/v1";modelBox.Text="gpt-test";Check(thinking.Items.Contains("Medium")&&thinking.Items.Contains("XHigh"),"OpenAI 思考强度提供对应选项");addressBox.Text="https://generativelanguage.googleapis.com/v1beta/openai/";modelBox.Text="gemini-3-flash";Check(thinking.Items.Contains("Medium")&&!thinking.Items.Contains("Off")&&!thinking.Items.Contains("Max"),"Gemini 显示当前兼容接口可用的思考强度");addressBox.Text=prefs.BaseUrl;modelBox.Text=prefs.Model;Check(thinking.Items.Cast<object>().Select(x=>x.ToString()).SequenceEqual(new[]{"Off","Low","High","Max"})&&thinking.SelectedIndex==2,"设置按 Off、Low、High、Max 显示并保留默认强度");protocol.SelectedIndex=2;Check(thinking.IsEnabled&&thinking.Items.Cast<object>().Select(x=>x.ToString()).SequenceEqual(new[]{"Off","On"}),"思考开关协议仅显示 Off 和 On");thinking.SelectedIndex=0;settings.Close();settingsShell.Close();Check(Preferences.Load().ThinkingMode=="none"&&Preferences.Load().ThinkingProtocol=="toggle"&&Preferences.Load().GenerationMode=="direct","关闭设置后思考协议、模式和笔记模式持久保存");prefs.ThinkingMode="default";prefs.ThinkingProtocol="auto";prefs.GenerationMode="classroom";prefs.SaveApi();
        var activePrefs=Field<Preferences>("prefs");activePrefs.GenerationMode="direct";activePrefs.SaveApi();
        Invoke("NewUnassigned_Click");Invoke("Start_Click");Pump();Check(Field<Course?>("recording")!=null,"测试录音成功开始");
        var active=Field<Course>("recording");Check(active.DirectRefinement&&store.All().Single(c=>c.Id==active.Id).DirectRefinement,"新录音保存所选精编模式");activePrefs.GenerationMode="classroom";activePrefs.SaveApi();Check(active.DirectRefinement,"切换设置不改变已开始录音的模式");
        var audio=Field<AudioRecorder>("recorder");var before=audio.Elapsed;
        Invoke("Settings_Click");Wait(250);Check(Field<ContentControl>("SettingsHost").IsVisible&&Field<Border>("Sidebar").Visibility==Visibility.Collapsed&&audio.Elapsed>before&&Field<Course>("recording").Id==active.Id,"录音期间进入设置替换侧栏且真实录音继续增长");Invoke("LeaveSettings");Check(Field<Course>("recording").Id==active.Id&&!Field<bool>("paused"),"返回设置不改变当前录音及暂停状态");
        var old=history.Items.Cast<Course>().First(c=>c.Id!=active.Id);history.SelectedItem=old;Pump();
        Check(history.IsEnabled&&Field<Course>("selected").Id==old.Id&&Field<TextBox>("Editor").Text==old.Note,"录音期间可选择并查看未分区历史笔记");
        Check(Field<Course>("recording").Id==active.Id&&!Field<bool>("paused")&&Field<TextBlock>("StatusLabel").Text=="录音中","切换历史记录保留原录音和录音状态");
        Invoke("TranscriptTab_Click");Check(Field<bool>("transcriptView"),"录音期间可切换历史记录的转写页");
        var topicRow=topics.Children.OfType<Border>().First();Click(Visuals<Button>(topicRow).Last());
        Check(Field<Course>("selected").Topic=="主题1"&&Field<Course>("recording").Id==active.Id,"录音期间可查看主题内记录");
        sectionContent=Field<StackPanel>("CustomSections").Children.OfType<StackPanel>().Single();
        Click(Visuals<Button>(sectionContent.Children.OfType<Border>().First()).Last());
        Check(Field<Course>("selected").SectionId==section.Id,"录音期间可查看自定义分区记录");
        var elapsed=System.Diagnostics.Stopwatch.StartNew();while(elapsed.ElapsedMilliseconds<350)Pump();
        Check(audio.Elapsed>before&&!Field<bool>("paused"),"查看其他记录时录音计时持续增长且未暂停");
        Check(store.All().First(c=>c.Id==old.Id).Note==old.Note,"浏览不会把录音内容写入历史笔记");
        Snapshot(window,"artifacts/recording-browse-ui-check.png");
        window.WindowState=WindowState.Minimized;Pump();Invoke("Stop_Click");Pump();
        Check(window.IsVisible&&window.WindowState!=WindowState.Minimized,"最小化后结束录音立即恢复主窗口");
        while(Field<bool>("busy"))Pump();
        Check(Field<Course>("selected").Id==active.Id&&store.All().First(c=>c.Id==old.Id).Note==old.Note,"结束后返回本次录音且历史笔记不被处理结果覆盖");
        Check(Field<Course?>("recording")==null,"结束录音完成并保存");
        var indicator=Field<ProcessingIndicator>("ProcessingHint");
        var setBusy=typeof(MainWindow).GetMethod("SetBusy",BindingFlags.Instance|BindingFlags.NonPublic)!;
        setBusy.Invoke(window,[true]);Pump();
        Check(indicator.IsRunning&&indicator.Visibility==Visibility.Visible&&Field<TextBlock>("Hint").Visibility==Visibility.Collapsed,"处理期间显示幽灵提示并替换普通提示文字");
        var loadingGhost=Visuals<GhostMascot>(indicator).Single();Check(loadingGhost.IsBlinking&&loadingGhost.IsFloating,"处理幽灵同时飘动和眨眼");
        var dotText=Visuals<TextBlock>(indicator).Last();Wait(420);Check(dotText.Text==".","处理省略号逐个出现");
        Snapshot(window,"artifacts/processing-ghost-ui-check.png");
        setBusy.Invoke(window,[false]);Pump();Check(!indicator.IsRunning&&indicator.Visibility==Visibility.Collapsed&&!loadingGhost.IsBlinking,"处理结束隐藏提示并停止动画");
        var editable=new Course {Title="可编辑笔记",Note="# 网络单位\n\n| 语境 | K | M |\n|---|---|---|\n| 计算机网络 | 10³ | 10⁶ |\n\n公式：\\(1.3\\text{ MBPS}=1.3\\times10^{6}\\text{ bps}\\)\n\n- **保留要点**\n\n```python\nx = 10 ** 3\n```"};store.Save(editable);
        void Show(Course course){typeof(MainWindow).GetMethod("ShowCourse",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[course]);Pump();}
        typeof(MainWindow).GetMethod("RefreshHistory",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[null]);
        Show(editable);var titleBox=Field<TextBox>("TitleBox");titleBox.Focus();titleBox.Text="实时修改的标题";titleBox.CaretIndex=titleBox.Text.Length;Pump();
        var sidebarCopy=history.Items.Cast<Course>().First(c=>c.Id==editable.Id);
        Check(sidebarCopy.Title=="实时修改的标题"&&titleBox.IsKeyboardFocused&&titleBox.CaretIndex==titleBox.Text.Length,"输入标题时侧边栏立即同步且不打断输入");
        typeof(MainWindow).GetMethod("RefreshHistory",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[editable.Id]);Pump();
        Check(Field<Course>("selected").Title=="实时修改的标题"&&store.All().Single(c=>c.Id==editable.Id).Title=="实时修改的标题","刷新侧边栏时保存并保留正在编辑的标题");
        titleBox.Text="   ";Invoke("Title_Changed");Check(titleBox.Text=="实时修改的标题"&&store.All().Single(c=>c.Id==editable.Id).Title=="实时修改的标题","空标题恢复上一个有效名称");
        var sectionCourse=store.All().First(c=>c.Topic=="主题1");Show(sectionCourse);var topicRows=Field<Dictionary<string,Border>>("topicRecordRows");
        titleBox.Text="主题中的实时标题";Pump();var titleRow=topicRows[sectionCourse.Id];Check(((Course)titleRow.Tag).Title=="主题中的实时标题"&&Visuals<TextBlock>(titleRow).Any(t=>t.Text=="主题中的实时标题"),"主题与分区中的侧边栏标题同步更新");
        Show(editable);Check(store.All().Single(c=>c.Id==sectionCourse.Id).Title=="主题中的实时标题","切换记录时保存标题更改");
        var rich=Field<RichTextBox>("Preview");
        Check(Field<CheckBox>("EditToggle").Visibility==Visibility.Collapsed&&Field<CheckBox>("EditToggle").IsChecked!=true&&!rich.IsReadOnly,"源码选项隐藏且可直接编辑格式化笔记");
        Check(rich.Document.Blocks.OfType<System.Windows.Documents.Table>().Count()==1,"Markdown 表格显示为原生可编辑表格");
        var readable=new TextRange(rich.Document.ContentStart,rich.Document.ContentEnd).Text;
        Check(readable.Contains("1.3×10⁶ bps")&&!readable.Contains(@"\text")&&!readable.Contains(@"\times"),"已有笔记中的单位和幂次公式可读显示");
        rich.Document.Blocks.Add(new Paragraph(new Run("直接追加的笔记")));Wait(800);
        var edited=store.All().Single(c=>c.Id==editable.Id).Note;
        Check(edited.Contains("直接追加的笔记")&&edited.Contains("| --- |")&&edited.Contains("**保留要点**")&&edited.Contains("```python"),"直接编辑自动保存并保留表格、加粗与代码块");
        Field<CheckBox>("EditToggle").IsChecked=true;Pump();
        Field<TextBox>("Editor").AppendText("\n\n源码补充");Field<CheckBox>("EditToggle").IsChecked=false;Pump();
        Check(new TextRange(rich.Document.ContentStart,rich.Document.ContentEnd).Text.Contains("源码补充"),"源码与格式化编辑切换保留修改");
        Show(active);Show(store.All().Single(c=>c.Id==editable.Id));
        Check(new TextRange(rich.Document.ContentStart,rich.Document.ContentEnd).Text.Contains("直接追加的笔记"),"切换录音后直接编辑内容仍保留");
        Check(Field<Button>("RegenerateButton").IsVisible&&Field<Button>("RegenerateButton").Content.ToString()=="重新整理课堂笔记","笔记工具栏提供重新整理按钮");
        Snapshot(window,"artifacts/editable-notes-ui-check.png");

        // Hold the actual processing queue, so checks cover a genuinely pending
        // job and overlapping captures without a real model or paid AI request.
        var gate=Field<SemaphoreSlim>("processingGate");gate.Wait();
        var run=typeof(MainWindow).GetMethod("RunProcessing",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var pending=(Task)run.Invoke(window,[Field<Course>("selected"),true,null,null])!;Pump();
        Invoke("Settings_Click");Pump();Check(Field<ContentControl>("SettingsHost").IsVisible&&!pending.IsCompleted&&Field<bool>("busy"),"真实排队任务在设置切换期间保持运行");Invoke("LeaveSettings");
        Check(Field<bool>("busy")&&Field<Button>("GroupButton").IsEnabled,"处理任务期间未分区收起展开按钮可用");
        var oldTopics=Field<ScrollViewer>("TopicsScroll").Visibility;Click(Field<Button>("TopicsHeader"));
        Check(Field<ScrollViewer>("TopicsScroll").Visibility!=oldTopics,"处理期间主题可以收起展开");Click(Field<Button>("TopicsHeader"));
        var oldUnassigned=history.Visibility;Click(Field<Button>("GroupButton"));
        Check(history.Visibility!=oldUnassigned,"处理期间未分区可以收起展开");Click(Field<Button>("GroupButton"));
        var sectionHeader=Field<StackPanel>("CustomSections").Children.OfType<DockPanel>().Single();
        Click(sectionHeader.Children.OfType<Button>().Last());
        Check(Field<StackPanel>("CustomSections").Children.OfType<StackPanel>().Single().Visibility==Visibility.Collapsed,"处理期间自定义分区可以折叠");Click(sectionHeader.Children.OfType<Button>().Last());
        Invoke("NewUnassigned_Click");Check(Field<Course?>("selected")==null&&Field<Button>("StartButton").IsEnabled,"处理期间可新建录音并显示可用的开始按钮");
        Invoke("Start_Click");Wait(250);var second=Field<Course>("recording");
        Check(Field<bool>("busy")&&second.Id!=editable.Id,"上一段处理期间可以开始另一段真实录音");
        Invoke("Stop_Click");Pump();
        var pendingCourses=Field<Dictionary<string,Course>>("processingCourses");
        Check(pendingCourses.ContainsKey(second.Id)&&pendingCourses.ContainsKey(editable.Id),"结束第二段录音后独立排队，不覆盖上一段处理任务");
        Invoke("NewUnassigned_Click");Invoke("Start_Click");Wait(250);var third=Field<Course>("recording");
        Show(store.All().Single(c=>c.Id==editable.Id));Invoke("Cancel_Click");Pump();
        Check(Field<Course>("recording").Id==third.Id&&!Field<bool>("paused"),"取消旧处理任务不会取消或暂停当前新录音");
        Show(second);Invoke("Cancel_Click");gate.Release();
        var end=(Task)typeof(MainWindow).GetMethod("EndWithoutSummary",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null)!;
        var deadline=System.Diagnostics.Stopwatch.StartNew();while((!pending.IsCompleted||!end.IsCompleted||Field<bool>("busy"))&&deadline.ElapsedMilliseconds<10000)Pump();
        Check(pending.IsCompleted&&end.IsCompleted&&!Field<bool>("busy"),"取消排队处理和结束录音后任务正常清理");
        Check(store.All().Single(c=>c.Id==editable.Id).Note.Contains("源码补充")&&second.Parts.All(p=>p.File.StartsWith(second.Folder))&&third.Parts.All(p=>p.File.StartsWith(third.Folder)),"并行工作期间各段录音文件和原有笔记互不串写");

        var listener=new System.Net.HttpListener();
        var reserve=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);reserve.Start();var port=((System.Net.IPEndPoint)reserve.LocalEndpoint).Port;reserve.Stop();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");listener.Start();
        var apiPrefs=Field<Preferences>("prefs");apiPrefs.BaseUrl=$"http://127.0.0.1:{port}/v1";apiPrefs.Model="test";apiPrefs.SetKey("ui-test-placeholder");apiPrefs.Stream=false;apiPrefs.SaveApi();
        var requestReady=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var releaseResponse=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int calls=0;string directSystem="",directUser="";var directPreviewReady=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var finishDirect=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server=Task.Run(async()=>{for(int i=0;i<6;i++){
            var context=await listener.GetContextAsync();Interlocked.Increment(ref calls);
            using(var reader=new StreamReader(context.Request.InputStream)){var body=await reader.ReadToEndAsync();if(i>=4){using var json=System.Text.Json.JsonDocument.Parse(body);var messages=json.RootElement.GetProperty("messages");directSystem=messages[0].GetProperty("content").GetString()!;directUser=messages[1].GetProperty("content").GetString()!;}}
            if(i==1){requestReady.SetResult();await releaseResponse.Task.WaitAsync(TimeSpan.FromSeconds(10));}
            if(i==5){
                context.Response.ContentType="text/event-stream";context.Response.SendChunked=true;
                foreach(var part in new[]{"## 重新整理结果\n\n","继续"}){await Task.Delay(350);var line=System.Text.Json.JsonSerializer.Serialize(new {choices=new[]{new {delta=new {content=part}}}});await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("data: "+line+"\n\n"));await context.Response.OutputStream.FlushAsync();}
                directPreviewReady.SetResult();await finishDirect.Task.WaitAsync(TimeSpan.FromSeconds(10));await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("data: [DONE]\n\n"));context.Response.Close();continue;
            }
            var payload=System.Text.Encoding.UTF8.GetBytes(i==0||i==4?"{}":"{\"choices\":[{\"message\":{\"content\":\"## 重新整理结果\\n\\n| 单位 | 数值 |\\n|---|---|\\n| M | 10⁶ |\"},\"finish_reason\":\"stop\"}]}");
            context.Response.StatusCode=i==0||i==4?503:200;context.Response.ContentType="application/json";context.Response.ContentLength64=payload.Length;await context.Response.OutputStream.WriteAsync(payload);context.Response.Close();
        }});
        editable=store.All().Single(c=>c.Id==editable.Id);editable.Parts.Add(new AudioPart {Text="课程原始转写：网络 M 表示 10 的六次方。",Done=true});store.Save(editable);Show(editable);var preserved=editable.Note;
        Click(Field<Button>("RegenerateButton"));deadline.Restart();while(Field<bool>("busy")&&deadline.ElapsedMilliseconds<10000)Pump();
        Check(calls==1&&!Field<bool>("busy")&&store.All().Single(c=>c.Id==editable.Id).Note==preserved,"生成失败保留原笔记，允许重试");
        Click(Field<Button>("RegenerateButton"));deadline.Restart();while(!requestReady.Task.IsCompleted&&deadline.ElapsedMilliseconds<10000)Pump();
        Check(requestReady.Task.IsCompleted,"重新整理按钮重试发起新的 AI 请求");
        Show(store.All().Single(c=>c.Id==old.Id));rich.Document.Blocks.Add(new Paragraph(new Run("后台处理时修改其他笔记")));Invoke("SaveEditor");
        releaseResponse.SetResult();deadline.Restart();while(Field<bool>("busy")&&deadline.ElapsedMilliseconds<10000)Pump();
        var regenerated=store.All().Single(c=>c.Id==editable.Id);
        Check(!Field<bool>("busy")&&calls==2&&regenerated.Note.Contains("重新整理结果")&&regenerated.Versions.Contains(preserved),"失败后按钮重试只发一次生成请求，成功替换结果并保存旧版本");
        Check(Field<Course>("selected").Id==old.Id&&store.All().Single(c=>c.Id==old.Id).Note.Contains("后台处理时修改其他笔记"),"AI 完成后不抢回当前页面且不覆盖其他笔记的修改");
        Show(regenerated);Click(Field<Button>("EnhanceButton"));deadline.Restart();while(Field<bool>("busy")&&deadline.ElapsedMilliseconds<10000)Pump();
        var enhanced=store.All().Single(c=>c.Id==editable.Id);
        Check(calls==3&&!string.IsNullOrWhiteSpace(enhanced.EnhancedNote)&&enhanced.Note==regenerated.Note,"右侧单独生成完善笔记并保留左侧课堂笔记");
        var right=Field<RichTextBox>("EnhancedPreview");right.Document.Blocks.Add(new Paragraph(new Run("右栏独立修改")));Wait(800);
        Check(store.All().Single(c=>c.Id==editable.Id).EnhancedNote.Contains("右栏独立修改")&&!store.All().Single(c=>c.Id==editable.Id).Note.Contains("右栏独立修改"),"两栏直接编辑分别自动保存且互不覆盖");
        Snapshot(window,"artifacts/dual-notes-ui-check.png");
        var automatic=new Course {Title="自动双版本"};automatic.Parts.Add(new AudioPart {Text="计算机网络性能指标包括速率和带宽。",Done=true});store.Save(automatic);Show(automatic);
        var autoTask=(Task)run.Invoke(window,[automatic,false,null,null])!;deadline.Restart();while(!autoTask.IsCompleted&&deadline.ElapsedMilliseconds<10000)Pump();
        var automaticSaved=store.All().Single(c=>c.Id==automatic.Id);
        Check(autoTask.IsCompleted&&calls==4&&!string.IsNullOrWhiteSpace(automaticSaved.Note)&&string.IsNullOrWhiteSpace(automaticSaved.EnhancedNote)&&automaticSaved.State=="已完成","录音处理默认只生成课堂笔记，不自动请求完善笔记");
        Check(Field<Border>("EnhancedPane").Visibility==Visibility.Visible&&Grid.GetColumnSpan(Field<Grid>("ClassroomPane"))==1,"课堂笔记完成后双栏展示，右侧按需生成完善笔记");
        apiPrefs.Stream=true;apiPrefs.SaveApi();
        var directRecord=new Course {Title="直接精编测试",DirectRefinement=true};directRecord.Parts.Add(new AudioPart {Text="网络时延的定义和传播条件。",Done=true});store.Save(directRecord);Show(directRecord);
        var directTask=(Task)run.Invoke(window,[directRecord,false,null,null])!;deadline.Restart();while(!directTask.IsCompleted&&deadline.ElapsedMilliseconds<10000)Pump();
        var directSaved=store.All().Single(c=>c.Id==directRecord.Id);Show(directSaved);
        Check(directTask.IsCompleted&&calls==5&&directSaved.State=="待重试"&&string.IsNullOrEmpty(directSaved.Note)&&directSaved.Transcript.Contains("网络时延"),"直接精编失败保留转写并允许重试");
        Click(Field<Button>("RegenerateButton"));deadline.Restart();while((!directPreviewReady.Task.IsCompleted||!Field<TextBox>("Editor").Text.Contains("继续"))&&deadline.ElapsedMilliseconds<10000)Pump();
        var liveDirect=Field<TextBox>("Editor").Text;Check(liveDirect.Contains("重新整理结果")&&liveDirect.Contains("继续")&&liveDirect.Split("重新整理结果").Length==2,"直接精编流式预览使用最新快照，不重复叠加");
        finishDirect.SetResult();deadline.Restart();while(Field<bool>("busy")&&deadline.ElapsedMilliseconds<10000)Pump();
        directSaved=store.All().Single(c=>c.Id==directRecord.Id);Show(directSaved);
        Check(calls==6&&string.IsNullOrWhiteSpace(directSaved.Note)&&directSaved.EnhancedNote.Contains("重新整理结果")&&directSaved.DirectRefinement,"直接模式重试只生成精编笔记并保留转写");
        Check(directSystem.Contains("【输入类型：录音转写】")&&directUser.Contains("<录音转写>")&&directUser.Contains("网络时延")&&!directUser.Contains("<课堂笔记>"),"直接模式请求发送转写线索和独立主题整理规则");
        Check(Field<Button>("NoteTab").Content.ToString()=="笔记"&&Field<Border>("EnhancedPane").Visibility==Visibility.Collapsed&&Field<Button>("OpenEnhanceButton").Visibility==Visibility.Collapsed,"直接模式以单栏精编笔记显示，不出现空课堂栏");
        rich.Document.Blocks.Add(new Paragraph(new Run("直接精编编辑")));Invoke("SaveEditor");Check(store.All().Single(c=>c.Id==directRecord.Id).EnhancedNote.Contains("直接精编编辑")&&string.IsNullOrEmpty(store.All().Single(c=>c.Id==directRecord.Id).Note),"直接模式编辑写入精编笔记而非课堂笔记");
        Check(server.IsCompletedSuccessfully,"本机模拟 AI 服务正常结束，无真实 API 请求");
        listener.Close();
        var parallelListener=new System.Net.HttpListener();var reserveParallel=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);reserveParallel.Start();var parallelPort=((System.Net.IPEndPoint)reserveParallel.LocalEndpoint).Port;reserveParallel.Stop();parallelListener.Prefixes.Add($"http://127.0.0.1:{parallelPort}/");parallelListener.Start();
        apiPrefs.BaseUrl=$"http://127.0.0.1:{parallelPort}/v1";apiPrefs.Stream=true;apiPrefs.SaveApi();
        var bothReady=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var finishBoth=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int concurrent=0;
        var parallelServer=Task.Run(async()=>{var responders=new List<Task>();for(int i=0;i<2;i++){var context=await parallelListener.GetContextAsync();responders.Add(Task.Run(async()=>{
            using(var reader=new StreamReader(context.Request.InputStream))await reader.ReadToEndAsync();
            context.Response.ContentType="text/event-stream";context.Response.SendChunked=true;
            await Task.Delay(350);
            var partial=System.Text.Json.JsonSerializer.Serialize(new {choices=new[]{new {delta=new {content="完善结果：实时草稿"}}}});
            await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("data: "+partial+"\n\n"));await context.Response.OutputStream.FlushAsync();
            if(Interlocked.Increment(ref concurrent)==2)bothReady.SetResult();await finishBoth.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("data: [DONE]\n\n"));context.Response.Close();
        }));}await Task.WhenAll(responders);});
        Show(regenerated);Click(Field<Button>("EnhanceButton"));Show(automaticSaved);Click(Field<Button>("OpenEnhanceButton"));
        deadline.Restart();while(!bothReady.Task.IsCompleted&&deadline.ElapsedMilliseconds<10000)Pump();
        Check(bothReady.Task.IsCompleted&&pendingCourses.Count==2,"两个记录的完善请求同时到达服务，互不排队阻塞");
        Check(Field<Border>("EnhancedPane").IsVisible,"用户选择精编后展开双栏布局");
        Check(Field<Button>("EnhanceButton").Content.ToString()!.Contains("精编笔记")&&Field<Button>("OpenEnhanceButton").Content.ToString()=="精编笔记","两栏入口和操作使用精编笔记名称");
        var splitter=Field<GridSplitter>("NoteSplitter");var left=Field<Grid>("ClassroomPane");var widthBeforeDrag=left.ActualWidth;
        splitter.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0,0){RoutedEvent=System.Windows.Controls.Primitives.Thumb.DragStartedEvent});
        splitter.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(60,0){RoutedEvent=System.Windows.Controls.Primitives.Thumb.DragDeltaEvent});
        splitter.RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(60,0,false){RoutedEvent=System.Windows.Controls.Primitives.Thumb.DragCompletedEvent});
        Pump();Check(splitter.IsVisible&&left.ActualWidth>widthBeforeDrag+10,"双栏分隔条可拖动并调整笔记宽度");
        deadline.Restart();while(!Field<TextBox>("EnhancedLive").Text.Contains("实时草稿")&&deadline.ElapsedMilliseconds<5000)Pump();
        Check(Field<TextBox>("EnhancedLive").IsVisible&&Field<TextBox>("EnhancedLive").Text.Contains("实时草稿")&&!Visuals<TextBlock>(Field<Border>("EnhancedPane")).Any(t=>t.Text.Contains("完成后保存")||t.Text.Contains("最多两段并行")),"实时显示草稿且不显示多余状态提示");
        Check(string.IsNullOrEmpty(store.All().Single(c=>c.Id==automaticSaved.Id).EnhancedNote),"生成中的实时草稿不写入正式完善笔记");
        var longNote=new Course {Title="滚轮测试",Note=string.Join("\n\n",Enumerable.Repeat("长段落用于滚动检查。",150)),EnhancedNote=string.Join("\n\n",Enumerable.Repeat("完善内容用于滚动检查。",150))};store.Save(longNote);Show(longNote);Wait(150);
        var hit=window.InputHitTest(rich.TranslatePoint(new Point(40,140),window)) as DependencyObject;
        while(hit is FrameworkContentElement content)hit=content.Parent;
        Check(hit!=null&&(hit==rich||rich.IsAncestorOf(hit)),"左栏正文的实际鼠标命中落在编辑区，标题不会覆盖拦截滚轮");
        foreach(var pane in new[]{rich,right}){pane.UpdateLayout();var bar=Visuals<System.Windows.Controls.Primitives.ScrollBar>(pane).First(b=>b.Orientation==Orientation.Vertical&&b.IsVisible);Check(Math.Abs(bar.ActualWidth-8)<1&&Visuals<System.Windows.Controls.Primitives.Thumb>(bar).Any(),"笔记滚动条宽度为 8 且保留可拖动滑块");var scroll=Visuals<ScrollViewer>(pane).First();pane.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,-120){RoutedEvent=System.Windows.Input.Mouse.PreviewMouseWheelEvent});Wait(80);Check(scroll.VerticalOffset>0,"后台完善期间笔记区域鼠标滚轮可以滚动");var offsetBeforeThumb=scroll.VerticalOffset;var thumb=Visuals<System.Windows.Controls.Primitives.Thumb>(bar).First();thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0,0){RoutedEvent=System.Windows.Controls.Primitives.Thumb.DragStartedEvent});thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(0,25){RoutedEvent=System.Windows.Controls.Primitives.Thumb.DragDeltaEvent});thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(0,25,false){RoutedEvent=System.Windows.Controls.Primitives.Thumb.DragCompletedEvent});Wait(80);Check(scroll.VerticalOffset>offsetBeforeThumb,"细滚动条滑块可拖动正文");}
        Snapshot(window,"artifacts/thin-scrollbar-ui-check.png");
        finishBoth.SetResult();deadline.Restart();while(Field<bool>("busy")&&deadline.ElapsedMilliseconds<10000)Pump();
        Check(!Field<bool>("busy")&&Field<Course>("selected").Id==longNote.Id&&store.All().Single(c=>c.Id==automaticSaved.Id).EnhancedNote.Contains("完善结果"),"并发完善完成后分别保存结果且不抢回当前记录");
        parallelListener.Close();

        var floating=Field<FloatingBar>("floating");floating.Update(12,.1f,false);floating.Show();Pump();
        floating.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0){RoutedEvent=System.Windows.Input.Mouse.MouseEnterEvent});Wait(130);
        var collapse=Visuals<Button>(floating).Single(b=>b.ToolTip?.ToString()=="收起悬浮条");
        Check(collapse.Opacity>.9&&collapse.IsHitTestVisible,"悬浮条悬停显示圆形减号按钮");Snapshot(floating,"artifacts/floating-expanded-ui-check.png");
        var widths=new List<double>();var descriptor=System.ComponentModel.DependencyPropertyDescriptor.FromProperty(Window.WidthProperty,typeof(FloatingBar));EventHandler changed=(_,_)=>widths.Add(floating.Width);descriptor.AddValueChanged(floating,changed);
        floating.SetCompact(true);Wait(400);descriptor.RemoveValueChanged(floating,changed);Check(widths.Any(w=>w>64&&w<414),"收起动画宽度连续变化");
        Check(floating.IsCompact&&Math.Abs(floating.Width-64)<1,"悬浮条平滑收成圆形");Snapshot(floating,"artifacts/floating-compact-ui-check.png");
        var compactGhost=Visuals<GhostMascot>(floating).Single();Check(compactGhost.IsBlinking,"录音状态的小幽灵开启眨眼");
        var frames=new HashSet<string>();for(int i=0;i<70;i++){Wait(60);frames.Add(FrameHash(compactGhost));}
        Check(frames.Count>1,"小幽灵在实际绘制帧中发生眨眼变化");
        floating.Update(13,0,true);Wait(150);var pausedFrame=FrameHash(compactGhost);Wait(400);
        Check(!compactGhost.IsBlinking&&FrameHash(compactGhost)==pausedFrame,"暂停后幽灵停止眨眼并保持睁眼静止");Snapshot(floating,"artifacts/floating-paused-ui-check.png");
        floating.Update(14,.1f,false);Check(compactGhost.IsBlinking,"恢复录音后重新眨眼");
        floating.SetCompact(false);Wait(340);Check(!floating.IsCompact&&Math.Abs(floating.Width-414)<1,"圆形可展开恢复全部录音控制");floating.Hide();Invoke("Exit");
    }
    private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    private static void Wait(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    private static string FrameHash(FrameworkElement element){var bitmap=new RenderTargetBitmap((int)element.ActualWidth,(int)element.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(element);var bytes=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(bytes,bitmap.PixelWidth*4,0);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));}
    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T match)yield return match;foreach(var descendant in Visuals<T>(child))yield return descendant;}}
    private static void Snapshot(Window window,string path){window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);}
}
