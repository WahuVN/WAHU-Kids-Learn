using System;
using System.Collections.Generic;
using System.IO;

namespace WAHU.Audio
{
    public enum TypingGameSfxCue
    {
        CharacterCharge = 0,
        GentleWrong = 1,
        WordBeam = 2,
        Rescue = 3,
        Unlock = 4,
        BossHit = 5,
        BossDefeated = 6,
        LevelComplete = 7
    }

    public sealed class TypingGameSfxAsset
    {
        public TypingGameSfxCue Cue { get; set; }
        public string FileName { get; set; }
        public TypingGameSfxCue? FallbackCue { get; set; }
        public AudioPriority Priority { get; set; }
        public double Volume { get; set; }
        public int MinIntervalMs { get; set; }
    }

    /// <summary>
    /// Small semantic registry owned by the typing-space feedback layer. Gameplay publishes events;
    /// this registry owns filenames and fallback choices so missing audio never leaks into gameplay.
    /// </summary>
    public sealed class TypingGameSfxRegistry
    {
        private readonly Dictionary<TypingGameSfxCue, TypingGameSfxAsset> _assets =
            new Dictionary<TypingGameSfxCue, TypingGameSfxAsset>();

        public TypingGameSfxRegistry()
        {
            Register(new TypingGameSfxAsset { Cue = TypingGameSfxCue.CharacterCharge, FileName = "typing_char_charge.wav", Priority = AudioPriority.Sfx, Volume = 0.48, MinIntervalMs = 35 });
            Register(new TypingGameSfxAsset { Cue = TypingGameSfxCue.GentleWrong, FileName = "typing_wrong_soft.wav", Priority = AudioPriority.Sfx, Volume = 0.32, MinIntervalMs = 90 });
            Register(new TypingGameSfxAsset { Cue = TypingGameSfxCue.WordBeam, FileName = "typing_word_beam.wav", FallbackCue = TypingGameSfxCue.CharacterCharge, Priority = AudioPriority.ImportantSignal, Volume = 0.68, MinIntervalMs = 80 });
            Register(new TypingGameSfxAsset { Cue = TypingGameSfxCue.Rescue, FileName = "typing_rescue.wav", FallbackCue = TypingGameSfxCue.WordBeam, Priority = AudioPriority.ImportantSignal, Volume = 0.72, MinIntervalMs = 120 });
            Register(new TypingGameSfxAsset { Cue = TypingGameSfxCue.Unlock, FileName = "typing_unlock.wav", FallbackCue = TypingGameSfxCue.WordBeam, Priority = AudioPriority.ImportantSignal, Volume = 0.72, MinIntervalMs = 120 });
            Register(new TypingGameSfxAsset { Cue = TypingGameSfxCue.BossHit, FileName = "typing_boss_hit.wav", FallbackCue = TypingGameSfxCue.WordBeam, Priority = AudioPriority.ImportantSignal, Volume = 0.74, MinIntervalMs = 100 });
            Register(new TypingGameSfxAsset { Cue = TypingGameSfxCue.BossDefeated, FileName = "typing_boss_defeated.wav", FallbackCue = TypingGameSfxCue.LevelComplete, Priority = AudioPriority.ImportantSignal, Volume = 0.86, MinIntervalMs = 300 });
            Register(new TypingGameSfxAsset { Cue = TypingGameSfxCue.LevelComplete, FileName = "typing_level_complete.wav", FallbackCue = TypingGameSfxCue.WordBeam, Priority = AudioPriority.ImportantSignal, Volume = 0.82, MinIntervalMs = 300 });
        }

        public void Register(TypingGameSfxAsset asset)
        {
            if (asset == null) throw new ArgumentNullException("asset");
            if (string.IsNullOrWhiteSpace(asset.FileName)) throw new ArgumentException("Audio filename is required.", "asset");
            asset.Volume = Clamp01(asset.Volume);
            asset.MinIntervalMs = Math.Max(0, asset.MinIntervalMs);
            _assets[asset.Cue] = asset;
        }

        public bool TryGet(TypingGameSfxCue cue, out TypingGameSfxAsset asset)
        {
            return _assets.TryGetValue(cue, out asset);
        }

        private static double Clamp01(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 1.0;
            return Math.Max(0.0, Math.Min(1.0, value));
        }
    }

