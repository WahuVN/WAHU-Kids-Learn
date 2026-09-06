using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
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
                TestAuthoredQuestionBank(questionBankPath);
                TestTargetedLessonUnlockAndResume(root, schemaPath, templatePath, lessonCatalogPath);
                TestTargetedCorruptOpenQuestionReplaysAuthoredOrdinal(root, schemaPath, templatePath, lessonCatalogPath);
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

        private static void TestAuthoredQuestionBank(string questionBankPath)
        {
            var bank = new MathAuthoredQuestionSource().Load(questionBankPath);
            A(bank != null && bank.Questions != null && bank.Questions.Count == 201, "authored_bank_loads_all_201_questions");
            A(bank.Questions.Select(x => x.ContentQuestionId).Distinct(StringComparer.Ordinal).Count() == 201,
                "authored_bank_content_ids_unique");
            var lessonGroups = bank.Questions.GroupBy(x => x.LessonId, StringComparer.Ordinal).ToList();
            A(lessonGroups.Count == 67, "authored_bank_covers_67_lessons");
            A(lessonGroups.All(x => x.Count() == 3), "authored_bank_each_lesson_has_three_questions");
            A(bank.Questions.All(x => !string.IsNullOrWhiteSpace(x.ContentQuestionId) && !string.IsNullOrWhiteSpace(x.LessonId) &&
                                      !string.IsNullOrWhiteSpace(x.QuestionType) && !string.IsNullOrWhiteSpace(x.Difficulty)),
                "authored_bank_maps_traceability_metadata");
            A(bank.Questions.All(x => x.IsCorrectAnswer(x.CorrectAnswerText)), "authored_bank_every_expected_answer_validates");

            var expression = bank.Questions.Single(x => x.AnswerKind == "expression");
            A(expression.QuestionType == "expression_input" && expression.DisplayChoices.Count == 0,
                "authored_expression_stays_input_without_fake_choices");
            A(expression.IsCorrectAnswer("75"), "authored_expression_accepts_equivalent_numeric_result");

            var unit = bank.Questions.Single(x => x.AnswerKind == "unit");
            A(unit.QuestionType == "unit_input" && unit.DisplayChoices.Count == 0 && unit.ExpectedUnit == "kg",
                "authored_unit_maps_expected_unit_without_fake_choices");
            A(unit.IsCorrectAnswer("5 kilôgam"), "authored_unit_accepts_declared_alias");

            var interaction = bank.Questions.Single(x => x.AnswerKind == "interaction_integer");
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
            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 7002, prerequisite.Id))
            {
                var started = first.Start("Bé targeted");
                sessionId = started.SessionId;
                A(started.SessionMode == "lesson" && started.TargetLessonId == prerequisite.Id,
                    "targeted_start_publishes_lesson_identity");
                A(started.TargetQuestionCount == prerequisite.PracticeSets.TotalCount,
                    "targeted_start_uses_catalog_practice_count");
                A(started.LessonAccess != null && started.LessonAccess.IsUnlocked,
                    "targeted_start_publishes_access_snapshot");

                var q1 = first.NextQuestion();
                A(q1.LessonId == prerequisite.Id && q1.SkillId == prerequisite.SkillId,
                    "targeted_question_matches_selected_lesson_skill");
                A(q1.ContentQuestionId == prerequisite.PracticeSets.Basic[0],
                    "targeted_first_question_uses_basic_practice_id");
                first.SubmitAnswerAt(q1.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 900);

                var q2 = first.NextQuestion();
                openQuestionId = q2.QuestionId;
                A(q2.ContentQuestionId == prerequisite.PracticeSets.Medium[0],
                    "targeted_second_question_uses_medium_practice_id");
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
                A(q2.QuestionId == openQuestionId && q2.ContentQuestionId == prerequisite.PracticeSets.Medium[0],
                    "targeted_resume_returns_exact_open_authored_question");
                resumed.SubmitAnswerAt(q2.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 950);

                var q3 = resumed.NextQuestion();
                A(q3.ContentQuestionId == prerequisite.PracticeSets.Application[0],
                    "targeted_third_question_uses_application_practice_id");
                resumed.SubmitAnswerAt(q3.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 1000);
                A(resumed.NextQuestion() == null, "targeted_session_stops_after_catalog_practice_set");

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

        private static void TestTargetedCorruptOpenQuestionReplaysAuthoredOrdinal(string root, string schemaPath, string templatePath, string lessonCatalogPath)
        {
            var catalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var lesson = catalog.Lessons.First(x => (x.PrerequisiteSkills == null || x.PrerequisiteSkills.Count == 0) &&
                                                     x.PracticeSets != null && x.PracticeSets.TotalCount == 3);
            var database = NewDatabase(Path.Combine(root, "targeted-corrupt-cache.db"), schemaPath);
            string sessionId;

            using (var first = new MathSessionCoordinator(database, templatePath, "LOW", 8101, lesson.Id))
            {
                var start = first.Start("Bé targeted corrupt");
                sessionId = start.SessionId;
                var basic = first.NextQuestion();
                A(basic.ContentQuestionId == lesson.PracticeSets.Basic[0],
                    "targeted_corrupt_fixture_starts_with_basic");
                first.SubmitAnswerAt(basic.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 800);

                var medium = first.NextQuestion();
                A(medium.ContentQuestionId == lesson.PracticeSets.Medium[0],
                    "targeted_corrupt_fixture_opens_medium");
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
                A(Count(database, "SELECT count(*) FROM math_session_runtime WHERE session_id='" + sessionId +
                    "' AND generated_question_count=1 AND current_question_json IS NULL;") == 1,
                    "targeted_corrupt_resume_rolls_cursor_back_to_committed_ordinal");

                var medium = resumed.NextQuestion();
                A(medium != null && medium.ContentQuestionId == lesson.PracticeSets.Medium[0],
                    "targeted_corrupt_resume_replays_medium_not_application");
                resumed.SubmitAnswerAt(medium.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 850);

                var application = resumed.NextQuestion();
                A(application != null && application.ContentQuestionId == lesson.PracticeSets.Application[0],
                    "targeted_corrupt_resume_then_serves_application");
                resumed.SubmitAnswerAt(application.CorrectAnswerDisplay, 0, "smoke", DateTime.UtcNow, 900);
                A(resumed.Summary.Attempts == 3 && resumed.NextQuestion() == null,
                    "targeted_corrupt_resume_requires_all_three_authored_attempts");

                var summary = resumed.Complete();
                A(summary.LessonCompleted && summary.Attempts == 3 && summary.TargetLessonId == lesson.Id,
                    "targeted_corrupt_resume_completes_only_after_full_authored_set");
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
            A(init.SchemaVersion == 4 && init.Health.IsHealthy, "database_ready_v4_" + Path.GetFileNameWithoutExtension(dbPath));
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
