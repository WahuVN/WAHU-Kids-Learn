using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using WAHU.Data;
using WAHU.Learning;
using WAHU.Session;

namespace WAHU.MathRescueGameplayRuntimeSmoke
{
    internal static class Program
    {
        private const string FirstEventId = "m2_evt_number_sign_rescue_01";
        private const string FirstLessonId = "m2_ls_num_count_read_write_0_1000";
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        private static int _assertions;

        private static int Main()
        {
            var root = Path.Combine(Path.GetTempPath(), "wahu-math-rescue-gameplay-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var repo = FindRepoRoot();
                var schemaPath = Path.Combine(repo, "data", "schema", "001_initial.sql");
                var contentRoot = Path.Combine(repo, "content_packs", "math_grade2_v1");
                var templatePath = Path.Combine(contentRoot, "verified_templates_v1.json");
                var eventPath = Path.Combine(contentRoot, "game_events_v1.json");

                TestHeadlessThreeCheckpointJourney(root, schemaPath, templatePath, eventPath);
                TestTransitionRaceAndDoubleSubmit(root, schemaPath, templatePath, eventPath);
                TestExactRepairResumeToken(root, schemaPath, templatePath, eventPath);

                Console.WriteLine("MATH_RESCUE_GAMEPLAY_RUNTIME_SMOKE_PASS assertions=" + _assertions);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("MATH_RESCUE_GAMEPLAY_RUNTIME_SMOKE_FAIL " + ex);
                return 1;
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        private static void TestHeadlessThreeCheckpointJourney(
            string root,
            string schemaPath,
            string templatePath,
            string eventPath)
        {
            var database = NewDatabase(Path.Combine(root, "headless-three-checkpoints.db"), schemaPath);
            var phases = new List<MathRescueGameplayPhase>();
            var reasons = new List<string>();
            using (var game = NewGame(database, templatePath, eventPath, 91001))
            {
                game.StateChanged += delegate(object sender, MathRescueGameplayStateChangedEventArgs args)
                {
                    phases.Add(args.Current.Phase);
                    reasons.Add(args.Reason);
                };

                var start = game.Start("Bé rescue headless");
                A(start.State.Phase == MathRescueGameplayPhase.INTRO && start.State.CurrentCheckpoint == 1,
                    "headless_starts_at_intro_checkpoint_one");
                A(!start.State.CanAnswer && start.State.Progress.CompletedCheckpoints == 0 &&
                  start.State.Progress.TotalCheckpoints == 3 && !start.State.Completed,
                    "headless_intro_exposes_ui_safe_initial_snapshot");

                var ready = game.AcknowledgeIntro();
                A(ready.Phase == MathRescueGameplayPhase.CHECKPOINT_READY && !ready.CanAnswer,
                    "intro_advances_to_checkpoint_ready");

                for (var checkpoint = 1; checkpoint <= 3; checkpoint++)
                {
                    var active = game.BeginQuestion();
                    A(active.Phase == MathRescueGameplayPhase.QUESTION_ACTIVE && active.CanAnswer &&
                      active.CurrentQuestion != null && active.CurrentCheckpoint == checkpoint,
                        "checkpoint_" + checkpoint + "_question_becomes_active");
                    var question = active.CurrentQuestion;

                    if (checkpoint == 2)
                    {
                        var wrong = game.SubmitAnswerAt(WrongAnswer(question), 0, "smoke", DateTime.UtcNow, 940);
                        A(!wrong.Learning.Learning.IsCorrect && wrong.Learning.Learning.CanRetry &&
                          wrong.State.Phase == MathRescueGameplayPhase.ANSWER_FEEDBACK && !wrong.State.CanAnswer &&
                          wrong.State.AnswerState == MathRescueAnswerState.WRONG &&
                          wrong.State.FeedbackState == MathRescueFeedbackState.TRY_AGAIN,
                            "wrong_first_try_enters_feedback_before_repair");

                        var duplicateRejected = false;
                        try { game.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "double-click", DateTime.UtcNow, 10); }
                        catch (InvalidOperationException) { duplicateRejected = true; }
                        A(duplicateRejected,
                            "feedback_phase_rejects_sequential_double_submit_before_repair_transition");

                        var repair = game.AdvanceAfterFeedback();
                        A(repair.Phase == MathRescueGameplayPhase.REPAIR && repair.CanAnswer && repair.RetryPending &&
                          repair.CurrentQuestion != null && repair.CurrentQuestion.QuestionId == question.QuestionId,
                            "feedback_advances_to_repair_without_changing_question");

                        var repaired = game.SubmitRepairAnswerAt(
                            question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 1020);
                        A(repaired.Learning.Learning.IsCorrect && repaired.Learning.Learning.QuestionCompleted &&
                          !repaired.Learning.Learning.CanRetry &&
                          repaired.State.Phase == MathRescueGameplayPhase.ANSWER_FEEDBACK,
                            "repair_retry_finalizes_same_checkpoint");
                    }
                    else
                    {
                        var correct = game.SubmitAnswerAt(
                            question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 880 + checkpoint * 40);
                        A(correct.Learning.Learning.IsCorrect && correct.Learning.Learning.QuestionCompleted &&
                          !correct.Learning.Learning.CanRetry &&
                          correct.State.Phase == MathRescueGameplayPhase.ANSWER_FEEDBACK && !correct.State.CanAnswer,
                            "checkpoint_" + checkpoint + "_correct_answer_enters_feedback");
                    }

                    var checkpointComplete = game.AdvanceAfterFeedback();
                    A(checkpointComplete.Phase == MathRescueGameplayPhase.CHECKPOINT_COMPLETE &&
                      checkpointComplete.Progress.CompletedCheckpoints == checkpoint &&
                      Math.Abs(checkpointComplete.Progress.Fraction - checkpoint / 3.0) < 0.000001,
                        "checkpoint_" + checkpoint + "_completion_updates_progress_exactly_once");

                    var next = game.AdvanceCheckpoint();
                    A(next.Phase == MathRescueGameplayPhase.NEXT_CHECKPOINT && !next.CanAnswer,
                        "checkpoint_" + checkpoint + "_passes_through_next_checkpoint_phase");

                    var continued = game.ContinueAfterCheckpoint();
                    if (checkpoint < 3)
                    {
                        A(continued.Completion == null && continued.State.Phase == MathRescueGameplayPhase.CHECKPOINT_READY &&
                          continued.State.CurrentCheckpoint == checkpoint + 1,
                            "checkpoint_" + checkpoint + "_continues_to_next_ready_checkpoint");
                    }
                    else
                    {
                        A(continued.Completion != null && continued.Completion.LearningSummary.LessonCompleted &&
                          continued.State.Phase == MathRescueGameplayPhase.GAME_COMPLETE && continued.State.Completed &&
                          !continued.State.CanAnswer && !continued.State.Resumable,
                            "third_checkpoint_transitions_to_game_complete");
                        A(continued.State.CompletionSummary != null &&
                          continued.State.CompletionSummary.Attempts == 3 &&
                          continued.State.Progress.CompletedCheckpoints == 3 &&
                          Math.Abs(continued.State.Progress.Fraction - 1.0) < 0.000001,
                            "game_complete_exposes_reward_and_ui_completion_contract");
                    }
                }

                A(phases.Count >= 15 && phases[0] == MathRescueGameplayPhase.INTRO &&
                  phases[phases.Count - 1] == MathRescueGameplayPhase.GAME_COMPLETE &&
                  reasons.Contains("repair_required") && reasons.Contains("game_completed"),
                    "state_changed_event_exposes_full_ui_transition_stream");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id=@session;", "@session", start.State.SessionId) == 4,
                    "headless_journey_has_three_finalized_questions_plus_one_retry_attempt");
            }
        }

