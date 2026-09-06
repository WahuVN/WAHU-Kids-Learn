using System;
using System.IO;
using System.Windows.Forms;
using WAHU.Data;
using WAHU.Platform;

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
                    MessageBox.Show(
                        "Cấu hình ứng dụng không hợp lệ hoặc bị thiếu.\r\n\r\n" + ex.Message,
                        "WAHU Kids Learn — lỗi cấu hình",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
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
                    string databaseError = null;
                    try
                    {
                        BootstrapRuntime(config, out preflight, out database);
                    }
                    catch (Exception ex)
                    {
                        databaseError = ex.GetType().Name + ": " + ex.Message;
                    }

                    Application.Run(new MainForm(preflight, database, databaseError, runtimeMarker.PreviousRunUnclean));
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
                BootstrapRuntime(config, out preflight, out database);
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
                    "foreign_key_issues=" + database.Health.ForeignKeyIssues
                });
                return database.Health != null && database.Health.IsHealthy ? 0 : 31;
            }
            catch (Exception ex)
            {
                WriteBootstrapFailure(outPath, "BOOTSTRAP_FAILED", ex);
                return 30;
            }
        }

        private static void BootstrapRuntime(RuntimeConfigBundle config, out PreflightReport preflight, out DatabaseBootstrapResult database)
        {
            if (config == null) throw new ArgumentNullException("config");
            preflight = PreflightProbe.Collect(Application.ExecutablePath);
            JsonReportWriter.Write(preflight, Path.Combine(config.UserRoot, "diagnostics", "preflight.json"));

            var schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "schema", "001_initial.sql");
            var learningDb = new LearningDatabase(config.DatabasePath, schemaPath);
            database = learningDb.Initialize(config.DatabaseJournalMode, config.BackupsDirectory, config.AppVersion);
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
