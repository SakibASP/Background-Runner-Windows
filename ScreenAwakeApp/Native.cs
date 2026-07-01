using System.Runtime.InteropServices;

namespace ScreenAwake;

/// <summary>
/// Thin wrapper over the Win32 calls that keep the machine awake and let us
/// tell our own injected input apart from the real user's input.
/// </summary>
internal static partial class Native
{
    // --- Keep the display + system awake -----------------------------------
    [Flags]
    public enum ExecutionState : uint
    {
        Continuous = 0x80000000,
        SystemRequired = 0x00000001,
        DisplayRequired = 0x00000002,
    }

    [LibraryImport("kernel32.dll")]
    public static partial uint SetThreadExecutionState(ExecutionState esFlags);

    [LibraryImport("kernel32.dll")]
    public static partial uint GetTickCount();

    public static void KeepAwake(bool on)
    {
        SetThreadExecutionState(on
            ? ExecutionState.Continuous | ExecutionState.SystemRequired | ExecutionState.DisplayRequired
            : ExecutionState.Continuous);
    }

    // --- Idle timer (the same one Teams uses to decide you're 'Away') ------
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public static uint LastInputTick()
    {
        var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        GetLastInputInfo(ref lii);
        return lii.dwTime;
    }

    // --- Cursor position ----------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT p);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCursorPos(int x, int y);

    // --- SendInput: a GENUINE input event that resets the idle timer -------
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    private const uint INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_MOVE = 0x0001;

    [LibraryImport("user32.dll")]
    private static partial uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);

    /// <summary>
    /// Fire a 1px move-and-return real input event. Unlike SetCursorPos this
    /// counts as genuine activity, so it keeps Teams/Slack showing 'Available'.
    /// The cursor is restored to its exact spot so it's barely perceptible.
    /// </summary>
    public static void WakePulse()
    {
        GetCursorPos(out POINT before);
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT { dx = 1, dy = 0, dwFlags = MOUSEEVENTF_MOVE },
        };
        SendInput(1, ref input, Marshal.SizeOf<INPUT>());
        SetCursorPos(before.X, before.Y);
    }
}
