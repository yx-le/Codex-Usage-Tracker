using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace CodexUsageTracker.App;

internal static class GlassWindow
{
    public static void Enable(Window window, Func<bool> isDark)
    {
        window.SourceInitialized += (_, _) => Apply(window, isDark());
    }

    public static void Apply(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        if (HwndSource.FromHwnd(handle) is { CompositionTarget: { } target }) target.BackgroundColor = Colors.Transparent;

        var enabled = dark ? 1 : 0;
        DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
        var corner = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(handle, 33, ref corner, sizeof(int));

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            var backdrop = 3; // DWMSBT_TRANSIENTWINDOW: acrylic-style system backdrop
            DwmSetWindowAttribute(handle, 38, ref backdrop, sizeof(int));
            var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            DwmExtendFrameIntoClientArea(handle, ref margins);
        }
        else
        {
            var accent = new AccentPolicy
            {
                AccentState = 4, // ACCENT_ENABLE_ACRYLICBLURBEHIND
                GradientColor = dark ? unchecked((int)0xCC201810) : unchecked((int)0xCCF8F2EE)
            };
            var size = Marshal.SizeOf<AccentPolicy>();
            var pointer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, pointer, false);
                var data = new WindowCompositionAttributeData { Attribute = 19, Data = pointer, SizeOfData = size };
                SetWindowCompositionAttribute(handle, ref data);
            }
            finally { Marshal.FreeHGlobal(pointer); }
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left, Right, Top, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct AccentPolicy { public int AccentState, AccentFlags, GradientColor, AnimationId; }
    [StructLayout(LayoutKind.Sequential)] private struct WindowCompositionAttributeData { public int Attribute; public IntPtr Data; public int SizeOfData; }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
}
