using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using WAHU.Data;
using WAHU.Learning;
using WAHU.Session;

namespace WAHU.MathSessionPersistenceRuntimeSmoke
{
    internal static class Program
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        private static int _assertions;

        private static int Main()
        {
            var root = Path.Combine(Path.GetTempPath(), "wahu-math-session-persistence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var repo = FindRepoRoot();
                var schemaPath = Path.Combine(repo, "data", "schema", "001_initial.sql");
                var templatePath = Path.Combine(repo, "content_packs", "math_grade2_v1", "verified_templates_v1.json");
                TestResumeOpenQuestionAndComplete(root, schemaPath, templatePath);
                TestCommittedStaleQuestionIsNotReplayed(root, schemaPath, templatePath);
                TestCorruptOpenQuestionRecoversWithoutProgressReset(root, schemaPath, templatePath);
                TestDeterministicSecondQuestionAcrossResume(root, schemaPath, templatePath);
                TestInteractiveIntegerFinalization();
                Console.WriteLine("MATH_SESSION_PERSISTENCE_RUNTIME_SMOKE_PASS assertions=" + _assertions);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("MATH_SESSION_PERSISTENCE_RUNTIME_SMOKE_FAIL " + ex);
                return 1;
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        private static void TestResumeOpenQuestionAndComplete(string root, string schemaPath, string templatePath)
        {
            var database = NewDatabase(Path.Combine(root, "resume-complete.db"), schemaPath);
            string sessionId;
            string openQuestionId;
            string openPrompt;
            string openAnswer;

            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 424242, 4))
            {
                var start = first.Start("Bé resume");
                A(!start.ResumedExistingSession && start.CompletedQuestionCount == 0, "fresh_session_not_resumed");
                A(start.TargetQuestionCount == 4, "fresh_target_four");
                sessionId = start.SessionId;

                var q1 = first.NextQuestion();
                first.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 1200);
                A(first.Summary.Attempts == 1 && first.Summary.Correct == 1, "first_answer_committed_before_suspend");

                var q2 = first.NextQuestion();
                openQuestionId = q2.QuestionId;
                openPrompt = q2.PromptVi;
                openAnswer = q2.CorrectAnswerDisplay;
                A(first.HasOpenQuestion, "open_question_before_suspend");
                var suspended = first.Suspend("test_close_app");
                A(!first.IsActive && suspended.EndedAtUtc == null, "suspend_leaves_session_unended");
            }

