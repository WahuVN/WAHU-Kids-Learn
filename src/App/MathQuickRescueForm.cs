using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WAHU.Content;
using WAHU.Data;
using WAHU.Performance;
using WAHU.Session;

namespace WAHUKidsLearn
{
    internal sealed class MathQuickRescueEventPresentation
    {
        public MathQuickRescueEventPresentation(string id, string titleVi, string introVi, string completionVi,
            string targetLessonId, string theme, string repairCopyVi, string breakCopyVi, params string[] checkpointNounsVi)
        {
            Id = id;
            TitleVi = titleVi;
            IntroVi = introVi;
            CompletionVi = completionVi;
            TargetLessonId = targetLessonId;
            Theme = theme;
            RepairCopyVi = repairCopyVi;
            BreakCopyVi = breakCopyVi;
            CheckpointNounsVi = (checkpointNounsVi ?? new string[0]).ToList();
        }

        public string Id { get; private set; }
        public string TitleVi { get; private set; }
        public string IntroVi { get; private set; }
        public string CompletionVi { get; private set; }
        public string TargetLessonId { get; private set; }
        public string Theme { get; private set; }
        public string RepairCopyVi { get; private set; }
        public string BreakCopyVi { get; private set; }
        public IList<string> CheckpointNounsVi { get; private set; }

        public string CheckpointName(int zeroBasedIndex)
        {
            if (CheckpointNounsVi == null || CheckpointNounsVi.Count == 0)
                return "chặng " + (Math.Max(0, zeroBasedIndex) + 1);
            var index = Math.Max(0, Math.Min(CheckpointNounsVi.Count - 1, zeroBasedIndex));
            return CheckpointNounsVi[index];
        }

        public static MathQuickRescueEventPresentation FromDefinition(MathGameEventDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            return new MathQuickRescueEventPresentation(definition.Id, definition.TitleVi, definition.IntroVi,
                definition.CompletionVi, definition.TargetLessonId, definition.Theme, definition.RepairCopyVi,
                definition.BreakCopyVi, (definition.CheckpointNounsVi ?? new List<string>()).ToArray());
        }
    }

    internal static class MathQuickRescueFixtureCatalog
    {
        public static IList<MathQuickRescueEventPresentation> CreateAll()
        {
            return new List<MathQuickRescueEventPresentation>
            {
                new MathQuickRescueEventPresentation(
                    "m2_evt_number_sign_rescue_01",
                    "Sửa biển số trong Rừng Toán",
                    "Gió làm ba biển số bị lộn xộn. Con giúp đặt lại từng biển nhé.",
                    "Ba biển số đã về đúng chỗ. Đường trong rừng lại rõ ràng rồi.",
                    "m2_ls_num_count_read_write_0_1000",
                    "forest_path",
                    "Mình xem lại một bước nhỏ rồi sửa tiếp nhé.",
                    "Phần đã làm được lưu rồi. Khi nào muốn mình quay lại tiếp nhé.",
                    "biển số 1", "biển số 2", "biển số 3"),
                new MathQuickRescueEventPresentation(
                    "m2_evt_hundred_station_rescue_01",
                    "Khôi phục các trạm trăm",
                    "Ba trạm cần gắn lại nhãn số tròn trăm. Con giúp từng trạm sáng đúng nhãn nhé.",
                    "Ba trạm đã có nhãn đúng và cùng sáng trở lại.",
                    "m2_ls_num_full_hundreds_recognize",
                    "hundred_station",
                    "Mình làm từng bước nhé. Tìm dấu hiệu của số tròn trăm trước.",
                    "Phần đã làm được lưu rồi. Khi nào muốn mình quay lại tiếp nhé.",
                    "trạm trăm 1", "trạm trăm 2", "trạm trăm 3"),
                new MathQuickRescueEventPresentation(
                    "m2_evt_number_path_rescue_01",
                    "Nối lại đường số",
                    "Ba đoạn đường số đang thiếu mắt nối. Con đặt đúng số liền trước hoặc liền sau nhé.",
                    "Ba đoạn đường số đã nối liền và đi tiếp thật rõ ràng.",
                    "m2_ls_num_predecessor_successor",
                    "number_path",
                    "Mình làm từng bước nhé. Chỉ cần nhìn số đứng ngay trước hoặc ngay sau.",
                    "Phần đã làm được lưu rồi. Khi nào muốn mình quay lại tiếp nhé.",
                    "đoạn đường 1", "đoạn đường 2", "đoạn đường 3"),
                new MathQuickRescueEventPresentation(
                    "m2_evt_place_value_workshop_01",
                    "Sắp đúng kho hàng",
                    "Kho hàng có ba ngăn trăm, chục, đơn vị bị xáo vị trí. Con giúp xếp lại từng ngăn nhé.",
                    "Ba ngăn đã về đúng hàng. Kho số lại gọn gàng rồi.",
                    "m2_ls_place_value_hundreds_tens_ones",
                    "place_value_workshop",
                    "Mình làm từng bước nhé. Xác định hàng của chữ số trước.",
                    "Phần đã làm được lưu rồi. Khi nào muốn mình quay lại tiếp nhé.",
                    "ngăn hàng trăm", "ngăn hàng chục", "ngăn hàng đơn vị"),
                new MathQuickRescueEventPresentation(
                    "m2_evt_number_machine_rescue_01",
                    "Sửa máy ghép số",
                    "Máy ghép số có ba bộ phận cần nối lại dạng khai triển. Con sửa từng bộ phận nhé.",
                    "Ba bộ phận đã khớp lại. Máy ghép số hoạt động êm rồi.",
                    "m2_ls_num_expanded_form_hto",
                    "number_machine",
                    "Mình xem lại một bước nhỏ rồi ghép trăm, chục, đơn vị tiếp nhé.",
                    "Phần đã làm được lưu rồi. Khi nào muốn mình quay lại tiếp nhé.",
                    "bộ phận 1", "bộ phận 2", "bộ phận 3")
            };
        }

