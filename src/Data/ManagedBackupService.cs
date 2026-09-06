using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using WAHU.Platform;

namespace WAHU.Data
{
    public sealed class ManagedBackupArtifact
    {
        public string BackupId { get; set; }
        public string BackupType { get; set; }
        public string DatabasePath { get; set; }
        public string MetadataPath { get; set; }
        public string Sha256 { get; set; }
        public long Bytes { get; set; }
        public int SchemaVersion { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DatabaseHealthResult Health { get; set; }
    }

    public sealed class AutomaticBackupSet
    {
        public ManagedBackupArtifact Recent { get; set; }
        public ManagedBackupArtifact Weekly { get; set; }
        public int RecentDeleted { get; set; }
        public int WeeklyDeleted { get; set; }
    }

    public sealed class BackupMetadataVerification
    {
        public bool IsValid { get; set; }
        public string BackupId { get; set; }
        public string BackupType { get; set; }
        public string DatabasePath { get; set; }
        public string MetadataPath { get; set; }
        public string ExpectedSha256 { get; set; }
        public string ActualSha256 { get; set; }
        public int SchemaVersion { get; set; }
        public DatabaseHealthResult Health { get; set; }
    }

    public static class ManagedBackupService
    {
        public const int RecentKeepDefault = 5;
        public const int WeeklyKeepDefault = 4;
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer
        {
            MaxJsonLength = 4 * 1024 * 1024,
            RecursionLimit = 64
        };

        public static AutomaticBackupSet CreateAutomaticBackupSet(
            LearningDatabase database,
            string backupsRoot,
            string appVersion,
            int recentKeep,
            int weeklyKeep)
        {
            return CreateAutomaticBackupSetAtUtc(database, backupsRoot, appVersion, recentKeep, weeklyKeep, DateTime.UtcNow);
        }

        public static AutomaticBackupSet CreateAutomaticBackupSetAtUtc(
            LearningDatabase database,
            string backupsRoot,
            string appVersion,
            int recentKeep,
            int weeklyKeep,
            DateTime createdAtUtc)
        {
            ValidateCommon(database, backupsRoot, appVersion);
            if (recentKeep < 1 || weeklyKeep < 1) throw new ArgumentOutOfRangeException("retention");
            if (createdAtUtc.Kind == DateTimeKind.Local) createdAtUtc = createdAtUtc.ToUniversalTime();
            else if (createdAtUtc.Kind == DateTimeKind.Unspecified) createdAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);

            var createdAt = createdAtUtc;
            var packs = ReadActiveContentPacks(database);
            var recent = CreateSnapshot(database, backupsRoot, "automatic", "recent", appVersion, packs, createdAt);
            ManagedBackupArtifact weekly = null;

            var weeklyDir = Path.Combine(backupsRoot, "weekly");
            Directory.CreateDirectory(weeklyDir);
            var weekKey = IsoWeekKey(createdAt);
            if (!HasWeeklySnapshotForKey(weeklyDir, weekKey))
                weekly = CloneVerifiedSnapshot(database, recent, backupsRoot, "weekly", "weekly", appVersion, packs, createdAt, weekKey);

            var recentDeleted = ApplyRetention(Path.Combine(backupsRoot, "recent"), recentKeep);
            var weeklyDeleted = ApplyRetention(weeklyDir, weeklyKeep);
            return new AutomaticBackupSet
            {
                Recent = recent,
                Weekly = weekly,
                RecentDeleted = recentDeleted,
                WeeklyDeleted = weeklyDeleted
            };
        }

        public static ManagedBackupArtifact CreatePreMigrationBackup(
            LearningDatabase database,
            string backupsRoot,
            string appVersion,
            int fromSchemaVersion,
            int toSchemaVersion)
        {
            ValidateCommon(database, backupsRoot, appVersion);
            if (fromSchemaVersion < 1 || toSchemaVersion <= fromSchemaVersion)
                throw new ArgumentException("Pre-migration backup requires toSchemaVersion > fromSchemaVersion.");

            var artifact = CreateSnapshot(
                database,
                backupsRoot,
                "pre_migration",
                "pre_migration",
                appVersion,
                ReadActiveContentPacks(database),
                DateTime.UtcNow,
                "v" + fromSchemaVersion + "-to-v" + toSchemaVersion);
            return artifact;
        }

