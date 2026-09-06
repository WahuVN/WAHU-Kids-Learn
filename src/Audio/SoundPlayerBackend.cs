using System;
using System.IO;
using System.Media;

namespace WAHU.Audio
{
    public sealed class SoundPlayerBackend : IAudioBackend
    {
        private SoundPlayer _player;
        private MemoryStream _stream;
        private bool _disposed;

        public bool IsAvailable { get { return !_disposed; } }

        public void Play(byte[] wavBytes, bool loop)
        {
            if (_disposed) throw new ObjectDisposedException("SoundPlayerBackend");
            if (wavBytes == null || wavBytes.Length == 0) throw new ArgumentException("wavBytes");
            Stop();
            _stream = new MemoryStream(wavBytes, false);
            _player = new SoundPlayer(_stream);
            _player.Load();
            if (loop) _player.PlayLooping(); else _player.Play();
        }

        public void Stop()
        {
            if (_player != null)
            {
                try { _player.Stop(); } catch { }
                _player.Dispose();
                _player = null;
            }
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _disposed = true;
        }
    }
}
