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
    public UpdateService(HttpMessageHandler? handler=null)
    {
        client=handler==null?new HttpClient():new HttpClient(handler);client.Timeout=TimeSpan.FromMinutes(15);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LingNote/"+DisplayVersion);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }
    public static Version ParseVersion(string text)
    {
        if(!Regex.IsMatch(text,@"^v?\d+\.\d+\.\d+(\.\d+)?$",RegexOptions.CultureInvariant))throw new IOException("发布版本号格式不正确。");
        var v=Version.Parse(text.TrimStart('v'));return new Version(v.Major,v.Minor,v.Build,Math.Max(0,v.Revision));
    }
    public async Task<AppRelease?> Latest(CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var response=await client.GetAsync("https://api.github.com/repos/"+Repository+"/releases/latest",timeout.Token);
        if(response.StatusCode==HttpStatusCode.NotFound)return null;
        if(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)throw new IOException("GitHub 暂时限制了请求，请稍后再检查。");
        response.EnsureSuccessStatusCode();var json=await response.Content.ReadAsStringAsync(timeout.Token);
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
            using var sumResponse=await client.GetAsync(AssetUrl(checksum,tag,fileName+".sha256"),timeout.Token);sumResponse.EnsureSuccessStatusCode();
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
            using var response=await client.GetAsync(release.Download,HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();
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
    public void Dispose()=>client.Dispose();
}
public sealed class UpdatePreferences
{
    public bool AutoCheck {get;set;}=true;
    public DateTimeOffset LastCheck {get;set;}
    private static string FileName=>Path.Combine(Paths.Root,"update-settings.json");
    public static UpdatePreferences Load(){try{return JsonSerializer.Deserialize<UpdatePreferences>(File.ReadAllText(FileName))??new();}catch(IOException){return new();}catch(JsonException){return new();}}
    public void Save(){Paths.Init();var temp=FileName+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,FileName,true);}
}