        private static void TestTransitionRaceAndDoubleSubmit(
            string root,
            string schemaPath,
            string templatePath,
            string eventPath)
        {
            var database = NewDatabase(Path.Combine(root, "transition-race.db"), schemaPath);
            using (var game = NewGame(database, templatePath, eventPath, 91002))
            {
                var start = game.Start("Bé rescue race");
                game.AcknowledgeIntro();

                int beginSucceeded;
                int beginRejected;
                Exception beginUnexpected;
                RunTwoWayRace(delegate { game.BeginQuestion(); }, out beginSucceeded, out beginRejected, out beginUnexpected);
                A(beginUnexpected == null && beginSucceeded == 1 && beginRejected == 1,
                    "concurrent_begin_question_has_one_deterministic_winner");
                var active = game.CurrentState;
                A(active.Phase == MathRescueGameplayPhase.QUESTION_ACTIVE && active.CurrentQuestion != null && active.CanAnswer,
                    "question_race_leaves_one_valid_active_question");

                var answer = active.CurrentQuestion.CorrectAnswerDisplay;
                int submitSucceeded;
                int submitRejected;
                Exception submitUnexpected;
                RunTwoWayRace(
                    delegate { game.SubmitAnswerAt(answer, 0, "race", DateTime.UtcNow, 900); },
                    out submitSucceeded, out submitRejected, out submitUnexpected);
                A(submitUnexpected == null && submitSucceeded == 1 && submitRejected == 1,
                    "concurrent_double_submit_has_one_deterministic_winner");
                A(game.CurrentState.Phase == MathRescueGameplayPhase.ANSWER_FEEDBACK && !game.CurrentState.CanAnswer,
                    "submit_race_closes_answer_gate_immediately_after_winner");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id=@session;", "@session", start.State.SessionId) == 1,
                    "double_submit_race_persists_exactly_one_attempt");
            }
        }

