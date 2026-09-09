using System;
using System.Diagnostics;
using System.Linq;
using WAHU.TypingSpace;

namespace WAHU.TypingSpaceIntegrationRuntimeSmoke
{
    internal static class Program
    {
        private static int _a;
        private static void Main()
        {
            var events = new InMemoryEventSink();
            var rewards = new IdempotentMockRewardAdapter();
            var shell = new TypingSpaceIntegrationShell(events, rewards, new MockAssetAdapter(), TypingSpaceMockFactory.CreateV1Flow());

            shell.Start();
            A(shell.Phase == GamePhase.Playing && shell.CurrentTarget.Id == "en-cat", "entry_game");
            Type(shell, "cat");
            A(events.Events.Any(x => x.Type == "TYPING_WORD_COMPLETED" && x.TargetId == "en-cat"), "english_target_complete");

            A(shell.CurrentTarget.Id == "vi-meo" && shell.CurrentTarget.DisplayText == "mèo", "vietnamese_display");
            A(!shell.Input(I("x")) && shell.Progress.ErrorCount == 1, "wrong_retry_no_lock");
            Type(shell, "meo");
            A(events.Events.Any(x => x.Type == "TARGET_RESCUED" && x.TargetId == "vi-meo"), "vietnamese_no_accent_accepted");

            A(shell.CurrentTarget.Id == "en-key", "multiple_targets_lock_sequence");
            shell.Pause();
            A(shell.Phase == GamePhase.Paused && !shell.Input(I("k")), "pause_blocks_input");
            shell.Resume();
            A(shell.Phase == GamePhase.Playing, "resume_restores_phase");
            Type(shell, "key");

            A(shell.Phase == GamePhase.Boss && shell.CurrentTarget.Id == "boss-1", "boss_enter");
            Type(shell, "star"); Type(shell, "moon"); Type(shell, "sun");
            A(events.Events.Count(x => x.Type == "BOSS_HIT") == 3, "boss_three_phases");
            A(events.Events.Count(x => x.Type == "BOSS_DEFEATED") == 1, "boss_defeated_once");
            A(shell.Phase == GamePhase.Complete, "level_complete");
            A(rewards.GrantCount == 1 && events.Events.Count(x => x.Type == "REWARD_GRANTED") == 1, "reward_once");
            shell.InjectDuplicateRewardEventForQa();
            A(rewards.GrantCount == 1 && events.Events.Count(x => x.Type == "REWARD_GRANTED") == 1, "duplicate_reward_blocked");
            A(shell.SceneAsset.StartsWith("mock://"), "asset_adapter_present");
            A(new MockAssetAdapter().ResolveOrFallback(null) == "fallback://placeholder", "missing_asset_fallback");

            shell.Replay();
            A(shell.Phase == GamePhase.Playing && shell.CurrentTarget.Id == "en-cat", "replay_after_complete");

            var sw = Stopwatch.StartNew();
            for (var i = 0; i < 20000; i++)
            {
                var e = new InMemoryEventSink();
                var r = new IdempotentMockRewardAdapter();
                var s = new TypingSpaceIntegrationShell(e, r, new MockAssetAdapter(), TypingSpaceMockFactory.CreateV1Flow());
                s.Start(); Type(s, "cat"); Type(s, "meo"); Type(s, "key"); Type(s, "star"); Type(s, "moon"); Type(s, "sun");
                if (s.Phase != GamePhase.Complete || r.GrantCount != 1) throw new Exception("LONG_SESSION_FAIL");
            }
            sw.Stop();
            A(sw.Elapsed.TotalSeconds < 30, "virtual_long_session_performance_sanity");

            Console.WriteLine("TYPING_SPACE_INTEGRATION_RUNTIME_SMOKE_PASS assertions=" + _a + " events=" + events.Events.Count + " perf_ms=" + sw.ElapsedMilliseconds);
        }

        private static void Type(TypingSpaceIntegrationShell shell, string text)
        {
            foreach (var c in text) if (!shell.Input(I(c.ToString()))) throw new Exception("TYPE_FAIL: " + text + " @ " + c);
        }
        private static TypingInput I(string key) { return new TypingInput { RawKey = key, NormalizedKey = key, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Source = "physical" }; }
        private static void A(bool ok, string name) { if (!ok) throw new Exception("ASSERT_FAIL: " + name); _a++; }
    }
}
