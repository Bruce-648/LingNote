using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace LingNote;

public static class Paths
{
    public static string Root { get; } = ResolveRoot();
    private static string ResolveRoot()
    {
        // The desktop app always opens the same user library. Test runners may
        // isolate their data without an inherited test variable hiding real recordings.
        var desktop=System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name=="LingNote";
        var testRoot=Environment.GetEnvironmentVariable("LINGNOTE_DATA");
        if(!desktop&&!string.IsNullOrWhiteSpace(testRoot))return Path.GetFullPath(testRoot);
        var binding=Path.Combine(AppContext.BaseDirectory,"library-location.json");
        if(desktop&&File.Exists(binding)) {
            using var document=JsonDocument.Parse(File.ReadAllText(binding));
            var saved=document.RootElement.GetProperty("DataDirectory").GetString();
            if(string.IsNullOrWhiteSpace(saved)||!Path.IsPathFullyQualified(saved)||!File.Exists(Path.Combine(saved,"library.db")))
                throw new IOException("原录音库暂时无法访问。请检查数据目录："+saved);
            return Path.GetFullPath(saved);
        }
        var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if(string.IsNullOrWhiteSpace(local))local=Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if(string.IsNullOrWhiteSpace(local)||!Path.IsPathFullyQualified(local))
            throw new IOException("无法确定用户数据目录。已停止启动，避免创建空的录音库。");
        return Path.Combine(local,"LingNote");
    }
    public static string Models => Path.Combine(Root, "models");
    public static string Model => Path.Combine(Models, "ggml-small.bin");
    public static void Init() { Directory.CreateDirectory(Root); Directory.CreateDirectory(Models); }
}
public sealed class Preferences
{
    public string BaseUrl { get; set; } = "https://api.deepseek.com/v1";
    public string Model { get; set; } = "deepseek-chat";
    public string EncryptedKey { get; set; } = "";
    public string ActiveTopic { get; set; } = "";
    public List<string> Topics { get; set; } = [];
    public List<LibrarySection> Sections { get; set; } = [];
    public Dictionary<string,string> TopicSections { get; set; } = [];
    public string ActiveSection { get; set; } = "";
    public bool GroupUnassignedByDay { get; set; }
    public bool GroupByTopic { get; set; }
    public bool Stream { get; set; } = true;
    public int ChunkCharacters { get; set; } = 10000;
    public double FloatingLeft { get; set; } = -1;
    public double FloatingTop { get; set; } = 48;
    public string GetKey() => string.IsNullOrEmpty(EncryptedKey) ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(EncryptedKey), null, DataProtectionScope.CurrentUser));
    public void SetKey(string key) => EncryptedKey = string.IsNullOrWhiteSpace(key) ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), null, DataProtectionScope.CurrentUser));
    private static readonly object settingsGate=new();
    private static string SettingsFile=>Path.Combine(Paths.Root,"settings.json");
    public static Preferences Load()
    {
        lock(settingsGate) {
            foreach(var file in new[]{SettingsFile,SettingsFile+".bak"}) {
                if(!File.Exists(file))continue;
                try {var value=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(file));if(value!=null)return value;}catch(JsonException) {}catch(IOException) {}
            }
            if(File.Exists(SettingsFile)||File.Exists(SettingsFile+".bak"))throw new IOException("设置文件无法读取，原文件已保留。请检查数据文件夹。");
            return new();
        }
    }
    public void SaveApi()=>SaveCore(true);
    public void Save()=>SaveCore(false);
    private void SaveCore(bool apiChanged)
    {
        lock(settingsGate) {
            Paths.Init();
            if(!apiChanged && (File.Exists(SettingsFile)||File.Exists(SettingsFile+".bak"))) {
                var saved=Load();BaseUrl=saved.BaseUrl;Model=saved.Model;EncryptedKey=saved.EncryptedKey;Stream=saved.Stream;ChunkCharacters=saved.ChunkCharacters;
            }
            var temporary=SettingsFile+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {File.WriteAllText(temporary,JsonSerializer.Serialize(this));
                if(File.Exists(SettingsFile))File.Replace(temporary,SettingsFile,SettingsFile+".bak");else File.Move(temporary,SettingsFile);
            }finally {if(File.Exists(temporary))File.Delete(temporary);}
        }
    }

}
public sealed class Course
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "录音 " + DateTime.Now.ToString("MM-dd HH:mm");
    public string Topic { get; set; } = "";
    public string SectionId { get; set; } = "";
    public bool Archived { get; set; }
    public bool IsRead { get; set; }
    public string TopicGroup => string.IsNullOrWhiteSpace(Topic)?"未分区":Topic;
    public string DayGroup => Created.Date==DateTime.Today?"今天":Created.Date==DateTime.Today.AddDays(-1)?"昨天":Created.ToString("yyyy年MM月dd日");
    public DateTime Created { get; set; } = DateTime.Now;
    public double Seconds { get; set; }
    public string State { get; set; } = "录音中";
    public string Note { get; set; } = "";
    public string Error { get; set; } = "";
    public List<AudioPart> Parts { get; set; } = [];
    public List<string> Versions { get; set; } = [];
    public List<string> Drafts { get; set; } = [];
    public string DraftFingerprint { get; set; } = "";
    public string Folder => Path.Combine(Paths.Root,"recordings",Id);
    public string Transcript => string.Join("\n\n",Parts.Where(p=>p.Done && !string.IsNullOrWhiteSpace(p.Text)).Select(p=>$"[{TimeSpan.FromSeconds(p.Offset):hh\\:mm\\:ss}] {p.Text}"));
    public string Caption => $"{Created:MM月dd日 HH:mm}  ·  {TimeSpan.FromSeconds(Seconds):hh\\:mm\\:ss}  ·  {State}";
}
public sealed class AudioPart
{
    public string File { get; set; } = "";
    public double Offset { get; set; }
    public double Duration { get; set; }
    public bool Done { get; set; }
    public string Text { get; set; } = "";
}
