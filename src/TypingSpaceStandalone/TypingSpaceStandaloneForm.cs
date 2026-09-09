using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WAHU.TypingSpace;

namespace WAHU.TypingSpaceStandalone
{
    internal sealed class DurableRewardAdapter : ITypingSpaceRewardAdapter
    {
        private readonly string _path;
        private readonly HashSet<string> _keys = new HashSet<string>(StringComparer.Ordinal);
        public int GrantCount { get; private set; }

        public DurableRewardAdapter(string path)
        {
            _path = path;
            try
            {
                if (File.Exists(_path))
                {
                    foreach (var line in File.ReadAllLines(_path))
                        if (!string.IsNullOrWhiteSpace(line)) _keys.Add(line.Trim());
                    GrantCount = _keys.Count;
                }
            }
            catch { }
        }

        public bool TryGrant(string rewardKey, int value)
        {
            if (string.IsNullOrWhiteSpace(rewardKey) || value < 0) return false;
            if (!_keys.Add(rewardKey)) return false;
            GrantCount = _keys.Count;
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
                File.WriteAllLines(_path, _keys.OrderBy(x => x).ToArray());
            }
            catch { }
            return true;
        }
    }

    internal sealed class SpaceCanvas : Panel
    {
        public GamePhase Phase { get; set; }
        public int BossStep { get; set; }
        public string TargetText { get; set; }
        public int TypedCount { get; set; }
        private readonly Random _random = new Random(7);
        private readonly Point[] _stars;

        public SpaceCanvas()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(11, 20, 48);
            _stars = Enumerable.Range(0, 70).Select(_ => new Point(_random.Next(10, 1200), _random.Next(10, 650))).ToArray();
            Resize += delegate { Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new LinearGradientBrush(ClientRectangle, Color.FromArgb(9, 18, 44), Color.FromArgb(33, 26, 74), 90f))
                g.FillRectangle(bg, ClientRectangle);
            using (var star = new SolidBrush(Color.FromArgb(210, 239, 250, 255)))
                foreach (var p in _stars) g.FillEllipse(star, p.X % Math.Max(1, Width), p.Y % Math.Max(1, Height), 2, 2);

            DrawPlanet(g);
            DrawShip(g);
            if (Phase == GamePhase.Boss) DrawBoss(g);
            DrawTarget(g);
        }

        private void DrawPlanet(Graphics g)
        {
            var r = Math.Max(70, Math.Min(150, Width / 8));
            var rect = new Rectangle(Width - r - 30, 30, r, r);
            using (var b = new LinearGradientBrush(rect, Color.FromArgb(73, 179, 213), Color.FromArgb(41, 84, 150), 45f))
                g.FillEllipse(b, rect);
            using (var p = new Pen(Color.FromArgb(120, 230, 248, 255), 8f))
                g.DrawArc(p, rect.X - r / 4, rect.Y + r / 3, rect.Width + r / 2, rect.Height / 3, 8, 165);
        }

        private void DrawShip(Graphics g)
        {
            var x = 50;
            var y = Math.Max(80, Height / 2 - 45);
            var body = new Point[] { new Point(x, y + 38), new Point(x + 78, y), new Point(x + 115, y + 38), new Point(x + 78, y + 76) };
            using (var b = new SolidBrush(Color.FromArgb(219, 236, 249))) g.FillPolygon(b, body);
            using (var b = new SolidBrush(Color.FromArgb(78, 180, 224))) g.FillEllipse(b, x + 47, y + 20, 35, 28);
            using (var flame = new SolidBrush(Color.FromArgb(246, 155, 62))) g.FillPolygon(flame, new[] { new Point(x, y + 30), new Point(x - 28, y + 38), new Point(x, y + 48) });
        }

        private void DrawBoss(Graphics g)
        {
            var size = Math.Max(90, Math.Min(160, Width / 7));
            var rect = new Rectangle(Width - size - 80, Height / 2 - size / 2, size, size);
            using (var b = new SolidBrush(Color.FromArgb(191, 74, 112))) g.FillEllipse(b, rect);
            using (var eye = new SolidBrush(Color.White))
            {
                g.FillEllipse(eye, rect.X + size / 4, rect.Y + size / 3, 22, 22);
                g.FillEllipse(eye, rect.Right - size / 4 - 22, rect.Y + size / 3, 22, 22);
            }
            using (var p = new Pen(Color.FromArgb(255, 218, 93), 5f))
                g.DrawArc(p, rect.X + 20, rect.Y - 12, rect.Width - 40, 28, 10, 160);
            using (var font = new Font("Segoe UI", 11f, FontStyle.Bold))
                TextRenderer.DrawText(g, "BOSS " + Math.Max(1, BossStep) + "/3", font, new Rectangle(rect.X - 20, rect.Bottom + 8, rect.Width + 40, 30), Color.White, TextFormatFlags.HorizontalCenter);
        }

        private void DrawTarget(Graphics g)
        {
            var text = TargetText ?? string.Empty;
            if (text.Length == 0) return;
            var rect = new Rectangle(Math.Max(180, Width / 2 - 180), Math.Max(60, Height / 2 - 70), 360, 140);
            using (var path = Rounded(rect, 24))
            using (var b = new SolidBrush(Color.FromArgb(220, 19, 34, 74)))
            using (var p = new Pen(Color.FromArgb(126, 212, 255), 2f))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            using (var font = new Font("Segoe UI", 30f, FontStyle.Bold))
                TextRenderer.DrawText(g, text, font, rect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (TypedCount > 0)
            {
                var typed = text.Substring(0, Math.Min(TypedCount, text.Length));
                using (var font = new Font("Segoe UI", 11f, FontStyle.Bold))
                    TextRenderer.DrawText(g, "Đã gõ: " + typed, font, new Rectangle(rect.X, rect.Bottom - 34, rect.Width, 28), Color.FromArgb(146, 245, 172), TextFormatFlags.HorizontalCenter);
            }
        }

        private static GraphicsPath Rounded(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class TypingSpaceStandaloneForm : Form
    {
        private readonly InMemoryEventSink _events = new InMemoryEventSink();
        private readonly DurableRewardAdapter _rewards;
        private readonly TypingSpaceIntegrationShell _game;
        private readonly SpaceCanvas _canvas;
        private readonly Label _target;
        private readonly Label _feedback;
        private readonly Label _stats;
        private readonly ProgressBar _progress;
        private readonly Button _pause;
        private readonly FlowLayoutPanel _keyboard;
        private int _eventCursor;
        private int _bossStep;
        private int _correct;
        private int _wrong;

        public TypingSpaceStandaloneForm()
        {
            Text = "Typing Space — Standalone";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 620);
            ClientSize = new Size(1180, 760);
            KeyPreview = true;
            BackColor = Color.FromArgb(8, 16, 38);
            Font = new Font("Segoe UI", 10f);
            var saveRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Typing Space Standalone");
            _rewards = new DurableRewardAdapter(Path.Combine(saveRoot, "rewards.txt"));
            _game = new TypingSpaceIntegrationShell(_events, _rewards, new MockAssetAdapter(), TypingSpaceMockFactory.CreateV1Flow());

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12), BackColor = BackColor };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 196));

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2, BackColor = Color.FromArgb(18, 29, 61), Padding = new Padding(12) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            header.Controls.Add(new Label { Text = "🚀 TYPING SPACE", ForeColor = Color.White, Font = new Font("Segoe UI", 16f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _target = new Label { Text = "Sẵn sàng", ForeColor = Color.FromArgb(157, 224, 255), Font = new Font("Segoe UI", 13f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            header.Controls.Add(_target, 1, 0);
            _stats = new Label { ForeColor = Color.FromArgb(207, 217, 235), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            header.Controls.Add(_stats, 2, 0);
            _pause = new Button { Text = "Tạm nghỉ", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(52, 74, 120), ForeColor = Color.White };
            _pause.FlatAppearance.BorderSize = 0;
            _pause.Click += delegate { TogglePause(); };
            header.Controls.Add(_pause, 3, 0);
            var replay = new Button { Text = "Chơi lại", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(54, 126, 94), ForeColor = Color.White };
            replay.FlatAppearance.BorderSize = 0;
            replay.Click += delegate { Replay(); };
            header.Controls.Add(replay, 4, 0);
            _progress = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Margin = new Padding(8, 10, 8, 2) };
            header.Controls.Add(_progress, 0, 1);
            header.SetColumnSpan(_progress, 5);
            root.Controls.Add(header, 0, 0);

            _canvas = new SpaceCanvas();
            root.Controls.Add(_canvas, 0, 1);

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.FromArgb(14, 25, 54), Padding = new Padding(10) };
            bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _feedback = new Label { Text = "Gõ bằng bàn phím thật hoặc chạm bàn phím bên dưới.", ForeColor = Color.White, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
            bottom.Controls.Add(_feedback, 0, 0);
            _keyboard = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, AutoScroll = true, Padding = new Padding(18, 8, 18, 4) };
            foreach (var c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
            {
                var key = new Button { Text = c.ToString(), Width = 58, Height = 46, Margin = new Padding(5), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(236, 242, 250), ForeColor = Color.FromArgb(28, 43, 70), Font = new Font("Segoe UI", 10f, FontStyle.Bold), TabStop = false };
                key.FlatAppearance.BorderColor = Color.FromArgb(121, 164, 210);
                key.Click += delegate(object sender, EventArgs e) { Submit(((Button)sender).Text.ToLowerInvariant()); };
                _keyboard.Controls.Add(key);
            }
            bottom.Controls.Add(_keyboard, 0, 1);
            root.Controls.Add(bottom, 0, 2);
            Controls.Add(root);

            KeyPress += delegate(object sender, KeyPressEventArgs e)
            {
                if (char.IsLetter(e.KeyChar) || e.KeyChar == ' ')
                {
                    Submit(char.ToLowerInvariant(e.KeyChar).ToString());
                    e.Handled = true;
                }
            };
            Shown += delegate { StartGame(); };
        }

        private void StartGame()
        {
            _bossStep = 0; _correct = 0; _wrong = 0; _eventCursor = _events.Events.Count;
            _game.Start();
            _feedback.Text = "Bắt đầu! Hoàn thành mục tiêu để nạp năng lượng cho phi thuyền.";
            RefreshView();
        }

        private void Submit(string key)
        {
            if (_game.Phase == GamePhase.Paused || _game.Phase == GamePhase.Complete) return;
            var ok = _game.Input(new TypingInput { RawKey = key, NormalizedKey = key, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Source = "standalone" });
            if (ok) _correct++; else _wrong++;
            ProcessEvents();
            RefreshView();
        }

        private void ProcessEvents()
        {
            while (_eventCursor < _events.Events.Count)
            {
                var ev = _events.Events[_eventCursor++];
                switch (ev.Type)
                {
                    case "TYPING_CHAR_CORRECT": _feedback.Text = "Đúng rồi — tiếp tục!"; _feedback.ForeColor = Color.FromArgb(145, 244, 171); break;
                    case "TYPING_CHAR_WRONG": _feedback.Text = "Chưa đúng phím này. Thử lại nhé."; _feedback.ForeColor = Color.FromArgb(255, 184, 142); break;
                    case "TARGET_RESCUED": _feedback.Text = "Đã cứu mục tiêu!"; break;
                    case "TARGET_DESTROYED": _feedback.Text = "Mục tiêu đã hoàn tất!"; break;
                    case "BOSS_HIT": _bossStep++; _feedback.Text = "Boss trúng đòn " + _bossStep + "/3!"; break;
                    case "BOSS_DEFEATED": _feedback.Text = "Boss đã bị đánh bại!"; break;
                    case "REWARD_GRANTED": _feedback.Text = "Hoàn thành! Phần thưởng đã được lưu."; break;
                    case "LEVEL_COMPLETED":
                        BeginInvoke((MethodInvoker)delegate
                        {
                            MessageBox.Show(this, "Bạn đã hoàn thành Typing Space!\nPhần thưởng được lưu riêng trên máy.", "Typing Space", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        });
                        break;
                }
            }
        }

        private void RefreshView()
        {
            var t = _game.CurrentTarget;
            var p = _game.Progress;
            _target.Text = t == null ? (_game.Phase == GamePhase.Complete ? "HOÀN THÀNH" : "Sẵn sàng") : (t.Language == "vi" ? "VI • " : "EN • ") + t.DisplayText;
            var total = Math.Max(1, p.TotalCount);
            _progress.Value = Math.Max(0, Math.Min(100, p.TypedCount * 100 / total));
            _stats.Text = "Đúng " + _correct + "  •  Sai " + _wrong + "  •  Thưởng " + _rewards.GrantCount;
            _pause.Text = _game.Phase == GamePhase.Paused ? "Học tiếp" : "Tạm nghỉ";
            _canvas.Phase = _game.Phase;
            _canvas.BossStep = Math.Max(1, _bossStep + 1);
            _canvas.TargetText = t == null ? string.Empty : t.DisplayText;
            _canvas.TypedCount = p.TypedCount;
            _canvas.Invalidate();
            foreach (Button b in _keyboard.Controls.OfType<Button>())
            {
                var next = GetNextExpectedKey();
                b.BackColor = string.Equals(b.Text, next, StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(255, 228, 113) : Color.FromArgb(236, 242, 250);
            }
        }

        private string GetNextExpectedKey()
        {
            var t = _game.CurrentTarget;
            if (t == null || t.AcceptedInputs == null || t.AcceptedInputs.Count == 0) return string.Empty;
            var p = _game.Progress;
            var candidate = t.AcceptedInputs.OrderBy(x => x.Length).FirstOrDefault(x => x.Length > p.TypedCount);
            if (string.IsNullOrEmpty(candidate) || p.TypedCount >= candidate.Length) return string.Empty;
            var c = candidate[p.TypedCount];
            return char.IsLetter(c) ? char.ToUpperInvariant(c).ToString() : string.Empty;
        }

        private void TogglePause()
        {
            if (_game.Phase == GamePhase.Paused) _game.Resume(); else _game.Pause();
            ProcessEvents();
            _feedback.Text = _game.Phase == GamePhase.Paused ? "Đang nghỉ. Tiến độ vẫn được giữ." : "Tiếp tục nào!";
            RefreshView();
        }

        private void Replay()
        {
            if (_game.Phase == GamePhase.Complete) _game.Replay(); else _game.Start();
            _bossStep = 0; _correct = 0; _wrong = 0; _eventCursor = _events.Events.Count;
            _feedback.Text = "Ván mới bắt đầu.";
            RefreshView();
        }
    }
}
