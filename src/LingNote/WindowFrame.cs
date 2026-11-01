using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace LingNote;
internal static class WindowFrame
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo { public Point Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public Rect Monitor,Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    private static IntPtr WorkAreaHook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message!=0x0024)return IntPtr.Zero;
        var monitor=MonitorFromWindow(hwnd,2);var info=new MonitorInfo {Size=Marshal.SizeOf<MonitorInfo>()};
        if(monitor==IntPtr.Zero||!GetMonitorInfo(monitor,ref info))return IntPtr.Zero;
        var bounds=Marshal.PtrToStructure<MinMaxInfo>(lParam);
        bounds.MaxPosition.X=info.Work.Left-info.Monitor.Left;bounds.MaxPosition.Y=info.Work.Top-info.Monitor.Top;
        bounds.MaxSize.X=info.Work.Right-info.Work.Left;bounds.MaxSize.Y=info.Work.Bottom-info.Work.Top;
        Marshal.StructureToPtr(bounds,lParam,false);handled=true;return IntPtr.Zero;
    }
    public static void Attach(Window window)
    {
        void Apply()
        {
            var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;
            int policy=2;DwmSetWindowAttribute(handle,2,ref policy,sizeof(int)); // enable DWM non-client rendering / shadow
            int corners=2;DwmSetWindowAttribute(handle,33,ref corners,sizeof(int)); // Windows 11 rounded corners
            int color=window.IsActive?0x00BDBDBD:0x00D2D2D2;DwmSetWindowAttribute(handle,34,ref color,sizeof(int));
        }
        window.SourceInitialized+=(_,_)=>{Apply();HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)?.AddHook(WorkAreaHook);};window.Activated+=(_,_)=>Apply();window.Deactivated+=(_,_)=>Apply();
    }
}
