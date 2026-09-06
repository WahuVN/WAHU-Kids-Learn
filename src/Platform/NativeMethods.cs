using System;
using System.Runtime.InteropServices;

namespace WAHU.Platform
{
    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct RTL_OSVERSIONINFOEX
        {
            internal uint dwOSVersionInfoSize;
            internal uint dwMajorVersion;
            internal uint dwMinorVersion;
            internal uint dwBuildNumber;
            internal uint dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string szCSDVersion;
            internal ushort wServicePackMajor;
            internal ushort wServicePackMinor;
            internal ushort wSuiteMask;
            internal byte wProductType;
            internal byte wReserved;
        }

        [DllImport("ntdll.dll", CharSet = CharSet.Unicode)]
        internal static extern int RtlGetVersion(ref RTL_OSVERSIONINFOEX versionInfo);

        [DllImport("winmm.dll")]
        internal static extern uint waveOutGetNumDevs();

        [DllImport("winmm.dll")]
        internal static extern uint waveInGetNumDevs();
    }
}
