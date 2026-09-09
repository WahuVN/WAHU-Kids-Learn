using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WAHU.Typing.Targets;

namespace WAHU.TypingTargetRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        private static void Main()
        {
            TestMockContract();
            TestValidationAndIsolation();
            TestSpawnThrottleAndPrefixAvoidance();
            TestDeterministicPrefixLockAndExplicitFocus();
            TestMovementAndTypingProtection();
            TestPauseResolveAndDespawn();
            TestBossFloorAndNoAutoDespawn();
            TestSeedDeterminism();
            RunHeadlessDemo();
            Console.WriteLine("TYPING_TARGET_RUNTIME_SMOKE_PASS assertions=" + _assertions);
        }

        private static void TestMockContract()
        {
            var mocks = MockTypingTargets.CreateTen();
            A(mocks.Count == 10, "mock_ten_targets_available");
            A(mocks.Select(x => x.Target.Id).Distinct(StringComparer.Ordinal).Count() == 10, "mock_ids_unique");
            A(mocks.Count(x => x.Target.Language == "vi") == 5 && mocks.Count(x => x.Target.Language == "en") == 5, "mock_bilingual_5_5");
            A(mocks.All(x => x.Target.AcceptedInputs != null && x.Target.AcceptedInputs.Count > 0), "mock_accepted_inputs_present");
            A(mocks.All(x => x.Target.Difficulty >= 1 && x.Target.Difficulty <= 5), "mock_difficulty_valid");
            A(mocks.Any(x => x.Target.Kind == "boss") && mocks.Any(x => x.Target.Kind == "rescue") && mocks.Any(x => x.Target.Kind == "shoot"), "mock_kinds_cover_runtime");
        }

        private static void TestValidationAndIsolation()
        {
            var system = NewSystem(31);
            var source = Request("clone_guard", "nova", "nova", "en", 1, "shoot", TypingTargetMovementKind.Asteroid, 0);
            system.Enqueue(source, 0);
            source.Target.DisplayText = "MUTATED_OUTSIDE";
            source.Target.AcceptedInputs[0] = "x";
            system.Tick(0, 0);
            var snapshot = system.Snapshot().Single();
            A(snapshot.Target.DisplayText == "nova" && snapshot.Target.AcceptedInputs[0] == "nova", "enqueue_clones_contract_input_against_external_mutation");
            snapshot.Target.DisplayText = "MUTATED_SNAPSHOT";
            A(system.Snapshot().Single().Target.DisplayText == "nova", "snapshot_returns_defensive_target_clone");

            var duplicateRejected = false;
            try { system.Enqueue(Request("clone_guard", "again", "again", "en", 1, "shoot", TypingTargetMovementKind.Asteroid, 0), 1); }
            catch (InvalidOperationException) { duplicateRejected = true; }
            A(duplicateRejected, "duplicate_target_id_rejected");

            var invalidLanguage = false;
            try { system.Enqueue(Request("bad_lang", "x", "x", "jp", 1, "shoot", TypingTargetMovementKind.Asteroid, 0), 2); }
            catch (ArgumentException) { invalidLanguage = true; }
            A(invalidLanguage, "invalid_language_rejected");

            var invalidKind = false;
            try { system.Enqueue(Request("bad_kind", "x", "x", "en", 1, "loot", TypingTargetMovementKind.Asteroid, 0), 3); }
            catch (ArgumentException) { invalidKind = true; }
            A(invalidKind, "invalid_kind_rejected");

            var invalidLane = false;
            try { system.Enqueue(Request("bad_lane", "x", "x", "en", 1, "shoot", TypingTargetMovementKind.Asteroid, 99), 4); }
            catch (ArgumentOutOfRangeException) { invalidLane = true; }
            A(invalidLane, "invalid_lane_rejected");

            var drained = system.DrainEvents();
            A(drained.Count >= 2 && system.DrainEvents().Count == 0, "event_drain_is_destructive_and_bounded");
            drained[0].Text = "MUTATED_EVENT_COPY";
            A(system.DrainEvents().Count == 0, "drained_event_mutation_cannot_reenter_system");

            system.SetPaused(true, 100);
            A(!system.MarkTypingActivity("clone_guard", 100), "paused_system_rejects_typing_activity_mutation");
            A(!system.ResolveTarget("clone_guard", "paused-resolve", 100), "paused_system_rejects_resolution_mutation");
            A(!system.FocusTarget("clone_guard", 100), "paused_system_rejects_focus_mutation");
            string locked;
            A(!system.TryLockByInput("n", 100, out locked), "paused_system_rejects_prefix_lock");
            system.SetPaused(false, 200);
            A(system.ResolveTarget("clone_guard", "done", 200), "resolution_allowed_after_resume");
        }

        private static void TestSpawnThrottleAndPrefixAvoidance()
        {
            var system = NewSystem(17);
            foreach (var request in MockTypingTargets.CreateTen()) system.Enqueue(request, 0);
            A(system.QueuedCount == 10 && system.ActiveCount == 0, "all_targets_start_queued");

            system.Tick(0, 0);
            var first = system.Snapshot();
            A(first.Count == 1, "first_tick_spawns_at_most_one");
            var events = system.DrainEvents();
            A(events.Count(x => x.Type == TypingTargetSystem.CoreSpawnEvent) == 1, "spawn_emits_frozen_core_event");
            A(events.Any(x => x.Type == TypingTargetSystem.CoreSpawnEvent && x.Timestamp == 0), "spawn_event_timestamp_preserved");

            system.Tick(1000, 1000);
            A(system.ActiveCount == 1, "scheduler_does_not_burst_before_interval");
            system.Tick(4000, 3000);
            A(system.ActiveCount == 2, "second_target_spawns_after_child_safe_gap");
            system.Tick(8000, 4000);
            A(system.ActiveCount <= 3, "max_concurrent_targets_respected");

            var active = system.Snapshot();
            var firstKeys = active.SelectMany(x => x.Target.AcceptedInputs.Select(s => s.Substring(0, 1).ToLowerInvariant())).ToList();
            A(firstKeys.Count >= active.Count, "active_targets_expose_lock_keys");
            A(active.All(x => x.Lane >= 0 && x.Lane < 3), "spawn_assigns_valid_lanes");
            A(active.All(x => x.X <= 1.04 && x.X >= 0.02), "spawn_positions_normalized");
        }

        private static void TestDeterministicPrefixLockAndExplicitFocus()
        {
            var options = TypingTargetSystemOptions.ChildSafeDefaults();
            options.MaxConcurrentTargets = 4;
            options.MaxAmbiguousPrefixDeferralMs = 1000;
            var system = new TypingTargetSystem(options, new FastPolicy(), new SeededTypingTargetRandom(2));
            system.Enqueue(Request("cat_a", "cat", "cat", "en", 1, "shoot", TypingTargetMovementKind.Asteroid, 0), 0);
            system.Enqueue(Request("car_b", "car", "car", "en", 1, "shoot", TypingTargetMovementKind.Ufo, 1), 0);
            system.Enqueue(Request("moon_c", "moon", "moon", "en", 1, "rescue", TypingTargetMovementKind.Satellite, 2), 0);
            system.Tick(0, 0);
            system.Tick(200, 200);
            A(system.ActiveCount == 2, "conflict_free_candidate_skips_ambiguous_prefix");
            A(system.Snapshot().Any(x => x.Target.Id == "cat_a") && system.Snapshot().Any(x => x.Target.Id == "moon_c"), "spawn_scheduler_prefers_non_ambiguous_prefix");

            system.Tick(1200, 1000);
            A(system.ActiveCount == 3, "ambiguous_prefix_eventually_spawns_after_bounded_deferral");
            string locked;
            A(system.TryLockByInput("c", 1200, out locked), "prefix_lock_acquired");
            A(locked == "cat_a", "ambiguous_prefix_lock_uses_deterministic_spawn_order_when_positions_equalish");
            A(system.LockedTargetId == "cat_a", "lock_state_exposed");

            A(system.FocusTarget("car_b", 1250), "explicit_focus_switches_target");
            A(system.LockedTargetId == "car_b", "explicit_focus_has_priority");
            var snapshots = system.Snapshot();
            A(snapshots.Single(x => x.Target.Id == "car_b").IsLocked, "focused_target_marked_locked");
            A(!snapshots.Single(x => x.Target.Id == "cat_a").IsLocked, "previous_target_unlocked_on_focus_switch");
            A(system.ReleaseLock(1300) && system.LockedTargetId == null, "explicit_lock_release_supported");
        }

        private static void TestMovementAndTypingProtection()
        {
            var options = TypingTargetSystemOptions.ChildSafeDefaults();
            options.MaxConcurrentTargets = 3;
            var system = new TypingTargetSystem(options, new FixedPolicy(0.10, 100), new SeededTypingTargetRandom(9));
            system.Enqueue(Request("asteroid", "alpha", "alpha", "en", 2, "shoot", TypingTargetMovementKind.Asteroid, 0), 0);
            system.Enqueue(Request("ufo", "beta", "beta", "en", 2, "shoot", TypingTargetMovementKind.Ufo, 1), 0);
            system.Enqueue(Request("satellite", "gamma", "gamma", "en", 2, "rescue", TypingTargetMovementKind.Satellite, 2), 0);
            system.Tick(0, 0);
            system.Tick(100, 100);
            system.Tick(200, 100);
            A(system.ActiveCount == 3, "movement_fixture_spawned_three_targets");

            var before = system.Snapshot();
            system.Tick(1200, 1000);
            var after = system.Snapshot();
            A(after.All(x => x.X < before.Single(b => b.Target.Id == x.Target.Id).X), "all_movement_types_advance_left");
            A(Math.Abs(after.Single(x => x.Target.Id == "asteroid").Y - before.Single(x => x.Target.Id == "asteroid").Y) < 0.000001, "asteroid_lane_is_linear");
            A(Math.Abs(after.Single(x => x.Target.Id == "ufo").Y - before.Single(x => x.Target.Id == "ufo").Y) > 0.0001, "ufo_has_bob_motion");
            A(Math.Abs(after.Single(x => x.Target.Id == "satellite").Y - before.Single(x => x.Target.Id == "satellite").Y) > 0.0001, "satellite_has_gentle_motion");

            A(system.FocusTarget("asteroid", 1200), "typing_protection_target_focus");
            system.MarkTypingActivity("asteroid", 1200);
            var protectedStart = system.Snapshot().Single(x => x.Target.Id == "asteroid").X;
            system.Tick(2200, 1000);
            var protectedEnd = system.Snapshot().Single(x => x.Target.Id == "asteroid").X;
            A(protectedStart - protectedEnd < 0.03, "typing_activity_slows_target_significantly");
            A(system.Snapshot().Single(x => x.Target.Id == "asteroid").TypingProtected, "typing_protection_visible_to_renderer");

            for (var t = 2300; t <= 40000; t += 500)
            {
                system.MarkTypingActivity("asteroid", t);
                system.Tick(t, 500);
            }
            var protectedFloor = system.Snapshot().Single(x => x.Target.Id == "asteroid");
            A(protectedFloor.X >= options.SafeFloorX - 0.000001, "typing_target_never_crosses_child_safe_floor");
            A(system.ActiveCount >= 1, "typing_target_not_forced_to_fail_while_child_engaged");
        }

        private static void TestPauseResolveAndDespawn()
        {
            var options = TypingTargetSystemOptions.ChildSafeDefaults();
            var system = new TypingTargetSystem(options, new FixedPolicy(0.05, 1000), new SeededTypingTargetRandom(3));
            system.Enqueue(Request("pause_one", "planet", "planet", "en", 1, "unlock", TypingTargetMovementKind.Ufo, 1), 0);
            system.Tick(0, 0);
            var start = system.Snapshot().Single();
            system.SetPaused(true, 100);
            system.Tick(5100, 5000);
            var paused = system.Snapshot().Single();
            A(Math.Abs(start.X - paused.X) < 0.000001, "pause_freezes_movement");
            A(system.Paused, "pause_state_exposed");
            system.SetPaused(false, 5100);
            system.Tick(6100, 1000);
            A(system.Snapshot().Single().X < paused.X, "resume_restores_movement_without_catchup_burst");

            A(system.ResolveTarget("pause_one", "unlock-complete", 6200), "resolved_target_transition_succeeds");
            A(system.Snapshot().Single().State == TypingTargetLifecycleState.Resolved, "resolved_state_visible_during_feedback_grace");
            var events = system.DrainEvents();
            A(events.Any(x => x.Type == TypingTargetSystem.InternalPausedEvent) && events.Any(x => x.Type == TypingTargetSystem.InternalResumedEvent), "pause_resume_lifecycle_events_emitted");
            A(events.Any(x => x.Type == TypingTargetSystem.InternalResolvedEvent && x.TargetId == "pause_one"), "resolved_lifecycle_event_emitted");
            system.Tick(6500, 300);
            A(system.ActiveCount == 1, "resolved_target_kept_for_short_feedback_window");
            system.Tick(6700, 200);
            A(system.ActiveCount == 0, "resolved_target_despawns_after_feedback_window");
            A(system.DrainEvents().Any(x => x.Type == TypingTargetSystem.InternalDespawnedEvent), "despawn_lifecycle_event_emitted");
        }

        private static void TestBossFloorAndNoAutoDespawn()
        {
            var options = TypingTargetSystemOptions.ChildSafeDefaults();
            var system = new TypingTargetSystem(options, new FixedPolicy(0.5, 1000), new SeededTypingTargetRandom(4));
            system.Enqueue(Request("boss", "meteor", "meteor", "en", 5, "boss", TypingTargetMovementKind.Ufo, 1), 0);
            system.Tick(0, 0);
            for (var i = 1; i <= 100; i++) system.Tick(i * 1000, 1000);
            A(system.ActiveCount == 1, "boss_target_never_auto_despawns");
            var boss = system.Snapshot().Single();
            A(boss.X >= options.BossFloorX - 0.000001, "boss_stops_at_boss_safe_floor");
            A(system.ResolveTarget("boss", "boss-defeated", 101000), "boss_can_be_resolved_by_combat_owner");
            system.Tick(102000, 1000);
            A(system.ActiveCount == 0, "resolved_boss_despawns_normally");
        }

        private static void TestSeedDeterminism()
        {
            var a = SimulationFingerprint(77);
            var b = SimulationFingerprint(77);
            var c = SimulationFingerprint(78);
            A(a == b, "same_seed_produces_identical_spawn_lane_motion_fingerprint");
            A(a != c, "different_seed_changes_lane_or_motion_phase_fingerprint");
        }

        private static string SimulationFingerprint(int seed)
        {
            var system = NewSystem(seed);
            foreach (var request in MockTypingTargets.CreateTen().Take(6))
            {
                request.PreferredLane = null;
                system.Enqueue(request, 0);
            }
            for (var t = 0; t <= 15000; t += 500) system.Tick(t, t == 0 ? 0 : 500);
            return string.Join("|", system.Snapshot().Select(x =>
                x.Target.Id + ":" + x.Lane + ":" + x.X.ToString("F4", CultureInfo.InvariantCulture) + ":" + x.Y.ToString("F4", CultureInfo.InvariantCulture)));
        }

        private static void RunHeadlessDemo()
        {
            var system = NewSystem(20260909);
            foreach (var request in MockTypingTargets.CreateTen()) system.Enqueue(request, 0);
            var trace = new List<string>();
            for (var t = 0; t <= 22000; t += 500)
            {
                system.Tick(t, t == 0 ? 0 : 500);
                if (t == 4500)
                {
                    string target;
                    if (system.TryLockByInput("t", t, out target)) trace.Add("lock:" + target);
                }
                if (t >= 4500 && t <= 7500 && system.LockedTargetId != null) system.MarkTypingActivity(system.LockedTargetId, t);
                if (t == 8000 && system.LockedTargetId != null)
                {
                    var resolved = system.LockedTargetId;
                    system.ResolveTarget(resolved, "demo-word-complete", t);
                    trace.Add("resolve:" + resolved);
                }
            }
            var spawnEvents = system.DrainEvents().Count(x => x.Type == TypingTargetSystem.CoreSpawnEvent);
            A(spawnEvents >= 3, "headless_demo_spawns_multiple_targets");
            A(trace.Any(x => x.StartsWith("lock:", StringComparison.Ordinal)), "headless_demo_can_lock_target");
            A(trace.Any(x => x.StartsWith("resolve:", StringComparison.Ordinal)), "headless_demo_can_resolve_target");
            Console.WriteLine("TYPING_TARGET_HEADLESS_DEMO active=" + system.ActiveCount + " queued=" + system.QueuedCount + " trace=" + string.Join(",", trace));
        }

        private static TypingTargetSystem NewSystem(int seed)
        {
            return new TypingTargetSystem(
                TypingTargetSystemOptions.ChildSafeDefaults(),
                new SafeTypingTargetDifficultyPolicy(),
                new SeededTypingTargetRandom(seed));
        }

        private static TypingTargetSpawnRequest Request(string id, string display, string input, string language, int difficulty, string kind, TypingTargetMovementKind movement, int? lane)
        {
            return new TypingTargetSpawnRequest
            {
                Target = new TypingTarget
                {
                    Id = id,
                    DisplayText = display,
                    AcceptedInputs = new List<string> { input },
                    Language = language,
                    Difficulty = difficulty,
                    Kind = kind,
                    RewardValue = 1
                },
                MovementKind = movement,
                PreferredLane = lane
            };
        }

        private static void A(bool condition, string name)
        {
            if (!condition) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }

        private sealed class FixedPolicy : ITypingTargetDifficultyPolicy
        {
            private readonly double _speed;
            private readonly int _interval;
            public FixedPolicy(double speed, int interval) { _speed = speed; _interval = interval; }
            public int SpawnIntervalMs(TypingTarget target, int queuedCount, int activeCount) { return _interval; }
            public double SpeedPerSecond(TypingTarget target, TypingTargetMovementKind movementKind) { return _speed; }
        }

        private sealed class FastPolicy : ITypingTargetDifficultyPolicy
        {
            public int SpawnIntervalMs(TypingTarget target, int queuedCount, int activeCount) { return 100; }
            public double SpeedPerSecond(TypingTarget target, TypingTargetMovementKind movementKind) { return 0.04; }
        }
    }
}