    public sealed class TypingGameAudioFeedback
    {
        private readonly AudioCoordinator _audio;
        private readonly TypingGameSfxRegistry _registry;
        private readonly string _audioRoot;
        private readonly Dictionary<TypingGameSfxCue, long> _lastPlayedAt = new Dictionary<TypingGameSfxCue, long>();
        private double _sfxVolume = 1.0;

        public TypingGameAudioFeedback(AudioCoordinator audio, string audioRoot, TypingGameSfxRegistry registry = null)
        {
            _audio = audio ?? throw new ArgumentNullException("audio");
            _audioRoot = string.IsNullOrWhiteSpace(audioRoot) ? string.Empty : audioRoot;
            _registry = registry ?? new TypingGameSfxRegistry();
        }

        /// <summary>Logical typing-game SFX gain. Backends supporting IAudioVolumeBackend receive it.</summary>
        public double SfxVolume
        {
            get { return _sfxVolume; }
            set
            {
                var next = Clamp01(value);
                if (Math.Abs(next - _sfxVolume) < 0.0001) return;
                _sfxVolume = next;
                var changed = SfxVolumeChanged;
                if (changed != null) changed(next);
            }
        }

        public event Action<double> SfxVolumeChanged;

        public AudioPlaybackResult HandleEvent(string eventType, long timestamp)
        {
            TypingGameSfxCue cue;
            switch (eventType ?? string.Empty)
            {
                case "TYPING_CHAR_CORRECT": cue = TypingGameSfxCue.CharacterCharge; break;
                case "TYPING_CHAR_WRONG": cue = TypingGameSfxCue.GentleWrong; break;
                case "TYPING_WORD_COMPLETED":
                case "TARGET_DESTROYED": cue = TypingGameSfxCue.WordBeam; break;
                case "TARGET_RESCUED": cue = TypingGameSfxCue.Rescue; break;
                case "REWARD_GRANTED": cue = TypingGameSfxCue.Unlock; break;
                case "BOSS_HIT": cue = TypingGameSfxCue.BossHit; break;
                case "BOSS_DEFEATED": cue = TypingGameSfxCue.BossDefeated; break;
                case "LEVEL_COMPLETED": cue = TypingGameSfxCue.LevelComplete; break;
                default: return Denied("typing_audio_event_ignored");
            }
            return PlayCue(cue, timestamp);
        }

        public AudioPlaybackResult PlayCue(TypingGameSfxCue cue, long timestamp)
        {
            TypingGameSfxAsset asset;
            if (!_registry.TryGet(cue, out asset)) return Denied("typing_audio_cue_unregistered");

            long previous;
            if (_lastPlayedAt.TryGetValue(cue, out previous) && timestamp >= previous && timestamp - previous < asset.MinIntervalMs)
                return Denied("typing_audio_throttled");

            var visited = new HashSet<TypingGameSfxCue>();
            var result = TryPlayWithFallback(cue, visited);
            if (result.Played) _lastPlayedAt[cue] = timestamp;
            return result;
        }

        private AudioPlaybackResult TryPlayWithFallback(TypingGameSfxCue cue, HashSet<TypingGameSfxCue> visited)
        {
            if (!visited.Add(cue)) return Denied("typing_audio_fallback_cycle");

            TypingGameSfxAsset asset;
            if (!_registry.TryGet(cue, out asset)) return Denied("typing_audio_cue_unregistered");
            var path = string.IsNullOrEmpty(_audioRoot) ? asset.FileName : Path.Combine(_audioRoot, asset.FileName);
            var result = _audio.TryPlay(new AudioPlaybackRequest
            {
                Path = path,
                Priority = asset.Priority,
                Loop = false,
                Volume = Clamp01(asset.Volume * _sfxVolume)
            });
            if (result.Played || !asset.FallbackCue.HasValue) return result;

            // Only asset/clip failures fall through. Device/channel/priority failures are intentional runtime policy.
            if (!IsAssetFailure(result.Reason)) return result;
            return TryPlayWithFallback(asset.FallbackCue.Value, visited);
        }

        private static bool IsAssetFailure(string reason)
        {
            return string.Equals(reason, "audio_file_missing", StringComparison.Ordinal) ||
                   (reason != null && reason.StartsWith("audio_clip_invalid:", StringComparison.Ordinal));
        }

        private static double Clamp01(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 1.0;
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private static AudioPlaybackResult Denied(string reason)
        {
            return new AudioPlaybackResult { Played = false, Reason = reason, DurationMs = 0 };
        }
    }
}
