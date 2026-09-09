using System;
using System.Collections.Generic;

namespace WAHU.TypingSpace.Combat
{
    public sealed class TypingSpaceCombatController
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, TypingCombatTargetBinding> _targets = new Dictionary<string, TypingCombatTargetBinding>(StringComparer.Ordinal);
        private readonly HashSet<string> _completedNonBossTargets = new HashSet<string>(StringComparer.Ordinal);
        private readonly TypingSpaceCombatSettings _settings;
        private readonly TypingSpaceBossController _boss;

        private ShipActionMode _mode = ShipActionMode.Idle;
        private string _activeTargetId;
        private int _charge;
        private long _actionUntilTimestamp;
        private long _lastTimestamp;
        private long _actionSequence;
        private bool _paused;
        private long _pausedAtTimestamp;

        public TypingSpaceCombatController()
            : this(TypingSpaceCombatSettings.CreateDefault())
        {
        }

        public TypingSpaceCombatController(TypingSpaceCombatSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException("settings");
            _settings.Validate();
            _boss = new TypingSpaceBossController(_settings.BossRequiredHits);
        }

        public event EventHandler<ShipActionChangedEventArgs> StateChanged;
        public event EventHandler<CombatOutputEventArgs> OutputEmitted;

        public ShipActionSnapshot CurrentState
        {
            get
            {
                lock (_gate) return BuildSnapshotLocked();
            }
        }

        public bool RegisterTarget(TypingCombatTargetBinding target)
        {
            if (target == null) throw new ArgumentNullException("target");
            if (string.IsNullOrWhiteSpace(target.Id)) throw new ArgumentException("Target id is required.", "target");
            if (!TypingCombatTargetKinds.IsSupported(target.Kind)) throw new ArgumentException("Unsupported target kind: " + target.Kind, "target");

            lock (_gate)
            {
                TypingCombatTargetBinding existing;
                if (_targets.TryGetValue(target.Id, out existing))
                {
                    if (!string.Equals(existing.Kind, target.Kind, StringComparison.Ordinal))
                        throw new InvalidOperationException("Target id already registered with a different kind: " + target.Id);
                    return false;
                }

                _targets.Add(target.Id, new TypingCombatTargetBinding { Id = target.Id, Kind = target.Kind });
                if (string.Equals(target.Kind, TypingCombatTargetKinds.Boss, StringComparison.Ordinal)) _boss.Activate();
                return true;
            }
        }

        public bool RegisterTarget(string id, string kind)
        {
            return RegisterTarget(new TypingCombatTargetBinding { Id = id, Kind = kind });
        }

        public bool HandleCoreEvent(string type, string targetId, int? value, string text, long timestamp)
        {
            return HandleEvent(new TypingCombatEvent
            {
                Type = type,
                TargetId = targetId,
                Value = value,
                Text = text,
                Timestamp = timestamp
            });
        }

