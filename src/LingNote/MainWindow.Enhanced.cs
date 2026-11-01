using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace LingNote;
public partial class MainWindow
{
    private readonly HashSet<string> enhanceRequests=new();
    private readonly HashSet<string> enhancedOpen=new();
    private readonly Dictionary<string,string> enhancementPreview=new(),enhancementStatus=new();
    private async Task CompleteEnhancement(Course course,CancellationToken ct)
    {
        course.State="正在精编";store.Save(course);
        enhancementStatus[course.Id]="正在连接 AI 服务…";
        UpdateControls();
        string note;
        try {note=await EnhancementWorker.Generate(course,prefs,ct,new Progress<EnhancementWorker.Update>(update=>{
            if(!processingCourses.ContainsKey(course.Id)||!enhancementStatus.ContainsKey(course.Id))return;
            if(update.Kind=="preview")enhancementPreview[course.Id]=update.Text;else enhancementStatus[course.Id]=update.Text;
            if(selected?.Id!=course.Id)return;
            UpdateEnhancementDisplay();
        }));}
        finally {enhancementStatus.Remove(course.Id);enhancementPreview.Remove(course.Id);UpdateEnhancementDisplay();}
        if(!string.IsNullOrWhiteSpace(course.EnhancedNote))course.EnhancedVersions.Add(course.EnhancedNote);
        course.EnhancedNote=note;course.EnhancedDrafts.Clear();course.EnhancedFingerprint="";course.State="已完成";course.Error="";store.Save(course);
    }
    private void UpdateEnhancementDisplay()
    {
        bool live=selected!=null&&enhancementStatus.ContainsKey(selected.Id)&&!transcriptView;
        EnhancedLive.Visibility=live?Visibility.Visible:Visibility.Collapsed;
        EnhancedPreview.Visibility=live?Visibility.Collapsed:Visibility.Visible;
        if(live){var bottom=EnhancedLive.VerticalOffset>=EnhancedLive.ExtentHeight-EnhancedLive.ViewportHeight-24;EnhancedLive.Text=enhancementPreview.GetValueOrDefault(selected!.Id,"");if(bottom)EnhancedLive.ScrollToEnd();}
    }
    private async void Enhance_Click(object sender,RoutedEventArgs e)
    {
        if(selected==null||SelectedProcessing||recording?.Id==selected.Id||string.IsNullOrWhiteSpace(selected.Note))return;
        SaveEditor();var course=selected;enhancedOpen.Add(course.Id);UpdateControls();
        var resume=(course.State is "待重试" or "已取消")&& course.EnhancedDrafts.Count>0;
        if(!string.IsNullOrWhiteSpace(course.EnhancedNote)&&!resume){course.EnhancedDrafts.Clear();course.EnhancedFingerprint="";}
        enhanceRequests.Add(course.Id);await RunProcessing(course,false);
    }
    private void Enhanced_Changed(object sender,TextChangedEventArgs e)
    {
        if(loadingEditor||selected==null||SelectedProcessing||e.Changes.Count==0)return;
        editorDirty=true;selected.EnhancedNote=MarkdownView.ToMarkdown(EnhancedPreview.Document);autosave.Stop();autosave.Start();UpdateControls();
    }
    private void EnhancedCopy_Click(object sender,RoutedEventArgs e)
    {
        SaveEditor();if(!string.IsNullOrWhiteSpace(selected?.EnhancedNote))Clipboard.SetText(selected.EnhancedNote);
    }
    private void EnhancedExport_Click(object sender,RoutedEventArgs e)
    {
        SaveEditor();if(string.IsNullOrWhiteSpace(selected?.EnhancedNote))return;
        var title=new string(selected.Title.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c).ToArray());
        var dialog=new Microsoft.Win32.SaveFileDialog {Filter="Markdown 文档|*.md",FileName=title+"_精编笔记.md"};
        if(dialog.ShowDialog(this)==true)File.WriteAllText(dialog.FileName,selected.EnhancedNote,new System.Text.UTF8Encoding(false));
    }
    private void EnhancedRestore_Click(object sender,RoutedEventArgs e)
    {
        if(selected==null||SelectedProcessing||selected.EnhancedVersions.Count==0)return;
        SaveEditor();var course=selected;var last=course.EnhancedVersions[^1];course.EnhancedVersions.RemoveAt(course.EnhancedVersions.Count-1);
        course.EnhancedVersions.Insert(0,course.EnhancedNote);course.EnhancedNote=last;store.Save(course);Render();
    }
    private void NoteWheel(object sender,System.Windows.Input.MouseWheelEventArgs e)
    {
        static ScrollViewer? Find(DependencyObject root){for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++){var child=System.Windows.Media.VisualTreeHelper.GetChild(root,i);if(child is ScrollViewer scroll)return scroll;var found=Find(child);if(found!=null)return found;}return null;}
        var viewer=Find((DependencyObject)sender);if(viewer==null)return;
        var lines=SystemParameters.WheelScrollLines;
        var distance=lines<0?viewer.ViewportHeight:Math.Max(1,lines)*24d;
        viewer.ScrollToVerticalOffset(viewer.VerticalOffset-e.Delta/120d*distance);e.Handled=true;
    }

}
