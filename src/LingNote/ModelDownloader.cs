using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Diagnostics;

namespace LingNote;
internal static class ModelDownloader
{
    internal static async Task Download(HttpClient http,Uri source,string destination,string sha256,long minimumBytes,IProgress<string> progress,CancellationToken ct)
    {
        var temp=destination+".download";
        try {
            progress.Report("正在连接模型下载服务…");
            using var response=await http.GetAsync(source,HttpCompletionOption.ResponseHeadersRead,ct);
            response.EnsureSuccessStatusCode();
            var length=response.Content.Headers.ContentLength;
            await using(var output=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None,81920,true))
            await using(var input=await response.Content.ReadAsStreamAsync(ct)) {
                var buffer=new byte[81920];long total=0;int n;var watch=Stopwatch.StartNew();
                while((n=await input.ReadAsync(buffer,ct))>0) {
                    await output.WriteAsync(buffer.AsMemory(0,n),ct);total+=n;
                    if(watch.ElapsedMilliseconds>=250){Report(total,length,progress);watch.Restart();}
                }
                Report(total,length,progress);
                if(length.HasValue&&total!=length)throw new IOException("模型下载不完整，请重试。");
            }
            ct.ThrowIfCancellationRequested();progress.Report("下载完成，正在校验模型…");
            if(new FileInfo(temp).Length<minimumBytes)throw new IOException("模型文件大小异常。");
            await using(var file=File.OpenRead(temp)) {
                var magic=new byte[4];await file.ReadExactlyAsync(magic,ct);
                if(BitConverter.ToUInt32(magic)!=0x67676d6c)throw new IOException("模型格式校验失败。");
                file.Position=0;
                if(!Convert.ToHexString(await SHA256.HashDataAsync(file,ct)).Equals(sha256,StringComparison.OrdinalIgnoreCase))throw new IOException("模型 SHA-256 校验失败，请重试或导入官方 small 模型。");
            }
            ct.ThrowIfCancellationRequested();File.Move(temp,destination,true);
            progress.Report("模型已就绪，可以离线转写。");
        } catch {if(File.Exists(temp))File.Delete(temp);throw;}
    }
    private static void Report(long bytes,long? length,IProgress<string> progress)=>progress.Report(
        $"已下载 {bytes/1048576.0:F1} MB"+(length is >0?$" / {length/1048576.0:F1} MB（{Math.Min(100,bytes*100/length.Value)}%）":""));
}
