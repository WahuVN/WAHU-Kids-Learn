using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace WAHU.Content
{
    public sealed class MathVerifiedTemplateDescriptor
    {
        public string Id { get; set; }
        public string SkillId { get; set; }
        public string Status { get; set; }
    }

    public sealed class MathVerifiedTemplateSource
    {
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer
        {
            MaxJsonLength = 4 * 1024 * 1024,
            RecursionLimit = 64
        };

        public IList<MathVerifiedTemplateDescriptor> Load(string templateFilePath)
        {
            if (string.IsNullOrWhiteSpace(templateFilePath)) throw new ArgumentException("templateFilePath");
            if (!File.Exists(templateFilePath)) throw new FileNotFoundException("Không tìm thấy Math VERIFIED template source.", templateFilePath);

            Dictionary<string, object> root;
            try { root = _json.DeserializeObject(File.ReadAllText(templateFilePath)) as Dictionary<string, object>; }
            catch (Exception ex) { throw new InvalidDataException("Math VERIFIED template JSON invalid.", ex); }
            if (root == null) throw new InvalidDataException("Math VERIFIED template root must be object.");

            if (ReadInt(root, "schema_version") != 1) throw new InvalidDataException("Unsupported math template schema_version.");
            if (!string.Equals(ReadString(root, "subject"), "math", StringComparison.Ordinal))
                throw new InvalidDataException("Math template source subject mismatch.");

            object rawTemplates;
            if (!root.TryGetValue("templates", out rawTemplates)) throw new InvalidDataException("Math template source missing templates.");
            var templates = rawTemplates as object[];
            if (templates == null) throw new InvalidDataException("Math template source templates must be array.");

            var result = new List<MathVerifiedTemplateDescriptor>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raw in templates)
            {
                var item = raw as Dictionary<string, object>;
                if (item == null) continue;
                var id = OptionalString(item, "id");
                var skill = OptionalString(item, "skill");
                var status = OptionalString(item, "status");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(skill)) continue;
                if (!string.Equals(status, "VERIFIED_A_TEMPLATE", StringComparison.Ordinal)) continue;
                if (!ids.Add(id)) throw new InvalidDataException("Duplicate VERIFIED math template id: " + id);
                result.Add(new MathVerifiedTemplateDescriptor { Id = id, SkillId = skill, Status = status });
            }
            if (result.Count == 0) throw new InvalidDataException("Math template source contains no child-ready VERIFIED_A_TEMPLATE items.");
            return result;
        }

        private static string ReadString(Dictionary<string, object> root, string key)
        {
            var value = OptionalString(root, key);
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("Math template source missing " + key);
            return value;
        }

        private static string OptionalString(Dictionary<string, object> root, string key)
        {
            object value;
            if (!root.TryGetValue(key, out value) || value == null) return null;
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static int ReadInt(Dictionary<string, object> root, string key)
        {
            object value;
            if (!root.TryGetValue(key, out value) || value == null) throw new InvalidDataException("Math template source missing " + key);
            try { return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture); }
            catch (Exception ex) { throw new InvalidDataException("Math template source invalid integer " + key, ex); }
        }
    }
}
