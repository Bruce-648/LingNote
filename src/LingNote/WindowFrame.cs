using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace LingNote;
internal static class WindowFrame
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    public static void Attach(Window window)
    {
        void Apply()
        {
            var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;
            int policy=2;DwmSetWindowAttribute(handle,2,ref policy,sizeof(int)); // enable DWM non-client rendering / shadow
            int corners=2;DwmSetWindowAttribute(handle,33,ref corners,sizeof(int)); // Windows 11 rounded corners
            int color=window.IsActive?0x00BDBDBD:0x00D2D2D2;DwmSetWindowAttribute(handle,34,ref color,sizeof(int));
        }
        window.SourceInitialized+=(_,_)=>Apply();window.Activated+=(_,_)=>Apply();window.Deactivated+=(_,_)=>Apply();
    }
}
