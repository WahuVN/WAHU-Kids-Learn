using System;
using System.Collections.Generic;

namespace WAHU.TypingSpace.Combat
{
    internal sealed class TypingSpaceBossController
    {
        private readonly int _requiredHits;
        private readonly HashSet<string> _hitTargetIds = new HashSet<string>(StringComparer.Ordinal);
        private int _hitsTaken;
        private bool _active;
        private bool _defeated;

        public TypingSpaceBossController(int requiredHits)
        {
            if (requiredHits < 1) throw new ArgumentOutOfRangeException("requiredHits");
            _requiredHits = requiredHits;
        }

        public BossHitResult TryHit(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId)) return BossHitResult.Rejected(Snapshot());
            if (_defeated) return BossHitResult.Rejected(Snapshot());
            if (!_hitTargetIds.Add(targetId)) return BossHitResult.Rejected(Snapshot());

            _active = true;
            _hitsTaken++;
            if (_hitsTaken >= _requiredHits)
            {
                _hitsTaken = _requiredHits;
                _defeated = true;
                _active = false;
            }

            return new BossHitResult
            {
                Accepted = true,
                DefeatedNow = _defeated,
                Snapshot = Snapshot()
            };
        }

        public void Activate()
        {
            if (!_defeated) _active = true;
        }

        public BossSnapshot Snapshot()
        {
            var remaining = Math.Max(0, _requiredHits - _hitsTaken);
            var phase = _defeated ? _requiredHits : Math.Min(_requiredHits, _hitsTaken + 1);
            return new BossSnapshot
            {
                RequiredHits = _requiredHits,
                HitsTaken = _hitsTaken,
                RemainingHits = remaining,
                PhaseNumber = phase,
                Active = _active,
                Defeated = _defeated
            };
        }
    }

    internal sealed class BossHitResult
    {
        public bool Accepted { get; set; }
        public bool DefeatedNow { get; set; }
        public BossSnapshot Snapshot { get; set; }

        public static BossHitResult Rejected(BossSnapshot snapshot)
        {
            return new BossHitResult { Accepted = false, DefeatedNow = false, Snapshot = snapshot };
        }
    }
}
