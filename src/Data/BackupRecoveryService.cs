using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WAHU.Data
{
    public sealed class RecoveryBackupCandidate
    {
        public string BackupId { get; set; }
        public string BackupType { get; set; }
        public string MetadataPath { get; set; }
        public string DatabasePath { get; set; }
        public int SchemaVersion { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class RecoveryRestoreResult
    {
        public RecoveryBackupCandidate Candidate { get; set; }
        public DatabaseRestoreResult Restore { get; set; }
    }

    public static class BackupRecoveryService
    {
        public static IList<RecoveryBackupCandidate> FindVerifiedBackups(string backupsRoot)
        {
            var result = new List<RecoveryBackupCandidate>();
            if (string.IsNullOrWhiteSpace(backupsRoot) || !Directory.Exists(backupsRoot)) return result;

            foreach (var metadataPath in Directory.GetFiles(backupsRoot, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var verified = ManagedBackupService.VerifyManagedBackup(metadataPath);
                    if (!verified.IsValid || verified.Health == null || !verified.Health.IsHealthy) continue;
                    result.Add(new RecoveryBackupCandidate
                    {
                        BackupId = verified.BackupId,
                        BackupType = verified.BackupType,
                        MetadataPath = verified.MetadataPath,
                        DatabasePath = verified.DatabasePath,
                        SchemaVersion = verified.SchemaVersion,
                        CreatedAtUtc = verified.CreatedAtUtc
                    });
                }
                catch
                {
                    // Fail-closed per artifact: malformed or untrusted backups are ignored.
                }
            }

            return result.OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.BackupId, StringComparer.Ordinal)
                .ToList();
        }

        public static RecoveryRestoreResult RestoreNewestVerified(string backupsRoot, string targetDatabasePath)
        {
            if (string.IsNullOrWhiteSpace(targetDatabasePath)) throw new ArgumentException("targetDatabasePath");
            var candidates = FindVerifiedBackups(backupsRoot);
            if (candidates.Count == 0) throw new FileNotFoundException("Không tìm thấy bản sao lưu đã xác minh để phục hồi.");
            var selected = candidates[0];
            var restored = ManagedBackupService.RestoreManagedBackup(selected.MetadataPath, targetDatabasePath);
            if (restored.Health == null || !restored.Health.IsHealthy)
                throw new InvalidDataException("Bản phục hồi không qua health gate.");
            return new RecoveryRestoreResult { Candidate = selected, Restore = restored };
        }
    }
}
