using System;
using System.IO;
using System.Text;

namespace WAHU.Audio
{
    public sealed class WavePcmInspector
    {
        public const long DefaultMaxClipBytes = 4L * 1024 * 1024;
        public const double DefaultMaxClipDurationMs = 30000.0;

        public WaveClipInfo Inspect(string path, long maxClipBytes = DefaultMaxClipBytes, double maxDurationMs = DefaultMaxClipDurationMs)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("WAV clip not found.", path);
            var info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > maxClipBytes) throw new InvalidDataException("WAV file size outside allowed limit.");

            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream, Encoding.ASCII))
            {
                if (ReadFourCc(reader) != "RIFF") throw new InvalidDataException("Missing RIFF header.");
                reader.ReadUInt32();
                if (ReadFourCc(reader) != "WAVE") throw new InvalidDataException("Missing WAVE signature.");

                ushort formatTag = 0, channels = 0, bits = 0;
                uint sampleRate = 0, byteRate = 0;
                long dataBytes = -1;
                var fmtFound = false;
                var dataFound = false;

                while (stream.Position + 8 <= stream.Length)
                {
                    var chunkId = ReadFourCc(reader);
                    var chunkSize = reader.ReadUInt32();
                    var chunkStart = stream.Position;
                    if (chunkStart + chunkSize > stream.Length) throw new InvalidDataException("WAV chunk exceeds file boundary.");

                    if (chunkId == "fmt ")
                    {
                        if (chunkSize < 16) throw new InvalidDataException("WAV fmt chunk too short.");
                        formatTag = reader.ReadUInt16();
                        channels = reader.ReadUInt16();
                        sampleRate = reader.ReadUInt32();
                        byteRate = reader.ReadUInt32();
                        reader.ReadUInt16();
                        bits = reader.ReadUInt16();
                        fmtFound = true;
                    }
                    else if (chunkId == "data")
                    {
                        dataBytes = chunkSize;
                        dataFound = true;
                    }

                    stream.Position = chunkStart + chunkSize + (chunkSize % 2);
                }

                if (!fmtFound || !dataFound) throw new InvalidDataException("WAV requires fmt and data chunks.");
                if (formatTag != 1) throw new InvalidDataException("Only PCM WAV is allowed in core runtime.");
                if (channels < 1 || channels > 2) throw new InvalidDataException("Only mono/stereo WAV is allowed.");
                if (sampleRate < 8000 || sampleRate > 48000) throw new InvalidDataException("WAV sample rate outside allowed range.");
                if (bits != 8 && bits != 16) throw new InvalidDataException("Only 8/16-bit PCM WAV is allowed.");
                if (byteRate == 0) throw new InvalidDataException("WAV byte rate invalid.");
                var duration = dataBytes * 1000.0 / byteRate;
                if (duration <= 0 || duration > maxDurationMs) throw new InvalidDataException("WAV duration outside allowed range.");

                return new WaveClipInfo
                {
                    Path = Path.GetFullPath(path),
                    Channels = channels,
                    SampleRate = (int)sampleRate,
                    BitsPerSample = bits,
                    DataBytes = dataBytes,
                    DurationMs = duration
                };
            }
        }

        private static string ReadFourCc(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(4);
            if (bytes.Length != 4) throw new EndOfStreamException();
            return Encoding.ASCII.GetString(bytes);
        }
    }
}
