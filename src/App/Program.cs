using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using WAHU.Content;
using WAHU.Data;
using WAHU.Platform;
using WAHU.Performance;

namespace WAHUKidsLearn
{
    internal static class Program
    {
        private const string SingleInstanceName = "WAHU-Kids-Learn-87D1C739-A798-4CE4-9CB3-E22EC6E5DE95";

        [STAThread]
        private static int Main(string[] args)
        {
            var applicationBase = AppDomain.CurrentDomain.BaseDirectory;
            var portableMode = HasArg(args, "--portable") || File.Exists(Path.Combine(applicationBase, "portable.mode"));
            var instanceName = BuildSingleInstanceName(applicationBase, portableMode);
            using (var instance = SingleInstanceGuard.TryAcquire(instanceName))
            {
                if (!instance.IsPrimaryInstance)
                {
                    if (HasArg(args, "--bootstrap-smoke")) return 41;
                    MessageBox.Show("WAHU Kids Learn đang chạy.", "WAHU Kids Learn", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 41;
                }

                RuntimeConfigBundle config;
                try
                {
                    config = RuntimeConfigBundle.Load(
                        Path.Combine(applicationBase, "config"),
                        applicationBase,
                        portableMode);
                }
                catch (Exception ex)
                {
                    if (HasArg(args, "--bootstrap-smoke"))
                    {
                        WriteBootstrapFailure(GetArgValue(args, "--out"), "CONFIG_INVALID", ex);
                        return 42;
                    }
                    WriteStartupDiagnostic(applicationBase, portableMode, "CONFIG_INVALID", ex);
                    MessageBox.Show(
                        "Ứng dụng chưa thể mở vì một số tệp cài đặt bị thiếu hoặc đã thay đổi.\r\n\r\nDữ liệu học chưa bị xóa. Hãy nhờ người lớn cài lại hoặc kiểm tra WAHU Kids Learn.",
                        "WAHU Kids Learn",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return 42;
                }

                var markerPath = Path.Combine(config.RecoveryDirectory, "runtime.running");
                using (var runtimeMarker = RuntimeSessionMarker.Begin(markerPath))
                {
                    if (HasArg(args, "--bootstrap-smoke"))
                        return RunBootstrapSmoke(args, config, runtimeMarker.PreviousRunUnclean);

                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    PreflightReport preflight = null;
                    DatabaseBootstrapResult database = null;
                    RuntimePerformanceSettings performance = null;
                    RuntimeBootstrapIssue issue = null;
                    var learningDatabase = CreateLearningDatabase(config);
                    try
                    {
                        int verifiedContentPackCount;
                        BootstrapRuntime(config, learningDatabase, out preflight, out database, out verifiedContentPackCount, out performance);
                    }
                    catch (RuntimeBootstrapException ex)
                    {
                        issue = ex.ToIssue();
                        WriteRuntimeDiagnostic(config, issue);
                    }
                    catch (Exception ex)
                    {
                        issue = new RuntimeBootstrapIssue
                        {
                            Kind = RuntimeIssueKind.PlatformUnavailable,
                            ChildMessage = "Ứng dụng cần người lớn kiểm tra trước khi học.",
                            TechnicalMessage = ex.GetType().Name + ": " + ex.Message,
                            CanRecoverDatabase = false
                        };
                        WriteRuntimeDiagnostic(config, issue);
                    }

                    if (issue == null && database != null && database.Health != null && database.Health.IsHealthy &&
                        !portableMode && !runtimeMarker.PreviousRunUnclean && config.UpdateAutoInstallOnNextStart)
                    {
                        if (TryLaunchStagedUpdate(config, learningDatabase, applicationBase)) return 0;
                    }

                    if (issue == null && !portableMode && config.UpdateEnabled && config.UpdateCheckOnStartup)
                        QueueBackgroundUpdateCheck(config);

                    Application.Run(new MainForm(config, learningDatabase, preflight, database, issue, runtimeMarker.PreviousRunUnclean, performance));
                    return 0;
                }
            }
        }

        private static int RunBootstrapSmoke(string[] args, RuntimeConfigBundle config, bool previousRunUnclean)
        {
            string outPath = GetArgValue(args, "--out");
            if (string.IsNullOrWhiteSpace(outPath))
                outPath = Path.Combine(config.UserRoot, "diagnostics", "bootstrap-smoke.txt");

            try
            {
                PreflightReport preflight;
                DatabaseBootstrapResult database;
                int verifiedContentPackCount;
                RuntimePerformanceSettings performance;
                var learningDatabase = CreateLearningDatabase(config);
                BootstrapRuntime(config, learningDatabase, out preflight, out database, out verifiedContentPackCount, out performance);
                var parent = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
                File.WriteAllLines(outPath, new[]
                {
                    "result=PASS",
                    "previous_run_unclean=" + previousRunUnclean,
                    "storage_mode=" + config.StorageMode,
                    "config_files=" + config.FileSha256.Count,
                    "app_version=" + config.AppVersion,
                    "config_user_root=" + config.UserRoot,
                    "config_backups_path=" + config.BackupsDirectory,
                    "config_database_path=" + config.DatabasePath,
                    "config_journal=" + config.DatabaseJournalMode,
                    "update_enabled=" + config.UpdateEnabled,
                    "update_channel=" + config.UpdateChannel,
                    "update_manifest_url=" + config.UpdateManifestUrl,
                    "update_installed_mode_only=" + (!config.PortableMode),
                    "startup_default=" + config.LaunchWithWindowsDefault,
                    "config_low_fps_cap=" + config.LowMotionFpsCap,
                    "config_normal_fps_cap=" + config.NormalMotionFpsCap,
                    "compatibility=" + preflight.CompatibilityLevel,
                    "process_arch=" + preflight.ProcessArch,
                    "net_release=" + preflight.NetFrameworkRelease,
                    "db_path=" + database.DatabasePath,
                    "db_created=" + database.CreatedSchema,
                    "schema_version=" + database.SchemaVersion,
                    "provider_version=" + database.ProviderVersion,
                    "sqlite_version=" + database.SQLiteVersion,
                    "journal=" + database.JournalMode,
                    "migration_version=" + (database.Migration == null ? "?" : database.Migration.Version.ToString()),
                    "migration_sha256=" + (database.Migration == null ? "?" : database.Migration.ChecksumSha256),
                    "pre_migration_backup=" + (database.PreMigrationBackup == null ? "none" : database.PreMigrationBackup.MetadataPath),
                    "integrity=" + database.Health.Integrity,
                    "foreign_key_issues=" + database.Health.ForeignKeyIssues,
                    "verified_content_packs=" + verifiedContentPackCount,
                    "performance_profile=" + performance.Profile,
                    "performance_motion_fps_cap=" + performance.MotionFpsCap,
                    "performance_max_animated_regions=" + performance.MaxAnimatedRegions,
                    "performance_image_cache_mb=" + (performance.ImageCacheBytes / (1024L * 1024L)),
                    "performance_audio_cache_mb=" + (performance.AudioCacheBytes / (1024L * 1024L)),
                    "performance_decorative_outside_focus=" + performance.DecorativeMotionAllowedOutsideLearningFocus,
                    "performance_evidence=" + string.Join(",", performance.Evidence ?? new string[0]),
                    "parent_pin_configured=" + File.Exists(Path.Combine(config.UserRoot, "security", "parent_pin.json"))
                });
                return database.Health != null && database.Health.IsHealthy ? 0 : 31;
            }
            catch (Exception ex)
            {
                WriteBootstrapFailure(outPath, "BOOTSTRAP_FAILED", ex);
                return 30;
            }
        }

        private static void BootstrapRuntime(RuntimeConfigBundle config, LearningDatabase learningDatabase, out PreflightReport preflight, out DatabaseBootstrapResult database, out int verifiedContentPackCount, out RuntimePerformanceSettings performance)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (learningDatabase == null) throw new ArgumentNullException("learningDatabase");

            try
            {
                preflight = PreflightProbe.Collect(Application.ExecutablePath);
                JsonReportWriter.Write(preflight, Path.Combine(config.UserRoot, "diagnostics", "preflight.json"));
            }
            catch (Exception ex)
            {
                throw new RuntimeBootstrapException(RuntimeIssueKind.PlatformUnavailable,
                    "Ứng dụng cần người lớn kiểm tra máy trước khi học.", ex);
            }

            try
            {
                var autotuner = new PerformanceAutotuner();
                var performanceDecision = autotuner.Select(preflight);
                performance = autotuner.BuildRuntimeSettings(config, performanceDecision);
            }
            catch
            {
                performance = BuildLowPerformanceFallback(config);
            }

            try
            {
                verifiedContentPackCount = ValidateBundledContent();
            }
            catch (Exception ex)
            {
                throw new RuntimeBootstrapException(RuntimeIssueKind.ContentInvalid,
                    "Nội dung học chưa sẵn sàng. Hãy nhờ người lớn kiểm tra ứng dụng.", ex);
            }

            try
            {
                database = learningDatabase.Initialize(config.DatabaseJournalMode, config.BackupsDirectory, config.AppVersion);
            }
            catch (Exception ex)
            {
                throw new RuntimeBootstrapException(RuntimeIssueKind.DatabaseUnavailable,
                    "Dữ liệu học đang cần người lớn phục hồi hoặc kiểm tra.", ex);
            }
        }

        private static bool TryLaunchStagedUpdate(RuntimeConfigBundle config, LearningDatabase learningDatabase, string applicationBase)
        {
            StagedUpdate staged;
            if (!UpdateStagingService.TryGetStagedUpdate(config, config.AppVersion, out staged)) return false;
            try
            {
                var backupDir = Path.Combine(config.BackupsDirectory, "pre_update");
                var backup = learningDatabase.CreateManualBackup(backupDir, config.AppVersion);
                if (backup == null || backup.Health == null || !backup.Health.IsHealthy)
                    throw new InvalidDataException("Pre-update backup verification failed.");
                string error;
                if (!UpdateLaunchService.TryLaunch(config, staged, applicationBase, Application.ExecutablePath, out error))
                    throw new InvalidOperationException("Updater helper launch failed: " + error);
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    Directory.CreateDirectory(config.UpdatesDirectory);
                    File.WriteAllLines(Path.Combine(config.UpdatesDirectory, "update-launch-error.txt"), new[]
                    {
                        "captured_at_utc=" + DateTime.UtcNow.ToString("o"),
                        "error_type=" + ex.GetType().Name,
                        "error=" + ex.Message
                    });
                }
                catch { }
                return false;
            }
        }

