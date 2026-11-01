using System.Net.Http;
using LingNote;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using NAudio.Wave;

if(args.Contains("--bulk-check")){BulkDeletionChecks.Run();return;}
if(args.Contains("--settings-review")){SettingsCaptureChecks.Run(args.Contains("--keep-open"));return;}
if(args.Contains("--topic-gap-review")){await QualityReview.TopicGap();return;}
if(args.Contains("--readability-review")){await QualityReview.Readability();return;}
if(args.Contains("--quality-review")){await QualityReview.Run(args.Contains("--short-only"));return;}
if(args.Contains("--design-review")){SidebarUiChecks.DesignReview(args.Contains("--keep-open"));return;}
if(args.Contains("--ui-check")) { SidebarUiChecks.Run(); return; }
if(args.Contains("--floating-preview")) { SidebarUiChecks.Preview(); return; }
if(args.Contains("--check-update-live")) {using var updateService=new UpdateService();var latest=await updateService.Latest(CancellationToken.None);Console.WriteLine(latest==null?"No published release":$"Latest release: {latest.Version}; verified asset: {latest.FileName}");return;}
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
Check(NotePrompts.EnhancementRules.Contains("课堂笔记没有提到也可以补充")&&NotePrompts.EnhancementRules.Contains("标题只是录音日期")&&NotePrompts.EnhancementRules.Contains("不要拓展到邻近主题"),"精编以主题组织并允许补齐未提到的核心知识，标题模糊时依内容识别");
Check(NotePrompts.Segment(true,"测试主题",1,2,"甲","甲；乙").Contains("明显缺少的核心知识点")&&!NotePrompts.Segment(true,"测试主题",2,2,"乙","甲；乙").Contains("这是第一个分段"),"长笔记由首段识别遗漏核心知识，后续段避免重复其他主题");
Check(NotePrompts.Merge(true,"索引","正文",2,true).Contains("补齐主题内明显缺少的核心知识点")&&NotePrompts.Merge(true,"索引","正文",1,false).Contains("不根据索引猜写其他批次正文"),"完整合并可补齐主题，超长分批只处理自身范围");
Check(!JsonSerializer.Deserialize<Course>("{}").DirectRefinement&&new Preferences().GenerationMode=="classroom","旧记录和旧设置默认保持课堂笔记模式");
var directPrompt=NotePrompts.Segment(true,"网络性能",1,1,"[00:00:00] 信道利用率",null,true);
Check(directPrompt.Contains("<录音转写>")&&directPrompt.Contains("信道利用率")&&NotePrompts.DirectTranscriptRules.Contains("不是需要逐句复述")&&NotePrompts.DirectTranscriptRules.Contains("核心知识点"),"直接精编以转写识别知识范围，不要求逐字复述");
var original=string.Join("\n",Enumerable.Range(0,1000).Select(i=>$"课程第{i}段：这里保留知识点与数值。")); var parts=NoteService.Split(original,2000);
Check(parts.All(p=>p.Length<=2001) && string.Concat(parts).Replace("\n","")==original.Replace("\n",""),"长课程分段不丢失尾部和中间内容");
Check(NoteService.Endpoint("https://example.com/v1/").AbsolutePath=="/v1/chat/completions" && NoteService.Endpoint("https://example.com/v1/chat/completions").AbsolutePath=="/v1/chat/completions","API 地址拼接不重复");
var thinkingPrefs=new Preferences {BaseUrl="https://api.deepseek.com/v1",Model="deepseek-flash",ThinkingMode="none"};
using(var payload=JsonDocument.Parse(NoteService.BuildPayload(thinkingPrefs,"规则","内容")))Check(payload.RootElement.GetProperty("reasoning_effort").GetString()=="none","DeepSeek 关闭思考时发送对应参数");
thinkingPrefs.ThinkingMode="low";using(var payload=JsonDocument.Parse(NoteService.BuildPayload(thinkingPrefs,"规则","内容")))Check(payload.RootElement.GetProperty("reasoning_effort").GetString()=="low","DeepSeek 轻度思考使用低强度参数");
thinkingPrefs.ThinkingMode="max";using(var payload=JsonDocument.Parse(NoteService.BuildPayload(thinkingPrefs,"规则","内容")))Check(payload.RootElement.GetProperty("reasoning_effort").GetString()=="max","DeepSeek Max 使用最大思考强度");
thinkingPrefs.ThinkingMode="default";using(var payload=JsonDocument.Parse(NoteService.BuildPayload(thinkingPrefs,"规则","内容")))Check(!payload.RootElement.TryGetProperty("reasoning_effort",out _),"默认模式不改变服务端原有行为");
thinkingPrefs.ThinkingMode="high";thinkingPrefs.BaseUrl="https://example.com/v1";using(var payload=JsonDocument.Parse(NoteService.BuildPayload(thinkingPrefs,"规则","内容")))Check(!payload.RootElement.TryGetProperty("reasoning_effort",out _),"其他服务不收到 DeepSeek 专用参数");
thinkingPrefs.BaseUrl="https://api.openai.com/v1";thinkingPrefs.Model="gpt-test";thinkingPrefs.ThinkingMode="low";using(var payload=JsonDocument.Parse(NoteService.BuildPayload(thinkingPrefs,"规则","内容")))Check(payload.RootElement.GetProperty("reasoning_effort").GetString()=="low","OpenAI 兼容接口识别并使用思考强度参数");
thinkingPrefs.BaseUrl="https://generativelanguage.googleapis.com/v1beta/openai/";thinkingPrefs.Model="gemini-test";thinkingPrefs.ThinkingMode="low";using(var payload=JsonDocument.Parse(NoteService.BuildPayload(thinkingPrefs,"规则","内容")))Check(payload.RootElement.GetProperty("reasoning_effort").GetString()=="low","Gemini OpenAI 兼容接口识别并使用思考强度参数");
thinkingPrefs.BaseUrl="https://api.openai.com/v1";thinkingPrefs.ThinkingMode="xhigh";using(var payload=JsonDocument.Parse(NoteService.BuildPayload(thinkingPrefs,"规则","内容")))Check(payload.RootElement.GetProperty("reasoning_effort").GetString()=="xhigh","OpenAI 支持按所选值发送 XHigh 思考强度");
thinkingPrefs.BaseUrl="https://dashscope.aliyuncs.com/compatible-mode/v1";thinkingPrefs.Model="qwen-test";thinkingPrefs.ThinkingMode="none";using(var payload=JsonDocument.Parse(NoteService.BuildPayload(thinkingPrefs,"规则","内容")))Check(!payload.RootElement.GetProperty("enable_thinking").GetBoolean()&&!payload.RootElement.TryGetProperty("reasoning_effort",out _),"通义兼容接口使用思考开关关闭模式");
thinkingPrefs.ThinkingMode="high";using(var payload=JsonDocument.Parse(NoteService.BuildPayload(thinkingPrefs,"规则","内容")))Check(payload.RootElement.GetProperty("enable_thinking").GetBoolean(),"通义兼容接口可开启思考");
thinkingPrefs.BaseUrl="https://example.com/v1";thinkingPrefs.ThinkingProtocol="effort";thinkingPrefs.ThinkingMode="max";using(var payload=JsonDocument.Parse(NoteService.BuildPayload(thinkingPrefs,"规则","内容")))Check(payload.RootElement.GetProperty("reasoning_effort").GetString()=="max","第三方兼容接口可手动选思考强度协议");
thinkingPrefs.ThinkingProtocol="toggle";using(var payload=JsonDocument.Parse(NoteService.BuildPayload(thinkingPrefs,"规则","内容")))Check(payload.RootElement.GetProperty("enable_thinking").GetBoolean()&&!payload.RootElement.TryGetProperty("reasoning_effort",out _),"第三方兼容接口可手动选思考开关协议");
thinkingPrefs.BaseUrl="https://api.deepseek.com/v1";thinkingPrefs.ThinkingProtocol="disabled";using(var payload=JsonDocument.Parse(NoteService.BuildPayload(thinkingPrefs,"规则","内容")))Check(!payload.RootElement.TryGetProperty("reasoning_effort",out _)&&!payload.RootElement.TryGetProperty("enable_thinking",out _),"不发送选项不会向任何服务附加思考参数");
prefs.ThinkingMode="low";prefs.ThinkingProtocol="toggle";prefs.GenerationMode="direct";prefs.SaveApi();new Preferences().Save();Check(Preferences.Load().ThinkingMode=="low"&&Preferences.Load().ThinkingProtocol=="toggle"&&Preferences.Load().GenerationMode=="direct","常规设置保存保留思考和笔记生成模式");prefs.ThinkingMode="default";prefs.ThinkingProtocol="auto";prefs.GenerationMode="classroom";prefs.SaveApi();
// Fail immediately if any request accidentally consults a proxy.
HttpClient.DefaultProxy=new RejectingProxy();
async Task<string> Mock(string body,bool stream=true,int status=200) {
    var reserve=new TcpListener(IPAddress.Loopback,0); reserve.Start(); var port=((IPEndPoint)reserve.LocalEndpoint).Port; reserve.Stop();
    using var listener=new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start(); prefs.BaseUrl=$"http://127.0.0.1:{port}/v1"; prefs.Stream=stream;
    var serving=Task.Run(async()=>{var context=await listener.GetContextAsync(); using var reader=new StreamReader(context.Request.InputStream); var input=await reader.ReadToEndAsync(); using var doc=JsonDocument.Parse(input); if(doc.RootElement.GetProperty("model").GetString()!="test") throw new Exception("Model missing"); if(prefs.ThinkingProtocol=="toggle"&&!doc.RootElement.GetProperty("enable_thinking").GetBoolean())throw new Exception("Thinking toggle missing"); context.Response.StatusCode=status; context.Response.ContentType=stream?"text/event-stream":"application/json"; var bytes=Encoding.UTF8.GetBytes(body); await context.Response.OutputStream.WriteAsync(bytes); context.Response.Close();});
    try {return await new NoteService().Request(prefs,"规则","课程文字",null,CancellationToken.None);} finally {await serving; listener.Stop();}
}
Check(await Mock("data: {\"choices\":[{\"delta\":{\"content\":\"# 笔记\"},\"finish_reason\":null}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"\\n完整内容\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n")=="# 笔记\n完整内容","兼容 API 流式内容组装");
Check(await Mock("{\"choices\":[{\"message\":{\"content\":\"完整笔记\"},\"finish_reason\":\"stop\"}]}",false)=="完整笔记","兼容 API 非流式响应");
prefs.ThinkingProtocol="toggle";prefs.ThinkingMode="high";
Check(await Mock("data: {\"choices\":[{\"delta\":{\"reasoning_content\":\"隐藏思考\"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"知识正文\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n")=="知识正文","思考开关参数随请求发送，流式思考内容不混入笔记正文");
prefs.ThinkingProtocol="auto";prefs.ThinkingMode="default";
try {await Mock("data: {\"choices\":[{\"delta\":{\"content\":\"不完整\"},\"finish_reason\":\"length\"}]}\n\n"); throw new Exception("未检测截断");} catch(IOException) {Check(true,"模型长度截断不会误标完成");}
try {await Mock("data: {\"choices\":[{\"delta\":{\"content\":\"断开\"}}]}\n\n"); throw new Exception("未检测断流");} catch(IOException) {Check(true,"流式中断可重试");}
try {await Mock("{}",false,401); throw new Exception("未检测认证失败");} catch(HttpRequestException) {Check(true,"API 认证错误提示");}
{
    var reserve=new TcpListener(IPAddress.Loopback,0); reserve.Start(); int port=((IPEndPoint)reserve.LocalEndpoint).Port; reserve.Stop();
    using var listener=new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start(); prefs.BaseUrl=$"http://127.0.0.1:{port}/v1"; prefs.Stream=false; prefs.ChunkCharacters=2000;
    int calls=0; bool sawTail=false,failedSecond=false;string reconciliation="";var requests=new List<(string System,string User)>();
    var server=Task.Run(async()=> {try {while(listener.IsListening) {var ctx=await listener.GetContextAsync(); var number=Interlocked.Increment(ref calls); using var input=new StreamReader(ctx.Request.InputStream); var body=await input.ReadToEndAsync(); using var parsed=JsonDocument.Parse(body); var material=parsed.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!; requests.Add((parsed.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!,material)); sawTail|=material.Contains("课程第999段");if(material.Contains("<课堂笔记分段稿>"))reconciliation=material; if(!failedSecond&&material.Contains("材料范围：第 2 /")){failedSecond=true;ctx.Response.StatusCode=503;} var output=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {choices=new[]{new{message=new{content=$"## 知识点 {number}\n\n完整段落 {number}"},finish_reason="stop"}}})); ctx.Response.ContentType="application/json"; await ctx.Response.OutputStream.WriteAsync(output); ctx.Response.Close();}} catch(HttpListenerException) {} catch(ObjectDisposedException) {} });
    var longCourse=new Course {DraftFingerprint="old-prompt"};longCourse.Drafts.Add("旧提示词草稿"); longCourse.Parts.Add(new AudioPart {Text=original,Done=true}); var service=new NoteService();
    try {await service.Generate(longCourse,prefs,store,new Progress<string>(),_=>{},CancellationToken.None);} catch(HttpRequestException) {}
    Check(longCourse.Drafts.Count==1,"长课程 API 失败时已完成分段持久化");
    var expected=NoteService.Split(longCourse.Transcript,2000).Count; var note=await service.Generate(longCourse,prefs,store,new Progress<string>(),_=>{},CancellationToken.None);
    Check(calls==expected+2 && longCourse.Drafts.Count==expected && sawTail && note.Contains($"完整段落 {calls}"),"失败重试复用分段结果并保留课程最后一段");
    Check(longCourse.Drafts.All(d=>reconciliation.Contains(d)),"最终合并接收全部分段草稿，包含首段和尾段");
    Check(requests.All(r=>r.System.Contains("【当前任务：课堂笔记】")&&r.System.Contains("【材料边界】")&&!r.System.Contains("【当前任务：完善笔记】"))&&requests.Any(r=>r.User.Contains("任务：整理课堂笔记")&&r.User.Contains("材料范围：第 1 / ")),"课堂请求使用新版共同规则、专用规则和分段模板");
    Check(reconciliation.Contains("任务：合并课堂笔记分段稿")&&reconciliation.Contains("不声称已经回查录音")&&!note.Contains("旧提示词草稿"),"课堂合并使用专用模板且提示词升级清除旧草稿缓存");
    listener.Stop(); await server;
}
{
    var reserve=new TcpListener(IPAddress.Loopback,0);reserve.Start();var port=((IPEndPoint)reserve.LocalEndpoint).Port;reserve.Stop();
    using var listener=new HttpListener();listener.Prefixes.Add($"http://127.0.0.1:{port}/");listener.Start();prefs.BaseUrl=$"http://127.0.0.1:{port}/v1";prefs.Stream=false;prefs.ChunkCharacters=2000;
    var enhancedRequests=new System.Collections.Concurrent.ConcurrentBag<(string System,string User)>();
    async Task Reply(HttpListenerContext context,string text){using(var reader=new StreamReader(context.Request.InputStream)){using var doc=JsonDocument.Parse(await reader.ReadToEndAsync());var messages=doc.RootElement.GetProperty("messages");enhancedRequests.Add((messages[0].GetProperty("content").GetString()!,messages[1].GetProperty("content").GetString()!));}var bytes=JsonSerializer.SerializeToUtf8Bytes(new {choices=new[]{new {message=new {content=text},finish_reason="stop"}}});context.Response.ContentType="application/json";context.Response.ContentLength64=bytes.Length;await context.Response.OutputStream.WriteAsync(bytes);context.Response.Close();}
    bool overlapping=false;
    var server=Task.Run(async()=>{var first=await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(8));var second=await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(8));overlapping=true;await Task.WhenAll(Reply(first,"## 主题甲\n\n首段内容"),Reply(second,"## 主题乙\n\n末段内容"));await Reply(await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(8)),"## 校订结果\n\n首段内容与末段内容");});
    var courseForSpeed=new Course {Title="主题 {{总段数}}",Note="## 原有主题\n"+new string('甲',1900)+"\n"+new string('乙',700),EnhancedFingerprint="old-prompt"};courseForSpeed.EnhancedDrafts.Add("旧完善草稿");var snapshots=new List<string>();using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var enhanced=await new NoteService().Enhance(courseForSpeed,prefs,null,new Progress<string>(),limit.Token,s=>{lock(snapshots)snapshots.Add(s);});await server;
    Check(overlapping&&courseForSpeed.EnhancedDrafts.Count==2,"同一篇长笔记的两段请求并行发送并按原顺序保存");
    Check(enhanced.Contains("首段内容")&&enhanced.Contains("末段内容")&&snapshots.Count>0,"并行草稿显示后继续合并，首尾信息保留");
    Check(enhancedRequests.All(r=>r.System.Contains("【当前任务：完善笔记】")&&r.System.Contains("【材料边界】")&&!r.System.Contains("【当前任务：课堂笔记】"))&&enhancedRequests.Count(r=>r.User.Contains("任务：完善笔记")&&r.User.Replace("\r", "").Contains("<全文主题索引>\n原有主题")&&r.User.Contains("主题：主题 {{总段数}}"))==2,"完善请求使用新版专用规则、真实全文索引，材料不被当作模板再次替换");
    Check(enhancedRequests.Any(r=>r.User.Contains("任务：合并完善笔记分段稿")&&r.User.Contains("<完善笔记分段稿>"))&&!enhanced.Contains("旧完善草稿"),"完善合并使用独立模板，升级后不复用旧草稿");
}
await NoteEditingChecks.Run(Check,store);
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
