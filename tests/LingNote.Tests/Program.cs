using System.Net.Http;
using LingNote;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using NAudio.Wave;

if(args.Contains("--ui-check")) { SidebarUiChecks.Run(); return; }
if(args.Contains("--floating-preview")) { SidebarUiChecks.Preview(); return; }
if(args.Contains("--read-settings")) {var saved=Preferences.Load();Environment.Exit(saved.GetKey()=="test-placeholder-only" && saved.Model=="test" && saved.BaseUrl=="http://127.0.0.1:18766/v1"?0:1);return;}
var testRoot=Path.Combine(AppContext.BaseDirectory,"test-data",Guid.NewGuid().ToString("N"));
Environment.SetEnvironmentVariable("LINGNOTE_DATA",testRoot);
Paths.Init(); var results=new List<object>();
void Check(bool condition,string name) {if(!condition) throw new Exception("FAIL: "+name); Console.WriteLine("PASS: "+name); results.Add(new {name,passed=true});}
var activationName="Local\\LingNote.Activation.Test."+Guid.NewGuid().ToString("N");
using(var receiver=new ActivationSignal(activationName))using(var sender=new ActivationSignal(activationName))using(var notified=new ManualResetEventSlim()) {
    sender.Request();receiver.Listen(()=>notified.Set());Check(notified.Wait(3000),"重复启动通知在窗口尚未准备完成时仍保留");
    notified.Reset();sender.Request();Check(notified.Wait(3000),"已有实例可重复接收恢复窗口通知");
}
Exception? windowError=null;int closedCount=0;
var windowThread=new Thread(()=>{try {var window=new DismissibleWindow();window.Closing+=(_,_)=>window.Dismiss();window.Closed+=(_,_)=>closedCount++;window.Dismiss();window.Dismiss();}catch(Exception ex){windowError=ex;}});
windowThread.SetApartmentState(ApartmentState.STA);windowThread.Start();windowThread.Join();
Check(windowError==null && closedCount==1,"托盘窗口关闭回调重入及重复关闭不会抛异常");
var store=new Store(); var course=new Course {Title="数据库往返测试",Note="# 中文笔记\n\n完整内容"}; course.Parts.Add(new AudioPart {File="x.wav",Text="术语与数字 2026",Done=true,Duration=10}); store.Save(course);
Check(store.All().Single().Note==course.Note && store.All().Single().Transcript.Contains("2026"),"SQLite 保存并恢复 Unicode 笔记和转写");
var upgradeBackup=Directory.GetFiles(Path.Combine(Paths.Root,"backups"),"*.db").Single();var originalBackup=File.ReadAllBytes(upgradeBackup);
Check(new Store().All().Single().Id==course.Id && File.ReadAllBytes(upgradeBackup).SequenceEqual(originalBackup),"升级后读取原库且不会覆盖版本备份");
using(var snapshot=new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder {DataSource=upgradeBackup,Mode=Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,Pooling=false}.ToString())) {snapshot.Open();using var command=snapshot.CreateCommand();command.CommandText="PRAGMA integrity_check";Check((string?)command.ExecuteScalar()=="ok","升级自动备份为完整可读取的 SQLite 数据库");}
course.Versions.Add(course.Note); course.Note="修改后"; store.Save(course); Check(store.All().Single().Versions.Single().StartsWith("# 中文"),"笔记版本保留");
course.Topic="计算机网络";store.Save(course);Check(new Store().All().Single().TopicGroup=="计算机网络","课程主题持久化并在重启后恢复");
course.Topic="";store.Save(course);Check(store.All().Single().TopicGroup=="未分区","清空主题返回未分区");
Check(new Course {Created=DateTime.Today}.DayGroup=="今天" && new Course {Created=DateTime.Today.AddDays(-1)}.DayGroup=="昨天" && new Course {Created=new DateTime(2025,1,2)}.DayGroup=="2025年01月02日","日期分组跨天边界与旧课程兼容");
var deleted=new Course {Title="删除回归"}; Directory.CreateDirectory(deleted.Folder); File.WriteAllText(Path.Combine(deleted.Folder,"recording.wav"),"test");store.Save(deleted);store.Delete(deleted);
Check(!store.All().Any(c=>c.Id==deleted.Id) && !Directory.Exists(deleted.Folder),"删除课程同时清除记录和音频");
store.Save(deleted);new Store().Save(deleted);Check(!store.All().Any(c=>c.Id==deleted.Id),"延迟保存及重启后的旧回调不能恢复已删除课程");
var locked=new Course {Title="文件占用删除回归"};Directory.CreateDirectory(locked.Folder);store.Save(locked);using(var handle=new FileStream(Path.Combine(locked.Folder,"busy.wav"),FileMode.Create,FileAccess.Write,FileShare.None)){store.Delete(locked);Check(!store.All().Any(c=>c.Id==locked.Id),"音频占用不会阻止课程记录删除");}
var prefs=new Preferences {BaseUrl="http://127.0.0.1:18766/v1",Model="test",Stream=true}; prefs.Topics.Add("预先创建的主题"); prefs.SetKey("test-placeholder-only"); prefs.Save(); Check(Preferences.Load().GetKey()=="test-placeholder-only" && !File.ReadAllText(Path.Combine(Paths.Root,"settings.json")).Contains("test-placeholder-only"),"DPAPI 密钥加密与解密");
Check(Preferences.Load().Topics.Contains("预先创建的主题"),"无课程的自定义主题可持久化");
prefs.SaveApi();
using(var child=Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"--read-settings") {UseShellExecute=false})!) {child.WaitForExit();Check(child.ExitCode==0,"独立新进程重新读取 API 地址、模型与加密密钥");}
new Preferences {ActiveTopic="另一个主题"}.Save();Check(Preferences.Load().GetKey()=="test-placeholder-only" && Preferences.Load().Model=="test","主题或悬浮窗保存不会覆盖已有 API 配置");
prefs.SaveApi();File.WriteAllText(Path.Combine(Paths.Root,"settings.json"),"{invalid");Check(Preferences.Load().GetKey()=="test-placeholder-only","配置损坏时从备份恢复，避免静默清空");prefs.SaveApi();prefs.SaveApi();
var original=string.Join("\n",Enumerable.Range(0,1000).Select(i=>$"课程第{i}段：这里保留知识点与数值。")); var parts=NoteService.Split(original,2000);
Check(parts.All(p=>p.Length<=2001) && string.Concat(parts).Replace("\n","")==original.Replace("\n",""),"长课程分段不丢失尾部和中间内容");
Check(NoteService.Endpoint("https://example.com/v1/").AbsolutePath=="/v1/chat/completions" && NoteService.Endpoint("https://example.com/v1/chat/completions").AbsolutePath=="/v1/chat/completions","API 地址拼接不重复");
// Fail immediately if any request accidentally consults a proxy.
HttpClient.DefaultProxy=new RejectingProxy();
async Task<string> Mock(string body,bool stream=true,int status=200) {
    var reserve=new TcpListener(IPAddress.Loopback,0); reserve.Start(); var port=((IPEndPoint)reserve.LocalEndpoint).Port; reserve.Stop();
    using var listener=new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start(); prefs.BaseUrl=$"http://127.0.0.1:{port}/v1"; prefs.Stream=stream;
    var serving=Task.Run(async()=>{var context=await listener.GetContextAsync(); using var reader=new StreamReader(context.Request.InputStream); var input=await reader.ReadToEndAsync(); using var doc=JsonDocument.Parse(input); if(doc.RootElement.GetProperty("model").GetString()!="test") throw new Exception("Model missing"); context.Response.StatusCode=status; context.Response.ContentType=stream?"text/event-stream":"application/json"; var bytes=Encoding.UTF8.GetBytes(body); await context.Response.OutputStream.WriteAsync(bytes); context.Response.Close();});
    try {return await new NoteService().Request(prefs,"规则","课程文字",null,CancellationToken.None);} finally {await serving; listener.Stop();}
}
Check(await Mock("data: {\"choices\":[{\"delta\":{\"content\":\"# 笔记\"},\"finish_reason\":null}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"\\n完整内容\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n")=="# 笔记\n完整内容","兼容 API 流式内容组装");
Check(await Mock("{\"choices\":[{\"message\":{\"content\":\"完整笔记\"},\"finish_reason\":\"stop\"}]}",false)=="完整笔记","兼容 API 非流式响应");
try {await Mock("data: {\"choices\":[{\"delta\":{\"content\":\"不完整\"},\"finish_reason\":\"length\"}]}\n\n"); throw new Exception("未检测截断");} catch(IOException) {Check(true,"模型长度截断不会误标完成");}
try {await Mock("data: {\"choices\":[{\"delta\":{\"content\":\"断开\"}}]}\n\n"); throw new Exception("未检测断流");} catch(IOException) {Check(true,"流式中断可重试");}
try {await Mock("{}",false,401); throw new Exception("未检测认证失败");} catch(HttpRequestException) {Check(true,"API 认证错误提示");}
{
    var reserve=new TcpListener(IPAddress.Loopback,0); reserve.Start(); int port=((IPEndPoint)reserve.LocalEndpoint).Port; reserve.Stop();
    using var listener=new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start(); prefs.BaseUrl=$"http://127.0.0.1:{port}/v1"; prefs.Stream=false; prefs.ChunkCharacters=2000;
    int calls=0; bool sawTail=false;
    var server=Task.Run(async()=> {try {while(listener.IsListening) {var ctx=await listener.GetContextAsync(); var number=Interlocked.Increment(ref calls); using var input=new StreamReader(ctx.Request.InputStream); var body=await input.ReadToEndAsync(); using var parsed=JsonDocument.Parse(body); var material=parsed.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!; sawTail|=material.Contains("课程第999段"); if(number==2) ctx.Response.StatusCode=503; var output=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {choices=new[]{new{message=new{content=$"## 知识点 {number}\n\n完整段落 {number}"},finish_reason="stop"}}})); ctx.Response.ContentType="application/json"; await ctx.Response.OutputStream.WriteAsync(output); ctx.Response.Close();}} catch(HttpListenerException) {} catch(ObjectDisposedException) {} });
    var longCourse=new Course(); longCourse.Parts.Add(new AudioPart {Text=original,Done=true}); var service=new NoteService();
    try {await service.Generate(longCourse,prefs,store,new Progress<string>(),_=>{},CancellationToken.None);} catch(HttpRequestException) {}
    Check(longCourse.Drafts.Count==1,"长课程 API 失败时已完成分段持久化");
    var expected=NoteService.Split(longCourse.Transcript,2000).Count; var note=await service.Generate(longCourse,prefs,store,new Progress<string>(),_=>{},CancellationToken.None);
    Check(calls==expected+1 && longCourse.Drafts.Count==expected && sawTail && note.Contains($"完整段落 {calls}"),"失败重试复用分段结果并保留课程最后一段");
    listener.Stop(); await server;
}
if(args.Contains("--audio")) {
    var recorded=new List<AudioPart>(); using var recorder=new AudioRecorder(); var folder=Path.Combine(Paths.Root,"capture"); recorder.PartReady+=p=>recorded.Add(p);
    recorder.Begin(folder); await Task.Delay(1500); recorder.Pause(); var first=recorder.TotalSeconds; await Task.Delay(600); recorder.Resume(); await Task.Delay(1500); recorder.Pause();
    Check(recorded.Count==2 && recorder.TotalSeconds>2.5 && recorder.TotalSeconds<5,"WASAPI 真实设备录音、暂停、恢复和静音补齐");
    Check(Math.Abs(recorded[1].Offset-first)<.1 && recorded.All(p=>File.Exists(p.File)),"分段时间轴与音频文件落盘");
    foreach(var part in recorded) {using var wav=new WaveFileReader(part.File); Check(wav.Length>0 && wav.TotalTime.TotalSeconds>1,"WAV 头与静音音轨可读取");}
}
if(args.Contains("--asr")) {
    var assetRoot=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../assets"));
    var modelSource=Path.Combine(assetRoot,"ggml-small.bin"); Directory.CreateDirectory(Paths.Models); File.Copy(modelSource,Paths.Model,true);
    using var transcriber=new Transcriber(); var watch=Stopwatch.StartNew(); var text=await transcriber.Run(new AudioPart {File=Path.Combine(assetRoot,"jfk.wav")},CancellationToken.None); watch.Stop();
    Check(text.Contains("country",StringComparison.OrdinalIgnoreCase),"官方测试语音经本地 Whisper small 真实转写"); Console.WriteLine($"ASR: {watch.Elapsed.TotalSeconds:F1}s: {text}");
    var chinese=await transcriber.Run(new AudioPart {File=Path.Combine(assetRoot,"chinese-lesson.wav")},CancellationToken.None);
    Check(chinese.Contains("网络") && chinese.Contains("语法"),"中文课程本地识别保留关键术语"); Console.WriteLine("Chinese ASR: "+chinese);
}
var sectionsPrefs=Preferences.Load();
var sectionA=new LibrarySection {Name="考试复习"};var sectionB=new LibrarySection {Name="专题学习"};
sectionsPrefs.Sections.AddRange([sectionA,sectionB]);sectionsPrefs.Topics.Add("分区测试主题");sectionsPrefs.Save();
var sectionRecord=new Course {Title="分区独立录音",Note="保留的笔记"};sectionRecord.Parts.Add(new AudioPart {File="retained.wav",Text="保留转写",Done=true});store.Save(sectionRecord);
var topicRecord=new Course {Title="主题录音",Topic="分区测试主题",Note="主题笔记"};store.Save(topicRecord);
LibrarySections.MoveRecord(store,sectionRecord,sectionA.Id);
LibrarySections.MoveTopic(sectionsPrefs,"分区测试主题",sectionA.Id);
var freshPrefs=Preferences.Load();var freshRecords=new Store().All();
Check(freshPrefs.Sections.Any(s=>s.Name=="考试复习")&&LibrarySections.SectionOf(freshPrefs,freshRecords.Single(c=>c.Id==sectionRecord.Id))==sectionA.Id&&LibrarySections.SectionOf(freshPrefs,freshRecords.Single(c=>c.Id==topicRecord.Id))==sectionA.Id,"独立录音和整个主题移至同级分区并在重启后恢复");
sectionA.Name="复习资料";sectionsPrefs.Save();
Check(Preferences.Load().Sections.Single(s=>s.Id==sectionA.Id).Name=="复习资料"&&LibrarySections.SectionOf(sectionsPrefs,topicRecord)==sectionA.Id,"重命名分区不改变主题与录音归属");
LibrarySections.MoveRecord(store,topicRecord,sectionB.Id);
Check(new Store().All().Single(c=>c.Id==topicRecord.Id).Topic==""&&LibrarySections.TopicSection(sectionsPrefs,"分区测试主题")==sectionA.Id,"单独移动主题内录音不移动整个主题");
sectionRecord.Archived=true;sectionRecord.IsRead=true;store.Save(sectionRecord);
Check(new Store().All().Single(c=>c.Id==sectionRecord.Id).Archived&&new Store().All().Single(c=>c.Id==sectionRecord.Id).Note=="保留的笔记","归档状态持久化且保留笔记转写");
sectionRecord.Archived=false;store.Save(sectionRecord);
var priorCount=store.All().Count;LibrarySections.Remove(store,sectionsPrefs,sectionA.Id);
var retained=store.All().Single(c=>c.Id==sectionRecord.Id);
Check(store.All().Count==priorCount&&retained.SectionId==""&&retained.Note=="保留的笔记"&&retained.Parts.Single().Text=="保留转写"&&LibrarySections.TopicSection(Preferences.Load(),"分区测试主题")=="","移除分区将内容移回默认区域且不删除音频记录或笔记");
Check(JsonSerializer.Deserialize<Course>("{\"Title\":\"旧记录\"}") is {SectionId:"",Archived:false},"旧版本记录兼容新增分区字段");
var deleteSection=new LibrarySection {Name="删除范围测试"};sectionsPrefs.Sections.Add(deleteSection);sectionsPrefs.Save();
LibrarySections.MoveTopic(sectionsPrefs,"分区测试主题",deleteSection.Id);
var directDelete=new Course {Title="分区独立录音",SectionId=deleteSection.Id};
var nestedDelete=new Course {Title="分区主题录音",Topic="分区测试主题"};
foreach(var testCourse in new[]{directDelete,nestedDelete}) {Directory.CreateDirectory(testCourse.Folder);File.WriteAllText(Path.Combine(testCourse.Folder,"part.wav"),"test");store.Save(testCourse);}
var keepCount=store.All().Count-2;
LibrarySections.DeleteRecordings(store,sectionsPrefs,deleteSection.Id);
Check(store.All().Count==keepCount&&store.All().Any(c=>c.Id==sectionRecord.Id)&&!Directory.Exists(directDelete.Folder)&&!Directory.Exists(nestedDelete.Folder)&&Preferences.Load().Sections.Any(s=>s.Id==deleteSection.Id)&&Preferences.Load().Topics.Contains("分区测试主题"),"删除分区录音覆盖独立录音和主题内录音，保留分区主题及其他分区内容");
new Store().Save(directDelete);new Store().Save(nestedDelete);
Check(!new Store().All().Any(c=>c.Id==directDelete.Id||c.Id==nestedDelete.Id),"删除分区录音后旧回调不能复活记录");
var modelBytes=new byte[1024];BitConverter.GetBytes(0x67676d6c).CopyTo(modelBytes,0);
var modelHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(modelBytes));
var modelTarget=Path.Combine(testRoot,"downloaded-model.bin");var modelProgress=new RecordingProgress();
using(var client=new HttpClient(new ModelResponseHandler(modelBytes)))await ModelDownloader.Download(client,new Uri("https://model.test/model"),modelTarget,modelHash,1024,modelProgress,CancellationToken.None);
Check(File.ReadAllBytes(modelTarget).SequenceEqual(modelBytes)&&!File.Exists(modelTarget+".download")&&modelProgress.Messages.Any(m=>m.Contains("100%")),"模型流式下载完成并校验后才就绪，进度可显示");
File.WriteAllText(modelTarget,"existing-model");
using(var client=new HttpClient(new ModelResponseHandler(modelBytes))) {try {await ModelDownloader.Download(client,new Uri("https://model.test/model"),modelTarget,new string('0',64),1024,modelProgress,CancellationToken.None);throw new Exception("Bad hash accepted");}catch(IOException){}}
Check(File.ReadAllText(modelTarget)=="existing-model"&&!File.Exists(modelTarget+".download"),"模型校验失败删除临时文件且保留已有模型");
using(var client=new HttpClient(new ModelResponseHandler(modelBytes,1))) {try {await ModelDownloader.Download(client,new Uri("https://model.test/model"),modelTarget,modelHash,1024,modelProgress,CancellationToken.None);throw new Exception("Partial download accepted");}catch(IOException){}}
Check(File.ReadAllText(modelTarget)=="existing-model"&&!File.Exists(modelTarget+".download"),"不完整模型不会替换已有模型");
using(var client=new HttpClient(new ModelResponseHandler(modelBytes)))using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try {await ModelDownloader.Download(client,new Uri("https://model.test/model"),modelTarget,modelHash,1024,modelProgress,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
Check(File.ReadAllText(modelTarget)=="existing-model"&&!File.Exists(modelTarget+".download"),"取消下载保留已有模型并清理临时文件");
await UpdateChecks.Run(testRoot,Check);
File.WriteAllText(Path.Combine(testRoot,"results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions {WriteIndented=true})); Console.WriteLine("Results: "+testRoot);


sealed class RejectingProxy : IWebProxy
{
    public System.Net.ICredentials? Credentials {get;set;}
    public bool IsBypassed(Uri host)=>false;
    public Uri GetProxy(Uri destination)=>throw new InvalidOperationException("请求不应访问系统代理");
}
sealed class RecordingProgress : IProgress<string>
{
    public List<string> Messages {get;}=[];
    public void Report(string value)=>Messages.Add(value);
}
sealed class ModelResponseHandler(byte[] bytes,int extraLength=0) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response=new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)};
        response.Content.Headers.ContentLength=bytes.Length+extraLength;
        return Task.FromResult(response);
    }
}
