using System;
using System.Collections.Generic;
using System.IO;
using WAHU.Audio;

namespace WAHU.AudioRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        private static void Main()
        {
            var root = Path.Combine(Path.GetTempPath(), "wahu-audio-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var shortWav = Path.Combine(root, "short.wav");
                var otherWav = Path.Combine(root, "other.wav");
                WritePcmWave(shortWav, 8000, 1, 16, 800);
                WritePcmWave(otherWav, 8000, 1, 16, 400);
                TestInspector(shortWav, root);
                TestCache(shortWav, otherWav);
                TestCoordinator(shortWav, otherWav);
                TestRescueHooks(shortWav, otherWav, root);
                Console.WriteLine("AUDIO_RUNTIME_SMOKE_PASS assertions=" + _assertions);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static void TestInspector(string wav, string root)
        {
            var inspector = new WavePcmInspector();
            var info = inspector.Inspect(wav);
            Assert(info.Channels == 1 && info.SampleRate == 8000 && info.BitsPerSample == 16, "pcm_format_detected");
            Assert(info.DurationMs > 790 && info.DurationMs < 810, "pcm_duration_detected");

            var bad = Path.Combine(root, "bad.wav");
            File.WriteAllText(bad, "not-wave");
            var rejected = false;
            try { inspector.Inspect(bad); } catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "invalid_wave_rejected");
        }

        private static void TestCache(string a, string b)
        {
            var sizeA = new FileInfo(a).Length;
            var sizeB = new FileInfo(b).Length;
            var cache = new BoundedWaveCache(Math.Max(65536, sizeA + 16));
            var x1 = cache.GetOrLoad(a);
            var x2 = cache.GetOrLoad(a);
            Assert(object.ReferenceEquals(x1, x2), "cache_reuses_clip_bytes");
            cache.GetOrLoad(b);
            Assert(cache.CurrentBytes <= Math.Max(65536, sizeA + 16), "cache_respects_byte_budget");
        }

        private static void TestCoordinator(string voice, string sfx)
        {
            var backend = new FakeBackend();
            using (var audio = new AudioCoordinator(backend, 128 * 1024))
            {
                Assert(audio.VoiceEnabled && audio.SfxEnabled && !audio.MusicEnabled, "default_audio_channels");
                var voiceResult = audio.PlayVoice(voice);
                Assert(voiceResult.Played && backend.PlayCount == 1, "voice_plays");
                var sfxBlocked = audio.TryPlay(new AudioPlaybackRequest { Path = sfx, Priority = AudioPriority.Sfx });
                Assert(!sfxBlocked.Played && sfxBlocked.Reason == "higher_priority_audio_active", "sfx_blocked_during_voice");
                var replay = audio.ReplayLastVoice();
                Assert(replay.Played && backend.PlayCount == 2, "voice_replay_unlimited");
                var music = audio.TryPlay(new AudioPlaybackRequest { Path = sfx, Priority = AudioPriority.Music, Loop = true });
                Assert(!music.Played && music.Reason == "audio_channel_disabled", "music_off_by_default");
                var missing = audio.PlayVoice(Path.Combine(Path.GetDirectoryName(voice), "missing.wav"));
                Assert(!missing.Played && missing.Reason == "audio_file_missing", "missing_clip_safe_fallback");
                audio.VoiceEnabled = false;
                Assert(!audio.PlayVoice(voice).Played, "voice_toggle_respected");
                audio.VoiceEnabled = true;
                backend.Available = false;
                Assert(audio.PlayVoice(voice).Reason == "audio_device_unavailable", "missing_device_safe_mode");
            }

            var failing = new FakeBackend { ThrowOnPlay = true };
            using (var audio = new AudioCoordinator(failing, 128 * 1024))
                Assert(audio.PlayVoice(voice).Reason == "audio_backend_failure", "backend_failure_no_crash");
        }

        private static void TestRescueHooks(string sourceWav, string shortGardenWav, string root)
        {
            var cueRoot = Path.Combine(root, "rescue-cues");
            Directory.CreateDirectory(cueRoot);
            foreach (var file in new[]
            {
                RescueGameAudioHooks.PositiveAnswerFile,
                RescueGameAudioHooks.CheckpointStarFile,
                RescueGameAudioHooks.MissionCompleteFile,
                RescueGameAudioHooks.GardenUnlockFile,
                RescueGameAudioHooks.MissionMusicFile
            })
                File.Copy(sourceWav, Path.Combine(cueRoot, file), true);
            File.Copy(shortGardenWav, Path.Combine(cueRoot, RescueGameAudioHooks.GardenUnlockFile), true);

            var backend = new FakeBackend();
            using (var audio = new AudioCoordinator(backend, 128 * 1024))
            {
                var hooks = new RescueGameAudioHooks(audio, cueRoot);
                Assert(hooks.PlayPositiveAnswer().Played, "rescue_positive_answer_hook_plays");
                audio.Stop();
                Assert(hooks.PlayCheckpointStar().Played, "rescue_checkpoint_star_hook_plays");
                audio.Stop();
                Assert(hooks.PlayMissionComplete().Played, "rescue_mission_complete_hook_plays");
                audio.Stop();
                Assert(hooks.PlayGardenUnlock().Played, "rescue_garden_unlock_hook_plays");
                audio.Stop();

                var noReward = hooks.PlayRewardFeedback(0, false, false);
                Assert(!noReward.Played && noReward.Reason == "no_reward_transition", "rescue_reward_bridge_silent_without_transition");
                var checkpointReward = hooks.PlayRewardFeedback(1, false, false);
                Assert(checkpointReward.Played && checkpointReward.DurationMs > 790, "rescue_reward_bridge_maps_checkpoint_star");
                audio.Stop();
                var finalReward = hooks.PlayRewardFeedback(0, true, false);
                Assert(finalReward.Played && finalReward.DurationMs > 790, "rescue_reward_bridge_maps_plain_mission_complete");
                audio.Stop();
                var gardenReward = hooks.PlayRewardFeedback(1, true, true);
                Assert(gardenReward.Played && gardenReward.DurationMs > 390 && gardenReward.DurationMs < 410,
                    "rescue_reward_bridge_terminal_garden_cue_wins_over_checkpoint");
                audio.Stop();

                var musicDisabled = hooks.TryStartMissionMusic();
                Assert(!musicDisabled.Played && musicDisabled.Reason == "audio_channel_disabled", "rescue_music_respects_opt_in");
                audio.MusicEnabled = true;
                Assert(hooks.TryStartMissionMusic().Played, "rescue_music_hook_plays_when_enabled");
                audio.Stop();

                var unknown = hooks.Play((RescueGameAudioCue)999);
                Assert(!unknown.Played && unknown.Reason == "rescue_audio_cue_unknown", "rescue_unknown_cue_safe_fallback");
            }

            var missingBackend = new FakeBackend();
            using (var audio = new AudioCoordinator(missingBackend, 128 * 1024))
            {
                var hooks = new RescueGameAudioHooks(audio, Path.Combine(root, "missing-cues"));
                var missing = hooks.PlayCheckpointStar();
                Assert(!missing.Played && missing.Reason == "audio_file_missing", "rescue_missing_audio_asset_no_crash");
            }
        }

        private static void WritePcmWave(string path, int sampleRate, short channels, short bits, int durationMs)
        {
            var bytesPerSample = bits / 8;
            var sampleCount = sampleRate * durationMs / 1000;
            var dataBytes = sampleCount * channels * bytesPerSample;
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
                w.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write(channels);
                w.Write(sampleRate);
                var byteRate = sampleRate * channels * bytesPerSample;
                w.Write(byteRate);
                w.Write((short)(channels * bytesPerSample));
                w.Write(bits);
                w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                w.Write(dataBytes);
                for (var i = 0; i < dataBytes; i++) w.Write((byte)0);
            }
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }

        private sealed class FakeBackend : IAudioBackend
        {
            public bool Available = true;
            public bool ThrowOnPlay;
            public int PlayCount;
            public bool IsAvailable { get { return Available; } }
            public void Play(byte[] wavBytes, bool loop)
            {
                if (ThrowOnPlay) throw new InvalidOperationException("fake backend failure");
                PlayCount++;
            }
            public void Stop() { }
            public void Dispose() { }
        }
    }
}
