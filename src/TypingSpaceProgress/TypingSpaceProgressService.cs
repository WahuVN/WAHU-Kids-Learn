using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace WAHU.TypingSpace.Progress
{
    public sealed class TypingSpaceSaveState
    {
        public int SchemaVersion { get; set; }
        public string ChildId { get; set; }
        public string RunId { get; set; }
        public string LevelId { get; set; }
        public string Language { get; set; }
        public string Mode { get; set; }
        public int Difficulty { get; set; }
        public int TargetIndex { get; set; }
        public string TargetId { get; set; }
        public int TypedCount { get; set; }
        public int ErrorCount { get; set; }
        public int Stars { get; set; }
        public int Energy { get; set; }
        public bool Completed { get; set; }
        public long UpdatedAtUtcMs { get; set; }
        public IList<string> GrantedRewardKeys { get; set; }
    }

    public sealed class TypingSpaceReward
    {
        public string RewardKey { get; set; }
        public int Stars { get; set; }
        public int Energy { get; set; }
        public int GardenGrowth { get; set; }
        public bool NewlyGranted { get; set; }
    }

    public interface ITypingSpaceGardenProgressAdapter
    {
        void ApplyGrowth(string childId, string rewardKey, int growth);
    }

    public sealed class NullGardenProgressAdapter : ITypingSpaceGardenProgressAdapter
    {
        public void ApplyGrowth(string childId, string rewardKey, int growth) { }
    }

    public sealed class TypingSpaceProgressService
    {
        private const int CurrentSchema = 1;
        private readonly string _root;
        private readonly ITypingSpaceGardenProgressAdapter _garden;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private readonly object _gate = new object();

        public TypingSpaceProgressService(string root, ITypingSpaceGardenProgressAdapter garden)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("root is required.", "root");
            _root = Path.GetFullPath(root);
            _garden = garden ?? new NullGardenProgressAdapter();
            Directory.CreateDirectory(_root);
        }

        public TypingSpaceSaveState StartOrResume(string childId, string runId, string levelId, string language, string mode, int difficulty)
        {
            ValidateIdentity(childId, runId, levelId);
            ValidatePreferences(language, mode, difficulty);
            lock (_gate)
            {
                var existing = LoadInternal(childId, runId);
                if (existing != null) return existing;
                var state = new TypingSpaceSaveState
                {
                    SchemaVersion = CurrentSchema, ChildId = childId, RunId = runId, LevelId = levelId,
                    Language = language, Mode = mode, Difficulty = difficulty, TargetIndex = 0,
                    Stars = 0, Energy = 0, Completed = false, UpdatedAtUtcMs = NowMs(),
                    GrantedRewardKeys = new List<string>()
                };
                SaveInternal(state);
                return Clone(state);
            }
        }

        public void SaveCheckpoint(TypingSpaceSaveState state)
        {
            if (state == null) throw new ArgumentNullException("state");
            ValidateIdentity(state.ChildId, state.RunId, state.LevelId);
            ValidatePreferences(state.Language, state.Mode, state.Difficulty);
            if (state.TargetIndex < 0 || state.TypedCount < 0 || state.ErrorCount < 0 || state.Stars < 0 || state.Energy < 0)
                throw new ArgumentOutOfRangeException("state", "Progress values cannot be negative.");
            lock (_gate)
            {
                var durable = LoadInternal(state.ChildId, state.RunId);
                if (durable != null && durable.Completed && !state.Completed)
                    throw new InvalidOperationException("A completed run cannot be reopened by a stale checkpoint.");
                state.SchemaVersion = CurrentSchema;
                state.UpdatedAtUtcMs = NowMs();
                state.GrantedRewardKeys = MergeKeys(durable, state);
                SaveInternal(state);
            }
        }

        public TypingSpaceReward HandleLevelCompleted(string childId, string runId, int rewardValue)
        {
            if (rewardValue < 0) throw new ArgumentOutOfRangeException("rewardValue");
            lock (_gate)
            {
                var state = LoadInternal(childId, runId);
                if (state == null) throw new InvalidOperationException("Typing run does not exist.");
                var key = "typing-space:level-completed:" + state.LevelId + ":" + state.RunId;
                var keys = new HashSet<string>(state.GrantedRewardKeys ?? new List<string>(), StringComparer.Ordinal);
                if (keys.Contains(key))
                    return new TypingSpaceReward { RewardKey = key, Stars = 0, Energy = 0, GardenGrowth = 0, NewlyGranted = false };

                var stars = Math.Max(1, Math.Min(3, rewardValue));
                var energy = Math.Max(1, rewardValue);
                const int gardenGrowth = 1;
                state.Completed = true;
                state.Stars += stars;
                state.Energy += energy;
                keys.Add(key);
                state.GrantedRewardKeys = keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
                state.UpdatedAtUtcMs = NowMs();
                SaveInternal(state);

                // Garden is deliberately downstream of the durable idempotency marker. The adapter
                // receives the same stable key and must also be idempotent; a retry cannot mint local reward twice.
                _garden.ApplyGrowth(state.ChildId, key, gardenGrowth);
                return new TypingSpaceReward { RewardKey = key, Stars = stars, Energy = energy, GardenGrowth = gardenGrowth, NewlyGranted = true };
            }
        }

        public TypingSpaceSaveState Load(string childId, string runId)
        {
            lock (_gate) { return Clone(LoadInternal(childId, runId)); }
        }

        public TypingSpaceSaveState ReadLastPreferences(string childId)
        {
            if (string.IsNullOrWhiteSpace(childId)) throw new ArgumentException("childId is required.", "childId");
            lock (_gate)
            {
                var prefix = Safe(childId) + "__";
                var candidates = Directory.GetFiles(_root, prefix + "*.json")
                    .Select(ReadFileSafe).Where(x => x != null)
                    .OrderByDescending(x => x.UpdatedAtUtcMs).ToList();
                return candidates.Count == 0 ? null : Clone(candidates[0]);
            }
        }

        private TypingSpaceSaveState LoadInternal(string childId, string runId)
        {
            if (string.IsNullOrWhiteSpace(childId) || string.IsNullOrWhiteSpace(runId)) return null;
            return ReadFileSafe(PathFor(childId, runId));
        }

        private TypingSpaceSaveState ReadFileSafe(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var state = _json.Deserialize<TypingSpaceSaveState>(File.ReadAllText(path));
                if (state == null || state.SchemaVersion != CurrentSchema) return null;
                state.GrantedRewardKeys = state.GrantedRewardKeys ?? new List<string>();
                return state;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException)
            {
                return null;
            }
        }

        private void SaveInternal(TypingSpaceSaveState state)
        {
            var path = PathFor(state.ChildId, state.RunId);
            var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temp, _json.Serialize(state));
            try
            {
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        private string PathFor(string childId, string runId) { return Path.Combine(_root, Safe(childId) + "__" + Safe(runId) + ".json"); }
        private static string Safe(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Identifier is required.");
            var invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
        private static IList<string> MergeKeys(TypingSpaceSaveState durable, TypingSpaceSaveState incoming)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (durable != null && durable.GrantedRewardKeys != null) foreach (var x in durable.GrantedRewardKeys) keys.Add(x);
            if (incoming.GrantedRewardKeys != null) foreach (var x in incoming.GrantedRewardKeys) keys.Add(x);
            return keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
        }
        private TypingSpaceSaveState Clone(TypingSpaceSaveState value) { return value == null ? null : _json.Deserialize<TypingSpaceSaveState>(_json.Serialize(value)); }
        private static long NowMs() { return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }
        private static void ValidateIdentity(string childId, string runId, string levelId)
        {
            if (string.IsNullOrWhiteSpace(childId)) throw new ArgumentException("childId is required.");
            if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("runId is required.");
            if (string.IsNullOrWhiteSpace(levelId)) throw new ArgumentException("levelId is required.");
        }
        private static void ValidatePreferences(string language, string mode, int difficulty)
        {
            if (language != "vi" && language != "en") throw new ArgumentException("language must be vi/en.");
            if (string.IsNullOrWhiteSpace(mode)) throw new ArgumentException("mode is required.");
            if (difficulty < 1) throw new ArgumentOutOfRangeException("difficulty");
        }
    }
}
