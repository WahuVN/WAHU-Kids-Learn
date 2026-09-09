using System;
using System.IO;

namespace WAHU.Audio
{
    public enum RescueGameAudioCue
    {
        PositiveAnswer = 0,
        CheckpointStar = 1,
        MissionComplete = 2,
        GardenUnlock = 3,
        MissionMusic = 4
    }

    /// <summary>
    /// Optional audio adapter for the rescue game. The reward/gameplay layers only need semantic cues;
    /// WAV files can be supplied later without changing gameplay. Missing/corrupt assets, unavailable
    /// devices and backend failures are converted to AudioPlaybackResult failures and never escape into
    /// the child-facing game flow.
    /// </summary>
    public sealed class RescueGameAudioHooks
    {
        public const string PositiveAnswerFile = "rescue_positive_answer.wav";
        public const string CheckpointStarFile = "rescue_checkpoint_star.wav";
        public const string MissionCompleteFile = "rescue_mission_complete.wav";
        public const string GardenUnlockFile = "rescue_garden_unlock.wav";
        public const string MissionMusicFile = "rescue_mission_loop.wav";

        private readonly AudioCoordinator _audio;
        private readonly string _audioRoot;

        public RescueGameAudioHooks(AudioCoordinator audio, string audioRoot)
        {
            _audio = audio ?? throw new ArgumentNullException("audio");
            _audioRoot = string.IsNullOrWhiteSpace(audioRoot) ? string.Empty : audioRoot;
        }

        public AudioPlaybackResult PlayPositiveAnswer()
        {
            return Play(RescueGameAudioCue.PositiveAnswer);
        }

        public AudioPlaybackResult PlayCheckpointStar()
        {
            return Play(RescueGameAudioCue.CheckpointStar);
        }

        public AudioPlaybackResult PlayMissionComplete()
        {
            return Play(RescueGameAudioCue.MissionComplete);
        }

        public AudioPlaybackResult PlayGardenUnlock()
        {
            return Play(RescueGameAudioCue.GardenUnlock);
        }

        /// <summary>
        /// Bridges a durable reward transition to exactly one child-facing cue. Terminal reward wins
        /// over checkpoint feedback so GAME_COMPLETE never stacks two sounds on top of each other.
        /// A newly unlocked Garden item gets the Garden cue; otherwise completion gets the fixed
        /// mission-complete cue. Resume/no-change transitions intentionally stay silent.
        /// </summary>
        public AudioPlaybackResult PlayRewardFeedback(
            int newlyEarnedCheckpointStars,
            bool finalChestNewlyUnlocked,
            bool gardenItemNewlyUnlocked)
        {
            if (finalChestNewlyUnlocked)
            {
                if (!gardenItemNewlyUnlocked) return PlayMissionComplete();
                var garden = PlayGardenUnlock();
                if (garden.Played) return garden;
                var fallback = PlayMissionComplete();
                return fallback.Played ? fallback : garden;
            }
            if (newlyEarnedCheckpointStars > 0)
                return PlayCheckpointStar();
            return Denied("no_reward_transition");
        }

        /// <summary>
        /// Music remains opt-in through AudioCoordinator.MusicEnabled. Calling this while music is
        /// disabled is safe and returns audio_channel_disabled.
        /// </summary>
        public AudioPlaybackResult TryStartMissionMusic()
        {
            return Play(RescueGameAudioCue.MissionMusic);
        }

        public AudioPlaybackResult Play(RescueGameAudioCue cue)
        {
            try
            {
                string file;
                AudioPriority priority;
                bool loop;
                switch (cue)
                {
                    case RescueGameAudioCue.PositiveAnswer:
                        file = PositiveAnswerFile;
                        priority = AudioPriority.Sfx;
                        loop = false;
                        break;
                    case RescueGameAudioCue.CheckpointStar:
                        file = CheckpointStarFile;
                        priority = AudioPriority.Sfx;
                        loop = false;
                        break;
                    case RescueGameAudioCue.MissionComplete:
                        file = MissionCompleteFile;
                        priority = AudioPriority.ImportantSignal;
                        loop = false;
                        break;
                    case RescueGameAudioCue.GardenUnlock:
                        file = GardenUnlockFile;
                        priority = AudioPriority.ImportantSignal;
                        loop = false;
                        break;
                    case RescueGameAudioCue.MissionMusic:
                        file = MissionMusicFile;
                        priority = AudioPriority.Music;
                        loop = true;
                        break;
                    default:
                        return Denied("rescue_audio_cue_unknown");
                }

                var path = string.IsNullOrEmpty(_audioRoot) ? file : Path.Combine(_audioRoot, file);
                return _audio.TryPlay(new AudioPlaybackRequest
                {
                    Path = path,
                    Priority = priority,
                    Loop = loop
                });
            }
            catch (Exception ex)
            {
                return Denied("rescue_audio_hook_failure:" + ex.GetType().Name);
            }
        }

        private static AudioPlaybackResult Denied(string reason)
        {
            return new AudioPlaybackResult
            {
                Played = false,
                Reason = reason,
                DurationMs = 0
            };
        }
    }
}
