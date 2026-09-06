using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WAHU.Data;

namespace WAHU.SQLiteRuntimeSmoke
{
    internal static class Program
    {
        private static int _passed;

        private static int Main(string[] args)
        {
            try
            {
                var schemaPath = args.Length > 0 ? args[0] : FindSchema();
                if (!File.Exists(schemaPath)) throw new FileNotFoundException("schema not found", schemaPath);
                Console.WriteLine("PROVIDER=" + typeof(SQLiteConnection).Assembly.FullName);
                Console.WriteLine("SQLITE_VERSION=" + SQLiteConnection.SQLiteVersion);
                Assert(typeof(SQLiteConnection).Assembly.GetName().Version.ToString() == "2.0.4.0", "provider_2_0_4");
                Assert(SQLiteConnection.SQLiteVersion.StartsWith("3.53.4", StringComparison.Ordinal), "sqlite_3_53_4");
                Assert(IntPtr.Size == 4, "process_x86");
                Assert(File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "e_sqlite3.dll")), "native_e_sqlite3_present");

                var root = Path.Combine(Path.GetTempPath(), "wahu-sqlite-smoke-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                try
                {
                    TestJournalMode(root, schemaPath, "DELETE");
                    TestJournalMode(root, schemaPath, "WAL");
                    TestDataService(root, schemaPath);
                    TestMigrationChecksumAndCoordinator(root, schemaPath);
                    TestManagedBackupLifecycle(root, schemaPath);
                    TestExistingV1MigrationWithPreBackup(root, schemaPath);
                    TestAnswerCommitAtomicity(root, schemaPath);
                }
                finally
                {
                    try { Directory.Delete(root, true); } catch { }
                }

                Console.WriteLine("SQLITE_RUNTIME_SMOKE_PASS assertions=" + _passed);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SQLITE_RUNTIME_SMOKE_FAIL " + ex);
                return 1;
            }
        }

        private static void TestJournalMode(string root, string schemaPath, string mode)
        {
            var db = Path.Combine(root, "learning-" + mode.ToLowerInvariant() + ".db");
            var backup = Path.Combine(root, "backup-" + mode.ToLowerInvariant() + ".db");
            var cs = "Data Source=" + db + ";Version=3;Foreign Keys=True;Pooling=False;";
            using (var source = new SQLiteConnection(cs))
            {
                source.Open();
                Scalar(source, "PRAGMA foreign_keys=ON;");
                var actualMode = Convert.ToString(Scalar(source, "PRAGMA journal_mode=" + mode + ";"), CultureInfo.InvariantCulture).ToUpperInvariant();
                Assert(actualMode == mode, mode + "_journal_mode");

                using (var cmd = source.CreateCommand())
                {
                    cmd.CommandText = File.ReadAllText(schemaPath);
                    cmd.ExecuteNonQuery();
                }
                Assert(Convert.ToInt32(Scalar(source, "SELECT count(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';"), CultureInfo.InvariantCulture) == 18, mode + "_schema_18_tables");
                Assert(Convert.ToString(Scalar(source, "SELECT value FROM app_meta WHERE key='schema_version';"), CultureInfo.InvariantCulture) == "1", mode + "_schema_version_1");

                InsertLearningFixture(source);
                Assert(Convert.ToInt32(Scalar(source, "SELECT count(*) FROM attempt;"), CultureInfo.InvariantCulture) == 1, mode + "_attempt_committed");
                Assert(Convert.ToInt32(Scalar(source, "SELECT count(*) FROM reward_event;"), CultureInfo.InvariantCulture) == 1, mode + "_reward_committed");
                AssertRewardIdempotency(source, mode);

                if (mode == "WAL")
                    Assert(File.Exists(db + "-wal"), "wal_sidecar_present_while_connection_open");

                var backupCs = "Data Source=" + backup + ";Version=3;Foreign Keys=True;Pooling=False;";
                using (var dest = new SQLiteConnection(backupCs))
                {
                    dest.Open();
                    source.BackupDatabase(dest, "main", "main", -1, null, 0);
                }
                Assert(File.Exists(backup), mode + "_online_backup_created");
                VerifyDatabase(backup, mode + "_backup");
            }

            VerifyDatabase(db, mode + "_source_after_close");
        }


