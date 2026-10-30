using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LingNote;

internal static class SidebarUiChecks
{
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
        var settings=new SettingsWindow(prefs){Owner=window};settings.Show();Pump();Snapshot(settings,"artifacts/settings-ui-check.png");var updates=Visuals<UpdatePanel>(settings).Single();updates.BringIntoView();Wait(120);Check(Visuals<TextBlock>(updates).Any(t=>t.Text=="当前版本 "+UpdateService.DisplayVersion)&&Visuals<Button>(updates).Any(b=>b.Content?.ToString()=="检查更新"),"设置展示当前版本及检查更新入口");Snapshot(settings,"artifacts/update-settings-ui-check.png");settings.Close();
        Invoke("NewUnassigned_Click");Invoke("Start_Click");Pump();Check(Field<Course?>("recording")!=null,"测试录音成功开始");
        var active=Field<Course>("recording");var audio=Field<AudioRecorder>("recorder");var before=audio.Elapsed;
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
        var floating=Field<FloatingBar>("floating");floating.Update(12,.1f,false);floating.Show();Pump();
        floating.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0){RoutedEvent=System.Windows.Input.Mouse.MouseEnterEvent});Wait(130);
        var collapse=Visuals<Button>(floating).Single(b=>b.ToolTip?.ToString()=="收起悬浮条");
        Check(collapse.Opacity>.9&&collapse.IsHitTestVisible,"悬浮条悬停显示圆形减号按钮");Snapshot(floating,"artifacts/floating-expanded-ui-check.png");
        var widths=new List<double>();var descriptor=System.ComponentModel.DependencyPropertyDescriptor.FromProperty(Window.WidthProperty,typeof(FloatingBar));EventHandler changed=(_,_)=>widths.Add(floating.Width);descriptor.AddValueChanged(floating,changed);
        floating.SetCompact(true);Wait(400);descriptor.RemoveValueChanged(floating,changed);Check(widths.Any(w=>w>64&&w<330),"收起动画宽度连续变化");
        Check(floating.IsCompact&&Math.Abs(floating.Width-64)<1,"悬浮条平滑收成圆形");Snapshot(floating,"artifacts/floating-compact-ui-check.png");
        var compactGhost=Visuals<GhostMascot>(floating).Single();Check(compactGhost.IsBlinking,"录音状态的小幽灵开启眨眼");
        var frames=new HashSet<string>();for(int i=0;i<70;i++){Wait(60);frames.Add(FrameHash(compactGhost));}
        Check(frames.Count>1,"小幽灵在实际绘制帧中发生眨眼变化");
        floating.Update(13,0,true);Wait(150);var pausedFrame=FrameHash(compactGhost);Wait(400);
        Check(!compactGhost.IsBlinking&&FrameHash(compactGhost)==pausedFrame,"暂停后幽灵停止眨眼并保持睁眼静止");Snapshot(floating,"artifacts/floating-paused-ui-check.png");
        floating.Update(14,.1f,false);Check(compactGhost.IsBlinking,"恢复录音后重新眨眼");
        floating.SetCompact(false);Wait(340);Check(!floating.IsCompact&&Math.Abs(floating.Width-330)<1,"圆形可展开恢复全部录音控制");floating.Hide();Invoke("Exit");
    }
    private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    private static void Wait(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    private static string FrameHash(FrameworkElement element){var bitmap=new RenderTargetBitmap((int)element.ActualWidth,(int)element.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(element);var bytes=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(bytes,bitmap.PixelWidth*4,0);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));}
    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T match)yield return match;foreach(var descendant in Visuals<T>(child))yield return descendant;}}
    private static void Snapshot(Window window,string path){window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);}
}
