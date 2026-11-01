using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace LingNote;

public static class LibraryLocation
{
    public static void Copy(string source,string destination)
    {
        source=Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        destination=Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        if(destination.Equals(source,StringComparison.OrdinalIgnoreCase))return;
        if(destination.StartsWith(source+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||source.StartsWith(destination+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new IOException("请选择与当前数据目录互不包含的文件夹。");
        Directory.CreateDirectory(destination);
        if(Directory.EnumerateFileSystemEntries(destination).Any())throw new IOException("请选择空文件夹，避免覆盖已有文件或录音库。");
        CopyFiles(source,destination);
        using var original=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=Path.Combine(source,"library.db"),Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());original.Open();
        using var target=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=Path.Combine(destination,"library.db"),Pooling=false}.ToString());target.Open();original.BackupDatabase(target);
        using var transaction=target.BeginTransaction();
        var documents=new List<(string Id,Course Course)>();
        using(var query=target.CreateCommand()){query.Transaction=transaction;query.CommandText="SELECT id,document FROM courses";using var reader=query.ExecuteReader();while(reader.Read())documents.Add((reader.GetString(0),JsonSerializer.Deserialize<Course>(reader.GetString(1))!));}
        foreach(var (id,course) in documents){foreach(var part in course.Parts)if(Path.IsPathFullyQualified(part.File)&&Path.GetFullPath(part.File).StartsWith(source+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))part.File=Path.Combine(destination,Path.GetRelativePath(source,part.File));using var update=target.CreateCommand();update.Transaction=transaction;update.CommandText="UPDATE courses SET document=$doc WHERE id=$id";update.Parameters.AddWithValue("$doc",JsonSerializer.Serialize(course));update.Parameters.AddWithValue("$id",id);update.ExecuteNonQuery();}
        transaction.Commit();
        using var integrity=target.CreateCommand();integrity.CommandText="PRAGMA integrity_check";if((string?)integrity.ExecuteScalar()!="ok")throw new IOException("新录音库验证失败，原目录仍然保留。");
    }
    private static void CopyFiles(string source,string destination)
    {
        foreach(var file in Directory.EnumerateFiles(source)){var name=Path.GetFileName(file);if(name is "library.db" or "library.db-wal" or "library.db-shm")continue;File.Copy(file,Path.Combine(destination,name),false);}
        foreach(var folder in Directory.EnumerateDirectories(source)){if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)throw new IOException("数据目录包含链接文件夹，请先移除链接再更改位置。");var next=Path.Combine(destination,Path.GetFileName(folder));Directory.CreateDirectory(next);CopyFiles(folder,next);}
    }
}
