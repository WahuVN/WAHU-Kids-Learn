using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using WAHU.Platform;

namespace WAHU.Data
{
    public sealed class MigrationVerificationResult
    {
        public int Version { get; set; }
        public string Name { get; set; }
        public string ChecksumSha256 { get; set; }
        public bool RecordedNow { get; set; }
    }

    public static class MigrationManager
    {
        public const int InitialVersion = 1;
        public const string InitialName = "001_initial";
        public const int AttemptImmutabilityVersion = 2;
        public const string AttemptImmutabilityName = "002_attempt_immutability";
        public const int MathAttemptRuntimeVersion = 3;
        public const string MathAttemptRuntimeName = "003_math_attempt_idempotency_runtime";
        public const int MathLessonProgressVersion = 4;
        public const string MathLessonProgressName = "004_math_lesson_progress";
        public const int MathRuntimePackIdentityVersion = 5;
        public const string MathRuntimePackIdentityName = "005_math_runtime_pack_identity";
        // A short-lived V5 development build shipped the same V5 schema contract
        // with a different migration-file byte representation. Existing learner
        // databases may legitimately contain this recorded checksum. Keep the
        // exact known value fail-closed instead of rewriting learner history.
        private const string LegacyEquivalentV5Checksum =
            "1E87F8780BB1D67FEB50854C80700A86D3F26D994F33524B20760C9506FD77BA";
        private const string CanonicalV5Checksum =
            "C625584A8ED62BFF4D14FC8EA994C43C03D3A79A5A16121D7C1DAAAF5E0E5CC5";
        public const int AttemptCommitKeyImmutabilityVersion = 6;
        public const string AttemptCommitKeyImmutabilityName = "006_attempt_commit_key_immutability";