        private static void TestExactRepairResumeToken(
            string root,
            string schemaPath,
            string templatePath,
            string eventPath)
        {
            var database = NewDatabase(Path.Combine(root, "repair-resume.db"), schemaPath);
            MathRescueGameplayResumeToken token;
            string sessionId;
            string questionId;

            using (var first = NewGame(database, templatePath, eventPath, 91003))
            {
                var start = first.Start("Bé rescue resume");
                sessionId = start.State.SessionId;
                first.AcknowledgeIntro();
                var question = first.BeginQuestion().CurrentQuestion;
                questionId = question.QuestionId;
                var wrong = first.SubmitAnswerAt(WrongAnswer(question), 0, "smoke", DateTime.UtcNow, 930);
                A(wrong.Learning.Learning.CanRetry, "resume_fixture_creates_retry_pending_first_attempt");
                var repair = first.AdvanceAfterFeedback();
                A(repair.Phase == MathRescueGameplayPhase.REPAIR && repair.RetryPending && repair.CanAnswer,
                    "resume_fixture_reaches_repair_phase");

                var suspended = first.SuspendForBreak("child_break");
                A(suspended.State.Paused && suspended.State.Resumable && !suspended.State.CanAnswer &&
                  suspended.ResumeToken.Phase == MathRescueGameplayPhase.REPAIR &&
                  suspended.ResumeToken.RetryPending,
                    "suspend_freezes_gameplay_and_returns_exact_repair_token");
                token = Json.Deserialize<MathRescueGameplayResumeToken>(Json.Serialize(suspended.ResumeToken));
                A(token != null && token.CurrentQuestion != null && token.CurrentQuestion.QuestionId == questionId,
                    "resume_token_roundtrips_through_json_for_persistence_owner");
            }

            using (var resumed = NewGame(database, templatePath, eventPath, 12345))
            {
                var start = resumed.Resume("Bé rescue resume", token);
                A(start.ResumedFromGameplayToken && start.Learning.Session.ResumedExistingSession &&
                  start.State.SessionId == sessionId,
                    "resume_reuses_same_durable_learning_session");
                A(start.State.Phase == MathRescueGameplayPhase.REPAIR && start.State.CurrentCheckpoint == 1 &&
                  start.State.RetryPending && start.State.CanAnswer && start.State.CurrentQuestion != null &&
                  start.State.CurrentQuestion.QuestionId == questionId,
                    "resume_restores_exact_repair_phase_checkpoint_and_question");

                var repaired = resumed.SubmitRepairAnswerAt(
                    start.State.CurrentQuestion.CorrectAnswerDisplay, 0, "resume", DateTime.UtcNow, 1010);
                A(repaired.Learning.Learning.IsCorrect && repaired.Learning.Learning.QuestionCompleted,
                    "resumed_repair_finalizes_original_question");
                resumed.AdvanceAfterFeedback();
                resumed.AdvanceCheckpoint();
                var checkpoint2 = resumed.ContinueAfterCheckpoint();
                A(checkpoint2.State.Phase == MathRescueGameplayPhase.CHECKPOINT_READY &&
                  checkpoint2.State.CurrentCheckpoint == 2,
                    "resumed_flow_continues_to_checkpoint_two");

                CompleteCorrectCheckpoint(resumed, 2);
                var final = CompleteCorrectCheckpoint(resumed, 3);
                A(final != null && final.Completion != null && final.State.Completed &&
                  final.State.Phase == MathRescueGameplayPhase.GAME_COMPLETE &&
                  final.Completion.LearningSummary.LessonCompleted,
                    "resumed_game_reaches_game_complete_without_replaying_checkpoint_one");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id=@session;", "@session", sessionId) == 4,
                    "resume_preserves_one_wrong_retry_plus_two_later_checkpoints_exactly_once");
            }
        }

        private static MathRescueGameplayTransitionResult CompleteCorrectCheckpoint(
            MathRescueGameplayCoordinator game,
            int expectedCheckpoint)
        {
            var question = game.BeginQuestion();
            A(question.Phase == MathRescueGameplayPhase.QUESTION_ACTIVE &&
              question.CurrentCheckpoint == expectedCheckpoint && question.CurrentQuestion != null,
                "resume_checkpoint_" + expectedCheckpoint + "_starts_expected_question");
            var answer = game.SubmitAnswerAt(
                question.CurrentQuestion.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 920 + expectedCheckpoint * 20);
            A(answer.Learning.Learning.IsCorrect && answer.Learning.Learning.QuestionCompleted,
                "resume_checkpoint_" + expectedCheckpoint + "_answers_correctly");
            game.AdvanceAfterFeedback();
            game.AdvanceCheckpoint();
            return game.ContinueAfterCheckpoint();
        }

        private static void RunTwoWayRace(Action action, out int succeeded, out int invalid, out Exception unexpected)
        {
            var start = new ManualResetEvent(false);
            var successCount = 0;
            var invalidCount = 0;
            Exception unexpectedError = null;
            var unexpectedGate = new object();
            ThreadStart body = delegate
            {
                start.WaitOne();
                try
                {
                    action();
                    Interlocked.Increment(ref successCount);
                }
                catch (InvalidOperationException)
                {
                    Interlocked.Increment(ref invalidCount);
                }
                catch (Exception ex)
                {
                    lock (unexpectedGate)
                    {
                        if (unexpectedError == null) unexpectedError = ex;
                    }
                }
            };
            var first = new Thread(body);
            var second = new Thread(body);
            first.Start();
            second.Start();
            start.Set();
            first.Join();
            second.Join();
            start.Dispose();
            succeeded = successCount;
            invalid = invalidCount;
            unexpected = unexpectedError;
        }

        private static MathRescueGameplayCoordinator NewGame(
            LearningDatabase database,
            string templatePath,
            string eventPath,
            int seed)
        {
            return new MathRescueGameplayCoordinator(
                database, templatePath, eventPath, "LOW", seed, FirstEventId, FirstLessonId);
        }

        private static string WrongAnswer(MathQuestion question)
        {
            if (question == null) throw new ArgumentNullException("question");
            if (question.DisplayChoices != null)
            {
                foreach (var candidate in question.DisplayChoices)
                    if (!question.IsCorrectAnswer(candidate)) return candidate;
            }
            for (var delta = 1; delta < 1000; delta++)
            {
                var candidate = (question.CorrectAnswer + delta).ToString(CultureInfo.InvariantCulture);
                if (!question.IsCorrectAnswer(candidate)) return candidate;
            }
            return "__wrong_answer__";
        }

        private static LearningDatabase NewDatabase(string dbPath, string schemaPath)
        {
            var database = new LearningDatabase(dbPath, schemaPath);
            var init = database.Initialize("DELETE");
            A(init.SchemaVersion == 6 && init.Health.IsHealthy,
                "database_ready_v6_" + Path.GetFileNameWithoutExtension(dbPath));
            return database;
        }

        private static int Count(LearningDatabase database, string sql, params object[] parameters)
        {
            using (var connection = database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                for (var i = 0; i + 1 < parameters.Length; i += 2)
                    command.Parameters.AddWithValue(Convert.ToString(parameters[i], CultureInfo.InvariantCulture), parameters[i + 1]);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
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
