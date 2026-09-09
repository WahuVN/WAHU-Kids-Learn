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
            var pack = new MathQuickRescueContentSource().Load(path);
            var selector = new MathQuickRescueAdaptiveSelector();

            Assert(pack.CheckpointCount == 3, "three_checkpoints");
            Assert(pack.GetCheckpoint(1).Difficulty == "basic", "checkpoint1_basic");
            Assert(pack.GetCheckpoint(2).Difficulty == "medium", "checkpoint2_medium");
            Assert(pack.GetCheckpoint(3).Difficulty == "application", "checkpoint3_application");

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

            var frustrated = selector.Select(pack, 3, Decision(BehaviorState.FRUSTRATED_LIKELY), new string[0], 9);
            Assert(frustrated.Variant == "support", "frustrated_prefers_support_variant");
            Assert(frustrated.RecommendedHintLevel == 2 && frustrated.UseRepair, "frustrated_uses_repair");
            Assert(frustrated.SupportVi == pack.GetCheckpoint(3).RepairVi, "frustrated_uses_checkpoint_repair_copy");

            var flow = selector.Select(pack, 2, Decision(BehaviorState.FLOW_LIKELY), new string[0], 9);
            Assert(flow.MinimalFeedback && !flow.UseRepair, "flow_minimizes_interruptions");

            var fatigued = selector.Select(pack, 3, Decision(BehaviorState.FATIGUED_LIKELY), new string[0], 9);
            Assert(fatigued.OfferBreak, "fatigue_offers_break");
            Assert(string.IsNullOrWhiteSpace(fatigued.QuestionId), "fatigue_does_not_push_next_question");
            Assert(fatigued.SupportVi == pack.Feedback.BreakVi, "fatigue_uses_safe_break_copy");

            var allRecent = new List<string>();
            foreach (var option in pack.GetCheckpoint(2).QuestionOptions) allRecent.Add(option.QuestionId);
            var fallbackA = selector.Select(pack, 2, Decision(BehaviorState.READY), allRecent, 17);
            var fallbackB = selector.Select(pack, 2, Decision(BehaviorState.READY), allRecent, 17);
            Assert(fallbackA.QuestionId == fallbackB.QuestionId, "repeat_fallback_is_deterministic");
            Assert(pack.GetCheckpoint(2).QuestionOptions.Count == 2, "checkpoint_has_variety_for_repeat_control");

            Console.WriteLine("QUICK_RESCUE_LEARNING_RUNTIME_SMOKE_PASS assertions=" + _assertions);
        }

        private static BehaviorDecision Decision(BehaviorState state)
        {
            return new BehaviorDecision { State = state, CandidateState = state, Actions = new List<string>() };
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
