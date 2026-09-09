using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WAHU.Content;

namespace WAHU.TypingSpaceContentRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        private static void Main()
        {
            var root = FindProjectRoot();
            var path = Path.Combine(root, "content_packs", "typing_space_grade2_v1", "typing_content_v1.json");
            var source = new TypingSpaceContentSource();
            var pack = source.Load(path);
            var selector = new TypingSpaceAdaptiveSelector();

            Assert(pack.entries.Count >= 52, "dataset_size");
            Assert(pack.entries.Count(x => x.target.language == "en" && x.stage != "single") >= 20, "english_20_plus_words");
            Assert(pack.entries.Count(x => x.target.language == "vi" && x.stage != "single") >= 20, "vietnamese_20_plus_words");
            Assert(pack.entries.Count(x => x.target.kind == "boss" && x.target.language == "en") >= 2, "english_boss_vocab");
            Assert(pack.entries.Count(x => x.target.kind == "boss" && x.target.language == "vi") >= 2, "vietnamese_boss_vocab");
            Assert(new[] { "animals", "school", "family", "space", "colors", "numbers" }.All(tag => pack.entries.Any(x => x.tags.Contains(tag))), "topic_coverage");
            Assert(pack.entries.Where(x => x.target.language == "vi").All(x => x.target.acceptedInputs.Count >= 1), "vi_accepted_inputs_present");
            Assert(!pack.adaptivePolicy.speedPressureAllowed, "no_speed_pressure");

            var steady = selector.Select(pack, Context("en", 2, 0.10, 0, 2, 100, 1));
            Assert(steady.Entry.target.language == "en", "steady_language");
            Assert(steady.Entry.target.kind != "boss", "normal_round_avoids_boss");
            Assert(steady.RecommendedHintLevel == 1 && !steady.ReducedChallenge, "steady_hint_level");

            var strained = selector.Select(pack, Context("en", 3, 0.50, 4, 0, 101, 2));
            Assert(strained.ReducedChallenge, "errors_reduce_challenge");
            Assert(strained.RecommendedHintLevel == 2, "errors_increase_hint");
            Assert(strained.Entry.target.difficulty <= 2, "errors_step_down_at_most_one_level");
            Assert(strained.Entry.target.displayText.Replace(" ", string.Empty).Length <= 3, "errors_choose_short_word");

            var stable = selector.Select(pack, Context("en", 2, 0.0, 0, 6, 102, 3));
            Assert(stable.Entry.target.difficulty <= 3, "stable_never_jumps_more_than_one_level");
            Assert(stable.RecommendedHintLevel == 0, "stable_reduces_hint");
            Assert(stable.Reason == "stable_correct:gentle_step_up", "stable_reason_auditable");

            var first = selector.Select(pack, Context("vi", 1, 0.0, 0, 1, 77, 4));
            var recent = Context("vi", 1, 0.0, 0, 1, 77, 4);
            recent.RecentTargetIds = new List<string> { first.Entry.target.id };
            var second = selector.Select(pack, recent);
            Assert(second.Entry.target.id != first.Entry.target.id, "hard_anti_repeat_when_alternative_exists");

            var deterministicA = selector.Select(pack, Context("en", 2, 0.0, 0, 1, 900, 8));
            var deterministicB = selector.Select(pack, Context("en", 2, 0.0, 0, 1, 900, 8));
            Assert(deterministicA.Entry.target.id == deterministicB.Entry.target.id, "same_context_is_deterministic");

            var school = Context("en", 2, 0.0, 0, 1, 34, 9);
            school.PreferredTag = "school";
            var schoolDecision = selector.Select(pack, school);
            Assert(schoolDecision.Entry.tags.Contains("school"), "preferred_tag_when_available");

            var boss = Context("vi", 4, 0.0, 0, 5, 55, 10);
            boss.BossRound = true;
            var bossDecision = selector.Select(pack, boss);
            Assert(bossDecision.Entry.target.kind == "boss" && bossDecision.Entry.target.language == "vi", "boss_round_selects_boss_vocab");

            TestMalformedFailsClosed(source, path);
            Console.WriteLine("TYPING_SPACE_CONTENT_RUNTIME_SMOKE_PASS assertions=" + _assertions);
        }

        private static void TestMalformedFailsClosed(TypingSpaceContentSource source, string path)
        {
            var original = File.ReadAllText(path);
            var temp = Path.Combine(Path.GetTempPath(), "typing-space-content-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(temp, original.Replace("\"speedPressureAllowed\": false", "\"speedPressureAllowed\": true"));
                AssertInvalidData(delegate { source.Load(temp); }, "speed_pressure_policy_rejected");
                File.WriteAllText(temp, original.Replace("\"language\":\"en\"", "\"language\":\"xx\""));
                AssertInvalidData(delegate { source.Load(temp); }, "unsupported_language_rejected");
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        private static TypingSpaceAdaptiveContext Context(string language, int difficulty, double errorRate, int errors, int correct, int seed, int ordinal)
        {
            return new TypingSpaceAdaptiveContext
            {
                Language = language,
                CurrentDifficulty = difficulty,
                RecentErrorRate = errorRate,
                RecentErrors = errors,
                ConsecutiveCorrect = correct,
                RecentTargetIds = new List<string>(),
                Seed = seed,
                Ordinal = ordinal
            };
        }

        private static void AssertInvalidData(Action action, string name)
        {
            var rejected = false;
            try { action(); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected, name);
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
