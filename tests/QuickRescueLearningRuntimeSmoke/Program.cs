using System;
using System.Collections.Generic;
using System.IO;
using WAHU.Content;
using WAHU.Learning;

namespace WAHU.QuickRescueLearningRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        private static void Main()
        {
            var root = FindProjectRoot();
            var packDirectory = Path.Combine(root, "content_packs", "math_quick_rescue_v1");
            var path = Path.Combine(packDirectory, "learning_content_v1.json");
            var validation = new ContentPackValidator().ValidateDirectory(packDirectory, true);
            Assert(validation.IsValid && validation.ChildRuntimeAllowed, "verified_content_pack_manifest");
            var source = new MathQuickRescueContentSource();
            var pack = source.Load(path);
            var selector = new MathQuickRescueAdaptiveSelector();

            Assert(pack.CheckpointCount == 3, "three_checkpoints");
            Assert(pack.GetCheckpoint(1).Difficulty == "basic", "checkpoint1_basic");
            Assert(pack.GetCheckpoint(2).Difficulty == "medium", "checkpoint2_medium");
            Assert(pack.GetCheckpoint(3).Difficulty == "application", "checkpoint3_application");

            TestAuthoredQuestionCrossValidation(source, path, pack);
            TestMalformedPackFailsClosed(source, path);

            var ready = selector.Select(pack, 1, Decision(BehaviorState.READY), new string[0], 42);
            Assert(!string.IsNullOrWhiteSpace(ready.QuestionId), "ready_selects_question");
            Assert(ready.Difficulty == "basic", "first_question_confidence_basic");
            Assert(ready.RecommendedHintLevel == 0 && !ready.UseRepair && !ready.OfferBreak, "ready_no_forced_support");

            var readyAgain = selector.Select(pack, 1, Decision(BehaviorState.READY), new string[0], 42);
            Assert(readyAgain.QuestionId == ready.QuestionId, "same_seed_is_deterministic");

            var alternate = selector.Select(pack, 1, Decision(BehaviorState.READY), new[] { ready.QuestionId }, 42);
            Assert(alternate.QuestionId != ready.QuestionId, "recent_question_is_avoided");
            Assert(alternate.Difficulty == "basic", "anti_repeat_keeps_checkpoint_difficulty");

            var bored = selector.Select(pack, 2, Decision(BehaviorState.BORED_OR_UNDERCHALLENGED), new string[0], 9);
            Assert(bored.Variant == "transfer", "bored_prefers_transfer_variant");
            Assert(bored.RecommendedHintLevel == 0, "bored_does_not_add_scaffold");

            var strained = selector.Select(pack, 2, Decision(BehaviorState.STRAINED), new string[0], 9);
            Assert(strained.Variant == "support", "strained_prefers_support_variant");
            Assert(strained.RecommendedHintLevel == 1 && !strained.UseRepair, "strained_small_hint_only");
            Assert(strained.SupportVi == pack.GetCheckpoint(2).HintLevel1Vi, "strained_uses_checkpoint_hint1");

            var earlyRepair = selector.Select(pack, 2, RepairDecision(BehaviorState.STRAINED), new string[0], 9);
            Assert(earlyRepair.UseRepair && earlyRepair.RecommendedHintLevel == 2, "explicit_behavior_repair_escalates_before_frustrated_state");
            Assert(earlyRepair.SupportVi == pack.GetCheckpoint(2).RepairVi, "explicit_behavior_repair_uses_checkpoint_repair_copy");
            Assert(earlyRepair.Reason.Contains("explicit_repair"), "explicit_behavior_repair_is_auditable");

            var frustrated = selector.Select(pack, 3, Decision(BehaviorState.FRUSTRATED_LIKELY), new string[0], 9);
            Assert(frustrated.Variant == "support", "frustrated_prefers_support_variant");
            Assert(frustrated.RecommendedHintLevel == 2 && frustrated.UseRepair, "frustrated_uses_repair");
            Assert(frustrated.SupportVi == pack.GetCheckpoint(3).RepairVi, "frustrated_uses_checkpoint_repair_copy");

            var flow = selector.Select(pack, 2, Decision(BehaviorState.FLOW_LIKELY), new string[0], 9);
            Assert(flow.MinimalFeedback && !flow.UseRepair, "flow_minimizes_interruptions");

            var fatigued = selector.Select(pack, 3, RepairDecision(BehaviorState.FATIGUED_LIKELY), new string[0], 9);
            Assert(fatigued.OfferBreak, "fatigue_offers_break");
            Assert(string.IsNullOrWhiteSpace(fatigued.QuestionId), "fatigue_does_not_push_next_question");
            Assert(!fatigued.UseRepair && fatigued.RecommendedHintLevel == 0, "fatigue_overrides_explicit_repair_signal");
            Assert(fatigued.SupportVi == pack.Feedback.BreakVi, "fatigue_uses_safe_break_copy");

            TestHardAntiRepeatBeforeAdaptivePreference(pack, selector);
            TestCommonErrorSupport(pack, selector);

            var allRecent = new List<string>();
            foreach (var option in pack.GetCheckpoint(2).QuestionOptions) allRecent.Add(option.QuestionId);
            var fallbackA = selector.Select(pack, 2, Decision(BehaviorState.READY), allRecent, 17);
            var fallbackB = selector.Select(pack, 2, Decision(BehaviorState.READY), allRecent, 17);
            Assert(fallbackA.QuestionId == fallbackB.QuestionId, "repeat_fallback_is_deterministic");
            Assert(pack.GetCheckpoint(2).QuestionOptions.Count == 2, "checkpoint_has_variety_for_repeat_control");

            Console.WriteLine("QUICK_RESCUE_LEARNING_RUNTIME_SMOKE_PASS assertions=" + _assertions);
        }

        private static void TestAuthoredQuestionCrossValidation(MathQuickRescueContentSource source, string path, MathQuickRescueLearningPack pack)
        {
            var authored = new List<MathQuestion>();
            foreach (var checkpoint in pack.Checkpoints)
            {
                foreach (var option in checkpoint.QuestionOptions)
                {
                    authored.Add(new MathQuestion
                    {
                        ContentQuestionId = option.QuestionId,
                        LessonId = pack.TargetLessonId,
                        SkillId = pack.TargetSkillId,
                        Difficulty = checkpoint.Difficulty
                    });
                }
            }

            MathQuickRescueContentSource.ValidateAgainstAuthoredQuestions(pack, authored);
            Assert(true, "authored_reference_cross_validation_passes");
            var loadedWithRefs = source.Load(path, authored);
            Assert(loadedWithRefs.CheckpointCount == pack.CheckpointCount, "load_with_authored_validation_passes");

            var missing = new List<MathQuestion>(authored);
            missing.RemoveAt(missing.Count - 1);
            AssertInvalidData(delegate { MathQuickRescueContentSource.ValidateAgainstAuthoredQuestions(pack, missing); }, "missing_authored_question_rejected");

            var wrongDifficulty = CloneQuestions(authored);
            wrongDifficulty[0].Difficulty = "application";
            AssertInvalidData(delegate { MathQuickRescueContentSource.ValidateAgainstAuthoredQuestions(pack, wrongDifficulty); }, "authored_difficulty_mismatch_rejected");

            var wrongLesson = CloneQuestions(authored);
            wrongLesson[0].LessonId = "other_lesson";
            AssertInvalidData(delegate { MathQuickRescueContentSource.ValidateAgainstAuthoredQuestions(pack, wrongLesson); }, "authored_lesson_mismatch_rejected");
        }

        private static void TestMalformedPackFailsClosed(MathQuickRescueContentSource source, string productionPath)
        {
            var original = File.ReadAllText(productionPath);
            var root = Path.Combine(Path.GetTempPath(), "wahu-rescue-content-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var badVariant = Path.Combine(root, "bad-variant.json");
                File.WriteAllText(badVariant, original.Replace("\"variant\": \"support\"", "\"variant\": \"typo\""));
                AssertInvalidData(delegate { source.Load(badVariant); }, "unsupported_question_variant_rejected");

                var badAdaptive = Path.Combine(root, "bad-adaptive.json");
                File.WriteAllText(badAdaptive, original.Replace(
                    "\"state\": \"STRAINED\",\n      \"preferred_variant\": \"support\",\n      \"hint_level\": 1",
                    "\"state\": \"STRAINED\",\n      \"preferred_variant\": \"support\",\n      \"hint_level\": 0"));
                AssertInvalidData(delegate { source.Load(badAdaptive); }, "strained_zero_hint_rule_rejected");

                var duplicateError = Path.Combine(root, "duplicate-error.json");
                File.WriteAllText(duplicateError, original.Replace("\"id\": \"zero_place_omitted\"", "\"id\": \"place_value_shift\""));
                AssertInvalidData(delegate { source.Load(duplicateError); }, "duplicate_common_error_id_rejected");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void TestHardAntiRepeatBeforeAdaptivePreference(MathQuickRescueLearningPack pack, MathQuickRescueAdaptiveSelector selector)
        {
            var checkpoint = pack.GetCheckpoint(2);
            var support = FindVariant(checkpoint, "support");
            var transfer = FindVariant(checkpoint, "transfer");

            var strainedLru = selector.Select(pack, 2, Decision(BehaviorState.STRAINED), new[] { transfer.QuestionId, support.QuestionId }, 99);
            Assert(strainedLru.QuestionId == transfer.QuestionId, "anti_repeat_beats_recent_support_preference");
            Assert(strainedLru.RecommendedHintLevel == 1, "lru_transfer_still_gets_strained_support_hint");

            var boredLru = selector.Select(pack, 2, Decision(BehaviorState.BORED_OR_UNDERCHALLENGED), new[] { support.QuestionId, transfer.QuestionId }, 99);
            Assert(boredLru.QuestionId == support.QuestionId, "anti_repeat_beats_recent_transfer_preference");
            Assert(boredLru.RecommendedHintLevel == 0, "lru_support_does_not_invent_bored_hint");
        }

        private static void TestCommonErrorSupport(MathQuickRescueLearningPack pack, MathQuickRescueAdaptiveSelector selector)
        {
            var checkpoint = pack.GetCheckpoint(1);
            var known = checkpoint.CommonErrors[0];

            var cue = selector.ResolveErrorSupport(pack, 1, known.Id.ToUpperInvariant(), Decision(BehaviorState.READY));
            Assert(cue.KnownError && cue.ErrorId == known.Id, "common_error_matches_case_insensitively");
            Assert(!cue.UseRepair && cue.RecommendedHintLevel == 1, "ready_error_gets_small_specific_cue");
            Assert(cue.RecommendedCopyVi == known.CueVi, "known_error_uses_specific_cue");

            var repair = selector.ResolveErrorSupport(pack, 1, known.Id, Decision(BehaviorState.FRUSTRATED_LIKELY));
            Assert(repair.KnownError && repair.UseRepair && repair.RecommendedHintLevel == 2, "frustrated_error_gets_specific_repair");
            Assert(repair.RecommendedCopyVi == known.RepairVi, "frustrated_error_uses_common_error_repair_copy");

            var earlyRepair = selector.ResolveErrorSupport(pack, 1, known.Id, RepairDecision(BehaviorState.READY));
            Assert(earlyRepair.UseRepair && earlyRepair.RecommendedHintLevel == 2, "explicit_repair_escalates_common_error_support");
            Assert(earlyRepair.RecommendedCopyVi == known.RepairVi, "explicit_repair_uses_specific_common_error_repair");

            var generic = selector.ResolveErrorSupport(pack, 1, "unknown_error", Decision(BehaviorState.STRAINED));
            Assert(!generic.KnownError && generic.RecommendedHintLevel == 1, "unknown_error_falls_back_to_checkpoint_hint");
            Assert(generic.RecommendedCopyVi == checkpoint.HintLevel1Vi, "unknown_error_generic_copy_is_safe");

            var fatigue = selector.ResolveErrorSupport(pack, 1, known.Id, RepairDecision(BehaviorState.FATIGUED_LIKELY));
            Assert(fatigue.OfferBreak && !fatigue.UseRepair && fatigue.RecommendedHintLevel == 0, "fatigue_overrides_error_repair_pressure");
            Assert(fatigue.RecommendedCopyVi == pack.Feedback.BreakVi, "fatigue_error_path_uses_break_copy");
        }

        private static MathQuickRescueQuestionOption FindVariant(MathQuickRescueCheckpoint checkpoint, string variant)
        {
            foreach (var option in checkpoint.QuestionOptions)
                if (string.Equals(option.Variant, variant, StringComparison.Ordinal)) return option;
            throw new Exception("Missing test variant: " + variant);
        }

        private static List<MathQuestion> CloneQuestions(IList<MathQuestion> source)
        {
            var result = new List<MathQuestion>();
            foreach (var x in source)
            {
                result.Add(new MathQuestion
                {
                    ContentQuestionId = x.ContentQuestionId,
                    LessonId = x.LessonId,
                    SkillId = x.SkillId,
                    Difficulty = x.Difficulty
                });
            }
            return result;
        }

        private static void AssertInvalidData(Action action, string name)
        {
            var rejected = false;
            try { action(); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected, name);
        }

        private static BehaviorDecision Decision(BehaviorState state)
        {
            return new BehaviorDecision { State = state, CandidateState = state, Actions = new List<string>() };
        }

        private static BehaviorDecision RepairDecision(BehaviorState state)
        {
            return new BehaviorDecision
            {
                State = state,
                CandidateState = state,
                TriggerPrerequisiteRepair = true,
                Actions = new List<string> { "prerequisite_repair" }
            };
        }

        private static string FindProjectRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "WAHUKidsLearn.sln"))) return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Project root not found.");
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }
    }
}
