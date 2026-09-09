using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WAHU.Content;
using WAHU.Data;
using WAHU.Session;

namespace WAHUKidsLearn
{
    internal sealed class MathWorldPage : LearnerPage
    {
        private readonly LearnerShellContext _context;
        private readonly Action<LearnerRoute> _navigate;
        private readonly string _catalogPath;
        private MathLessonCatalogSnapshot _catalog;
        private IDictionary<string, MathLessonAccessSnapshot> _access = new Dictionary<string, MathLessonAccessSnapshot>(StringComparer.Ordinal);
        private MathChapterDescriptor _selectedChapter;
        private MathLessonDescriptor _selectedLesson;
        private FlowLayoutPanel _chapterStrip;
        private FlowLayoutPanel _lessonFlow;
        private ChildCard _detailCard;
        private TableLayoutPanel _detailLayout;
        private Label _summary;
        private Label _chapterTitle;
        private Label _lessonTitle;
        private Label _lessonState;
        private Label _lessonBody;
        private Label _lessonExample;
        private ChildActionButton _practiceButton;
        private ChildActionButton _rescueButton;
        private ChildActionButton _continueButton;
        private string _resumableLessonId;

        public MathWorldPage(LearnerShellContext context, Action<LearnerRoute> navigate)
        {
            _context = context ?? throw new ArgumentNullException("context");
            _navigate = navigate ?? throw new ArgumentNullException("navigate");
            _catalogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "content_packs", "math_grade2_v1", "lesson_catalog_v1.json");
            AccessibleName = "Thế giới Toán lớp 2";
            BuildUi();
        }

        public override LearnerRoute Route { get { return LearnerRoute.MathWorld; } }
        public override string PageTitle { get { return "Thế giới Toán"; } }

        public override void OnNavigatedTo()
        {
            LoadWorld();
        }