        public static ManagedBackupArtifact CreateManualBackup(
            LearningDatabase database,
            string destinationDirectory,
            string appVersion)
        {
            ValidateCommon(database, destinationDirectory, appVersion);
            return CreateSnapshot(
                database,
                destinationDirectory,
                "manual",
                string.Empty,
                appVersion,
                ReadActiveContentPacks(database),
                DateTime.UtcNow);
        }

        public static BackupMetadataVerification VerifyManagedBackup(string metadataPath)
        {
            if (string.IsNullOrWhiteSpace(metadataPath)) throw new ArgumentException("metadataPath");
            if (!File.Exists(metadataPath)) throw new FileNotFoundException("Không tìm thấy backup metadata.", metadataPath);

            var root = ReadMetadata(metadataPath);
            RequireMetadataSchema(root);
            var backupId = RequiredString(root, "backup_id");
            var backupType = RequiredString(root, "backup_type");
            var databaseFile = RequiredString(root, "database_file");
            var expectedHash = RequiredString(root, "database_sha256");
            var schemaVersion = RequiredInt(root, "database_schema_version");
            var verified = RequiredBool(root, "verified");
            if (!verified) throw new InvalidDataException("Backup metadata chưa được đánh dấu verified.");

            var metadataDirectory = Path.GetDirectoryName(Path.GetFullPath(metadataPath));
            var databasePath = Path.GetFullPath(Path.Combine(metadataDirectory, databaseFile));
            if (!IsPathUnder(metadataDirectory, databasePath)) throw new InvalidDataException("database_file thoát khỏi thư mục backup.");
            if (!File.Exists(databasePath)) throw new FileNotFoundException("Backup DB không tồn tại.", databasePath);

            var actualHash = Hashing.Sha256File(databasePath);
            if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Backup SHA-256 không khớp metadata.");

            var health = SQLiteBackupService.VerifyPath(databasePath);
            if (!health.IsHealthy) throw new InvalidDataException("Backup DB không qua integrity/FK gate.");
            var actualSchema = ReadSchemaVersion(databasePath);
            if (actualSchema != schemaVersion)
                throw new InvalidDataException("Backup schema_version không khớp metadata.");

            return new BackupMetadataVerification
            {
                IsValid = true,
                BackupId = backupId,
                BackupType = backupType,
                DatabasePath = databasePath,
                MetadataPath = Path.GetFullPath(metadataPath),
                ExpectedSha256 = expectedHash,
                ActualSha256 = actualHash,
                SchemaVersion = actualSchema,
                Health = health
            };
        }

        public static DatabaseRestoreResult RestoreManagedBackup(string metadataPath, string targetDatabasePath)
        {
            var verified = VerifyManagedBackup(metadataPath);
            return SQLiteBackupService.RestoreVerifiedBackup(verified.DatabasePath, targetDatabasePath);
        }

