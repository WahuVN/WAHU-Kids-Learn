using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;

namespace WAHU.Data
{
    public sealed class DatabaseBootstrapResult
    {
        public string DatabasePath { get; set; }
        public bool CreatedSchema { get; set; }
        public int SchemaVersion { get; set; }
        public string JournalMode { get; set; }
        public string ProviderVersion { get; set; }
        public string SQLiteVersion { get; set; }
        public MigrationVerificationResult Migration { get; set; }
        public ManagedBackupArtifact PreMigrationBackup { get; set; }
        public DatabaseHealthResult Health { get; set; }
    }

    public sealed class LearningDatabase
    {
        public const string SafeDefaultJournalMode = "DELETE";
        public const int DefaultBusyTimeoutMs = 2500;
        public const int CurrentSchemaVersion = 4;

        private readonly string _databasePath;
        private readonly string _schemaPath;
        private readonly string _schemaDirectory;
        private readonly SQLiteConnectionFactory _factory;
        private readonly SerializedWriteCoordinator _writes;

        public LearningDatabase(string databasePath, string schemaPath)
        {
            if (string.IsNullOrWhiteSpace(databasePath)) throw new ArgumentException("databasePath");
            if (string.IsNullOrWhiteSpace(schemaPath)) throw new ArgumentException("schemaPath");
            _databasePath = databasePath;
            _schemaPath = schemaPath;
            _schemaDirectory = Path.GetDirectoryName(Path.GetFullPath(schemaPath));
            _factory = new SQLiteConnectionFactory(databasePath, DefaultBusyTimeoutMs);
            _writes = new SerializedWriteCoordinator(_factory);
        }

        public string DatabasePath { get { return _databasePath; } }
        public SerializedWriteCoordinator Writes { get { return _writes; } }

        public SQLiteConnection OpenConnection() { return _factory.Open(); }

        public DatabaseBootstrapResult Initialize(string requestedJournalMode)
        {
            return Initialize(requestedJournalMode, null, null);
        }

        public DatabaseBootstrapResult Initialize(string requestedJournalMode, string migrationBackupRoot, string appVersion)
        {
            return _writes.ExecuteExclusive(() => InitializeExclusive(requestedJournalMode, migrationBackupRoot, appVersion));
        }

        private DatabaseBootstrapResult InitializeExclusive(string requestedJournalMode, string migrationBackupRoot, string appVersion)
        {
            if (!File.Exists(_schemaPath)) throw new FileNotFoundException("Không tìm thấy SQLite schema V1.", _schemaPath);
            var migrationV2Path = Path.Combine(_schemaDirectory, "002_attempt_immutability.sql");
            if (!File.Exists(migrationV2Path)) throw new FileNotFoundException("Không tìm thấy SQLite migration V2.", migrationV2Path);
            var migrationV3Path = Path.Combine(_schemaDirectory, "003_math_attempt_idempotency_runtime.sql");
            if (!File.Exists(migrationV3Path)) throw new FileNotFoundException("Không tìm thấy SQLite migration V3.", migrationV3Path);
            var migrationV4Path = Path.Combine(_schemaDirectory, "004_math_lesson_progress.sql");
            if (!File.Exists(migrationV4Path)) throw new FileNotFoundException("Không tìm thấy SQLite migration V4.", migrationV4Path);

            var parent = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
            var journal = NormalizeJournal(requestedJournalMode);
            bool created = false;
            int schemaVersion;
            MigrationVerificationResult latestMigration;

            using (var connection = _factory.Open())
            {
                SetJournalMode(connection, journal);
                if (!TableExists(connection, "app_meta"))
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = File.ReadAllText(_schemaPath);
                        command.ExecuteNonQuery();
                    }
                    created = true;
                }

                schemaVersion = MigrationManager.GetSchemaVersion(connection);
                if (schemaVersion < MigrationManager.InitialVersion || schemaVersion > CurrentSchemaVersion)
                    throw new InvalidOperationException("Unsupported learner DB schema_version=" + schemaVersion);

