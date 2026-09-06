using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
            Set(summary, "HintedCorrect", 2);
            Set(summary, "Wrong", 2);
            Set(summary, "DistinctSkills", 4);
            Set(summary, "GardenGrowthSteps", 3);
            Set(summary, "GardenUnlockMessage", "Mở khóa: Bồn hoa.");

            var performance = (string)performanceMethod.Invoke(null, new[] { summary });
            A(performance.IndexOf("Đã làm 8", StringComparison.OrdinalIgnoreCase) >= 0 &&
                performance.IndexOf("Tự làm đúng 4", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_completion_shows_independent_correct_count");
            A(performance.IndexOf("Đúng nhờ gợi ý 2", StringComparison.OrdinalIgnoreCase) >= 0 &&
                performance.IndexOf("Cần luyện lại 2", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_completion_shows_hinted_and_practice_counts");

            var support = (string)supportMethod.Invoke(null, new[] { summary });
            A(support.IndexOf("4 kỹ năng", StringComparison.OrdinalIgnoreCase) >= 0 &&
                support.IndexOf("Khu vườn", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_completion_support_shows_skills_and_garden_progress");
            A(support.IndexOf("Mở khóa: Bồn hoa", StringComparison.OrdinalIgnoreCase) >= 0,
                "math_completion_support_shows_unlock_message");

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

        private static void TestInteractiveSegmentAnswer(Assembly appAssembly)
        {
            var generator = new MathQuestionGenerator(314159);
            var question = generator.Generate(new MathSelectionDecision
            {
                Template = new MathTemplateRef
                {
                    TemplateId = "draw_segment_given_length",
                    SkillId = "DRAW_SEGMENT_GIVEN_LENGTH"
                },
                DifficultyFit = 0.5
            });
            A(string.Equals(question.AnswerKind, "interaction_integer", StringComparison.Ordinal),
                "segment_generated_question_preserves_interaction_kind");
            A(question.DisplayChoices.Count == 0, "segment_generated_question_has_no_fake_choices");
            A(!string.IsNullOrWhiteSpace(question.IllustrationData) &&
                question.IllustrationData.StartsWith("segmentdraw|", StringComparison.Ordinal),
                "segment_generated_question_has_interaction_geometry");

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
