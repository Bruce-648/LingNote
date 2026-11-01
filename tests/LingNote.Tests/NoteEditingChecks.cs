using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LingNote;

internal static class NoteEditingChecks
{
    public static async Task Run(Action<bool,string> check, Store store)
    {
        var grouped=NoteService.TopicSections("## 一、定义\n首次说明。\n\n```markdown\n## 代码里的标题\n\n保持代码内容\n```\n\n## 例子\n独有例子。\n\n## 定义\n补充条件。");
        check(grouped.Count==2&&grouped["定义"].Contains("首次说明")&&grouped["定义"].Contains("补充条件")&&grouped["定义"].Contains("## 代码里的标题")&&grouped["例子"].Contains("独有例子"),"按主题归并不丢互补条件，代码块中的标题不被误拆");
        var reserve=new TcpListener(IPAddress.Loopback,0);reserve.Start();var port=((IPEndPoint)reserve.LocalEndpoint).Port;reserve.Stop();
        using var listener=new HttpListener();listener.Prefixes.Add($"http://127.0.0.1:{port}/");listener.Start();
        var prefs=new Preferences {BaseUrl=$"http://127.0.0.1:{port}/v1",Model="test",Stream=false,ChunkCharacters=20000};prefs.SetKey("local-edit-test");
        var requests=new List<string>();
        var server=Task.Run(async()=>
        {
            for(int i=0;i<2;i++)
            {
                var context=await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(10));
                using var reader=new StreamReader(context.Request.InputStream);
                using var input=JsonDocument.Parse(await reader.ReadToEndAsync());
                requests.Add(input.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
                
                var text=i==0?"## 定义与条件\n课堂知识正文。":"## 定义与公式\n完善知识正文。";
                var bytes=JsonSerializer.SerializeToUtf8Bytes(new {choices=new[]{new {message=new {content=text},finish_reason="stop"}}});
                context.Response.ContentType="application/json";context.Response.ContentLength64=bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);context.Response.Close();
            }
        });
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var service=new NoteService();var classroom=new Course {Note="已有课堂笔记"};classroom.Parts.Add(new AudioPart {Done=true,Text="速度的定义及唯一条件，不能丢失。"});
        var note=await service.Generate(classroom,prefs,store,new Progress<string>(),_=>{},timeout.Token);
        check(requests.Count==1&&requests[0].Contains(classroom.Transcript)&&note.Contains("课堂知识正文")&&!note.Contains("录制于")&&classroom.Note=="已有课堂笔记","单段课堂笔记只请求一次并直接返回成稿，服务不提前覆盖旧笔记");
        var edited="## 用户当前编辑的定义\n已有公式、单位和限制条件。";
        var course=new Course {Note=edited,EnhancedNote="不应作为输入的旧完善笔记"};
        var refined=await service.Enhance(course,prefs,null,new Progress<string>(),timeout.Token);
        check(requests.Count==2&&requests[1].Contains(edited)&&!requests[1].Contains(course.EnhancedNote)&&requests[1].Contains("0–2 个，可以自拟")&&refined.Contains("完善知识正文")&&course.Note==edited,"单段完善只请求一次，使用当前课堂笔记并明确整篇例子上限及自拟权限");
        await server;
        var diagnostics=Path.Combine(Paths.Root,"diagnostics");
        var logs=string.Join("\n",Directory.GetFiles(diagnostics,"performance-*.jsonl").Select(File.ReadAllText));
        check(logs.Contains("ai_request")&&logs.Contains("firstTokenMilliseconds")&&!logs.Contains(edited)&&!logs.Contains("local-edit-test"),"性能日志记录响应时间，不包含笔记正文或密钥");
        bool emptyClassroom=false,emptyEnhanced=false;
        try {await service.Generate(new Course(),prefs,store,new Progress<string>(),_=>{},timeout.Token);} catch(InvalidOperationException) {emptyClassroom=true;}
        try {await service.Enhance(new Course {Note=" \n "},prefs,null,new Progress<string>(),timeout.Token);} catch(InvalidOperationException) {emptyEnhanced=true;}
        check(emptyClassroom&&emptyEnhanced&&requests.Count==2,"空内容在发起 AI 请求前停止");
        await ClassroomParallel(check,store);
        await TitleChange(check);
        await Topics(check);
    }

    private static async Task ClassroomParallel(Action<bool,string> check, Store store)
    {
        var reserve=new TcpListener(IPAddress.Loopback,0);reserve.Start();var port=((IPEndPoint)reserve.LocalEndpoint).Port;reserve.Stop();
        using var listener=new HttpListener();listener.Prefixes.Add($"http://127.0.0.1:{port}/");listener.Start();
        var prefs=new Preferences {BaseUrl=$"http://127.0.0.1:{port}/v1",Model="test",Stream=false,ChunkCharacters=2000};prefs.SetKey("local-parallel-test");
        async Task Reply(HttpListenerContext c,string result){var bytes=JsonSerializer.SerializeToUtf8Bytes(new {choices=new[]{new {message=new {content=result},finish_reason="stop"}}});c.Response.ContentType="application/json";await c.Response.OutputStream.WriteAsync(bytes);c.Response.Close();}
        async Task<string> Read(HttpListenerContext c){using var reader=new StreamReader(c.Request.InputStream);using var doc=JsonDocument.Parse(await reader.ReadToEndAsync());return doc.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;}
        var server=Task.Run(async()=>{
            var first=await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(8));var second=await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(8));
            var a=await Read(first);var b=await Read(second);
            await Reply(second,b.Contains("第 1 /")?"首段知识":"尾段知识");await Reply(first,a.Contains("第 1 /")?"首段知识":"尾段知识");
            var merge=await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(8));var body=await Read(merge);await Reply(merge,body);
        });
        var course=new Course();course.Parts.Add(new AudioPart {Done=true,Text=new string('甲',1900)+"\n"+new string('乙',700)});
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(12));
        await new NoteService().Generate(course,prefs,store,new Progress<string>(),_=>{},timeout.Token);await server;
        check(course.Drafts.Count==2&&course.Drafts[0]=="首段知识"&&course.Drafts[1]=="尾段知识","课堂笔记两段请求同时到达，逆序完成后仍按原顺序保存");
    }

    private static async Task TitleChange(Action<bool,string> check)
    {
        var reserve=new TcpListener(IPAddress.Loopback,0);reserve.Start();var port=((IPEndPoint)reserve.LocalEndpoint).Port;reserve.Stop();
        using var listener=new HttpListener();listener.Prefixes.Add($"http://127.0.0.1:{port}/");listener.Start();
        var prefs=new Preferences {BaseUrl=$"http://127.0.0.1:{port}/v1",Model="test",Stream=false,ChunkCharacters=20000};prefs.SetKey("title-cache-test");
        var requests=new List<string>();var server=Task.Run(async()=>{for(int i=0;i<2;i++){
            var context=await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(8));using var reader=new StreamReader(context.Request.InputStream);using var doc=JsonDocument.Parse(await reader.ReadToEndAsync());requests.Add(doc.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
            var bytes=JsonSerializer.SerializeToUtf8Bytes(new {choices=new[]{new {message=new {content="## 知识\n主题内容"},finish_reason="stop"}}});context.Response.ContentType="application/json";await context.Response.OutputStream.WriteAsync(bytes);context.Response.Close();
        }});
        var course=new Course {Title="旧主题",Note="## 知识\n必要条件"};var service=new NoteService();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await service.Enhance(course,prefs,null,new Progress<string>(),timeout.Token);course.Title="新主题";
        await service.Enhance(course,prefs,null,new Progress<string>(),timeout.Token);await server;
        check(requests.Count==2&&requests[0].Contains("旧主题")&&requests[1].Contains("新主题")&&course.EnhancedDrafts.Count==1,"更改主题标题会丢弃旧标题的缓存草稿并重新生成");
    }

    private static async Task Topics(Action<bool,string> check)
    {
        var reserve=new TcpListener(IPAddress.Loopback,0);reserve.Start();var port=((IPEndPoint)reserve.LocalEndpoint).Port;reserve.Stop();
        using var listener=new HttpListener();listener.Prefixes.Add($"http://127.0.0.1:{port}/");listener.Start();
        var prefs=new Preferences {BaseUrl=$"http://127.0.0.1:{port}/v1",Model="test",Stream=false,ChunkCharacters=20000};prefs.SetKey("local-topic-test");
        prefs.ChunkCharacters=2000;
        var requests=new System.Collections.Concurrent.ConcurrentBag<string>();
        int active=0,maximum=0;var gate=new object();
        var draft=string.Join("\n\n",new[]{"甲","乙","丙"}.Select(t=>$"## {t}\n"+new string(t[0],6000)));
        async Task<string> Read(HttpListenerContext context){using var reader=new StreamReader(context.Request.InputStream);using var doc=JsonDocument.Parse(await reader.ReadToEndAsync());return doc.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;}
        async Task Reply(HttpListenerContext context,string text){var bytes=JsonSerializer.SerializeToUtf8Bytes(new {choices=new[]{new {message=new {content=text},finish_reason="stop"}}});context.Response.ContentType="application/json";await context.Response.OutputStream.WriteAsync(bytes);context.Response.Close();}
        var server=Task.Run(async()=>
        {
            for(int i=0;i<2;i++){var initial=await listener.GetContextAsync();await Read(initial);await Reply(initial,draft);}
            var replies=new List<Task>();
            for(int i=0;i<3;i++)
            {
                var context=await listener.GetContextAsync();
                replies.Add(Task.Run(async()=>
                {
                    var request=await Read(context);requests.Add(request);
                    var topic=request.Contains(new string('甲',20))?"甲":request.Contains(new string('乙',20))?"乙":"丙";
                    lock(gate){active++;maximum=Math.Max(maximum,active);}
                    await Task.Delay(topic=="甲"?200:50);
                    lock(gate)active--;
                    await Reply(context,$"## {topic}\n合并后的{topic}知识。");
                }));
            }
            await Task.WhenAll(replies);
        });
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var course=new Course {Note="## 当前笔记\n"+new string('甲',1900)+"\n"+new string('乙',700)};
        var result=await new NoteService().Enhance(course,prefs,null,new Progress<string>(),timeout.Token);await server;
        check(maximum==2&&result.IndexOf("合并后的甲",StringComparison.Ordinal)<result.IndexOf("合并后的乙",StringComparison.Ordinal)&&result.IndexOf("合并后的乙",StringComparison.Ordinal)<result.IndexOf("合并后的丙",StringComparison.Ordinal),"超长稿按主题最多两批并发合并，乱序完成后仍按知识顺序输出");
        check(requests.Count==3&&requests.All(r=>new[]{'甲','乙','丙'}.Count(t=>r.Contains(new string(t,20)))==1)&&requests.Count(r=>r.Contains("本批例子上限为 1 个"))==2&&requests.Count(r=>r.Contains("本批例子上限为 0 个"))==1,"超长稿的所有主题均参与合并，总计例子额度不超过两个");
    }
}