        private static ManagedBackupArtifact CreateSnapshot(
            LearningDatabase database,
            string root,
            string backupType,
            string bucket,
            string appVersion,
            string[] activePacks,
            DateTime createdAtUtc,
            string suffix = null)
        {
            var directory = string.IsNullOrEmpty(bucket) ? root : Path.Combine(root, bucket);
            Directory.CreateDirectory(directory);
            var backupId = BuildBackupId(backupType, createdAtUtc, suffix);
            var databasePath = Path.Combine(directory, backupId + ".db");
            var result = SQLiteBackupService.CreateVerifiedBackup(database, databasePath);
            var schemaVersion = ReadSchemaVersion(databasePath);
            var metadataPath = Path.Combine(directory, backupId + ".json");

            WriteMetadataAtomically(metadataPath, backupId, backupType, Path.GetFileName(databasePath), result,
                appVersion, schemaVersion, activePacks, createdAtUtc, suffix);
            RecordBackupHistory(database, backupId, backupType, databasePath, result.Sha256, appVersion,
                schemaVersion, activePacks, createdAtUtc);

            return new ManagedBackupArtifact
            {
                BackupId = backupId,
                BackupType = backupType,
                DatabasePath = databasePath,
                MetadataPath = metadataPath,
                Sha256 = result.Sha256,
                Bytes = result.Bytes,
                SchemaVersion = schemaVersion,
                CreatedAtUtc = createdAtUtc,
                Health = result.Health
            };
        }

