using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LabDisplaySwitch {
    internal static class Native {
        [StructLayout(LayoutKind.Sequential)] public struct Luid { public uint Low; public int High; public override string ToString() { return High + ":" + Low; } }
        [StructLayout(LayoutKind.Sequential)] public struct Rational { public uint Numerator, Denominator; }
        [StructLayout(LayoutKind.Sequential)] public struct Source { public Luid Adapter; public uint Id, ModeIndex, Status; }
        [StructLayout(LayoutKind.Sequential)] public struct Target { public Luid Adapter; public uint Id, ModeIndex, Technology, Rotation, Scaling; public Rational Refresh; public uint Scanline, Available, Status; }
        [StructLayout(LayoutKind.Sequential)] public struct DisplayPath { public Source Source; public Target Target; public uint Flags; }
        [StructLayout(LayoutKind.Explicit, Size=64)] public struct Mode {
            [FieldOffset(0)] public uint Type; [FieldOffset(4)] public uint Id; [FieldOffset(8)] public Luid Adapter;
            [FieldOffset(16)] public ulong A; [FieldOffset(24)] public ulong B; [FieldOffset(32)] public ulong C;
            [FieldOffset(40)] public ulong D; [FieldOffset(48)] public ulong E; [FieldOffset(56)] public ulong F;
            [FieldOffset(16)] public uint Width; [FieldOffset(20)] public uint Height; [FieldOffset(24)] public uint PixelFormat;
            [FieldOffset(28)] public int X; [FieldOffset(32)] public int Y;
        }
        [StructLayout(LayoutKind.Sequential)] public struct Header { public uint Type, Size; public Luid Adapter; public uint Id; }
        [StructLayout(LayoutKind.Sequential)] public struct DpiGet { public Header Header; public int Minimum, Current, Maximum; }
        [StructLayout(LayoutKind.Sequential)] public struct DpiSet { public Header Header; public int Relative; }
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct TargetName {
            public Header Header; public uint Flags, Technology; public ushort Manufacturer, Product; public uint Connector;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=64)] public string FriendlyName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string DevicePath;
        }
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct SourceName {
            public Header Header; [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string Name;
        }
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct AdapterName {
            public Header Header; [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string DevicePath;
        }
        [StructLayout(LayoutKind.Sequential)] public struct Rect {
            public int Left, Top, Right, Bottom;
            public int Width { get { return Right - Left; } } public int Height { get { return Bottom - Top; } }
        }
        [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct Placement { public int Length; public uint Flags, Show; public Point Min, Max; public Rect Normal; }
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct MonitorInfo {
            public int Size; public Rect Bounds, Work; public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string Device;
        }
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct DisplayDevice {
            public int Size; [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string Description; public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string Id;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string Key;
        }
        public delegate bool MonitorCallback(IntPtr monitor, IntPtr hdc, ref Rect rect, IntPtr data);
        public delegate bool WindowCallback(IntPtr hwnd, IntPtr data);
        [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] public static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
        [DllImport("user32.dll")] public static extern int QueryDisplayConfig(uint flags, ref uint paths, [Out] DisplayPath[] pathData, ref uint modes, [Out] Mode[] modeData, IntPtr topology);
        [DllImport("user32.dll")] public static extern int SetDisplayConfig(uint paths, [In] DisplayPath[] pathData, uint modes, [In] Mode[] modeData, uint flags);
        [DllImport("user32.dll", EntryPoint="DisplayConfigGetDeviceInfo")] public static extern int GetTargetName(ref TargetName name);
        [DllImport("user32.dll", EntryPoint="DisplayConfigGetDeviceInfo")] public static extern int GetSourceName(ref SourceName name);
        [DllImport("user32.dll", EntryPoint="DisplayConfigGetDeviceInfo")] public static extern int GetAdapterName(ref AdapterName name);
        [DllImport("user32.dll", EntryPoint="DisplayConfigGetDeviceInfo")] public static extern int GetDpiScale(ref DpiGet packet);
        [DllImport("user32.dll", EntryPoint="DisplayConfigSetDeviceInfo")] public static extern int SetDpiScale(ref DpiSet packet);
        [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr rect, MonitorCallback callback, IntPtr data);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice data, uint flags);
        [DllImport("shcore.dll")] public static extern int GetScaleFactorForMonitor(IntPtr monitor, out int scale);
        [DllImport("user32.dll")] public static extern bool EnumWindows(WindowCallback callback, IntPtr data);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, uint command);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr hwnd, ref Placement placement);
        [DllImport("user32.dll")] public static extern bool SetWindowPlacement(IntPtr hwnd, ref Placement placement);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder text, int capacity);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, uint attribute, out int value, int size);
        [DllImport("user32.dll",SetLastError=true)] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] public static extern bool LockWorkStation();
        [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
        [DllImport("kernel32.dll")] public static extern ulong GetTickCount64();
    }
}
