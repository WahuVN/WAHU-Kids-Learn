using System;
using System.IO;
using WAHU.Audio;
using WAHU.Motion;

namespace WAHU.TypingGameFeelRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        private static void Main()
        {
            TestFeelPlanner();
            TestAssetFallback();
            TestAudioFeedback();
            Console.WriteLine("TYPING_GAME_FEEL_RUNTIME_SMOKE_PASS assertions=" + _assertions);
        }

        private static void TestFeelPlanner()
        {
            var planner = new TypingGameFeelPlanner();
            var correct = planner.Plan(E("TYPING_CHAR_CORRECT"), MotionMode.Normal, PerformanceProfile.NORMAL);
            Assert(correct.Cues.Count == 2, "correct_has_glow_and_spark");
            Assert(correct.Cues[0].Effect == TypingGameFeelEffect.ChargeGlow, "correct_glow_first");
            Assert(correct.Cues[1].ParticleCount <= TypingGameFeelPlanner.MaxParticlesNormal, "particle_count_bounded");

            var wrong = planner.Plan(E("TYPING_CHAR_WRONG"), MotionMode.Normal, PerformanceProfile.NORMAL);
            Assert(wrong.Cues.Count == 1 && wrong.Cues[0].Effect == TypingGameFeelEffect.GentleShake, "wrong_is_gentle_feedback");
            Assert(wrong.Cues[0].Intensity < 0.25, "wrong_not_punitive");

            var beam = planner.Plan(E("TYPING_WORD_COMPLETED"), MotionMode.Normal, PerformanceProfile.NORMAL);
            Assert(beam.Cues.Count == 1 && beam.Cues[0].Effect == TypingGameFeelEffect.BeamBurst, "word_completion_beam");

            var boss = planner.Plan(E("BOSS_DEFEATED"), MotionMode.Reduced, PerformanceProfile.LOW);
            Assert(boss.Cues[0].ParticleCount <= TypingGameFeelPlanner.MaxParticlesLow, "low_profile_particle_cap");
            Assert(boss.Cues[0].DurationMs < 420, "low_profile_shorter_effect");

            var minimal = planner.Plan(E("LEVEL_COMPLETED"), MotionMode.Minimal, PerformanceProfile.NORMAL);
            Assert(minimal.Cues[0].StaticFallback && minimal.Cues[0].ParticleCount == 0 && minimal.Cues[0].DurationMs == 0, "minimal_motion_static_fallback");

            Assert(planner.Plan(E("GAME_PAUSED"), MotionMode.Normal, PerformanceProfile.NORMAL).IsEmpty, "unknown_or_nonfeedback_event_is_safe_noop");
            Assert(planner.Plan(null, MotionMode.Normal, PerformanceProfile.NORMAL).IsEmpty, "null_event_safe_noop");
        }

        private static void TestAssetFallback()
        {
            var root = Path.Combine(Path.GetTempPath(), "wahu-gamefeel-assets-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var fallback = Path.Combine(root, "fallback.dat");
                File.WriteAllText(fallback, "ok");
                var registry = new GameFeelAssetRegistry();
                registry.Register("beam", Path.Combine(root, "missing.dat"), "spark");
                registry.Register("spark", fallback);
                var resolved = registry.Resolve("beam");
                Assert(resolved.Found && resolved.UsedFallback && resolved.ResolvedKey == "spark", "missing_vfx_uses_registered_fallback");

                registry.Register("cycle-a", Path.Combine(root, "a.dat"), "cycle-b");
                registry.Register("cycle-b", Path.Combine(root, "b.dat"), "cycle-a");
                Assert(!registry.Resolve("cycle-a").Found && registry.Resolve("cycle-a").Reason == "asset_fallback_cycle", "asset_fallback_cycle_safe");
                Assert(!registry.Resolve("unknown").Found, "unknown_asset_safe");
            }
            finally { Directory.Delete(root, true); }
        }

        private static void TestAudioFeedback()
        {
            var root = Path.Combine(Path.GetTempPath(), "wahu-gamefeel-audio-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                WritePcmWave(Path.Combine(root, "typing_char_charge.wav"), 120);
                WritePcmWave(Path.Combine(root, "typing_wrong_soft.wav"), 120);
                WritePcmWave(Path.Combine(root, "typing_word_beam.wav"), 120);
                var backend = new FakeVolumeBackend();
                using (var audio = new AudioCoordinator(backend, 128 * 1024))
                {
                    var feedback = new TypingGameAudioFeedback(audio, root);
                    var volumeEvent = 0;
                    feedback.SfxVolumeChanged += _ => volumeEvent++;
                    feedback.SfxVolume = 0.5;
                    Assert(volumeEvent == 1, "sfx_volume_hook_fires");

                    var correct = feedback.HandleEvent("TYPING_CHAR_CORRECT", 1000);
                    Assert(correct.Played, "correct_sfx_plays");
                    Assert(backend.LastVolume > 0.23 && backend.LastVolume < 0.25, "semantic_and_master_volume_applied");
                    audio.Stop();
                    var throttled = feedback.HandleEvent("TYPING_CHAR_CORRECT", 1010);
                    Assert(!throttled.Played && throttled.Reason == "typing_audio_throttled", "rapid_char_sfx_throttled");

                    audio.Stop();
                    var missingRescueFallsBack = feedback.HandleEvent("TARGET_RESCUED", 1500);
                    Assert(missingRescueFallsBack.Played, "missing_rescue_asset_falls_back_to_beam");

                    audio.Stop();
                    var ignored = feedback.HandleEvent("GAME_PAUSED", 2000);
                    Assert(!ignored.Played && ignored.Reason == "typing_audio_event_ignored", "nonfeedback_event_silent");
                }
            }
            finally { Directory.Delete(root, true); }
        }

        private static TypingGameFeedbackEvent E(string type)
        {
            return new TypingGameFeedbackEvent { Type = type, TargetId = "mock-target", Timestamp = 1 };
        }

        private static void WritePcmWave(string path, int durationMs)
        {
            const int sampleRate = 8000;
            const short channels = 1;
            const short bits = 16;
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
                w.Write(16); w.Write((short)1); w.Write(channels); w.Write(sampleRate);
                w.Write(sampleRate * channels * bytesPerSample); w.Write((short)(channels * bytesPerSample)); w.Write(bits);
                w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' }); w.Write(dataBytes);
                for (var i = 0; i < dataBytes; i++) w.Write((byte)0);
            }
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }

        private sealed class FakeVolumeBackend : IAudioBackend, IAudioVolumeBackend
        {
            public double LastVolume = -1;
            public bool IsAvailable { get { return true; } }
            public void SetVolume(double volume) { LastVolume = volume; }
            public void Play(byte[] wavBytes, bool loop) { }
            public void Stop() { }
            public void Dispose() { }
        }
    }
}
