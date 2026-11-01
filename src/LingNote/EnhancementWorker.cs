using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace LingNote;
internal static class EnhancementWorker
{
    private sealed record Input(string Title,string Note,Preferences Preferences,List<string> Drafts,string Fingerprint);
    private sealed record Output(string Note,List<string> Drafts,string Fingerprint,string? Error);
    internal sealed record Update(string Kind,string Text);
    private sealed class Reporter(Action<string> action):IProgress<string>{public void Report(string value)=>action(value);}
    public static async Task Serve()
    {
        Console.InputEncoding=Console.OutputEncoding=Encoding.UTF8;
        var outputGate=new object();var last=Stopwatch.StartNew();
        void Send(Update update){lock(outputGate){Console.Out.WriteLine(JsonSerializer.Serialize(update));Console.Out.Flush();}}
        Course course=new();string note="";string? error=null;
        try {
            var input=JsonSerializer.Deserialize<Input>(await Console.In.ReadToEndAsync().ConfigureAwait(false))!;
            course.Title=input.Title;course.Note=input.Note;course.EnhancedDrafts=input.Drafts;course.EnhancedFingerprint=input.Fingerprint;
            note=await new NoteService().Enhance(course,input.Preferences,null,new Reporter(s=>Send(new Update("progress",s))),CancellationToken.None,s=>{if(last.ElapsedMilliseconds<250)return;last.Restart();Send(new Update("preview",s));}).ConfigureAwait(false);
        } catch(Exception ex){error=ex.Message;}
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new Output(note,course.EnhancedDrafts,course.EnhancedFingerprint,error))).ConfigureAwait(false);
        await Console.Out.FlushAsync().ConfigureAwait(false);
    }
    public static async Task<string> Generate(Course course,Preferences prefs,CancellationToken ct,IProgress<Update>? progress=null)
    {
        var input=JsonSerializer.Serialize(new Input(course.Title,course.Note,prefs,course.EnhancedDrafts,course.EnhancedFingerprint));
        var start=new ProcessStartInfo(Path.ChangeExtension(typeof(App).Assembly.Location,".exe"),"--enhance-worker") {
            UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,
            RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
            StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
        };
        using var process=Process.Start(start)??throw new IOException("无法启动笔记完善进程。");
        using var cancel=ct.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}});
        try {
            async Task<string> Read(){string? line;string result="";while((line=await process.StandardOutput.ReadLineAsync(ct))!=null){using var json=JsonDocument.Parse(line);if(json.RootElement.TryGetProperty("Kind",out _))progress?.Report(JsonSerializer.Deserialize<Update>(line)!);else result=line;}return result;}
            var output=Read();var errors=process.StandardError.ReadToEndAsync(ct);
            await process.StandardInput.WriteAsync(input.AsMemory(),ct);process.StandardInput.Close();
            await process.WaitForExitAsync(ct);var text=await output;await errors;ct.ThrowIfCancellationRequested();
            if(process.ExitCode!=0||string.IsNullOrWhiteSpace(text))throw new IOException("笔记完善进程意外退出，原笔记已保留，可重试。");
            var result=JsonSerializer.Deserialize<Output>(text)??throw new IOException("无法读取完善结果。");
            course.EnhancedDrafts=result.Drafts;course.EnhancedFingerprint=result.Fingerprint;
            if(result.Error!=null)throw new IOException(result.Error);
            return result.Note;
        } catch(Exception) when(ct.IsCancellationRequested){throw new OperationCanceledException(ct);}
        finally {try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}}
    }
}
