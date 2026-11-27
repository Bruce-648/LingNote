using System.IO;
using System.Text.RegularExpressions;

namespace LingNote;

internal enum ExportNoteKind { Classroom, Enhanced }

internal sealed record ExportFolder(string Key,string Name);
internal sealed record ExportNote(string CourseId,string Title,ExportFolder[] Folders,ExportNoteKind Kind,string Markdown);
internal sealed record ExportFailure(string FileName,string Message);
internal sealed record NoteExportResult(int Written,IReadOnlyList<ExportFailure> Failures);

internal static class BulkNoteExport
{
    public static IReadOnlyList<ExportNote> Collect(IEnumerable<Course> courses,Preferences prefs)
    {
        var sections=prefs.Sections.ToDictionary(section=>section.Id,section=>section.Name);
        var notes=new List<ExportNote>();
        foreach(var course in courses)
        {
            var folders=new List<ExportFolder>();
            var sectionId=LibrarySections.SectionOf(prefs,course);
            if(sections.TryGetValue(sectionId,out var sectionName))
                folders.Add(new("section:"+sectionId,sectionName));
            if(!string.IsNullOrEmpty(course.Topic))
                folders.Add(new("topic:"+course.Topic,course.Topic));
            if(folders.Count==0)
                folders.Add(new("unassigned","未分区"));

            var path=folders.ToArray();
            if(!string.IsNullOrWhiteSpace(course.Note))
                notes.Add(new(course.Id,course.Title,path,ExportNoteKind.Classroom,course.Note));
            if(!string.IsNullOrWhiteSpace(course.EnhancedNote))
                notes.Add(new(course.Id,course.Title,path,ExportNoteKind.Enhanced,course.EnhancedNote));
        }
        return notes;
    }

    public static NoteExportResult Write(string destination,IReadOnlyList<ExportNote> notes)
    {
        var folderPaths=new Dictionary<(string Parent,string Key),string>();
        var claimedPaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failures=new List<ExportFailure>();
        int written=0;
        foreach(var note in notes)
        {
            var label=note.Kind==ExportNoteKind.Classroom?"课堂笔记":"精编笔记";
            var stem=SafeName(note.Title,"笔记")+"_"+label;
            var directory=destination;
            try
            {
                foreach(var folder in note.Folders)
                {
                    var key=(directory,folder.Key);
                    if(!folderPaths.TryGetValue(key,out var path))
                    {
                        var name=SafeName(folder.Name,"未命名");
                        path=Path.Combine(directory,name);
                        for(int sequence=2;!claimedPaths.Add(path);sequence++)
                            path=Path.Combine(directory,WithSequence(name,sequence));
                        folderPaths.Add(key,path);
                    }
                    directory=path;
                }
                Directory.CreateDirectory(directory);
                WriteNewFile(directory,stem,note.Markdown);
                written++;
            }
            catch(Exception error) when(error is IOException or UnauthorizedAccessException)
            {
                failures.Add(new(stem+".md",error.Message));
            }
        }
        return new(written,failures);
    }

    private static readonly char[] InvalidNameChars=Path.GetInvalidFileNameChars();
    private static readonly Regex DeviceName=new(@"^(CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|COM[1-9¹²³]|LPT[1-9¹²³])$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);

    private static string SafeName(string value,string fallback)
    {
        var name=new string(value.Select(character=>InvalidNameChars.Contains(character)?'_':character).ToArray()).Trim().TrimEnd('.',' ');
        if(name.Length>100)
            name=name[..(char.IsHighSurrogate(name[99])?99:100)].TrimEnd('.',' ');
        if(name.Length==0)return fallback;
        return DeviceName.IsMatch(name.Split('.',2)[0].TrimEnd())?"_"+name:name;
    }

    private static string WithSequence(string stem,int sequence)=>sequence==1?stem:$"{stem} ({sequence})";

    private static void WriteNewFile(string directory,string stem,string markdown)
    {
        var temporary=Path.Combine(directory,$".lingnote-{Guid.NewGuid():N}.tmp");
        var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        try
        {
            using(stream)
            using(var writer=new StreamWriter(stream))
                writer.Write(markdown);
            for(int sequence=1;;sequence++)
            {
                var target=Path.Combine(directory,WithSequence(stem,sequence)+".md");
                try
                {
                    File.Move(temporary,target);
                    return;
                }
                catch(IOException) when(File.Exists(target)||Directory.Exists(target))
                {
                    // Another export or an existing file owns this name.
                }
            }
        }
        finally
        {
            if(File.Exists(temporary))File.Delete(temporary);
        }
    }
}
