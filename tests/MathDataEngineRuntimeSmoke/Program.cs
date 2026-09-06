using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using WAHU.Data;

namespace WAHU.MathDataEngineRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        private static int Main(string[] args)
        {
            var root = Path.Combine(Path.GetTempPath(), "wahu-math-data-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var repo = FindRepoRoot();
                var sourceSchema = Path.Combine(repo, "data", "schema");
                TestFreshV3AndIdempotentCommit(root, sourceSchema);
                TestExistingV1UpgradesToV3WithBackup(root, sourceSchema);
                TestV3BackfillPreservesLegacyDuplicates(root, sourceSchema);
                Console.WriteLine("MATH_DATA_ENGINE_RUNTIME_SMOKE_PASS assertions=" + _assertions);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("MATH_DATA_ENGINE_RUNTIME_SMOKE_FAIL " + ex);
                return 1;
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        private static void TestFreshV3AndIdempotentCommit(string root, string sourceSchema)
        {
            var schemaDir = Path.Combine(root, "schema-fresh");
            CopySchemas(sourceSchema, schemaDir);
            var database = new LearningDatabase(Path.Combine(root, "fresh.db"), Path.Combine(schemaDir, "001_initial.sql"));
            var init = database.Initialize("DELETE");
            A(init.SchemaVersion == 4, "fresh_schema_v4");
            A(init.Migration != null && init.Migration.Version == 4, "fresh_latest_migration_v4");
            A(init.Health != null && init.Health.IsHealthy, "fresh_health_ok");

            var sessions = new LearnerSessionService(database);
            var profile = sessions.EnsurePrimaryChild("Bé idempotent");
            var session = sessions.BeginSession(profile.ChildId, "math", "LOW");
            var now = DateTime.UtcNow;
            var service = new AnswerCommitService(database);
            var request = BuildRequest(profile.ChildId, session.SessionId, "attempt-original", "q-semantic-1", "12", true, now);

            var first = service.Commit(request);
            A(!first.AlreadyCommitted, "first_commit_not_replay");
            A(first.AttemptId == "attempt-original", "first_commit_attempt_id");
            A(first.MasteryWritten && first.ChildSkillWritten && first.ReviewWritten, "first_commit_learning_components");

            var replay = BuildRequest(profile.ChildId, session.SessionId, "attempt-replay-new-guid", "q-semantic-1", "12", true, now.AddSeconds(5));
            var second = service.Commit(replay);
            A(second.AlreadyCommitted, "same_payload_is_replay");
            A(second.AttemptId == "attempt-original", "replay_returns_durable_attempt_id");
            A(!second.MasteryWritten && !second.ChildSkillWritten && !second.ReviewWritten, "replay_writes_no_learning_components");

            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM attempt WHERE session_id='" + session.SessionId + "';") == 1, "replay_attempt_count_one");
                A(Count(c, "SELECT count(*) FROM attempt_commit_key WHERE session_id='" + session.SessionId + "';") == 1, "replay_semantic_key_count_one");
                A(Count(c, "SELECT count(*) FROM mastery_event WHERE child_id='" + profile.ChildId + "' AND skill_id='TEST_MATH_SKILL';") == 1, "replay_mastery_count_one");
                A(Convert.ToInt32(Scalar(c, "SELECT attempts_count FROM child_skill WHERE child_id='" + profile.ChildId + "' AND skill_id='TEST_MATH_SKILL';"), CultureInfo.InvariantCulture) == 1, "replay_child_skill_not_incremented");
            }

            var conflict = BuildRequest(profile.ChildId, session.SessionId, "attempt-conflict", "q-semantic-1", "13", false, now.AddSeconds(10));
            var conflictRejected = false;
            try { service.Commit(conflict); }
            catch (InvalidOperationException) { conflictRejected = true; }
            A(conflictRejected, "different_payload_same_semantic_key_rejected");
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM attempt WHERE session_id='" + session.SessionId + "';") == 1, "conflict_attempt_count_still_one");
                A(Count(c, "SELECT count(*) FROM mastery_event WHERE child_id='" + profile.ChildId + "' AND skill_id='TEST_MATH_SKILL';") == 1, "conflict_mastery_count_still_one");
            }

            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='math_session_runtime';") == 1, "runtime_table_exists");
                Exec(c, "INSERT INTO math_session_runtime(session_id,seed,target_question_count,generated_question_count,current_question_json,current_selection_json,question_started_at_utc,forced_repair_template_id,updated_at_utc) VALUES(@session,1234,8,2,'{\"QuestionId\":\"q-open\"}','{\"Score\":1.0}',@started,NULL,@updated);",
                    "@session", session.SessionId,
                    "@started", now.ToString("o", CultureInfo.InvariantCulture),
                    "@updated", now.ToString("o", CultureInfo.InvariantCulture));
                A(Count(c, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + session.SessionId + "' AND generated_question_count=2;") == 1, "runtime_state_insertable");
            }
        }

        private static void TestExistingV1UpgradesToV3WithBackup(string root, string sourceSchema)
        {
            var schemaDir = Path.Combine(root, "schema-v1-upgrade");
            CopySchemas(sourceSchema, schemaDir);
            var dbPath = Path.Combine(root, "legacy-v1-upgrade.db");
            using (var c = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;Foreign Keys=True;Pooling=False;"))
            {
                c.Open();
                using (var command = c.CreateCommand())
                {
                    command.CommandText = File.ReadAllText(Path.Combine(schemaDir, "001_initial.sql"));
                    command.ExecuteNonQuery();
                }
                MigrationManager.VerifyOrRecordInitial(c, Path.Combine(schemaDir, "001_initial.sql"));
                A(MigrationManager.GetSchemaVersion(c) == 1, "v1_upgrade_fixture_schema_one");
            }

            var database = new LearningDatabase(dbPath, Path.Combine(schemaDir, "001_initial.sql"));
            var refusedWithoutBackup = false;
            try { database.Initialize("DELETE"); }
            catch (InvalidOperationException) { refusedWithoutBackup = true; }
            A(refusedWithoutBackup, "v1_upgrade_requires_backup_context");

            var backupRoot = Path.Combine(root, "v1-upgrade-backups");
            var migrated = database.Initialize("DELETE", backupRoot, "ai2-smoke");
            A(migrated.SchemaVersion == 4, "v1_upgrade_reaches_schema_four");
            A(migrated.Migration != null && migrated.Migration.Version == 4, "v1_upgrade_latest_migration_four");
            A(migrated.PreMigrationBackup != null && File.Exists(migrated.PreMigrationBackup.DatabasePath), "v1_upgrade_prebackup_database_exists");
            A(migrated.PreMigrationBackup != null && File.Exists(migrated.PreMigrationBackup.MetadataPath), "v1_upgrade_prebackup_metadata_exists");
            var verified = ManagedBackupService.VerifyManagedBackup(migrated.PreMigrationBackup.MetadataPath);
            A(verified.IsValid && verified.SchemaVersion == 1, "v1_upgrade_prebackup_verifies_as_schema_one");
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM migration_history WHERE version=2;") == 1, "v1_upgrade_records_v2_once");
                A(Count(c, "SELECT count(*) FROM migration_history WHERE version=3;") == 1, "v1_upgrade_records_v3_once");
                A(Count(c, "SELECT count(*) FROM migration_history WHERE version=4;") == 1, "v1_upgrade_records_v4_once");
            }
        }

        private static void TestV3BackfillPreservesLegacyDuplicates(string root, string sourceSchema)
        {
            var schemaDir = Path.Combine(root, "schema-legacy");
            CopySchemas(sourceSchema, schemaDir);
            var dbPath = Path.Combine(root, "legacy-v2.db");
            using (var c = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;Foreign Keys=True;Pooling=False;"))
            {
                c.Open();
                using (var command = c.CreateCommand())
                {
                    command.CommandText = File.ReadAllText(Path.Combine(schemaDir, "001_initial.sql"));
                    command.ExecuteNonQuery();
                }
                MigrationManager.VerifyOrRecordInitial(c, Path.Combine(schemaDir, "001_initial.sql"));
                MigrationManager.ApplyMigration(c, 2, MigrationManager.AttemptImmutabilityName, Path.Combine(schemaDir, "002_attempt_immutability.sql"));
                A(MigrationManager.GetSchemaVersion(c) == 2, "legacy_fixture_schema_v2");

                var t0 = new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc);
                Exec(c, "INSERT INTO child(id,display_name,grade_level,created_at_utc,updated_at_utc) VALUES('legacy-child','Bé cũ',2,@t,@t);", "@t", t0.ToString("o"));
                Exec(c, "INSERT INTO session(id,child_id,started_at_utc,state,planned_subject,performance_profile) VALUES('legacy-session','legacy-child',@t,'active','math','LOW');", "@t", t0.ToString("o"));
                InsertLegacyAttempt(c, "legacy-attempt-a", t0.AddSeconds(1));
                InsertLegacyAttempt(c, "legacy-attempt-b", t0.AddSeconds(2));
                A(Count(c, "SELECT count(*) FROM attempt WHERE session_id='legacy-session';") == 2, "legacy_duplicate_attempts_exist_before_v3");

                var migrated = MigrationManager.ApplyMigration(c, 3, MigrationManager.MathAttemptRuntimeName, Path.Combine(schemaDir, "003_math_attempt_idempotency_runtime.sql"));
                A(migrated.Version == 3 && migrated.RecordedNow, "legacy_v3_migration_applied");
                A(MigrationManager.GetSchemaVersion(c) == 3, "legacy_schema_now_v3");
                A(Count(c, "SELECT count(*) FROM attempt WHERE session_id='legacy-session';") == 2, "legacy_attempt_history_preserved");
                A(Count(c, "SELECT count(*) FROM attempt_commit_key WHERE session_id='legacy-session' AND question_id='legacy-q' AND attempt_index=1;") == 1, "legacy_semantic_key_backfilled_once");
                A(Convert.ToString(Scalar(c, "SELECT attempt_id FROM attempt_commit_key WHERE session_id='legacy-session' AND question_id='legacy-q' AND attempt_index=1;"), CultureInfo.InvariantCulture) == "legacy-attempt-a", "legacy_backfill_pins_earliest_attempt");
            }
        }

        private static AnswerCommitRequest BuildRequest(string childId, string sessionId, string attemptId, string questionId, string answer, bool correct, DateTime answered)
        {
            return new AnswerCommitRequest
            {
                AttemptId = attemptId,
                SessionId = sessionId,
                ChildId = childId,
                PackId = "math_grade2_verified_templates_v1",
                PackVersion = "1.9.0",
                QuestionId = questionId,
                SkillId = "TEST_MATH_SKILL",
                Subject = "math",
                StartedAtUtc = answered.AddSeconds(-2),
                AnsweredAtUtc = answered,
                AnswerJson = "{\"answer\":\"" + answer + "\"}",
                IsCorrect = correct,
                ResponseMs = 2000,
                HintLevel = 0,
                Representation = "symbolic",
                InputMethod = "keyboard",
                AttemptIndex = 1,
                ListenCount = 0,
                Mastery = new MasteryEventWrite
                {
                    Id = "mastery-" + attemptId,
                    EventType = "attempt_result",
                    Delta = correct ? 0.10 : -0.05,
                    ScoreBefore = 0.25,
                    ScoreAfter = correct ? 0.35 : 0.20,
                    ConfidenceAfter = 0.30,
                    ReasonJson = "{\"smoke\":true}",
                    MasteryEngineVersion = "mastery-v1"
                },
                ChildSkill = new ChildSkillWrite
                {
                    MasteryScore = correct ? 0.35 : 0.20,
                    Confidence = 0.30,
                    AttemptsCount = 1,
                    IndependentSuccessCount = correct ? 1 : 0,
                    HintedSuccessCount = 0,
                    TransferSuccessCount = 0,
                    LastSeenAtUtc = answered,
                    LastSuccessAtUtc = correct ? (DateTime?)answered : null,
                    NextReviewAtUtc = answered.AddDays(1),
                    LearningState = "LEARNING",
                    MasteryEngineVersion = "mastery-v1"
                },
                Review = new ReviewScheduleWrite
                {
                    DueAtUtc = answered.AddDays(1),
                    IntervalDays = 1,
                    Reason = "smoke",
                    SchedulerVersion = "review-v1"
                }
            };
        }

        private static void InsertLegacyAttempt(SQLiteConnection c, string id, DateTime answered)
        {
            Exec(c, "INSERT INTO attempt(id,session_id,child_id,pack_id,pack_version,question_id,skill_id,subject,started_at_utc,answered_at_utc,answer_json,is_correct,response_ms,hint_level,representation,input_method,attempt_index,listen_count) VALUES(@id,'legacy-session','legacy-child','math_grade2_verified_templates_v1','1.8.0','legacy-q','LEGACY_SKILL','math',@started,@answered,'{\"answer\":\"12\"}',1,1000,0,'symbolic','keyboard',1,0);",
                "@id", id,
                "@started", answered.AddSeconds(-1).ToString("o", CultureInfo.InvariantCulture),
                "@answered", answered.ToString("o", CultureInfo.InvariantCulture));
        }

        private static void CopySchemas(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var name in new[] { "001_initial.sql", "002_attempt_immutability.sql", "003_math_attempt_idempotency_runtime.sql", "004_math_lesson_progress.sql" })
                File.Copy(Path.Combine(source, name), Path.Combine(destination, name), true);
        }

        private static string FindRepoRoot()
        {
            var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "WAHUKidsLearn.sln"))) return current.FullName;
                current = current.Parent;
            }
            current = new DirectoryInfo(Environment.CurrentDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "WAHUKidsLearn.sln"))) return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException("Repository root not found.");
        }

        private static int Count(SQLiteConnection c, string sql)
        {
            return Convert.ToInt32(Scalar(c, sql), CultureInfo.InvariantCulture);
        }

        private static object Scalar(SQLiteConnection c, string sql)
        {
            using (var command = c.CreateCommand()) { command.CommandText = sql; return command.ExecuteScalar(); }
        }

        private static void Exec(SQLiteConnection c, string sql, params object[] parameters)
        {
            using (var command = c.CreateCommand())
            {
                command.CommandText = sql;
                for (var i = 0; i + 1 < parameters.Length; i += 2)
                    command.Parameters.AddWithValue(Convert.ToString(parameters[i], CultureInfo.InvariantCulture), parameters[i + 1] ?? DBNull.Value);
                command.ExecuteNonQuery();
            }
        }

        private static void A(bool condition, string name)
        {
            if (!condition) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }
    }
}
