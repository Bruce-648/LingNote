using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using NAudio.Wave;
using Whisper.net;
using Whisper.net.Ggml;

namespace LingNote;
public sealed class Transcriber : IDisposable
{
    public const string SmallSha256="1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b";
    private WhisperFactory? factory;
    private readonly SemaphoreSlim inferenceGate=new(1,1);
    public static string ModelFile => File.Exists(Paths.Model) ? Paths.Model : Path.Combine(AppContext.BaseDirectory,"models","ggml-small.bin");
    public static bool ModelAvailable => File.Exists(ModelFile) && new FileInfo(ModelFile).Length>400_000_000;
    public static async Task Download(IProgress<string> progress,CancellationToken ct)
    {
        Directory.CreateDirectory(Paths.Models);
        using var http=new HttpClient(new HttpClientHandler {UseProxy=false}) { Timeout=TimeSpan.FromHours(2) };
        const string url="https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin";
        await ModelDownloader.Download(http,new Uri(url),Paths.Model,SmallSha256,400_000_000,progress,ct);
    }
    public async Task<string> Run(AudioPart part,CancellationToken ct,string context="")
    {
        if(!ModelAvailable) throw new InvalidOperationException("请先在设置中下载或导入本地转写模型。");
        await inferenceGate.WaitAsync(ct);
        try {return await Task.Run(async ()=> {
            ct.ThrowIfCancellationRequested(); factory??=WhisperFactory.FromPath(ModelFile);
            using var reader=new AudioFileReader(part.File);
            using var pcm=new MemoryStream();
            var samples=reader.ToSampleProvider();
            if(samples.WaveFormat.Channels==2) samples=new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(samples);
            if(samples.WaveFormat.SampleRate!=16000) samples=new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(samples,16000);
            var buffer=new float[16000]; int n; float max=0;
            var values=new List<float>(); while((n=samples.Read(buffer,0,buffer.Length))>0) { ct.ThrowIfCancellationRequested(); for(int i=0;i<n;i++) { max=Math.Max(max,Math.Abs(buffer[i])); values.Add(buffer[i]); } }
            if(max<0.001f) return "";
            var builder=factory.CreateBuilder().WithLanguage("auto").WithThreads(Math.Min(8,Math.Max(2,Environment.ProcessorCount/2)));
            if(!string.IsNullOrWhiteSpace(context)) builder.WithPrompt(context.Length>300?context[^300..]:context);
            using var processor=builder.Build();
            var text=new List<string>();
            await foreach(var segment in processor.ProcessAsync(values.ToArray(),ct)) { if(!string.IsNullOrWhiteSpace(segment.Text)) text.Add(segment.Text.Trim()); }
            return string.Join(" ",text);
        },ct);} finally {inferenceGate.Release();}
    }
    public void Dispose()=>factory?.Dispose();
}
