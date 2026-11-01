using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace LingNote;
public partial class MainWindow
{
    private string? bulkKind;
    private string bulkScope="",bulkLabel="";
    private readonly HashSet<string> bulkSelected=new();
    private readonly HashSet<CheckBox> bulkChecks=new();
    private bool updatingBulk;
    private bool InBulkScope(Course course)=>bulkKind switch {
        "unassigned"=>string.IsNullOrEmpty(course.Topic)&&LibrarySections.SectionOf(prefs,course)=="",
        "topic"=>course.Topic==bulkScope,
        "section"=>LibrarySections.SectionOf(prefs,course)==bulkScope,
        _=>false
    };
    private bool BulkEligible(Course course)=>recording?.Id!=course.Id&&!processingCourses.ContainsKey(course.Id);
    private List<Course> BulkCandidates()=>store.All().Where(c=>InBulkScope(c)&&BulkEligible(c)).ToList();
    private void BeginBulkSelection(string kind,string scope,string label)
    {
        if(!LeaveSettings())return;
        bulkKind=kind;bulkScope=scope;bulkLabel=label;bulkSelected.Clear();RefreshHistory();UpdateBulkSelection();
    }
    private void EndBulkSelection(){bulkKind=null;bulkSelected.Clear();UpdateBulkSelection();}
    private CheckBox CreateBulkCheck(Course course)
    {
        var check=new CheckBox {DataContext=course,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(6,0,6,0),Visibility=Visibility.Collapsed};
        check.Loaded+=BulkCheck_Loaded;check.Unloaded+=BulkCheck_Unloaded;check.PreviewMouseLeftButtonDown+=BulkCheck_Preview;check.Checked+=BulkCheck_Changed;check.Unchecked+=BulkCheck_Changed;return check;
    }
    private void BulkCheck_Loaded(object sender,RoutedEventArgs e){var check=(CheckBox)sender;bulkChecks.Add(check);UpdateBulkCheck(check);}
    private void BulkCheck_Unloaded(object sender,RoutedEventArgs e)=>bulkChecks.Remove((CheckBox)sender);
    private void UpdateBulkCheck(CheckBox check)
    {
        if(check.DataContext is not Course course)return;
        bool previous=updatingBulk;updatingBulk=true;
        check.Visibility=InBulkScope(course)?Visibility.Visible:Visibility.Collapsed;check.IsEnabled=BulkEligible(course);check.IsChecked=bulkSelected.Contains(course.Id)&&check.IsEnabled;
        check.ToolTip=check.IsEnabled?"选择录音："+course.Title:"正在录音或处理中，不能批量删除";System.Windows.Automation.AutomationProperties.SetName(check,"选择录音："+course.Title);updatingBulk=previous;
    }
    private void BulkCheck_Preview(object sender,MouseButtonEventArgs e)
    {
        var check=(CheckBox)sender;if(!check.IsEnabled)return;check.IsChecked=check.IsChecked!=true;e.Handled=true;
    }
    private void BulkCheck_Changed(object sender,RoutedEventArgs e)
    {
        if(updatingBulk||sender is not CheckBox check||check.DataContext is not Course course)return;
        if(check.IsChecked==true&&InBulkScope(course)&&BulkEligible(course))bulkSelected.Add(course.Id);else bulkSelected.Remove(course.Id);UpdateBulkSelection();
    }
    private void UpdateBulkSelection()
    {
        if(BulkBar==null)return;
        BulkBar.Visibility=bulkKind==null?Visibility.Collapsed:Visibility.Visible;
        if(bulkKind!=null){var available=BulkCandidates().Select(c=>c.Id).ToHashSet();bulkSelected.IntersectWith(available);BulkSummary.Text=bulkLabel+" · 已选 "+bulkSelected.Count+" 条";BulkDelete.IsEnabled=bulkSelected.Count>0;BulkAll.IsEnabled=available.Count>0;BulkAll.Content=available.Count>0&&available.SetEquals(bulkSelected)?"清空":"全选";}
        foreach(var check in bulkChecks.ToArray())UpdateBulkCheck(check);
    }
    private void BulkAll_Click(object sender,RoutedEventArgs e)
    {
        var available=BulkCandidates().Select(c=>c.Id).ToHashSet();if(available.SetEquals(bulkSelected))bulkSelected.Clear();else bulkSelected.UnionWith(available);UpdateBulkSelection();
    }
    private void BulkCancel_Click(object sender,RoutedEventArgs e)=>EndBulkSelection();
    private void BulkDelete_Click(object sender,RoutedEventArgs e)
    {
        UpdateBulkSelection();var ids=bulkSelected.ToArray();if(ids.Length==0)return;
        if(!ConfirmWindow.Ask(this,"删除所选录音？",$"将删除「{bulkLabel}」中所选的 {ids.Length} 条录音，以及对应笔记、转写和音频文件。主题和分区保留，此操作无法撤销。","删除 "+ids.Length+" 条"))return;
        CommitBulkDeletion(ids);
    }
    private void CommitBulkDeletion(IReadOnlyCollection<string> confirmedIds)
    {
        var targets=BulkCandidates().Where(c=>bulkSelected.Contains(c.Id)&&confirmedIds.Contains(c.Id)).ToList();if(targets.Count==0){UpdateBulkSelection();return;}
        SaveEditor();autosave.Stop();
        try {
            store.DeleteMany(targets);
            if(targets.Any(c=>c.Id==selected?.Id)){
                selected=null;titleDirty=editorDirty=false;History.SelectedItem=null;
                if(recording!=null)ShowCourse(recording);else {loadingEditor=true;Editor.Clear();loadingEditor=false;EditToggle.IsChecked=false;transcriptView=false;loadingTitle=true;TitleBox.Text=originalTitle="新录音";loadingTitle=false;Meta.Text=string.IsNullOrEmpty(draftTopic)?"系统声音":draftTopic+" · 系统声音";StatusLabel.Text="准备录音";TimerLabel.Text="00:00:00";GenerateButton.IsEnabled=false;LevelBar.Clear();Render();}
            }
            EndBulkSelection();RefreshHistory();UpdateControls();Hint.Text=$"已删除 {targets.Count} 条录音，主题和分区已保留。";
        }catch(Exception ex){RefreshHistory();UpdateBulkSelection();ShowError(ex);}
    }
}
