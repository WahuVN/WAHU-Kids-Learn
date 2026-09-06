using System;
using System.IO;
using System.Reflection;
using WAHU.Platform;

namespace WAHU.SetupPreflight
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string output = null, signatureTarget = null;
            bool production = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--out" && i + 1 < args.Length) output = args[++i];
                else if (args[i] == "--signature-target" && i + 1 < args.Length) signatureTarget = args[++i];
                else if (args[i] == "--production") production = true;
                else if (args[i] == "--help" || args[i] == "-h") { PrintHelp(); return 0; }
                else { Console.Error.WriteLine("Tham số không hợp lệ: " + args[i]); return 2; }
            }

            if (string.IsNullOrWhiteSpace(signatureTarget)) signatureTarget = Assembly.GetEntryAssembly().Location;
            if (string.IsNullOrWhiteSpace(output))
            {
                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WAHU Kids Learn", "diagnostics");
                output = Path.Combine(root, "preflight.json");
            }

            try
            {
                var report = PreflightProbe.Collect(signatureTarget);
                JsonReportWriter.Write(report, output);
                Console.WriteLine("PREFLIGHT_REPORT=" + Path.GetFullPath(output));
                Console.WriteLine("COMPATIBILITY=" + report.CompatibilityLevel);
                Console.WriteLine("OS=" + report.OsVersion + " " + report.ServicePack);
                Console.WriteLine("NET_RELEASE=" + report.NetFrameworkRelease);
                Console.WriteLine("SHA2=" + report.LegacySha2Readiness);
                Console.WriteLine("SIGNATURE=" + report.SignatureNote);

                if (!report.OsSupported) return 10;
                if (!report.Net48OrLater) return 11;
                if (production && report.PublisherChainTrusted != true) return 12;
                if (production && report.SignatureDigestValid != true) return 13;
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
                return 20;
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("WAHU.SetupPreflight --out <json> [--signature-target <exe>] [--production]");
            Console.WriteLine("--production chỉ PASS khi Authenticode digest + cached publisher chain đều trusted.");
        }
    }
}
