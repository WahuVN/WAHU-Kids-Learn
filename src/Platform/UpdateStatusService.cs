using System;
using System.Globalization;
using System.IO;

namespace WAHU.Platform
{
    public sealed class UpdateRuntimeStatus
    {
        public string CurrentVersion { get; set; }
        public string StagedVersion { get; set; }
        public DateTime? LastCheckUtc { get; set; }
        public string LastResult { get; set; }
        public string LastDetail { get; set; }
        public bool StartupEnabled { get; set; }
        public bool PortableMode { get; set; }
    }

    public static class UpdateStatusService
    {
        public static UpdateRuntimeStatus Read(RuntimeConfigBundle config, string appExePath)
        {
            if (config == null) throw new ArgumentNullException("config");
            var status = new UpdateRuntimeStatus
            {
                CurrentVersion = config.AppVersion,
                PortableMode = config.PortableMode,
                StartupEnabled = !config.PortableMode && StartupRegistrationService.IsEnabled(appExePath)
            };

            try
            {
                var lastCheck = Path.Combine(config.UpdatesDirectory, "last-check-utc.txt");
                if (File.Exists(lastCheck))
                {
                    DateTime parsed;
                    if (DateTime.TryParse(File.ReadAllText(lastCheck).Trim(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed))
                        status.LastCheckUtc = parsed.ToUniversalTime();
                }

                var lastResult = Path.Combine(config.UpdatesDirectory, "last-result.txt");
                if (File.Exists(lastResult))
                {
                    foreach (var line in File.ReadAllLines(lastResult))
                    {
                        if (line.StartsWith("result=", StringComparison.Ordinal)) status.LastResult = line.Substring(7);
                        else if (line.StartsWith("detail=", StringComparison.Ordinal)) status.LastDetail = line.Substring(7);
                    }
                }

                status.StagedVersion = UpdateStagingService.PeekStagedVersion(config, config.AppVersion);
            }
            catch { }
            return status;
        }

        public static void WriteResult(RuntimeConfigBundle config, string result, string detail)
        {
            if (config == null) return;
            try
            {
                Directory.CreateDirectory(config.UpdatesDirectory);
                var path = Path.Combine(config.UpdatesDirectory, "last-result.txt");
                var temp = path + ".tmp." + Guid.NewGuid().ToString("N");
                File.WriteAllLines(temp, new[]
                {
                    "captured_at_utc=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    "result=" + (result ?? string.Empty),
                    "detail=" + (detail ?? string.Empty).Replace("\r", " ").Replace("\n", " ")
                });
                if (File.Exists(path))
                {
                    try { File.Replace(temp, path, null); return; }
                    catch { File.Delete(path); }
                }
                File.Move(temp, path);
            }
            catch { }
        }
    }
}
