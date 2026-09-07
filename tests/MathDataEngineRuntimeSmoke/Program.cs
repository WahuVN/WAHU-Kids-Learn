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
            if (args != null && args.Length > 0 && string.Equals(args[0], "--session-worker", StringComparison.Ordinal))
                return RunSessionWorker(args);
            if (args != null && args.Length > 0 && string.Equals(args[0], "--runtime-session-worker", StringComparison.Ordinal))
                return RunRuntimeSessionWorker(args);
            if (args != null && args.Length > 0 && string.Equals(args[0], "--recovery-worker", StringComparison.Ordinal))
                return RunRecoveryWorker(args);
            if (args != null && args.Length > 0 && string.Equals(args[0], "--terminal-cleanup-worker", StringComparison.Ordinal))
                return RunTerminalCleanupWorker(args);

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
                TestCrossProcessSingleActiveSessionGuard(root, sourceSchema);
                TestCrossProcessAtomicMathRuntimeStart(root, sourceSchema);
                TestCrossProcessRecoveryDoesNotReclaimLiveRuntime(root, sourceSchema);
                TestCrossProcessRecoveryCannotSplitAtomicStart(root, sourceSchema);
                TestCrossProcessTerminalCleanupPreservesFreshRuntime(root, sourceSchema);
                TestDuplicateActiveRecoveryRequiresLiveKeeper(root, sourceSchema);
                TestRuntimePackIdentityArgumentValidation(root, sourceSchema);
                TestTargetedAtomicStartRollsBackOnProgressFailure(root, sourceSchema);
                TestDanglingRecoveryIsScopedToMath(root, sourceSchema);
                TestDanglingRecoveryIsScopedToChild(root, sourceSchema);
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
            A(init.SchemaVersion == 5, "fresh_schema_v5");
            A(init.Migration != null && init.Migration.Version == 5, "fresh_latest_migration_v5");
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

        private static void TestCrossProcessSingleActiveSessionGuard(string root, string sourceSchema)
        {
            var schemaDir = Path.Combine(root, "schema-cross-session");
            CopySchemas(sourceSchema, schemaDir);
            var schemaPath = Path.Combine(schemaDir, "001_initial.sql");
            var dbPath = Path.Combine(root, "cross-session.db");
            var database = new LearningDatabase(dbPath, schemaPath);
            database.Initialize("DELETE");
            var sessions = new LearnerSessionService(database);
            var profile = sessions.EnsurePrimaryChild("Bé single active");
            var gatePath = Path.Combine(root, "cross-session-go.flag");
            var exePath = Assembly.GetExecutingAssembly().Location;

            using (var first = StartSessionWorker(exePath, dbPath, schemaPath, profile.ChildId, gatePath))
            using (var second = StartSessionWorker(exePath, dbPath, schemaPath, profile.ChildId, gatePath))
            {
                File.WriteAllText(gatePath, "go");
                var firstExited = first.WaitForExit(20000);
                var secondExited = second.WaitForExit(20000);
                if (!firstExited) { try { first.Kill(); } catch { } }
                if (!secondExited) { try { second.Kill(); } catch { } }
                var firstOutput = first.StandardOutput.ReadToEnd() + first.StandardError.ReadToEnd();
                var secondOutput = second.StandardOutput.ReadToEnd() + second.StandardError.ReadToEnd();
                A(firstExited && secondExited,
                    "cross_session_workers_exit_without_hanging_on_sqlite_lock");
                var oneStarted = (first.ExitCode == 0 && second.ExitCode == 2) ||
                                 (first.ExitCode == 2 && second.ExitCode == 0);
                A(oneStarted,
                    "cross_session_exactly_one_cold_start_wins outputs=" + firstOutput.Trim() + " | " + secondOutput.Trim());
            }

            string activeSessionId;
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM session WHERE child_id='" + profile.ChildId + "' AND planned_subject='math' AND state IN ('started','active') AND ended_at_utc IS NULL;") == 1,
                    "cross_session_race_keeps_exactly_one_active_math_session");
                activeSessionId = Convert.ToString(Scalar(c,
                    "SELECT id FROM session WHERE child_id='" + profile.ChildId + "' AND planned_subject='math' AND state IN ('started','active') AND ended_at_utc IS NULL LIMIT 1;"),
                    CultureInfo.InvariantCulture);
                A(!string.IsNullOrWhiteSpace(activeSessionId),
                    "cross_session_winner_is_durable");
            }

            var sequentialRejected = false;
            try { sessions.BeginSession(profile.ChildId, "math", "LOW"); }
            catch (InvalidOperationException) { sequentialRejected = true; }
            A(sequentialRejected,
                "cross_session_guard_rejects_second_active_session_in_same_process_too");

            sessions.CompleteSession(activeSessionId, true, "{}", "{}");
            var next = sessions.BeginSession(profile.ChildId, "math", "LOW");
            A(next != null && !string.IsNullOrWhiteSpace(next.SessionId) && next.SessionId != activeSessionId,
                "cross_session_terminal_state_releases_slot_for_next_session");
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM session WHERE child_id='" + profile.ChildId + "' AND planned_subject='math' AND state IN ('started','active') AND ended_at_utc IS NULL;") == 1,
                    "cross_session_next_session_is_only_active_math_session");
            }
            sessions.CompleteSession(next.SessionId, true, "{}", "{}");
        }

        private static Process StartSessionWorker(string exePath, string dbPath, string schemaPath, string childId, string gatePath)
        {
            var info = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = QuoteArg("--session-worker") + " " + QuoteArg(dbPath) + " " + QuoteArg(schemaPath) + " " +
                    QuoteArg(childId) + " " + QuoteArg(gatePath),
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var process = Process.Start(info);
            if (process == null) throw new InvalidOperationException("Could not start MathData session worker.");
            return process;
        }

        private static int RunSessionWorker(string[] args)
        {
            if (args == null || args.Length != 5) return 92;
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!File.Exists(args[4]) && DateTime.UtcNow < deadline) Thread.Sleep(10);
            if (!File.Exists(args[4])) return 93;

            try
            {
                var database = new LearningDatabase(args[1], args[2]);
                var session = new LearnerSessionService(database).BeginSession(args[3], "math", "LOW");
                Console.WriteLine("CROSS_SESSION_WORKER_STARTED " + session.SessionId);
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine("CROSS_SESSION_WORKER_REJECTED " + ex.Message);
                return 2;
            }
            catch (SQLiteException ex)
            {
                Console.WriteLine("CROSS_SESSION_WORKER_REJECTED_SQLITE " + ex.ErrorCode);
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("CROSS_SESSION_WORKER_FAIL " + ex);
                return 3;
            }
        }

        private static void TestRuntimePackIdentityArgumentValidation(string root, string sourceSchema)
        {
            var schemaDir = Path.Combine(root, "schema-pack-identity-validation");
            CopySchemas(sourceSchema, schemaDir);
            var schemaPath = Path.Combine(schemaDir, "001_initial.sql");
            var database = new LearningDatabase(Path.Combine(root, "pack-identity-validation.db"), schemaPath);
            database.Initialize("DELETE");
            var profile = new LearnerSessionService(database).EnsurePrimaryChild("Bé pack identity validation");
            var runtime = new MathSessionRuntimeService(database);

            var blankRejected = false;
            try
            {
                runtime.TryCreateSession(profile.ChildId, "LOW", 5511, 8, "adaptive", null, null, " ", "\t");
            }
            catch (ArgumentException)
            {
                blankRejected = true;
            }
            A(blankRejected, "runtime_pack_identity_rejects_blank_pair");

            var partialRejected = false;
            try
            {
                runtime.TryCreateSession(profile.ChildId, "LOW", 5512, 8, "adaptive", null, null, "pack-only", null);
            }
            catch (ArgumentException)
            {
                partialRejected = true;
            }
            A(partialRejected, "runtime_pack_identity_rejects_partial_pair");

            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM session WHERE child_id='" + profile.ChildId + "' AND planned_subject='math';") == 0 &&
                  Count(c, "SELECT count(*) FROM math_session_runtime;") == 0,
                    "invalid_runtime_pack_identity_writes_nothing");
            }

            var valid = runtime.TryCreateSession(
                profile.ChildId, "LOW", 5513, 8, "adaptive", null, null, "math-pack-test", "1.0");
            A(valid != null, "runtime_pack_identity_accepts_complete_nonempty_pair");
            using (var c = database.OpenConnection())
            {
                A(Count(c,
                    "SELECT count(*) FROM math_session_runtime WHERE session_id='" + valid.SessionId + "' AND pack_id='math-pack-test' AND pack_version='1.0';") == 1,
                    "runtime_pack_identity_persists_pair_atomically");
            }
            new LearnerSessionService(database).CompleteSession(valid.SessionId, false, "{}", "{}");
            runtime.Delete(valid.SessionId);
        }
        private static void TestCrossProcessAtomicMathRuntimeStart(string root, string sourceSchema)
        {
            var schemaDir = Path.Combine(root, "schema-atomic-runtime-start");
            CopySchemas(sourceSchema, schemaDir);
            var schemaPath = Path.Combine(schemaDir, "001_initial.sql");
            var dbPath = Path.Combine(root, "atomic-runtime-start.db");
            var database = new LearningDatabase(dbPath, schemaPath);
            database.Initialize("DELETE");
            var sessions = new LearnerSessionService(database);
            var profile = sessions.EnsurePrimaryChild("Bé atomic runtime");
            var gatePath = Path.Combine(root, "atomic-runtime-go.flag");
            var exePath = Assembly.GetExecutingAssembly().Location;

            using (var first = StartRuntimeSessionWorker(exePath, dbPath, schemaPath, profile.ChildId, gatePath))
            using (var second = StartRuntimeSessionWorker(exePath, dbPath, schemaPath, profile.ChildId, gatePath))
            {
                File.WriteAllText(gatePath, "go");
                var firstExited = first.WaitForExit(20000);
                var secondExited = second.WaitForExit(20000);
                if (!firstExited) { try { first.Kill(); } catch { } }
                if (!secondExited) { try { second.Kill(); } catch { } }
                var firstOutput = first.StandardOutput.ReadToEnd() + first.StandardError.ReadToEnd();
                var secondOutput = second.StandardOutput.ReadToEnd() + second.StandardError.ReadToEnd();
                A(firstExited && secondExited,
                    "atomic_runtime_workers_exit_without_hanging");
                var oneCreated = (first.ExitCode == 0 && second.ExitCode == 2) ||
                                 (first.ExitCode == 2 && second.ExitCode == 0);
                A(oneCreated,
                    "atomic_runtime_exactly_one_cross_process_start_wins outputs=" + firstOutput.Trim() + " | " + secondOutput.Trim());
            }

            string activeSessionId;
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM session WHERE child_id='" + profile.ChildId + "' AND planned_subject='math' AND state IN ('started','active') AND ended_at_utc IS NULL;") == 1,
                    "atomic_runtime_has_one_active_math_session");
                A(Count(c, "SELECT count(*) FROM math_session_runtime r JOIN session s ON s.id=r.session_id WHERE s.child_id='" + profile.ChildId + "' AND s.state IN ('started','active') AND s.ended_at_utc IS NULL;") == 1,
                    "atomic_runtime_active_session_has_exactly_one_runtime");
                A(Count(c, "SELECT count(*) FROM session s LEFT JOIN math_session_runtime r ON r.session_id=s.id WHERE s.child_id='" + profile.ChildId + "' AND s.planned_subject='math' AND s.state IN ('started','active') AND s.ended_at_utc IS NULL AND r.session_id IS NULL;") == 0,
                    "atomic_runtime_never_exposes_active_math_session_without_runtime");
                activeSessionId = Convert.ToString(Scalar(c,
                    "SELECT s.id FROM session s JOIN math_session_runtime r ON r.session_id=s.id WHERE s.child_id='" + profile.ChildId + "' AND s.state IN ('started','active') AND s.ended_at_utc IS NULL LIMIT 1;"),
                    CultureInfo.InvariantCulture);
            }

            A(sessions.RecoverDanglingSessions() == 0,
                "atomic_runtime_winner_is_not_misclassified_as_dangling");
            var resumable = new MathSessionRuntimeService(database).LoadLatestResumable(profile.ChildId);
            A(resumable != null && resumable.SessionId == activeSessionId,
                "atomic_runtime_winner_is_immediately_resumable");

            sessions.CompleteSession(activeSessionId, true, "{}", "{}");
            new MathSessionRuntimeService(database).Delete(activeSessionId);
            var next = new MathSessionRuntimeService(database).TryCreateSession(
                profile.ChildId, "LOW", 7788, 8, "adaptive", null);
            A(next != null && next.SessionId != activeSessionId,
                "atomic_runtime_terminal_state_releases_slot_for_next_runtime_session");
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM session s JOIN math_session_runtime r ON r.session_id=s.id WHERE s.id='" + next.SessionId + "' AND s.state='active';") == 1,
                    "atomic_runtime_next_session_and_runtime_commit_together");
            }
            sessions.CompleteSession(next.SessionId, true, "{}", "{}");
            new MathSessionRuntimeService(database).Delete(next.SessionId);
        }

        private static Process StartRuntimeSessionWorker(string exePath, string dbPath, string schemaPath, string childId, string gatePath)
        {
            var info = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = QuoteArg("--runtime-session-worker") + " " + QuoteArg(dbPath) + " " + QuoteArg(schemaPath) + " " +
                    QuoteArg(childId) + " " + QuoteArg(gatePath),
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var process = Process.Start(info);
            if (process == null) throw new InvalidOperationException("Could not start atomic Math runtime worker.");
            return process;
        }

        private static int RunRuntimeSessionWorker(string[] args)
        {
            if (args == null || args.Length != 5) return 94;
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!File.Exists(args[4]) && DateTime.UtcNow < deadline) Thread.Sleep(10);
            if (!File.Exists(args[4])) return 95;

            try
            {
                var database = new LearningDatabase(args[1], args[2]);
                var session = new MathSessionRuntimeService(database).TryCreateSession(
                    args[3], "LOW", 6677, 8, "adaptive", null);
                if (session == null)
                {
                    Console.WriteLine("ATOMIC_RUNTIME_WORKER_REJECTED active_session_exists");
                    return 2;
                }
                Console.WriteLine("ATOMIC_RUNTIME_WORKER_CREATED " + session.SessionId);
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine("ATOMIC_RUNTIME_WORKER_REJECTED " + ex.Message);
                return 2;
            }
            catch (SQLiteException ex)
            {
                Console.WriteLine("ATOMIC_RUNTIME_WORKER_REJECTED_SQLITE " + ex.ErrorCode);
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ATOMIC_RUNTIME_WORKER_FAIL " + ex);
                return 3;
            }
        }

        private static Process StartMaintenanceWorker(string mode, string exePath, string dbPath, string schemaPath, string childId, string gatePath)
        {
            var info = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = QuoteArg(mode) + " " + QuoteArg(dbPath) + " " + QuoteArg(schemaPath) + " " +
                    QuoteArg(childId) + " " + QuoteArg(gatePath),
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var process = Process.Start(info);
            if (process == null) throw new InvalidOperationException("Could not start MathData maintenance worker.");
            return process;
        }

        private static int RunRecoveryWorker(string[] args)
        {
            if (args == null || args.Length != 5) return 96;
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!File.Exists(args[4]) && DateTime.UtcNow < deadline) Thread.Sleep(10);
            if (!File.Exists(args[4])) return 97;
            try
            {
                var database = new LearningDatabase(args[1], args[2]);
                var recovered = new LearnerSessionService(database).RecoverDanglingSessions(args[3]);
                Console.WriteLine("RECOVERY_WORKER_RECOVERED " + recovered.ToString(CultureInfo.InvariantCulture));
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("RECOVERY_WORKER_FAIL " + ex);
                return 3;
            }
        }

        private static int RunTerminalCleanupWorker(string[] args)
        {
            if (args == null || args.Length != 5) return 98;
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!File.Exists(args[4]) && DateTime.UtcNow < deadline) Thread.Sleep(10);
            if (!File.Exists(args[4])) return 99;
            try
            {
                var database = new LearningDatabase(args[1], args[2]);
                var deleted = new MathSessionRuntimeService(database).DeleteTerminalCheckpoints(args[3]);
                Console.WriteLine("TERMINAL_CLEANUP_WORKER_DELETED " + deleted.ToString(CultureInfo.InvariantCulture));
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("TERMINAL_CLEANUP_WORKER_FAIL " + ex);
                return 3;
            }
        }

        private static void TestCrossProcessRecoveryDoesNotReclaimLiveRuntime(string root, string sourceSchema)
        {
            var schemaDir = Path.Combine(root, "schema-recovery-vs-live-commit");
            CopySchemas(sourceSchema, schemaDir);
            var schemaPath = Path.Combine(schemaDir, "001_initial.sql");
            var dbPath = Path.Combine(root, "recovery-vs-live-commit.db");
            var database = new LearningDatabase(dbPath, schemaPath);
            database.Initialize("DELETE");
            var sessions = new LearnerSessionService(database);
            var profile = sessions.EnsurePrimaryChild("Bé recovery live runtime");
            var runtime = new MathSessionRuntimeService(database);
            var active = runtime.TryCreateSession(profile.ChildId, "LOW", 8801, 8, "adaptive", null);
            A(active != null, "recovery_live_runtime_fixture_starts_atomic_session");

            var gatePath = Path.Combine(root, "recovery-live-go.flag");
            var exePath = Assembly.GetExecutingAssembly().Location;
            using (var commit = StartCommitWorker(exePath, dbPath, schemaPath, profile.ChildId, active.SessionId,
                "recovery-live-attempt", "recovery-live-question", gatePath))
            using (var recovery = StartMaintenanceWorker("--recovery-worker", exePath, dbPath, schemaPath, profile.ChildId, gatePath))
            {
                File.WriteAllText(gatePath, "go");
                var commitExited = commit.WaitForExit(20000);
                var recoveryExited = recovery.WaitForExit(20000);
                if (!commitExited) { try { commit.Kill(); } catch { } }
                if (!recoveryExited) { try { recovery.Kill(); } catch { } }
                var commitOutput = commit.StandardOutput.ReadToEnd() + commit.StandardError.ReadToEnd();
                var recoveryOutput = recovery.StandardOutput.ReadToEnd() + recovery.StandardError.ReadToEnd();
                A(commitExited && recoveryExited && commit.ExitCode == 0 && recovery.ExitCode == 0,
                    "recovery_live_runtime_workers_finish_without_lock_failure outputs=" + commitOutput.Trim() + " | " + recoveryOutput.Trim());
                A(recoveryOutput.Contains("RECOVERY_WORKER_RECOVERED 0"),
                    "recovery_live_runtime_never_reclaims_session_with_runtime_marker");
            }

            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM session WHERE id='" + active.SessionId + "' AND state='active' AND ended_at_utc IS NULL;") == 1 &&
                  Count(c, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + active.SessionId + "';") == 1,
                    "recovery_live_runtime_keeps_owner_session_active_and_resumable");
                A(Count(c, "SELECT count(*) FROM attempt WHERE session_id='" + active.SessionId + "';") == 1 &&
                  Count(c, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + active.SessionId + "';") == 1,
                    "recovery_live_runtime_allows_concurrent_answer_learning_commit");
            }
            sessions.CompleteSession(active.SessionId, true, "{}", "{}");
            runtime.Delete(active.SessionId);
        }

        private static void TestCrossProcessRecoveryCannotSplitAtomicStart(string root, string sourceSchema)
        {
            var schemaDir = Path.Combine(root, "schema-recovery-vs-atomic-start");
            CopySchemas(sourceSchema, schemaDir);
            var schemaPath = Path.Combine(schemaDir, "001_initial.sql");
            var dbPath = Path.Combine(root, "recovery-vs-atomic-start.db");
            var database = new LearningDatabase(dbPath, schemaPath);
            database.Initialize("DELETE");
            var sessions = new LearnerSessionService(database);
            var profile = sessions.EnsurePrimaryChild("Bé recovery atomic start");
            var gatePath = Path.Combine(root, "recovery-start-go.flag");
            var exePath = Assembly.GetExecutingAssembly().Location;

            using (var starter = StartRuntimeSessionWorker(exePath, dbPath, schemaPath, profile.ChildId, gatePath))
            using (var recovery = StartMaintenanceWorker("--recovery-worker", exePath, dbPath, schemaPath, profile.ChildId, gatePath))
            {
                File.WriteAllText(gatePath, "go");
                var startExited = starter.WaitForExit(20000);
                var recoveryExited = recovery.WaitForExit(20000);
                if (!startExited) { try { starter.Kill(); } catch { } }
                if (!recoveryExited) { try { recovery.Kill(); } catch { } }
                var startOutput = starter.StandardOutput.ReadToEnd() + starter.StandardError.ReadToEnd();
                var recoveryOutput = recovery.StandardOutput.ReadToEnd() + recovery.StandardError.ReadToEnd();
                A(startExited && recoveryExited && starter.ExitCode == 0 && recovery.ExitCode == 0,
                    "recovery_atomic_start_workers_finish_without_lock_failure outputs=" + startOutput.Trim() + " | " + recoveryOutput.Trim());
                A(recoveryOutput.Contains("RECOVERY_WORKER_RECOVERED 0"),
                    "recovery_atomic_start_never_observes_half_created_session");
            }

            string activeSessionId;
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM session s JOIN math_session_runtime r ON r.session_id=s.id WHERE s.child_id='" + profile.ChildId + "' AND s.state='active' AND s.ended_at_utc IS NULL;") == 1 &&
                  Count(c, "SELECT count(*) FROM session s LEFT JOIN math_session_runtime r ON r.session_id=s.id WHERE s.child_id='" + profile.ChildId + "' AND s.state='active' AND s.ended_at_utc IS NULL AND r.session_id IS NULL;") == 0,
                    "recovery_atomic_start_commits_session_and_runtime_as_one_visible_unit");
                activeSessionId = Convert.ToString(Scalar(c,
                    "SELECT s.id FROM session s JOIN math_session_runtime r ON r.session_id=s.id WHERE s.child_id='" + profile.ChildId + "' AND s.state='active' LIMIT 1;"), CultureInfo.InvariantCulture);
            }
            sessions.CompleteSession(activeSessionId, true, "{}", "{}");
            new MathSessionRuntimeService(database).Delete(activeSessionId);
        }

        private static void TestCrossProcessTerminalCleanupPreservesFreshRuntime(string root, string sourceSchema)
        {
            var schemaDir = Path.Combine(root, "schema-cleanup-vs-fresh-start");
            CopySchemas(sourceSchema, schemaDir);
            var schemaPath = Path.Combine(schemaDir, "001_initial.sql");
            var dbPath = Path.Combine(root, "cleanup-vs-fresh-start.db");
            var database = new LearningDatabase(dbPath, schemaPath);
            database.Initialize("DELETE");
            var sessions = new LearnerSessionService(database);
            var profile = sessions.EnsurePrimaryChild("Bé cleanup fresh runtime");
            var runtime = new MathSessionRuntimeService(database);
            var terminal = runtime.TryCreateSession(profile.ChildId, "LOW", 8802, 8, "adaptive", null);
            A(terminal != null, "cleanup_fresh_start_fixture_creates_old_runtime");
            sessions.CompleteSession(terminal.SessionId, false, "{}", "{}");
            using (var c = database.OpenConnection())
                A(Count(c, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + terminal.SessionId + "';") == 1,
                    "cleanup_fresh_start_fixture_leaves_terminal_runtime_stale");

            var gatePath = Path.Combine(root, "cleanup-fresh-start-go.flag");
            var exePath = Assembly.GetExecutingAssembly().Location;
            using (var starter = StartRuntimeSessionWorker(exePath, dbPath, schemaPath, profile.ChildId, gatePath))
            using (var cleanup = StartMaintenanceWorker("--terminal-cleanup-worker", exePath, dbPath, schemaPath, profile.ChildId, gatePath))
            {
                File.WriteAllText(gatePath, "go");
                var startExited = starter.WaitForExit(20000);
                var cleanupExited = cleanup.WaitForExit(20000);
                if (!startExited) { try { starter.Kill(); } catch { } }
                if (!cleanupExited) { try { cleanup.Kill(); } catch { } }
                var startOutput = starter.StandardOutput.ReadToEnd() + starter.StandardError.ReadToEnd();
                var cleanupOutput = cleanup.StandardOutput.ReadToEnd() + cleanup.StandardError.ReadToEnd();
                A(startExited && cleanupExited && starter.ExitCode == 0 && cleanup.ExitCode == 0,
                    "cleanup_fresh_start_workers_finish_without_lock_failure outputs=" + startOutput.Trim() + " | " + cleanupOutput.Trim());
                A(cleanupOutput.Contains("TERMINAL_CLEANUP_WORKER_DELETED 1"),
                    "cleanup_fresh_start_deletes_exactly_old_terminal_checkpoint");
            }

            string freshSessionId;
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + terminal.SessionId + "';") == 0,
                    "cleanup_fresh_start_removes_old_terminal_runtime");
                A(Count(c, "SELECT count(*) FROM session s JOIN math_session_runtime r ON r.session_id=s.id WHERE s.child_id='" + profile.ChildId + "' AND s.state='active' AND s.ended_at_utc IS NULL;") == 1 &&
                  Count(c, "SELECT count(*) FROM math_session_runtime;") == 1,
                    "cleanup_fresh_start_preserves_only_new_active_runtime");
                freshSessionId = Convert.ToString(Scalar(c,
                    "SELECT s.id FROM session s JOIN math_session_runtime r ON r.session_id=s.id WHERE s.child_id='" + profile.ChildId + "' AND s.state='active' LIMIT 1;"), CultureInfo.InvariantCulture);
            }
            sessions.CompleteSession(freshSessionId, true, "{}", "{}");
            runtime.Delete(freshSessionId);
        }

        private static void TestDuplicateActiveRecoveryRequiresLiveKeeper(string root, string sourceSchema)
        {
            var schemaDir = Path.Combine(root, "schema-duplicate-active-live-keeper");
            CopySchemas(sourceSchema, schemaDir);
            var schemaPath = Path.Combine(schemaDir, "001_initial.sql");
            var database = new LearningDatabase(Path.Combine(root, "duplicate-active-live-keeper.db"), schemaPath);
            database.Initialize("DELETE");
            var sessions = new LearnerSessionService(database);
            var profile = sessions.EnsurePrimaryChild("Bé duplicate active keeper");
            var runtime = new MathSessionRuntimeService(database);

            var staleKeeper = runtime.TryCreateSession(profile.ChildId, "LOW", 8810, 8, "adaptive", null);
            A(staleKeeper != null, "duplicate_active_keeper_fixture_starts_keeper");
            sessions.CompleteSession(staleKeeper.SessionId, true, "{}", "{}");
            runtime.Delete(staleKeeper.SessionId);
            var fresh = runtime.TryCreateSession(profile.ChildId, "LOW", 8811, 8, "adaptive", null);
            A(fresh != null && fresh.SessionId != staleKeeper.SessionId,
                "duplicate_active_keeper_fixture_starts_fresh_after_stale_keeper_terminal");

            var recovered = runtime.RecoverDuplicateActiveMathSessions(profile.ChildId, staleKeeper.SessionId);
            A(recovered == 0,
                "duplicate_active_recovery_stale_keeper_cannot_recover_fresh_session");
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM session WHERE id='" + fresh.SessionId + "' AND state='active' AND ended_at_utc IS NULL;") == 1 &&
                  Count(c, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + fresh.SessionId + "';") == 1,
                    "duplicate_active_recovery_stale_keeper_preserves_fresh_runtime");
            }
            sessions.CompleteSession(fresh.SessionId, true, "{}", "{}");
            runtime.Delete(fresh.SessionId);
        }

        private static void TestTargetedAtomicStartRollsBackOnProgressFailure(string root, string sourceSchema)
        {
            var schemaDir = Path.Combine(root, "schema-targeted-start-failure");
            CopySchemas(sourceSchema, schemaDir);
            var schemaPath = Path.Combine(schemaDir, "001_initial.sql");
            var dbPath = Path.Combine(root, "targeted-start-failure.db");
            var database = new LearningDatabase(dbPath, schemaPath);
            database.Initialize("DELETE");
            var sessions = new LearnerSessionService(database);
            var profile = sessions.EnsurePrimaryChild("Bé targeted atomic");
            var runtime = new MathSessionRuntimeService(database);

            using (var c = database.OpenConnection())
            {
                Exec(c, @"CREATE TRIGGER smoke_fail_math_lesson_start
BEFORE INSERT ON math_lesson_progress
BEGIN
    SELECT RAISE(ABORT, 'injected_math_lesson_start_failure');
END;");
            }

            var failed = false;
            try
            {
                runtime.TryCreateSession(profile.ChildId, "LOW", 9901, 9, "lesson", "M2-L1", "TEST_MATH_SKILL");
            }
            catch (SQLiteException)
            {
                failed = true;
            }
            A(failed, "targeted_atomic_start_injected_progress_failure_surfaces_error");
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM session WHERE child_id='" + profile.ChildId + "' AND planned_subject='math';") == 0,
                    "targeted_atomic_start_failure_rolls_back_session");
                A(Count(c, "SELECT count(*) FROM math_session_runtime;") == 0,
                    "targeted_atomic_start_failure_rolls_back_runtime");
                A(Count(c, "SELECT count(*) FROM math_lesson_progress WHERE child_id='" + profile.ChildId + "' AND lesson_id='M2-L1';") == 0,
                    "targeted_atomic_start_failure_rolls_back_lesson_progress");
                Exec(c, "DROP TRIGGER smoke_fail_math_lesson_start;");
            }

            var started = runtime.TryCreateSession(
                profile.ChildId, "LOW", 9902, 9, "lesson", "M2-L1", "TEST_MATH_SKILL");
            A(started != null && !string.IsNullOrWhiteSpace(started.SessionId),
                "targeted_atomic_start_retry_creates_session");
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM session WHERE id='" + started.SessionId + "' AND state='active';") == 1,
                    "targeted_atomic_start_retry_has_active_session");
                A(Count(c, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + started.SessionId + "' AND session_mode='lesson' AND target_lesson_id='M2-L1';") == 1,
                    "targeted_atomic_start_retry_has_runtime");
                A(Count(c, "SELECT count(*) FROM math_lesson_progress WHERE child_id='" + profile.ChildId + "' AND lesson_id='M2-L1' AND skill_id='TEST_MATH_SKILL' AND started_count=1 AND completed_count=0;") == 1,
                    "targeted_atomic_start_retry_writes_lesson_progress_once");
            }
            A(sessions.RecoverDanglingSessions() == 0,
                "targeted_atomic_start_retry_is_immediately_resumable_not_dangling");
            sessions.CompleteSession(started.SessionId, true, "{}", "{}");
            runtime.Delete(started.SessionId);
        }

        private static void TestDanglingRecoveryIsScopedToMath(string root, string sourceSchema)
        {
            var schemaDir = Path.Combine(root, "schema-recovery-subject-scope");
            CopySchemas(sourceSchema, schemaDir);
            var schemaPath = Path.Combine(schemaDir, "001_initial.sql");
            var database = new LearningDatabase(Path.Combine(root, "recovery-subject-scope.db"), schemaPath);
            database.Initialize("DELETE");
            var sessions = new LearnerSessionService(database);
            var profile = sessions.EnsurePrimaryChild("Bé recovery scope");

            var math = sessions.BeginSession(profile.ChildId, "math", "LOW");
            var english = sessions.BeginSession(profile.ChildId, "english", "LOW");
            var mixed = sessions.BeginSession(profile.ChildId, "mixed", "LOW");

            A(sessions.RecoverDanglingSessions() == 1,
                "dangling_recovery_only_recovers_math_session_without_runtime");
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM session WHERE id='" + math.SessionId + "' AND state='recovered' AND ended_at_utc IS NOT NULL;") == 1,
                    "dangling_recovery_marks_math_session_recovered");
                A(Count(c, "SELECT count(*) FROM session WHERE id='" + english.SessionId + "' AND state='active' AND ended_at_utc IS NULL;") == 1,
                    "dangling_recovery_preserves_active_english_session");
                A(Count(c, "SELECT count(*) FROM session WHERE id='" + mixed.SessionId + "' AND state='active' AND ended_at_utc IS NULL;") == 1,
                    "dangling_recovery_preserves_active_mixed_session");
            }

            sessions.CompleteSession(english.SessionId, true, "{}", "{}");
            sessions.CompleteSession(mixed.SessionId, true, "{}", "{}");
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM session WHERE id IN ('" + english.SessionId + "','" + mixed.SessionId + "') AND state='aborted';") == 2,
                    "dangling_recovery_non_math_sessions_remain_completable");
            }
        }

        private static void TestDanglingRecoveryIsScopedToChild(string root, string sourceSchema)
        {
            var schemaDir = Path.Combine(root, "schema-recovery-child-scope");
            CopySchemas(sourceSchema, schemaDir);
            var schemaPath = Path.Combine(schemaDir, "001_initial.sql");
            var database = new LearningDatabase(Path.Combine(root, "recovery-child-scope.db"), schemaPath);
            database.Initialize("DELETE");
            var sessions = new LearnerSessionService(database);
            var primary = sessions.EnsurePrimaryChild("Bé primary recovery");
            var otherChildId = "child-recovery-other";
            using (var c = database.OpenConnection())
            {
                var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                Exec(c, "INSERT INTO child(id,display_name,grade_level,created_at_utc,updated_at_utc) VALUES(@id,@name,2,@utc,@utc);",
                    "@id", otherChildId, "@name", "Bé khác", "@utc", now);
            }

            var primaryMath = sessions.BeginSession(primary.ChildId, "math", "LOW");
            var otherMath = sessions.BeginSession(otherChildId, "math", "LOW");

            A(sessions.RecoverDanglingSessions(primary.ChildId) == 1,
                "dangling_recovery_child_scope_recovers_only_requested_child");
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM session WHERE id='" + primaryMath.SessionId + "' AND state='recovered' AND ended_at_utc IS NOT NULL;") == 1,
                    "dangling_recovery_child_scope_marks_requested_child_recovered");
                A(Count(c, "SELECT count(*) FROM session WHERE id='" + otherMath.SessionId + "' AND state='active' AND ended_at_utc IS NULL;") == 1,
                    "dangling_recovery_child_scope_preserves_other_child_math_session");
            }

            A(sessions.RecoverDanglingSessions() == 1,
                "dangling_recovery_global_overload_still_recovers_remaining_math_session");
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM session WHERE id='" + otherMath.SessionId + "' AND state='recovered' AND ended_at_utc IS NOT NULL;") == 1,
                    "dangling_recovery_global_overload_keeps_maintenance_behavior");
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
            A(migrated.SchemaVersion == 5, "v1_upgrade_reaches_schema_five");
            A(migrated.Migration != null && migrated.Migration.Version == 5, "v1_upgrade_latest_migration_five");
            A(migrated.PreMigrationBackup != null && File.Exists(migrated.PreMigrationBackup.DatabasePath), "v1_upgrade_prebackup_database_exists");
            A(migrated.PreMigrationBackup != null && File.Exists(migrated.PreMigrationBackup.MetadataPath), "v1_upgrade_prebackup_metadata_exists");
            var verified = ManagedBackupService.VerifyManagedBackup(migrated.PreMigrationBackup.MetadataPath);
            A(verified.IsValid && verified.SchemaVersion == 1, "v1_upgrade_prebackup_verifies_as_schema_one");
            using (var c = database.OpenConnection())
            {
                A(Count(c, "SELECT count(*) FROM migration_history WHERE version=2;") == 1, "v1_upgrade_records_v2_once");
                A(Count(c, "SELECT count(*) FROM migration_history WHERE version=3;") == 1, "v1_upgrade_records_v3_once");
                A(Count(c, "SELECT count(*) FROM migration_history WHERE version=4;") == 1, "v1_upgrade_records_v4_once");
                A(Count(c, "SELECT count(*) FROM migration_history WHERE version=5;") == 1, "v1_upgrade_records_v5_once");
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
            foreach (var name in new[] { "001_initial.sql", "002_attempt_immutability.sql", "003_math_attempt_idempotency_runtime.sql", "004_math_lesson_progress.sql", "005_math_runtime_pack_identity.sql" })
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
