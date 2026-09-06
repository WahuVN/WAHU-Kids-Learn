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
                    case "word_problem_add_more": question = WordProblemAddMore(decision.Template); break;
                    case "word_problem_sub_less": question = WordProblemSubLess(decision.Template); break;
                    case "word_problem_more_than": question = WordProblemMoreThan(decision.Template); break;
                    case "word_problem_less_than": question = WordProblemLessThan(decision.Template); break;
                    case "word_problem_multiply_groups_2_5": question = WordProblemMultiply(decision.Template); break;
                    case "word_problem_divide_groups_2_5": question = WordProblemDivide(decision.Template); break;
                    case "add_components_recognize": question = AddComponents(decision.Template); break;
                    case "sub_components_recognize": question = SubComponents(decision.Template); break;
                    case "multiplication_meaning_groups": question = MultiplicationMeaning(decision.Template); break;
                    case "division_meaning_share": question = DivisionMeaning(decision.Template); break;
                    case "multiplication_components_recognize": question = MultiplicationComponents(decision.Template); break;
                    case "division_components_recognize": question = DivisionComponents(decision.Template); break;
                    case "operation_meaning_from_visual": question = OperationMeaningFromVisual(decision.Template); break;
                    case "word_problem_select_operation_one_step": question = WordProblemSelectOperation(decision.Template); break;
                    case "full_hundreds_recognize": question = FullHundredsRecognize(decision.Template); break;
                    case "number_ray_fill_1000": question = NumberRayFill(decision.Template); break;
                    case "min_max_up_to_4": question = MinMaxUpToFour(decision.Template); break;
                    case "sort_up_to_4": question = SortUpToFour(decision.Template); break;
                    case "add_sub_two_operators_left_to_right": question = TwoOperatorsLeftToRight(decision.Template); break;
                    case "mental_round_tens_hundreds_1000": question = MentalRoundTensHundreds(decision.Template); break;
                    case "heavier_lighter_balance": question = HeavierLighterBalance(decision.Template); break;
                    case "mass_kg_read_write": question = MassKgReadWrite(decision.Template); break;
                    case "capacity_liter_read_write": question = CapacityLiterReadWrite(decision.Template); break;
                    case "length_dm_m_km_relation": question = LengthUnitRelation(decision.Template); break;
                    case "time_day_24_hours": question = TimeDay24Hours(decision.Template); break;
                    case "time_hour_60_minutes": question = TimeHour60Minutes(decision.Template); break;
                    case "calendar_days_in_month_date": question = CalendarDaysInMonthDate(decision.Template); break;
                    case "measure_with_ruler_cm": question = MeasureWithRulerCm(decision.Template); break;
                    case "measure_with_common_scale": question = MeasureWithCommonScale(decision.Template); break;
                    case "measurement_convert_calculate_learned_units": question = MeasurementConvertCalculate(decision.Template); break;
                    case "measurement_real_world_one_step": question = MeasurementRealWorldOneStep(decision.Template); break;
                    case "data_collect_classify_count": question = DataCollectClassifyCount(decision.Template); break;
                    case "count_place_value_to_1000": question = CountPlaceValueTo1000(decision.Template); break;
                    case "read_number_to_1000": question = ReadNumberTo1000(decision.Template); break;
                    case "write_number_to_1000": question = WriteNumberTo1000(decision.Template); break;
                    case "estimate_objects_by_tens": question = EstimateObjectsByTens(decision.Template); break;
                    case "measurement_estimate_reference_10cm": question = MeasurementEstimateReference10Cm(decision.Template); break;
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

        private MathQuestion AddComponents(MathTemplateRef template)
        {
            var a = _random.Next(1, 61);
            var b = _random.Next(1, 41);
            while (a + b > 100) b = _random.Next(1, 41);
            var result = a + b;
            var target = _random.Next(0, 3);
            var value = target == 0 ? a : (target == 1 ? b : result);
            var answer = target < 2 ? "số hạng" : "tổng";
            var question = NewTextQuestion(template,
                "Trong phép tính " + a + " + " + b + " = " + result + ", số " + value + " được gọi là gì?",
                answer, Shuffle(new List<string> { "số hạng", "tổng", "hiệu", "số trừ" }),
                "Hai số đứng trước dấu bằng trong phép cộng là các số hạng.",
                "Kết quả của phép cộng được gọi là tổng.");
            question.IllustrationData = "equationparts|add|" + a + "|" + b + "|" + result + "|" + target;
            return question;
        }

        private MathQuestion SubComponents(MathTemplateRef template)
        {
            var a = _random.Next(10, 101);
            var b = _random.Next(1, a);
            while (a - b == b) b = _random.Next(1, a);
            var result = a - b;
            var target = _random.Next(0, 3);
            var value = target == 0 ? a : (target == 1 ? b : result);
            var answer = target == 0 ? "số bị trừ" : (target == 1 ? "số trừ" : "hiệu");
            var question = NewTextQuestion(template,
                "Trong phép tính " + a + " - " + b + " = " + result + ", số " + value + " được gọi là gì?",
                answer, Shuffle(new List<string> { "số bị trừ", "số trừ", "hiệu", "tổng" }),
                "Trong phép trừ, số đứng trước dấu trừ là số bị trừ; số đứng sau dấu trừ là số trừ.",
                "Kết quả của phép trừ được gọi là hiệu.");
            question.IllustrationData = "equationparts|sub|" + a + "|" + b + "|" + result + "|" + target;
            return question;
        }

        private MathQuestion MultiplicationComponents(MathTemplateRef template)
        {
            var factor = _random.Next(0, 2) == 0 ? 2 : 5;
            var other = _random.Next(2, 11);
            var result = factor * other;
            var target = _random.Next(0, 3);
            var value = target == 0 ? factor : (target == 1 ? other : result);
            var answer = target < 2 ? "thừa số" : "tích";
            var question = NewTextQuestion(template,
                "Trong phép tính " + factor + " × " + other + " = " + result + ", số " + value + " được gọi là gì?",
                answer, Shuffle(new List<string> { "thừa số", "tích", "số chia", "thương" }),
                "Hai số được nhân với nhau gọi là các thừa số.",
                "Kết quả của phép nhân được gọi là tích.");
            question.IllustrationData = "equationparts|mul|" + factor + "|" + other + "|" + result + "|" + target;
            return question;
        }

        private MathQuestion DivisionComponents(MathTemplateRef template)
        {
            var divisor = _random.Next(0, 2) == 0 ? 2 : 5;
            var quotient = _random.Next(1, 11);
            while (quotient == divisor) quotient = _random.Next(1, 11);
            var total = divisor * quotient;
            var target = _random.Next(0, 3);
            var value = target == 0 ? total : (target == 1 ? divisor : quotient);
            var answer = target == 0 ? "số bị chia" : (target == 1 ? "số chia" : "thương");
            var question = NewTextQuestion(template,
                "Trong phép tính " + total + " : " + divisor + " = " + quotient + ", số " + value + " được gọi là gì?",
                answer, Shuffle(new List<string> { "số bị chia", "số chia", "thương", "thừa số" }),
                "Trong phép chia, số đứng trước dấu chia là số bị chia; số đứng sau dấu chia là số chia.",
                "Kết quả của phép chia được gọi là thương.");
            question.IllustrationData = "equationparts|div|" + total + "|" + divisor + "|" + quotient + "|" + target;
            return question;
        }

        private MathQuestion MultiplicationMeaning(MathTemplateRef template)
        {
            var factor = _random.Next(0, 2) == 0 ? 2 : 5;
            var groups = _random.Next(2, 10);
            var correct = groups + " × " + factor;
            var otherFactor = factor == 2 ? 5 : 2;
            var choices = new List<string>
            {
                correct,
                (groups + 1) + " × " + factor,
                groups + " × " + otherFactor,
                (groups - 1) + " × " + factor
            };
            var question = NewTextQuestion(template,
                "Quan sát các nhóm bằng nhau. Phép nhân nào biểu diễn đúng mô hình?",
                correct, Shuffle(choices.Distinct(StringComparer.Ordinal).ToList()),
                "Đếm số nhóm và số vật trong mỗi nhóm.",
                "Viết: số nhóm × số vật trong mỗi nhóm.");
            question.IllustrationData = "wordgroups|" + factor + "|" + groups;
            return question;
        }

        private MathQuestion DivisionMeaning(MathTemplateRef template)
        {
            var divisor = _random.Next(0, 2) == 0 ? 2 : 5;
            var otherDivisor = divisor == 2 ? 5 : 2;
            var quotient = _random.Next(1, 11);
            while (quotient == divisor || quotient == otherDivisor) quotient = _random.Next(1, 11);
            var total = divisor * quotient;
            var correct = total + " : " + divisor;
            var choices = new List<string>
            {
                correct,
                (total + divisor) + " : " + divisor,
                total + " : " + quotient,
                total + " : " + otherDivisor
            };
            var question = NewTextQuestion(template,
                "Quan sát việc chia đều. Phép chia nào biểu diễn đúng mô hình?",
                correct, TakeAndShuffle(choices, 4),
                "Nhìn tổng số vật và số phần bằng nhau.",
                "Viết: tổng số vật : số phần bằng nhau.");
            question.IllustrationData = "wordshare|" + total + "|" + divisor;
            return question;
        }

        private MathQuestion OperationMeaningFromVisual(MathTemplateRef template)
        {
            var relation = _random.Next(0, 4);
            string answer;
            string data;
            if (relation == 0)
            {
                var a = _random.Next(5, 31); var b = _random.Next(1, 21);
                answer = "cộng"; data = "wordbar|add|" + a + "|" + b;
            }
            else if (relation == 1)
            {
                var a = _random.Next(10, 51); var b = _random.Next(1, a);
                answer = "trừ"; data = "wordbar|sub|" + a + "|" + b;
            }
            else if (relation == 2)
            {
                var factor = _random.Next(0, 2) == 0 ? 2 : 5; var groups = _random.Next(2, 9);
                answer = "nhân"; data = "wordgroups|" + factor + "|" + groups;
            }
            else
            {
                var divisor = _random.Next(0, 2) == 0 ? 2 : 5; var quotient = _random.Next(1, 9);
                answer = "chia"; data = "wordshare|" + (divisor * quotient) + "|" + divisor;
            }
            var question = NewTextQuestion(template,
                "Quan sát mô hình. Mô hình phù hợp nhất với phép tính nào?",
                answer, Shuffle(OperationChoices()),
                "Xác định mô hình đang gộp, bớt, tạo các nhóm bằng nhau hay chia đều.",
                "Gộp → cộng; bớt → trừ; nhóm bằng nhau → nhân; chia thành phần bằng nhau → chia.");
            question.IllustrationData = data;
            return question;
        }

        private MathQuestion WordProblemSelectOperation(MathTemplateRef template)
        {
            var relation = _random.Next(0, 6);
            string answer;
            string data;
            string prompt;
            if (relation == 0)
            {
                var a = _random.Next(5, 41); var b = _random.Next(1, 31);
                answer = "cộng"; data = "wordbar|add|" + a + "|" + b;
                prompt = "Lan có " + a + " nhãn vở, được cho thêm " + b + " nhãn. Muốn tìm tất cả, phép tính nào phù hợp?";
            }
            else if (relation == 1)
            {
                var a = _random.Next(10, 61); var b = _random.Next(1, a);
                answer = "trừ"; data = "wordbar|sub|" + a + "|" + b;
                prompt = "Lan có " + a + " nhãn vở, cho bạn " + b + " nhãn. Muốn tìm số còn lại, phép tính nào phù hợp?";
            }
            else if (relation == 2)
            {
                var a = _random.Next(5, 41); var b = _random.Next(1, 21);
                answer = "cộng"; data = "wordbar|more|" + a + "|" + b;
                prompt = "Mai có " + a + " bông hoa. Lan nhiều hơn Mai " + b + " bông. Muốn tìm số hoa của Lan, phép tính nào phù hợp?";
            }
            else if (relation == 3)
            {
                var a = _random.Next(10, 61); var b = _random.Next(1, a);
                answer = "trừ"; data = "wordbar|less|" + a + "|" + b;
                prompt = "Lan có " + a + " bông hoa. Mai ít hơn Lan " + b + " bông. Muốn tìm số hoa của Mai, phép tính nào phù hợp?";
            }
            else if (relation == 4)
            {
                var factor = _random.Next(0, 2) == 0 ? 2 : 5; var groups = _random.Next(2, 9);
                answer = "nhân"; data = "wordgroups|" + factor + "|" + groups;
                prompt = "Có " + groups + " giỏ, mỗi giỏ " + factor + " quả. Muốn tìm tất cả số quả, phép tính nào phù hợp?";
            }
            else
            {
                var divisor = _random.Next(0, 2) == 0 ? 2 : 5; var quotient = _random.Next(1, 9); var total = divisor * quotient;
                answer = "chia"; data = "wordshare|" + total + "|" + divisor;
                prompt = "Có " + total + " chiếc bánh chia đều cho " + divisor + " bạn. Muốn tìm mỗi bạn được bao nhiêu, phép tính nào phù hợp?";
            }
            var question = NewTextQuestion(template, prompt, answer, Shuffle(OperationChoices()),
                "Xác định dữ kiện, điều cần tìm và quan hệ giữa chúng trước khi chọn phép tính.",
                "Gộp/nhiều hơn → cộng; bớt/ít hơn → trừ; nhóm đều → nhân; chia đều → chia.");
            question.IllustrationData = data;
            return question;
        }

        private static List<string> OperationChoices()
        {
            return new List<string> { "cộng", "trừ", "nhân", "chia" };
        }

        private MathQuestion WordProblemAddMore(MathTemplateRef template)
        {
            var a = _random.Next(5, 51);
            var maxB = Math.Min(40, 100 - a);
            var b = _random.Next(1, maxB + 1);
            var question = NewNumericQuestion(template,
                "Lan có " + a + " nhãn vở. Mẹ cho thêm " + b + " nhãn vở. Lan có tất cả bao nhiêu nhãn vở?",
                a + b,
                "Sơ đồ có hai phần: số nhãn Lan đã có và số nhãn được cho thêm. Cần tìm cả hai phần gộp lại.",
                "Phép tính phù hợp là " + a + " + " + b + ".");
            question.IllustrationData = "wordbar|add|" + a + "|" + b;
            return question;
        }

        private MathQuestion WordProblemSubLess(MathTemplateRef template)
        {
            var a = _random.Next(10, 101);
            var maxB = Math.Min(40, a);
            var b = _random.Next(1, maxB + 1);
            var question = NewNumericQuestion(template,
                "Lan có " + a + " nhãn vở. Lan cho bạn " + b + " nhãn vở. Lan còn lại bao nhiêu nhãn vở?",
                a - b,
                "Sơ đồ bắt đầu bằng cả số nhãn Lan có, rồi tách ra phần đã cho bạn. Cần tìm phần còn lại.",
                "Phép tính phù hợp là " + a + " - " + b + ".");
            question.IllustrationData = "wordbar|sub|" + a + "|" + b;
            return question;
        }

        private MathQuestion WordProblemMoreThan(MathTemplateRef template)
        {
            var a = _random.Next(5, 51);
            var maxB = Math.Min(30, 100 - a);
            var b = _random.Next(1, maxB + 1);
            var question = NewNumericQuestion(template,
                "Mai có " + a + " bông hoa. Lan có nhiều hơn Mai " + b + " bông hoa. Lan có bao nhiêu bông hoa?",
                a + b,
                "Lan có một phần bằng số hoa của Mai và thêm một phần nhiều hơn.",
                "Phép tính phù hợp là " + a + " + " + b + ".");
            question.IllustrationData = "wordbar|more|" + a + "|" + b;
            return question;
        }

        private MathQuestion WordProblemLessThan(MathTemplateRef template)
        {
            var a = _random.Next(10, 101);
            var maxB = Math.Min(30, a);
            var b = _random.Next(1, maxB + 1);
            var question = NewNumericQuestion(template,
                "Lan có " + a + " bông hoa. Mai có ít hơn Lan " + b + " bông hoa. Mai có bao nhiêu bông hoa?",
                a - b,
                "Thanh của Mai ngắn hơn thanh của Lan đúng phần chênh lệch đã biết.",
                "Phép tính phù hợp là " + a + " - " + b + ".");
            question.IllustrationData = "wordbar|less|" + a + "|" + b;
            return question;
        }

        private MathQuestion WordProblemMultiply(MathTemplateRef template)
        {
            var factor = _random.Next(0, 2) == 0 ? 2 : 5;
            var groups = _random.Next(1, 11);
            var question = NewNumericQuestion(template,
                "Có " + groups + " giỏ, mỗi giỏ có " + factor + " quả. Có tất cả bao nhiêu quả?",
                groups * factor,
                "Mỗi giỏ có số quả bằng nhau. Hãy nghĩ thành " + groups + " nhóm, mỗi nhóm " + factor + ".",
                "Phép tính phù hợp là " + groups + " × " + factor + ".");
            question.IllustrationData = "wordgroups|" + factor + "|" + groups;
            return question;
        }

        private MathQuestion WordProblemDivide(MathTemplateRef template)
        {
            var divisor = _random.Next(0, 2) == 0 ? 2 : 5;
            var quotient = _random.Next(1, 11);
            var total = divisor * quotient;
            var question = NewNumericQuestion(template,
                "Có " + total + " chiếc bánh chia đều cho " + divisor + " bạn. Mỗi bạn được bao nhiêu chiếc bánh?",
                quotient,
                "Chia đều nghĩa là mỗi bạn nhận số bánh bằng nhau. Hãy phân " + total + " chiếc bánh vào " + divisor + " phần bằng nhau.",
                "Phép tính phù hợp là " + total + " : " + divisor + ".");
            question.IllustrationData = "wordshare|" + total + "|" + divisor;
            return question;
        }

        private MathQuestion EstimateObjectsByTens(MathTemplateRef template)
        {
            var targetTens = _random.Next(2, 10) * 10;
            var offsetOptions = new[] { -4, -3, -2, -1, 0, 1, 2, 3, 4 };
            var count = targetTens + offsetOptions[_random.Next(0, offsetOptions.Length)];
            count = Math.Max(16, Math.Min(94, count));
            var nearest = ((count + 5) / 10) * 10;
            var values = new HashSet<int> { nearest };
            foreach (var delta in new[] { -10, 10, -20, 20, -30, 30 })
            {
                var candidate = nearest + delta;
                if (candidate >= 10 && candidate <= 100) values.Add(candidate);
                if (values.Count >= 4) break;
            }
            var question = NewNumericQuestion(template,
                "Không đếm từng chấm. Nhóm chấm này gần với bao nhiêu chục nhất?", nearest,
                "Nhìn cả nhóm và tưởng tượng chia thành các nhóm khoảng 10.",
                "So sánh xem nhóm chấm gần mốc chục nào nhất.");
            question.Choices = Shuffle(values.Take(4).ToList());
            question.IllustrationData = "estimatedots|" + count + "|" + nearest;
            return question;
        }

        private MathQuestion MeasurementEstimateReference10Cm(MathTemplateRef template)
        {
            var target = _random.Next(1, 6) * 10;
            var jitterPercent = _random.Next(-8, 9);
            var choices = new HashSet<int> { target };
            foreach (var delta in new[] { -10, 10, -20, 20, -30, 30 })
            {
                var candidate = target + delta;
                if (candidate >= 10 && candidate <= 60) choices.Add(candidate);
                if (choices.Count >= 4) break;
            }
            var question = NewTextQuestion(template,
                "Thanh mẫu dài 10 cm. Đoạn màu cam dài gần bao nhiêu xăng-ti-mét?", target + " cm",
                Shuffle(choices.Take(4).Select(x => x + " cm").ToList()),
                "So sánh độ dài đoạn màu cam với thanh mẫu 10 cm.",
                "Ước lượng xem có thể đặt khoảng bao nhiêu thanh 10 cm nối tiếp nhau.");
            question.IllustrationData = "estimatelength|10|" + target + "|" + jitterPercent;
            return question;
        }

        private MathQuestion CountPlaceValueTo1000(MathTemplateRef template)
        {
            var h = _random.Next(0, 10);
            var t = _random.Next(0, 10);
            var o = _random.Next(0, 10);
            if (h == 0 && t == 0 && o == 0) o = 1;
            var value = h * 100 + t * 10 + o;
            var distractors = new HashSet<int> { value };
            foreach (var candidate in new[]
            {
                t * 100 + h * 10 + o,
                h * 100 + o * 10 + t,
                Math.Min(1000, value + 100),
                Math.Max(0, value - 100),
                Math.Min(1000, value + 10),
                Math.Max(0, value - 10),
                Math.Min(1000, value + 1),
                Math.Max(0, value - 1)
            })
            {
                if (candidate >= 0 && candidate <= 1000) distractors.Add(candidate);
                if (distractors.Count >= 4) break;
            }
            for (var candidate = 0; distractors.Count < 4 && candidate <= 1000; candidate++) distractors.Add(candidate);
            var question = NewNumericQuestion(template,
                "Quan sát mô hình khối trăm, thanh chục và ô đơn vị. Số nào được biểu diễn?", value,
                "Đếm riêng số trăm, số chục và số đơn vị.",
                h + " trăm, " + t + " chục, " + o + " đơn vị.");
            question.Choices = Shuffle(distractors.Take(4).ToList());
            question.IllustrationData = "base10count|" + h + "|" + t + "|" + o;
            return question;
        }

        private MathQuestion ReadNumberTo1000(MathTemplateRef template)
        {
            var value = _random.Next(0, 1001);
            var correct = NumberToVietnamese(value);
            var candidates = NearbyNumberCandidates(value);
            var choices = new List<string> { correct };
            foreach (var candidate in candidates)
            {
                AddUnique(choices, NumberToVietnamese(candidate));
                if (choices.Count >= 4) break;
            }
            var question = NewTextQuestion(template,
                "Số " + value + " đọc là:", correct, TakeAndShuffle(choices, 4),
                "Đọc từ hàng trăm đến hàng chục rồi hàng đơn vị.",
                "Chú ý cách đọc mốt, tư, lăm và linh khi phù hợp.");
            question.IllustrationData = "numberword|read|" + value;
            return question;
        }

        private MathQuestion WriteNumberTo1000(MathTemplateRef template)
        {
            var value = _random.Next(0, 1001);
            var words = NumberToVietnamese(value);
            var choices = NearbyNumberCandidates(value).Take(3).Concat(new[] { value }).Distinct().Take(4).ToList();
            while (choices.Count < 4)
            {
                var candidate = _random.Next(0, 1001);
                if (!choices.Contains(candidate)) choices.Add(candidate);
            }
            var question = NewNumericQuestion(template,
                "Viết số: " + words + ".", value,
                "Tách cách đọc thành hàng trăm, hàng chục và hàng đơn vị.",
                "Ghép các chữ số đúng theo từng hàng rồi viết số.");
            question.Choices = Shuffle(choices);
            question.IllustrationData = "numberword|write";
            return question;
        }

        private IList<int> NearbyNumberCandidates(int value)
        {
            var values = new List<int>();
            foreach (var candidate in new[]
            {
                value + 1, value - 1, value + 10, value - 10, value + 100, value - 100,
                (value / 100) * 100 + (value % 10) * 10 + ((value / 10) % 10)
            })
            {
                if (candidate >= 0 && candidate <= 1000 && candidate != value && !values.Contains(candidate)) values.Add(candidate);
            }
            for (var candidate = 0; values.Count < 6 && candidate <= 1000; candidate += 7)
                if (candidate != value && !values.Contains(candidate)) values.Add(candidate);
            return values;
        }

        private static string NumberToVietnamese(int value)
        {
            if (value < 0 || value > 1000) throw new ArgumentOutOfRangeException("value");
            if (value == 1000) return "một nghìn";
            if (value == 0) return "không";
            var units = new[] { "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
            var hundreds = value / 100;
            var remainder = value % 100;
            if (hundreds == 0) return TwoDigitVietnamese(remainder, units);
            var text = units[hundreds] + " trăm";
            if (remainder == 0) return text;
            if (remainder < 10) return text + " linh " + units[remainder];
            return text + " " + TwoDigitVietnamese(remainder, units);
        }

        private static string TwoDigitVietnamese(int value, string[] units)
        {
            if (value < 10) return units[value];
            var tens = value / 10;
            var ones = value % 10;
            if (tens == 1)
            {
                if (ones == 0) return "mười";
                if (ones == 5) return "mười lăm";
                return "mười " + units[ones];
            }
            var text = units[tens] + " mươi";
            if (ones == 0) return text;
            if (ones == 1) return text + " mốt";
            if (ones == 4) return text + " tư";
            if (ones == 5) return text + " lăm";
            return text + " " + units[ones];
        }

        private MathQuestion MeasureWithRulerCm(MathTemplateRef template)
        {
            var start = _random.Next(0, 6);
            var length = _random.Next(1, 11);
            var end = start + length;
            var question = NewTextQuestion(template,
                "Quan sát thước. Đoạn AB dài bao nhiêu xăng-ti-mét?", length + " cm",
                UnitChoices(length, 1, 15, " cm"),
                "Đọc vị trí hai đầu A và B trên thước.",
                "Độ dài bằng số ở đầu B trừ số ở đầu A.");
            question.IllustrationData = "rulercm|" + start + "|" + end;
            return question;
        }

        private MathQuestion MeasureWithCommonScale(MathTemplateRef template)
        {
            var stepOptions = new[] { 1, 2, 5 };
            var step = stepOptions[_random.Next(0, stepOptions.Length)];
            var tickCount = _random.Next(5, 11);
            var max = step * tickCount;
            var tick = _random.Next(1, tickCount);
            var value = tick * step;
            var values = new HashSet<int> { value };
            foreach (var delta in new[] { -step, step, -2 * step, 2 * step })
            {
                var candidate = value + delta;
                if (candidate >= 0 && candidate <= max) values.Add(candidate);
                if (values.Count >= 4) break;
            }
            for (var candidate = 0; values.Count < 4 && candidate <= max; candidate += step) values.Add(candidate);
            var question = NewNumericQuestion(template,
                "Quan sát thang đo. Mũi tên đang chỉ giá trị nào?", value,
                "Tìm giá trị tăng thêm giữa hai vạch liền nhau.",
                "Mỗi vạch tăng " + step + " đơn vị.");
            question.Choices = Shuffle(values.Take(4).ToList());
            question.IllustrationData = "commonscale|0|" + max + "|" + step + "|" + value;
            return question;
        }

        private MathQuestion MeasurementConvertCalculate(MathTemplateRef template)
        {
            var mode = _random.Next(0, 5);
            MathQuestion question;
            if (mode == 0)
            {
                var meters = _random.Next(1, 10);
                var dm = meters * 10;
                question = NewTextQuestion(template, meters + " m bằng bao nhiêu dm?", dm + " dm",
                    UnitChoices(dm, 10, 100, " dm"), "Mỗi mét có 10 đề-xi-mét.", meters + " × 10 = " + dm + ".");
                question.IllustrationData = "unitcalc|convert_m_dm|" + meters + "|" + dm;
            }
            else if (mode == 1)
            {
                question = NewTextQuestion(template, "1 km bằng bao nhiêu m?", "1000 m",
                    new[] { "1000 m", "100 m", "10 m", "1 m" }, "Nhớ quan hệ giữa ki-lô-mét và mét.", "1 km = 1000 m.");
                question.IllustrationData = "unitcalc|convert_km_m|1|1000";
            }
            else if (mode == 2)
            {
                var a = _random.Next(1, 11); var b = _random.Next(1, 11); var result = a + b;
                question = NewTextQuestion(template, a + " kg + " + b + " kg = ?", result + " kg",
                    UnitChoices(result, 2, 20, " kg"), "Hai số đo cùng đơn vị nên cộng các số.", a + " + " + b + " = " + result + ".");
                question.IllustrationData = "unitcalc|add|" + a + "|" + b + "|kg";
            }
            else if (mode == 3)
            {
                var a = _random.Next(5, 21); var b = _random.Next(1, a); var result = a - b;
                question = NewTextQuestion(template, a + " lít - " + b + " lít = ?", result + " lít",
                    UnitChoices(result, 1, 20, " lít"), "Hai số đo cùng đơn vị nên trừ các số.", a + " - " + b + " = " + result + ".");
                question.IllustrationData = "unitcalc|sub|" + a + "|" + b + "|lít";
            }
            else
            {
                var a = _random.Next(1, 51); var b = _random.Next(1, 51); var result = a + b;
                question = NewTextQuestion(template, a + " m + " + b + " m = ?", result + " m",
                    UnitChoices(result, 2, 100, " m"), "Hai số đo cùng đơn vị nên cộng các số.", a + " + " + b + " = " + result + ".");
                question.IllustrationData = "unitcalc|add|" + a + "|" + b + "|m";
            }
            return question;
        }

        private MathQuestion MeasurementRealWorldOneStep(MathTemplateRef template)
        {
            var unitOptions = new[] { "kg", "lít", "m" };
            var unit = unitOptions[_random.Next(0, unitOptions.Length)];
            var add = _random.Next(0, 2) == 0;
            var a = add ? _random.Next(2, 41) : _random.Next(10, 61);
            var b = add ? _random.Next(1, 21) : _random.Next(1, a);
            var result = add ? a + b : a - b;
            string prompt;
            if (unit == "kg")
                prompt = add ? "Một bao có " + a + " kg gạo, thêm " + b + " kg. Có tất cả bao nhiêu ki-lô-gam gạo?"
                             : "Một bao có " + a + " kg gạo, lấy ra " + b + " kg. Còn lại bao nhiêu ki-lô-gam gạo?";
            else if (unit == "lít")
                prompt = add ? "Bình có " + a + " lít nước, thêm " + b + " lít. Có tất cả bao nhiêu lít nước?"
                             : "Bình có " + a + " lít nước, dùng " + b + " lít. Còn lại bao nhiêu lít nước?";
            else
                prompt = add ? "Đoạn dây dài " + a + " m, nối thêm " + b + " m. Đoạn dây mới dài bao nhiêu mét?"
                             : "Đoạn dây dài " + a + " m, cắt đi " + b + " m. Còn lại bao nhiêu mét?";
            var suffix = " " + unit;
            var question = NewTextQuestion(template, prompt, result + suffix,
                UnitChoices(result, 1, 100, suffix),
                "Xác định số đo ban đầu, phần thêm vào hoặc bớt đi.",
                "Phép tính phù hợp là " + a + (add ? " + " : " - ") + b + ".");
            question.IllustrationData = "wordbar|" + (add ? "add" : "sub") + "|" + a + "|" + b;
            return question;
        }

        private MathQuestion DataCollectClassifyCount(MathTemplateRef template)
        {
            var circle = _random.Next(1, 6);
            var square = _random.Next(1, 6);
            var triangle = _random.Next(1, 6);
            var targets = new[] { "circle", "square", "triangle" };
            var target = targets[_random.Next(0, targets.Length)];
            var answer = target == "circle" ? circle : target == "square" ? square : triangle;
            var label = target == "circle" ? "hình tròn" : target == "square" ? "hình vuông" : "hình tam giác";
            var question = NewNumericQuestion(template,
                "Quan sát dữ liệu. Có bao nhiêu " + label + "?", answer,
                "Phân loại các hình cùng loại rồi đếm riêng nhóm cần tìm.",
                "Chỉ đếm " + label + ", không cộng các nhóm khác.");
            question.IllustrationData = "classify|circle=" + circle + "|square=" + square + "|triangle=" + triangle + "|target=" + target;
            return question;
        }

        private MathQuestion HeavierLighterBalance(MathTemplateRef template)
        {
            var left = _random.Next(1, 10);
            var right = _random.Next(1, 10);
            while (right == left) right = _random.Next(1, 10);
            var askHeavier = _random.Next(0, 2) == 0;
            var leftIsAnswer = askHeavier ? left > right : left < right;
            var answer = leftIsAnswer ? "bên trái" : "bên phải";
            var question = NewTextQuestion(template,
                "Quan sát cân. " + (askHeavier ? "Bên nào có vật nặng hơn?" : "Bên nào có vật nhẹ hơn?"),
                answer, new[] { "bên trái", "bên phải" },
                "Bên nặng hơn làm đĩa cân hạ thấp hơn.",
                askHeavier ? "Chọn phía có đĩa cân thấp hơn." : "Chọn phía có đĩa cân cao hơn.");
            question.IllustrationData = "balance|" + left + "|" + right + "|" + (askHeavier ? "heavier" : "lighter");
            return question;
        }

        private MathQuestion MassKgReadWrite(MathTemplateRef template)
        {
            var kg = _random.Next(1, 21);
            var options = UnitChoices(kg, 1, 20, " kg");
            var question = NewTextQuestion(template,
                "Quan sát cân và chọn số đo khối lượng đúng.", kg + " kg", options,
                "Đọc số trên mặt cân rồi nhớ đơn vị ki-lô-gam.",
                "Kí hiệu của ki-lô-gam là kg.");
            question.IllustrationData = "masskg|" + kg;
            return question;
        }

        private MathQuestion CapacityLiterReadWrite(MathTemplateRef template)
        {
            var liters = _random.Next(1, 11);
            var options = UnitChoices(liters, 1, 10, " lít");
            var question = NewTextQuestion(template,
                "Quan sát bình đo và chọn dung tích đúng.", liters + " lít", options,
                "Đọc vạch mà mực nước đang chạm tới.",
                "Đơn vị đang dùng là lít.");
            question.IllustrationData = "liter|" + liters;
            return question;
        }

        private MathQuestion LengthUnitRelation(MathTemplateRef template)
        {
            var mode = _random.Next(0, 4);
            string prompt, answer, data;
            IList<string> choices;
            if (mode == 0)
            {
                prompt = "1 m bằng bao nhiêu dm?"; answer = "10 dm";
                choices = new[] { "10 dm", "100 dm", "1 dm", "1000 dm" };
                data = "unitrelation|1|m|10|dm";
            }
            else if (mode == 1)
            {
                prompt = "10 dm bằng bao nhiêu m?"; answer = "1 m";
                choices = new[] { "1 m", "10 m", "100 m", "1000 m" };
                data = "unitrelation|10|dm|1|m";
            }
            else if (mode == 2)
            {
                prompt = "1 km bằng bao nhiêu m?"; answer = "1000 m";
                choices = new[] { "1000 m", "100 m", "10 m", "1 m" };
                data = "unitrelation|1|km|1000|m";
            }
            else
            {
                prompt = "1000 m bằng bao nhiêu km?"; answer = "1 km";
                choices = new[] { "1 km", "10 km", "100 km", "1000 km" };
                data = "unitrelation|1000|m|1|km";
            }
            var question = NewTextQuestion(template, prompt, answer, Shuffle(choices.ToList()),
                "Nhìn hai đơn vị và nhớ quan hệ đã học.",
                mode < 2 ? "1 m = 10 dm." : "1 km = 1000 m.");
            question.IllustrationData = data;
            return question;
        }

        private MathQuestion TimeDay24Hours(MathTemplateRef template)
        {
            var question = NewTextQuestion(template,
                "1 ngày có bao nhiêu giờ?", "24 giờ",
                Shuffle(new List<string> { "24 giờ", "12 giờ", "60 giờ", "30 giờ" }),
                "Một ngày gồm cả ban ngày và ban đêm.",
                "1 ngày = 24 giờ.");
            question.IllustrationData = "timerelation|1|ngày|24|giờ";
            return question;
        }

        private MathQuestion TimeHour60Minutes(MathTemplateRef template)
        {
            var question = NewTextQuestion(template,
                "1 giờ có bao nhiêu phút?", "60 phút",
                Shuffle(new List<string> { "60 phút", "30 phút", "24 phút", "100 phút" }),
                "Hãy nghĩ đến một vòng đầy đủ của kim phút.",
                "1 giờ = 60 phút.");
            question.IllustrationData = "timerelation|1|giờ|60|phút";
            return question;
        }

        private MathQuestion CalendarDaysInMonthDate(MathTemplateRef template)
        {
            var months30 = new[] { 4, 6, 9, 11 };
            var months31 = new[] { 1, 3, 5, 7, 8, 10, 12 };
            var allMonths = months30.Concat(months31).ToArray();
            var month = allMonths[_random.Next(0, allMonths.Length)];
            var days = months30.Contains(month) ? 30 : 31;
            var highlighted = _random.Next(1, days + 1);
            var askDate = _random.Next(0, 2) == 0;
            MathQuestion question;
            if (askDate)
            {
                var answer = "ngày " + highlighted + " tháng " + month;
                var d2 = highlighted == days ? highlighted - 1 : highlighted + 1;
                var d3 = highlighted <= 2 ? highlighted + 2 : highlighted - 2;
                var otherMonth = month == 12 ? 11 : month + 1;
                question = NewTextQuestion(template,
                    "Quan sát lịch. Ngày được đánh dấu là ngày nào?", answer,
                    Shuffle(new List<string>
                    {
                        answer,
                        "ngày " + d2 + " tháng " + month,
                        "ngày " + d3 + " tháng " + month,
                        "ngày " + highlighted + " tháng " + otherMonth
                    }),
                    "Đọc số trong ô được đánh dấu rồi đọc tên tháng.",
                    "Ô được đánh dấu nằm trong lịch tháng " + month + ".");
            }
            else
            {
                var answer = days + " ngày";
                question = NewTextQuestion(template,
                    "Quan sát lịch tháng " + month + ". Tháng này có bao nhiêu ngày?", answer,
                    Shuffle(new List<string> { "30 ngày", "31 ngày", "28 ngày", "29 ngày" }),
                    "Nhìn số ngày cuối cùng xuất hiện trong tờ lịch.",
                    "Tháng " + month + " kết thúc ở ngày " + days + ".");
            }
            question.IllustrationData = "calendar|" + month + "|" + days + "|" + highlighted + "|" + (askDate ? "date" : "days");
            return question;
        }

        private IList<string> UnitChoices(int correct, int min, int max, string suffix)
        {
            var values = new HashSet<int> { correct };
            foreach (var delta in new[] { -1, 1, -2, 2, -5, 5 })
            {
                var value = correct + delta;
                if (value >= min && value <= max) values.Add(value);
                if (values.Count >= 4) break;
            }
            for (var value = min; values.Count < 4 && value <= max; value++) values.Add(value);
            return Shuffle(values.Take(4).Select(x => x + suffix).ToList());
        }

        private MathQuestion FullHundredsRecognize(MathTemplateRef template)
        {
            var hundreds = _random.Next(1, 10);
            var correct = hundreds * 100;
            var choices = Enumerable.Range(1, 9).Where(x => x != hundreds).Select(x => x * 100).ToList();
            choices = Shuffle(choices).Take(3).Concat(new[] { correct }).ToList();
            var question = NewNumericQuestion(template,
                "Quan sát mô hình trăm. Số nào được biểu diễn?", correct,
                "Mỗi ô lớn biểu diễn một trăm.",
                "Đếm số ô trăm rồi nhân với 100.");
            question.Choices = Shuffle(choices);
            question.IllustrationData = "hundreds|" + hundreds.ToString(CultureInfo.InvariantCulture);
            return question;
        }

        private MathQuestion NumberRayFill(MathTemplateRef template)
        {
            var step = _random.Next(0, 2) == 0 ? 10 : 100;
            var maxStartIndex = (1000 - 4 * step) / step;
            var start = _random.Next(0, maxStartIndex + 1) * step;
            var missingIndex = _random.Next(1, 4);
            var correct = start + missingIndex * step;
            var choices = new HashSet<int> { correct };
            foreach (var delta in new[] { -step, step, -2 * step, 2 * step, -10, 10, -100, 100 })
            {
                var candidate = correct + delta;
                if (candidate >= 0 && candidate <= 1000) choices.Add(candidate);
                if (choices.Count >= 4) break;
            }
            var question = NewNumericQuestion(template,
                "Điền số còn thiếu trên trục số.", correct,
                "Nhìn khoảng cách đều giữa các mốc để tìm bước nhảy.",
                "Các mốc tăng đều " + step + " đơn vị mỗi bước.");
            question.Choices = Shuffle(choices.Take(4).ToList());
            question.IllustrationData = "numberlinefill|" + start + "|" + step + "|" + missingIndex + "|5";
            return question;
        }

        private MathQuestion MinMaxUpToFour(MathTemplateRef template)
        {
            var values = DistinctNumbers(4, 0, 1001);
            var askMax = _random.Next(0, 2) == 0;
            var correct = askMax ? values.Max() : values.Min();
            var question = NewNumericQuestion(template,
                "Trong các số " + string.Join(", ", values) + ", số " + (askMax ? "lớn nhất" : "bé nhất") + " là số nào?", correct,
                "So sánh từ hàng trăm, rồi hàng chục, rồi hàng đơn vị.",
                "Hãy tìm số đứng xa nhất về phía " + (askMax ? "lớn" : "bé") + " khi so sánh từng hàng.");
            question.Choices = Shuffle(values.ToList());
            question.IllustrationData = "numbercards|" + string.Join("|", values) + "|" + (askMax ? "max" : "min");
            return question;
        }

        private MathQuestion SortUpToFour(MathTemplateRef template)
        {
            var values = DistinctNumbers(4, 0, 1001);
            var ascending = _random.Next(0, 2) == 0;
            var sorted = ascending ? values.OrderBy(x => x).ToArray() : values.OrderByDescending(x => x).ToArray();
            var separator = ascending ? " < " : " > ";
            var correct = string.Join(separator, sorted);
            var options = new List<string> { correct };
            AddUnique(options, string.Join(separator, sorted.Reverse()));
            var swapMiddle = sorted.ToArray();
            var temp = swapMiddle[1]; swapMiddle[1] = swapMiddle[2]; swapMiddle[2] = temp;
            AddUnique(options, string.Join(separator, swapMiddle));
            var rotate = new[] { sorted[1], sorted[2], sorted[3], sorted[0] };
            AddUnique(options, string.Join(separator, rotate));
            while (options.Count < 4)
            {
                var shuffled = Shuffle(values.ToList()).ToArray();
                AddUnique(options, string.Join(separator, shuffled));
            }
            var question = NewTextQuestion(template,
                "Sắp xếp các số " + string.Join(", ", values) + " theo thứ tự " + (ascending ? "từ bé đến lớn" : "từ lớn đến bé") + ".",
                correct, Shuffle(options.Take(4).ToList()),
                "So sánh từng số từ hàng lớn nhất.",
                "Đặt số " + (ascending ? "bé nhất trước rồi tăng dần" : "lớn nhất trước rồi giảm dần") + ".");
            question.IllustrationData = "numbercards|" + string.Join("|", values) + "|" + (ascending ? "asc" : "desc");
            return question;
        }

        private MathQuestion TwoOperatorsLeftToRight(MathTemplateRef template)
        {
            for (var attempt = 0; attempt < 500; attempt++)
            {
                var a = _random.Next(0, 501);
                var b = _random.Next(0, 301);
                var c = _random.Next(0, 301);
                var op1 = _random.Next(0, 2) == 0 ? "+" : "-";
                var op2 = _random.Next(0, 2) == 0 ? "+" : "-";
                var middle = op1 == "+" ? a + b : a - b;
                if (middle < 0 || middle > 1000) continue;
                var final = op2 == "+" ? middle + c : middle - c;
                if (final < 0 || final > 1000) continue;
                var question = NewNumericQuestion(template,
                    "Tính từ trái sang phải: " + a + " " + op1 + " " + b + " " + op2 + " " + c + " = ?", final,
                    "Làm phép tính thứ nhất trước, rồi dùng kết quả để làm phép tính thứ hai.",
                    "Bước 1: " + a + " " + op1 + " " + b + " = " + middle + ". Sau đó tính " + middle + " " + op2 + " " + c + ".");
                question.IllustrationData = "twostep|" + a + "|" + op1 + "|" + b + "|" + op2 + "|" + c + "|" + middle;
                return question;
            }
            throw new InvalidOperationException("Could not generate grade-2 two-operator expression.");
        }

        private MathQuestion MentalRoundTensHundreds(MathTemplateRef template)
        {
            for (var attempt = 0; attempt < 500; attempt++)
            {
                var unit = _random.Next(0, 2) == 0 ? 10 : 100;
                var maxIndex = 1000 / unit;
                var a = _random.Next(0, maxIndex + 1) * unit;
                var b = _random.Next(1, maxIndex + 1) * unit;
                var add = _random.Next(0, 2) == 0;
                var answer = add ? a + b : a - b;
                if (answer < 0 || answer > 1000) continue;
                var op = add ? "+" : "-";
                var question = NewNumericQuestion(template,
                    "Tính nhẩm: " + a + " " + op + " " + b + " = ?", answer,
                    "Xem các số là những nhóm " + unit + " đơn vị.",
                    "Bỏ cùng " + (unit == 10 ? "một chữ số 0" : "hai chữ số 0") + ", tính phần còn lại rồi ghép số 0 trở lại.");
                question.IllustrationData = "roundchunks|" + a + "|" + op + "|" + b + "|" + unit;
                return question;
            }
            throw new InvalidOperationException("Could not generate rounded mental arithmetic item.");
        }

        private int[] DistinctNumbers(int count, int minInclusive, int maxExclusive)
        {
            var values = new HashSet<int>();
            while (values.Count < count) values.Add(_random.Next(minInclusive, maxExclusive));
            return values.ToArray();
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
            if (templateId == "add_components_recognize" || templateId == "sub_components_recognize" ||
                templateId == "multiplication_components_recognize" || templateId == "division_components_recognize")
                return "equation_components";
            if (templateId == "multiplication_meaning_groups" || templateId == "division_meaning_share" ||
                templateId == "operation_meaning_from_visual")
                return "operation_model";
            if (!string.IsNullOrWhiteSpace(templateId) && templateId.StartsWith("word_problem_", StringComparison.Ordinal))
                return "word_problem_model";
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
                case "full_hundreds_recognize":
                    return "hundreds_blocks";
                case "number_ray_fill_1000":
                    return "number_line_fill";
                case "min_max_up_to_4":
                case "sort_up_to_4":
                    return "number_cards";
                case "add_sub_two_operators_left_to_right":
                    return "two_step_strip";
                case "mental_round_tens_hundreds_1000":
                    return "round_number_chunks";
                case "heavier_lighter_balance":
                    return "balance_scale";
                case "mass_kg_read_write":
                    return "mass_kg_scale";
                case "capacity_liter_read_write":
                    return "liter_measure";
                case "length_dm_m_km_relation":
                    return "unit_relation";
                case "time_day_24_hours":
                case "time_hour_60_minutes":
                    return "time_relation";
                case "calendar_days_in_month_date":
                    return "calendar";
                case "measure_with_ruler_cm":
                    return "ruler_cm";
                case "measure_with_common_scale":
                    return "common_scale";
                case "measurement_convert_calculate_learned_units":
                    return "measurement_calc";
                case "measurement_real_world_one_step":
                    return "measurement_word_model";
                case "data_collect_classify_count":
                    return "classify_count";
                case "count_place_value_to_1000":
                    return "base10_count";
                case "read_number_to_1000":
                case "write_number_to_1000":
                    return "number_word_card";
                case "estimate_objects_by_tens":
                    return "estimate_dots";
                case "measurement_estimate_reference_10cm":
                    return "estimate_length";
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
            if (string.Equals(question.AnswerKind, "interaction_integer", StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(question.CorrectAnswerText))
                    question.CorrectAnswerText = question.CorrectAnswer.ToString(CultureInfo.InvariantCulture);
                question.Choices = new int[0];
                question.ChoiceTexts = new string[0];
                return;
            }

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
            if (question.Choices == null || question.Choices.Count < 2)
                question.Choices = BuildChoices(question.CorrectAnswer);
            else
            {
                var explicitChoices = question.Choices.Distinct().ToList();
                if (!explicitChoices.Contains(question.CorrectAnswer)) explicitChoices.Add(question.CorrectAnswer);
                if (explicitChoices.Count < 2 || explicitChoices.Count > 4)
                    throw new InvalidOperationException("Explicit numeric choices must contain 2 to 4 unique values.");
                question.Choices = explicitChoices;
            }
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
