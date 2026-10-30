using System.Runtime.InteropServices;
namespace LingNote;
public sealed class ActivationSignal : IDisposable
{
    private readonly EventWaitHandle signal;
    private RegisteredWaitHandle? registration;
    public ActivationSignal(string name) => signal=new EventWaitHandle(false,EventResetMode.AutoReset,name);
    public void Request() => signal.Set();
    public void Listen(Action activate) => registration=ThreadPool.RegisterWaitForSingleObject(signal,(_,timedOut)=>{if(!timedOut)activate();},null,Timeout.Infinite,false);
    public void Dispose() {registration?.Unregister(null);signal.Dispose();}
}
internal static class WindowActivation
{
    [DllImport("user32.dll")] internal static extern bool AllowSetForegroundWindow(uint processId);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
}