                latestMigration = MigrationManager.VerifyOrRecordInitial(connection, _schemaPath);
                if (schemaVersion >= MigrationManager.AttemptImmutabilityVersion)
                {
                    latestMigration = MigrationManager.VerifyRecordedMigration(
                        connection,
                        MigrationManager.AttemptImmutabilityVersion,
                        MigrationManager.AttemptImmutabilityName,
                        migrationV2Path);
                }
                if (schemaVersion >= MigrationManager.MathAttemptRuntimeVersion)
                {
                    latestMigration = MigrationManager.VerifyRecordedMigration(
                        connection,
                        MigrationManager.MathAttemptRuntimeVersion,
                        MigrationManager.MathAttemptRuntimeName,
                        migrationV3Path);
                }
                if (schemaVersion >= MigrationManager.MathLessonProgressVersion)
                {
                    latestMigration = MigrationManager.VerifyRecordedMigration(
                        connection,
                        MigrationManager.MathLessonProgressVersion,
                        MigrationManager.MathLessonProgressName,
                        migrationV4Path);
                }
            }

            ManagedBackupArtifact preMigrationBackup = null;
            if (schemaVersion < CurrentSchemaVersion)
            {
                if (!created)
                {
                    if (string.IsNullOrWhiteSpace(migrationBackupRoot) || string.IsNullOrWhiteSpace(appVersion))
                        throw new InvalidOperationException(
                            "Learner DB cần migration V" + schemaVersion + "→V" + CurrentSchemaVersion +
                            " nhưng chưa có migration backup context. Startup dừng để bảo vệ dữ liệu.");

                    preMigrationBackup = ManagedBackupService.CreatePreMigrationBackup(
                        this,
                        migrationBackupRoot,
                        appVersion,
                        schemaVersion,
                        CurrentSchemaVersion);
                }

                using (var connection = _factory.Open())
                {
                    SetJournalMode(connection, journal);
                    if (schemaVersion < MigrationManager.AttemptImmutabilityVersion)
                    {
                        latestMigration = MigrationManager.ApplyMigration(
                            connection,
                            MigrationManager.AttemptImmutabilityVersion,
                            MigrationManager.AttemptImmutabilityName,
                            migrationV2Path);
                        schemaVersion = MigrationManager.GetSchemaVersion(connection);
                    }
                    if (schemaVersion < MigrationManager.MathAttemptRuntimeVersion)
                    {
                        latestMigration = MigrationManager.ApplyMigration(
                            connection,
                            MigrationManager.MathAttemptRuntimeVersion,
                            MigrationManager.MathAttemptRuntimeName,
                            migrationV3Path);
                        schemaVersion = MigrationManager.GetSchemaVersion(connection);
                    }
                    if (schemaVersion < MigrationManager.MathLessonProgressVersion)
                    {
                        latestMigration = MigrationManager.ApplyMigration(
                            connection,
                            MigrationManager.MathLessonProgressVersion,
                            MigrationManager.MathLessonProgressName,
                            migrationV4Path);
                        schemaVersion = MigrationManager.GetSchemaVersion(connection);
                    }
                    if (schemaVersion != CurrentSchemaVersion)
                        throw new InvalidDataException("Migration completed without expected schema_version=" + CurrentSchemaVersion);
                }
            }

