using System;
using System.Collections.Generic;
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
            switch (decision.Template.TemplateId)
            {
                case "mental_add_within_20": question = MentalAdd(decision.Template); break;
                case "mental_sub_within_20": question = MentalSub(decision.Template); break;
                case "times_table_2": question = Times(decision.Template, 2); break;
                case "times_table_5": question = Times(decision.Template, 5); break;
                case "add_within_1000_no_carry": question = AddNoCarry(decision.Template); break;
                case "add_within_1000_one_carry": question = AddOneCarry(decision.Template); break;
                case "subtract_within_1000_no_borrow": question = SubNoBorrow(decision.Template); break;
                case "subtract_within_1000_one_borrow": question = SubOneBorrow(decision.Template); break;
                default: throw new InvalidOperationException("Unsupported VERIFIED math template: " + decision.Template.TemplateId);
            }
            question.QuestionId = decision.Template.TemplateId + "-" + Guid.NewGuid().ToString("N");
            question.TemplateId = decision.Template.TemplateId;
            question.SkillId = decision.Template.SkillId;
            question.Representation = "symbolic";
            question.DifficultyFit = decision.DifficultyFit;
            question.Choices = BuildChoices(question.CorrectAnswer);
            return question;
        }

        private MathQuestion MentalAdd(MathTemplateRef template)
        {
            var a = _random.Next(2, 16);
            var b = _random.Next(1, 21 - a);
            return NewQuestion(template, "Tính nhẩm: " + a + " + " + b + " = ?", a + b,
                "Con thử bắt đầu từ số lớn hơn rồi đếm thêm " + b + ".",
                "Có thể tách " + b + " thành phần giúp tròn 10 trước, rồi cộng phần còn lại.");
        }

        private MathQuestion MentalSub(MathTemplateRef template)
        {
            var a = _random.Next(5, 21);
            var b = _random.Next(1, a + 1);
            return NewQuestion(template, "Tính nhẩm: " + a + " - " + b + " = ?", a - b,
                "Con thử lùi " + b + " bước từ " + a + ".",
                "Tách số trừ thành phần dễ bớt trước, rồi bớt phần còn lại.");
        }

        private MathQuestion Times(MathTemplateRef template, int factor)
        {
            var n = _random.Next(1, 11);
            return NewQuestion(template, "Tính: " + factor + " × " + n + " = ?", factor * n,
                "Hãy nghĩ thành " + n + " nhóm, mỗi nhóm có " + factor + ".",
                "Có thể cộng " + factor + " lặp lại " + n + " lần.");
        }

        private MathQuestion AddNoCarry(MathTemplateRef template)
        {
            int a, b;
            GenerateNoCarryPair(out a, out b);
            return NewQuestion(template, "Tính: " + a + " + " + b + " = ?", a + b,
                "Cộng lần lượt hàng đơn vị, hàng chục rồi hàng trăm.",
                "Mỗi hàng trong phép tính này đều có tổng nhỏ hơn 10 nên không cần nhớ.");
        }

        private MathQuestion AddOneCarry(MathTemplateRef template)
        {
            int a, b;
            GenerateExactlyOneCarryPair(out a, out b);
            return NewQuestion(template, "Tính: " + a + " + " + b + " = ?", a + b,
                "Tìm hàng có tổng từ 10 trở lên rồi nhớ 1 sang hàng bên trái.",
                "Phép tính này chỉ có đúng một lần nhớ. Viết phần đơn vị của tổng ở hàng đó và nhớ 1.");
        }

        private MathQuestion SubNoBorrow(MathTemplateRef template)
        {
            int b, result;
            GenerateNoCarryPair(out b, out result);
            var a = b + result;
            return NewQuestion(template, "Tính: " + a + " - " + b + " = ?", result,
                "Trừ lần lượt hàng đơn vị, chục rồi trăm.",
                "Mỗi chữ số của số bị trừ đủ lớn nên phép tính này không cần mượn.");
        }

        private MathQuestion SubOneBorrow(MathTemplateRef template)
        {
            int b, result;
            GenerateExactlyOneCarryPair(out b, out result);
            var a = b + result;
            return NewQuestion(template, "Tính: " + a + " - " + b + " = ?", result,
                "Tìm hàng chưa đủ để trừ rồi mượn 1 từ hàng bên trái.",
                "Phép tính này chỉ có đúng một lần mượn. Sau khi mượn, nhớ giảm hàng bên trái đi 1.");
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

        private static MathQuestion NewQuestion(MathTemplateRef template, string prompt, int answer, string hint1, string hint2)
        {
            return new MathQuestion
            {
                TemplateId = template.TemplateId,
                SkillId = template.SkillId,
                PromptVi = prompt,
                CorrectAnswer = answer,
                HintLevel1 = hint1,
                HintLevel2 = hint2
            };
        }

        private IList<int> BuildChoices(int correct)
        {
            var values = new HashSet<int>();
            values.Add(correct);
            var offsets = new[] { -10, 10, -1, 1, -2, 2, -5, 5, -100, 100 };
            var start = _random.Next(0, offsets.Length);
            for (var i = 0; values.Count < 4 && i < offsets.Length * 2; i++)
            {
                var candidate = correct + offsets[(start + i) % offsets.Length];
                if (candidate >= 0 && candidate <= 1000) values.Add(candidate);
            }
            var fallback = 0;
            while (values.Count < 4) { if (fallback != correct) values.Add(fallback); fallback++; }
            var list = values.ToList();
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = _random.Next(0, i + 1);
                var temp = list[i]; list[i] = list[j]; list[j] = temp;
            }
            return list;
        }
    }
}