        public static bool ContainsLesson(string lessonId)
        {
            if (string.IsNullOrWhiteSpace(lessonId)) return false;
            return CreateAll().Any(x => string.Equals(x.TargetLessonId, lessonId, StringComparison.Ordinal));
        }
    }

    internal sealed class QuickRescueCheckpointStrip : Control
    {
        private int _completed;
        private int _activeIndex;
        private IList<string> _nouns = new List<string>();

        public QuickRescueCheckpointStrip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 24;
            AccessibleName = "Ba chặng nhiệm vụ cứu hộ";
        }

        public void SetState(int completed, int activeIndex, IList<string> nouns)
        {
            _completed = Math.Max(0, Math.Min(3, completed));
            _activeIndex = Math.Max(0, Math.Min(2, activeIndex));
            _nouns = nouns == null ? new List<string>() : nouns.ToList();
            var parts = new List<string>();
            for (var i = 0; i < 3; i++)
            {
                var name = i < _nouns.Count ? _nouns[i] : "chặng " + (i + 1);
                var state = i < _completed ? "đã xong" : (i == _activeIndex && _completed < 3 ? "đang làm" : "chưa làm");
                parts.Add(name + " " + state);
            }
            AccessibleDescription = string.Join("; ", parts) + ".";
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var centerY = Math.Max(8, Height / 2);
            var left = Math.Max(18, Width / 8);
            var right = Math.Max(left + 40, Width - left);
            var step = Math.Max(24, (right - left) / 2);
            using (var line = new Pen(Color.FromArgb(207, 216, 203), 4f))
                e.Graphics.DrawLine(line, left, centerY, left + step * 2, centerY);
            for (var i = 0; i < 3; i++)
            {
                var x = left + step * i;
                var done = i < _completed;
                var active = !done && i == _activeIndex && _completed < 3;
                var fillColor = done ? ChildVisualTheme.MintStrong : (active ? ChildVisualTheme.Sun : Color.FromArgb(226, 231, 221));
                using (var fill = new SolidBrush(fillColor)) e.Graphics.FillEllipse(fill, x - 8, centerY - 8, 16, 16);
                using (var border = new Pen(active ? Color.FromArgb(183, 143, 52) : Color.FromArgb(173, 187, 169), 2f))
                    e.Graphics.DrawEllipse(border, x - 8, centerY - 8, 16, 16);
            }
        }
    }

    internal sealed class RescueMissionButton : Button
    {
        private bool _hover;
        private bool _pressed;
        private bool _selected;
        private bool _locked;

        public string TitleText { get; set; }
        public string StateText { get; set; }
        public Color AccentColor { get; set; } = ChildVisualTheme.MintStrong;
        public bool Locked
        {
            get { return _locked; }
            set { _locked = value; Invalidate(); }
        }
        public bool Selected
        {
            get { return _selected; }
            set { _selected = value; Invalidate(); }
        }

        public RescueMissionButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { _pressed = false; Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var pressOffset = Enabled && _pressed ? 2 : 0;
            var rect = new Rectangle(2, 1 + pressOffset, Math.Max(1, Width - 4), Math.Max(1, Height - 7));
            var fill = _locked
                ? Color.FromArgb(242, 241, 235)
                : (_pressed ? ChildVisualTheme.Blend(Color.White, AccentColor, 0.13f)
                    : (_selected ? Color.FromArgb(246, 250, 241) : (_hover ? Color.FromArgb(251, 249, 241) : Color.White)));
            var border = _selected ? AccentColor : ChildVisualTheme.Blend(Color.FromArgb(226, 223, 210), AccentColor, _hover ? 0.18f : 0.06f);

            var shadowRect = new Rectangle(rect.X, 5, rect.Width, rect.Height);
            var shadowBase = ChildVisualTheme.Blend(AccentColor, ChildVisualTheme.Ink, 0.38f);
            using (var shadowPath = ChildVisualTheme.RoundedRect(shadowRect, 18))
            using (var shadow = new SolidBrush(Color.FromArgb(Enabled ? 45 : 24, shadowBase.R, shadowBase.G, shadowBase.B)))
                e.Graphics.FillPath(shadow, shadowPath);

            using (var path = ChildVisualTheme.RoundedRect(rect, 18))
            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(rect,
                ChildVisualTheme.Blend(fill, Color.White, _hover && !_pressed ? 0.34f : 0.20f), fill, 90f))
            using (var pen = new Pen(border, _selected ? 2f : 1f))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            var accent = new Rectangle(rect.Left + 9, rect.Top + 13, 7, Math.Max(24, rect.Height - 26));
            using (var path = ChildVisualTheme.RoundedRect(accent, 4))
            using (var brush = new SolidBrush(_locked ? Color.FromArgb(184, 188, 181) : AccentColor))
                e.Graphics.FillPath(brush, path);

            if (_selected)
            {
                var marker = new Rectangle(rect.Right - 21, rect.Top + 10, 10, 10);
                using (var outer = new SolidBrush(AccentColor)) e.Graphics.FillEllipse(outer, marker);
                var inner = Rectangle.Inflate(marker, -3, -3);
                using (var innerBrush = new SolidBrush(Color.White)) e.Graphics.FillEllipse(innerBrush, inner);
            }

            var titleRect = new Rectangle(rect.Left + 29, rect.Top + 11, Math.Max(60, rect.Width - 42), 32);
            var stateRect = new Rectangle(rect.Left + 29, rect.Top + 42, Math.Max(60, rect.Width - 42), Math.Max(22, rect.Height - 48));
            TextRenderer.DrawText(e.Graphics, TitleText ?? Text, ChildVisualTheme.Font(10.2f, FontStyle.Bold), titleRect,
                _locked ? ChildVisualTheme.MutedInk : ChildVisualTheme.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(e.Graphics, StateText ?? string.Empty, ChildVisualTheme.Font(8.6f), stateRect,
                _locked ? Color.FromArgb(132, 137, 132) : ChildVisualTheme.MutedInk,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (Focused && ShowFocusCues)
            {
                var focusRect = Rectangle.Inflate(rect, -4, -4);
                using (var focusPath = ChildVisualTheme.RoundedRect(focusRect, 14))
                using (var focusPen = new Pen(Color.FromArgb(205, ChildVisualTheme.Sun), 2f))
                    e.Graphics.DrawPath(focusPen, focusPath);
            }
        }
    }

    internal sealed class RescueCheckpointCard : Control
    {
        public int StepNumber { get; set; }
        public string StepTitle { get; set; }
        public Color AccentColor { get; set; } = ChildVisualTheme.MintStrong;

        public RescueCheckpointCard()
        {
            Height = 64;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
            using (var path = ChildVisualTheme.RoundedRect(rect, 16))
            using (var brush = new SolidBrush(Color.FromArgb(248, 251, 245)))
            using (var pen = new Pen(Color.FromArgb(221, 231, 216)))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            var bubble = new Rectangle(14, 14, 34, 34);
            using (var brush = new SolidBrush(AccentColor)) e.Graphics.FillEllipse(brush, bubble);
            TextRenderer.DrawText(e.Graphics, Math.Max(1, StepNumber).ToString(), ChildVisualTheme.Font(10f, FontStyle.Bold), bubble,
                Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            var titleRect = new Rectangle(60, 9, Math.Max(80, Width - 72), 27);
            var subRect = new Rectangle(60, 35, Math.Max(80, Width - 72), 20);
            TextRenderer.DrawText(e.Graphics, StepTitle ?? string.Empty, ChildVisualTheme.Font(10.2f, FontStyle.Bold), titleRect,
                ChildVisualTheme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(e.Graphics, "1 câu • không giới hạn thời gian", ChildVisualTheme.Font(8.5f), subRect,
                ChildVisualTheme.MutedInk, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    internal sealed class MathQuickRescueForm : Form
    {
        private readonly LearningDatabase _database;
        private readonly RuntimePerformanceSettings _performance;
        private readonly string _catalogPath;
        private readonly string _preferredLessonId;
        private readonly IList<MathQuickRescueEventPresentation> _events;
        private readonly bool _eventsInjected;
        private readonly Dictionary<string, Button> _eventButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private MathLessonCatalogSnapshot _catalog;
        private IDictionary<string, MathLessonAccessSnapshot> _access = new Dictionary<string, MathLessonAccessSnapshot>(StringComparer.Ordinal);
        private MathQuickRescueEventPresentation _selectedEvent;
        private string _resumableLessonId;
        private string _resumableSessionId;
        private FlowLayoutPanel _eventFlow;
        private Label _eventTitle;
        private Label _intro;
        private FlowLayoutPanel _checkpoints;
        private Label _status;
        private ChildActionButton _startButton;

        public MathQuickRescueForm(LearningDatabase database, RuntimePerformanceSettings performance)
            : this(database, performance, ResolveCatalogPath(), null, new List<MathQuickRescueEventPresentation>())
        {
        }

        internal MathQuickRescueForm(LearningDatabase database, RuntimePerformanceSettings performance, string catalogPath,
            string preferredLessonId, IList<MathQuickRescueEventPresentation> events)
        {
            _database = database ?? throw new ArgumentNullException("database");
            _performance = performance;
            _catalogPath = string.IsNullOrWhiteSpace(catalogPath) ? throw new ArgumentException("catalogPath") : catalogPath;
            _preferredLessonId = preferredLessonId;
            _events = events == null ? new List<MathQuickRescueEventPresentation>() : events.ToList();
            _eventsInjected = _events.Count > 0;

            Text = "Toán nhanh — Nhiệm vụ cứu hộ";
            AccessibleName = "Toán nhanh — Nhiệm vụ cứu hộ";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 640);
            ClientSize = new Size(1040, 720);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = ChildVisualTheme.Cream;
            Font = ChildVisualTheme.Font(10.5f);
            KeyPreview = true;
            DoubleBuffered = true;
            BuildUi();
            Shown += delegate { LoadEvents(); };
        }

        internal int EventCount { get { return _events.Count; } }
        internal string SelectedEventId { get { return _selectedEvent == null ? null : _selectedEvent.Id; } }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(28, 22, 28, 18),
                ColumnCount = 1,
                RowCount = 3,
                BackColor = ChildVisualTheme.Cream
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            header.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "3 CHẶNG • KHÔNG ĐẾM NGƯỢC",
                TextAlign = ContentAlignment.BottomLeft,
                Font = ChildVisualTheme.Font(8.8f, FontStyle.Bold),
                ForeColor = ChildVisualTheme.MintStrong
            }, 0, 0);
            var title = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Toán nhanh — Nhiệm vụ cứu hộ",
                TextAlign = ContentAlignment.TopLeft,
                Font = ChildVisualTheme.Font(21.5f, FontStyle.Bold),
                ForeColor = ChildVisualTheme.Ink,
                AccessibleName = "Toán nhanh — Nhiệm vụ cứu hộ"
            };
            header.Controls.Add(title, 0, 1);
            var exit = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(10, 10, 0, 10),
                Text = "Để sau",
                FillColor = Color.FromArgb(236, 233, 223),
                HoverColor = Color.FromArgb(224, 220, 208),
                PressedColor = Color.FromArgb(211, 207, 194),
                TextColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(9.8f, FontStyle.Bold),
                Radius = 18,
                AccessibleName = "Đóng nhiệm vụ cứu hộ",
                AccessibleDescription = "Đóng màn hình này. Không mất phần học đã lưu."
            };
            exit.Click += delegate { Close(); };
            CancelButton = exit;
            header.Controls.Add(exit, 1, 0);
            header.SetRowSpan(exit, 2);
            root.Controls.Add(header, 0, 0);

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63));

            _eventFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(8)
            };
            var listCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 10, 4),
                Padding = new Padding(10),
                CardColor = Color.FromArgb(250, 248, 239),
                BorderColor = Color.FromArgb(226, 223, 210),
                Radius = 22
            };
            var listLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0) };
            listLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            listLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            listLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            listLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "CHỌN NHIỆM VỤ",
                TextAlign = ContentAlignment.BottomLeft,
                Font = ChildVisualTheme.Font(8.8f, FontStyle.Bold),
                ForeColor = ChildVisualTheme.MintStrong,
                Padding = new Padding(6, 0, 0, 0)
            }, 0, 0);
            listLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Mỗi nhiệm vụ là một câu chuyện 3 chặng.",
                TextAlign = ContentAlignment.TopLeft,
                Font = ChildVisualTheme.Font(8.8f),
                ForeColor = ChildVisualTheme.MutedInk,
                Padding = new Padding(6, 2, 4, 0)
            }, 0, 1);
            listLayout.Controls.Add(_eventFlow, 0, 2);
            listCard.Controls.Add(listLayout);
            body.Controls.Add(listCard, 0, 0);

            var introCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 4, 0, 4),
                Padding = new Padding(26, 20, 26, 20),
                CardColor = Color.FromArgb(255, 253, 246),
                BorderColor = Color.FromArgb(226, 221, 204),
                Radius = 24
            };
            var introLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5 };
            introLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            introLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            introLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            introLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            introLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            introLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            introLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            _eventTitle = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Chọn một nhiệm vụ",
                TextAlign = ContentAlignment.MiddleLeft,
                Font = ChildVisualTheme.Font(19.5f, FontStyle.Bold),
                ForeColor = ChildVisualTheme.Ink,
                AccessibleName = "Tên nhiệm vụ cứu hộ"
            };
            introLayout.Controls.Add(_eventTitle, 0, 0);
            _intro = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Mỗi nhiệm vụ có ba chặng nhỏ. Không có đồng hồ đếm ngược và con có thể dừng bất cứ lúc nào.",
                TextAlign = ContentAlignment.TopLeft,
                Font = ChildVisualTheme.Font(11f),
                ForeColor = ChildVisualTheme.MutedInk,
                AccessibleName = "Câu chuyện nhiệm vụ cứu hộ"
            };
            introLayout.Controls.Add(_intro, 0, 1);
            var heroArt = new RescueHeroArtControl(_performance)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(12, 0, 0, 8)
            };
            introLayout.Controls.Add(heroArt, 1, 0);
            introLayout.SetRowSpan(heroArt, 2);
            _checkpoints = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown,
                AutoScroll = false,
                Padding = new Padding(8, 10, 8, 8)
            };
            introLayout.Controls.Add(_checkpoints, 0, 2);
            introLayout.SetColumnSpan(_checkpoints, 2);
            _status = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9.8f),
                AccessibleName = "Trạng thái nhiệm vụ cứu hộ"
            };
            introLayout.Controls.Add(_status, 0, 3);
            introLayout.SetColumnSpan(_status, 2);
            _startButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 0),
                Text = "Bắt đầu 3 chặng",
                FillColor = ChildVisualTheme.MintStrong,
                HoverColor = Color.FromArgb(90, 156, 103),
                PressedColor = Color.FromArgb(75, 139, 88),
                Font = ChildVisualTheme.Font(13f, FontStyle.Bold),
                Radius = 18,
                AccessibleName = "Bắt đầu nhiệm vụ cứu hộ ba chặng",
                AccessibleDescription = "Mở ba câu Toán của bài đã chọn. Không có giới hạn thời gian.",
                Enabled = false
            };
            _startButton.Click += delegate { StartSelectedEvent(); };
            AcceptButton = _startButton;
            introLayout.Controls.Add(_startButton, 0, 4);
            introLayout.SetColumnSpan(_startButton, 2);
            introCard.Controls.Add(introLayout);
            body.Controls.Add(introCard, 1, 0);
            root.Controls.Add(body, 0, 1);

            root.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Ba chặng ngắn • Không đếm ngược • Có thể nghỉ và quay lại đúng chỗ",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9.8f),
                AccessibleName = "Nguyên tắc nhiệm vụ cứu hộ"
            }, 0, 2);
            Controls.Add(root);
        }

        internal void LoadEvents()
        {
            try
            {
                _catalog = new MathLessonCatalogSource().Load(_catalogPath);
                if (!_eventsInjected)
                {
                    _events.Clear();
                    var eventPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_catalogPath), "game_events_v1.json");
                    var eventCatalog = new MathGameEventCatalogSource().Load(eventPath, _catalogPath);
                    foreach (var definition in eventCatalog.Events ?? new List<MathGameEventDefinition>())
                        _events.Add(MathQuickRescueEventPresentation.FromDefinition(definition));
                }
                _access = new MathLessonProgressService(_database, _catalogPath)
                    .GetAllAccess(LearnerSessionService.PrimaryChildId)
                    .ToDictionary(x => x.LessonId, StringComparer.Ordinal);
                RefreshResumableSessionHint();
                PopulateEventButtons();
                var resumable = _events.FirstOrDefault(x => IsResumableEvent(x) && IsUnlocked(x));
                var preferred = _events.FirstOrDefault(x => string.Equals(x.TargetLessonId, _preferredLessonId, StringComparison.Ordinal) && IsUnlocked(x));
                var recommended = resumable ?? preferred ?? _events.FirstOrDefault(x => IsUnlocked(x) && !IsLessonCompleted(x)) ?? _events.FirstOrDefault(IsUnlocked);
                if (recommended != null) SelectEvent(recommended);
                else ShowUnavailable("Các nhiệm vụ đầu đang chờ bài nền được mở. Con vẫn có thể học trong thư viện Toán.");
            }
            catch
            {
                ShowUnavailable("Nhiệm vụ cứu hộ đang cần được kiểm tra lại. Con vẫn có thể học Toán theo bài như bình thường.");
            }
        }

        private void PopulateEventButtons()
        {
            _eventFlow.SuspendLayout();
            try
            {
                _eventFlow.Controls.Clear();
                _eventButtons.Clear();
                foreach (var item in _events)
                {
                    var lesson = _catalog == null ? null : _catalog.FindLesson(item.TargetLessonId);
                    var access = AccessFor(item);
                    var unlocked = access != null && access.IsUnlocked;
                    var completed = access != null && access.IsCompleted;
                    var resumable = IsResumableEvent(item);
                    var lockReason = unlocked ? string.Empty : BuildLockReason(access);
                    var state = resumable ? "Đang dở • tiếp tục chặng đã lưu" :
                        (completed ? "Bài nền đã xong • có thể chơi" : (unlocked ? "Sẵn sàng • 3 chặng" : "Xem bài cần học trước"));
                    var captured = item;
                    var button = new RescueMissionButton
                    {
                        AutoSize = false,
                        Width = 300,
                        Height = 78,
                        Margin = new Padding(4, 5, 4, 5),
                        Text = item.TitleVi + "\r\n" + state,
                        TitleText = item.TitleVi,
                        StateText = state,
                        AccentColor = AccentForTheme(item.Theme),
                        Locked = !unlocked,
                        Enabled = true,
                        AccessibleName = "Nhiệm vụ " + item.TitleVi,
                        AccessibleDescription = (unlocked ? state : lockReason) + (lesson == null ? "." : " Bài Toán: " + lesson.TitleVi + ".")
                    };
                    button.Click += delegate { SelectEvent(captured); };
                    _eventButtons[item.Id] = button;
                    _eventFlow.Controls.Add(button);
                }
            }
            finally
            {
                _eventFlow.ResumeLayout();
                ResizeEventButtons();
            }
        }

        private void SelectEvent(MathQuickRescueEventPresentation item)
        {
            if (item == null) return;
            _selectedEvent = item;
            _eventTitle.Text = item.TitleVi;
            _eventTitle.AccessibleDescription = "Nhiệm vụ " + item.TitleVi + ".";
            _intro.Text = item.IntroVi;
            _intro.AccessibleDescription = item.IntroVi;
            _checkpoints.Controls.Clear();
            for (var i = 0; i < 3; i++)
            {
                var name = item.CheckpointName(i);
                _checkpoints.Controls.Add(new RescueCheckpointCard
                {
                    AutoSize = false,
                    Width = 500,
                    Height = 64,
                    Margin = new Padding(4, 4, 4, 4),
                    StepNumber = i + 1,
                    StepTitle = name,
                    AccentColor = AccentForTheme(item.Theme),
                    AccessibleName = "Chặng " + (i + 1) + ": " + name,
                    AccessibleDescription = "Một câu Toán. Không có giới hạn thời gian."
                });
            }
            var access = AccessFor(item);
            var unlocked = access != null && access.IsUnlocked;
            var completed = access != null && access.IsCompleted;
            var resumable = IsResumableEvent(item);
            var lockReason = unlocked ? string.Empty : BuildLockReason(access);
            _status.Text = resumable
                ? "Chặng đang dở đã được lưu. Con có thể tiếp tục đúng chỗ, không cần làm lại từ đầu."
                : (completed
                    ? "Bài nền của nhiệm vụ này đã hoàn thành. Con có thể bắt đầu ba chặng mà tiến bộ cũ vẫn được giữ nguyên."
                    : (unlocked ? "Ba chặng, mỗi chặng một câu. Làm chắc từng bước là được." : lockReason));
            _status.AccessibleDescription = _status.Text;
            _startButton.Enabled = unlocked;
            _startButton.Text = resumable ? "Tiếp tục chặng đang dở" : (unlocked ? "Bắt đầu 3 chặng" : "Học bài nền trước");
            _startButton.AccessibleName = resumable
                ? "Tiếp tục nhiệm vụ cứu hộ đang dở"
                : (unlocked ? "Bắt đầu nhiệm vụ cứu hộ ba chặng" : "Học bài nền trước để mở nhiệm vụ cứu hộ");
            _startButton.AccessibleDescription = resumable
                ? "Tiếp tục đúng chặng Toán đang dở đã được lưu. Không có giới hạn thời gian."
                : (unlocked ? "Mở ba câu Toán của bài đã chọn. Không có giới hạn thời gian." : lockReason);
            foreach (var pair in _eventButtons)
            {
                var missionButton = pair.Value as RescueMissionButton;
                if (missionButton != null) missionButton.Selected = string.Equals(pair.Key, item.Id, StringComparison.Ordinal);
            }
        }

        private void StartSelectedEvent()
        {
            if (_selectedEvent == null || !IsUnlocked(_selectedEvent)) return;
            try
            {
                using (var lesson = new MathLessonForm(_database, _performance, _selectedEvent.TargetLessonId, _selectedEvent))
                    lesson.ShowDialog(this);
                LoadEvents();
            }
            catch
            {
                _status.Text = "Chưa thể mở nhiệm vụ lúc này. Phần học đã lưu vẫn an toàn; con có thể quay lại thư viện Toán.";
                _status.AccessibleDescription = _status.Text;
            }
        }

        private void RefreshResumableSessionHint()
        {
            _resumableLessonId = null;
            _resumableSessionId = null;
            try
            {
                var runtime = new MathSessionRuntimeService(_database)
                    .LoadLatestResumable(LearnerSessionService.PrimaryChildId);
                if (runtime == null || !string.Equals(runtime.SessionMode, "lesson", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(runtime.TargetLessonId) ||
                    !string.Equals(runtime.PackId, MathSessionCoordinator.PackId, StringComparison.Ordinal) ||
                    !string.Equals(runtime.PackVersion, MathSessionCoordinator.PackVersion, StringComparison.Ordinal) ||
                    !_events.Any(x => string.Equals(x.TargetLessonId, runtime.TargetLessonId, StringComparison.Ordinal)))
                    return;
                _resumableLessonId = runtime.TargetLessonId;
                _resumableSessionId = runtime.SessionId;
            }
            catch (MathSessionRuntimeCorruptException)
            {
            }
            catch
            {
            }
        }

        private bool IsResumableEvent(MathQuickRescueEventPresentation item)
        {
            return item != null && !string.IsNullOrWhiteSpace(_resumableSessionId) &&
                string.Equals(item.TargetLessonId, _resumableLessonId, StringComparison.Ordinal);
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

        private string BuildLockReason(MathLessonAccessSnapshot access)
        {
            var missing = access == null || access.UnsatisfiedPrerequisiteLessonIds == null
                ? new List<string>()
                : access.UnsatisfiedPrerequisiteLessonIds
                    .Select(id => _catalog == null ? null : _catalog.FindLesson(id))
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.TitleVi))
                    .Select(x => x.TitleVi)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            return missing.Count == 0
                ? "Nhiệm vụ này đang khóa vì còn bài nền cần hoàn thành trước."
                : "Cần hoàn thành trước: " + string.Join(", ", missing) + ".";
        }

        private bool IsLessonCompleted(MathQuickRescueEventPresentation item)
        {
            var access = AccessFor(item);
            return access != null && access.IsCompleted;
        }

        private void ShowUnavailable(string message)
        {
            _selectedEvent = null;
            _resumableLessonId = null;
            _resumableSessionId = null;
            _catalog = null;
            _access.Clear();
            _eventButtons.Clear();
            if (!_eventsInjected) _events.Clear();
            _eventFlow.Controls.Clear();
            _eventTitle.Text = "Nhiệm vụ cứu hộ đang chuẩn bị";
            _eventTitle.AccessibleDescription = "Danh sách nhiệm vụ cứu hộ hiện chưa sẵn sàng.";
            _intro.Text = message;
            _intro.AccessibleDescription = message;
            _checkpoints.Controls.Clear();
            _status.Text = "Không có tiến bộ nào bị mất.";
            _status.AccessibleDescription = "Phần học đã lưu vẫn an toàn.";
            _startButton.Enabled = false;
            _startButton.Text = "Chưa thể bắt đầu";
            _startButton.AccessibleName = "Nhiệm vụ cứu hộ chưa thể bắt đầu";
            _startButton.AccessibleDescription = "Nhiệm vụ cứu hộ hiện chưa thể bắt đầu. Con vẫn có thể học Toán theo bài như bình thường.";
        }

        private void ResizeEventButtons()
        {
            if (_eventFlow != null && !_eventFlow.IsDisposed)
            {
                var width = Math.Max(220, _eventFlow.ClientSize.Width - _eventFlow.Padding.Horizontal - 24);
                foreach (Control control in _eventFlow.Controls) control.Width = width;
            }
            if (_checkpoints != null && !_checkpoints.IsDisposed)
            {
                var checkpointWidth = Math.Max(300, _checkpoints.ClientSize.Width - _checkpoints.Padding.Horizontal - 20);
                foreach (Control control in _checkpoints.Controls) control.Width = checkpointWidth;
            }
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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ResizeEventButtons();
        }

        private static string ResolveCatalogPath()
        {
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "content_packs", "math_grade2_v1", "lesson_catalog_v1.json");
        }
    }
}
