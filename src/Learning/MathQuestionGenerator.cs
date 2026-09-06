using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WAHU.Learning
{
    public sealed class MathQuestionGenerator
    {
        private readonly Random _random;

        public MathQuestionGenerator(int seed)
        {
            _random = new Random(seed);
        }

        public MathQuestion Generate(MathSelectionDecision decision)
        {
            if (decision == null || decision.Template == null) throw new ArgumentNullException("decision");
            MathQuestion question;
            if (decision.Template.TemplateId.StartsWith("possible_certain_impossible_die__", StringComparison.Ordinal))
            {
                question = ProbabilityEvent(decision.Template);
            }
            else if (decision.Template.TemplateId.StartsWith("geometry_identify_basic__", StringComparison.Ordinal))
            {
                question = GeometryVariant(decision.Template);
            }
            else if (decision.Template.TemplateId.StartsWith("pictograph_animals_legend1__", StringComparison.Ordinal))
            {
                question = PictographVariant(decision.Template);
            }
            else
            {
                switch (decision.Template.TemplateId)
                {
                    case "place_value_decompose_3digit": question = PlaceValueDecompose(decision.Template); break;
                    case "expanded_form_3digit": question = ExpandedForm(decision.Template); break;
                    case "predecessor_successor": question = PredecessorSuccessor(decision.Template); break;
                    case "compare_two_numbers_1000": question = CompareTwoNumbers(decision.Template); break;
                    case "mental_add_within_20": question = MentalAdd(decision.Template); break;
                    case "mental_sub_within_20": question = MentalSub(decision.Template); break;
                    case "times_table_2": question = Times(decision.Template, 2); break;
                    case "times_table_5": question = Times(decision.Template, 5); break;
                    case "divide_table_2_exact": question = Divide(decision.Template, 2); break;
                    case "divide_table_5_exact": question = Divide(decision.Template, 5); break;
                    case "add_within_1000_no_carry": question = AddNoCarry(decision.Template); break;
                    case "add_within_1000_one_carry": question = AddOneCarry(decision.Template); break;
                    case "subtract_within_1000_no_borrow": question = SubNoBorrow(decision.Template); break;
                    case "subtract_within_1000_one_borrow": question = SubOneBorrow(decision.Template); break;
                    case "polyline_length": question = PolylineLength(decision.Template); break;
                    case "clock_read_minute_hand_3_or_6": question = ClockRead(decision.Template); break;
                    default: throw new InvalidOperationException("Unsupported VERIFIED math template: " + decision.Template.TemplateId);
                }
            }

            question.QuestionId = decision.Template.TemplateId + "-" + Guid.NewGuid().ToString("N");
            question.TemplateId = decision.Template.TemplateId;
            question.SkillId = decision.Template.SkillId;
            question.Representation = RepresentationFor(decision.Template.TemplateId);
            question.DifficultyFit = decision.DifficultyFit;
            FinalizeAnswerOptions(question);
            return question;
        }

        private MathQuestion PlaceValueDecompose(MathTemplateRef template)
        {
            var h = _random.Next(1, 10);
            var t = _random.Next(0, 10);
            var o = _random.Next(0, 10);
            var n = 100 * h + 10 * t + o;
            var correct = PlaceValueText(h, t, o);
            var options = new List<string> { correct };
            AddUnique(options, PlaceValueText(h, o, t));
            AddUnique(options, PlaceValueText(h == 9 ? 8 : h + 1, t, o));
            AddUnique(options, PlaceValueText(h, t == 9 ? 8 : t + 1, o));
            AddUnique(options, PlaceValueText(h, t, o == 9 ? 8 : o + 1));
            return NewTextQuestion(template,
                "Số " + n + " gồm bao nhiêu trăm, chục và đơn vị?",
                correct, TakeAndShuffle(options, 4),
                "Tách số thành ba hàng: trăm, chục và đơn vị.",
                "Trong " + n + ", chữ số từ trái sang phải lần lượt cho biết số trăm, số chục và số đơn vị.");
        }

        private MathQuestion ExpandedForm(MathTemplateRef template)
        {
            var h = _random.Next(1, 10);
            var t = _random.Next(0, 10);
            var o = _random.Next(0, 10);
            var n = 100 * h + 10 * t + o;
            var correct = ExpandedText(h, t, o);
            var options = new List<string> { correct };
            AddUnique(options, ExpandedText(h == 9 ? 8 : h + 1, t, o));
            AddUnique(options, ExpandedText(h, t == 9 ? 8 : t + 1, o));
            AddUnique(options, ExpandedText(h, t, o == 9 ? 8 : o + 1));
            AddUnique(options, ExpandedText(h, o, t));
            return NewTextQuestion(template,
                "Viết " + n + " thành tổng của trăm, chục và đơn vị.",
                correct, TakeAndShuffle(options, 4),
                "Nhìn từng chữ số theo hàng trăm, chục và đơn vị.",
                h + " trăm là " + (h * 100) + ", " + t + " chục là " + (t * 10) + ", rồi cộng thêm " + o + " đơn vị.");
        }

        private MathQuestion PredecessorSuccessor(MathTemplateRef template)
        {
            var n = _random.Next(1, 1000);
            var correct = (n - 1) + " và " + (n + 1);
            var options = new List<string>
            {
                correct,
                n + " và " + (n + 1),
                (n - 1) + " và " + n,
                (n + 1) + " và " + (n - 1)
            };
            return NewTextQuestion(template,
                "Số liền trước và số liền sau của " + n + " là gì?",
                correct, TakeAndShuffle(options, 4),
                "Số liền trước kém " + n + " đúng 1; số liền sau hơn " + n + " đúng 1.",
                "Tính " + n + " - 1 và " + n + " + 1.");
        }

        private MathQuestion CompareTwoNumbers(MathTemplateRef template)
        {
            var a = _random.Next(0, 1001);
            var b = _random.Next(0, 1001);
            while (b == a) b = _random.Next(0, 1001);
            var correct = a > b ? ">" : "<";
            return NewTextQuestion(template,
                "Điền dấu > hoặc < : " + a + " __ " + b,
                correct, Shuffle(new List<string> { ">", "<" }),
                "So sánh từ hàng lớn nhất: trăm, rồi chục, rồi đơn vị.",
                "Số nào lớn hơn thì phía mở rộng của dấu so sánh quay về phía số đó.");
        }

        private MathQuestion ProbabilityEvent(MathTemplateRef template)
        {
            if (template == null || string.IsNullOrWhiteSpace(template.FixedContextVi) ||
                string.IsNullOrWhiteSpace(template.StatementVi) || string.IsNullOrWhiteSpace(template.AnswerText))
                throw new InvalidOperationException("VERIFIED probability variant metadata is incomplete.");
            var choices = Shuffle(new List<string> { "có thể", "chắc chắn", "không thể" });
            return NewTextQuestion(template,
                template.FixedContextVi + " " + template.StatementVi + " Điều này là gì?",
                template.AnswerText, choices,
                "Hãy nhìn tất cả sáu kết quả có thể xuất hiện trên xúc xắc: 1, 2, 3, 4, 5, 6.",
                "So sánh câu đã cho với toàn bộ sáu mặt của xúc xắc rồi chọn: có thể, chắc chắn hoặc không thể.");
        }

        private MathQuestion ClockRead(MathTemplateRef template)
        {
            var hour = _random.Next(1, 13);
            var minute = _random.Next(0, 2) == 0 ? 15 : 30;
            var correct = ClockText(hour, minute);
            var options = new List<string> { correct };
            AddUnique(options, ClockText(hour, minute == 15 ? 30 : 15));
            AddUnique(options, ClockText(hour == 12 ? 1 : hour + 1, minute));
            AddUnique(options, ClockText(hour == 1 ? 12 : hour - 1, minute));
            var question = NewTextQuestion(template,
                "Quan sát đồng hồ và chọn thời gian đúng.", correct, TakeAndShuffle(options, 4),
                "Kim phút chỉ số 3 là 15 phút; kim phút chỉ số 6 là 30 phút.",
                "Đọc kim phút trước, rồi nhìn vị trí kim giờ để chọn giờ đúng.");
            question.IllustrationData = "clock|" + hour.ToString(CultureInfo.InvariantCulture) + "|" + minute.ToString(CultureInfo.InvariantCulture);
            return question;
        }

        private MathQuestion GeometryVariant(MathTemplateRef template)
        {
            if (template == null || string.IsNullOrWhiteSpace(template.SkillId) || string.IsNullOrWhiteSpace(template.AnswerText))
                throw new InvalidOperationException("VERIFIED geometry variant metadata is incomplete.");
            var question = NewTextQuestion(template,
                "Quan sát hình minh họa. " + (template.StatementVi ?? "Hình được vẽ là gì?"),
                template.AnswerText, GeometryChoices(template.SkillId, template.AnswerText),
                "Hãy nhìn đặc điểm của hình: điểm, nét, số cạnh hoặc dạng khối.",
                GeometryHint(template.SkillId));
            question.IllustrationData = "geometry|" + template.SkillId;
            return question;
        }

        private MathQuestion PictographVariant(MathTemplateRef template)
        {
            if (template == null || string.IsNullOrWhiteSpace(template.SkillId) || string.IsNullOrWhiteSpace(template.AnswerText))
                throw new InvalidOperationException("VERIFIED pictograph variant metadata is incomplete.");
            int numeric;
            IList<string> choices;
            if (int.TryParse(template.AnswerText, NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric))
                choices = Shuffle(new List<string> { "1", "2", "3", "4" });
            else
                choices = Shuffle(new List<string> { "mèo", "chó", "thỏ" });
            var question = NewTextQuestion(template,
                "Quan sát biểu đồ tranh. " + (template.StatementVi ?? string.Empty),
                template.AnswerText, choices,
                "Mỗi hình đại diện 1 con. Hãy đếm hoặc so sánh số hình ở từng hàng.",
                "Biểu đồ có 3 mèo, 2 chó và 4 thỏ. Dùng các số đó để trả lời câu hỏi.");
            question.IllustrationData = "pictograph|cat=3|dog=2|rabbit=4|legend=1";
            return question;
        }

        private IList<string> GeometryChoices(string skillId, string correct)
        {
            List<string> values;
            switch (skillId)
            {
                case "POINT_RECOGNIZE":
                    values = new List<string> { "điểm", "đoạn thẳng", "đường thẳng", "đường cong" }; break;
                case "LINE_SEGMENT_RECOGNIZE":
                    values = new List<string> { "đoạn thẳng", "đường thẳng", "đường cong", "đường gấp khúc" }; break;
                case "CURVE_RECOGNIZE":
                    values = new List<string> { "đường cong", "đường thẳng", "đoạn thẳng", "đường gấp khúc" }; break;
                case "STRAIGHT_LINE_RECOGNIZE":
                    values = new List<string> { "đường thẳng", "đoạn thẳng", "đường cong", "đường gấp khúc" }; break;
                case "POLYLINE_RECOGNIZE":
                    values = new List<string> { "đường gấp khúc", "đường thẳng", "đường cong", "đoạn thẳng" }; break;
                case "THREE_COLLINEAR_POINTS":
                    values = new List<string> { "ba điểm thẳng hàng", "điểm", "đoạn thẳng", "đường gấp khúc" }; break;
                case "QUADRILATERAL_RECOGNIZE":
                    values = new List<string> { "hình tứ giác", "đường gấp khúc", "khối trụ", "khối cầu" }; break;
                case "CYLINDER_RECOGNIZE":
                case "SPHERE_RECOGNIZE":
                    values = new List<string> { "khối trụ", "khối cầu", "hình tứ giác", "đường cong" }; break;
                default:
                    throw new InvalidOperationException("Unsupported VERIFIED geometry skill: " + skillId);
            }
            if (!values.Contains(correct)) values[0] = correct;
            return Shuffle(values.Distinct(StringComparer.Ordinal).Take(4).ToList());
        }

        private static string GeometryHint(string skillId)
        {
            switch (skillId)
            {
                case "POINT_RECOGNIZE": return "Điểm chỉ một vị trí; nó không có độ dài như đoạn thẳng.";
                case "LINE_SEGMENT_RECOGNIZE": return "Đoạn thẳng có hai đầu mút rõ ràng.";
                case "CURVE_RECOGNIZE": return "Đường cong đổi hướng liên tục, không tạo các đoạn thẳng gãy.";
                case "STRAIGHT_LINE_RECOGNIZE": return "Đường thẳng đi thẳng theo một hướng và được hiểu là kéo dài về hai phía.";
                case "POLYLINE_RECOGNIZE": return "Đường gấp khúc gồm nhiều đoạn thẳng nối tiếp nhau.";
                case "THREE_COLLINEAR_POINTS": return "Ba điểm thẳng hàng khi cùng nằm trên một đường thẳng.";
                case "QUADRILATERAL_RECOGNIZE": return "Hình tứ giác có bốn cạnh.";
                case "CYLINDER_RECOGNIZE": return "Khối trụ có hai mặt đáy tròn và một mặt cong xung quanh.";
                case "SPHERE_RECOGNIZE": return "Khối cầu tròn đều theo mọi hướng, giống một quả bóng.";
                default: return "Quan sát đặc điểm của hình rồi chọn tên phù hợp.";
            }
        }

        private static string ClockText(int hour, int minute)
        {
            return hour.ToString(CultureInfo.InvariantCulture) + " giờ " + minute.ToString("00", CultureInfo.InvariantCulture) + " phút";
        }

        private MathQuestion MentalAdd(MathTemplateRef template)
        {
            var a = _random.Next(2, 16);
            var b = _random.Next(1, 21 - a);
            return NewNumericQuestion(template, "Tính nhẩm: " + a + " + " + b + " = ?", a + b,
                "Con thử bắt đầu từ số lớn hơn rồi đếm thêm " + b + ".",
                "Có thể tách " + b + " thành phần giúp tròn 10 trước, rồi cộng phần còn lại.");
        }

        private MathQuestion MentalSub(MathTemplateRef template)
        {
            var a = _random.Next(5, 21);
            var b = _random.Next(1, a + 1);
            return NewNumericQuestion(template, "Tính nhẩm: " + a + " - " + b + " = ?", a - b,
                "Con thử lùi " + b + " bước từ " + a + ".",
                "Tách số trừ thành phần dễ bớt trước, rồi bớt phần còn lại.");
        }

        private MathQuestion Times(MathTemplateRef template, int factor)
        {
            var n = _random.Next(1, 11);
            return NewNumericQuestion(template, "Tính: " + factor + " × " + n + " = ?", factor * n,
                "Hãy nghĩ thành " + n + " nhóm, mỗi nhóm có " + factor + ".",
                "Có thể cộng " + factor + " lặp lại " + n + " lần.");
        }

        private MathQuestion Divide(MathTemplateRef template, int divisor)
        {
            var answer = _random.Next(1, 11);
            var dividend = divisor * answer;
            return NewNumericQuestion(template, "Tính: " + dividend + " : " + divisor + " = ?", answer,
                "Hãy chia " + dividend + " đồ vật thành các nhóm, mỗi nhóm " + divisor + ".",
                "Tìm số mà khi nhân với " + divisor + " thì được " + dividend + ".");
        }

        private MathQuestion AddNoCarry(MathTemplateRef template)
        {
            int a, b;
            GenerateNoCarryPair(out a, out b);
            return NewNumericQuestion(template, "Tính: " + a + " + " + b + " = ?", a + b,
                "Cộng lần lượt hàng đơn vị, hàng chục rồi hàng trăm.",
                "Mỗi hàng trong phép tính này đều có tổng nhỏ hơn 10 nên không cần nhớ.");
        }

        private MathQuestion AddOneCarry(MathTemplateRef template)
        {
            int a, b;
            GenerateExactlyOneCarryPair(out a, out b);
            return NewNumericQuestion(template, "Tính: " + a + " + " + b + " = ?", a + b,
                "Tìm hàng có tổng từ 10 trở lên rồi nhớ 1 sang hàng bên trái.",
                "Phép tính này chỉ có đúng một lần nhớ. Viết phần đơn vị của tổng ở hàng đó và nhớ 1.");
        }

        private MathQuestion SubNoBorrow(MathTemplateRef template)
        {
            int b, result;
            GenerateNoCarryPair(out b, out result);
            var a = b + result;
            return NewNumericQuestion(template, "Tính: " + a + " - " + b + " = ?", result,
                "Trừ lần lượt hàng đơn vị, chục rồi trăm.",
                "Mỗi chữ số của số bị trừ đủ lớn nên phép tính này không cần mượn.");
        }

        private MathQuestion SubOneBorrow(MathTemplateRef template)
        {
            int b, result;
            GenerateExactlyOneCarryPair(out b, out result);
            var a = b + result;
            return NewNumericQuestion(template, "Tính: " + a + " - " + b + " = ?", result,
                "Tìm hàng chưa đủ để trừ rồi mượn 1 từ hàng bên trái.",
                "Phép tính này chỉ có đúng một lần mượn. Sau khi mượn, nhớ giảm hàng bên trái đi 1.");
        }

        private MathQuestion PolylineLength(MathTemplateRef template)
        {
            var a = _random.Next(1, 21);
            var b = _random.Next(1, 21);
            var c = _random.Next(1, 21);
            return NewNumericQuestion(template,
                "Đường gấp khúc có ba đoạn dài " + a + " cm, " + b + " cm và " + c + " cm. Độ dài đường gấp khúc là bao nhiêu?",
                a + b + c,
                "Độ dài cả đường bằng tổng độ dài của tất cả các đoạn.",
                "Cộng lần lượt: " + a + " + " + b + " + " + c + ".");
        }

        private void GenerateNoCarryPair(out int a, out int b)
        {
            var ah = _random.Next(0, 6); var bh = _random.Next(0, Math.Min(4, 10 - ah));
            var at = _random.Next(0, 10); var bt = _random.Next(0, 10 - at);
            var ao = _random.Next(0, 10); var bo = _random.Next(0, 10 - ao);
            a = ah * 100 + at * 10 + ao;
            b = bh * 100 + bt * 10 + bo;
            if (a == 0 && b == 0) a = 10;
        }

        private void GenerateExactlyOneCarryPair(out int a, out int b)
        {
            if (_random.Next(0, 2) == 0)
            {
                var ao = _random.Next(5, 10); var bo = _random.Next(10 - ao, 10);
                var at = _random.Next(0, 8); var bt = _random.Next(0, 9 - at);
                var ah = _random.Next(0, 5); var bh = _random.Next(0, 9 - ah);
                a = ah * 100 + at * 10 + ao;
                b = bh * 100 + bt * 10 + bo;
            }
            else
            {
                var ao = _random.Next(0, 10); var bo = _random.Next(0, 10 - ao);
                var at = _random.Next(5, 10); var bt = _random.Next(10 - at, 10);
                var ah = _random.Next(0, 4); var bh = _random.Next(0, 9 - ah);
                a = ah * 100 + at * 10 + ao;
                b = bh * 100 + bt * 10 + bo;
            }
            if (a + b > 1000) GenerateExactlyOneCarryPair(out a, out b);
        }

        private static string RepresentationFor(string templateId)
        {
            if (!string.IsNullOrWhiteSpace(templateId) && templateId.StartsWith("possible_certain_impossible_die__", StringComparison.Ordinal))
                return "die_outcomes";
            if (!string.IsNullOrWhiteSpace(templateId) && templateId.StartsWith("geometry_identify_basic__", StringComparison.Ordinal))
                return "geometry_basic";
            if (!string.IsNullOrWhiteSpace(templateId) && templateId.StartsWith("pictograph_animals_legend1__", StringComparison.Ordinal))
                return "pictograph";
            switch (templateId)
            {
                case "place_value_decompose_3digit":
                case "expanded_form_3digit":
                    return "place_value_blocks";
                case "predecessor_successor":
                case "compare_two_numbers_1000":
                    return "number_line_1000";
                case "mental_add_within_20":
                case "mental_sub_within_20":
                    return "number_ray";
                case "times_table_2":
                case "times_table_5":
                    return "equal_groups";
                case "divide_table_2_exact":
                case "divide_table_5_exact":
                    return "equal_groups_division";
                case "add_within_1000_no_carry":
                case "add_within_1000_one_carry":
                case "subtract_within_1000_no_borrow":
                case "subtract_within_1000_one_borrow":
                    return "place_value";
                case "polyline_length":
                    return "polyline";
                case "clock_read_minute_hand_3_or_6":
                    return "clock";
                default:
                    return "symbolic";
            }
        }

        private static MathQuestion NewNumericQuestion(MathTemplateRef template, string prompt, int answer, string hint1, string hint2)
        {
            return new MathQuestion
            {
                TemplateId = template.TemplateId,
                SkillId = template.SkillId,
                PromptVi = prompt,
                CorrectAnswer = answer,
                AnswerKind = "integer",
                CorrectAnswerText = answer.ToString(CultureInfo.InvariantCulture),
                HintLevel1 = hint1,
                HintLevel2 = hint2
            };
        }

        private static MathQuestion NewTextQuestion(MathTemplateRef template, string prompt, string answer, IList<string> choices, string hint1, string hint2)
        {
            return new MathQuestion
            {
                TemplateId = template.TemplateId,
                SkillId = template.SkillId,
                PromptVi = prompt,
                CorrectAnswer = 0,
                AnswerKind = "text",
                CorrectAnswerText = answer,
                ChoiceTexts = choices,
                HintLevel1 = hint1,
                HintLevel2 = hint2
            };
        }

        private void FinalizeAnswerOptions(MathQuestion question)
        {
            if (question.UsesTextChoices)
            {
                if (question.ChoiceTexts == null || question.ChoiceTexts.Count < 2)
                    throw new InvalidOperationException("Text math question requires at least two choices.");
                if (!question.ChoiceTexts.Contains(question.CorrectAnswerText))
                    throw new InvalidOperationException("Text math question choices omit correct answer.");
                question.Choices = new int[0];
                return;
            }

            question.AnswerKind = "integer";
            question.CorrectAnswerText = question.CorrectAnswer.ToString(CultureInfo.InvariantCulture);
            question.Choices = BuildChoices(question.CorrectAnswer);
            question.ChoiceTexts = question.Choices.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList();
        }

        private IList<int> BuildChoices(int correct)
        {
            var values = new HashSet<int> { correct };
            var offsets = new[] { -10, 10, -1, 1, -2, 2, -5, 5, -100, 100 };
            var start = _random.Next(0, offsets.Length);
            for (var i = 0; values.Count < 4 && i < offsets.Length * 2; i++)
            {
                var candidate = correct + offsets[(start + i) % offsets.Length];
                if (candidate >= 0 && candidate <= 1000) values.Add(candidate);
            }
            var fallback = 0;
            while (values.Count < 4) { if (fallback != correct) values.Add(fallback); fallback++; }
            return Shuffle(values.ToList());
        }

        private IList<string> TakeAndShuffle(IList<string> source, int count)
        {
            return Shuffle(source.Distinct(StringComparer.Ordinal).Take(count).ToList());
        }

        private IList<T> Shuffle<T>(IList<T> source)
        {
            var list = source.ToList();
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = _random.Next(0, i + 1);
                var temp = list[i]; list[i] = list[j]; list[j] = temp;
            }
            return list;
        }

        private static void AddUnique(ICollection<string> values, string value)
        {
            if (!values.Contains(value)) values.Add(value);
        }

        private static string PlaceValueText(int h, int t, int o)
        {
            return h + " trăm, " + t + " chục, " + o + " đơn vị";
        }

        private static string ExpandedText(int h, int t, int o)
        {
            return (h * 100).ToString(CultureInfo.InvariantCulture) + " + " +
                   (t * 10).ToString(CultureInfo.InvariantCulture) + " + " +
                   o.ToString(CultureInfo.InvariantCulture);
        }
    }
}
