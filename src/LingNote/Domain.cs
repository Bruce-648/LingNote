using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace LingNote;

public static class Paths
{
    public static string Root { get; private set; } = ResolveRoot();
    public static string ProjectDirectory {
        get {var home=AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);if(Path.GetFileName(home).Equals("app",StringComparison.OrdinalIgnoreCase))home=Path.GetDirectoryName(home)!;for(var candidate=new DirectoryInfo(home);candidate!=null;candidate=candidate.Parent)if(Directory.Exists(Path.Combine(candidate.FullName,"src","LingNote"))&&Directory.Exists(Path.Combine(candidate.FullName,"tests")))return candidate.FullName;return home;}
    }
    public static string DefaultDirectory=>Path.Combine(ProjectDirectory,"Data");
    public static void ChangeDirectory(string destination)
    {
        destination=Path.GetFullPath(destination);if(Path.TrimEndingDirectorySeparator(destination).Equals(Path.TrimEndingDirectorySeparator(Root),StringComparison.OrdinalIgnoreCase))return;
        LibraryLocation.Copy(Root,destination);
        var binding=Path.Combine(AppContext.BaseDirectory,"library-location.json");
        var values=File.Exists(binding)?JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(binding))!:new();
        values["DataDirectory"]=JsonSerializer.SerializeToElement(destination);
        var temporary=binding+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(values));File.Move(temporary,binding,true);
        Root=destination;
    }
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
        return DefaultDirectory;
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
    public string ThinkingMode { get; set; } = "default";
    public string ThinkingProtocol { get; set; } = "auto";
    public string GenerationMode { get; set; } = "classroom";
    public string ThemeAccent {get;set;}="#1263F1";
    public string ThemeBackground {get;set;}="#FFFFFF";
    public string ScreenshotDirectory {get;set;}="";
    public Dictionary<string,string> TopicScreenshotDirectories {get;set;}=[];
    public int RequestTimeoutSeconds {get;set;}=600;
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
                var saved=Load();BaseUrl=saved.BaseUrl;Model=saved.Model;EncryptedKey=saved.EncryptedKey;ThinkingMode=saved.ThinkingMode;ThinkingProtocol=saved.ThinkingProtocol;GenerationMode=saved.GenerationMode;RequestTimeoutSeconds=saved.RequestTimeoutSeconds;Stream=saved.Stream;ChunkCharacters=saved.ChunkCharacters;
            }
            var temporary=SettingsFile+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {File.WriteAllText(temporary,JsonSerializer.Serialize(this));
                if(File.Exists(SettingsFile))File.Replace(temporary,SettingsFile,SettingsFile+".bak");else File.Move(temporary,SettingsFile);
            }finally {if(File.Exists(temporary))File.Delete(temporary);}
        }
    }

}
public sealed class Course : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private string title="录音 " + DateTime.Now.ToString("MM-dd HH:mm");
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get=>title; set {if(title==value)return;title=value;PropertyChanged?.Invoke(this,new System.ComponentModel.PropertyChangedEventArgs(nameof(Title)));} }
    public string Topic { get; set; } = "";
    public string SectionId { get; set; } = "";
    public bool Archived { get; set; }
    public bool IsRead { get; set; }
    public string TopicGroup => string.IsNullOrWhiteSpace(Topic)?"未分区":Topic;
    public string DayGroup => Created.Date==DateTime.Today?"今天":Created.Date==DateTime.Today.AddDays(-1)?"昨天":Created.ToString("yyyy年MM月dd日");
    public DateTime Created { get; set; } = DateTime.Now;
    public double Seconds { get; set; }
    public string State { get; set; } = "录音中";
    public bool DirectRefinement { get; set; }
    public string EnhancedNote { get; set; } = "";
    public List<string> EnhancedVersions { get; set; } = new();
    public List<string> EnhancedDrafts { get; set; } = new();
    public string EnhancedFingerprint { get; set; } = "";
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