        public static int GetSchemaVersion(SQLiteConnection connection)
        {
            if (connection == null) throw new ArgumentNullException("connection");
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT value FROM app_meta WHERE key='schema_version';";
                var value = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                int version;
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out version))
                    throw new InvalidDataException("Invalid learner DB schema_version=" + value);
                return version;
            }
        }

        public static MigrationVerificationResult VerifyOrRecordInitial(SQLiteConnection connection, string schemaPath)
        {
            ValidateMigrationFile(schemaPath);
            var currentChecksum = CanonicalMigrationChecksum(schemaPath);
            var rawChecksum = Hashing.Sha256File(schemaPath);
            var existing = TryReadRecorded(connection, InitialVersion);
            if (existing != null)
                return VerifyExisting(existing, InitialVersion, InitialName, currentChecksum, rawChecksum);

            if (GetSchemaVersion(connection) < InitialVersion)
                throw new InvalidDataException("Cannot record initial migration before schema V1 exists.");

            using (var tx = connection.BeginTransaction())
            {
                InsertHistory(connection, tx, InitialVersion, InitialName, currentChecksum, DateTime.UtcNow);
                tx.Commit();
            }

            return new MigrationVerificationResult
            {
                Version = InitialVersion,
                Name = InitialName,
                ChecksumSha256 = currentChecksum,
                RecordedNow = true
            };
        }

        public static MigrationVerificationResult VerifyRecordedMigration(
            SQLiteConnection connection,
            int version,
            string name,
            string migrationPath)
        {
            ValidateMigrationFile(migrationPath);
            var checksum = CanonicalMigrationChecksum(migrationPath);
            var rawChecksum = Hashing.Sha256File(migrationPath);
            var existing = TryReadRecorded(connection, version);
            if (existing == null)
                throw new InvalidDataException("Missing migration_history row for version " + version + ".");
            var result = VerifyExisting(existing, version, name, checksum, rawChecksum);
            if (GetSchemaVersion(connection) < version)
                throw new InvalidDataException("migration_history is ahead of app_meta schema_version.");
            return result;
        }

        public static MigrationVerificationResult ApplyMigration(
            SQLiteConnection connection,
            int version,
            string name,
            string migrationPath)
        {
            if (connection == null) throw new ArgumentNullException("connection");
            if (version <= InitialVersion) throw new ArgumentOutOfRangeException("version");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name");
            ValidateMigrationFile(migrationPath);

            var checksum = CanonicalMigrationChecksum(migrationPath);
            var rawChecksum = Hashing.Sha256File(migrationPath);
            var existing = TryReadRecorded(connection, version);
            if (existing != null)
                return VerifyExisting(existing, version, name, checksum, rawChecksum);

            var current = GetSchemaVersion(connection);
            if (current != version - 1)
                throw new InvalidOperationException("Migration " + version + " requires schema " + (version - 1) + ", actual=" + current);

            var sql = File.ReadAllText(migrationPath);
            RejectTransactionControl(sql, migrationPath);

            using (var tx = connection.BeginTransaction())
            {
                if (version == AttemptCommitKeyImmutabilityVersion)
                    ValidateAttemptCommitKeyAuthority(connection, tx);

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = tx;
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }

                using (var update = connection.CreateCommand())
                {
                    update.Transaction = tx;
                    update.CommandText = "UPDATE app_meta SET value=@version, updated_at_utc=@utc WHERE key='schema_version';";
                    update.Parameters.AddWithValue("@version", version.ToString(CultureInfo.InvariantCulture));
                    update.Parameters.AddWithValue("@utc", DateTime.UtcNow.ToString("o"));
                    if (update.ExecuteNonQuery() != 1)
                        throw new InvalidDataException("app_meta schema_version row missing during migration.");
                }

                InsertHistory(connection, tx, version, name, checksum, DateTime.UtcNow);
                tx.Commit();
            }

            return new MigrationVerificationResult
            {
                Version = version,
                Name = name,
                ChecksumSha256 = checksum,
                RecordedNow = true
            };
        }

        private sealed class RecordedMigration
        {
            public string Name { get; set; }
            public string Checksum { get; set; }
        }

        private static RecordedMigration TryReadRecorded(SQLiteConnection connection, int version)
        {
            if (connection == null) throw new ArgumentNullException("connection");
            using (var query = connection.CreateCommand())
            {
                query.CommandText = "SELECT name, checksum_sha256 FROM migration_history WHERE version=@version;";
                query.Parameters.AddWithValue("@version", version);
                using (var reader = query.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new RecordedMigration
                    {
                        Name = Convert.ToString(reader[0], CultureInfo.InvariantCulture),
                        Checksum = Convert.ToString(reader[1], CultureInfo.InvariantCulture)
                    };
                }
            }
        }

        private static MigrationVerificationResult VerifyExisting(
            RecordedMigration existing,
            int version,
            string expectedName,
            string expectedChecksum,
            string rawChecksum)
        {
            if (!string.Equals(existing.Name, expectedName, StringComparison.Ordinal))
                throw new InvalidDataException("Migration v" + version + " name mismatch: " + existing.Name);
            var checksumMatches =
                string.Equals(existing.Checksum, expectedChecksum, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existing.Checksum, rawChecksum, StringComparison.OrdinalIgnoreCase) ||
                (version == MathRuntimePackIdentityVersion &&
                 string.Equals(expectedChecksum, CanonicalV5Checksum, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(existing.Checksum, LegacyEquivalentV5Checksum, StringComparison.OrdinalIgnoreCase));
            if (!checksumMatches)
                throw new InvalidDataException("Migration v" + version + " checksum mismatch. Shipped migration differs from recorded migration.");
            return new MigrationVerificationResult
            {
                Version = version,
                Name = expectedName,
                ChecksumSha256 = expectedChecksum,
                RecordedNow = false
            };
        }

        private static string CanonicalMigrationChecksum(string path)
        {
            // Git, ZIP and Windows checkouts may materialize the exact same SQL
            // with LF or CRLF. Migration identity follows SQL text, not checkout
            // newline bytes, so a harmless line-ending conversion cannot brick
            // an otherwise healthy learner database.
            var text = File.ReadAllText(path)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");
            return Hashing.Sha256Text(text);
        }

        private static void ValidateAttemptCommitKeyAuthority(SQLiteConnection connection, SQLiteTransaction tx)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = tx;
                command.CommandText = @"SELECT count(*)
FROM attempt_commit_key k
LEFT JOIN attempt a ON a.id=k.attempt_id
WHERE a.id IS NULL
   OR a.session_id<>k.session_id
   OR a.question_id<>k.question_id
   OR a.attempt_index<>k.attempt_index;";
                var invalid = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                if (invalid != 0)
                    throw new InvalidDataException("attempt_commit_key semantic authority mismatch before migration V6.");
            }
        }

        private static void InsertHistory(
            SQLiteConnection connection,
            SQLiteTransaction tx,
            int version,
            string name,
            string checksum,
            DateTime appliedAtUtc)
        {
            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = tx;
                insert.CommandText = @"INSERT INTO migration_history(version,name,checksum_sha256,applied_at_utc)
VALUES(@version,@name,@checksum,@utc);";
                insert.Parameters.AddWithValue("@version", version);
                insert.Parameters.AddWithValue("@name", name);
                insert.Parameters.AddWithValue("@checksum", checksum);
                insert.Parameters.AddWithValue("@utc", appliedAtUtc.ToString("o"));
                insert.ExecuteNonQuery();
            }
        }

        private static void ValidateMigrationFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("Không tìm thấy migration schema.", path);
        }

        private static void RejectTransactionControl(string sql, string path)
        {
            var upper = (sql ?? string.Empty).ToUpperInvariant();
            if (upper.Contains("BEGIN TRANSACTION") || upper.Contains("BEGIN IMMEDIATE") ||
                upper.Contains("BEGIN EXCLUSIVE") || upper.Contains("\nCOMMIT;") ||
                upper.Contains("\rCOMMIT;") || upper.Contains("\nROLLBACK;") || upper.Contains("\rROLLBACK;"))
                throw new InvalidDataException("Versioned migration must not control its own transaction: " + path);
        }
    }
}
