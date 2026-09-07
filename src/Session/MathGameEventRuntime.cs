using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using WAHU.Content;
using WAHU.Data;
using WAHU.Learning;

namespace WAHU.Session
{
    public sealed class MathGameEventDefinition
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string TitleVi { get; set; }
        public string IntroVi { get; set; }
        public string CompletionVi { get; set; }
        public string TargetLessonId { get; set; }
        public string TargetSkillId { get; set; }
        public int QuestionCount { get; set; }
        public IList<string> CheckpointNounsVi { get; set; }
        public string Theme { get; set; }
        public string RepairCopyVi { get; set; }
        public string BreakCopyVi { get; set; }
        public string RewardPresentation { get; set; }
    }

    public sealed class MathGameEventCatalog
    {
        public int SchemaVersion { get; set; }
        public string CatalogId { get; set; }
        public string Language { get; set; }
        public IList<MathGameEventDefinition> Events { get; set; }

        public MathGameEventDefinition FindById(string eventId)
        {
            return (Events ?? new List<MathGameEventDefinition>())
                .FirstOrDefault(x => string.Equals(x.Id, eventId, StringComparison.Ordinal));
        }

        public MathGameEventDefinition FindByLesson(string lessonId)
        {
            return (Events ?? new List<MathGameEventDefinition>())
                .FirstOrDefault(x => string.Equals(x.TargetLessonId, lessonId, StringComparison.Ordinal));
        }
    }

    public sealed class MathGameEventCatalogSource
    {
        public const string CatalogId = "math_grade2_game_events_v1";
        public const string Kind = "quick_rescue";
        public const string RewardPresentation = "garden_progress";

        private static readonly HashSet<string> RootKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "schema_version", "catalog_id", "language", "events"
        };

        private static readonly HashSet<string> EventKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "kind", "title_vi", "intro_vi", "completion_vi", "target_lesson_id", "target_skill_id",
            "question_count", "checkpoint_nouns_vi", "theme", "repair_copy_vi", "break_copy_vi", "reward_presentation"
        };

        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public bool TryLoad(string eventCatalogPath, string lessonCatalogPath, out MathGameEventCatalog catalog)
        {
            try
            {
                catalog = Load(eventCatalogPath, lessonCatalogPath);
                return true;
            }
            catch
            {
                catalog = null;
                return false;
            }
        }

        public MathGameEventCatalog Load(string eventCatalogPath, string lessonCatalogPath)
        {
            if (string.IsNullOrWhiteSpace(eventCatalogPath)) throw new ArgumentException("eventCatalogPath");
            if (string.IsNullOrWhiteSpace(lessonCatalogPath)) throw new ArgumentException("lessonCatalogPath");
            if (!File.Exists(eventCatalogPath)) throw new FileNotFoundException("Math game event catalog missing.", eventCatalogPath);

            var raw = _json.Deserialize<Dictionary<string, object>>(File.ReadAllText(eventCatalogPath));
            if (raw == null) throw new InvalidDataException("Math game event catalog root is invalid.");
            RequireExactKeys(raw, RootKeys, "event catalog root");

            var schemaVersion = ReadInt(raw, "schema_version");
            if (schemaVersion != 1) throw new InvalidDataException("Unsupported Math game event schema_version.");
            var catalogId = ReadString(raw, "catalog_id");
            if (!string.Equals(catalogId, CatalogId, StringComparison.Ordinal))
                throw new InvalidDataException("Unexpected Math game event catalog_id.");
            var language = ReadString(raw, "language");
            if (!string.Equals(language, "vi", StringComparison.Ordinal))
                throw new InvalidDataException("Math game event catalog language must be vi.");

            object rawEvents;
            if (!raw.TryGetValue("events", out rawEvents) || rawEvents == null)
                throw new InvalidDataException("Math game event catalog events missing.");
            var enumerable = rawEvents as IEnumerable;
            if (enumerable == null || rawEvents is string)
                throw new InvalidDataException("Math game event catalog events must be an array.");

            var lessonCatalog = new MathLessonCatalogSource().Load(lessonCatalogPath);
            var events = new List<MathGameEventDefinition>();
            foreach (var item in enumerable)
            {
                var data = item as Dictionary<string, object>;
                if (data == null) throw new InvalidDataException("Math game event item must be an object.");
                RequireExactKeys(data, EventKeys, "event item");
                var definition = ReadEvent(data);
                var lesson = lessonCatalog.FindLesson(definition.TargetLessonId);
                if (lesson == null) throw new InvalidDataException("Math game event target lesson does not exist: " + definition.TargetLessonId);
                if (!string.Equals(lesson.SkillId, definition.TargetSkillId, StringComparison.Ordinal))
                    throw new InvalidDataException("Math game event target lesson/skill mismatch: " + definition.Id);
                events.Add(definition);
            }
            if (events.Count == 0) throw new InvalidDataException("Math game event catalog contains no events.");
            if (events.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != events.Count)
                throw new InvalidDataException("Math game event ids must be unique.");
            if (events.Select(x => x.TargetLessonId).Distinct(StringComparer.Ordinal).Count() != events.Count)
                throw new InvalidDataException("V1 Math game event target lessons must be unique for deterministic resume.");

            return new MathGameEventCatalog
            {
                SchemaVersion = schemaVersion,
                CatalogId = catalogId,
                Language = language,
                Events = events
            };
        }

        private static MathGameEventDefinition ReadEvent(IDictionary<string, object> data)
        {
            var definition = new MathGameEventDefinition
            {
                Id = ReadString(data, "id"),
                Kind = ReadString(data, "kind"),
                TitleVi = ReadString(data, "title_vi"),
                IntroVi = ReadString(data, "intro_vi"),
                CompletionVi = ReadString(data, "completion_vi"),
                TargetLessonId = ReadString(data, "target_lesson_id"),
                TargetSkillId = ReadString(data, "target_skill_id"),
                QuestionCount = ReadInt(data, "question_count"),
                CheckpointNounsVi = ReadStringList(data, "checkpoint_nouns_vi"),
                Theme = ReadString(data, "theme"),
                RepairCopyVi = ReadString(data, "repair_copy_vi"),
                BreakCopyVi = ReadString(data, "break_copy_vi"),
                RewardPresentation = ReadString(data, "reward_presentation")
            };

            if (!string.Equals(definition.Kind, Kind, StringComparison.Ordinal))
                throw new InvalidDataException("Math game event kind must be quick_rescue.");
            if (definition.QuestionCount != MathSessionCoordinator.TargetedLessonQuestionCount)
                throw new InvalidDataException("Math game event question_count must equal targeted lesson count.");
            if (!string.Equals(definition.RewardPresentation, RewardPresentation, StringComparison.Ordinal))
                throw new InvalidDataException("Math game event reward presentation must use garden_progress.");
            if (definition.TitleVi.Length > 60) throw new InvalidDataException("Math game event title is too long.");
            if (definition.IntroVi.Length > 180 || definition.CompletionVi.Length > 180 ||
                definition.RepairCopyVi.Length > 180 || definition.BreakCopyVi.Length > 180)
                throw new InvalidDataException("Math game event child-facing copy exceeds V1 length limits.");
            if (definition.CheckpointNounsVi.Count != MathSessionCoordinator.TargetedLessonQuestionCount ||
                definition.CheckpointNounsVi.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("Math game event requires exactly three non-empty checkpoint nouns.");
            return definition;
        }

        private static void RequireExactKeys(IDictionary<string, object> data, ISet<string> expected, string label)
        {
            if (data.Count != expected.Count || data.Keys.Any(x => !expected.Contains(x)) || expected.Any(x => !data.ContainsKey(x)))
                throw new InvalidDataException("Unexpected or missing keys in Math game " + label + ".");
        }

        private static string ReadString(IDictionary<string, object> data, string key)
        {
            object value;
            if (!data.TryGetValue(key, out value) || value == null) throw new InvalidDataException("Missing Math game event key: " + key);
            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Empty Math game event key: " + key);
            return text.Trim();
        }

        private static int ReadInt(IDictionary<string, object> data, string key)
        {
            object value;
            if (!data.TryGetValue(key, out value) || value == null) throw new InvalidDataException("Missing Math game event key: " + key);
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch (Exception ex) { throw new InvalidDataException("Invalid integer Math game event key: " + key, ex); }
        }

        private static IList<string> ReadStringList(IDictionary<string, object> data, string key)
        {
            object value;
            if (!data.TryGetValue(key, out value) || value == null) throw new InvalidDataException("Missing Math game event key: " + key);
            var enumerable = value as IEnumerable;
            if (enumerable == null || value is string) throw new InvalidDataException("Math game event array key is invalid: " + key);
            var result = new List<string>();
            foreach (var item in enumerable)
            {
                var text = Convert.ToString(item, CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Math game event array contains empty text: " + key);
                result.Add(text.Trim());
            }
            return result;
        }
    }

    public sealed class MathGameEventAction
    {
        public BehaviorState BehaviorState { get; set; }
        public string Action { get; set; }
        public bool MinimizeInterruptions { get; set; }
        public bool UseSmallCue { get; set; }
        public bool UseRepair { get; set; }
        public bool OfferBreak { get; set; }
        public bool SuggestPositiveClose { get; set; }
        public bool PreserveCheckpoint { get; set; }
    }

    public static class MathGameEventBehaviorMapper
    {
        public static MathGameEventAction Map(BehaviorDecision decision)
        {
            var state = decision == null ? BehaviorState.READY : decision.State;
            var result = new MathGameEventAction
            {
                BehaviorState = state,
                Action = "normal",
                PreserveCheckpoint = true
            };
            switch (state)
            {
                case BehaviorState.FLOW_LIKELY:
                    result.Action = "minimize_interruptions";
                    result.MinimizeInterruptions = true;
                    break;
                case BehaviorState.BORED_OR_UNDERCHALLENGED:
                    result.Action = "context_transfer";
                    break;
                case BehaviorState.STRAINED:
                    result.Action = "small_cue";
                    result.UseSmallCue = true;
                    break;
                case BehaviorState.FRUSTRATED_LIKELY:
                    result.Action = "repair";
                    result.UseRepair = true;
                    break;
                case BehaviorState.FATIGUED_LIKELY:
                    result.Action = "offer_break";
                    result.OfferBreak = true;
                    result.SuggestPositiveClose = true;
                    break;
            }
            return result;
        }
    }

    public sealed class MathGameEventState
    {
        public bool EventPresentationAvailable { get; set; }
        public bool FallbackToLessonPresentation { get; set; }
        public string EventId { get; set; }
        public string SessionId { get; set; }
        public string TargetLessonId { get; set; }
        public string TargetSkillId { get; set; }
        public IList<string> SelectedContentQuestionIds { get; set; }
        public int CompletedCheckpointCount { get; set; }
        public int TotalCheckpointCount { get; set; }
        public int CurrentCheckpointNumber { get; set; }
        public string CurrentCheckpointNounVi { get; set; }
        public bool RetryPending { get; set; }
        public bool IsComplete { get; set; }
        public MathGameEventAction Action { get; set; }
    }

    public sealed class MathGameEventStartResult
    {
        public MathSessionStartResult Session { get; set; }
        public MathGameEventDefinition Event { get; set; }
        public MathGameEventState EventState { get; set; }
    }

    public sealed class MathGameEventAnswerResult
    {
        public MathAnswerOutcome Learning { get; set; }
        public MathGameEventState EventState { get; set; }
    }

    public sealed class MathGameEventCompletionResult
    {
        public MathSessionSummary LearningSummary { get; set; }
        public MathGameEventState EventState { get; set; }
    }

    public sealed class MathGameEventCoordinator : IDisposable
    {
        private readonly LearningDatabase _database;
        private readonly string _templatePath;
        private readonly string _eventCatalogPath;
        private readonly string _performanceProfile;
        private readonly int _seed;
        private readonly string _requestedEventId;
        private readonly string _fallbackLessonId;
        private MathGameEventDefinition _event;
        private MathSessionCoordinator _session;
        private MathSessionStartResult _start;
        private MathGameEventCompletionResult _completion;
        private MathGameEventAction _lastAction = MathGameEventBehaviorMapper.Map(null);
        private bool _started;

        public MathGameEventCoordinator(
            LearningDatabase database,
            string templatePath,
            string eventCatalogPath,
            string performanceProfile,
            int seed,
            string eventId,
            string fallbackLessonId)
        {
            _database = database ?? throw new ArgumentNullException("database");
            if (string.IsNullOrWhiteSpace(templatePath)) throw new ArgumentException("templatePath");
            _templatePath = templatePath;
            _eventCatalogPath = eventCatalogPath;
            _performanceProfile = performanceProfile == "NORMAL" ? "NORMAL" : "LOW";
            _seed = seed;
            _requestedEventId = string.IsNullOrWhiteSpace(eventId) ? null : eventId.Trim();
            _fallbackLessonId = string.IsNullOrWhiteSpace(fallbackLessonId) ? null : fallbackLessonId.Trim();
            if (_requestedEventId == null && _fallbackLessonId == null)
                throw new ArgumentException("eventId or fallbackLessonId is required.");
        }

        public MathGameEventDefinition Event { get { return _event; } }
        public MathSessionCoordinator LearningSession { get { return _session; } }
        public MathGameEventState CurrentState { get { return BuildState(); } }

        public MathGameEventStartResult Start(string displayName)
        {
            if (_started) throw new InvalidOperationException("Math game event already started.");
            ResolveEventFailSafe();
            var lessonId = _event == null ? _fallbackLessonId : _event.TargetLessonId;
            if (string.IsNullOrWhiteSpace(lessonId))
                throw new InvalidOperationException("Math game event metadata is unavailable and no fallback lesson was supplied.");

            _session = new MathSessionCoordinator(_database, _templatePath, _performanceProfile, _seed, lessonId);
            _start = _session.Start(displayName);
            _started = true;
            try
            {
                // A prior completed Math session may have become durable just before a transient
                // Garden reward write failed. Reward absence is discoverable from durable session
                // history, so repair it idempotently when the next game event opens; never block play.
                new GameWorldRewardService(_database).ReconcileMissingCompletedMathSessionRewards(_start.ChildId);
            }
            catch
            {
                // Game-world repair is downstream of learning and must not make the event unplayable.
            }
            if (!string.Equals(_start.SessionMode, "lesson", StringComparison.Ordinal) ||
                !string.Equals(_start.TargetLessonId, lessonId, StringComparison.Ordinal) ||
                _start.TargetQuestionCount != MathSessionCoordinator.TargetedLessonQuestionCount)
                throw new InvalidOperationException("Math game event must bind to one targeted three-question Math session.");
            if (_event != null && !string.Equals(_event.TargetLessonId, _start.TargetLessonId, StringComparison.Ordinal))
                throw new InvalidOperationException("Math game event target lesson changed during start/resume.");

            return new MathGameEventStartResult
            {
                Session = _start,
                Event = _event,
                EventState = BuildState()
            };
        }

        public MathQuestion NextQuestion()
        {
            EnsureStarted();
            return _session.NextQuestion();
        }

        public MathGameEventAnswerResult SubmitAnswerAt(string answer, int hintLevel, string inputMethod, DateTime answeredAtUtc, int responseMs)
        {
            EnsureStarted();
            var learning = _session.SubmitAnswerAt(answer, hintLevel, inputMethod, answeredAtUtc, responseMs);
            _lastAction = MathGameEventBehaviorMapper.Map(learning.Behavior);
            return new MathGameEventAnswerResult { Learning = learning, EventState = BuildState() };
        }

        public MathGameEventAnswerResult SubmitAnswerWithRetryAt(string answer, int hintLevel, string inputMethod, DateTime answeredAtUtc, int responseMs)
        {
            EnsureStarted();
            var learning = _session.SubmitAnswerWithRetryAt(answer, hintLevel, inputMethod, answeredAtUtc, responseMs);
            _lastAction = MathGameEventBehaviorMapper.Map(learning.Behavior);
            return new MathGameEventAnswerResult { Learning = learning, EventState = BuildState() };
        }

        public MathGameEventAnswerResult SubmitRetryAnswerAt(string answer, int hintLevel, string inputMethod, DateTime answeredAtUtc, int responseMs)
        {
            EnsureStarted();
            var learning = _session.SubmitRetryAnswerAt(answer, hintLevel, inputMethod, answeredAtUtc, responseMs);
            _lastAction = MathGameEventBehaviorMapper.Map(learning.Behavior);
            return new MathGameEventAnswerResult { Learning = learning, EventState = BuildState() };
        }

        public MathSessionSummary SuspendForBreak(string reason)
        {
            EnsureStarted();
            return _session.Suspend(string.IsNullOrWhiteSpace(reason) ? "game_event_break" : reason);
        }

        public MathGameEventCompletionResult Complete()
        {
            EnsureStarted();
            if (_completion != null) return _completion;
            var before = _session.Summary;
            if (before.Attempts < MathSessionCoordinator.TargetedLessonQuestionCount)
                throw new InvalidOperationException("Math game event cannot complete before all three checkpoints are completed. Use SuspendForBreak instead.");
            var summary = _session.Complete();
            _completion = new MathGameEventCompletionResult
            {
                LearningSummary = summary,
                EventState = BuildCompletedState(summary)
            };
            return _completion;
        }

        public void Dispose()
        {
            if (_session != null) _session.Dispose();
        }

        private void ResolveEventFailSafe()
        {
            _event = null;
            if (string.IsNullOrWhiteSpace(_eventCatalogPath)) return;
            var lessonCatalogPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_templatePath)), "lesson_catalog_v1.json");
            MathGameEventCatalog catalog;
            if (!new MathGameEventCatalogSource().TryLoad(_eventCatalogPath, lessonCatalogPath, out catalog)) return;

            if (_requestedEventId != null) _event = catalog.FindById(_requestedEventId);
            else if (_fallbackLessonId != null) _event = catalog.FindByLesson(_fallbackLessonId);
            if (_event != null && _fallbackLessonId != null &&
                !string.Equals(_event.TargetLessonId, _fallbackLessonId, StringComparison.Ordinal))
                throw new InvalidOperationException("Math game event does not match requested fallback lesson.");
        }

        private MathGameEventState BuildState()
        {
            if (!_started || _session == null || _start == null)
            {
                return new MathGameEventState
                {
                    EventPresentationAvailable = _event != null,
                    FallbackToLessonPresentation = _event == null,
                    EventId = _event == null ? null : _event.Id,
                    TotalCheckpointCount = MathSessionCoordinator.TargetedLessonQuestionCount,
                    CurrentCheckpointNumber = 1,
                    Action = _lastAction
                };
            }
            var summary = _session.Summary;
            var completed = Math.Max(0, Math.Min(MathSessionCoordinator.TargetedLessonQuestionCount, summary.Attempts));
            var current = completed >= MathSessionCoordinator.TargetedLessonQuestionCount
                ? MathSessionCoordinator.TargetedLessonQuestionCount
                : completed + 1;
            return new MathGameEventState
            {
                EventPresentationAvailable = _event != null,
                FallbackToLessonPresentation = _event == null,
                EventId = _event == null ? null : _event.Id,
                SessionId = _start.SessionId,
                TargetLessonId = _start.TargetLessonId,
                TargetSkillId = _event == null ? null : _event.TargetSkillId,
                SelectedContentQuestionIds = new List<string>(_start.SelectedContentQuestionIds ?? new List<string>()),
                CompletedCheckpointCount = completed,
                TotalCheckpointCount = MathSessionCoordinator.TargetedLessonQuestionCount,
                CurrentCheckpointNumber = current,
                CurrentCheckpointNounVi = CheckpointNoun(current),
                RetryPending = _session.HasOpenQuestion && summary.AnswerAttempts > summary.Attempts,
                IsComplete = completed >= MathSessionCoordinator.TargetedLessonQuestionCount,
                Action = _lastAction
            };
        }

        private MathGameEventState BuildCompletedState(MathSessionSummary summary)
        {
            return new MathGameEventState
            {
                EventPresentationAvailable = _event != null,
                FallbackToLessonPresentation = _event == null,
                EventId = _event == null ? null : _event.Id,
                SessionId = _start == null ? null : _start.SessionId,
                TargetLessonId = _start == null ? _fallbackLessonId : _start.TargetLessonId,
                TargetSkillId = _event == null ? null : _event.TargetSkillId,
                SelectedContentQuestionIds = _start == null ? new List<string>() : new List<string>(_start.SelectedContentQuestionIds ?? new List<string>()),
                CompletedCheckpointCount = MathSessionCoordinator.TargetedLessonQuestionCount,
                TotalCheckpointCount = MathSessionCoordinator.TargetedLessonQuestionCount,
                CurrentCheckpointNumber = MathSessionCoordinator.TargetedLessonQuestionCount,
                CurrentCheckpointNounVi = CheckpointNoun(MathSessionCoordinator.TargetedLessonQuestionCount),
                RetryPending = false,
                IsComplete = summary != null && summary.LessonCompleted,
                Action = _lastAction
            };
        }

        private string CheckpointNoun(int oneBased)
        {
            if (_event == null || _event.CheckpointNounsVi == null || oneBased < 1 || oneBased > _event.CheckpointNounsVi.Count)
                return null;
            return _event.CheckpointNounsVi[oneBased - 1];
        }

        private void EnsureStarted()
        {
            if (!_started || _session == null) throw new InvalidOperationException("Math game event has not started.");
        }
    }
}
