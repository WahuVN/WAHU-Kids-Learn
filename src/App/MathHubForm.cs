using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WAHU.Content;
using WAHU.Data;
using WAHU.Learning;
using WAHU.Performance;
using WAHU.Session;

namespace WAHUKidsLearn
{
    public sealed class MathHubForm : Form
    {
        private readonly LearningDatabase _database;
        private readonly RuntimePerformanceSettings _performance;
        private readonly string _catalogPath;
        private readonly Dictionary<string, Button> _chapterButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> _lessonButtons = new Dictionary<string, Button>(StringComparer.Ordinal);

        private MathLessonCatalogSnapshot _catalog;
        private IDictionary<string, SkillSnapshot> _skills = new Dictionary<string, SkillSnapshot>(StringComparer.Ordinal);
        private IDictionary<string, MathLessonAccessSnapshot> _lessonAccess = new Dictionary<string, MathLessonAccessSnapshot>(StringComparer.Ordinal);
        private MathChapterDescriptor _selectedChapter;
        private MathLessonDescriptor _selectedLesson;
        private FlowLayoutPanel _chapterFlow;
        private FlowLayoutPanel _lessonFlow;
        private FlowLayoutPanel _detailFlow;
        private Label _summary;
        private Label _detailEmpty;
        private ChildActionButton _continueLessonButton;
        private ChildActionButton _rescueButton;
        private ChildActionButton _missionButton;
        private MathLessonDescriptor _continueLesson;
        private bool _continueResumesSession;

        public MathHubForm(LearningDatabase database, RuntimePerformanceSettings performance)
            : this(database, performance, ResolveCatalogPath())
        {
        }

        internal MathHubForm(LearningDatabase database, RuntimePerformanceSettings performance, string catalogPath)
        {
            _database = database ?? throw new ArgumentNullException("database");
            _performance = performance;
            if (string.IsNullOrWhiteSpace(catalogPath)) throw new ArgumentException("catalogPath");
            _catalogPath = catalogPath;

            Text = "Toán lớp 2";
            AccessibleName = "Thư viện Toán lớp 2";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 640);
            ClientSize = new Size(1180, 760);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = ChildVisualTheme.Cream;
            Font = ChildVisualTheme.Font(10.5f);
            KeyPreview = true;
            DoubleBuffered = true;
            BuildUi();
            Shown += delegate { LoadCatalogAndProgress(); };
        }

        internal int ChapterCount { get { return _catalog == null || _catalog.Chapters == null ? 0 : _catalog.Chapters.Count; } }
        internal int TopicCount { get { return _catalog == null || _catalog.Topics == null ? 0 : _catalog.Topics.Count; } }
        internal int LessonCount { get { return _catalog == null || _catalog.Lessons == null ? 0 : _catalog.Lessons.Count; } }
        internal string SelectedLessonId { get { return _selectedLesson == null ? null : _selectedLesson.Id; } }
        internal string ContinueLessonId { get { return _continueLesson == null ? null : _continueLesson.Id; } }

        internal void LoadCatalogAndProgress()
        {
            try
            {
                _catalog = new MathLessonCatalogSource().Load(_catalogPath);
                RefreshSkillProgress();
                RefreshLessonAccess();
                _summary.Text = _catalog.Chapters.Count + " chương  •  " + _catalog.Topics.Count + " chủ đề  •  " +
                    _catalog.Lessons.Count + " bài học";
                PopulateChapters();
                RefreshContinueLessonState();
                if (_continueLesson != null) SelectLessonInCatalog(_continueLesson);
                else if (_catalog.Chapters.Count > 0) SelectChapter(_catalog.Chapters[0]);
            }
            catch
            {
                _catalog = null;
                _selectedChapter = null;
                _selectedLesson = null;
                _chapterButtons.Clear();
                _lessonButtons.Clear();
                _skills.Clear();
                _lessonAccess.Clear();
                _chapterFlow.Controls.Clear();
                _lessonFlow.Controls.Clear();
                _detailFlow.Controls.Clear();
                _detailFlow.Visible = false;
                _continueLesson = null;
                _continueResumesSession = false;
                if (_continueLessonButton != null)
                {
                    _continueLessonButton.Text = "Chưa có bài đang học";
                    _continueLessonButton.AccessibleName = "Chưa có bài Toán đang học";
                    _continueLessonButton.Enabled = false;
                    _continueLessonButton.AccessibleDescription = "Chưa có bài học đang học dở để tiếp tục.";
                }
                _summary.Text = "Nội dung bài học đang cần được kiểm tra lại.";
                _detailEmpty.Text = "Chưa thể mở thư viện bài học lúc này. Con vẫn có thể làm nhiệm vụ Toán hôm nay.";
                _detailEmpty.Visible = true;
            }
        }

        private void BuildUi()
        {
            var root = new ChildSceneLayout
            {
                Dock = DockStyle.Fill,
                BackColor = ChildVisualTheme.Cream,
                Padding = new Padding(22, 18, 22, 18),
                ColumnCount = 1,
                RowCount = 3
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildCatalogBody(), 0, 1);
            root.Controls.Add(BuildFooter(), 0, 2);
            Controls.Add(root);

            Resize += delegate
            {
                ResizeNavigationButtons();
                ResizeDetailChildren();
            };
        }

