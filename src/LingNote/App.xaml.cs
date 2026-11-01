using System.Windows;
using System.Windows.Threading;
using System.IO;

namespace LingNote;
public partial class App : System.Windows.Application
{
    private System.Threading.Mutex? singleton;
    private ActivationSignal? activation;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args.Contains("--enhance-worker")){_ = EnhancementWorker.Serve().ContinueWith(_=>Dispatcher.BeginInvoke(()=>Shutdown()));return;}
        activation=new ActivationSignal("Local\\LingNote.Activate");
        singleton=new System.Threading.Mutex(true,"Local\\LingNote.Desktop",out var first);
        if(!first) {
            WindowActivation.AllowSetForegroundWindow(uint.MaxValue);
            activation.Request(); Shutdown(); return;
        }
        DispatcherUnhandledException+=(_,args)=> { System.Windows.MessageBox.Show(args.Exception.Message,"LingNote",MessageBoxButton.OK,MessageBoxImage.Warning); args.Handled=true; };
        try { Paths.Init(); var window=new MainWindow(); MainWindow=window; window.Show();
            activation.Listen(()=>Dispatcher.BeginInvoke(()=>{if(!Dispatcher.HasShutdownStarted)window.Restore();})); } catch(Exception ex) { System.Windows.MessageBox.Show("启动失败："+ex.Message); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e) { activation?.Dispose();singleton?.Dispose(); base.OnExit(e); }
}