        private static void TestDataService(string root, string schemaPath)
        {
            var dbPath = Path.Combine(root, "service-learning.db");
            var backupPath = Path.Combine(root, "service-backup.db");
            var database = new LearningDatabase(dbPath, schemaPath);
            var init = database.Initialize("DELETE");
            Assert(init.CreatedSchema, "service_schema_created");
            Assert(init.Health.IsHealthy, "service_initial_health");
            Assert(init.ProviderVersion == "2.0.4.0", "service_provider_version");
            Assert(init.SQLiteVersion.StartsWith("3.53.4", StringComparison.Ordinal), "service_sqlite_version");
            Assert(init.JournalMode == "DELETE", "service_safe_default_delete");
            Assert(init.SchemaVersion == 2, "service_schema_version_2");
            Assert(init.Migration != null && init.Migration.Version == 2, "service_latest_migration_v2");

            using (var c = database.OpenConnection()) InsertLearningFixture(c);
            var backup = SQLiteBackupService.CreateVerifiedBackup(database, backupPath);
            Assert(File.Exists(backup.Path), "service_backup_exists");
            Assert(backup.Health.IsHealthy, "service_backup_health");
            Assert(!string.IsNullOrWhiteSpace(backup.Sha256) && backup.Sha256.Length == 64, "service_backup_sha256");

            using (var c = database.OpenConnection())
            {
                Exec(c, null, "DELETE FROM attempt; DELETE FROM reward_event;");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM attempt;"), CultureInfo.InvariantCulture) == 0, "service_mutation_before_restore");
            }

            var restored = SQLiteBackupService.RestoreVerifiedBackup(backupPath, dbPath);
            Assert(restored.Health.IsHealthy, "service_restore_health");
            Assert(!string.IsNullOrWhiteSpace(restored.PreservedOriginalPath) && File.Exists(restored.PreservedOriginalPath), "service_original_preserved");
            using (var c = database.OpenConnection())
            {
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM attempt;"), CultureInfo.InvariantCulture) == 1, "service_restore_attempt");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM reward_event;"), CultureInfo.InvariantCulture) == 1, "service_restore_reward");
            }
        }

        private static void TestMigrationChecksumAndCoordinator(string root, string schemaPath)
        {
            var migrationDir = Path.Combine(root, "migration-coordinator-schema");
            Directory.CreateDirectory(migrationDir);
            var schemaCopy = Path.Combine(migrationDir, "001_initial.sql");
            var sourceV2 = Path.Combine(Path.GetDirectoryName(schemaPath), "002_attempt_immutability.sql");
            var migrationV2Copy = Path.Combine(migrationDir, "002_attempt_immutability.sql");
            File.Copy(schemaPath, schemaCopy, true);
            File.Copy(sourceV2, migrationV2Copy, true);
            var dbPath = Path.Combine(root, "migration-coordinator.db");
            var database = new LearningDatabase(dbPath, schemaCopy);

            var first = database.Initialize("DELETE");
            Assert(first.SchemaVersion == 2, "migration_fresh_boot_schema_v2");
            Assert(first.Migration != null && first.Migration.Version == 2 && first.Migration.RecordedNow, "migration_v2_recorded_first_boot");
            Assert(first.Migration.ChecksumSha256.Length == 64, "migration_v2_checksum_sha256");
            using (var c = database.OpenConnection())
            {
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM migration_history;"), CultureInfo.InvariantCulture) == 2, "migration_history_two_rows");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM migration_history WHERE version=1;"), CultureInfo.InvariantCulture) == 1, "migration_history_v1_one_row");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM migration_history WHERE version=2;"), CultureInfo.InvariantCulture) == 1, "migration_history_v2_one_row");
                Assert(Convert.ToString(Scalar(c, "SELECT checksum_sha256 FROM migration_history WHERE version=2;"), CultureInfo.InvariantCulture) == first.Migration.ChecksumSha256, "migration_history_v2_checksum_matches");
            }

            var second = database.Initialize("DELETE");
            Assert(second.SchemaVersion == 2 && second.Migration != null && second.Migration.Version == 2 && !second.Migration.RecordedNow, "migration_v2_not_duplicated_second_boot");

            database.Writes.Execute((c, tx) =>
            {
                Exec(c, tx, "INSERT INTO child(id,display_name,grade_level,created_at_utc,updated_at_utc) VALUES('coordinator-child','Bé coordinator',2,@t,@t);", "@t", DateTime.UtcNow.ToString("o"));
            });

            int active = 0;
            int maxActive = 0;
            const int writerCount = 12;
            var tasks = new Task[writerCount];
            for (int i = 0; i < writerCount; i++)
            {
                var index = i;
                tasks[i] = Task.Run(() =>
                {
                    database.Writes.Execute((c, tx) =>
                    {
                        var nowActive = Interlocked.Increment(ref active);
                        UpdateMax(ref maxActive, nowActive);
                        try
                        {
                            Thread.Sleep(8);
                            Exec(c, tx,
                                "INSERT INTO parent_note(id,child_id,note,created_at_utc,updated_at_utc) VALUES(@id,'coordinator-child',@note,@t,@t);",
                                "@id", "note-" + index,
                                "@note", "serialized-" + index,
                                "@t", DateTime.UtcNow.ToString("o"));
                        }
                        finally
                        {
                            Interlocked.Decrement(ref active);
                        }
                    });
                });
            }
            Task.WaitAll(tasks);
            Assert(maxActive == 1, "write_coordinator_max_one_active_writer");
            using (var c = database.OpenConnection())
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM parent_note WHERE child_id='coordinator-child';"), CultureInfo.InvariantCulture) == writerCount, "write_coordinator_all_committed");

            bool rollbackObserved = false;
            try
            {
                database.Writes.Execute((c, tx) =>
                {
                    Exec(c, tx,
                        "INSERT INTO parent_note(id,child_id,note,created_at_utc,updated_at_utc) VALUES('rollback-note','coordinator-child','should rollback',@t,@t);",
                        "@t", DateTime.UtcNow.ToString("o"));
                    throw new InvalidOperationException("intentional rollback smoke");
                });
            }
            catch (InvalidOperationException) { rollbackObserved = true; }
            Assert(rollbackObserved, "write_coordinator_exception_propagated");
            using (var c = database.OpenConnection())
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM parent_note WHERE id='rollback-note';"), CultureInfo.InvariantCulture) == 0, "write_coordinator_transaction_rolled_back");

            File.AppendAllText(migrationV2Copy, Environment.NewLine + "-- intentional v2 checksum tamper" + Environment.NewLine);
            bool v2TamperRejected = false;
            try { database.Initialize("DELETE"); }
            catch (InvalidDataException) { v2TamperRejected = true; }
            Assert(v2TamperRejected, "migration_v2_checksum_tamper_rejected");

            File.Copy(sourceV2, migrationV2Copy, true);
            File.AppendAllText(schemaCopy, Environment.NewLine + "-- intentional v1 checksum tamper" + Environment.NewLine);
            bool v1TamperRejected = false;
            try { database.Initialize("DELETE"); }
            catch (InvalidDataException) { v1TamperRejected = true; }
            Assert(v1TamperRejected, "migration_v1_checksum_tamper_rejected");
        }

        private static void UpdateMax(ref int target, int value)
        {
            while (true)
            {
                var current = target;
                if (value <= current) return;
                if (Interlocked.CompareExchange(ref target, value, current) == current) return;
            }
        }


        private static void TestManagedBackupLifecycle(string root, string schemaPath)
        {
            var dbPath = Path.Combine(root, "managed-backup-learning.db");
            var backupsRoot = Path.Combine(root, "managed-backups");
            var manualRoot = Path.Combine(root, "managed-manual-export");
            var database = new LearningDatabase(dbPath, schemaPath);
            var init = database.Initialize("DELETE");
            Assert(init.Health.IsHealthy, "managed_backup_db_ready");
            using (var c = database.OpenConnection())
            {
                InsertLearningFixture(c);
                var now = DateTime.UtcNow.ToString("o");
                Exec(c, null, @"INSERT INTO content_pack_state(
pack_id,version,subject,grade,install_source,status,active,manifest_sha256,relative_path,installed_at_utc,last_verified_at_utc)
VALUES('math_grade2_v1','1','math',2,'builtin','VERIFIED',1,'ABCDEF','content_packs/math_grade2_v1',@t,@t);", "@t", now);
            }

            var start = new DateTime(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);
            ManagedBackupArtifact latestRecent = null;
            ManagedBackupArtifact preMigration = null;
            for (int i = 0; i < 8; i++)
            {
                if (i == 6)
                {
                    preMigration = database.CreatePreMigrationBackup(backupsRoot, "0.1-test", 1, 2);
                    Assert(File.Exists(preMigration.DatabasePath), "pre_migration_db_created");
                    Assert(File.Exists(preMigration.MetadataPath), "pre_migration_metadata_created");
                    Assert(ManagedBackupService.VerifyManagedBackup(preMigration.MetadataPath).IsValid, "pre_migration_metadata_verified");
                }

                var set = database.CreateAutomaticBackupSetAtUtc(
                    backupsRoot, "0.1-test", 5, 4, start.AddDays(i * 7));
                latestRecent = set.Recent;
                Assert(set.Recent != null && File.Exists(set.Recent.DatabasePath), "managed_recent_created_" + i);
                Assert(set.Weekly != null && File.Exists(set.Weekly.DatabasePath), "managed_weekly_created_" + i);
            }

            Assert(Directory.GetFiles(Path.Combine(backupsRoot, "recent"), "*.db").Length == 5, "managed_recent_rotation_5_db");
            Assert(Directory.GetFiles(Path.Combine(backupsRoot, "recent"), "*.json").Length == 5, "managed_recent_rotation_5_metadata");
            Assert(Directory.GetFiles(Path.Combine(backupsRoot, "weekly"), "*.db").Length == 4, "managed_weekly_rotation_4_db");
            Assert(Directory.GetFiles(Path.Combine(backupsRoot, "weekly"), "*.json").Length == 4, "managed_weekly_rotation_4_metadata");
            Assert(preMigration != null && File.Exists(preMigration.DatabasePath), "pre_migration_survives_rotation_db");
            Assert(File.Exists(preMigration.MetadataPath), "pre_migration_survives_rotation_metadata");

            var verified = ManagedBackupService.VerifyManagedBackup(latestRecent.MetadataPath);
            Assert(verified.IsValid, "managed_latest_metadata_valid");
            Assert(verified.SchemaVersion == 2, "managed_latest_schema_v2");
            Assert(string.Equals(verified.ActualSha256, latestRecent.Sha256, StringComparison.OrdinalIgnoreCase), "managed_latest_sha_matches");
            var metadataText = File.ReadAllText(latestRecent.MetadataPath);
            Assert(metadataText.Contains("math_grade2_v1@1"), "managed_metadata_active_pack_recorded");

            using (var c = database.OpenConnection())
            {
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM backup_history WHERE backup_type='automatic';"), CultureInfo.InvariantCulture) == 8, "backup_history_automatic_8");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM backup_history WHERE backup_type='weekly';"), CultureInfo.InvariantCulture) == 8, "backup_history_weekly_8");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM backup_history WHERE backup_type='pre_migration';"), CultureInfo.InvariantCulture) == 1, "backup_history_pre_migration_1");
            }

            var manual = database.CreateManualBackup(manualRoot, "0.1-test");
            Assert(File.Exists(manual.DatabasePath) && File.Exists(manual.MetadataPath), "manual_backup_pair_created");
            Assert(ManagedBackupService.VerifyManagedBackup(manual.MetadataPath).IsValid, "manual_backup_verified");

            var tamperDir = Path.Combine(root, "managed-backup-tamper");
            Directory.CreateDirectory(tamperDir);
            var tamperMeta = Path.Combine(tamperDir, Path.GetFileName(latestRecent.MetadataPath));
            var tamperDb = Path.Combine(tamperDir, Path.GetFileName(latestRecent.DatabasePath));
            File.Copy(latestRecent.MetadataPath, tamperMeta, true);
            File.Copy(latestRecent.DatabasePath, tamperDb, true);
            var tampered = File.ReadAllText(tamperMeta).Replace(latestRecent.Sha256, new string('0', 64));
            File.WriteAllText(tamperMeta, tampered);
            bool hashTamperRejected = false;
            try { ManagedBackupService.VerifyManagedBackup(tamperMeta); }
            catch (InvalidDataException) { hashTamperRejected = true; }
            Assert(hashTamperRejected, "managed_backup_hash_tamper_rejected");

            using (var c = database.OpenConnection())
            {
                Exec(c, null, "INSERT INTO parent_note(id,child_id,note,created_at_utc,updated_at_utc) VALUES('post-backup-note','child-1','must disappear on restore',@t,@t);", "@t", DateTime.UtcNow.ToString("o"));
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM parent_note WHERE id='post-backup-note';"), CultureInfo.InvariantCulture) == 1, "managed_restore_fixture_mutated");
            }

            var restored = database.RestoreManagedBackup(latestRecent.MetadataPath);
            Assert(restored.Health.IsHealthy, "managed_restore_health");
            Assert(!string.IsNullOrWhiteSpace(restored.PreservedOriginalPath) && File.Exists(restored.PreservedOriginalPath), "managed_restore_preserved_original");
            using (var c = database.OpenConnection())
            {
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM parent_note WHERE id='post-backup-note';"), CultureInfo.InvariantCulture) == 0, "managed_restore_reverted_post_backup_mutation");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM attempt;"), CultureInfo.InvariantCulture) == 1, "managed_restore_learning_data_preserved");
            }
        }


        private static void TestExistingV1MigrationWithPreBackup(string root, string schemaPath)
        {
            var migrationDir = Path.Combine(root, "existing-v1-schema");
            Directory.CreateDirectory(migrationDir);
            var schemaV1 = Path.Combine(migrationDir, "001_initial.sql");
            var schemaV2 = Path.Combine(migrationDir, "002_attempt_immutability.sql");
            File.Copy(schemaPath, schemaV1, true);
            File.Copy(Path.Combine(Path.GetDirectoryName(schemaPath), "002_attempt_immutability.sql"), schemaV2, true);

            var dbPath = Path.Combine(root, "existing-v1-learning.db");
            using (var c = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;Foreign Keys=True;Pooling=False;"))
            {
                c.Open();
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = File.ReadAllText(schemaV1);
                    cmd.ExecuteNonQuery();
                }
                MigrationManager.VerifyOrRecordInitial(c, schemaV1);
                InsertLearningFixture(c);
                Assert(MigrationManager.GetSchemaVersion(c) == 1, "existing_v1_fixture_schema_1");
            }

            var database = new LearningDatabase(dbPath, schemaV1);
            bool refusedWithoutBackup = false;
            try { database.Initialize("DELETE"); }
            catch (InvalidOperationException) { refusedWithoutBackup = true; }
            Assert(refusedWithoutBackup, "existing_v1_migration_refused_without_backup_context");

            using (var c = database.OpenConnection())
            {
                Assert(MigrationManager.GetSchemaVersion(c) == 1, "refused_migration_keeps_schema_v1");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM attempt;"), CultureInfo.InvariantCulture) == 1, "refused_migration_keeps_attempt");
            }

            var backupRoot = Path.Combine(root, "existing-v1-pre-migration-backups");
            var migrated = database.Initialize("DELETE", backupRoot, "0.1-test");
            Assert(migrated.SchemaVersion == 2, "existing_v1_migrated_to_v2");
            Assert(migrated.Migration != null && migrated.Migration.Version == 2, "existing_v1_latest_migration_v2");
            Assert(migrated.PreMigrationBackup != null, "existing_v1_pre_migration_backup_returned");
            Assert(File.Exists(migrated.PreMigrationBackup.DatabasePath), "existing_v1_pre_migration_db_exists");
            Assert(File.Exists(migrated.PreMigrationBackup.MetadataPath), "existing_v1_pre_migration_metadata_exists");
            var verifiedBackup = ManagedBackupService.VerifyManagedBackup(migrated.PreMigrationBackup.MetadataPath);
            Assert(verifiedBackup.IsValid && verifiedBackup.SchemaVersion == 1, "existing_v1_pre_migration_backup_is_schema1");

            using (var c = database.OpenConnection())
            {
                Assert(MigrationManager.GetSchemaVersion(c) == 2, "existing_v1_db_now_schema2");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM migration_history WHERE version=2;"), CultureInfo.InvariantCulture) == 1, "existing_v1_migration_history_v2");

                bool updateRejected = false;
                try { Exec(c, null, "UPDATE attempt SET response_ms=999 WHERE id='attempt-1';"); }
                catch (SQLiteException) { updateRejected = true; }
                Assert(updateRejected, "attempt_update_rejected_by_v2_trigger");
                Assert(Convert.ToInt32(Scalar(c, "SELECT response_ms FROM attempt WHERE id='attempt-1';"), CultureInfo.InvariantCulture) == 1200, "attempt_original_value_unchanged");

                Exec(c, null, @"INSERT INTO attempt_correction_event(id,attempt_id,correction_type,before_json,after_json,reason,created_at_utc)
VALUES('corr-1','attempt-1','metadata_fix','{""response_ms"":1200}','{""response_ms"":999}','test correction path',@t);", "@t", DateTime.UtcNow.ToString("o"));
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM attempt_correction_event WHERE attempt_id='attempt-1';"), CultureInfo.InvariantCulture) == 1, "attempt_correction_event_append_allowed");
            }
        }


        private static void TestAnswerCommitAtomicity(string root, string schemaPath)
        {
            var dbPath = Path.Combine(root, "answer-commit-learning.db");
            var database = new LearningDatabase(dbPath, schemaPath);
            var init = database.Initialize("DELETE");
            Assert(init.SchemaVersion == 2, "answer_commit_schema_v2_ready");

            var now = DateTime.UtcNow;
            using (var c = database.OpenConnection())
            {
                Exec(c, null,
                    "INSERT INTO child(id,display_name,grade_level,created_at_utc,updated_at_utc) VALUES('answer-child','Bé answer',2,@t,@t);",
                    "@t", now.ToString("o"));
                Exec(c, null,
                    "INSERT INTO session(id,child_id,started_at_utc,state,planned_subject,performance_profile) VALUES('answer-session','answer-child',@t,'active','math','LOW');",
                    "@t", now.ToString("o"));
            }

            var service = new AnswerCommitService(database);
            var success = new AnswerCommitRequest
            {
                AttemptId = "answer-attempt-1",
                SessionId = "answer-session",
                ChildId = "answer-child",
                PackId = "math_grade2_v1",
                PackVersion = "1",
                QuestionId = "q-answer-1",
                SkillId = "ADD_2DIGIT_WITH_CARRY",
                Subject = "math",
                StartedAtUtc = now.AddSeconds(-3),
                AnsweredAtUtc = now,
                AnswerJson = "{\"answer\":84}",
                IsCorrect = false,
                ResponseMs = 3000,
                HintLevel = 0,
                Representation = "symbolic",
                InputMethod = "mouse",
                AttemptIndex = 1,
                ListenCount = 0,
                Error = new ErrorEventWrite
                {
                    Id = "answer-error-1",
                    ErrorType = "CARRY_MISSING",
                    Confidence = 0.91,
                    EvidenceJson = "{\"expected\":85,\"actual\":84}",
                    ClassifierVersion = "error-v1"
                },
                Mastery = new MasteryEventWrite
                {
                    Id = "answer-mastery-1",
                    EventType = "attempt_result",
                    Delta = -0.05,
                    ScoreBefore = 0.60,
                    ScoreAfter = 0.55,
                    ConfidenceAfter = 0.72,
                    ReasonJson = "{\"reason\":\"carry_missing\"}",
                    MasteryEngineVersion = "mastery-v1"
                },
                ChildSkill = new ChildSkillWrite
                {
                    MasteryScore = 0.55,
                    Confidence = 0.72,
                    AttemptsCount = 1,
                    IndependentSuccessCount = 0,
                    HintedSuccessCount = 0,
                    TransferSuccessCount = 0,
                    LastSeenAtUtc = now,
                    NextReviewAtUtc = now.AddHours(4),
                    LearningState = "REPAIR",
                    MasteryEngineVersion = "mastery-v1"
                },
                Review = new ReviewScheduleWrite
                {
                    DueAtUtc = now.AddHours(4),
                    IntervalDays = 0.17,
                    Reason = "repair_after_error",
                    SchedulerVersion = "scheduler-v1"
                }
            };

            var committed = service.Commit(success);
            Assert(committed.AttemptId == "answer-attempt-1", "answer_commit_returns_attempt_id");
            Assert(committed.ErrorWritten && committed.MasteryWritten && committed.ChildSkillWritten && committed.ReviewWritten, "answer_commit_all_components_written");
            using (var c = database.OpenConnection())
            {
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM attempt WHERE id='answer-attempt-1';"), CultureInfo.InvariantCulture) == 1, "answer_commit_attempt_written");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM error_event WHERE id='answer-error-1';"), CultureInfo.InvariantCulture) == 1, "answer_commit_error_written");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM mastery_event WHERE id='answer-mastery-1';"), CultureInfo.InvariantCulture) == 1, "answer_commit_mastery_written");
                Assert(Math.Abs(Convert.ToDouble(Scalar(c, "SELECT mastery_score FROM child_skill WHERE child_id='answer-child' AND skill_id='ADD_2DIGIT_WITH_CARRY';"), CultureInfo.InvariantCulture) - 0.55) < 0.0001, "answer_commit_child_skill_updated");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM review_schedule WHERE child_id='answer-child' AND skill_id='ADD_2DIGIT_WITH_CARRY';"), CultureInfo.InvariantCulture) == 1, "answer_commit_review_written");
            }

            service.RecordCorrection(new AttemptCorrectionWrite
            {
                Id = "answer-correction-1",
                AttemptId = "answer-attempt-1",
                CorrectionType = "annotation",
                BeforeJson = "{\"note\":null}",
                AfterJson = "{\"note\":\"parent verified\"}",
                Reason = "append-only correction smoke",
                CreatedAtUtc = now.AddSeconds(1)
            });
            using (var c = database.OpenConnection())
            {
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM attempt_correction_event WHERE id='answer-correction-1';"), CultureInfo.InvariantCulture) == 1, "answer_commit_correction_append_written");
                bool updateRejected = false;
                try { Exec(c, null, "UPDATE attempt SET response_ms=1 WHERE id='answer-attempt-1';"); }
                catch (SQLiteException) { updateRejected = true; }
                Assert(updateRejected, "answer_commit_attempt_update_still_immutable");
            }

            var fail = new AnswerCommitRequest
            {
                AttemptId = "answer-attempt-fail",
                SessionId = "answer-session",
                ChildId = "answer-child",
                PackId = "math_grade2_v1",
                PackVersion = "1",
                QuestionId = "q-answer-fail",
                SkillId = "SUB_2DIGIT_WITH_BORROW",
                Subject = "math",
                StartedAtUtc = now.AddSeconds(2),
                AnsweredAtUtc = now.AddSeconds(4),
                AnswerJson = "{\"answer\":31}",
                IsCorrect = true,
                ResponseMs = 2000,
                HintLevel = 0,
                Representation = "symbolic",
                InputMethod = "mouse",
                AttemptIndex = 1,
                ListenCount = 0,
                Mastery = new MasteryEventWrite
                {
                    Id = "answer-mastery-fail",
                    EventType = "attempt_result",
                    Delta = 0.10,
                    ScoreBefore = 0.50,
                    ScoreAfter = 0.60,
                    ConfidenceAfter = 0.80,
                    ReasonJson = "{\"reason\":\"correct\"}",
                    MasteryEngineVersion = "mastery-v1"
                },
                ChildSkill = new ChildSkillWrite
                {
                    MasteryScore = 1.50,
                    Confidence = 0.80,
                    AttemptsCount = 1,
                    IndependentSuccessCount = 1,
                    HintedSuccessCount = 0,
                    TransferSuccessCount = 0,
                    LastSeenAtUtc = now.AddSeconds(4),
                    LastSuccessAtUtc = now.AddSeconds(4),
                    NextReviewAtUtc = now.AddDays(1),
                    LearningState = "LEARNING",
                    MasteryEngineVersion = "mastery-v1"
                },
                Review = new ReviewScheduleWrite
                {
                    DueAtUtc = now.AddDays(1),
                    IntervalDays = 1,
                    Reason = "normal_review",
                    SchedulerVersion = "scheduler-v1"
                }
            };

            bool lateConstraintFailed = false;
            try { service.Commit(fail); }
            catch (SQLiteException) { lateConstraintFailed = true; }
            Assert(lateConstraintFailed, "answer_commit_late_constraint_failure_observed");
            using (var c = database.OpenConnection())
            {
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM attempt WHERE id='answer-attempt-fail';"), CultureInfo.InvariantCulture) == 0, "answer_commit_failed_attempt_rolled_back");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM mastery_event WHERE id='answer-mastery-fail';"), CultureInfo.InvariantCulture) == 0, "answer_commit_failed_mastery_rolled_back");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM child_skill WHERE child_id='answer-child' AND skill_id='SUB_2DIGIT_WITH_BORROW';"), CultureInfo.InvariantCulture) == 0, "answer_commit_failed_skill_rolled_back");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM review_schedule WHERE child_id='answer-child' AND skill_id='SUB_2DIGIT_WITH_BORROW';"), CultureInfo.InvariantCulture) == 0, "answer_commit_failed_review_absent");
            }
        }

        private static void InsertLearningFixture(SQLiteConnection c)
        {
            var now = DateTime.UtcNow.ToString("o");
            using (var tx = c.BeginTransaction())
            {
                Exec(c, tx, "INSERT INTO child(id,display_name,grade_level,created_at_utc,updated_at_utc) VALUES(@id,@n,2,@t,@t);", "@id", "child-1", "@n", "Bé thử", "@t", now);
                Exec(c, tx, "INSERT INTO session(id,child_id,started_at_utc,state,planned_subject,performance_profile) VALUES(@id,@c,@t,'active','math','LOW');", "@id", "session-1", "@c", "child-1", "@t", now);
                Exec(c, tx, "INSERT INTO attempt(id,session_id,child_id,pack_id,pack_version,question_id,skill_id,subject,started_at_utc,answered_at_utc,answer_json,is_correct,response_ms,hint_level,attempt_index,listen_count) VALUES(@id,@s,@c,'math_grade2_v1','1','q1','ADD_2DIGIT_WITH_CARRY','math',@t,@t,'{\"answer\":85}',1,1200,0,1,0);", "@id", "attempt-1", "@s", "session-1", "@c", "child-1", "@t", now);
                Exec(c, tx, "INSERT INTO reward_event(id,child_id,reward_type,reward_id,source_event,source_ref,source_key,created_at_utc) VALUES(@id,@c,'cosmetic','seed-1','attempt','attempt-1','attempt:attempt-1:seed-1',@t);", "@id", "reward-1", "@c", "child-1", "@t", now);
                tx.Commit();
            }
        }

        private static void AssertRewardIdempotency(SQLiteConnection c, string mode)
        {
            bool rejected = false;
            try
            {
                Exec(c, null, "INSERT INTO reward_event(id,child_id,reward_type,reward_id,source_event,source_ref,source_key,created_at_utc) VALUES('reward-duplicate','child-1','cosmetic','seed-1','attempt','attempt-1','attempt:attempt-1:seed-1',@t);", "@t", DateTime.UtcNow.ToString("o"));
            }
            catch (SQLiteException) { rejected = true; }
            Assert(rejected, mode + "_reward_source_key_unique");
        }

        private static void VerifyDatabase(string path, string name)
        {
            using (var c = new SQLiteConnection("Data Source=" + path + ";Version=3;Foreign Keys=True;Pooling=False;"))
            {
                c.Open();
                Assert(Convert.ToString(Scalar(c, "PRAGMA integrity_check;"), CultureInfo.InvariantCulture) == "ok", name + "_integrity_ok");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM pragma_foreign_key_check;"), CultureInfo.InvariantCulture) == 0, name + "_fk_ok");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM attempt;"), CultureInfo.InvariantCulture) == 1, name + "_attempt_preserved");
                Assert(Convert.ToInt32(Scalar(c, "SELECT count(*) FROM reward_event;"), CultureInfo.InvariantCulture) == 1, name + "_reward_preserved");
            }
        }

        private static object Scalar(SQLiteConnection c, string sql)
        {
            using (var cmd = c.CreateCommand()) { cmd.CommandText = sql; return cmd.ExecuteScalar(); }
        }

        private static void Exec(SQLiteConnection c, SQLiteTransaction tx, string sql, params object[] nameValues)
        {
            using (var cmd = c.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                for (int i = 0; i < nameValues.Length; i += 2) cmd.Parameters.AddWithValue((string)nameValues[i], nameValues[i + 1]);
                cmd.ExecuteNonQuery();
            }
        }

        private static string FindSchema()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "data", "schema", "001_initial.sql");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return "data\\schema\\001_initial.sql";
        }

        private static void Assert(bool value, string name)
        {
            if (!value) throw new InvalidOperationException(name);
            _passed++;
        }
    }
}

