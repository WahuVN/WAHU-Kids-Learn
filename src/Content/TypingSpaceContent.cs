using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace WAHU.Content
{
    public sealed class TypingSpaceTarget
    {
        public string id { get; set; }
        public string displayText { get; set; }
        public IList<string> acceptedInputs { get; set; }
        public string language { get; set; }
        public int difficulty { get; set; }
        public string kind { get; set; }
        public int rewardValue { get; set; }
    }

    public sealed class TypingSpaceContentEntry
    {
        public TypingSpaceTarget target { get; set; }
        public IList<string> tags { get; set; }
        public string stage { get; set; }
        public string hintText { get; set; }
    }

    public sealed class TypingSpaceAdaptivePolicy
    {
        public int recentWindow { get; set; }
        public double strainedErrorRate { get; set; }
        public int strainedRecentErrors { get; set; }
        public int stableCorrectStreak { get; set; }
        public int maxDifficultyStep { get; set; }
        public bool speedPressureAllowed { get; set; }
    }

    public sealed class TypingSpaceContentPack
    {
        public int schemaVersion { get; set; }
        public string packId { get; set; }
        public int grade { get; set; }
        public TypingSpaceAdaptivePolicy adaptivePolicy { get; set; }
        public IList<TypingSpaceContentEntry> entries { get; set; }
    }

    public sealed class TypingSpaceAdaptiveContext
    {
        public string Language { get; set; }
        public int CurrentDifficulty { get; set; }
        public double RecentErrorRate { get; set; }
        public int RecentErrors { get; set; }
        public int ConsecutiveCorrect { get; set; }
        public IList<string> RecentTargetIds { get; set; }
        public string PreferredTag { get; set; }
        public bool BossRound { get; set; }
        public int Seed { get; set; }
        public int Ordinal { get; set; }
    }

    public sealed class TypingSpaceSelectionDecision
    {
        public TypingSpaceContentEntry Entry { get; set; }
        public int RecommendedHintLevel { get; set; }
        public string HintText { get; set; }
        public bool ReducedChallenge { get; set; }
        public bool IncreasedChallenge { get; set; }
        public string Reason { get; set; }
    }

    public sealed class TypingSpaceContentSource
    {
        private static readonly HashSet<string> AllowedLanguages = new HashSet<string>(new[] { "vi", "en" }, StringComparer.Ordinal);
        private static readonly HashSet<string> AllowedKinds = new HashSet<string>(new[] { "shoot", "rescue", "unlock", "boss" }, StringComparer.Ordinal);
        private static readonly HashSet<string> AllowedStages = new HashSet<string>(new[] { "single", "short", "medium", "phrase", "boss" }, StringComparer.Ordinal);
        private static readonly HashSet<string> AllowedTags = new HashSet<string>(new[] { "animals", "school", "family", "space", "colors", "numbers" }, StringComparer.Ordinal);
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024, RecursionLimit = 64 };

        public TypingSpaceContentPack Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path");
            if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy Typing Space content.", path);
            TypingSpaceContentPack pack;
            try { pack = _json.Deserialize<TypingSpaceContentPack>(File.ReadAllText(path)); }
            catch (Exception ex) { throw new InvalidDataException("Typing Space JSON invalid.", ex); }
            Validate(pack);
            return pack;
        }

        public static void Validate(TypingSpaceContentPack pack)
        {
            if (pack == null) throw new InvalidDataException("Typing Space pack missing.");
            if (pack.schemaVersion != 1 || !string.Equals(pack.packId, "typing_space_grade2_v1", StringComparison.Ordinal) || pack.grade != 2)
                throw new InvalidDataException("Typing Space pack identity invalid.");
            if (pack.adaptivePolicy == null || pack.adaptivePolicy.recentWindow < 2 || pack.adaptivePolicy.recentWindow > 8 ||
                pack.adaptivePolicy.strainedErrorRate <= 0 || pack.adaptivePolicy.strainedErrorRate > 1 ||
                pack.adaptivePolicy.strainedRecentErrors < 1 || pack.adaptivePolicy.stableCorrectStreak < 2 ||
                pack.adaptivePolicy.maxDifficultyStep != 1 || pack.adaptivePolicy.speedPressureAllowed)
                throw new InvalidDataException("Typing Space adaptive policy violates child-safe V1 rules.");
            if (pack.entries == null || pack.entries.Count < 44) throw new InvalidDataException("Typing Space V1 requires at least 44 entries.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var viCount = 0;
            var enCount = 0;
            var viBoss = 0;
            var enBoss = 0;
            var tagCoverage = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in pack.entries)
            {
                if (entry == null || entry.target == null) throw new InvalidDataException("Typing Space entry/target missing.");
                var t = entry.target;
                if (string.IsNullOrWhiteSpace(t.id) || !ids.Add(t.id)) throw new InvalidDataException("Typing Space target id missing/duplicate.");
                if (string.IsNullOrWhiteSpace(t.displayText)) throw new InvalidDataException("Typing Space displayText missing: " + t.id);
                if (t.acceptedInputs == null || t.acceptedInputs.Count == 0 || t.acceptedInputs.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidDataException("Typing Space acceptedInputs missing: " + t.id);
                if (!AllowedLanguages.Contains(t.language)) throw new InvalidDataException("Typing Space language invalid: " + t.id);
                if (t.difficulty < 1 || t.difficulty > 4) throw new InvalidDataException("Typing Space difficulty invalid: " + t.id);
                if (!AllowedKinds.Contains(t.kind)) throw new InvalidDataException("Typing Space kind invalid: " + t.id);
                if (t.rewardValue < 1 || t.rewardValue > 20) throw new InvalidDataException("Typing Space reward invalid: " + t.id);
                if (!AllowedStages.Contains(entry.stage) || string.IsNullOrWhiteSpace(entry.hintText)) throw new InvalidDataException("Typing Space metadata invalid: " + t.id);
                if (entry.tags == null || entry.tags.Count == 0 || entry.tags.Any(x => !AllowedTags.Contains(x))) throw new InvalidDataException("Typing Space tags invalid: " + t.id);
                foreach (var tag in entry.tags) tagCoverage.Add(tag);
                if (t.language == "vi")
                {
                    viCount++;
                    if (!t.acceptedInputs.Any(x => string.Equals(x, RemoveVietnameseMarks(t.displayText), StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException("Vietnamese target needs unaccented accepted input: " + t.id);
                    if (t.kind == "boss") viBoss++;
                }
                else
                {
                    enCount++;
                    if (!t.acceptedInputs.Any(x => string.Equals(x, t.displayText, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException("English target needs displayText accepted input: " + t.id);
                    if (t.kind == "boss") enBoss++;
                }
            }
            if (viCount < 20 || enCount < 20) throw new InvalidDataException("Typing Space V1 requires 20+ words per language.");
            if (viBoss < 2 || enBoss < 2) throw new InvalidDataException("Typing Space V1 needs boss vocabulary in both languages.");
            if (AllowedTags.Any(x => !tagCoverage.Contains(x))) throw new InvalidDataException("Typing Space topic coverage incomplete.");
        }

        internal static string RemoveVietnameseMarks(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            var chars = normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray();
            return new string(chars).Replace('đ', 'd').Replace('Đ', 'D').Normalize(System.Text.NormalizationForm.FormC);
        }
    }

    public sealed class TypingSpaceAdaptiveSelector
    {
        public const string EngineVersion = "typing-space-adaptive-v1";

        public TypingSpaceSelectionDecision Select(TypingSpaceContentPack pack, TypingSpaceAdaptiveContext context)
        {
            if (pack == null) throw new ArgumentNullException("pack");
            if (context == null) throw new ArgumentNullException("context");
            TypingSpaceContentSource.Validate(pack);
            if (context.Language != "vi" && context.Language != "en") throw new ArgumentException("Language must be vi or en.");

            var policy = pack.adaptivePolicy;
            var recent = (context.RecentTargetIds ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).TakeLastCompat(policy.recentWindow).ToList();
            var strained = context.RecentErrorRate >= policy.strainedErrorRate || context.RecentErrors >= policy.strainedRecentErrors;
            var stable = !strained && context.ConsecutiveCorrect >= policy.stableCorrectStreak;
            var baseDifficulty = Math.Max(1, Math.Min(4, context.CurrentDifficulty <= 0 ? 1 : context.CurrentDifficulty));
            var targetDifficulty = strained ? Math.Max(1, baseDifficulty - policy.maxDifficultyStep) : stable ? Math.Min(4, baseDifficulty + policy.maxDifficultyStep) : baseDifficulty;
            var maxLength = strained ? 3 : targetDifficulty == 1 ? 3 : targetDifficulty == 2 ? 5 : targetDifficulty == 3 ? 8 : 16;
            var hintLevel = strained ? 2 : stable ? 0 : 1;

            var candidates = pack.entries.Where(x => x.target.language == context.Language &&
                (context.BossRound ? x.target.kind == "boss" : x.target.kind != "boss") &&
                x.target.difficulty <= targetDifficulty && VisibleLength(x.target.displayText) <= maxLength).ToList();
            if (candidates.Count == 0)
                candidates = pack.entries.Where(x => x.target.language == context.Language && (context.BossRound ? x.target.kind == "boss" : x.target.kind != "boss")).ToList();
            if (candidates.Count == 0) throw new InvalidOperationException("No Typing Space candidate for requested language/round.");

            var unseen = candidates.Where(x => !recent.Contains(x.target.id, StringComparer.Ordinal)).ToList();
            if (unseen.Count > 0) candidates = unseen;
            else if (recent.Count > 0)
            {
                var oldest = candidates.Min(x => LastIndex(recent, x.target.id));
                candidates = candidates.Where(x => LastIndex(recent, x.target.id) == oldest).ToList();
            }

            if (!string.IsNullOrWhiteSpace(context.PreferredTag))
            {
                var tagged = candidates.Where(x => x.tags.Contains(context.PreferredTag, StringComparer.Ordinal)).ToList();
                if (tagged.Count > 0) candidates = tagged;
            }

            var bestDistance = candidates.Min(x => Math.Abs(x.target.difficulty - targetDifficulty));
            candidates = candidates.Where(x => Math.Abs(x.target.difficulty - targetDifficulty) == bestDistance)
                .OrderBy(x => x.target.id, StringComparer.Ordinal).ToList();
            var selected = candidates[PositiveMod(StableHash(context.Seed, context.Ordinal, context.Language, targetDifficulty), candidates.Count)];
            return new TypingSpaceSelectionDecision
            {
                Entry = selected,
                RecommendedHintLevel = hintLevel,
                HintText = selected.hintText,
                ReducedChallenge = strained,
                IncreasedChallenge = stable && selected.target.difficulty > baseDifficulty,
                Reason = strained ? "errors_high:shorter_more_hint" : stable ? "stable_correct:gentle_step_up" : "steady:keep_level"
            };
        }

        private static int VisibleLength(string text) { return string.IsNullOrEmpty(text) ? 0 : text.Replace(" ", string.Empty).Length; }
        private static int LastIndex(IList<string> values, string value) { for (var i = values.Count - 1; i >= 0; i--) if (values[i] == value) return i; return -1; }
        private static int PositiveMod(int value, int count) { var x = value % count; return x < 0 ? x + count : x; }
        private static int StableHash(int seed, int ordinal, string language, int difficulty)
        {
            unchecked
            {
                var h = 17;
                h = h * 31 + seed;
                h = h * 31 + ordinal;
                h = h * 31 + difficulty;
                foreach (var c in language) h = h * 31 + c;
                return h;
            }
        }
    }

    internal static class TypingSpaceEnumerableCompat
    {
        public static IEnumerable<T> TakeLastCompat<T>(this IEnumerable<T> source, int count)
        {
            if (source == null || count <= 0) yield break;
            var queue = new Queue<T>();
            foreach (var item in source)
            {
                queue.Enqueue(item);
                if (queue.Count > count) queue.Dequeue();
            }
            foreach (var item in queue) yield return item;
        }
    }
}
