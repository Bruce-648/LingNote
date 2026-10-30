using System.Diagnostics;
using System.IO;
using System.Windows;

namespace LingNote;
public partial class MainWindow
{
    private async Task CheckUpdatesAtStartup()
    {
        if(System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name!="LingNote")return;
        try {
            await Task.Delay(4000);var preferences=UpdatePreferences.Load();
            if(!preferences.AutoCheck||DateTimeOffset.UtcNow-preferences.LastCheck<TimeSpan.FromHours(20)||exiting)return;
            using var service=new UpdateService();var release=await service.Latest(CancellationToken.None);
            preferences.LastCheck=DateTimeOffset.UtcNow;preferences.Save();
            if(release!=null&&release.Version>UpdateService.CurrentVersion&&!busy&&recording==null&&!exiting)Hint.Text=$"发现新版本 {release.Version.ToString(3)}，可到「设置 → 应用更新」下载。";
        }catch { /* Automatic checks remain quiet when offline. Manual checks explain errors. */ }
    }
    private async Task InstallUpdate(string installer,string hash)
    {
        if(recording!=null||busy||exiting){Hint.Text="请先结束录音或整理任务，再安装更新。";return;}
        try {
            await UpdateService.Verify(installer,hash,CancellationToken.None);
            if(recording!=null||busy||exiting)return;SaveEditor();
            var start=new ProcessStartInfo(installer){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(installer)!};
            foreach(var argument in new[]{"/SP-","/SILENT","/NORESTART","/NOCLOSEAPPLICATIONS","/NOFORCECLOSEAPPLICATIONS","/LINGNOTEUPDATE=1","/DIR="+AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)})start.ArgumentList.Add(argument);
            if(Process.Start(start)==null)throw new IOException("无法启动更新安装程序。");
            Exit();
        }catch(Exception ex){Hint.Text="安装未开始："+ex.Message;}
    }
}
