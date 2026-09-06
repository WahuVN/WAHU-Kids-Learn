using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace WAHU.Content
{
    public sealed class MathLessonCatalogSnapshot
    {
        public string CatalogId { get; set; }
        public int Grade { get; set; }
        public string CurriculumId { get; set; }
        public IList<MathChapterDescriptor> Chapters { get; set; }
        public IList<MathTopicDescriptor> Topics { get; set; }
        public IList<MathLessonDescriptor> Lessons { get; set; }

        public MathChapterDescriptor FindChapter(string id)
        {
            return (Chapters ?? new List<MathChapterDescriptor>())
                .FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal));
        }

        public MathTopicDescriptor FindTopic(string id)
        {
            return (Topics ?? new List<MathTopicDescriptor>())
                .FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal));
        }

        public MathLessonDescriptor FindLesson(string id)
        {
            return (Lessons ?? new List<MathLessonDescriptor>())
                .FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal));
        }

        public MathLessonDescriptor FindLessonBySkill(string skillId)
        {
            return (Lessons ?? new List<MathLessonDescriptor>())
                .FirstOrDefault(x => string.Equals(x.SkillId, skillId, StringComparison.Ordinal));
        }

        public IList<MathTopicDescriptor> TopicsForChapter(string chapterId)
        {
            return (Topics ?? new List<MathTopicDescriptor>())
                .Where(x => string.Equals(x.ChapterId, chapterId, StringComparison.Ordinal))
                .ToList();
        }

        public IList<MathLessonDescriptor> LessonsForTopic(string topicId)
        {
            return (Lessons ?? new List<MathLessonDescriptor>())
                .Where(x => string.Equals(x.TopicId, topicId, StringComparison.Ordinal))
                .OrderBy(x => x.OrderInDomain)
                .ToList();
        }
    }

    public sealed class MathChapterDescriptor
    {
        public string Id { get; set; }
        public string TitleVi { get; set; }
        public string DomainKey { get; set; }
    }

    public sealed class MathTopicDescriptor
    {
        public string Id { get; set; }
        public string ChapterId { get; set; }
        public string TitleVi { get; set; }
    }

    public sealed class MathConceptDescriptor
    {
        public string Id { get; set; }
        public string NameVi { get; set; }
        public string DefinitionVi { get; set; }
    }

    public sealed class MathWorkedExampleDescriptor
    {
        public string Id { get; set; }
        public string PromptVi { get; set; }
        public IList<string> SolutionStepsVi { get; set; }
        public string Answer { get; set; }
    }

    public sealed class MathPracticeSetDescriptor
    {
        public IList<string> Basic { get; set; }
        public IList<string> Medium { get; set; }
        public IList<string> Application { get; set; }

        public int TotalCount
        {
            get { return Count(Basic) + Count(Medium) + Count(Application); }
        }

        private static int Count(IList<string> values) { return values == null ? 0 : values.Count; }
    }

    public sealed class MathLessonDescriptor
    {
        public string Id { get; set; }
        public string ChapterId { get; set; }
        public string TopicId { get; set; }
        public string SkillId { get; set; }
        public int OrderInDomain { get; set; }
        public string TitleVi { get; set; }
        public IList<string> ObjectivesVi { get; set; }
        public string ExplanationVi { get; set; }
        public IList<MathConceptDescriptor> Concepts { get; set; }
        public IList<MathWorkedExampleDescriptor> WorkedExamples { get; set; }
        public MathPracticeSetDescriptor PracticeSets { get; set; }
        public IList<string> PrerequisiteSkills { get; set; }
        public IList<string> DifficultySpan { get; set; }
        public string Status { get; set; }
    }

    public sealed class MathLessonCatalogSource
    {
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer
        {
            MaxJsonLength = 4 * 1024 * 1024,
            RecursionLimit = 96
        };

        public MathLessonCatalogSnapshot Load(string catalogFilePath)
        {
            if (string.IsNullOrWhiteSpace(catalogFilePath)) throw new ArgumentException("catalogFilePath");
            if (!File.Exists(catalogFilePath))
                throw new FileNotFoundException("Không tìm thấy Math lesson catalog.", catalogFilePath);

            Dictionary<string, object> root;
            try { root = _json.DeserializeObject(File.ReadAllText(catalogFilePath)) as Dictionary<string, object>; }
            catch (Exception ex) { throw new InvalidDataException("Math lesson catalog JSON invalid.", ex); }
            if (root == null) throw new InvalidDataException("Math lesson catalog root must be object.");
            if (ReadInt(root, "schema_version") != 1)
                throw new InvalidDataException("Unsupported math lesson catalog schema_version.");
            if (!string.Equals(ReadString(root, "subject"), "math", StringComparison.Ordinal))
                throw new InvalidDataException("Math lesson catalog subject mismatch.");
            if (!string.Equals(ReadString(root, "language"), "vi", StringComparison.Ordinal))
                throw new InvalidDataException("Math lesson catalog language mismatch.");

            var snapshot = new MathLessonCatalogSnapshot
            {
                CatalogId = ReadString(root, "catalog_id"),
                Grade = ReadInt(root, "grade"),
                CurriculumId = ReadString(root, "curriculum_id"),
                Chapters = ReadChapters(root),
                Topics = ReadTopics(root),
                Lessons = ReadLessons(root)
            };
            ValidateReferences(snapshot);
            return snapshot;
        }

        private static IList<MathChapterDescriptor> ReadChapters(Dictionary<string, object> root)
        {
            var result = new List<MathChapterDescriptor>();
            foreach (var item in ReadObjectArray(root, "chapters"))
            {
                result.Add(new MathChapterDescriptor
                {
                    Id = ReadString(item, "id"),
                    TitleVi = ReadString(item, "title_vi"),
                    DomainKey = ReadString(item, "domain_key")
                });
            }
            if (result.Count == 0) throw new InvalidDataException("Math lesson catalog contains no chapters.");
            EnsureUnique(result.Select(x => x.Id), "chapter");
            return result;
        }

        private static IList<MathTopicDescriptor> ReadTopics(Dictionary<string, object> root)
        {
            var result = new List<MathTopicDescriptor>();
            foreach (var item in ReadObjectArray(root, "topics"))
            {
                result.Add(new MathTopicDescriptor
                {
                    Id = ReadString(item, "id"),
                    ChapterId = ReadString(item, "chapter_id"),
                    TitleVi = ReadString(item, "title_vi")
                });
            }
            if (result.Count == 0) throw new InvalidDataException("Math lesson catalog contains no topics.");
            EnsureUnique(result.Select(x => x.Id), "topic");
            return result;
        }

        private static IList<MathLessonDescriptor> ReadLessons(Dictionary<string, object> root)
        {
            var result = new List<MathLessonDescriptor>();
            foreach (var item in ReadObjectArray(root, "lessons"))
            {
                result.Add(new MathLessonDescriptor
                {
                    Id = ReadString(item, "id"),
                    ChapterId = ReadString(item, "chapter_id"),
                    TopicId = ReadString(item, "topic_id"),
                    SkillId = ReadString(item, "skill_id"),
                    OrderInDomain = ReadInt(item, "order_in_domain"),
                    TitleVi = ReadString(item, "title_vi"),
                    ObjectivesVi = ReadStringArray(item, "objectives_vi", true),
                    ExplanationVi = ReadString(item, "explanation_vi"),
                    Concepts = ReadConcepts(item),
                    WorkedExamples = ReadWorkedExamples(item),
                    PracticeSets = ReadPracticeSets(item),
                    PrerequisiteSkills = ReadStringArray(item, "prerequisite_skills", false),
                    DifficultySpan = ReadStringArray(item, "difficulty_span", true),
                    Status = ReadString(item, "status")
                });
            }
            if (result.Count == 0) throw new InvalidDataException("Math lesson catalog contains no lessons.");
            EnsureUnique(result.Select(x => x.Id), "lesson");
            EnsureUnique(result.Select(x => x.SkillId), "lesson skill");
            return result;
        }

        private static IList<MathConceptDescriptor> ReadConcepts(Dictionary<string, object> lesson)
        {
            var result = new List<MathConceptDescriptor>();
            foreach (var item in ReadObjectArray(lesson, "concepts"))
            {
                result.Add(new MathConceptDescriptor
                {
                    Id = ReadString(item, "id"),
                    NameVi = ReadString(item, "name_vi"),
                    DefinitionVi = ReadString(item, "definition_vi")
                });
            }
            if (result.Count == 0) throw new InvalidDataException("Math lesson missing concepts.");
            return result;
        }

        private static IList<MathWorkedExampleDescriptor> ReadWorkedExamples(Dictionary<string, object> lesson)
        {
            var result = new List<MathWorkedExampleDescriptor>();
            foreach (var item in ReadObjectArray(lesson, "worked_examples"))
            {
                result.Add(new MathWorkedExampleDescriptor
                {
                    Id = ReadString(item, "id"),
                    PromptVi = ReadString(item, "prompt_vi"),
                    SolutionStepsVi = ReadStringArray(item, "solution_steps_vi", true),
                    Answer = ReadString(item, "answer")
                });
            }
            if (result.Count == 0) throw new InvalidDataException("Math lesson missing worked_examples.");
            return result;
        }

        private static MathPracticeSetDescriptor ReadPracticeSets(Dictionary<string, object> lesson)
        {
            object raw;
            if (!lesson.TryGetValue("practice_sets", out raw))
                throw new InvalidDataException("Math lesson missing practice_sets.");
            var item = raw as Dictionary<string, object>;
            if (item == null) throw new InvalidDataException("Math lesson practice_sets must be object.");
            var value = new MathPracticeSetDescriptor
            {
                Basic = ReadStringArray(item, "basic", true),
                Medium = ReadStringArray(item, "medium", true),
                Application = ReadStringArray(item, "application", true)
            };
            if (value.TotalCount <= 0) throw new InvalidDataException("Math lesson has no practice question ids.");
            return value;
        }

        private static void ValidateReferences(MathLessonCatalogSnapshot snapshot)
        {
            var chapterIds = new HashSet<string>(snapshot.Chapters.Select(x => x.Id), StringComparer.Ordinal);
            var topicIds = new HashSet<string>(snapshot.Topics.Select(x => x.Id), StringComparer.Ordinal);
            var skillIds = new HashSet<string>(snapshot.Lessons.Select(x => x.SkillId), StringComparer.Ordinal);
            foreach (var topic in snapshot.Topics)
                if (!chapterIds.Contains(topic.ChapterId))
                    throw new InvalidDataException("Math topic references unknown chapter: " + topic.Id);
            foreach (var lesson in snapshot.Lessons)
            {
                if (!chapterIds.Contains(lesson.ChapterId))
                    throw new InvalidDataException("Math lesson references unknown chapter: " + lesson.Id);
                if (!topicIds.Contains(lesson.TopicId))
                    throw new InvalidDataException("Math lesson references unknown topic: " + lesson.Id);
                var topic = snapshot.FindTopic(lesson.TopicId);
                if (topic == null || !string.Equals(topic.ChapterId, lesson.ChapterId, StringComparison.Ordinal))
                    throw new InvalidDataException("Math lesson topic/chapter mismatch: " + lesson.Id);
                foreach (var prerequisite in lesson.PrerequisiteSkills ?? new List<string>())
                    if (!skillIds.Contains(prerequisite))
                        throw new InvalidDataException("Math lesson prerequisite references unknown skill: " + lesson.Id);
                if (!string.Equals(lesson.Status, "CHILD_READY", StringComparison.Ordinal))
                    throw new InvalidDataException("Math lesson is not CHILD_READY: " + lesson.Id);
            }
        }

        private static IList<Dictionary<string, object>> ReadObjectArray(Dictionary<string, object> root, string key)
        {
            object raw;
            if (!root.TryGetValue(key, out raw) || raw == null)
                throw new InvalidDataException("Math lesson catalog missing " + key + ".");
            var array = raw as object[];
            if (array == null) throw new InvalidDataException("Math lesson catalog " + key + " must be array.");
            var result = new List<Dictionary<string, object>>();
            foreach (var value in array)
            {
                var item = value as Dictionary<string, object>;
                if (item == null) throw new InvalidDataException("Math lesson catalog " + key + " item must be object.");
                result.Add(item);
            }
            return result;
        }

        private static IList<string> ReadStringArray(Dictionary<string, object> root, string key, bool requireNonEmpty)
        {
            object raw;
            if (!root.TryGetValue(key, out raw) || raw == null)
            {
                if (requireNonEmpty) throw new InvalidDataException("Math lesson catalog missing " + key + ".");
                return new List<string>();
            }
            var array = raw as object[];
            if (array == null) throw new InvalidDataException("Math lesson catalog " + key + " must be array.");
            var result = new List<string>();
            foreach (var value in array)
            {
                var text = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Math lesson catalog contains blank " + key + ".");
                result.Add(text.Trim());
            }
            if (requireNonEmpty && result.Count == 0)
                throw new InvalidDataException("Math lesson catalog " + key + " must be non-empty.");
            return result;
        }

        private static string ReadString(Dictionary<string, object> root, string key)
        {
            object raw;
            if (!root.TryGetValue(key, out raw) || raw == null)
                throw new InvalidDataException("Math lesson catalog missing " + key + ".");
            var value = Convert.ToString(raw, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException("Math lesson catalog has blank " + key + ".");
            return value.Trim();
        }

        private static int ReadInt(Dictionary<string, object> root, string key)
        {
            object raw;
            if (!root.TryGetValue(key, out raw) || raw == null)
                throw new InvalidDataException("Math lesson catalog missing " + key + ".");
            try { return Convert.ToInt32(raw, CultureInfo.InvariantCulture); }
            catch (Exception ex) { throw new InvalidDataException("Math lesson catalog invalid integer " + key + ".", ex); }
        }

        private static void EnsureUnique(IEnumerable<string> values, string kind)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
                if (!ids.Add(value)) throw new InvalidDataException("Duplicate math " + kind + " id: " + value);
        }
    }
}