        private Control BuildHeader()
        {
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));

            var back = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 12, 8),
                Text = "← Về nhà",
                FillColor = Color.FromArgb(232, 230, 220),
                HoverColor = Color.FromArgb(220, 218, 207),
                PressedColor = Color.FromArgb(208, 205, 194),
                TextColor = ChildVisualTheme.Ink,
                Radius = 16,
                AccessibleName = "Đóng thư viện Toán và về trang chính"
            };
            back.Click += delegate { Close(); };
            header.Controls.Add(back, 0, 0);

            var title = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            title.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            title.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
            title.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Toán lớp 2",
                TextAlign = ContentAlignment.BottomLeft,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(25f, FontStyle.Bold),
                AccessibleName = "Toán lớp 2"
            }, 0, 0);
            _summary = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Đang mở thư viện bài học…",
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9.8f),
                AccessibleName = "Tóm tắt chương trình Toán"
            };
            title.Controls.Add(_summary, 0, 1);
            header.Controls.Add(title, 1, 0);

            var grade = new Label
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(12, 12, 0, 12),
                Text = "LỚP 2",
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = ChildVisualTheme.Mint,
                ForeColor = ChildVisualTheme.MintStrong,
                Font = ChildVisualTheme.Font(11f, FontStyle.Bold),
                AccessibleName = "Chương trình lớp 2"
            };
            header.Controls.Add(grade, 2, 0);
            return header;
        }

        private Control BuildCatalogBody()
        {
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));

            _chapterFlow = CreateColumnFlow();
            _lessonFlow = CreateColumnFlow();
            _detailFlow = CreateColumnFlow();
            _detailFlow.Padding = new Padding(14, 10, 14, 14);
            _detailFlow.Visible = false;

            body.Controls.Add(BuildColumnCard("CHƯƠNG", "Chọn một phần để xem các bài học.", _chapterFlow,
                new Padding(0, 6, 8, 6), ChildVisualTheme.SkyStrong, ChildVisualTheme.Sky), 0, 0);
            body.Controls.Add(BuildColumnCard("BÀI HỌC", "Chọn bài để đọc mục tiêu, kiến thức và ví dụ.", _lessonFlow,
                new Padding(4, 6, 8, 6), ChildVisualTheme.LavenderStrong, ChildVisualTheme.Lavender), 1, 0);

            var detailHost = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 6, 0, 6),
                Padding = new Padding(6),
                CardColor = ChildVisualTheme.Blend(ChildVisualTheme.Peach, Color.White, 0.76f),
                BorderColor = ChildVisualTheme.Blend(ChildVisualTheme.Peach, ChildVisualTheme.PeachStrong, 0.30f),
                Radius = 22
            };
            _detailEmpty = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Chọn một bài học để xem nội dung.",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(11f),
                AccessibleName = "Trạng thái nội dung bài học"
            };
            detailHost.Controls.Add(_detailEmpty);
            detailHost.Controls.Add(_detailFlow);
            _detailFlow.BringToFront();
            body.Controls.Add(detailHost, 2, 0);
            return body;
        }

        private Control BuildFooter()
        {
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Padding = new Padding(4, 8, 2, 2) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
            footer.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Con có thể học theo bài, chơi một nhiệm vụ cứu hộ ngắn hoặc làm nhiệm vụ thích ứng.",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9.2f),
                Padding = new Padding(8, 0, 8, 0),
                AccessibleName = "Các cách luyện Toán"
            }, 0, 0);

            _continueLessonButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(5, 2, 5, 2),
                Text = "Chưa có bài đang học",
                FillColor = Color.FromArgb(226, 239, 247),
                HoverColor = Color.FromArgb(210, 230, 241),
                PressedColor = Color.FromArgb(194, 219, 233),
                TextColor = ChildVisualTheme.SkyStrong,
                Font = ChildVisualTheme.Font(9.5f, FontStyle.Bold),
                Radius = 18,
                AccessibleName = "Tiếp tục bài Toán đang học",
                AccessibleDescription = "Chưa có bài học đang học dở để tiếp tục.",
                Enabled = false
            };
            _continueLessonButton.Click += delegate { ContinueCurrentLesson(); };
            footer.Controls.Add(_continueLessonButton, 1, 0);

            _rescueButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(5, 2, 5, 2),
                Text = "Toán nhanh — Cứu hộ",
                BadgeText = "3",
                FillColor = Color.FromArgb(238, 209, 154),
                HoverColor = Color.FromArgb(231, 194, 129),
                PressedColor = Color.FromArgb(219, 178, 111),
                TextColor = Color.FromArgb(104, 73, 34),
                Font = ChildVisualTheme.Font(10.2f, FontStyle.Bold),
                Radius = 18,
                AccessibleName = "Mở Toán nhanh — Nhiệm vụ cứu hộ",
                AccessibleDescription = "Mở nhiệm vụ ba chặng cho năm bài Toán đầu. Không có đồng hồ đếm ngược."
            };
            _rescueButton.Click += delegate { OpenQuickRescue(); };
            footer.Controls.Add(_rescueButton, 2, 0);

            var adaptiveQuestionCount = MathSessionCoordinator.DefaultTargetQuestionCount;
            var adaptiveQuestionCountText = adaptiveQuestionCount.ToString();
            _missionButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(5, 2, 0, 2),
                Text = "Luyện " + adaptiveQuestionCountText + " câu hôm nay",
                BadgeText = adaptiveQuestionCountText,
                FillColor = ChildVisualTheme.MintStrong,
                HoverColor = Color.FromArgb(90, 156, 103),
                PressedColor = Color.FromArgb(75, 139, 88),
                Font = ChildVisualTheme.Font(10.2f, FontStyle.Bold),
                Radius = 18,
                AccessibleName = "Bắt đầu nhiệm vụ Toán hôm nay",
                AccessibleDescription = "Mở nhiệm vụ Toán thích ứng gồm khoảng " + adaptiveQuestionCountText + " câu."
            };
            _missionButton.Click += delegate { OpenAdaptiveMission(); };
            footer.Controls.Add(_missionButton, 3, 0);
            return footer;
        }

        private static FlowLayoutPanel CreateColumnFlow()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown,
                BackColor = Color.Transparent,
                Padding = new Padding(6)
            };
        }

        private static Control BuildColumnCard(string title, string subtitle, Control content, Padding margin, Color accent, Color surface)
        {
            var card = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = margin,
                Padding = new Padding(10),
                CardColor = ChildVisualTheme.Blend(surface, Color.White, 0.70f),
                BorderColor = ChildVisualTheme.Blend(surface, accent, 0.28f),
                Radius = 22
            };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = title,
                TextAlign = ContentAlignment.BottomLeft,
                ForeColor = accent,
                Font = ChildVisualTheme.Font(9f, FontStyle.Bold)
            }, 0, 0);
            layout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = subtitle,
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(8.7f),
                Padding = new Padding(0, 4, 0, 4)
            }, 0, 1);
            layout.Controls.Add(content, 0, 2);
            card.Controls.Add(layout);
            return card;
        }

        private void PopulateChapters()
        {
            _chapterFlow.SuspendLayout();
            try
            {
                _chapterFlow.Controls.Clear();
                _chapterButtons.Clear();
                for (var i = 0; i < _catalog.Chapters.Count; i++)
                {
                    var chapter = _catalog.Chapters[i];
                    var lessonCount = _catalog.Lessons.Count(x => string.Equals(x.ChapterId, chapter.Id, StringComparison.Ordinal));
                    var progress = ChapterProgressText(chapter, lessonCount);
                    var captured = chapter;
                    var button = CreateCatalogButton((i + 1) + ". " + chapter.TitleVi + "\r\n" + progress, 72);
                    button.AccessibleName = "Chương " + (i + 1) + ": " + chapter.TitleVi;
                    button.AccessibleDescription = progress + ". Nhấn Enter để mở chương.";
                    button.Click += delegate { SelectChapter(captured); };
                    _chapterButtons[chapter.Id] = button;
                    _chapterFlow.Controls.Add(button);
                }
            }
            finally
            {
                _chapterFlow.ResumeLayout();
                ResizeNavigationButtons();
            }
        }

        private void SelectChapter(MathChapterDescriptor chapter)
        {
            if (chapter == null) return;
            _selectedChapter = chapter;
            foreach (var pair in _chapterButtons)
            {
                var selected = string.Equals(pair.Key, chapter.Id, StringComparison.Ordinal);
                var chapterIndex = ChapterIndex(pair.Key);
                var tone = ChapterTone(chapterIndex);
                var fill = ChildVisualTheme.Blend(tone, Color.White, selected ? 0.14f : 0.46f);
                var ink = selected ? ChapterAccent(chapterIndex) : ChildVisualTheme.Ink;
                var border = ChildVisualTheme.Blend(tone, ChapterAccent(chapterIndex), selected ? 0.52f : 0.20f);
                var themed = pair.Value as ChildActionButton;
                if (themed != null)
                {
                    themed.FillColor = fill;
                    themed.HoverColor = ChildVisualTheme.Blend(fill, ChapterAccent(chapterIndex), 0.10f);
                    themed.PressedColor = ChildVisualTheme.Blend(fill, ChapterAccent(chapterIndex), 0.18f);
                    themed.TextColor = ink;
                    themed.BorderColor = border;
                    themed.BorderThickness = selected ? 2f : 1f;
                    themed.Depth = selected ? 4 : 3;
                    themed.Invalidate();
                }
                else
                {
                    pair.Value.BackColor = fill;
                    pair.Value.ForeColor = ink;
                    pair.Value.FlatAppearance.BorderColor = border;
                }
            }

            _lessonFlow.SuspendLayout();
            try
            {
                _lessonFlow.Controls.Clear();
                _lessonButtons.Clear();
                foreach (var topic in _catalog.TopicsForChapter(chapter.Id))
                {
                    var topicTitle = new Label
                    {
                        AutoSize = false,
                        Height = 42,
                        Margin = new Padding(4, 12, 4, 3),
                        Text = topic.TitleVi,
                        TextAlign = ContentAlignment.MiddleLeft,
                        ForeColor = ChildVisualTheme.SkyStrong,
                        Font = ChildVisualTheme.Font(9.5f, FontStyle.Bold),
                        AccessibleName = "Chủ đề " + topic.TitleVi
                    };
                    _lessonFlow.Controls.Add(topicTitle);
                    foreach (var lesson in _catalog.LessonsForTopic(topic.Id))
                    {
                        var captured = lesson;
                        var state = LessonStateText(lesson);
                        var button = CreateCatalogButton(lesson.TitleVi + "\r\n" + state, 66);
                        ApplyLessonStateStyle(button, lesson);
                        button.AccessibleName = "Bài " + lesson.TitleVi;
                        button.AccessibleDescription = state + ". Nhấn Enter để xem nội dung bài.";
                        button.Click += delegate { SelectLesson(captured); };
                        _lessonButtons[lesson.Id] = button;
                        _lessonFlow.Controls.Add(button);
                    }
                }
            }
            finally
            {
                _lessonFlow.ResumeLayout();
                ResizeNavigationButtons();
            }

            var first = _catalog.Lessons
                .Where(x => string.Equals(x.ChapterId, chapter.Id, StringComparison.Ordinal))
                .OrderBy(x => _catalog.Topics.IndexOf(_catalog.FindTopic(x.TopicId)))
                .ThenBy(x => x.OrderInDomain)
                .FirstOrDefault();
            if (first != null) SelectLesson(first);
        }

        private void SelectLesson(MathLessonDescriptor lesson)
        {
            if (lesson == null) return;
            _selectedLesson = lesson;
            foreach (var pair in _lessonButtons)
            {
                var button = pair.Value;
                var descriptor = _catalog.FindLesson(pair.Key);
                if (descriptor == null) continue;
                ApplyLessonStateStyle(button, descriptor);
                var selected = string.Equals(pair.Key, lesson.Id, StringComparison.Ordinal);
                var themed = button as ChildActionButton;
                if (themed != null)
                {
                    themed.BorderThickness = selected ? 2f : 1f;
                    themed.BorderColor = selected ? ChildVisualTheme.MintStrong : Color.FromArgb(222, 219, 205);
                    themed.Depth = selected ? 4 : 3;
                    themed.Invalidate();
                }
                else
                {
                    button.FlatAppearance.BorderSize = selected ? 2 : 1;
                    button.FlatAppearance.BorderColor = selected ? ChildVisualTheme.MintStrong : Color.FromArgb(222, 219, 205);
                }
            }
            RenderLessonDetail(lesson);
        }

        private void RenderLessonDetail(MathLessonDescriptor lesson)
        {
            _detailEmpty.Visible = false;
            _detailFlow.Visible = true;
            _detailFlow.SuspendLayout();
            try
            {
                _detailFlow.Controls.Clear();
                var topic = _catalog.FindTopic(lesson.TopicId);
                AddDetailLabel(topic == null ? "BÀI HỌC" : topic.TitleVi.ToUpperInvariant(), 9f, FontStyle.Bold,
                    ChildVisualTheme.SkyStrong, 26, ContentAlignment.MiddleLeft);
                AddDetailLabel(lesson.TitleVi, 18f, FontStyle.Bold, ChildVisualTheme.Ink, 0, ContentAlignment.MiddleLeft);

                var snapshot = Skill(lesson.SkillId);
                var state = LessonStateText(lesson);
                var stateLabel = AddDetailLabel(state, 9.3f, FontStyle.Bold, LessonStateColor(lesson), 32, ContentAlignment.MiddleCenter);
                stateLabel.BackColor = LessonStateBackground(lesson);
                stateLabel.Padding = new Padding(10, 4, 10, 4);
                stateLabel.AccessibleName = "Tiến độ bài học: " + state;

                AddSection("Mục tiêu");
                AddBody(Bullets(lesson.ObjectivesVi));

                AddSection("Kiến thức cần nhớ");
                AddBody(lesson.ExplanationVi);
                foreach (var concept in lesson.Concepts ?? new List<MathConceptDescriptor>())
                {
                    var conceptTitle = AddDetailLabel(concept.NameVi, 10.3f, FontStyle.Bold, ChildVisualTheme.MintStrong, 0, ContentAlignment.MiddleLeft);
                    conceptTitle.Margin = new Padding(8, 4, 8, 0);
                    AddBody(concept.DefinitionVi);
                }

                AddSection("Ví dụ có lời giải");
                foreach (var example in lesson.WorkedExamples ?? new List<MathWorkedExampleDescriptor>())
                {
                    AddBody(example.PromptVi, true);
                    var numbered = new List<string>();
                    var steps = example.SolutionStepsVi ?? new List<string>();
                    for (var i = 0; i < steps.Count; i++) numbered.Add((i + 1) + ". " + steps[i]);
                    AddBody(string.Join("\r\n", numbered));
                    var answer = AddDetailLabel("Đáp án: " + example.Answer, 10f, FontStyle.Bold, ChildVisualTheme.MintStrong, 34, ContentAlignment.MiddleLeft);
                    answer.BackColor = Color.FromArgb(229, 244, 227);
                    answer.Padding = new Padding(10, 5, 10, 5);
                }

                AddSection("Luyện tập");
                var practiceCount = lesson.PracticeSets == null ? 0 : lesson.PracticeSets.TotalCount;
                AddBody(practiceCount + " câu trong ngân hàng bài học: cơ bản, vừa sức và vận dụng.");
                var access = LessonAccess(lesson.Id);
                _detailFlow.Controls.Add(CreateLessonPracticeButton(lesson, access));
                if (access != null && access.IsCompleted && access.LastScorePercent.HasValue)
                {
                    var scoreText = "Lần gần nhất: " + Math.Round(access.LastScorePercent.Value) + "%";
                    if (access.BestScorePercent.HasValue)
                        scoreText += "  •  Tốt nhất: " + Math.Round(access.BestScorePercent.Value) + "%";
                    AddBody(scoreText, true);
                }

                var prerequisites = lesson.PrerequisiteSkills ?? new List<string>();
                if (prerequisites.Count > 0)
                {
                    AddSection("Nên học trước");
                    var names = prerequisites.Select(PrerequisiteName).ToList();
                    AddBody(Bullets(names));
                }
                else
                {
                    AddSection("Bắt đầu từ đây");
                    AddBody("Bài này không yêu cầu bài học trước trong chương trình lớp 2.");
                }

                if (snapshot != null && snapshot.NextReviewAtUtc.HasValue)
                {
                    AddSection("Lần ôn tiếp theo");
                    AddBody(ReviewText(snapshot.NextReviewAtUtc.Value));
                }
            }
            finally
            {
                _detailFlow.ResumeLayout();
                ResizeDetailChildren();
                _detailFlow.AutoScrollPosition = Point.Empty;
            }
        }

        private ChildActionButton CreateLessonPracticeButton(MathLessonDescriptor lesson, MathLessonAccessSnapshot access)
        {
            var button = new ChildActionButton
            {
                AutoSize = false,
                Height = 54,
                Margin = new Padding(8, 8, 8, 8),
                FillColor = ChildVisualTheme.MintStrong,
                HoverColor = Color.FromArgb(90, 156, 103),
                PressedColor = Color.FromArgb(75, 139, 88),
                Font = ChildVisualTheme.Font(10.5f, FontStyle.Bold),
                Radius = 16,
                AccessibleName = "Luyện tập bài " + lesson.TitleVi
            };

            if (access == null)
            {
                button.Text = "Chưa thể mở luyện tập bài này";
                button.Enabled = false;
                button.AccessibleDescription = "Tiến độ bài học chưa sẵn sàng. Nội dung lý thuyết vẫn có thể xem.";
                return button;
            }

            if (!access.IsUnlocked)
            {
                var missing = (access.UnsatisfiedPrerequisiteLessonIds ?? new List<string>())
                    .Select(LessonTitle)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                button.Text = "Học bài trước để mở luyện tập";
                button.Enabled = false;
                button.FillColor = Color.FromArgb(226, 224, 216);
                button.TextColor = ChildVisualTheme.MutedInk;
                button.AccessibleDescription = missing.Count == 0
                    ? "Bài luyện tập đang khóa vì còn bài học cần hoàn thành trước."
                    : "Cần hoàn thành trước: " + string.Join(", ", missing) + ".";
                return button;
            }

            button.Text = access.IsCompleted ? "Luyện lại bài này" : "Luyện bài này";
            button.BadgeText = string.Empty;
            button.AccessibleDescription = access.IsCompleted
                ? "Mở lại bài luyện tập " + lesson.TitleVi + "."
                : "Bắt đầu bài luyện tập " + lesson.TitleVi + ".";
            button.Click += delegate { OpenLessonPractice(lesson); };
            return button;
        }

        private string LessonTitle(string lessonId)
        {
            var lesson = _catalog == null ? null : _catalog.FindLesson(lessonId);
            return lesson == null ? lessonId : lesson.TitleVi;
        }

        private int ChapterIndex(string chapterId)
        {
            if (_catalog == null || _catalog.Chapters == null) return 0;
            for (var i = 0; i < _catalog.Chapters.Count; i++)
            {
                if (string.Equals(_catalog.Chapters[i].Id, chapterId, StringComparison.Ordinal)) return i;
            }
            return 0;
        }

        private static Color ChapterTone(int index)
        {
            switch (Math.Abs(index) % 4)
            {
                case 1: return ChildVisualTheme.Mint;
                case 2: return ChildVisualTheme.Peach;
                case 3: return ChildVisualTheme.Lavender;
                default: return ChildVisualTheme.Sky;
            }
        }

        private static Color ChapterAccent(int index)
        {
            switch (Math.Abs(index) % 4)
            {
                case 1: return ChildVisualTheme.MintStrong;
                case 2: return ChildVisualTheme.PeachStrong;
                case 3: return ChildVisualTheme.LavenderStrong;
                default: return ChildVisualTheme.SkyStrong;
            }
        }

        private Button CreateCatalogButton(string text, int height)
        {
            return new ChildActionButton
            {
                AutoSize = false,
                Height = height,
                Margin = new Padding(4, 4, 4, 4),
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 6, 10, 6),
                FillColor = Color.FromArgb(247, 249, 244),
                HoverColor = Color.FromArgb(236, 246, 241),
                PressedColor = Color.FromArgb(226, 239, 232),
                TextColor = ChildVisualTheme.Ink,
                BorderColor = ChildVisualTheme.Line,
                BorderThickness = 1f,
                Depth = 3,
                Radius = 16,
                Font = ChildVisualTheme.Font(9.5f, FontStyle.Bold),
                TabStop = true
            };
        }

        private void ApplyLessonStateStyle(Button button, MathLessonDescriptor lesson)
        {
            var fill = LessonStateBackground(lesson);
            var ink = LessonStateColor(lesson);
            var themed = button as ChildActionButton;
            if (themed != null)
            {
                themed.FillColor = fill;
                themed.HoverColor = ChildVisualTheme.Blend(fill, ink, 0.08f);
                themed.PressedColor = ChildVisualTheme.Blend(fill, ink, 0.15f);
                themed.TextColor = ink;
                themed.DisabledFillColor = ChildVisualTheme.Blend(fill, Color.FromArgb(235, 236, 231), 0.34f);
                themed.DisabledTextColor = ChildVisualTheme.MutedInk;
                themed.Invalidate();
                return;
            }
            button.BackColor = fill;
            button.ForeColor = ink;
        }

        private Color LessonStateBackground(MathLessonDescriptor lesson)
        {
            var access = lesson == null ? null : LessonAccess(lesson.Id);
            if (access != null && !access.IsUnlocked) return Color.FromArgb(235, 234, 229);
            if (access != null && access.IsCompleted) return Color.FromArgb(225, 243, 224);
            return StateBackground(lesson == null ? null : Skill(lesson.SkillId));
        }

        private Color LessonStateColor(MathLessonDescriptor lesson)
        {
            var access = lesson == null ? null : LessonAccess(lesson.Id);
            if (access != null && !access.IsUnlocked) return Color.FromArgb(132, 130, 124);
            if (access != null && access.IsCompleted) return ChildVisualTheme.MintStrong;
            return StateColor(lesson == null ? null : Skill(lesson.SkillId));
        }

        private string ChapterProgressText(MathChapterDescriptor chapter, int lessonCount)
        {
            if (chapter == null || _catalog == null) return lessonCount + " bài học";
            var lessons = _catalog.Lessons.Where(x => string.Equals(x.ChapterId, chapter.Id, StringComparison.Ordinal)).ToList();
            var attempted = 0;
            var stable = 0;
            foreach (var lesson in lessons)
            {
                var snapshot = Skill(lesson.SkillId);
                if (snapshot == null || snapshot.AttemptsCount <= 0) continue;
                attempted++;
                if (string.Equals(snapshot.LearningState, "STABLE", StringComparison.OrdinalIgnoreCase)) stable++;
            }
            if (attempted <= 0) return lessonCount + " bài học";
            return lessonCount + " bài • " + attempted + " đã học" + (stable > 0 ? " • " + stable + " vững" : string.Empty);
        }

        private MathLessonDescriptor FindContinueLesson()
        {
            if (_catalog == null || _catalog.Lessons == null) return null;
            _continueResumesSession = false;

            bool hasResumableMathSession;
            var resumableLesson = FindResumableTargetLesson(out hasResumableMathSession);
            if (resumableLesson != null)
            {
                _continueResumesSession = true;
                return resumableLesson;
            }
            if (hasResumableMathSession) return null;

            if (_skills == null || _skills.Count == 0) return null;
            var active = new List<MathLessonDescriptor>();
            var studied = new List<MathLessonDescriptor>();
            foreach (var lesson in _catalog.Lessons)
            {
                var snapshot = Skill(lesson.SkillId);
                if (snapshot == null || snapshot.AttemptsCount <= 0) continue;
                studied.Add(lesson);
                if (!string.Equals(snapshot.LearningState, "STABLE", StringComparison.OrdinalIgnoreCase)) active.Add(lesson);
            }
            var candidates = active.Count > 0 ? active : studied;
            return candidates
                .OrderByDescending(x =>
                {
                    var snapshot = Skill(x.SkillId);
                    return snapshot != null && snapshot.LastSeenAtUtc.HasValue ? snapshot.LastSeenAtUtc.Value : DateTime.MinValue;
                })
                .ThenBy(x => x.OrderInDomain)
                .FirstOrDefault();
        }

        private MathLessonDescriptor FindResumableTargetLesson(out bool hasResumableMathSession)
        {
            hasResumableMathSession = false;
            try
            {
                var runtime = new MathSessionRuntimeService(_database)
                    .LoadLatestResumable(LearnerSessionService.PrimaryChildId);
                if (runtime == null) return null;
                hasResumableMathSession = true;
                if (!string.Equals(runtime.SessionMode, "lesson", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(runtime.TargetLessonId)) return null;
                return _catalog.FindLesson(runtime.TargetLessonId);
            }
            catch (MathSessionRuntimeCorruptException)
            {
                hasResumableMathSession = true;
                return null;
            }
            catch
            {
                return null;
            }
        }

        private void RefreshContinueLessonState()
        {
            _continueLesson = FindContinueLesson();
            if (_continueLessonButton == null) return;
            if (_continueLesson == null)
            {
                _continueLessonButton.Text = "Chưa có bài đang học";
                _continueLessonButton.AccessibleName = "Chưa có bài Toán đang học";
                _continueLessonButton.Enabled = false;
                _continueLessonButton.AccessibleDescription = "Chưa có bài học đang học dở để tiếp tục.";
                return;
            }
            _continueLessonButton.Enabled = true;
            if (_continueResumesSession)
            {
                _continueLessonButton.Text = "Tiếp tục bài đang học";
                _continueLessonButton.AccessibleName = "Tiếp tục phiên bài Toán đang học";
                _continueLessonButton.AccessibleDescription = "Tiếp tục đúng phiên bài " + _continueLesson.TitleVi + " đang học dở.";
            }
            else
            {
                _continueLessonButton.Text = "Học tiếp bài gần đây";
                _continueLessonButton.AccessibleName = "Học tiếp bài Toán gần đây";
                _continueLessonButton.AccessibleDescription = "Mở lại bài " + _continueLesson.TitleVi + " để xem và luyện tiếp.";
            }
        }

        private void ContinueCurrentLesson()
        {
            if (_continueLesson == null) return;
            var lesson = _continueLesson;
            if (_continueResumesSession)
            {
                OpenLessonPractice(lesson);
                return;
            }
            SelectLessonInCatalog(lesson);
        }

        private void SelectLessonInCatalog(MathLessonDescriptor lesson)
        {
            if (lesson == null || _catalog == null) return;
            var chapter = _catalog.FindChapter(lesson.ChapterId);
            if (chapter != null && (_selectedChapter == null || !string.Equals(_selectedChapter.Id, chapter.Id, StringComparison.Ordinal)))
                SelectChapter(chapter);
            SelectLesson(lesson);
        }

        private string LessonStateText(MathLessonDescriptor lesson)
        {
            var access = lesson == null ? null : LessonAccess(lesson.Id);
            if (access != null && !access.IsUnlocked) return "Đang khóa  •  học bài trước";
            if (access != null && access.IsCompleted)
            {
                var best = access.BestScorePercent.HasValue
                    ? "  •  tốt nhất " + Math.Round(access.BestScorePercent.Value) + "%"
                    : string.Empty;
                return "Đã hoàn thành" + best;
            }
            if (access != null && access.StartedCount > 0) return "Đang học  •  chưa hoàn thành";

            var snapshot = Skill(lesson.SkillId);
            if (snapshot == null || snapshot.AttemptsCount <= 0) return "Chưa học";
            var percent = (int)Math.Round(Math.Max(0, Math.Min(1, snapshot.MasteryScore)) * 100.0);
            switch ((snapshot.LearningState ?? string.Empty).ToUpperInvariant())
            {
                case "STABLE": return "Đã vững  •  " + percent + "%";
                case "REVIEW": return "Cần ôn  •  " + percent + "%";
                case "LEARNING": return "Đang học  •  " + percent + "%";
                default: return "Đã làm " + snapshot.AttemptsCount + " câu  •  " + percent + "%";
            }
        }

        private SkillSnapshot Skill(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId) || _skills == null) return null;
            SkillSnapshot snapshot;
            return _skills.TryGetValue(skillId, out snapshot) ? snapshot : null;
        }

        private MathLessonAccessSnapshot LessonAccess(string lessonId)
        {
            if (string.IsNullOrWhiteSpace(lessonId) || _lessonAccess == null) return null;
            MathLessonAccessSnapshot access;
            return _lessonAccess.TryGetValue(lessonId, out access) ? access : null;
        }

        private static Color StateBackground(SkillSnapshot snapshot)
        {
            if (snapshot == null || snapshot.AttemptsCount <= 0) return Color.FromArgb(244, 242, 232);
            switch ((snapshot.LearningState ?? string.Empty).ToUpperInvariant())
            {
                case "STABLE": return Color.FromArgb(225, 243, 224);
                case "REVIEW": return Color.FromArgb(252, 235, 222);
                case "LEARNING": return Color.FromArgb(231, 241, 247);
                default: return Color.FromArgb(241, 239, 229);
            }
        }

        private static Color StateColor(SkillSnapshot snapshot)
        {
            if (snapshot == null || snapshot.AttemptsCount <= 0) return ChildVisualTheme.MutedInk;
            switch ((snapshot.LearningState ?? string.Empty).ToUpperInvariant())
            {
                case "STABLE": return ChildVisualTheme.MintStrong;
                case "REVIEW": return ChildVisualTheme.PeachStrong;
                case "LEARNING": return ChildVisualTheme.SkyStrong;
                default: return ChildVisualTheme.Ink;
            }
        }

        private Label AddSection(string text)
        {
            var label = AddDetailLabel(text, 10.2f, FontStyle.Bold, ChildVisualTheme.Ink, 30, ContentAlignment.BottomLeft);
            label.Margin = new Padding(8, 13, 8, 2);
            return label;
        }

        private Label AddBody(string text) { return AddBody(text, false); }

        private Label AddBody(string text, bool bold)
        {
            var label = AddDetailLabel(text, 9.6f, bold ? FontStyle.Bold : FontStyle.Regular,
                bold ? ChildVisualTheme.Ink : ChildVisualTheme.MutedInk, 0, ContentAlignment.TopLeft);
            label.Margin = new Padding(8, 2, 8, 4);
            label.Padding = new Padding(2, 2, 2, 2);
            return label;
        }

        private Label AddDetailLabel(string text, float size, FontStyle style, Color color, int fixedHeight, ContentAlignment align)
        {
            var label = new Label
            {
                AutoSize = fixedHeight <= 0,
                Height = fixedHeight <= 0 ? 24 : fixedHeight,
                Text = text ?? string.Empty,
                TextAlign = align,
                ForeColor = color,
                Font = ChildVisualTheme.Font(size, style),
                Margin = new Padding(8, 2, 8, 2),
                AccessibleName = text ?? string.Empty
            };
            _detailFlow.Controls.Add(label);
            return label;
        }

        private string PrerequisiteName(string skillId)
        {
            var lesson = _catalog == null ? null : _catalog.FindLessonBySkill(skillId);
            if (lesson == null) return skillId;
            var state = Skill(skillId);
            var suffix = state != null && string.Equals(state.LearningState, "STABLE", StringComparison.OrdinalIgnoreCase)
                ? " (đã vững)"
                : string.Empty;
            return lesson.TitleVi + suffix;
        }

        private static string Bullets(IEnumerable<string> values)
        {
            if (values == null) return string.Empty;
            return string.Join("\r\n", values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => "• " + x.Trim()));
        }

        private static string ReviewText(DateTime dueAtUtc)
        {
            var local = dueAtUtc.Kind == DateTimeKind.Utc ? dueAtUtc.ToLocalTime() : dueAtUtc;
            var today = DateTime.Now.Date;
            if (local.Date <= today) return "Đã đến lúc ôn lại bài này.";
            if (local.Date == today.AddDays(1)) return "Ôn lại vào ngày mai.";
            return "Ôn lại vào " + local.ToString("dd/MM/yyyy") + ".";
        }

        private void RefreshSkillProgress()
        {
            try
            {
                _skills = new LearnerSessionService(_database)
                    .LoadSkillSnapshots(LearnerSessionService.PrimaryChildId, "math");
            }
            catch
            {
                _skills = new Dictionary<string, SkillSnapshot>(StringComparer.Ordinal);
            }
        }

        private void RefreshLessonAccess()
        {
            try
            {
                _lessonAccess = new MathLessonProgressService(_database, _catalogPath)
                    .GetAllAccess(LearnerSessionService.PrimaryChildId)
                    .ToDictionary(x => x.LessonId, x => x, StringComparer.Ordinal);
            }
            catch
            {
                _lessonAccess = new Dictionary<string, MathLessonAccessSnapshot>(StringComparer.Ordinal);
            }
        }

        private void OpenLessonPractice(MathLessonDescriptor lessonDescriptor)
        {
            if (lessonDescriptor == null) return;
            var access = LessonAccess(lessonDescriptor.Id);
            if (access == null || !access.IsUnlocked) return;

            try
            {
                using (var lesson = new MathLessonForm(_database, _performance, lessonDescriptor.Id))
                    lesson.ShowDialog(this);
                RefreshSkillProgress();
                RefreshLessonAccess();
                PopulateChapters();
                RefreshContinueLessonState();
                var refreshed = _catalog == null ? null : _catalog.FindLesson(lessonDescriptor.Id);
                if (refreshed != null) SelectLessonInCatalog(refreshed);
            }
            catch
            {
                MessageBox.Show(this,
                    "Chưa thể mở phần luyện tập của bài này lúc này. Con vẫn có thể xem lại kiến thức và ví dụ.",
                    "WAHU Kids Learn", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OpenQuickRescue()
        {
            try
            {
                _rescueButton.Enabled = false;
                var preferredLessonId = _selectedLesson != null && MathQuickRescueFixtureCatalog.ContainsLesson(_selectedLesson.Id)
                    ? _selectedLesson.Id : null;
                using (var rescue = new MathQuickRescueForm(_database, _performance, _catalogPath,
                    preferredLessonId, new List<MathQuickRescueEventPresentation>()))
                    rescue.ShowDialog(this);
                RefreshSkillProgress();
                RefreshLessonAccess();
                PopulateChapters();
                RefreshContinueLessonState();
                if (_catalog != null && !string.IsNullOrWhiteSpace(preferredLessonId))
                {
                    var refreshedLesson = _catalog.FindLesson(preferredLessonId);
                    if (refreshedLesson != null) SelectLessonInCatalog(refreshedLesson);
                }
            }
            catch
            {
                MessageBox.Show(this,
                    "Chưa thể mở nhiệm vụ cứu hộ lúc này. Con vẫn có thể luyện theo bài hoặc làm nhiệm vụ Toán hôm nay.",
                    "WAHU Kids Learn", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                _rescueButton.Enabled = true;
            }
        }

        private void OpenAdaptiveMission()
        {
            try
            {
                _missionButton.Enabled = false;
                var selectedChapterId = _selectedChapter == null ? null : _selectedChapter.Id;
                var selectedLessonId = _selectedLesson == null ? null : _selectedLesson.Id;
                using (var lesson = new MathLessonForm(_database, _performance)) lesson.ShowDialog(this);
                RefreshSkillProgress();
                RefreshLessonAccess();
                PopulateChapters();
                RefreshContinueLessonState();
                if (_catalog != null && !string.IsNullOrWhiteSpace(selectedChapterId))
                {
                    var refreshedChapter = _catalog.FindChapter(selectedChapterId);
                    if (refreshedChapter != null) SelectChapter(refreshedChapter);
                }
                if (_catalog != null && !string.IsNullOrWhiteSpace(selectedLessonId))
                {
                    var refreshedLesson = _catalog.FindLesson(selectedLessonId);
                    if (refreshedLesson != null) SelectLesson(refreshedLesson);
                }
            }
            catch
            {
                MessageBox.Show(this,
                    "Chưa thể mở nhiệm vụ Toán lúc này. Nhờ người lớn mở mục Phụ huynh để kiểm tra nhé.",
                    "WAHU Kids Learn", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                _missionButton.Enabled = true;
            }
        }

        private void ResizeNavigationButtons()
        {
            ResizeFlowChildren(_chapterFlow, 18);
            ResizeFlowChildren(_lessonFlow, 18);
        }

        private static void ResizeFlowChildren(FlowLayoutPanel flow, int padding)
        {
            if (flow == null) return;
            var width = Math.Max(120, flow.ClientSize.Width - padding - (flow.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0));
            foreach (Control child in flow.Controls) child.Width = width;
        }

        private void ResizeDetailChildren()
        {
            if (_detailFlow == null) return;
            var width = Math.Max(180, _detailFlow.ClientSize.Width - 30 - (_detailFlow.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0));
            foreach (Control child in _detailFlow.Controls)
            {
                child.Width = width;
                var label = child as Label;
                if (label != null && label.AutoSize) label.MaximumSize = new Size(width, 0);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private static string ResolveCatalogPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "content_packs", "math_grade2_v1", "lesson_catalog_v1.json");
        }
    }
}
