using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LingNote;
internal static class SettingsCaptureChecks
{
    public static void Run(bool keepOpen)
    {
        Exception? error=null;var thread=new Thread(()=>{try{Check(keepOpen);}catch(Exception ex){error=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error!=null)throw new Exception("Settings/capture regression",error);
    }
    static IEnumerable<T> Visuals<T>(DependencyObject root) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T t)yield return t;foreach(var descendant in Visuals<T>(child))yield return descendant;}}
    static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,()=>frame.Continue=false);Dispatcher.PushFrame(frame);}
    static void Snapshot(Window window,string name){window.UpdateLayout();Pump();var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);CaptureService.Save(image,Path.GetFullPath("artifacts/settings-capture/"+name));}
    static void Check(bool keepOpen)
    {
        Environment.SetEnvironmentVariable("LINGNOTE_DATA",Path.Combine(AppContext.BaseDirectory,"settings-review-data",Guid.NewGuid().ToString("N")));Paths.Init();SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var app=(Application)typeof(SidebarUiChecks).GetMethod("CreateApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;
        var prefs=new Preferences();prefs.Topics.Add("计算机网络");prefs.Save();var store=new Store();var course=new Course {Title="网络性能分析",Topic="计算机网络",Note="# 网络性能\n\n保留原笔记和主题展开状态。",Parts=[new AudioPart {Done=true,Text="网络性能介绍"}]};store.Save(course);
        var window=new MainWindow();window.Show();Pump();
        T Field<T>(string name)=>(T)typeof(MainWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.GetValue(window)!;
        object? Invoke(string name,params object?[] args)=>typeof(MainWindow).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,args);
        void Verify(bool condition,string label){if(!condition)throw new Exception(label);Console.WriteLine("PASS: "+label);}
        void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();}
        Invoke("ShowCourse",course);Snapshot(window,"01-main");var sidebar=Field<Border>("Sidebar");var history=Field<ListBox>("History");var selected=Field<Course>("selected");var topics=Field<StackPanel>("EmptyTopics");var count=topics.Children.Count;
        Invoke("Settings_Click",window,new RoutedEventArgs());Pump();var host=Field<ContentControl>("SettingsHost");
        Verify(sidebar.Visibility==Visibility.Collapsed&&Field<ColumnDefinition>("SidebarColumn").Width.Value==0&&Grid.GetColumn(host)==0&&Grid.GetColumnSpan(host)==2,"设置占据同一布局位置，主侧栏无空白占位");
        var nav=Visuals<Button>(host).Where(b=>b.Tag is string).ToList();Verify(nav.Count==8,"设置包含八个真实导航分类");Verify(Visuals<TextBlock>(host).Any(t=>t.Text=="录音与笔记"),"进入设置默认展示常规内容");Snapshot(window,"04-general");Click(nav.Single(b=>(string)b.Tag=="AI 连接"));Snapshot(window,"02-ai");
        Verify(!Visuals<TextBlock>(host).Any(t=>t.FontSize==26&&t.Text=="AI 连接"),"移除右侧重复分类标题");
        var rightEdges=Visuals<Button>(host).Where(b=>b.Content?.ToString() is "测试连接" or "导入模型" or "检查更新").Select(b=>b.TranslatePoint(new Point(b.ActualWidth,0),host).X).ToList();Verify(rightEdges.Max()-rightEdges.Min()<1,"API、模型导入及更新按钮右边缘统一对齐");
        foreach(var button in nav){Click(button);Verify(Visuals<TextBlock>(host).Any(t=>t.Text==button.Tag!.ToString()),"切换分类："+button.Tag);Snapshot(window,"page-"+button.Tag);}
        Click(nav.Single(b=>(string)b.Tag=="外观"));
        var themeChoices=Visuals<Button>(host).Where(b=>System.Windows.Automation.AutomationProperties.GetName(b).StartsWith("强调色：")).ToList();Verify(themeChoices.Count==12&&Visuals<Button>(host).Count(b=>System.Windows.Automation.AutomationProperties.GetName(b).StartsWith("页面底色："))==6,"外观提供12种强调色和6种背景色");
        Click(themeChoices.Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="强调色：紫色"));
        Verify(((SolidColorBrush)app.Resources["Accent"]).Color.ToString()=="#FF6D28D9"&&((SolidColorBrush)Field<Button>("StartButton").Background).Color.ToString()=="#FF6D28D9","强调色即时应用到主窗口按钮");
        Click(Visuals<Button>(host).Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="页面底色：暖米"));
        Verify(((SolidColorBrush)window.Background).Color.ToString()=="#FFF8F5EF"&&Preferences.Load().ThemeAccent=="#6D28D9"&&Preferences.Load().ThemeBackground=="#F8F5EF","背景即时生效且配色持久保存");Verify(Visuals<Border>(host).Where(b=>b.CornerRadius.TopLeft==14).All(b=>b.Background is SolidColorBrush fill&&fill.Color==Colors.White),"浅色背景下设置卡片仍保持白色");Snapshot(window,"appearance-colors");
        window.Width=920;window.Height=620;Pump();Snapshot(window,"appearance-compact");window.Width=1280;window.Height=860;Pump();
        Click(Visuals<Button>(host).Single(b=>b.Content?.ToString()=="恢复默认配色"));Verify(Preferences.Load().ThemeAccent=="#1263F1"&&Preferences.Load().ThemeBackground=="#FFFFFF","默认配色可以恢复并保存");
        foreach(var category in nav){Click(category);foreach(var button in Visuals<Button>(host).Where(b=>b.IsVisible&&b.Content is string)){
            var label=new System.Windows.Media.FormattedText((string)button.Content,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface(button.FontFamily,button.FontStyle,button.FontWeight,button.FontStretch),button.FontSize,Brushes.Black,VisualTreeHelper.GetDpi(button).PixelsPerDip);
            Verify(button.ActualHeight-button.Padding.Top-button.Padding.Bottom>=label.Height-1,"按钮文字有足够高度："+button.Content);
        }}
        Click(nav.Single(b=>(string)b.Tag=="AI 连接"));var search=Visuals<TextBox>(host).Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="搜索设置");search.Text="截屏";Pump();Verify(nav.Single(b=>(string)b.Tag=="存储与数据").Visibility==Visibility.Visible&&nav.Single(b=>(string)b.Tag=="AI 连接").Visibility==Visibility.Collapsed,"搜索按截屏关键词筛选并显示存储设置");search.Text="不存在的设置";Pump();Verify(Visuals<TextBlock>(host).Any(t=>t.Text=="没有找到相关设置"),"无搜索结果明确显示空态");search.Clear();Click(nav.Single(b=>(string)b.Tag=="AI 连接"));
        window.Width=920;window.Height=620;Pump();Snapshot(window,"03-compact");Verify(host.ActualWidth>880&&Visuals<ScrollViewer>(host).Any(v=>v.ScrollableHeight>0),"最小窗口尺寸设置内容可滚动且无双侧栏");window.Width=1280;window.Height=860;Pump();
        Invoke("LeaveSettings");Verify(sidebar.IsVisible&&ReferenceEquals(selected,Field<Course>("selected"))&&ReferenceEquals(topics,Field<StackPanel>("EmptyTopics"))&&topics.Children.Count==count,"返回恢复原笔记对象、主导航及主题展开状态");
        Invoke("SidebarToggle_Click",window,new RoutedEventArgs());Invoke("Settings_Click",window,new RoutedEventArgs());Invoke("LeaveSettings");Verify(sidebar.Visibility==Visibility.Collapsed&&Field<ColumnDefinition>("SidebarColumn").Width.Value==0,"返回保留进入前主侧栏收起状态");Invoke("SidebarToggle_Click",window,new RoutedEventArgs());
        var floating=Field<FloatingBar>("floating");floating.Show();Pump();var menuAnchor=Visuals<Button>(floating).Single(b=>b.ToolTip?.ToString()=="截屏选项");Click(menuAnchor);var popup=typeof(ActionPopup).GetField("popup",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var openPopups=PresentationSource.CurrentSources.Cast<PresentationSource>().Where(p=>p.RootVisual!=null).SelectMany(p=>Visuals<TextBlock>(p.RootVisual)).ToList();Verify(openPopups.Any(t=>t.Text=="截取窗口")&&openPopups.Any(t=>t.Text=="Ctrl + Shift + 1"),"截屏菜单名称及独立快捷键列正确显示");
        Click(menuAnchor);Pump();Verify(!PresentationSource.CurrentSources.Cast<PresentationSource>().Where(p=>p.RootVisual!=null).SelectMany(p=>Visuals<TextBlock>(p.RootVisual)).Any(t=>t.IsVisible&&t.Text=="Ctrl + Shift + 1"),"再次点击截屏下拉按钮收起菜单");floating.Hide();
        var bitmap=CaptureService.Grab(CaptureService.WindowBounds(new System.Windows.Interop.WindowInteropHelper(window).Handle));var captureFolder=Path.GetFullPath("artifacts/settings-capture/captured");var png=CaptureService.Save(bitmap,captureFolder);Verify(File.Exists(png)&&new BitmapImage(new Uri(png)).PixelWidth==bitmap.PixelWidth,"真实窗口截图保存为有效 PNG，像素尺寸一致");
        Verify(CaptureService.DefaultDirectory==Path.GetFullPath("download"),"默认截屏目录动态定位项目 download");prefs.ScreenshotDirectory=captureFolder;prefs.TopicScreenshotDirectories["计算机网络"]=Path.Combine(captureFolder,"topic");prefs.Save();var loaded=Preferences.Load();Verify(CaptureService.DirectoryFor(loaded,"计算机网络").EndsWith("topic")&&CaptureService.DirectoryFor(loaded,"")==captureFolder,"主题目录优先，默认自定义目录及配置持久化生效");
        if(keepOpen){Field<Preferences>("prefs").ScreenshotDirectory=captureFolder;Invoke("Settings_Click",window,new RoutedEventArgs());Dispatcher.Run();}else{Field<System.Windows.Forms.NotifyIcon>("tray").Dispose();Field<FloatingBar>("floating").Close();typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(window,true);window.Close();app.Shutdown();}
    }
}

