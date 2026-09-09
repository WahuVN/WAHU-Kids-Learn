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
    internal sealed class RescueMapPage : LearnerPage
    {
        private readonly LearnerShellContext _context;
        private readonly Action<LearnerRoute> _navigate;
        private readonly string _catalogPath;
        private readonly List<MathQuickRescueEventPresentation> _events = new List<MathQuickRescueEventPresentation>();
        private MathLessonCatalogSnapshot _catalog;
        private IDictionary<string, MathLessonAccessSnapshot> _access = new Dictionary<string, MathLessonAccessSnapshot>(StringComparer.Ordinal);
        private string _resumableLessonId;
        private string _resumableSessionId;
        private MathQuickRescueEventPresentation _selectedEvent;
        private FlowLayoutPanel _eventStrip;
        private TableLayoutPanel _heroLayout;
        private RescueMissionIllustrationControl _missionArt;
        private Label _eventTitle;
        private Label _intro;
        private Label _status;
        private FlowLayoutPanel _checkpoints;
        private ChildActionButton _startButton;
        private Label _summary;

        public RescueMapPage(LearnerShellContext context, Action<LearnerRoute> navigate)
        {
            _context = context ?? throw new ArgumentNullException("context");
            _navigate = navigate ?? throw new ArgumentNullException("navigate");
            _catalogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "content_packs", "math_grade2_v1", "lesson_catalog_v1.json");
            AccessibleName = "Bản đồ nhiệm vụ cứu hộ";
            BuildUi();
        }

        public override LearnerRoute Route { get { return LearnerRoute.RescueMap; } }
        public override string PageTitle { get { return "Nhiệm vụ cứu hộ"; } }
        internal int EventCount { get { return _events.Count; } }
        internal string SelectedEventId { get { return _selectedEvent == null ? null : _selectedEvent.Id; } }
        internal bool StartEnabled { get { return _startButton != null && _startButton.Enabled; } }

        public override void OnNavigatedTo()
        {
            LoadEvents();
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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
            header.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Bản đồ cứu hộ",
                TextAlign = ContentAlignment.BottomLeft,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(22f, FontStyle.Bold),
                AccessibleName = "Bản đồ cứu hộ Toán"
            }, 0, 0);
            _summary = new Label
            {
                Dock = DockStyle.Fill,
                Text = "5 nhiệm vụ • mỗi nhiệm vụ 3 chặng • không đếm giờ",
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9.5f),
                AccessibleName = "Nguyên tắc nhiệm vụ cứu hộ"
            };
            header.Controls.Add(_summary, 0, 1);
            root.Controls.Add(header, 0, 0);

            var hero = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 8),
                Padding = new Padding(18, 14, 18, 14),
                CardColor = Color.FromArgb(255, 253, 246),
                BorderColor = Color.FromArgb(232, 213, 178),
                Radius = LearnerDesignTokens.RadiusHero
            };
            _heroLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            _heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
            _heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            _heroLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var artHost = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 12, 0),
                Padding = new Padding(8),
                CardColor = Color.FromArgb(244, 249, 241),
                BorderColor = Color.FromArgb(213, 225, 203),
                Radius = LearnerDesignTokens.RadiusCard,
                ShowShadow = false
            };
            _missionArt = new RescueMissionIllustrationControl
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                AccessibleName = "Minh họa nhiệm vụ cứu hộ"
            };
            artHost.Controls.Add(_missionArt);
            _heroLayout.Controls.Add(artHost, 0, 0);

            var detail = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty };
            detail.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            detail.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            detail.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            detail.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            detail.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            detail.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            _eventTitle = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Chọn một nhiệm vụ",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(18f, FontStyle.Bold),
                AccessibleName = "Nhiệm vụ cứu hộ đang chọn"
            };
            detail.Controls.Add(_eventTitle, 0, 0);
            _intro = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Mỗi nhiệm vụ có ba chặng. Bé có thể nghỉ bất cứ lúc nào.",
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(10.5f),
                AccessibleName = "Câu chuyện nhiệm vụ"
            };
            detail.Controls.Add(_intro, 0, 1);
            _checkpoints = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = new Padding(0, 4, 0, 4),
                BackColor = Color.Transparent
            };
            detail.Controls.Add(_checkpoints, 0, 2);
            _status = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Chọn nhiệm vụ để xem trạng thái.",
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9.7f),
                AccessibleName = "Trạng thái nhiệm vụ"
            };
            detail.Controls.Add(_status, 0, 3);
            _startButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 2),
                Text = "Chọn nhiệm vụ trước",
                IconAssetPath = "10_UIIcons/icon_reward.png",
                IconSize = 26,
                FillColor = Color.FromArgb(232, 174, 93),
                HoverColor = Color.FromArgb(220, 158, 77),
                PressedColor = Color.FromArgb(204, 142, 64),
                TextColor = Color.FromArgb(79, 55, 31),
                BorderColor = Color.FromArgb(210, 145, 65),
                BorderThickness = 1.2f,
                Depth = 5,
                Radius = LearnerDesignTokens.RadiusButton,
                Font = ChildVisualTheme.Font(11.5f, FontStyle.Bold),
                Enabled = false,
                AccessibleName = "Bắt đầu nhiệm vụ cứu hộ"
            };
            _startButton.Click += delegate { StartSelectedEvent(); };
            detail.Controls.Add(_startButton, 0, 4);
            detail.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Phần đã làm luôn được lưu.",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(8.8f)
            }, 0, 5);
            _heroLayout.Controls.Add(detail, 1, 0);
            hero.Controls.Add(_heroLayout);
            root.Controls.Add(hero, 0, 1);

            var stripCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 4),
                Padding = new Padding(8, 6, 8, 6),
                CardColor = Color.FromArgb(247, 248, 243),
                BorderColor = Color.FromArgb(220, 222, 211),
                Radius = LearnerDesignTokens.RadiusCard,
                ShowShadow = false
            };
            _eventStrip = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent
            };
            stripCard.Controls.Add(_eventStrip);
            root.Controls.Add(stripCard, 0, 2);
            root.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Nhiệm vụ khóa vẫn có thể chọn để xem chính xác bài nào cần học trước.",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9f),
                Padding = new Padding(4, 0, 0, 0),
                AccessibleName = "Giải thích nhiệm vụ khóa"
            }, 0, 3);
            Controls.Add(root);
        }

        private void LoadEvents()
        {
            try
            {
                _catalog = new MathLessonCatalogSource().Load(_catalogPath);
                _events.Clear();
                var eventPath = Path.Combine(Path.GetDirectoryName(_catalogPath), "game_events_v1.json");
                var eventCatalog = new MathGameEventCatalogSource().Load(eventPath, _catalogPath);
                foreach (var definition in eventCatalog.Events ?? new List<MathGameEventDefinition>())
                    _events.Add(MathQuickRescueEventPresentation.FromDefinition(definition));
                _access = new MathLessonProgressService(_context.LearningDatabase, _catalogPath)
                    .GetAllAccess(LearnerSessionService.PrimaryChildId).ToDictionary(x => x.LessonId, StringComparer.Ordinal);
                RefreshResumableSessionHint();
                PopulateEvents();
                var resumable = _events.FirstOrDefault(x => IsResumableEvent(x) && IsUnlocked(x));
                var preferred = _context.RequestedEvent == null ? null : _events.FirstOrDefault(x => string.Equals(x.Id, _context.RequestedEvent.Id, StringComparison.Ordinal) && IsUnlocked(x));
                var recommended = resumable ?? preferred ?? _events.FirstOrDefault(x => IsUnlocked(x) && !IsLessonCompleted(x)) ?? _events.FirstOrDefault(IsUnlocked) ?? _events.FirstOrDefault();
                if (recommended != null) SelectEvent(recommended);
                else ShowUnavailable("Các nhiệm vụ đầu đang chờ bài nền được mở. Con vẫn có thể học theo bài.");
                _summary.Text = _events.Count + " nhiệm vụ • mỗi nhiệm vụ 3 chặng • không đếm giờ";
            }
            catch
            {
                ShowUnavailable("Nhiệm vụ cứu hộ đang cần được kiểm tra lại. Con vẫn có thể học Toán theo bài như bình thường.");
            }
        }

        private void PopulateEvents()
        {
            _eventStrip.SuspendLayout();
            try
            {
                _eventStrip.Controls.Clear();
                foreach (var item in _events)
                {
                    var access = AccessFor(item);
                    var unlocked = access != null && access.IsUnlocked;
                    var completed = access != null && access.IsCompleted;
                    var resumable = IsResumableEvent(item);
                    var state = resumable ? "▶ Đang dở" : (completed ? "✓ Đã xong" : (unlocked ? "Sẵn sàng" : "🔒 Cần bài nền"));
                    var button = new RescueMissionButton
                    {
                        AutoSize = false,
                        Size = new Size(196, 58),
                        Margin = new Padding(4, 2, 4, 2),
                        Text = item.TitleVi + "\r\n" + state,
                        TitleText = item.TitleVi,
                        StateText = state,
                        AccentColor = AccentForTheme(item.Theme),
                        Locked = !unlocked,
                        Enabled = true,
                        AccessibleName = "Nhiệm vụ " + item.TitleVi,
                        AccessibleDescription = unlocked ? state + ". Chọn để xem nhiệm vụ." : BuildLockReason(access) + " Chọn để xem yêu cầu mở khóa.",
                        Tag = item.Id
                    };
                    var captured = item;
                    button.Click += delegate { SelectEvent(captured); };
                    _eventStrip.Controls.Add(button);
                }
            }
            finally { _eventStrip.ResumeLayout(true); }
        }

        private void SelectEvent(MathQuickRescueEventPresentation item)
        {
            if (item == null) return;
            _selectedEvent = item;
            _context.RequestedEvent = item;
            _eventTitle.Text = item.TitleVi;
            _intro.Text = item.IntroVi;
            _missionArt.Theme = item.Theme;
            _missionArt.AccessibleDescription = "Minh họa cho nhiệm vụ " + item.TitleVi + ".";
            _checkpoints.SuspendLayout();
            try
            {
                _checkpoints.Controls.Clear();
                for (var i = 0; i < 3; i++)
                {
                    _checkpoints.Controls.Add(new RescueCheckpointCard
                    {
                        AutoSize = false,
                        Width = 138,
                        Height = 60,
                        Margin = new Padding(3),
                        StepNumber = i + 1,
                        StepTitle = item.CheckpointName(i),
                        AccentColor = AccentForTheme(item.Theme),
                        AccessibleName = "Chặng " + (i + 1) + ": " + item.CheckpointName(i),
                        AccessibleDescription = "Một câu Toán. Không có giới hạn thời gian."
                    });
                }
            }
            finally { _checkpoints.ResumeLayout(true); ResizeCheckpoints(); }

            var access = AccessFor(item);
            var unlocked = access != null && access.IsUnlocked;
            var completed = access != null && access.IsCompleted;
            var resumable = IsResumableEvent(item);
            var lockReason = unlocked ? string.Empty : BuildLockReason(access);
            _status.Text = resumable
                ? "Chặng đang dở đã được lưu. Bé sẽ tiếp tục đúng chỗ, không phải làm lại từ đầu."
                : (completed
                    ? "Bài nền đã hoàn thành. Bé có thể chơi lại ba chặng mà tiến bộ cũ vẫn được giữ."
                    : (unlocked ? "Ba chặng, mỗi chặng một câu. Làm chắc từng bước là được." : lockReason));
            _status.ForeColor = unlocked ? ChildVisualTheme.MutedInk : Color.FromArgb(166, 91, 73);
            _startButton.Enabled = unlocked;
            _startButton.Text = resumable ? "Tiếp tục chặng đang dở" : (unlocked ? "Bắt đầu 3 chặng" : "Học bài nền trước");
            _startButton.AccessibleDescription = resumable
                ? "Tiếp tục đúng chặng Toán đang dở. Không có giới hạn thời gian."
                : (unlocked ? "Mở ba câu Toán của nhiệm vụ. Không có giới hạn thời gian." : lockReason);
            foreach (Control control in _eventStrip.Controls)
            {
                var button = control as RescueMissionButton;
                if (button != null) button.Selected = string.Equals(Convert.ToString(button.Tag), item.Id, StringComparison.Ordinal);
            }
        }

        private void StartSelectedEvent()
        {
            if (_selectedEvent == null || !IsUnlocked(_selectedEvent)) return;
            _context.RequestedLessonId = _selectedEvent.TargetLessonId;
            _context.RequestedEvent = _selectedEvent;
            _navigate(LearnerRoute.LessonPlay);
        }

        private void RefreshResumableSessionHint()
        {
            _resumableLessonId = null;
            _resumableSessionId = null;
            try
            {
                var runtime = new MathSessionRuntimeService(_context.LearningDatabase).LoadLatestResumable(LearnerSessionService.PrimaryChildId);
                if (runtime == null || !string.Equals(runtime.SessionMode, "lesson", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(runtime.TargetLessonId) ||
                    !string.Equals(runtime.PackId, MathSessionCoordinator.PackId, StringComparison.Ordinal) ||
                    !string.Equals(runtime.PackVersion, MathSessionCoordinator.PackVersion, StringComparison.Ordinal) ||
                    !_events.Any(x => string.Equals(x.TargetLessonId, runtime.TargetLessonId, StringComparison.Ordinal))) return;
                _resumableLessonId = runtime.TargetLessonId;
                _resumableSessionId = runtime.SessionId;
            }
            catch { }
        }

        private bool IsResumableEvent(MathQuickRescueEventPresentation item)
        {
            return item != null && !string.IsNullOrWhiteSpace(_resumableSessionId) && string.Equals(item.TargetLessonId, _resumableLessonId, StringComparison.Ordinal);
        }

        private MathLessonAccessSnapshot AccessFor(MathQuickRescueEventPresentation item)
        {
            MathLessonAccessSnapshot access;
            return item != null && _access != null && _access.TryGetValue(item.TargetLessonId, out access) ? access : null;
        }

        private bool IsUnlocked(MathQuickRescueEventPresentation item)
        {
            var access = AccessFor(item);
            return access != null && access.IsUnlocked;
        }

        private bool IsLessonCompleted(MathQuickRescueEventPresentation item)
        {
            var access = AccessFor(item);
            return access != null && access.IsCompleted;
        }

        private string BuildLockReason(MathLessonAccessSnapshot access)
        {
            var missing = access == null || access.UnsatisfiedPrerequisiteLessonIds == null
                ? new List<string>()
                : access.UnsatisfiedPrerequisiteLessonIds.Select(id => _catalog == null ? null : _catalog.FindLesson(id))
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.TitleVi)).Select(x => x.TitleVi).Distinct(StringComparer.Ordinal).ToList();
            return missing.Count == 0 ? "Nhiệm vụ này đang khóa vì còn bài nền cần hoàn thành trước." : "Cần hoàn thành trước: " + string.Join(", ", missing) + ".";
        }

        private void ShowUnavailable(string message)
        {
            _selectedEvent = null;
            _eventStrip.Controls.Clear();
            _eventTitle.Text = "Nhiệm vụ cứu hộ đang chuẩn bị";
            _intro.Text = message;
            _missionArt.Theme = null;
            _checkpoints.Controls.Clear();
            _status.Text = "Không có tiến bộ nào bị mất.";
            _startButton.Enabled = false;
            _startButton.Text = "Chưa thể bắt đầu";
        }

        private void ResizeCheckpoints()
        {
            if (_checkpoints == null) return;
            var availableWidth = Math.Max(1, _checkpoints.ClientSize.Width - _checkpoints.Padding.Horizontal);
            var width = Math.Max(100, (availableWidth - 24) / 3);
            foreach (Control control in _checkpoints.Controls)
            {
                control.Width = width;
                control.Height = 60;
            }
        }

        protected override void ApplyLayoutProfile(LearnerLayoutProfile profile)
        {
            if (_heroLayout == null) return;
            if (profile == LearnerLayoutProfile.Compact)
            {
                _heroLayout.ColumnStyles[0].Width = 48;
                _heroLayout.ColumnStyles[1].Width = 52;
            }
            else if (profile == LearnerLayoutProfile.Wide)
            {
                _heroLayout.ColumnStyles[0].Width = 60;
                _heroLayout.ColumnStyles[1].Width = 40;
            }
            else
            {
                _heroLayout.ColumnStyles[0].Width = 54;
                _heroLayout.ColumnStyles[1].Width = 46;
            }
            ResizeCheckpoints();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ResizeCheckpoints();
        }

        private static Color AccentForTheme(string theme)
        {
            switch ((theme ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "forest_path": return Color.FromArgb(91, 157, 103);
                case "hundred_station": return Color.FromArgb(92, 154, 189);
                case "number_path": return Color.FromArgb(209, 145, 73);
                case "place_value_workshop": return Color.FromArgb(146, 119, 176);
                case "number_machine": return Color.FromArgb(207, 112, 88);
                default: return ChildVisualTheme.MintStrong;
            }
        }
    }
}
