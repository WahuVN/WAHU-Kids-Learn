using System;
using System.Diagnostics;
using System.IO;

namespace WAHU.Platform
{
    public static class UpdateLaunchService
    {
        public static bool TryLaunch(RuntimeConfigBundle config, StagedUpdate staged, string applicationBase, string appExePath, out string error)
        {
            error = null;
            try
            {
                if (config == null || staged == null) throw new ArgumentNullException();
                if (config.PortableMode) throw new InvalidOperationException("Portable mode does not auto-install binary updates.");
                var sourceHelper = Path.Combine(applicationBase, "WAHU.Updater.exe");
                if (!File.Exists(sourceHelper)) throw new FileNotFoundException("Updater helper missing.", sourceHelper);
                var helperDir = Path.Combine(config.UpdatesDirectory, "helper-run", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(helperDir);
                var helper = Path.Combine(helperDir, "WAHU.Updater.exe");
                File.Copy(sourceHelper, helper, false);
                var args = string.Join(" ", new[]
                {
                    "--wait-pid", Process.GetCurrentProcess().Id.ToString(),
                    "--installer", Quote(staged.InstallerPath),
                    "--sha256", staged.InstallerSha256,
                    "--app", Quote(appExePath),
                    "--staged-state", Quote(Path.Combine(config.UpdatesDirectory, "staged-update.json")),
                    "--log", Quote(Path.Combine(config.UpdatesDirectory, "update-install.log")),
                    "--production", (config.UpdateChannel == "stable" ? "true" : "false"),
                    "--startup-enabled", (StartupRegistrationService.IsEnabled(appExePath) ? "true" : "false")
                });
                Process.Start(new ProcessStartInfo { FileName = helper, Arguments = args, WorkingDirectory = helperDir, UseShellExecute = false, CreateNoWindow = true });
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
