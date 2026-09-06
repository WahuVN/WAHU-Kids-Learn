using System;
using System.IO;
using WAHU.Data;
using WAHU.Platform;

namespace WAHUKidsLearn
{
    internal static class UpdateApplyCoordinator
    {
        public static bool TryLaunch(RuntimeConfigBundle config, LearningDatabase learningDatabase,
            string applicationBase, string appExePath, out StagedUpdate staged, out string error)
        {
            staged = null;
            error = null;
            if (config == null || learningDatabase == null)
            {
                error = "runtime_not_ready";
                return false;
            }
            if (config.PortableMode)
            {
                error = "portable_manual_only";
                return false;
            }
            if (!UpdateStagingService.TryGetStagedUpdate(config, config.AppVersion, out staged))
            {
                error = "no_verified_staged_update";
                return false;
            }

            try
            {
                var backupDir = Path.Combine(config.BackupsDirectory, "pre_update");
                var backup = learningDatabase.CreateManualBackup(backupDir, config.AppVersion);
                if (backup == null || backup.Health == null || !backup.Health.IsHealthy)
                    throw new InvalidDataException("Pre-update backup verification failed.");

                string launchError;
                if (!UpdateLaunchService.TryLaunch(config, staged, applicationBase, appExePath, out launchError))
                    throw new InvalidOperationException("Updater helper launch failed: " + launchError);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }
    }
}
