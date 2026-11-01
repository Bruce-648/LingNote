using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.ComponentModel;
namespace LingNote;
public partial class MainWindow
{
    private readonly Dictionary<string,DateTime> savedAt=new();
    private readonly DispatcherTimer toastTimer=new() {Interval=TimeSpan.FromSeconds(5)};
    private void SetupDesign()
    {
        Sidebar.AddHandler(Mouse.PreviewMouseWheelEvent,new MouseWheelEventHandler(NoteWheel),true);Editor.AddHandler(Mouse.PreviewMouseWheelEvent,new MouseWheelEventHandler(NoteWheel),true);
        Preview.AddHandler(Mouse.PreviewMouseWheelEvent,new MouseWheelEventHandler(NoteWheel),true);EnhancedPreview.AddHandler(Mouse.PreviewMouseWheelEvent,new MouseWheelEventHandler(NoteWheel),true);
        foreach(var editor in new FrameworkElement[]{Preview,Editor,EnhancedPreview,EnhancedLive})AttachNoteScrollbars(editor);
        WindowFrame.Attach(this); RevealActions(TopicsHeaderRow,AddTopicButton);RevealActions(UnassignedHeaderRow,UnassignedMore,UnassignedNew);StateChanged+=(_,_)=>{WindowOutline.CornerRadius=new CornerRadius(WindowState==WindowState.Maximized?0:10);WindowOutline.Visibility=WindowState==WindowState.Maximized?Visibility.Collapsed:Visibility.Visible;};
        MinimizeCaption.Content=UiIcons.Image("minimize",16);MaximizeCaption.Content=UiIcons.Image("maximize",16);CloseCaption.Content=UiIcons.Image("close",16);SidebarToggle.Content=UiIcons.Image("sidebar",20);UpdateSections();draftTopic=prefs.ActiveTopic;draftSection=prefs.ActiveSection;UnassignedNew.Content=UiIcons.Image("edit",16);AddTopicButton.Content=UiIcons.Image("plus",16);NewButton.Content=UiIcons.Label("plus","新录音",true);StartButton.Content=UiIcons.Label("play","开始录音",true);StopButton.Content=UiIcons.Label("stop","结束并整理",true);FloatButton.Content=UiIcons.Image("open",24);
        toastTimer.Tick+=(_,_)=> {toastTimer.Stop();Hint.Text="";};
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty,typeof(TextBlock)).AddValueChanged(Hint,(_,_)=> {toastTimer.Stop();if(!string.IsNullOrEmpty(Hint.Text))toastTimer.Start();});
        CopyButton.Content=UiIcons.Image("copy",20);ExportButton.Content=UiIcons.Image("export",20);EnhancedCopy.Content=UiIcons.Image("copy",20);EnhancedExport.Content=UiIcons.Image("export",20);
        foreach(var button in new[]{CopyButton,ExportButton,EnhancedCopy,EnhancedExport}){button.Width=40;button.Height=36;button.Padding=new Thickness(8);button.Background=Brushes.Transparent;}
        EnhancedRestore.Content=UiIcons.Image("more",20);EnhancedRestore.ToolTip="恢复上一版精编笔记";
        EnhancedRestore.Width=36;EnhancedRestore.Height=36;
        SizeChanged+=(_,_)=>UpdateResponsiveLayout();
        ClassroomPane.SizeChanged+=(_,_)=>UpdateResponsiveLayout();
        UpdateControls();
    }
    private void UpdateResponsiveLayout()
    {
        if(ClassroomActions==null)return;
        foreach(var actions in new[]{ClassroomActions,EnhancedActions}){bool narrow=ClassroomPane.ActualWidth<450;Grid.SetRow(actions,narrow?2:0);Grid.SetColumn(actions,narrow?0:1);Grid.SetColumnSpan(actions,narrow?2:1);}
        bool compact=ActualWidth<1100;
        TimerLabel.FontSize=compact?24:30;TimerLabel.Margin=compact?new Thickness(14,0,12,0):new Thickness(30,0,26,0);
        LevelBar.Visibility=ActualWidth<1000?Visibility.Collapsed:Visibility.Visible;
        PauseButton.Padding=StopButton.Padding=compact?new Thickness(12,10,12,10):new Thickness(22,10,22,10);
        foreach(var button in new[]{RegenerateButton,EnhanceButton}){button.FontSize=13;button.Margin=new Thickness(0,2,2,2);button.Padding=new Thickness(6);}
    }
    private static void AttachNoteScrollbars(FrameworkElement editor)
    {
        static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}
        }
        EventHandler? update=null;
        update=(_,_)=>{
            var bars=Descendants(editor).OfType<System.Windows.Controls.Primitives.ScrollBar>().Where(b=>b.Orientation==Orientation.Vertical).ToList();
            if(bars.Count==0)return;
            foreach(var bar in bars){bar.Style=(Style)editor.FindResource("NoteBarStyle");bar.MinWidth=0;bar.Width=8;}
            editor.LayoutUpdated-=update;
        };
        void Schedule(){editor.LayoutUpdated-=update;editor.LayoutUpdated+=update;}
        editor.Loaded+=(_,_)=>Schedule();editor.IsVisibleChanged+=(_,_)=>{if(editor.IsVisible)Schedule();};
    }
    private void UpdateControls()
    {
        if(StartButton==null)return;UpdateBulkSelection();
        ProcessingHint.SetRunning(busy);Hint.Visibility=busy?Visibility.Collapsed:Visibility.Visible;
        ClassroomSaved.Text=EnhancedSaved.Text=selected==null?"":editorDirty?"正在保存…":savedAt.TryGetValue(selected.Id,out var saved)?"已保存 "+saved.ToString("HH:mm"):"已保存";
        bool active=recording!=null,has=selected!=null,viewingRecording=active&&selected?.Id==recording!.Id;
        StartButton.Visibility=!has&&!active?Visibility.Visible:Visibility.Collapsed;StartButton.IsEnabled=!stoppingRecording;
        PauseButton.Visibility=StopButton.Visibility=active?Visibility.Visible:Visibility.Collapsed;
        RecorderCard.Visibility=!has||active||busy?Visibility.Visible:Visibility.Collapsed;
        NoteTools.Visibility=has&&!viewingRecording?Visibility.Visible:Visibility.Collapsed;
        ClassroomSaved.Visibility=has&&!viewingRecording?Visibility.Visible:Visibility.Collapsed;
        GenerateButton.Visibility=Visibility.Collapsed;
        RegenerateButton.IsEnabled=has&&!SelectedProcessing&&recording?.Id!=selected?.Id;
        var direct=selected?.DirectRefinement==true;var current=CurrentNote(selected);
        RegenerateButton.Content=direct?(string.IsNullOrWhiteSpace(current)?"生成精编笔记":"重新精编笔记"):(string.IsNullOrWhiteSpace(current)?"继续整理课堂笔记":"重新整理课堂笔记");
        NoteTab.Content="笔记";
        CopyButton.ToolTip=direct?"复制精编笔记；转写页复制转写内容":"复制课堂笔记；转写页复制转写内容";
        ExportButton.ToolTip=direct?"导出精编笔记":"导出课堂笔记";
        ClassroomHeading.Text=transcriptView?"转写":direct?"精编笔记":"课堂笔记";
        ClassroomActions.Visibility=has?Visibility.Visible:Visibility.Collapsed;
        RegenerateButton.Visibility=ExportButton.Visibility=transcriptView?Visibility.Collapsed:Visibility.Visible;
        NewButton.IsEnabled=!active&&!stoppingRecording;
        PauseButton.IsEnabled=StopButton.IsEnabled=active&&!stoppingRecording;
        CancelButton.Visibility=SelectedProcessing?Visibility.Visible:Visibility.Collapsed;
        Editor.IsReadOnly=Preview.IsReadOnly=!has||transcriptView||SelectedProcessing;
        EmptyTitle.Text=viewingRecording?"正在记录课程":has?"暂无笔记":"准备好就开始";
        EmptySubtitle.Text=viewingRecording?"结束后自动转写并生成笔记。":has?"可从课程右键菜单回放录音":"播放课程，点击上方开始录音";
        CopyButton.IsEnabled=!string.IsNullOrWhiteSpace(transcriptView?selected?.Transcript:current);
        ExportButton.IsEnabled=!string.IsNullOrWhiteSpace(current);
        EditToggle.IsEnabled=has&&!SelectedProcessing&&!transcriptView;
        GroupButton.IsEnabled=true;UnassignedNew.IsEnabled=!active&&!stoppingRecording;AddTopicButton.IsEnabled=!busy&&!active;
        bool dual=!direct&&!transcriptView&&selected!=null&&!viewingRecording;
        EnhancedPane.Visibility=dual?Visibility.Visible:Visibility.Collapsed;
        NoteSplitter.Visibility=dual?Visibility.Visible:Visibility.Collapsed;
        Grid.SetColumnSpan(ClassroomPane,dual?1:3);
        OpenEnhanceButton.Visibility=has&&!direct&&!transcriptView&&!dual?Visibility.Visible:Visibility.Collapsed;
        OpenEnhanceButton.IsEnabled=has&&!SelectedProcessing&&!string.IsNullOrWhiteSpace(selected?.Note)&&recording?.Id!=selected?.Id;
        EnhanceButton.IsEnabled=has&&!SelectedProcessing&&recording?.Id!=selected?.Id&&!string.IsNullOrWhiteSpace(selected?.Note);
        EnhanceButton.Content=string.IsNullOrWhiteSpace(selected?.EnhancedNote)?"生成精编笔记":"重新精编笔记";
        EnhancedPreview.IsReadOnly=!has||SelectedProcessing;
        NotesFrame.BorderThickness=new Thickness(!has||viewingRecording?0:1);
        ClassroomHeader.SetValue(VisibilityProperty,!has||viewingRecording?Visibility.Collapsed:Visibility.Visible);
        PlaybackButton.IsEnabled=selected?.Parts.Count>0;
        PlaybackButton.Visibility=viewingRecording?Visibility.Collapsed:Visibility.Visible;
        RecordingDot.Fill=(System.Windows.Media.Brush)FindResource(!active||paused?"TextMuted":"RecordingRed");
        FloatButton.Visibility=active?Visibility.Visible:Visibility.Collapsed;
        EnhancedCopy.IsEnabled=EnhancedExport.IsEnabled=!string.IsNullOrWhiteSpace(selected?.EnhancedNote);
        EnhancedRestore.IsEnabled=has&&!SelectedProcessing&&selected!.EnhancedVersions.Count>0;
        UpdateResponsiveLayout();
        UpdateEnhancementDisplay();
        HighlightTopicRecord();
        NoteTab.Background=transcriptView?Brushes.Transparent:new SolidColorBrush(Color.FromRgb(242,242,242));
        TranscriptTab.Background=transcriptView?new SolidColorBrush(Color.FromRgb(242,242,242)):Brushes.Transparent;
    }
    private void MinimizeCaption_Click(object sender,RoutedEventArgs e)=>SystemCommands.MinimizeWindow(this);
    private void MaximizeCaption_Click(object sender,RoutedEventArgs e) {if(WindowState==WindowState.Maximized)SystemCommands.RestoreWindow(this);else SystemCommands.MaximizeWindow(this);}
    private void CloseCaption_Click(object sender,RoutedEventArgs e)=>Close();
    private void SidebarToggle_Click(object sender,RoutedEventArgs e) {bool hide=Sidebar.Visibility==Visibility.Visible;Sidebar.Visibility=hide?Visibility.Collapsed:Visibility.Visible;SidebarColumn.Width=new GridLength(hide?0:240);}
    private bool topicsCollapsed,unassignedCollapsed,showAllUnassigned;
    private void UnassignedExpand_Click(object sender,RoutedEventArgs e) {showAllUnassigned=!showAllUnassigned;RefreshHistory();}
    private void UpdateSections()
    {
        TopicsHeader.Content=UiIcons.Section("主题",topicsCollapsed);GroupButton.Content=UiIcons.Section("未分区",unassignedCollapsed);
        UnassignedExpand.Visibility=!unassignedCollapsed && UnassignedExpand.Tag is true?Visibility.Visible:Visibility.Collapsed;
        TopicsScroll.Visibility=topicsCollapsed?Visibility.Collapsed:Visibility.Visible;History.Visibility=unassignedCollapsed?Visibility.Collapsed:Visibility.Visible;
    }
    private void TopicsToggle_Click(object sender,RoutedEventArgs e) {topicsCollapsed=!topicsCollapsed;UpdateSections();}
    private void UnassignedToggle_Click(object sender,RoutedEventArgs e) {unassignedCollapsed=!unassignedCollapsed;UpdateSections();}
    private void AppMenu_Click(object sender,RoutedEventArgs e)
    {
        var menu=new ActionPopup((FrameworkElement)sender,width:160);menu.Add("新录音","plus",()=>New_Click(this,new RoutedEventArgs()),recording==null);menu.Add("设置","settings",()=>Settings_Click(this,new RoutedEventArgs()),true);menu.Separator();menu.Add("退出","close",Exit);menu.Open();
    }
    private void EditMenu_Click(object sender,RoutedEventArgs e)
    {
        var menu=new ActionPopup((FrameworkElement)sender,width:160);menu.Add("编辑笔记","edit",()=>EditToggle.IsChecked=EditToggle.IsChecked!=true,selected!=null&&!busy&&recording==null&&!transcriptView);menu.Add("复制内容","open",()=>Copy_Click(this,new RoutedEventArgs()),CopyButton.IsEnabled);menu.Add("导出 Markdown","folder",()=>Export_Click(this,new RoutedEventArgs()),ExportButton.IsEnabled);menu.Open();
    }
    private void New_Click(object sender,RoutedEventArgs e)
    {
        if(recording!=null||stoppingRecording)return;if(!LeaveSettings())return;SaveEditor();selected=null;History.SelectedItem=null;transcriptView=false;EditToggle.IsChecked=false;
        TitleBox.Text="新录音";Meta.Text=string.IsNullOrEmpty(draftTopic)?(prefs.Sections.FirstOrDefault(s=>s.Id==draftSection)?.Name is string sectionName?sectionName+" · 系统声音":"系统声音"):draftTopic+" · 系统声音";StatusLabel.Text="准备录音";TimerLabel.Text="00:00:00";Hint.Text="";Preview.Document=new();LevelBar.Clear();Render();
    }
    private void NewUnassigned_Click(object sender,RoutedEventArgs e) {if(recording!=null||stoppingRecording)return;SetActiveTopic("");draftSection="";prefs.ActiveSection="";prefs.Save();New_Click(sender,e);}
    private bool showAllTopics;
    private void Group_Click(object sender,RoutedEventArgs e)
    {
        var menu=new ActionPopup((FrameworkElement)sender,width:164);menu.Add("批量选择","checks",()=>BeginBulkSelection("unassigned","","未分区"));menu.Separator();menu.Add("按时间排列","restore",()=>SetGrouping(false));menu.Add("按天分组","folder",()=>SetGrouping(true));menu.Separator();menu.Add("新建分区","plus",CreateSection);menu.Open();
    }
    private void SetGrouping(bool topic) {SaveEditor();prefs.GroupUnassignedByDay=topic;prefs.Save();RefreshHistory(selected?.Id);}
    private void History_RightClick(object sender,MouseButtonEventArgs e)
    {
        if(busy||recording!=null||sender is not ListBoxItem row||row.DataContext is not Course course)return;
        e.Handled=true;OpenRecordMenu(row,course,true);
    }
    private ActionPopup? recordMenu;
    private void RecordMore_Click(object sender,RoutedEventArgs e) {if(sender is FrameworkElement row && row.DataContext is Course course)OpenRecordMenu(row,course,false);e.Handled=true;}
    private void OpenRecordMenu(FrameworkElement row,Course course,bool pointer)
    {
        if(busy||recording!=null)return;row.Focus();ShowCourse(course);
        var menu=new ActionPopup(row,pointer);recordMenu=menu;
        menu.Add("重命名","edit",()=> {var title=TextPrompt.Ask(this,"重命名课程",course.Title);if(title!=null){course.Title=title;store.Save(course);RefreshHistory(course.Id);}});
        menu.Add("移至分区…","folder",()=>ChooseSection(row,false,null,course));
        menu.Add("移至主题…","folder",()=> {var topic=TextPrompt.Ask(this,"课程主题",course.Topic,true,prefs.Topics.Concat(store.All().Select(c=>c.Topic)).Where(t=>!string.IsNullOrWhiteSpace(t)).Distinct().ToArray());if(topic!=null){course.Topic=topic;course.SectionId="";if(topic.Length>0&&!prefs.Topics.Contains(topic)){prefs.Topics.Add(topic);prefs.Save();}store.Save(course);RefreshHistory(course.Id);Hint.Text=topic.Length==0?"已移至未分区":"已移至「"+topic+"」";}});
        menu.Separator();
        menu.Add("回放录音片段","play",()=>new PlaybackWindow(course){Owner=this}.ShowDialog(),course.Parts.Count>0);
        if(course.Versions.Count>0)menu.Add("恢复上一版笔记","restore",()=>{SaveEditor();var last=course.Versions[^1];course.Versions.RemoveAt(course.Versions.Count-1);course.Versions.Insert(0,course.Note);course.Note=last;store.Save(course);ShowCourse(course);});
        if(!string.IsNullOrWhiteSpace(course.Note))menu.Add("重新整理笔记","restore",()=>Generate_Click(this,new RoutedEventArgs()));
        menu.Separator();menu.Add("删除课程和录音","trash",()=>DeleteCourse(course));Dispatcher.BeginInvoke(()=>menu.Open(),DispatcherPriority.ContextIdle);
    }
    private string draftTopic="";
    private string draftSection="";
    private void AddTopic_Click(object sender,RoutedEventArgs e)
    {
        if(busy||recording!=null)return;
        var created=TextPrompt.CreateTopic(this);if(created==null)return;var name=created.Value.Name;
        if(prefs.Topics.Concat(store.All().Select(c=>c.Topic)).Any(t=>string.Equals(t,name,StringComparison.OrdinalIgnoreCase))){Hint.Text="这个主题已经存在。";return;}
        if(created.Value.Folder is string folder)prefs.TopicScreenshotDirectories[name]=folder;
        prefs.Topics.Add(name);if(draftSection.Length>0)prefs.TopicSections[name]=draftSection;prefs.GroupByTopic=true;prefs.Save();SetActiveTopic(name);New_Click(this,new RoutedEventArgs());RefreshHistory();Hint.Text="主题已创建。";
    }
    private void SetActiveTopic(string topic) {draftTopic=topic;if(topic.Length>0)draftSection=LibrarySections.TopicSection(prefs,topic);if(prefs.ActiveSection!=draftSection||prefs.ActiveTopic!=topic){prefs.ActiveTopic=topic;prefs.ActiveSection=draftSection;prefs.Save();}}
    private readonly Dictionary<string,Border> topicRecordRows=new();
    private void HighlightTopicRecord() {foreach(var pair in topicRecordRows)pair.Value.Background=pair.Key==selected?.Id?new SolidColorBrush(Color.FromRgb(239,239,239)):Brushes.Transparent;}
    private void RefreshEmptyTopics()
    {
        EmptyTopics.Children.Clear();topicRecordRows.Clear();var all=store.All();
        var topics=prefs.Topics.Concat(all.Select(c=>c.Topic)).Where(t=>!string.IsNullOrWhiteSpace(t)).Distinct().Where(t=>LibrarySections.TopicSection(prefs,t)=="").ToList();
        foreach(var topic in topics.Take(showAllTopics?int.MaxValue:5)) RenderTopic(EmptyTopics,topic,all);
        if(topics.Count>5) {var more=new Button {Content=showAllTopics?"收起":"展开显示",Foreground=Brushes.Gray,Background=Brushes.Transparent,HorizontalAlignment=HorizontalAlignment.Left};more.Click+=(_,_)=>{showAllTopics=!showAllTopics;RefreshHistory();};EmptyTopics.Children.Add(more);}
        RenderSections(all);
    }
    private void TopicMenu(FrameworkElement anchor,string topic)
    {
        anchor.Focus();var menu=new ActionPopup(anchor);recordMenu=menu;menu.Add("批量选择","checks",()=>BeginBulkSelection("topic",topic,topic));if(busy||recording!=null){menu.Open();return;}menu.Separator();
        menu.Add("截屏保存文件夹…","folder",()=>{var picker=new Microsoft.Win32.OpenFolderDialog {Title="选择「"+topic+"」的截屏保存文件夹"};if(picker.ShowDialog(this)==true){prefs.TopicScreenshotDirectories[topic]=picker.FolderName;prefs.Save();}});
        menu.Add("使用默认截屏目录","restore",()=>{prefs.TopicScreenshotDirectories.Remove(topic);prefs.Save();});menu.Separator();
        menu.Add("移至分区…","folder",()=>ChooseSection(anchor,true,topic,null));
        menu.Add("重命名主题","edit",()=>{var name=TextPrompt.Ask(this,"重命名主题",topic);if(name==null||name==topic)return;if(prefs.Topics.Concat(store.All().Select(c=>c.Topic)).Any(t=>string.Equals(t,name,StringComparison.OrdinalIgnoreCase))){Hint.Text="主题名称已存在。";return;}SaveEditor();foreach(var c in store.All().Where(c=>c.Topic==topic)){c.Topic=name;store.Save(c);}if(selected?.Topic==topic)selected.Topic=name;prefs.Topics.Remove(topic);prefs.Topics.Add(name);if(prefs.TopicScreenshotDirectories.Remove(topic,out var screenshotFolder))prefs.TopicScreenshotDirectories[name]=screenshotFolder;if(prefs.TopicSections.Remove(topic,out var section))prefs.TopicSections[name]=section;if(draftTopic==topic)SetActiveTopic(name);prefs.Save();RefreshHistory(selected?.Id);});
        menu.Add("删除主题","trash",()=>{SaveEditor();foreach(var c in store.All().Where(c=>c.Topic==topic)){c.SectionId=LibrarySections.TopicSection(prefs,topic);c.Topic="";store.Save(c);}if(selected?.Topic==topic){selected.SectionId=LibrarySections.TopicSection(prefs,topic);selected.Topic="";}prefs.Topics.Remove(topic);prefs.TopicSections.Remove(topic);prefs.TopicScreenshotDirectories.Remove(topic);if(draftTopic==topic)SetActiveTopic("");prefs.Save();RefreshHistory(selected?.Id);Hint.Text="主题已删除，录音已移至未分区。";});
        Dispatcher.BeginInvoke(()=>menu.Open(),DispatcherPriority.ContextIdle);
    }
    private DismissibleWindow? trayMenu;
    private void ShowTrayMenu()
    {
        if(exiting)return;trayMenu?.Dismiss();var menu=new ActionPopup(null,width:164);var window=new DismissibleWindow {Width=174,SizeToContent=SizeToContent.Height,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,Topmost=true,Content=menu.DetachContent()};
        trayMenu=window;menu.Dismiss=window.Dismiss;menu.Add("打开 LingNote","open",Restore);menu.Separator();menu.Add("退出","close",Exit);
        window.Deactivated+=(_,_)=>window.Dismiss();window.Closed+=(_,_)=>{if(ReferenceEquals(trayMenu,window))trayMenu=null;};window.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape)window.Dismiss();};
        window.Loaded+=(_,_)=>{var dpi=VisualTreeHelper.GetDpi(window);var pos=System.Windows.Forms.Cursor.Position;var area=System.Windows.Forms.Screen.FromPoint(pos).WorkingArea;window.Left=Math.Clamp(pos.X/dpi.DpiScaleX-window.ActualWidth/2,area.Left/dpi.DpiScaleX,Math.Max(area.Left/dpi.DpiScaleX,area.Right/dpi.DpiScaleX-window.ActualWidth));window.Top=Math.Max(area.Top/dpi.DpiScaleY,Math.Min(pos.Y/dpi.DpiScaleY-window.ActualHeight+2,area.Bottom/dpi.DpiScaleY-window.ActualHeight));};window.Show();window.Activate();
    }
}