            using (var connection = _factory.Open())
            {
                SetJournalMode(connection, journal);
                var verifiedV1 = MigrationManager.VerifyOrRecordInitial(connection, _schemaPath);
                var verifiedV2 = MigrationManager.VerifyRecordedMigration(
                    connection,
                    MigrationManager.AttemptImmutabilityVersion,
                    MigrationManager.AttemptImmutabilityName,
                    migrationV2Path);
                var verifiedV3 = MigrationManager.VerifyRecordedMigration(
                    connection,
                    MigrationManager.MathAttemptRuntimeVersion,
                    MigrationManager.MathAttemptRuntimeName,
                    migrationV3Path);
                var verifiedV4 = MigrationManager.VerifyRecordedMigration(
                    connection,
                    MigrationManager.MathLessonProgressVersion,
                    MigrationManager.MathLessonProgressName,
                    migrationV4Path);
                if (latestMigration == null || latestMigration.Version < verifiedV4.Version)
                    latestMigration = verifiedV4;
                schemaVersion = MigrationManager.GetSchemaVersion(connection);
                if (schemaVersion != CurrentSchemaVersion)
                    throw new InvalidDataException("Final learner DB schema_version mismatch=" + schemaVersion);
                if (verifiedV1.Version != MigrationManager.InitialVersion)
                    throw new InvalidDataException("V1 migration verification failed.");

                var health = DatabaseHealth.Check(connection);
                if (!health.IsHealthy)
                    throw new InvalidDataException("Learner DB failed health check: integrity=" + health.Integrity + ", fk=" + health.ForeignKeyIssues);

                return new DatabaseBootstrapResult
                {
                    DatabasePath = _databasePath,
                    CreatedSchema = created,
                    SchemaVersion = schemaVersion,
                    JournalMode = health.JournalMode,
                    ProviderVersion = typeof(SQLiteConnection).Assembly.GetName().Version.ToString(),
                    SQLiteVersion = SQLiteConnection.SQLiteVersion,
                    Migration = latestMigration,
                    PreMigrationBackup = preMigrationBackup,
                    Health = health
                };
            }
        }

        public DatabaseBackupResult CreateVerifiedBackup(string destinationPath)
        {
            return _writes.ExecuteExclusive(() => SQLiteBackupService.CreateVerifiedBackup(this, destinationPath));
        }

        public DatabaseRestoreResult RestoreVerifiedBackup(string backupPath)
        {
            return _writes.ExecuteExclusive(() => SQLiteBackupService.RestoreVerifiedBackup(backupPath, _databasePath));
        }

        public AutomaticBackupSet CreateAutomaticBackupSet(string backupsRoot, string appVersion, int recentKeep, int weeklyKeep)
        {
            return _writes.ExecuteExclusive(() => ManagedBackupService.CreateAutomaticBackupSet(
                this, backupsRoot, appVersion, recentKeep, weeklyKeep));
        }

        public AutomaticBackupSet CreateAutomaticBackupSet(string backupsRoot, string appVersion)
        {
            return CreateAutomaticBackupSet(backupsRoot, appVersion,
                ManagedBackupService.RecentKeepDefault, ManagedBackupService.WeeklyKeepDefault);
        }

        public AutomaticBackupSet CreateAutomaticBackupSetAtUtc(string backupsRoot, string appVersion, int recentKeep, int weeklyKeep, DateTime createdAtUtc)
        {
            return _writes.ExecuteExclusive(() => ManagedBackupService.CreateAutomaticBackupSetAtUtc(
                this, backupsRoot, appVersion, recentKeep, weeklyKeep, createdAtUtc));
        }

        public ManagedBackupArtifact CreatePreMigrationBackup(string backupsRoot, string appVersion, int fromSchemaVersion, int toSchemaVersion)
        {
            return _writes.ExecuteExclusive(() => ManagedBackupService.CreatePreMigrationBackup(
                this, backupsRoot, appVersion, fromSchemaVersion, toSchemaVersion));
        }

        public ManagedBackupArtifact CreateManualBackup(string destinationDirectory, string appVersion)
        {
            return _writes.ExecuteExclusive(() => ManagedBackupService.CreateManualBackup(this, destinationDirectory, appVersion));
        }

        public DatabaseRestoreResult RestoreManagedBackup(string metadataPath)
        {
            return _writes.ExecuteExclusive(() => ManagedBackupService.RestoreManagedBackup(metadataPath, _databasePath));
        }

        private static string NormalizeJournal(string requested)
        {
            if (string.Equals(requested, "WAL", StringComparison.OrdinalIgnoreCase)) return "WAL";
            return SafeDefaultJournalMode;
        }

        private static void SetJournalMode(SQLiteConnection connection, string mode)
        {
            var actual = Convert.ToString(
                DatabaseHealth.Scalar(connection, "PRAGMA journal_mode=" + mode + ";"),
                CultureInfo.InvariantCulture).ToUpperInvariant();
            if (actual != mode)
                throw new InvalidOperationException("SQLite refused journal_mode " + mode + "; actual=" + actual);
        }

        private static bool TableExists(SQLiteConnection connection, string name)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=@name;";
                command.Parameters.AddWithValue("@name", name);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
            }
        }
    }
}
