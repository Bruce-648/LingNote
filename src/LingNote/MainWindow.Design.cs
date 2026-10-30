using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.ComponentModel;
namespace LingNote;
public partial class MainWindow
{
    private readonly DispatcherTimer toastTimer=new() {Interval=TimeSpan.FromSeconds(5)};
    private void SetupDesign()
    {
        WindowFrame.Attach(this); RevealActions(TopicsHeaderRow,AddTopicButton);RevealActions(UnassignedHeaderRow,UnassignedMore,UnassignedNew);StateChanged+=(_,_)=>{WindowOutline.CornerRadius=new CornerRadius(WindowState==WindowState.Maximized?0:10);WindowOutline.Visibility=WindowState==WindowState.Maximized?Visibility.Collapsed:Visibility.Visible;};
        MinimizeCaption.Content=UiIcons.Image("minimize",16);MaximizeCaption.Content=UiIcons.Image("maximize",16);CloseCaption.Content=UiIcons.Image("close",16);SidebarToggle.Content=UiIcons.Image("sidebar",20);UpdateSections();draftTopic=prefs.ActiveTopic;draftSection=prefs.ActiveSection;UnassignedNew.Content=UiIcons.Image("edit",16);AddTopicButton.Content=UiIcons.Image("plus",16);NewButton.Content=UiIcons.Label("plus","新录音");StartButton.Content=UiIcons.Label("play","开始录音",true);StopButton.Content=UiIcons.Image("stop",20);
        toastTimer.Tick+=(_,_)=> {toastTimer.Stop();Hint.Text="";};
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty,typeof(TextBlock)).AddValueChanged(Hint,(_,_)=> {toastTimer.Stop();if(!string.IsNullOrEmpty(Hint.Text))toastTimer.Start();});
        UpdateControls();
    }
    private void UpdateControls()
    {
        if(StartButton==null)return;
        ProcessingHint.SetRunning(busy);Hint.Visibility=busy?Visibility.Collapsed:Visibility.Visible;
        bool active=recording!=null,has=selected!=null,viewingRecording=active&&selected?.Id==recording!.Id;
        StartButton.Visibility=!has&&!active?Visibility.Visible:Visibility.Collapsed;StartButton.IsEnabled=!busy;
        PauseButton.Visibility=StopButton.Visibility=active?Visibility.Visible:Visibility.Collapsed;
        RecorderCard.Visibility=!has||active||busy?Visibility.Visible:Visibility.Collapsed;
        NoteTools.Visibility=has?Visibility.Visible:Visibility.Collapsed;
        GenerateButton.Visibility=has&&!active&&!busy&&selected!.State!="已完成"?Visibility.Visible:Visibility.Collapsed;
        EmptyTitle.Text=viewingRecording?"正在记录课程":has?"暂无笔记":"准备好就开始";
        EmptySubtitle.Text=viewingRecording?"结束录音后自动整理":has?"可从课程右键菜单回放录音":"播放课程，点击上方开始录音";
        CopyButton.IsEnabled=!string.IsNullOrWhiteSpace(transcriptView?selected?.Transcript:selected?.Note);
        ExportButton.IsEnabled=!string.IsNullOrWhiteSpace(selected?.Note);
        EditToggle.IsEnabled=has&&!busy&&!active&&!transcriptView;
        UnassignedNew.IsEnabled=GroupButton.IsEnabled=AddTopicButton.IsEnabled=!busy&&!active;
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
        var menu=new ActionPopup((FrameworkElement)sender,width:160);menu.Add("新录音","plus",()=>New_Click(this,new RoutedEventArgs()),!busy&&recording==null);menu.Add("设置","settings",()=>Settings_Click(this,new RoutedEventArgs()),!busy&&recording==null);menu.Separator();menu.Add("退出","close",Exit);menu.Open();
    }
    private void EditMenu_Click(object sender,RoutedEventArgs e)
    {
        var menu=new ActionPopup((FrameworkElement)sender,width:160);menu.Add("编辑笔记","edit",()=>EditToggle.IsChecked=EditToggle.IsChecked!=true,selected!=null&&!busy&&recording==null&&!transcriptView);menu.Add("复制内容","open",()=>Copy_Click(this,new RoutedEventArgs()),CopyButton.IsEnabled);menu.Add("导出 Markdown","folder",()=>Export_Click(this,new RoutedEventArgs()),ExportButton.IsEnabled);menu.Open();
    }
    private void New_Click(object sender,RoutedEventArgs e)
    {
        if(busy||recording!=null)return;SaveEditor();selected=null;History.SelectedItem=null;transcriptView=false;EditToggle.IsChecked=false;
        TitleBox.Text="新录音";Meta.Text=string.IsNullOrEmpty(draftTopic)?(prefs.Sections.FirstOrDefault(s=>s.Id==draftSection)?.Name is string sectionName?sectionName+" · 系统声音":"系统声音"):draftTopic+" · 系统声音";StatusLabel.Text="准备录音";TimerLabel.Text="00:00:00";Hint.Text="";Preview.Document=new();LevelBar.Clear();Render();
    }
    private void NewUnassigned_Click(object sender,RoutedEventArgs e) {if(busy||recording!=null)return;SetActiveTopic("");draftSection="";prefs.ActiveSection="";prefs.Save();New_Click(sender,e);}
    private bool showAllTopics;
    private void Group_Click(object sender,RoutedEventArgs e)
    {
        var menu=new ActionPopup((FrameworkElement)sender,width:164);menu.Add("按时间排列","restore",()=>SetGrouping(false));menu.Add("按天分组","folder",()=>SetGrouping(true));menu.Separator();menu.Add("新建分区","plus",CreateSection);menu.Open();
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
        var name=TextPrompt.Ask(this,"创建主题","");if(name==null)return;
        if(prefs.Topics.Concat(store.All().Select(c=>c.Topic)).Any(t=>string.Equals(t,name,StringComparison.OrdinalIgnoreCase))){Hint.Text="这个主题已经存在。";return;}
        prefs.Topics.Add(name);if(draftSection.Length>0)prefs.TopicSections[name]=draftSection;prefs.GroupByTopic=true;prefs.Save();SetActiveTopic(name);New_Click(this,new RoutedEventArgs());RefreshHistory();Hint.Text="主题已创建。";
    }
    private void SetActiveTopic(string topic) {draftTopic=topic;prefs.ActiveTopic=topic;if(topic.Length>0)draftSection=LibrarySections.TopicSection(prefs,topic);prefs.ActiveSection=draftSection;prefs.Save();}
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
        if(busy||recording!=null)return;anchor.Focus();var menu=new ActionPopup(anchor);recordMenu=menu;
        menu.Add("移至分区…","folder",()=>ChooseSection(anchor,true,topic,null));
        menu.Add("重命名主题","edit",()=>{var name=TextPrompt.Ask(this,"重命名主题",topic);if(name==null||name==topic)return;if(prefs.Topics.Concat(store.All().Select(c=>c.Topic)).Any(t=>string.Equals(t,name,StringComparison.OrdinalIgnoreCase))){Hint.Text="主题名称已存在。";return;}SaveEditor();foreach(var c in store.All().Where(c=>c.Topic==topic)){c.Topic=name;store.Save(c);}if(selected?.Topic==topic)selected.Topic=name;prefs.Topics.Remove(topic);prefs.Topics.Add(name);if(prefs.TopicSections.Remove(topic,out var section))prefs.TopicSections[name]=section;if(draftTopic==topic)SetActiveTopic(name);prefs.Save();RefreshHistory(selected?.Id);});
        menu.Add("删除主题","trash",()=>{SaveEditor();foreach(var c in store.All().Where(c=>c.Topic==topic)){c.SectionId=LibrarySections.TopicSection(prefs,topic);c.Topic="";store.Save(c);}if(selected?.Topic==topic){selected.SectionId=LibrarySections.TopicSection(prefs,topic);selected.Topic="";}prefs.Topics.Remove(topic);prefs.TopicSections.Remove(topic);if(draftTopic==topic)SetActiveTopic("");prefs.Save();RefreshHistory(selected?.Id);Hint.Text="主题已删除，录音已移至未分区。";});
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
