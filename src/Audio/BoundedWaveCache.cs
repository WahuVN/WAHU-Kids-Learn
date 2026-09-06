using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WAHU.Audio
{
    public sealed class BoundedWaveCache
    {
        private sealed class Entry
        {
            public string Path;
            public byte[] Data;
            public long LastUse;
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private readonly long _maxBytes;
        private long _clock;
        private long _currentBytes;

        public BoundedWaveCache(long maxBytes)
        {
            if (maxBytes < 64 * 1024) throw new ArgumentOutOfRangeException("maxBytes");
            _maxBytes = maxBytes;
        }

        public long CurrentBytes { get { return _currentBytes; } }
        public int Count { get { return _entries.Count; } }

        public byte[] GetOrLoad(string path)
        {
            var full = Path.GetFullPath(path);
            Entry entry;
            if (_entries.TryGetValue(full, out entry))
            {
                entry.LastUse = ++_clock;
                return entry.Data;
            }

            var data = File.ReadAllBytes(full);
            if (data.LongLength > _maxBytes) throw new InvalidDataException("Single WAV clip exceeds audio cache capacity.");
            while (_currentBytes + data.LongLength > _maxBytes && _entries.Count > 0)
            {
                var victim = _entries.Values.OrderBy(x => x.LastUse).First();
                _entries.Remove(victim.Path);
                _currentBytes -= victim.Data.LongLength;
            }
            entry = new Entry { Path = full, Data = data, LastUse = ++_clock };
            _entries.Add(full, entry);
            _currentBytes += data.LongLength;
            return data;
        }

        public void Clear()
        {
            _entries.Clear();
            _currentBytes = 0;
        }
    }
}
