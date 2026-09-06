using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace WAHU.Platform
{
    public static class JsonReportWriter
    {
        public static void Write(PreflightReport report, string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var serializer = new DataContractJsonSerializer(typeof(PreflightReport));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, report);
                var json = Encoding.UTF8.GetString(ms.ToArray());
                var temp = path + ".tmp";
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
        }
    }
}
