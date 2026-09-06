using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using WAHU.Data;

namespace WAHU.MathDataEngineRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        private static int Main(string[] args)
        {
            if (args != null && args.Length > 0 && string.Equals(args[0], "--commit-worker", StringComparison.Ordinal))
                return RunCommitWorker(args);

            var root = Path.Combine(Path.GetTempPath(), "wahu-math-data-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var repo = FindRepoRoot();
                var sourceSchema = Path.Combine(repo, "data", "schema");
                TestFreshV3AndIdempotentCommit(root, sourceSchema);
                TestTerminalSessionRejectsNewAttemptsButAllowsExactReplay(root, sourceSchema);
                TestOptimisticSkillStateGuard(root, sourceSchema);
                TestCrossProcessConcurrentSkillWrites(root, sourceSchema);
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

        private static void TestTerminalSessionRejectsNewAttemptsButAllowsExactReplay(string root, string sourceSchema)
        {
            var schemaDir = Path.Combine(root, "schema-terminal-session");
            CopySchemas(sourceSchema, schemaDir);
            var database = new LearningDatabase(Path.Combine(root, "terminal-session.db"), Path.Combine(schemaDir, "001_initial.sql"));
            database.Initialize("DELETE");
            var sessions = new LearnerSessionService(database);
            var profile = sessions.EnsurePrimaryChild("Bé stale guard");
            var service = new AnswerCommitService(database);
            var now = DateTime.UtcNow;

            var completedSession = sessions.BeginSession(profile.ChildId, "math", "LOW");
            var durable = BuildRequest(profile.ChildId, completedSession.SessionId, "attempt-before-complete", "q-before-complete", "12", true, now);
            var first = service.Commit(durable);
            A(!first.AlreadyCommitted, "terminal_guard_fixture_commits_before_complete");
            sessions.CompleteSession(completedSession.SessionId, false, "{}", "{}");

            var replay = BuildRequest(profile.ChildId, completedSession.SessionId, "attempt-replay-after-complete", "q-before-complete", "12", true, now.AddSeconds(2));
            var replayResult = service.Commit(replay);
            A(replayResult.AlreadyCommitted && replayResult.AttemptId == "attempt-before-complete",
                "terminal_guard_exact_replay_after_complete_still_idempotent");

            var newAfterComplete = BuildRequest(profile.ChildId, completedSession.SessionId, "attempt-after-complete", "q-after-complete", "13", false, now.AddSeconds(4));
            var completedRejected = false;
            try { service.Commit(newAfterComplete); }
            catch (InvalidOperationException) { completedRejected = true; }
            A(completedRejected, "terminal_guard_rejects_new_attempt_after_complete");
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM attempt WHERE session_id='" + completedSession.SessionId + "';") == 1,
                    "terminal_guard_complete_keeps_attempt_count_unchanged");
                A(Count(c, "SELECT count(*) FROM attempt_commit_key WHERE session_id='" + completedSession.SessionId + "' AND question_id='q-after-complete';") == 0,
                    "terminal_guard_complete_writes_no_semantic_key_for_rejected_attempt");
                A(Count(c, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + completedSession.SessionId + "';") == 1,
                    "terminal_guard_complete_writes_no_extra_mastery");
            }

            var abortedSession = sessions.BeginSession(profile.ChildId, "math", "LOW");
            sessions.CompleteSession(abortedSession.SessionId, true, "{}", "{}");
            var newAfterAbort = BuildRequest(profile.ChildId, abortedSession.SessionId, "attempt-after-abort", "q-after-abort", "12", true, now.AddSeconds(6));
            var abortedRejected = false;
            try { service.Commit(newAfterAbort); }
            catch (InvalidOperationException) { abortedRejected = true; }
            A(abortedRejected, "terminal_guard_rejects_new_attempt_after_abort");
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM attempt WHERE session_id='" + abortedSession.SessionId + "';") == 0,
                    "terminal_guard_abort_writes_no_attempt");
                A(Count(c, "SELECT count(*) FROM attempt_commit_key WHERE session_id='" + abortedSession.SessionId + "';") == 0,
                    "terminal_guard_abort_writes_no_semantic_key");
                A(Count(c, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + abortedSession.SessionId + "';") == 0,
                    "terminal_guard_abort_writes_no_mastery");
            }
        }

        private static void TestOptimisticSkillStateGuard(string root, string sourceSchema)
        {
            var schemaDir = Path.Combine(root, "schema-optimistic-skill");
            CopySchemas(sourceSchema, schemaDir);
            var database = new LearningDatabase(Path.Combine(root, "optimistic-skill.db"), Path.Combine(schemaDir, "001_initial.sql"));
            database.Initialize("DELETE");
            var sessions = new LearnerSessionService(database);
            var profile = sessions.EnsurePrimaryChild("Bé optimistic guard");
            var session = sessions.BeginSession(profile.ChildId, "math", "LOW");
            var service = new AnswerCommitService(database);
            var now = DateTime.UtcNow;

            var first = BuildRequest(profile.ChildId, session.SessionId, "optimistic-attempt-1", "optimistic-q-1", "12", true, now);
            first.ExpectedSkillMasteryScore = 0.25;
            first.ExpectedSkillAttemptsCount = 0;
            var firstResult = service.Commit(first);
            A(!firstResult.AlreadyCommitted,
                "optimistic_guard_accepts_first_learning_write_from_empty_skill_state");

            var stale = BuildRequest(profile.ChildId, session.SessionId, "optimistic-attempt-stale", "optimistic-q-2", "13", false, now.AddSeconds(2));
            stale.ExpectedSkillMasteryScore = 0.25;
            stale.ExpectedSkillAttemptsCount = 0;
            var staleRejected = false;
            try { service.Commit(stale); }
            catch (InvalidOperationException) { staleRejected = true; }
            A(staleRejected,
                "optimistic_guard_rejects_distinct_attempt_from_stale_skill_snapshot");
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM attempt WHERE session_id='" + session.SessionId + "';") == 1,
                    "optimistic_guard_stale_rejection_rolls_back_attempt");
                A(Count(c, "SELECT count(*) FROM attempt_commit_key WHERE session_id='" + session.SessionId + "' AND question_id='optimistic-q-2';") == 0,
                    "optimistic_guard_stale_rejection_writes_no_semantic_key");
                A(Count(c, "SELECT count(*) FROM mastery_event WHERE child_id='" + profile.ChildId + "' AND skill_id='TEST_MATH_SKILL';") == 1,
                    "optimistic_guard_stale_rejection_writes_no_mastery");
            }

            var fresh = BuildRequest(profile.ChildId, session.SessionId, "optimistic-attempt-2", "optimistic-q-2", "12", true, now.AddSeconds(4));
            fresh.ExpectedSkillMasteryScore = 0.35;
            fresh.ExpectedSkillAttemptsCount = 1;
            fresh.Mastery.ScoreBefore = 0.35;
            fresh.Mastery.ScoreAfter = 0.45;
            fresh.Mastery.Delta = 0.10;
            fresh.ChildSkill.MasteryScore = 0.45;
            fresh.ChildSkill.AttemptsCount = 2;
            fresh.ChildSkill.IndependentSuccessCount = 2;
            var freshResult = service.Commit(fresh);
            A(!freshResult.AlreadyCommitted && freshResult.MasteryWritten && freshResult.ChildSkillWritten,
                "optimistic_guard_accepts_write_from_current_skill_snapshot");
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM attempt WHERE session_id='" + session.SessionId + "';") == 2,
                    "optimistic_guard_fresh_write_adds_second_attempt_once");
                A(Convert.ToInt32(Scalar(c, "SELECT attempts_count FROM child_skill WHERE child_id='" + profile.ChildId + "' AND skill_id='TEST_MATH_SKILL';"), CultureInfo.InvariantCulture) == 2,
                    "optimistic_guard_fresh_write_advances_skill_attempt_count");
                A(Math.Abs(Convert.ToDouble(Scalar(c, "SELECT mastery_score FROM child_skill WHERE child_id='" + profile.ChildId + "' AND skill_id='TEST_MATH_SKILL';"), CultureInfo.InvariantCulture) - 0.45) < 0.0000001,
                    "optimistic_guard_fresh_write_preserves_expected_mastery_chain");
            }

            var replay = BuildRequest(profile.ChildId, session.SessionId, "optimistic-replay", "optimistic-q-2", "12", true, now.AddSeconds(6));
            replay.ExpectedSkillMasteryScore = 0.35;
            replay.ExpectedSkillAttemptsCount = 1;
            replay.Mastery.ScoreBefore = 0.35;
            replay.Mastery.ScoreAfter = 0.45;
            replay.Mastery.Delta = 0.10;
            replay.ChildSkill.MasteryScore = 0.45;
            replay.ChildSkill.AttemptsCount = 2;
            replay.ChildSkill.IndependentSuccessCount = 2;
            var replayResult = service.Commit(replay);
            A(replayResult.AlreadyCommitted && replayResult.AttemptId == "optimistic-attempt-2",
                "optimistic_guard_exact_replay_precedes_stale_state_check");
        }

        private static void TestCrossProcessConcurrentSkillWrites(string root, string sourceSchema)
        {
            var schemaDir = Path.Combine(root, "schema-cross-process");
            CopySchemas(sourceSchema, schemaDir);
            var schemaPath = Path.Combine(schemaDir, "001_initial.sql");
            var dbPath = Path.Combine(root, "cross-process.db");
            var database = new LearningDatabase(dbPath, schemaPath);
            database.Initialize("DELETE");
            var sessions = new LearnerSessionService(database);
            var profile = sessions.EnsurePrimaryChild("Bé cross process");
            var session = sessions.BeginSession(profile.ChildId, "math", "LOW");
            var gatePath = Path.Combine(root, "cross-process-go.flag");
            var exePath = Assembly.GetExecutingAssembly().Location;

            using (var first = StartCommitWorker(exePath, dbPath, schemaPath, profile.ChildId, session.SessionId,
                "cross-attempt-a", "cross-question-a", gatePath))
            using (var second = StartCommitWorker(exePath, dbPath, schemaPath, profile.ChildId, session.SessionId,
                "cross-attempt-b", "cross-question-b", gatePath))
            {
                File.WriteAllText(gatePath, "go");
                var firstExited = first.WaitForExit(20000);
                var secondExited = second.WaitForExit(20000);
                if (!firstExited) { try { first.Kill(); } catch { } }
                if (!secondExited) { try { second.Kill(); } catch { } }
                var firstOutput = first.StandardOutput.ReadToEnd() + first.StandardError.ReadToEnd();
                var secondOutput = second.StandardOutput.ReadToEnd() + second.StandardError.ReadToEnd();
                A(firstExited && secondExited,
                    "cross_process_workers_exit_without_hanging_on_sqlite_lock");
                var oneCommitted = (first.ExitCode == 0 && second.ExitCode == 2) ||
                                   (first.ExitCode == 2 && second.ExitCode == 0);
                A(oneCommitted,
                    "cross_process_exactly_one_distinct_stale_skill_write_commits outputs=" + firstOutput.Trim() + " | " + secondOutput.Trim());
            }

            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM attempt WHERE session_id='" + session.SessionId + "';") == 1,
                    "cross_process_race_keeps_one_attempt");
                A(Count(c, "SELECT count(*) FROM attempt_commit_key WHERE session_id='" + session.SessionId + "';") == 1,
                    "cross_process_race_keeps_one_semantic_key");
                A(Count(c, "SELECT count(*) FROM mastery_event WHERE child_id='" + profile.ChildId + "' AND skill_id='TEST_MATH_SKILL';") == 1,
                    "cross_process_race_keeps_one_mastery_event");
                A(Convert.ToInt32(Scalar(c, "SELECT attempts_count FROM child_skill WHERE child_id='" + profile.ChildId + "' AND skill_id='TEST_MATH_SKILL';"), CultureInfo.InvariantCulture) == 1,
                    "cross_process_race_prevents_lost_update_attempt_counter");
                A(Math.Abs(Convert.ToDouble(Scalar(c, "SELECT mastery_score FROM child_skill WHERE child_id='" + profile.ChildId + "' AND skill_id='TEST_MATH_SKILL';"), CultureInfo.InvariantCulture) - 0.35) < 0.0000001,
                    "cross_process_race_preserves_single_mastery_transition");
                A(Count(c, "SELECT count(*) FROM review_schedule WHERE child_id='" + profile.ChildId + "' AND skill_id='TEST_MATH_SKILL';") == 1,
                    "cross_process_race_keeps_one_review_state");
            }
        }

        private static Process StartCommitWorker(string exePath, string dbPath, string schemaPath, string childId,
            string sessionId, string attemptId, string questionId, string gatePath)
        {
            var info = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = QuoteArg("--commit-worker") + " " + QuoteArg(dbPath) + " " + QuoteArg(schemaPath) + " " +
                    QuoteArg(childId) + " " + QuoteArg(sessionId) + " " + QuoteArg(attemptId) + " " +
                    QuoteArg(questionId) + " " + QuoteArg(gatePath),
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var process = Process.Start(info);
            if (process == null) throw new InvalidOperationException("Could not start MathData cross-process worker.");
            return process;
        }

        private static int RunCommitWorker(string[] args)
        {
            if (args == null || args.Length != 8) return 90;
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!File.Exists(args[7]) && DateTime.UtcNow < deadline) Thread.Sleep(10);
            if (!File.Exists(args[7])) return 91;

            try
            {
                var database = new LearningDatabase(args[1], args[2]);
                var request = BuildRequest(args[3], args[4], args[5], args[6], "12", true, DateTime.UtcNow);
                request.ExpectedSkillMasteryScore = 0.25;
                request.ExpectedSkillAttemptsCount = 0;
                new AnswerCommitService(database).Commit(request);
                Console.WriteLine("CROSS_PROCESS_WORKER_COMMITTED " + args[5]);
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine("CROSS_PROCESS_WORKER_REJECTED " + ex.Message);
                return 2;
            }
            catch (SQLiteException ex)
            {
                Console.WriteLine("CROSS_PROCESS_WORKER_REJECTED_SQLITE " + ex.ErrorCode);
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("CROSS_PROCESS_WORKER_FAIL " + ex);
                return 3;
            }
        }

        private static string QuoteArg(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
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
