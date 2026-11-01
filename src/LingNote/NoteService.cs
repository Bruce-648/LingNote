using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.IO;

namespace LingNote;
public sealed class NoteService
{
    private static readonly HttpClient Client=new(new HttpClientHandler {UseProxy=false}) {Timeout=TimeSpan.FromMinutes(10)};
    public static Uri Endpoint(string address)
    {
        if(!Uri.TryCreate(address.Trim().TrimEnd('/'),UriKind.Absolute,out var uri) || (uri.Scheme!="https" && !(uri.Scheme=="http" && uri.IsLoopback))) throw new ArgumentException("服务地址应是 HTTPS 地址；本机服务可使用 HTTP。");
        return new Uri(uri.AbsoluteUri.TrimEnd('/')+(uri.AbsolutePath.EndsWith("/chat/completions")?"":"/chat/completions"));
    }
    public static List<string> Split(string text,int limit)
    {
        var chunks=new List<string>(); var current=new StringBuilder();
        foreach(var paragraph in text.Replace("\r","").Split('\n')) {
            for(int at=0;at<Math.Max(1,paragraph.Length);at+=limit) { var piece=paragraph.Substring(at,Math.Min(limit,paragraph.Length-at)); if(current.Length+piece.Length+1>limit && current.Length>0) { chunks.Add(current.ToString()); current.Clear(); } current.Append(piece).Append('\n'); }
        }
        if(current.Length>0) chunks.Add(current.ToString()); return chunks;
    }
    internal static string ResolveThinkingProtocol(string address,string model,string setting)
    {
        if(setting is "effort" or "toggle" or "disabled")return setting;
        var host=Endpoint(address).Host;
        if(host.Equals("api.deepseek.com",StringComparison.OrdinalIgnoreCase))
            return model.Trim().Equals("deepseek-flash",StringComparison.OrdinalIgnoreCase)||model.Trim().Equals("deepseek-v4-pro",StringComparison.OrdinalIgnoreCase)?"deepseek":"disabled";
        if(host.Equals("api.openai.com",StringComparison.OrdinalIgnoreCase))return "openai";
        if(host.Equals("generativelanguage.googleapis.com",StringComparison.OrdinalIgnoreCase)&&Endpoint(address).AbsolutePath.Contains("/openai/",StringComparison.OrdinalIgnoreCase))return "gemini";
        if(host.StartsWith("dashscope",StringComparison.OrdinalIgnoreCase)&&host.EndsWith(".aliyuncs.com",StringComparison.OrdinalIgnoreCase))return "toggle";
        return "disabled";
    }
    internal static string BuildPayload(Preferences config,string system,string content)
    {
        var payload=new Dictionary<string,object> { ["model"]=config.Model, ["messages"]=new[]{new{role="system",content=system},new{role="user",content}}, ["stream"]=config.Stream };
        var protocol=ResolveThinkingProtocol(config.BaseUrl,config.Model,config.ThinkingProtocol);var mode=config.ThinkingMode;
        if(protocol is "effort" or "deepseek" or "openai" or "gemini") {
            if(mode is "high" or "low" or "medium" or "xhigh" or "max" or "none")payload["reasoning_effort"]=mode;
        } else if(protocol=="toggle"&&mode is "high" or "low" or "medium" or "xhigh" or "max" or "none")payload["enable_thinking"]=mode!="none";
        return JsonSerializer.Serialize(payload);
    }
    public async Task<string> Request(Preferences config,string system,string content,Action<string>? delta,CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(config.RequestTimeoutSeconds,30,600)));ct=timeout.Token;
        var timer=System.Diagnostics.Stopwatch.StartNew(); double? firstToken=null;
        var key=config.GetKey(); if(string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("请先在设置中填写 API Key。");
        if(string.IsNullOrWhiteSpace(config.Model)) throw new InvalidOperationException("请填写模型名称。");
        using var request=new HttpRequestMessage(HttpMethod.Post,Endpoint(config.BaseUrl));
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
        request.Content=new StringContent(BuildPayload(config,system,content),Encoding.UTF8,"application/json");
        using var response=await Client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct).ConfigureAwait(false);
        if(!response.IsSuccessStatusCode) {
            var code=(int)response.StatusCode;
            throw new HttpRequestException(code switch {401 or 403=>"API 认证失败，请检查 Key 与访问权限。",429=>"API 限流或额度不足，请稍后重试。",404=>"接口或模型不存在，请检查服务地址和模型名称。",400=>"接口不接受当前请求，请检查模型和思考参数；必要时选择不发送思考参数。",_=>$"AI 服务返回 HTTP {code}，请检查配置或稍后重试。"});
        }
        var output=new StringBuilder();
        if(config.Stream && response.Content.Headers.ContentType?.MediaType=="text/event-stream") {
            using var reader=new StreamReader(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false)); string? line; bool ended=false;
            while((line=await reader.ReadLineAsync(ct).ConfigureAwait(false))!=null) {
                if(!line.StartsWith("data:")) continue; var data=line[5..].Trim(); if(data=="[DONE]") { ended=true; break; } if(string.IsNullOrEmpty(data)) continue;
                using var doc=JsonDocument.Parse(data); var root=doc.RootElement;
                if(root.TryGetProperty("error",out _)) throw new IOException("AI 服务在生成过程中返回错误，请重试。");
                if(root.TryGetProperty("choices",out var choices) && choices.GetArrayLength()>0) { var choice=choices[0]; if(choice.TryGetProperty("finish_reason",out var reason) && reason.ValueKind==JsonValueKind.String) { var finish=reason.GetString(); if(finish=="length") throw new IOException("AI 输出因长度限制被截断，请减小每次整理长度后重试。"); if(finish=="stop") ended=true; } if(choice.TryGetProperty("delta",out var change) && change.TryGetProperty("content",out var token) && token.ValueKind==JsonValueKind.String) { var value=token.GetString()!; if(value.Length>0)firstToken ??= timer.Elapsed.TotalMilliseconds; output.Append(value); delta?.Invoke(value); } }
            }
            if(!ended) throw new IOException("AI 连接提前断开，结果未保存为完成状态，请重试。");
        } else {
            using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var choice=doc.RootElement.GetProperty("choices")[0];
            if(choice.TryGetProperty("finish_reason",out var reason) && reason.GetString()=="length") throw new IOException("AI 输出达到长度限制，请减小每次整理长度。");
            output.Append(choice.GetProperty("message").GetProperty("content").GetString()); firstToken=timer.Elapsed.TotalMilliseconds; delta?.Invoke(output.ToString());
        }
        if(string.IsNullOrWhiteSpace(output.ToString())) throw new IOException("AI 返回了空内容，请检查模型或重试。");
        PerformanceLog.Record("ai_request",timer.Elapsed.TotalMilliseconds,output.Length,firstToken);
        return output.ToString();
    }
    public async Task<string> Generate(Course course,Preferences prefs,Store store,IProgress<string> progress,Action<string> preview,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(course.Transcript))throw new InvalidOperationException("录音中没有识别到有效讲话，请检查录音音量。");
        var chunks=Split(course.Transcript,Math.Clamp(prefs.ChunkCharacters,2000,20000));
        if(chunks.Count==0) throw new InvalidOperationException("录音中没有识别到有效讲话，请检查录音音量。");
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(course.Transcript+prefs.Model+prefs.BaseUrl+prefs.ChunkCharacters+prefs.ThinkingMode+prefs.ThinkingProtocol+NotePrompts.ClassroomCacheKey)));
        if(course.DraftFingerprint!=fingerprint) { course.Drafts.Clear(); course.DraftFingerprint=fingerprint; store.Save(course); }
        using var batchCancellation=CancellationTokenSource.CreateLinkedTokenSource(ct);
        for(int start=course.Drafts.Count;start<chunks.Count;start+=2) {
            var offset=start;var count=Math.Min(2,chunks.Count-start);
            progress.Report($"正在整理笔记 {start+1}–{start+count}/{chunks.Count}");
            var tasks=Enumerable.Range(0,count).Select(j=>Request(prefs,NotePrompts.ClassroomRules,NotePrompts.Segment(false,course.Title,offset+j+1,chunks.Count,chunks[offset+j]),j==0?preview:null,batchCancellation.Token)).ToArray();
            try {
                for(int j=0;j<count;j++) {
                    var draft=await tasks[j];
                    course.Drafts.Add(draft);store.Save(course);
                    if(j>0)preview("\n\n"+draft);
                }
            } catch {batchCancellation.Cancel();try{await Task.WhenAll(tasks);}catch{}throw;}
        }
        var body=await Consolidate(course.Drafts,false,prefs,progress,ct);
        return $"# {course.Title}\n\n"+body;
    }
    public async Task<string> Enhance(Course course,Preferences prefs,Store? store,IProgress<string> progress,CancellationToken ct,Action<string>? preview=null,bool fromTranscript=false)
    {
        var source=fromTranscript?course.Transcript:course.Note;
        if(string.IsNullOrWhiteSpace(source))throw new InvalidOperationException(fromTranscript?"当前没有可整理的转写。":"当前没有可精编的笔记。");
        var rules=fromTranscript?NotePrompts.EnhancementRules.Replace("课堂笔记","录音转写").Replace("用户当前笔记","录音转写")+"\n\n"+NotePrompts.DirectTranscriptRules:NotePrompts.EnhancementRules;
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(course.Title+"\n"+source+fromTranscript+prefs.Model+prefs.BaseUrl+prefs.ChunkCharacters+prefs.ThinkingMode+prefs.ThinkingProtocol+NotePrompts.EnhancementCacheKey+(fromTranscript?NotePrompts.DirectTranscriptRules:""))));
        if(course.EnhancedFingerprint!=fingerprint){course.EnhancedDrafts.Clear();course.EnhancedFingerprint=fingerprint;store?.Save(course);}
        var chunks=Split(source,Math.Clamp(prefs.ChunkCharacters,2000,20000));
        var outline=Outline(source);
        using var batchCancellation=CancellationTokenSource.CreateLinkedTokenSource(ct);
        for(int start=course.EnhancedDrafts.Count;start<chunks.Count;start+=2){
            var offset=start;var count=Math.Min(2,chunks.Count-start);var live=new string[count];var prefix=string.Join("\n\n",course.EnhancedDrafts);var sync=new object();
            progress.Report(chunks.Count==1?"正在精编笔记":$"正在精编笔记 {start+1}–{start+count}/{chunks.Count}");
            var tasks=Enumerable.Range(0,count).Select(async j=>await Request(prefs,rules,NotePrompts.Segment(true,course.Title,offset+j+1,chunks.Count,chunks[offset+j],outline,fromTranscript),delta=>{lock(sync){live[j]+=delta;preview?.Invoke(prefix+"\n\n"+string.Join("\n\n",live));}},batchCancellation.Token)).ToArray();
            try {foreach(var task in tasks){course.EnhancedDrafts.Add(await task);store?.Save(course);}}
            catch {batchCancellation.Cancel();try{await Task.WhenAll(tasks);}catch{}throw;}
        }
        var body=await Consolidate(course.EnhancedDrafts,true,prefs,progress,ct);
        return $"# {course.Title}\n\n"+body;
    }

    private static string Outline(string text)
    {
        var result=new List<string>();int length=0;
        foreach(var part in TopicSections(text).Where(s=>s.Value.StartsWith("## ")))
        {
            if(length+part.Key.Length+1>4000)break;
            result.Add(part.Key);length+=part.Key.Length+1;
        }
        return string.Join("；",result);
    }

    internal static Dictionary<string,string> TopicSections(string text)
    {
        var sections=new Dictionary<string,StringBuilder>();string current="知识要点";
        char fence='\0';int fenceLength=0;
        foreach(var line in text.Replace("\r","").Split('\n'))
        {
            var marker=System.Text.RegularExpressions.Regex.Match(line,@"^ {0,3}(`{3,}|~{3,})(.*)$");
            if(marker.Success)
            {
                var token=marker.Groups[1].Value;
                if(fence=='\0'){fence=token[0];fenceLength=token.Length;}
                else if(token[0]==fence&&token.Length>=fenceLength&&string.IsNullOrWhiteSpace(marker.Groups[2].Value))fence='\0';
            }
            else if(fence=='\0'&&line.StartsWith("## "))current=System.Text.RegularExpressions.Regex.Replace(line[3..].Trim(),@"^(?:[一二三四五六七八九十]+[、.．]|\d+[、.．])\s*","");
            if(!sections.TryGetValue(current,out var part)){part=new StringBuilder();sections.Add(current,part);}
            part.AppendLine(line);
        }
        return sections.Where(s=>!string.IsNullOrWhiteSpace(s.Value.ToString())).ToDictionary(s=>s.Key,s=>s.Value.ToString().Trim());
    }

    private async Task<string> Consolidate(List<string> drafts,bool enhance,Preferences prefs,IProgress<string> progress,CancellationToken ct)
    {
        // A single response is already the finished note. Only multi-part
        // output needs merging; do not pay for another full rewrite by default.
        if(drafts.Count==1)return drafts[0];
        var mergeTimer=System.Diagnostics.Stopwatch.StartNew();
        var all=string.Join("\n\n",drafts);
        var outline=Outline(all);
        var material=all.Length<=30000?new List<string>{all}:TopicSections(all).Values.SelectMany(section=>Split(section,20000)).ToList();
        using var cancel=CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var slots=new SemaphoreSlim(2);
        int completed=0;
        var tasks=material.Select(async (item,index)=>
        {
            await slots.WaitAsync(cancel.Token);
            try
            {
                var result=await Request(prefs,enhance?NotePrompts.EnhancementRules:NotePrompts.ClassroomRules,NotePrompts.Merge(enhance,outline,item,material.Count==1?2:index<2?1:0,material.Count==1),null,cancel.Token);
                progress.Report($"正在合并笔记 {Interlocked.Increment(ref completed)}/{material.Count}");
                return result;
            }
            catch {cancel.Cancel();throw;}
            finally {slots.Release();}
        }).ToArray();
        progress.Report($"正在合并笔记 0/{material.Count}");
        var merged=string.Join("\n\n",await Task.WhenAll(tasks));
        PerformanceLog.Record("merge",mergeTimer.Elapsed.TotalMilliseconds,material.Count);
        return merged;
    }

}
