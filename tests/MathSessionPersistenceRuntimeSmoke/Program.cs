using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using WAHU.Content;
using WAHU.Data;
using WAHU.Learning;
using WAHU.Session;

namespace WAHU.MathSessionPersistenceRuntimeSmoke
{
    internal static class Program
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        private static int _assertions;

        private sealed class SyntheticPoolPack
        {
            public string TemplatePath { get; set; }
            public string LessonId { get; set; }
            public IList<string> Basic { get; set; }
            public IList<string> Medium { get; set; }
            public IList<string> Application { get; set; }
        }

        private static int Main()
        {
            var root = Path.Combine(Path.GetTempPath(), "wahu-math-session-persistence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var repo = FindRepoRoot();
                var schemaPath = Path.Combine(repo, "data", "schema", "001_initial.sql");
                var templatePath = Path.Combine(repo, "content_packs", "math_grade2_v1", "verified_templates_v1.json");
                var questionBankPath = Path.Combine(repo, "content_packs", "math_grade2_v1", "question_bank_v1.json");
                var lessonCatalogPath = Path.Combine(repo, "content_packs", "math_grade2_v1", "lesson_catalog_v1.json");
                var gameEventPath = Path.Combine(repo, "content_packs", "math_grade2_v1", "game_events_v1.json");
                TestAuthoredQuestionBank(questionBankPath);
                TestRealPoolSelectionBreadth(lessonCatalogPath);
                TestAnswerUnitFeedbackSurvivesCoordinatorResume(root, schemaPath, templatePath, lessonCatalogPath);
                TestTargetedLessonUnlockAndResume(root, schemaPath, templatePath, lessonCatalogPath);
                TestFirstFiveLessonsGoldenPath(root, schemaPath, templatePath, lessonCatalogPath);
                TestFirstFiveAllAuthoredVariantsEndToEnd(root, schemaPath, templatePath, lessonCatalogPath);
                TestFirstFiveRetryErrorMatrix(root, schemaPath, templatePath, lessonCatalogPath);
                TestFirstFiveHintedSuccessMatrix(root, schemaPath, templatePath, lessonCatalogPath);
                TestFirstLessonAllSixVariantsWithRetryResume(root, schemaPath, templatePath, lessonCatalogPath);
                TestTargetedCompletionSurvivesNextLessonReadFailure(root, schemaPath, templatePath, lessonCatalogPath);
                TestCorruptRuntimeMetadataIsQuarantined(root, schemaPath, templatePath);
                TestOperationalDatabaseFailureDoesNotQuarantineRuntime(root, schemaPath, templatePath);
                TestRuntimePackIdentityResumePolicy(root, schemaPath, templatePath);
                TestTargetedCorruptOpenQuestionReplaysAuthoredOrdinal(root, schemaPath, templatePath, lessonCatalogPath);
                TestTargetedGeneratedOrdinalSelfHealsWithoutOpenQuestion(root, schemaPath, templatePath, lessonCatalogPath);
                TestTargetedSemanticOpenQuestionOrdinalMismatchSelfHeals(root, schemaPath, templatePath, lessonCatalogPath, questionBankPath);
                TestTargetedTamperedAuthoredCacheIsDiscarded(root, schemaPath, templatePath, lessonCatalogPath);
                TestExpandedTargetedPoolSelectsDurableThree(root, schemaPath, templatePath, lessonCatalogPath, questionBankPath);
                TestTargetedSelectedSetSurvivesCommitFailure(root, schemaPath, templatePath, lessonCatalogPath);
                TestTargetedConcurrentCoordinatorsCannotDuplicateOrdinal(root, schemaPath, templatePath, lessonCatalogPath);
                TestTargetedConcurrentPendingRetryKeepsFirstTrySemantics(root, schemaPath, templatePath, lessonCatalogPath);
                TestAdaptiveConcurrentCoordinatorsShareSemanticOrdinalId(root, schemaPath, templatePath);
                TestTargetedResumeDiscardsStaleConcurrentOrdinalCache(root, schemaPath, templatePath, lessonCatalogPath);
                TestRetryAwareAnswerFlow(root, schemaPath, templatePath);
                TestRetryWrongFinalizesOnce(root, schemaPath, templatePath);
                TestStaleCoordinatorCannotAppendAfterTerminalSession(root, schemaPath, templatePath);
                TestCoordinatorRejectsStaleSkillSnapshot(root, schemaPath, templatePath);
                TestCommitFailureRollsBackAndRestoresBehavior(root, schemaPath, templatePath);
                TestLateReviewFailureRollsBackEntireLearningChain(root, schemaPath, templatePath);
                TestResumeOpenQuestionAndComplete(root, schemaPath, templatePath);
                TestCommittedStaleQuestionIsNotReplayed(root, schemaPath, templatePath);
                TestCorruptOpenQuestionRecoversWithoutProgressReset(root, schemaPath, templatePath);
                TestAdaptiveTamperedOpenQuestionIsDiscarded(root, schemaPath, templatePath);
                TestDeterministicSecondQuestionAcrossResume(root, schemaPath, templatePath);
                TestInteractiveIntegerFinalization();
                TestGameEventBehaviorMapping();
                TestGameEventRuntimeResumeRewardAndFallback(root, schemaPath, templatePath, lessonCatalogPath);
                TestGameEventCorruptCatalogCompletesLearningSafely(root, schemaPath, templatePath, lessonCatalogPath);
                TestGameEventStaleIdFallsBackToLessonEvent(root, schemaPath, templatePath, lessonCatalogPath, gameEventPath);
                TestGameEventMismatchedEventLessonFailsBeforeSession(root, schemaPath, templatePath, lessonCatalogPath, gameEventPath);
                TestProductionFirstFiveGameEventsRuntime(root, schemaPath, templatePath, lessonCatalogPath, gameEventPath);
                TestProductionFirstEventResumeJourney(root, schemaPath, templatePath, lessonCatalogPath, gameEventPath);
                TestGameEventResumeAfterThirdAnswerBeforeComplete(root, schemaPath, templatePath, lessonCatalogPath, gameEventPath);
                TestGameEventCompletedEventReopensAsFreshReplay(root, schemaPath, templatePath, lessonCatalogPath, gameEventPath);
                TestGameEventPublicLifecycleIsFailClosed(root, schemaPath, templatePath, lessonCatalogPath, gameEventPath);
                TestGameEventDisposeSuspendsAndResumesExactQuestion(root, schemaPath, templatePath, lessonCatalogPath, gameEventPath);
                TestGameEventResumeRestoresBehaviorAction(root, schemaPath, templatePath, lessonCatalogPath, gameEventPath);
                TestGameEventResumeRestoresExplicitRepairBeforeStateTransition(root, schemaPath, templatePath, lessonCatalogPath, gameEventPath);
                TestGameEventDifferentSelectionResumesDurableActiveEvent(root, schemaPath, templatePath, lessonCatalogPath, gameEventPath);
                TestGameEventRewardFaultReconcilesOnNextStart(root, schemaPath, templatePath, lessonCatalogPath, gameEventPath);
                TestGameEventConcurrentRewardReconcileIsIdempotent(root, schemaPath, templatePath, lessonCatalogPath, gameEventPath);
                TestGameEventTerminalRuntimeCleanupReconcilesOnNextStart(root, schemaPath, templatePath, lessonCatalogPath, gameEventPath);
                TestGameEventCommitFaultPreservesCheckpoint(root, schemaPath, templatePath, lessonCatalogPath);
                TestGameEventConcurrentCoordinatorsStayIdempotent(root, schemaPath, templatePath, lessonCatalogPath);
                TestGameEventCompleteVsSuspendRaceStaysTerminal(root, schemaPath, templatePath, lessonCatalogPath);
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

        private static void TestGameEventBehaviorMapping()
        {
            var ready = MathGameEventBehaviorMapper.Map(new BehaviorDecision { State = BehaviorState.READY });
            A(ready.Action == "normal" && !ready.OfferBreak && !ready.UseRepair && ready.PreserveCheckpoint,
                "game_event_behavior_ready_maps_normal");
            var flow = MathGameEventBehaviorMapper.Map(new BehaviorDecision { State = BehaviorState.FLOW_LIKELY });
            A(flow.Action == "minimize_interruptions" && flow.MinimizeInterruptions && flow.PreserveCheckpoint,
                "game_event_behavior_flow_minimizes_interruptions");
            var bored = MathGameEventBehaviorMapper.Map(new BehaviorDecision { State = BehaviorState.BORED_OR_UNDERCHALLENGED });
            A(bored.Action == "context_transfer" && !bored.OfferBreak && bored.PreserveCheckpoint,
                "game_event_behavior_bored_uses_transfer_without_more_questions");
            var strained = MathGameEventBehaviorMapper.Map(new BehaviorDecision { State = BehaviorState.STRAINED });
            A(strained.Action == "small_cue" && strained.UseSmallCue && strained.PreserveCheckpoint,
                "game_event_behavior_strained_uses_small_cue");
            var frustrated = MathGameEventBehaviorMapper.Map(new BehaviorDecision { State = BehaviorState.FRUSTRATED_LIKELY });
            A(frustrated.Action == "repair" && frustrated.UseRepair && frustrated.PreserveCheckpoint,
                "game_event_behavior_frustrated_uses_repair_without_losing_checkpoint");
            var fatigued = MathGameEventBehaviorMapper.Map(new BehaviorDecision { State = BehaviorState.FATIGUED_LIKELY });
            A(fatigued.Action == "offer_break" && fatigued.OfferBreak && fatigued.SuggestPositiveClose && fatigued.PreserveCheckpoint,
                "game_event_behavior_fatigued_offers_safe_break_and_positive_close");
            var explicitRepair = MathGameEventBehaviorMapper.Map(new BehaviorDecision
            {
                State = BehaviorState.STRAINED,
                TriggerPrerequisiteRepair = true,
                Actions = new List<string> { "small_cue", "prerequisite_repair" }
            });
            A(explicitRepair.Action == "repair" && explicitRepair.UseRepair && explicitRepair.PreserveCheckpoint,
                "game_event_behavior_explicit_prerequisite_repair_overrides_state_only_small_cue");
        }

        private static void TestGameEventRuntimeResumeRewardAndFallback(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath)
        {
            var eventPath = Path.Combine(root, "synthetic-game-events-v1.json");
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var lesson = catalog.Lessons[0];
            var eventId = "m2_evt_test_rescue_01";
            WriteSyntheticGameEventCatalog(eventPath, eventId, lesson.Id, lesson.SkillId);

            var loaded = new MathGameEventCatalogSource().Load(eventPath, lessonCatalogPath);
            A(loaded.SchemaVersion == 1 && loaded.CatalogId == MathGameEventCatalogSource.CatalogId &&
              loaded.Events.Count == 1 && loaded.Events[0].QuestionCount == 3,
                "game_event_catalog_loads_frozen_v1_schema");
            A(loaded.FindById(eventId) != null && loaded.FindByLesson(lesson.Id) != null &&
              loaded.Events[0].CheckpointNounsVi.SequenceEqual(new[] { "biển số 1", "biển số 2", "biển số 3" }),
                "game_event_catalog_supports_deterministic_id_and_lesson_lookup");

            var strictPath = Path.Combine(root, "synthetic-game-events-extra-key.json");
            File.Copy(eventPath, strictPath, true);
            var strictRaw = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(strictPath));
            var strictEvents = ((System.Collections.IEnumerable)strictRaw["events"]).Cast<object>().ToList();
            var strictEvent = (Dictionary<string, object>)strictEvents[0];
            strictEvent["timer_seconds"] = 30;
            strictRaw["events"] = strictEvents;
            File.WriteAllText(strictPath, Json.Serialize(strictRaw));
            MathGameEventCatalog ignoredCatalog;
            A(!new MathGameEventCatalogSource().TryLoad(strictPath, lessonCatalogPath, out ignoredCatalog),
                "game_event_catalog_rejects_unknown_timer_like_schema_field");

            var database = NewDatabase(Path.Combine(root, "game-event-runtime.db"), schemaPath);
            string sessionId;
            string q2Id;
            IList<string> selected;
            using (var first = new MathGameEventCoordinator(database, templatePath, eventPath, "LOW", 14001, eventId, lesson.Id))
            {
                var start = first.Start("Bé event cứu hộ");
                sessionId = start.Session.SessionId;
                selected = start.Session.SelectedContentQuestionIds.ToList();
                A(start.Event != null && start.Event.Id == eventId && start.EventState.EventPresentationAvailable &&
                  !start.EventState.FallbackToLessonPresentation && start.EventState.CompletedCheckpointCount == 0 &&
                  start.EventState.CurrentCheckpointNumber == 1 && start.EventState.CurrentCheckpointNounVi == "biển số 1",
                    "game_event_start_binds_event_and_checkpoint_one");
                A(start.Session.SessionMode == "lesson" && start.Session.TargetLessonId == lesson.Id &&
                  start.Session.TargetQuestionCount == 3 && selected.Count == 3,
                    "game_event_start_reuses_exact_targeted_three_question_session");

                var earlyRejected = false;
                try { first.Complete(); }
                catch (InvalidOperationException) { earlyRejected = true; }
                A(earlyRejected && Count(database, "SELECT count(*) FROM reward_event WHERE child_id='" + start.Session.ChildId + "';") == 0,
                    "game_event_early_complete_is_rejected_without_reward");

                var q1 = first.NextQuestion();
                var r1 = first.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                A(r1.Learning.IsCorrect && r1.Learning.QuestionCompleted &&
                  r1.EventState.CompletedCheckpointCount == 1 && r1.EventState.CurrentCheckpointNumber == 2 &&
                  r1.EventState.CurrentCheckpointNounVi == "biển số 2",
                    "game_event_correct_answer_advances_one_checkpoint");

                var q2 = first.NextQuestion();
                q2Id = q2.QuestionId;
                var wrong = first.SubmitAnswerWithRetryAt(WrongAnswer(q2), 2, "smoke", DateTime.UtcNow, 800);
                A(!wrong.Learning.IsCorrect && wrong.Learning.CanRetry && !wrong.Learning.QuestionCompleted &&
                  wrong.EventState.CompletedCheckpointCount == 1 && wrong.EventState.CurrentCheckpointNumber == 2 &&
                  wrong.EventState.RetryPending,
                    "game_event_wrong_retry_preserves_checkpoint_two");
                var retryEarlyCompleteRejected = false;
                try { first.Complete(); }
                catch (InvalidOperationException) { retryEarlyCompleteRejected = true; }
                A(retryEarlyCompleteRejected && first.CurrentState.RetryPending &&
                  first.CurrentState.CompletedCheckpointCount == 1 && first.NextQuestion().QuestionId == q2Id &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE child_id='" + start.Session.ChildId + "';") == 0,
                    "game_event_complete_during_pending_retry_is_rejected_without_state_loss_or_reward");
                first.SuspendForBreak("event_safe_break");
                A(Count(database, "SELECT count(*) FROM reward_event WHERE child_id='" + start.Session.ChildId + "';") == 0 &&
                  Count(database, "SELECT count(*) FROM session WHERE id='" + sessionId + "' AND state='active';") == 1,
                    "game_event_suspend_preserves_active_session_and_grants_no_reward");
            }

            using (var resumed = new MathGameEventCoordinator(database, templatePath, eventPath, "NORMAL", 999999, null, lesson.Id))
            {
                var start = resumed.Start("Bé event cứu hộ");
                A(start.Session.ResumedExistingSession && start.Session.SessionId == sessionId &&
                  start.Session.SelectedContentQuestionIds.SequenceEqual(selected) && start.Session.RetryPending &&
                  start.EventState.EventId == eventId && start.EventState.CompletedCheckpointCount == 1 &&
                  start.EventState.CurrentCheckpointNumber == 2 && start.EventState.RetryPending,
                    "game_event_resume_derives_event_from_unique_target_lesson_and_restores_checkpoint_retry");
                var q2 = resumed.NextQuestion();
                A(q2.QuestionId == q2Id && q2.ContentQuestionId == selected[1],
                    "game_event_resume_restores_exact_open_question");
                var retry = resumed.SubmitRetryAnswerAt(q2.CorrectAnswerDisplay, 1, "smoke", DateTime.UtcNow, 850);
                A(retry.Learning.IsCorrect && retry.Learning.IsRetry && !retry.Learning.IndependentSuccess &&
                  retry.EventState.CompletedCheckpointCount == 2 && retry.EventState.CurrentCheckpointNumber == 3,
                    "game_event_retry_correct_advances_to_third_checkpoint_as_assisted");
                var q3 = resumed.NextQuestion();
                var q3Answer = resumed.SubmitAnswerAt(q3.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 900);
                A(q3Answer.EventState.CompletedCheckpointCount == 3 && !q3Answer.EventState.IsComplete &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + sessionId + "';") == 0,
                    "game_event_three_checkpoints_ready_but_not_terminal_before_complete");
                var complete = resumed.Complete();
                A(complete.LearningSummary.LessonCompleted && complete.LearningSummary.Attempts == 3 &&
                  complete.EventState.IsComplete && complete.EventState.CompletedCheckpointCount == 3 &&
                  complete.EventState.EventId == eventId,
                    "game_event_completion_finishes_three_checkpoint_targeted_session");
                A(complete.LearningSummary.GardenGrowthSteps == 1 &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE child_id='" + start.Session.ChildId +
                    "' AND reward_type='garden_growth' AND source_ref='" + sessionId + "';") == 1,
                    "game_event_completion_grants_existing_garden_reward_exactly_once");
                var replay = resumed.Complete();
                A(object.ReferenceEquals(complete, replay) &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + sessionId + "';") == 1,
                    "game_event_completion_replay_is_wrapper_idempotent_and_does_not_duplicate_reward");
            }

            var fallbackDb = NewDatabase(Path.Combine(root, "game-event-corrupt-fallback.db"), schemaPath);
            string fallbackSessionId;
            string fallbackQ2Id;
            IList<string> fallbackSelected;
            string fallbackChildId;
            WriteSyntheticGameEventCatalog(eventPath, eventId, lesson.Id, lesson.SkillId);
            using (var first = new MathGameEventCoordinator(fallbackDb, templatePath, eventPath, "LOW", 14002, eventId, lesson.Id))
            {
                var start = first.Start("Bé event fallback");
                fallbackSessionId = start.Session.SessionId;
                fallbackSelected = start.Session.SelectedContentQuestionIds.ToList();
                fallbackChildId = start.Session.ChildId;
                var q1 = first.NextQuestion();
                first.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                var q2 = first.NextQuestion();
                fallbackQ2Id = q2.QuestionId;
                var wrong = first.SubmitAnswerWithRetryAt(WrongAnswer(q2), 2, "smoke", DateTime.UtcNow, 800);
                A(!wrong.Learning.IsCorrect && wrong.Learning.CanRetry && !wrong.Learning.QuestionCompleted &&
                  wrong.EventState.RetryPending && wrong.EventState.CompletedCheckpointCount == 1,
                    "game_event_corrupt_metadata_fixture_suspends_with_pending_retry");
                first.SuspendForBreak("event_before_metadata_corruption");
            }
            File.WriteAllText(eventPath, "{ definitely-not-valid-event-json ");
            using (var fallback = new MathGameEventCoordinator(fallbackDb, templatePath, eventPath, "LOW", 123, eventId, lesson.Id))
            {
                var start = fallback.Start("Bé event fallback");
                A(start.Session.ResumedExistingSession && start.Session.SessionId == fallbackSessionId &&
                  start.Session.SelectedContentQuestionIds.SequenceEqual(fallbackSelected) && start.Session.CompletedQuestionCount == 1 &&
                  start.Session.RetryPending && start.EventState.RetryPending,
                    "game_event_corrupt_metadata_fallback_preserves_math_session_progress_and_retry_state");
                A(start.Event == null && !start.EventState.EventPresentationAvailable && start.EventState.FallbackToLessonPresentation &&
                  start.EventState.EventId == null && start.EventState.TargetLessonId == lesson.Id,
                    "game_event_corrupt_metadata_falls_back_to_ordinary_lesson_presentation");
                var q2 = fallback.NextQuestion();
                A(q2.QuestionId == fallbackQ2Id && q2.ContentQuestionId == fallbackSelected[1],
                    "game_event_corrupt_metadata_fallback_keeps_exact_open_question");
                var retry = fallback.SubmitRetryAnswerAt(q2.CorrectAnswerDisplay, 2, "smoke", DateTime.UtcNow, 850);
                A(retry.Learning.IsCorrect && retry.Learning.IsRetry && !retry.Learning.IndependentSuccess &&
                  retry.Learning.QuestionCompleted && retry.EventState.CompletedCheckpointCount == 2 && !retry.EventState.RetryPending,
                    "game_event_corrupt_metadata_fallback_allows_exact_assisted_retry_without_event_presentation");
                fallback.SuspendForBreak("fallback_cleanup_suspend");
                A(Count(fallbackDb, "SELECT count(*) FROM attempt WHERE session_id='" + fallbackSessionId + "';") == 3 &&
                  Count(fallbackDb, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + fallbackSessionId + "';") == 2 &&
                  Count(fallbackDb, "SELECT count(*) FROM reward_event WHERE child_id='" + fallbackChildId + "';") == 0,
                    "game_event_corrupt_metadata_fallback_loses_no_learning_and_grants_no_fake_reward");
            }

            WriteSyntheticGameEventCatalog(eventPath, eventId, lesson.Id, lesson.SkillId);
            using (var restoredEvent = new MathGameEventCoordinator(fallbackDb, templatePath, eventPath, "NORMAL", 999999, null, lesson.Id))
            {
                var start = restoredEvent.Start("Bé event fallback");
                A(start.Session.ResumedExistingSession && start.Session.SessionId == fallbackSessionId &&
                  start.Session.CompletedQuestionCount == 2 && start.Session.SelectedContentQuestionIds.SequenceEqual(fallbackSelected),
                    "game_event_metadata_recovery_resumes_same_learning_session_after_fallback");
                A(start.Event != null && start.Event.Id == eventId && start.EventState.EventPresentationAvailable &&
                  !start.EventState.FallbackToLessonPresentation && start.EventState.CompletedCheckpointCount == 2 &&
                  start.EventState.CurrentCheckpointNumber == 3,
                    "game_event_metadata_recovery_restores_event_presentation_at_exact_checkpoint");
                var q3 = restoredEvent.NextQuestion();
                A(q3.ContentQuestionId == fallbackSelected[2] &&
                  Count(fallbackDb, "SELECT count(*) FROM reward_event WHERE child_id='" + fallbackChildId + "';") == 0,
                    "game_event_metadata_recovery_keeps_selected_q3_and_no_fake_reward");
                restoredEvent.SuspendForBreak("metadata_recovery_cleanup");
            }
        }

        private static void TestGameEventCorruptCatalogCompletesLearningSafely(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var lesson = catalog.Lessons[0];
            var eventId = "m2_evt_corrupt_complete_01";
            var eventPath = Path.Combine(root, "synthetic-game-event-corrupt-complete.json");
            WriteSyntheticGameEventCatalog(eventPath, eventId, lesson.Id, lesson.SkillId);
            var database = NewDatabase(Path.Combine(root, "game-event-corrupt-complete.db"), schemaPath);
            string sessionId;
            string childId;
            IList<string> selected;

            using (var first = new MathGameEventCoordinator(database, templatePath, eventPath, "LOW", 14003, eventId, lesson.Id))
            {
                var start = first.Start("Bé fallback hoàn thành");
                sessionId = start.Session.SessionId;
                childId = start.Session.ChildId;
                selected = start.Session.SelectedContentQuestionIds.ToList();
                var q1 = first.NextQuestion();
                first.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                first.SuspendForBreak("corrupt_complete_before_catalog_loss");
            }

            File.WriteAllText(eventPath, "{ broken-until-after-completion ");
            using (var fallback = new MathGameEventCoordinator(database, templatePath, eventPath, "NORMAL", 999999, eventId, lesson.Id))
            {
                var start = fallback.Start("Bé fallback hoàn thành");
                A(start.Session.ResumedExistingSession && start.Session.SessionId == sessionId &&
                  start.Event == null && start.EventState.FallbackToLessonPresentation &&
                  start.EventState.CompletedCheckpointCount == 1,
                    "game_event_corrupt_catalog_completion_resumes_learning_in_lesson_fallback");
                for (var ordinal = 1; ordinal < 3; ordinal++)
                {
                    var question = fallback.NextQuestion();
                    A(question.ContentQuestionId == selected[ordinal],
                        "game_event_corrupt_catalog_completion_keeps_selected_question_" + (ordinal + 1));
                    fallback.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 800 + ordinal * 100);
                }
                A(fallback.CurrentState.CompletedCheckpointCount == 3 && !fallback.CurrentState.IsComplete &&
                  fallback.CurrentState.FallbackToLessonPresentation,
                    "game_event_corrupt_catalog_completion_reaches_three_checkpoints_without_fake_terminal");
                var completed = fallback.Complete();
                A(completed.LearningSummary.LessonCompleted && completed.EventState.IsComplete &&
                  completed.EventState.FallbackToLessonPresentation && !completed.EventState.EventPresentationAvailable,
                    "game_event_corrupt_catalog_completion_terminalizes_learning_without_event_metadata");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + sessionId + "';") == 3 &&
                  Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + sessionId + "';") == 3 &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + sessionId + "';") == 1,
                    "game_event_corrupt_catalog_completion_keeps_exact_learning_chain_and_one_reward");
            }

            WriteSyntheticGameEventCatalog(eventPath, eventId, lesson.Id, lesson.SkillId);
            using (var replay = new MathGameEventCoordinator(database, templatePath, eventPath, "LOW", 14004, eventId, lesson.Id))
            {
                var start = replay.Start("Bé fallback hoàn thành");
                A(!start.Session.ResumedExistingSession && start.Session.SessionId != sessionId &&
                  start.Event != null && start.Event.Id == eventId && start.EventState.EventPresentationAvailable &&
                  start.EventState.CompletedCheckpointCount == 0 && !start.EventState.IsComplete,
                    "game_event_after_corrupt_catalog_completion_reopens_as_fresh_event_session");
                A(Count(database, "SELECT count(*) FROM reward_event WHERE child_id='" + childId + "';") == 1,
                    "game_event_after_corrupt_catalog_completion_does_not_duplicate_prior_reward");
                replay.SuspendForBreak("corrupt_complete_replay_cleanup");
            }
        }

        private static void TestGameEventStaleIdFallsBackToLessonEvent(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath,
            string gameEventPath)
        {
            var events = new MathGameEventCatalogSource().Load(gameEventPath, lessonCatalogPath);
            var expected = events.Events[0];
            var database = NewDatabase(Path.Combine(root, "game-event-stale-id-fallback.db"), schemaPath);
            using (var game = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW", 14951,
                "legacy_event_id_that_no_longer_exists", expected.TargetLessonId))
            {
                var start = game.Start("Bé stale event id");
                A(start.Event != null && start.Event.Id == expected.Id &&
                  start.EventState.EventPresentationAvailable && !start.EventState.FallbackToLessonPresentation,
                    "game_event_stale_requested_id_falls_back_to_current_event_by_lesson");
                A(start.Session.TargetLessonId == expected.TargetLessonId && start.Session.TargetQuestionCount == 3,
                    "game_event_stale_requested_id_keeps_targeted_three_question_session");
                game.SuspendForBreak("stale_event_id_cleanup");
            }
        }

        private static void TestGameEventMismatchedEventLessonFailsBeforeSession(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath,
            string gameEventPath)
        {
            var events = new MathGameEventCatalogSource().Load(gameEventPath, lessonCatalogPath);
            A(events.Events.Count >= 2, "game_event_mismatch_fixture_has_two_production_events");
            var requested = events.Events[0];
            var wrongLesson = events.Events[1].TargetLessonId;
            var database = NewDatabase(Path.Combine(root, "game-event-mismatched-lesson.db"), schemaPath);
            var rejected = false;
            using (var game = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW", 14961,
                requested.Id, wrongLesson))
            {
                try { game.Start("Bé stale mapping"); }
                catch (InvalidOperationException) { rejected = true; }
            }
            A(rejected,
                "game_event_mismatched_event_and_fallback_lesson_is_rejected_fail_closed");
            A(Count(database, "SELECT count(*) FROM session WHERE state='active';") == 0 &&
              Count(database, "SELECT count(*) FROM math_session_runtime;") == 0 &&
              Count(database, "SELECT count(*) FROM math_lesson_progress;") == 0 &&
              Count(database, "SELECT count(*) FROM reward_event;") == 0,
                "game_event_mismatch_fails_before_creating_session_progress_or_reward");
        }

        private static void TestProductionFirstFiveGameEventsRuntime(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath,
            string gameEventPath)
        {
            var lessonCatalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var events = new MathGameEventCatalogSource().Load(gameEventPath, lessonCatalogPath);
            var firstFive = lessonCatalog.Lessons.Take(5).ToList();
            A(events.Events.Count == 5 && events.Events.Select(x => x.TargetLessonId).SequenceEqual(firstFive.Select(x => x.Id)),
                "production_game_events_cover_exact_first_five_lessons_in_order");
            A(events.Events.Select(x => x.TargetSkillId).SequenceEqual(firstFive.Select(x => x.SkillId)) &&
              events.Events.All(x => x.QuestionCount == 3 && x.CheckpointNounsVi.Count == 3),
                "production_game_events_match_first_five_skills_and_three_checkpoints");

            var database = NewDatabase(Path.Combine(root, "production-first-five-game-events.db"), schemaPath);
            string childId = null;
            for (var eventIndex = 0; eventIndex < events.Events.Count; eventIndex++)
            {
                var definition = events.Events[eventIndex];
                using (var game = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW",
                    15000 + eventIndex, definition.Id, definition.TargetLessonId))
                {
                    var start = game.Start("Bé production 5 event");
                    if (childId == null) childId = start.Session.ChildId;
                    A(start.Session.ChildId == childId && start.Event != null && start.Event.Id == definition.Id &&
                      start.EventState.EventPresentationAvailable && !start.EventState.FallbackToLessonPresentation,
                        "production_game_event_starts_with_real_metadata_" + (eventIndex + 1));
                    A(start.Session.TargetLessonId == definition.TargetLessonId && start.Session.TargetQuestionCount == 3 &&
                      start.Session.SelectedContentQuestionIds.Count == 3 && start.EventState.CompletedCheckpointCount == 0 &&
                      start.EventState.CurrentCheckpointNumber == 1 &&
                      start.EventState.CurrentCheckpointNounVi == definition.CheckpointNounsVi[0],
                        "production_game_event_starts_exact_target_and_checkpoint_" + (eventIndex + 1));

                    for (var ordinal = 0; ordinal < 3; ordinal++)
                    {
                        var before = game.CurrentState;
                        A(before.CompletedCheckpointCount == ordinal && before.CurrentCheckpointNounVi == definition.CheckpointNounsVi[ordinal],
                            "production_game_event_checkpoint_noun_before_answer_" + eventIndex + "_" + ordinal);
                        var question = game.NextQuestion();
                        A(question != null && question.LessonId == definition.TargetLessonId &&
                          question.SkillId == definition.TargetSkillId &&
                          question.ContentQuestionId == start.Session.SelectedContentQuestionIds[ordinal],
                            "production_game_event_serves_selected_authored_question_" + eventIndex + "_" + ordinal);
                        var answer = game.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700 + ordinal * 100);
                        A(answer.Learning.IsCorrect && answer.Learning.QuestionCompleted &&
                          answer.EventState.CompletedCheckpointCount == ordinal + 1,
                            "production_game_event_answer_advances_checkpoint_" + eventIndex + "_" + ordinal);
                    }

                    var completion = game.Complete();
                    A(completion.LearningSummary.LessonCompleted && completion.LearningSummary.Attempts == 3 &&
                      completion.LearningSummary.Correct == 3 && completion.EventState.IsComplete &&
                      completion.EventState.CompletedCheckpointCount == 3,
                        "production_game_event_completes_three_questions_" + (eventIndex + 1));
                    A(completion.LearningSummary.GardenGrowthSteps == eventIndex + 1 &&
                      Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + start.Session.SessionId + "';") == 1,
                        "production_game_event_grants_one_predictable_garden_reward_" + (eventIndex + 1));
                    if (eventIndex + 1 < events.Events.Count)
                    {
                        var nextDefinition = events.Events[eventIndex + 1];
                        A(completion.LearningSummary.NextLessonId == nextDefinition.TargetLessonId &&
                          Count(database, "SELECT count(*) FROM session WHERE child_id='" + childId + "' AND state='active';") == 0 &&
                          Count(database, "SELECT count(*) FROM math_lesson_progress WHERE child_id='" + childId +
                          "' AND lesson_id='" + nextDefinition.TargetLessonId + "';") == 0,
                            "production_game_event_completion_recommends_but_does_not_autoplay_next_event_" + (eventIndex + 1));
                    }
                }
            }

            A(Count(database, "SELECT count(*) FROM session WHERE child_id='" + childId + "' AND state='completed';") == 5,
                "production_first_five_events_complete_exactly_five_sessions");
            A(Count(database, "SELECT count(*) FROM attempt WHERE child_id='" + childId + "';") == 15 &&
              Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.child_id='" + childId + "';") == 15,
                "production_first_five_events_commit_fifteen_attempts_and_mastery");
            A(Count(database, "SELECT count(*) FROM reward_event WHERE child_id='" + childId + "' AND reward_type='garden_growth';") == 5,
                "production_first_five_events_create_exactly_five_garden_rewards");
            A(Count(database, "SELECT count(*) FROM math_lesson_progress WHERE child_id='" + childId + "' AND completed_count=1;") == 5,
                "production_first_five_events_complete_five_lesson_progress_rows");
            A(Count(database, "SELECT count(*) FROM session WHERE child_id='" + childId + "' AND state='active';") == 0,
                "production_first_five_events_leave_no_active_session");
        }

        private static void TestProductionFirstEventResumeJourney(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath,
            string gameEventPath)
        {
            var events = new MathGameEventCatalogSource().Load(gameEventPath, lessonCatalogPath);
            var definition = events.Events[0];
            var database = NewDatabase(Path.Combine(root, "production-first-event-resume.db"), schemaPath);
            string sessionId;
            string childId;
            string openQuestionId;
            IList<string> selected;

            using (var first = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW",
                15101, definition.Id, definition.TargetLessonId))
            {
                var start = first.Start("Bé production resume event");
                sessionId = start.Session.SessionId;
                childId = start.Session.ChildId;
                selected = start.Session.SelectedContentQuestionIds.ToList();
                var q1 = first.NextQuestion();
                first.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                var q2 = first.NextQuestion();
                openQuestionId = q2.QuestionId;
                var wrong = first.SubmitAnswerWithRetryAt(WrongAnswer(q2), 2, "smoke", DateTime.UtcNow, 800);
                A(!wrong.Learning.IsCorrect && wrong.Learning.CanRetry && wrong.EventState.RetryPending &&
                  wrong.EventState.CompletedCheckpointCount == 1 && wrong.EventState.CurrentCheckpointNumber == 2,
                    "production_first_event_wrong_retry_preserves_second_checkpoint");
                first.SuspendForBreak("production_event_break");
                A(Count(database, "SELECT count(*) FROM reward_event WHERE child_id='" + childId + "';") == 0,
                    "production_first_event_suspend_grants_no_reward");
            }

            using (var resumed = new MathGameEventCoordinator(database, templatePath, gameEventPath, "NORMAL",
                999999, null, definition.TargetLessonId))
            {
                var start = resumed.Start("Bé production resume event");
                A(start.Session.ResumedExistingSession && start.Session.SessionId == sessionId &&
                  start.Event != null && start.Event.Id == definition.Id &&
                  start.Session.SelectedContentQuestionIds.SequenceEqual(selected),
                    "production_first_event_resume_derives_same_event_from_lesson");
                A(start.Session.RetryPending && start.Session.CurrentAttemptIndex == 2 &&
                  start.EventState.CompletedCheckpointCount == 1 && start.EventState.CurrentCheckpointNumber == 2,
                    "production_first_event_resume_restores_retry_checkpoint_two");
                var q2 = resumed.NextQuestion();
                A(q2.QuestionId == openQuestionId && q2.ContentQuestionId == selected[1],
                    "production_first_event_resume_restores_exact_open_medium");
                var retry = resumed.SubmitRetryAnswerAt(q2.CorrectAnswerDisplay, 1, "smoke", DateTime.UtcNow, 850);
                A(retry.Learning.IsCorrect && retry.Learning.IsRetry && !retry.Learning.IndependentSuccess &&
                  retry.EventState.CompletedCheckpointCount == 2,
                    "production_first_event_retry_correct_is_assisted_and_advances_checkpoint");
                var q3 = resumed.NextQuestion();
                resumed.SubmitAnswerAt(q3.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 900);
                var completion = resumed.Complete();
                A(completion.LearningSummary.LessonCompleted && completion.LearningSummary.AnswerAttempts == 4 &&
                  completion.LearningSummary.Attempts == 3 && completion.LearningSummary.RetriedQuestions == 1 &&
                  completion.EventState.IsComplete,
                    "production_first_event_resume_journey_completes_three_from_four_answers");
                A(Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + sessionId + "';") == 1 &&
                  Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + sessionId + "';") == 3,
                    "production_first_event_resume_journey_grants_one_reward_three_mastery");
            }
        }

        private static void TestGameEventResumeAfterThirdAnswerBeforeComplete(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath,
            string gameEventPath)
        {
            var events = new MathGameEventCatalogSource().Load(gameEventPath, lessonCatalogPath);
            var definition = events.Events[0];
            var database = NewDatabase(Path.Combine(root, "game-event-resume-after-q3.db"), schemaPath);
            string sessionId;
            string childId;
            IList<string> selected;

            using (var first = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW", 15121,
                definition.Id, definition.TargetLessonId))
            {
                var start = first.Start("Bé resume sau câu ba");
                sessionId = start.Session.SessionId;
                childId = start.Session.ChildId;
                selected = start.Session.SelectedContentQuestionIds.ToList();
                MathGameEventAnswerResult last = null;
                for (var ordinal = 0; ordinal < 3; ordinal++)
                {
                    var question = first.NextQuestion();
                    last = first.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700 + ordinal * 100);
                }
                A(last != null && last.EventState.CompletedCheckpointCount == 3 && !last.EventState.IsComplete &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + sessionId + "';") == 0,
                    "game_event_after_q3_before_complete_has_three_checkpoints_but_no_terminal_reward");
            }

            A(SessionState(database, sessionId) == "active" &&
              Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + sessionId + "';") == 3 &&
              Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + sessionId + "';") == 0,
                "game_event_dispose_after_q3_keeps_learning_active_without_reward");

            using (var resumed = new MathGameEventCoordinator(database, templatePath, gameEventPath, "NORMAL", 999999,
                null, definition.TargetLessonId))
            {
                var start = resumed.Start("Bé resume sau câu ba");
                A(start.Session.ResumedExistingSession && start.Session.SessionId == sessionId &&
                  start.Session.CompletedQuestionCount == 3 && start.Session.SelectedContentQuestionIds.SequenceEqual(selected),
                    "game_event_resume_after_q3_restores_three_completed_checkpoints");
                A(start.EventState.CompletedCheckpointCount == 3 && !start.EventState.IsComplete &&
                  start.EventState.CurrentCheckpointNumber == 3 && resumed.NextQuestion() == null,
                    "game_event_resume_after_q3_is_ready_to_complete_not_false_terminal");
                var completion = resumed.Complete();
                A(completion.EventState.IsComplete && completion.LearningSummary.LessonCompleted &&
                  completion.LearningSummary.Attempts == 3 && SessionState(database, sessionId) == "completed",
                    "game_event_resume_after_q3_terminalizes_existing_session");
                A(Count(database, "SELECT count(*) FROM reward_event WHERE child_id='" + childId +
                  "' AND source_ref='" + sessionId + "' AND reward_type='garden_growth';") == 1,
                    "game_event_resume_after_q3_grants_exactly_one_reward");
            }
        }

        private static void TestGameEventCompletedEventReopensAsFreshReplay(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath,
            string gameEventPath)
        {
            var events = new MathGameEventCatalogSource().Load(gameEventPath, lessonCatalogPath);
            var definition = events.Events[0];
            var database = NewDatabase(Path.Combine(root, "game-event-completed-replay.db"), schemaPath);
            string completedSessionId;
            string childId;

            using (var first = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW", 15123,
                definition.Id, definition.TargetLessonId))
            {
                var start = first.Start("Bé replay event đã xong");
                completedSessionId = start.Session.SessionId;
                childId = start.Session.ChildId;
                for (var ordinal = 0; ordinal < 3; ordinal++)
                {
                    var question = first.NextQuestion();
                    first.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700 + ordinal * 100);
                }
                var completed = first.Complete();
                A(completed.EventState.IsComplete && completed.LearningSummary.LessonCompleted &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + completedSessionId + "';") == 1,
                    "game_event_completed_replay_fixture_terminal_and_rewarded_once");
            }

            string replaySessionId;
            string replayQ1Id;
            IList<string> replaySelected;
            using (var replay = new MathGameEventCoordinator(database, templatePath, gameEventPath, "NORMAL", 15124,
                definition.Id, definition.TargetLessonId))
            {
                var start = replay.Start("Bé replay event đã xong");
                replaySessionId = start.Session.SessionId;
                replaySelected = start.Session.SelectedContentQuestionIds.ToList();
                A(!start.Session.ResumedExistingSession && replaySessionId != completedSessionId &&
                  start.Session.CompletedQuestionCount == 0 && !start.Session.RetryPending &&
                  start.EventState.CompletedCheckpointCount == 0 && start.EventState.CurrentCheckpointNumber == 1 &&
                  !start.EventState.IsComplete,
                    "game_event_completed_event_reopens_as_fresh_zero_checkpoint_session");
                A(Count(database, "SELECT count(*) FROM reward_event WHERE child_id='" + childId + "';") == 1 &&
                  Count(database, "SELECT count(*) FROM math_lesson_progress WHERE child_id='" + childId +
                  "' AND lesson_id='" + definition.TargetLessonId + "' AND started_count=2 AND completed_count=1;") == 1,
                    "game_event_fresh_replay_does_not_grant_reward_or_fake_completion_before_play");
                using (var racingReplay = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW", 515124,
                    definition.Id, definition.TargetLessonId))
                {
                    var raced = racingReplay.Start("Bé replay event đã xong");
                    A(raced.Session.ResumedExistingSession && raced.Session.SessionId == replaySessionId &&
                      raced.Session.SelectedContentQuestionIds.SequenceEqual(replaySelected) && raced.Session.CompletedQuestionCount == 0,
                        "game_event_concurrent_fresh_replay_binds_single_new_active_session");
                    A(Count(database, "SELECT count(*) FROM math_lesson_progress WHERE child_id='" + childId +
                      "' AND lesson_id='" + definition.TargetLessonId + "' AND started_count=2 AND completed_count=1;") == 1 &&
                      Count(database, "SELECT count(*) FROM reward_event WHERE child_id='" + childId + "';") == 1,
                        "game_event_concurrent_fresh_replay_does_not_double_start_or_reward");
                }
                var q1 = replay.NextQuestion();
                replayQ1Id = q1.QuestionId;
                A(q1.ContentQuestionId == replaySelected[0],
                    "game_event_fresh_replay_starts_from_selected_basic_checkpoint_one");
            }

            using (var resumedReplay = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW", 999999,
                null, definition.TargetLessonId))
            {
                var start = resumedReplay.Start("Bé replay event đã xong");
                A(start.Session.ResumedExistingSession && start.Session.SessionId == replaySessionId &&
                  start.Session.SelectedContentQuestionIds.SequenceEqual(replaySelected) &&
                  start.Session.CompletedQuestionCount == 0 && start.EventState.CompletedCheckpointCount == 0,
                    "game_event_fresh_replay_close_reopens_same_new_session_not_old_completed_session");
                A(resumedReplay.NextQuestion().QuestionId == replayQ1Id &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE child_id='" + childId + "';") == 1,
                    "game_event_fresh_replay_resume_keeps_exact_q1_and_existing_reward_count");
                resumedReplay.SuspendForBreak("completed_replay_cleanup");
            }
        }

        private static void TestGameEventPublicLifecycleIsFailClosed(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath,
            string gameEventPath)
        {
            var events = new MathGameEventCatalogSource().Load(gameEventPath, lessonCatalogPath);
            var definition = events.Events[0];
            var database = NewDatabase(Path.Combine(root, "game-event-public-lifecycle.db"), schemaPath);
            var game = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW", 15125,
                definition.Id, definition.TargetLessonId);
            var start = game.Start("Bé lifecycle cứu hộ");
            var sessionId = start.Session.SessionId;
            var childId = start.Session.ChildId;

            var doubleStartRejected = false;
            try { game.Start("Bé lifecycle cứu hộ"); }
            catch (InvalidOperationException) { doubleStartRejected = true; }
            A(doubleStartRejected && Count(database, "SELECT count(*) FROM session WHERE child_id='" + childId + "' AND state='active';") == 1 &&
              Count(database, "SELECT count(*) FROM math_lesson_progress WHERE child_id='" + childId +
              "' AND lesson_id='" + definition.TargetLessonId + "' AND started_count=1;") == 1,
                "game_event_double_start_rejected_without_duplicate_session_or_started_count");

            for (var ordinal = 0; ordinal < 3; ordinal++)
            {
                var question = game.NextQuestion();
                game.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700 + ordinal * 100);
            }
            var completed = game.Complete();
            var replay = game.Complete();
            A(object.ReferenceEquals(completed, replay) && completed.EventState.IsComplete && completed.LearningSummary.LessonCompleted,
                "game_event_terminal_complete_replay_returns_cached_result");

            var nextRejected = false;
            var submitRejected = false;
            var restartRejected = false;
            try { game.NextQuestion(); } catch (InvalidOperationException) { nextRejected = true; }
            try { game.SubmitAnswerAt("0", 0, "smoke", DateTime.UtcNow, 999); } catch (InvalidOperationException) { submitRejected = true; }
            var terminalSuspend = game.SuspendForBreak("after_terminal");
            try { game.Start("Bé lifecycle cứu hộ"); } catch (InvalidOperationException) { restartRejected = true; }
            A(nextRejected && submitRejected && restartRejected && terminalSuspend != null && terminalSuspend.Attempts == 3,
                "game_event_terminal_mutations_fail_closed_while_suspend_is_idempotent_noop");
            A(SessionState(database, sessionId) == "completed" &&
              Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + sessionId + "';") == 3 &&
              Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + sessionId + "';") == 3 &&
              Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + sessionId + "';") == 1,
                "game_event_terminal_rejected_calls_leave_learning_and_reward_exactly_once");
            A(game.CurrentState.IsComplete && game.CurrentState.CompletedCheckpointCount == 3 &&
              game.CurrentState.CurrentCheckpointNumber == 3 && !game.CurrentState.RetryPending,
                "game_event_terminal_current_state_remains_stable_after_rejected_calls");
            game.Dispose();
            game.Dispose();
            A(SessionState(database, sessionId) == "completed" &&
              Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + sessionId + "';") == 1,
                "game_event_terminal_dispose_is_idempotent_and_does_not_mutate_reward");
        }

        private static void TestGameEventDisposeSuspendsAndResumesExactQuestion(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath,
            string gameEventPath)
        {
            var events = new MathGameEventCatalogSource().Load(gameEventPath, lessonCatalogPath);
            var definition = events.Events[0];
            var database = NewDatabase(Path.Combine(root, "game-event-dispose-resume.db"), schemaPath);
            string sessionId;
            string childId;
            string q2Id;
            IList<string> selected;

            var first = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW", 15126,
                definition.Id, definition.TargetLessonId);
            var start = first.Start("Bé đóng cửa sổ cứu hộ");
            sessionId = start.Session.SessionId;
            childId = start.Session.ChildId;
            selected = start.Session.SelectedContentQuestionIds.ToList();
            var q1 = first.NextQuestion();
            first.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
            var q2 = first.NextQuestion();
            q2Id = q2.QuestionId;
            var wrong = first.SubmitAnswerWithRetryAt(WrongAnswer(q2), 1, "smoke", DateTime.UtcNow, 800);
            A(!wrong.Learning.IsCorrect && wrong.Learning.CanRetry && !wrong.Learning.QuestionCompleted &&
              wrong.EventState.RetryPending && wrong.EventState.CompletedCheckpointCount == 1,
                "game_event_dispose_pending_retry_fixture_is_open_before_close");
            first.Dispose();

            A(SessionState(database, sessionId) == "active" &&
              Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + sessionId + "';") == 2 &&
              Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + sessionId + "';") == 1 &&
              Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + sessionId + "';") == 0,
                "game_event_dispose_keeps_pending_retry_active_without_fake_mastery_or_reward");
            A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + sessionId + "';") == 1,
                "game_event_dispose_keeps_runtime_checkpoint_for_resume");

            using (var resumed = new MathGameEventCoordinator(database, templatePath, gameEventPath, "NORMAL", 999999,
                null, definition.TargetLessonId))
            {
                var resumedStart = resumed.Start("Bé đóng cửa sổ cứu hộ");
                A(resumedStart.Session.ResumedExistingSession && resumedStart.Session.SessionId == sessionId &&
                  resumedStart.Session.CompletedQuestionCount == 1 && resumedStart.Session.SelectedContentQuestionIds.SequenceEqual(selected) &&
                  resumedStart.Session.RetryPending,
                    "game_event_dispose_resume_restores_same_session_selected_set_and_retry_pending");
                A(resumedStart.EventState.CompletedCheckpointCount == 1 && resumedStart.EventState.CurrentCheckpointNumber == 2 &&
                  resumedStart.EventState.RetryPending && !resumedStart.EventState.IsComplete,
                    "game_event_dispose_resume_restores_checkpoint_two_pending_retry_not_terminal");
                var restoredQ2 = resumed.NextQuestion();
                A(restoredQ2.QuestionId == q2Id && restoredQ2.ContentQuestionId == selected[1],
                    "game_event_dispose_resume_restores_exact_open_q2");
                var retry = resumed.SubmitRetryAnswerAt(restoredQ2.CorrectAnswerDisplay, 1, "smoke", DateTime.UtcNow, 850);
                A(retry.Learning.IsCorrect && retry.Learning.IsRetry && !retry.Learning.IndependentSuccess &&
                  retry.EventState.CompletedCheckpointCount == 2 && !retry.EventState.RetryPending,
                    "game_event_dispose_resume_pending_retry_completes_as_assisted_once");
                resumed.SuspendForBreak("dispose_resume_cleanup");
            }

            A(Count(database, "SELECT count(*) FROM reward_event WHERE child_id='" + childId + "';") == 0,
                "game_event_dispose_resume_never_creates_completion_reward");
        }

        private static void TestGameEventResumeRestoresBehaviorAction(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath,
            string gameEventPath)
        {
            var events = new MathGameEventCatalogSource().Load(gameEventPath, lessonCatalogPath);
            var definition = events.Events[0];
            var database = NewDatabase(Path.Combine(root, "game-event-behavior-resume.db"), schemaPath);
            string sessionId;
            string openQ3Id;
            BehaviorState stateBeforeSuspend;
            string actionBeforeSuspend;

            using (var first = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW", 15131,
                definition.Id, definition.TargetLessonId))
            {
                var start = first.Start("Bé behavior resume");
                sessionId = start.Session.SessionId;

                for (var ordinal = 0; ordinal < 2; ordinal++)
                {
                    var question = first.NextQuestion();
                    var wrong1 = first.SubmitAnswerWithRetryAt(WrongAnswer(question), 2, "smoke", DateTime.UtcNow, 5000);
                    A(!wrong1.Learning.IsCorrect && wrong1.Learning.CanRetry && !wrong1.Learning.QuestionCompleted,
                        "game_event_behavior_resume_wrong_pending_" + ordinal);
                    var wrong2 = first.SubmitRetryAnswerAt(WrongAnswer(question), 2, "smoke", DateTime.UtcNow, 5000);
                    A(!wrong2.Learning.IsCorrect && wrong2.Learning.QuestionCompleted,
                        "game_event_behavior_resume_retry_wrong_finalizes_" + ordinal);
                    if (ordinal == 0)
                    {
                        A(wrong2.Learning.Behavior != null && wrong2.Learning.Behavior.State == BehaviorState.READY &&
                          wrong2.Learning.Behavior.TriggerPrerequisiteRepair && wrong2.EventState.Action.UseRepair &&
                          wrong2.EventState.Action.Action == "repair",
                            "game_event_runtime_explicit_repair_works_before_behavior_state_transition");
                    }
                }

                var q3 = first.NextQuestion();
                openQ3Id = q3.QuestionId;
                var q3Wrong = first.SubmitAnswerWithRetryAt(WrongAnswer(q3), 2, "smoke", DateTime.UtcNow, 5000);
                stateBeforeSuspend = q3Wrong.EventState.Action.BehaviorState;
                actionBeforeSuspend = q3Wrong.EventState.Action.Action;
                A(!q3Wrong.Learning.IsCorrect && q3Wrong.Learning.CanRetry && q3Wrong.EventState.RetryPending &&
                  q3Wrong.EventState.CompletedCheckpointCount == 2,
                    "game_event_behavior_resume_keeps_third_checkpoint_pending");
                A(stateBeforeSuspend != BehaviorState.READY && actionBeforeSuspend != "normal",
                    "game_event_behavior_resume_reaches_non_normal_support_action_before_suspend");
                first.SuspendForBreak("behavior_action_resume");
            }

            using (var resumed = new MathGameEventCoordinator(database, templatePath, gameEventPath, "NORMAL", 999999,
                null, definition.TargetLessonId))
            {
                var start = resumed.Start("Bé behavior resume");
                A(start.Session.ResumedExistingSession && start.Session.SessionId == sessionId &&
                  start.Session.RetryPending && start.EventState.CompletedCheckpointCount == 2,
                    "game_event_behavior_resume_restores_pending_third_checkpoint");
                A(start.EventState.Action.BehaviorState == stateBeforeSuspend &&
                  start.EventState.Action.Action == actionBeforeSuspend,
                    "game_event_behavior_resume_restores_last_durable_behavior_action");
                A(resumed.NextQuestion().QuestionId == openQ3Id,
                    "game_event_behavior_resume_keeps_exact_open_third_question");
                resumed.SuspendForBreak("behavior_resume_cleanup");
            }
        }

        private static void TestGameEventResumeRestoresExplicitRepairBeforeStateTransition(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath,
            string gameEventPath)
        {
            var events = new MathGameEventCatalogSource().Load(gameEventPath, lessonCatalogPath);
            var definition = events.Events[0];
            var database = NewDatabase(Path.Combine(root, "game-event-explicit-repair-resume.db"), schemaPath);
            string sessionId;
            string q2Id;

            using (var first = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW", 15141,
                definition.Id, definition.TargetLessonId))
            {
                var start = first.Start("Bé explicit repair resume");
                sessionId = start.Session.SessionId;
                var q1 = first.NextQuestion();
                var wrong1 = first.SubmitAnswerWithRetryAt(WrongAnswer(q1), 2, "smoke", DateTime.UtcNow, 5000);
                A(!wrong1.Learning.IsCorrect && wrong1.Learning.CanRetry,
                    "game_event_explicit_repair_resume_first_wrong_pending");
                var wrong2 = first.SubmitRetryAnswerAt(WrongAnswer(q1), 2, "smoke", DateTime.UtcNow, 5000);
                A(wrong2.Learning.Behavior != null && wrong2.Learning.Behavior.State == BehaviorState.READY &&
                  wrong2.Learning.Behavior.TriggerPrerequisiteRepair && wrong2.EventState.Action.UseRepair &&
                  wrong2.EventState.Action.Action == "repair",
                    "game_event_explicit_repair_resume_signal_exists_before_state_transition");
                var q2 = first.NextQuestion();
                q2Id = q2.QuestionId;
                first.SuspendForBreak("explicit_repair_before_state_transition");
            }

            using (var resumed = new MathGameEventCoordinator(database, templatePath, gameEventPath, "NORMAL", 999999,
                null, definition.TargetLessonId))
            {
                var start = resumed.Start("Bé explicit repair resume");
                A(start.Session.ResumedExistingSession && start.Session.SessionId == sessionId &&
                  start.EventState.CompletedCheckpointCount == 1 && start.EventState.CurrentCheckpointNumber == 2,
                    "game_event_explicit_repair_resume_restores_checkpoint_two");
                A(start.EventState.Action.BehaviorState == BehaviorState.READY &&
                  start.EventState.Action.UseRepair && start.EventState.Action.Action == "repair",
                    "game_event_explicit_repair_resume_preserves_repair_signal_before_state_transition");
                A(resumed.NextQuestion().QuestionId == q2Id,
                    "game_event_explicit_repair_resume_keeps_exact_q2");
                resumed.SuspendForBreak("explicit_repair_resume_cleanup");
            }
        }

        private static void TestGameEventDifferentSelectionResumesDurableActiveEvent(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath,
            string gameEventPath)
        {
            var events = new MathGameEventCatalogSource().Load(gameEventPath, lessonCatalogPath);
            var firstEvent = events.Events[0];
            var secondEvent = events.Events[1];
            var database = NewDatabase(Path.Combine(root, "game-event-durable-active-wins.db"), schemaPath);
            string firstSessionId;
            string childId;
            string openQ2Id;
            IList<string> selected;

            using (var first = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW", 15151,
                firstEvent.Id, firstEvent.TargetLessonId))
            {
                var start = first.Start("Bé durable event thắng");
                firstSessionId = start.Session.SessionId;
                childId = start.Session.ChildId;
                selected = start.Session.SelectedContentQuestionIds.ToList();
                var q1 = first.NextQuestion();
                first.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                var q2 = first.NextQuestion();
                openQ2Id = q2.QuestionId;
                first.SuspendForBreak("switch_event_screen");
            }

            var ordinaryLessonRejected = false;
            try
            {
                using (var ordinary = new MathSessionCoordinator(database, templatePath, "LOW", 888888, secondEvent.TargetLessonId))
                    ordinary.Start("Bé durable event thắng");
            }
            catch (InvalidOperationException)
            {
                ordinaryLessonRejected = true;
            }
            A(ordinaryLessonRejected,
                "ordinary_targeted_lesson_still_rejects_different_active_lesson");

            using (var requestedSecond = new MathGameEventCoordinator(database, templatePath, gameEventPath, "NORMAL", 999999,
                secondEvent.Id, secondEvent.TargetLessonId))
            {
                var resumed = requestedSecond.Start("Bé durable event thắng");
                A(resumed.Session.ResumedExistingSession && resumed.Session.SessionId == firstSessionId &&
                  resumed.Session.TargetLessonId == firstEvent.TargetLessonId &&
                  resumed.Session.SelectedContentQuestionIds.SequenceEqual(selected),
                    "game_event_requested_different_event_resumes_durable_active_session");
                A(resumed.Event != null && resumed.Event.Id == firstEvent.Id &&
                  resumed.EventState.EventId == firstEvent.Id && resumed.EventState.TargetLessonId == firstEvent.TargetLessonId &&
                  resumed.EventState.CompletedCheckpointCount == 1 && resumed.EventState.CurrentCheckpointNumber == 2,
                    "game_event_requested_different_event_rebinds_presentation_to_durable_event");
                var q2 = requestedSecond.NextQuestion();
                A(q2.QuestionId == openQ2Id && q2.ContentQuestionId == selected[1],
                    "game_event_requested_different_event_keeps_exact_open_question");
                A(Count(database, "SELECT count(*) FROM math_lesson_progress WHERE child_id='" + childId +
                  "' AND lesson_id='" + secondEvent.TargetLessonId + "';") == 0,
                    "game_event_requested_different_event_does_not_start_new_lesson_progress");

                requestedSecond.SubmitAnswerAt(q2.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 800);
                var q3 = requestedSecond.NextQuestion();
                requestedSecond.SubmitAnswerAt(q3.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 900);
                var completed = requestedSecond.Complete();
                A(completed.LearningSummary.LessonCompleted && completed.LearningSummary.TargetLessonId == firstEvent.TargetLessonId &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + firstSessionId + "';") == 1,
                    "game_event_durable_active_event_completes_original_lesson_and_rewards_once");
            }

            using (var second = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW", 15152,
                secondEvent.Id, secondEvent.TargetLessonId))
            {
                var started = second.Start("Bé durable event thắng");
                A(!started.Session.ResumedExistingSession && started.Session.SessionId != firstSessionId &&
                  started.Session.TargetLessonId == secondEvent.TargetLessonId && started.Event != null && started.Event.Id == secondEvent.Id,
                    "game_event_requested_second_event_starts_only_after_durable_first_event_completed");
                A(Count(database, "SELECT count(*) FROM math_lesson_progress WHERE child_id='" + childId +
                  "' AND lesson_id='" + secondEvent.TargetLessonId + "' AND started_count=1;") == 1,
                    "game_event_second_lesson_started_count_increments_only_when_really_started");
                second.SuspendForBreak("cleanup_second_event");
            }
        }

        private static void TestGameEventRewardFaultReconcilesOnNextStart(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath,
            string gameEventPath)
        {
            var events = new MathGameEventCatalogSource().Load(gameEventPath, lessonCatalogPath);
            var firstEvent = events.Events[0];
            var secondEvent = events.Events[1];
            var database = NewDatabase(Path.Combine(root, "game-event-reward-fault-reconcile.db"), schemaPath);
            string firstSessionId;
            string childId;

            Exec(database, @"CREATE TRIGGER fail_game_event_reward BEFORE INSERT ON reward_event
BEGIN SELECT RAISE(ABORT,'game event injected reward failure'); END;");
            using (var first = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW",
                15201, firstEvent.Id, firstEvent.TargetLessonId))
            {
                var start = first.Start("Bé event reward fault");
                firstSessionId = start.Session.SessionId;
                childId = start.Session.ChildId;
                for (var ordinal = 0; ordinal < 3; ordinal++)
                {
                    var question = first.NextQuestion();
                    first.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700 + ordinal * 100);
                }
                var completion = first.Complete();
                A(completion.LearningSummary.LessonCompleted && completion.EventState.IsComplete &&
                  completion.LearningSummary.GardenGrowthSteps == 0,
                    "game_event_reward_fault_keeps_learning_completed_without_fake_growth");
                A(SessionState(database, firstSessionId) == "completed" &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + firstSessionId + "';") == 0 &&
                  Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + firstSessionId + "';") == 3,
                    "game_event_reward_fault_does_not_roll_back_learning_or_create_partial_reward");
            }

            Exec(database, "DROP TRIGGER fail_game_event_reward;");
            string secondSessionId;
            using (var second = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW",
                15202, secondEvent.Id, secondEvent.TargetLessonId))
            {
                var start = second.Start("Bé event reward fault");
                secondSessionId = start.Session.SessionId;
                A(start.Event != null && start.Event.Id == secondEvent.Id && start.Session.TargetLessonId == secondEvent.TargetLessonId,
                    "game_event_reward_reconcile_next_unlocked_event_starts_normally");
                A(Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + firstSessionId +
                  "' AND reward_type='garden_growth';") == 1,
                    "game_event_next_start_repairs_missing_completed_session_reward");
                var progress = new GameWorldRewardService(database).ReadProgress(childId);
                A(progress.GrowthSteps == 1 && progress.CompletedMathSessions == 1,
                    "game_event_reward_reconcile_progress_reflects_repaired_growth_once");
                A(new GameWorldRewardService(database).ReconcileMissingCompletedMathSessionRewards(childId) == 0 &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + firstSessionId + "';") == 1,
                    "game_event_reward_reconcile_replay_is_idempotent");
                second.SuspendForBreak("reward_reconcile_active_cleanup");
            }

            A(SessionState(database, secondSessionId) == "active" &&
              Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + secondSessionId + "';") == 0,
                "game_event_reward_reconcile_never_rewards_active_session");
        }

        private static void TestGameEventConcurrentRewardReconcileIsIdempotent(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath,
            string gameEventPath)
        {
            var events = new MathGameEventCatalogSource().Load(gameEventPath, lessonCatalogPath);
            var firstEvent = events.Events[0];
            var secondEvent = events.Events[1];
            var database = NewDatabase(Path.Combine(root, "game-event-reward-concurrent-reconcile.db"), schemaPath);
            string sessionId;
            string childId;

            Exec(database, @"CREATE TRIGGER fail_concurrent_reward_seed BEFORE INSERT ON reward_event
BEGIN SELECT RAISE(ABORT,'game event injected concurrent reward seed failure'); END;");
            using (var game = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW", 15221,
                firstEvent.Id, firstEvent.TargetLessonId))
            {
                var start = game.Start("Bé concurrent reward reconcile");
                sessionId = start.Session.SessionId;
                childId = start.Session.ChildId;
                for (var ordinal = 0; ordinal < 3; ordinal++)
                {
                    var question = game.NextQuestion();
                    game.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700 + ordinal * 100);
                }
                var completion = game.Complete();
                A(completion.LearningSummary.LessonCompleted && completion.LearningSummary.GardenGrowthSteps == 0 &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + sessionId + "';") == 0,
                    "game_event_concurrent_reward_fixture_has_completed_learning_and_missing_reward");
            }
            Exec(database, "DROP TRIGGER fail_concurrent_reward_seed;");

            var workerA = new LearningDatabase(database.DatabasePath, schemaPath);
            var workerB = new LearningDatabase(database.DatabasePath, schemaPath);
            var gate = new ManualResetEvent(false);
            var readyA = new ManualResetEvent(false);
            var readyB = new ManualResetEvent(false);
            Exception errorA = null;
            Exception errorB = null;
            var repairedA = -1;
            var repairedB = -1;
            var threadA = new Thread(() =>
            {
                readyA.Set();
                gate.WaitOne();
                try { repairedA = new GameWorldRewardService(workerA).ReconcileMissingCompletedMathSessionRewards(childId); }
                catch (Exception ex) { errorA = ex; }
            });
            var threadB = new Thread(() =>
            {
                readyB.Set();
                gate.WaitOne();
                try { repairedB = new GameWorldRewardService(workerB).ReconcileMissingCompletedMathSessionRewards(childId); }
                catch (Exception ex) { errorB = ex; }
            });
            threadA.Start();
            threadB.Start();
            A(readyA.WaitOne(5000) && readyB.WaitOne(5000),
                "game_event_concurrent_reward_reconcile_workers_ready");
            gate.Set();
            A(threadA.Join(10000) && threadB.Join(10000),
                "game_event_concurrent_reward_reconcile_workers_finish");
            gate.Dispose();
            readyA.Dispose();
            readyB.Dispose();

            A(errorA == null && errorB == null && repairedA + repairedB == 1,
                "game_event_concurrent_reward_reconcile_creates_exactly_one_reward_without_worker_error");
            A(Count(database, "SELECT count(*) FROM reward_event WHERE child_id='" + childId +
              "' AND reward_type='garden_growth' AND source_ref='" + sessionId + "';") == 1 &&
              Count(database, "SELECT count(*) FROM inventory WHERE child_id='" + childId +
              "' AND item_id='garden_seedling';") == 1,
                "game_event_concurrent_reward_reconcile_keeps_reward_and_first_milestone_unique");
            var progress = new GameWorldRewardService(database).ReadProgress(childId);
            A(progress.GrowthSteps == 1 && progress.CompletedMathSessions == 1 &&
              progress.UnlockedItems.Count(x => x == "garden_seedling") == 1,
                "game_event_concurrent_reward_reconcile_progress_is_consistent");

            using (var next = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW", 15222,
                secondEvent.Id, secondEvent.TargetLessonId))
            {
                var start = next.Start("Bé concurrent reward reconcile");
                A(!start.Session.ResumedExistingSession && start.Session.TargetLessonId == secondEvent.TargetLessonId &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + sessionId + "';") == 1,
                    "game_event_concurrent_reward_reconcile_next_event_starts_without_duplicate_repair");
                next.SuspendForBreak("concurrent_reward_reconcile_cleanup");
            }
        }

        private static void TestGameEventTerminalRuntimeCleanupReconcilesOnNextStart(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath,
            string gameEventPath)
        {
            var events = new MathGameEventCatalogSource().Load(gameEventPath, lessonCatalogPath);
            var firstEvent = events.Events[0];
            var secondEvent = events.Events[1];
            var database = NewDatabase(Path.Combine(root, "game-event-terminal-runtime-cleanup.db"), schemaPath);
            string firstSessionId;
            string childId;

            using (var first = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW", 15251,
                firstEvent.Id, firstEvent.TargetLessonId))
            {
                var start = first.Start("Bé terminal cleanup");
                firstSessionId = start.Session.SessionId;
                childId = start.Session.ChildId;
                for (var ordinal = 0; ordinal < 3; ordinal++)
                {
                    var question = first.NextQuestion();
                    first.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700 + ordinal * 100);
                }
                Exec(database, @"CREATE TRIGGER fail_terminal_runtime_delete BEFORE DELETE ON math_session_runtime
BEGIN SELECT RAISE(ABORT,'game event injected terminal runtime delete failure'); END;");
                var completed = first.Complete();
                A(completed.LearningSummary.LessonCompleted && SessionState(database, firstSessionId) == "completed" &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + firstSessionId + "';") == 1,
                    "game_event_terminal_runtime_delete_fault_keeps_completion_and_reward_durable");
                A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + firstSessionId + "';") == 1,
                    "game_event_terminal_runtime_delete_fault_leaves_repairable_checkpoint");
            }

            Exec(database, "DROP TRIGGER fail_terminal_runtime_delete;");
            string secondSessionId;
            using (var second = new MathGameEventCoordinator(database, templatePath, gameEventPath, "LOW", 15252,
                secondEvent.Id, secondEvent.TargetLessonId))
            {
                var start = second.Start("Bé terminal cleanup");
                secondSessionId = start.Session.SessionId;
                A(!start.Session.ResumedExistingSession && start.Session.TargetLessonId == secondEvent.TargetLessonId,
                    "game_event_after_terminal_cleanup_starts_next_event_normally");
                A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + firstSessionId + "';") == 0,
                    "game_event_next_start_cleans_stale_terminal_runtime_checkpoint");
                A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + secondSessionId + "';") == 1,
                    "game_event_terminal_cleanup_preserves_new_active_runtime_checkpoint");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + firstSessionId + "';") == 3 &&
                  Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + firstSessionId + "';") == 3 &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + firstSessionId + "';") == 1,
                    "game_event_terminal_cleanup_preserves_learning_history_and_reward");
                second.SuspendForBreak("terminal_cleanup_next_event");
            }

            A(Count(database, "SELECT count(*) FROM session WHERE child_id='" + childId + "' AND state='completed';") == 1,
                "game_event_terminal_cleanup_does_not_change_terminal_session_history");
        }

        private static void TestGameEventCommitFaultPreservesCheckpoint(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var lesson = catalog.Lessons[0];
            var eventId = "m2_evt_fault_rescue_01";
            var eventPath = Path.Combine(root, "synthetic-game-event-fault.json");
            WriteSyntheticGameEventCatalog(eventPath, eventId, lesson.Id, lesson.SkillId);
            var database = NewDatabase(Path.Combine(root, "game-event-commit-fault.db"), schemaPath);

            using (var game = new MathGameEventCoordinator(database, templatePath, eventPath, "LOW", 14101, eventId, lesson.Id))
            {
                var start = game.Start("Bé event fault");
                var q1 = game.NextQuestion();
                Exec(database, @"CREATE TRIGGER fail_game_event_mastery BEFORE INSERT ON mastery_event
BEGIN SELECT RAISE(ABORT,'game event injected mastery failure'); END;");
                var failed = false;
                try { game.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700); }
                catch (Exception) { failed = true; }
                var afterFault = game.CurrentState;
                A(failed && afterFault.CompletedCheckpointCount == 0 && afterFault.CurrentCheckpointNumber == 1 &&
                  !afterFault.IsComplete && game.NextQuestion().QuestionId == q1.QuestionId,
                    "game_event_answer_fault_preserves_open_question_and_checkpoint");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + start.Session.SessionId + "';") == 0 &&
                  Count(database, "SELECT count(*) FROM mastery_event;") == 0 &&
                  Count(database, "SELECT count(*) FROM reward_event;") == 0,
                    "game_event_answer_fault_rolls_back_learning_chain_and_reward");

                Exec(database, "DROP TRIGGER fail_game_event_mastery;");
                var q1Retry = game.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 720);
                A(q1Retry.Learning.IsCorrect && q1Retry.EventState.CompletedCheckpointCount == 1 &&
                  Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + start.Session.SessionId + "';") == 1,
                    "game_event_retry_after_fault_commits_checkpoint_exactly_once");
                for (var ordinal = 1; ordinal < 3; ordinal++)
                {
                    var question = game.NextQuestion();
                    game.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 800 + ordinal * 100);
                }
                var completion = game.Complete();
                A(completion.LearningSummary.LessonCompleted && completion.EventState.IsComplete &&
                  Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + start.Session.SessionId + "';") == 3 &&
                  Count(database, "SELECT count(*) FROM mastery_event;") == 3,
                    "game_event_fault_recovery_completes_with_exact_three_learning_events");
                A(Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + start.Session.SessionId + "';") == 1,
                    "game_event_fault_recovery_grants_one_completion_reward");
            }
        }

        private static void TestGameEventConcurrentCoordinatorsStayIdempotent(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var lesson = catalog.Lessons[0];
            var eventId = "m2_evt_concurrent_rescue_01";
            var eventPath = Path.Combine(root, "synthetic-game-event-concurrent.json");
            WriteSyntheticGameEventCatalog(eventPath, eventId, lesson.Id, lesson.SkillId);
            var database = NewDatabase(Path.Combine(root, "game-event-concurrent.db"), schemaPath);

            using (var first = new MathGameEventCoordinator(database, templatePath, eventPath, "LOW", 14201, eventId, lesson.Id))
            {
                var firstStart = first.Start("Bé event concurrent");
                using (var second = new MathGameEventCoordinator(database, templatePath, eventPath, "NORMAL", 999999, eventId, lesson.Id))
                {
                    var secondStart = second.Start("Bé event concurrent");
                    A(secondStart.Session.ResumedExistingSession && secondStart.Session.SessionId == firstStart.Session.SessionId &&
                      secondStart.EventState.EventId == eventId &&
                      secondStart.Session.SelectedContentQuestionIds.SequenceEqual(firstStart.Session.SelectedContentQuestionIds),
                        "game_event_concurrent_second_wrapper_binds_durable_winner_event_session");

                    for (var ordinal = 0; ordinal < 3; ordinal++)
                    {
                        var firstQuestion = first.NextQuestion();
                        var secondQuestion = second.NextQuestion();
                        A(firstQuestion.QuestionId == secondQuestion.QuestionId &&
                          firstQuestion.ContentQuestionId == secondQuestion.ContentQuestionId,
                            "game_event_concurrent_same_checkpoint_same_question_" + ordinal);
                        var committed = first.SubmitAnswerAt(firstQuestion.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700 + ordinal * 100);
                        var replay = second.SubmitAnswerAt(secondQuestion.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 720 + ordinal * 100);
                        A(committed.Learning.QuestionCompleted && replay.Learning.QuestionCompleted &&
                          first.CurrentState.CompletedCheckpointCount == ordinal + 1 &&
                          second.CurrentState.CompletedCheckpointCount == ordinal + 1,
                            "game_event_concurrent_replay_keeps_checkpoint_in_sync_" + ordinal);
                    }

                    MathGameEventCompletionResult completionA = null;
                    MathGameEventCompletionResult completionB = null;
                    Exception completionErrorA = null;
                    Exception completionErrorB = null;
                    var completionGate = new ManualResetEvent(false);
                    var completionReadyA = new ManualResetEvent(false);
                    var completionReadyB = new ManualResetEvent(false);
                    var completionThreadA = new Thread(() =>
                    {
                        completionReadyA.Set();
                        completionGate.WaitOne();
                        try { completionA = first.Complete(); } catch (Exception ex) { completionErrorA = ex; }
                    });
                    var completionThreadB = new Thread(() =>
                    {
                        completionReadyB.Set();
                        completionGate.WaitOne();
                        try { completionB = second.Complete(); } catch (Exception ex) { completionErrorB = ex; }
                    });
                    completionThreadA.Start();
                    completionThreadB.Start();
                    A(completionReadyA.WaitOne(5000) && completionReadyB.WaitOne(5000),
                        "game_event_concurrent_completion_workers_ready");
                    completionGate.Set();
                    A(completionThreadA.Join(10000) && completionThreadB.Join(10000),
                        "game_event_concurrent_completion_workers_finish");
                    completionGate.Dispose();
                    completionReadyA.Dispose();
                    completionReadyB.Dispose();

                    A((completionA != null || completionB != null) &&
                      (completionErrorA == null || completionErrorB == null) &&
                      Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + firstStart.Session.SessionId + "';") == 3 &&
                      Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + firstStart.Session.SessionId + "';") == 3,
                        "game_event_concurrent_completion_preserves_exact_learning_chain");
                    A(SessionState(database, firstStart.Session.SessionId) == "completed" &&
                      Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + firstStart.Session.SessionId + "';") == 1 &&
                      Count(database, "SELECT count(*) FROM math_lesson_progress WHERE child_id='" + firstStart.Session.ChildId +
                      "' AND lesson_id='" + lesson.Id + "' AND completed_count=1;") == 1,
                        "game_event_concurrent_completion_terminalizes_progress_and_reward_exactly_once");
                }
            }
        }

        private static void TestGameEventCompleteVsSuspendRaceStaysTerminal(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var lesson = catalog.Lessons[0];
            var eventId = "m2_evt_complete_suspend_race_01";
            var eventPath = Path.Combine(root, "synthetic-game-event-complete-suspend-race.json");
            WriteSyntheticGameEventCatalog(eventPath, eventId, lesson.Id, lesson.SkillId);
            var database = NewDatabase(Path.Combine(root, "game-event-complete-suspend-race.db"), schemaPath);

            using (var completing = new MathGameEventCoordinator(database, templatePath, eventPath, "LOW", 14231, eventId, lesson.Id))
            using (var closing = new MathGameEventCoordinator(database, templatePath, eventPath, "NORMAL", 999999, eventId, lesson.Id))
            {
                var firstStart = completing.Start("Bé complete suspend race");
                var secondStart = closing.Start("Bé complete suspend race");
                A(secondStart.Session.ResumedExistingSession && secondStart.Session.SessionId == firstStart.Session.SessionId,
                    "game_event_complete_suspend_race_wrappers_share_durable_session");

                for (var ordinal = 0; ordinal < 3; ordinal++)
                {
                    var qA = completing.NextQuestion();
                    var qB = closing.NextQuestion();
                    completing.SubmitAnswerAt(qA.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700 + ordinal * 100);
                    closing.SubmitAnswerAt(qB.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 720 + ordinal * 100);
                }

                MathGameEventCompletionResult completion = null;
                Exception completionError = null;
                Exception suspendError = null;
                var gate = new ManualResetEvent(false);
                var readyComplete = new ManualResetEvent(false);
                var readySuspend = new ManualResetEvent(false);
                var completeThread = new Thread(() =>
                {
                    readyComplete.Set();
                    gate.WaitOne();
                    try { completion = completing.Complete(); } catch (Exception ex) { completionError = ex; }
                });
                var suspendThread = new Thread(() =>
                {
                    readySuspend.Set();
                    gate.WaitOne();
                    try { closing.SuspendForBreak("close_during_completion"); } catch (Exception ex) { suspendError = ex; }
                });
                completeThread.Start();
                suspendThread.Start();
                A(readyComplete.WaitOne(5000) && readySuspend.WaitOne(5000),
                    "game_event_complete_suspend_race_workers_ready");
                gate.Set();
                A(completeThread.Join(10000) && suspendThread.Join(10000),
                    "game_event_complete_suspend_race_workers_finish");
                gate.Dispose();
                readyComplete.Dispose();
                readySuspend.Dispose();

                A(completionError == null && suspendError == null && completion != null &&
                  completion.EventState.IsComplete && completion.LearningSummary.LessonCompleted,
                    "game_event_complete_suspend_race_completion_wins_without_surface_error");
                A(SessionState(database, firstStart.Session.SessionId) == "completed" &&
                  Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + firstStart.Session.SessionId + "';") == 0,
                    "game_event_complete_suspend_race_cannot_revive_terminal_runtime_checkpoint");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + firstStart.Session.SessionId + "';") == 3 &&
                  Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + firstStart.Session.SessionId + "';") == 3 &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + firstStart.Session.SessionId + "';") == 1,
                    "game_event_complete_suspend_race_keeps_exact_learning_and_reward_chain");

                closing.Dispose();
                A(SessionState(database, firstStart.Session.SessionId) == "completed" &&
                  Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + firstStart.Session.SessionId + "';") == 0 &&
                  Count(database, "SELECT count(*) FROM reward_event WHERE source_ref='" + firstStart.Session.SessionId + "';") == 1,
                    "game_event_dispose_after_completion_race_is_terminal_idempotent");
            }
        }

        private static void WriteSyntheticGameEventCatalog(string path, string eventId, string lessonId, string skillId)
        {
            var payload = new Dictionary<string, object>
            {
                { "schema_version", 1 },
                { "catalog_id", MathGameEventCatalogSource.CatalogId },
                { "language", "vi" },
                { "events", new object[]
                    {
                        new Dictionary<string, object>
                        {
                            { "id", eventId },
                            { "kind", "quick_rescue" },
                            { "title_vi", "Sửa biển số trong Rừng Toán" },
                            { "intro_vi", "Ba biển số đang lộn xộn. Con giúp đặt lại từng biển nhé." },
                            { "completion_vi", "Ba biển số đã về đúng chỗ. Đường trong rừng lại rõ ràng rồi." },
                            { "target_lesson_id", lessonId },
                            { "target_skill_id", skillId },
                            { "question_count", 3 },
                            { "checkpoint_nouns_vi", new[] { "biển số 1", "biển số 2", "biển số 3" } },
                            { "theme", "forest_path" },
                            { "repair_copy_vi", "Mình xem lại một bước nhỏ rồi sửa tiếp nhé." },
                            { "break_copy_vi", "Phần đã làm được lưu rồi. Khi nào muốn mình quay lại tiếp nhé." },
                            { "reward_presentation", "garden_progress" }
                        }
                    }
                }
            };
            File.WriteAllText(path, Json.Serialize(payload));
        }

        private static void TestAuthoredQuestionBank(string questionBankPath)
        {
            var bank = new MathAuthoredQuestionSource().Load(questionBankPath);
            A(bank != null && bank.Questions != null && bank.Questions.Count >= 201, "authored_bank_loads_at_least_baseline_201_questions");
            A(bank.Questions.Select(x => x.ContentQuestionId).Distinct(StringComparer.Ordinal).Count() == bank.Questions.Count,
                "authored_bank_content_ids_unique");
            var lessonGroups = bank.Questions.GroupBy(x => x.LessonId, StringComparer.Ordinal).ToList();
            A(lessonGroups.Count == 67, "authored_bank_covers_67_lessons");
            A(lessonGroups.All(x => x.Count() >= 3 &&
                x.Any(q => q.Difficulty == "basic") && x.Any(q => q.Difficulty == "medium") && x.Any(q => q.Difficulty == "application")),
                "authored_bank_each_lesson_has_all_three_difficulties");
            A(bank.Questions.All(x => !string.IsNullOrWhiteSpace(x.ContentQuestionId) && !string.IsNullOrWhiteSpace(x.LessonId) &&
                                      !string.IsNullOrWhiteSpace(x.QuestionType) && !string.IsNullOrWhiteSpace(x.Difficulty)),
                "authored_bank_maps_traceability_metadata");
            A(bank.Questions.All(x => x.IsCorrectAnswer(x.CorrectAnswerText)), "authored_bank_every_expected_answer_validates");

            var expression = bank.Questions.First(x => x.AnswerKind == "expression");
            A(expression.QuestionType == "expression_input" && expression.DisplayChoices.Count == 0,
                "authored_expression_stays_input_without_fake_choices");
            A(expression.IsCorrectAnswer("75"), "authored_expression_accepts_equivalent_numeric_result");
            A(expression.AllowedExpressionOperators != null &&
              expression.AllowedExpressionOperators.SequenceEqual(new[] { "+", "-", "(", ")" }),
                "authored_expression_loads_per_question_operator_whitelist");
            A(expression.IsCorrectAnswer("100 - 30 + 5"), "authored_expression_accepts_declared_expression");
            A(expression.IsCorrectAnswer("70 + 5"), "authored_expression_accepts_equivalent_allowed_expression");
            A(!expression.IsCorrectAnswer("15*5"), "authored_expression_rejects_multiply_equivalent");
            A(!expression.IsCorrectAnswer("150/2"), "authored_expression_rejects_divide_equivalent");

            var expressionRuntime = MathAuthoredQuestionSource.CreateRuntimeInstance(expression);
            A(expressionRuntime.AllowedExpressionOperators != null &&
              expressionRuntime.AllowedExpressionOperators.SequenceEqual(expression.AllowedExpressionOperators),
                "authored_expression_runtime_instance_preserves_operator_whitelist");
            var expressionCurrentQuestionJson = Json.Serialize(expressionRuntime);
            var restoredExpression = Json.Deserialize<MathQuestion>(expressionCurrentQuestionJson);
            A(restoredExpression != null && restoredExpression.AllowedExpressionOperators != null &&
              restoredExpression.AllowedExpressionOperators.SequenceEqual(expression.AllowedExpressionOperators),
                "authored_expression_current_question_json_preserves_operator_whitelist");
            A(restoredExpression.IsCorrectAnswer("75") && restoredExpression.IsCorrectAnswer("70 + 5") &&
              !restoredExpression.IsCorrectAnswer("15*5") && !restoredExpression.IsCorrectAnswer("150/2"),
                "authored_expression_restored_question_enforces_operator_whitelist");

            var legacyExpression = new MathQuestion
            {
                AnswerKind = "expression",
                CorrectAnswerText = "75",
                AcceptedAnswers = new[] { "75" }
            };
            A(legacyExpression.IsCorrectAnswer("15*5") && legacyExpression.IsCorrectAnswer("150/2"),
                "legacy_expression_without_whitelist_keeps_global_numeric_equivalence");

            var unit = bank.Questions.First(x => x.AnswerKind == "unit" && x.ExpectedUnit == "kg");
            A(unit.QuestionType == "unit_input" && unit.DisplayChoices.Count == 0 && unit.ExpectedUnit == "kg",
                "authored_unit_maps_expected_unit_without_fake_choices");
            A(unit.IsCorrectAnswer("5 kilôgam"), "authored_unit_accepts_declared_alias");

            var displayUnitQuestions = bank.Questions.Where(x => x.AnswerKind == "integer" && !string.IsNullOrWhiteSpace(x.AnswerUnit)).ToList();
            A(displayUnitQuestions.Count >= 23, "authored_integer_display_units_are_preserved");
            var displayCm = displayUnitQuestions.First(x => x.AnswerUnit == "cm");
            A(displayCm.IsCorrectAnswer(displayCm.CorrectAnswerDisplay),
                "authored_display_unit_keeps_raw_integer_answer_valid");
            A(!displayCm.IsCorrectAnswer(displayCm.CorrectAnswerFeedbackDisplay),
                "authored_display_unit_does_not_widen_integer_grading_to_unit_text");
            A(displayCm.CorrectAnswerFeedbackDisplay == displayCm.CorrectAnswerDisplay + " cm",
                "authored_display_unit_formats_feedback_with_unit");
            var displayRuntime = MathAuthoredQuestionSource.CreateRuntimeInstance(displayCm);
            A(displayRuntime.AnswerUnit == "cm" && displayRuntime.CorrectAnswerFeedbackDisplay == displayCm.CorrectAnswerFeedbackDisplay,
                "authored_display_unit_survives_runtime_instance");
            var restoredDisplay = Json.Deserialize<MathQuestion>(Json.Serialize(displayRuntime));
            A(restoredDisplay != null && restoredDisplay.AnswerUnit == "cm" &&
              restoredDisplay.CorrectAnswerFeedbackDisplay == displayCm.CorrectAnswerFeedbackDisplay &&
              restoredDisplay.IsCorrectAnswer(restoredDisplay.CorrectAnswerDisplay),
                "authored_display_unit_survives_current_question_json_without_changing_raw_grading");

            var interaction = bank.Questions.First(x => x.AnswerKind == "interaction_integer");
            A(interaction.QuestionType == "interactive_measurement" && interaction.DisplayChoices.Count == 0,
                "authored_interaction_stays_choice_free");
            A((interaction.IllustrationData ?? string.Empty).StartsWith("segmentdraw|", StringComparison.Ordinal),
                "authored_interaction_maps_existing_segment_control_contract");

            var textChoice = bank.Questions.First(x => x.AnswerKind == "text" && x.QuestionType == "multiple_choice");
            A(textChoice.DisplayChoices.Count >= 2 && textChoice.DisplayChoices.Contains(textChoice.CorrectAnswerText),
                "authored_text_choice_preserves_declared_options");
            var numericInput = bank.Questions.First(x => x.QuestionType == "numeric_input");
            A(numericInput.DisplayChoices.Count == 0, "authored_numeric_input_does_not_generate_choices");

            var authored = bank.Questions[0];
            var runtimeA = MathAuthoredQuestionSource.CreateRuntimeInstance(authored);
            var runtimeB = MathAuthoredQuestionSource.CreateRuntimeInstance(authored);
            A(runtimeA.ContentQuestionId == authored.ContentQuestionId && runtimeB.ContentQuestionId == authored.ContentQuestionId,
                "authored_runtime_instances_keep_stable_content_id");
            A(runtimeA.QuestionId != runtimeB.QuestionId && runtimeA.QuestionId.StartsWith(authored.ContentQuestionId + "-", StringComparison.Ordinal),
                "authored_runtime_question_id_remains_unique_instance_id");
        }

        private static void TestRealPoolSelectionBreadth(string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            A(catalog != null && catalog.Lessons != null && catalog.Lessons.Count == 67,
                "real_pool_selection_breadth_loads_67_lessons");

            var deterministicIndex = typeof(MathSessionCoordinator).GetMethod(
                "DeterministicBucketIndex",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            A(deterministicIndex != null, "real_pool_selection_breadth_finds_engine_selector");

            var bucketNames = new[] { "basic", "medium", "application" };
            foreach (var lesson in catalog.Lessons)
            {
                var buckets = new[]
                {
                    lesson.PracticeSets.Basic,
                    lesson.PracticeSets.Medium,
                    lesson.PracticeSets.Application
                };
                A(buckets.All(x => x != null && x.Count == 2),
                    "real_pool_selection_breadth_two_variants_each_bucket_" + lesson.Id);

                for (var bucketIndex = 0; bucketIndex < bucketNames.Length; bucketIndex++)
                {
                    var bucket = buckets[bucketIndex];
                    var bucketName = bucketNames[bucketIndex];
                    var reached = new HashSet<int>();
                    for (var seed = 0; seed < 16; seed++)
                    {
                        var args = new object[] { seed, lesson.Id, bucketName, bucket.Count };
                        var first = (int)deterministicIndex.Invoke(null, args);
                        var repeated = (int)deterministicIndex.Invoke(null, args);
                        A(first == repeated,
                            "real_pool_selection_breadth_deterministic_" + lesson.Id + "_" + bucketName + "_" + seed.ToString(CultureInfo.InvariantCulture));
                        A(first >= 0 && first < bucket.Count,
                            "real_pool_selection_breadth_in_range_" + lesson.Id + "_" + bucketName + "_" + seed.ToString(CultureInfo.InvariantCulture));
                        reached.Add(first);
                    }
                    A(reached.Count == bucket.Count,
                        "real_pool_selection_breadth_rotates_both_variants_" + lesson.Id + "_" + bucketName);
                }
            }
        }

        private static void TestAnswerUnitFeedbackSurvivesCoordinatorResume(string root, string schemaPath, string templatePath, string lessonCatalogPath)
        {
            const string lessonId = "m2_ls_time_day_24_hours";
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var lesson = catalog.FindLesson(lessonId);
            A(lesson != null && (lesson.PrerequisiteSkills == null || lesson.PrerequisiteSkills.Count == 0),
                "answer_unit_feedback_fixture_is_directly_startable");
            var database = NewDatabase(Path.Combine(root, "answer-unit-feedback-resume.db"), schemaPath);
            string sessionId;
            string questionId;

            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 20260907, lessonId))
            {
                var start = first.Start("Bé answer unit");
                sessionId = start.SessionId;
                var question = first.NextQuestion();
                questionId = question.QuestionId;
                A(question.ContentQuestionId == start.SelectedContentQuestionIds[0] &&
                  lesson.PracticeSets.Basic.Contains(question.ContentQuestionId) && question.AnswerKind == "integer",
                    "answer_unit_feedback_opens_selected_authored_integer_question");
                A(question.AnswerUnit == "giờ" &&
                  question.CorrectAnswerFeedbackDisplay == question.CorrectAnswerDisplay + " giờ",
                    "answer_unit_feedback_separates_raw_answer_from_feedback_display");
                first.Suspend("answer_unit_feedback_resume_fixture");
            }

            using (var resumed = new MathSessionCoordinator(database, templatePath, "NORMAL", 1, lessonId))
            {
                var start = resumed.Start("Bé answer unit");
                A(start.ResumedExistingSession && start.SessionId == sessionId && start.RestoredOpenQuestion,
                    "answer_unit_feedback_resumes_open_question");
                var question = resumed.NextQuestion();
                A(question.QuestionId == questionId && question.AnswerUnit == "giờ" &&
                  question.CorrectAnswerFeedbackDisplay == question.CorrectAnswerDisplay + " giờ",
                    "answer_unit_feedback_survives_runtime_json_resume");
                var rawAnswer = question.CorrectAnswerDisplay;
                var feedbackAnswer = question.CorrectAnswerFeedbackDisplay;
                var outcome = resumed.SubmitAnswerAt(rawAnswer, 0, "smoke", DateTime.UtcNow, 800);
                A(outcome.IsCorrect && outcome.CorrectAnswerDisplay == feedbackAnswer,
                    "answer_unit_feedback_outcome_publishes_value_with_display_unit");
                resumed.Abort("answer_unit_feedback_cleanup");
            }
        }

        private static void TestTargetedLessonUnlockAndResume(string root, string schemaPath, string templatePath, string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            MathLessonDescriptor prerequisite = null;
            MathLessonDescriptor dependent = null;
            for (var i = 1; i < catalog.Lessons.Count; i++)
            {
                var prior = catalog.Lessons[i - 1];
                var candidate = catalog.Lessons[i];
                var prerequisites = candidate.PrerequisiteSkills ?? new List<string>();
                if (prerequisites.Count == 1 &&
                    string.Equals(prerequisites[0], prior.SkillId, StringComparison.Ordinal) &&
                    (prior.PrerequisiteSkills == null || prior.PrerequisiteSkills.Count == 0))
                {
                    prerequisite = prior;
                    dependent = candidate;
                    break;
                }
            }
            A(dependent != null, "targeted_test_has_adjacent_simple_prerequisite_edge");
            A(prerequisite != null, "targeted_test_resolves_prerequisite_lesson");

            var database = NewDatabase(Path.Combine(root, "targeted-lesson.db"), schemaPath);
            var profile = new LearnerSessionService(database).EnsurePrimaryChild("Bé targeted");
            var accessService = new MathLessonProgressService(database, lessonCatalogPath);
            var prerequisiteAccess = accessService.GetAccess(profile.ChildId, prerequisite.Id);
            var dependentAccess = accessService.GetAccess(profile.ChildId, dependent.Id);
            A(prerequisiteAccess.IsUnlocked, "root_prerequisite_lesson_unlocked");
            A(!dependentAccess.IsUnlocked && dependentAccess.UnsatisfiedPrerequisiteLessonIds.Contains(prerequisite.Id),
                "dependent_lesson_locked_before_prerequisite_completion");

            var directStartBlocked = false;
            try
            {
                using (var blocked = new MathSessionCoordinator(database, templatePath, "LOW", 7001, dependent.Id))
                    blocked.Start("Bé targeted");
            }
            catch (MathLessonLockedException ex)
            {
                directStartBlocked = string.Equals(ex.LessonId, dependent.Id, StringComparison.Ordinal) &&
                                     ex.UnsatisfiedPrerequisiteLessonIds.Contains(prerequisite.Id);
            }
            A(directStartBlocked, "engine_blocks_direct_start_of_locked_lesson");
            A(Count(database, "SELECT count(*) FROM session WHERE state='active';") == 0,
                "blocked_lesson_start_creates_no_active_session");

            string sessionId;
            string openQuestionId;
            IList<string> targetedSelection;
            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 7002, prerequisite.Id))
            {
                var started = first.Start("Bé targeted");
                sessionId = started.SessionId;
                A(started.SessionMode == "lesson" && started.TargetLessonId == prerequisite.Id,
                    "targeted_start_publishes_lesson_identity");
                targetedSelection = started.SelectedContentQuestionIds.ToList();
                A(started.TargetQuestionCount == MathSessionCoordinator.TargetedLessonQuestionCount && targetedSelection.Count == 3,
                    "targeted_start_uses_selected_three_question_target");
                A(started.LessonAccess != null && started.LessonAccess.IsUnlocked,
                    "targeted_start_publishes_access_snapshot");

                var q1 = first.NextQuestion();
                A(q1.LessonId == prerequisite.Id && q1.SkillId == prerequisite.SkillId,
                    "targeted_question_matches_selected_lesson_skill");
                A(q1.ContentQuestionId == targetedSelection[0] && prerequisite.PracticeSets.Basic.Contains(q1.ContentQuestionId),
                    "targeted_first_question_uses_selected_basic_practice_id");
                first.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 900);

                var q2 = first.NextQuestion();
                openQuestionId = q2.QuestionId;
                A(q2.ContentQuestionId == targetedSelection[1] && prerequisite.PracticeSets.Medium.Contains(q2.ContentQuestionId),
                    "targeted_second_question_uses_selected_medium_practice_id");
                first.Suspend("targeted_resume_test");
            }

            A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + sessionId +
                "' AND session_mode='lesson' AND target_lesson_id='" + prerequisite.Id + "';") == 1,
                "targeted_runtime_persists_mode_and_lesson");

            using (var resumed = new MathSessionCoordinator(database, templatePath, "NORMAL", 9999, 9))
            {
                var started = resumed.Start("Bé targeted");
                A(started.ResumedExistingSession && started.SessionId == sessionId,
                    "adaptive_entry_resumes_existing_targeted_session");
                A(started.SessionMode == "lesson" && started.TargetLessonId == prerequisite.Id,
                    "targeted_resume_restores_mode_and_lesson");
                A(started.SelectedContentQuestionIds.SequenceEqual(targetedSelection),
                    "targeted_resume_preserves_selected_question_set");
                A(started.CompletedQuestionCount == 1 && started.RestoredOpenQuestion,
                    "targeted_resume_rebuilds_progress_and_open_question");
                var resumedSummary = resumed.Summary;
                A(resumedSummary.MasteryChanges != null && resumedSummary.MasteryChanges.Count == 1,
                    "targeted_resume_reconstructs_mastery_change_from_durable_events");
                var resumedMastery = resumedSummary.MasteryChanges.Single(x => x.SkillId == prerequisite.SkillId);
                A(resumedMastery.ScoreAfter > resumedMastery.ScoreBefore && resumedMastery.Delta > 0,
                    "targeted_resume_mastery_change_keeps_first_before_and_latest_after");
                A(resumedSummary.TargetSkillMasteryDelta.HasValue &&
                  Math.Abs(resumedSummary.TargetSkillMasteryDelta.Value - resumedMastery.Delta) < 0.0000001,
                    "targeted_resume_publishes_target_skill_mastery_delta");

                var q2 = resumed.NextQuestion();
                A(q2.QuestionId == openQuestionId && q2.ContentQuestionId == targetedSelection[1],
                    "targeted_resume_returns_exact_open_authored_question");
                resumed.SubmitAnswerAt(q2.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 950);

                var q3 = resumed.NextQuestion();
                A(q3.ContentQuestionId == targetedSelection[2] && prerequisite.PracticeSets.Application.Contains(q3.ContentQuestionId),
                    "targeted_third_question_uses_selected_application_practice_id");
                resumed.SubmitAnswerAt(q3.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 1000);
                A(resumed.NextQuestion() == null, "targeted_session_stops_after_selected_three_question_set");

                var summary = resumed.Complete();
                A(summary.LessonCompleted && summary.TargetLessonId == prerequisite.Id,
                    "targeted_completion_marks_lesson_completed");
                A(summary.LessonScorePercent.HasValue && Math.Abs(summary.LessonScorePercent.Value - 100.0) < 0.0001,
                    "targeted_completion_reports_numeric_score");
                A(summary.LessonBestScorePercent.HasValue && Math.Abs(summary.LessonBestScorePercent.Value - 100.0) < 0.0001,
                    "targeted_completion_reports_best_score");
                A(summary.MasteryChanges != null && summary.MasteryChanges.Count == 1 && summary.ImprovedSkillCount == 1,
                    "targeted_completion_reports_one_improved_target_skill");
                var targetMastery = summary.MasteryChanges.Single(x => x.SkillId == prerequisite.SkillId);
                A(targetMastery.ScoreAfter > targetMastery.ScoreBefore &&
                  Math.Abs(targetMastery.Delta - (targetMastery.ScoreAfter - targetMastery.ScoreBefore)) < 0.0000001,
                    "targeted_completion_reports_durable_mastery_before_after_delta");
                A(summary.TargetSkillMasteryBefore.HasValue && summary.TargetSkillMasteryAfter.HasValue &&
                  summary.TargetSkillMasteryDelta.HasValue &&
                  Math.Abs(summary.TargetSkillMasteryDelta.Value - targetMastery.Delta) < 0.0000001,
                    "targeted_completion_publishes_target_mastery_shortcut");
                A(summary.NextLessonId == dependent.Id && summary.NextLessonTitleVi == dependent.TitleVi,
                    "targeted_completion_publishes_unlocked_immediate_next_lesson");
            }

            var stored = new MathLessonProgressStore(database).LoadOne(profile.ChildId, prerequisite.Id);
            A(stored != null && stored.StartedCount == 1 && stored.CompletedCount == 1,
                "lesson_progress_persists_started_and_completed_counts");
            A(stored.LastScorePercent.HasValue && stored.BestScorePercent.HasValue &&
              Math.Abs(stored.LastScorePercent.Value - 100.0) < 0.0001 && Math.Abs(stored.BestScorePercent.Value - 100.0) < 0.0001,
                "lesson_progress_persists_last_and_best_score");

            dependentAccess = accessService.GetAccess(profile.ChildId, dependent.Id);
            A(dependentAccess.IsUnlocked && dependentAccess.UnsatisfiedPrerequisiteLessonIds.Count == 0,
                "completing_prerequisite_unlocks_dependent_lesson");

            using (var next = new MathSessionCoordinator(database, templatePath, "LOW", 7003, dependent.Id))
            {
                var started = next.Start("Bé targeted");
                A(started.TargetLessonId == dependent.Id && started.LessonAccess.IsUnlocked,
                    "newly_unlocked_lesson_can_start_through_engine_guard");
                var question = next.NextQuestion();
                A(question.LessonId == dependent.Id, "newly_unlocked_session_targets_exact_lesson");
                next.Abort("targeted_cleanup");
            }
        }

        private static void TestFirstFiveLessonsGoldenPath(string root, string schemaPath, string templatePath, string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var firstFive = catalog.Lessons.Take(5).ToList();
            var expectedIds = new[]
            {
                "m2_ls_num_count_read_write_0_1000",
                "m2_ls_num_full_hundreds_recognize",
                "m2_ls_num_predecessor_successor",
                "m2_ls_place_value_hundreds_tens_ones",
                "m2_ls_num_expanded_form_hto"
            };
            A(firstFive.Count == 5 && firstFive.Select(x => x.Id).SequenceEqual(expectedIds),
                "first_five_golden_slice_is_exact_curriculum_front");

            var database = NewDatabase(Path.Combine(root, "first-five-lessons-golden.db"), schemaPath);
            var profile = new LearnerSessionService(database).EnsurePrimaryChild("Bé 5 bài đầu");
            var accessService = new MathLessonProgressService(database, lessonCatalogPath);
            var progressStore = new MathLessonProgressStore(database);

            A(accessService.GetAccess(profile.ChildId, firstFive[0].Id).IsUnlocked,
                "first_five_lesson1_is_initially_unlocked");
            A(!accessService.GetAccess(profile.ChildId, firstFive[1].Id).IsUnlocked &&
              !accessService.GetAccess(profile.ChildId, firstFive[2].Id).IsUnlocked &&
              !accessService.GetAccess(profile.ChildId, firstFive[3].Id).IsUnlocked &&
              !accessService.GetAccess(profile.ChildId, firstFive[4].Id).IsUnlocked,
                "first_five_dependents_are_initially_locked");

            for (var lessonIndex = 0; lessonIndex < firstFive.Count; lessonIndex++)
            {
                var lesson = firstFive[lessonIndex];
                var access = accessService.GetAccess(profile.ChildId, lesson.Id);
                A(access.IsUnlocked && access.UnsatisfiedPrerequisiteLessonIds.Count == 0,
                    "first_five_reached_lesson_is_unlocked_" + (lessonIndex + 1));

                using (var coordinator = new MathSessionCoordinator(database, templatePath, "LOW", 9300 + lessonIndex, lesson.Id))
                {
                    var start = coordinator.Start("Bé 5 bài đầu");
                    var selected = start.SelectedContentQuestionIds.ToList();
                    A(start.SessionMode == "lesson" && start.TargetLessonId == lesson.Id &&
                      start.TargetQuestionCount == 3 && selected.Count == 3,
                        "first_five_start_targets_exact_three_" + (lessonIndex + 1));
                    A(lesson.PracticeSets.Basic.Contains(selected[0]) &&
                      lesson.PracticeSets.Medium.Contains(selected[1]) &&
                      lesson.PracticeSets.Application.Contains(selected[2]),
                        "first_five_selected_set_is_basic_medium_application_" + (lessonIndex + 1));

                    var q1 = coordinator.NextQuestion();
                    A(q1 != null && q1.LessonId == lesson.Id && q1.SkillId == lesson.SkillId &&
                      q1.ContentQuestionId == selected[0] && lesson.PracticeSets.Basic.Contains(q1.ContentQuestionId),
                        "first_five_q1_is_selected_basic_" + (lessonIndex + 1));
                    var q1Again = coordinator.NextQuestion();
                    A(q1Again != null && q1Again.QuestionId == q1.QuestionId,
                        "first_five_q1_next_is_idempotent_" + (lessonIndex + 1));
                    var r1 = coordinator.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                    A(r1.IsCorrect && r1.QuestionCompleted && coordinator.Summary.Attempts == 1,
                        "first_five_q1_commits_correctly_" + (lessonIndex + 1));

                    var q2 = coordinator.NextQuestion();
                    A(q2 != null && q2.LessonId == lesson.Id && q2.SkillId == lesson.SkillId &&
                      q2.ContentQuestionId == selected[1] && lesson.PracticeSets.Medium.Contains(q2.ContentQuestionId),
                        "first_five_q2_is_selected_medium_" + (lessonIndex + 1));
                    var r2 = coordinator.SubmitAnswerAt(q2.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 800);
                    A(r2.IsCorrect && r2.QuestionCompleted && coordinator.Summary.Attempts == 2,
                        "first_five_q2_commits_correctly_" + (lessonIndex + 1));

                    var q3 = coordinator.NextQuestion();
                    A(q3 != null && q3.LessonId == lesson.Id && q3.SkillId == lesson.SkillId &&
                      q3.ContentQuestionId == selected[2] && lesson.PracticeSets.Application.Contains(q3.ContentQuestionId),
                        "first_five_q3_is_selected_application_" + (lessonIndex + 1));
                    var r3 = coordinator.SubmitAnswerAt(q3.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 900);
                    A(r3.IsCorrect && r3.QuestionCompleted && coordinator.Summary.Attempts == 3 && coordinator.NextQuestion() == null,
                        "first_five_q3_completes_three_question_target_" + (lessonIndex + 1));

                    var summary = coordinator.Complete();
                    A(summary.LessonCompleted && summary.TargetLessonId == lesson.Id && summary.Attempts == 3 && summary.Correct == 3,
                        "first_five_lesson_completes_" + (lessonIndex + 1));
                    A(summary.LessonScorePercent.HasValue && summary.LessonBestScorePercent.HasValue &&
                      Math.Abs(summary.LessonScorePercent.Value - 100.0) < 0.0001 &&
                      Math.Abs(summary.LessonBestScorePercent.Value - 100.0) < 0.0001,
                        "first_five_lesson_scores_are_100_" + (lessonIndex + 1));
                    if (lessonIndex < firstFive.Count - 1)
                        A(summary.NextLessonId == firstFive[lessonIndex + 1].Id,
                            "first_five_completion_points_to_immediate_next_" + (lessonIndex + 1));
                }

                var stored = progressStore.LoadOne(profile.ChildId, lesson.Id);
                A(stored != null && stored.StartedCount == 1 && stored.CompletedCount == 1 &&
                  stored.LastScorePercent.HasValue && stored.BestScorePercent.HasValue &&
                  Math.Abs(stored.LastScorePercent.Value - 100.0) < 0.0001 &&
                  Math.Abs(stored.BestScorePercent.Value - 100.0) < 0.0001,
                    "first_five_progress_persists_completion_" + (lessonIndex + 1));

                if (lessonIndex == 0)
                {
                    A(accessService.GetAccess(profile.ChildId, firstFive[1].Id).IsUnlocked &&
                      accessService.GetAccess(profile.ChildId, firstFive[2].Id).IsUnlocked &&
                      accessService.GetAccess(profile.ChildId, firstFive[3].Id).IsUnlocked,
                        "first_five_lesson1_unlocks_lessons2_3_4");
                    A(!accessService.GetAccess(profile.ChildId, firstFive[4].Id).IsUnlocked,
                        "first_five_lesson5_stays_locked_until_place_value_completed");
                }
                if (lessonIndex == 3)
                    A(accessService.GetAccess(profile.ChildId, firstFive[4].Id).IsUnlocked,
                        "first_five_lesson4_unlocks_lesson5");
            }

            A(Count(database, "SELECT count(*) FROM session WHERE state='completed';") == 5,
                "first_five_exactly_five_sessions_completed");
            A(Count(database, "SELECT count(*) FROM attempt;") == 15 &&
              Count(database, "SELECT count(*) FROM mastery_event;") == 15,
                "first_five_exactly_fifteen_attempts_and_mastery_events");
            A(Count(database, "SELECT count(*) FROM math_lesson_progress WHERE child_id='" + profile.ChildId + "' AND completed_count=1;") == 5,
                "first_five_exactly_five_completed_progress_rows");
            A(Count(database, "SELECT count(*) FROM session WHERE state='active';") == 0,
                "first_five_leave_no_active_session");
        }

        private static void TestFirstFiveAllAuthoredVariantsEndToEnd(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var firstFive = catalog.Lessons.Take(5).ToList();
            var database = NewDatabase(Path.Combine(root, "first-five-all-variants.db"), schemaPath);
            var profile = new LearnerSessionService(database).EnsurePrimaryChild("Bé 30 câu đầu");
            var accessService = new MathLessonProgressService(database, lessonCatalogPath);
            var progressStore = new MathLessonProgressStore(database);
            var deterministicIndex = typeof(MathSessionCoordinator).GetMethod(
                "DeterministicBucketIndex",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            A(deterministicIndex != null, "first_five_all_variants_finds_bucket_selector");

            var allDurableIds = new HashSet<string>(StringComparer.Ordinal);
            var totalCompletedSessions = 0;
            for (var lessonIndex = 0; lessonIndex < firstFive.Count; lessonIndex++)
            {
                var lesson = firstFive[lessonIndex];
                var access = accessService.GetAccess(profile.ChildId, lesson.Id);
                A(access.IsUnlocked && access.UnsatisfiedPrerequisiteLessonIds.Count == 0,
                    "first_five_all_variants_lesson_unlocked_" + (lessonIndex + 1));

                var buckets = new[]
                {
                    lesson.PracticeSets.Basic,
                    lesson.PracticeSets.Medium,
                    lesson.PracticeSets.Application
                };
                var bucketNames = new[] { "basic", "medium", "application" };
                A(buckets.All(x => x != null && x.Count == 2),
                    "first_five_all_variants_two_per_bucket_" + (lessonIndex + 1));
                var allIds = new HashSet<string>(buckets.SelectMany(x => x), StringComparer.Ordinal);
                A(allIds.Count == 6,
                    "first_five_all_variants_six_unique_ids_" + (lessonIndex + 1));

                var plannedSeen = new HashSet<string>(StringComparer.Ordinal);
                var seedsToRun = new List<int>();
                for (var seed = 0; seed < 16 && plannedSeen.Count < allIds.Count; seed++)
                {
                    var selectedForSeed = new List<string>();
                    for (var bucketIndex = 0; bucketIndex < buckets.Length; bucketIndex++)
                    {
                        var bucketChoice = (int)deterministicIndex.Invoke(null,
                            new object[] { seed, lesson.Id, bucketNames[bucketIndex], buckets[bucketIndex].Count });
                        selectedForSeed.Add(buckets[bucketIndex][bucketChoice]);
                    }
                    if (seed == 0 || selectedForSeed.Any(x => !plannedSeen.Contains(x)))
                    {
                        seedsToRun.Add(seed);
                        foreach (var id in selectedForSeed) plannedSeen.Add(id);
                    }
                }
                A(plannedSeen.SetEquals(allIds) && seedsToRun.Count >= 2 && seedsToRun.Count <= 4,
                    "first_five_all_variants_small_seed_plan_covers_six_" + (lessonIndex + 1));

                var seenInSessions = new HashSet<string>(StringComparer.Ordinal);
                foreach (var seed in seedsToRun)
                {
                    using (var coordinator = new MathSessionCoordinator(database, templatePath, "LOW",
                        10000 + lessonIndex * 100 + seed, lesson.Id))
                    {
                        var start = coordinator.Start("Bé 30 câu đầu");
                        var selected = start.SelectedContentQuestionIds.ToList();
                        A(start.TargetLessonId == lesson.Id && start.TargetQuestionCount == 3 && selected.Count == 3,
                            "first_five_all_variants_session_targets_three_" + lessonIndex + "_" + seed);
                        A(buckets[0].Contains(selected[0]) && buckets[1].Contains(selected[1]) && buckets[2].Contains(selected[2]),
                            "first_five_all_variants_session_bucket_order_" + lessonIndex + "_" + seed);
                        foreach (var id in selected)
                        {
                            seenInSessions.Add(id);
                            allDurableIds.Add(id);
                        }

                        for (var ordinal = 0; ordinal < 3; ordinal++)
                        {
                            var question = coordinator.NextQuestion();
                            A(question != null && question.LessonId == lesson.Id && question.SkillId == lesson.SkillId &&
                              question.ContentQuestionId == selected[ordinal] && buckets[ordinal].Contains(question.ContentQuestionId),
                                "first_five_all_variants_serves_exact_selected_" + lessonIndex + "_" + seed + "_" + ordinal);
                            var outcome = coordinator.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow,
                                700 + ordinal * 100);
                            A(outcome.IsCorrect && outcome.QuestionCompleted,
                                "first_five_all_variants_answer_commits_" + lessonIndex + "_" + seed + "_" + ordinal);
                        }
                        var summary = coordinator.Complete();
                        A(summary.LessonCompleted && summary.Attempts == 3 && summary.Correct == 3 &&
                          summary.TargetLessonId == lesson.Id,
                            "first_five_all_variants_session_completes_" + lessonIndex + "_" + seed);
                    }
                    totalCompletedSessions++;
                }

                A(seenInSessions.SetEquals(allIds),
                    "first_five_all_variants_real_sessions_cover_all_six_" + (lessonIndex + 1));
                foreach (var id in allIds)
                    A(Count(database, "SELECT count(*) FROM attempt WHERE question_id LIKE '" + id + "-%';") >= 1,
                        "first_five_all_variants_durable_attempt_" + id);

                var progress = progressStore.LoadOne(profile.ChildId, lesson.Id);
                A(progress != null && progress.StartedCount == seedsToRun.Count && progress.CompletedCount == seedsToRun.Count &&
                  progress.BestScorePercent.HasValue && Math.Abs(progress.BestScorePercent.Value - 100.0) < 0.0001,
                    "first_five_all_variants_progress_counts_replays_" + (lessonIndex + 1));
            }

            A(allDurableIds.Count == 30,
                "first_five_all_variants_cover_exactly_thirty_authored_questions");
            A(Count(database, "SELECT count(*) FROM attempt;") == totalCompletedSessions * 3 &&
              Count(database, "SELECT count(*) FROM mastery_event;") == totalCompletedSessions * 3,
                "first_five_all_variants_all_questions_commit_one_mastery_each");
            A(Count(database, "SELECT count(*) FROM math_lesson_progress WHERE child_id='" + profile.ChildId + "' AND completed_count>0;") == 5,
                "first_five_all_variants_leave_five_completed_progress_rows");
            A(Count(database, "SELECT count(*) FROM session WHERE child_id='" + profile.ChildId + "' AND state='active';") == 0,
                "first_five_all_variants_leave_no_active_session");
        }

        private static void TestFirstFiveRetryErrorMatrix(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var firstFive = catalog.Lessons.Take(5).ToList();
            var failOrdinals = new[] { 1, 1, 2, 0, 0 };
            var expectedKinds = new[] { "text", "text", "integer", "integer", "text" };
            var expectedTypes = new[] { "multiple_choice", "true_false", "numeric_input", "numeric_input", "multiple_choice" };
            var database = NewDatabase(Path.Combine(root, "first-five-retry-matrix.db"), schemaPath);
            var profile = new LearnerSessionService(database).EnsurePrimaryChild("Bé 5 bài lỗi thử");
            var accessService = new MathLessonProgressService(database, lessonCatalogPath);

            for (var lessonIndex = 0; lessonIndex < firstFive.Count; lessonIndex++)
            {
                var lesson = firstFive[lessonIndex];
                A(accessService.GetAccess(profile.ChildId, lesson.Id).IsUnlocked,
                    "first_five_retry_lesson_unlocked_" + (lessonIndex + 1));
                using (var coordinator = new MathSessionCoordinator(database, templatePath, "LOW", 12000 + lessonIndex, lesson.Id))
                {
                    var start = coordinator.Start("Bé 5 bài lỗi thử");
                    var selected = start.SelectedContentQuestionIds.ToList();
                    var failedQuestionId = string.Empty;
                    for (var ordinal = 0; ordinal < 3; ordinal++)
                    {
                        var question = coordinator.NextQuestion();
                        A(question != null && question.ContentQuestionId == selected[ordinal],
                            "first_five_retry_serves_selected_" + lessonIndex + "_" + ordinal);
                        if (ordinal == failOrdinals[lessonIndex])
                        {
                            failedQuestionId = question.QuestionId;
                            A(string.Equals(question.AnswerKind, expectedKinds[lessonIndex], StringComparison.Ordinal) &&
                              string.Equals(question.QuestionType, expectedTypes[lessonIndex], StringComparison.Ordinal),
                                "first_five_retry_hits_expected_answer_surface_" + (lessonIndex + 1));
                            var wrongText = WrongAnswer(question);
                            A(!question.IsCorrectAnswer(wrongText),
                                "first_five_retry_wrong_answer_is_really_wrong_" + (lessonIndex + 1));
                            var wrong = coordinator.SubmitAnswerWithRetryAt(wrongText, 0, "smoke", DateTime.UtcNow,
                                700 + ordinal * 100);
                            A(!wrong.IsCorrect && !wrong.QuestionCompleted && wrong.CanRetry && wrong.AttemptIndex == 1 &&
                              coordinator.Summary.Attempts == ordinal && coordinator.Summary.AnswerAttempts == ordinal + 1,
                                "first_five_retry_wrong_stays_open_" + (lessonIndex + 1));
                            A(coordinator.NextQuestion().QuestionId == question.QuestionId,
                                "first_five_retry_wrong_keeps_same_question_" + (lessonIndex + 1));
                            var retry = coordinator.SubmitRetryAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow,
                                760 + ordinal * 100);
                            A(retry.IsCorrect && retry.QuestionCompleted && retry.IsRetry && retry.AttemptIndex == 2 &&
                              !retry.IndependentSuccess && retry.Mastery != null && retry.Review != null,
                                "first_five_retry_correct_is_assisted_" + (lessonIndex + 1));
                        }
                        else
                        {
                            var outcome = coordinator.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow,
                                700 + ordinal * 100);
                            A(outcome.IsCorrect && outcome.QuestionCompleted && outcome.IndependentSuccess,
                                "first_five_retry_other_question_is_independent_" + lessonIndex + "_" + ordinal);
                        }
                    }

                    var summary = coordinator.Complete();
                    A(summary.LessonCompleted && summary.Attempts == 3 && summary.AnswerAttempts == 4 && summary.Correct == 3 &&
                      summary.IndependentCorrect == 2 && summary.RetriedQuestions == 1 && summary.RetriedCorrect == 1,
                        "first_five_retry_summary_separates_retry_" + (lessonIndex + 1));
                    A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + start.SessionId + "';") == 4 &&
                      Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + start.SessionId + "';") == 3,
                        "first_five_retry_session_has_four_answers_three_mastery_" + (lessonIndex + 1));
                    A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + start.SessionId +
                      "' AND question_id='" + failedQuestionId + "' AND attempt_index IN (1,2);") == 2,
                        "first_five_retry_failed_question_has_exact_two_attempts_" + (lessonIndex + 1));
                }
            }

            A(Count(database, "SELECT count(*) FROM session WHERE child_id='" + profile.ChildId + "' AND state='completed';") == 5,
                "first_five_retry_matrix_completes_five_sessions");
            A(Count(database, "SELECT count(*) FROM attempt;") == 20 && Count(database, "SELECT count(*) FROM mastery_event;") == 15,
                "first_five_retry_matrix_totals_twenty_answers_fifteen_mastery");
            A(Count(database, "SELECT count(*) FROM math_lesson_progress WHERE child_id='" + profile.ChildId + "' AND completed_count=1;") == 5,
                "first_five_retry_matrix_persists_five_completed_lessons");
            A(Count(database, "SELECT count(*) FROM session WHERE child_id='" + profile.ChildId + "' AND state='active';") == 0,
                "first_five_retry_matrix_leaves_no_active_session");
        }

        private static void TestFirstFiveHintedSuccessMatrix(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var firstFive = catalog.Lessons.Take(5).ToList();
            var database = NewDatabase(Path.Combine(root, "first-five-hint-matrix.db"), schemaPath);
            var profile = new LearnerSessionService(database).EnsurePrimaryChild("Bé 5 bài dùng gợi ý");
            var accessService = new MathLessonProgressService(database, lessonCatalogPath);

            for (var lessonIndex = 0; lessonIndex < firstFive.Count; lessonIndex++)
            {
                var lesson = firstFive[lessonIndex];
                A(accessService.GetAccess(profile.ChildId, lesson.Id).IsUnlocked,
                    "first_five_hint_lesson_unlocked_" + (lessonIndex + 1));
                using (var coordinator = new MathSessionCoordinator(database, templatePath, "LOW", 13000 + lessonIndex, lesson.Id))
                {
                    var start = coordinator.Start("Bé 5 bài dùng gợi ý");
                    var q1 = coordinator.NextQuestion();
                    A(q1 != null && !string.IsNullOrWhiteSpace(q1.HintLevel1) && !string.IsNullOrWhiteSpace(q1.HintLevel2),
                        "first_five_hint_q1_has_two_child_hints_" + (lessonIndex + 1));
                    var hinted = coordinator.SubmitAnswerAt(q1.CorrectAnswerDisplay, 2, "smoke", DateTime.UtcNow, 1000);
                    A(hinted.IsCorrect && hinted.QuestionCompleted && !hinted.IndependentSuccess && hinted.Mastery != null &&
                      hinted.Mastery.Reasons != null && hinted.Mastery.Reasons.Contains("hinted_correct_lower_weight"),
                        "first_five_hint_correct_is_not_independent_" + (lessonIndex + 1));
                    A(hinted.Review != null && string.Equals(hinted.Review.Reason, "hinted_success_short_recall", StringComparison.Ordinal),
                        "first_five_hint_schedules_short_recall_" + (lessonIndex + 1));

                    var q2 = coordinator.NextQuestion();
                    var q2Outcome = coordinator.SubmitAnswerAt(q2.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 800);
                    A(q2Outcome.IsCorrect && q2Outcome.IndependentSuccess,
                        "first_five_hint_q2_is_independent_" + (lessonIndex + 1));
                    var q3 = coordinator.NextQuestion();
                    var q3Outcome = coordinator.SubmitAnswerAt(q3.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 850);
                    A(q3Outcome.IsCorrect && q3Outcome.IndependentSuccess,
                        "first_five_hint_q3_is_independent_" + (lessonIndex + 1));

                    var summary = coordinator.Complete();
                    A(summary.LessonCompleted && summary.Attempts == 3 && summary.AnswerAttempts == 3 && summary.Correct == 3 &&
                      summary.IndependentCorrect == 2 && summary.HintedCorrect == 1 && summary.RetriedQuestions == 0,
                        "first_five_hint_summary_separates_hinted_success_" + (lessonIndex + 1));
                    A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + start.SessionId + "' AND hint_level=2;") == 1 &&
                      Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + start.SessionId + "' AND hint_level=0;") == 2,
                        "first_five_hint_persists_one_max_hint_two_independent_" + (lessonIndex + 1));
                    A(Count(database, "SELECT count(*) FROM child_skill WHERE child_id='" + profile.ChildId + "' AND skill_id='" + lesson.SkillId +
                      "' AND independent_success_count=2 AND hinted_success_count=1;") == 1,
                        "first_five_hint_mastery_buckets_are_exact_" + (lessonIndex + 1));
                }
            }

            A(Count(database, "SELECT count(*) FROM session WHERE child_id='" + profile.ChildId + "' AND state='completed';") == 5,
                "first_five_hint_matrix_completes_five_sessions");
            A(Count(database, "SELECT count(*) FROM attempt;") == 15 && Count(database, "SELECT count(*) FROM mastery_event;") == 15,
                "first_five_hint_matrix_commits_fifteen_learning_events");
            A(Count(database, "SELECT count(*) FROM math_lesson_progress WHERE child_id='" + profile.ChildId + "' AND completed_count=1;") == 5,
                "first_five_hint_matrix_persists_five_completed_lessons");
            A(Count(database, "SELECT count(*) FROM session WHERE child_id='" + profile.ChildId + "' AND state='active';") == 0,
                "first_five_hint_matrix_leaves_no_active_session");
        }

        private static void TestFirstLessonAllSixVariantsWithRetryResume(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var lesson = catalog.Lessons.First();
            A(lesson.Id == "m2_ls_num_count_read_write_0_1000" &&
              lesson.PracticeSets.Basic.Count == 2 && lesson.PracticeSets.Medium.Count == 2 &&
              lesson.PracticeSets.Application.Count == 2,
                "first_lesson_deep_slice_is_expected_six_question_pool");

            var deterministicIndex = typeof(MathSessionCoordinator).GetMethod(
                "DeterministicBucketIndex",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            A(deterministicIndex != null, "first_lesson_deep_slice_finds_bucket_selector");

            var buckets = new[]
            {
                lesson.PracticeSets.Basic,
                lesson.PracticeSets.Medium,
                lesson.PracticeSets.Application
            };
            var bucketNames = new[] { "basic", "medium", "application" };
            var allIds = new HashSet<string>(buckets.SelectMany(x => x), StringComparer.Ordinal);
            var plannedSeen = new HashSet<string>(StringComparer.Ordinal);
            var seedsToRun = new List<int>();
            for (var seed = 0; seed < 16 && plannedSeen.Count < allIds.Count; seed++)
            {
                var selectedForSeed = new List<string>();
                for (var bucketIndex = 0; bucketIndex < buckets.Length; bucketIndex++)
                {
                    var index = (int)deterministicIndex.Invoke(null,
                        new object[] { seed, lesson.Id, bucketNames[bucketIndex], buckets[bucketIndex].Count });
                    selectedForSeed.Add(buckets[bucketIndex][index]);
                }
                if (seed == 0 || selectedForSeed.Any(x => !plannedSeen.Contains(x)))
                {
                    seedsToRun.Add(seed);
                    foreach (var id in selectedForSeed) plannedSeen.Add(id);
                }
            }
            A(plannedSeen.SetEquals(allIds) && seedsToRun.Count >= 2 && seedsToRun.Count <= 4,
                "first_lesson_deep_slice_plans_small_seed_set_covering_all_six_questions");

            var database = NewDatabase(Path.Combine(root, "first-lesson-all-six.db"), schemaPath);
            var profile = new LearnerSessionService(database).EnsurePrimaryChild("Bé bài đầu kỹ");
            var seenDurable = new HashSet<string>(StringComparer.Ordinal);
            var completedSessions = 0;

            for (var runIndex = 0; runIndex < seedsToRun.Count; runIndex++)
            {
                var seed = seedsToRun[runIndex];
                if (runIndex == 0)
                {
                    string sessionId;
                    string mediumQuestionId;
                    IList<string> selected;
                    using (var first = new MathSessionCoordinator(database, templatePath, "LOW", seed, lesson.Id))
                    {
                        var start = first.Start("Bé bài đầu kỹ");
                        sessionId = start.SessionId;
                        selected = start.SelectedContentQuestionIds.ToList();
                        foreach (var id in selected) seenDurable.Add(id);
                        A(start.TargetLessonId == lesson.Id && start.TargetQuestionCount == 3,
                            "first_lesson_retry_session_targets_exact_lesson");

                        var q1 = first.NextQuestion();
                        first.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                        var q2 = first.NextQuestion();
                        mediumQuestionId = q2.QuestionId;
                        var wrong = first.SubmitAnswerWithRetryAt(WrongAnswer(q2), 0, "smoke", DateTime.UtcNow, 800);
                        A(!wrong.IsCorrect && !wrong.QuestionCompleted && wrong.CanRetry && wrong.AttemptIndex == 1 &&
                          first.Summary.Attempts == 1 && first.Summary.AnswerAttempts == 2,
                            "first_lesson_medium_wrong_enters_retry_without_completing_question");
                        first.Suspend("first_lesson_retry_resume");
                    }

                    using (var resumed = new MathSessionCoordinator(database, templatePath, "NORMAL", 999999, lesson.Id))
                    {
                        var start = resumed.Start("Bé bài đầu kỹ");
                        A(start.ResumedExistingSession && start.SessionId == sessionId && start.RetryPending &&
                          start.CurrentAttemptIndex == 2 && start.SelectedContentQuestionIds.SequenceEqual(selected),
                            "first_lesson_retry_resume_restores_same_selected_set_and_attempt_two");
                        var q2 = resumed.NextQuestion();
                        A(q2.QuestionId == mediumQuestionId && q2.ContentQuestionId == selected[1],
                            "first_lesson_retry_resume_keeps_exact_medium_question");
                        var retry = resumed.SubmitRetryAnswerAt(q2.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 850);
                        A(retry.IsCorrect && retry.QuestionCompleted && retry.IsRetry && !retry.IndependentSuccess &&
                          resumed.Summary.RetriedQuestions == 1 && resumed.Summary.RetriedCorrect == 1,
                            "first_lesson_retry_correct_is_assisted_and_finalizes_once");
                        var q3 = resumed.NextQuestion();
                        resumed.SubmitAnswerAt(q3.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 900);
                        var summary = resumed.Complete();
                        A(summary.LessonCompleted && summary.Attempts == 3 && summary.Correct == 3 &&
                          summary.AnswerAttempts == 4 && summary.IndependentCorrect == 2,
                            "first_lesson_retry_session_completes_three_questions_from_four_answers");
                    }
                }
                else
                {
                    using (var coordinator = new MathSessionCoordinator(database, templatePath, "LOW", seed, lesson.Id))
                    {
                        var start = coordinator.Start("Bé bài đầu kỹ");
                        var selected = start.SelectedContentQuestionIds.ToList();
                        foreach (var id in selected) seenDurable.Add(id);
                        A(start.TargetLessonId == lesson.Id && selected.Count == 3,
                            "first_lesson_variant_session_starts_" + runIndex);
                        for (var ordinal = 0; ordinal < 3; ordinal++)
                        {
                            var question = coordinator.NextQuestion();
                            A(question.ContentQuestionId == selected[ordinal],
                                "first_lesson_variant_session_serves_selected_" + runIndex + "_" + ordinal);
                            coordinator.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700 + ordinal * 100);
                        }
                        var summary = coordinator.Complete();
                        A(summary.LessonCompleted && summary.Attempts == 3 && summary.Correct == 3,
                            "first_lesson_variant_session_completes_" + runIndex);
                    }
                }
                completedSessions++;
            }

            A(seenDurable.SetEquals(allIds),
                "first_lesson_deep_slice_exercises_all_six_authored_ids_through_real_sessions");
            foreach (var id in allIds)
                A(Count(database, "SELECT count(*) FROM attempt WHERE question_id LIKE '" + id + "-%';") >= 1,
                    "first_lesson_deep_slice_has_durable_attempt_for_" + id);

            var progress = new MathLessonProgressStore(database).LoadOne(profile.ChildId, lesson.Id);
            A(progress != null && progress.StartedCount == completedSessions && progress.CompletedCount == completedSessions &&
              progress.BestScorePercent.HasValue && Math.Abs(progress.BestScorePercent.Value - 100.0) < 0.0001,
                "first_lesson_deep_slice_tracks_all_replays_and_best_score");
            A(Count(database, "SELECT count(*) FROM session WHERE child_id='" + profile.ChildId + "' AND state='completed';") == completedSessions,
                "first_lesson_deep_slice_completes_every_planned_session");
            A(Count(database, "SELECT count(*) FROM session WHERE child_id='" + profile.ChildId + "' AND state='active';") == 0,
                "first_lesson_deep_slice_leaves_no_active_session");
        }

        private static void TestTargetedCompletionSurvivesNextLessonReadFailure(string root, string schemaPath, string templatePath, string lessonCatalogPath)
        {
            var packDir = Path.Combine(root, "targeted-complete-postcommit-pack");
            Directory.CreateDirectory(packDir);
            var localTemplatePath = Path.Combine(packDir, "verified_templates_v1.json");
            var localCatalogPath = Path.Combine(packDir, "lesson_catalog_v1.json");
            var localQuestionBankPath = Path.Combine(packDir, "question_bank_v1.json");
            File.Copy(templatePath, localTemplatePath, true);
            File.Copy(lessonCatalogPath, localCatalogPath, true);
            File.Copy(Path.Combine(Path.GetDirectoryName(templatePath), "question_bank_v1.json"), localQuestionBankPath, true);

            var catalog = new MathLessonCatalogSource().Load(localCatalogPath);
            var lesson = catalog.Lessons.First(x =>
                (x.PrerequisiteSkills == null || x.PrerequisiteSkills.Count == 0) &&
                x.PracticeSets != null && x.PracticeSets.Basic.Count > 0 &&
                x.PracticeSets.Medium.Count > 0 && x.PracticeSets.Application.Count > 0);
            var database = NewDatabase(Path.Combine(root, "targeted-complete-postcommit.db"), schemaPath);
            string sessionId;

            using (var coordinator = new MathSessionCoordinator(database, localTemplatePath, "LOW", 8051, lesson.Id))
            {
                var started = coordinator.Start("Bé complete postcommit");
                sessionId = started.SessionId;
                A(started.TargetLessonId == lesson.Id && started.TargetQuestionCount == 3,
                    "postcommit_fixture_starts_targeted_lesson");

                for (var i = 0; i < 3; i++)
                {
                    var question = coordinator.NextQuestion();
                    A(question != null && question.LessonId == lesson.Id,
                        "postcommit_fixture_serves_authored_question_" + (i + 1));
                    coordinator.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 800 + i * 50);
                }
                A(coordinator.NextQuestion() == null,
                    "postcommit_fixture_finishes_all_targeted_questions");

                File.WriteAllText(localCatalogPath, "{ broken catalog after durable answers ");
                var summary = coordinator.Complete();
                A(summary.LessonCompleted && summary.LessonScorePercent.HasValue &&
                  Math.Abs(summary.LessonScorePercent.Value - 100.0) < 0.0001,
                    "postcommit_next_lesson_failure_does_not_fail_completion");
                A(string.IsNullOrWhiteSpace(summary.NextLessonId),
                    "postcommit_next_lesson_failure_only_drops_derived_recommendation");

                var secondCompleteRejected = false;
                try { coordinator.Complete(); }
                catch (InvalidOperationException) { secondCompleteRejected = true; }
                A(secondCompleteRejected,
                    "postcommit_success_marks_coordinator_inactive_before_downstream_enrichment");
            }

            A(Count(database, "SELECT count(*) FROM session WHERE id='" + sessionId + "' AND state='completed' AND ended_at_utc IS NOT NULL;") == 1,
                "postcommit_failure_keeps_session_durably_completed");
            A(Count(database, "SELECT count(*) FROM math_lesson_progress WHERE child_id='" + LearnerSessionService.PrimaryChildId +
                "' AND lesson_id='" + lesson.Id + "' AND started_count=1 AND completed_count=1;") == 1,
                "postcommit_failure_keeps_lesson_progress_exactly_once");
            A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + sessionId + "';") == 0,
                "postcommit_failure_still_cleans_runtime_checkpoint");
        }

        private static void TestCorruptRuntimeMetadataIsQuarantined(string root, string schemaPath, string templatePath)
        {
            var database = NewDatabase(Path.Combine(root, "corrupt-runtime-metadata.db"), schemaPath);
            string oldSessionId;
            string childId;
            string skillId;

            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 8071, 2))
            {
                var started = first.Start("Bé corrupt runtime metadata");
                oldSessionId = started.SessionId;
                childId = started.ChildId;
                var question = first.NextQuestion();
                skillId = question.SkillId;
                var outcome = first.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 850);
                A(outcome.IsCorrect && outcome.QuestionCompleted && first.Summary.Attempts == 1,
                    "corrupt_runtime_fixture_keeps_one_durable_completed_question");
                first.Suspend("corrupt_runtime_metadata_fixture");
            }

            A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + oldSessionId + "';") == 1,
                "corrupt_runtime_fixture_has_resumable_checkpoint_before_corruption");
            Exec(database,
                "UPDATE math_session_runtime SET updated_at_utc='not-a-date' WHERE session_id=@session;",
                "@session", oldSessionId);

            using (var replacement = new MathSessionCoordinator(database, templatePath, "NORMAL", 8072, 2))
            {
                var started = replacement.Start("Bé corrupt runtime metadata");
                A(!started.ResumedExistingSession && started.SessionId != oldSessionId && started.RecoveredDanglingSessions == 1,
                    "corrupt_runtime_metadata_is_quarantined_and_new_session_starts");
                A(started.CompletedQuestionCount == 0 && replacement.Summary.Attempts == 0,
                    "corrupt_runtime_quarantine_does_not_replay_partial_session_as_new_progress");
                A(Count(database, "SELECT count(*) FROM session WHERE id='" + oldSessionId + "' AND state='recovered' AND ended_at_utc IS NOT NULL;") == 1,
                    "corrupt_runtime_quarantine_marks_only_old_session_recovered");
                A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + oldSessionId + "';") == 0,
                    "corrupt_runtime_quarantine_removes_only_broken_checkpoint");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + oldSessionId + "';") == 1,
                    "corrupt_runtime_quarantine_preserves_durable_attempt_history");
                A(Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + oldSessionId + "';") == 1,
                    "corrupt_runtime_quarantine_preserves_mastery_event_history");
                A(Count(database, "SELECT count(*) FROM child_skill WHERE child_id='" + childId + "' AND skill_id='" + skillId + "' AND attempts_count=1;") == 1,
                    "corrupt_runtime_quarantine_preserves_child_skill_progress");
                A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + started.SessionId + "';") == 1,
                    "corrupt_runtime_quarantine_replacement_session_is_durably_resumable");
                replacement.Abort("corrupt_runtime_metadata_cleanup");
            }
        }

        private static void TestOperationalDatabaseFailureDoesNotQuarantineRuntime(string root, string schemaPath, string templatePath)
        {
            var dbPath = Path.Combine(root, "operational-db-failure.db");
            var database = NewDatabase(dbPath, schemaPath);
            string sessionId;
            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 8075, 2))
            {
                var started = first.Start("Bé operational DB failure");
                sessionId = started.SessionId;
                first.Suspend("operational_db_failure_fixture");
            }

            SQLiteConnection.ClearAllPools();
            var surfacedOperationalError = false;
            using (var fileLock = new FileStream(dbPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                try
                {
                    using (var blocked = new MathSessionCoordinator(database, templatePath, "NORMAL", 8076, 2))
                        blocked.Start("Bé operational DB failure");
                }
                catch (Exception ex)
                {
                    surfacedOperationalError = ex is SQLiteException || ex is IOException || ex.InnerException is SQLiteException;
                }
            }
            A(surfacedOperationalError,
                "operational_db_failure_surfaces_instead_of_being_classified_as_runtime_corruption");
            A(Count(database, "SELECT count(*) FROM session WHERE id='" + sessionId + "' AND state='active' AND ended_at_utc IS NULL;") == 1,
                "operational_db_failure_does_not_mark_resumable_session_recovered");
            A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + sessionId + "';") == 1,
                "operational_db_failure_does_not_delete_runtime_checkpoint");

            using (var resumed = new MathSessionCoordinator(database, templatePath, "NORMAL", 8077, 2))
            {
                var start = resumed.Start("Bé operational DB failure");
                A(start.ResumedExistingSession && start.SessionId == sessionId,
                    "operational_db_failure_session_resumes_after_lock_is_released");
                resumed.Abort("operational_db_failure_cleanup");
            }
        }

        private static void TestRuntimePackIdentityResumePolicy(string root, string schemaPath, string templatePath)
        {
            var bindDatabase = NewDatabase(Path.Combine(root, "runtime-pack-bind.db"), schemaPath);
            string bindSessionId;
            using (var first = new MathSessionCoordinator(bindDatabase, templatePath, "LOW", 8081, 2))
            {
                var started = first.Start("Bé legacy runtime bind");
                bindSessionId = started.SessionId;
                first.Suspend("legacy_zero_attempt_runtime_fixture");
            }

            Exec(bindDatabase,
                "UPDATE math_session_runtime SET pack_id=NULL,pack_version=NULL WHERE session_id=@session;",
                "@session", bindSessionId);
            using (var resumed = new MathSessionCoordinator(bindDatabase, templatePath, "NORMAL", 9991, 2))
            {
                var started = resumed.Start("Bé legacy runtime bind");
                A(started.ResumedExistingSession && started.SessionId == bindSessionId && started.RecoveredDanglingSessions == 0,
                    "legacy_zero_attempt_runtime_binds_current_pack_and_resumes");
                A(Count(bindDatabase,
                    "SELECT count(*) FROM math_session_runtime WHERE session_id='" + bindSessionId +
                    "' AND pack_id='" + MathSessionCoordinator.PackId + "' AND pack_version='" + MathSessionCoordinator.PackVersion + "';") == 1,
                    "legacy_zero_attempt_runtime_persists_current_pack_identity");
                resumed.Abort("legacy_zero_attempt_runtime_cleanup");
            }

            var partialDatabase = NewDatabase(Path.Combine(root, "runtime-pack-partial.db"), schemaPath);
            string partialSessionId;
            using (var first = new MathSessionCoordinator(partialDatabase, templatePath, "LOW", 8086, 2))
            {
                var started = first.Start("Bé partial pack identity");
                partialSessionId = started.SessionId;
                first.Suspend("partial_pack_identity_fixture");
            }
            var partialRejected = false;
            try
            {
                Exec(partialDatabase,
                    "UPDATE math_session_runtime SET pack_version=NULL WHERE session_id=@session;",
                    "@session", partialSessionId);
            }
            catch (System.Data.SQLite.SQLiteException)
            {
                partialRejected = true;
            }
            A(partialRejected, "partial_pack_identity_is_rejected_by_v5_database_guard");
            A(Count(partialDatabase,
                "SELECT count(*) FROM math_session_runtime WHERE session_id='" + partialSessionId +
                "' AND pack_id='" + MathSessionCoordinator.PackId + "' AND pack_version='" + MathSessionCoordinator.PackVersion + "';") == 1,
                "partial_pack_identity_rejected_write_leaves_complete_pair_intact");
            using (var resumedPartial = new MathSessionCoordinator(partialDatabase, templatePath, "NORMAL", 8087, 2))
            {
                var started = resumedPartial.Start("Bé partial pack identity");
                A(started.ResumedExistingSession && started.SessionId == partialSessionId && started.RecoveredDanglingSessions == 0,
                    "partial_pack_identity_rejected_write_keeps_session_resumable");
                resumedPartial.Abort("partial_pack_identity_cleanup");
            }

            var mismatchDatabase = NewDatabase(Path.Combine(root, "runtime-pack-mismatch.db"), schemaPath);
            var sessions = new LearnerSessionService(mismatchDatabase);
            var profile = sessions.EnsurePrimaryChild("Bé legacy pack mismatch");
            var runtime = new MathSessionRuntimeService(mismatchDatabase);
            var legacy = runtime.TryCreateSession(
                profile.ChildId, "LOW", 8082, 2, "adaptive", null, null, "legacy-pack", "0.9");
            A(legacy != null, "legacy_pack_fixture_creates_active_runtime");

            var now = DateTime.UtcNow;
            var legacyAttemptId = "attempt-legacy-pack-" + Guid.NewGuid().ToString("N");
            new AnswerCommitService(mismatchDatabase).Commit(new AnswerCommitRequest
            {
                AttemptId = legacyAttemptId,
                SessionId = legacy.SessionId,
                ChildId = profile.ChildId,
                PackId = "legacy-pack",
                PackVersion = "0.9",
                QuestionId = "legacy-pack-question",
                SkillId = "LEGACY_PACK_SKILL",
                Subject = "math",
                StartedAtUtc = now.AddSeconds(-1),
                AnsweredAtUtc = now,
                AnswerJson = "{\"answer\":1}",
                IsCorrect = true,
                ResponseMs = 1000,
                HintLevel = 0,
                Representation = "symbolic",
                InputMethod = "smoke",
                AttemptIndex = 1,
                ListenCount = 0
            });
            A(Count(mismatchDatabase,
                "SELECT count(*) FROM attempt WHERE id='" + legacyAttemptId + "' AND pack_id='legacy-pack' AND pack_version='0.9';") == 1,
                "legacy_pack_fixture_has_durable_old_pack_attempt");

            using (var replacement = new MathSessionCoordinator(mismatchDatabase, templatePath, "NORMAL", 8083, 2))
            {
                var started = replacement.Start("Bé legacy pack mismatch");
                A(!started.ResumedExistingSession && started.SessionId != legacy.SessionId && started.RecoveredDanglingSessions == 1,
                    "old_pack_runtime_is_recovered_instead_of_mixed_with_current_pack");
                A(Count(mismatchDatabase,
                    "SELECT count(*) FROM session WHERE id='" + legacy.SessionId + "' AND state='recovered' AND ended_at_utc IS NOT NULL;") == 1,
                    "old_pack_runtime_marks_only_legacy_session_recovered");
                A(Count(mismatchDatabase,
                    "SELECT count(*) FROM math_session_runtime WHERE session_id='" + legacy.SessionId + "';") == 0,
                    "old_pack_runtime_checkpoint_is_removed_after_recovery");
                A(Count(mismatchDatabase,
                    "SELECT count(*) FROM attempt WHERE id='" + legacyAttemptId + "' AND session_id='" + legacy.SessionId + "';") == 1,
                    "old_pack_recovery_preserves_durable_attempt_history");
                A(Count(mismatchDatabase,
                    "SELECT count(*) FROM math_session_runtime WHERE session_id='" + started.SessionId +
                    "' AND pack_id='" + MathSessionCoordinator.PackId + "' AND pack_version='" + MathSessionCoordinator.PackVersion + "';") == 1,
                    "replacement_runtime_is_pinned_to_current_pack_identity");
                replacement.Abort("legacy_pack_mismatch_cleanup");
            }

            var blankDatabase = NewDatabase(Path.Combine(root, "runtime-pack-blank-history.db"), schemaPath);
            var blankSessions = new LearnerSessionService(blankDatabase);
            var blankProfile = blankSessions.EnsurePrimaryChild("Bé legacy blank pack");
            var blankRuntime = new MathSessionRuntimeService(blankDatabase);
            var blankLegacy = blankRuntime.TryCreateSession(
                blankProfile.ChildId, "LOW", 8084, 2, "adaptive", null);
            A(blankLegacy != null, "blank_pack_fixture_creates_legacy_unbound_runtime");
            var blankAttemptId = "attempt-blank-pack-" + Guid.NewGuid().ToString("N");
            Exec(blankDatabase, @"INSERT INTO attempt(
id,session_id,child_id,pack_id,pack_version,question_id,skill_id,subject,started_at_utc,answered_at_utc,answer_json,is_correct,response_ms,hint_level,representation,input_method,attempt_index,listen_count)
VALUES(@id,@session,@child,' ','','blank-pack-question','LEGACY_PACK_SKILL','math',@started,@answered,'{}',1,700,0,'symbolic','smoke',1,0);",
                "@id", blankAttemptId,
                "@session", blankLegacy.SessionId,
                "@child", blankProfile.ChildId,
                "@started", DateTime.UtcNow.AddSeconds(-1).ToString("o", CultureInfo.InvariantCulture),
                "@answered", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

            using (var replacement = new MathSessionCoordinator(blankDatabase, templatePath, "NORMAL", 8085, 2))
            {
                var started = replacement.Start("Bé legacy blank pack");
                A(!started.ResumedExistingSession && started.SessionId != blankLegacy.SessionId && started.RecoveredDanglingSessions == 1,
                    "blank_pack_history_is_quarantined_instead_of_bound_to_current_pack");
                A(Count(blankDatabase,
                    "SELECT count(*) FROM session WHERE id='" + blankLegacy.SessionId + "' AND state='recovered' AND ended_at_utc IS NOT NULL;") == 1,
                    "blank_pack_history_marks_legacy_session_recovered");
                A(Count(blankDatabase,
                    "SELECT count(*) FROM attempt WHERE id='" + blankAttemptId + "' AND session_id='" + blankLegacy.SessionId + "';") == 1,
                    "blank_pack_history_preserves_durable_attempt");
                A(Count(blankDatabase,
                    "SELECT count(*) FROM math_session_runtime WHERE session_id='" + started.SessionId +
                    "' AND pack_id='" + MathSessionCoordinator.PackId + "' AND pack_version='" + MathSessionCoordinator.PackVersion + "';") == 1,
                    "blank_pack_history_replacement_is_pinned_to_current_pack");
                replacement.Abort("blank_pack_history_cleanup");
            }
        }
        private static void TestTargetedCorruptOpenQuestionReplaysAuthoredOrdinal(string root, string schemaPath, string templatePath, string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var lesson = catalog.Lessons.First(x => (x.PrerequisiteSkills == null || x.PrerequisiteSkills.Count == 0) &&
                                                     x.PracticeSets != null && x.PracticeSets.Basic.Count > 0 &&
                                                     x.PracticeSets.Medium.Count > 0 && x.PracticeSets.Application.Count > 0);
            var database = NewDatabase(Path.Combine(root, "targeted-corrupt-cache.db"), schemaPath);
            string sessionId;
            IList<string> corruptSelectedIds;

            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 8101, lesson.Id))
            {
                var start = first.Start("Bé targeted corrupt");
                sessionId = start.SessionId;
                corruptSelectedIds = start.SelectedContentQuestionIds.ToList();
                var basic = first.NextQuestion();
                A(basic.ContentQuestionId == corruptSelectedIds[0] && lesson.PracticeSets.Basic.Contains(basic.ContentQuestionId),
                    "targeted_corrupt_fixture_starts_with_selected_basic");
                first.SubmitAnswerAt(basic.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 800);

                var medium = first.NextQuestion();
                A(medium.ContentQuestionId == corruptSelectedIds[1] && lesson.PracticeSets.Medium.Contains(medium.ContentQuestionId),
                    "targeted_corrupt_fixture_opens_selected_medium");
                Exec(database, "UPDATE math_session_runtime SET current_question_json='not-json-at-all' WHERE session_id=@session;",
                    "@session", sessionId);
                first.Suspend("simulate_targeted_corrupt_cache");
            }

            A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + sessionId + "';") == 1,
                "targeted_corrupt_fixture_keeps_one_committed_attempt");
            A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + sessionId +
                "' AND generated_question_count=2 AND current_question_json='not-json-at-all';") == 1,
                "targeted_corrupt_fixture_persists_broken_medium_cursor");

            using (var resumed = new MathSessionCoordinator(database, templatePath, "NORMAL", 9999, 9))
            {
                var start = resumed.Start("Bé targeted corrupt");
                A(start.ResumedExistingSession && start.TargetLessonId == lesson.Id,
                    "targeted_corrupt_resume_keeps_target_lesson");
                A(start.DiscardedCorruptOpenQuestion && !start.RestoredOpenQuestion && start.CompletedQuestionCount == 1,
                    "targeted_corrupt_resume_discards_only_open_question");
                A(start.SelectedContentQuestionIds.SequenceEqual(corruptSelectedIds),
                    "targeted_corrupt_resume_preserves_selected_set");
                A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + sessionId +
                    "' AND generated_question_count=1 AND current_question_json IS NULL;") == 1,
                    "targeted_corrupt_resume_rolls_cursor_back_to_committed_ordinal");

                var medium = resumed.NextQuestion();
                A(medium != null && medium.ContentQuestionId == corruptSelectedIds[1],
                    "targeted_corrupt_resume_replays_selected_medium_not_application");
                resumed.SubmitAnswerAt(medium.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 850);

                var application = resumed.NextQuestion();
                A(application != null && application.ContentQuestionId == corruptSelectedIds[2],
                    "targeted_corrupt_resume_then_serves_selected_application");
                resumed.SubmitAnswerAt(application.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 900);
                A(resumed.Summary.Attempts == 3 && resumed.NextQuestion() == null,
                    "targeted_corrupt_resume_requires_all_three_authored_attempts");

                var summary = resumed.Complete();
                A(summary.LessonCompleted && summary.Attempts == 3 && summary.TargetLessonId == lesson.Id,
                    "targeted_corrupt_resume_completes_only_after_full_authored_set");
            }
        }

        private static void TestTargetedGeneratedOrdinalSelfHealsWithoutOpenQuestion(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var lesson = catalog.Lessons.First(x =>
                (x.PrerequisiteSkills == null || x.PrerequisiteSkills.Count == 0) &&
                x.PracticeSets != null && x.PracticeSets.Basic.Count >= 1 &&
                x.PracticeSets.Medium.Count >= 1 && x.PracticeSets.Application.Count >= 1);
            var database = NewDatabase(Path.Combine(root, "targeted-corrupt-generated-ordinal.db"), schemaPath);
            string sessionId;
            IList<string> selected;

            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 8111, lesson.Id))
            {
                var start = first.Start("Bé targeted ordinal repair");
                sessionId = start.SessionId;
                selected = start.SelectedContentQuestionIds.ToList();
                var basic = first.NextQuestion();
                A(basic.ContentQuestionId == selected[0],
                    "targeted_ordinal_repair_fixture_opens_selected_basic");
                first.SubmitAnswerAt(basic.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                A(first.Summary.Attempts == 1 && !first.HasOpenQuestion,
                    "targeted_ordinal_repair_fixture_has_one_committed_question_no_open_cache");
                first.Suspend("targeted_ordinal_repair_fixture");
            }

            Exec(database,
                "UPDATE math_session_runtime SET generated_question_count=3,current_question_json=NULL,question_started_at_utc=NULL WHERE session_id=@session;",
                "@session", sessionId);
            A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + sessionId +
                "' AND generated_question_count=3 AND current_question_json IS NULL;") == 1,
                "targeted_ordinal_repair_fixture_persists_ahead_cursor_without_open_question");

            using (var resumed = new MathSessionCoordinator(database, templatePath, "NORMAL", 999999, lesson.Id))
            {
                var start = resumed.Start("Bé targeted ordinal repair");
                A(start.ResumedExistingSession && start.CompletedQuestionCount == 1 && !start.RestoredOpenQuestion,
                    "targeted_ordinal_repair_resumes_one_committed_question");
                A(start.SelectedContentQuestionIds.SequenceEqual(selected),
                    "targeted_ordinal_repair_preserves_selected_set");
                A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + sessionId +
                    "' AND generated_question_count=1 AND current_question_json IS NULL;") == 1,
                    "targeted_ordinal_repair_rewrites_cursor_to_committed_ordinal");
                var medium = resumed.NextQuestion();
                A(medium != null && medium.ContentQuestionId == selected[1] &&
                  lesson.PracticeSets.Medium.Contains(medium.ContentQuestionId),
                    "targeted_ordinal_repair_serves_selected_medium_not_end_of_session");
                resumed.Abort("targeted_ordinal_repair_cleanup");
            }
        }

        private static void TestTargetedSemanticOpenQuestionOrdinalMismatchSelfHeals(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath,
            string questionBankPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var lesson = catalog.Lessons.First(x =>
                (x.PrerequisiteSkills == null || x.PrerequisiteSkills.Count == 0) &&
                x.PracticeSets != null && x.PracticeSets.Basic.Count >= 1 &&
                x.PracticeSets.Medium.Count >= 1 && x.PracticeSets.Application.Count >= 1);
            var bank = new MathAuthoredQuestionSource().Load(questionBankPath);
            var database = NewDatabase(Path.Combine(root, "targeted-semantic-ordinal-mismatch.db"), schemaPath);
            string sessionId;
            IList<string> selected;

            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 8121, lesson.Id))
            {
                var start = first.Start("Bé targeted semantic ordinal");
                sessionId = start.SessionId;
                selected = start.SelectedContentQuestionIds.ToList();
                var basic = first.NextQuestion();
                first.SubmitAnswerAt(basic.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                var medium = first.NextQuestion();
                A(medium.ContentQuestionId == selected[1],
                    "targeted_semantic_ordinal_fixture_opens_selected_medium");
                first.Suspend("targeted_semantic_ordinal_fixture");
            }

            var authoredApplication = bank.Questions.Single(x => x.ContentQuestionId == selected[2]);
            var cachedApplication = MathAuthoredQuestionSource.CreateRuntimeInstance(authoredApplication);
            Exec(database,
                "UPDATE math_session_runtime SET generated_question_count=3,current_question_json=@question WHERE session_id=@session;",
                "@question", Json.Serialize(cachedApplication), "@session", sessionId);
            A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + sessionId +
                "' AND generated_question_count=3 AND current_question_json IS NOT NULL;") == 1,
                "targeted_semantic_ordinal_fixture_persists_valid_application_cache_ahead_of_progress");

            using (var resumed = new MathSessionCoordinator(database, templatePath, "NORMAL", 999999, lesson.Id))
            {
                var start = resumed.Start("Bé targeted semantic ordinal");
                A(start.ResumedExistingSession && start.CompletedQuestionCount == 1 &&
                  start.DiscardedCorruptOpenQuestion && !start.RestoredOpenQuestion,
                    "targeted_semantic_ordinal_resume_discards_semantically_valid_but_skipping_cache");
                A(start.SelectedContentQuestionIds.SequenceEqual(selected),
                    "targeted_semantic_ordinal_resume_preserves_selected_set");
                A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + sessionId +
                    "' AND generated_question_count=1 AND current_question_json IS NULL;") == 1,
                    "targeted_semantic_ordinal_resume_rewrites_cursor_to_one_committed_question");
                var medium = resumed.NextQuestion();
                A(medium != null && medium.ContentQuestionId == selected[1] &&
                  lesson.PracticeSets.Medium.Contains(medium.ContentQuestionId),
                    "targeted_semantic_ordinal_resume_replays_medium_before_application");
                resumed.Abort("targeted_semantic_ordinal_cleanup");
            }
        }

        private static void TestTargetedTamperedAuthoredCacheIsDiscarded(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var lesson = catalog.Lessons.First(x =>
                (x.PrerequisiteSkills == null || x.PrerequisiteSkills.Count == 0) &&
                x.PracticeSets != null && x.PracticeSets.Basic.Count >= 1 &&
                x.PracticeSets.Medium.Count >= 1 && x.PracticeSets.Application.Count >= 1);
            var database = NewDatabase(Path.Combine(root, "targeted-tampered-authored-cache.db"), schemaPath);
            string sessionId;
            IList<string> selected;
            string canonicalMediumQuestionId;
            string canonicalMediumPrompt;
            string canonicalMediumAnswer;

            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 8125, lesson.Id))
            {
                var start = first.Start("Bé targeted tampered cache");
                sessionId = start.SessionId;
                selected = start.SelectedContentQuestionIds.ToList();
                var basic = first.NextQuestion();
                first.SubmitAnswerAt(basic.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                var medium = first.NextQuestion();
                canonicalMediumQuestionId = medium.QuestionId;
                canonicalMediumPrompt = medium.PromptVi;
                canonicalMediumAnswer = medium.CorrectAnswerDisplay;
                A(medium.ContentQuestionId == selected[1],
                    "targeted_tampered_cache_fixture_opens_selected_medium");
                first.Suspend("targeted_tampered_cache_fixture");
            }

            var cachedJson = ScalarText(database,
                "SELECT current_question_json FROM math_session_runtime WHERE session_id='" + sessionId + "';");
            var tampered = Json.Deserialize<MathQuestion>(cachedJson);
            tampered.PromptVi = canonicalMediumPrompt + " [TAMPERED]";
            tampered.CorrectAnswer = tampered.CorrectAnswer + 111;
            tampered.CorrectAnswerText = "__tampered_answer__";
            tampered.AcceptedAnswers = new[] { "__tampered_answer__" };
            tampered.ExpectedUnit = "__tampered_unit__";
            tampered.AcceptedUnits = new[] { "__tampered_unit__" };
            Exec(database,
                "UPDATE math_session_runtime SET current_question_json=@question WHERE session_id=@session;",
                "@question", Json.Serialize(tampered), "@session", sessionId);

            using (var resumed = new MathSessionCoordinator(database, templatePath, "NORMAL", 999999, lesson.Id))
            {
                var start = resumed.Start("Bé targeted tampered cache");
                A(start.ResumedExistingSession && start.CompletedQuestionCount == 1 &&
                  start.DiscardedCorruptOpenQuestion && !start.RestoredOpenQuestion,
                    "targeted_tampered_cache_is_discarded_instead_of_trusted");
                A(start.SelectedContentQuestionIds.SequenceEqual(selected),
                    "targeted_tampered_cache_preserves_selected_set");
                var medium = resumed.NextQuestion();
                A(medium != null && medium.ContentQuestionId == selected[1] &&
                  medium.QuestionId == canonicalMediumQuestionId && medium.PromptVi == canonicalMediumPrompt &&
                  medium.CorrectAnswerDisplay == canonicalMediumAnswer,
                    "targeted_tampered_cache_replays_canonical_medium_contract");
                resumed.Abort("targeted_tampered_cache_cleanup");
            }
        }

        private static void TestExpandedTargetedPoolSelectsDurableThree(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath,
            string questionBankPath)
        {
            var pack = BuildSyntheticExpandedPoolPack(root, templatePath, lessonCatalogPath, questionBankPath);

            IList<string> seedASelection;
            var seedADatabase = NewDatabase(Path.Combine(root, "pool6-seed-a.db"), schemaPath);
            using (var first = new MathSessionCoordinator(seedADatabase, pack.TemplatePath, "LOW", 9101, pack.LessonId))
            {
                var start = first.Start("Bé pool sáu A");
                seedASelection = start.SelectedContentQuestionIds.ToList();
                AssertValidSelectedSet(start, pack, "pool6_seed_a");
                A(start.TargetQuestionCount == MathSessionCoordinator.TargetedLessonQuestionCount,
                    "pool6_seed_a_target_is_three_not_pool_six");
                var checkpoint = ScalarText(seedADatabase,
                    "SELECT current_selection_json FROM math_session_runtime WHERE session_id='" + start.SessionId + "';");
                A(!string.IsNullOrWhiteSpace(checkpoint) && checkpoint.Contains("selected_content_question_ids"),
                    "pool6_seed_a_persists_selected_set_before_first_question");
                first.Abort("pool6_seed_a_cleanup");
            }

            var seedARepeatDatabase = NewDatabase(Path.Combine(root, "pool6-seed-a-repeat.db"), schemaPath);
            using (var repeated = new MathSessionCoordinator(seedARepeatDatabase, pack.TemplatePath, "LOW", 9101, pack.LessonId))
            {
                var start = repeated.Start("Bé pool sáu A lặp");
                AssertValidSelectedSet(start, pack, "pool6_seed_a_repeat");
                A(start.SelectedContentQuestionIds.SequenceEqual(seedASelection),
                    "pool6_same_seed_and_lesson_selects_same_ordered_set");
                repeated.Abort("pool6_seed_a_repeat_cleanup");
            }

            var seedBDatabase = NewDatabase(Path.Combine(root, "pool6-seed-b.db"), schemaPath);
            using (var second = new MathSessionCoordinator(seedBDatabase, pack.TemplatePath, "LOW", 9102, pack.LessonId))
            {
                var start = second.Start("Bé pool sáu B");
                AssertValidSelectedSet(start, pack, "pool6_seed_b");
                A(!seedASelection.SequenceEqual(start.SelectedContentQuestionIds),
                    "pool6_different_selection_identity_can_choose_different_set");
                second.Abort("pool6_seed_b_cleanup");
            }

            var stressDatabase = NewDatabase(Path.Combine(root, "pool6-selection-stress.db"), schemaPath);
            var stressBasic = new HashSet<string>(StringComparer.Ordinal);
            var stressMedium = new HashSet<string>(StringComparer.Ordinal);
            var stressApplication = new HashSet<string>(StringComparer.Ordinal);
            var stressSignatures = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < 16; i++)
            {
                using (var coordinator = new MathSessionCoordinator(stressDatabase, pack.TemplatePath, "LOW", 9600 + i, pack.LessonId))
                {
                    var start = coordinator.Start("Bé pool sáu stress");
                    AssertValidSelectedSet(start, pack, "pool6_stress_" + i.ToString(CultureInfo.InvariantCulture));
                    A(start.TargetQuestionCount == MathSessionCoordinator.TargetedLessonQuestionCount,
                        "pool6_stress_target_three_" + i.ToString(CultureInfo.InvariantCulture));
                    stressBasic.Add(start.SelectedContentQuestionIds[0]);
                    stressMedium.Add(start.SelectedContentQuestionIds[1]);
                    stressApplication.Add(start.SelectedContentQuestionIds[2]);
                    stressSignatures.Add(string.Join("|", start.SelectedContentQuestionIds));
                    coordinator.Abort("pool6_stress_cleanup");
                }
            }
            A(stressBasic.Count == pack.Basic.Count, "pool6_stress_rotates_all_basic_variants");
            A(stressMedium.Count == pack.Medium.Count, "pool6_stress_rotates_all_medium_variants");
            A(stressApplication.Count == pack.Application.Count, "pool6_stress_rotates_all_application_variants");
            A(stressSignatures.Count >= 2, "pool6_stress_produces_multiple_valid_selected_sets");

            var retryDatabase = NewDatabase(Path.Combine(root, "pool6-retry-resume.db"), schemaPath);
            IList<string> retrySelection;
            string retrySessionId;
            string retryQuestionInstanceId;
            using (var first = new MathSessionCoordinator(retryDatabase, pack.TemplatePath, "LOW", 9201, pack.LessonId))
            {
                var start = first.Start("Bé pool sáu retry");
                retrySessionId = start.SessionId;
                retrySelection = start.SelectedContentQuestionIds.ToList();
                AssertValidSelectedSet(start, pack, "pool6_retry_fresh");
                var q1 = first.NextQuestion();
                A(q1.ContentQuestionId == retrySelection[0], "pool6_retry_serves_selected_basic");
                first.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                var q2 = first.NextQuestion();
                retryQuestionInstanceId = q2.QuestionId;
                A(q2.ContentQuestionId == retrySelection[1], "pool6_retry_serves_selected_medium");
                var wrong = first.SubmitAnswerWithRetryAt(WrongAnswer(q2), 0, "smoke", DateTime.UtcNow, 750);
                A(!wrong.IsCorrect && wrong.CanRetry && !wrong.QuestionCompleted,
                    "pool6_retry_keeps_selected_medium_open_after_first_wrong");
                first.Suspend("pool6_retry_suspend");
            }

            using (var resumed = new MathSessionCoordinator(retryDatabase, pack.TemplatePath, "NORMAL", 999999, pack.LessonId))
            {
                var start = resumed.Start("Bé pool sáu retry");
                A(start.ResumedExistingSession && start.SessionId == retrySessionId,
                    "pool6_retry_resumes_same_session");
                A(start.SelectedContentQuestionIds.SequenceEqual(retrySelection),
                    "pool6_retry_resume_preserves_ordered_selected_set");
                A(start.TargetQuestionCount == 3 && start.CompletedQuestionCount == 1 && start.RetryPending && start.CurrentAttemptIndex == 2,
                    "pool6_retry_resume_keeps_three_target_and_attempt_two");
                var q2 = resumed.NextQuestion();
                A(q2.QuestionId == retryQuestionInstanceId && q2.ContentQuestionId == retrySelection[1],
                    "pool6_retry_resume_restores_exact_selected_medium_instance");
                var corrected = resumed.SubmitRetryAnswerAt(q2.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 800);
                A(corrected.IsCorrect && corrected.QuestionCompleted,
                    "pool6_retry_second_attempt_finalizes_selected_medium");
                var q3 = resumed.NextQuestion();
                A(q3.ContentQuestionId == retrySelection[2], "pool6_retry_then_serves_selected_application");
                resumed.SubmitAnswerAt(q3.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 850);
                A(resumed.Summary.Attempts == 3 && resumed.NextQuestion() == null,
                    "pool6_retry_completes_after_three_selected_questions_not_six");
                var summary = resumed.Complete();
                A(summary.LessonCompleted, "pool6_retry_selected_three_can_complete_lesson");
            }

            var corruptDatabase = NewDatabase(Path.Combine(root, "pool6-corrupt-open.db"), schemaPath);
            IList<string> corruptSelection;
            string corruptSessionId;
            using (var first = new MathSessionCoordinator(corruptDatabase, pack.TemplatePath, "LOW", 9301, pack.LessonId))
            {
                var start = first.Start("Bé pool sáu corrupt");
                corruptSessionId = start.SessionId;
                corruptSelection = start.SelectedContentQuestionIds.ToList();
                AssertValidSelectedSet(start, pack, "pool6_corrupt_fresh");
                var q1 = first.NextQuestion();
                first.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                var q2 = first.NextQuestion();
                A(q2.ContentQuestionId == corruptSelection[1], "pool6_corrupt_opens_selected_medium");
                Exec(corruptDatabase,
                    "UPDATE math_session_runtime SET current_question_json='not-json-at-all' WHERE session_id=@session;",
                    "@session", corruptSessionId);
                first.Suspend("pool6_corrupt_suspend");
            }

            using (var resumed = new MathSessionCoordinator(corruptDatabase, pack.TemplatePath, "NORMAL", 123456, pack.LessonId))
            {
                var start = resumed.Start("Bé pool sáu corrupt");
                A(start.ResumedExistingSession && start.DiscardedCorruptOpenQuestion && start.CompletedQuestionCount == 1,
                    "pool6_corrupt_resume_discards_only_open_question");
                A(start.SelectedContentQuestionIds.SequenceEqual(corruptSelection),
                    "pool6_corrupt_resume_preserves_selected_set");
                var medium = resumed.NextQuestion();
                A(medium.ContentQuestionId == corruptSelection[1],
                    "pool6_corrupt_replays_selected_medium_not_other_pool_question");
                resumed.SubmitAnswerAt(medium.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 800);
                var application = resumed.NextQuestion();
                A(application.ContentQuestionId == corruptSelection[2],
                    "pool6_corrupt_then_serves_selected_application");
                resumed.SubmitAnswerAt(application.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 850);
                A(resumed.Summary.Attempts == 3 && resumed.NextQuestion() == null,
                    "pool6_corrupt_recovery_completes_selected_three_only");
                resumed.Abort("pool6_corrupt_cleanup");
            }

            var selectionRepairDatabase = NewDatabase(Path.Combine(root, "pool6-corrupt-selection.db"), schemaPath);
            IList<string> selectionBeforeCorruption;
            string selectionRepairSessionId;
            using (var first = new MathSessionCoordinator(selectionRepairDatabase, pack.TemplatePath, "LOW", 9401, pack.LessonId))
            {
                var start = first.Start("Bé pool sáu selection corrupt");
                selectionRepairSessionId = start.SessionId;
                selectionBeforeCorruption = start.SelectedContentQuestionIds.ToList();
                var basic = first.NextQuestion();
                A(basic.ContentQuestionId == selectionBeforeCorruption[0],
                    "pool6_selection_corrupt_fixture_serves_selected_basic");
                first.SubmitAnswerAt(basic.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                Exec(selectionRepairDatabase,
                    "UPDATE math_session_runtime SET current_selection_json=@selection WHERE session_id=@session;",
                    "@selection", "not-json-at-all", "@session", selectionRepairSessionId);
                first.Suspend("pool6_selection_corrupt_suspend");
            }

            using (var resumed = new MathSessionCoordinator(selectionRepairDatabase, pack.TemplatePath, "NORMAL", 987654, pack.LessonId))
            {
                var start = resumed.Start("Bé pool sáu selection corrupt");
                A(start.ResumedExistingSession && start.CompletedQuestionCount == 1 && !start.RestoredOpenQuestion,
                    "pool6_corrupt_selection_metadata_resumes_committed_progress");
                A(start.SelectedContentQuestionIds.SequenceEqual(selectionBeforeCorruption),
                    "pool6_corrupt_selection_metadata_rebuilds_same_deterministic_set");
                var repairedJson = ScalarText(selectionRepairDatabase,
                    "SELECT current_selection_json FROM math_session_runtime WHERE session_id='" + selectionRepairSessionId + "';");
                A(!string.IsNullOrWhiteSpace(repairedJson) && repairedJson.Contains("selected_content_question_ids") &&
                  repairedJson.Contains(selectionBeforeCorruption[1]),
                    "pool6_corrupt_selection_metadata_is_repaired_in_checkpoint");
                var medium = resumed.NextQuestion();
                A(medium.ContentQuestionId == selectionBeforeCorruption[1],
                    "pool6_corrupt_selection_metadata_continues_selected_medium_ordinal");
                resumed.Abort("pool6_selection_corrupt_cleanup");
            }

            AssertTargetSelectionRepairVariant(root, schemaPath, pack, "missing-key", selected => "{}");
            AssertTargetSelectionRepairVariant(root, schemaPath, pack, "duplicate-ids", selected => Json.Serialize(new Dictionary<string, object>
            {
                { "selected_content_question_ids", new[] { selected[0], selected[0], selected[2] } }
            }));
            AssertTargetSelectionRepairVariant(root, schemaPath, pack, "wrong-bucket", selected => Json.Serialize(new Dictionary<string, object>
            {
                { "selected_content_question_ids", new[] { selected[1], selected[0], selected[2] } }
            }));
            AssertTargetSelectionRepairVariant(root, schemaPath, pack, "unknown-id", selected => Json.Serialize(new Dictionary<string, object>
            {
                { "selected_content_question_ids", new[] { selected[0], selected[1], "m2_q_missing_selected_application_99" } }
            }));
        }

        private static void AssertTargetSelectionRepairVariant(
            string root,
            string schemaPath,
            SyntheticPoolPack pack,
            string caseName,
            Func<IList<string>, string> corruptSelectionJson)
        {
            var safeName = caseName.Replace("-", "_");
            var database = NewDatabase(Path.Combine(root, "pool6-selection-repair-" + safeName + ".db"), schemaPath);
            IList<string> expectedSelected;
            string sessionId;
            using (var first = new MathSessionCoordinator(database, pack.TemplatePath, "LOW", 9511, pack.LessonId))
            {
                var start = first.Start("Bé selected repair " + caseName);
                sessionId = start.SessionId;
                expectedSelected = start.SelectedContentQuestionIds.ToList();
                var basic = first.NextQuestion();
                A(basic.ContentQuestionId == expectedSelected[0],
                    "selection_repair_" + safeName + "_starts_selected_basic");
                var basicOutcome = first.SubmitAnswerAt(basic.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                A(basicOutcome.QuestionCompleted && basicOutcome.IsCorrect && first.Summary.Attempts == 1,
                    "selection_repair_" + safeName + "_commits_one_basic_before_corruption");
                Exec(database,
                    "UPDATE math_session_runtime SET current_selection_json=@selection WHERE session_id=@session;",
                    "@selection", corruptSelectionJson(expectedSelected), "@session", sessionId);
                first.Suspend("selection_repair_" + safeName + "_suspend");
            }

            using (var resumed = new MathSessionCoordinator(database, pack.TemplatePath, "NORMAL", 1234567, pack.LessonId))
            {
                var start = resumed.Start("Bé selected repair " + caseName);
                A(start.ResumedExistingSession && start.CompletedQuestionCount == 1,
                    "selection_repair_" + safeName + "_resumes_committed_progress");
                A(start.SelectedContentQuestionIds.SequenceEqual(expectedSelected),
                    "selection_repair_" + safeName + "_restores_deterministic_selected_set");
                var repairedJson = ScalarText(database,
                    "SELECT current_selection_json FROM math_session_runtime WHERE session_id='" + sessionId + "';");
                A(expectedSelected.All(repairedJson.Contains),
                    "selection_repair_" + safeName + "_rewrites_repaired_checkpoint");
                var medium = resumed.NextQuestion();
                A(medium.ContentQuestionId == expectedSelected[1],
                    "selection_repair_" + safeName + "_continues_selected_medium");
                resumed.Abort("selection_repair_" + safeName + "_cleanup");
            }
        }

        private static void TestTargetedSelectedSetSurvivesCommitFailure(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var lesson = catalog.Lessons.First(x =>
                (x.PrerequisiteSkills == null || x.PrerequisiteSkills.Count == 0) &&
                x.PracticeSets != null && x.PracticeSets.Basic.Count == 2 &&
                x.PracticeSets.Medium.Count == 2 && x.PracticeSets.Application.Count == 2);
            var database = NewDatabase(Path.Combine(root, "targeted-selected-set-commit-failure.db"), schemaPath);

            using (var coordinator = new MathSessionCoordinator(database, templatePath, "LOW", 8451, lesson.Id))
            {
                var start = coordinator.Start("Bé targeted selected-set rollback");
                A(start.TargetQuestionCount == 3 && start.SelectedContentQuestionIds != null && start.SelectedContentQuestionIds.Count == 3,
                    "targeted_commit_failure_fixture_selects_three");
                var selected = start.SelectedContentQuestionIds.ToList();
                var question = coordinator.NextQuestion();
                A(question.ContentQuestionId == selected[0] && lesson.PracticeSets.Basic.Contains(question.ContentQuestionId),
                    "targeted_commit_failure_opens_selected_basic");
                var checkpointBeforeFailure = ScalarText(database,
                    "SELECT current_selection_json FROM math_session_runtime WHERE session_id='" + start.SessionId + "';");
                A(!string.IsNullOrWhiteSpace(checkpointBeforeFailure) && selected.All(checkpointBeforeFailure.Contains),
                    "targeted_commit_failure_checkpoint_contains_selected_set_before_failure");

                Exec(database, @"CREATE TRIGGER smoke_fail_targeted_math_mastery
BEFORE INSERT ON mastery_event
BEGIN
    SELECT RAISE(ABORT, 'injected_targeted_math_commit_failure');
END;");

                var failed = false;
                try
                {
                    coordinator.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 900);
                }
                catch (SQLiteException)
                {
                    failed = true;
                }
                A(failed, "targeted_commit_failure_surfaces_injected_error");
                A(coordinator.HasOpenQuestion && coordinator.Summary.Attempts == 0 && coordinator.Summary.AnswerAttempts == 0,
                    "targeted_commit_failure_keeps_selected_basic_open_and_counters_unadvanced");
                var sameQuestion = coordinator.NextQuestion();
                A(sameQuestion.QuestionId == question.QuestionId && sameQuestion.ContentQuestionId == selected[0],
                    "targeted_commit_failure_keeps_exact_selected_basic_instance");
                var checkpointAfterFailure = ScalarText(database,
                    "SELECT current_selection_json FROM math_session_runtime WHERE session_id='" + start.SessionId + "';");
                A(checkpointAfterFailure == checkpointBeforeFailure && selected.All(checkpointAfterFailure.Contains),
                    "targeted_commit_failure_preserves_selected_set_checkpoint_exactly");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + start.SessionId + "';") == 0 &&
                  Count(database, "SELECT count(*) FROM mastery_event WHERE child_id='" + start.ChildId + "';") == 0,
                    "targeted_commit_failure_writes_no_ghost_learning_state");

                Exec(database, "DROP TRIGGER smoke_fail_targeted_math_mastery;");
                var recovered = coordinator.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 950);
                A(recovered.QuestionCompleted && recovered.IsCorrect && coordinator.Summary.Attempts == 1,
                    "targeted_commit_failure_retry_commits_selected_basic_once");
                var medium = coordinator.NextQuestion();
                A(medium != null && medium.ContentQuestionId == selected[1] && lesson.PracticeSets.Medium.Contains(medium.ContentQuestionId),
                    "targeted_commit_failure_retry_continues_same_selected_medium");
                coordinator.Abort("targeted_selected_set_commit_failure_cleanup");
            }
        }

        private static void TestTargetedConcurrentCoordinatorsCannotDuplicateOrdinal(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var lesson = catalog.Lessons.First(x =>
                (x.PrerequisiteSkills == null || x.PrerequisiteSkills.Count == 0) &&
                x.PracticeSets != null && x.PracticeSets.Basic.Count == 2 &&
                x.PracticeSets.Medium.Count == 2 && x.PracticeSets.Application.Count == 2);
            var database = NewDatabase(Path.Combine(root, "targeted-concurrent-ordinal.db"), schemaPath);

            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 8601, lesson.Id))
            {
                var firstStart = first.Start("Bé targeted concurrent");
                var selected = firstStart.SelectedContentQuestionIds.ToList();
                var firstBasic = first.NextQuestion();

                using (var second = new MathSessionCoordinator(database, templatePath, "NORMAL", 999999, lesson.Id))
                {
                    var secondStart = second.Start("Bé targeted concurrent");
                    A(secondStart.ResumedExistingSession && secondStart.RestoredOpenQuestion &&
                      secondStart.SessionId == firstStart.SessionId,
                        "targeted_concurrent_second_coordinator_resumes_active_session");
                    A(secondStart.SelectedContentQuestionIds.SequenceEqual(selected),
                        "targeted_concurrent_second_coordinator_uses_winner_selected_set");
                    var secondBasic = second.NextQuestion();
                    A(secondBasic.QuestionId == firstBasic.QuestionId && secondBasic.ContentQuestionId == selected[0],
                        "targeted_concurrent_both_hold_same_selected_basic_instance");

                    first.SubmitAnswerAt(firstBasic.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                    var replay = second.SubmitAnswerAt(secondBasic.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 720);
                    A(replay.QuestionCompleted && second.Summary.Attempts == 1,
                        "targeted_concurrent_basic_replay_is_idempotent");

                    var firstMedium = first.NextQuestion();
                    var secondMedium = second.NextQuestion();
                    A(firstMedium.ContentQuestionId == selected[1] && secondMedium.ContentQuestionId == selected[1] &&
                      firstMedium.QuestionId == secondMedium.QuestionId,
                        "targeted_concurrent_same_selected_medium_uses_deterministic_runtime_id");

                    first.SubmitAnswerAt(firstMedium.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 800);
                    var replayMedium = second.SubmitAnswerAt(secondMedium.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 820);
                    A(replayMedium.QuestionCompleted && replayMedium.IsCorrect,
                        "targeted_concurrent_second_medium_submit_replays_same_semantic_attempt");
                    A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + firstStart.SessionId + "';") == 2,
                        "targeted_concurrent_medium_replay_writes_no_duplicate_attempt");
                    A(second.Summary.Attempts == 2 && !second.HasOpenQuestion,
                        "targeted_concurrent_medium_replay_reconciles_to_two_committed_ordinals");

                    var application = second.NextQuestion();
                    A(application != null && application.ContentQuestionId == selected[2] &&
                      lesson.PracticeSets.Application.Contains(application.ContentQuestionId),
                        "targeted_concurrent_reconciled_coordinator_advances_to_selected_application");
                    second.Abort("targeted_concurrent_cleanup");
                }
            }
        }

        private static void TestTargetedConcurrentPendingRetryKeepsFirstTrySemantics(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var lesson = catalog.Lessons.First(x =>
                (x.PrerequisiteSkills == null || x.PrerequisiteSkills.Count == 0) &&
                x.PracticeSets != null && x.PracticeSets.Basic.Count == 2 &&
                x.PracticeSets.Medium.Count == 2 && x.PracticeSets.Application.Count == 2);
            var database = NewDatabase(Path.Combine(root, "targeted-concurrent-pending-retry.db"), schemaPath);

            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 8621, lesson.Id))
            {
                var firstStart = first.Start("Bé targeted pending retry");
                var selected = firstStart.SelectedContentQuestionIds.ToList();
                var firstBasic = first.NextQuestion();

                using (var second = new MathSessionCoordinator(database, templatePath, "NORMAL", 987654, lesson.Id))
                {
                    var secondStart = second.Start("Bé targeted pending retry");
                    var secondBasic = second.NextQuestion();
                    A(secondStart.ResumedExistingSession && secondBasic.QuestionId == firstBasic.QuestionId,
                        "targeted_pending_retry_second_coordinator_resumes_same_basic");

                    first.SubmitAnswerAt(firstBasic.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 650);
                    var basicReplay = second.SubmitAnswerAt(secondBasic.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 670);
                    A(basicReplay.QuestionCompleted && second.Summary.Attempts == 1,
                        "targeted_pending_retry_basic_replay_is_idempotent");

                    var firstMedium = first.NextQuestion();
                    var secondMedium = second.NextQuestion();
                    A(firstMedium.ContentQuestionId == selected[1] && secondMedium.ContentQuestionId == selected[1] &&
                      firstMedium.QuestionId == secondMedium.QuestionId,
                        "targeted_pending_retry_medium_runtime_id_is_shared");
                    var wrong = WrongAnswer(firstMedium);
                    var firstWrong = first.SubmitAnswerWithRetryAt(wrong, 0, "smoke", DateTime.UtcNow, 700);
                    A(firstWrong.CanRetry && !firstWrong.QuestionCompleted && firstWrong.AttemptIndex == 1 &&
                      first.Summary.Attempts == 1,
                        "targeted_pending_retry_first_wrong_stays_pending_without_completed_progress");
                    A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + firstStart.SessionId +
                      "' AND question_id='" + firstMedium.QuestionId + "' AND attempt_index=1;") == 1,
                        "targeted_pending_retry_persists_exactly_one_first_wrong_attempt");
                    A(Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" +
                      firstStart.SessionId + "' AND a.question_id='" + firstMedium.QuestionId + "';") == 0,
                        "targeted_pending_retry_first_wrong_has_no_mastery_finalization");

                    var staleFirstTryRejected = false;
                    try
                    {
                        second.SubmitAnswerAt(secondMedium.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 720);
                    }
                    catch (InvalidOperationException)
                    {
                        staleFirstTryRejected = true;
                    }
                    A(staleFirstTryRejected,
                        "targeted_pending_retry_other_coordinator_cannot_claim_first_try_after_wrong_attempt");
                    A(second.HasOpenQuestion && second.Summary.Attempts == 1,
                        "targeted_pending_retry_conflict_keeps_medium_open_without_progress_inflation");

                    var retryCorrect = second.SubmitRetryAnswerAt(secondMedium.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 760);
                    A(retryCorrect.IsCorrect && retryCorrect.QuestionCompleted && retryCorrect.IsRetry &&
                      retryCorrect.AttemptIndex == 2 && !retryCorrect.IndependentSuccess,
                        "targeted_pending_retry_second_attempt_is_assisted_not_independent");
                    var afterRetry = second.Summary;
                    A(afterRetry.Attempts == 2 && afterRetry.AnswerAttempts == 3 && afterRetry.Correct == 2 &&
                      afterRetry.IndependentCorrect == 1 && afterRetry.RetriedQuestions == 1 && afterRetry.RetriedCorrect == 1,
                        "targeted_pending_retry_summary_preserves_first_wrong_then_assisted_success_semantics");
                    A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + firstStart.SessionId +
                      "' AND question_id='" + firstMedium.QuestionId + "' AND attempt_index IN (1,2);") == 2,
                        "targeted_pending_retry_writes_exactly_two_semantic_attempts_for_medium");
                    A(Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" +
                      firstStart.SessionId + "' AND a.question_id='" + firstMedium.QuestionId + "';") == 1,
                        "targeted_pending_retry_writes_exactly_one_medium_mastery_finalization");

                    var replayFromFirst = first.SubmitRetryAnswerAt(firstMedium.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 780);
                    A(replayFromFirst.QuestionCompleted && replayFromFirst.IsRetry && first.Summary.Attempts == 2 &&
                      first.Summary.AnswerAttempts == 3,
                        "targeted_pending_retry_original_coordinator_replays_final_retry_without_duplication");
                    A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + firstStart.SessionId +
                      "' AND question_id='" + firstMedium.QuestionId + "';") == 2,
                        "targeted_pending_retry_replay_never_creates_third_attempt");

                    var application = second.NextQuestion();
                    A(application != null && application.ContentQuestionId == selected[2],
                        "targeted_pending_retry_advances_to_selected_application");
                    second.Abort("targeted_pending_retry_cleanup");
                }
            }
        }

        private static void TestAdaptiveConcurrentCoordinatorsShareSemanticOrdinalId(
            string root,
            string schemaPath,
            string templatePath)
        {
            var database = NewDatabase(Path.Combine(root, "adaptive-concurrent-semantic-id.db"), schemaPath);
            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 8701, 3))
            {
                var firstStart = first.Start("Bé adaptive concurrent");
                var firstQ1 = first.NextQuestion();
                using (var second = new MathSessionCoordinator(database, templatePath, "NORMAL", 999999, 9))
                {
                    var secondStart = second.Start("Bé adaptive concurrent");
                    var secondQ1 = second.NextQuestion();
                    A(secondStart.ResumedExistingSession && secondStart.SessionId == firstStart.SessionId &&
                      secondQ1.QuestionId == firstQ1.QuestionId,
                        "adaptive_concurrent_second_coordinator_resumes_same_q1");

                    first.SubmitAnswerAt(firstQ1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                    var q1Replay = second.SubmitAnswerAt(secondQ1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 720);
                    A(q1Replay.QuestionCompleted && second.Summary.Attempts == 1,
                        "adaptive_concurrent_q1_replay_is_idempotent");

                    var firstQ2 = first.NextQuestion();
                    var secondQ2 = second.NextQuestion();
                    var firstFingerprint = firstQ2.TemplateId + "|" + firstQ2.PromptVi + "|" + firstQ2.CorrectAnswerDisplay + "|" + string.Join("~", firstQ2.DisplayChoices);
                    var secondFingerprint = secondQ2.TemplateId + "|" + secondQ2.PromptVi + "|" + secondQ2.CorrectAnswerDisplay + "|" + string.Join("~", secondQ2.DisplayChoices);
                    A(firstFingerprint == secondFingerprint && firstQ2.QuestionId == secondQ2.QuestionId,
                        "adaptive_concurrent_same_ordinal_has_same_content_and_semantic_id");

                    first.SubmitAnswerAt(firstQ2.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 800);
                    var q2Replay = second.SubmitAnswerAt(secondQ2.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 820);
                    A(q2Replay.QuestionCompleted && q2Replay.IsCorrect && second.Summary.Attempts == 2,
                        "adaptive_concurrent_q2_submit_replays_same_semantic_attempt");
                    A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + firstStart.SessionId + "';") == 2 &&
                      Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + firstStart.SessionId + "';") == 2,
                        "adaptive_concurrent_q2_replay_writes_no_duplicate_attempt_or_mastery");
                    second.Abort("adaptive_concurrent_cleanup");
                }
            }
        }

        private static void TestTargetedResumeDiscardsStaleConcurrentOrdinalCache(
            string root,
            string schemaPath,
            string templatePath,
            string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var lesson = catalog.Lessons.First(x =>
                (x.PrerequisiteSkills == null || x.PrerequisiteSkills.Count == 0) &&
                x.PracticeSets != null && x.PracticeSets.Basic.Count == 2 &&
                x.PracticeSets.Medium.Count == 2 && x.PracticeSets.Application.Count == 2);
            var database = NewDatabase(Path.Combine(root, "targeted-stale-concurrent-cache.db"), schemaPath);
            IList<string> selected;
            string sessionId;

            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 8611, lesson.Id))
            {
                var firstStart = first.Start("Bé targeted stale cache");
                sessionId = firstStart.SessionId;
                selected = firstStart.SelectedContentQuestionIds.ToList();
                var firstBasic = first.NextQuestion();

                using (var second = new MathSessionCoordinator(database, templatePath, "NORMAL", 123456, lesson.Id))
                {
                    var secondStart = second.Start("Bé targeted stale cache");
                    var secondBasic = second.NextQuestion();
                    A(secondStart.ResumedExistingSession && secondBasic.QuestionId == firstBasic.QuestionId,
                        "targeted_stale_cache_second_coordinator_resumes_basic");

                    first.SubmitAnswerAt(firstBasic.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 700);
                    second.SubmitAnswerAt(secondBasic.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 720);

                    var firstMedium = first.NextQuestion();
                    first.SubmitAnswerAt(firstMedium.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 800);
                    A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + sessionId + "';") == 2,
                        "targeted_stale_cache_two_ordinals_committed_before_stale_open");

                    var staleMedium = second.NextQuestion();
                    A(staleMedium.ContentQuestionId == selected[1] && staleMedium.QuestionId == firstMedium.QuestionId,
                        "targeted_stale_cache_new_runtime_generation_is_deterministic");
                    var legacyStaleQuestionId = selected[1] + "-" + Guid.NewGuid().ToString("N");
                    var legacyStaleJson = Json.Serialize(staleMedium).Replace(staleMedium.QuestionId, legacyStaleQuestionId);
                    Exec(database,
                        "UPDATE math_session_runtime SET current_question_json=@question WHERE session_id=@session;",
                        "@question", legacyStaleJson, "@session", sessionId);
                    var persistedStaleJson = ScalarText(database,
                        "SELECT current_question_json FROM math_session_runtime WHERE session_id='" + sessionId + "';");
                    A(!string.IsNullOrWhiteSpace(persistedStaleJson) && persistedStaleJson.Contains(legacyStaleQuestionId),
                        "targeted_stale_cache_persists_legacy_random_runtime_instance");
                    second.Suspend("targeted_stale_cache_simulate_restart");
                }
                first.Suspend("targeted_stale_cache_first_coordinator_closed");
            }

            using (var resumed = new MathSessionCoordinator(database, templatePath, "NORMAL", 999999, lesson.Id))
            {
                var start = resumed.Start("Bé targeted stale cache");
                A(start.ResumedExistingSession && start.CompletedQuestionCount == 2 && !start.RestoredOpenQuestion,
                    "targeted_stale_cache_resume_discards_already_finalized_medium_cache");
                A(start.SelectedContentQuestionIds.SequenceEqual(selected),
                    "targeted_stale_cache_resume_preserves_selected_set");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + sessionId + "';") == 2,
                    "targeted_stale_cache_resume_keeps_two_durable_attempts");
                var application = resumed.NextQuestion();
                A(application != null && application.ContentQuestionId == selected[2],
                    "targeted_stale_cache_resume_advances_to_selected_application");
                resumed.Abort("targeted_stale_cache_cleanup");
            }
        }

        private static void AssertValidSelectedSet(MathSessionStartResult start, SyntheticPoolPack pack, string prefix)
        {
            A(start.SelectedContentQuestionIds != null && start.SelectedContentQuestionIds.Count == 3 &&
              start.SelectedContentQuestionIds.Distinct(StringComparer.Ordinal).Count() == 3,
                prefix + "_selected_three_unique");
            A(pack.Basic.Contains(start.SelectedContentQuestionIds[0]), prefix + "_selected_basic_from_basic_bucket");
            A(pack.Medium.Contains(start.SelectedContentQuestionIds[1]), prefix + "_selected_medium_from_medium_bucket");
            A(pack.Application.Contains(start.SelectedContentQuestionIds[2]), prefix + "_selected_application_from_application_bucket");
        }

        private static SyntheticPoolPack BuildSyntheticExpandedPoolPack(
            string root,
            string templatePath,
            string lessonCatalogPath,
            string questionBankPath)
        {
            var packDir = Path.Combine(root, "expanded-pool-pack");
            Directory.CreateDirectory(packDir);
            var localTemplate = Path.Combine(packDir, "verified_templates_v1.json");
            var localCatalog = Path.Combine(packDir, "lesson_catalog_v1.json");
            var localBank = Path.Combine(packDir, "question_bank_v1.json");
            File.Copy(templatePath, localTemplate, true);

            var catalogRoot = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(lessonCatalogPath));
            var lessons = ((System.Collections.IEnumerable)catalogRoot["lessons"]).Cast<object>().Cast<Dictionary<string, object>>().ToList();
            var lesson = lessons.First(x =>
            {
                var prerequisites = ((System.Collections.IEnumerable)x["prerequisite_skills"]).Cast<object>().ToList();
                return prerequisites.Count == 0;
            });
            var lessonId = Convert.ToString(lesson["id"], CultureInfo.InvariantCulture);
            var practice = (Dictionary<string, object>)lesson["practice_sets"];
            var basic = ((System.Collections.IEnumerable)practice["basic"]).Cast<object>().Select(x => Convert.ToString(x, CultureInfo.InvariantCulture)).ToList();
            var medium = ((System.Collections.IEnumerable)practice["medium"]).Cast<object>().Select(x => Convert.ToString(x, CultureInfo.InvariantCulture)).ToList();
            var application = ((System.Collections.IEnumerable)practice["application"]).Cast<object>().Select(x => Convert.ToString(x, CultureInfo.InvariantCulture)).ToList();
            A(basic.Count >= 1 && medium.Count >= 1 && application.Count >= 1,
                "pool6_fixture_starts_from_valid_three_difficulty_lesson");

            var bankRoot = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(questionBankPath));
            var questions = ((System.Collections.IEnumerable)bankRoot["questions"]).Cast<object>().Cast<Dictionary<string, object>>().ToList();
            var byId = questions.ToDictionary(x => Convert.ToString(x["id"], CultureInfo.InvariantCulture), StringComparer.Ordinal);
            if (basic.Count < 2) basic.Add(DuplicateSyntheticQuestion(questions, byId[basic[0]], "04", " [biến thể B]"));
            if (medium.Count < 2) medium.Add(DuplicateSyntheticQuestion(questions, byId[medium[0]], "05", " [biến thể B]"));
            if (application.Count < 2) application.Add(DuplicateSyntheticQuestion(questions, byId[application[0]], "06", " [biến thể B]"));
            A(basic.Count >= 2 && medium.Count >= 2 && application.Count >= 2,
                "pool6_fixture_has_at_least_two_questions_per_difficulty");
            practice["basic"] = basic.ToArray();
            practice["medium"] = medium.ToArray();
            practice["application"] = application.ToArray();
            catalogRoot["lessons"] = lessons.Cast<object>().ToArray();
            bankRoot["questions"] = questions.Cast<object>().ToArray();
            File.WriteAllText(localCatalog, Json.Serialize(catalogRoot));
            File.WriteAllText(localBank, Json.Serialize(bankRoot));

            return new SyntheticPoolPack
            {
                TemplatePath = localTemplate,
                LessonId = lessonId,
                Basic = basic,
                Medium = medium,
                Application = application
            };
        }

        private static string DuplicateSyntheticQuestion(
            IList<Dictionary<string, object>> questions,
            Dictionary<string, object> source,
            string ordinal,
            string promptSuffix)
        {
            var clone = Json.Deserialize<Dictionary<string, object>>(Json.Serialize(source));
            var oldId = Convert.ToString(clone["id"], CultureInfo.InvariantCulture);
            var split = oldId.LastIndexOf('_');
            if (split < 0) throw new InvalidDataException("Synthetic authored id has no ordinal: " + oldId);
            var newId = oldId.Substring(0, split + 1) + ordinal;
            clone["id"] = newId;
            clone["prompt_vi"] = Convert.ToString(clone["prompt_vi"], CultureInfo.InvariantCulture) + promptSuffix;
            questions.Add(clone);
            return newId;
        }

        private static void TestRetryAwareAnswerFlow(string root, string schemaPath, string templatePath)
        {
            var database = NewDatabase(Path.Combine(root, "retry-resume.db"), schemaPath);
            string sessionId;
            string childId;
            string questionId;
            string skillId;

            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 8201, 2))
            {
                var start = first.Start("Bé retry");
                sessionId = start.SessionId;
                childId = start.ChildId;
                var question = first.NextQuestion();
                questionId = question.QuestionId;
                skillId = question.SkillId;
                var wrong = WrongAnswer(question);
                var firstWrong = first.SubmitAnswerWithRetryAt(wrong, 0, "smoke", DateTime.UtcNow, 800);
                A(!firstWrong.IsCorrect && !firstWrong.QuestionCompleted && firstWrong.CanRetry && firstWrong.AttemptIndex == 1,
                    "retry_first_wrong_stays_on_same_question");
                A(firstWrong.Mastery == null && firstWrong.Review == null && firstWrong.CompletedQuestionCount == 0,
                    "retry_first_wrong_does_not_update_learning_progress");
                A(first.Summary.Attempts == 0 && first.Summary.AnswerAttempts == 1,
                    "retry_first_wrong_counts_answer_attempt_not_completed_question");
                A(first.NextQuestion().QuestionId == questionId,
                    "retry_first_wrong_next_question_is_same_open_question");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + sessionId + "' AND question_id='" + questionId + "' AND attempt_index=1;") == 1,
                    "retry_first_wrong_persists_attempt_index_one");
                A(Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + sessionId + "';") == 0,
                    "retry_first_wrong_writes_no_mastery_event");
                var duplicateInitialRejected = false;
                try
                {
                    first.SubmitAnswerWithRetryAt(wrong, 0, "smoke", DateTime.UtcNow, 810);
                }
                catch (InvalidOperationException)
                {
                    duplicateInitialRejected = true;
                }
                A(duplicateInitialRejected && Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + sessionId + "' AND question_id='" + questionId + "';") == 1,
                    "retry_duplicate_initial_intent_cannot_consume_attempt_two");
                first.Suspend("retry_resume_test");
            }

            using (var resumed = new MathSessionCoordinator(database, templatePath, "NORMAL", 9999, 9))
            {
                var start = resumed.Start("Bé retry");
                A(start.ResumedExistingSession && start.RestoredOpenQuestion && start.RetryPending && start.CurrentAttemptIndex == 2,
                    "retry_resume_restores_pending_attempt_two");
                A(start.CompletedQuestionCount == 0 && resumed.Summary.Attempts == 0 && resumed.Summary.AnswerAttempts == 1,
                    "retry_resume_reconstructs_answer_attempt_without_completed_progress");
                var question = resumed.NextQuestion();
                A(question.QuestionId == questionId,
                    "retry_resume_keeps_exact_question_id");

                var retryCorrect = resumed.SubmitRetryAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 900);
                A(retryCorrect.IsCorrect && retryCorrect.QuestionCompleted && !retryCorrect.CanRetry && retryCorrect.AttemptIndex == 2 && retryCorrect.IsRetry,
                    "retry_second_attempt_correct_finalizes_question");
                A(!retryCorrect.IndependentSuccess && retryCorrect.Mastery != null && retryCorrect.Review != null,
                    "retry_correct_is_assisted_not_independent");
                A(retryCorrect.Mastery.Reasons != null && retryCorrect.Mastery.Reasons.Contains("retry_assisted_attempt"),
                    "retry_correct_mastery_records_retry_reason");
                var afterRetry = resumed.Summary;
                A(afterRetry.Attempts == 1 && afterRetry.AnswerAttempts == 2 && afterRetry.Correct == 1,
                    "retry_correct_counts_one_completed_question_from_two_answers");
                A(afterRetry.IndependentCorrect == 0 && afterRetry.RetriedQuestions == 1 && afterRetry.RetriedCorrect == 1,
                    "retry_correct_summary_separates_independent_and_retry_success");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + sessionId + "' AND question_id='" + questionId + "' AND attempt_index IN (1,2);") == 2,
                    "retry_correct_persists_two_semantic_attempt_indices");
                A(Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + sessionId + "';") == 1,
                    "retry_correct_writes_exactly_one_mastery_event");
                A(Count(database, "SELECT count(*) FROM child_skill WHERE child_id='" + childId + "' AND skill_id='" + skillId + "' AND independent_success_count=0 AND hinted_success_count=1;") == 1,
                    "retry_correct_updates_assisted_mastery_bucket_once");

                var q2 = resumed.NextQuestion();
                var direct = resumed.SubmitAnswerWithRetryAt(q2.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 950);
                A(direct.QuestionCompleted && direct.AttemptIndex == 1 && direct.IndependentSuccess,
                    "retry_api_first_try_correct_remains_independent");
                A(resumed.Summary.Attempts == 2 && resumed.Summary.AnswerAttempts == 3 && resumed.Summary.IndependentCorrect == 1,
                    "retry_api_mixes_retry_and_first_try_without_progress_inflation");
                var summary = resumed.Complete();
                A(summary.Attempts == 2 && summary.AnswerAttempts == 3 && summary.RetriedCorrect == 1,
                    "retry_completed_session_keeps_question_and_answer_attempt_counters");
            }
        }

        private static void TestRetryWrongFinalizesOnce(string root, string schemaPath, string templatePath)
        {
            var database = NewDatabase(Path.Combine(root, "retry-wrong.db"), schemaPath);
            string sessionId;
            using (var coordinator = new MathSessionCoordinator(database, templatePath, "LOW", 8202, 1))
            {
                var start = coordinator.Start("Bé retry wrong");
                sessionId = start.SessionId;
                var question = coordinator.NextQuestion();
                var wrong = WrongAnswer(question);
                var firstWrong = coordinator.SubmitAnswerWithRetryAt(wrong, 0, "smoke", DateTime.UtcNow, 700);
                A(firstWrong.CanRetry && !firstWrong.QuestionCompleted && coordinator.Summary.Attempts == 0,
                    "retry_wrong_first_attempt_waits_for_retry");

                var retryWrong = coordinator.SubmitRetryAnswerAt(wrong, 0, "smoke", DateTime.UtcNow, 750);
                A(!retryWrong.IsCorrect && retryWrong.QuestionCompleted && !retryWrong.CanRetry && retryWrong.AttemptIndex == 2,
                    "retry_wrong_second_attempt_finalizes_failure");
                A(retryWrong.Mastery != null && retryWrong.Mastery.Delta <= 0 && retryWrong.Review != null,
                    "retry_wrong_applies_one_final_negative_mastery_update");
                var summary = coordinator.Summary;
                A(summary.Attempts == 1 && summary.AnswerAttempts == 2 && summary.Correct == 0 && summary.Wrong == 1 && summary.RetriedQuestions == 1,
                    "retry_wrong_summary_counts_one_failed_question_from_two_answers");
                A(Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + sessionId + "';") == 1,
                    "retry_wrong_writes_exactly_one_mastery_event");
                A(coordinator.NextQuestion() == null,
                    "retry_wrong_finalized_question_consumes_target_once");
                coordinator.Complete();
            }
        }

        private static string WrongAnswer(MathQuestion question)
        {
            if (question == null) throw new ArgumentNullException("question");
            foreach (var candidate in new[] { "__wahu_wrong_answer__", "-999999999", "999999999" })
                if (!question.IsCorrectAnswer(candidate)) return candidate;
            throw new InvalidOperationException("Could not construct a guaranteed wrong Math answer for smoke test.");
        }

        private static void TestStaleCoordinatorCannotAppendAfterTerminalSession(string root, string schemaPath, string templatePath)
        {
            var database = NewDatabase(Path.Combine(root, "stale-terminal-coordinator.db"), schemaPath);
            string sessionId;
            MathQuestion staleSecondQuestion;

            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 8301, 2))
            {
                var started = first.Start("Bé stale terminal");
                sessionId = started.SessionId;
                var q1 = first.NextQuestion();

                using (var second = new MathSessionCoordinator(database, templatePath, "LOW", 9999, 9))
                {
                    var resumed = second.Start("Bé stale terminal");
                    A(resumed.ResumedExistingSession && resumed.RestoredOpenQuestion && resumed.SessionId == sessionId,
                        "stale_terminal_second_coordinator_resumes_same_open_question");
                    var q1b = second.NextQuestion();
                    A(q1b.QuestionId == q1.QuestionId,
                        "stale_terminal_both_coordinators_hold_same_first_question");

                    first.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 800);
                    staleSecondQuestion = first.NextQuestion();
                    A(staleSecondQuestion != null && staleSecondQuestion.QuestionId != q1.QuestionId,
                        "stale_terminal_first_coordinator_advances_to_second_question");

                    second.SubmitAnswerAt(q1b.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 850);
                    A(second.Summary.Attempts == 1,
                        "stale_terminal_second_coordinator_replays_committed_first_question");
                    second.Complete();
                }

                A(Count(database, "SELECT count(*) FROM session WHERE id='" + sessionId + "' AND state='completed' AND ended_at_utc IS NOT NULL;") == 1,
                    "stale_terminal_session_is_durably_completed_by_second_coordinator");
                var rejected = false;
                try
                {
                    first.SubmitAnswerAt(staleSecondQuestion.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 900);
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }
                A(rejected,
                    "stale_terminal_first_coordinator_cannot_append_new_attempt_after_completion");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + sessionId + "';") == 1,
                    "stale_terminal_rejected_submit_keeps_attempt_count_one");
                A(Count(database, "SELECT count(*) FROM attempt_commit_key WHERE session_id='" + sessionId + "' AND question_id='" + staleSecondQuestion.QuestionId + "';") == 0,
                    "stale_terminal_rejected_submit_writes_no_semantic_key");
                A(Count(database, "SELECT count(*) FROM mastery_event m JOIN attempt a ON a.id=m.attempt_id WHERE a.session_id='" + sessionId + "';") == 1,
                    "stale_terminal_rejected_submit_writes_no_extra_mastery");
            }
        }

        private static void TestCoordinatorRejectsStaleSkillSnapshot(string root, string schemaPath, string templatePath)
        {
            var database = NewDatabase(Path.Combine(root, "stale-skill-snapshot.db"), schemaPath);
            using (var coordinator = new MathSessionCoordinator(database, templatePath, "LOW", 8302, 1))
            {
                var started = coordinator.Start("Bé stale skill");
                var question = coordinator.NextQuestion();
                var now = DateTime.UtcNow;
                Exec(database, @"INSERT INTO child_skill(
child_id,skill_id,subject,mastery_score,confidence,attempts_count,
independent_success_count,hinted_success_count,transfer_success_count,last_seen_at_utc,
last_success_at_utc,next_review_at_utc,learning_state,mastery_engine_version,updated_at_utc)
VALUES(@child,@skill,'math',0.61,0.70,3,2,0,0,@now,@now,@due,'LEARNING','mastery-v1',@now);",
                    "@child", started.ChildId,
                    "@skill", question.SkillId,
                    "@now", now.ToString("o", CultureInfo.InvariantCulture),
                    "@due", now.AddDays(1).ToString("o", CultureInfo.InvariantCulture));
                A(Count(database, "SELECT count(*) FROM child_skill WHERE child_id='" + started.ChildId + "' AND skill_id='" + question.SkillId + "' AND attempts_count=3;") == 1,
                    "stale_skill_fixture_updates_skill_after_question_open");

                var rejected = false;
                try
                {
                    coordinator.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 900);
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }
                A(rejected,
                    "stale_skill_coordinator_rejects_answer_from_outdated_skill_snapshot");
                A(coordinator.Summary.Attempts == 0,
                    "stale_skill_rejection_does_not_advance_completed_question_count");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + started.SessionId + "';") == 0,
                    "stale_skill_rejection_writes_no_attempt");
                A(Count(database, "SELECT count(*) FROM attempt_commit_key WHERE session_id='" + started.SessionId + "';") == 0,
                    "stale_skill_rejection_writes_no_semantic_key");
                A(Count(database, "SELECT count(*) FROM mastery_event WHERE child_id='" + started.ChildId + "' AND skill_id='" + question.SkillId + "';") == 0,
                    "stale_skill_rejection_writes_no_mastery_event");
                A(Count(database, "SELECT count(*) FROM child_skill WHERE child_id='" + started.ChildId + "' AND skill_id='" + question.SkillId + "' AND attempts_count=3 AND abs(mastery_score-0.61)<0.0000001;") == 1,
                    "stale_skill_rejection_preserves_newer_skill_state");
            }
        }

        private static void TestCommitFailureRollsBackAndRestoresBehavior(string root, string schemaPath, string templatePath)
        {
            var database = NewDatabase(Path.Combine(root, "commit-failure-rollback.db"), schemaPath);
            using (var coordinator = new MathSessionCoordinator(database, templatePath, "LOW", 8303, 1))
            {
                var started = coordinator.Start("Bé rollback");
                var question = coordinator.NextQuestion();
                Exec(database, @"CREATE TRIGGER smoke_fail_math_mastery
BEFORE INSERT ON mastery_event
BEGIN
    SELECT RAISE(ABORT, 'injected_math_commit_failure');
END;");

                var failed = false;
                try
                {
                    coordinator.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 900);
                }
                catch (SQLiteException)
                {
                    failed = true;
                }
                A(failed, "commit_failure_injected_mastery_write_surfaces_error");
                A(coordinator.HasOpenQuestion && coordinator.Summary.Attempts == 0 && coordinator.Summary.AnswerAttempts == 0,
                    "commit_failure_keeps_question_open_and_counters_unadvanced");
                A(coordinator.NextQuestion().QuestionId == question.QuestionId,
                    "commit_failure_keeps_same_open_question_for_retry");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + started.SessionId + "';") == 0,
                    "commit_failure_rolls_back_attempt");
                A(Count(database, "SELECT count(*) FROM attempt_commit_key WHERE session_id='" + started.SessionId + "';") == 0,
                    "commit_failure_rolls_back_semantic_key");
                A(Count(database, "SELECT count(*) FROM mastery_event WHERE child_id='" + started.ChildId + "' AND skill_id='" + question.SkillId + "';") == 0,
                    "commit_failure_rolls_back_mastery_event");
                A(Count(database, "SELECT count(*) FROM child_skill WHERE child_id='" + started.ChildId + "' AND skill_id='" + question.SkillId + "';") == 0,
                    "commit_failure_rolls_back_child_skill");
                A(Count(database, "SELECT count(*) FROM review_schedule WHERE child_id='" + started.ChildId + "' AND skill_id='" + question.SkillId + "';") == 0,
                    "commit_failure_rolls_back_review_schedule");
                A(Count(database, "SELECT count(*) FROM behavior_state_event WHERE session_id='" + started.SessionId + "';") == 0,
                    "commit_failure_writes_no_behavior_audit");

                Exec(database, "DROP TRIGGER smoke_fail_math_mastery;");
                var recovered = coordinator.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 950);
                A(recovered.QuestionCompleted && recovered.IsCorrect && recovered.Behavior != null && recovered.Behavior.RecentAttemptCount == 1,
                    "commit_failure_retry_commits_once_without_ghost_behavior_observation");
                A(coordinator.Summary.Attempts == 1 && coordinator.Summary.AnswerAttempts == 1,
                    "commit_failure_retry_advances_counters_once");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + started.SessionId + "';") == 1 &&
                  Count(database, "SELECT count(*) FROM attempt_commit_key WHERE session_id='" + started.SessionId + "';") == 1 &&
                  Count(database, "SELECT count(*) FROM mastery_event WHERE child_id='" + started.ChildId + "' AND skill_id='" + question.SkillId + "';") == 1,
                    "commit_failure_retry_writes_one_durable_learning_chain");
                coordinator.Complete();
            }
        }

        private static void TestLateReviewFailureRollsBackEntireLearningChain(string root, string schemaPath, string templatePath)
        {
            var database = NewDatabase(Path.Combine(root, "late-review-failure-rollback.db"), schemaPath);
            using (var coordinator = new MathSessionCoordinator(database, templatePath, "LOW", 8310, 1))
            {
                var started = coordinator.Start("Bé late review rollback");
                var question = coordinator.NextQuestion();
                Exec(database, @"CREATE TRIGGER smoke_fail_math_review
BEFORE INSERT ON review_schedule
BEGIN
    SELECT RAISE(ABORT, 'injected_math_review_failure');
END;");

                var failed = false;
                try
                {
                    coordinator.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 900);
                }
                catch (SQLiteException)
                {
                    failed = true;
                }
                A(failed, "late_review_failure_surfaces_injected_error");
                A(coordinator.HasOpenQuestion && coordinator.Summary.Attempts == 0 && coordinator.Summary.AnswerAttempts == 0,
                    "late_review_failure_keeps_question_open_and_counters_unadvanced");
                A(coordinator.NextQuestion().QuestionId == question.QuestionId,
                    "late_review_failure_keeps_exact_open_question_for_retry");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + started.SessionId + "';") == 0,
                    "late_review_failure_rolls_back_attempt");
                A(Count(database, "SELECT count(*) FROM attempt_commit_key WHERE session_id='" + started.SessionId + "';") == 0,
                    "late_review_failure_rolls_back_semantic_key");
                A(Count(database, "SELECT count(*) FROM mastery_event WHERE child_id='" + started.ChildId + "' AND skill_id='" + question.SkillId + "';") == 0,
                    "late_review_failure_rolls_back_mastery_event");
                A(Count(database, "SELECT count(*) FROM child_skill WHERE child_id='" + started.ChildId + "' AND skill_id='" + question.SkillId + "';") == 0,
                    "late_review_failure_rolls_back_child_skill");
                A(Count(database, "SELECT count(*) FROM review_schedule WHERE child_id='" + started.ChildId + "' AND skill_id='" + question.SkillId + "';") == 0,
                    "late_review_failure_leaves_no_review_row");

                Exec(database, "DROP TRIGGER smoke_fail_math_review;");
                var recovered = coordinator.SubmitAnswerAt(question.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 950);
                A(recovered.QuestionCompleted && recovered.IsCorrect && coordinator.Summary.Attempts == 1 && coordinator.Summary.AnswerAttempts == 1,
                    "late_review_failure_retry_commits_once");
                A(Count(database, "SELECT count(*) FROM attempt WHERE session_id='" + started.SessionId + "';") == 1 &&
                  Count(database, "SELECT count(*) FROM mastery_event WHERE child_id='" + started.ChildId + "' AND skill_id='" + question.SkillId + "';") == 1 &&
                  Count(database, "SELECT count(*) FROM child_skill WHERE child_id='" + started.ChildId + "' AND skill_id='" + question.SkillId + "';") == 1 &&
                  Count(database, "SELECT count(*) FROM review_schedule WHERE child_id='" + started.ChildId + "' AND skill_id='" + question.SkillId + "';") == 1,
                    "late_review_failure_retry_writes_exactly_one_complete_learning_chain");
                coordinator.Complete();
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
            string expectedSecondQuestionId;
            string expectedSecondFingerprint;
            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 314159, 3))
            {
                var start = first.Start("Bé corrupt");
                sessionId = start.SessionId;
                var q1 = first.NextQuestion();
                first.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 1000);
                var q2 = first.NextQuestion();
                expectedSecondQuestionId = q2.QuestionId;
                expectedSecondFingerprint = q2.TemplateId + "|" + q2.PromptVi + "|" + q2.CorrectAnswerDisplay + "|" + string.Join("~", q2.DisplayChoices);
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
                A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + sessionId +
                    "' AND generated_question_count=1 AND current_question_json IS NULL;") == 1,
                    "corrupt_adaptive_cache_rolls_cursor_back_to_committed_ordinal");
                var replacement = resumed.NextQuestion();
                var replacementFingerprint = replacement.TemplateId + "|" + replacement.PromptVi + "|" + replacement.CorrectAnswerDisplay + "|" + string.Join("~", replacement.DisplayChoices);
                A(replacement != null && replacementFingerprint == expectedSecondFingerprint,
                    "corrupt_adaptive_cache_regenerates_same_second_question_not_third");
                A(replacement.QuestionId == expectedSecondQuestionId,
                    "corrupt_adaptive_cache_regenerates_same_semantic_question_id");
                resumed.Abort("cleanup");
            }
        }

        private static void TestAdaptiveTamperedOpenQuestionIsDiscarded(string root, string schemaPath, string templatePath)
        {
            var database = NewDatabase(Path.Combine(root, "adaptive-tampered-cache.db"), schemaPath);
            string sessionId;
            string canonicalFingerprint;
            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 271828, 3))
            {
                var start = first.Start("Bé adaptive tampered cache");
                sessionId = start.SessionId;
                var q1 = first.NextQuestion();
                canonicalFingerprint = q1.TemplateId + "|" + q1.PromptVi + "|" + q1.CorrectAnswerDisplay + "|" + string.Join("~", q1.DisplayChoices);
                first.Suspend("adaptive_tampered_cache_fixture");
            }

            var cachedJson = ScalarText(database,
                "SELECT current_question_json FROM math_session_runtime WHERE session_id='" + sessionId + "';");
            var tampered = Json.Deserialize<MathQuestion>(cachedJson);
            tampered.PromptVi = tampered.PromptVi + " [TAMPERED]";
            tampered.CorrectAnswer = tampered.CorrectAnswer + 111;
            tampered.CorrectAnswerText = "__adaptive_tampered__";
            tampered.AcceptedAnswers = new[] { "__adaptive_tampered__" };
            Exec(database,
                "UPDATE math_session_runtime SET current_question_json=@question WHERE session_id=@session;",
                "@question", Json.Serialize(tampered), "@session", sessionId);

            using (var resumed = new MathSessionCoordinator(database, templatePath, "NORMAL", 999999, 9))
            {
                var start = resumed.Start("Bé adaptive tampered cache");
                A(start.ResumedExistingSession && start.CompletedQuestionCount == 0 &&
                  start.DiscardedCorruptOpenQuestion && !start.RestoredOpenQuestion,
                    "adaptive_tampered_cache_is_discarded_not_trusted");
                A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + sessionId +
                    "' AND generated_question_count=0 AND current_question_json IS NULL;") == 1,
                    "adaptive_tampered_cache_rewinds_generated_cursor_to_zero");
                var regenerated = resumed.NextQuestion();
                var regeneratedFingerprint = regenerated.TemplateId + "|" + regenerated.PromptVi + "|" + regenerated.CorrectAnswerDisplay + "|" + string.Join("~", regenerated.DisplayChoices);
                A(regeneratedFingerprint == canonicalFingerprint,
                    "adaptive_tampered_cache_regenerates_canonical_first_question");
                resumed.Abort("adaptive_tampered_cache_cleanup");
            }

            var selectionDatabase = NewDatabase(Path.Combine(root, "adaptive-tampered-selection.db"), schemaPath);
            string selectionSessionId;
            string selectionCanonicalFingerprint;
            using (var first = new MathSessionCoordinator(selectionDatabase, templatePath, "LOW", 271829, 3))
            {
                var start = first.Start("Bé adaptive tampered selection");
                selectionSessionId = start.SessionId;
                var q1 = first.NextQuestion();
                selectionCanonicalFingerprint = q1.TemplateId + "|" + q1.PromptVi + "|" + q1.CorrectAnswerDisplay + "|" + string.Join("~", q1.DisplayChoices);
                first.Suspend("adaptive_tampered_selection_fixture");
            }
            var selectionJson = ScalarText(selectionDatabase,
                "SELECT current_selection_json FROM math_session_runtime WHERE session_id='" + selectionSessionId + "';");
            var selectionData = Json.Deserialize<Dictionary<string, object>>(selectionJson);
            selectionData["template_id"] = "__tampered_template__";
            selectionData["skill_id"] = "__tampered_skill__";
            Exec(selectionDatabase,
                "UPDATE math_session_runtime SET current_selection_json=@selection WHERE session_id=@session;",
                "@selection", Json.Serialize(selectionData), "@session", selectionSessionId);

            using (var resumed = new MathSessionCoordinator(selectionDatabase, templatePath, "NORMAL", 999999, 9))
            {
                var start = resumed.Start("Bé adaptive tampered selection");
                A(start.ResumedExistingSession && start.DiscardedCorruptOpenQuestion && !start.RestoredOpenQuestion &&
                  start.CompletedQuestionCount == 0,
                    "adaptive_tampered_selection_metadata_is_discarded");
                A(Count(selectionDatabase, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + selectionSessionId +
                    "' AND generated_question_count=0 AND current_question_json IS NULL;") == 1,
                    "adaptive_tampered_selection_rewinds_generated_cursor");
                var regenerated = resumed.NextQuestion();
                var fingerprint = regenerated.TemplateId + "|" + regenerated.PromptVi + "|" + regenerated.CorrectAnswerDisplay + "|" + string.Join("~", regenerated.DisplayChoices);
                A(fingerprint == selectionCanonicalFingerprint,
                    "adaptive_tampered_selection_regenerates_canonical_question");
                resumed.Abort("adaptive_tampered_selection_cleanup");
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
            A(init.SchemaVersion == 5 && init.Health.IsHealthy, "database_ready_v5_" + Path.GetFileNameWithoutExtension(dbPath));
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

        private static string ScalarText(LearningDatabase database, string sql)
        {
            using (var c = database.OpenConnection())
            using (var command = c.CreateCommand())
            {
                command.CommandText = sql;
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
