using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WAHU.TypingCore;

namespace WAHU.TypingCoreRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        private static int Main()
        {
            try
            {
                TestSingleCharacter();
                TestMultiCharacterWord();
                TestWrongThenRetry();
                TestKeyRepeatGuardAndLegitimateDoubleLetter();
                TestPauseResume();
                TestPausedStateFreezesTargetMutation();
                TestCompletedThenExtraInput();
                TestReactivatedSameIdDoesNotDuplicateCompletion();
                TestTargetSwitch();
                TestAcceptedAlternativesAndUnicodeNormalization();
                TestSpaceAndControlKeyBoundary();
                TestSnapshotIsolation();
                TestConcurrentDuplicateDispatch();
                TestBossPhase();
                Console.WriteLine("TYPING_CORE_RUNTIME_SMOKE_PASS assertions=" + _assertions);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("TYPING_CORE_RUNTIME_SMOKE_FAIL " + ex);
                return 1;
            }
        }

        private static void TestSingleCharacter()
        {
            var engine = new TypingEngine();
            engine.SetActiveTarget(Target("one", "A", TypingTargetKind.Shoot, 4));
            var result = engine.ProcessInput(Input("A", null, 1000));
            A(result.Accepted && !result.Ignored, "single_char_accepted");
            A(result.Snapshot.Progress.TypedCount == 1 && result.Snapshot.Progress.Completed, "single_char_completed");
            A(Count(result, TypingEventType.CharCorrect) == 1 && Count(result, TypingEventType.WordCompleted) == 1,
                "single_char_emits_correct_and_completion_once");
            A(result.Events.Single(x => x.Type == TypingEventType.WordCompleted).Value == 4,
                "completion_carries_target_reward_value_without_granting_reward");
        }

        private static void TestMultiCharacterWord()
        {
            var engine = new TypingEngine();
            engine.SetActiveTarget(Target("cat", "cat", TypingTargetKind.Rescue, 2));
            var r1 = engine.ProcessInput(Input("c", "c", 1000));
            var r2 = engine.ProcessInput(Input("a", "a", 1100));
            var r3 = engine.ProcessInput(Input("t", "t", 1200));
            A(r1.Snapshot.Progress.TypedCount == 1 && !r1.Snapshot.Progress.Completed, "multi_first_char");
            A(r2.Snapshot.Progress.TypedCount == 2 && !r2.Snapshot.Progress.Completed, "multi_second_char");
            A(r3.Snapshot.Progress.TypedCount == 3 && r3.Snapshot.Progress.Completed, "multi_final_char");
            A(Count(r1, TypingEventType.WordCompleted) + Count(r2, TypingEventType.WordCompleted) + Count(r3, TypingEventType.WordCompleted) == 1,
                "multi_completion_exactly_once");
        }

        private static void TestWrongThenRetry()
        {
            var engine = new TypingEngine();
            engine.SetActiveTarget(Target("sun", "sun", TypingTargetKind.Unlock, 1));
            var wrong = engine.ProcessInput(Input("x", "x", 1000));
            A(!wrong.Accepted && !wrong.Ignored && wrong.Reason == "wrong_key", "wrong_is_feedback_not_ignored");
            A(wrong.Snapshot.Progress.TypedCount == 0 && wrong.Snapshot.Progress.ErrorCount == 1, "wrong_does_not_advance");
            A(wrong.Snapshot.Phase == TypingGamePhase.Feedback && Count(wrong, TypingEventType.CharWrong) == 1,
                "wrong_emits_light_repair_feedback");
            var retry = engine.ProcessInput(Input("s", "s", 1100));
            A(retry.Accepted && retry.Snapshot.Progress.TypedCount == 1 && retry.Snapshot.Progress.ErrorCount == 1,
                "retry_advances_without_losing_error_history");
            A(retry.Snapshot.Phase == TypingGamePhase.Playing, "retry_returns_to_playing");
        }

        private static void TestKeyRepeatGuardAndLegitimateDoubleLetter()
        {
            var engine = new TypingEngine(20);
            engine.SetActiveTarget(Target("book", "book", TypingTargetKind.Shoot, 1));
            engine.ProcessInput(Input("b", "b", 1000));
            var firstO = engine.ProcessInput(Input("o", "o", 1100));
            var duplicate = engine.ProcessInput(Input("o", "o", 1105));
            var secondO = engine.ProcessInput(Input("o", "o", 1140));
            var k = engine.ProcessInput(Input("k", "k", 1200));
            A(firstO.Accepted && firstO.Snapshot.Progress.TypedCount == 2, "repeat_first_o_accepted");
            A(duplicate.Ignored && duplicate.Reason == "duplicate_or_repeat" && duplicate.Snapshot.Progress.TypedCount == 2,
                "abnormal_repeat_is_ignored");
            A(secondO.Accepted && secondO.Snapshot.Progress.TypedCount == 3, "legitimate_double_letter_after_window_accepted");
            A(k.Snapshot.Progress.Completed, "double_letter_word_completes");
        }

        private static void TestPauseResume()
        {
            var engine = new TypingEngine();
            engine.SetActiveTarget(Target("pause", "go", TypingTargetKind.Shoot, 1));
            engine.ProcessInput(Input("g", "g", 1000));
            var paused = engine.Pause(1050);
            var whilePaused = engine.ProcessInput(Input("o", "o", 1100));
            var resumed = engine.Resume(1150);
            var finish = engine.ProcessInput(Input("o", "o", 1200));
            A(paused.Snapshot.Paused && paused.Snapshot.Phase == TypingGamePhase.Paused && Count(paused, TypingEventType.GamePaused) == 1,
                "pause_state_and_event");
            A(whilePaused.Ignored && whilePaused.Snapshot.Progress.TypedCount == 1, "paused_input_cannot_advance");
            A(!resumed.Snapshot.Paused && resumed.Snapshot.Progress.TypedCount == 1 && Count(resumed, TypingEventType.GameResumed) == 1,
                "resume_restores_exact_progress");
            A(finish.Snapshot.Progress.Completed, "resume_can_finish_same_target");
        }

        private static void TestPausedStateFreezesTargetMutation()
        {
            var engine = new TypingEngine();
            engine.SetActiveTarget(Target("freeze-a", "ab", TypingTargetKind.Shoot, 1));
            engine.ProcessInput(Input("a", "a", 1000));
            engine.Pause(1050);
            var setWhilePaused = engine.SetActiveTarget(Target("freeze-b", "xy", TypingTargetKind.Rescue, 1), 1100);
            var clearWhilePaused = engine.ClearActiveTarget();
            A(setWhilePaused.Ignored && setWhilePaused.Reason == "paused", "paused_state_rejects_target_switch");
            A(clearWhilePaused.Ignored && clearWhilePaused.Reason == "paused", "paused_state_rejects_target_clear");
            var frozen = engine.Snapshot;
            A(frozen.Paused && frozen.Progress.TargetId == "freeze-a" && frozen.Progress.TypedCount == 1,
                "pause_freezes_exact_active_target_and_progress");
            engine.Resume(1200);
            var b = engine.ProcessInput(Input("b", "b", 1300));
            A(b.Snapshot.Progress.Completed && b.Snapshot.Progress.TargetId == "freeze-a",
                "resume_continues_original_target_after_rejected_mutations");
        }

        private static void TestCompletedThenExtraInput()
        {
            var engine = new TypingEngine();
            engine.SetActiveTarget(Target("done", "ok", TypingTargetKind.Rescue, 3));
            engine.ProcessInput(Input("o", "o", 1000));
            var done = engine.ProcessInput(Input("k", "k", 1100));
            var extra = engine.ProcessInput(Input("k", "k", 1200));
            A(Count(done, TypingEventType.WordCompleted) == 1, "completion_first_emit");
            A(extra.Ignored && extra.Reason == "target_completed" && extra.Events.Count == 0,
                "completed_target_rejects_extra_input_without_duplicate_event");
            A(engine.Snapshot.Progress.Completed && engine.Snapshot.Progress.TypedCount == 2,
                "completed_state_remains_stable");
        }

        private static void TestReactivatedSameIdDoesNotDuplicateCompletion()
        {
            var engine = new TypingEngine();
            engine.SetActiveTarget(Target("same-id", "a", TypingTargetKind.Shoot, 7));
            var first = engine.ProcessInput(Input("a", "a", 1000));
            engine.SetActiveTarget(Target("same-id", "a", TypingTargetKind.Shoot, 7), 1100);
            var second = engine.ProcessInput(Input("a", "a", 1200));
            A(Count(first, TypingEventType.WordCompleted) == 1, "same_id_first_activation_emits_completion");
            A(second.Snapshot.Progress.Completed && Count(second, TypingEventType.WordCompleted) == 0,
                "same_target_id_never_emits_completion_twice");
        }

        private static void TestTargetSwitch()
        {
            var engine = new TypingEngine();
            engine.SetActiveTarget(Target("alpha", "ab", TypingTargetKind.Shoot, 1));
            engine.ProcessInput(Input("a", "a", 1000));
            var switched = engine.SetActiveTarget(Target("beta", "xy", TypingTargetKind.Rescue, 1), 1050);
            A(switched.Snapshot.Progress.TargetId == "beta" && switched.Snapshot.Progress.TypedCount == 0 && switched.Snapshot.Progress.ErrorCount == 0,
                "switch_target_resets_progress_only_for_new_active_target");
            var x = engine.ProcessInput(Input("x", "x", 1100));
            A(x.Snapshot.Progress.TargetId == "beta" && x.Snapshot.Progress.TypedCount == 1, "switch_target_uses_new_expected_text");
        }

        private static void TestAcceptedAlternativesAndUnicodeNormalization()
        {
            var engine = new TypingEngine();
            var target = Target("alt", "cat", TypingTargetKind.Shoot, 1);
            target.AcceptedInputs = new List<string> { "cat", "kat" };
            engine.SetActiveTarget(target);
            engine.ProcessInput(Input("K", "K", 1000));
            engine.ProcessInput(Input("a", "a", 1100));
            var altDone = engine.ProcessInput(Input("t", "t", 1200));
            A(altDone.Snapshot.Progress.Completed, "accepted_alternative_sequence_completes");

            engine.SetActiveTarget(Target("unicode", "bé", TypingTargetKind.Rescue, 1));
            engine.ProcessInput(Input("b", "b", 1300));
            var unicodeDone = engine.ProcessInput(Input("e", "e\u0301", 1400));
            A(unicodeDone.Snapshot.Progress.Completed, "nfc_normalization_accepts_decomposed_normalized_key");
        }

        private static void TestSpaceAndControlKeyBoundary()
        {
            var engine = new TypingEngine();
            engine.SetActiveTarget(Target("space", "a b", TypingTargetKind.Unlock, 1));
            engine.ProcessInput(Input("a", "a", 1000));
            var space = engine.ProcessInput(Input("Space", " ", 1100));
            var b = engine.ProcessInput(Input("b", "b", 1200));
            A(space.Accepted && space.Snapshot.Progress.TypedCount == 2, "space_normalized_key_is_a_valid_character");
            A(b.Snapshot.Progress.Completed, "target_with_space_completes");

            engine.SetActiveTarget(Target("control", "x", TypingTargetKind.Shoot, 1));
            var control = engine.ProcessInput(Input("Shift", null, 1300));
            A(control.Ignored && control.Reason == "invalid_normalized_key" && control.Snapshot.Progress.ErrorCount == 0,
                "multi_element_control_key_is_ignored_not_counted_as_child_error");
            var x = engine.ProcessInput(Input("x", "x", 1400));
            A(x.Snapshot.Progress.Completed, "valid_key_after_control_key_still_completes");
        }

        private static void TestSnapshotIsolation()
        {
            var engine = new TypingEngine();
            var original = Target("isolation", "hi", TypingTargetKind.Shoot, 1);
            engine.SetActiveTarget(original);
            original.DisplayText = "tampered";
            var external = engine.Snapshot;
            external.ActiveTarget.DisplayText = "poison";
            external.ActiveTarget.AcceptedInputs[0] = "zz";
            external.Progress.TypedCount = 99;
            var actual = engine.Snapshot;
            A(actual.ActiveTarget.DisplayText == "hi" && actual.Progress.TypedCount == 0,
                "target_and_snapshot_are_defensively_detached");
            var h = engine.ProcessInput(Input("h", "h", 1000));
            A(h.Accepted && h.Snapshot.Progress.TypedCount == 1, "snapshot_mutation_cannot_poison_matching_core");
        }

        private static void TestConcurrentDuplicateDispatch()
        {
            var engine = new TypingEngine();
            engine.SetActiveTarget(Target("race", "a", TypingTargetKind.Shoot, 1));
            var start = new ManualResetEvent(false);
            var accepted = 0;
            var ignored = 0;
            Exception unexpected = null;
            ThreadStart body = delegate
            {
                start.WaitOne();
                try
                {
                    var result = engine.ProcessInput(Input("a", "a", 1000));
                    if (result.Accepted) Interlocked.Increment(ref accepted);
                    if (result.Ignored) Interlocked.Increment(ref ignored);
                }
                catch (Exception ex) { unexpected = ex; }
            };
            var t1 = new Thread(body);
            var t2 = new Thread(body);
            t1.Start(); t2.Start(); start.Set(); t1.Join(); t2.Join(); start.Dispose();
            A(unexpected == null && accepted == 1 && ignored == 1, "concurrent_duplicate_dispatch_has_one_winner");
            A(engine.Snapshot.Progress.Completed, "concurrent_dispatch_completes_once");
        }

        private static void TestBossPhase()
        {
            var engine = new TypingEngine();
            var start = engine.SetActiveTarget(Target("boss", "zap", TypingTargetKind.Boss, 5));
            A(start.Snapshot.Phase == TypingGamePhase.Boss, "boss_target_enters_boss_phase");
            var wrong = engine.ProcessInput(Input("x", "x", 1000));
            A(wrong.Snapshot.Phase == TypingGamePhase.Feedback, "boss_wrong_uses_feedback_phase");
            var repaired = engine.ProcessInput(Input("z", "z", 1100));
            A(repaired.Snapshot.Phase == TypingGamePhase.Boss, "boss_repair_returns_to_boss_phase");
        }

        private static TypingTarget Target(string id, string text, string kind, int reward)
        {
            return new TypingTarget
            {
                Id = id,
                DisplayText = text,
                AcceptedInputs = new List<string> { text },
                Language = TypingLanguage.English,
                Difficulty = 1,
                Kind = kind,
                RewardValue = reward
            };
        }

        private static TypingInput Input(string raw, string normalized, long timestamp)
        {
            return new TypingInput
            {
                RawKey = raw,
                NormalizedKey = normalized,
                Timestamp = timestamp,
                Source = TypingInputSource.Physical
            };
        }

        private static int Count(TypingEngineResult result, string type)
        {
            return result.Events.Count(x => x.Type == type);
        }

        private static void A(bool condition, string name)
        {
            _assertions++;
            if (!condition) throw new InvalidOperationException("ASSERT_FAIL: " + name);
        }
    }
}
