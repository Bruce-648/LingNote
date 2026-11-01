using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LingNote;
internal static class BulkDeletionChecks
{
    public static void Run()
    {
        Exception? error=null;var thread=new Thread(()=>{try{Check();}catch(Exception ex){error=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error!=null)throw new Exception("Bulk deletion regression",error);
    }
    static IEnumerable<T> Visuals<T>(DependencyObject root) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;foreach(var nested in Visuals<T>(child))yield return nested;}}
    static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,()=>frame.Continue=false);Dispatcher.PushFrame(frame);}
    static void Check()
    {
        Environment.SetEnvironmentVariable("LINGNOTE_DATA",Path.Combine(AppContext.BaseDirectory,"bulk-test-data",Guid.NewGuid().ToString("N")));Paths.Init();SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var app=(Application)typeof(SidebarUiChecks).GetMethod("CreateApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;
        var prefs=new Preferences();var section=new LibrarySection {Name="课程分区",Collapsed=true};prefs.Sections.Add(section);prefs.Topics.AddRange(["计算机网络","其他主题"]);prefs.TopicSections["计算机网络"]=section.Id;prefs.Save();var store=new Store();
        Course Make(string name,string topic="",string sectionId="",int order=0){var course=new Course {Title=name,Topic=topic,SectionId=sectionId,Created=DateTime.Today.AddMinutes(order),Note="# 保留的笔记",EnhancedNote="独立精编内容"};Directory.CreateDirectory(course.Folder);File.WriteAllText(Path.Combine(course.Folder,"audio-test.dat"),"isolated test audio");store.Save(course);return course;}
        var unassigned=Enumerable.Range(0,20).Select(i=>Make("未分区录音 "+i,order:i)).ToList();var topic=Enumerable.Range(0,7).Select(i=>Make("主题录音 "+i,"计算机网络",order:30+i)).ToList();var independent=Enumerable.Range(0,3).Select(i=>Make("分区独立录音 "+i,sectionId:section.Id,order:50+i)).ToList();var other=Make("不应删除的其他主题录音","其他主题",order:60);
        var window=new MainWindow();window.Show();Pump();
        T Field<T>(string name)=>(T)typeof(MainWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.GetValue(window)!;
        void Set(string name,object? value)=>typeof(MainWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.SetValue(window,value);
        object? Invoke(string name,params object?[] values)=>typeof(MainWindow).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,values);
        void Verify(bool result,string name){if(!result)throw new Exception(name);Console.WriteLine("PASS: "+name);}
        void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();}
        Invoke("ShowCourse",unassigned[0]);Verify(Field<Border>("BulkBar").Visibility==Visibility.Collapsed,"普通模式隐藏批量操作栏");
        var active=unassigned[19];var processing=Field<Dictionary<string,Course>>("processingCourses");processing[unassigned[18].Id]=unassigned[18];Set("recording",active);
        Invoke("BeginBulkSelection","unassigned","","未分区");Pump();Verify(Field<Border>("BulkBar").IsVisible&&Field<ListBox>("History").Items.Count==15,"进入批量模式保留默认15条列表与折叠状态");
        var checkboxes=Visuals<CheckBox>(Field<ListBox>("History")).ToList();Verify(checkboxes.Count==15&&checkboxes.All(c=>c.IsVisible),"未分区录音显示复选框");Verify(checkboxes.Where(c=>((Course)c.DataContext).Id==active.Id||((Course)c.DataContext).Id==unassigned[18].Id).All(c=>!c.IsEnabled),"正在录音与后台处理的记录禁选");
        Click(Field<Button>("BulkAll"));Verify(Field<HashSet<string>>("bulkSelected").Count==18,"全选包含未显示的折叠项并排除活动任务");Click(Field<Button>("BulkAll"));Verify(Field<HashSet<string>>("bulkSelected").Count==0&&!Field<Button>("BulkDelete").IsEnabled,"再次全选清空，空选择禁用删除");Click(Field<Button>("BulkAll"));
        var uncheckedBox=checkboxes.Single(c=>((Course)c.DataContext).Id==unassigned[17].Id);uncheckedBox.IsChecked=false;Pump();Verify(Field<HashSet<string>>("bulkSelected").Count==17&&Field<Course>("selected").Id==unassigned[0].Id,"单独取消勾选只改变选择集合，保留当前笔记");
        Directory.CreateDirectory("artifacts/bulk-delete");var snapshot=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);snapshot.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(snapshot));using(var file=File.Create("artifacts/bulk-delete/selection.png"))encoder.Save(file);
        int before=store.All().Count;Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,()=>{var dialog=app.Windows.OfType<ConfirmWindow>().Single();Verify(Visuals<TextBlock>(dialog).Any(t=>t.Text.Contains("17 条录音")&&t.Text.Contains("无法撤销")),"确认框显示真实数量与不可撤销说明");Visuals<Button>(dialog).Single(b=>b.IsCancel).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));});
        Click(Field<Button>("BulkDelete"));Verify(store.All().Count==before&&Field<HashSet<string>>("bulkSelected").Count==17,"取消删除不改变数据并保留勾选");
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,()=>{processing[unassigned[16].Id]=unassigned[16];var dialog=app.Windows.OfType<ConfirmWindow>().Single();Visuals<Button>(dialog).Single(b=>b.Content?.ToString()=="删除 17 条").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));});
        Click(Field<Button>("BulkDelete"));Verify(store.All().Count==before-16&&store.All().Any(c=>c.Id==unassigned[16].Id),"确认期间新进入处理的记录也会跳过");Verify(store.All().Any(c=>c.Id==active.Id)&&store.All().Any(c=>c.Id==unassigned[18].Id)&&store.All().Any(c=>c.Id==unassigned[17].Id),"活动任务及未勾选的记录完整保留");Verify(unassigned.Take(16).All(c=>!Directory.Exists(c.Folder)),"批量删除同步清理所选录音文件夹");store.Save(unassigned[0]);Verify(!store.All().Any(c=>c.Id==unassigned[0].Id),"批量删除墓碑阻止延迟保存复活记录");
        Set("recording",null);processing.Clear();Invoke("ShowCourse",other);var preserved=Field<Course>("selected");var collapsed=Field<HashSet<string>>("collapsedTopics");collapsed.Add("计算机网络");Invoke("BeginBulkSelection","topic","计算机网络","计算机网络");Click(Field<Button>("BulkAll"));Verify(Field<HashSet<string>>("bulkSelected").Count==7,"折叠主题也能全选当前主题全部录音");Invoke("CommitBulkDeletion",(object)Field<HashSet<string>>("bulkSelected").ToArray());Verify(!store.All().Any(c=>c.Topic=="计算机网络")&&Preferences.Load().Topics.Contains("计算机网络"),"清空主题只删除记录，空主题保留");Verify(collapsed.Contains("计算机网络")&&ReferenceEquals(preserved,Field<Course>("selected")),"删除其他记录保留主题展开状态与当前笔记对象");
        var nested=Make("新主题内录音","计算机网络",order:80);Invoke("BeginBulkSelection","section",section.Id,section.Name);Click(Field<Button>("BulkAll"));Verify(Field<HashSet<string>>("bulkSelected").Count==4,"分区全选包括独立录音与所属主题录音");Invoke("CommitBulkDeletion",(object)Field<HashSet<string>>("bulkSelected").ToArray());Verify(!store.All().Any(c=>LibrarySections.SectionOf(Preferences.Load(),c)==section.Id)&&Preferences.Load().Sections.Single().Collapsed&&Preferences.Load().TopicSections["计算机网络"]==section.Id,"分区批量清空保留分区、主题归属与折叠状态");Verify(store.All().Any(c=>c.Id==other.Id)&&Directory.Exists(other.Folder),"跨范围的其他记录与文件不受影响");
        Invoke("BeginBulkSelection","unassigned","","未分区");Click(Field<Button>("BulkAll"));Invoke("BulkCancel_Click",window,new RoutedEventArgs());Verify(Field<Border>("BulkBar").Visibility==Visibility.Collapsed&&Field<HashSet<string>>("bulkSelected").Count==0,"取消批量模式隐藏复选框并清空选择");
        Invoke("BeginBulkSelection","topic","其他主题","其他主题");Pump();var topicCheck=Visuals<CheckBox>(Field<StackPanel>("EmptyTopics")).Single(c=>c.DataContext is Course record&&record.Id==other.Id);topicCheck.IsChecked=true;Pump();Verify(Field<HashSet<string>>("bulkSelected").SetEquals([other.Id]),"主题列表复选框正确勾选指定录音");Invoke("BulkCancel_Click",window,new RoutedEventArgs());
        Invoke("ShowCourse",unassigned[17]);Invoke("BeginBulkSelection","unassigned","","未分区");Field<HashSet<string>>("bulkSelected").Add(unassigned[17].Id);Invoke("CommitBulkDeletion",(object)new[]{unassigned[17].Id});Verify(Field<Course?>("selected")==null&&Field<TextBox>("TitleBox").Text=="新录音"&&Field<TextBlock>("TimerLabel").Text=="00:00:00","删除当前笔记后清空正文并恢复新录音状态");
        Field<System.Windows.Forms.NotifyIcon>("tray").Dispose();Field<FloatingBar>("floating").Close();Set("exiting",true);window.Close();app.Shutdown();
    }
}
