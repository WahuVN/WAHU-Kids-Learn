using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace WAHU.Learning
{
    public sealed class MathQuickRescueQuestionOption
    {
        public string QuestionId { get; set; }
        public string Variant { get; set; }
    }

    public sealed class MathQuickRescueCommonError
    {
        public string Id { get; set; }
        public string CueVi { get; set; }
        public string RepairVi { get; set; }
    }

    public sealed class MathQuickRescueCheckpoint
    {
        public int Number { get; set; }
        public string Difficulty { get; set; }
        public string LabelVi { get; set; }
        public string GoalVi { get; set; }
        public IList<MathQuickRescueQuestionOption> QuestionOptions { get; set; }
        public string HintLevel1Vi { get; set; }
        public string HintLevel2Vi { get; set; }
        public string RepairVi { get; set; }
        public IList<MathQuickRescueCommonError> CommonErrors { get; set; }
    }

    public sealed class MathQuickRescueAdaptiveRule
    {
        public string State { get; set; }
        public string PreferredVariant { get; set; }
        public int HintLevel { get; set; }
        public bool UseRepair { get; set; }
        public bool OfferBreak { get; set; }
        public bool MinimalFeedback { get; set; }
        public bool SelectQuestion { get; set; }
    }

    public sealed class MathQuickRescueAntiRepeatPolicy
    {
        public int RecentQuestionWindow { get; set; }
        public bool PreferUnseen { get; set; }
    }

    public sealed class MathQuickRescueFeedback
    {
        public string ReadyVi { get; set; }
        public string CorrectVi { get; set; }
        public string WrongVi { get; set; }
        public string BreakVi { get; set; }
    }

    public sealed class MathQuickRescueLearningPack
    {
        public int SchemaVersion { get; set; }
        public string PackId { get; set; }
        public string Subject { get; set; }
        public int Grade { get; set; }
        public string Language { get; set; }
        public string TargetLessonId { get; set; }
        public string TargetSkillId { get; set; }
        public int CheckpointCount { get; set; }
        public IList<string> Pacing { get; set; }
        public MathQuickRescueAntiRepeatPolicy AntiRepeat { get; set; }
        public MathQuickRescueFeedback Feedback { get; set; }
        public IList<MathQuickRescueAdaptiveRule> AdaptiveRules { get; set; }
        public IList<MathQuickRescueCheckpoint> Checkpoints { get; set; }

        public MathQuickRescueCheckpoint GetCheckpoint(int oneBasedCheckpoint)
        {
            return (Checkpoints ?? new List<MathQuickRescueCheckpoint>())
                .FirstOrDefault(x => x.Number == oneBasedCheckpoint);
        }

        public MathQuickRescueAdaptiveRule GetRule(BehaviorState state)
        {
            var name = state.ToString();
            return (AdaptiveRules ?? new List<MathQuickRescueAdaptiveRule>())
                .FirstOrDefault(x => string.Equals(x.State, name, StringComparison.Ordinal));
        }
    }

    public sealed class MathQuickRescueContentDecision
    {
        public int CheckpointNumber { get; set; }
        public string Difficulty { get; set; }
        public string QuestionId { get; set; }
        public string Variant { get; set; }
        public int RecommendedHintLevel { get; set; }
        public bool UseRepair { get; set; }
        public bool OfferBreak { get; set; }
        public bool MinimalFeedback { get; set; }
        public string SupportVi { get; set; }
        public string Reason { get; set; }
    }

    public sealed class MathQuickRescueErrorSupportDecision
    {
        public int CheckpointNumber { get; set; }
        public string ErrorId { get; set; }
        public bool KnownError { get; set; }
        public string CueVi { get; set; }
        public string RepairVi { get; set; }
        public int RecommendedHintLevel { get; set; }
        public bool UseRepair { get; set; }
        public bool OfferBreak { get; set; }
        public string RecommendedCopyVi { get; set; }
        public string Reason { get; set; }
    }

    public sealed class MathQuickRescueContentSource
    {
        private static readonly HashSet<string> AllowedVariants = new HashSet<string>(StringComparer.Ordinal)
        {
            "support", "transfer"
        };

        private static readonly HashSet<string> AllowedPreferredVariants = new HashSet<string>(StringComparer.Ordinal)
        {
            "any", "support", "transfer"
        };

        private readonly JavaScriptSerializer _json = new JavaScriptSerializer
        {
            MaxJsonLength = 1024 * 1024,
            RecursionLimit = 64
        };

        public MathQuickRescueLearningPack Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path");
            if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy Quick Rescue learning content.", path);

            RootDto root;
            try { root = _json.Deserialize<RootDto>(File.ReadAllText(path)); }
            catch (Exception ex) { throw new InvalidDataException("Quick Rescue learning content JSON invalid.", ex); }
            if (root == null) throw new InvalidDataException("Quick Rescue learning content root invalid.");

            var pack = new MathQuickRescueLearningPack
            {
                SchemaVersion = root.schema_version,
                PackId = Trim(root.pack_id),
                Subject = Trim(root.subject),
                Grade = root.grade,
                Language = Trim(root.language),
                TargetLessonId = Trim(root.target_lesson_id),
                TargetSkillId = Trim(root.target_skill_id),
                CheckpointCount = root.checkpoint_count,
                Pacing = Copy(root.pacing),
                AntiRepeat = root.anti_repeat == null ? null : new MathQuickRescueAntiRepeatPolicy
                {
                    RecentQuestionWindow = root.anti_repeat.recent_question_window,
                    PreferUnseen = root.anti_repeat.prefer_unseen
                },
                Feedback = root.feedback == null ? null : new MathQuickRescueFeedback
                {
                    ReadyVi = Trim(root.feedback.ready_vi),
                    CorrectVi = Trim(root.feedback.correct_vi),
                    WrongVi = Trim(root.feedback.wrong_vi),
                    BreakVi = Trim(root.feedback.break_vi)
                },
                AdaptiveRules = (root.adaptive_rules ?? new List<AdaptiveRuleDto>()).Select(MapRule).ToList(),
                Checkpoints = (root.checkpoints ?? new List<CheckpointDto>()).Select(MapCheckpoint).ToList()
            };
            Validate(pack);
            return pack;
        }

        public MathQuickRescueLearningPack Load(string path, IEnumerable<MathQuestion> authoredQuestions)
        {
            var pack = Load(path);
            ValidateAgainstAuthoredQuestions(pack, authoredQuestions);
            return pack;
        }

        public static void ValidateAgainstAuthoredQuestions(MathQuickRescueLearningPack pack, IEnumerable<MathQuestion> authoredQuestions)
        {
            if (pack == null) throw new ArgumentNullException("pack");
            if (authoredQuestions == null) throw new ArgumentNullException("authoredQuestions");

            var byId = new Dictionary<string, MathQuestion>(StringComparer.Ordinal);
            foreach (var question in authoredQuestions)
            {
                if (question == null || string.IsNullOrWhiteSpace(question.ContentQuestionId))
                    throw new InvalidDataException("Authored Math question used for Quick Rescue validation has no stable content id.");
                if (byId.ContainsKey(question.ContentQuestionId))
                    throw new InvalidDataException("Duplicate authored Math content id while validating Quick Rescue: " + question.ContentQuestionId);
                byId.Add(question.ContentQuestionId, question);
            }

            foreach (var checkpoint in pack.Checkpoints ?? new List<MathQuickRescueCheckpoint>())
            {
                foreach (var option in checkpoint.QuestionOptions ?? new List<MathQuickRescueQuestionOption>())
                {
                    MathQuestion question;
                    if (!byId.TryGetValue(option.QuestionId, out question))
                        throw new InvalidDataException("Quick Rescue references missing authored Math question: " + option.QuestionId);
                    if (!string.Equals(question.LessonId, pack.TargetLessonId, StringComparison.Ordinal))
                        throw new InvalidDataException("Quick Rescue authored question lesson mismatch: " + option.QuestionId);
                    if (!string.Equals(question.SkillId, pack.TargetSkillId, StringComparison.Ordinal))
                        throw new InvalidDataException("Quick Rescue authored question skill mismatch: " + option.QuestionId);
                    if (!string.Equals(question.Difficulty, checkpoint.Difficulty, StringComparison.Ordinal))
                        throw new InvalidDataException("Quick Rescue authored question difficulty mismatch: " + option.QuestionId);
                }
            }
        }

        private static MathQuickRescueCheckpoint MapCheckpoint(CheckpointDto dto)
        {
            return new MathQuickRescueCheckpoint
            {
                Number = dto.number,
                Difficulty = Trim(dto.difficulty),
                LabelVi = Trim(dto.label_vi),
                GoalVi = Trim(dto.goal_vi),
                QuestionOptions = (dto.question_options ?? new List<QuestionOptionDto>()).Select(x => new MathQuickRescueQuestionOption
                {
                    QuestionId = Trim(x.question_id),
                    Variant = Trim(x.variant)
                }).ToList(),
                HintLevel1Vi = Trim(dto.hint_level_1_vi),
                HintLevel2Vi = Trim(dto.hint_level_2_vi),
                RepairVi = Trim(dto.repair_vi),
                CommonErrors = (dto.common_errors ?? new List<CommonErrorDto>()).Select(x => new MathQuickRescueCommonError
                {
                    Id = Trim(x.id),
                    CueVi = Trim(x.cue_vi),
                    RepairVi = Trim(x.repair_vi)
                }).ToList()
            };
        }

        private static MathQuickRescueAdaptiveRule MapRule(AdaptiveRuleDto dto)
        {
            return new MathQuickRescueAdaptiveRule
            {
                State = Trim(dto.state),
                PreferredVariant = Trim(dto.preferred_variant),
                HintLevel = dto.hint_level,
                UseRepair = dto.use_repair,
                OfferBreak = dto.offer_break,
                MinimalFeedback = dto.minimal_feedback,
                SelectQuestion = dto.select_question
            };
        }

        private static void Validate(MathQuickRescueLearningPack pack)
        {
            if (pack.SchemaVersion != 1) throw new InvalidDataException("Unsupported Quick Rescue schema_version.");
            if (!string.Equals(pack.PackId, "math_grade2_quick_rescue_learning_v1", StringComparison.Ordinal))
                throw new InvalidDataException("Unexpected Quick Rescue pack_id.");
            if (!string.Equals(pack.Subject, "math", StringComparison.Ordinal) || pack.Grade != 2 || !string.Equals(pack.Language, "vi", StringComparison.Ordinal))
                throw new InvalidDataException("Quick Rescue subject/grade/language mismatch.");
            if (string.IsNullOrWhiteSpace(pack.TargetLessonId) || string.IsNullOrWhiteSpace(pack.TargetSkillId))
                throw new InvalidDataException("Quick Rescue target lesson/skill missing.");
            if (pack.CheckpointCount != 3 || pack.Checkpoints == null || pack.Checkpoints.Count != 3)
                throw new InvalidDataException("Quick Rescue V1 requires exactly three checkpoints.");
            var expectedPacing = new[] { "basic", "medium", "application" };
            if (pack.Pacing == null || !pack.Pacing.SequenceEqual(expectedPacing, StringComparer.Ordinal))
                throw new InvalidDataException("Quick Rescue V1 pacing must be basic -> medium -> application.");
            if (pack.AntiRepeat == null || pack.AntiRepeat.RecentQuestionWindow < 2 || !pack.AntiRepeat.PreferUnseen)
                throw new InvalidDataException("Quick Rescue anti-repeat policy is too weak.");
            if (pack.Feedback == null || string.IsNullOrWhiteSpace(pack.Feedback.ReadyVi) || string.IsNullOrWhiteSpace(pack.Feedback.CorrectVi) ||
                string.IsNullOrWhiteSpace(pack.Feedback.WrongVi) || string.IsNullOrWhiteSpace(pack.Feedback.BreakVi))
                throw new InvalidDataException("Quick Rescue feedback copy is incomplete.");

            var questionIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < expectedPacing.Length; index++)
            {
                var checkpoint = pack.GetCheckpoint(index + 1);
                if (checkpoint == null || !string.Equals(checkpoint.Difficulty, expectedPacing[index], StringComparison.Ordinal))
                    throw new InvalidDataException("Quick Rescue checkpoint numbering/difficulty mismatch.");
                if (string.IsNullOrWhiteSpace(checkpoint.LabelVi) || string.IsNullOrWhiteSpace(checkpoint.GoalVi) ||
                    string.IsNullOrWhiteSpace(checkpoint.HintLevel1Vi) || string.IsNullOrWhiteSpace(checkpoint.HintLevel2Vi) || string.IsNullOrWhiteSpace(checkpoint.RepairVi))
                    throw new InvalidDataException("Quick Rescue checkpoint child-facing copy is incomplete.");
                if (checkpoint.QuestionOptions == null || checkpoint.QuestionOptions.Count < 2)
                    throw new InvalidDataException("Quick Rescue checkpoint needs at least two question options for anti-repeat.");
                var checkpointVariants = new HashSet<string>(StringComparer.Ordinal);
                foreach (var option in checkpoint.QuestionOptions)
                {
                    if (string.IsNullOrWhiteSpace(option.QuestionId) || string.IsNullOrWhiteSpace(option.Variant))
                        throw new InvalidDataException("Quick Rescue question option invalid.");
                    if (!AllowedVariants.Contains(option.Variant))
                        throw new InvalidDataException("Quick Rescue question option declares unsupported variant: " + option.Variant);
                    checkpointVariants.Add(option.Variant);
                    if (!questionIds.Add(option.QuestionId)) throw new InvalidDataException("Quick Rescue question id reused across checkpoints: " + option.QuestionId);
                }
                if (!checkpointVariants.Contains("support") || !checkpointVariants.Contains("transfer"))
                    throw new InvalidDataException("Quick Rescue checkpoint must expose both support and transfer variants.");
                if (checkpoint.CommonErrors == null || checkpoint.CommonErrors.Count < 2 || checkpoint.CommonErrors.Any(x =>
                    string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.CueVi) || string.IsNullOrWhiteSpace(x.RepairVi)))
                    throw new InvalidDataException("Quick Rescue checkpoint requires at least two complete common-error repairs.");
                if (checkpoint.CommonErrors.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != checkpoint.CommonErrors.Count)
                    throw new InvalidDataException("Quick Rescue checkpoint common-error ids must be unique.");
            }

            var expectedStates = Enum.GetNames(typeof(BehaviorState));
            if (pack.AdaptiveRules == null || pack.AdaptiveRules.Count != expectedStates.Length ||
                pack.AdaptiveRules.Select(x => x.State).Distinct(StringComparer.Ordinal).Count() != expectedStates.Length ||
                expectedStates.Any(state => !pack.AdaptiveRules.Any(x => string.Equals(x.State, state, StringComparison.Ordinal))))
                throw new InvalidDataException("Quick Rescue adaptive rules must cover every BehaviorState exactly once.");
            foreach (var rule in pack.AdaptiveRules)
            {
                if (rule.HintLevel < 0 || rule.HintLevel > 2) throw new InvalidDataException("Quick Rescue adaptive hint level out of range.");
                if (string.IsNullOrWhiteSpace(rule.PreferredVariant) || !AllowedPreferredVariants.Contains(rule.PreferredVariant))
                    throw new InvalidDataException("Quick Rescue adaptive preferred variant is invalid: " + rule.PreferredVariant);
            }

            var ready = pack.GetRule(BehaviorState.READY);
            if (ready == null || !ready.SelectQuestion || ready.OfferBreak || ready.UseRepair || ready.HintLevel != 0 ||
                !string.Equals(ready.PreferredVariant, "support", StringComparison.Ordinal))
                throw new InvalidDataException("READY Quick Rescue rule must prefer confidence-first support with zero hint.");
            var flow = pack.GetRule(BehaviorState.FLOW_LIKELY);
            if (flow == null || !flow.SelectQuestion || !flow.MinimalFeedback || flow.HintLevel != 0)
                throw new InvalidDataException("FLOW Quick Rescue rule must keep play moving with minimal feedback.");
            var bored = pack.GetRule(BehaviorState.BORED_OR_UNDERCHALLENGED);
            if (bored == null || !bored.SelectQuestion || !string.Equals(bored.PreferredVariant, "transfer", StringComparison.Ordinal) || bored.HintLevel != 0 || bored.UseRepair)
                throw new InvalidDataException("BORED Quick Rescue rule must prefer transfer without extra scaffold.");
            var strained = pack.GetRule(BehaviorState.STRAINED);
            if (strained == null || !strained.SelectQuestion || !string.Equals(strained.PreferredVariant, "support", StringComparison.Ordinal) || strained.HintLevel < 1 || strained.UseRepair)
                throw new InvalidDataException("STRAINED Quick Rescue rule must prefer support with a small hint.");
            var fatigue = pack.GetRule(BehaviorState.FATIGUED_LIKELY);
            if (fatigue == null || !fatigue.OfferBreak || fatigue.SelectQuestion)
                throw new InvalidDataException("Fatigued Quick Rescue rule must offer a break and not select another question.");
            var frustration = pack.GetRule(BehaviorState.FRUSTRATED_LIKELY);
            if (frustration == null || !string.Equals(frustration.PreferredVariant, "support", StringComparison.Ordinal) || !frustration.UseRepair || frustration.HintLevel < 2)
                throw new InvalidDataException("Frustrated Quick Rescue rule must use support + repair with strong support.");
        }

        private static IList<string> Copy(IList<string> values)
        {
            return values == null ? new List<string>() : values.Select(Trim).ToList();
        }

        private static string Trim(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private sealed class RootDto
        {
            public int schema_version { get; set; }
            public string pack_id { get; set; }
            public string subject { get; set; }
            public int grade { get; set; }
            public string language { get; set; }
            public string target_lesson_id { get; set; }
            public string target_skill_id { get; set; }
            public int checkpoint_count { get; set; }
            public IList<string> pacing { get; set; }
            public AntiRepeatDto anti_repeat { get; set; }
            public FeedbackDto feedback { get; set; }
            public IList<AdaptiveRuleDto> adaptive_rules { get; set; }
            public IList<CheckpointDto> checkpoints { get; set; }
        }

        private sealed class AntiRepeatDto { public int recent_question_window { get; set; } public bool prefer_unseen { get; set; } }
        private sealed class FeedbackDto { public string ready_vi { get; set; } public string correct_vi { get; set; } public string wrong_vi { get; set; } public string break_vi { get; set; } }
        private sealed class AdaptiveRuleDto
        {
            public string state { get; set; }
            public string preferred_variant { get; set; }
            public int hint_level { get; set; }
            public bool use_repair { get; set; }
            public bool offer_break { get; set; }
            public bool minimal_feedback { get; set; }
            public bool select_question { get; set; }
        }
        private sealed class CheckpointDto
        {
            public int number { get; set; }
            public string difficulty { get; set; }
            public string label_vi { get; set; }
            public string goal_vi { get; set; }
            public IList<QuestionOptionDto> question_options { get; set; }
            public string hint_level_1_vi { get; set; }
            public string hint_level_2_vi { get; set; }
            public string repair_vi { get; set; }
            public IList<CommonErrorDto> common_errors { get; set; }
        }
        private sealed class QuestionOptionDto { public string question_id { get; set; } public string variant { get; set; } }
        private sealed class CommonErrorDto { public string id { get; set; } public string cue_vi { get; set; } public string repair_vi { get; set; } }
    }

    public sealed class MathQuickRescueAdaptiveSelector
    {
        public MathQuickRescueContentDecision Select(
            MathQuickRescueLearningPack pack,
            int oneBasedCheckpoint,
            BehaviorDecision behavior,
            IEnumerable<string> recentContentQuestionIds,
            int seed)
        {
            if (pack == null) throw new ArgumentNullException("pack");
            var checkpoint = pack.GetCheckpoint(oneBasedCheckpoint);
            if (checkpoint == null) throw new ArgumentOutOfRangeException("oneBasedCheckpoint");
            var state = behavior == null ? BehaviorState.READY : behavior.State;
            var rule = pack.GetRule(state);
            if (rule == null) throw new InvalidOperationException("Quick Rescue adaptive rule missing for " + state + ".");

            if (rule.OfferBreak && !rule.SelectQuestion)
            {
                return new MathQuickRescueContentDecision
                {
                    CheckpointNumber = checkpoint.Number,
                    Difficulty = checkpoint.Difficulty,
                    RecommendedHintLevel = 0,
                    UseRepair = false,
                    OfferBreak = true,
                    MinimalFeedback = rule.MinimalFeedback,
                    SupportVi = pack.Feedback.BreakVi,
                    Reason = "behavior_" + state + ":offer_break"
                };
            }

            var explicitRepair = HasExplicitRepair(behavior);
            var preferredVariant = explicitRepair ? "support" : rule.PreferredVariant;
            var options = checkpoint.QuestionOptions.ToList();
            var recent = NormalizeRecent(recentContentQuestionIds, pack.AntiRepeat.RecentQuestionWindow);
            var unseen = options.Where(x => !recent.Contains(x.QuestionId, StringComparer.Ordinal)).ToList();
            var eligible = unseen.Count > 0 ? unseen : options;

            // Anti-repeat is a hard child-UX guard. When every option was seen recently,
            // prefer the least-recently-seen question first; adaptive variant preference
            // only breaks ties so a state change cannot force the exact same prompt again.
            if (unseen.Count == 0 && recent.Count > 0)
            {
                var oldestIndex = eligible.Min(x => LastIndexOf(recent, x.QuestionId));
                var oldest = eligible.Where(x => LastIndexOf(recent, x.QuestionId) == oldestIndex).ToList();
                if (oldest.Count > 0) eligible = oldest;
            }
            if (!string.Equals(preferredVariant, "any", StringComparison.Ordinal))
            {
                var preferred = eligible.Where(x => string.Equals(x.Variant, preferredVariant, StringComparison.Ordinal)).ToList();
                if (preferred.Count > 0) eligible = preferred;
            }

            eligible = eligible.OrderBy(x => x.QuestionId, StringComparer.Ordinal).ToList();
            var selected = eligible[PositiveMod(StableHash(seed, checkpoint.Number, state.ToString()), eligible.Count)];
            var useRepair = rule.UseRepair || explicitRepair;
            var hintLevel = Math.Max(0, Math.Min(2, rule.HintLevel));
            if (useRepair) hintLevel = Math.Max(2, hintLevel);
            var support = hintLevel >= 2 ? checkpoint.HintLevel2Vi : hintLevel == 1 ? checkpoint.HintLevel1Vi : pack.Feedback.ReadyVi;
            if (useRepair) support = checkpoint.RepairVi;

            return new MathQuickRescueContentDecision
            {
                CheckpointNumber = checkpoint.Number,
                Difficulty = checkpoint.Difficulty,
                QuestionId = selected.QuestionId,
                Variant = selected.Variant,
                RecommendedHintLevel = hintLevel,
                UseRepair = useRepair,
                OfferBreak = rule.OfferBreak,
                MinimalFeedback = rule.MinimalFeedback,
                SupportVi = support,
                Reason = "behavior_" + state + ":" + preferredVariant + (unseen.Count > 0 ? ":unseen" : ":repeat_fallback") + (explicitRepair ? ":explicit_repair" : string.Empty)
            };
        }

        public MathQuickRescueErrorSupportDecision ResolveErrorSupport(
            MathQuickRescueLearningPack pack,
            int oneBasedCheckpoint,
            BehaviorObservation observation,
            BehaviorDecision behavior)
        {
            return ResolveErrorSupport(pack, oneBasedCheckpoint, observation == null ? null : observation.ErrorType, behavior);
        }

        public MathQuickRescueErrorSupportDecision ResolveErrorSupport(
            MathQuickRescueLearningPack pack,
            int oneBasedCheckpoint,
            string errorType,
            BehaviorDecision behavior)
        {
            if (pack == null) throw new ArgumentNullException("pack");
            var checkpoint = pack.GetCheckpoint(oneBasedCheckpoint);
            if (checkpoint == null) throw new ArgumentOutOfRangeException("oneBasedCheckpoint");
            var state = behavior == null ? BehaviorState.READY : behavior.State;
            var rule = pack.GetRule(state);
            if (rule == null) throw new InvalidOperationException("Quick Rescue adaptive rule missing for " + state + ".");

            var normalizedError = string.IsNullOrWhiteSpace(errorType) ? null : errorType.Trim();
            var known = checkpoint.CommonErrors == null ? null : checkpoint.CommonErrors.FirstOrDefault(x =>
                string.Equals(x.Id, normalizedError, StringComparison.OrdinalIgnoreCase));

            if (rule.OfferBreak && !rule.SelectQuestion)
            {
                return new MathQuickRescueErrorSupportDecision
                {
                    CheckpointNumber = checkpoint.Number,
                    ErrorId = known == null ? normalizedError : known.Id,
                    KnownError = known != null,
                    CueVi = known == null ? null : known.CueVi,
                    RepairVi = known == null ? checkpoint.RepairVi : known.RepairVi,
                    RecommendedHintLevel = 0,
                    UseRepair = false,
                    OfferBreak = true,
                    RecommendedCopyVi = pack.Feedback.BreakVi,
                    Reason = "behavior_" + state + ":offer_break"
                };
            }

            var explicitRepair = HasExplicitRepair(behavior);
            var hintLevel = Math.Max(1, Math.Min(2, rule.HintLevel));
            var useRepair = rule.UseRepair || explicitRepair || state == BehaviorState.FRUSTRATED_LIKELY;
            var cue = known == null ? checkpoint.HintLevel1Vi : known.CueVi;
            var repair = known == null ? checkpoint.RepairVi : known.RepairVi;
            return new MathQuickRescueErrorSupportDecision
            {
                CheckpointNumber = checkpoint.Number,
                ErrorId = known == null ? normalizedError : known.Id,
                KnownError = known != null,
                CueVi = cue,
                RepairVi = repair,
                RecommendedHintLevel = useRepair ? Math.Max(2, hintLevel) : hintLevel,
                UseRepair = useRepair,
                OfferBreak = rule.OfferBreak,
                RecommendedCopyVi = useRepair ? repair : cue,
                Reason = known == null
                    ? "behavior_" + state + ":generic_error_support"
                    : "behavior_" + state + ":common_error_" + known.Id
            };
        }

        private static bool HasExplicitRepair(BehaviorDecision decision)
        {
            return decision != null &&
                (decision.TriggerPrerequisiteRepair ||
                 (decision.Actions != null && decision.Actions.Any(x => string.Equals(x, "prerequisite_repair", StringComparison.Ordinal))));
        }

        private static List<string> NormalizeRecent(IEnumerable<string> values, int window)
        {
            var all = (values ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();
            if (window <= 0 || all.Count <= window) return all;
            return all.Skip(all.Count - window).ToList();
        }

        private static int LastIndexOf(IList<string> values, string value)
        {
            for (var i = values.Count - 1; i >= 0; i--)
                if (string.Equals(values[i], value, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static int StableHash(int seed, int checkpoint, string state)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + seed;
                hash = hash * 31 + checkpoint;
                foreach (var ch in state ?? string.Empty) hash = hash * 31 + ch;
                return hash;
            }
        }

        private static int PositiveMod(int value, int divisor)
        {
            if (divisor <= 0) throw new ArgumentOutOfRangeException("divisor");
            var mod = value % divisor;
            return mod < 0 ? mod + divisor : mod;
        }
    }
}
