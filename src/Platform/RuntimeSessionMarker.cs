using System;
using System.IO;
using System.Text;

namespace WAHU.Platform
{
    public sealed class RuntimeSessionMarker : IDisposable
    {
        private readonly string _path;
        private bool _cleaned;

        private RuntimeSessionMarker(string path, bool previousRunUnclean)
        {
            _path = path;
            PreviousRunUnclean = previousRunUnclean;
        }

        public bool PreviousRunUnclean { get; private set; }

        public static RuntimeSessionMarker Begin(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path");
            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
            var existed = File.Exists(path);
            var temp = path + ".tmp." + Guid.NewGuid().ToString("N");
            File.WriteAllText(temp,
                "started_at_utc=" + DateTime.UtcNow.ToString("o") + Environment.NewLine +
                "pid=" + System.Diagnostics.Process.GetCurrentProcess().Id + Environment.NewLine,
                new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null, true);
            else File.Move(temp, path);
            return new RuntimeSessionMarker(path, existed);
        }

        public void MarkCleanAndDispose()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_cleaned) return;
            _cleaned = true;
            try { if (File.Exists(_path)) File.Delete(_path); } catch { }
        }
    }
}
