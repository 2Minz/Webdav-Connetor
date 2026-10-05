using System.Diagnostics;
using System.Runtime.InteropServices;

internal static class NativeResources
{
    public static (uint Gdi, uint User) Count()
    {
        using var process = Process.GetCurrentProcess();
        return (GetGuiResources(process.Handle, 0), GetGuiResources(process.Handle, 1));
    }
    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr process, uint flags);
}
