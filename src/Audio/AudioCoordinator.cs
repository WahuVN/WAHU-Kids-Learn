using System;
using System.IO;

namespace WAHU.Audio
{
    public sealed class AudioCoordinator : IDisposable
    {
        private readonly IAudioBackend _backend;
        private readonly WavePcmInspector _inspector;
        private readonly BoundedWaveCache _cache;
        private DateTime _busyUntilUtc;
        private AudioPriority? _activePriority;
        private string _lastVoicePath;
        private bool _disposed;

        public AudioCoordinator(IAudioBackend backend, long cacheBytes)
        {
            _backend = backend ?? throw new ArgumentNullException("backend");
            _inspector = new WavePcmInspector();
            _cache = new BoundedWaveCache(cacheBytes);
            VoiceEnabled = true;
            SfxEnabled = true;
            MusicEnabled = false;
        }

        public bool VoiceEnabled { get; set; }
        public bool SfxEnabled { get; set; }
        public bool MusicEnabled { get; set; }
        public bool IsAvailable { get { return !_disposed && _backend.IsAvailable; } }
        public long CachedBytes { get { return _cache.CurrentBytes; } }

        public AudioPlaybackResult PlayVoice(string path)
        {
            var result = TryPlay(new AudioPlaybackRequest { Path = path, Priority = AudioPriority.InstructionalVoice });
            if (result.Played) _lastVoicePath = Path.GetFullPath(path);
            return result;
        }

        public AudioPlaybackResult ReplayLastVoice()
        {
            if (string.IsNullOrWhiteSpace(_lastVoicePath)) return Denied("no_voice_to_replay");
            return PlayVoice(_lastVoicePath);
        }

        public AudioPlaybackResult TryPlay(AudioPlaybackRequest request)
        {
            if (_disposed) return Denied("audio_runtime_disposed");
            if (request == null || string.IsNullOrWhiteSpace(request.Path)) return Denied("audio_request_invalid");
            if (!Enabled(request.Priority)) return Denied("audio_channel_disabled");
            if (!_backend.IsAvailable) return Denied("audio_device_unavailable");
            if (!File.Exists(request.Path)) return Denied("audio_file_missing");

            WaveClipInfo clip;
            byte[] data;
            try
            {
                clip = _inspector.Inspect(request.Path);
                data = _cache.GetOrLoad(request.Path);
            }
            catch (Exception ex)
            {
                return Denied("audio_clip_invalid:" + ex.GetType().Name);
            }

            var now = DateTime.UtcNow;
            var active = _activePriority.HasValue && now < _busyUntilUtc;
            if (active && request.Priority < _activePriority.Value)
                return Denied("higher_priority_audio_active");

            try
            {
                _backend.Stop();
                var volumeBackend = _backend as IAudioVolumeBackend;
                if (volumeBackend != null)
                {
                    var volume = request.Volume;
                    if (double.IsNaN(volume) || double.IsInfinity(volume)) volume = 1.0;
                    volumeBackend.SetVolume(Math.Max(0.0, Math.Min(1.0, volume)));
                }
                _backend.Play(data, request.Loop);
                _activePriority = request.Priority;
                _busyUntilUtc = request.Loop ? DateTime.MaxValue : now.AddMilliseconds(clip.DurationMs);
                return new AudioPlaybackResult { Played = true, Reason = "played", DurationMs = clip.DurationMs };
            }
            catch
            {
                _activePriority = null;
                _busyUntilUtc = DateTime.MinValue;
                return Denied("audio_backend_failure");
            }
        }

        public void Stop()
        {
            if (_disposed) return;
            try { _backend.Stop(); } catch { }
            _activePriority = null;
            _busyUntilUtc = DateTime.MinValue;
        }

        private bool Enabled(AudioPriority priority)
        {
            if (priority == AudioPriority.InstructionalVoice) return VoiceEnabled;
            if (priority == AudioPriority.Music) return MusicEnabled;
            return SfxEnabled;
        }

        private static AudioPlaybackResult Denied(string reason)
        {
            return new AudioPlaybackResult { Played = false, Reason = reason, DurationMs = 0 };
        }

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _cache.Clear();
            _backend.Dispose();
            _disposed = true;
        }
    }
}
