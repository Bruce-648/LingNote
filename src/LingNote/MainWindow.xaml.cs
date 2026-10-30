using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.IO;
using System.Threading.Channels;
using System.Diagnostics;

namespace LingNote;
public partial class MainWindow : Window
{
    private readonly Store store=new();
    private readonly Preferences prefs=Preferences.Load();
    private readonly Transcriber asr=new();
    private readonly NoteService ai=new();
    private readonly AudioRecorder recorder=new();
    private readonly FloatingBar floating;
    private readonly DispatcherTimer ticker=new() {Interval=TimeSpan.FromMilliseconds(200)};
    private readonly DispatcherTimer autosave=new() {Interval=TimeSpan.FromMilliseconds(700)};
    private readonly System.Windows.Forms.NotifyIcon tray;
    private Course? selected;
    private Course? recording;
    private Channel<AudioPart>? queue;
    private Task? worker;
    private CancellationTokenSource? processing;
    private bool paused,busy,exiting,loadingEditor,transcriptView;
    private int ticks;
    private bool shown;
    public MainWindow()
    {
        InitializeComponent(); SetupDesign(); SettingsButton.Content=UiIcons.Label("settings","设置"); PauseButton.Content=UiIcons.Label("pause","暂停"); floating=new FloatingBar(prefs,()=>Pause_Click(this,new RoutedEventArgs()),()=>Stop_Click(this,new RoutedEventArgs()),Restore);
        using var iconStream=System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/LingNote;component/Assets/app.ico")).Stream;
        tray=new System.Windows.Forms.NotifyIcon {Icon=new System.Drawing.Icon(iconStream),Text="LingNote",Visible=true};
        tray.MouseClick+=(_,e)=>Dispatcher.BeginInvoke(()=> {if(e.Button==System.Windows.Forms.MouseButtons.Right) ShowTrayMenu(); else if(e.Button==System.Windows.Forms.MouseButtons.Left) Restore();});
        tray.DoubleClick+=(_,_)=>Dispatcher.BeginInvoke(Restore);
        recorder.PartReady+=part=>Dispatcher.BeginInvoke(()=> {if(recording==null) return; recording.Parts.Add(part); recording.Seconds=part.Offset+part.Duration; store.Save(recording); queue?.Writer.TryWrite(part);});
        recorder.Fault+=message=>Dispatcher.BeginInvoke(()=> {if(recording!=null && !paused) {Pause_Click(this,new RoutedEventArgs()); Hint.Text=message;} });
        ticker.Tick+=(_,_)=> { if(recording!=null) {var seconds=recorder.Elapsed; TimerLabel.Text=TimeSpan.FromSeconds(seconds).ToString("hh\\:mm\\:ss"); if(!paused) LevelBar.Push(recorder.Level); floating.Update(seconds,recorder.Level,paused); if(++ticks%25==0) { recording.Seconds=seconds; store.Save(recording); } } };
        ticker.Start(); autosave.Tick+=(_,_)=> {autosave.Stop(); if(selected!=null && !transcriptView) { selected.Note=Editor.Text; store.Save(selected); }};
        Deactivated+=(_,_)=> {if(recording!=null && !exiting) floating.Show();}; Activated+=(_,_)=>floating.Hide(); StateChanged+=(_,_)=> {if(WindowState==WindowState.Minimized && recording!=null) floating.Show();};
        Closing+=(_,e)=> {if(!exiting) { e.Cancel=true; Hide(); if(recording!=null) floating.Show(); else if(!shown) { tray.ShowBalloonTip(2500,"LingNote 仍在运行","可从系统托盘重新打开，或右键退出。",System.Windows.Forms.ToolTipIcon.Info); shown=true; }}};
        foreach(var course in store.All()) Recover(course);
        RefreshHistory();
        Loaded+=(_,_)=> { if(!Transcriber.ModelAvailable) Hint.Text="首次使用：到「设置」下载语音模型（约 466 MB）。也可以先录音。";
            WriteStartupDiagnostic();_ = CheckUpdatesAtStartup();
        };
    }
    private void WriteStartupDiagnostic()
    {
        try {
            var report=System.Text.Json.JsonSerializer.Serialize(new {
                Time=DateTimeOffset.Now,Version=typeof(App).Assembly.GetName().Version?.ToString(),
                Executable=Environment.ProcessPath,DataDirectory=Paths.Root,
                StoredRecords=store.All().Count,VisibleItems=History.Items.Count,
                SidebarVisible=Sidebar.IsVisible,ListVisible=History.IsVisible,
                ListWidth=History.ActualWidth,ListHeight=History.ActualHeight
            });
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"startup-status.json"),report);
        } catch(IOException) {} catch(UnauthorizedAccessException) {}
    }
    private void Recover(Course course)
    {
        if(course.State is not ("录音中" or "已暂停" or "正在转写" or "正在整理")) return;
        if(Directory.Exists(course.Folder)) foreach(var file in Directory.GetFiles(course.Folder,"part-*.wav").Order()) {
            if(course.Parts.Any(p=>p.File.Equals(file,StringComparison.OrdinalIgnoreCase))) continue;
            try {using var wave=new NAudio.Wave.WaveFileReader(file); if(wave.TotalTime.TotalSeconds>.05) course.Parts.Add(new AudioPart {File=file,Offset=course.Parts.Sum(p=>p.Duration),Duration=wave.TotalTime.TotalSeconds});} catch { }
        }
        course.Seconds=course.Parts.Sum(p=>p.Duration); course.State="可恢复"; course.Error="上次运行中断，已保留可恢复的录音。点击继续整理。"; store.Save(course);
    }
    private void RefreshHistory(string? select=null)
    {
        RefreshEmptyTopics(); var list=store.All().Where(c=>string.IsNullOrEmpty(c.Topic)&&LibrarySections.SectionOf(prefs,c)=="").ToList(); var visible=list.Take(showAllUnassigned?int.MaxValue:15).ToList(); var view=new System.Windows.Data.ListCollectionView(visible);
        UnassignedExpand.Tag=list.Count>15; UnassignedExpand.Content=showAllUnassigned?"收起":"展开显示";
        if(prefs.GroupUnassignedByDay)view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(Course.DayGroup)));
        History.ItemsSource=prefs.GroupUnassignedByDay?view:visible; History.Items.Refresh();
        File.WriteAllText(Path.Combine(Paths.Root,"library-status.json"),System.Text.Json.JsonSerializer.Serialize(new {DataDirectory=Paths.Root,StoredRecords=store.All().Count,UnassignedRecords=list.Count,VisibleItems=History.Items.Count,Grouped=prefs.GroupUnassignedByDay}));
        UpdateSections();
        if(select!=null) {var target=store.All().FirstOrDefault(c=>c.Id==select);if(target!=null)ShowCourse(target);}
    }
    private void ShowCourse(Course course)
    {
        if(recording?.Id==course.Id)course=recording;
        SaveEditor();draftSection=LibrarySections.SectionOf(prefs,course); SetActiveTopic(course.Topic);course.IsRead=true;store.Save(course); selected=course; TitleBox.Text=course.Title; Meta.Text=course.Caption; StatusLabel.Text=recording?.State??course.State; TimerLabel.Text=TimeSpan.FromSeconds(recording!=null?recorder.Elapsed:course.Seconds).ToString("hh\\:mm\\:ss"); Hint.Text=course.Error; transcriptView=false; EditToggle.IsChecked=false; Render(); GenerateButton.IsEnabled=!busy && recording==null; GenerateButton.Content=course.State=="已完成"?"重新整理":"继续整理";
    }
    private void Render()
    {
        var text=transcriptView?selected?.Transcript:selected?.Note;
        loadingEditor=true; Editor.Text=text??""; loadingEditor=false;
        EmptyPanel.Visibility=string.IsNullOrWhiteSpace(text)?Visibility.Visible:Visibility.Collapsed;
        Preview.Document=string.IsNullOrWhiteSpace(text)?new():MarkdownView.Render(text);
        Preview.Visibility=EditToggle.IsChecked==true?Visibility.Collapsed:Visibility.Visible;
        Editor.Visibility=EditToggle.IsChecked==true?Visibility.Visible:Visibility.Collapsed;
        Editor.IsReadOnly=transcriptView || busy; UpdateControls();
    }
    private void SaveEditor() { autosave.Stop(); if(selected!=null && !transcriptView && !loadingEditor && EditToggle.IsChecked==true && !busy) {selected.Note=Editor.Text; store.Save(selected);} }
    private async void Start_Click(object sender,RoutedEventArgs e)
    {
        if(recording!=null || busy) return;
        try {
            SaveEditor(); LevelBar.Clear(); var course=new Course {Topic=draftTopic,SectionId=draftSection}; if(selected==null && TitleBox.Text.Trim() is {Length:>0} title && title!="新录音") course.Title=title; store.Save(course); recording=course; paused=false; PauseButton.Content=UiIcons.Label("pause","暂停");
            queue=Channel.CreateUnbounded<AudioPart>(new UnboundedChannelOptions {SingleReader=true});
            processing=new(); worker=Consume(queue.Reader,course,processing.Token);
            recorder.Begin(course.Folder); RefreshHistory(); ShowCourse(course); History.IsEnabled=true; NewButton.IsEnabled=false; PauseButton.IsEnabled=StopButton.IsEnabled=true; GenerateButton.IsEnabled=false; UpdateControls(); StatusLabel.Text="录音中"; Hint.Text="录制默认播放设备的声音。可以最小化窗口，继续专心听课。";
        } catch(Exception ex) {recording=null; queue?.Writer.TryComplete(); if(worker!=null) await worker; SetBusy(false); ShowError(ex);}
    }
    private async Task Consume(ChannelReader<AudioPart> reader,Course course,CancellationToken ct)
    {
        await foreach(var part in reader.ReadAllAsync(ct)) {
            if(!Transcriber.ModelAvailable) continue;
            try {var context=course.Parts.LastOrDefault(p=>p.Done)?.Text??""; var text=await asr.Run(part,ct,context); await Dispatcher.InvokeAsync(()=> {part.Text=text; part.Done=true; store.Save(course);});}
            catch(OperationCanceledException) {break;}
            catch(Exception ex) {await Dispatcher.InvokeAsync(()=>Hint.Text="录音继续保存；后台转写稍后重试："+ex.Message);}
        }
    }
    private void Pause_Click(object sender,RoutedEventArgs e)
    {
        if(recording==null) return;
        try {if(paused) recorder.Resume(); else recorder.Pause(); paused=!paused; PauseButton.Content=UiIcons.Label(paused?"play":"pause",paused?"继续":"暂停"); recording.State=paused?"已暂停":"录音中"; StatusLabel.Text=recording.State;floating.Update(recorder.Elapsed,recorder.Level,paused); store.Save(recording);} catch(Exception ex) {ShowError(ex);}
    }
    private async void Stop_Click(object sender,RoutedEventArgs e)
    {
        if(recording==null || busy) return;
        Restore(); busy=true; UpdateControls();StopButton.IsEnabled=PauseButton.IsEnabled=false;
        var course=recording; ShowCourse(course);
        try {
            recorder.Pause(); floating.Hide(); await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Background); // drain committed-part notifications before closing queue
            course.Seconds=recorder.TotalSeconds; course.State="正在转写"; store.Save(course); queue?.Writer.TryComplete(); recording=null;
            SetBusy(true); StatusLabel.Text="正在完成转写"; if(worker!=null) await worker;
            await ProcessCourse(course,false);
        } catch(Exception ex) {course.State="待重试"; course.Error=ex.Message; store.Save(course); ShowError(ex);} finally {recording=null; SetBusy(false); RefreshHistory(course.Id);}
    }
    private async Task ProcessCourse(Course course,bool regenerate)
    {
        processing?.Dispose(); processing=new(); var ct=processing.Token;
        if(!Transcriber.ModelAvailable) throw new InvalidOperationException("录音已保存。请到设置中安装本地模型，再点击「继续整理」。");
        course.State="正在转写"; store.Save(course);
        for(int i=0;i<course.Parts.Count;i++) {var part=course.Parts[i]; if(part.Done) continue; StatusLabel.Text=$"正在转写 {i+1}/{course.Parts.Count}"; part.Text=await asr.Run(part,ct,i>0?course.Parts[i-1].Text:""); part.Done=true; store.Save(course);}
        if(string.IsNullOrWhiteSpace(course.Transcript)) throw new InvalidOperationException("未识别到有效讲话。录音已保留，可回放检查声音。");
        if(regenerate) {course.Drafts.Clear(); course.DraftFingerprint="";}
        course.State="正在整理"; store.Save(course); StatusLabel.Text=course.State; Hint.Text="正在根据课程内容梳理笔记…";
        var preview=new System.Text.StringBuilder(); var progress=new Progress<string>(s=>StatusLabel.Text=s); var lastPreview=Stopwatch.StartNew();
        var note=await ai.Generate(course,prefs,store,progress,delta=> {preview.Append(delta); if(lastPreview.ElapsedMilliseconds<250)return; lastPreview.Restart(); var snapshot=preview.ToString(); Dispatcher.BeginInvoke(()=> {loadingEditor=true; Editor.Text=snapshot; loadingEditor=false; EmptyPanel.Visibility=Visibility.Collapsed; if(EditToggle.IsChecked!=true) Preview.Document=MarkdownView.Render(snapshot);});},ct);
        if(!string.IsNullOrEmpty(course.Note)) course.Versions.Add(course.Note);
        course.Note=note; course.State="已完成"; course.Error=""; store.Save(course); Hint.Text="笔记已自动保存，可以复制、编辑或导出。";
    }
    private async void Generate_Click(object sender,RoutedEventArgs e)
    {
        if(selected==null || busy || recording!=null) return;
        SaveEditor(); var course=selected;
        SetBusy(true);
        try {await ProcessCourse(course,course.State=="已完成" && !string.IsNullOrEmpty(course.Note));}
        catch(OperationCanceledException) {course.State="已取消"; course.Error="处理已取消，录音和已完成的内容仍保留。";}
        catch(Exception ex) {course.State="待重试"; course.Error=ex.Message; ShowError(ex);}
        finally {store.Save(course); SetBusy(false); RefreshHistory(course.Id);}
    }
    private void SetBusy(bool value) {busy=value; NewButton.IsEnabled=!value && recording==null; History.IsEnabled=!value; GenerateButton.IsEnabled=!value && selected!=null && recording==null; CancelButton.Visibility=value?Visibility.Visible:Visibility.Collapsed; EditToggle.IsEnabled=!value; UpdateControls();}
    private void ShowError(Exception ex) {Hint.Text=ex is OperationCanceledException?"处理已取消，录音仍保留。":ex.Message; StatusLabel.Text="需要处理";}
    private void History_Changed(object sender,SelectionChangedEventArgs e) {if(History.SelectedItem is Course course && !busy) ShowCourse(course);}
    private void Title_Changed(object sender,RoutedEventArgs e) {if(selected==null) return; var title=TitleBox.Text.Trim(); if(title==selected.Title)return; if(title.Length==0) {TitleBox.Text=selected.Title;return;} selected.Title=title; store.Save(selected); if(recording==null && !busy) RefreshHistory(selected.Id);}
    private void Settings_Click(object sender,RoutedEventArgs e) {if(busy || recording!=null) {Hint.Text="请先结束当前录音或处理任务，再修改设置。";return;} var settings=new SettingsWindow(prefs) {Owner=this};settings.ShowDialog();if(settings.PendingInstaller is string installer&&settings.PendingInstallerHash is string hash)_ = InstallUpdate(installer,hash);}
    private void Folder_Click(object sender,RoutedEventArgs e)=>Process.Start(new ProcessStartInfo(Paths.Root) {UseShellExecute=true});
    private void NoteTab_Click(object sender,RoutedEventArgs e) {if(busy)return; SaveEditor(); transcriptView=false; EditToggle.IsChecked=false; Render();}
    private void TranscriptTab_Click(object sender,RoutedEventArgs e) {if(busy)return; SaveEditor(); transcriptView=true; EditToggle.IsChecked=false; Render();}
    private void Edit_Changed(object sender,RoutedEventArgs e) {if(Editor==null)return; if(EditToggle.IsChecked!=true) SaveEditor(); Render();}
    private void Editor_Changed(object sender,TextChangedEventArgs e) {if(loadingEditor || busy || transcriptView || selected==null) return; selected.Note=Editor.Text; autosave.Stop(); autosave.Start();}
    private void Copy_Click(object sender,RoutedEventArgs e) {SaveEditor(); var text=transcriptView?selected?.Transcript:selected?.Note; if(!string.IsNullOrEmpty(text)) {System.Windows.Clipboard.SetText(text); Hint.Text="已复制到剪贴板。";}}
    private void Export_Click(object sender,RoutedEventArgs e)
    {
        SaveEditor(); if(selected==null || string.IsNullOrWhiteSpace(selected.Note)) return; var invalid=Path.GetInvalidFileNameChars(); var name=new string(selected.Title.Select(c=>invalid.Contains(c)?'_':c).ToArray()); var dialog=new Microsoft.Win32.SaveFileDialog {Filter="Markdown 文档|*.md",FileName=name+".md"}; if(dialog.ShowDialog(this)==true) {File.WriteAllText(dialog.FileName,selected.Note,new System.Text.UTF8Encoding(false)); Hint.Text="已导出："+dialog.FileName;}
    }
    private void Cancel_Click(object sender,RoutedEventArgs e)=>processing?.Cancel();
    private void More_Click(object sender,RoutedEventArgs e)
    {
        if(selected==null || busy || recording!=null) return;
        SaveEditor(); var target=selected;
        var popup=new ActionPopup((FrameworkElement)sender);
        popup.Add("回放录音片段", "play", ()=> {var dialog=new PlaybackWindow(target) {Owner=this};dialog.ShowDialog();},target.Parts.Count>0);
        popup.Add("恢复上一版笔记", "restore", ()=> {var last=target.Versions[^1];target.Versions.RemoveAt(target.Versions.Count-1);target.Versions.Insert(0,target.Note);target.Note=last;store.Save(target);ShowCourse(target);},target.Versions.Count>0);
        popup.Separator();
        popup.Add("删除课程和录音", "trash", ()=>DeleteCourse(target));
        popup.Open();
    }
    private void DeleteCourse(Course course)
    {
        if(!ConfirmWindow.Ask(this,"删除课程？","将删除这门课程的笔记和录音，此操作无法撤销。"))return;
        autosave.Stop();
        try {store.Delete(course);selected=null;loadingEditor=true;Editor.Clear();loadingEditor=false;EditToggle.IsChecked=false;Preview.Document=new();EmptyPanel.Visibility=Visibility.Visible;TitleBox.Text="新录音";Meta.Text="系统声音";StatusLabel.Text="准备录音";TimerLabel.Text="00:00:00";GenerateButton.IsEnabled=false;Hint.Text="课程已删除。";RefreshHistory();UpdateControls();LevelBar.Clear();}
        catch(Exception ex){ShowError(ex);}
    }

    internal void Restore() {if(exiting)return;Show(); if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal; Activate(); WindowActivation.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle); floating.Hide();}
    private async void Exit()
    {
        if(exiting)return;
        if(recording!=null) {await EndWithoutSummary();}
        if(busy) {processing?.Cancel(); Hint.Text="正在取消处理并保存，请稍后从托盘退出。"; return;}
        SaveEditor(); exiting=true; trayMenu?.Dismiss(); ticker.Stop(); autosave.Stop(); toastTimer.Stop(); floating.Close(); recorder.Dispose(); asr.Dispose(); tray.Dispose(); System.Windows.Application.Current.Shutdown();
    }
    private async Task EndWithoutSummary()
    {
        SetBusy(true); var course=recording!; recorder.Pause(); await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Background); queue?.Writer.TryComplete(); recording=null; if(worker!=null) await worker; course.Seconds=recorder.TotalSeconds; course.State="等待整理"; store.Save(course); SetBusy(false);
    }
}
