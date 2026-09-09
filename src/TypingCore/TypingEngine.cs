using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WAHU.TypingCore
{
    public sealed class TypingEngine
    {
        private readonly object _gate = new object();
        private readonly int _duplicateWindowMs;
        private readonly HashSet<string> _completedTargetIds = new HashSet<string>(StringComparer.Ordinal);

        private TypingTarget _target;
        private List<IList<string>> _candidateElements = new List<IList<string>>();
        private bool[] _candidateAlive = new bool[0];
        private int _typedCount;
        private int _errorCount;
        private bool _completed;
        private string _phase = TypingGamePhase.Intro;
        private string _phaseBeforePause = TypingGamePhase.Intro;
        private string _lastRawKey;
        private string _lastNormalizedKey;
        private string _lastSource;
        private long _lastTimestamp = long.MinValue;

        public TypingEngine(int duplicateWindowMs = 20)
        {
            if (duplicateWindowMs < 0 || duplicateWindowMs > 250)
                throw new ArgumentOutOfRangeException("duplicateWindowMs");
            _duplicateWindowMs = duplicateWindowMs;
        }

        public int DuplicateWindowMs { get { return _duplicateWindowMs; } }

        public TypingEngineSnapshot Snapshot
        {
            get
            {
                lock (_gate) return BuildSnapshotLocked();
            }
        }

        public TypingEngineResult SetActiveTarget(TypingTarget target, long timestamp = 0)
        {
            lock (_gate)
            {
                if (_phase == TypingGamePhase.Paused)
                    return ResultLocked(false, true, "paused", new TypingCoreEvent[0]);
                var prepared = PrepareTarget(target);
                _target = prepared.Target;
                _candidateElements = prepared.Candidates;
                _candidateAlive = Enumerable.Repeat(true, _candidateElements.Count).ToArray();
                _typedCount = 0;
                _errorCount = 0;
                _completed = false;
                ResetRepeatGuardLocked();
                _phase = string.Equals(_target.Kind, TypingTargetKind.Boss, StringComparison.Ordinal)
                    ? TypingGamePhase.Boss
                    : TypingGamePhase.Playing;
                _phaseBeforePause = _phase;

                return ResultLocked(true, false, "target_activated", new TypingCoreEvent[0]);
            }
        }

        public TypingEngineResult ClearActiveTarget()
        {
            lock (_gate)
            {
                if (_phase == TypingGamePhase.Paused)
                    return ResultLocked(false, true, "paused", new TypingCoreEvent[0]);
                _target = null;
                _candidateElements = new List<IList<string>>();
                _candidateAlive = new bool[0];
                _typedCount = 0;
                _errorCount = 0;
                _completed = false;
                ResetRepeatGuardLocked();
                _phase = TypingGamePhase.Intro;
                _phaseBeforePause = _phase;
                return ResultLocked(true, false, "target_cleared", new TypingCoreEvent[0]);
            }
        }

        public TypingEngineResult ProcessInput(TypingInput input)
        {
            if (input == null) throw new ArgumentNullException("input");
            lock (_gate)
            {
                if (_phase == TypingGamePhase.Paused)
                    return ResultLocked(false, true, "paused", new TypingCoreEvent[0]);
                if (_target == null)
                    return ResultLocked(false, true, "no_active_target", new TypingCoreEvent[0]);
                if (_completed)
                    return ResultLocked(false, true, "target_completed", new TypingCoreEvent[0]);

                ValidateSource(input.Source);
                var normalized = NormalizeKey(input);
                if (string.IsNullOrEmpty(normalized))
                    return ResultLocked(false, true, "empty_key", new TypingCoreEvent[0]);
                if (SplitTextElements(normalized).Count != 1)
                    return ResultLocked(false, true, "invalid_normalized_key", new TypingCoreEvent[0]);
                if (IsAbnormalRepeatLocked(input, normalized))
                    return ResultLocked(false, true, "duplicate_or_repeat", new TypingCoreEvent[0]);

                RememberInputLocked(input, normalized);
                var matchingCandidates = new bool[_candidateAlive.Length];
                var anyMatch = false;
                for (var i = 0; i < _candidateAlive.Length; i++)
                {
                    if (!_candidateAlive[i]) continue;
                    var elements = _candidateElements[i];
                    if (_typedCount >= elements.Count) continue;
                    if (!string.Equals(elements[_typedCount], normalized, StringComparison.Ordinal)) continue;
                    matchingCandidates[i] = true;
                    anyMatch = true;
                }

                if (!anyMatch)
                {
                    _errorCount++;
                    _phase = TypingGamePhase.Feedback;
                    return ResultLocked(false, false, "wrong_key", new[]
                    {
                        NewEvent(TypingEventType.CharWrong, input.Timestamp, _typedCount, normalized)
                    });
                }

                _candidateAlive = matchingCandidates;
                _typedCount++;
                var events = new List<TypingCoreEvent>
                {
                    NewEvent(TypingEventType.CharCorrect, input.Timestamp, _typedCount, normalized)
                };

                if (_typedCount >= CurrentTotalCountLocked())
                {
                    _completed = true;
                    _phase = TypingGamePhase.Feedback;
                    if (_completedTargetIds.Add(_target.Id))
                    {
                        events.Add(new TypingCoreEvent
                        {
                            Type = TypingEventType.WordCompleted,
                            TargetId = _target.Id,
                            Value = _target.RewardValue,
                            Text = _target.DisplayText,
                            Timestamp = input.Timestamp
                        });
                    }
                    return ResultLocked(true, false, "word_completed", events);
                }

                _phase = string.Equals(_target.Kind, TypingTargetKind.Boss, StringComparison.Ordinal)
                    ? TypingGamePhase.Boss
                    : TypingGamePhase.Playing;
                return ResultLocked(true, false, "char_correct", events);
            }
        }

        public TypingEngineResult Pause(long timestamp = 0)
        {
            lock (_gate)
            {
                if (_phase == TypingGamePhase.Paused)
                    return ResultLocked(false, true, "already_paused", new TypingCoreEvent[0]);
                _phaseBeforePause = _phase;
                _phase = TypingGamePhase.Paused;
                return ResultLocked(true, false, "paused", new[]
                {
                    NewEvent(TypingEventType.GamePaused, timestamp, null, null)
                });
            }
        }

        public TypingEngineResult Resume(long timestamp = 0)
        {
            lock (_gate)
            {
                if (_phase != TypingGamePhase.Paused)
                    return ResultLocked(false, true, "not_paused", new TypingCoreEvent[0]);
                _phase = string.IsNullOrWhiteSpace(_phaseBeforePause) ? TypingGamePhase.Intro : _phaseBeforePause;
                ResetRepeatGuardLocked();
                return ResultLocked(true, false, "resumed", new[]
                {
                    NewEvent(TypingEventType.GameResumed, timestamp, null, null)
                });
            }
        }

        private PreparedTarget PrepareTarget(TypingTarget source)
        {
            if (source == null) throw new ArgumentNullException("target");
            if (string.IsNullOrWhiteSpace(source.Id)) throw new ArgumentException("TypingTarget.id is required.", "target");
            if (string.IsNullOrWhiteSpace(source.DisplayText)) throw new ArgumentException("TypingTarget.displayText is required.", "target");
            if (!string.Equals(source.Language, TypingLanguage.Vietnamese, StringComparison.Ordinal) &&
                !string.Equals(source.Language, TypingLanguage.English, StringComparison.Ordinal))
                throw new ArgumentException("TypingTarget.language must be 'vi' or 'en'.", "target");
            if (!IsAllowedKind(source.Kind)) throw new ArgumentException("TypingTarget.kind is invalid.", "target");
            if (source.Difficulty < 0) throw new ArgumentOutOfRangeException("target", "TypingTarget.difficulty cannot be negative.");
            if (source.RewardValue < 0) throw new ArgumentOutOfRangeException("target", "TypingTarget.rewardValue cannot be negative.");

            var displayElements = SplitTextElements(NormalizeText(source.DisplayText));
            if (displayElements.Count == 0) throw new ArgumentException("TypingTarget.displayText has no typable text elements.", "target");

            var accepted = source.AcceptedInputs == null || source.AcceptedInputs.Count == 0
                ? new List<string> { source.DisplayText }
                : source.AcceptedInputs.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (accepted.Count == 0) accepted.Add(source.DisplayText);

            var candidates = new List<IList<string>>();
            foreach (var candidate in accepted)
            {
                var elements = SplitTextElements(NormalizeText(candidate));
                if (elements.Count != displayElements.Count)
                    throw new ArgumentException("Each acceptedInputs entry must have the same text-element count as displayText. AI07 should normalize Telex/VNI into display characters before core matching.", "target");
                candidates.Add(elements);
            }

            return new PreparedTarget
            {
                Target = CloneTarget(source, accepted),
                Candidates = candidates
            };
        }

        private bool IsAbnormalRepeatLocked(TypingInput input, string normalized)
        {
            if (_lastTimestamp == long.MinValue) return false;
            if (!string.Equals(_lastRawKey ?? string.Empty, input.RawKey ?? string.Empty, StringComparison.Ordinal)) return false;
            if (!string.Equals(_lastNormalizedKey ?? string.Empty, normalized, StringComparison.Ordinal)) return false;
            if (!string.Equals(_lastSource ?? string.Empty, input.Source ?? string.Empty, StringComparison.Ordinal)) return false;
            if (input.Timestamp <= _lastTimestamp) return true;
            return input.Timestamp - _lastTimestamp < _duplicateWindowMs;
        }

        private void RememberInputLocked(TypingInput input, string normalized)
        {
            _lastRawKey = input.RawKey;
            _lastNormalizedKey = normalized;
            _lastSource = input.Source;
            _lastTimestamp = input.Timestamp;
        }

        private void ResetRepeatGuardLocked()
        {
            _lastRawKey = null;
            _lastNormalizedKey = null;
            _lastSource = null;
            _lastTimestamp = long.MinValue;
        }

        private int CurrentTotalCountLocked()
        {
            for (var i = 0; i < _candidateAlive.Length; i++)
                if (_candidateAlive[i]) return _candidateElements[i].Count;
            return _candidateElements.Count == 0 ? 0 : _candidateElements[0].Count;
        }

        private TypingCoreEvent NewEvent(string type, long timestamp, int? value, string text)
        {
            return new TypingCoreEvent
            {
                Type = type,
                TargetId = _target == null ? null : _target.Id,
                Value = value,
                Text = text,
                Timestamp = timestamp
            };
        }

        private TypingEngineResult ResultLocked(bool accepted, bool ignored, string reason, IList<TypingCoreEvent> events)
        {
            return new TypingEngineResult
            {
                Accepted = accepted,
                Ignored = ignored,
                Reason = reason,
                Events = events == null ? new List<TypingCoreEvent>() : events.Select(CloneEvent).ToList(),
                Snapshot = BuildSnapshotLocked()
            };
        }

        private TypingEngineSnapshot BuildSnapshotLocked()
        {
            return new TypingEngineSnapshot
            {
                Phase = _phase,
                Paused = _phase == TypingGamePhase.Paused,
                ActiveTarget = CloneTarget(_target, _target == null ? null : _target.AcceptedInputs),
                Progress = _target == null ? null : new TypingProgress
                {
                    TargetId = _target.Id,
                    TypedCount = _typedCount,
                    TotalCount = CurrentTotalCountLocked(),
                    Completed = _completed,
                    ErrorCount = _errorCount
                }
            };
        }

        private static TypingCoreEvent CloneEvent(TypingCoreEvent source)
        {
            if (source == null) return null;
            return new TypingCoreEvent
            {
                Type = source.Type,
                TargetId = source.TargetId,
                Value = source.Value,
                Text = source.Text,
                Timestamp = source.Timestamp
            };
        }

        private static TypingTarget CloneTarget(TypingTarget source, IEnumerable<string> accepted)
        {
            if (source == null) return null;
            return new TypingTarget
            {
                Id = source.Id,
                DisplayText = source.DisplayText,
                AcceptedInputs = accepted == null ? new List<string>() : accepted.ToList(),
                Language = source.Language,
                Difficulty = source.Difficulty,
                Kind = source.Kind,
                RewardValue = source.RewardValue
            };
        }

        private static string NormalizeKey(TypingInput input)
        {
            var value = string.IsNullOrEmpty(input.NormalizedKey) ? input.RawKey : input.NormalizedKey;
            return NormalizeText(value);
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }

        private static IList<string> SplitTextElements(string value)
        {
            var result = new List<string>();
            var enumerator = StringInfo.GetTextElementEnumerator(value ?? string.Empty);
            while (enumerator.MoveNext()) result.Add((string)enumerator.Current);
            return result;
        }

        private static void ValidateSource(string source)
        {
            if (!string.Equals(source, TypingInputSource.Physical, StringComparison.Ordinal) &&
                !string.Equals(source, TypingInputSource.Onscreen, StringComparison.Ordinal))
                throw new ArgumentException("TypingInput.source must be 'physical' or 'onscreen'.", "input");
        }

        private static bool IsAllowedKind(string kind)
        {
            return string.Equals(kind, TypingTargetKind.Shoot, StringComparison.Ordinal) ||
                   string.Equals(kind, TypingTargetKind.Rescue, StringComparison.Ordinal) ||
                   string.Equals(kind, TypingTargetKind.Unlock, StringComparison.Ordinal) ||
                   string.Equals(kind, TypingTargetKind.Boss, StringComparison.Ordinal);
        }

        private sealed class PreparedTarget
        {
            public TypingTarget Target { get; set; }
            public List<IList<string>> Candidates { get; set; }
        }
    }
}