        private void BuildUi()
        {
            var root = new ChildSceneLayout
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = LearnerDesignTokens.PagePadding(LearnerLayoutProfile.Standard)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var title = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            title.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            title.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
            title.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Thế giới Toán lớp 2",
                TextAlign = ContentAlignment.BottomLeft,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(22f, FontStyle.Bold),
                AccessibleName = "Thế giới Toán lớp 2"
            }, 0, 0);
            _summary = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Đang mở lộ trình…",
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9.5f),
                AccessibleName = "Tóm tắt lộ trình Toán"
            };
            title.Controls.Add(_summary, 0, 1);
            header.Controls.Add(title, 0, 0);
            _continueButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(12, 5, 0, 5),
                Text = "Chưa có bài đang học",
                IconAssetPath = "10_UIIcons/icon_math.png",
                IconSize = 22,
                FillColor = Color.FromArgb(228, 241, 248),
                HoverColor = Color.FromArgb(211, 232, 243),
                PressedColor = Color.FromArgb(196, 221, 235),
                TextColor = ChildVisualTheme.SkyStrong,
                BorderColor = Color.FromArgb(174, 207, 225),
                Font = ChildVisualTheme.Font(9.2f, FontStyle.Bold),
                Radius = LearnerDesignTokens.RadiusButton,
                Enabled = false,
                AccessibleName = "Tiếp tục bài Toán đang học"
            };
            _continueButton.Click += delegate { ContinueLesson(); };
            header.Controls.Add(_continueButton, 1, 0);
            root.Controls.Add(header, 0, 0);

            var chapterCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 7),
                Padding = new Padding(8, 6, 8, 6),
                CardColor = Color.FromArgb(248, 250, 246),
                BorderColor = Color.FromArgb(220, 225, 212),
                Radius = LearnerDesignTokens.RadiusCard,
                ShowShadow = false
            };
            _chapterStrip = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent
            };
            chapterCard.Controls.Add(_chapterStrip);
            root.Controls.Add(chapterCard, 0, 1);

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var lessonCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 8, 4),
                Padding = new Padding(10),
                CardColor = ChildVisualTheme.Blend(ChildVisualTheme.Sky, Color.White, 0.70f),
                BorderColor = Color.FromArgb(199, 220, 232),
                Radius = LearnerDesignTokens.RadiusCard
            };
            var lessonLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            lessonLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            lessonLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _chapterTitle = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Chọn một chương",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.SkyStrong,
                Font = ChildVisualTheme.Font(12f, FontStyle.Bold),
                AccessibleName = "Chương Toán đang chọn"
            };
            lessonLayout.Controls.Add(_chapterTitle, 0, 0);
            _lessonFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(2),
                BackColor = Color.Transparent
            };
            lessonLayout.Controls.Add(_lessonFlow, 0, 1);
            lessonCard.Controls.Add(lessonLayout);
            body.Controls.Add(lessonCard, 0, 0);

            _detailCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 4, 0, 4),
                Padding = new Padding(18, 14, 18, 14),
                CardColor = Color.FromArgb(255, 253, 246),
                BorderColor = Color.FromArgb(232, 216, 186),
                Radius = LearnerDesignTokens.RadiusHero
            };
            _detailLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty };
            _detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            _detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            _detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            _detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            _detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            _detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            _lessonTitle = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Chọn một bài để xem nội dung",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(17f, FontStyle.Bold),
                AccessibleName = "Bài Toán đang chọn"
            };
            _detailLayout.Controls.Add(_lessonTitle, 0, 0);
            _lessonState = new Label
            {
                Dock = DockStyle.Fill,
                Text = "",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9.5f, FontStyle.Bold),
                AccessibleName = "Trạng thái bài học"
            };
            _detailLayout.Controls.Add(_lessonState, 0, 1);
            _lessonBody = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Mỗi bài chỉ mở những phần bé cần học ở thời điểm này.",
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(10.5f),
                AutoEllipsis = true,
                AccessibleName = "Mục tiêu và kiến thức bài học"
            };
            _detailLayout.Controls.Add(_lessonBody, 0, 2);
            _lessonExample = new Label
            {
                Dock = DockStyle.Fill,
                Text = "",
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9.6f),
                AutoEllipsis = true,
                AccessibleName = "Ví dụ bài học"
            };
            _detailLayout.Controls.Add(_lessonExample, 0, 3);
            _practiceButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 2),
                Text = "Học bài này",
                IconAssetPath = "10_UIIcons/icon_math.png",
                IconSize = 24,
                FillColor = ChildVisualTheme.MintStrong,
                HoverColor = Color.FromArgb(90, 156, 103),
                PressedColor = Color.FromArgb(75, 139, 88),
                Font = ChildVisualTheme.Font(11f, FontStyle.Bold),
                Radius = LearnerDesignTokens.RadiusButton,
                Enabled = false,
                AccessibleName = "Học bài Toán đang chọn"
            };
            _practiceButton.Click += delegate { StartSelectedLesson(); };
            _detailLayout.Controls.Add(_practiceButton, 0, 4);
            _detailLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Bài học có 3 câu mục tiêu: cơ bản • vừa • vận dụng.",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(8.8f)
            }, 0, 5);
            _detailCard.Controls.Add(_detailLayout);
            body.Controls.Add(_detailCard, 1, 0);
            root.Controls.Add(body, 0, 2);

            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            footer.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Chọn một chương, rồi một bài. Bài khóa sẽ nói rõ cần hoàn thành bài nào trước.",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9.2f),
                Padding = new Padding(4, 0, 12, 0)
            }, 0, 0);
            _rescueButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 6, 0, 2),
                Text = "Mở bản đồ cứu hộ",
                IconAssetPath = "10_UIIcons/icon_reward.png",
                IconSize = 24,
                FillColor = Color.FromArgb(238, 209, 154),
                HoverColor = Color.FromArgb(231, 194, 129),
                PressedColor = Color.FromArgb(219, 178, 111),
                TextColor = Color.FromArgb(104, 73, 34),
                Font = ChildVisualTheme.Font(10.2f, FontStyle.Bold),
                Radius = LearnerDesignTokens.RadiusButton,
                AccessibleName = "Mở bản đồ nhiệm vụ cứu hộ"
            };
            _rescueButton.Click += delegate { _navigate(LearnerRoute.RescueMap); };
            footer.Controls.Add(_rescueButton, 1, 0);
            root.Controls.Add(footer, 0, 3);
            Controls.Add(root);
        }

        private void LoadWorld()
        {
            try
            {
                _catalog = new MathLessonCatalogSource().Load(_catalogPath);
                var childId = LearnerSessionService.PrimaryChildId;
                _access = new MathLessonProgressService(_context.LearningDatabase, _catalogPath)
                    .GetAllAccess(childId).ToDictionary(x => x.LessonId, StringComparer.Ordinal);
                _summary.Text = _catalog.Chapters.Count + " chương • " + _catalog.Topics.Count + " chủ đề • " + _catalog.Lessons.Count + " bài";
                LoadResumableLesson(childId);
                PopulateChapters();
                var resumeLesson = string.IsNullOrWhiteSpace(_resumableLessonId) ? null : _catalog.FindLesson(_resumableLessonId);
                if (resumeLesson != null) SelectLessonInWorld(resumeLesson);
                else if (_selectedLesson != null && _catalog.FindLesson(_selectedLesson.Id) != null) SelectLessonInWorld(_catalog.FindLesson(_selectedLesson.Id));
                else if (_catalog.Chapters.Count > 0) SelectChapter(_catalog.Chapters[0]);
            }
            catch
            {
                _summary.Text = "Lộ trình đang cần được kiểm tra lại. Phần học đã lưu vẫn an toàn.";
                _chapterStrip.Controls.Clear();
                _lessonFlow.Controls.Clear();
                _practiceButton.Enabled = false;
            }
        }

        private void LoadResumableLesson(string childId)
        {
            _resumableLessonId = null;
            try
            {
                var runtime = new MathSessionRuntimeService(_context.LearningDatabase).LoadLatestResumable(childId);
                if (runtime != null && string.Equals(runtime.SessionMode, "lesson", StringComparison.Ordinal) &&
                    string.Equals(runtime.PackId, MathSessionCoordinator.PackId, StringComparison.Ordinal) &&
                    string.Equals(runtime.PackVersion, MathSessionCoordinator.PackVersion, StringComparison.Ordinal))
                    _resumableLessonId = runtime.TargetLessonId;
            }
            catch { }
            var lesson = _catalog == null ? null : _catalog.FindLesson(_resumableLessonId);
            _continueButton.Enabled = lesson != null;
            _continueButton.Text = lesson == null ? "Chưa có bài đang học" : "Tiếp tục: " + lesson.TitleVi;
            _continueButton.AccessibleDescription = lesson == null ? "Không có bài học đang dở." : "Tiếp tục đúng bài " + lesson.TitleVi + " đang được lưu.";
        }

        private void PopulateChapters()
        {
            _chapterStrip.SuspendLayout();
            try
            {
                _chapterStrip.Controls.Clear();
                for (var i = 0; i < _catalog.Chapters.Count; i++)
                {
                    var chapter = _catalog.Chapters[i];
                    var captured = chapter;
                    var button = new ChildActionButton
                    {
                        AutoSize = false,
                        Size = new Size(148, 44),
                        Margin = new Padding(4, 2, 4, 2),
                        Text = (i + 1) + ". " + chapter.TitleVi,
                        FillColor = Color.FromArgb(244, 248, 250),
                        HoverColor = Color.FromArgb(228, 240, 246),
                        PressedColor = Color.FromArgb(212, 231, 240),
                        TextColor = ChildVisualTheme.SkyStrong,
                        BorderColor = Color.FromArgb(198, 219, 230),
                        Radius = 14,
                        Font = ChildVisualTheme.Font(8.8f, FontStyle.Bold),
                        AccessibleName = "Chương " + (i + 1) + ": " + chapter.TitleVi
                    };
                    button.Click += delegate { SelectChapter(captured); };
                    button.Tag = chapter.Id;
                    _chapterStrip.Controls.Add(button);
                }
            }
            finally { _chapterStrip.ResumeLayout(true); }
        }

        private void SelectChapter(MathChapterDescriptor chapter)
        {
            if (chapter == null || _catalog == null) return;
            _selectedChapter = chapter;
            _chapterTitle.Text = chapter.TitleVi;
            foreach (Control control in _chapterStrip.Controls)
            {
                var button = control as ChildActionButton;
                if (button == null) continue;
                var selected = string.Equals(Convert.ToString(button.Tag), chapter.Id, StringComparison.Ordinal);
                button.BorderThickness = selected ? 2f : 1f;
                button.FillColor = selected ? Color.FromArgb(224, 241, 231) : Color.FromArgb(244, 248, 250);
                button.TextColor = selected ? ChildVisualTheme.MintStrong : ChildVisualTheme.SkyStrong;
                button.Invalidate();
            }

            _lessonFlow.SuspendLayout();
            try
            {
                _lessonFlow.Controls.Clear();
                foreach (var topic in _catalog.TopicsForChapter(chapter.Id))
                {
                    _lessonFlow.Controls.Add(new Label
                    {
                        AutoSize = false,
                        Height = 30,
                        Width = 320,
                        Margin = new Padding(5, 8, 5, 2),
                        Text = topic.TitleVi,
                        TextAlign = ContentAlignment.MiddleLeft,
                        ForeColor = ChildVisualTheme.SkyStrong,
                        Font = ChildVisualTheme.Font(9.2f, FontStyle.Bold),
                        AccessibleName = "Chủ đề " + topic.TitleVi
                    });
                    foreach (var lesson in _catalog.LessonsForTopic(topic.Id))
                        _lessonFlow.Controls.Add(BuildLessonNode(lesson));
                }
            }
            finally
            {
                _lessonFlow.ResumeLayout(true);
                ResizeLessonNodes();
            }
            var first = _catalog.Lessons.Where(x => string.Equals(x.ChapterId, chapter.Id, StringComparison.Ordinal)).FirstOrDefault();
            if (first != null) SelectLesson(first);
        }

        private Control BuildLessonNode(MathLessonDescriptor lesson)
        {
            MathLessonAccessSnapshot access;
            _access.TryGetValue(lesson.Id, out access);
            var unlocked = access != null && access.IsUnlocked;
            var completed = access != null && access.IsCompleted;
            var resume = string.Equals(_resumableLessonId, lesson.Id, StringComparison.Ordinal);
            var state = resume ? "▶ Đang học dở" : (completed ? "✓ Đã hoàn thành" : (unlocked ? "Sẵn sàng" : "🔒 Chưa mở"));
            var button = new ChildActionButton
            {
                AutoSize = false,
                Height = 62,
                Width = 320,
                Margin = new Padding(4, 3, 4, 3),
                Text = lesson.TitleVi + "\r\n" + state,
                FillColor = completed ? Color.FromArgb(233, 246, 232) : (unlocked ? Color.White : Color.FromArgb(242, 241, 235)),
                HoverColor = unlocked ? Color.FromArgb(239, 247, 251) : Color.FromArgb(237, 236, 230),
                PressedColor = unlocked ? Color.FromArgb(225, 239, 246) : Color.FromArgb(232, 231, 225),
                TextColor = unlocked ? ChildVisualTheme.Ink : ChildVisualTheme.MutedInk,
                BorderColor = completed ? Color.FromArgb(187, 219, 186) : Color.FromArgb(220, 219, 207),
                Radius = 16,
                Font = ChildVisualTheme.Font(9.4f, FontStyle.Bold),
                AccessibleName = "Bài " + lesson.TitleVi,
                AccessibleDescription = unlocked ? state + ". Nhấn Enter để xem bài." : BuildLockReason(access) + " Nhấn Enter để xem yêu cầu mở khóa.",
                Tag = lesson.Id
            };
            var captured = lesson;
            button.Click += delegate { SelectLesson(captured); };
            return button;
        }

        private void SelectLessonInWorld(MathLessonDescriptor lesson)
        {
            var chapter = _catalog.FindChapter(lesson.ChapterId);
            if (chapter != null && (_selectedChapter == null || !string.Equals(_selectedChapter.Id, chapter.Id, StringComparison.Ordinal)))
                SelectChapter(chapter);
            SelectLesson(lesson);
        }

        private void SelectLesson(MathLessonDescriptor lesson)
        {
            if (lesson == null) return;
            _selectedLesson = lesson;
            foreach (Control control in _lessonFlow.Controls)
            {
                var button = control as ChildActionButton;
                if (button == null || button.Tag == null) continue;
                button.BorderThickness = string.Equals(Convert.ToString(button.Tag), lesson.Id, StringComparison.Ordinal) ? 2f : 1f;
                button.BorderColor = button.BorderThickness > 1f ? ChildVisualTheme.MintStrong : Color.FromArgb(220, 219, 207);
                button.Invalidate();
            }
            RenderLessonDetail(lesson);
        }

        private void RenderLessonDetail(MathLessonDescriptor lesson)
        {
            MathLessonAccessSnapshot access;
            _access.TryGetValue(lesson.Id, out access);
            var unlocked = access != null && access.IsUnlocked;
            var completed = access != null && access.IsCompleted;
            _lessonTitle.Text = lesson.TitleVi;
            _lessonState.Text = string.Equals(_resumableLessonId, lesson.Id, StringComparison.Ordinal)
                ? "Đang học dở • có thể tiếp tục đúng chỗ"
                : (completed ? "Đã hoàn thành" : (unlocked ? "Sẵn sàng học" : BuildLockReason(access)));
            _lessonState.ForeColor = unlocked ? ChildVisualTheme.MintStrong : Color.FromArgb(167, 92, 74);
            var objectives = lesson.ObjectivesVi == null ? string.Empty : string.Join("\r\n• ", lesson.ObjectivesVi);
            _lessonBody.Text = "Con sẽ học:\r\n• " + objectives + "\r\n\r\n" + lesson.ExplanationVi;
            var example = lesson.WorkedExamples == null ? null : lesson.WorkedExamples.FirstOrDefault();
            _lessonExample.Text = example == null ? "" : "Ví dụ: " + example.PromptVi + "\r\n" + string.Join(" → ", example.SolutionStepsVi ?? new List<string>()) + " → " + example.Answer;
            _practiceButton.Enabled = unlocked;
            _practiceButton.Text = string.Equals(_resumableLessonId, lesson.Id, StringComparison.Ordinal) ? "Tiếp tục bài này" : (completed ? "Luyện lại bài này" : (unlocked ? "Học bài này" : "Hoàn thành bài nền trước"));
            _practiceButton.AccessibleDescription = unlocked ? "Mở ba câu mục tiêu của bài " + lesson.TitleVi + "." : BuildLockReason(access);
        }

        private string BuildLockReason(MathLessonAccessSnapshot access)
        {
            if (access == null || access.UnsatisfiedPrerequisiteLessonIds == null || access.UnsatisfiedPrerequisiteLessonIds.Count == 0)
                return "Bài này đang chờ bài nền được hoàn thành trước.";
            var names = access.UnsatisfiedPrerequisiteLessonIds.Select(id => _catalog == null ? null : _catalog.FindLesson(id))
                .Where(x => x != null).Select(x => x.TitleVi).Distinct(StringComparer.Ordinal).ToList();
            return names.Count == 0 ? "Bài này đang chờ bài nền được hoàn thành trước." : "Cần hoàn thành trước: " + string.Join(", ", names) + ".";
        }

        private void StartSelectedLesson()
        {
            if (_selectedLesson == null || !_practiceButton.Enabled) return;
            _context.RequestedLessonId = _selectedLesson.Id;
            _context.RequestedEvent = null;
            _navigate(LearnerRoute.LessonPlay);
        }

        private void ContinueLesson()
        {
            if (string.IsNullOrWhiteSpace(_resumableLessonId)) return;
            _context.RequestedLessonId = _resumableLessonId;
            _context.RequestedEvent = null;
            _navigate(LearnerRoute.LessonPlay);
        }

        private void ResizeLessonNodes()
        {
            if (_lessonFlow == null) return;
            var width = Math.Max(220, _lessonFlow.ClientSize.Width - _lessonFlow.Padding.Horizontal - 24);
            foreach (Control control in _lessonFlow.Controls)
                if (control is Button || control is Label) control.Width = width;
        }

        protected override void ApplyLayoutProfile(LearnerLayoutProfile profile)
        {
            if (_detailCard == null) return;
            _detailCard.Padding = profile == LearnerLayoutProfile.Compact ? new Padding(12, 10, 12, 10) : new Padding(18, 14, 18, 14);
            ResizeLessonNodes();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ResizeLessonNodes();
        }
    }
}
