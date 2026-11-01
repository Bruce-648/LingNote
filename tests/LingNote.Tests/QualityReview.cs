using System.IO;
using System.Text.Json;
using LingNote;
using Microsoft.Data.Sqlite;

internal static class QualityReview
{
    public static async Task TopicGap()
    {
        var output=Path.GetFullPath(Path.Combine("artifacts","topic-gap-review",DateTime.Now.ToString("yyyyMMdd-HHmmss")));Directory.CreateDirectory(output);
        var prefs=JsonSerializer.Deserialize<Preferences>(File.ReadAllText("Data/settings.json"))!;
        using var connection=new SqliteConnection("Data Source=E:/LingNote/Data/library.db;Mode=ReadOnly");connection.Open();
        using var command=connection.CreateCommand();command.CommandText="SELECT document FROM courses";
        var courses=new List<Course>();using(var reader=command.ExecuteReader())while(reader.Read())courses.Add(JsonSerializer.Deserialize<Course>(reader.GetString(0))!);
        var source=courses.Single(c=>c.Title=="计算机网络的性能分析");
        var text=source.Note.Replace("\r","");
        text=System.Text.RegularExpressions.Regex.Replace(text,@"(?ms)^## (?:信道利用率|网络利用率与信道利用率的实际意义|时延及相关性能指标小结)[^\n]*\n.*?(?=^## |\z)","");
        text=string.Join("\n",text.Split('\n').Where(line=>!line.Contains("利用率")));
        if(text.Contains("利用率"))throw new InvalidOperationException("测试输入未成功移除目标知识");
        await File.WriteAllTextAsync(Path.Combine(output,"01-测试输入（移除利用率）.md"),text);
        var copy=new Course {Title=source.Title,Note=text};using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(8));
        Console.WriteLine("Gap review: "+output);var timer=System.Diagnostics.Stopwatch.StartNew();
        var result=await new NoteService().Enhance(copy,prefs,null,new Progress<string>(Console.WriteLine),timeout.Token);timer.Stop();
        await File.WriteAllTextAsync(Path.Combine(output,"02-原始生成结果.md"),result);
        await File.WriteAllTextAsync(Path.Combine(output,"说明.md"),$"# 受控补全测试\n\n从同一份课堂笔记的副本中移除有关利用率的段落和提及，保留主题标题和其他内容，以检查模型是否能根据主题补充该知识。原数据库只读。\n\n模型：{prefs.Model}；思考模式：{prefs.ThinkingMode}；耗时：{timer.Elapsed.TotalSeconds:F1} 秒。输出是模型原文。\n");
        Console.WriteLine("Result contains utilization: "+result.Contains("利用率"));
    }

    public static async Task Readability()
    {
        var output=Path.GetFullPath(Path.Combine("artifacts","note-direct-review",DateTime.Now.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(output);
        var prefs=JsonSerializer.Deserialize<Preferences>(File.ReadAllText("Data/settings.json"))!;
        using var connection=new SqliteConnection("Data Source=E:/LingNote/Data/library.db;Mode=ReadOnly");connection.Open();
        using var command=connection.CreateCommand();command.CommandText="SELECT document FROM courses ORDER BY created DESC";
        var courses=new List<Course>();using(var reader=command.ExecuteReader())while(reader.Read())courses.Add(JsonSerializer.Deserialize<Course>(reader.GetString(0))!);
        var source=courses.Single(c=>c.Title=="计算机网络的性能分析");
        await File.WriteAllTextAsync(Path.Combine(output,"01-输入课堂笔记.md"),source.Note);
        await File.WriteAllTextAsync(Path.Combine(output,"02-修改前完善笔记.md"),source.EnhancedNote);
        var copy=new Course {Title=source.Title,Note=source.Note};
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(8));
        Console.WriteLine("Review output: "+output);
        var watch=System.Diagnostics.Stopwatch.StartNew();
        var revised=await new NoteService().Enhance(copy,prefs,null,new Progress<string>(Console.WriteLine),timeout.Token);
        watch.Stop();
        await File.WriteAllTextAsync(Path.Combine(output,"03-单次生成结果.md"),revised);
        await File.WriteAllTextAsync(Path.Combine(output,"04-分段输出.md"),string.Join("\n\n",copy.EnhancedDrafts));
        await File.WriteAllTextAsync(Path.Combine(output,"review-info.json"),JsonSerializer.Serialize(new {source.Title,prefs.Model,prefs.ChunkCharacters,ElapsedSeconds=watch.Elapsed.TotalSeconds,BeforeLength=source.EnhancedNote.Length,AfterLength=revised.Length,Drafts=copy.EnhancedDrafts.Count,SourceSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source.Note))),Fingerprint=copy.EnhancedFingerprint},new JsonSerializerOptions {WriteIndented=true}));
        Console.WriteLine($"Saved sample: {source.EnhancedNote.Length} -> {revised.Length} characters; original library opened read-only.");
    }

    public static async Task Run(bool shortOnly=false)
    {
        var output=Path.GetFullPath("artifacts/note-quality-review");Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("LINGNOTE_DATA",Path.Combine(output,"isolated-data"));
        var prefs=JsonSerializer.Deserialize<Preferences>(File.ReadAllText("Data/settings.json"))!;
        prefs.ChunkCharacters=6000;
        using var connection=new SqliteConnection("Data Source=E:/LingNote/Data/library.db;Mode=ReadOnly");connection.Open();
        using var command=connection.CreateCommand();command.CommandText="SELECT document FROM courses ORDER BY created DESC LIMIT 2";
        var courses=new List<Course>();using(var reader=command.ExecuteReader())while(reader.Read())courses.Add(JsonSerializer.Deserialize<Course>(reader.GetString(0))!);
        var service=new NoteService();using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(8));
        int index=0;
        foreach(var source in courses.Take(shortOnly?1:2)){
            index++;var name=$"{index}";await File.WriteAllTextAsync(Path.Combine(output,name+"-原课堂笔记.md"),source.Note);
            await File.WriteAllTextAsync(Path.Combine(output,name+"-原完善笔记.md"),source.EnhancedNote);
            var copy=new Course {Title=source.Title,Note=source.Note};
            Console.WriteLine($"Review {index}: {source.Title}");
            var revised=await service.Enhance(copy,prefs,null,new Progress<string>(Console.WriteLine),timeout.Token);
            await File.WriteAllTextAsync(Path.Combine(output,name+"-新版完善笔记.md"),revised);
            if(index==1){
                var classroom=new Course {Title=source.Title,Created=source.Created,Seconds=source.Seconds,Parts=source.Parts};
                var note=await service.Generate(classroom,prefs,new Store(),new Progress<string>(Console.WriteLine),_=>{},timeout.Token);
                await File.WriteAllTextAsync(Path.Combine(output,name+"-新版课堂笔记.md"),note);
            }
            Console.WriteLine($"Saved review {index}");
        }
    }
}