        private static ManagedBackupArtifact CloneVerifiedSnapshot(
            LearningDatabase database,
            ManagedBackupArtifact source,
            string root,
            string backupType,
            string bucket,
            string appVersion,
            string[] activePacks,
            DateTime createdAtUtc,
            string suffix)
        {
            if (source == null || !File.Exists(source.DatabasePath)) throw new InvalidDataException("Recent snapshot source missing.");
            var sourceHealth = SQLiteBackupService.VerifyPath(source.DatabasePath);
            if (!sourceHealth.IsHealthy) throw new InvalidDataException("Recent snapshot source is not healthy.");

            var directory = Path.Combine(root, bucket);
            Directory.CreateDirectory(directory);
            var backupId = BuildBackupId(backupType, createdAtUtc, suffix);
            var destination = Path.Combine(directory, backupId + ".db");
            var temp = destination + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                File.Copy(source.DatabasePath, temp, false);
                var clonedHealth = SQLiteBackupService.VerifyPath(temp);
                if (!clonedHealth.IsHealthy) throw new InvalidDataException("Weekly clone failed health verification.");
                var hash = Hashing.Sha256File(temp);
                if (!string.Equals(hash, source.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Weekly clone hash differs from verified recent snapshot.");
                if (File.Exists(destination)) throw new IOException("Backup destination already exists: " + destination);
                File.Move(temp, destination);

                var result = new DatabaseBackupResult
                {
                    Path = destination,
                    Sha256 = hash,
                    Bytes = new FileInfo(destination).Length,
                    Health = clonedHealth
                };
                var schemaVersion = ReadSchemaVersion(destination);
                var metadataPath = Path.Combine(directory, backupId + ".json");
                WriteMetadataAtomically(metadataPath, backupId, backupType, Path.GetFileName(destination), result,
                    appVersion, schemaVersion, activePacks, createdAtUtc, suffix);
                RecordBackupHistory(database, backupId, backupType, destination, hash, appVersion,
                    schemaVersion, activePacks, createdAtUtc);
                return new ManagedBackupArtifact
                {
                    BackupId = backupId,
                    BackupType = backupType,
                    DatabasePath = destination,
                    MetadataPath = metadataPath,
                    Sha256 = hash,
                    Bytes = result.Bytes,
                    SchemaVersion = schemaVersion,
                    CreatedAtUtc = createdAtUtc,
                    Health = clonedHealth
                };
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private static void WriteMetadataAtomically(
            string metadataPath,
            string backupId,
            string backupType,
            string databaseFile,
            DatabaseBackupResult result,
            string appVersion,
            int schemaVersion,
            string[] activePacks,
            DateTime createdAtUtc,
            string retentionKey)
        {
            var verification = new Dictionary<string, object>
            {
                { "sqlite_open", true },
                { "integrity_check", result.Health != null && result.Health.Integrity == "ok" },
                { "foreign_key_issues", result.Health == null ? -1 : result.Health.ForeignKeyIssues },
                { "hash_match", true }
            };
            var metadata = new Dictionary<string, object>
            {
                { "schema_version", 1 },
                { "backup_id", backupId },
                { "backup_type", backupType },
                { "created_at_utc", createdAtUtc.ToString("o") },
                { "database_file", databaseFile },
                { "database_sha256", result.Sha256 },
                { "database_bytes", result.Bytes },
                { "app_version", appVersion },
                { "database_schema_version", schemaVersion },
                { "active_content_packs", activePacks ?? new string[0] },
                { "retention_key", retentionKey },
                { "verified", result.Health != null && result.Health.IsHealthy },
                { "verification", verification }
            };

            var temp = metadataPath + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temp, Serializer.Serialize(metadata));
                var check = Serializer.DeserializeObject(File.ReadAllText(temp)) as Dictionary<string, object>;
                if (check == null || !RequiredBool(check, "verified")) throw new InvalidDataException("Backup metadata self-check failed.");
                if (File.Exists(metadataPath)) throw new IOException("Backup metadata destination already exists: " + metadataPath);
                File.Move(temp, metadataPath);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private static void RecordBackupHistory(
            LearningDatabase database,
            string backupId,
            string backupType,
            string databasePath,
            string sha256,
            string appVersion,
            int schemaVersion,
            string[] activePacks,
            DateTime createdAtUtc)
        {
            using (var connection = database.OpenConnection())
            using (var tx = connection.BeginTransaction())
            using (var command = connection.CreateCommand())
            {
                command.Transaction = tx;
                command.CommandText = @"INSERT INTO backup_history(
    id, backup_type, relative_or_external_path, sha256, app_version, schema_version,
    content_manifest_json, created_at_utc, verified)
VALUES(@id,@type,@path,@sha,@app,@schema,@packs,@utc,1);";
                command.Parameters.AddWithValue("@id", backupId);
                command.Parameters.AddWithValue("@type", NormalizeHistoryType(backupType));
                command.Parameters.AddWithValue("@path", databasePath);
                command.Parameters.AddWithValue("@sha", sha256);
                command.Parameters.AddWithValue("@app", appVersion);
                command.Parameters.AddWithValue("@schema", schemaVersion);
                command.Parameters.AddWithValue("@packs", Serializer.Serialize(activePacks ?? new string[0]));
                command.Parameters.AddWithValue("@utc", createdAtUtc.ToString("o"));
                command.ExecuteNonQuery();
                tx.Commit();
            }
        }

        private static string NormalizeHistoryType(string backupType)
        {
            if (backupType == "automatic" || backupType == "weekly" || backupType == "manual" || backupType == "pre_migration")
                return backupType;
            throw new InvalidDataException("Unsupported backup_type: " + backupType);
        }

        private static int ApplyRetention(string directory, int keep)
        {
            if (!Directory.Exists(directory)) return 0;
            var valid = new List<Tuple<DateTime, string, string>>();
            foreach (var metadataPath in Directory.GetFiles(directory, "*.json"))
            {
                try
                {
                    var root = ReadMetadata(metadataPath);
                    RequireMetadataSchema(root);
                    var created = DateTime.Parse(RequiredString(root, "created_at_utc"), CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                    var dbFile = RequiredString(root, "database_file");
                    var dbPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(metadataPath), dbFile));
                    if (!IsPathUnder(Path.GetDirectoryName(Path.GetFullPath(metadataPath)), dbPath)) continue;
                    if (!File.Exists(dbPath)) continue;
                    valid.Add(Tuple.Create(created, metadataPath, dbPath));
                }
                catch
                {
                    // Fail-safe retention: unknown/malformed files are never deleted automatically.
                }
            }

            var delete = valid.OrderByDescending(x => x.Item1).Skip(keep).ToArray();
            var count = 0;
            foreach (var item in delete)
            {
                try
                {
                    File.Delete(item.Item2);
                    File.Delete(item.Item3);
                    count++;
                }
                catch
                {
                    // Retention failure must not invalidate the newly created backup.
                }
            }
            return count;
        }

        private static bool HasWeeklySnapshotForKey(string weeklyDirectory, string weekKey)
        {
            foreach (var metadataPath in Directory.GetFiles(weeklyDirectory, "*.json"))
            {
                try
                {
                    var root = ReadMetadata(metadataPath);
                    object value;
                    if (root.TryGetValue("retention_key", out value) &&
                        string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), weekKey, StringComparison.Ordinal))
                        return true;
                }
                catch { }
            }
            return false;
        }

