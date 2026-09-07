using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WAHU.Content;
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
            TestMathCatalogAndHub(appAssembly);
            TestResumePresentation(appAssembly);
            TestCompletionPresentation(appAssembly);
            TestAnswerGridLayout(appAssembly);
            TestTypedAnswerInput(appAssembly);
            TestAllAuthoredAnswerSurfaces(appAssembly);
            TestTargetedLessonUiFlow(appAssembly);
            TestChoiceRetryUiFlow(appAssembly);
            TestSubmitFailureRecoveryUiFlow(appAssembly);
            TestTypedAndInteractionSubmitFailureRecoveryUiFlow(appAssembly);
            TestRetryResumeUiFlow(appAssembly);
            TestInteractionRetryUiFlow(appAssembly);
            TestInteractiveSegmentAnswer(appAssembly);
            TestBasicControls(appAssembly);

            Console.WriteLine("CHILD_UI_RUNTIME_SMOKE_PASS assertions=" + _assertions);
        }

        private static void TestInstructionVisuals(Assembly appAssembly, float scale)
        {
            var cases = new[]
            {
                QV("estimate_objects_by_tens", "ESTIMATE_OBJECTS_BY_TENS", "Không đếm từng chấm. Nhóm chấm này gần với bao nhiêu chục nhất?", "40", "estimatedots|37|40", "estimate_dots"),
                QV("measurement_estimate_reference_10cm", "MEASUREMENT_ESTIMATE_BASIC", "Thanh mẫu dài 10 cm. Đoạn màu cam dài gần bao nhiêu xăng-ti-mét?", "30 cm", "estimatelength|10|30|5", "estimate_length"),
                QV("count_place_value_to_1000", "NUM_COUNT_READ_WRITE_0_1000", "Quan sát mô hình khối trăm, thanh chục và ô đơn vị.", "472", "base10count|4|7|2", "base10_count"),
                QV("read_number_to_1000", "NUM_COUNT_READ_WRITE_0_1000", "Số 472 đọc là:", "bốn trăm bảy mươi hai", "numberword|read|472", "number_word_card"),
                QV("write_number_to_1000", "NUM_COUNT_READ_WRITE_0_1000", "Viết số: bốn trăm bảy mươi hai.", "472", "numberword|write", "number_word_card"),
                QV("measure_with_ruler_cm", "MEASURE_WITH_RULER_CM", "Quan sát thước. Đoạn AB dài bao nhiêu xăng-ti-mét?", "7 cm", "rulercm|2|9", "ruler_cm"),
                QV("measure_with_common_scale", "MEASURE_WITH_COMMON_SCALE", "Quan sát thang đo. Mũi tên đang chỉ giá trị nào?", "12", "commonscale|0|20|2|12", "common_scale"),
                QV("measurement_convert_calculate_learned_units", "MEASUREMENT_CONVERT_CALCULATE_LEARNED_UNITS", "3 m bằng bao nhiêu dm?", "30 dm", "unitcalc|convert_m_dm|3|30", "measurement_calc"),
                QV("measurement_real_world_one_step", "MEASUREMENT_REAL_WORLD_ONE_STEP", "Đoạn dây dài 12 m, nối thêm 5 m. Đoạn dây mới dài bao nhiêu mét?", "17 m", "wordbar|add|12|5", "measurement_word_model"),
                QV("data_collect_classify_count", "DATA_COLLECT_CLASSIFY_COUNT", "Quan sát dữ liệu. Có bao nhiêu hình tam giác?", "4", "classify|circle=3|square=2|triangle=4|target=triangle", "classify_count"),
                QV("heavier_lighter_balance", "HEAVIER_LIGHTER", "Quan sát cân. Bên nào có vật nặng hơn?", "bên trái", "balance|8|3|heavier", "balance_scale"),
                QV("mass_kg_read_write", "MASS_KG_READ_WRITE", "Quan sát cân và chọn số đo khối lượng đúng.", "7 kg", "masskg|7", "mass_kg_scale"),
                QV("capacity_liter_read_write", "CAPACITY_LITER_READ_WRITE", "Quan sát bình đo và chọn dung tích đúng.", "6 lít", "liter|6", "liter_measure"),
                QV("length_dm_m_km_relation", "LENGTH_DM_M_KM_RECOGNIZE_RELATION", "1 m bằng bao nhiêu dm?", "10 dm", "unitrelation|1|m|10|dm", "unit_relation"),
                QV("time_day_24_hours", "TIME_DAY_24_HOURS", "1 ngày có bao nhiêu giờ?", "24 giờ", "timerelation|1|ngày|24|giờ", "time_relation"),
                QV("time_hour_60_minutes", "TIME_HOUR_60_MINUTES", "1 giờ có bao nhiêu phút?", "60 phút", "timerelation|1|giờ|60|phút", "time_relation"),
                QV("calendar_days_in_month_date", "CALENDAR_DAYS_IN_MONTH_DATE", "Quan sát lịch. Ngày được đánh dấu là ngày nào?", "ngày 15 tháng 4", "calendar|4|30|15|date", "calendar"),
                QV("full_hundreds_recognize", "NUM_FULL_HUNDREDS_RECOGNIZE", "Quan sát mô hình trăm.", "500", "hundreds|5", "hundreds_blocks"),
                QV("number_ray_fill_1000", "NUMBER_RAY_FILL", "Điền số còn thiếu trên trục số.", "340", "numberlinefill|300|10|4|5", "number_line_fill"),
                QV("min_max_up_to_4", "NUM_MIN_MAX_UP_TO_4", "Trong các số 472, 810, 305, 699, số lớn nhất là số nào?", "810", "numbercards|472|810|305|699|max", "number_cards"),
                QV("sort_up_to_4", "NUM_SORT_UP_TO_4", "Sắp xếp 472, 810, 305, 699 từ bé đến lớn.", "305 < 472 < 699 < 810", "numbercards|472|810|305|699|asc", "number_cards"),
                QV("add_sub_two_operators_left_to_right", "ADD_SUB_TWO_OPERATORS_LEFT_TO_RIGHT", "Tính từ trái sang phải: 120 + 80 - 50 = ?", "150", "twostep|120|+|80|-|50|200", "two_step_strip"),
                QV("mental_round_tens_hundreds_1000", "MENTAL_ADD_SUB_ROUND_TENS_HUNDREDS_1000", "Tính nhẩm: 340 + 50 = ?", "390", "roundchunks|340|+|50|10", "round_number_chunks"),
                QV("add_components_recognize", "ADD_COMPONENTS_RECOGNIZE", "Trong phép tính 23 + 7 = 30, số 23 được gọi là gì?", "số hạng", "equationparts|add|23|7|30|0", "equation_components"),
                QV("sub_components_recognize", "SUB_COMPONENTS_RECOGNIZE", "Trong phép tính 41 - 12 = 29, số 12 được gọi là gì?", "số trừ", "equationparts|sub|41|12|29|1", "equation_components"),
                QV("multiplication_components_recognize", "MULTIPLICATION_COMPONENTS", "Trong phép tính 5 × 4 = 20, số 20 được gọi là gì?", "tích", "equationparts|mul|5|4|20|2", "equation_components"),
                QV("division_components_recognize", "DIVISION_COMPONENTS", "Trong phép tính 20 : 5 = 4, số 5 được gọi là gì?", "số chia", "equationparts|div|20|5|4|1", "equation_components"),
                QV("multiplication_meaning_groups", "MULTIPLICATION_MEANING", "Quan sát các nhóm bằng nhau. Phép nhân nào biểu diễn đúng mô hình?", "4 × 5", "wordgroups|5|4", "operation_model"),
                QV("division_meaning_share", "DIVISION_MEANING", "Quan sát việc chia đều. Phép chia nào biểu diễn đúng mô hình?", "20 : 5", "wordshare|20|5", "operation_model"),
                QV("operation_meaning_from_visual", "OPERATION_MEANING_FROM_VISUAL", "Quan sát mô hình. Mô hình phù hợp nhất với phép tính nào?", "nhân", "wordgroups|5|4", "operation_model"),
                QV("word_problem_select_operation_one_step", "WP_SELECT_OPERATION_ONE_STEP", "Có 20 chiếc bánh chia đều cho 5 bạn. Phép tính nào phù hợp?", "chia", "wordshare|20|5", "word_problem_model"),
                QWP("word_problem_add_more", "WP_ONE_STEP_ADD_MORE", "Lan có 23 nhãn vở. Mẹ cho thêm 7 nhãn vở. Lan có tất cả bao nhiêu nhãn vở?", 30, "wordbar|add|23|7"),
                QWP("word_problem_sub_less", "WP_ONE_STEP_SUB_LESS", "Lan có 41 nhãn vở. Lan cho bạn 12 nhãn vở. Lan còn lại bao nhiêu nhãn vở?", 29, "wordbar|sub|41|12"),
                QWP("word_problem_more_than", "WP_ONE_STEP_MORE_THAN", "Mai có 18 bông hoa. Lan có nhiều hơn Mai 6 bông hoa. Lan có bao nhiêu bông hoa?", 24, "wordbar|more|18|6"),
                QWP("word_problem_less_than", "WP_ONE_STEP_LESS_THAN", "Lan có 30 bông hoa. Mai có ít hơn Lan 8 bông hoa. Mai có bao nhiêu bông hoa?", 22, "wordbar|less|30|8"),
                QWP("word_problem_multiply_groups_2_5", "WP_ONE_STEP_MULTIPLICATION_CONTEXT", "Có 4 giỏ, mỗi giỏ có 5 quả. Có tất cả bao nhiêu quả?", 20, "wordgroups|5|4"),
                QWP("word_problem_divide_groups_2_5", "WP_ONE_STEP_DIVISION_CONTEXT", "Có 20 chiếc bánh chia đều cho 5 bạn. Mỗi bạn được bao nhiêu chiếc bánh?", 4, "wordshare|20|5"),                QV("clock_read_minute_hand_3_or_6", "CLOCK_MINUTE_HAND_AT_3_OR_6", "Quan sát đồng hồ và chọn thời gian đúng.", "3 giờ 30 phút", "clock|3|30", "clock"),
                QV("geometry_identify_basic__point_recognize", "POINT_RECOGNIZE", "Quan sát hình minh họa.", "điểm", "geometry|POINT_RECOGNIZE", "geometry_basic"),
                QV("geometry_identify_basic__line_segment_recognize", "LINE_SEGMENT_RECOGNIZE", "Quan sát hình minh họa.", "đoạn thẳng", "geometry|LINE_SEGMENT_RECOGNIZE", "geometry_basic"),
                QV("geometry_identify_basic__curve_recognize", "CURVE_RECOGNIZE", "Quan sát hình minh họa.", "đường cong", "geometry|CURVE_RECOGNIZE", "geometry_basic"),
                QV("geometry_identify_basic__straight_line_recognize", "STRAIGHT_LINE_RECOGNIZE", "Quan sát hình minh họa.", "đường thẳng", "geometry|STRAIGHT_LINE_RECOGNIZE", "geometry_basic"),
                QV("geometry_identify_basic__polyline_recognize", "POLYLINE_RECOGNIZE", "Quan sát hình minh họa.", "đường gấp khúc", "geometry|POLYLINE_RECOGNIZE", "geometry_basic"),
                QV("geometry_identify_basic__three_collinear_points", "THREE_COLLINEAR_POINTS", "Quan sát hình minh họa.", "ba điểm thẳng hàng", "geometry|THREE_COLLINEAR_POINTS", "geometry_basic"),
                QV("geometry_identify_basic__quadrilateral_recognize", "QUADRILATERAL_RECOGNIZE", "Quan sát hình minh họa.", "hình tứ giác", "geometry|QUADRILATERAL_RECOGNIZE", "geometry_basic"),
                QV("geometry_identify_basic__cylinder_recognize", "CYLINDER_RECOGNIZE", "Quan sát hình minh họa.", "khối trụ", "geometry|CYLINDER_RECOGNIZE", "geometry_basic"),
                QV("geometry_identify_basic__sphere_recognize", "SPHERE_RECOGNIZE", "Quan sát hình minh họa.", "khối cầu", "geometry|SPHERE_RECOGNIZE", "geometry_basic"),
                QV("pictograph_animals_legend1__pictograph_read_describe", "PICTOGRAPH_READ_DESCRIBE", "Quan sát biểu đồ tranh. Có bao nhiêu con mèo?", "3", "pictograph|cat=3|dog=2|rabbit=4|legend=1", "pictograph"),
                QV("pictograph_animals_legend1__pictograph_read_describe__2", "PICTOGRAPH_READ_DESCRIBE", "Quan sát biểu đồ tranh. Có bao nhiêu con chó?", "2", "pictograph|cat=3|dog=2|rabbit=4|legend=1", "pictograph"),
                QV("pictograph_animals_legend1__pictograph_read_describe__3", "PICTOGRAPH_READ_DESCRIBE", "Quan sát biểu đồ tranh. Có bao nhiêu con thỏ?", "4", "pictograph|cat=3|dog=2|rabbit=4|legend=1", "pictograph"),
                QV("pictograph_animals_legend1__pictograph_simple_inference", "PICTOGRAPH_SIMPLE_INFERENCE", "Quan sát biểu đồ tranh. Loài nào có nhiều nhất?", "thỏ", "pictograph|cat=3|dog=2|rabbit=4|legend=1", "pictograph"),
                QV("pictograph_animals_legend1__pictograph_simple_inference__2", "PICTOGRAPH_SIMPLE_INFERENCE", "Quan sát biểu đồ tranh. Mèo nhiều hơn chó bao nhiêu con?", "1", "pictograph|cat=3|dog=2|rabbit=4|legend=1", "pictograph"),
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

        private static void TestMathCatalogAndHub(Assembly appAssembly)
        {
            var catalogPath = Path.Combine(Directory.GetCurrentDirectory(), "content_packs", "math_grade2_v1", "lesson_catalog_v1.json");
            var catalog = new MathLessonCatalogSource().Load(catalogPath);
            A(catalog.Grade == 2, "math_catalog_grade2");
            A(catalog.Chapters.Count == 7, "math_catalog_seven_chapters");
            A(catalog.Topics.Count == 17, "math_catalog_seventeen_topics");
            A(catalog.Lessons.Count == 67, "math_catalog_sixty_seven_lessons");
            A(catalog.FindChapter("m2_ch01_numbers") != null, "math_catalog_first_chapter_resolves");
            A(catalog.FindLesson("m2_ls_num_count_read_write_0_1000") != null, "math_catalog_first_lesson_resolves");
            var firstLesson = catalog.FindLesson("m2_ls_num_count_read_write_0_1000");
            A(firstLesson.ObjectivesVi.Count >= 2, "math_catalog_lesson_has_objectives");
            A(firstLesson.Concepts.Count > 0, "math_catalog_lesson_has_concept");
            A(firstLesson.WorkedExamples.Count > 0, "math_catalog_lesson_has_worked_example");
            A(firstLesson.PracticeSets.TotalCount == 3, "math_catalog_lesson_has_three_practice_questions");
            A(catalog.FindLessonBySkill(firstLesson.SkillId).Id == firstLesson.Id, "math_catalog_skill_maps_to_lesson");

            var tempRoot = Path.Combine(Path.GetTempPath(), "wahu-child-ui-math-hub-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var sourceSchemaDir = Path.Combine(Directory.GetCurrentDirectory(), "data", "schema");
                var schemaDir = Path.Combine(tempRoot, "schema");
                Directory.CreateDirectory(schemaDir);
                foreach (var source in Directory.GetFiles(sourceSchemaDir, "*.sql"))
                    File.Copy(source, Path.Combine(schemaDir, Path.GetFileName(source)), true);
                var schema = Path.Combine(schemaDir, "001_initial.sql");
                var database = new LearningDatabase(Path.Combine(tempRoot, "learning.db"), schema);
                database.Initialize("DELETE");
                new LearnerSessionService(database).EnsurePrimaryChild("Bé học");

                var ctor = typeof(WAHUKidsLearn.MathHubForm).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(LearningDatabase), typeof(RuntimePerformanceSettings), typeof(string) },
                    null);
                A(ctor != null, "math_hub_test_constructor_available");

                using (var form = (WAHUKidsLearn.MathHubForm)ctor.Invoke(new object[]
                {
                    database,
                    new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW },
                    catalogPath
                }))
                {
                    Invoke(form, "LoadCatalogAndProgress");
                    A(Get<int>(form, "ChapterCount") == 7, "math_hub_loads_seven_chapters");
                    A(Get<int>(form, "TopicCount") == 17, "math_hub_loads_seventeen_topics");
                    A(Get<int>(form, "LessonCount") == 67, "math_hub_loads_sixty_seven_lessons");
                    A(!string.IsNullOrWhiteSpace(Get<string>(form, "SelectedLessonId")), "math_hub_selects_first_lesson");

                    var summary = GetField<Label>(form, "_summary");
                    A(summary.Text.IndexOf("67 bài học", StringComparison.Ordinal) >= 0, "math_hub_summary_shows_lesson_count");
                    var chapterFlow = GetField<FlowLayoutPanel>(form, "_chapterFlow");
                    var lessonFlow = GetField<FlowLayoutPanel>(form, "_lessonFlow");
                    var detailFlow = GetField<FlowLayoutPanel>(form, "_detailFlow");
                    A(chapterFlow.Controls.Count == 7, "math_hub_renders_all_chapter_buttons");
                    A(lessonFlow.Controls.Count > 2, "math_hub_renders_topics_and_lessons");
                    A(ContainsControlText(detailFlow, "Mục tiêu"), "math_hub_detail_shows_objectives_section");
                    A(ContainsControlText(detailFlow, "Kiến thức cần nhớ"), "math_hub_detail_shows_concept_section");
                    A(ContainsControlText(detailFlow, "Ví dụ có lời giải"), "math_hub_detail_shows_example_section");
                    A(ContainsControlText(detailFlow, "Luyện tập"), "math_hub_detail_shows_practice_section");
                    A(ContainsControlText(detailFlow, "3 câu trong ngân hàng bài học"), "math_hub_detail_shows_practice_count");
                    var firstPractice = FindButtonContaining(detailFlow, "Luyện 3 câu bài này");
                    A(firstPractice != null && firstPractice.Enabled,
                        "math_hub_first_lesson_targeted_practice_unlocked");
                    A(firstPractice.AccessibleDescription.IndexOf(firstLesson.TitleVi, StringComparison.OrdinalIgnoreCase) >= 0,
                        "math_hub_targeted_practice_names_lesson");

                    var lockedLesson = catalog.Lessons.FirstOrDefault(x => x.PrerequisiteSkills != null && x.PrerequisiteSkills.Count > 0);
                    A(lockedLesson != null, "math_hub_catalog_has_prerequisite_lesson_for_lock_test");
                    Invoke(form, "SelectLessonInCatalog", lockedLesson);
                    var lockedPractice = FindButtonContaining(detailFlow, "Học bài trước để mở luyện tập");
                    A(lockedPractice != null && !lockedPractice.Enabled,
                        "math_hub_prerequisite_lesson_practice_locked");
                    A(ContainsControlText(detailFlow, "Mục tiêu") && ContainsControlText(detailFlow, "Kiến thức cần nhớ"),
                        "math_hub_locked_lesson_theory_remains_readable");
                    var prerequisiteLesson = catalog.FindLessonBySkill(lockedLesson.PrerequisiteSkills[0]);
                    A(prerequisiteLesson != null && lockedPractice.AccessibleDescription.IndexOf(prerequisiteLesson.TitleVi, StringComparison.OrdinalIgnoreCase) >= 0,
                        "math_hub_locked_practice_names_missing_prerequisite");
                    var accessMap = GetField<object>(form, "_lessonAccess") as System.Collections.IDictionary;
                    A(accessMap != null && accessMap.Count == catalog.Lessons.Count,
                        "math_hub_access_snapshot_covers_all_lessons");
                    var sweptLessons = 0;
                    foreach (var lessonDescriptor in catalog.Lessons)
                    {
                        Invoke(form, "SelectLessonInCatalog", lessonDescriptor);
                        A(Get<string>(form, "SelectedLessonId") == lessonDescriptor.Id,
                            "math_hub_sweep_selects_lesson_" + lessonDescriptor.Id);
                        A(ContainsControlText(detailFlow, lessonDescriptor.TitleVi) &&
                            ContainsControlText(detailFlow, "Mục tiêu") &&
                            ContainsControlText(detailFlow, "Ví dụ có lời giải"),
                            "math_hub_sweep_renders_detail_" + lessonDescriptor.Id);
                        A(ContainsControlText(detailFlow, lessonDescriptor.PracticeSets.TotalCount + " câu trong ngân hàng bài học"),
                            "math_hub_sweep_renders_practice_count_" + lessonDescriptor.Id);
                        var lessonPractice = detailFlow.Controls.OfType<Button>()
                            .FirstOrDefault(x => string.Equals(x.AccessibleName, "Luyện tập bài " + lessonDescriptor.TitleVi, StringComparison.Ordinal));
                        A(lessonPractice != null && !string.IsNullOrWhiteSpace(lessonPractice.AccessibleDescription),
                            "math_hub_sweep_practice_accessible_" + lessonDescriptor.Id);
                        var access = accessMap[lessonDescriptor.Id];
                        A(access != null && lessonPractice.Enabled == Get<bool>(access, "IsUnlocked"),
                            "math_hub_sweep_practice_matches_unlock_" + lessonDescriptor.Id);
                        sweptLessons++;
                    }
                    A(sweptLessons == 67, "math_hub_sweeps_all_sixty_seven_lessons");

                    Invoke(form, "SelectLessonInCatalog", firstLesson);

                    var firstChapterButton = chapterFlow.Controls[0] as Button;
                    A(firstChapterButton != null && !string.IsNullOrWhiteSpace(firstChapterButton.AccessibleName),
                        "math_hub_chapter_button_accessible_name");
                    var firstLessonButton = FindFirstButton(lessonFlow);
                    A(firstLessonButton != null && !string.IsNullOrWhiteSpace(firstLessonButton.AccessibleDescription),
                        "math_hub_lesson_button_accessible_description");
                    var mission = GetField<Button>(form, "_missionButton");
                    A(!string.IsNullOrWhiteSpace(mission.AccessibleName), "math_hub_mission_button_accessible_name");
                    A(mission.TabStop, "math_hub_mission_button_keyboard_focusable");
                    var continueButton = GetField<Button>(form, "_continueLessonButton");
                    A(!continueButton.Enabled, "math_hub_continue_disabled_without_progress");

                    var secondLesson = catalog.FindLesson("m2_ls_num_full_hundreds_recognize");
                    var progressSkills = new Dictionary<string, SkillSnapshot>(StringComparer.Ordinal)
                    {
                        {
                            secondLesson.SkillId,
                            new SkillSnapshot
                            {
                                SkillId = secondLesson.SkillId,
                                AttemptsCount = 3,
                                MasteryScore = 0.55,
                                LearningState = "LEARNING",
                                LastSeenAtUtc = DateTime.UtcNow
                            }
                        }
                    };
                    SetField(form, "_skills", progressSkills);
                    Invoke(form, "PopulateChapters");
                    Invoke(form, "RefreshContinueLessonState");
                    A(Get<string>(form, "ContinueLessonId") == secondLesson.Id, "math_hub_continue_selects_recent_active_lesson");
                    A(continueButton.Enabled, "math_hub_continue_enabled_with_progress");
                    A(continueButton.AccessibleDescription.IndexOf(secondLesson.TitleVi, StringComparison.OrdinalIgnoreCase) >= 0,
                        "math_hub_continue_names_target_lesson");
                    A(chapterFlow.Controls[0].Text.IndexOf("1 đã học", StringComparison.OrdinalIgnoreCase) >= 0,
                        "math_hub_chapter_shows_real_studied_count");
                    Invoke(form, "ContinueCurrentLesson");
                    A(Get<string>(form, "SelectedLessonId") == secondLesson.Id, "math_hub_continue_opens_target_lesson");

                    RenderFormAndAssert(form, 1180, 760, "math_hub_default_window");
                    RenderFormAndAssert(form, 900, 640, "math_hub_min_window");
                }

                using (var missing = (WAHUKidsLearn.MathHubForm)ctor.Invoke(new object[]
                {
                    database,
                    new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW },
                    Path.Combine(tempRoot, "missing_lesson_catalog.json")
                }))
                {
                    Invoke(missing, "LoadCatalogAndProgress");
                    A(Get<int>(missing, "ChapterCount") == 0, "math_hub_missing_catalog_has_zero_chapters");
                    A(GetField<Label>(missing, "_summary").Text.IndexOf("kiểm tra lại", StringComparison.OrdinalIgnoreCase) >= 0,
                        "math_hub_missing_catalog_child_safe_summary");
                    A(GetField<Label>(missing, "_detailEmpty").Text.IndexOf("Chưa thể mở", StringComparison.OrdinalIgnoreCase) >= 0,
                        "math_hub_missing_catalog_child_safe_detail");
                    A(GetField<Control>(missing, "_detailFlow").Visible == false,
                        "math_hub_missing_catalog_hides_detail_flow");
                    A(GetField<Button>(missing, "_missionButton").Enabled,
                        "math_hub_missing_catalog_keeps_adaptive_mission_available");
                }
            }
            finally
            {
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }

        private static void TestResumePresentation(Assembly appAssembly)
        {
            var sessionPath = Path.Combine(Path.GetDirectoryName(appAssembly.Location), "WAHU.Session.dll");
            var sessionAssembly = Assembly.LoadFrom(sessionPath);
            var startType = sessionAssembly.GetType("WAHU.Session.MathSessionStartResult", true);
            var method = typeof(WAHUKidsLearn.MathLessonForm).GetMethod(
                "BuildSessionStartNotice", BindingFlags.Static | BindingFlags.NonPublic);
            A(method != null, "math_resume_notice_helper_available");

            var restored = Activator.CreateInstance(startType);
            Set(restored, "ResumedExistingSession", true);
            Set(restored, "RestoredOpenQuestion", true);
            Set(restored, "CompletedQuestionCount", 2);
            var restoredText = (string)method.Invoke(null, new[] { restored });
            A(restoredText.IndexOf("đúng câu", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_resume_notice_exact_open_question");

            var retry = Activator.CreateInstance(startType);
            Set(retry, "ResumedExistingSession", true);
            Set(retry, "RestoredOpenQuestion", true);
            Set(retry, "RetryPending", true);
            Set(retry, "CurrentAttemptIndex", 2);
            Set(retry, "CompletedQuestionCount", 1);
            var retryText = (string)method.Invoke(null, new[] { retry });
            A(retryText.IndexOf("thử lại", StringComparison.OrdinalIgnoreCase) >= 0 &&
                retryText.IndexOf("gợi ý", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_resume_notice_retry_pending_same_question");

            var corrupt = Activator.CreateInstance(startType);
            Set(corrupt, "ResumedExistingSession", true);
            Set(corrupt, "DiscardedCorruptOpenQuestion", true);
            Set(corrupt, "CompletedQuestionCount", 1);
            var corruptText = (string)method.Invoke(null, new[] { corrupt });
            A(corruptText.IndexOf("vẫn an toàn", StringComparison.OrdinalIgnoreCase) >= 0 &&
                corruptText.IndexOf("câu mới", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_resume_notice_corrupt_cache_safe");

            var resumed = Activator.CreateInstance(startType);
            Set(resumed, "ResumedExistingSession", true);
            Set(resumed, "CompletedQuestionCount", 3);
            var resumedText = (string)method.Invoke(null, new[] { resumed });
            A(resumedText.IndexOf("tiếp tục", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_resume_notice_completed_progress");

            var targetedCtor = typeof(WAHUKidsLearn.MathLessonForm).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(LearningDatabase), typeof(RuntimePerformanceSettings), typeof(string) },
                null);
            A(targetedCtor != null, "math_targeted_lesson_constructor_available");

            var targeted = Activator.CreateInstance(startType);
            Set(targeted, "SessionMode", "lesson");
            Set(targeted, "TargetQuestionCount", 3);
            Set(targeted, "TargetLessonTitleVi", "Đọc, viết số đến 1000");
            var targetedText = (string)method.Invoke(null, new[] { targeted });
            A(targetedText.IndexOf("3 câu", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_targeted_session_notice_uses_engine_question_count");

            var fresh = Activator.CreateInstance(startType);
            var freshText = (string)method.Invoke(null, new[] { fresh });
            A(freshText == null, "math_fresh_session_has_no_resume_notice");
        }

        private static void TestCompletionPresentation(Assembly appAssembly)
        {
            var sessionPath = Path.Combine(Path.GetDirectoryName(appAssembly.Location), "WAHU.Session.dll");
            var sessionAssembly = Assembly.LoadFrom(sessionPath);
            var summaryType = sessionAssembly.GetType("WAHU.Session.MathSessionSummary", true);
            var performanceMethod = typeof(WAHUKidsLearn.MathLessonForm).GetMethod(
                "BuildCompletionPerformanceText", BindingFlags.Static | BindingFlags.NonPublic);
            var supportMethod = typeof(WAHUKidsLearn.MathLessonForm).GetMethod(
                "BuildCompletionSupportText", BindingFlags.Static | BindingFlags.NonPublic);
            A(performanceMethod != null && supportMethod != null, "math_completion_helpers_available");

            var summary = Activator.CreateInstance(summaryType);
            Set(summary, "Attempts", 8);
            Set(summary, "Correct", 6);
            Set(summary, "IndependentCorrect", 4);
            Set(summary, "HintedCorrect", 2);
            Set(summary, "Wrong", 2);
            Set(summary, "DistinctSkills", 4);
            Set(summary, "ImprovedSkillCount", 2);
            Set(summary, "GardenGrowthSteps", 3);
            Set(summary, "GardenUnlockMessage", "Mở khóa: Bồn hoa.");

            var performance = (string)performanceMethod.Invoke(null, new[] { summary });
            A(performance.IndexOf("Đã làm 8", StringComparison.OrdinalIgnoreCase) >= 0 &&
                performance.IndexOf("Tự làm đúng 4", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_completion_shows_independent_correct_count");
            A(performance.IndexOf("Đúng nhờ gợi ý 2", StringComparison.OrdinalIgnoreCase) >= 0 &&
                performance.IndexOf("Cần luyện lại 2", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_completion_shows_hinted_and_practice_counts");

            var retrySummary = Activator.CreateInstance(summaryType);
            Set(retrySummary, "Attempts", 3);
            Set(retrySummary, "Correct", 3);
            Set(retrySummary, "IndependentCorrect", 2);
            Set(retrySummary, "RetriedQuestions", 1);
            Set(retrySummary, "RetriedCorrect", 1);
            var retryPerformance = (string)performanceMethod.Invoke(null, new[] { retrySummary });
            A(retryPerformance.IndexOf("Tự làm đúng 2", StringComparison.OrdinalIgnoreCase) >= 0 &&
                retryPerformance.IndexOf("Thử lại 1 câu (đúng 1)", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_completion_uses_first_class_retry_counters");
            A(retryPerformance.IndexOf("Tự làm đúng 3", StringComparison.OrdinalIgnoreCase) < 0,
                "math_completion_retry_correct_not_counted_independent");

            var support = (string)supportMethod.Invoke(null, new[] { summary });
            A(support.IndexOf("4 kỹ năng", StringComparison.OrdinalIgnoreCase) >= 0 &&
                support.IndexOf("Khu vườn", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_completion_support_shows_skills_and_garden_progress");
            A(support.IndexOf("Mở khóa: Bồn hoa", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_completion_support_shows_unlock_message");

            A(support.IndexOf("2 kỹ năng tiến bộ", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_completion_adaptive_uses_improved_skill_count");

            var targetedSummary = Activator.CreateInstance(summaryType);
            Set(targetedSummary, "Attempts", 3);
            Set(targetedSummary, "Correct", 2);
            Set(targetedSummary, "HintedCorrect", 1);
            Set(targetedSummary, "Wrong", 1);
            Set(targetedSummary, "DistinctSkills", 1);
            Set(targetedSummary, "SessionMode", "lesson");
            Set(targetedSummary, "LessonCompleted", true);
            Set(targetedSummary, "LessonScorePercent", 66.6666667d);
            Set(targetedSummary, "LessonBestScorePercent", 100d);
            Set(targetedSummary, "TargetSkillMasteryBefore", 0.25d);
            Set(targetedSummary, "TargetSkillMasteryAfter", 0.55d);
            Set(targetedSummary, "TargetSkillMasteryDelta", 0.30d);
            Set(targetedSummary, "NextLessonId", "m2_ls_num_full_hundreds_recognize");
            Set(targetedSummary, "NextLessonTitleVi", "Nhận biết các số tròn trăm");
            var targetedPerformance = (string)performanceMethod.Invoke(null, new[] { targetedSummary });
            var targetedSupport = (string)supportMethod.Invoke(null, new[] { targetedSummary });
            A(targetedPerformance.IndexOf("Điểm bài 67%", StringComparison.OrdinalIgnoreCase) >= 0 &&
                targetedPerformance.IndexOf("Tốt nhất 100%", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_completion_targeted_score_uses_engine_contract");
            A(targetedSupport.IndexOf("trong bài này", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_completion_targeted_support_names_lesson_context");

            A(targetedSupport.IndexOf("Mức thành thạo hiện tại: 55%", StringComparison.OrdinalIgnoreCase) >= 0 &&
                targetedSupport.IndexOf("tăng thêm 30 điểm phần trăm", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_completion_targeted_shows_engine_mastery_delta");
            A(targetedSupport.IndexOf("Bài tiếp theo đã sẵn sàng", StringComparison.OrdinalIgnoreCase) >= 0 &&
                targetedSupport.IndexOf("Nhận biết các số tròn trăm", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_completion_targeted_shows_engine_next_lesson");

            var inconsistent = Activator.CreateInstance(summaryType);
            Set(inconsistent, "Attempts", 3);
            Set(inconsistent, "Correct", 8);
            Set(inconsistent, "HintedCorrect", 9);
            Set(inconsistent, "Wrong", 9);
            var bounded = (string)performanceMethod.Invoke(null, new[] { inconsistent });
            A(bounded.IndexOf("Đã làm 3", StringComparison.OrdinalIgnoreCase) >= 0 &&
                bounded.IndexOf("Đúng nhờ gợi ý 3", StringComparison.OrdinalIgnoreCase) >= 0 &&
                bounded.IndexOf("Cần luyện lại 0", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_completion_bounds_inconsistent_counters");

            var tempRoot = Path.Combine(Path.GetTempPath(), "wahu-child-ui-completion-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var schema = Path.Combine(Directory.GetCurrentDirectory(), "data", "schema", "001_initial.sql");
                var database = new LearningDatabase(Path.Combine(tempRoot, "learning.db"), schema);
                using (var form = new WAHUKidsLearn.MathLessonForm(database,
                    new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW }))
                {
                    Invoke(form, "ShowCompletion", summary);
                    var feedback = GetField<Label>(form, "_feedback");
                    var supportLabel = GetField<Label>(form, "_support");
                    var next = GetField<Button>(form, "_nextButton");
                    A(feedback.Text == performance && feedback.AccessibleName.IndexOf("Kết quả nhiệm vụ", StringComparison.OrdinalIgnoreCase) >= 0,
                        "math_completion_feedback_accessible_summary");
                    A(supportLabel.Text == support && supportLabel.AccessibleName.IndexOf("Tóm tắt tiến bộ", StringComparison.OrdinalIgnoreCase) >= 0,
                        "math_completion_support_accessible_summary");
                    A(next.Text == "Về thư viện Toán" && next.AccessibleDescription.IndexOf("danh sách bài Toán", StringComparison.OrdinalIgnoreCase) >= 0,
                        "math_completion_return_route_matches_math_hub");
                    Invoke(form, "ShowCompletion", targetedSummary);
                    A(GetField<Label>(form, "_prompt").Text == "Hoàn thành bài học" &&
                        GetField<Label>(form, "_feedback").Text == targetedPerformance,
                        "math_completion_targeted_form_shows_lesson_result");
                    SetField(form, "_finished", true);
                    Invoke(form, "HandleNextButton");
                    A(form.DialogResult == DialogResult.OK,
                        "math_completion_returns_ok_to_math_hub_modal_flow");
                }
            }
            finally
            {
                try { Directory.Delete(tempRoot, true); } catch { }
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
                    var stopButton = GetField<Button>(form, "_stopButton");
                    A(stopButton.Text.IndexOf("học tiếp", StringComparison.OrdinalIgnoreCase) >= 0,
                        "math_stop_button_promises_resume_semantics");
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

        private static void TestTypedAnswerInput(Assembly appAssembly)
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "wahu-child-ui-typed-answer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var schema = Path.Combine(Directory.GetCurrentDirectory(), "data", "schema", "001_initial.sql");
                var database = new LearningDatabase(Path.Combine(tempRoot, "learning.db"), schema);
                using (var form = new WAHUKidsLearn.MathLessonForm(database, new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW }))
                {
                    var numeric = new MathQuestion
                    {
                        QuestionId = "typed_numeric",
                        QuestionType = "numeric_input",
                        AnswerKind = "integer",
                        CorrectAnswerText = "507",
                        PromptVi = "Viết số năm trăm linh bảy.",
                        Choices = new List<int>(),
                        ChoiceTexts = new List<string>()
                    };
                    Invoke(form, "ConfigureAnswerInput", numeric);
                    var typedLayout = GetField<Control>(form, "_typedAnswerLayout");
                    var typedBox = GetField<TextBox>(form, "_typedAnswerBox");
                    var typedSubmit = GetField<Button>(form, "_typedSubmitButton");
                    A(typedLayout.Controls.Contains(typedBox) && !GetField<Control>(form, "_answerGrid").Visible && !GetField<Control>(form, "_interactiveAnswerLayout").Visible,
                        "typed_numeric_switches_to_input_surface");
                    A(typedBox.Enabled && string.IsNullOrEmpty(typedBox.Text) && !typedSubmit.Enabled,
                        "typed_numeric_starts_empty_and_submit_disabled");
                    typedBox.Text = "507";
                    A(typedSubmit.Enabled, "typed_numeric_submit_enabled_after_input");
                    A(GetField<Label>(form, "_support").Text.IndexOf("Nhập đáp án", StringComparison.OrdinalIgnoreCase) >= 0,
                        "typed_numeric_has_child_safe_guidance");

                    var expression = new MathQuestion
                    {
                        QuestionId = "typed_expression",
                        QuestionType = "expression_input",
                        AnswerKind = "expression",
                        CorrectAnswerText = "100 - 30 + 5",
                        AcceptedAnswers = new List<string> { "75" },
                        Choices = new List<int>(),
                        ChoiceTexts = new List<string>()
                    };
                    Invoke(form, "ConfigureAnswerInput", expression);
                    A(typedBox.Enabled && GetField<Label>(form, "_support").Text.IndexOf("biểu thức", StringComparison.OrdinalIgnoreCase) >= 0,
                        "typed_expression_uses_expression_guidance");

                    var unit = new MathQuestion
                    {
                        QuestionId = "typed_unit",
                        QuestionType = "unit_input",
                        AnswerKind = "unit",
                        CorrectAnswerText = "5 kg",
                        ExpectedUnit = "kg",
                        AcceptedUnits = new List<string> { "kg", "kilôgam" },
                        Choices = new List<int>(),
                        ChoiceTexts = new List<string>()
                    };
                    Invoke(form, "ConfigureAnswerInput", unit);
                    A(typedBox.Enabled && GetField<Label>(form, "_support").Text.IndexOf("đơn vị", StringComparison.OrdinalIgnoreCase) >= 0,
                        "typed_unit_requests_number_and_unit");
                    A(typedBox.AccessibleDescription.IndexOf("Enter", StringComparison.OrdinalIgnoreCase) >= 0,
                        "typed_answer_keyboard_submit_is_announced");

                    var wordProblem = new MathQuestion
                    {
                        QuestionId = "typed_word_problem",
                        QuestionType = "word_problem",
                        AnswerKind = "integer",
                        CorrectAnswerText = "25",
                        Choices = new List<int>(),
                        ChoiceTexts = new List<string>()
                    };
                    Invoke(form, "ConfigureAnswerInput", wordProblem);
                    A(typedBox.Enabled && !GetField<Control>(form, "_answerGrid").Visible, "typed_word_problem_uses_input_surface");
                    RenderFormAndAssert(form, 1080, 720, "typed_answer_default_window");
                    RenderFormAndAssert(form, 900, 640, "typed_answer_min_window");
                }
            }
            finally
            {
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }
        private static void TestAllAuthoredAnswerSurfaces(Assembly appAssembly)
        {
            var sessionPath = Path.Combine(Path.GetDirectoryName(appAssembly.Location), "WAHU.Session.dll");
            var sessionAssembly = Assembly.LoadFrom(sessionPath);
            var sourceType = sessionAssembly.GetType("WAHU.Session.MathAuthoredQuestionSource", true);
            var source = Activator.CreateInstance(sourceType);
            var load = sourceType.GetMethod("Load", BindingFlags.Instance | BindingFlags.Public);
            A(load != null, "authored_ui_source_loader_available");
            var bankPath = Path.Combine(Directory.GetCurrentDirectory(), "content_packs", "math_grade2_v1", "question_bank_v1.json");
            var bank = load.Invoke(source, new object[] { bankPath });
            var questionsProperty = bank.GetType().GetProperty("Questions", BindingFlags.Instance | BindingFlags.Public);
            A(questionsProperty != null, "authored_ui_bank_questions_available");
            var questions = questionsProperty.GetValue(bank, null) as System.Collections.IEnumerable;
            A(questions != null, "authored_ui_bank_questions_enumerable");

            var tempRoot = Path.Combine(Path.GetTempPath(), "wahu-child-ui-all-authored-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var schema = Path.Combine(Directory.GetCurrentDirectory(), "data", "schema", "001_initial.sql");
                var database = new LearningDatabase(Path.Combine(tempRoot, "learning.db"), schema);
                using (var form = new WAHUKidsLearn.MathLessonForm(database, new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW }))
                {
                    var count = 0;
                    var typedCount = 0;
                    var choiceCount = 0;
                    var interactionCount = 0;
                    foreach (var raw in questions)
                    {
                        var question = raw as MathQuestion;
                        A(question != null && !string.IsNullOrWhiteSpace(question.ContentQuestionId), "authored_ui_question_maps_" + count);
                        try
                        {
                            Invoke(form, "ConfigureAnswerInput", question);
                        }
                        catch (TargetInvocationException ex)
                        {
                            throw new Exception("Authored UI cannot render " + question.ContentQuestionId + " (" + question.QuestionType + "/" + question.AnswerKind + ")", ex.InnerException ?? ex);
                        }

                        if (string.Equals(question.AnswerKind, "interaction_integer", StringComparison.Ordinal))
                        {
                            interactionCount++;
                            A(GetField<Control>(form, "_interactiveAnswer").Enabled, "authored_ui_interaction_ready_" + question.ContentQuestionId);
                        }
                        else if (question.DisplayChoices == null || question.DisplayChoices.Count == 0)
                        {
                            typedCount++;
                            A(GetField<TextBox>(form, "_typedAnswerBox").Enabled && !GetField<Button>(form, "_typedSubmitButton").Enabled,
                                "authored_ui_typed_ready_" + question.ContentQuestionId);
                        }
                        else
                        {
                            choiceCount++;
                            A(question.DisplayChoices.Count >= 2 && question.DisplayChoices.Count <= 4,
                                "authored_ui_choice_count_supported_" + question.ContentQuestionId);
                        }
                        count++;
                    }
                    A(count == 201, "authored_ui_sweep_all_201_questions");
                    A(typedCount == 109, "authored_ui_sweep_109_typed_questions");
                    A(choiceCount == 91, "authored_ui_sweep_91_choice_questions");
                    A(interactionCount == 1, "authored_ui_sweep_one_interaction_question");
                }
            }
            finally
            {
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }
        private static void TestTargetedLessonUiFlow(Assembly appAssembly)
        {
            var repo = Directory.GetCurrentDirectory();
            var sourceContent = Path.Combine(repo, "content_packs", "math_grade2_v1");
            var runtimeContent = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "content_packs", "math_grade2_v1");
            Directory.CreateDirectory(runtimeContent);
            foreach (var name in new[] { "verified_templates_v1.json", "lesson_catalog_v1.json", "question_bank_v1.json" })
                File.Copy(Path.Combine(sourceContent, name), Path.Combine(runtimeContent, name), true);

            var catalogPath = Path.Combine(sourceContent, "lesson_catalog_v1.json");
            var catalog = new MathLessonCatalogSource().Load(catalogPath);
            var prerequisite = catalog.FindLesson("m2_ls_num_count_read_write_0_1000");
            var dependent = catalog.FindLesson("m2_ls_num_full_hundreds_recognize");
            A(prerequisite != null && dependent != null && dependent.PrerequisiteSkills.Contains(prerequisite.SkillId),
                "targeted_ui_flow_fixture_prerequisite_edge");

            var tempRoot = Path.Combine(Path.GetTempPath(), "wahu-child-ui-targeted-flow-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var schemaSource = Path.Combine(repo, "data", "schema");
                var schemaDir = Path.Combine(tempRoot, "schema");
                Directory.CreateDirectory(schemaDir);
                foreach (var source in Directory.GetFiles(schemaSource, "*.sql"))
                    File.Copy(source, Path.Combine(schemaDir, Path.GetFileName(source)), true);
                var database = new LearningDatabase(Path.Combine(tempRoot, "learning.db"), Path.Combine(schemaDir, "001_initial.sql"));
                var init = database.Initialize("DELETE");
                A(init.SchemaVersion == 4 && init.Health.IsHealthy, "targeted_ui_flow_database_v4_ready");
                new LearnerSessionService(database).EnsurePrimaryChild("Bé UI targeted");

                var lessonCtor = typeof(WAHUKidsLearn.MathLessonForm).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(LearningDatabase), typeof(RuntimePerformanceSettings), typeof(string) },
                    null);
                A(lessonCtor != null, "targeted_ui_flow_lesson_constructor_available");
                using (var form = (WAHUKidsLearn.MathLessonForm)lessonCtor.Invoke(new object[]
                {
                    database,
                    new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW },
                    prerequisite.Id
                }))
                {
                    Invoke(form, "StartSession");
                    for (var ordinal = 0; ordinal < 3; ordinal++)
                    {
                        var question = GetField<MathQuestion>(form, "_question");
                        A(question != null && question.LessonId == prerequisite.Id && !string.IsNullOrWhiteSpace(question.ContentQuestionId),
                            "targeted_ui_flow_question_traceability_" + ordinal);
                        if (question.DisplayChoices == null || question.DisplayChoices.Count == 0)
                        {
                            var input = GetField<TextBox>(form, "_typedAnswerBox");
                            if (ordinal == 0)
                            {
                                input.Text = "not-a-valid-answer";
                                A(GetField<Button>(form, "_typedSubmitButton").Enabled,
                                    "targeted_ui_retry_first_attempt_submit_ready");
                                Invoke(form, "SubmitTypedAnswer", "ui_e2e_first_try_wrong");
                                var retryCoordinator = GetField<object>(form, "_coordinator");
                                var pendingRetrySummary = Get<object>(retryCoordinator, "Summary");
                                A(GetField<bool>(form, "_retryPending") && Get<int>(pendingRetrySummary, "Attempts") == 0 &&
                                    Get<int>(pendingRetrySummary, "AnswerAttempts") == 1,
                                    "targeted_ui_retry_wrong_first_try_does_not_advance_progress");
                                A(input.Enabled && GetField<Button>(form, "_typedSubmitButton").Enabled &&
                                    !GetField<Button>(form, "_nextButton").Visible &&
                                    GetField<Label>(form, "_progressText").Text.IndexOf("thử lại", StringComparison.OrdinalIgnoreCase) >= 0,
                                    "targeted_ui_retry_keeps_same_typed_question_editable");
                                input.Text = question.CorrectAnswerDisplay;
                                Invoke(form, "SubmitTypedAnswer", "ui_e2e_retry_correct");
                                var finalizedRetrySummary = Get<object>(retryCoordinator, "Summary");
                                A(!GetField<bool>(form, "_retryPending") && Get<int>(finalizedRetrySummary, "Attempts") == 1 &&
                                    Get<int>(finalizedRetrySummary, "AnswerAttempts") == 2 && Get<int>(finalizedRetrySummary, "RetriedCorrect") == 1 &&
                                    Get<int>(finalizedRetrySummary, "IndependentCorrect") == 0,
                                    "targeted_ui_retry_correct_finalizes_once_as_assisted");
                            }
                            else
                            {
                                input.Text = question.CorrectAnswerDisplay;
                                A(GetField<Button>(form, "_typedSubmitButton").Enabled,
                                    "targeted_ui_flow_typed_submit_ready_" + ordinal);
                                Invoke(form, "SubmitTypedAnswer", "ui_e2e");
                            }
                        }
                        else
                        {
                            var correctIndex = -1;
                            for (var i = 0; i < question.DisplayChoices.Count; i++)
                                if (string.Equals(question.DisplayChoices[i], question.CorrectAnswerDisplay, StringComparison.Ordinal)) correctIndex = i;
                            A(correctIndex >= 0, "targeted_ui_flow_choice_correct_index_" + ordinal);
                            if (ordinal == 0)
                            {
                                var wrongIndex = (correctIndex + 1) % question.DisplayChoices.Count;
                                Invoke(form, "SubmitChoice", wrongIndex, "ui_e2e_first_try_wrong");
                                var retryCoordinator = GetField<object>(form, "_coordinator");
                                var pendingChoiceRetrySummary = Get<object>(retryCoordinator, "Summary");
                                A(GetField<bool>(form, "_retryPending") && Get<int>(pendingChoiceRetrySummary, "Attempts") == 0 &&
                                    Get<int>(pendingChoiceRetrySummary, "AnswerAttempts") == 1 && !GetField<Button>(form, "_nextButton").Visible,
                                    "targeted_ui_retry_choice_wrong_first_try_keeps_question_open");
                                Invoke(form, "SubmitChoice", correctIndex, "ui_e2e_retry_correct");
                            }
                            else
                            {
                                Invoke(form, "SubmitChoice", correctIndex, "ui_e2e");
                            }
                        }

                        if (ordinal < 2)
                            Invoke(form, "HandleNextButton");
                        else
                        {
                            A(GetField<bool>(form, "_completeOnNext"), "targeted_ui_flow_last_answer_routes_to_result");
                            Invoke(form, "HandleNextButton");
                        }
                    }

                    A(GetField<bool>(form, "_finished"), "targeted_ui_flow_form_completed");
                    A(GetField<Label>(form, "_prompt").Text == "Hoàn thành bài học",
                        "targeted_ui_flow_result_title_is_lesson");
                    var resultFeedback = GetField<Label>(form, "_feedback").Text;
                    A(resultFeedback.IndexOf("Điểm bài 100%", StringComparison.OrdinalIgnoreCase) >= 0,
                        "targeted_ui_flow_result_uses_engine_score");
                    A(resultFeedback.IndexOf("Tự làm đúng 2", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        resultFeedback.IndexOf("Thử lại 1 câu (đúng 1)", StringComparison.OrdinalIgnoreCase) >= 0,
                        "targeted_ui_flow_result_preserves_first_try_retry_semantics");
                    var resultSupport = GetField<Label>(form, "_support").Text;
                    A(resultSupport.IndexOf("Mức thành thạo hiện tại", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        resultSupport.IndexOf("tăng thêm", StringComparison.OrdinalIgnoreCase) >= 0,
                        "targeted_ui_flow_result_uses_engine_mastery_delta");
                    A(resultSupport.IndexOf("Bài tiếp theo đã sẵn sàng", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        resultSupport.IndexOf(dependent.TitleVi, StringComparison.OrdinalIgnoreCase) >= 0,
                        "targeted_ui_flow_result_uses_engine_next_lesson");
                }

                var storedProgress = new MathLessonProgressStore(database)
                    .LoadOne(LearnerSessionService.PrimaryChildId, prerequisite.Id);
                A(storedProgress != null && storedProgress.CompletedCount == 1,
                    "targeted_ui_flow_progress_row_persisted");
                A(storedProgress.LastScorePercent.HasValue && storedProgress.BestScorePercent.HasValue &&
                    Math.Abs(storedProgress.LastScorePercent.Value - 100.0) < 0.001 &&
                    Math.Abs(storedProgress.BestScorePercent.Value - 100.0) < 0.001,
                    "targeted_ui_flow_progress_score_persisted");

                var hubCtor = typeof(WAHUKidsLearn.MathHubForm).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(LearningDatabase), typeof(RuntimePerformanceSettings), typeof(string) },
                    null);
                A(hubCtor != null, "targeted_ui_flow_hub_constructor_available");
                using (var hub = (WAHUKidsLearn.MathHubForm)hubCtor.Invoke(new object[]
                {
                    database,
                    new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW },
                    catalogPath
                }))
                {
                    Invoke(hub, "LoadCatalogAndProgress");
                    Invoke(hub, "SelectLessonInCatalog", prerequisite);
                    var detail = GetField<FlowLayoutPanel>(hub, "_detailFlow");
                    A(ContainsControlText(detail, "Đã hoàn thành") && ContainsControlText(detail, "Tốt nhất: 100%"),
                        "targeted_ui_flow_hub_shows_completed_score");
                    Invoke(hub, "SelectLessonInCatalog", dependent);
                    var unlockedPractice = FindButtonContaining(detail, "Luyện 3 câu bài này");
                    A(unlockedPractice != null && unlockedPractice.Enabled,
                        "targeted_ui_flow_completing_prerequisite_unlocks_next_lesson");
                }
            }
            finally
            {
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }
        private static void TestChoiceRetryUiFlow(Assembly appAssembly)
        {
            var repo = Directory.GetCurrentDirectory();
            var sourceContent = Path.Combine(repo, "content_packs", "math_grade2_v1");
            var runtimeContent = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "content_packs", "math_grade2_v1");
            Directory.CreateDirectory(runtimeContent);
            foreach (var name in new[] { "verified_templates_v1.json", "lesson_catalog_v1.json", "question_bank_v1.json" })
                File.Copy(Path.Combine(sourceContent, name), Path.Combine(runtimeContent, name), true);

            var catalogPath = Path.Combine(sourceContent, "lesson_catalog_v1.json");
            var lesson = new MathLessonCatalogSource().Load(catalogPath).FindLesson("m2_ls_point_recognize");
            A(lesson != null && (lesson.PrerequisiteSkills == null || lesson.PrerequisiteSkills.Count == 0),
                "choice_retry_fixture_root_lesson_unlocked");

            var tempRoot = Path.Combine(Path.GetTempPath(), "wahu-child-ui-choice-retry-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var schemaSource = Path.Combine(repo, "data", "schema");
                var schemaDir = Path.Combine(tempRoot, "schema");
                Directory.CreateDirectory(schemaDir);
                foreach (var source in Directory.GetFiles(schemaSource, "*.sql"))
                    File.Copy(source, Path.Combine(schemaDir, Path.GetFileName(source)), true);
                var database = new LearningDatabase(Path.Combine(tempRoot, "learning.db"), Path.Combine(schemaDir, "001_initial.sql"));
                var init = database.Initialize("DELETE");
                A(init.SchemaVersion == 4 && init.Health.IsHealthy, "choice_retry_database_v4_ready");
                new LearnerSessionService(database).EnsurePrimaryChild("Bé UI choice retry");

                var ctor = typeof(WAHUKidsLearn.MathLessonForm).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(LearningDatabase), typeof(RuntimePerformanceSettings), typeof(string) },
                    null);
                A(ctor != null, "choice_retry_lesson_constructor_available");
                using (var form = (WAHUKidsLearn.MathLessonForm)ctor.Invoke(new object[]
                {
                    database,
                    new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW },
                    lesson.Id
                }))
                {
                    Invoke(form, "StartSession");
                    var question = GetField<MathQuestion>(form, "_question");
                    A(question != null && question.LessonId == lesson.Id && question.DisplayChoices != null && question.DisplayChoices.Count >= 2,
                        "choice_retry_first_question_is_choice");
                    var correctIndex = -1;
                    for (var i = 0; i < question.DisplayChoices.Count; i++)
                        if (string.Equals(question.DisplayChoices[i], question.CorrectAnswerDisplay, StringComparison.Ordinal)) correctIndex = i;
                    A(correctIndex >= 0, "choice_retry_correct_index_resolves");
                    var wrongIndex = (correctIndex + 1) % question.DisplayChoices.Count;
                    var buttons = GetField<Array>(form, "_answerButtons");

                    Invoke(form, "SubmitChoice", wrongIndex, "ui_choice_first_try_wrong");
                    var coordinator = GetField<object>(form, "_coordinator");
                    var pending = Get<object>(coordinator, "Summary");
                    A(GetField<bool>(form, "_retryPending") && Get<int>(pending, "Attempts") == 0 &&
                        Get<int>(pending, "AnswerAttempts") == 1 && !GetField<Button>(form, "_nextButton").Visible,
                        "choice_retry_wrong_first_try_keeps_same_question_open");
                    var wrongButton = (Control)buttons.GetValue(wrongIndex);
                    var correctButton = (Control)buttons.GetValue(correctIndex);
                    A(Get<object>(wrongButton, "VisualState").ToString() == "Incorrect",
                        "choice_retry_marks_selected_wrong_choice");
                    A(wrongButton.Enabled, "choice_retry_reenables_selected_wrong_choice");
                    A(Get<object>(correctButton, "VisualState").ToString() == "Idle",
                        "choice_retry_does_not_reveal_correct_choice_before_retry");
                    A(correctButton.Enabled, "choice_retry_keeps_other_choices_enabled");
                    A(GetField<Label>(form, "_progressText").Text.IndexOf("thử lại", StringComparison.OrdinalIgnoreCase) >= 0,
                        "choice_retry_progress_labels_retry_state");

                    Invoke(form, "SubmitChoice", correctIndex, "ui_choice_retry_correct");
                    var finalized = Get<object>(coordinator, "Summary");
                    A(!GetField<bool>(form, "_retryPending") && Get<int>(finalized, "Attempts") == 1 &&
                        Get<int>(finalized, "AnswerAttempts") == 2 && Get<int>(finalized, "RetriedQuestions") == 1 &&
                        Get<int>(finalized, "RetriedCorrect") == 1 && Get<int>(finalized, "IndependentCorrect") == 0,
                        "choice_retry_correct_finalizes_once_as_assisted");
                    A(Get<object>(correctButton, "VisualState").ToString() == "Correct" &&
                        Get<object>(wrongButton, "VisualState").ToString() == "Muted",
                        "choice_retry_final_attempt_reveals_correct_choice");
                    A(!correctButton.Enabled && !wrongButton.Enabled,
                        "choice_retry_final_attempt_locks_choices");

                    Invoke(coordinator, "Abort", "choice_retry_cleanup");
                    SetField(form, "_finished", true);
                }
            }
            finally
            {
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }

        private static void TestSubmitFailureRecoveryUiFlow(Assembly appAssembly)
        {
            var repo = Directory.GetCurrentDirectory();
            var sourceContent = Path.Combine(repo, "content_packs", "math_grade2_v1");
            var runtimeContent = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "content_packs", "math_grade2_v1");
            Directory.CreateDirectory(runtimeContent);
            foreach (var name in new[] { "verified_templates_v1.json", "lesson_catalog_v1.json", "question_bank_v1.json" })
                File.Copy(Path.Combine(sourceContent, name), Path.Combine(runtimeContent, name), true);

            var lesson = new MathLessonCatalogSource().Load(Path.Combine(sourceContent, "lesson_catalog_v1.json"))
                .FindLesson("m2_ls_point_recognize");
            A(lesson != null && (lesson.PrerequisiteSkills == null || lesson.PrerequisiteSkills.Count == 0),
                "submit_failure_fixture_root_lesson_unlocked");

            var tempRoot = Path.Combine(Path.GetTempPath(), "wahu-child-ui-submit-failure-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var schemaSource = Path.Combine(repo, "data", "schema");
                var schemaDir = Path.Combine(tempRoot, "schema");
                Directory.CreateDirectory(schemaDir);
                foreach (var source in Directory.GetFiles(schemaSource, "*.sql"))
                    File.Copy(source, Path.Combine(schemaDir, Path.GetFileName(source)), true);
                var database = new LearningDatabase(Path.Combine(tempRoot, "learning.db"), Path.Combine(schemaDir, "001_initial.sql"));
                var init = database.Initialize("DELETE");
                A(init.SchemaVersion == 4 && init.Health.IsHealthy, "submit_failure_database_v4_ready");
                new LearnerSessionService(database).EnsurePrimaryChild("Bé UI write recovery");

                var ctor = typeof(WAHUKidsLearn.MathLessonForm).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(LearningDatabase), typeof(RuntimePerformanceSettings), typeof(string) },
                    null);
                A(ctor != null, "submit_failure_lesson_constructor_available");
                using (var form = (WAHUKidsLearn.MathLessonForm)ctor.Invoke(new object[]
                {
                    database,
                    new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW },
                    lesson.Id
                }))
                {
                    Invoke(form, "StartSession");
                    var question = GetField<MathQuestion>(form, "_question");
                    A(question != null && question.DisplayChoices != null && question.DisplayChoices.Count >= 2,
                        "submit_failure_first_question_is_choice");
                    var contentQuestionId = question.ContentQuestionId;
                    var correctIndex = -1;
                    for (var i = 0; i < question.DisplayChoices.Count; i++)
                        if (string.Equals(question.DisplayChoices[i], question.CorrectAnswerDisplay, StringComparison.Ordinal)) correctIndex = i;
                    A(correctIndex >= 0, "submit_failure_correct_index_resolves");

                    ExecuteDatabaseSql(database, @"CREATE TRIGGER ui_fail_math_mastery
BEFORE INSERT ON mastery_event
BEGIN
    SELECT RAISE(ABORT, 'ui_injected_math_commit_failure');
END;");
                    Invoke(form, "SubmitChoice", correctIndex, "ui_write_failure");

                    var coordinator = GetField<object>(form, "_coordinator");
                    var failedSummary = Get<object>(coordinator, "Summary");
                    A(!GetField<bool>(form, "_finished") && !GetField<bool>(form, "_submitting") &&
                        Get<bool>(coordinator, "IsActive") && Get<bool>(coordinator, "HasOpenQuestion"),
                        "submit_failure_ui_keeps_recoverable_session_active");
                    A(Get<int>(failedSummary, "Attempts") == 0 && Get<int>(failedSummary, "AnswerAttempts") == 0,
                        "submit_failure_ui_does_not_advance_counters");
                    var restored = GetField<MathQuestion>(form, "_question");
                    A(restored != null && string.Equals(restored.ContentQuestionId, contentQuestionId, StringComparison.Ordinal),
                        "submit_failure_ui_keeps_same_authored_question");
                    A(GetField<Label>(form, "_feedback").Text.IndexOf("Chưa lưu", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        GetField<Label>(form, "_support").Text.IndexOf("an toàn", StringComparison.OrdinalIgnoreCase) >= 0,
                        "submit_failure_ui_explains_safe_retry");
                    var buttons = GetField<Array>(form, "_answerButtons");
                    for (var i = 0; i < question.DisplayChoices.Count; i++)
                    {
                        var button = (Control)buttons.GetValue(i);
                        A(button.Enabled && Get<object>(button, "VisualState").ToString() == "Idle",
                            "submit_failure_ui_choice_reenabled_" + i);
                    }
                    A(!GetField<bool>(form, "_completeOnNext"), "submit_failure_ui_does_not_route_to_next_question");

                    ExecuteDatabaseSql(database, "DROP TRIGGER ui_fail_math_mastery;");
                    Invoke(form, "SubmitChoice", correctIndex, "ui_write_recovery_retry");
                    var recoveredSummary = Get<object>(coordinator, "Summary");
                    A(Get<int>(recoveredSummary, "Attempts") == 1 && Get<int>(recoveredSummary, "AnswerAttempts") == 1 &&
                        Get<int>(recoveredSummary, "IndependentCorrect") == 1 && Get<int>(recoveredSummary, "RetriedQuestions") == 0,
                        "submit_failure_retry_commits_once_as_true_first_durable_try");
                    var correctButton = (Control)buttons.GetValue(correctIndex);
                    A(!correctButton.Enabled && Get<object>(correctButton, "VisualState").ToString() == "Correct",
                        "submit_failure_retry_finalizes_normal_answer_ui");

                    Invoke(coordinator, "Abort", "submit_failure_cleanup");
                    SetField(form, "_finished", true);
                }

                var retryDatabase = new LearningDatabase(Path.Combine(tempRoot, "learning-retry.db"), Path.Combine(schemaDir, "001_initial.sql"));
                var retryInit = retryDatabase.Initialize("DELETE");
                A(retryInit.SchemaVersion == 4 && retryInit.Health.IsHealthy, "submit_retry_failure_database_v4_ready");
                new LearnerSessionService(retryDatabase).EnsurePrimaryChild("Bé UI retry write recovery");
                using (var retryForm = (WAHUKidsLearn.MathLessonForm)ctor.Invoke(new object[]
                {
                    retryDatabase,
                    new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW },
                    lesson.Id
                }))
                {
                    Invoke(retryForm, "StartSession");
                    var retryQuestion = GetField<MathQuestion>(retryForm, "_question");
                    var retryCorrectIndex = -1;
                    for (var i = 0; i < retryQuestion.DisplayChoices.Count; i++)
                        if (string.Equals(retryQuestion.DisplayChoices[i], retryQuestion.CorrectAnswerDisplay, StringComparison.Ordinal)) retryCorrectIndex = i;
                    A(retryCorrectIndex >= 0, "submit_retry_failure_correct_index_resolves");
                    var retryWrongIndex = (retryCorrectIndex + 1) % retryQuestion.DisplayChoices.Count;
                    Invoke(retryForm, "SubmitChoice", retryWrongIndex, "ui_retry_write_fixture_wrong");
                    var retryCoordinator = GetField<object>(retryForm, "_coordinator");
                    var beforeFailure = Get<object>(retryCoordinator, "Summary");
                    A(GetField<bool>(retryForm, "_retryPending") && Get<int>(beforeFailure, "Attempts") == 0 &&
                        Get<int>(beforeFailure, "AnswerAttempts") == 1,
                        "submit_retry_failure_fixture_is_attempt_two");

                    ExecuteDatabaseSql(retryDatabase, @"CREATE TRIGGER ui_fail_math_retry_mastery
BEFORE INSERT ON mastery_event
BEGIN
    SELECT RAISE(ABORT, 'ui_injected_math_retry_commit_failure');
END;");
                    Invoke(retryForm, "SubmitChoice", retryCorrectIndex, "ui_retry_write_failure");
                    var failedRetrySummary = Get<object>(retryCoordinator, "Summary");
                    A(!GetField<bool>(retryForm, "_finished") && GetField<bool>(retryForm, "_retryPending") &&
                        Get<bool>(retryCoordinator, "IsActive") && Get<bool>(retryCoordinator, "HasOpenQuestion"),
                        "submit_retry_failure_ui_preserves_retry_pending_session");
                    A(Get<int>(failedRetrySummary, "Attempts") == 0 && Get<int>(failedRetrySummary, "AnswerAttempts") == 1 &&
                        Get<int>(failedRetrySummary, "RetriedCorrect") == 0,
                        "submit_retry_failure_does_not_add_ghost_retry_attempt");
                    A(GetField<Label>(retryForm, "_progressText").Text.IndexOf("thử lại", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        GetField<Label>(retryForm, "_support").Text.IndexOf("Lần thử lại", StringComparison.OrdinalIgnoreCase) >= 0,
                        "submit_retry_failure_ui_keeps_attempt_two_presentation");
                    var retryButtons = GetField<Array>(retryForm, "_answerButtons");
                    var retryCorrectButton = (Control)retryButtons.GetValue(retryCorrectIndex);
                    A(retryCorrectButton.Enabled && Get<object>(retryCorrectButton, "VisualState").ToString() == "Idle",
                        "submit_retry_failure_choices_reenabled_without_reveal");

                    ExecuteDatabaseSql(retryDatabase, "DROP TRIGGER ui_fail_math_retry_mastery;");
                    Invoke(retryForm, "SubmitChoice", retryCorrectIndex, "ui_retry_write_recovered");
                    var retryRecovered = Get<object>(retryCoordinator, "Summary");
                    A(!GetField<bool>(retryForm, "_retryPending") && Get<int>(retryRecovered, "Attempts") == 1 &&
                        Get<int>(retryRecovered, "AnswerAttempts") == 2 && Get<int>(retryRecovered, "RetriedQuestions") == 1 &&
                        Get<int>(retryRecovered, "RetriedCorrect") == 1 && Get<int>(retryRecovered, "IndependentCorrect") == 0,
                        "submit_retry_failure_recovery_preserves_assisted_semantics");

                    Invoke(retryCoordinator, "Abort", "submit_retry_failure_cleanup");
                    SetField(retryForm, "_finished", true);
                }
            }
            finally
            {
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }

        private static void TestTypedAndInteractionSubmitFailureRecoveryUiFlow(Assembly appAssembly)
        {
            var repo = Directory.GetCurrentDirectory();
            var sourceContent = Path.Combine(repo, "content_packs", "math_grade2_v1");
            var runtimeContent = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "content_packs", "math_grade2_v1");
            Directory.CreateDirectory(runtimeContent);
            foreach (var name in new[] { "verified_templates_v1.json", "lesson_catalog_v1.json", "question_bank_v1.json" })
                File.Copy(Path.Combine(sourceContent, name), Path.Combine(runtimeContent, name), true);

            var catalog = new MathLessonCatalogSource().Load(Path.Combine(sourceContent, "lesson_catalog_v1.json"));
            var typedLesson = catalog.FindLesson("m2_ls_num_count_read_write_0_1000");
            var pointLesson = catalog.FindLesson("m2_ls_point_recognize");
            var lineLesson = catalog.FindLesson("m2_ls_line_segment_recognize");
            var segmentLesson = catalog.FindLesson("m2_ls_draw_segment_given_length");
            A(typedLesson != null && pointLesson != null && lineLesson != null && segmentLesson != null,
                "submit_failure_multi_surface_fixtures_exist");
            A(typedLesson.PrerequisiteSkills.Count == 0,
                "submit_failure_typed_fixture_is_root_lesson");

            var tempRoot = Path.Combine(Path.GetTempPath(), "wahu-child-ui-submit-failure-surfaces-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var schemaSource = Path.Combine(repo, "data", "schema");
                var schemaDir = Path.Combine(tempRoot, "schema");
                Directory.CreateDirectory(schemaDir);
                foreach (var source in Directory.GetFiles(schemaSource, "*.sql"))
                    File.Copy(source, Path.Combine(schemaDir, Path.GetFileName(source)), true);

                var ctor = typeof(WAHUKidsLearn.MathLessonForm).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(LearningDatabase), typeof(RuntimePerformanceSettings), typeof(string) },
                    null);
                A(ctor != null, "submit_failure_multi_surface_constructor_available");

                var typedDatabase = new LearningDatabase(Path.Combine(tempRoot, "typed.db"), Path.Combine(schemaDir, "001_initial.sql"));
                var typedInit = typedDatabase.Initialize("DELETE");
                A(typedInit.SchemaVersion == 4 && typedInit.Health.IsHealthy, "submit_failure_typed_database_v4_ready");
                new LearnerSessionService(typedDatabase).EnsurePrimaryChild("Bé UI typed write recovery");
                using (var typedForm = (WAHUKidsLearn.MathLessonForm)ctor.Invoke(new object[]
                {
                    typedDatabase,
                    new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW },
                    typedLesson.Id
                }))
                {
                    Invoke(typedForm, "StartSession");
                    var question = GetField<MathQuestion>(typedForm, "_question");
                    var input = GetField<TextBox>(typedForm, "_typedAnswerBox");
                    A(question != null && question.ContentQuestionId == "m2_q_num_count_read_write_0_1000_01" &&
                        question.DisplayChoices.Count == 0 && input.Enabled,
                        "submit_failure_typed_fixture_uses_real_typed_surface");
                    input.Text = question.CorrectAnswerDisplay;
                    ExecuteDatabaseSql(typedDatabase, @"CREATE TRIGGER ui_fail_typed_mastery
BEFORE INSERT ON mastery_event
BEGIN
    SELECT RAISE(ABORT, 'ui_injected_typed_commit_failure');
END;");
                    Invoke(typedForm, "SubmitTypedAnswer", "ui_typed_write_failure");

                    var coordinator = GetField<object>(typedForm, "_coordinator");
                    var failed = Get<object>(coordinator, "Summary");
                    A(!GetField<bool>(typedForm, "_finished") && Get<bool>(coordinator, "IsActive") &&
                        Get<bool>(coordinator, "HasOpenQuestion") && Get<int>(failed, "Attempts") == 0 &&
                        Get<int>(failed, "AnswerAttempts") == 0,
                        "submit_failure_typed_keeps_same_session_and_zero_counters");
                    A(input.Enabled && GetField<Button>(typedForm, "_typedSubmitButton").Enabled &&
                        input.AccessibleDescription.IndexOf("chưa lưu", StringComparison.OrdinalIgnoreCase) >= 0,
                        "submit_failure_typed_reenables_accessible_input");
                    A(GetField<Label>(typedForm, "_support").Text.IndexOf("an toàn", StringComparison.OrdinalIgnoreCase) >= 0,
                        "submit_failure_typed_child_message_is_safe");

                    ExecuteDatabaseSql(typedDatabase, "DROP TRIGGER ui_fail_typed_mastery;");
                    Invoke(typedForm, "SubmitTypedAnswer", "ui_typed_write_recovered");
                    var recovered = Get<object>(coordinator, "Summary");
                    A(Get<int>(recovered, "Attempts") == 1 && Get<int>(recovered, "AnswerAttempts") == 1 &&
                        Get<int>(recovered, "IndependentCorrect") == 1 && Get<int>(recovered, "RetriedQuestions") == 0,
                        "submit_failure_typed_recovery_commits_once_as_independent");
                    A(!input.Enabled && !GetField<Button>(typedForm, "_typedSubmitButton").Enabled,
                        "submit_failure_typed_final_result_locks_input");
                    Invoke(coordinator, "Abort", "submit_failure_typed_cleanup");
                    SetField(typedForm, "_finished", true);
                }

                var interactionDatabase = new LearningDatabase(Path.Combine(tempRoot, "interaction.db"), Path.Combine(schemaDir, "001_initial.sql"));
                var interactionInit = interactionDatabase.Initialize("DELETE");
                A(interactionInit.SchemaVersion == 4 && interactionInit.Health.IsHealthy, "submit_failure_interaction_database_v4_ready");
                new LearnerSessionService(interactionDatabase).EnsurePrimaryChild("Bé UI interaction write recovery");
                CompleteTargetedLessonCorrectly(ctor, interactionDatabase, pointLesson.Id, "submit_failure_interaction_point_prerequisite");
                CompleteTargetedLessonCorrectly(ctor, interactionDatabase, lineLesson.Id, "submit_failure_interaction_line_prerequisite");

                using (var interactionForm = (WAHUKidsLearn.MathLessonForm)ctor.Invoke(new object[]
                {
                    interactionDatabase,
                    new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW },
                    segmentLesson.Id
                }))
                {
                    Invoke(interactionForm, "StartSession");
                    var basic = GetField<MathQuestion>(interactionForm, "_question");
                    var basicInput = GetField<TextBox>(interactionForm, "_typedAnswerBox");
                    A(basic != null && basic.ContentQuestionId == "m2_q_draw_segment_given_length_01",
                        "submit_failure_interaction_starts_with_basic");
                    basicInput.Text = basic.CorrectAnswerDisplay;
                    Invoke(interactionForm, "SubmitTypedAnswer", "submit_failure_interaction_basic");
                    Invoke(interactionForm, "HandleNextButton");

                    var medium = GetField<MathQuestion>(interactionForm, "_question");
                    var interactive = GetField<Control>(interactionForm, "_interactiveAnswer");
                    A(medium != null && medium.ContentQuestionId == "m2_q_draw_segment_given_length_02" &&
                        string.Equals(medium.AnswerKind, "interaction_integer", StringComparison.Ordinal),
                        "submit_failure_interaction_fixture_uses_real_segment_surface");
                    Invoke(interactive, "SelectCursor");
                    Invoke(interactive, "MoveCursor", medium.CorrectAnswer);
                    Invoke(interactive, "SelectCursor");
                    A(Get<int>(interactive, "SelectedLength") == medium.CorrectAnswer,
                        "submit_failure_interaction_draws_correct_segment_before_failure");

                    ExecuteDatabaseSql(interactionDatabase, @"CREATE TRIGGER ui_fail_interaction_mastery
BEFORE INSERT ON mastery_event
BEGIN
    SELECT RAISE(ABORT, 'ui_injected_interaction_commit_failure');
END;");
                    Invoke(interactionForm, "SubmitInteractiveAnswer", "ui_interaction_write_failure");
                    var coordinator = GetField<object>(interactionForm, "_coordinator");
                    var failed = Get<object>(coordinator, "Summary");
                    A(!GetField<bool>(interactionForm, "_finished") && Get<bool>(coordinator, "IsActive") &&
                        Get<bool>(coordinator, "HasOpenQuestion") && Get<int>(failed, "Attempts") == 1 &&
                        Get<int>(failed, "AnswerAttempts") == 1,
                        "submit_failure_interaction_keeps_medium_open_without_ghost_attempt");
                    A(GetField<Button>(interactionForm, "_interactiveSubmitButton").Enabled &&
                        GetField<Label>(interactionForm, "_support").Text.IndexOf("an toàn", StringComparison.OrdinalIgnoreCase) >= 0,
                        "submit_failure_interaction_reenables_submit_with_safe_message");

                    Invoke(interactive, "SelectCursor");
                    A(Get<int>(interactive, "SelectedLength") == 0,
                        "submit_failure_interaction_control_remains_editable_after_failure");
                    Invoke(interactive, "SetQuestion", medium);
                    Invoke(interactive, "SelectCursor");
                    Invoke(interactive, "MoveCursor", medium.CorrectAnswer);
                    Invoke(interactive, "SelectCursor");
                    A(Get<int>(interactive, "SelectedLength") == medium.CorrectAnswer,
                        "submit_failure_interaction_can_redraw_correct_segment");

                    ExecuteDatabaseSql(interactionDatabase, "DROP TRIGGER ui_fail_interaction_mastery;");
                    Invoke(interactionForm, "SubmitInteractiveAnswer", "ui_interaction_write_recovered");
                    var recovered = Get<object>(coordinator, "Summary");
                    A(Get<int>(recovered, "Attempts") == 2 && Get<int>(recovered, "AnswerAttempts") == 2 &&
                        Get<int>(recovered, "IndependentCorrect") == 2 && Get<int>(recovered, "RetriedQuestions") == 0,
                        "submit_failure_interaction_recovery_commits_medium_once_as_independent");
                    A(!GetField<Button>(interactionForm, "_interactiveSubmitButton").Enabled,
                        "submit_failure_interaction_final_result_disables_submit");
                    var lockedLength = Get<int>(interactive, "SelectedLength");
                    Invoke(interactive, "MoveCursor", -1);
                    Invoke(interactive, "SelectCursor");
                    A(Get<int>(interactive, "SelectedLength") == lockedLength,
                        "submit_failure_interaction_final_result_locks_control");

                    Invoke(coordinator, "Abort", "submit_failure_interaction_cleanup");
                    SetField(interactionForm, "_finished", true);
                }
            }
            finally
            {
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }

        private static void TestRetryResumeUiFlow(Assembly appAssembly)
        {
            var repo = Directory.GetCurrentDirectory();
            var sourceContent = Path.Combine(repo, "content_packs", "math_grade2_v1");
            var runtimeContent = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "content_packs", "math_grade2_v1");
            Directory.CreateDirectory(runtimeContent);
            foreach (var name in new[] { "verified_templates_v1.json", "lesson_catalog_v1.json", "question_bank_v1.json" })
                File.Copy(Path.Combine(sourceContent, name), Path.Combine(runtimeContent, name), true);

            var lesson = new MathLessonCatalogSource().Load(Path.Combine(sourceContent, "lesson_catalog_v1.json"))
                .FindLesson("m2_ls_point_recognize");
            A(lesson != null && (lesson.PrerequisiteSkills == null || lesson.PrerequisiteSkills.Count == 0),
                "retry_resume_fixture_root_lesson_unlocked");

            var tempRoot = Path.Combine(Path.GetTempPath(), "wahu-child-ui-retry-resume-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var schemaSource = Path.Combine(repo, "data", "schema");
                var schemaDir = Path.Combine(tempRoot, "schema");
                Directory.CreateDirectory(schemaDir);
                foreach (var source in Directory.GetFiles(schemaSource, "*.sql"))
                    File.Copy(source, Path.Combine(schemaDir, Path.GetFileName(source)), true);
                var database = new LearningDatabase(Path.Combine(tempRoot, "learning.db"), Path.Combine(schemaDir, "001_initial.sql"));
                var init = database.Initialize("DELETE");
                A(init.SchemaVersion == 4 && init.Health.IsHealthy, "retry_resume_database_v4_ready");
                new LearnerSessionService(database).EnsurePrimaryChild("Bé UI retry resume");

                var ctor = typeof(WAHUKidsLearn.MathLessonForm).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(LearningDatabase), typeof(RuntimePerformanceSettings), typeof(string) },
                    null);
                A(ctor != null, "retry_resume_lesson_constructor_available");
                string contentQuestionId;

                using (var first = (WAHUKidsLearn.MathLessonForm)ctor.Invoke(new object[]
                {
                    database,
                    new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW },
                    lesson.Id
                }))
                {
                    Invoke(first, "StartSession");
                    var question = GetField<MathQuestion>(first, "_question");
                    A(question != null && question.DisplayChoices != null && question.DisplayChoices.Count >= 2,
                        "retry_resume_first_question_is_choice");
                    contentQuestionId = question.ContentQuestionId;
                    var correctIndex = -1;
                    for (var i = 0; i < question.DisplayChoices.Count; i++)
                        if (string.Equals(question.DisplayChoices[i], question.CorrectAnswerDisplay, StringComparison.Ordinal)) correctIndex = i;
                    A(correctIndex >= 0, "retry_resume_correct_index_resolves");
                    var wrongIndex = (correctIndex + 1) % question.DisplayChoices.Count;
                    Invoke(first, "SubmitChoice", wrongIndex, "ui_retry_resume_first_wrong");
                    var coordinator = GetField<object>(first, "_coordinator");
                    var pending = Get<object>(coordinator, "Summary");
                    A(GetField<bool>(first, "_retryPending") && Get<int>(pending, "Attempts") == 0 &&
                        Get<int>(pending, "AnswerAttempts") == 1,
                        "retry_resume_first_form_persists_pending_retry");
                    Invoke(coordinator, "Suspend", "ui_retry_resume_fixture");
                    SetField(first, "_finished", true);
                }

                using (var resumed = (WAHUKidsLearn.MathLessonForm)ctor.Invoke(new object[]
                {
                    database,
                    new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW },
                    lesson.Id
                }))
                {
                    Invoke(resumed, "StartSession");
                    var question = GetField<MathQuestion>(resumed, "_question");
                    A(question != null && string.Equals(question.ContentQuestionId, contentQuestionId, StringComparison.Ordinal),
                        "retry_resume_restores_exact_authored_question");
                    A(GetField<bool>(resumed, "_retryPending") &&
                        GetField<Label>(resumed, "_progressText").Text.IndexOf("thử lại", StringComparison.OrdinalIgnoreCase) >= 0,
                        "retry_resume_restores_attempt_two_ui_state");
                    A(GetField<Label>(resumed, "_support").Text.IndexOf("thử lại", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        GetField<Label>(resumed, "_support").Text.IndexOf("gợi ý", StringComparison.OrdinalIgnoreCase) >= 0,
                        "retry_resume_child_message_explains_same_question_retry");

                    var correctIndex = -1;
                    for (var i = 0; i < question.DisplayChoices.Count; i++)
                        if (string.Equals(question.DisplayChoices[i], question.CorrectAnswerDisplay, StringComparison.Ordinal)) correctIndex = i;
                    A(correctIndex >= 0, "retry_resume_restored_correct_index_resolves");
                    var buttons = GetField<Array>(resumed, "_answerButtons");
                    var correctButton = (Control)buttons.GetValue(correctIndex);
                    A(correctButton.Enabled && Get<object>(correctButton, "VisualState").ToString() == "Idle",
                        "retry_resume_restored_choices_are_editable_without_answer_reveal");
                    Invoke(resumed, "SubmitChoice", correctIndex, "ui_retry_resume_correct");

                    var coordinator = GetField<object>(resumed, "_coordinator");
                    var finalized = Get<object>(coordinator, "Summary");
                    A(!GetField<bool>(resumed, "_retryPending") && Get<int>(finalized, "Attempts") == 1 &&
                        Get<int>(finalized, "AnswerAttempts") == 2 && Get<int>(finalized, "RetriedQuestions") == 1 &&
                        Get<int>(finalized, "RetriedCorrect") == 1 && Get<int>(finalized, "IndependentCorrect") == 0,
                        "retry_resume_correct_finishes_same_question_as_assisted");

                    Invoke(coordinator, "Abort", "retry_resume_cleanup");
                    SetField(resumed, "_finished", true);
                }
            }
            finally
            {
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }

        private static void CompleteTargetedLessonCorrectly(ConstructorInfo ctor, LearningDatabase database, string lessonId, string assertionPrefix)
        {
            using (var form = (WAHUKidsLearn.MathLessonForm)ctor.Invoke(new object[]
            {
                database,
                new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW },
                lessonId
            }))
            {
                Invoke(form, "StartSession");
                for (var ordinal = 0; ordinal < 3; ordinal++)
                {
                    var question = GetField<MathQuestion>(form, "_question");
                    A(question != null && question.LessonId == lessonId,
                        assertionPrefix + "_question_" + ordinal);
                    if (string.Equals(question.AnswerKind, "interaction_integer", StringComparison.Ordinal))
                    {
                        var interactive = GetField<Control>(form, "_interactiveAnswer");
                        Invoke(interactive, "SelectCursor");
                        Invoke(interactive, "MoveCursor", question.CorrectAnswer);
                        Invoke(interactive, "SelectCursor");
                        Invoke(form, "SubmitInteractiveAnswer", assertionPrefix);
                    }
                    else if (question.DisplayChoices == null || question.DisplayChoices.Count == 0)
                    {
                        var input = GetField<TextBox>(form, "_typedAnswerBox");
                        input.Text = question.CorrectAnswerDisplay;
                        Invoke(form, "SubmitTypedAnswer", assertionPrefix);
                    }
                    else
                    {
                        var correctIndex = -1;
                        for (var i = 0; i < question.DisplayChoices.Count; i++)
                            if (string.Equals(question.DisplayChoices[i], question.CorrectAnswerDisplay, StringComparison.Ordinal)) correctIndex = i;
                        A(correctIndex >= 0, assertionPrefix + "_correct_index_" + ordinal);
                        Invoke(form, "SubmitChoice", correctIndex, assertionPrefix);
                    }
                    Invoke(form, "HandleNextButton");
                }
                A(GetField<bool>(form, "_finished"), assertionPrefix + "_completed");
            }
        }

        private static void TestInteractionRetryUiFlow(Assembly appAssembly)
        {
            var repo = Directory.GetCurrentDirectory();
            var sourceContent = Path.Combine(repo, "content_packs", "math_grade2_v1");
            var runtimeContent = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "content_packs", "math_grade2_v1");
            Directory.CreateDirectory(runtimeContent);
            foreach (var name in new[] { "verified_templates_v1.json", "lesson_catalog_v1.json", "question_bank_v1.json" })
                File.Copy(Path.Combine(sourceContent, name), Path.Combine(runtimeContent, name), true);

            var catalog = new MathLessonCatalogSource().Load(Path.Combine(sourceContent, "lesson_catalog_v1.json"));
            var pointLesson = catalog.FindLesson("m2_ls_point_recognize");
            var lineLesson = catalog.FindLesson("m2_ls_line_segment_recognize");
            var segmentLesson = catalog.FindLesson("m2_ls_draw_segment_given_length");
            A(pointLesson != null && lineLesson != null && segmentLesson != null,
                "interaction_retry_fixture_lessons_exist");
            A(lineLesson.PrerequisiteSkills.Contains(pointLesson.SkillId) &&
                segmentLesson.PrerequisiteSkills.Contains(lineLesson.SkillId),
                "interaction_retry_fixture_prerequisite_chain");

            var tempRoot = Path.Combine(Path.GetTempPath(), "wahu-child-ui-interaction-retry-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var schemaSource = Path.Combine(repo, "data", "schema");
                var schemaDir = Path.Combine(tempRoot, "schema");
                Directory.CreateDirectory(schemaDir);
                foreach (var source in Directory.GetFiles(schemaSource, "*.sql"))
                    File.Copy(source, Path.Combine(schemaDir, Path.GetFileName(source)), true);
                var database = new LearningDatabase(Path.Combine(tempRoot, "learning.db"), Path.Combine(schemaDir, "001_initial.sql"));
                var init = database.Initialize("DELETE");
                A(init.SchemaVersion == 4 && init.Health.IsHealthy, "interaction_retry_database_v4_ready");
                new LearnerSessionService(database).EnsurePrimaryChild("Bé UI interaction retry");

                var ctor = typeof(WAHUKidsLearn.MathLessonForm).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(LearningDatabase), typeof(RuntimePerformanceSettings), typeof(string) },
                    null);
                A(ctor != null, "interaction_retry_lesson_constructor_available");

                CompleteTargetedLessonCorrectly(ctor, database, pointLesson.Id, "interaction_retry_point_prerequisite");
                CompleteTargetedLessonCorrectly(ctor, database, lineLesson.Id, "interaction_retry_line_prerequisite");

                using (var form = (WAHUKidsLearn.MathLessonForm)ctor.Invoke(new object[]
                {
                    database,
                    new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW },
                    segmentLesson.Id
                }))
                {
                    Invoke(form, "StartSession");
                    var basic = GetField<MathQuestion>(form, "_question");
                    A(basic != null && basic.ContentQuestionId == "m2_q_draw_segment_given_length_01",
                        "interaction_retry_segment_starts_with_basic");
                    var basicInput = GetField<TextBox>(form, "_typedAnswerBox");
                    basicInput.Text = basic.CorrectAnswerDisplay;
                    Invoke(form, "SubmitTypedAnswer", "interaction_retry_basic");
                    Invoke(form, "HandleNextButton");

                    var medium = GetField<MathQuestion>(form, "_question");
                    A(medium != null && medium.ContentQuestionId == "m2_q_draw_segment_given_length_02" &&
                        string.Equals(medium.AnswerKind, "interaction_integer", StringComparison.Ordinal) &&
                        medium.IllustrationData.StartsWith("segmentdraw|", StringComparison.Ordinal),
                        "interaction_retry_medium_is_authored_segment");
                    var interactive = GetField<Control>(form, "_interactiveAnswer");
                    var target = medium.CorrectAnswer;
                    var wrongLength = Math.Max(1, target - 1);
                    Invoke(interactive, "SelectCursor");
                    Invoke(interactive, "MoveCursor", wrongLength);
                    Invoke(interactive, "SelectCursor");
                    A(Get<int>(interactive, "SelectedLength") == wrongLength,
                        "interaction_retry_first_segment_is_intentionally_wrong");
                    Invoke(form, "SubmitInteractiveAnswer", "interaction_first_try_wrong");

                    var coordinator = GetField<object>(form, "_coordinator");
                    var pending = Get<object>(coordinator, "Summary");
                    A(GetField<bool>(form, "_retryPending") && Get<int>(pending, "Attempts") == 1 &&
                        Get<int>(pending, "AnswerAttempts") == 2 && GetField<Button>(form, "_interactiveSubmitButton").Enabled,
                        "interaction_retry_wrong_first_try_keeps_ruler_editable_without_progress");

                    Invoke(interactive, "SelectCursor");
                    Invoke(interactive, "MoveCursor", target);
                    Invoke(interactive, "SelectCursor");
                    A(Get<int>(interactive, "SelectedLength") == target,
                        "interaction_retry_child_can_redraw_correct_segment");
                    Invoke(form, "SubmitInteractiveAnswer", "interaction_retry_correct");

                    var finalized = Get<object>(coordinator, "Summary");
                    A(!GetField<bool>(form, "_retryPending") && Get<int>(finalized, "Attempts") == 2 &&
                        Get<int>(finalized, "AnswerAttempts") == 3 && Get<int>(finalized, "RetriedQuestions") == 1 &&
                        Get<int>(finalized, "RetriedCorrect") == 1 && Get<int>(finalized, "IndependentCorrect") == 1,
                        "interaction_retry_correct_finalizes_medium_once_as_assisted");
                    A(!GetField<Button>(form, "_interactiveSubmitButton").Enabled,
                        "interaction_retry_final_result_disables_submit");
                    var lockedLength = Get<int>(interactive, "SelectedLength");
                    Invoke(interactive, "MoveCursor", -1);
                    Invoke(interactive, "SelectCursor");
                    A(Get<int>(interactive, "SelectedLength") == lockedLength,
                        "interaction_retry_final_result_locks_ruler");

                    Invoke(coordinator, "Abort", "interaction_retry_cleanup");
                    SetField(form, "_finished", true);
                }
            }
            finally
            {
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }

        private static void TestInteractiveSegmentAnswer(Assembly appAssembly)
        {
            var question = new MathQuestion
            {
                QuestionId = "segment_ui_smoke",
                TemplateId = "draw_segment_given_length",
                SkillId = "DRAW_SEGMENT_GIVEN_LENGTH",
                PromptVi = "Vẽ đoạn thẳng AB dài 7 cm bằng cách chọn hai đầu mút trên thước.",
                CorrectAnswer = 7,
                AnswerKind = "interaction_integer",
                CorrectAnswerText = "7",
                Choices = new List<int>(),
                ChoiceTexts = new List<string>(),
                IllustrationData = "segmentdraw|7|15"
            };
            A(string.Equals(question.AnswerKind, "interaction_integer", StringComparison.Ordinal),
                "segment_ui_question_preserves_interaction_kind");
            A(question.DisplayChoices.Count == 0, "segment_ui_question_has_no_fake_choices");
            A(!string.IsNullOrWhiteSpace(question.IllustrationData) &&
                question.IllustrationData.StartsWith("segmentdraw|", StringComparison.Ordinal),
                "segment_ui_question_has_interaction_geometry");

            using (var control = CreateInternalControl(appAssembly, "WAHUKidsLearn.SegmentDrawingAnswerControl"))
            {
                Invoke(control, "SetQuestion", question);
                A(!Get<bool>(control, "HasAnswer"), "segment_interaction_starts_without_answer");
                Invoke(control, "MoveCursor", 2);
                Invoke(control, "SelectCursor");
                Invoke(control, "MoveCursor", question.CorrectAnswer);
                Invoke(control, "SelectCursor");
                A(Get<bool>(control, "HasAnswer"), "segment_interaction_two_endpoints_complete_answer");
                A(Get<int>(control, "SelectedLength") == question.CorrectAnswer, "segment_interaction_computes_absolute_length");
                A(Get<string>(control, "SelectedAnswer") == question.CorrectAnswerDisplay,
                    "segment_interaction_serializes_integer_answer");
                Invoke(control, "SetHintLevel", 2);
                RenderAndAssert(control, 620, 154, "segment_interaction_hint2");
                Invoke(control, "ShowResult", true);
                RenderAndAssert(control, 620, 154, "segment_interaction_correct_locked");
            }

            var tempRoot = Path.Combine(Path.GetTempPath(), "wahu-child-ui-interactive-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var schema = Path.Combine(Directory.GetCurrentDirectory(), "data", "schema", "001_initial.sql");
                var database = new LearningDatabase(Path.Combine(tempRoot, "learning.db"), schema);
                using (var form = new WAHUKidsLearn.MathLessonForm(database, new RuntimePerformanceSettings { Profile = PerformanceProfileKind.LOW }))
                {
                    Invoke(form, "ConfigureAnswerInput", question);
                    var support = GetField<Label>(form, "_support");
                    var submit = GetField<Button>(form, "_interactiveSubmitButton");
                    A(support.Text.IndexOf("điểm A", StringComparison.OrdinalIgnoreCase) >= 0,
                        "segment_form_switches_to_interactive_guidance");
                    A(!submit.Enabled, "segment_submit_disabled_until_two_endpoints");

                    var interactive = GetField<Control>(form, "_interactiveAnswer");
                    Invoke(interactive, "MoveCursor", 1);
                    Invoke(interactive, "SelectCursor");
                    Invoke(interactive, "MoveCursor", question.CorrectAnswer);
                    Invoke(interactive, "SelectCursor");
                    A(submit.Enabled, "segment_submit_enabled_after_valid_segment");
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

        private static T Get<T>(object target, string property)
        {
            var info = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) throw new MissingMemberException(target.GetType().FullName, property);
            return (T)info.GetValue(target, null);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var info = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) throw new MissingFieldException(target.GetType().FullName, fieldName);
            return (T)info.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var info = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) throw new MissingFieldException(target.GetType().FullName, fieldName);
            info.SetValue(target, value);
        }

        private static void ExecuteDatabaseSql(LearningDatabase database, string sql)
        {
            var open = typeof(LearningDatabase).GetMethod("OpenConnection", BindingFlags.Instance | BindingFlags.Public);
            if (open == null) throw new MissingMethodException(typeof(LearningDatabase).FullName, "OpenConnection");
            var connection = open.Invoke(database, null) as IDisposable;
            if (connection == null) throw new InvalidOperationException("LearningDatabase.OpenConnection did not return IDisposable connection.");
            try
            {
                var createCommand = connection.GetType().GetMethod("CreateCommand", Type.EmptyTypes);
                if (createCommand == null) throw new MissingMethodException(connection.GetType().FullName, "CreateCommand");
                var commandObject = createCommand.Invoke(connection, null);
                var command = commandObject as IDisposable;
                if (command == null) throw new InvalidOperationException("SQLite command is not disposable.");
                try
                {
                    var commandText = commandObject.GetType().GetProperty("CommandText", BindingFlags.Instance | BindingFlags.Public);
                    if (commandText == null) throw new MissingMemberException(commandObject.GetType().FullName, "CommandText");
                    commandText.SetValue(commandObject, sql, null);
                    var execute = commandObject.GetType().GetMethod("ExecuteNonQuery", Type.EmptyTypes);
                    if (execute == null) throw new MissingMethodException(commandObject.GetType().FullName, "ExecuteNonQuery");
                    execute.Invoke(commandObject, null);
                }
                finally
                {
                    command.Dispose();
                }
            }
            finally
            {
                connection.Dispose();
            }
        }

        private static bool ContainsControlText(Control root, string needle)
        {
            if (root == null || string.IsNullOrWhiteSpace(needle)) return false;
            if (!string.IsNullOrWhiteSpace(root.Text) && root.Text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            foreach (Control child in root.Controls)
                if (ContainsControlText(child, needle)) return true;
            return false;
        }

        private static Button FindFirstButton(Control root)
        {
            if (root == null) return null;
            var button = root as Button;
            if (button != null) return button;
            foreach (Control child in root.Controls)
            {
                var found = FindFirstButton(child);
                if (found != null) return found;
            }
            return null;
        }

        private static Button FindButtonContaining(Control root, string text)
        {
            if (root == null || string.IsNullOrWhiteSpace(text)) return null;
            var button = root as Button;
            if (button != null && !string.IsNullOrWhiteSpace(button.Text) &&
                button.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return button;
            foreach (Control child in root.Controls)
            {
                var found = FindButtonContaining(child, text);
                if (found != null) return found;
            }
            return null;
        }

        private static void RenderFormAndAssert(Form form, int width, int height, string name)
        {
            A(width >= form.MinimumSize.Width && height >= form.MinimumSize.Height, name + "_meets_minimum_size");
            form.ClientSize = new Size(width, height);
            CreateAndLayoutTree(form);
            A(form.Controls.Count > 0, name + "_has_root_control");
            var root = form.Controls[0];
            A(root.Width > 0 && root.Height > 0, name + "_root_positive_bounds");
            A(Math.Abs(root.Width - form.ClientSize.Width) <= 2, name + "_root_fills_width");
            A(Math.Abs(root.Height - form.ClientSize.Height) <= 2, name + "_root_fills_height");
            A(CountSizedControls(root) >= 15, name + "_keeps_sized_layout_tree");
        }

        private static void CreateAndLayoutTree(Control root)
        {
            if (root == null) return;
            root.CreateControl();
            root.PerformLayout();
            foreach (Control child in root.Controls) CreateAndLayoutTree(child);
            root.PerformLayout();
        }

        private static int CountSizedControls(Control root)
        {
            if (root == null) return 0;
            var count = root.Width > 0 && root.Height > 0 ? 1 : 0;
            foreach (Control child in root.Controls) count += CountSizedControls(child);
            return count;
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

        private static MathQuestion QWP(string template, string skill, string prompt, int answer, string illustrationData)
        {
            return new MathQuestion
            {
                TemplateId = template,
                SkillId = skill,
                PromptVi = prompt,
                CorrectAnswer = answer,
                AnswerKind = "integer",
                CorrectAnswerText = answer.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Choices = new[] { answer, answer + 1, Math.Max(0, answer - 1), answer + 2 },
                ChoiceTexts = new[] { answer.ToString(), (answer + 1).ToString(), Math.Max(0, answer - 1).ToString(), (answer + 2).ToString() },
                IllustrationData = illustrationData,
                Representation = "word_problem_model",
                HintLevel1 = "hint 1",
                HintLevel2 = "hint 2"
            };
        }
        private static MathQuestion QV(string template, string skill, string prompt, string answer, string illustrationData, string representation)
        {
            return new MathQuestion
            {
                TemplateId = template,
                SkillId = skill,
                PromptVi = prompt,
                AnswerKind = "text",
                CorrectAnswerText = answer,
                ChoiceTexts = new[] { answer, "lựa chọn B", "lựa chọn C" },
                IllustrationData = illustrationData,
                Representation = representation,
                HintLevel1 = "hint 1",
                HintLevel2 = "hint 2"
            };
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
