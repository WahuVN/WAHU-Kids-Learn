using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using WAHU.Learning;

namespace WAHU.Session
{
    public sealed class MathAuthoredQuestionBank
    {
        public string BankId { get; set; }
        public int Grade { get; set; }
        public string CurriculumId { get; set; }
        public IList<MathQuestion> Questions { get; set; }

        public IList<MathQuestion> ForLesson(string lessonId)
        {
            if (string.IsNullOrWhiteSpace(lessonId)) return new List<MathQuestion>();
            return (Questions ?? new List<MathQuestion>())
                .Where(x => string.Equals(x.LessonId, lessonId, StringComparison.Ordinal))
                .ToList();
        }

        public MathQuestion FindContentQuestion(string contentQuestionId)
        {
            if (string.IsNullOrWhiteSpace(contentQuestionId)) return null;
            return (Questions ?? new List<MathQuestion>())
                .FirstOrDefault(x => string.Equals(x.ContentQuestionId, contentQuestionId, StringComparison.Ordinal));
        }
    }

    public sealed class MathAuthoredQuestionSource
    {
        private static readonly HashSet<string> SupportedAnswerKinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "integer", "interaction_integer", "number", "decimal", "fraction", "text", "unit", "expression"
        };

        private static readonly HashSet<string> SupportedQuestionTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "expression_input", "interactive_measurement", "multiple_choice", "numeric_input", "true_false", "unit_input", "word_problem"
        };

        private readonly JavaScriptSerializer _json = new JavaScriptSerializer
        {
            MaxJsonLength = 8 * 1024 * 1024,
            RecursionLimit = 128
        };

        public MathAuthoredQuestionBank Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path");
            if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy Math question bank.", path);

            Dictionary<string, object> root;
            try { root = _json.DeserializeObject(File.ReadAllText(path)) as Dictionary<string, object>; }
            catch (Exception ex) { throw new InvalidDataException("Math question bank JSON invalid.", ex); }
            if (root == null) throw new InvalidDataException("Math question bank root must be object.");
            if (ReadInt(root, "schema_version") != 1) throw new InvalidDataException("Unsupported Math question bank schema_version.");
            if (!string.Equals(ReadString(root, "subject"), "math", StringComparison.Ordinal))
                throw new InvalidDataException("Math question bank subject mismatch.");
            if (!string.Equals(ReadString(root, "language"), "vi", StringComparison.Ordinal))
                throw new InvalidDataException("Math question bank language mismatch.");

            ValidateDeclaredSet(root, "supported_answer_kinds", SupportedAnswerKinds);
            ValidateDeclaredSet(root, "question_types", SupportedQuestionTypes);

            var questions = new List<MathQuestion>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in ReadObjectArray(root, "questions"))
            {
                var question = ReadQuestion(item);
                if (!ids.Add(question.ContentQuestionId))
                    throw new InvalidDataException("Duplicate authored Math question id: " + question.ContentQuestionId);
                questions.Add(question);
            }
            if (questions.Count == 0) throw new InvalidDataException("Math question bank contains no questions.");

            return new MathAuthoredQuestionBank
            {
                BankId = ReadString(root, "bank_id"),
                Grade = ReadInt(root, "grade"),
                CurriculumId = ReadString(root, "curriculum_id"),
                Questions = questions
            };
        }

        public static MathQuestion CreateRuntimeInstance(MathQuestion authored)
        {
            if (authored == null) throw new ArgumentNullException("authored");
            if (string.IsNullOrWhiteSpace(authored.ContentQuestionId))
                throw new InvalidOperationException("Authored question has no stable content id.");

            return new MathQuestion
            {
                QuestionId = authored.ContentQuestionId + "-" + Guid.NewGuid().ToString("N"),
                ContentQuestionId = authored.ContentQuestionId,
                LessonId = authored.LessonId,
                QuestionType = authored.QuestionType,
                Difficulty = authored.Difficulty,
                TemplateId = authored.TemplateId,
                SkillId = authored.SkillId,
                PromptVi = authored.PromptVi,
                ExplanationVi = authored.ExplanationVi,
                CorrectAnswer = authored.CorrectAnswer,
                Choices = Copy(authored.Choices),
                AnswerKind = authored.AnswerKind,
                CorrectAnswerText = authored.CorrectAnswerText,
                AcceptedAnswers = Copy(authored.AcceptedAnswers),
                NumericTolerance = authored.NumericTolerance,
                ExpectedUnit = authored.ExpectedUnit,
                AcceptedUnits = Copy(authored.AcceptedUnits),
                ChoiceTexts = Copy(authored.ChoiceTexts),
                IllustrationData = authored.IllustrationData,
                Representation = authored.Representation,
                HintLevel1 = authored.HintLevel1,
                HintLevel2 = authored.HintLevel2,
                DifficultyFit = authored.DifficultyFit
            };
        }

        private static MathQuestion ReadQuestion(Dictionary<string, object> item)
        {
            var id = ReadString(item, "id");
            var lessonId = ReadString(item, "lesson_id");
            var skillId = ReadString(item, "skill_id");
            var questionType = ReadString(item, "question_type");
            var answerKind = ReadString(item, "answer_kind");
            var difficulty = ReadString(item, "difficulty");
            if (!SupportedQuestionTypes.Contains(questionType))
                throw new InvalidDataException("Unsupported authored Math question_type: " + questionType);
            if (!SupportedAnswerKinds.Contains(answerKind))
                throw new InvalidDataException("Unsupported authored Math answer_kind: " + answerKind);
            if (!string.Equals(ReadString(item, "status"), "CHILD_READY", StringComparison.Ordinal))
                throw new InvalidDataException("Authored Math question is not CHILD_READY: " + id);
            if (difficulty != "basic" && difficulty != "medium" && difficulty != "application")
                throw new InvalidDataException("Unsupported authored Math difficulty: " + difficulty);

            object rawAnswer;
            if (!item.TryGetValue("correct_answer", out rawAnswer) || rawAnswer == null)
                throw new InvalidDataException("Authored Math question missing correct_answer: " + id);
            var answerText = Convert.ToString(rawAnswer, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(answerText))
                throw new InvalidDataException("Authored Math question has blank correct_answer: " + id);

            int fallback = 0;
            int parsedInt;
            if (int.TryParse(answerText, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedInt)) fallback = parsedInt;

            var hints = ReadStringArray(item, "hints_vi", true);
            if (hints.Count < 2) throw new InvalidDataException("Authored Math question requires two hints: " + id);
            var accepted = ReadStringArray(item, "accepted_answers", true);
            var choiceTexts = ReadChoices(item, questionType, id);
            var numericChoices = new List<int>();
            if (choiceTexts.Count > 0 && !string.Equals(answerKind, "text", StringComparison.Ordinal))
            {
                foreach (var choice in choiceTexts)
                {
                    int numeric;
                    if (!int.TryParse(choice, NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric))
                    {
                        numericChoices.Clear();
                        break;
                    }
                    numericChoices.Add(numeric);
                }
            }

            var expectedUnit = OptionalString(item, "expected_unit");
            var acceptedUnits = ReadStringArray(item, "accepted_units", false);
            var question = new MathQuestion
            {
                ContentQuestionId = id,
                LessonId = lessonId,
                QuestionType = questionType,
                Difficulty = difficulty,
                TemplateId = "authored:" + id,
                SkillId = skillId,
                PromptVi = ReadString(item, "prompt_vi"),
                ExplanationVi = ReadString(item, "explanation_vi"),
                CorrectAnswer = fallback,
                AnswerKind = answerKind,
                CorrectAnswerText = answerText,
                AcceptedAnswers = accepted,
                ExpectedUnit = expectedUnit,
                AcceptedUnits = acceptedUnits,
                HintLevel1 = hints[0],
                HintLevel2 = hints[1],
                Representation = "authored_" + questionType,
                DifficultyFit = DifficultyFit(difficulty),
                Choices = numericChoices,
                ChoiceTexts = string.Equals(answerKind, "text", StringComparison.Ordinal) ? choiceTexts : new List<string>()
            };

            // Numeric multiple-choice authored content stays numeric; input questions stay choice-free.
            if (choiceTexts.Count > 0 && numericChoices.Count == 0 && !string.Equals(answerKind, "text", StringComparison.Ordinal))
                question.ChoiceTexts = choiceTexts;
            if (questionType == "interactive_measurement" && answerKind == "interaction_integer")
                question.IllustrationData = "segmentdraw|" + fallback.ToString(CultureInfo.InvariantCulture) + "|15";

            if (answerKind == "unit")
            {
                if (string.IsNullOrWhiteSpace(expectedUnit))
                    throw new InvalidDataException("Unit question missing expected_unit: " + id);
                if (acceptedUnits.Count == 0) throw new InvalidDataException("Unit question missing accepted_units: " + id);
            }
            if (questionType == "multiple_choice" || questionType == "true_false")
            {
                if (question.DisplayChoices.Count < 2 || question.DisplayChoices.Count > 4)
                    throw new InvalidDataException("Choice question must expose 2 to 4 options: " + id);
                if (!question.DisplayChoices.Contains(answerText))
                    throw new InvalidDataException("Choice question options omit correct answer: " + id);
            }
            else if (questionType != "interactive_measurement" && question.DisplayChoices.Count > 0)
            {
                throw new InvalidDataException("Input authored question unexpectedly contains choices: " + id);
            }
            return question;
        }

        private static IList<string> ReadChoices(Dictionary<string, object> item, string questionType, string id)
        {
            object raw;
            if (!item.TryGetValue("choices", out raw) || raw == null)
            {
                if (questionType == "multiple_choice" || questionType == "true_false")
                    throw new InvalidDataException("Choice question missing choices: " + id);
                return new List<string>();
            }
            var array = raw as object[];
            if (array == null) throw new InvalidDataException("Authored Math choices must be array: " + id);
            var result = new List<string>();
            var choiceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rawChoice in array)
            {
                var choice = rawChoice as Dictionary<string, object>;
                if (choice == null) throw new InvalidDataException("Authored Math choice must be object: " + id);
                var choiceId = ReadString(choice, "id");
                if (!choiceIds.Add(choiceId)) throw new InvalidDataException("Duplicate authored choice id: " + id);
                result.Add(ReadString(choice, "text"));
            }
            return result;
        }

        private static void ValidateDeclaredSet(Dictionary<string, object> root, string key, HashSet<string> supported)
        {
            var values = ReadStringArray(root, key, true);
            foreach (var value in values)
                if (!supported.Contains(value)) throw new InvalidDataException("Question bank declares unsupported " + key + ": " + value);
        }

        private static IList<Dictionary<string, object>> ReadObjectArray(Dictionary<string, object> root, string key)
        {
            object raw;
            if (!root.TryGetValue(key, out raw) || raw == null) throw new InvalidDataException("Math question bank missing " + key + ".");
            var array = raw as object[];
            if (array == null) throw new InvalidDataException("Math question bank " + key + " must be array.");
            var result = new List<Dictionary<string, object>>();
            foreach (var value in array)
            {
                var item = value as Dictionary<string, object>;
                if (item == null) throw new InvalidDataException("Math question bank " + key + " item must be object.");
                result.Add(item);
            }
            return result;
        }

        private static IList<string> ReadStringArray(Dictionary<string, object> root, string key, bool requireNonEmpty)
        {
            object raw;
            if (!root.TryGetValue(key, out raw) || raw == null)
            {
                if (requireNonEmpty) throw new InvalidDataException("Math question bank missing " + key + ".");
                return new List<string>();
            }
            var array = raw as object[];
            if (array == null) throw new InvalidDataException("Math question bank " + key + " must be array.");
            var result = new List<string>();
            foreach (var item in array)
            {
                var text = Convert.ToString(item, CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Math question bank contains blank " + key + ".");
                result.Add(text.Trim());
            }
            if (requireNonEmpty && result.Count == 0) throw new InvalidDataException("Math question bank " + key + " must be non-empty.");
            return result;
        }

        private static string OptionalString(Dictionary<string, object> root, string key)
        {
            object raw;
            if (!root.TryGetValue(key, out raw) || raw == null) return null;
            var value = Convert.ToString(raw, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string ReadString(Dictionary<string, object> root, string key)
        {
            var value = OptionalString(root, key);
            if (value == null) throw new InvalidDataException("Math question bank missing/blank " + key + ".");
            return value;
        }

        private static int ReadInt(Dictionary<string, object> root, string key)
        {
            object raw;
            if (!root.TryGetValue(key, out raw) || raw == null) throw new InvalidDataException("Math question bank missing " + key + ".");
            try { return Convert.ToInt32(raw, CultureInfo.InvariantCulture); }
            catch (Exception ex) { throw new InvalidDataException("Math question bank invalid integer " + key + ".", ex); }
        }

        private static double DifficultyFit(string difficulty)
        {
            if (difficulty == "basic") return 0.45;
            if (difficulty == "medium") return 0.65;
            return 0.82;
        }

        private static IList<T> Copy<T>(IList<T> values)
        {
            return values == null ? new List<T>() : new List<T>(values);
        }
    }
}
