using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace WAHU.Updater
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (Has(args, "--self-test")) return Sha256Text("abc") == "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD" ? 0 : 9;
            string log = Get(args, "--log");
            try
            {
                var waitPid = int.Parse(Get(args, "--wait-pid"));
                var installer = Path.GetFullPath(Get(args, "--installer"));
                var expectedSha = Get(args, "--sha256").ToUpperInvariant();
                var app = Path.GetFullPath(Get(args, "--app"));
                var state = Path.GetFullPath(Get(args, "--staged-state"));
                var production = string.Equals(Get(args, "--production"), "true", StringComparison.OrdinalIgnoreCase);
                Write(log, "start wait_pid=" + waitPid);
                WaitForProcessExit(waitPid, TimeSpan.FromMinutes(2));
                if (!File.Exists(installer)) throw new FileNotFoundException("Staged installer missing.", installer);
                var actual = Sha256File(installer);
                if (!string.Equals(actual, expectedSha, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Installer hash changed before install.");
                if (production && !VerifyAuthenticode(installer)) throw new InvalidDataException("Production Authenticode verification failed.");
                Write(log, "verified installer sha256=" + actual);
                var setup = Process.Start(new ProcessStartInfo
                {
                    FileName = installer,
                    Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (setup == null) throw new InvalidOperationException("Could not start installer.");
                setup.WaitForExit();
                if (setup.ExitCode != 0) throw new InvalidOperationException("Installer exit=" + setup.ExitCode);
                try { if (File.Exists(state)) File.Delete(state); } catch { }
                Write(log, "install_pass");
                if (File.Exists(app)) Process.Start(new ProcessStartInfo { FileName = app, Arguments = "--post-update", UseShellExecute = true });
                return 0;
            }
            catch (Exception ex)
            {
                Write(log, "FAIL " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static void WaitForProcessExit(int pid, TimeSpan timeout)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                if (!process.WaitForExit((int)timeout.TotalMilliseconds)) throw new TimeoutException("App did not exit before updater timeout.");
            }
            catch (ArgumentException) { }
        }

        private static string Sha256File(string path)
        {
            using (var sha = SHA256.Create()) using (var fs = File.OpenRead(path)) return Hex(sha.ComputeHash(fs));
        }
        private static string Sha256Text(string text)
        {
            using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text)));
        }
        private static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", string.Empty); }
        private static bool Has(string[] args, string name) { foreach (var a in args ?? new string[0]) if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true; return false; }
        private static string Get(string[] args, string name)
        {
            for (var i = 0; args != null && i + 1 < args.Length; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            throw new ArgumentException("Missing argument " + name);
        }
        private static void Write(string path, string text)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.AppendAllText(path, DateTime.UtcNow.ToString("o") + " " + text + Environment.NewLine); } catch { }
        }

        private static bool VerifyAuthenticode(string path)
        {
            var file = new WINTRUST_FILE_INFO { cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)), pcwszFilePath = path };
            IntPtr fp = IntPtr.Zero, dp = IntPtr.Zero;
            try
            {
                fp = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_FILE_INFO))); Marshal.StructureToPtr(file, fp, false);
                var data = new WINTRUST_DATA { cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_DATA)), dwUIChoice = 2, fdwRevocationChecks = 0, dwUnionChoice = 1, pFile = fp, dwStateAction = 0, dwProvFlags = 0x10 | 0x1000 };
                dp = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_DATA))); Marshal.StructureToPtr(data, dp, false);
                return WinVerifyTrust(IntPtr.Zero, Action, dp) == 0;
            }
            finally { if (dp != IntPtr.Zero) Marshal.FreeHGlobal(dp); if (fp != IntPtr.Zero) Marshal.FreeHGlobal(fp); }
        }

        private static readonly Guid Action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WINTRUST_FILE_INFO { public uint cbStruct; [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath; public IntPtr hFile; public IntPtr pgKnownSubject; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WINTRUST_DATA { public uint cbStruct; public IntPtr pPolicyCallbackData; public IntPtr pSIPClientData; public uint dwUIChoice; public uint fdwRevocationChecks; public uint dwUnionChoice; public IntPtr pFile; public uint dwStateAction; public IntPtr hWVTStateData; [MarshalAs(UnmanagedType.LPWStr)] public string pwszURLReference; public uint dwProvFlags; public uint dwUIContext; public IntPtr pSignatureSettings; }
        [DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true, CharSet = CharSet.Unicode)] private static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid action, IntPtr data);
    }
}
