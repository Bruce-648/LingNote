using LingNote;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class UpdateChecks
{
    public static async Task Run(string root,Action<bool,string> check)
    {
        var payload=new byte[4096];payload[0]=(byte)'M';payload[1]=(byte)'Z';Random.Shared.NextBytes(payload.AsSpan(2));
        var hash=Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        const string name="LingNote-9.0.0-Setup.exe";const string url="https://github.com/Bruce-648/LingNote/releases/download/v9.0.0/";
        string Manifest(string? digest=null,bool prerelease=false,string? download=null,bool checksum=false)=>JsonSerializer.Serialize(new {
            draft=false,prerelease,tag_name="v9.0.0",body="更新说明：保留原录音。",assets=checksum?
            new object[]{new {name,size=payload.Length,browser_download_url=url+name},new {name=name+".sha256",size=64+name.Length+3,browser_download_url=url+name+".sha256"}}:
            new object[]{new {name,size=payload.Length,browser_download_url=download??url+name,digest=digest??"sha256:"+hash}}
        });
        check(UpdateService.ParseVersion("v0.1.33")==UpdateService.ParseVersion("0.1.33.0")&&UpdateService.ParseVersion("0.1.100")>UpdateService.ParseVersion("0.1.99"),"更新版本按数字比较并兼容 v 前缀，不按字符串排序");
        using(var service=new UpdateService(new Handler(_=>new HttpResponseMessage(HttpStatusCode.NotFound))))check(await service.Latest(default)==null,"尚无正式 Release 时显示暂无版本，不误判为可更新");
        AppRelease release;
        using(var service=new UpdateService(new Handler(request=>{
            check(request.RequestUri!.AbsolutePath=="/repos/Bruce-648/LingNote/releases/latest"&&request.Headers.UserAgent.ToString().StartsWith("LingNote/"),"通过固定官方仓库检查版本且发送应用标识");return Json(Manifest());
        }))) {release=(await service.Latest(default))!;check(release.Version==new Version(9,0,0,0)&&release.Sha256==hash&&release.Notes.Contains("保留原录音"),"解析正式版本、更新说明、安装包长度及 GitHub SHA-256");}
        using(var service=new UpdateService(new Handler(_=>Json(Manifest(prerelease:true)))))check(await service.Latest(default)==null,"忽略预发布版本");
        using(var service=new UpdateService(new Handler(_=>Json(Manifest(download:"https://untrusted.example/update.exe"))))) {
            try{await service.Latest(default);throw new Exception("Untrusted URL accepted");}catch(IOException){check(true,"拒绝来自其他仓库或其他网站的安装包地址");}
        }
        using(var service=new UpdateService(new Handler(_=>Json(Manifest(digest:"sha256:invalid"))))) {
            try{await service.Latest(default);throw new Exception("Invalid checksum accepted");}catch(IOException){check(true,"缺失或格式错误的校验信息不会被用于安装");}
        }
        using(var service=new UpdateService(new Handler(request=>request.RequestUri!.Host=="api.github.com"?Json(Manifest(checksum:true)):new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(hash+"  "+name)})))check((await service.Latest(default))!.Sha256==hash,"兼容独立 sha256 附件且核对对应安装包文件名");
        var folder=Path.Combine(root,"updates-test");var progress=new InlineProgress();string target;
        using(var service=new UpdateService(new Handler(_=>Bytes(payload))))target=await service.Download(release,folder,progress,default);
        check(File.ReadAllBytes(target).SequenceEqual(payload)&&!File.Exists(target+".download")&&progress.Messages.Any(s=>s.Contains("下载完成")),"更新包完整下载校验后原子保存，不留下半成品");
        File.WriteAllText(target,"MZexisting-installer");
        using(var service=new UpdateService(new Handler(_=>Bytes(payload)))) {
            try{await service.Download(release with {Sha256=new string('0',64)},folder,null,default);throw new Exception("Bad checksum accepted");}catch(IOException){check(File.ReadAllText(target)=="MZexisting-installer"&&!File.Exists(target+".download"),"更新包校验失败保留原文件并清理临时文件");}
        }
        using(var service=new UpdateService(new Handler(_=>Bytes(payload[..1024])))) {
            try{await service.Download(release,folder,null,default);throw new Exception("Truncation accepted");}catch(IOException){check(File.ReadAllText(target)=="MZexisting-installer","更新包下载不完整时禁止安装");}
        }
        using(var service=new UpdateService(new Handler(_=>Bytes(payload))))using(var cancellation=new CancellationTokenSource()) {
            cancellation.Cancel();try{await service.Download(release,folder,null,cancellation.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){check(File.ReadAllText(target)=="MZexisting-installer"&&!File.Exists(target+".download"),"取消更新下载保留原文件并允许重试");}
        }
        var invalid=Path.Combine(folder,"not-executable.exe");File.WriteAllText(invalid,"not a Windows executable");
        try{await UpdateService.Verify(invalid,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(invalid))),default);throw new Exception("Invalid EXE accepted");}catch(IOException){check(true,"校验值匹配但不是 Windows 可执行文件时仍拒绝安装");}
        var apiBefore=File.ReadAllBytes(Path.Combine(Paths.Root,"settings.json"));new UpdatePreferences {AutoCheck=false}.Save();
        check(!UpdatePreferences.Load().AutoCheck&&File.ReadAllBytes(Path.Combine(Paths.Root,"settings.json")).SequenceEqual(apiBefore),"自动检查开关独立保存，不改动 API 密钥和录音配置");
    }
    private static HttpResponseMessage Json(string json)=>new(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")};
    private static HttpResponseMessage Bytes(byte[] bytes)=>new(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)};
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){ct.ThrowIfCancellationRequested();return Task.FromResult(respond(request));}
    }
    private sealed class InlineProgress:IProgress<string>{public List<string> Messages {get;}=[];public void Report(string value)=>Messages.Add(value);}
}