            A(SessionState(database, sessionId) == "active", "suspended_session_remains_active");
            A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + sessionId + "' AND current_question_json IS NOT NULL;") == 1,
                "suspended_open_question_persisted");
            A(Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + sessionId + "';") == 0,
                "suspend_grants_no_reward");

            using (var resumed = new MathSessionCoordinator(database, templatePath, "NORMAL", 999999, 9))
            {
                var start = resumed.Start("Bé resume");
                A(start.ResumedExistingSession, "existing_session_resumed");
                A(start.SessionId == sessionId, "resume_keeps_same_session_id");
                A(start.TargetQuestionCount == 4, "resume_uses_persisted_target_not_constructor_target");
                A(start.CompletedQuestionCount == 1, "resume_reconstructs_committed_count");
                A(start.RestoredOpenQuestion && !start.DiscardedCorruptOpenQuestion, "resume_restores_open_question");

                var restored = resumed.NextQuestion();
                A(restored.QuestionId == openQuestionId, "resume_returns_exact_open_question_id");
                A(restored.PromptVi == openPrompt && restored.CorrectAnswerDisplay == openAnswer, "resume_returns_exact_open_question_content");
                A(resumed.NextQuestion().QuestionId == openQuestionId, "next_question_is_idempotent_while_open");
                resumed.SubmitAnswerAt(restored.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 1300);

                while (resumed.Summary.Attempts < 4)
                {
                    var q = resumed.NextQuestion();
                    A(q != null, "resume_generates_remaining_question_" + resumed.Summary.Attempts);
                    resumed.SubmitAnswerAt(q.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 1400);
                }
                A(resumed.NextQuestion() == null, "resumed_session_stops_at_original_target");
                var summary = resumed.Complete();
                A(summary.Attempts == 4 && summary.Correct == 4, "resumed_session_completes_with_reconstructed_progress");
                A(summary.GardenGrowthSteps == 1, "resumed_completion_grants_reward_once");
            }

            A(SessionState(database, sessionId) == "completed", "resumed_session_persisted_completed");
            A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + sessionId + "';") == 0,
                "completed_session_runtime_checkpoint_deleted");
            A(Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + sessionId + "';") == 1,
                "completed_session_reward_exactly_once");

            using (var next = new MathSessionCoordinator(database, templatePath, "LOW", 424242, 2))
            {
                var start = next.Start("Bé resume");
                A(!start.ResumedExistingSession && start.SessionId != sessionId, "completed_session_is_not_resumed_again");
                next.Abort("cleanup");
            }
        }

        private static void TestCommittedStaleQuestionIsNotReplayed(string root, string schemaPath, string templatePath)
        {
            var database = NewDatabase(Path.Combine(root, "stale-after-commit.db"), schemaPath);
            string sessionId;
            MathQuestion answered;
            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 12345, 3))
            {
                var start = first.Start("Bé stale");
                sessionId = start.SessionId;
                answered = first.NextQuestion();
                first.SubmitAnswerAt(answered.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 900);

                var runtime = new MathSessionRuntimeService(database).LoadLatestResumable(start.ChildId);
                new MathSessionRuntimeService(database).SaveOpenQuestion(
                    sessionId,
                    runtime.GeneratedQuestionCount,
                    Json.Serialize(answered),
                    "{}",
                    DateTime.UtcNow.AddSeconds(-2),
                    runtime.ForcedRepairTemplateId);
                first.Suspend("simulate_crash_after_attempt_before_cache_clear");
            }

            A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + sessionId + "';") == 1, "stale_fixture_has_one_committed_attempt");
            A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + sessionId + "' AND current_question_json IS NOT NULL;") == 1,
                "stale_fixture_has_answered_question_cached");

            using (var resumed = new MathSessionCoordinator(database, templatePath, "LOW", 999, 3))
            {
                var start = resumed.Start("Bé stale");
                A(start.ResumedExistingSession && start.CompletedQuestionCount == 1, "stale_resume_rebuilds_committed_progress");
                A(!start.RestoredOpenQuestion, "already_committed_cached_question_not_restored");
                var next = resumed.NextQuestion();
                A(next != null && next.QuestionId != answered.QuestionId, "already_committed_question_not_shown_again");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + sessionId + "';") == 1,
                    "resume_does_not_duplicate_stale_attempt_before_submit");
                resumed.Abort("cleanup");
            }
        }

        private static void TestCorruptOpenQuestionRecoversWithoutProgressReset(string root, string schemaPath, string templatePath)
        {
            var database = NewDatabase(Path.Combine(root, "corrupt-cache.db"), schemaPath);
            string sessionId;
            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 314159, 3))
            {
                var start = first.Start("Bé corrupt");
                sessionId = start.SessionId;
                var q1 = first.NextQuestion();
                first.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 1000);
                first.NextQuestion();
                Exec(database, "UPDATE math_session_runtime SET current_question_json='not-json-at-all' WHERE session_id=@session;", "@session", sessionId);
                first.Suspend("simulate_corrupt_cache");
            }

            using (var resumed = new MathSessionCoordinator(database, templatePath, "LOW", 888, 9))
            {
                var start = resumed.Start("Bé corrupt");
                A(start.ResumedExistingSession, "corrupt_cache_session_still_resumed");
                A(start.DiscardedCorruptOpenQuestion && !start.RestoredOpenQuestion, "corrupt_open_question_discarded_only");
                A(start.CompletedQuestionCount == 1 && resumed.Summary.Correct == 1, "corrupt_cache_keeps_committed_progress");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + sessionId + "';") == 1,
                    "corrupt_cache_does_not_reset_attempts");
                var replacement = resumed.NextQuestion();
                A(replacement != null, "corrupt_cache_can_continue_with_new_question");
                resumed.Abort("cleanup");
            }
        }

        private static void TestInteractiveIntegerFinalization()
        {
            var generator = new MathQuestionGenerator(20260907);
            var question = new MathQuestion
            {
                AnswerKind = "interaction_integer",
                CorrectAnswer = 7,
                CorrectAnswerText = "7",
                Choices = new[] { 6, 7, 8 },
                ChoiceTexts = new[] { "6", "7", "8" }
            };
            var finalize = typeof(MathQuestionGenerator).GetMethod(
                "FinalizeAnswerOptions",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            A(finalize != null, "interactive_finalizer_method_found");
            finalize.Invoke(generator, new object[] { question });
            A(string.Equals(question.AnswerKind, "interaction_integer", StringComparison.Ordinal),
                "interactive_answer_kind_preserved");
            A(question.DisplayChoices != null && question.DisplayChoices.Count == 0,
                "interactive_answer_exposes_no_fake_choices");
            A(question.IsCorrectAnswer("7"), "interactive_numeric_answer_still_validates");
            A(!question.IsCorrectAnswer("8"), "interactive_wrong_numeric_answer_rejected");
        }

        private static void TestDeterministicSecondQuestionAcrossResume(string root, string schemaPath, string templatePath)
        {
            var uninterrupted = SecondQuestionFingerprint(Path.Combine(root, "deterministic-a.db"), schemaPath, templatePath, false);
            var resumed = SecondQuestionFingerprint(Path.Combine(root, "deterministic-b.db"), schemaPath, templatePath, true);
            A(uninterrupted == resumed, "same_seed_and_committed_state_generate_same_second_question_across_resume");
        }

        private static string SecondQuestionFingerprint(string dbPath, string schemaPath, string templatePath, bool suspendBetween)
        {
            var database = NewDatabase(dbPath, schemaPath);
            var first = new MathSessionCoordinator(database, templatePath, "LOW", 777777, 2);
            var start = first.Start("Bé deterministic");
            var q1 = first.NextQuestion();
            first.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 1100);

            MathSessionCoordinator active = first;
            if (suspendBetween)
            {
                first.Suspend("deterministic_resume");
                first.Dispose();
                active = new MathSessionCoordinator(database, templatePath, "LOW", 1, 9);
                var resumed = active.Start("Bé deterministic");
                A(resumed.ResumedExistingSession && resumed.CompletedQuestionCount == 1, "deterministic_path_resumed_after_one_attempt");
            }

            var q2 = active.NextQuestion();
            var fingerprint = q2.TemplateId + "|" + q2.PromptVi + "|" + q2.CorrectAnswerDisplay + "|" + string.Join("~", q2.DisplayChoices);
            active.Abort("cleanup");
            active.Dispose();
            if (!ReferenceEquals(active, first)) first.Dispose();
            return fingerprint;
        }

        private static LearningDatabase NewDatabase(string dbPath, string schemaPath)
        {
            var database = new LearningDatabase(dbPath, schemaPath);
            var init = database.Initialize("DELETE");
            A(init.SchemaVersion == 3 && init.Health.IsHealthy, "database_ready_v3_" + Path.GetFileNameWithoutExtension(dbPath));
            return database;
        }

        private static string SessionState(LearningDatabase database, string sessionId)
        {
            using (var c = database.OpenConnection())
            using (var command = c.CreateCommand())
            {
                command.CommandText = "SELECT state FROM session WHERE id=@id;";
                command.Parameters.AddWithValue("@id", sessionId);
                return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private static int Count(LearningDatabase database, string sql)
        {
            using (var c = database.OpenConnection())
            using (var command = c.CreateCommand())
            {
                command.CommandText = sql;
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private static void Exec(LearningDatabase database, string sql, params object[] parameters)
        {
            database.Writes.Execute((c, tx) =>
            {
                using (var command = c.CreateCommand())
                {
                    command.Transaction = tx;
                    command.CommandText = sql;
                    for (var i = 0; i + 1 < parameters.Length; i += 2)
                        command.Parameters.AddWithValue(Convert.ToString(parameters[i], CultureInfo.InvariantCulture), parameters[i + 1] ?? DBNull.Value);
                    command.ExecuteNonQuery();
                }
            });
        }

        private static string FindRepoRoot()
        {
            var current = new DirectoryInfo(Environment.CurrentDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "WAHUKidsLearn.sln"))) return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException("Repository root not found.");
        }

        private static void A(bool condition, string name)
        {
            if (!condition) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }
    }
}