        private static void QueueBackgroundUpdateCheck(RuntimeConfigBundle config)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string result;
                    new GitHubUpdateClient().TryCheckAndStage(config, config.AppVersion, out result);
                }
                catch { }
            });
        }

        private static LearningDatabase CreateLearningDatabase(RuntimeConfigBundle config)
        {
            var schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "schema", "001_initial.sql");
            return new LearningDatabase(config.DatabasePath, schemaPath);
        }

        private static RuntimePerformanceSettings BuildLowPerformanceFallback(RuntimeConfigBundle config)
        {
            return new RuntimePerformanceSettings
            {
                Profile = PerformanceProfileKind.LOW,
                MotionFpsCap = config.LowMotionFpsCap,
                MaxAnimatedRegions = config.LowMaxAnimatedRegions,
                ImageCacheBytes = (long)config.LowImageCacheMb * 1024L * 1024L,
                AudioCacheBytes = (long)config.LowAudioCacheMb * 1024L * 1024L,
                DecorativeMotionAllowedOutsideLearningFocus = false,
                Evidence = new string[] { "autotune_error_fallback_low" }
            };
        }

        private static int ValidateBundledContent()
        {
            var validator = new ContentPackValidator();
            var contentRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "content_packs");
            var required = new[] { "math_grade2_v1", "english_grade2_v1" };
            var verified = 0;
            foreach (var directory in required)
            {
                var result = validator.ValidateDirectory(Path.Combine(contentRoot, directory), true);
                if (!result.IsValid || !result.ChildRuntimeAllowed)
                    throw new InvalidDataException("Bundled content pack invalid: " + directory + " — " + string.Join(";", result.Errors ?? new string[0]));
                verified++;
            }
            return verified;
        }

        private static void WriteBootstrapFailure(string outPath, string code, Exception ex)
        {
            if (string.IsNullOrWhiteSpace(outPath)) return;
            try
            {
                var parent = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
                File.WriteAllLines(outPath, new[]
                {
                    "result=FAIL",
                    "code=" + code,
                    "error_type=" + ex.GetType().Name,
                    "error=" + ex.Message
                });
            }
            catch { }
        }

        private static void WriteStartupDiagnostic(string applicationBase, bool portableMode, string code, Exception ex)
        {
            try
            {
                var root = portableMode
                    ? Path.Combine(applicationBase, "UserData", "diagnostics")
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WAHU Kids Learn", "diagnostics");
                Directory.CreateDirectory(root);
                File.WriteAllLines(Path.Combine(root, "startup-error.txt"), new[]
                {
                    "captured_at_utc=" + DateTime.UtcNow.ToString("o"),
                    "code=" + code,
                    "application_base=" + applicationBase,
                    "error_type=" + ex.GetType().Name,
                    "error=" + ex.Message
                });
            }
            catch { }
        }

        private static void WriteRuntimeDiagnostic(RuntimeConfigBundle config, RuntimeBootstrapIssue issue)
        {
            if (config == null || issue == null) return;
            try
            {
                var directory = Path.Combine(config.UserRoot, "diagnostics");
                Directory.CreateDirectory(directory);
                File.WriteAllLines(Path.Combine(directory, "runtime-issue.txt"), new[]
                {
                    "captured_at_utc=" + DateTime.UtcNow.ToString("o"),
                    "kind=" + issue.Kind,
                    "can_recover_database=" + issue.CanRecoverDatabase,
                    "technical=" + (issue.TechnicalMessage ?? string.Empty)
                });
            }
            catch { }
        }

        private static string BuildSingleInstanceName(string applicationBase, bool portableMode)
        {
            if (!portableMode) return SingleInstanceName;
            var identity = Hashing.Sha256Text(Path.GetFullPath(applicationBase).ToUpperInvariant()).Substring(0, 16);
            return SingleInstanceName + "-PORTABLE-" + identity;
        }

        private static bool HasArg(string[] args, string name)
        {
            if (args == null) return false;
            foreach (var arg in args)
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string GetArgValue(string[] args, string name)
        {
            if (args == null) return null;
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
