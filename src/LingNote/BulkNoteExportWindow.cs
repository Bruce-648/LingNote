using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace LingNote;

internal sealed class BulkNoteExportWindow : Window
{
    private readonly int courseCount;
    private readonly IReadOnlyList<ExportNote> notes;
    private readonly CheckBox classroom=new(){Content="课堂笔记",IsChecked=true,Margin=new Thickness(0,0,24,0)};
    private readonly CheckBox enhanced=new(){Content="精编笔记",IsChecked=true};
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,0)};
    private readonly TextBlock folderLabel=new(){Text="尚未选择文件夹",TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};
    private readonly Button choose=new(){Content="选择文件夹…",Padding=new Thickness(12,7,12,7)};
    private readonly Button close=new(){Content="取消",IsCancel=true};
    private readonly Button export=new(){Content="导出",IsDefault=true};
    private readonly Button openFolder=new(){Content="打开文件夹",Visibility=Visibility.Collapsed};
    private bool writing;
    internal string? Destination {get;private set;}

    public BulkNoteExportWindow(int courseCount,IReadOnlyList<ExportNote> notes)
    {
        this.courseCount=courseCount;
        this.notes=notes;
        Title="批量导出笔记";
        Width=460;
        SizeToContent=SizeToContent.Height;
        ResizeMode=ResizeMode.NoResize;
        WindowStyle=WindowStyle.None;
        AllowsTransparency=true;
        Background=Brushes.Transparent;
        ShowInTaskbar=false;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;

        var panel=new StackPanel{Margin=new Thickness(24)};
        panel.Children.Add(new TextBlock{Text=Title,FontSize=20,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,18)});

        var kinds=new StackPanel{Orientation=Orientation.Horizontal};
        kinds.Children.Add(classroom);
        kinds.Children.Add(enhanced);
        panel.Children.Add(kinds);

        var folderRow=new DockPanel{Margin=new Thickness(0,16,0,0)};
        DockPanel.SetDock(choose,Dock.Right);
        choose.Margin=new Thickness(10,0,0,0);
        folderRow.Children.Add(choose);
        folderRow.Children.Add(folderLabel);
        panel.Children.Add(folderRow);

        status.Foreground=(Brush)Application.Current.FindResource("TextSecondary");
        AutomationProperties.SetLiveSetting(status,AutomationLiveSetting.Polite);
        panel.Children.Add(status);

        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};
        export.Style=(Style)Application.Current.FindResource("Primary");
        actions.Children.Add(close);
        actions.Children.Add(openFolder);
        actions.Children.Add(export);
        panel.Children.Add(actions);
        Content=new Border
        {
            Background=Brushes.White,BorderBrush=(Brush)Application.Current.FindResource("Line"),
            BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Child=panel
        };

        classroom.Checked+=(_,_)=>UpdatePreview();
        classroom.Unchecked+=(_,_)=>UpdatePreview();
        enhanced.Checked+=(_,_)=>UpdatePreview();
        enhanced.Unchecked+=(_,_)=>UpdatePreview();
        choose.Click+=(_,_)=>{
            var picker=new Microsoft.Win32.OpenFolderDialog{Title="选择笔记导出文件夹"};
            if(Destination!=null)picker.InitialDirectory=Destination;
            if(picker.ShowDialog(this)==true)SetDestination(picker.FolderName);
        };
        close.Click+=(_,_)=>Close();
        export.Click+=async (_,_)=>await ExportAsync();
        openFolder.Click+=(_,_)=>{
            try { CaptureService.OpenDirectory(Destination!); }
            catch(Exception error) { status.Text+="\n无法打开文件夹："+error.Message; }
        };
        UpdatePreview();
    }

    internal void SetDestination(string path)
    {
        Destination=path;
        folderLabel.Text=path;
        folderLabel.ToolTip=path;
        UpdatePreview();
    }

    private ExportNote[] SelectedNotes()=>notes.Where(note=>
        note.Kind==ExportNoteKind.Classroom?classroom.IsChecked==true:enhanced.IsChecked==true).ToArray();

    private int SkippedCourses(IReadOnlyList<ExportNote> selected)=>courseCount-selected.Select(note=>note.CourseId).Distinct().Count();

    private void UpdatePreview()
    {
        var selected=SelectedNotes();
        var skipped=SkippedCourses(selected);
        status.Text=classroom.IsChecked!=true&&enhanced.IsChecked!=true
            ?"请选择至少一种笔记。"
            :$"已选 {courseCount} 条录音，预计导出 {selected.Length} 个 Markdown 文件。"+
             (skipped>0?$" {skipped} 条录音没有所选类型的笔记。":"");
        export.IsEnabled=selected.Length>0&&Destination!=null;
    }

    private async Task ExportAsync()
    {
        var selected=SelectedNotes();
        if(writing||Destination is not { } destination||selected.Length==0)return;
        writing=true;
        classroom.IsEnabled=enhanced.IsEnabled=choose.IsEnabled=close.IsEnabled=export.IsEnabled=false;
        status.Text="正在导出笔记…";
        try
        {
            var result=await Task.Run(()=>BulkNoteExport.Write(destination,selected));
            status.Text=$"已导出 {result.Written} 个文件，跳过 {SkippedCourses(selected)} 条录音。";
            if(result.Failures.FirstOrDefault() is { } first)
                status.Text+=$"\n{result.Failures.Count} 个文件写入失败。\n{first.FileName}：{first.Message}";
            openFolder.Visibility=result.Written>0?Visibility.Visible:Visibility.Collapsed;
        }
        catch(Exception error)
        {
            status.Text="导出失败："+error.Message;
        }
        finally
        {
            writing=false;
            close.IsEnabled=true;
            close.Content="关闭";
            export.Visibility=Visibility.Collapsed;
            close.Focus();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel=writing;
        base.OnClosing(e);
    }
}
