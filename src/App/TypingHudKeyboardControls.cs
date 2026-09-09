using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace WAHUKidsLearn
{
    internal enum TypingHudFeedbackKind
    {
        None,
        Correct,
        Wrong,
        Completed
    }

    // AI05 presentation projection only. AI10 maps the frozen TypingTarget / TypingProgress /
    // GamePhase contract into this snapshot; AI05 deliberately does not perform matching or
    // Vietnamese normalization.
    internal sealed class TypingHudSnapshot
    {
        public string TargetId { get; set; }
        public string TargetDisplayText { get; set; }
        public string Language { get; set; }
        public int TypedCount { get; set; }
        public int TotalCount { get; set; }
        public bool Completed { get; set; }
        public int ErrorCount { get; set; }
        public string NextKey { get; set; }
        public string Phase { get; set; }
        public TypingHudFeedbackKind FeedbackKind { get; set; }
        public string LastRawKey { get; set; }
    }

    internal sealed class TypingOnScreenKeyEventArgs : EventArgs
    {
        public TypingOnScreenKeyEventArgs(string rawKey, long timestamp)
        {
            RawKey = rawKey ?? string.Empty;
            Timestamp = timestamp;
        }

        public string RawKey { get; private set; }
        public long Timestamp { get; private set; }
        public string Source { get { return "onscreen"; } }
    }

    internal sealed class TypingKeyboardKeyButton : ChildActionButton
    {
        public TypingKeyboardKeyButton(string token, string inputValue)
        {
            Token = token ?? string.Empty;
            InputValue = inputValue ?? string.Empty;
            Text = DisplayToken(Token);
            Dock = DockStyle.Fill;
            Margin = new Padding(3);
            Padding = Padding.Empty;
            Font = ChildVisualTheme.Font(Token == "SPACE" ? 10f : 12f, FontStyle.Bold);
            TextAlign = ContentAlignment.MiddleCenter;
            Radius = 13;
            Depth = 3;
            ShowDepth = true;
            TabStop = true;
            AccessibleName = "Phím " + AccessibleToken(Token);
            AccessibleDescription = "Phím bàn phím ảo " + AccessibleToken(Token) + ".";
            ApplyVisual(false, false, false);
        }

        public string Token { get; private set; }
        public string InputValue { get; private set; }
        internal bool IsNextKey { get; private set; }
        internal bool IsWrongPulse { get; private set; }
        internal bool IsCorrectPulse { get; private set; }

        public void ApplyVisual(bool next, bool wrongPulse, bool correctPulse)
        {
            IsNextKey = next;
            IsWrongPulse = wrongPulse;
            IsCorrectPulse = correctPulse;

            if (wrongPulse)
            {
                FillColor = Color.FromArgb(255, 226, 219);
                HoverColor = Color.FromArgb(255, 220, 212);
                PressedColor = Color.FromArgb(249, 207, 198);
                TextColor = Color.FromArgb(128, 73, 65);
                BorderColor = next ? ChildVisualTheme.MintStrong : Color.FromArgb(222, 149, 138);
                BorderThickness = next ? 2.2f : 1.4f;
            }
            else if (correctPulse)
            {
                FillColor = Color.FromArgb(212, 242, 222);
                HoverColor = Color.FromArgb(205, 238, 216);
                PressedColor = Color.FromArgb(194, 229, 206);
                TextColor = Color.FromArgb(40, 112, 70);
                BorderColor = ChildVisualTheme.MintStrong;
                BorderThickness = 2f;
            }
            else if (next)
            {
                FillColor = ChildVisualTheme.MintStrong;
                HoverColor = Color.FromArgb(78, 171, 111);
                PressedColor = Color.FromArgb(57, 143, 91);
                TextColor = Color.White;
                BorderColor = Color.FromArgb(39, 122, 77);
                BorderThickness = 2.2f;
            }
            else
            {
                FillColor = Color.FromArgb(250, 252, 248);
                HoverColor = Color.FromArgb(240, 248, 244);
                PressedColor = Color.FromArgb(229, 241, 235);
                TextColor = ChildVisualTheme.Ink;
                BorderColor = Color.FromArgb(204, 216, 209);
                BorderThickness = 1f;
            }

            AccessibleDescription = next
                ? "Phím " + AccessibleToken(Token) + " cần bấm tiếp theo."
                : "Phím bàn phím ảo " + AccessibleToken(Token) + ".";
            Invalidate();
        }

        private static string DisplayToken(string token)
        {
            return string.Equals(token, "SPACE", StringComparison.OrdinalIgnoreCase) ? "SPACE" : token;
        }

        private static string AccessibleToken(string token)
        {
            return string.Equals(token, "SPACE", StringComparison.OrdinalIgnoreCase) ? "cách" : token;
        }
    }

    internal sealed class TypingOnScreenKeyboardControl : UserControl
    {
        private static readonly string[][] Rows =
        {
            new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" },
            new[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" },
            new[] { "A", "S", "D", "F", "G", "H", "J", "K", "L" },
            new[] { "Z", "X", "C", "V", "B", "N", "M" },
            new[] { "SPACE" }
        };

        private readonly Dictionary<string, TypingKeyboardKeyButton> _keys =
            new Dictionary<string, TypingKeyboardKeyButton>(StringComparer.OrdinalIgnoreCase);
        private readonly TableLayoutPanel _layout;
        private readonly Timer _feedbackTimer;
        private string _nextKeyToken = string.Empty;
        private string _transientKeyToken = string.Empty;
        private bool _transientWrong;

        public TypingOnScreenKeyboardControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            AccessibleName = "Bàn phím gõ chữ trên màn hình";
            AccessibleDescription = "Có thể bấm bằng chuột hoặc cảm ứng. Phím cần gõ tiếp theo được làm nổi bật.";
            MinimumSize = new Size(520, 260);
            Padding = new Padding(6, 4, 6, 4);

            _layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = Rows.Length,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent
            };
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (var i = 0; i < Rows.Length; i++) _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / Rows.Length));
            Controls.Add(_layout);

            for (var rowIndex = 0; rowIndex < Rows.Length; rowIndex++) AddRow(Rows[rowIndex], rowIndex);

            _feedbackTimer = new Timer { Interval = 320 };
            _feedbackTimer.Tick += delegate
            {
                _feedbackTimer.Stop();
                _transientKeyToken = string.Empty;
                _transientWrong = false;
                ApplyVisualStates();
            };
        }

        public event EventHandler<TypingOnScreenKeyEventArgs> KeyInvoked;

        internal int KeyCount { get { return _keys.Count; } }
        internal string HighlightedKey { get { return _nextKeyToken; } }
        internal string TransientFeedbackKey { get { return _transientKeyToken; } }
        internal bool TransientFeedbackIsWrong { get { return _transientWrong; } }
        internal int MinimumRenderedKeyHeight
        {
            get
            {
                if (_keys.Count == 0) return 0;
                return _keys.Values.Select(x => x.Height).DefaultIfEmpty(0).Min();
            }
        }
        internal int MinimumRenderedKeyWidth
        {
            get
            {
                if (_keys.Count == 0) return 0;
                return _keys.Values.Select(x => x.Width).DefaultIfEmpty(0).Min();
            }
        }

        public void SetNextKey(string nextKey)
        {
            _nextKeyToken = CanonicalToken(nextKey);
            ApplyVisualStates();
            AccessibleDescription = string.IsNullOrWhiteSpace(_nextKeyToken)
                ? "Bàn phím ảo đang chờ phím tiếp theo."
                : "Phím cần gõ tiếp theo là " + AccessibleToken(_nextKeyToken) + ".";
        }

        public void ShowTransientFeedback(string rawKey, bool correct)
        {
            _transientKeyToken = CanonicalToken(rawKey);
            _transientWrong = !correct;
            _feedbackTimer.Stop();
            ApplyVisualStates();
            _feedbackTimer.Start();
        }

        public void ClearTransientFeedback()
        {
            _feedbackTimer.Stop();
            _transientKeyToken = string.Empty;
            _transientWrong = false;
            ApplyVisualStates();
        }

        internal TypingKeyboardKeyButton FindKey(string token)
        {
            TypingKeyboardKeyButton button;
            return _keys.TryGetValue(CanonicalToken(token), out button) ? button : null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _feedbackTimer.Dispose();
            base.Dispose(disposing);
        }

        private void AddRow(string[] tokens, int rowIndex)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = tokens.Length,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = rowIndex == Rows.Length - 1 ? new Padding(70, 0, 70, 0) : Padding.Empty,
                BackColor = Color.Transparent
            };
            for (var i = 0; i < tokens.Length; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / tokens.Length));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                var input = token == "SPACE" ? " " : token.ToLowerInvariant();
                var button = new TypingKeyboardKeyButton(token, input);
                var captured = button;
                button.Click += delegate
                {
                    var handler = KeyInvoked;
                    if (handler != null)
                        handler(this, new TypingOnScreenKeyEventArgs(captured.InputValue, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
                };
                _keys[token] = button;
                row.Controls.Add(button, i, 0);
            }
            _layout.Controls.Add(row, 0, rowIndex);
        }

        private void ApplyVisualStates()
        {
            foreach (var pair in _keys)
            {
                var next = string.Equals(pair.Key, _nextKeyToken, StringComparison.OrdinalIgnoreCase);
                var transient = string.Equals(pair.Key, _transientKeyToken, StringComparison.OrdinalIgnoreCase);
                pair.Value.ApplyVisual(next, transient && _transientWrong, transient && !_transientWrong);
            }
            Invalidate(true);
        }

        private static string CanonicalToken(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (key == " " || string.Equals(key, "space", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "spacebar", StringComparison.OrdinalIgnoreCase)) return "SPACE";
            return key.Trim().ToUpperInvariant();
        }

        private static string AccessibleToken(string token)
        {
            return string.Equals(token, "SPACE", StringComparison.OrdinalIgnoreCase) ? "phím cách" : token;
        }
    }

    internal sealed class TypingHudPanel : UserControl
    {
        private readonly Label _language;
        private readonly Label _phase;
        private readonly Label _progressText;
        private readonly Label _target;
        private readonly Label _feedback;
        private readonly Label _hint;
        private readonly ProgressStrip _progress;
        private readonly ChildActionButton _pauseButton;
        private readonly TypingOnScreenKeyboardControl _keyboard;
        private TypingHudSnapshot _snapshot;

        public TypingHudPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            AccessibleName = "Bảng điều khiển game gõ phím phi thuyền";
            MinimumSize = new Size(640, 540);
            Padding = new Padding(12);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(root);

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165f));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118f));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.Controls.Add(header, 0, 0);

            _language = NewHeaderLabel("Chế độ ngôn ngữ", ContentAlignment.MiddleLeft, ChildVisualTheme.SkyStrong);
            _phase = NewHeaderLabel("Trạng thái game", ContentAlignment.MiddleCenter, ChildVisualTheme.MutedInk);
            _progressText = NewHeaderLabel("Tiến độ từ hiện tại", ContentAlignment.MiddleRight, ChildVisualTheme.MutedInk);
            header.Controls.Add(_language, 0, 0);
            header.Controls.Add(_phase, 1, 0);
            header.Controls.Add(_progressText, 2, 0);

            _pauseButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 5, 0, 5),
                Text = "Nghỉ",
                FillColor = ChildVisualTheme.SkyStrong,
                HoverColor = Color.FromArgb(78, 151, 202),
                PressedColor = Color.FromArgb(53, 125, 177),
                Font = ChildVisualTheme.Font(10.5f, FontStyle.Bold),
                Radius = 15,
                AccessibleName = "Nghỉ game gõ phím",
                AccessibleDescription = "Tạm nghỉ. Tiến độ do game lưu và có thể tiếp tục sau."
            };
            _pauseButton.Click += delegate
            {
                var handler = PauseToggleRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            };
            header.Controls.Add(_pauseButton, 3, 0);

            var targetCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 6),
                Padding = new Padding(20, 10, 20, 10),
                Radius = 22,
                CardColor = Color.FromArgb(251, 253, 250),
                BorderColor = Color.FromArgb(204, 222, 213)
            };
            root.Controls.Add(targetCard, 0, 1);
            var targetLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            targetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            targetLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            targetLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            targetCard.Controls.Add(targetLayout);
            _target = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = ChildVisualTheme.Font(28f, FontStyle.Bold),
                ForeColor = ChildVisualTheme.Ink,
                AutoEllipsis = true,
                AccessibleName = "Từ cần gõ"
            };
            targetLayout.Controls.Add(_target, 0, 0);
            _progress = new ProgressStrip { Dock = DockStyle.Fill, Margin = new Padding(16, 2, 16, 2), Maximum = 1, Value = 0 };
            _progress.AccessibleName = "Tiến độ gõ từ hiện tại";
            targetLayout.Controls.Add(_progress, 0, 1);

            var feedbackLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent
            };
            feedbackLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            feedbackLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 56f));
            feedbackLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 44f));
            root.Controls.Add(feedbackLayout, 0, 2);
            _feedback = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = ChildVisualTheme.Font(11.5f, FontStyle.Bold),
                ForeColor = ChildVisualTheme.MutedInk,
                AutoEllipsis = true,
                AccessibleName = "Phản hồi gõ phím"
            };
            _hint = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = ChildVisualTheme.Font(10f, FontStyle.Bold),
                ForeColor = ChildVisualTheme.SkyStrong,
                AutoEllipsis = true,
                AccessibleName = "Gợi ý phím tiếp theo",
                Visible = false
            };
            feedbackLayout.Controls.Add(_feedback, 0, 0);
            feedbackLayout.Controls.Add(_hint, 0, 1);

            _keyboard = new TypingOnScreenKeyboardControl { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 0) };
            _keyboard.KeyInvoked += delegate(object sender, TypingOnScreenKeyEventArgs e)
            {
                var handler = KeyInvoked;
                if (handler != null) handler(this, e);
            };
            root.Controls.Add(_keyboard, 0, 3);

            ApplySnapshot(TypingHudDemoSnapshots.Default());
        }

        public event EventHandler<TypingOnScreenKeyEventArgs> KeyInvoked;
        public event EventHandler PauseToggleRequested;

        internal string CurrentTargetText { get { return _target.Text; } }
        internal string CurrentFeedbackText { get { return _feedback.Text; } }
        internal string CurrentHintText { get { return _hint.Text; } }
        internal string CurrentLanguageText { get { return _language.Text; } }
        internal string CurrentPhaseText { get { return _phase.Text; } }
        internal string CurrentProgressText { get { return _progressText.Text; } }
        internal bool HintVisible { get { return _hint.Visible; } }
        internal bool PauseEnabled { get { return _pauseButton.Enabled; } }
        internal string PauseButtonText { get { return _pauseButton.Text; } }
        internal TypingOnScreenKeyboardControl Keyboard { get { return _keyboard; } }

        public void ApplySnapshot(TypingHudSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            _snapshot = snapshot;
            var total = Math.Max(1, snapshot.TotalCount);
            var typed = Math.Max(0, Math.Min(total, snapshot.TypedCount));
            var completed = snapshot.Completed || string.Equals(snapshot.Phase, "complete", StringComparison.OrdinalIgnoreCase);
            var paused = string.Equals(snapshot.Phase, "paused", StringComparison.OrdinalIgnoreCase);

            _target.Text = string.IsNullOrWhiteSpace(snapshot.TargetDisplayText) ? "Sẵn sàng!" : snapshot.TargetDisplayText.Trim();
            _target.AccessibleDescription = "Từ hiện tại: " + _target.Text + ".";
            _language.Text = string.Equals(snapshot.Language, "en", StringComparison.OrdinalIgnoreCase)
                ? "EN  •  English"
                : "VI  •  Tiếng Việt";
            _phase.Text = PhaseText(snapshot.Phase);
            _progress.Maximum = total;
            _progress.Value = typed;
            _progressText.Text = typed + " / " + total + " phím";
            _progress.AccessibleDescription = "Đã gõ đúng " + typed + " trên " + total + " phím của mục tiêu hiện tại.";

            _keyboard.SetNextKey(completed || paused ? string.Empty : snapshot.NextKey);
            _keyboard.Enabled = !completed && !paused;
            if (completed || paused)
                _keyboard.ClearTransientFeedback();
            else if (!string.IsNullOrWhiteSpace(snapshot.LastRawKey) &&
                (snapshot.FeedbackKind == TypingHudFeedbackKind.Correct || snapshot.FeedbackKind == TypingHudFeedbackKind.Wrong))
                _keyboard.ShowTransientFeedback(snapshot.LastRawKey, snapshot.FeedbackKind == TypingHudFeedbackKind.Correct);

            if (completed || snapshot.FeedbackKind == TypingHudFeedbackKind.Completed)
            {
                _feedback.Text = "Tuyệt! Từ này đã hoàn thành.";
                _feedback.ForeColor = Color.FromArgb(43, 118, 74);
            }
            else if (snapshot.FeedbackKind == TypingHudFeedbackKind.Wrong)
            {
                _feedback.Text = string.IsNullOrWhiteSpace(snapshot.NextKey)
                    ? "Thử lại nhé — mình vẫn đang ở đây."
                    : "Thử lại nhé — phím " + DisplayKey(snapshot.NextKey) + " đang sáng.";
                _feedback.ForeColor = Color.FromArgb(137, 82, 69);
            }
            else if (snapshot.FeedbackKind == TypingHudFeedbackKind.Correct)
            {
                _feedback.Text = "Đúng rồi! Tiếp tục nào.";
                _feedback.ForeColor = Color.FromArgb(43, 118, 74);
            }
            else
            {
                _feedback.Text = string.IsNullOrWhiteSpace(snapshot.NextKey)
                    ? "Sẵn sàng cho mục tiêu tiếp theo."
                    : "Gõ từng phím theo ánh sáng.";
                _feedback.ForeColor = ChildVisualTheme.MutedInk;
            }

            var showHint = !completed && !paused && snapshot.ErrorCount >= 2 && !string.IsNullOrWhiteSpace(snapshot.NextKey);
            _hint.Visible = showHint;
            _hint.Text = showHint ? "Gợi ý: tìm phím " + DisplayKey(snapshot.NextKey) + " đang sáng." : string.Empty;
            _hint.AccessibleDescription = _hint.Text;

            _pauseButton.Enabled = !completed;
            _pauseButton.Text = paused ? "Tiếp tục" : "Nghỉ";
            _pauseButton.AccessibleName = paused ? "Tiếp tục game gõ phím" : "Nghỉ game gõ phím";
            _pauseButton.AccessibleDescription = paused
                ? "Tiếp tục từ đúng tiến độ đã lưu."
                : "Tạm nghỉ. Tiến độ do game lưu và có thể tiếp tục sau.";

            AccessibleDescription = BuildAccessibleDescription(snapshot, typed, total, completed, paused);
        }

        private static Label NewHeaderLabel(string accessibleName, ContentAlignment alignment, Color color)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = alignment,
                Font = ChildVisualTheme.Font(10.5f, FontStyle.Bold),
                ForeColor = color,
                AutoEllipsis = true,
                AccessibleName = accessibleName
            };
        }

        private static string PhaseText(string phase)
        {
            if (string.Equals(phase, "paused", StringComparison.OrdinalIgnoreCase)) return "Đang nghỉ";
            if (string.Equals(phase, "boss", StringComparison.OrdinalIgnoreCase)) return "Boss";
            if (string.Equals(phase, "feedback", StringComparison.OrdinalIgnoreCase)) return "Phản hồi";
            if (string.Equals(phase, "complete", StringComparison.OrdinalIgnoreCase)) return "Hoàn thành";
            if (string.Equals(phase, "intro", StringComparison.OrdinalIgnoreCase)) return "Chuẩn bị";
            return "Đang chơi";
        }

        private static string DisplayKey(string key)
        {
            if (key == " " || string.Equals(key, "space", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "spacebar", StringComparison.OrdinalIgnoreCase)) return "SPACE";
            return string.IsNullOrWhiteSpace(key) ? "?" : key.Trim().ToUpperInvariant();
        }

        private static string BuildAccessibleDescription(TypingHudSnapshot snapshot, int typed, int total, bool completed, bool paused)
        {
            var status = completed ? "đã hoàn thành" : (paused ? "đang nghỉ" : "đang chơi");
            var target = string.IsNullOrWhiteSpace(snapshot.TargetDisplayText) ? "chưa có mục tiêu" : snapshot.TargetDisplayText.Trim();
            var next = completed || paused || string.IsNullOrWhiteSpace(snapshot.NextKey)
                ? string.Empty
                : " Phím tiếp theo là " + DisplayKey(snapshot.NextKey) + ".";
            return "Game " + status + ". Mục tiêu " + target + ". Đã gõ " + typed + " trên " + total + " phím." + next;
        }
    }

    internal static class TypingHudDemoSnapshots
    {
        public static TypingHudSnapshot Default()
        {
            return New("hành tinh", "vi", 0, 8, false, 0, "a", "playing", TypingHudFeedbackKind.None, null);
        }

        public static TypingHudSnapshot Correct()
        {
            return New("hành tinh", "vi", 2, 8, false, 0, "n", "feedback", TypingHudFeedbackKind.Correct, "a");
        }

        public static TypingHudSnapshot Wrong()
        {
            return New("hành tinh", "vi", 2, 8, false, 2, "n", "feedback", TypingHudFeedbackKind.Wrong, "m");
        }

        public static TypingHudSnapshot Completed()
        {
            return New("planet", "en", 6, 6, true, 0, null, "complete", TypingHudFeedbackKind.Completed, "t");
        }

        private static TypingHudSnapshot New(string text, string language, int typed, int total, bool completed,
            int errors, string nextKey, string phase, TypingHudFeedbackKind feedback, string lastRawKey)
        {
            return new TypingHudSnapshot
            {
                TargetId = "ai05_mock_target",
                TargetDisplayText = text,
                Language = language,
                TypedCount = typed,
                TotalCount = total,
                Completed = completed,
                ErrorCount = errors,
                NextKey = nextKey,
                Phase = phase,
                FeedbackKind = feedback,
                LastRawKey = lastRawKey
            };
        }
    }
}