        private static string[] ReadActiveContentPacks(LearningDatabase database)
        {
            var result = new List<string>();
            using (var connection = database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT pack_id || '@' || version FROM content_pack_state WHERE active=1 AND status='VERIFIED' ORDER BY pack_id, version;";
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) result.Add(Convert.ToString(reader[0], CultureInfo.InvariantCulture));
            }
            return result.ToArray();
        }

        private static int ReadSchemaVersion(string databasePath)
        {
            using (var connection = new SQLiteConnection("Data Source=" + databasePath + ";Version=3;Foreign Keys=True;Pooling=False;"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT value FROM app_meta WHERE key='schema_version';";
                    var value = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                    int version;
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out version))
                        throw new InvalidDataException("Backup schema_version invalid: " + value);
                    return version;
                }
            }
        }

        private static Dictionary<string, object> ReadMetadata(string metadataPath)
        {
            try
            {
                var root = Serializer.DeserializeObject(File.ReadAllText(metadataPath)) as Dictionary<string, object>;
                if (root == null) throw new InvalidDataException("Backup metadata root is not object.");
                return root;
            }
            catch (InvalidDataException) { throw; }
            catch (Exception ex) { throw new InvalidDataException("Backup metadata JSON invalid.", ex); }
        }

        private static void RequireMetadataSchema(Dictionary<string, object> root)
        {
            if (RequiredInt(root, "schema_version") != 1) throw new InvalidDataException("Unsupported backup metadata schema.");
        }

        private static string RequiredString(Dictionary<string, object> root, string key)
        {
            object value;
            if (!root.TryGetValue(key, out value) || value == null) throw new InvalidDataException("Backup metadata missing " + key);
            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Backup metadata empty " + key);
            return text;
        }

        private static int RequiredInt(Dictionary<string, object> root, string key)
        {
            object value;
            if (!root.TryGetValue(key, out value) || value == null) throw new InvalidDataException("Backup metadata missing " + key);
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch (Exception ex) { throw new InvalidDataException("Backup metadata invalid integer " + key, ex); }
        }

        private static bool RequiredBool(Dictionary<string, object> root, string key)
        {
            object value;
            if (!root.TryGetValue(key, out value) || !(value is bool)) throw new InvalidDataException("Backup metadata invalid boolean " + key);
            return (bool)value;
        }

        private static string BuildBackupId(string backupType, DateTime utc, string suffix)
        {
            var id = backupType + "-" + utc.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(suffix)) id += "-" + SanitizeIdPart(suffix);
            return id + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static string SanitizeIdPart(string value)
        {
            var chars = value.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray();
            return new string(chars);
        }

        private static string IsoWeekKey(DateTime utc)
        {
            var date = utc.Date;
            var dayNumber = (int)date.DayOfWeek;
            if (dayNumber == 0) dayNumber = 7; // Sunday is ISO day 7.
            var thursday = date.AddDays(4 - dayNumber);
            var week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                thursday, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            return thursday.Year.ToString("0000", CultureInfo.InvariantCulture) + "-W" +
                   week.ToString("00", CultureInfo.InvariantCulture);
        }

        private static bool IsPathUnder(string root, string candidate)
        {
            var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var normalizedCandidate = Path.GetFullPath(candidate);
            return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateCommon(LearningDatabase database, string root, string appVersion)
        {
            if (database == null) throw new ArgumentNullException("database");
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("root");
            if (string.IsNullOrWhiteSpace(appVersion)) throw new ArgumentException("appVersion");
            Directory.CreateDirectory(root);
        }
    }
}
