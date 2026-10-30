using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LingNote;
public sealed record AppRelease(Version Version,string Notes,Uri Page,Uri Download,string FileName,long Size,string Sha256);
public sealed class UpdateService : IDisposable
{
    public const string Repository="Bruce-648/LingNote";
    public const string ReleasesPage="https://github.com/"+Repository+"/releases";
    public static Version CurrentVersion=>typeof(App).Assembly.GetName().Version??new Version(0,0,0,0);
    public static string DisplayVersion=>CurrentVersion.ToString(3);
    private readonly HttpClient client;
    private readonly HttpClient? directClient;
    private HttpClient activeClient;
    public UpdateService(HttpMessageHandler? handler=null,HttpMessageHandler? directHandler=null)
    {
        client=handler==null?new HttpClient():new HttpClient(handler);client.Timeout=TimeSpan.FromMinutes(15);
        directClient=directHandler!=null?new HttpClient(directHandler):handler==null?new HttpClient(new HttpClientHandler {UseProxy=false}):null;
        foreach(var connection in new[]{client,directClient}.OfType<HttpClient>()) {
            connection.Timeout=TimeSpan.FromMinutes(15);
            connection.DefaultRequestHeaders.UserAgent.ParseAdd("LingNote/"+DisplayVersion);
            connection.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }
        activeClient=client;
    }
    private async Task<HttpResponseMessage> Get(Uri uri,HttpCompletionOption completion,CancellationToken ct)
    {
        var primary=activeClient;var alternate=ReferenceEquals(primary,client)?directClient:client;
        HttpResponseMessage? response=null;
        try {
            using var attempt=CancellationTokenSource.CreateLinkedTokenSource(ct);attempt.CancelAfter(TimeSpan.FromSeconds(8));
            response=await primary.GetAsync(uri,completion,attempt.Token);
            if(alternate==null||response.IsSuccessStatusCode||response.StatusCode==HttpStatusCode.NotFound)return response;
            if(response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)&& (int)response.StatusCode<500)return response;
        }catch(Exception ex)when(alternate!=null&&!ct.IsCancellationRequested&&(ex is HttpRequestException or OperationCanceledException)) { }
        if(alternate==null)throw new HttpRequestException("无法连接 GitHub。");
        try {
            using var attempt=CancellationTokenSource.CreateLinkedTokenSource(ct);attempt.CancelAfter(TimeSpan.FromSeconds(12));
            var fallback=await alternate.GetAsync(uri,completion,attempt.Token);
            if(fallback.IsSuccessStatusCode){activeClient=alternate;response?.Dispose();return fallback;}
            response?.Dispose();return fallback;
        }catch(Exception ex)when(!ct.IsCancellationRequested&&(ex is HttpRequestException or OperationCanceledException)) {
            if(response!=null)return response;
            throw new HttpRequestException("无法连接 GitHub，已尝试当前网络设置和直连。",ex);
        }catch{response?.Dispose();throw;}
    }
    private static async Task EnsureAccess(HttpResponseMessage response,CancellationToken ct)
    {
        if(response.IsSuccessStatusCode)return;
        if(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests) {
            var body=await response.Content.ReadAsStringAsync(ct);
            var exhausted=response.Headers.TryGetValues("X-RateLimit-Remaining",out var remaining)&&remaining.Contains("0");
            var limited=exhausted||response.StatusCode==HttpStatusCode.TooManyRequests||response.Headers.RetryAfter!=null||body.Contains("rate limit",StringComparison.OrdinalIgnoreCase);
            if(limited) {
                DateTimeOffset? retryAt=response.Headers.RetryAfter?.Date;
                if(response.Headers.RetryAfter?.Delta is TimeSpan delta)retryAt=DateTimeOffset.UtcNow+delta;
                if(retryAt==null&&response.Headers.TryGetValues("X-RateLimit-Reset",out var reset)&&long.TryParse(reset.FirstOrDefault(),out var epoch)) {
                    try{retryAt=DateTimeOffset.FromUnixTimeSeconds(epoch);}catch(ArgumentOutOfRangeException){}
                }
                throw new IOException(retryAt>DateTimeOffset.UtcNow?$"GitHub 请求暂时受限，请在 {retryAt.Value.ToLocalTime():HH:mm:ss} 后重试。":"GitHub 请求暂时受限，请稍后重试。");
            }
            throw new IOException("GitHub 拒绝了更新请求（HTTP 403），请检查当前网络或代理的访问限制。");
        }
        response.EnsureSuccessStatusCode();
    }
    public static Version ParseVersion(string text)
    {
        if(!Regex.IsMatch(text,@"^v?\d+\.\d+\.\d+(\.\d+)?$",RegexOptions.CultureInvariant))throw new IOException("发布版本号格式不正确。");
        var v=Version.Parse(text.TrimStart('v'));return new Version(v.Major,v.Minor,v.Build,Math.Max(0,v.Revision));
    }
    public async Task<AppRelease?> Latest(CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var response=await Get(new Uri("https://api.github.com/repos/"+Repository+"/releases/latest"),HttpCompletionOption.ResponseContentRead,timeout.Token);
        if(response.StatusCode==HttpStatusCode.NotFound)return null;
        await EnsureAccess(response,timeout.Token);var json=await response.Content.ReadAsStringAsync(timeout.Token);
        using var document=JsonDocument.Parse(json);var root=document.RootElement;
        if(root.GetProperty("draft").GetBoolean()||root.GetProperty("prerelease").GetBoolean())return null;
        var tag=root.GetProperty("tag_name").GetString()??"";var version=ParseVersion(tag);
        var fileName="LingNote-"+version.ToString(3)+"-Setup.exe";
        var assets=root.GetProperty("assets").EnumerateArray().ToArray();
        var setup=assets.FirstOrDefault(a=>a.GetProperty("name").GetString()==fileName);
        if(setup.ValueKind==JsonValueKind.Undefined)throw new IOException("这个版本还没有完整的 Windows 安装包，请稍后再试。");
        var download=AssetUrl(setup,tag,fileName);var size=setup.GetProperty("size").GetInt64();
        if(size<1024||size>1024L*1024*1024)throw new IOException("安装包大小不正确。");
        var digest=setup.TryGetProperty("digest",out var value)?value.GetString():null;
        string hash;
        if(digest?.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase)==true)hash=digest[7..];
        else {
            var checksum=assets.FirstOrDefault(a=>a.GetProperty("name").GetString()==fileName+".sha256");
            if(checksum.ValueKind==JsonValueKind.Undefined)throw new IOException("这个版本缺少安装包校验文件，请稍后再试。");
            if(checksum.GetProperty("size").GetInt64()>4096)throw new IOException("校验文件大小不正确。");
            using var sumResponse=await Get(AssetUrl(checksum,tag,fileName+".sha256"),HttpCompletionOption.ResponseContentRead,timeout.Token);await EnsureAccess(sumResponse,timeout.Token);
            var sum=(await sumResponse.Content.ReadAsStringAsync(timeout.Token)).Trim();var fields=sum.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
            if(fields.Length!=2||fields[1].TrimStart('*')!=fileName)throw new IOException("校验文件与安装包不匹配。");hash=fields[0];
        }
        if(!Regex.IsMatch(hash,"^[a-fA-F0-9]{64}$"))throw new IOException("安装包 SHA-256 校验信息不正确。");
        var page=new Uri(ReleasesPage+"/tag/"+Uri.EscapeDataString(tag));
        return new AppRelease(version,root.TryGetProperty("body",out var notes)?notes.GetString()??"":"",page,download,fileName,size,hash);
    }
    private static Uri AssetUrl(JsonElement asset,string tag,string name)
    {
        var expected=new Uri("https://github.com/"+Repository+"/releases/download/"+Uri.EscapeDataString(tag)+"/"+Uri.EscapeDataString(name));
        if(!Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(),UriKind.Absolute,out var actual)||actual!=expected)throw new IOException("安装包下载地址不属于官方发布仓库。");
        return actual;
    }
    public async Task<string> Download(AppRelease release,string directory,IProgress<string>? progress,CancellationToken ct)
    {
        // File names are generated locally by Latest, never accepted as arbitrary paths.
        if(Path.GetFileName(release.FileName)!=release.FileName||!release.FileName.EndsWith("-Setup.exe",StringComparison.Ordinal))throw new IOException("安装包文件名不正确。");
        Directory.CreateDirectory(directory);var target=Path.Combine(directory,release.FileName);var partial=target+".download";
        try {
            if(File.Exists(target)){try{await Verify(target,release.Sha256,ct);if(new FileInfo(target).Length==release.Size)return target;}catch(IOException){}}
            using var response=await Get(release.Download,HttpCompletionOption.ResponseHeadersRead,ct);await EnsureAccess(response,ct);
            if(response.Content.Headers.ContentLength is long declared&&declared!=release.Size)throw new IOException("安装包长度与发布信息不一致。");
            await using(var output=new FileStream(partial,FileMode.Create,FileAccess.Write,FileShare.None,65536,true))
            await using(var input=await response.Content.ReadAsStreamAsync(ct)) {
                var buffer=new byte[65536];long total=0;int read;var watch=System.Diagnostics.Stopwatch.StartNew();
                while((read=await input.ReadAsync(buffer,ct))>0){total+=read;if(total>release.Size)throw new IOException("安装包大小超出发布信息。");await output.WriteAsync(buffer.AsMemory(0,read),ct);if(watch.ElapsedMilliseconds>=100){progress?.Report($"正在下载 {total*100/release.Size}% · {total/1048576d:F1} / {release.Size/1048576d:F1} MB");watch.Restart();}}
                if(total!=release.Size)throw new IOException("下载未完成，请重试。");
            }
            progress?.Report("正在校验安装包…");await Verify(partial,release.Sha256,ct);ct.ThrowIfCancellationRequested();File.Move(partial,target,true);progress?.Report("下载完成，可以安装并重启。");return target;
        }finally {if(File.Exists(partial))File.Delete(partial);}
    }
    public static async Task Verify(string path,string expectedHash,CancellationToken ct)
    {
        await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,65536,true);
        if(stream.ReadByte()!='M'||stream.ReadByte()!='Z')throw new IOException("文件不是有效的 Windows 安装程序。");stream.Position=0;
        var hash=Convert.ToHexString(await SHA256.HashDataAsync(stream,ct));
        if(!hash.Equals(expectedHash,StringComparison.OrdinalIgnoreCase))throw new IOException("安装包校验失败，请重新下载。");
    }
    public void Dispose(){client.Dispose();directClient?.Dispose();}
}
public sealed class UpdatePreferences
{
    public bool AutoCheck {get;set;}=true;
    public DateTimeOffset LastCheck {get;set;}
    private static string FileName=>Path.Combine(Paths.Root,"update-settings.json");
    public static UpdatePreferences Load(){try{return JsonSerializer.Deserialize<UpdatePreferences>(File.ReadAllText(FileName))??new();}catch(IOException){return new();}catch(JsonException){return new();}}
    public void Save(){Paths.Init();var temp=FileName+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,FileName,true);}
}
