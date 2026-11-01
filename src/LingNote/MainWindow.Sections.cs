using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace LingNote;
public partial class MainWindow
{
    private readonly HashSet<string> expandedSidebarLists=new();
    private readonly HashSet<string> collapsedTopics=new();
    private static void RevealActions(FrameworkElement row,params Button[] actions)
    {
        void Update(){foreach(var action in actions)action.Visibility=row.IsMouseOver||row.IsKeyboardFocusWithin?Visibility.Visible:Visibility.Hidden;}
        row.MouseEnter+=(_,_)=>Update();row.MouseLeave+=(_,_)=>Update();
        row.IsKeyboardFocusWithinChanged+=(_,_)=>Update();Update();
    }
    private void RenderLimited<T>(StackPanel parent,IEnumerable<T> source,string key,Action<T> render,double indent=10)
    {
        var items=source.ToList();var expanded=expandedSidebarLists.Contains(key);
        foreach(var item in items.Take(expanded?int.MaxValue:5))render(item);
        if(items.Count<=5)return;
        var toggle=new Button {Content=expanded?"收起":"展开显示",Foreground=Brushes.Gray,Background=Brushes.Transparent,HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(indent,6,4,6),Margin=new Thickness(0,4,0,0)};
        toggle.Click+=(_,_)=>{if(!expandedSidebarLists.Add(key))expandedSidebarLists.Remove(key);RefreshHistory();};parent.Children.Add(toggle);
    }
    private bool CanOrganize=>!busy&&recording==null;
    private static Button SidebarAction(string icon,string hint)=>new() {
        Content=UiIcons.Image(icon,18),ToolTip=hint,Width=28,Height=28,Padding=new Thickness(4),
        Margin=new Thickness(0),Background=Brushes.Transparent
    };
    private void RenderTopic(StackPanel parent,string topic,List<Course> all)
    {
        var row=new DockPanel {Background=Brushes.Transparent,Height=36,Margin=new Thickness(0,1,0,0)};
        var add=SidebarAction("edit","在此主题中新建录音");DockPanel.SetDock(add,Dock.Right);row.Children.Add(add);
        var more=SidebarAction("more","主题操作");DockPanel.SetDock(more,Dock.Right);row.Children.Add(more);
        var name=new Button {FontSize=14,Foreground=new SolidColorBrush(Color.FromRgb(75,75,75)),Content=UiIcons.TopicFolder(topic,!collapsedTopics.Contains(topic)),HorizontalContentAlignment=HorizontalAlignment.Left,Background=Brushes.Transparent,Padding=new Thickness(8,4,8,4),Margin=new Thickness(0)};
        row.Children.Add(name);parent.Children.Add(row);RevealActions(row,add,more);
        void NewInTopic(){if(recording!=null||stoppingRecording)return;SetActiveTopic(topic);New_Click(this,new RoutedEventArgs());}
        add.Click+=(_,_)=>NewInTopic();name.Click+=(_,_)=>{if(!collapsedTopics.Add(topic))collapsedTopics.Remove(topic);RefreshHistory();};
        more.Click+=(_,_)=>TopicMenu(more,topic);row.MouseRightButtonUp+=(_,e)=>{e.Handled=true;TopicMenu(more,topic);};
        if(collapsedTopics.Contains(topic))return;
        RenderLimited(parent,all.Where(c=>c.Topic==topic),"topic:"+topic,c=>RenderSectionRecord(parent,c,32),32);
    }
    private void RenderSectionRecord(StackPanel parent,Course course,double indent=10)
    {
        var item=new DockPanel {Height=34,Background=Brushes.Transparent};var spacer=new Border {Width=28};DockPanel.SetDock(spacer,Dock.Right);item.Children.Add(spacer);var actions=SidebarAction("more","录音操作");
        DockPanel.SetDock(actions,Dock.Right);item.Children.Add(actions);
        var title=new TextBlock {DataContext=course,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};title.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding(nameof(Course.Title)));
        var open=new Button {Content=title,HorizontalContentAlignment=HorizontalAlignment.Left,FontSize=14,Foreground=new SolidColorBrush(Color.FromRgb(75,75,75)),Padding=new Thickness(indent,3,4,3),Margin=new Thickness(0),Background=Brushes.Transparent};
        item.Children.Add(open);var background=new Border {CornerRadius=new CornerRadius(9),Margin=new Thickness(0,1,0,1),Child=item,Tag=course};
        topicRecordRows[course.Id]=background;parent.Children.Add(background);RevealActions(item,actions);
        item.MouseEnter+=(_,_)=>{if(selected?.Id!=course.Id)background.Background=new SolidColorBrush(Color.FromRgb(245,245,245));};
        item.MouseLeave+=(_,_)=>{HighlightTopicRecord();};
        open.Click+=(_,_)=>{ShowCourse(course);};actions.Click+=(_,_)=>OpenRecordMenu(actions,course,false);
        item.MouseRightButtonUp+=(_,e)=>{e.Handled=true;OpenRecordMenu(open,course,true);};
    }
    private void RenderSections(List<Course> all)
    {
        CustomSections.Children.Clear();
        foreach(var section in prefs.Sections) {
            var header=new DockPanel {Background=Brushes.Transparent,Margin=new Thickness(0,22,0,8),Height=28};
            var add=SidebarAction("edit","在此分区中新建录音");DockPanel.SetDock(add,Dock.Right);header.Children.Add(add);
            var more=SidebarAction("more","分区操作");DockPanel.SetDock(more,Dock.Right);header.Children.Add(more);
            var name=new Button {Content=UiIcons.Section(section.Name,section.Collapsed),FontSize=13,Foreground=new SolidColorBrush(Color.FromRgb(146,146,146)),Background=Brushes.Transparent,HorizontalContentAlignment=HorizontalAlignment.Left,Margin=new Thickness(0),Padding=new Thickness(10,4,10,4)};
            header.Children.Add(name);CustomSections.Children.Add(header);RevealActions(header,add,more);
            var content=new StackPanel {Visibility=section.Collapsed?Visibility.Collapsed:Visibility.Visible};CustomSections.Children.Add(content);
            name.Click+=(_,_)=>{section.Collapsed=!section.Collapsed;prefs.Save();content.Visibility=section.Collapsed?Visibility.Collapsed:Visibility.Visible;name.Content=UiIcons.Section(section.Name,section.Collapsed);};
            add.Click+=(_,_)=>{if(recording!=null||stoppingRecording)return;SetActiveTopic("");draftSection=section.Id;prefs.ActiveSection=section.Id;prefs.Save();New_Click(this,new RoutedEventArgs());};
            more.Click+=(_,_)=>SectionMenu(more,section);header.MouseRightButtonUp+=(_,e)=>{e.Handled=true;SectionMenu(more,section);};
            var topics=prefs.Topics.Concat(all.Select(c=>c.Topic)).Where(t=>!string.IsNullOrEmpty(t)&&LibrarySections.TopicSection(prefs,t)==section.Id).Distinct();
            RenderLimited(content,topics,"section-topics:"+section.Id,t=>RenderTopic(content,t,all));
            RenderLimited(content,all.Where(c=>string.IsNullOrEmpty(c.Topic)&&LibrarySections.SectionOf(prefs,c)==section.Id),"section-records:"+section.Id,c=>RenderSectionRecord(content,c));
            if(content.Children.Count==0)content.Children.Add(new TextBlock {Text="将录音或主题移至这里",Foreground=new SolidColorBrush(Color.FromRgb(160,160,160)),FontSize=11,Margin=new Thickness(10,2,0,4)});
        }
    }
    private void CreateSection()
    {
        if(!CanOrganize)return;
        var name=TextPrompt.Ask(this,"新建分区","");if(name==null)return;
        if(name is "主题" or "未分区"||prefs.Sections.Any(s=>string.Equals(s.Name,name,StringComparison.OrdinalIgnoreCase))){Hint.Text="这个分区名称已存在。";return;}
        prefs.Sections.Add(new LibrarySection {Name=name});prefs.Save();RefreshHistory();Hint.Text="分区已创建。";
    }
    private void ReloadOrganization()
    {
        if(selected!=null) {
            var saved=store.All().FirstOrDefault(c=>c.Id==selected.Id);
            if(saved!=null){selected.Topic=saved.Topic;selected.SectionId=saved.SectionId;selected.Archived=saved.Archived;selected.IsRead=saved.IsRead;}
        }
        if(!prefs.Sections.Any(s=>s.Id==draftSection))draftSection="";
        RefreshHistory(selected?.Id);
    }
    private void ChooseSection(FrameworkElement anchor,bool wholeTopic,string? topic,Course? course)
    {
        if(!CanOrganize)return;SaveEditor();
        var menu=new ActionPopup(anchor,width:190);recordMenu=menu;
        void Move(string id) {
            if(wholeTopic)LibrarySections.MoveTopic(prefs,topic!,id);
            else LibrarySections.MoveRecord(store,course!,id);
            if(wholeTopic&&draftTopic==topic){draftSection=id;prefs.ActiveSection=id;prefs.Save();}
            ReloadOrganization();Hint.Text="已移动。";
        }
        menu.Add(wholeTopic?"主题（默认）":"未分区","folder",()=>Move(""));
        foreach(var section in prefs.Sections)menu.Add(section.Name,"folder",()=>Move(section.Id));
        menu.Separator();menu.Add("新建分区…","plus",()=>{var before=prefs.Sections.Count;CreateSection();if(prefs.Sections.Count>before)Move(prefs.Sections[^1].Id);});
        Dispatcher.BeginInvoke(()=>menu.Open(),DispatcherPriority.ContextIdle);
    }
    private void SectionMenu(FrameworkElement anchor,LibrarySection section)
    {
        if(!CanOrganize)return;var menu=new ActionPopup(anchor,width:180);recordMenu=menu;
        menu.Add("编辑分区","edit",()=>{
            var name=TextPrompt.Ask(this,"重命名分区",section.Name);if(name==null)return;
            if(name is "主题" or "未分区"||prefs.Sections.Any(s=>s.Id!=section.Id&&string.Equals(s.Name,name,StringComparison.OrdinalIgnoreCase))){Hint.Text="这个分区名称已存在。";return;}
            section.Name=name;prefs.Save();RefreshHistory();
        });
        menu.Separator();
        menu.Add("全部标为已读","restore",()=>{SaveEditor();foreach(var c in store.All().Where(c=>LibrarySections.SectionOf(prefs,c)==section.Id)){c.IsRead=true;store.Save(c);}ReloadOrganization();Hint.Text="已全部标为已读。";});
        menu.Add("删除录音","trash",()=>DeleteSectionRecordings(section));
        menu.Separator();menu.Add("新建分区","plus",CreateSection);
        menu.Add("移除分区","close",()=>{
            if(!ConfirmWindow.Ask(this,"移除分区","主题将回到「主题」，独立录音将回到「未分区」。录音和笔记会保留。","移除"))return;
            SaveEditor();LibrarySections.Remove(store,prefs,section.Id);ReloadOrganization();Hint.Text="分区已移除，内容已保留。";
        });menu.Open();
    }
    private void DeleteSectionRecordings(LibrarySection section)
    {
        if(!CanOrganize)return;
        var targets=store.All().Where(c=>LibrarySections.SectionOf(prefs,c)==section.Id).ToList();
        if(targets.Count==0){Hint.Text="这个分区暂无录音。";return;}
        if(!ConfirmWindow.Ask(this,"删除分区内的录音？",$"将删除「{section.Name}」内的 {targets.Count} 条录音及其笔记，包含主题内的录音。分区和主题保留，此操作无法撤销。"))return;
        SaveEditor();autosave.Stop();
        try {
            LibrarySections.DeleteRecordings(store,prefs,section.Id);
            if(targets.Any(c=>c.Id==selected?.Id)) {
                selected=null;loadingEditor=true;Editor.Clear();loadingEditor=false;
                New_Click(this,new RoutedEventArgs());
            }
            RefreshHistory();UpdateControls();Hint.Text=$"已删除 {targets.Count} 条录音。";
        } catch(Exception ex){RefreshHistory();ShowError(ex);}
    }
}
