using System;
using System.Collections.Generic;
using WAHU.Learning;

namespace WAHU.BehaviorRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        private static void Main()
        {
            TestInsufficientEvidence();
            TestFlowAndHysteresis();
            TestBoredNeedsMoreThanSpeed();
            TestStrained();
            TestFrustrationAndRepair();
            TestFatigueCrossSkillAndMasteryProtection();
            Console.WriteLine("BEHAVIOR_RUNTIME_SMOKE_PASS assertions=" + _assertions);
        }

        private static BehaviorController Controller()
        {
            var c = new BehaviorController();
            c.SeedPersonalBaseline(new[]
            {
                O("BASE_A", true, 3000, 0, 0.80), O("BASE_B", true, 3100, 0, 0.82),
                O("BASE_A", true, 2900, 0, 0.84), O("BASE_B", true, 3000, 0, 0.85),
                O("BASE_A", true, 3050, 0, 0.84), O("BASE_B", false, 3000, 0, 0.78),
                O("BASE_A", true, 2950, 0, 0.83), O("BASE_B", true, 3100, 0, 0.86)
            });
            return c;
        }

        private static void TestInsufficientEvidence()
        {
            var c = Controller();
            var d = c.Observe(O("S", true, 3000, 0, 0.6));
            Assert(d.State == BehaviorState.READY, "insufficient_ready");
            Assert(d.Evidence.Contains("insufficient_recent_evidence"), "insufficient_evidence_marked");
        }

        private static void TestFlowAndHysteresis()
        {
            var c = Controller();
            BehaviorDecision d = null;
            var sequence = new[] { true, true, false, true, true };
            for (var i = 0; i < sequence.Length; i++)
                d = c.Observe(O("FLOW", sequence[i], 3000 + i * 20, 0, 0.65));
            Assert(d.CandidateState == BehaviorState.FLOW_LIKELY, "flow_candidate");
            Assert(d.State == BehaviorState.FLOW_LIKELY, "flow_entered_after_hysteresis");
            Assert(d.Confidence > 0.6, "flow_confidence");
        }

        private static void TestBoredNeedsMoreThanSpeed()
        {
            var c = Controller();
            BehaviorDecision d = null;
            for (var i = 0; i < 5; i++) d = c.Observe(O("FAST_ONLY", i % 2 == 0, 1200, 0, 0.9, representation: "symbol"));
            Assert(d.CandidateState != BehaviorState.BORED_OR_UNDERCHALLENGED, "speed_alone_never_bored");

            c = Controller();
            for (var i = 0; i < 5; i++) d = c.Observe(O("EASY", true, 1800, 0, 0.92, representation: "same"));
            Assert(d.CandidateState == BehaviorState.BORED_OR_UNDERCHALLENGED, "bored_requires_high_accuracy_mastery_plus_signal");
        }

        private static void TestStrained()
        {
            var c = Controller();
            BehaviorDecision d = null;
            for (var i = 0; i < 5; i++)
                d = c.Observe(O("HARD", i == 0 || i == 4, 4700, i < 3 ? 1 : 0, 0.52));
            Assert(d.CandidateState == BehaviorState.STRAINED, "strained_combined_signals");
        }

        private static void TestFrustrationAndRepair()
        {
            var c = Controller();
            BehaviorDecision d = null;
            for (var i = 0; i < 5; i++)
            {
                var x = O("CARRY", i == 0, i == 0 ? 3000 : 800, i > 1 ? 3 : 0, 0.45, "symbol");
                if (!x.IsCorrect) { x.ErrorType = "CARRY_MISSING"; x.RapidWrong = true; x.UsedMaxHint = i > 1; }
                d = c.Observe(x);
            }
            Assert(d.CandidateState == BehaviorState.FRUSTRATED_LIKELY, "frustrated_repeated_error_plus_signal");
            Assert(d.TriggerPrerequisiteRepair, "two_target_failures_trigger_repair");
            Assert(d.Actions.Contains("prerequisite_repair"), "repair_action_present");
        }

        private static void TestFatigueCrossSkillAndMasteryProtection()
        {
            var c = Controller();
            BehaviorDecision d = null;
            var skills = new[] { "ADD", "READ", "SUB", "VOCAB", "ADD", "READ" };
            for (var i = 0; i < skills.Length; i++)
            {
                var x = O(skills[i], i == 0, 5200, 0, 0.75, "mixed");
                x.InputMiss = i >= 2;
                x.SessionElapsedMinutes = 22;
                d = c.Observe(x);
            }
            Assert(d.DistinctRecentSkillCount >= 2, "fatigue_cross_skill_evidence");
            Assert(d.CandidateState == BehaviorState.FATIGUED_LIKELY, "fatigue_candidate");
            Assert(d.State == BehaviorState.FATIGUED_LIKELY, "fatigue_entered");
            Assert(d.ProtectMasteryFromNegativeUpdate, "fatigue_protects_mastery_without_repeated_skill_error");
        }

        private static BehaviorObservation O(string skill, bool correct, int ms, int hint, double mastery, string representation = "mixed")
        {
            return new BehaviorObservation
            {
                SkillId = skill,
                IsCorrect = correct,
                ResponseMs = ms,
                HintLevel = hint,
                MasteryScore = mastery,
                Representation = representation,
                SessionElapsedMinutes = 8
            };
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }
    }
}