        public bool HandleEvent(TypingCombatEvent input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.Type)) return false;

            ShipActionChangedEventArgs stateChange = null;
            List<CombatOutputEvent> outputs = null;
            bool handled;

            lock (_gate)
            {
                var isPause = string.Equals(input.Type, TypingCombatEventTypes.GamePaused, StringComparison.Ordinal);
                var isResume = string.Equals(input.Type, TypingCombatEventTypes.GameResumed, StringComparison.Ordinal);

                if (_paused)
                {
                    if (!isResume)
                    {
                        // While paused the combat clock is frozen. Duplicate pause/input events do not
                        // advance timestamps or mutate combat state.
                        handled = false;
                    }
                    else
                    {
                        var resumeTimestamp = NormalizeTimestampLocked(input.Timestamp);
                        handled = HandleResumeLocked(resumeTimestamp, out stateChange);
                    }
                }
                else if (isResume)
                {
                    // A stray resume is ignored without advancing the deterministic combat clock.
                    handled = false;
                }
                else
                {
                    var timestamp = NormalizeTimestampLocked(input.Timestamp);
                    if (isPause)
                    {
                        handled = HandlePauseLocked(timestamp, out stateChange);
                    }
                    else
                    {
                        stateChange = AdvanceLocked(timestamp, "action_elapsed");
                        if (string.Equals(input.Type, TypingCombatEventTypes.CharCorrect, StringComparison.Ordinal))
                        {
                            var previous = BuildSnapshotLocked();
                            _charge = Math.Min(_settings.MaxCharge, _charge + Math.Max(1, input.Value ?? 1));
                            _actionSequence++;
                            if (_mode == ShipActionMode.Idle || _mode == ShipActionMode.Charging)
                            {
                                _mode = ShipActionMode.Charging;
                                _activeTargetId = input.TargetId;
                            }
                            stateChange = NewChange(previous, BuildSnapshotLocked(), "char_correct_charge_pulse");
                            handled = true;
                        }
                        else if (string.Equals(input.Type, TypingCombatEventTypes.CharWrong, StringComparison.Ordinal))
                        {
                            handled = true;
                        }
                        else if (string.Equals(input.Type, TypingCombatEventTypes.WordCompleted, StringComparison.Ordinal))
                        {
                            ShipActionChangedEventArgs wordChange;
                            handled = HandleWordCompletedLocked(input.TargetId, timestamp, out wordChange, out outputs);
                            if (wordChange != null) stateChange = wordChange;
                        }
                        else
                        {
                            handled = false;
                        }
                    }
                }
            }

            PublishStateChange(stateChange);
            PublishOutputs(outputs);
            return handled;
        }

        public void Advance(long timestamp)
        {
            ShipActionChangedEventArgs change;
            lock (_gate)
            {
                if (_paused)
                {
                    change = null;
                }
                else
                {
                    var normalized = NormalizeTimestampLocked(timestamp);
                    change = AdvanceLocked(normalized, "action_elapsed");
                }
            }
            PublishStateChange(change);
        }

        private bool HandleWordCompletedLocked(string targetId, long timestamp, out ShipActionChangedEventArgs change, out List<CombatOutputEvent> outputs)
        {
            change = null;
            outputs = null;
            if (string.IsNullOrWhiteSpace(targetId)) return false;

            TypingCombatTargetBinding target;
            if (!_targets.TryGetValue(targetId, out target)) return false;

            var previous = BuildSnapshotLocked();
            if (string.Equals(target.Kind, TypingCombatTargetKinds.Boss, StringComparison.Ordinal))
            {
                var hit = _boss.TryHit(targetId);
                if (!hit.Accepted) return false;

                BeginActionLocked(ShipActionMode.BossStrike, targetId, timestamp, _settings.BossStrikeDurationMs);
                outputs = new List<CombatOutputEvent>
                {
                    NewOutput(TypingCombatEventTypes.BossHit, targetId, 1, "boss_hit_" + hit.Snapshot.HitsTaken, timestamp)
                };
                if (hit.DefeatedNow)
                {
                    outputs.Add(NewOutput(TypingCombatEventTypes.BossDefeated, targetId, hit.Snapshot.RequiredHits, "boss_defeated", timestamp));
                }
                change = NewChange(previous, BuildSnapshotLocked(), hit.DefeatedNow ? "boss_defeated" : "boss_hit");
                return true;
            }

            if (!_completedNonBossTargets.Add(targetId)) return false;

            if (string.Equals(target.Kind, TypingCombatTargetKinds.Shoot, StringComparison.Ordinal))
            {
                BeginActionLocked(ShipActionMode.Firing, targetId, timestamp, _settings.FireDurationMs);
                outputs = new List<CombatOutputEvent> { NewOutput(TypingCombatEventTypes.TargetDestroyed, targetId, 1, null, timestamp) };
                change = NewChange(previous, BuildSnapshotLocked(), "shoot_target_completed");
                return true;
            }

            if (string.Equals(target.Kind, TypingCombatTargetKinds.Rescue, StringComparison.Ordinal))
            {
                BeginActionLocked(ShipActionMode.RescueBeam, targetId, timestamp, _settings.RescueBeamDurationMs);
                outputs = new List<CombatOutputEvent> { NewOutput(TypingCombatEventTypes.TargetRescued, targetId, 1, null, timestamp) };
                change = NewChange(previous, BuildSnapshotLocked(), "rescue_target_completed");
                return true;
            }

            if (string.Equals(target.Kind, TypingCombatTargetKinds.Unlock, StringComparison.Ordinal))
            {
                BeginActionLocked(ShipActionMode.UnlockBeam, targetId, timestamp, _settings.UnlockBeamDurationMs);
                change = NewChange(previous, BuildSnapshotLocked(), "unlock_target_completed");
                return true;
            }

            return false;
        }

        private void BeginActionLocked(ShipActionMode mode, string targetId, long timestamp, int durationMs)
        {
            _mode = mode;
            _activeTargetId = targetId;
            _charge = 0;
            _actionSequence++;
            _actionUntilTimestamp = timestamp + Math.Max(0, durationMs);
        }

        private ShipActionChangedEventArgs AdvanceLocked(long timestamp, string reason)
        {
            if (_mode == ShipActionMode.Idle || _mode == ShipActionMode.Charging) return null;
            if (timestamp < _actionUntilTimestamp) return null;

            var previous = BuildSnapshotLocked();
            _mode = _charge > 0 ? ShipActionMode.Charging : ShipActionMode.Idle;
            if (_mode == ShipActionMode.Idle) _activeTargetId = null;
            _actionUntilTimestamp = 0;
            return NewChange(previous, BuildSnapshotLocked(), reason);
        }

        private bool HandlePauseLocked(long timestamp, out ShipActionChangedEventArgs change)
        {
            change = null;
            if (_paused) return false;
            var previous = BuildSnapshotLocked();
            _paused = true;
            _pausedAtTimestamp = timestamp;
            change = NewChange(previous, BuildSnapshotLocked(), "game_paused");
            return true;
        }

        private bool HandleResumeLocked(long timestamp, out ShipActionChangedEventArgs change)
        {
            change = null;
            if (!_paused) return false;
            var previous = BuildSnapshotLocked();
            var pausedDuration = Math.Max(0, timestamp - _pausedAtTimestamp);
            if (_actionUntilTimestamp > 0) _actionUntilTimestamp += pausedDuration;
            _paused = false;
            _pausedAtTimestamp = 0;
            change = NewChange(previous, BuildSnapshotLocked(), "game_resumed");
            return true;
        }

        private long NormalizeTimestampLocked(long timestamp)
        {
            if (timestamp < 0) timestamp = 0;
            if (timestamp < _lastTimestamp) timestamp = _lastTimestamp;
            _lastTimestamp = timestamp;
            return timestamp;
        }

        private ShipActionSnapshot BuildSnapshotLocked()
        {
            return new ShipActionSnapshot
            {
                Mode = _mode,
                ActiveTargetId = _activeTargetId,
                Charge = _charge,
                MaxCharge = _settings.MaxCharge,
                ActionUntilTimestamp = _actionUntilTimestamp,
                LastTimestamp = _lastTimestamp,
                ActionSequence = _actionSequence,
                Paused = _paused,
                InputLocked = _paused,
                Boss = _boss.Snapshot()
            };
        }

        private static ShipActionChangedEventArgs NewChange(ShipActionSnapshot previous, ShipActionSnapshot current, string reason)
        {
            return new ShipActionChangedEventArgs { Previous = previous, Current = current, Reason = reason };
        }

        private static CombatOutputEvent NewOutput(string type, string targetId, int? value, string text, long timestamp)
        {
            return new CombatOutputEvent { Type = type, TargetId = targetId, Value = value, Text = text, Timestamp = timestamp };
        }

        private void PublishStateChange(ShipActionChangedEventArgs change)
        {
            if (change == null) return;
            var handler = StateChanged;
            if (handler == null) return;
            foreach (EventHandler<ShipActionChangedEventArgs> subscriber in handler.GetInvocationList())
            {
                var isolated = new ShipActionChangedEventArgs
                {
                    Previous = CloneSnapshot(change.Previous),
                    Current = CloneSnapshot(change.Current),
                    Reason = change.Reason
                };
                try { subscriber(this, isolated); } catch { }
            }
        }

        private void PublishOutputs(IList<CombatOutputEvent> outputs)
        {
            if (outputs == null || outputs.Count == 0) return;
            var handler = OutputEmitted;
            if (handler == null) return;
            foreach (var output in outputs)
            {
                foreach (EventHandler<CombatOutputEventArgs> subscriber in handler.GetInvocationList())
                {
                    var isolated = new CombatOutputEventArgs { Event = CloneOutput(output) };
                    try { subscriber(this, isolated); } catch { }
                }
            }
        }

        private static ShipActionSnapshot CloneSnapshot(ShipActionSnapshot source)
        {
            if (source == null) return null;
            return new ShipActionSnapshot
            {
                Mode = source.Mode,
                ActiveTargetId = source.ActiveTargetId,
                Charge = source.Charge,
                MaxCharge = source.MaxCharge,
                ActionUntilTimestamp = source.ActionUntilTimestamp,
                LastTimestamp = source.LastTimestamp,
                ActionSequence = source.ActionSequence,
                Paused = source.Paused,
                InputLocked = source.InputLocked,
                Boss = CloneBoss(source.Boss)
            };
        }

        private static BossSnapshot CloneBoss(BossSnapshot source)
        {
            if (source == null) return null;
            return new BossSnapshot
            {
                RequiredHits = source.RequiredHits,
                HitsTaken = source.HitsTaken,
                RemainingHits = source.RemainingHits,
                PhaseNumber = source.PhaseNumber,
                Active = source.Active,
                Defeated = source.Defeated
            };
        }

        private static CombatOutputEvent CloneOutput(CombatOutputEvent source)
        {
            if (source == null) return null;
            return new CombatOutputEvent
            {
                Type = source.Type,
                TargetId = source.TargetId,
                Value = source.Value,
                Text = source.Text,
                Timestamp = source.Timestamp
            };
        }
    }
}
