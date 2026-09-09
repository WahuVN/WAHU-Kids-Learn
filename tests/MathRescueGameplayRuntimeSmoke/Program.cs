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
                TestMultiThreadTransitionStress(root, schemaPath, templatePath, eventPath);
                TestExactRepairResumeToken(root, schemaPath, templatePath, eventPath);
                TestResumeTokenPhaseMatrix(root, schemaPath, templatePath, eventPath);
                TestTamperedResumeTokenFailsClosed(root, schemaPath, templatePath, eventPath);
                TestObserverFailureCannotPoisonGameplay(root, schemaPath, templatePath, eventPath);
                TestPublicBoundaryMutationIsolation(root, schemaPath, templatePath, eventPath);
                TestTerminalAndReplayLifecycle(root, schemaPath, templatePath, eventPath);

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

        private static void TestMultiThreadTransitionStress(
            string root,
            string schemaPath,
            string templatePath,
            string eventPath)
        {
            const int workers = 12;
            var database = NewDatabase(Path.Combine(root, "multi-thread-transition-stress.db"), schemaPath);
            using (var game = NewGame(database, templatePath, eventPath, 91004))
            {
                var start = game.Start("Parallel transition stress");
                AssertRaceWinner(workers, delegate { game.AcknowledgeIntro(); }, "stress_intro_ack");

                for (var checkpoint = 1; checkpoint <= 3; checkpoint++)
                {
                    AssertRaceWinner(workers, delegate { game.BeginQuestion(); }, "stress_begin_checkpoint_" + checkpoint);
                    var active = game.CurrentState;
                    A(active.Phase == MathRescueGameplayPhase.QUESTION_ACTIVE &&
                      active.CurrentCheckpoint == checkpoint && active.CurrentQuestion != null,
                        "stress_checkpoint_" + checkpoint + "_has_one_active_question");
                    var question = active.CurrentQuestion;

                    if (checkpoint == 2)
                    {
                        var wrongAnswer = WrongAnswer(question);
                        AssertRaceWinner(workers,
                            delegate { game.SubmitAnswerAt(wrongAnswer, 0, "stress", DateTime.UtcNow, 900); },
                            "stress_wrong_submit_checkpoint_2");
                        A(game.CurrentState.Phase == MathRescueGameplayPhase.ANSWER_FEEDBACK &&
                          game.CurrentState.RetryPending && !game.CurrentState.CanAnswer,
                            "stress_wrong_submit_leaves_single_retry_feedback");
                        AssertRaceWinner(workers, delegate { game.AdvanceAfterFeedback(); }, "stress_enter_repair_checkpoint_2");
                        A(game.CurrentState.Phase == MathRescueGameplayPhase.REPAIR && game.CurrentState.CanAnswer,
                            "stress_repair_phase_is_single_and_answerable");
                        AssertRaceWinner(workers,
                            delegate { game.SubmitRepairAnswerAt(question.CorrectAnswerDisplay, 0, "stress", DateTime.UtcNow, 950); },
                            "stress_repair_submit_checkpoint_2");
                    }
                    else
                    {
                        AssertRaceWinner(workers,
                            delegate { game.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "stress", DateTime.UtcNow, 900); },
                            "stress_correct_submit_checkpoint_" + checkpoint);
                    }

                    A(game.CurrentState.Phase == MathRescueGameplayPhase.ANSWER_FEEDBACK && !game.CurrentState.CanAnswer,
                        "stress_checkpoint_" + checkpoint + "_feedback_closes_answer_gate");
                    AssertRaceWinner(workers, delegate { game.AdvanceAfterFeedback(); },
                        "stress_checkpoint_complete_" + checkpoint);
                    A(game.CurrentState.Phase == MathRescueGameplayPhase.CHECKPOINT_COMPLETE &&
                      game.CurrentState.Progress.CompletedCheckpoints == checkpoint,
                        "stress_checkpoint_" + checkpoint + "_progress_committed_once");
                    AssertRaceWinner(workers, delegate { game.AdvanceCheckpoint(); },
                        "stress_advance_checkpoint_" + checkpoint);
                    AssertRaceWinner(workers, delegate { game.ContinueAfterCheckpoint(); },
                        "stress_continue_checkpoint_" + checkpoint);

                    if (checkpoint < 3)
                    {
                        A(game.CurrentState.Phase == MathRescueGameplayPhase.CHECKPOINT_READY &&
                          game.CurrentState.CurrentCheckpoint == checkpoint + 1,
                            "stress_checkpoint_" + checkpoint + "_continues_once_to_next_ready");
                    }
                    else
                    {
                        A(game.CurrentState.Phase == MathRescueGameplayPhase.GAME_COMPLETE &&
                          game.CurrentState.Completed && game.CurrentState.Progress.CompletedCheckpoints == 3,
                            "stress_terminal_transition_has_one_game_complete_winner");
                    }
                }

                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id=@session;", "@session", start.State.SessionId) == 4,
                    "stress_twelve_way_races_persist_exactly_three_questions_plus_one_retry_attempt");
            }
        }

        private static void AssertRaceWinner(int workers, Action action, string name)
        {
            int succeeded;
            int invalid;
            Exception unexpected;
            RunManyWayRace(workers, action, out succeeded, out invalid, out unexpected);
            A(unexpected == null && succeeded == 1 && invalid == workers - 1,
                name + "_one_winner_rest_rejected");
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

        private static void TestResumeTokenPhaseMatrix(
            string root,
            string schemaPath,
            string templatePath,
            string eventPath)
        {
            var database = NewDatabase(Path.Combine(root, "resume-phase-matrix.db"), schemaPath);
            MathRescueGameplayResumeToken token;
            string sessionId;
            string questionId;

            using (var game = NewGame(database, templatePath, eventPath, 92001))
            {
                var start = game.Start("Bé resume phase matrix");
                sessionId = start.State.SessionId;
                var suspended = game.SuspendForBreak("matrix_intro");
                token = CloneToken(suspended.ResumeToken);
                A(token.Phase == MathRescueGameplayPhase.INTRO && token.CompletedCheckpointCount == 0,
                    "resume_matrix_captures_intro");
            }

            using (var game = NewGame(database, templatePath, eventPath, 1))
            {
                var resumed = game.Resume("Bé resume phase matrix", token);
                A(resumed.State.Phase == MathRescueGameplayPhase.INTRO && resumed.State.SessionId == sessionId,
                    "resume_matrix_restores_intro");
                game.AcknowledgeIntro();
                token = CloneToken(game.SuspendForBreak("matrix_ready").ResumeToken);
                A(token.Phase == MathRescueGameplayPhase.CHECKPOINT_READY && token.CurrentQuestion == null,
                    "resume_matrix_captures_checkpoint_ready");
            }

            using (var game = NewGame(database, templatePath, eventPath, 2))
            {
                var resumed = game.Resume("Bé resume phase matrix", token);
                A(resumed.State.Phase == MathRescueGameplayPhase.CHECKPOINT_READY && resumed.State.CurrentCheckpoint == 1,
                    "resume_matrix_restores_checkpoint_ready");
                var active = game.BeginQuestion();
                questionId = active.CurrentQuestion.QuestionId;
                token = CloneToken(game.SuspendForBreak("matrix_question_active").ResumeToken);
                A(token.Phase == MathRescueGameplayPhase.QUESTION_ACTIVE &&
                  token.CurrentQuestion != null && token.CurrentQuestion.QuestionId == questionId,
                    "resume_matrix_captures_question_active");
            }

            using (var game = NewGame(database, templatePath, eventPath, 3))
            {
                var resumed = game.Resume("Bé resume phase matrix", token);
                A(resumed.State.Phase == MathRescueGameplayPhase.QUESTION_ACTIVE && resumed.State.CanAnswer &&
                  resumed.State.CurrentQuestion.QuestionId == questionId,
                    "resume_matrix_restores_question_active");
                var wrong = game.SubmitAnswerAt(
                    WrongAnswer(resumed.State.CurrentQuestion), 0, "matrix", DateTime.UtcNow, 900);
                A(wrong.State.Phase == MathRescueGameplayPhase.ANSWER_FEEDBACK && wrong.State.RetryPending,
                    "resume_matrix_reaches_retry_feedback");
                token = CloneToken(game.SuspendForBreak("matrix_retry_feedback").ResumeToken);
                A(token.Phase == MathRescueGameplayPhase.ANSWER_FEEDBACK && token.RetryPending &&
                  token.FeedbackState == MathRescueFeedbackState.TRY_AGAIN,
                    "resume_matrix_captures_retry_feedback");
            }

            using (var game = NewGame(database, templatePath, eventPath, 4))
            {
                var resumed = game.Resume("Bé resume phase matrix", token);
                A(resumed.State.Phase == MathRescueGameplayPhase.ANSWER_FEEDBACK && resumed.State.RetryPending &&
                  resumed.State.CurrentQuestion.QuestionId == questionId,
                    "resume_matrix_restores_retry_feedback");
                var repair = game.AdvanceAfterFeedback();
                A(repair.Phase == MathRescueGameplayPhase.REPAIR && repair.CanAnswer,
                    "resume_matrix_advances_to_repair");
                token = CloneToken(game.SuspendForBreak("matrix_repair").ResumeToken);
                A(token.Phase == MathRescueGameplayPhase.REPAIR && token.RetryPending &&
                  token.FeedbackState == MathRescueFeedbackState.REPAIR,
                    "resume_matrix_captures_repair");
            }

            using (var game = NewGame(database, templatePath, eventPath, 5))
            {
                var resumed = game.Resume("Bé resume phase matrix", token);
                A(resumed.State.Phase == MathRescueGameplayPhase.REPAIR && resumed.State.CanAnswer &&
                  resumed.State.CurrentQuestion.QuestionId == questionId,
                    "resume_matrix_restores_repair");
                var repaired = game.SubmitRepairAnswerAt(
                    resumed.State.CurrentQuestion.CorrectAnswerDisplay, 0, "matrix", DateTime.UtcNow, 980);
                A(repaired.State.Phase == MathRescueGameplayPhase.ANSWER_FEEDBACK &&
                  repaired.State.AnswerState == MathRescueAnswerState.CORRECT && !repaired.State.RetryPending,
                    "resume_matrix_reaches_final_feedback");
                token = CloneToken(game.SuspendForBreak("matrix_final_feedback").ResumeToken);
                A(token.Phase == MathRescueGameplayPhase.ANSWER_FEEDBACK && !token.RetryPending &&
                  token.FeedbackState == MathRescueFeedbackState.CORRECT,
                    "resume_matrix_captures_final_feedback");
            }

            using (var game = NewGame(database, templatePath, eventPath, 6))
            {
                var resumed = game.Resume("Bé resume phase matrix", token);
                A(resumed.State.Phase == MathRescueGameplayPhase.ANSWER_FEEDBACK &&
                  resumed.State.AnswerState == MathRescueAnswerState.CORRECT &&
                  resumed.State.Progress.CompletedCheckpoints == 1,
                    "resume_matrix_restores_final_feedback");
                var checkpointComplete = game.AdvanceAfterFeedback();
                A(checkpointComplete.Phase == MathRescueGameplayPhase.CHECKPOINT_COMPLETE &&
                  checkpointComplete.CurrentQuestion == null,
                    "resume_matrix_advances_to_checkpoint_complete");
                token = CloneToken(game.SuspendForBreak("matrix_checkpoint_complete").ResumeToken);
                A(token.Phase == MathRescueGameplayPhase.CHECKPOINT_COMPLETE &&
                  token.CompletedCheckpointCount == 1 && token.CurrentQuestion == null,
                    "resume_matrix_captures_checkpoint_complete");
            }

            using (var game = NewGame(database, templatePath, eventPath, 7))
            {
                var resumed = game.Resume("Bé resume phase matrix", token);
                A(resumed.State.Phase == MathRescueGameplayPhase.CHECKPOINT_COMPLETE &&
                  resumed.State.Progress.CompletedCheckpoints == 1,
                    "resume_matrix_restores_checkpoint_complete");
                var next = game.AdvanceCheckpoint();
                A(next.Phase == MathRescueGameplayPhase.NEXT_CHECKPOINT && next.AnswerState == MathRescueAnswerState.NONE,
                    "resume_matrix_advances_to_next_checkpoint_phase");
                token = CloneToken(game.SuspendForBreak("matrix_next_checkpoint").ResumeToken);
                A(token.Phase == MathRescueGameplayPhase.NEXT_CHECKPOINT &&
                  token.CompletedCheckpointCount == 1 && token.CurrentCheckpoint == 1,
                    "resume_matrix_captures_next_checkpoint");
            }

            using (var game = NewGame(database, templatePath, eventPath, 8))
            {
                var resumed = game.Resume("Bé resume phase matrix", token);
                A(resumed.State.Phase == MathRescueGameplayPhase.NEXT_CHECKPOINT &&
                  resumed.State.Progress.CompletedCheckpoints == 1,
                    "resume_matrix_restores_next_checkpoint");
                var continued = game.ContinueAfterCheckpoint();
                A(continued.State.Phase == MathRescueGameplayPhase.CHECKPOINT_READY &&
                  continued.State.CurrentCheckpoint == 2,
                    "resume_matrix_continues_deterministically_to_checkpoint_two");
                game.SuspendForBreak("matrix_cleanup");
            }
        }

        private static void TestTamperedResumeTokenFailsClosed(
            string root,
            string schemaPath,
            string templatePath,
            string eventPath)
        {
            var database = NewDatabase(Path.Combine(root, "tampered-repair-token.db"), schemaPath);
            MathRescueGameplayResumeToken repairToken;
            string sessionId;
            string questionId;
            using (var game = NewGame(database, templatePath, eventPath, 92002))
            {
                var start = game.Start("Bé tampered repair token");
                sessionId = start.State.SessionId;
                game.AcknowledgeIntro();
                var question = game.BeginQuestion().CurrentQuestion;
                questionId = question.QuestionId;
                game.SubmitAnswerAt(WrongAnswer(question), 0, "tamper", DateTime.UtcNow, 910);
                game.AdvanceAfterFeedback();
                repairToken = CloneToken(game.SuspendForBreak("tamper_repair_fixture").ResumeToken);
            }

            var badRepairToken = CloneToken(repairToken);
            badRepairToken.Phase = MathRescueGameplayPhase.CHECKPOINT_COMPLETE;
            var rejected = false;
            using (var game = NewGame(database, templatePath, eventPath, 999))
            {
                try { game.Resume("Bé tampered repair token", badRepairToken); }
                catch (InvalidOperationException) { rejected = true; }
            }
            A(rejected, "tampered_repair_token_is_rejected");
            A(Count(database,
                "SELECT count(*) FROM session WHERE id=@session AND state IN ('started','active') AND ended_at_utc IS NULL;",
                "@session", sessionId) == 1 &&
              Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id=@session;", "@session", sessionId) == 1,
                "tampered_repair_token_keeps_durable_session_resumable");

            using (var game = NewGame(database, templatePath, eventPath, 1000))
            {
                var recovered = game.Start("Bé tampered repair token");
                A(recovered.Learning.Session.ResumedExistingSession &&
                  recovered.State.Phase == MathRescueGameplayPhase.REPAIR && recovered.State.RetryPending &&
                  recovered.State.CurrentQuestion != null && recovered.State.CurrentQuestion.QuestionId == questionId,
                    "durable_learning_recovers_exact_repair_after_bad_token");
                game.SuspendForBreak("tamper_repair_cleanup");
            }

            var finalDatabase = NewDatabase(Path.Combine(root, "tampered-final-feedback-token.db"), schemaPath);
            MathRescueGameplayResumeToken finalToken;
            string finalSessionId;
            using (var game = NewGame(finalDatabase, templatePath, eventPath, 92003))
            {
                var start = game.Start("Bé tampered final token");
                finalSessionId = start.State.SessionId;
                game.AcknowledgeIntro();
                var question = game.BeginQuestion().CurrentQuestion;
                game.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "tamper", DateTime.UtcNow, 920);
                finalToken = CloneToken(game.SuspendForBreak("tamper_final_fixture").ResumeToken);
                A(finalToken.Phase == MathRescueGameplayPhase.ANSWER_FEEDBACK &&
                  finalToken.CompletedCheckpointCount == 1 && finalToken.CurrentQuestion != null,
                    "tampered_final_fixture_has_final_feedback_token");
            }

            var badFinalToken = CloneToken(finalToken);
            badFinalToken.CurrentQuestion.ContentQuestionId = "__tampered_content_question__";
            rejected = false;
            using (var game = NewGame(finalDatabase, templatePath, eventPath, 1001))
            {
                try { game.Resume("Bé tampered final token", badFinalToken); }
                catch (InvalidOperationException) { rejected = true; }
            }
            A(rejected, "tampered_final_feedback_question_is_rejected");
            A(Count(finalDatabase,
                "SELECT count(*) FROM session WHERE id=@session AND state IN ('started','active') AND ended_at_utc IS NULL;",
                "@session", finalSessionId) == 1,
                "tampered_final_feedback_keeps_durable_session_active");

            using (var game = NewGame(finalDatabase, templatePath, eventPath, 1002))
            {
                var recovered = game.Start("Bé tampered final token");
                A(recovered.Learning.Session.ResumedExistingSession &&
                  recovered.State.Phase == MathRescueGameplayPhase.CHECKPOINT_COMPLETE &&
                  recovered.State.Progress.CompletedCheckpoints == 1,
                    "durable_learning_recovers_checkpoint_complete_after_bad_final_token");
                game.SuspendForBreak("tamper_final_cleanup");
            }
        }

        private static void TestObserverFailureCannotPoisonGameplay(
            string root,
            string schemaPath,
            string templatePath,
            string eventPath)
        {
            var database = NewDatabase(Path.Combine(root, "observer-failure.db"), schemaPath);
            using (var game = NewGame(database, templatePath, eventPath, 92004))
            {
                var delivered = 0;
                var reentryRejected = 0;
                game.StateChanged += delegate { throw new Exception("synthetic UI observer failure"); };
                game.StateChanged += delegate(object sender, MathRescueGameplayStateChangedEventArgs args)
                {
                    delivered++;
                    if (args.Reason == "start")
                    {
                        try { game.AcknowledgeIntro(); }
                        catch (InvalidOperationException) { reentryRejected++; }
                    }
                };

                var start = game.Start("Bé observer failure");
                A(start.State.Phase == MathRescueGameplayPhase.INTRO && game.CurrentState.Phase == MathRescueGameplayPhase.INTRO,
                    "observer_failure_does_not_advance_or_fail_start");
                A(delivered == 1 && reentryRejected == 1,
                    "observer_failure_isolated_and_synchronous_reentry_rejected");

                game.AcknowledgeIntro();
                var question = game.BeginQuestion().CurrentQuestion;
                var answer = game.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "observer", DateTime.UtcNow, 900);
                A(answer.Learning.Learning.IsCorrect && answer.State.Phase == MathRescueGameplayPhase.ANSWER_FEEDBACK,
                    "observer_failure_does_not_poison_answer_transition");
                A(delivered == 4,
                    "later_observer_still_receives_every_transition_after_throwing_subscriber");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id=@session;", "@session", start.State.SessionId) == 1,
                    "observer_failure_cannot_duplicate_or_rollback_durable_attempt");
                game.SuspendForBreak("observer_cleanup");
            }
        }

        private static void TestPublicBoundaryMutationIsolation(
            string root,
            string schemaPath,
            string templatePath,
            string eventPath)
        {
            var database = NewDatabase(Path.Combine(root, "public-boundary-mutation.db"), schemaPath);
            using (var game = NewGame(database, templatePath, eventPath, 92007))
            {
                var laterObserverStartOk = false;
                var laterObserverQuestionOk = false;
                game.StateChanged += delegate(object sender, MathRescueGameplayStateChangedEventArgs args)
                {
                    args.Reason = "tampered_reason";
                    if (args.Current == null) return;
                    args.Current.Phase = MathRescueGameplayPhase.GAME_COMPLETE;
                    if (args.Current.Progress != null) args.Current.Progress.CompletedCheckpoints = 99;
                    if (args.Current.CurrentQuestion != null) args.Current.CurrentQuestion.PromptVi = "tampered_question";
                    if (args.Current.EventState != null)
                    {
                        args.Current.EventState.SessionId = "tampered_session";
                        if (args.Current.EventState.Action != null) args.Current.EventState.Action.Action = "tampered_action";
                    }
                };
                game.StateChanged += delegate(object sender, MathRescueGameplayStateChangedEventArgs args)
                {
                    if (args.Reason == "start")
                        laterObserverStartOk = args.Current != null && args.Current.Phase == MathRescueGameplayPhase.INTRO &&
                            args.Current.Progress != null && args.Current.Progress.CompletedCheckpoints == 0;
                    if (args.Reason == "question_started")
                        laterObserverQuestionOk = args.Current != null &&
                            args.Current.Phase == MathRescueGameplayPhase.QUESTION_ACTIVE &&
                            args.Current.CurrentQuestion != null && args.Current.CurrentQuestion.PromptVi != "tampered_question";
                };

                var start = game.Start("Boundary mutation child");
                var sessionId = start.State.SessionId;
                var eventId = start.State.EventId;
                var selectedFirst = start.Learning.Session.SelectedContentQuestionIds[0];
                var completionCopy = start.Learning.Event == null ? null : start.Learning.Event.CompletionVi;

                start.State.Phase = MathRescueGameplayPhase.GAME_COMPLETE;
                start.State.Progress.CompletedCheckpoints = 77;
                start.State.EventState.SessionId = "mutated_state_session";
                if (start.State.EventState.Action != null) start.State.EventState.Action.Action = "mutated_state_action";
                start.Learning.Session.SessionId = "mutated_learning_session";
                start.Learning.Session.SelectedContentQuestionIds[0] = "mutated_selected_question";
                if (start.Learning.Event != null)
                {
                    start.Learning.Event.CompletionVi = "mutated_completion_copy";
                    start.Learning.Event.CheckpointNounsVi[0] = "mutated_checkpoint_copy";
                }
                if (start.Learning.EventState.Action != null) start.Learning.EventState.Action.Action = "mutated_learning_action";

                var afterStartMutation = game.CurrentState;
                A(laterObserverStartOk && afterStartMutation.Phase == MathRescueGameplayPhase.INTRO &&
                  afterStartMutation.SessionId == sessionId && afterStartMutation.EventId == eventId &&
                  afterStartMutation.Progress.CompletedCheckpoints == 0,
                    "public_start_and_event_subscriber_mutation_cannot_poison_core_snapshot");
                A(afterStartMutation.EventState.SessionId == sessionId &&
                  afterStartMutation.EventState.SelectedContentQuestionIds[0] == selectedFirst &&
                  (afterStartMutation.EventState.Action == null || afterStartMutation.EventState.Action.Action != "mutated_learning_action"),
                    "public_learning_start_nested_objects_are_defensively_detached");

                game.AcknowledgeIntro();
                var activeResult = game.BeginQuestion();
                var questionId = activeResult.CurrentQuestion.QuestionId;
                var prompt = activeResult.CurrentQuestion.PromptVi;
                activeResult.CurrentQuestion.PromptVi = "mutated_returned_question";
                if (activeResult.CurrentQuestion.ChoiceTexts != null && activeResult.CurrentQuestion.ChoiceTexts.Count > 0)
                    activeResult.CurrentQuestion.ChoiceTexts[0] = "mutated_choice";
                var activeCore = game.CurrentState;
                A(laterObserverQuestionOk && activeCore.Phase == MathRescueGameplayPhase.QUESTION_ACTIVE &&
                  activeCore.CurrentQuestion.QuestionId == questionId && activeCore.CurrentQuestion.PromptVi == prompt,
                    "returned_question_and_first_subscriber_mutation_do_not_affect_later_state");

                var answer = game.SubmitAnswerAt(activeCore.CurrentQuestion.CorrectAnswerDisplay, 0, "boundary", DateTime.UtcNow, 900);
                if (answer.Learning.Learning.Behavior != null && answer.Learning.Learning.Behavior.Actions != null)
                    answer.Learning.Learning.Behavior.Actions.Add("mutated_behavior_action");
                if (answer.Learning.EventState.Action != null) answer.Learning.EventState.Action.Action = "mutated_answer_action";
                answer.State.AnswerState = MathRescueAnswerState.WRONG;
                answer.State.Progress.CompletedCheckpoints = 88;
                var answerCore = game.CurrentState;
                A(answerCore.Phase == MathRescueGameplayPhase.ANSWER_FEEDBACK &&
                  answerCore.AnswerState == MathRescueAnswerState.CORRECT &&
                  answerCore.Progress.CompletedCheckpoints == 1 &&
                  (answerCore.EventState.Action == null || answerCore.EventState.Action.Action != "mutated_answer_action"),
                    "answer_result_mutation_cannot_poison_committed_gameplay_or_event_action");

                game.AdvanceAfterFeedback();
                game.AdvanceCheckpoint();
                game.ContinueAfterCheckpoint();
                CompleteCorrectCheckpoint(game, 2);
                var final = CompleteCorrectCheckpoint(game, 3);
                A(final.Completion != null && final.State.Phase == MathRescueGameplayPhase.GAME_COMPLETE,
                    "boundary_mutation_fixture_reaches_terminal_state");

                final.Completion.LearningSummary.Attempts = 999;
                if (final.Completion.LearningSummary.GardenUnlockedItemIds != null)
                    final.Completion.LearningSummary.GardenUnlockedItemIds.Add("mutated_reward_item");
                final.State.CompletionSummary.Attempts = 998;
                final.State.Phase = MathRescueGameplayPhase.IDLE;
                if (final.State.EventState.Action != null) final.State.EventState.Action.Action = "mutated_terminal_action";

                var terminalCore = game.CurrentState;
                A(terminalCore.Phase == MathRescueGameplayPhase.GAME_COMPLETE && terminalCore.Completed &&
                  terminalCore.CompletionSummary != null && terminalCore.CompletionSummary.Attempts == 3 &&
                  terminalCore.Progress.CompletedCheckpoints == 3,
                    "completion_result_and_snapshot_mutation_cannot_rewrite_terminal_core_state");
                A(terminalCore.FeedbackVi == completionCopy &&
                  (terminalCore.EventState.Action == null || terminalCore.EventState.Action.Action != "mutated_terminal_action"),
                    "start_event_definition_and_terminal_event_action_are_isolated_from_public_mutation");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id=@session;", "@session", sessionId) == 3,
                    "public_object_mutation_never_changes_durable_attempt_count");
            }
        }
        private static void TestTerminalAndReplayLifecycle(
            string root,
            string schemaPath,
            string templatePath,
            string eventPath)
        {
            var database = NewDatabase(Path.Combine(root, "terminal-replay-lifecycle.db"), schemaPath);
            string completedSessionId;
            using (var game = NewGame(database, templatePath, eventPath, 92005))
            {
                var start = game.Start("Bé terminal replay");
                completedSessionId = start.State.SessionId;

                var duplicateStartRejected = false;
                try { game.Start("Bé terminal replay"); }
                catch (InvalidOperationException) { duplicateStartRejected = true; }
                A(duplicateStartRejected && game.CurrentState.Phase == MathRescueGameplayPhase.INTRO,
                    "same_gameplay_coordinator_rejects_duplicate_start_without_state_change");

                game.AcknowledgeIntro();
                var afterOne = CompleteCorrectCheckpoint(game, 1);
                A(afterOne.Completion == null && afterOne.State.Phase == MathRescueGameplayPhase.CHECKPOINT_READY &&
                  afterOne.State.CurrentCheckpoint == 2,
                    "terminal_replay_fixture_completes_checkpoint_one");
                var afterTwo = CompleteCorrectCheckpoint(game, 2);
                A(afterTwo.Completion == null && afterTwo.State.CurrentCheckpoint == 3,
                    "terminal_replay_fixture_completes_checkpoint_two");
                var final = CompleteCorrectCheckpoint(game, 3);
                A(final.Completion != null && final.State.Phase == MathRescueGameplayPhase.GAME_COMPLETE &&
                  final.State.Completed && final.State.Progress.CompletedCheckpoints == 3,
                    "terminal_replay_fixture_reaches_game_complete");

                var duplicateCompleteRejected = false;
                try { game.ContinueAfterCheckpoint(); }
                catch (InvalidOperationException) { duplicateCompleteRejected = true; }
                var terminalSuspendRejected = false;
                try { game.SuspendForBreak("should_not_suspend_completed_game"); }
                catch (InvalidOperationException) { terminalSuspendRejected = true; }
                A(duplicateCompleteRejected && terminalSuspendRejected &&
                  game.CurrentState.Phase == MathRescueGameplayPhase.GAME_COMPLETE,
                    "game_complete_is_terminal_for_transition_and_suspend_apis");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id=@session;", "@session", completedSessionId) == 3,
                    "terminal_rejections_do_not_append_learning_attempts");
            }

            using (var replay = NewGame(database, templatePath, eventPath, 92006))
            {
                var start = replay.Start("Bé terminal replay");
                A(!start.Learning.Session.ResumedExistingSession && start.State.SessionId != completedSessionId,
                    "replay_after_completed_game_creates_new_session");
                A(start.State.Phase == MathRescueGameplayPhase.INTRO &&
                  start.State.Progress.CompletedCheckpoints == 0 && start.State.CurrentCheckpoint == 1 &&
                  !start.State.Completed,
                    "replay_after_completed_game_restarts_from_intro_zero_of_three");
                A(Count(database,
                    "SELECT count(*) FROM session WHERE id=@session AND state='completed' AND ended_at_utc IS NOT NULL;",
                    "@session", completedSessionId) == 1,
                    "replay_does_not_reopen_or_mutate_completed_session");
                replay.SuspendForBreak("replay_cleanup");
            }
        }

        private static MathRescueGameplayResumeToken CloneToken(MathRescueGameplayResumeToken token)
        {
            return Json.Deserialize<MathRescueGameplayResumeToken>(Json.Serialize(token));
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

        private static void RunManyWayRace(
            int workers,
            Action action,
            out int succeeded,
            out int invalid,
            out Exception unexpected)
        {
            if (workers < 2) throw new ArgumentOutOfRangeException("workers");
            var start = new ManualResetEvent(false);
            var threads = new List<Thread>();
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
            for (var i = 0; i < workers; i++)
            {
                var thread = new Thread(body);
                threads.Add(thread);
                thread.Start();
            }
            start.Set();
            foreach (var thread in threads) thread.Join();
            start.Dispose();
            succeeded = successCount;
            invalid = invalidCount;
            unexpected = unexpectedError;
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
