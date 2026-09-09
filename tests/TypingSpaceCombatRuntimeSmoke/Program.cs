using System;
using System.Collections.Generic;
using System.Linq;
using WAHU.TypingSpace.Combat;

namespace WAHU.TypingSpaceCombatRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        private static void Main()
        {
            TestRegistrationAndInitialState();
            TestChargePulseAndCap();
            TestShootActionAndNonBlockingCooldown();
            TestRescueAndUnlockActions();
            TestBossThreeShieldFlowAndIdempotency();
            TestWrongInputNeverHealsBoss();
            TestPauseResumePreservesActionWindow();
            TestUnknownAndMalformedEventsFailSafe();
            TestRendererHookIsolation();
            TestSnapshotIsolation();
            TestConcurrentCallbacksAreExactlyOnce();
            TestCustomSettingsValidationAndDurations();
            Console.WriteLine("TYPING_SPACE_COMBAT_RUNTIME_SMOKE_PASS assertions=" + _assertions);
        }

        private static void TestRegistrationAndInitialState()
        {
            var game = NewGame();
            var state = game.CurrentState;
            A(state.Mode == ShipActionMode.Idle && state.Charge == 0 && !state.InputLocked && !state.Paused,
                "initial_ship_idle_and_input_open");
            A(state.Boss.RequiredHits == 3 && state.Boss.HitsTaken == 0 && state.Boss.RemainingHits == 3 && !state.Boss.Defeated,
                "boss_defaults_to_three_shields");

            A(game.RegisterTarget("shoot-1", TypingCombatTargetKinds.Shoot), "register_shoot_target");
            A(game.RegisterTarget("rescue-1", TypingCombatTargetKinds.Rescue), "register_rescue_target");
            A(game.RegisterTarget("unlock-1", TypingCombatTargetKinds.Unlock), "register_unlock_target");
            A(game.RegisterTarget("boss-1", TypingCombatTargetKinds.Boss), "register_boss_target");
            A(!game.RegisterTarget("shoot-1", TypingCombatTargetKinds.Shoot), "same_binding_is_idempotent");
            A(game.HandleCoreEvent(TypingCombatEventTypes.CharCorrect, "adapter-probe", 1, "a", 1), "frozen_core_event_adapter_shape_is_directly_consumable");
            A(game.CurrentState.Boss.Active && game.CurrentState.Boss.PhaseNumber == 1, "boss_target_activates_phase_one");

            Throws<ArgumentException>(delegate { game.RegisterTarget("bad", "heal"); }, "unsupported_kind_rejected");
            Throws<InvalidOperationException>(delegate { game.RegisterTarget("shoot-1", TypingCombatTargetKinds.Rescue); }, "conflicting_target_kind_rejected");
        }

        private static void TestChargePulseAndCap()
        {
            var game = NewGame();
            var reasons = new List<string>();
            game.StateChanged += delegate(object sender, ShipActionChangedEventArgs e) { reasons.Add(e.Reason); };

            for (var i = 0; i < 20; i++)
            {
                A(game.HandleEvent(E(TypingCombatEventTypes.CharCorrect, "typing-word", 100 + i, 1)), "char_correct_handled_" + i);
            }

            var state = game.CurrentState;
            A(state.Mode == ShipActionMode.Charging && state.Charge == state.MaxCharge && state.MaxCharge == 12,
                "charge_caps_without_overflow");
            A(state.ActionSequence == 20, "every_correct_character_creates_render_pulse_sequence");
            A(reasons.Count == 20 && reasons.All(x => x == "char_correct_charge_pulse"), "every_correct_character_emits_charge_hook");
            A(!state.InputLocked, "charging_never_locks_typing");

            A(game.HandleEvent(E(TypingCombatEventTypes.CharWrong, "typing-word", 200, null)), "wrong_char_is_consumed_by_combat_adapter");
            state = game.CurrentState;
            A(state.Charge == 12 && state.Mode == ShipActionMode.Charging, "wrong_char_does_not_remove_charge");
        }

        private static void TestShootActionAndNonBlockingCooldown()
        {
            var game = NewGame();
            game.RegisterTarget("shoot-a", TypingCombatTargetKinds.Shoot);
            var outputs = CaptureOutputs(game);

            game.HandleEvent(E(TypingCombatEventTypes.CharCorrect, "shoot-a", 1000, 1));
            game.HandleEvent(E(TypingCombatEventTypes.CharCorrect, "shoot-a", 1010, 1));
            A(game.CurrentState.Charge == 2, "shoot_word_charges_before_completion");
            A(game.HandleEvent(E(TypingCombatEventTypes.WordCompleted, "shoot-a", 1020, null)), "shoot_word_completion_handled");

            var state = game.CurrentState;
            A(state.Mode == ShipActionMode.Firing && state.ActiveTargetId == "shoot-a" && state.Charge == 0,
                "shoot_completion_enters_firing_action");
            A(state.ActionUntilTimestamp == 1200, "default_fire_visual_window_is_short_180ms");
            A(!state.InputLocked, "firing_cooldown_never_locks_input");
            A(outputs.Count == 1 && outputs[0].Type == TypingCombatEventTypes.TargetDestroyed && outputs[0].TargetId == "shoot-a",
                "shoot_completion_emits_target_destroyed_once");

            A(game.HandleEvent(E(TypingCombatEventTypes.CharCorrect, "next-word", 1050, 1)), "typing_continues_during_fire_animation");
            state = game.CurrentState;
            A(state.Mode == ShipActionMode.Firing && state.Charge == 1 && !state.InputLocked,
                "fire_animation_keeps_new_word_charge_without_input_lock");

            game.Advance(1199);
            A(game.CurrentState.Mode == ShipActionMode.Firing, "fire_action_stays_until_visual_deadline");
            game.Advance(1200);
            state = game.CurrentState;
            A(state.Mode == ShipActionMode.Charging && state.Charge == 1, "elapsed_fire_returns_to_pending_charge_not_idle");

            A(!game.HandleEvent(E(TypingCombatEventTypes.WordCompleted, "shoot-a", 1300, null)), "duplicate_shoot_completion_rejected");
            A(outputs.Count == 1, "duplicate_shoot_does_not_double_destroy");
        }

        private static void TestRescueAndUnlockActions()
        {
            var game = NewGame();
            game.RegisterTarget("rescue-a", TypingCombatTargetKinds.Rescue);
            game.RegisterTarget("unlock-a", TypingCombatTargetKinds.Unlock);
            var outputs = CaptureOutputs(game);

            A(game.HandleEvent(E(TypingCombatEventTypes.WordCompleted, "rescue-a", 2000, null)), "rescue_completion_handled");
            var state = game.CurrentState;
            A(state.Mode == ShipActionMode.RescueBeam && state.ActiveTargetId == "rescue-a" && !state.InputLocked,
                "rescue_completion_enters_rescue_beam");
            A(outputs.Count == 1 && outputs[0].Type == TypingCombatEventTypes.TargetRescued,
                "rescue_emits_target_rescued");

            game.Advance(2220);
            A(game.CurrentState.Mode == ShipActionMode.Idle, "rescue_beam_returns_idle_after_short_window");

            A(game.HandleEvent(E(TypingCombatEventTypes.WordCompleted, "unlock-a", 2300, null)), "unlock_completion_handled");
            state = game.CurrentState;
            A(state.Mode == ShipActionMode.UnlockBeam && state.ActiveTargetId == "unlock-a" && state.ActionUntilTimestamp == 2520,
                "unlock_completion_enters_unlock_beam");
            A(outputs.Count == 1, "unlock_does_not_invent_non_contract_core_event");
            game.Advance(2520);
            A(game.CurrentState.Mode == ShipActionMode.Idle, "unlock_beam_returns_idle");
        }

        private static void TestBossThreeShieldFlowAndIdempotency()
        {
            var game = NewGame();
            game.RegisterTarget("boss-a", TypingCombatTargetKinds.Boss);
            game.RegisterTarget("boss-b", TypingCombatTargetKinds.Boss);
            game.RegisterTarget("boss-c", TypingCombatTargetKinds.Boss);
            game.RegisterTarget("boss-d", TypingCombatTargetKinds.Boss);
            var outputs = CaptureOutputs(game);

            A(game.HandleEvent(E(TypingCombatEventTypes.WordCompleted, "boss-a", 3000, null)), "boss_hit_one_handled");
            var state = game.CurrentState;
            A(state.Mode == ShipActionMode.BossStrike && state.Boss.HitsTaken == 1 && state.Boss.RemainingHits == 2 && state.Boss.PhaseNumber == 2,
                "boss_hit_one_advances_phase");
            A(outputs.Count == 1 && outputs[0].Type == TypingCombatEventTypes.BossHit && outputs[0].Value == 1,
                "boss_hit_one_event_emitted");

            A(!game.HandleEvent(E(TypingCombatEventTypes.WordCompleted, "boss-a", 3010, null)), "duplicate_boss_word_rejected");
            A(game.CurrentState.Boss.HitsTaken == 1 && outputs.Count == 1, "duplicate_boss_word_cannot_double_damage");

            A(game.HandleEvent(E(TypingCombatEventTypes.WordCompleted, "boss-b", 3100, null)), "boss_hit_two_handled");
            state = game.CurrentState;
            A(state.Boss.HitsTaken == 2 && state.Boss.RemainingHits == 1 && state.Boss.PhaseNumber == 3 && !state.Boss.Defeated,
                "boss_hit_two_reaches_phase_three");

            A(game.HandleEvent(E(TypingCombatEventTypes.WordCompleted, "boss-c", 3200, null)), "boss_hit_three_handled");
            state = game.CurrentState;
            A(state.Boss.HitsTaken == 3 && state.Boss.RemainingHits == 0 && state.Boss.Defeated && !state.Boss.Active,
                "boss_exactly_three_hits_defeated");
            A(outputs.Count(x => x.Type == TypingCombatEventTypes.BossHit) == 3, "boss_emits_exactly_three_hit_events");
            A(outputs.Count(x => x.Type == TypingCombatEventTypes.BossDefeated) == 1, "boss_defeated_emitted_exactly_once");
            A(outputs.Last().Type == TypingCombatEventTypes.BossDefeated && outputs.Last().Value == 3,
                "boss_defeat_event_reports_required_hit_count");

            A(!game.HandleEvent(E(TypingCombatEventTypes.WordCompleted, "boss-d", 3300, null)), "boss_cannot_take_damage_after_defeat");
            A(outputs.Count == 4, "post_defeat_word_cannot_emit_extra_events");
        }

        private static void TestWrongInputNeverHealsBoss()
        {
            var game = NewGame();
            game.RegisterTarget("boss-a", TypingCombatTargetKinds.Boss);
            game.RegisterTarget("boss-b", TypingCombatTargetKinds.Boss);
            game.HandleEvent(E(TypingCombatEventTypes.WordCompleted, "boss-a", 4000, null));
            var before = game.CurrentState.Boss;

            for (var i = 0; i < 8; i++)
                A(game.HandleEvent(E(TypingCombatEventTypes.CharWrong, "boss-b", 4010 + i, null)), "boss_wrong_char_handled_" + i);

            var after = game.CurrentState.Boss;
            A(after.HitsTaken == before.HitsTaken && after.RemainingHits == before.RemainingHits,
                "wrong_char_never_heals_or_changes_boss_hp");
            A(!after.Defeated, "wrong_char_never_forces_boss_state_change");
        }

        private static void TestPauseResumePreservesActionWindow()
        {
            var game = NewGame();
            game.RegisterTarget("shoot-pause", TypingCombatTargetKinds.Shoot);
            game.HandleEvent(E(TypingCombatEventTypes.WordCompleted, "shoot-pause", 5000, null));
            A(game.CurrentState.ActionUntilTimestamp == 5180, "pause_fixture_action_deadline_ready");

            A(game.HandleEvent(E(TypingCombatEventTypes.GamePaused, null, 5050, null)), "pause_event_handled");
            var paused = game.CurrentState;
            A(paused.Paused && paused.InputLocked && paused.Mode == ShipActionMode.Firing,
                "pause_is_only_state_that_locks_combat_input");
            game.Advance(9000);
            A(game.CurrentState.Mode == ShipActionMode.Firing, "advance_does_not_expire_action_while_paused");
            A(!game.HandleEvent(E(TypingCombatEventTypes.CharCorrect, "ignored", 5060, 1)), "typing_event_ignored_while_paused");
            A(game.CurrentState.Charge == 0, "paused_typing_does_not_mutate_charge");
            A(game.CurrentState.LastTimestamp == 5050, "paused_advance_and_input_do_not_move_combat_clock");

            A(game.HandleEvent(E(TypingCombatEventTypes.GameResumed, null, 6000, null)), "resume_event_handled");
            var resumed = game.CurrentState;
            A(!resumed.Paused && !resumed.InputLocked, "resume_reopens_input");
            A(resumed.ActionUntilTimestamp == 6130, "resume_shifts_visual_deadline_by_pause_duration");
            game.Advance(6129);
            A(game.CurrentState.Mode == ShipActionMode.Firing, "resumed_action_keeps_remaining_visual_time");
            game.Advance(6130);
            A(game.CurrentState.Mode == ShipActionMode.Idle, "resumed_action_expires_at_shifted_deadline");
        }

        private static void TestUnknownAndMalformedEventsFailSafe()
        {
            var game = NewGame();
            A(!game.HandleEvent(null), "null_event_ignored");
            A(!game.HandleEvent(new TypingCombatEvent { Type = null, Timestamp = 1 }), "blank_event_type_ignored");
            A(!game.HandleEvent(E("SOMETHING_ELSE", "x", 2, null)), "unknown_event_type_ignored");
            A(!game.HandleEvent(E(TypingCombatEventTypes.WordCompleted, "missing-target", 3, null)), "unknown_target_completion_ignored");
            A(game.CurrentState.Mode == ShipActionMode.Idle && game.CurrentState.Boss.HitsTaken == 0,
                "malformed_events_leave_state_safe");

            game.HandleEvent(E(TypingCombatEventTypes.CharCorrect, "x", 100, 1));
            game.HandleEvent(E(TypingCombatEventTypes.CharCorrect, "x", 50, 1));
            A(game.CurrentState.LastTimestamp == 100 && game.CurrentState.Charge == 2,
                "stale_timestamp_is_monotonic_clamped_without_dropping_input");
        }

        private static void TestRendererHookIsolation()
        {
            var game = NewGame();
            game.RegisterTarget("shoot-render", TypingCombatTargetKinds.Shoot);
            var healthyStateObserver = 0;
            var healthyOutputObserver = 0;
            var healthySawMutatedState = false;
            var healthySawMutatedOutput = false;
            game.StateChanged += delegate(object sender, ShipActionChangedEventArgs e)
            {
                if (e.Current != null) e.Current.Charge = 999;
                throw new Exception("renderer exploded");
            };
            game.StateChanged += delegate(object sender, ShipActionChangedEventArgs e)
            {
                healthyStateObserver++;
                if (e.Current != null && e.Current.Charge == 999) healthySawMutatedState = true;
            };
            game.OutputEmitted += delegate(object sender, CombatOutputEventArgs e)
            {
                if (e.Event != null) e.Event.Type = "MUTATED";
                throw new Exception("vfx exploded");
            };
            game.OutputEmitted += delegate(object sender, CombatOutputEventArgs e)
            {
                healthyOutputObserver++;
                if (e.Event != null && e.Event.Type == "MUTATED") healthySawMutatedOutput = true;
            };

            A(game.HandleEvent(E(TypingCombatEventTypes.CharCorrect, "shoot-render", 7000, 1)), "state_hook_exception_does_not_break_char_event");
            A(game.HandleEvent(E(TypingCombatEventTypes.WordCompleted, "shoot-render", 7010, null)), "output_hook_exception_does_not_break_word_event");
            A(healthyStateObserver == 2, "later_state_observer_survives_faulty_renderer");
            A(healthyOutputObserver == 1, "later_output_observer_survives_faulty_vfx");
            A(!healthySawMutatedState && !healthySawMutatedOutput, "subscriber_payloads_are_isolated_per_observer");
            A(game.CurrentState.Mode == ShipActionMode.Firing && game.CurrentState.Charge == 0,
                "renderer_failure_or_mutation_does_not_corrupt_gameplay_state");
        }

        private static void TestSnapshotIsolation()
        {
            var game = NewGame();
            game.RegisterTarget("boss-snapshot", TypingCombatTargetKinds.Boss);
            var exposed = game.CurrentState;
            exposed.Charge = 777;
            exposed.Mode = ShipActionMode.BossStrike;
            exposed.Boss.HitsTaken = 99;
            exposed.Boss.Defeated = true;

            var fresh = game.CurrentState;
            A(fresh.Charge == 0 && fresh.Mode == ShipActionMode.Idle, "external_snapshot_mutation_cannot_change_ship_state");
            A(fresh.Boss.HitsTaken == 0 && !fresh.Boss.Defeated, "external_snapshot_mutation_cannot_change_boss_state");
        }

        private static void TestConcurrentCallbacksAreExactlyOnce()
        {
            var game = NewGame();
            game.RegisterTarget("race-shoot", TypingCombatTargetKinds.Shoot);
            var outputs = new List<CombatOutputEvent>();
            var outputGate = new object();
            game.OutputEmitted += delegate(object sender, CombatOutputEventArgs e)
            {
                lock (outputGate) outputs.Add(e.Event);
            };

            var handledCount = 0;
            System.Threading.Tasks.Parallel.For(0, 32, delegate(int i)
            {
                if (game.HandleEvent(E(TypingCombatEventTypes.WordCompleted, "race-shoot", 9000 + i, null)))
                    System.Threading.Interlocked.Increment(ref handledCount);
            });

            A(handledCount == 1, "concurrent_duplicate_word_completion_has_one_winner");
            A(outputs.Count == 1 && outputs[0].Type == TypingCombatEventTypes.TargetDestroyed,
                "concurrent_duplicate_word_completion_emits_destroy_once");

            var chargeGame = NewGame();
            System.Threading.Tasks.Parallel.For(0, 100, delegate(int i)
            {
                chargeGame.HandleEvent(E(TypingCombatEventTypes.CharCorrect, "parallel-word", 10000 + i, 1));
            });
            var chargeState = chargeGame.CurrentState;
            A(chargeState.Charge == chargeState.MaxCharge && chargeState.ActionSequence == 100,
                "parallel_correct_callbacks_are_serialized_and_charge_caps_safely");
            A(!chargeState.InputLocked, "parallel_charge_processing_never_locks_input");
        }

        private static void TestCustomSettingsValidationAndDurations()
        {
            var settings = TypingSpaceCombatSettings.CreateDefault();
            settings.MaxCharge = 4;
            settings.FireDurationMs = 90;
            settings.BossRequiredHits = 3;
            var game = new TypingSpaceCombatController(settings);
            game.RegisterTarget("custom", TypingCombatTargetKinds.Shoot);
            for (var i = 0; i < 10; i++) game.HandleEvent(E(TypingCombatEventTypes.CharCorrect, "custom", 8000 + i, 1));
            A(game.CurrentState.Charge == 4 && game.CurrentState.MaxCharge == 4, "custom_charge_cap_honored");
            game.HandleEvent(E(TypingCombatEventTypes.WordCompleted, "custom", 8100, null));
            A(game.CurrentState.ActionUntilTimestamp == 8190, "custom_short_fire_duration_honored");

            var invalid = TypingSpaceCombatSettings.CreateDefault();
            invalid.BossRequiredHits = 0;
            Throws<ArgumentOutOfRangeException>(delegate { new TypingSpaceCombatController(invalid); }, "invalid_boss_hit_count_rejected");

            invalid = TypingSpaceCombatSettings.CreateDefault();
            invalid.FireDurationMs = 5000;
            Throws<ArgumentOutOfRangeException>(delegate { new TypingSpaceCombatController(invalid); }, "excessive_cooldown_rejected");
        }

        private static TypingSpaceCombatController NewGame()
        {
            return new TypingSpaceCombatController();
        }

        private static TypingCombatEvent E(string type, string targetId, long timestamp, int? value)
        {
            return new TypingCombatEvent { Type = type, TargetId = targetId, Timestamp = timestamp, Value = value };
        }

        private static List<CombatOutputEvent> CaptureOutputs(TypingSpaceCombatController game)
        {
            var outputs = new List<CombatOutputEvent>();
            game.OutputEmitted += delegate(object sender, CombatOutputEventArgs e) { outputs.Add(e.Event); };
            return outputs;
        }

        private static void A(bool condition, string name)
        {
            if (!condition) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }

        private static void Throws<T>(Action action, string name) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                _assertions++;
                return;
            }
            throw new Exception("ASSERT_FAIL: " + name);
        }
    }
}
