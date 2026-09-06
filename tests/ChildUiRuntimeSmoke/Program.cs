using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using WAHU.Data;
using WAHU.Learning;
using WAHU.Performance;

namespace WAHU.ChildUiRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var appAssembly = typeof(WAHUKidsLearn.MainForm).Assembly;

            TestInstructionVisuals(appAssembly, 1.00f);
            TestInstructionVisuals(appAssembly, 1.25f);
            TestGarden(appAssembly, 1.00f);
            TestGarden(appAssembly, 1.25f);
            TestCompanionAndCompletion(appAssembly);
            TestRoadmap(appAssembly);
            TestAnswerGridLayout(appAssembly);
            TestBasicControls(appAssembly);

            Console.WriteLine("CHILD_UI_RUNTIME_SMOKE_PASS assertions=" + _assertions);
        }

        private static void TestInstructionVisuals(Assembly appAssembly, float scale)
        {
            var cases = new[]
            {
                QT("possible_certain_impossible_die__event_possible", "Gieo một con xúc xắc chuẩn có các mặt 1,2,3,4,5,6. Xuất hiện số 3. Điều này là gì?", "có thể"),
                QT("possible_certain_impossible_die__event_certain", "Gieo một con xúc xắc chuẩn có các mặt 1,2,3,4,5,6. Xuất hiện một số từ 1 đến 6. Điều này là gì?", "chắc chắn"),
                QT("possible_certain_impossible_die__event_impossible", "Gieo một con xúc xắc chuẩn có các mặt 1,2,3,4,5,6. Xuất hiện số 8. Điều này là gì?", "không thể"),
                Q("place_value_decompose_3digit", "Số 472 gồm bao nhiêu trăm, chục và đơn vị?", 0),
                Q("expanded_form_3digit", "Viết 604 thành tổng của trăm, chục và đơn vị.", 0),
                Q("predecessor_successor", "Số liền trước và số liền sau của 472 là gì?", 0),
                Q("compare_two_numbers_1000", "Điền dấu > hoặc < : 472 __ 468", 0),
                Q("mental_add_within_20", "Tính nhẩm: 8 + 7 = ?", 15),
                Q("mental_sub_within_20", "Tính nhẩm: 17 - 6 = ?", 11),
                Q("times_table_2", "Tính: 2 × 6 = ?", 12),
                Q("times_table_5", "Tính: 5 × 4 = ?", 20),
                Q("divide_table_2_exact", "Tính: 16 : 2 = ?", 8),
                Q("divide_table_5_exact", "Tính: 35 : 5 = ?", 7),
                Q("add_within_1000_no_carry", "Tính: 243 + 315 = ?", 558),
                Q("add_within_1000_one_carry", "Tính: 247 + 135 = ?", 382),
                Q("subtract_within_1000_no_borrow", "Tính: 786 - 243 = ?", 543),
                Q("subtract_within_1000_one_borrow", "Tính: 432 - 157 = ?", 275),
                Q("polyline_length", "Đường gấp khúc có ba đoạn dài 8 cm, 11 cm và 6 cm. Độ dài đường gấp khúc là bao nhiêu?", 25)
            };

            foreach (var question in cases)
            {
                using (var control = CreateInternalControl(appAssembly, "WAHUKidsLearn.MathInstructionVisual"))
                {
                    Invoke(control, "SetQuestion", question, 0);
                    RenderAndAssert(control, (int)(620 * scale), (int)(118 * scale), "instruction_" + question.TemplateId + "_scale_" + scale);
                    Invoke(control, "SetQuestion", question, 2);
                    RenderAndAssert(control, (int)(620 * scale), (int)(118 * scale), "instruction_hint2_" + question.TemplateId + "_scale_" + scale);
                }
            }
        }

        private static void TestGarden(Assembly appAssembly, float scale)
        {
            using (var control = CreateInternalControl(appAssembly, "WAHUKidsLearn.GardenWorldControl"))
            {
                Set(control, "GrowthLevel", 8);
                Set(control, "HasSeedling", true);
                Set(control, "HasFlowerPatch", true);
                Set(control, "HasLantern", true);
                Set(control, "HasBench", true);
                RenderAndAssert(control, (int)(640 * scale), (int)(360 * scale), "garden_full_scale_" + scale);
            }
        }

        private static void TestCompanionAndCompletion(Assembly appAssembly)
        {
            var stateType = appAssembly.GetType("WAHUKidsLearn.CompanionReactionState", true);
            foreach (var name in new[] { "Calm", "Correct", "TryAgain", "Tired", "Celebrate" })
            {
                using (var companion = CreateInternalControl(appAssembly, "WAHUKidsLearn.CompanionReactionControl"))
                {
                    Set(companion, "State", Enum.Parse(stateType, name));
                    RenderAndAssert(companion, 74, 72, "companion_" + name);
                }
            }

            foreach (var scale in new[] { 1.00f, 1.25f })
            {
                using (var completion = CreateInternalControl(appAssembly, "WAHUKidsLearn.LessonCompletionVisual"))
                {
                    Invoke(completion, "SetProgress", 3, "garden_flower_patch", 3, "garden_lantern");
                    RenderAndAssert(completion, (int)(620 * scale), (int)(100 * scale), "completion_reward_scale_" + scale);
                }
            }
        }

        private static void TestRoadmap(Assembly appAssembly)
        {
            var snapshot = new MathRoadmapSnapshot
            {
                NumberSense = new MathRoadmapGroupProgress { GroupId = "number_sense_1000", SkillRows = 3, Attempts = 9, MasteryAverage = 0.61 },
                Mental20 = new MathRoadmapGroupProgress { GroupId = "mental_20", SkillRows = 1, Attempts = 8, MasteryAverage = 0.72 },
                Written1000 = new MathRoadmapGroupProgress { GroupId = "written_1000", SkillRows = 3, Attempts = 11, MasteryAverage = 0.54 },
                Tables25 = new MathRoadmapGroupProgress { GroupId = "tables_2_5", SkillRows = 2, Attempts = 7, MasteryAverage = 0.66 },
                Measurement = new MathRoadmapGroupProgress { GroupId = "measurement_geometry", SkillRows = 1, Attempts = 3, MasteryAverage = 0.47 },
                Chance = new MathRoadmapGroupProgress { GroupId = "chance_events", SkillRows = 2, Attempts = 5, MasteryAverage = 0.58 }
            };
            foreach (var scale in new[] { 1.00f, 1.25f })
            {
                using (var roadmap = CreateInternalControl(appAssembly, "WAHUKidsLearn.MathRoadmapControl"))
                {
                    Invoke(roadmap, "SetSnapshot", snapshot);
                    RenderAndAssert(roadmap, (int)(560 * scale), (int)(132 * scale), "math_roadmap_scale_" + scale);
                }
            }
        }

        private static void TestAnswerGridLayout(Assembly appAssembly)
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "wahu-child-ui-layout-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var schema = Path.Combine(Directory.GetCurrentDirectory(), "data", "schema", "001_initial.sql");
                var database = new LearningDatabase(Path.Combine(tempRoot, "learning.db"), schema);
                using (var form = new WAHUKidsLearn.MathLessonForm(database, new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW }))
                {
                    Invoke(form, "ConfigureAnswerLayout", 2);
                    var grid = GetField<TableLayoutPanel>(form, "_answerGrid");
                    var buttons = GetField<Array>(form, "_answerButtons");
                    var b0 = (Control)buttons.GetValue(0);
                    var b1 = (Control)buttons.GetValue(1);
                    A(Math.Abs(grid.RowStyles[0].Height - 100f) < 0.01f && Math.Abs(grid.RowStyles[1].Height) < 0.01f,
                        "answer_layout_two_choices_full_height");
                    A(grid.GetCellPosition(b0).Row == 0 && grid.GetCellPosition(b1).Row == 0,
                        "answer_layout_two_choices_same_row");

                    Invoke(form, "ConfigureAnswerLayout", 3);
                    var b2 = (Control)buttons.GetValue(2);
                    A(Math.Abs(grid.RowStyles[0].Height - 50f) < 0.01f && Math.Abs(grid.RowStyles[1].Height - 50f) < 0.01f,
                        "answer_layout_three_choices_restores_two_rows");
                    A(grid.GetCellPosition(b2).Row == 1 && grid.GetColumnSpan(b2) == 2,
                        "answer_layout_three_choices_bottom_spans_columns");

                    Invoke(form, "ConfigureAnswerLayout", 4);
                    var b3 = (Control)buttons.GetValue(3);
                    A(grid.GetColumnSpan(b2) == 1 && grid.GetCellPosition(b2).Column == 0 && grid.GetCellPosition(b2).Row == 1,
                        "answer_layout_four_choices_resets_span");
                    A(grid.GetCellPosition(b3).Column == 1 && grid.GetCellPosition(b3).Row == 1,
                        "answer_layout_four_choices_restores_bottom_right");
                }
            }
            finally
            {
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }

        private static void TestBasicControls(Assembly appAssembly)
        {
            using (var button = CreateInternalControl(appAssembly, "WAHUKidsLearn.AnswerChoiceButton"))
            {
                button.Text = "73";
                Set(button, "BadgeText", "✓");
                var stateType = appAssembly.GetType("WAHUKidsLearn.AnswerChoiceButton+ChoiceVisualState", true);
                Set(button, "VisualState", Enum.Parse(stateType, "Correct"));
                button.Enabled = false;
                RenderAndAssert(button, 300, 82, "answer_correct_disabled_keeps_feedback");
            }
            using (var longText = CreateInternalControl(appAssembly, "WAHUKidsLearn.AnswerChoiceButton"))
            {
                longText.Text = "4 trăm, 7 chục, 2 đơn vị";
                longText.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold, GraphicsUnit.Point);
                Set(longText, "BadgeText", "1");
                RenderAndAssert(longText, 360, 82, "answer_long_text_choice");
            }
            using (var progress = CreateInternalControl(appAssembly, "WAHUKidsLearn.ProgressStrip"))
            {
                Set(progress, "Maximum", 8);
                Set(progress, "Value", 5);
                RenderAndAssert(progress, 500, 18, "progress_strip");
            }
        }

        private static Control CreateInternalControl(Assembly assembly, string typeName)
        {
            var type = assembly.GetType(typeName, true);
            var value = Activator.CreateInstance(type, true) as Control;
            if (value == null) throw new Exception("Could not create " + typeName);
            return value;
        }

        private static void Invoke(object target, string method, params object[] args)
        {
            var info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) throw new MissingMethodException(target.GetType().FullName, method);
            info.Invoke(target, args);
        }

        private static void Set(object target, string property, object value)
        {
            var info = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) throw new MissingMemberException(target.GetType().FullName, property);
            info.SetValue(target, value, null);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var info = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) throw new MissingFieldException(target.GetType().FullName, fieldName);
            return (T)info.GetValue(target);
        }

        private static void RenderAndAssert(Control child, int width, int height, string name)
        {
            A(width > 0 && height > 0, name + "_positive_bounds");
            using (var host = new Panel { BackColor = Color.White, Size = new Size(width, height) })
            {
                child.Dock = DockStyle.Fill;
                host.Controls.Add(child);
                host.CreateControl();
                child.CreateControl();
                using (var bitmap = new Bitmap(width, height))
                {
                    host.DrawToBitmap(bitmap, new Rectangle(0, 0, width, height));
                    var colors = new HashSet<int>();
                    var stepX = Math.Max(1, width / 24);
                    var stepY = Math.Max(1, height / 14);
                    for (var y = 0; y < height; y += stepY)
                        for (var x = 0; x < width; x += stepX)
                            colors.Add(bitmap.GetPixel(x, y).ToArgb());
                    A(colors.Count >= 3, name + "_renders_nonempty_visual");
                }
                host.Controls.Remove(child);
            }
        }

        private static MathQuestion QT(string template, string prompt, string answer)
        {
            return new MathQuestion
            {
                TemplateId = template,
                SkillId = template,
                PromptVi = prompt,
                AnswerKind = "text",
                CorrectAnswerText = answer,
                ChoiceTexts = new[] { "có thể", "chắc chắn", "không thể" },
                Representation = "die_outcomes",
                HintLevel1 = "hint 1",
                HintLevel2 = "hint 2"
            };
        }

        private static MathQuestion Q(string template, string prompt, int answer)
        {
            return new MathQuestion
            {
                TemplateId = template,
                SkillId = template,
                PromptVi = prompt,
                CorrectAnswer = answer,
                Choices = new[] { answer, answer + 1, Math.Max(0, answer - 1), answer + 10 },
                Representation = "symbolic",
                HintLevel1 = "hint 1",
                HintLevel2 = "hint 2"
            };
        }

        private static void A(bool ok, string name)
        {
            if (!ok) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }
    }
}
