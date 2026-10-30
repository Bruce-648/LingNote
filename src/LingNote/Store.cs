using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.IO;

namespace LingNote;
public sealed class Store
{
    private readonly object gate = new();
    private SqliteConnection Open() { var c = new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=Path.Combine(Paths.Root,"library.db"),Pooling=false}.ToString()); c.Open(); return c; }
    public Store()
    {
        Paths.Init();using var c=Open();
        var binding=Path.Combine(AppContext.BaseDirectory,"library-location.json");
        if(File.Exists(binding)) {
            using var document=JsonDocument.Parse(File.ReadAllText(binding));
            if(document.RootElement.TryGetProperty("LibraryId",out var expected)) {
                using var identity=c.CreateCommand();identity.CommandText="SELECT value FROM library_meta WHERE key='library_id'";
                try {if(!string.Equals(identity.ExecuteScalar() as string,expected.GetString(),StringComparison.Ordinal))throw new IOException("打开的录音库与原库不一致，已停止加载。数据位置："+Paths.Root);}
                catch(SqliteException ex) {throw new IOException("无法验证原录音库，已停止加载。数据位置："+Paths.Root,ex);}
            }
        }
        using var q=c.CreateCommand();q.CommandText="PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS deleted_courses(id TEXT PRIMARY KEY); CREATE TABLE IF NOT EXISTS courses(id TEXT PRIMARY KEY, created TEXT NOT NULL, document TEXT NOT NULL);";q.ExecuteNonQuery();
        BackupForVersion(c);
    }
    private static void BackupForVersion(SqliteConnection source)
    {
        var version=typeof(Store).Assembly.GetName().Version?.ToString()??"unknown";
        var folder=Path.Combine(Paths.Root,"backups");Directory.CreateDirectory(folder);
        var target=Path.Combine(folder,"library-before-"+version+".db");if(File.Exists(target))return;
        var temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {using(var backup=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=temporary,Pooling=false}.ToString())){backup.Open();source.BackupDatabase(backup);}File.Move(temporary,target,false);}
        finally {if(File.Exists(temporary))File.Delete(temporary);}
    }

    public void Save(Course course)
    {
        lock(gate) { using var c=Open(); using var q=c.CreateCommand(); q.CommandText="INSERT INTO courses SELECT $id,$date,$doc WHERE NOT EXISTS(SELECT 1 FROM deleted_courses WHERE id=$id) ON CONFLICT(id) DO UPDATE SET document=$doc"; q.Parameters.AddWithValue("$id",course.Id); q.Parameters.AddWithValue("$date",course.Created.ToString("O")); q.Parameters.AddWithValue("$doc",JsonSerializer.Serialize(course)); q.ExecuteNonQuery(); }
    }
    public List<Course> All()
    {
        lock(gate) { using var c=Open(); using var q=c.CreateCommand(); q.CommandText="SELECT document FROM courses ORDER BY created DESC"; using var r=q.ExecuteReader(); var list=new List<Course>(); while(r.Read()) { var course=JsonSerializer.Deserialize<Course>(r.GetString(0)); if(course!=null) list.Add(course); } return list; }
    }
    public void Delete(Course course)
    {
        lock(gate) { using var c=Open(); using var transaction=c.BeginTransaction(); using var q=c.CreateCommand(); q.Transaction=transaction; q.CommandText="INSERT OR IGNORE INTO deleted_courses VALUES($id); DELETE FROM courses WHERE id=$id"; q.Parameters.AddWithValue("$id",course.Id); q.ExecuteNonQuery(); transaction.Commit(); }
        var root=Path.GetFullPath(Path.Combine(Paths.Root,"recordings"))+Path.DirectorySeparatorChar;
        var folder=Path.GetFullPath(course.Folder);
        if(folder.StartsWith(root,StringComparison.OrdinalIgnoreCase) && Directory.Exists(folder)) { try {Directory.Delete(folder,true);} catch(IOException) {} catch(UnauthorizedAccessException) {} }
    }
}
