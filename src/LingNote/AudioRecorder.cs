using NAudio.Wave;
using System.Diagnostics;
using System.IO;
using NAudio.CoreAudioApi;

namespace LingNote;
public sealed class AudioRecorder : IDisposable
{
    private readonly object gate=new();
    private WasapiLoopbackCapture? capture;
    private WaveFileWriter? writer;
    private readonly Stopwatch clock=new();
    private System.Threading.Timer? timer;
    private string folder="";
    private string currentPath="";
    private long written;
    private long partBytes;
    private double offset;
    private int index;
    private WaveFormat format=null!;
    private bool active;
    private string deviceId="";
    
    private System.Threading.Timer? deviceTimer;
    private DateTime lastTick;
    public double TotalSeconds { get; private set; }
    public float Level { get; private set; }
    public event Action<AudioPart>? PartReady;
    public event Action<string>? Fault;
    public void Begin(string destination, int nextIndex=0, double startOffset=0) { folder=destination; index=nextIndex; TotalSeconds=startOffset; Directory.CreateDirectory(folder); Resume(); }
    public void Resume()
    {
        lock(gate)
        {
            if(active) return;
            if(new DriveInfo(Path.GetPathRoot(folder)!).AvailableFreeSpace<1024L*1024*1024) throw new IOException("磁盘可用空间不足 1 GB，请清理后再录音。");
            using(var devices=new MMDeviceEnumerator()) using(var device=devices.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia)) deviceId=device.ID;
            capture=new WasapiLoopbackCapture(); format=capture.WaveFormat; offset=TotalSeconds; written=0; partBytes=0; OpenPart();
            capture.DataAvailable+=OnData;
            capture.RecordingStopped+=(_,e)=> { if(e.Exception!=null && active) Fault?.Invoke("音频设备中断："+e.Exception.Message+"。请检查播放设备，再点击继续。"); };
            active=true; lastTick=DateTime.UtcNow; clock.Restart(); capture.StartRecording();
            timer=new System.Threading.Timer(_=>Tick(),null,1000,1000);
            deviceTimer=new System.Threading.Timer(_=> {try {using var devices=new MMDeviceEnumerator(); using var device=devices.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia); if(device.ID!=deviceId && active) {Fault?.Invoke("播放设备已切换，录音已暂停；点击继续将录制新设备。");}} catch(Exception) {if(active) Fault?.Invoke("播放设备不可用，请连接耳机或音箱后继续。");}},null,2000,2000);
        }
    }
    private void OpenPart() { currentPath=Path.Combine(folder,$"part-{index++:D5}.wav"); writer=new WaveFileWriter(currentPath,format); partBytes=0; }
    private void OnData(object? sender,WaveInEventArgs e)
    {
        try { lock(gate) { if(!active) return; float peak=0; for(int i=0;i+4<=e.BytesRecorded;i+=4) peak=Math.Max(peak,Math.Abs(BitConverter.ToSingle(e.Buffer,i))); Level=Math.Clamp(peak,0,1); var target=Aligned((clock.Elapsed.TotalSeconds*format.AverageBytesPerSecond)-e.BytesRecorded); PadTo(target); Write(e.Buffer,0,e.BytesRecorded); } }
        catch(Exception ex) { Fault?.Invoke("录音写入失败："+ex.Message); }
    }
    private long Aligned(double bytes)=>Math.Max(0,(long)bytes/format.BlockAlign*format.BlockAlign);
    private void Tick()
    {
        try { lock(gate) { if(!active) return; if((DateTime.UtcNow-lastTick).TotalSeconds>10) { Fault?.Invoke("检测到睡眠或系统中断，录音已暂停。请点击继续。"); return; } lastTick=DateTime.UtcNow; PadTo(Aligned(Math.Max(0,clock.Elapsed.TotalSeconds-.3)*format.AverageBytesPerSecond)); writer?.Flush(); Level*=.5f; if(new DriveInfo(Path.GetPathRoot(folder)!).AvailableFreeSpace<200L*1024*1024) Fault?.Invoke("磁盘空间不足，录音已暂停。"); } }
        catch(Exception ex) { Fault?.Invoke("录音设备异常："+ex.Message); }
    }
    private void PadTo(long target) { var zeros=new byte[16384/format.BlockAlign*format.BlockAlign]; while(written<target) { var count=(int)Math.Min(zeros.Length,target-written); Write(zeros,0,count); } }
    private void Write(byte[] bytes,int at,int count)
    {
        long limit=Aligned(30d*format.AverageBytesPerSecond);
        while(count>0) { int take=(int)Math.Min(count,limit-partBytes); writer!.Write(bytes,at,take); written+=take; partBytes+=take; at+=take; count-=take; if(partBytes>=limit) { Commit(); OpenPart(); } }
    }
    private void Commit()
    {
        if(writer==null) return; writer.Flush(); writer.Dispose(); writer=null;
        double duration=(double)partBytes/format.AverageBytesPerSecond;
        if(duration>0) { var part=new AudioPart { File=currentPath,Offset=TotalSeconds,Duration=duration }; TotalSeconds+=duration; PartReady?.Invoke(part); } else File.Delete(currentPath);
    }
    public void Pause()
    {
        WasapiLoopbackCapture? old;
        lock(gate) { if(!active) return; active=false; timer?.Dispose(); timer=null; deviceTimer?.Dispose(); deviceTimer=null; clock.Stop(); PadTo(Aligned(Math.Min(clock.Elapsed.TotalSeconds,(DateTime.UtcNow-lastTick).TotalSeconds>10 ? (double)written/format.AverageBytesPerSecond : clock.Elapsed.TotalSeconds)*format.AverageBytesPerSecond)); Commit(); old=capture; capture=null; Level=0;  }
        if(old!=null) { old.DataAvailable-=OnData; old.StopRecording(); old.Dispose(); }
    }
    public double Elapsed => active ? offset+clock.Elapsed.TotalSeconds : TotalSeconds;
    public void Dispose()=>Pause();
}

