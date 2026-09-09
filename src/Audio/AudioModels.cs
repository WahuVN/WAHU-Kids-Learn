using System;

namespace WAHU.Audio
{
    public enum AudioPriority
    {
        Music = 1,
        Sfx = 2,
        ImportantSignal = 3,
        InstructionalVoice = 4
    }

    public sealed class WaveClipInfo
    {
        public string Path { get; set; }
        public int Channels { get; set; }
        public int SampleRate { get; set; }
        public int BitsPerSample { get; set; }
        public long DataBytes { get; set; }
        public double DurationMs { get; set; }
    }

    public sealed class AudioPlaybackRequest
    {
        public string Path { get; set; }
        public AudioPriority Priority { get; set; }
        public bool Loop { get; set; }
        public double Volume { get; set; } = 1.0;
    }

    /// <summary>Optional capability; legacy backends may ignore per-request volume safely.</summary>
    public interface IAudioVolumeBackend
    {
        void SetVolume(double volume);
    }

    public sealed class AudioPlaybackResult
    {
        public bool Played { get; set; }
        public string Reason { get; set; }
        public double DurationMs { get; set; }
    }

    public interface IAudioBackend : IDisposable
    {
        bool IsAvailable { get; }
        void Play(byte[] wavBytes, bool loop);
        void Stop();
    }
}
