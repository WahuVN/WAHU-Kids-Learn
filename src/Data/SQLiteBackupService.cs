using System;
using System.Data.SQLite;
using System.IO;
using WAHU.Platform;

namespace WAHU.Data
{
    public sealed class DatabaseBackupResult
    {
        public string Path { get; set; }
        public string Sha256 { get; set; }
        public long Bytes { get; set; }
        public DatabaseHealthResult Health { get; set; }
    }

    public sealed class DatabaseRestoreResult
    {
        public string RestoredPath { get; set; }
        public string PreservedOriginalPath { get; set; }
        public DatabaseHealthResult Health { get; set; }
    }

    public static class SQLiteBackupService
    {
        public static DatabaseBackupResult CreateVerifiedBackup(LearningDatabase database, string destinationPath)
        {
            if (database == null) throw new ArgumentNullException("database");
            if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("destinationPath");
            var parent = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
            var temp = destinationPath + ".tmp." + Guid.NewGuid().ToString("N");

            try
            {
                using (var source = database.OpenConnection())
                using (var dest = new SQLiteConnection("Data Source=" + temp + ";Version=3;Foreign Keys=True;Pooling=False;"))
                {
                    dest.Open();
                    source.BackupDatabase(dest, "main", "main", -1, null, 0);
                }

                var health = VerifyPath(temp);
                if (!health.IsHealthy) throw new InvalidDataException("Backup snapshot failed verification.");
                var hash = Hashing.Sha256File(temp);
                PublishAtomically(temp, destinationPath);
                return new DatabaseBackupResult { Path = destinationPath, Sha256 = hash, Bytes = new FileInfo(destinationPath).Length, Health = health };
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        public static DatabaseRestoreResult RestoreVerifiedBackup(string backupPath, string targetDatabasePath)
        {
            if (!File.Exists(backupPath)) throw new FileNotFoundException("Backup không tồn tại.", backupPath);
            if (string.IsNullOrWhiteSpace(targetDatabasePath)) throw new ArgumentException("targetDatabasePath");
            var backupHealth = VerifyPath(backupPath);
            if (!backupHealth.IsHealthy) throw new InvalidDataException("Backup không đạt health gate.");

            var parent = Path.GetDirectoryName(targetDatabasePath);
            if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
            var temp = targetDatabasePath + ".restore.tmp." + Guid.NewGuid().ToString("N");
            var preserved = targetDatabasePath + ".pre_restore." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".db";
            File.Copy(backupPath, temp, true);

            try
            {
                var tempHealth = VerifyPath(temp);
                if (!tempHealth.IsHealthy) throw new InvalidDataException("Restore temp DB failed verification.");
                if (File.Exists(targetDatabasePath))
                {
                    DeleteSidecars(targetDatabasePath);
                    File.Replace(temp, targetDatabasePath, preserved, true);
                }
                else
                {
                    File.Move(temp, targetDatabasePath);
                    preserved = null;
                }
                var finalHealth = VerifyPath(targetDatabasePath);
                if (!finalHealth.IsHealthy) throw new InvalidDataException("Restored DB failed final verification.");
                return new DatabaseRestoreResult { RestoredPath = targetDatabasePath, PreservedOriginalPath = preserved, Health = finalHealth };
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        public static DatabaseHealthResult VerifyPath(string databasePath)
        {
            using (var connection = new SQLiteConnection("Data Source=" + databasePath + ";Version=3;Foreign Keys=True;Pooling=False;"))
            {
                connection.Open();
                return DatabaseHealth.Check(connection);
            }
        }

        private static void PublishAtomically(string temp, string destination)
        {
            if (File.Exists(destination)) File.Replace(temp, destination, null, true);
            else File.Move(temp, destination);
        }

        private static void DeleteSidecars(string databasePath)
        {
            foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
            {
                var path = databasePath + suffix;
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
