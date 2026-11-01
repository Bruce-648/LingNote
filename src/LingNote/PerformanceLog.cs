using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace LingNote;
// Only timings and counts are recorded; never include note text, titles or credentials.
internal static class PerformanceLog
{
    private static readonly object Gate=new();
    internal static void Record(string stage, double milliseconds, int? count=null, double? firstTokenMilliseconds=null)
    {
        try {
            lock(Gate) {
                var folder=Path.Combine(Paths.Root,"diagnostics");Directory.CreateDirectory(folder);
                var line=JsonSerializer.Serialize(new {time=DateTimeOffset.Now,stage,milliseconds=Math.Round(milliseconds),count,firstTokenMilliseconds=firstTokenMilliseconds is null?(double?)null:Math.Round(firstTokenMilliseconds.Value)});
                File.AppendAllText(Path.Combine(folder,$"performance-{DateTime.Now:yyyyMMdd}-{Environment.ProcessId}.jsonl"),line+Environment.NewLine);
            }
        } catch(IOException) {} catch(UnauthorizedAccessException) {}
    }
}
