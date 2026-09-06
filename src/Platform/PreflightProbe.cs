using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WAHU.Platform
{
    public static class PreflightProbe
    {
        public static PreflightReport Collect(string signatureTarget)
        {
            var report = new PreflightReport { CapturedAtUtc = DateTime.UtcNow.ToString("o") };
            Version version; string servicePack;
            ReadRealOsVersion(out version, out servicePack);
            report.OsVersion = version.ToString();
            report.ServicePack = servicePack ?? string.Empty;
            report.IsTargetWindows7 = CompatibilityClassifier.IsWindows7(version);
            report.OsSupported = CompatibilityClassifier.IsWindows7Sp1OrLater(version, servicePack);
            report.ProcessArch = IntPtr.Size == 4 ? "x86" : "x64";
            report.OsArch = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            report.LogicalCores = Environment.ProcessorCount;
            report.RamTotalMb = TryGetRamMb();
            report.FreeDiskMb = TryGetFreeDiskMb();
            report.SystemDpi = TryGetSystemDpi();
            report.HighContrastEnabled = SystemInformation.HighContrast;
            report.StopwatchHighResolution = Stopwatch.IsHighResolution;
            report.NetFrameworkRelease = ReadNetFrameworkRelease();
            report.Net48OrLater = CompatibilityClassifier.IsNet48OrLater(report.NetFrameworkRelease);
            report.LegacySha2Readiness = ProbeLegacySha2(version, report.Sha2Evidence, report.Warnings);
            report.AudioOutputAvailable = SafeWaveOutCount() > 0;
            report.MicrophoneAvailable = SafeWaveInCount() > 0;

            if (!string.IsNullOrWhiteSpace(signatureTarget))
            {
                report.SignatureTarget = signatureTarget;
                var sig = SignatureVerifier.CheckCacheOnly(signatureTarget);
                report.SignaturePresent = sig.SignaturePresent;
                report.SignatureDigestValid = sig.SignatureDigestValid;
                report.PublisherChainTrusted = sig.PublisherChainTrusted;
                report.SignatureValidationElapsedMs = sig.ElapsedMs;
                report.WinVerifyTrustCode = sig.WinVerifyTrustCode;
                report.SignatureNote = sig.Note;
            }

            if (!report.OsSupported) report.Warnings.Add("OS_NOT_SUPPORTED_BY_V1_POLICY");
            if (!report.Net48OrLater) report.Warnings.Add("NET48_RELEASE_KEY_BELOW_528040");
            if (report.IsTargetWindows7 && report.LegacySha2Readiness != "PASS") report.Warnings.Add("WIN7_SHA2_READINESS_NOT_CONFIRMED");
            report.CompatibilityLevel = CompatibilityClassifier.Classify(version, servicePack, report.NetFrameworkRelease, report.LegacySha2Readiness);
            return report;
        }

        private static void ReadRealOsVersion(out Version version, out string servicePack)
        {
            var info = new NativeMethods.RTL_OSVERSIONINFOEX();
            info.dwOSVersionInfoSize = (uint)Marshal.SizeOf(typeof(NativeMethods.RTL_OSVERSIONINFOEX));
            if (NativeMethods.RtlGetVersion(ref info) == 0)
            {
                version = new Version((int)info.dwMajorVersion, (int)info.dwMinorVersion, (int)info.dwBuildNumber);
                servicePack = info.szCSDVersion;
                if (string.IsNullOrEmpty(servicePack) && info.wServicePackMajor > 0) servicePack = "Service Pack " + info.wServicePackMajor;
                return;
            }
            version = Environment.OSVersion.Version;
            servicePack = Environment.OSVersion.ServicePack;
        }

        private static int ReadNetFrameworkRelease()
        {
            int max = Math.Max(ReadRelease(RegistryView.Registry32), 0);
            if (Environment.Is64BitOperatingSystem) max = Math.Max(max, ReadRelease(RegistryView.Registry64));
            return max;
        }

        private static int ReadRelease(RegistryView view)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                {
                    var value = key == null ? null : key.GetValue("Release");
                    return value is int ? (int)value : 0;
                }
            }
            catch { return 0; }
        }

        private static string ProbeLegacySha2(Version version, List<string> evidence, List<string> warnings)
        {
            if (!CompatibilityClassifier.IsWindows7(version)) return "NOT_APPLICABLE";
            try
            {
                var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var searcher = new ManagementObjectSearcher("SELECT HotFixID FROM Win32_QuickFixEngineering"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject row in results)
                    {
                        var id = Convert.ToString(row["HotFixID"]);
                        if (string.Equals(id, "KB4490628", StringComparison.OrdinalIgnoreCase) || string.Equals(id, "KB4474419", StringComparison.OrdinalIgnoreCase)) found.Add(id);
                    }
                }
                foreach (var id in found) evidence.Add(id + ":INSTALLED_EVIDENCE");
                if (found.Contains("KB4490628") && found.Contains("KB4474419")) return "PASS";
                warnings.Add("EXACT_SHA2_KB_EVIDENCE_INCOMPLETE_SUPERSEDENCE_POSSIBLE");
                return "WARN";
            }
            catch (Exception ex)
            {
                warnings.Add("SHA2_EVIDENCE_PROBE_FAILED:" + ex.GetType().Name);
                return "UNKNOWN";
            }
        }

        private static long? TryGetRamMb()
        {
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                using (var r = s.Get())
                    foreach (ManagementObject o in r) return Convert.ToInt64(o["TotalPhysicalMemory"]) / (1024L * 1024L);
            }
            catch { }
            return null;
        }

        private static long? TryGetFreeDiskMb()
        {
            try
            {
                var root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                return new DriveInfo(root).AvailableFreeSpace / (1024L * 1024L);
            }
            catch { return null; }
        }

        private static float? TryGetSystemDpi()
        {
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX; }
            catch { return null; }
        }

        private static uint SafeWaveOutCount() { try { return NativeMethods.waveOutGetNumDevs(); } catch { return 0; } }
        private static uint SafeWaveInCount() { try { return NativeMethods.waveInGetNumDevs(); } catch { return 0; } }
    }
}
