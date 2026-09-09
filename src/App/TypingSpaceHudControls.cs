using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace WAHUKidsLearn
{
    internal enum TypingSpaceHudFeedbackState
    {
        Neutral = 0,
        Correct = 1,
        Wrong = 2,
        Completed = 3,
        Paused = 4
    }

    // UI-only mirror of the frozen Typing Space contract. This class intentionally does not
    // normalize Vietnamese input or decide whether a key is correct; AI01/AI07 own those rules.
    internal sealed class TypingSpaceHudState
    {
        public string TargetId { get; set; }
        public string DisplayText { get; set; }
        public string Language { get; set; }
        public string NextKey { get; set; }
        public string LastRawKey { get; set; }
        public int TypedCount { get; set; }
        public int TotalCount { get; set; }
        public int ErrorCount { get; set; }
        public bool Completed { get; set; }
        public bool Paused { get; set; }
        public TypingSpaceHudFeedbackState Feedback { get; set; }

        public static TypingSpaceHudState CreateMock()
        {
            return new TypingSpaceHudState
            {
                TargetId = "mock_target_01",
                DisplayText = "apple",
                Language = "en",
                NextKey = "a",
                LastRawKey = string.Empty,
                TypedCount = 0,
                TotalCount = 5,
                ErrorCount = 0,
                Completed = false,
                Paused = false,
                Feedback = TypingSpaceHudFeedbackState.Neutral
            };
        }
    }

    internal sealed class TypingSpaceTargetDisplayControl : Control
    {
        private string _text = string.Empty;
        private int _typedCount;
        private bool _completed;

        public TypingSpaceTargetDisplayControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            AccessibleName = "Từ đang gõ";
        }

        public void SetTarget(string text, int typedCount, bool completed)
        {
            _text = text ?? string.Empty;
            _typedCount = Math.Max(0, Math.Min(_text.Length, typedCount));
            _completed = completed;
            AccessibleDescription = BuildAccessibleDescription();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 20 || Height < 20) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var fontSize = Width < 560 ? 24f : 30f;
            using (var font = ChildVisualTheme.Font(fontSize, FontStyle.Bold))
            {
                var typed = _text.Substring(0, Math.Min(_typedCount, _text.Length));
                var next = _typedCount < _text.Length ? _text.Substring(_typedCount, 1) : string.Empty;
                var restStart = Math.Min(_text.Length, _typedCount + next.Length);
                var rest = _text.Substring(restStart);
                var totalSize = TextRenderer.MeasureText(_text, font, Size.Empty,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                var x = Math.Max(8, (Width - totalSize.Width) / 2);
                var y = Math.Max(4, (Height - totalSize.Height) / 2);

                if (!string.IsNullOrEmpty(typed))
                {
                    var size = TextRenderer.MeasureText(typed, font, Size.Empty,
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    TextRenderer.DrawText(g, typed, font, new Point(x, y),
                        _completed ? ChildVisualTheme.MintStrong : Color.FromArgb(80, 159, 113),
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    x += size.Width;
                }
                if (!string.IsNullOrEmpty(next))
                {
                    var size = TextRenderer.MeasureText(next, font, Size.Empty,
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    var marker = new Rectangle(x - 3, y + totalSize.Height - 5, Math.Max(8, size.Width + 6), 4);
                    using (var markerBrush = new SolidBrush(ChildVisualTheme.Sun)) g.FillRectangle(markerBrush, marker);
                    TextRenderer.DrawText(g, next, font, new Point(x, y), ChildVisualTheme.Ink,
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    x += size.Width;
                }
                if (!string.IsNullOrEmpty(rest))
                    TextRenderer.DrawText(g, rest, font, new Point(x, y), ChildVisualTheme.MutedInk,
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }
        }

        private string BuildAccessibleDescription()
        {
            if (string.IsNullOrEmpty(_text)) return "Chưa có từ cần gõ.";
            if (_completed) return "Đã gõ xong từ " + _text + ".";
            var next = _typedCount < _text.Length ? _text.Substring(_typedCount, 1) : string.Empty;
            return "Đang gõ từ " + _text + ". Đã gõ " + _typedCount + " trên " + _text.Length +
                " ký tự. Phím tiếp theo là " + next + ".";
        }
    }

    internal sealed class TypingSpaceHudControl : UserControl
    {
        private readonly Label _languageBadge;
        private readonly Label _progressLabel;
        private readonly ProgressBar _progressBar;
        private readonly TypingSpaceTargetDisplayControl _target;
        private readonly Label _feedback;
        private readonly ChildActionButton _pauseButton;
        private TypingSpaceHudState _state;

        public TypingSpaceHudControl()
        {
            BackColor = ChildVisualTheme.Cream;
            AccessibleName = "HUD game phi thuyền gõ phím";
            MinimumSize = new Size(520, 210);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty,
                Padding = new Padding(12, 8, 12, 8),
                BackColor = Color.Transparent
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

            var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _languageBadge = new Label
            {
                Dock = DockStyle.Fill,
                Text = "ENGLISH",
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = ChildVisualTheme.Sky,
                ForeColor = ChildVisualTheme.SkyStrong,
                Margin = new Padding(0, 3, 8, 3),
                Font = ChildVisualTheme.Font(9.5f, FontStyle.Bold),
                AccessibleName = "Ngôn ngữ hiện tại"
            };
            top.Controls.Add(_languageBadge, 0, 0);

            _progressLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "0 / 0 ký tự",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(10f, FontStyle.Bold),
                AccessibleName = "Tiến độ từ đang gõ"
            };
            top.Controls.Add(_progressLabel, 1, 0);

            _pauseButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Text = "Tạm nghỉ",
                Margin = new Padding(8, 0, 0, 0),
                FillColor = Color.FromArgb(238, 235, 224),
                HoverColor = Color.FromArgb(228, 224, 210),
                PressedColor = Color.FromArgb(216, 211, 196),
                TextColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(9.5f, FontStyle.Bold),
                Radius = LearnerDesignTokens.RadiusButton,
                AccessibleName = "Tạm nghỉ game",
                AccessibleDescription = "Tạm dừng game. Tiến độ hiện tại không bị mất."
            };
            _pauseButton.Click += delegate
            {
                var handler = PauseRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            };
            top.Controls.Add(_pauseButton, 2, 0);
            root.Controls.Add(top, 0, 0);

            _target = new TypingSpaceTargetDisplayControl { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 2) };
            root.Controls.Add(_target, 0, 1);

            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Margin = new Padding(60, 5, 60, 5),
                AccessibleName = "Thanh tiến độ từ"
            };
            root.Controls.Add(_progressBar, 0, 2);

            _feedback = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Gõ phím sáng tiếp theo nhé.",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(10f, FontStyle.Bold),
                AccessibleName = "Phản hồi gõ phím"
            };
            root.Controls.Add(_feedback, 0, 3);
            Controls.Add(root);
            SetState(TypingSpaceHudState.CreateMock());
        }

        public event EventHandler PauseRequested;

        internal string FeedbackText { get { return _feedback.Text; } }
        internal string LanguageText { get { return _languageBadge.Text; } }
        internal int ProgressValue { get { return _progressBar.Value; } }
        internal int PauseTouchHeight { get { return _pauseButton.Height; } }
        internal Control PauseButton { get { return _pauseButton; } }

        public void SetState(TypingSpaceHudState state)
        {
            _state = state ?? TypingSpaceHudState.CreateMock();
            var total = Math.Max(0, _state.TotalCount > 0 ? _state.TotalCount : (_state.DisplayText ?? string.Empty).Length);
            var typed = Math.Max(0, Math.Min(total, _state.TypedCount));
            _languageBadge.Text = string.Equals(_state.Language, "vi", StringComparison.OrdinalIgnoreCase) ? "TIẾNG VIỆT" : "ENGLISH";
            _languageBadge.BackColor = string.Equals(_state.Language, "vi", StringComparison.OrdinalIgnoreCase)
                ? ChildVisualTheme.Mint : ChildVisualTheme.Sky;
            _languageBadge.ForeColor = string.Equals(_state.Language, "vi", StringComparison.OrdinalIgnoreCase)
                ? ChildVisualTheme.MintStrong : ChildVisualTheme.SkyStrong;
            _progressLabel.Text = typed + " / " + total + " ký tự";
            _progressBar.Value = total <= 0 ? 0 : Math.Max(0, Math.Min(100, (int)Math.Round(typed * 100d / total)));
            _target.SetTarget(_state.DisplayText, typed, _state.Completed);
            _pauseButton.Text = _state.Paused ? "Học tiếp" : "Tạm nghỉ";
            _pauseButton.AccessibleName = _state.Paused ? "Tiếp tục game" : "Tạm nghỉ game";
            ApplyFeedback(_state);
            AccessibleDescription = BuildDescription(_state, typed, total);
        }

        private void ApplyFeedback(TypingSpaceHudState state)
        {
            if (state.Paused || state.Feedback == TypingSpaceHudFeedbackState.Paused)
            {
                _feedback.Text = "Đang nghỉ. Khi sẵn sàng mình tiếp tục nhé.";
                _feedback.ForeColor = ChildVisualTheme.MutedInk;
                return;
            }
            if (state.Completed || state.Feedback == TypingSpaceHudFeedbackState.Completed)
            {
                _feedback.Text = "Xong rồi! Phi thuyền đã nhận năng lượng.";
                _feedback.ForeColor = ChildVisualTheme.MintStrong;
                return;
            }
            if (state.Feedback == TypingSpaceHudFeedbackState.Correct)
            {
                _feedback.Text = "Đúng rồi — tiếp tục nhé!";
                _feedback.ForeColor = ChildVisualTheme.MintStrong;
                return;
            }
            if (state.Feedback == TypingSpaceHudFeedbackState.Wrong)
            {
                _feedback.Text = state.ErrorCount >= 2 && !string.IsNullOrWhiteSpace(state.NextKey)
                    ? "Gần đúng rồi. Nhìn phím " + state.NextKey.ToUpperInvariant() + " đang sáng nhé."
                    : "Chưa đúng phím này. Thử lại nhẹ nhàng nhé.";
                _feedback.ForeColor = ChildVisualTheme.PeachStrong;
                return;
            }
            _feedback.Text = string.IsNullOrWhiteSpace(state.NextKey)
                ? "Sẵn sàng cho mục tiêu tiếp theo."
                : "Gõ phím sáng tiếp theo nhé.";
            _feedback.ForeColor = ChildVisualTheme.MutedInk;
        }

        private static string BuildDescription(TypingSpaceHudState state, int typed, int total)
        {
            var language = string.Equals(state.Language, "vi", StringComparison.OrdinalIgnoreCase) ? "Tiếng Việt" : "Tiếng Anh";
            var next = string.IsNullOrWhiteSpace(state.NextKey) ? "chưa có" : state.NextKey;
            return language + ". Từ hiện tại " + (state.DisplayText ?? string.Empty) + ". Đã gõ " + typed +
                " trên " + total + " ký tự. Phím tiếp theo " + next + ".";
        }
    }

    internal sealed class TypingSpaceKeyboardControl : Control
    {
        private sealed class KeyCell
        {
            public string Key;
            public Rectangle Bounds;
        }

        private static readonly string[] Rows = { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
        private readonly List<KeyCell> _cells = new List<KeyCell>();
        private string _highlightedKey = "A";
        private string _wrongKey = string.Empty;
        private TypingSpaceHudFeedbackState _feedback;
        private int _errorCount;
        private int _focusedIndex;
        private int _minimumRenderedKeyWidth;
        private int _minimumRenderedKeyHeight;

        public TypingSpaceKeyboardControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            TabStop = true;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            MinimumSize = new Size(560, 200);
            AccessibleName = "Bàn phím gợi ý trên màn hình";
            UpdateAccessibleDescription();
        }

        public event EventHandler<TypingSpaceOnScreenKeyEventArgs> OnScreenKeyPressed;

        internal string HighlightedKey { get { return _highlightedKey; } }
        internal bool HintVisible { get { return _errorCount >= 2 && !string.IsNullOrWhiteSpace(_highlightedKey); } }
        internal int MinimumRenderedKeyWidth { get { EnsureLayout(); return _minimumRenderedKeyWidth; } }
        internal int MinimumRenderedKeyHeight { get { EnsureLayout(); return _minimumRenderedKeyHeight; } }
        internal int KeyCount { get { EnsureLayout(); return _cells.Count; } }

        public void SetVisualState(string nextKey, TypingSpaceHudFeedbackState feedback, string wrongKey, int errorCount)
        {
            _highlightedKey = (nextKey ?? string.Empty).Trim().ToUpperInvariant();
            _wrongKey = (wrongKey ?? string.Empty).Trim().ToUpperInvariant();
            _feedback = feedback;
            _errorCount = Math.Max(0, errorCount);
            var index = _cells.FindIndex(x => string.Equals(x.Key, _highlightedKey, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) _focusedIndex = index;
            UpdateAccessibleDescription();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            BuildLayout();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            BuildLayout();
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var cell in _cells) DrawKey(g, cell);

            if (HintVisible)
            {
                var hint = "Phím tiếp theo: " + _highlightedKey;
                ChildVisualTheme.DrawTextWithOwnedFont(g, hint, ChildVisualTheme.Font(9.5f, FontStyle.Bold),
                    new Rectangle(8, Math.Max(0, Height - 30), Math.Max(20, Width - 16), 24), ChildVisualTheme.PeachStrong,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            EnsureLayout();
            for (var i = 0; i < _cells.Count; i++)
            {
                if (!_cells[i].Bounds.Contains(e.Location)) continue;
                _focusedIndex = i;
                RaiseKey(_cells[i].Key);
                Invalidate();
                return;
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            var key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            EnsureLayout();
            if (_cells.Count == 0) return;
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Up)
            {
                _focusedIndex = (_focusedIndex - 1 + _cells.Count) % _cells.Count;
                e.Handled = true;
                Invalidate();
                return;
            }
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Down)
            {
                _focusedIndex = (_focusedIndex + 1) % _cells.Count;
                e.Handled = true;
                Invalidate();
                return;
            }
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                RaiseKey(_cells[Math.Max(0, Math.Min(_cells.Count - 1, _focusedIndex))].Key);
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        private void BuildLayout()
        {
            _cells.Clear();
            _minimumRenderedKeyWidth = int.MaxValue;
            _minimumRenderedKeyHeight = int.MaxValue;
            var footer = HintVisible ? 34 : 8;
            var availableHeight = Math.Max(90, Height - footer - 8);
            const int gap = 6;
            var rowHeight = Math.Max(32, (availableHeight - gap * 2) / 3);
            var maxCount = 10;
            var availableWidth = Math.Max(120, Width - 16);
            var keyWidth = Math.Max(32, (availableWidth - gap * (maxCount - 1)) / maxCount);
            var y = 6;
            foreach (var row in Rows)
            {
                var rowWidth = row.Length * keyWidth + Math.Max(0, row.Length - 1) * gap;
                var x = Math.Max(4, (Width - rowWidth) / 2);
                foreach (var ch in row)
                {
                    var bounds = new Rectangle(x, y, keyWidth, rowHeight);
                    _cells.Add(new KeyCell { Key = ch.ToString(), Bounds = bounds });
                    _minimumRenderedKeyWidth = Math.Min(_minimumRenderedKeyWidth, bounds.Width);
                    _minimumRenderedKeyHeight = Math.Min(_minimumRenderedKeyHeight, bounds.Height);
                    x += keyWidth + gap;
                }
                y += rowHeight + gap;
            }
            if (_minimumRenderedKeyWidth == int.MaxValue) _minimumRenderedKeyWidth = 0;
            if (_minimumRenderedKeyHeight == int.MaxValue) _minimumRenderedKeyHeight = 0;
            _focusedIndex = Math.Max(0, Math.Min(Math.Max(0, _cells.Count - 1), _focusedIndex));
        }

        private void EnsureLayout()
        {
            if (_cells.Count == 0) BuildLayout();
        }

        private void DrawKey(Graphics g, KeyCell cell)
        {
            var highlighted = string.Equals(cell.Key, _highlightedKey, StringComparison.OrdinalIgnoreCase);
            var wrong = _feedback == TypingSpaceHudFeedbackState.Wrong &&
                string.Equals(cell.Key, _wrongKey, StringComparison.OrdinalIgnoreCase);
            var correct = _feedback == TypingSpaceHudFeedbackState.Correct && highlighted;
            var focused = Focused && _focusedIndex >= 0 && _focusedIndex < _cells.Count && ReferenceEquals(_cells[_focusedIndex], cell);

            var fill = highlighted ? Color.FromArgb(255, 244, 194) : Color.FromArgb(250, 251, 247);
            var border = highlighted ? Color.FromArgb(213, 165, 42) : Color.FromArgb(199, 210, 207);
            if (correct)
            {
                fill = Color.FromArgb(221, 244, 225);
                border = ChildVisualTheme.MintStrong;
            }
            else if (wrong)
            {
                // Wrong feedback is deliberately local and muted: never flash the full screen red.
                fill = Color.FromArgb(255, 239, 226);
                border = Color.FromArgb(213, 139, 94);
            }

            var shadow = new Rectangle(cell.Bounds.X, cell.Bounds.Y + 3, cell.Bounds.Width, cell.Bounds.Height);
            using (var path = ChildVisualTheme.RoundedRect(shadow, 10))
            using (var brush = new SolidBrush(Color.FromArgb(32, 70, 82, 78))) g.FillPath(brush, path);
            using (var path = ChildVisualTheme.RoundedRect(cell.Bounds, 10))
            using (var brush = new LinearGradientBrush(cell.Bounds, ChildVisualTheme.Blend(fill, Color.White, 0.24f), fill, 90f))
            using (var pen = new Pen(border, highlighted || focused ? 2f : 1.2f))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
            if (focused)
            {
                var focus = Rectangle.Inflate(cell.Bounds, -4, -4);
                using (var path = ChildVisualTheme.RoundedRect(focus, 7))
                using (var pen = new Pen(ChildVisualTheme.SkyStrong, 1.5f)) g.DrawPath(pen, path);
            }
            ChildVisualTheme.DrawTextWithOwnedFont(g, cell.Key, ChildVisualTheme.Font(11f, FontStyle.Bold), cell.Bounds,
                ChildVisualTheme.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        private void RaiseKey(string key)
        {
            var handler = OnScreenKeyPressed;
            if (handler != null) handler(this, new TypingSpaceOnScreenKeyEventArgs(key));
        }

        private void UpdateAccessibleDescription()
        {
            AccessibleDescription = HintVisible
                ? "Bàn phím gợi ý. Phím tiếp theo là " + _highlightedKey + ". Có thể dùng phím mũi tên rồi Enter, hoặc chạm vào phím."
                : "Bàn phím gợi ý. Phím đang sáng là " + (_highlightedKey.Length == 0 ? "chưa có" : _highlightedKey) +
                  ". Có thể dùng bàn phím vật lý hoặc chạm trên màn hình.";
        }
    }

    internal sealed class TypingSpaceOnScreenKeyEventArgs : EventArgs
    {
        public TypingSpaceOnScreenKeyEventArgs(string rawKey)
        {
            RawKey = rawKey ?? string.Empty;
            TimestampUtc = DateTime.UtcNow;
        }

        public string RawKey { get; private set; }
        public DateTime TimestampUtc { get; private set; }
    }

    internal sealed class TypingSpaceHudDemoPanel : UserControl
    {
        private readonly TypingSpaceHudControl _hud;
        private readonly TypingSpaceKeyboardControl _keyboard;
        private TypingSpaceHudState _state;

        public TypingSpaceHudDemoPanel()
        {
            BackColor = ChildVisualTheme.Cream;
            AccessibleName = "Bản demo HUD và bàn phím Typing Space";
            MinimumSize = new Size(620, 440);
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12),
                Margin = Padding.Empty,
                BackColor = ChildVisualTheme.Cream
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 52));

            var hudCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(8),
                CardColor = Color.FromArgb(255, 254, 248),
                BorderColor = Color.FromArgb(217, 225, 216),
                Radius = LearnerDesignTokens.RadiusCard
            };
            _hud = new TypingSpaceHudControl { Dock = DockStyle.Fill };
            hudCard.Controls.Add(_hud);
            root.Controls.Add(hudCard, 0, 0);

            var keyboardCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 0),
                Padding = new Padding(10),
                CardColor = Color.FromArgb(249, 251, 252),
                BorderColor = Color.FromArgb(205, 220, 228),
                Radius = LearnerDesignTokens.RadiusCard
            };
            _keyboard = new TypingSpaceKeyboardControl { Dock = DockStyle.Fill };
            keyboardCard.Controls.Add(_keyboard);
            root.Controls.Add(keyboardCard, 0, 1);
            Controls.Add(root);

            SetState(TypingSpaceHudState.CreateMock());
        }

        internal TypingSpaceHudControl Hud { get { return _hud; } }
        internal TypingSpaceKeyboardControl Keyboard { get { return _keyboard; } }

        public void SetState(TypingSpaceHudState state)
        {
            _state = state ?? TypingSpaceHudState.CreateMock();
            _hud.SetState(_state);
            _keyboard.SetVisualState(_state.NextKey, _state.Feedback, _state.LastRawKey, _state.ErrorCount);
        }
    }
}