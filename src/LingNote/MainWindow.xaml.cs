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
    private CancellationTokenSource? recordingCancellation;
    private readonly Dictionary<string,Course> processingCourses=new();
    private readonly Dictionary<string,CancellationTokenSource> processingTokens=new();
    private readonly SemaphoreSlim processingGate=new(1,1);
    private bool stoppingRecording;
    private bool SelectedProcessing=>selected!=null&&processingCourses.ContainsKey(selected.Id);
    private bool paused,busy,exiting,loadingEditor,transcriptView;
    private bool editorDirty,loadingTitle,titleDirty;
    private string originalTitle="";
    private int ticks;
    private bool shown;
    public MainWindow()
    {
        AppTheme.Apply(prefs);InitializeComponent(); SetupDesign(); SettingsButton.Content=UiIcons.Label("settings","设置"); PauseButton.Content=UiIcons.Label("pause","暂停"); floating=new FloatingBar(prefs,()=>Pause_Click(this,new RoutedEventArgs()),()=>Stop_Click(this,new RoutedEventArgs()),Restore,region=>CaptureScreenshot(region));
        SourceInitialized+=(_,_)=>InitializeCaptureHotkeys();
        using var iconStream=System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/LingNote;component/Assets/app.ico")).Stream;
        tray=new System.Windows.Forms.NotifyIcon {Icon=new System.Drawing.Icon(iconStream),Text="LingNote",Visible=true};
        tray.MouseClick+=(_,e)=>Dispatcher.BeginInvoke(()=> {if(e.Button==System.Windows.Forms.MouseButtons.Right) ShowTrayMenu(); else if(e.Button==System.Windows.Forms.MouseButtons.Left) Restore();});
        tray.DoubleClick+=(_,_)=>Dispatcher.BeginInvoke(Restore);
        recorder.PartReady+=part=>Dispatcher.BeginInvoke(()=> {if(recording==null) return; recording.Parts.Add(part); recording.Seconds=part.Offset+part.Duration; store.Save(recording); queue?.Writer.TryWrite(part);});
        recorder.Fault+=message=>Dispatcher.BeginInvoke(()=> {if(recording!=null && !paused) {Pause_Click(this,new RoutedEventArgs()); Hint.Text=message;} });
        ticker.Tick+=(_,_)=> { if(recording!=null) {var seconds=recorder.Elapsed; TimerLabel.Text=TimeSpan.FromSeconds(seconds).ToString("hh\\:mm\\:ss"); if(!paused) LevelBar.Push(recorder.Level); floating.Update(seconds,recorder.Level,paused); if(++ticks%25==0) { recording.Seconds=seconds; store.Save(recording); } } };
        PreviewKeyDown+=(_,e)=>{if(bulkKind!=null&&e.Key==System.Windows.Input.Key.Escape){EndBulkSelection();e.Handled=true;}};
        ticker.Start(); autosave.Tick+=(_,_)=> {autosave.Stop(); if(selected!=null && !transcriptView) { store.Save(selected);savedAt[selected.Id]=DateTime.Now;editorDirty=false;UpdateControls(); }};
        Deactivated+=(_,_)=> {if(recording!=null && !exiting && !capturing) floating.Show();}; Activated+=(_,_)=>floating.Hide(); StateChanged+=(_,_)=> {if(WindowState==WindowState.Minimized && recording!=null) floating.Show();};
        Closing+=(_,e)=> {if(!LeaveSettings()){e.Cancel=true;return;}if(!exiting) { e.Cancel=true; Hide(); if(recording!=null) floating.Show(); else if(!shown) { tray.ShowBalloonTip(2500,"LingNote 仍在运行","可从系统托盘重新打开，或右键退出。",System.Windows.Forms.ToolTipIcon.Info); shown=true; }}};
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
        if(course.State is not ("录音中" or "已暂停" or "正在转写" or "正在整理" or "等待处理" or "正在完善" or "正在精编")) return;
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
        UpdateSections();UpdateBulkSelection();
        if(titleDirty&&selected!=null)UpdateSidebarTitle(selected.Id,TitleBox.Text);
        if(select!=null) {var target=store.All().FirstOrDefault(c=>c.Id==select);if(target!=null)ShowCourse(target);}
    }
    private void ShowCourse(Course course)
    {
        if(!LeaveSettings())return;
        if(titleDirty&&selected?.Id==course.Id){CommitTitle();course=selected;}
        if(recording?.Id==course.Id)course=recording;
        else if(processingCourses.TryGetValue(course.Id,out var pending))course=pending;
        SaveEditor();draftSection=LibrarySections.SectionOf(prefs,course); SetActiveTopic(course.Topic);if(!course.IsRead){course.IsRead=true;store.Save(course);} selected=course; loadingTitle=true;TitleBox.Text=course.Title;loadingTitle=false;titleDirty=false;originalTitle=course.Title; Meta.Text=course.Created.ToLocalTime().ToString("M月d日")+" · "+(recording?.Id==course.Id?"":TimeSpan.FromSeconds(course.Seconds).ToString("hh\\:mm\\:ss")+" · ")+(string.IsNullOrWhiteSpace(course.Topic)?"未分区":course.Topic)+(recording?.Id==course.Id?" · 系统声音":""); StatusLabel.Text=recording?.State??course.State; TimerLabel.Text=TimeSpan.FromSeconds(recording!=null?recorder.Elapsed:course.Seconds).ToString("hh\\:mm\\:ss"); Hint.Text=course.Error; transcriptView=false; EditToggle.IsChecked=false; Render(); GenerateButton.IsEnabled=!SelectedProcessing && recording?.Id!=course.Id; GenerateButton.Content=course.State=="已完成"?"重新整理":"继续整理";
    }
    private string? CurrentNote(Course? course)=>course?.DirectRefinement==true?course.EnhancedNote:course?.Note;
    private void SetCurrentNote(Course course,string text) {if(course.DirectRefinement)course.EnhancedNote=text;else course.Note=text;}
    private void Render()
    {
        var text=transcriptView?selected?.Transcript:CurrentNote(selected);
        loadingEditor=true; Editor.Text=text??""; loadingEditor=false;
        EmptyPanel.Visibility=string.IsNullOrWhiteSpace(text)?Visibility.Visible:Visibility.Collapsed;
        loadingEditor=true;Preview.Document=string.IsNullOrWhiteSpace(text)?new():MarkdownView.Render(text);loadingEditor=false;
        Preview.Visibility=EditToggle.IsChecked==true?Visibility.Collapsed:Visibility.Visible;
        Editor.Visibility=EditToggle.IsChecked==true?Visibility.Visible:Visibility.Collapsed;
        loadingEditor=true;EnhancedPreview.Document=MarkdownView.Render(selected?.EnhancedNote??"");loadingEditor=false;
        Editor.IsReadOnly=Preview.IsReadOnly=transcriptView || SelectedProcessing; UpdateControls();
    }
    private void SaveEditor() { CommitTitle();autosave.Stop(); if(editorDirty&&selected!=null && !loadingEditor && !SelectedProcessing) {store.Save(selected);savedAt[selected.Id]=DateTime.Now;editorDirty=false;} }
    private void Preview_Changed(object sender,TextChangedEventArgs e)
    {
        if(loadingEditor||transcriptView||SelectedProcessing||selected==null||e.Changes.Count==0)return;
        editorDirty=true;SetCurrentNote(selected,MarkdownView.ToMarkdown(Preview.Document));autosave.Stop();autosave.Start();UpdateControls();
    }
    private async void Start_Click(object sender,RoutedEventArgs e)
    {
        if(recording!=null || stoppingRecording) return;
        try {
            SaveEditor(); LevelBar.Clear(); var course=new Course {Topic=draftTopic,SectionId=draftSection,DirectRefinement=prefs.GenerationMode=="direct"}; if(selected==null && TitleBox.Text.Trim() is {Length:>0} title && title!="新录音") course.Title=title; store.Save(course); recording=course; paused=false; PauseButton.Content=UiIcons.Label("pause","暂停");
            queue=Channel.CreateUnbounded<AudioPart>(new UnboundedChannelOptions {SingleReader=true});
            recordingCancellation=new(); worker=Consume(queue.Reader,course,recordingCancellation.Token);
            recorder.Begin(course.Folder); RefreshHistory(); ShowCourse(course); History.IsEnabled=true; NewButton.IsEnabled=false; PauseButton.IsEnabled=StopButton.IsEnabled=true; GenerateButton.IsEnabled=false; UpdateControls(); StatusLabel.Text="录音中"; Hint.Text="录制默认播放设备的声音。可以最小化窗口，继续专心听课。";
        } catch(Exception ex) {recording=null; queue?.Writer.TryComplete(); if(worker!=null) await worker; SetBusy(processingCourses.Count>0); ShowError(ex);}
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
        if(recording==null||stoppingRecording)return;
        stoppingRecording=true;Restore();var course=recording;ShowCourse(course);
        Task? finishedWorker=null;CancellationTokenSource? finishedCancellation=null;
        try {
            recorder.Pause();floating.Hide();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Background);
            course.Seconds=recorder.TotalSeconds;course.State="正在转写";store.Save(course);
            queue?.Writer.TryComplete();finishedWorker=worker;recording=null;queue=null;worker=null;
            finishedCancellation=recordingCancellation;recordingCancellation=null;
        } catch(Exception ex) {course.State="待重试";course.Error=ex.Message;store.Save(course);ShowError(ex);return;}
        finally {stoppingRecording=false;UpdateControls();}
        await RunProcessing(course,false,finishedWorker,finishedCancellation);
    }
    private async Task RunProcessing(Course course,bool regenerate,Task? capturedWorker=null,CancellationTokenSource? capturedCancellation=null)
    {
        if(processingCourses.ContainsKey(course.Id))return;
        var cancellation=new CancellationTokenSource();processingCourses.Add(course.Id,course);processingTokens.Add(course.Id,cancellation);
        using var cancelCapture=cancellation.Token.Register(()=>capturedCancellation?.Cancel());
        course.State="等待处理";store.Save(course);SetBusy(true);bool entered=false;
        try {
            // The preceding capture keeps its own worker and cancellation source.
            // Another recording may start while this course waits or is summarized.
            if(capturedWorker!=null){var waiting=Stopwatch.StartNew();var remaining=course.Parts.Count(p=>!p.Done);await capturedWorker;PerformanceLog.Record("recording_tail_wait",waiting.Elapsed.TotalMilliseconds,remaining);}
            if(!enhanceRequests.Contains(course.Id)){await processingGate.WaitAsync(cancellation.Token);entered=true;}
            await ProcessCourse(course,regenerate,cancellation.Token);
        } catch(OperationCanceledException) {course.State="已取消";course.Error="处理已取消，录音和已完成的内容仍保留。";}
        catch(Exception ex) {course.State="待重试";course.Error=ex.Message;if(selected?.Id==course.Id)ShowError(ex);}
        finally {
            if(entered)processingGate.Release();capturedCancellation?.Dispose();
            store.Save(course);processingCourses.Remove(course.Id);processingTokens.Remove(course.Id);cancellation.Dispose();
            SetBusy(processingCourses.Count>0);RefreshHistory();
            if(selected?.Id==course.Id)ShowCourse(course);
        }
    }
    private void ProcessingStatus(Course course,string text) {if(selected?.Id==course.Id&&recording==null&&processingCourses.ContainsKey(course.Id))StatusLabel.Text=text;}
    private async Task ProcessCourse(Course course,bool regenerate,CancellationToken ct)
    {
        if(enhanceRequests.Remove(course.Id)){await CompleteEnhancement(course,ct);return;}
        if(course.Parts.Any(p=>!p.Done)&&!Transcriber.ModelAvailable) throw new InvalidOperationException("录音已保存。请到设置中安装本地模型，再点击「继续整理」。");
        course.State="正在转写"; store.Save(course);
        var transcriptionTimer=Stopwatch.StartNew();var pendingParts=course.Parts.Count(p=>!p.Done);
        for(int i=0;i<course.Parts.Count;i++) {var part=course.Parts[i]; if(part.Done) continue; ProcessingStatus(course,$"正在转写 {i+1}/{course.Parts.Count}"); part.Text=await asr.Run(part,ct,i>0?course.Parts[i-1].Text:""); part.Done=true; store.Save(course);}
        PerformanceLog.Record("transcription_remaining",transcriptionTimer.Elapsed.TotalMilliseconds,pendingParts);
        if(string.IsNullOrWhiteSpace(course.Transcript)) throw new InvalidOperationException("未识别到有效讲话。录音已保留，可回放检查声音。");
        if(regenerate) {if(course.DirectRefinement){course.EnhancedDrafts.Clear();course.EnhancedFingerprint="";}else{course.Drafts.Clear();course.DraftFingerprint="";}}
        course.State="正在整理"; store.Save(course); ProcessingStatus(course,course.State);
        var preview=new System.Text.StringBuilder(); var progress=new Progress<string>(s=>ProcessingStatus(course,s)); var lastPreview=Stopwatch.StartNew();
        void ShowPreview(string delta) {if(course.DirectRefinement)preview.Clear();preview.Append(delta);if(lastPreview.ElapsedMilliseconds<250)return;lastPreview.Restart();var snapshot=preview.ToString();Dispatcher.BeginInvoke(()=> {if(selected?.Id!=course.Id||transcriptView||!processingCourses.ContainsKey(course.Id))return;loadingEditor=true;Editor.Text=snapshot;EmptyPanel.Visibility=Visibility.Collapsed;if(EditToggle.IsChecked!=true)Preview.Document=MarkdownView.Render(snapshot);loadingEditor=false;});}
        var note=course.DirectRefinement
            ? await ai.Enhance(course,prefs,store,progress,ct,ShowPreview,fromTranscript:true)
            : await ai.Generate(course,prefs,store,progress,ShowPreview,ct);
        if(course.DirectRefinement){if(!string.IsNullOrEmpty(course.EnhancedNote))course.EnhancedVersions.Add(course.EnhancedNote);course.EnhancedNote=note;course.EnhancedDrafts.Clear();course.EnhancedFingerprint="";}
        else {if(!string.IsNullOrEmpty(course.Note))course.Versions.Add(course.Note);course.Note=note;course.Drafts.Clear();course.DraftFingerprint="";}
        store.Save(course);
        course.State="已完成"; course.Error=""; store.Save(course); if(selected?.Id==course.Id)Hint.Text="笔记已自动保存，可以复制、直接编辑或导出。";
    }
    private async void Generate_Click(object sender,RoutedEventArgs e)
    {
        if(selected==null||SelectedProcessing||recording?.Id==selected.Id)return;
        SaveEditor();var course=selected;
        var resume=(course.State is "待重试" or "已取消")&&(course.DirectRefinement?course.EnhancedDrafts.Count:course.Drafts.Count)>0;
        await RunProcessing(course,!string.IsNullOrEmpty(CurrentNote(course))&&!resume);
    }
    private void SetBusy(bool value) {busy=value;History.IsEnabled=true;UpdateControls();}
    private void ShowError(Exception ex) {Hint.Text=ex is OperationCanceledException?"处理已取消，录音仍保留。":ex.Message; StatusLabel.Text="需要处理";}
    private void History_Changed(object sender,SelectionChangedEventArgs e) {if(History.SelectedItem is Course course) ShowCourse(course);}
    private void UpdateSidebarTitle(string id,string title)
    {
        foreach(var course in History.Items.OfType<Course>().Where(c=>c.Id==id))course.Title=title;
        if(topicRecordRows.TryGetValue(id,out var row)&&row.Tag is Course sectionCourse)sectionCourse.Title=title;
    }
    private void Title_TextChanged(object sender,TextChangedEventArgs e)
    {
        if(loadingTitle||selected==null)return;
        titleDirty=true;selected.Title=TitleBox.Text;UpdateSidebarTitle(selected.Id,TitleBox.Text);
    }
    private void CommitTitle()
    {
        if(!titleDirty||selected==null)return;
        var title=TitleBox.Text.Trim();if(title.Length==0)title=originalTitle;
        selected.Title=title;UpdateSidebarTitle(selected.Id,title);
        loadingTitle=true;TitleBox.Text=title;loadingTitle=false;
        originalTitle=title;titleDirty=false;store.Save(selected);
    }
    private void Title_Changed(object sender,RoutedEventArgs e)=>CommitTitle();
    private SettingsView? embeddedSettings;
    private GridLength previousSidebarWidth;
    private Visibility previousSidebarVisibility;
    private bool LeaveSettings()
    {
        if(embeddedSettings==null)return true;
        embeddedSettings.Close();
        if(!embeddedSettings.CanDetach)return false;
        Sidebar.Visibility=previousSidebarVisibility;SidebarColumn.Width=previousSidebarWidth;SidebarToggle.IsEnabled=true;SettingsHost.Content=null;embeddedSettings=null;SettingsHost.Visibility=Visibility.Collapsed;Workspace.Visibility=Visibility.Visible;SettingsButton.Background=System.Windows.Media.Brushes.Transparent;return true;
    }
    private void Settings_Click(object sender,RoutedEventArgs e)
    {

        if(embeddedSettings!=null)return;SaveEditor();
        embeddedSettings=new SettingsView(prefs){Owner=this};
        var settings=embeddedSettings;settings.BackRequested+=()=>LeaveSettings();
        previousSidebarWidth=SidebarColumn.Width;previousSidebarVisibility=Sidebar.Visibility;Sidebar.Visibility=Visibility.Collapsed;SidebarColumn.Width=new GridLength(0);SidebarToggle.IsEnabled=false;
        settings.Closed+=(_,_)=>{if(settings.PendingInstaller is string installer&&settings.PendingInstallerHash is string hash)_=InstallUpdate(installer,hash);};
        SettingsHost.Content=settings;
        SettingsHost.Visibility=Visibility.Visible;Workspace.Visibility=Visibility.Collapsed;SettingsButton.Background=(System.Windows.Media.Brush)FindResource("Selection");
    }
    private void Playback_Click(object sender,RoutedEventArgs e){if(selected?.Parts.Count>0)new PlaybackWindow(selected){Owner=this}.ShowDialog();}
    private void Float_Click(object sender,RoutedEventArgs e){if(recording!=null){floating.Show();WindowState=WindowState.Minimized;}}

    private void Folder_Click(object sender,RoutedEventArgs e)=>Process.Start(new ProcessStartInfo(Paths.Root) {UseShellExecute=true});
    private void NoteTab_Click(object sender,RoutedEventArgs e) {SaveEditor(); transcriptView=false; EditToggle.IsChecked=false; Render();}
    private void TranscriptTab_Click(object sender,RoutedEventArgs e) {SaveEditor(); transcriptView=true; EditToggle.IsChecked=false; Render();}
    private void Edit_Changed(object sender,RoutedEventArgs e) {if(Editor==null)return; if(EditToggle.IsChecked!=true) SaveEditor(); Render();}
    private void Editor_Changed(object sender,TextChangedEventArgs e) {if(loadingEditor || SelectedProcessing || transcriptView || selected==null) return; editorDirty=true;SetCurrentNote(selected,Editor.Text); autosave.Stop(); autosave.Start();UpdateControls();}
    private void Copy_Click(object sender,RoutedEventArgs e) {SaveEditor(); var text=transcriptView?selected?.Transcript:CurrentNote(selected); if(!string.IsNullOrEmpty(text)) {System.Windows.Clipboard.SetText(text); Hint.Text="已复制到剪贴板。";}}
    private void Export_Click(object sender,RoutedEventArgs e)
    {
        SaveEditor(); if(selected==null || string.IsNullOrWhiteSpace(CurrentNote(selected))) return; var invalid=Path.GetInvalidFileNameChars(); var name=new string(selected.Title.Select(c=>invalid.Contains(c)?'_':c).ToArray()); var dialog=new Microsoft.Win32.SaveFileDialog {Filter="Markdown 文档|*.md",FileName=name+(selected.DirectRefinement?"_精编笔记.md":"_课堂笔记.md")}; if(dialog.ShowDialog(this)==true) {File.WriteAllText(dialog.FileName,CurrentNote(selected)!,new System.Text.UTF8Encoding(false)); Hint.Text="已导出："+dialog.FileName;}
    }
    private void Cancel_Click(object sender,RoutedEventArgs e) {if(selected!=null&&processingTokens.TryGetValue(selected.Id,out var token))token.Cancel();}
    private void More_Click(object sender,RoutedEventArgs e)
    {
        if(selected==null || busy || recording!=null) return;
        SaveEditor(); var target=selected;
        var popup=new ActionPopup((FrameworkElement)sender);
        popup.Add("回放录音片段", "play", ()=> {var dialog=new PlaybackWindow(target) {Owner=this};dialog.ShowDialog();},target.Parts.Count>0);
        var versions=target.DirectRefinement?target.EnhancedVersions:target.Versions;
        popup.Add("恢复上一版笔记", "restore", ()=> {var last=versions[^1];versions.RemoveAt(versions.Count-1);versions.Insert(0,CurrentNote(target)??"");SetCurrentNote(target,last);store.Save(target);ShowCourse(target);},versions.Count>0);
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
        if(exiting||!LeaveSettings())return;
        if(recording!=null) {await EndWithoutSummary();}
        if(busy) {foreach(var token in processingTokens.Values)token.Cancel(); Hint.Text="正在取消处理并保存，请稍后从托盘退出。"; return;}
        SaveEditor(); exiting=true; trayMenu?.Dismiss(); ticker.Stop(); autosave.Stop(); toastTimer.Stop(); floating.Close(); recorder.Dispose(); asr.Dispose(); tray.Dispose(); System.Windows.Application.Current.Shutdown();
    }
    private async Task EndWithoutSummary()
    {
        var course=recording!; recorder.Pause(); await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Background); queue?.Writer.TryComplete(); recording=null; if(worker!=null) await worker; course.Seconds=recorder.TotalSeconds; course.State="等待整理"; store.Save(course);recordingCancellation?.Dispose();recordingCancellation=null;SetBusy(processingCourses.Count>0);
    }
}
