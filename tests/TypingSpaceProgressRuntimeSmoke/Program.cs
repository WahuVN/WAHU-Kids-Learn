using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WAHU.TypingSpace.Progress;

internal static class Program
{
    private static int _assertions;
    private sealed class Garden : ITypingSpaceGardenProgressAdapter
    {
        private readonly HashSet<string> _keys = new HashSet<string>(StringComparer.Ordinal);
        public int Growth { get; private set; }
        public void ApplyGrowth(string childId, string rewardKey, int growth) { lock (_keys) if (_keys.Add(childId + ":" + rewardKey)) Growth += growth; }
    }

    private static void Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "wahu-typing-ai09-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var garden = new Garden();
            var service = new TypingSpaceProgressService(root, garden);
            var state = service.StartOrResume("child-1", "run-1", "level-1", "vi", "story", 2);
            Assert(!state.Completed && state.Language == "vi" && state.Difficulty == 2, "new_run_defaults_and_preferences");
            state.TargetIndex = 4; state.TargetId = "vi-meo"; state.TypedCount = 2; state.ErrorCount = 1;
            service.SaveCheckpoint(state);

            var reopened = new TypingSpaceProgressService(root, garden).StartOrResume("child-1", "run-1", "ignored", "en", "other", 5);
            Assert(reopened.TargetIndex == 4 && reopened.TargetId == "vi-meo" && reopened.TypedCount == 2 && reopened.ErrorCount == 1, "resume_restores_progress");
            Assert(reopened.Language == "vi" && reopened.Mode == "story" && reopened.Difficulty == 2, "resume_preserves_preferences");

            var first = service.HandleLevelCompleted("child-1", "run-1", 3);
            Assert(first.NewlyGranted && first.Stars == 3 && first.Energy == 3 && first.GardenGrowth == 1, "level_completed_grants_deterministic_reward");
            var duplicate = service.HandleLevelCompleted("child-1", "run-1", 3);
            Assert(!duplicate.NewlyGranted && duplicate.Stars == 0 && duplicate.Energy == 0, "duplicate_level_completed_is_idempotent");
            Assert(garden.Growth == 1, "duplicate_event_does_not_duplicate_garden_growth");

            var afterRestart = new TypingSpaceProgressService(root, garden);
            var restartDuplicate = afterRestart.HandleLevelCompleted("child-1", "run-1", 3);
            Assert(!restartDuplicate.NewlyGranted && garden.Growth == 1, "reopen_does_not_duplicate_reward");
            var completed = afterRestart.Load("child-1", "run-1");
            Assert(completed.Completed && completed.Stars == 3 && completed.Energy == 3 && completed.GrantedRewardKeys.Count == 1, "completed_state_is_durable");

            var stale = state; stale.Completed = false;
            var staleRejected = false;
            try { afterRestart.SaveCheckpoint(stale); } catch (InvalidOperationException) { staleRejected = true; }
            Assert(staleRejected, "stale_checkpoint_cannot_reopen_completed_run");

            var run2 = afterRestart.StartOrResume("child-1", "run-2", "level-2", "en", "practice", 4);
            Assert(run2.Language == "en" && run2.Difficulty == 4, "new_run_can_change_preferences");
            var prefs = afterRestart.ReadLastPreferences("child-1");
            Assert(prefs != null && prefs.RunId == "run-2" && prefs.Language == "en", "last_preferences_restored");

            var concurrent = afterRestart.StartOrResume("child-2", "run-concurrent", "level-c", "en", "practice", 1);
            var tasks = Enumerable.Range(0, 12).Select(_ => Task.Run(() => afterRestart.HandleLevelCompleted("child-2", "run-concurrent", 2))).ToArray();
            Task.WaitAll(tasks);
            Assert(tasks.Count(x => x.Result.NewlyGranted) == 1, "concurrent_duplicate_level_completed_has_one_grant");
            var c = afterRestart.Load("child-2", "run-concurrent");
            Assert(c.Stars == 2 && c.Energy == 2 && c.GrantedRewardKeys.Count == 1, "concurrent_reward_persisted_once");

            File.WriteAllText(Path.Combine(root, "child-corrupt__run-x.json"), "{not-json");
            Assert(afterRestart.Load("child-corrupt", "run-x") == null, "corrupt_save_fails_safe_without_crash");
            Console.WriteLine("TYPING_SPACE_PROGRESS_RUNTIME_SMOKE_PASS assertions=" + _assertions);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    private static void Assert(bool value, string name) { if (!value) throw new Exception("ASSERT_FAIL: " + name); _assertions++; }
}
