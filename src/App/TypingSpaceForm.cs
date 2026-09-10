using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WAHU.Content;
using WAHU.Data;
using WAHU.Performance;
using WAHU.TypingSpace;

namespace WAHUKidsLearn
{
    internal sealed class TypingSpaceGardenRewardAdapter : ITypingSpaceRewardAdapter
    {
        private readonly GameWorldRewardService _garden;
        private readonly string _childId;
        private readonly string _completionId;

        public TypingSpaceGardenRewardAdapter(LearningDatabase database, string childId)
        {
            _garden = new GameWorldRewardService(database ?? throw new ArgumentNullException("database"));
            _childId = childId ?? throw new ArgumentNullException("childId");
            // One durable Garden growth reward per day prevents replay farming while still rewarding practice.
            _completionId = DateTime.Now.ToString("yyyy-MM-dd");
        }

        public int GrantCount { get; private set; }

        public bool TryGrant(string rewardKey, int value)
        {
            if (string.IsNullOrWhiteSpace(rewardKey) || value < 0) return false;
            var created = _garden.GrantTypingSpaceCompletion(_childId, _completionId);
            if (created) GrantCount++;
            return created;
        }
    }

    internal sealed class TypingSpaceProductionAssetAdapter : ITypingSpaceAssetAdapter
    {
        public string ResolveOrFallback(string assetId)
        {
            return string.IsNullOrWhiteSpace(assetId) ? "20_typing_space_scene/typing_space_background_deep.png" : assetId;
        }
    }

    internal sealed class TypingSpaceGameCanvas : Panel
    {
        public GamePhase Phase { get; set; }
        public string TargetText { get; set; }
        public int TypedCount { get; set; }
        public int BossStep { get; set; }
        public bool ReducedMotion { get; set; }

        public TypingSpaceGameCanvas()
        {
            Dock = DockStyle.Fill;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(12, 27, 58);
            Resize += delegate { Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(0, 0, Width, Height);
            if (!GameAssetLibrary.DrawCover(g, "20_typing_space_scene/typing_space_background_deep.png", bounds))
            {
                using (var brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(12, 27, 58), Color.FromArgb(35, 34, 91), 90f))
                    g.FillRectangle(brush, ClientRectangle);
            }

            var planetSize = Math.Max(72, Math.Min(150, Width / 8));
            GameAssetLibrary.DrawContain(g, "20_typing_space_scene/typing_space_planet_01.png",
                new RectangleF(Width - planetSize - 28, 22, planetSize, planetSize));

            var shipW = Math.Max(105, Math.Min(180, Width / 6));
            var shipH = shipW * 0.72f;
            var shipPath = TypedCount > 0 && !ReducedMotion
                ? "21_typing_space_combat/ship_player_shoot.png"
                : "21_typing_space_combat/ship_player_idle.png";
            GameAssetLibrary.DrawContain(g, shipPath,
                new RectangleF(28, Height / 2f - shipH / 2f, shipW, shipH));

            if (Phase == GamePhase.Boss)
            {
                var boss = Math.Max(125, Math.Min(210, Width / 5));
                var bossAsset = BossStep >= 2 ? "21_typing_space_combat/typing_boss_damaged.png" : "21_typing_space_combat/typing_boss_core.png";
                GameAssetLibrary.DrawContain(g, bossAsset,
                    new RectangleF(Width - boss - 55, Height / 2f - boss / 2f, boss, boss));
            }
            else
            {
                var drone = Math.Max(90, Math.Min(140, Width / 8));
                GameAssetLibrary.DrawContain(g, "21_typing_space_combat/enemy_word_drone_01.png",
                    new RectangleF(Width - drone - 70, Height / 2f - drone / 2f, drone, drone));
            }

            DrawTargetCard(g);
        }

        private void DrawTargetCard(Graphics g)
        {
            var text = TargetText ?? string.Empty;
            if (text.Length == 0) return;
            var cardW = Math.Max(270, Math.Min(430, Width / 2));
            var cardH = Math.Max(105, Math.Min(145, Height / 3));
            var rect = new RectangleF((Width - cardW) / 2f, (Height - cardH) / 2f, cardW, cardH);
            using (var path = Rounded(rect, 22f))
            using (var fill = new SolidBrush(Color.FromArgb(220, 247, 250, 255)))
            using (var border = new Pen(Color.FromArgb(126, 190, 225), 2f))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            var mainFontSize = text.Length > 10 ? 24f : 31f;
            using (var font = ChildVisualTheme.Font(mainFontSize, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, text, font, Rectangle.Round(rect), ChildVisualTheme.Ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }

            if (TypedCount > 0)
            {
                var count = Math.Min(TypedCount, text.Length);
                var typed = text.Substring(0, count);
                using (var font = ChildVisualTheme.Font(9.5f, FontStyle.Bold))
                {
                    var small = Rectangle.Round(new RectangleF(rect.X + 12, rect.Bottom - 33, rect.Width - 24, 26));
                    TextRenderer.DrawText(g, "Đã gõ: " + typed, font, small, Color.FromArgb(55, 145, 101),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }

        private static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            var p = new GraphicsPath();
            var d = radius * 2f;
            p.AddArc(rect.X, rect.Y, d, d, 180, 90);
            p.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            p.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            p.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    internal sealed class TypingSpaceForm : Form
    {
        private readonly InMemoryEventSink _events = new InMemoryEventSink();
        private readonly TypingSpaceGardenRewardAdapter _rewards;
        private readonly TypingSpaceIntegrationShell _game;
        private readonly TypingSpaceGameCanvas _canvas;
        private readonly Label _targetLabel;
        private readonly Label _feedback;
        private readonly Label _stats;
        private readonly ChildActionButton _pauseButton;
        private readonly ChildActionButton _replayButton;
        private readonly FlowLayoutPanel _keyboard;
        private int _eventCursor;
        private int _bossHits;
        private int _correct;
        private int _wrong;

        public event EventHandler ExitRequested;

        public TypingSpaceForm(LearningDatabase database, RuntimePerformanceSettings performance)
        {
            if (database == null) throw new ArgumentNullException("database");
            Text = "Phi thuyền gõ phím — WAHU Kids Learn";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 640);
            ClientSize = new Size(1080, 720);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(13, 27, 58);
            Font = ChildVisualTheme.Font(10f);

            _rewards = new TypingSpaceGardenRewardAdapter(database, LearnerSessionService.PrimaryChildId);
            _game = new TypingSpaceIntegrationShell(_events, _rewards, new TypingSpaceProductionAssetAdapter(), BuildProductionFlow());

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(13, 27, 58)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 172));

            var header = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(14, 9, 14, 9),
                CardColor = Color.FromArgb(242, 248, 253),
                BorderColor = Color.FromArgb(172, 209, 230),
                Radius = 20,
                ShowShadow = false
            };
            var headerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2 };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 44));

            headerLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "PHI THUYỀN GÕ PHÍM",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.SkyStrong,
                Font = ChildVisualTheme.Font(13.5f, FontStyle.Bold)
            }, 0, 0);

            _targetLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Sẵn sàng",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(12f, FontStyle.Bold),
                AutoEllipsis = true
            };
            headerLayout.Controls.Add(_targetLabel, 1, 0);

            _stats = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9.2f, FontStyle.Bold)
            };
            headerLayout.Controls.Add(_stats, 2, 0);

            _pauseButton = MakeHeaderButton("Tạm nghỉ", Color.FromArgb(219, 236, 249), ChildVisualTheme.SkyStrong);
            _pauseButton.Click += delegate { TogglePause(); };
            headerLayout.Controls.Add(_pauseButton, 3, 0);

            var close = MakeHeaderButton("Đóng", Color.FromArgb(238, 236, 229), ChildVisualTheme.Ink);
            close.Click += delegate
            {
                var handler = ExitRequested;
                if (handler != null) handler(this, EventArgs.Empty);
                else Close();
            };
            headerLayout.Controls.Add(close, 4, 0);

            var progress = new TypingProgressStrip { Dock = DockStyle.Fill, Margin = new Padding(2, 5, 2, 2) };
            progress.Name = "typingProgressStrip";
            headerLayout.Controls.Add(progress, 0, 1);
            headerLayout.SetColumnSpan(progress, 5);
            header.Controls.Add(headerLayout);
            root.Controls.Add(header, 0, 0);

            _canvas = new TypingSpaceGameCanvas
            {
                Margin = new Padding(0, 0, 0, 8),
                ReducedMotion = performance != null && performance.MotionFpsCap <= 18
            };
            root.Controls.Add(_canvas, 0, 1);

            var bottom = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(10, 7, 10, 7),
                CardColor = Color.FromArgb(247, 250, 253),
                BorderColor = Color.FromArgb(181, 209, 226),
                Radius = 20,
                ShowShadow = false
            };
            var bottomLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 2 };
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
            bottomLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
            bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _feedback = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Gõ bằng bàn phím thật hoặc bấm bàn phím bên dưới.",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(10.2f, FontStyle.Bold),
                AutoEllipsis = true
            };
            bottomLayout.Controls.Add(_feedback, 0, 0);

            _replayButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 1, 0, 1),
                Text = "Chơi lại",
                FillColor = Color.FromArgb(224, 241, 228),
                HoverColor = Color.FromArgb(208, 233, 214),
                PressedColor = Color.FromArgb(193, 221, 201),
                TextColor = Color.FromArgb(50, 112, 72),
                Radius = 14,
                Depth = 2,
                Font = ChildVisualTheme.Font(9.2f, FontStyle.Bold)
            };
            _replayButton.Click += delegate { Replay(); };
            bottomLayout.Controls.Add(_replayButton, 1, 0);

            _keyboard = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                Padding = new Padding(8, 4, 8, 2),
                Margin = new Padding(0)
            };
            BuildKeyboard();
            bottomLayout.Controls.Add(_keyboard, 0, 1);
            bottomLayout.SetColumnSpan(_keyboard, 2);
            bottom.Controls.Add(bottomLayout);
            root.Controls.Add(bottom, 0, 2);
            Controls.Add(root);

            KeyPress += OnKeyPress;
            Shown += delegate { StartGame(); };
        }

        private static ChildActionButton MakeHeaderButton(string text, Color fill, Color ink)
        {
            return new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 2, 4, 2),
                Text = text,
                FillColor = fill,
                HoverColor = ControlPaint.Light(fill, 0.05f),
                PressedColor = ControlPaint.Dark(fill, 0.05f),
                TextColor = ink,
                Radius = 14,
                Depth = 2,
                Font = ChildVisualTheme.Font(9.2f, FontStyle.Bold)
            };
        }

        private void BuildKeyboard()
        {
            foreach (var c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
            {
                var key = new ChildActionButton
                {
                    Text = c.ToString(),
                    Size = new Size(48, 39),
                    Margin = new Padding(3),
                    FillColor = Color.FromArgb(238, 244, 250),
                    HoverColor = Color.FromArgb(220, 236, 248),
                    PressedColor = Color.FromArgb(204, 226, 242),
                    TextColor = ChildVisualTheme.Ink,
                    Radius = 10,
                    Depth = 2,
                    Font = ChildVisualTheme.Font(9.5f, FontStyle.Bold),
                    TabStop = false
                };
                key.Click += delegate(object sender, EventArgs e) { Submit(((Button)sender).Text.ToLowerInvariant()); };
                _keyboard.Controls.Add(key);
            }

            var space = new ChildActionButton
            {
                Text = "SPACE",
                Size = new Size(156, 39),
                Margin = new Padding(3),
                FillColor = Color.FromArgb(238, 244, 250),
                HoverColor = Color.FromArgb(220, 236, 248),
                PressedColor = Color.FromArgb(204, 226, 242),
                TextColor = ChildVisualTheme.Ink,
                Radius = 10,
                Depth = 2,
                Font = ChildVisualTheme.Font(9.2f, FontStyle.Bold),
                TabStop = false,
                AccessibleName = "Phím cách"
            };
            space.Click += delegate { Submit(" "); };
            _keyboard.Controls.Add(space);
        }

        private void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsLetter(e.KeyChar) || e.KeyChar == ' ')
            {
                Submit(char.ToLowerInvariant(e.KeyChar).ToString());
                e.Handled = true;
            }
        }

        private void StartGame()
        {
            _bossHits = 0;
            _correct = 0;
            _wrong = 0;
            _eventCursor = _events.Events.Count;
            _game.Start();
            _feedback.Text = "Bắt đầu nhé. Gõ đúng từng chữ để nạp năng lượng cho phi thuyền.";
            _feedback.ForeColor = ChildVisualTheme.Ink;
            RefreshView();
        }

        private void Submit(string key)
        {
            if (_game.Phase == GamePhase.Paused || _game.Phase == GamePhase.Complete) return;
            var ok = _game.Input(new TypingInput
            {
                RawKey = key,
                NormalizedKey = key,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Source = "wahu-app"
            });
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
                    case "TYPING_CHAR_CORRECT":
                        _feedback.Text = "Đúng rồi — tiếp tục!";
                        _feedback.ForeColor = Color.FromArgb(55, 145, 101);
                        break;
                    case "TYPING_CHAR_WRONG":
                        _feedback.Text = "Chưa đúng phím này. Thử lại nhé.";
                        _feedback.ForeColor = Color.FromArgb(184, 101, 64);
                        break;
                    case "TARGET_RESCUED":
                        _feedback.Text = "Đã cứu được một mục tiêu!";
                        break;
                    case "TARGET_DESTROYED":
                        _feedback.Text = "Mục tiêu hoàn tất!";
                        break;
                    case "BOSS_HIT":
                        _bossHits++;
                        _feedback.Text = "Tín hiệu lớn đã được sửa " + Math.Min(3, _bossHits) + "/3.";
                        break;
                    case "BOSS_DEFEATED":
                        _feedback.Text = "Hoàn thành thử thách cuối!";
                        break;
                    case "LEVEL_COMPLETED":
                        _feedback.Text = "Hoàn thành chuyến bay! Khu vườn đang nhận phần thưởng.";
                        _feedback.ForeColor = Color.FromArgb(55, 145, 101);
                        break;
                    case "REWARD_GRANTED":
                        _feedback.Text = "Tuyệt! Khu vườn của bé đã lớn thêm một bước hôm nay.";
                        _feedback.ForeColor = Color.FromArgb(55, 145, 101);
                        break;
                }
            }
        }

        private void RefreshView()
        {
            var t = _game.CurrentTarget;
            var p = _game.Progress;
            _targetLabel.Text = t == null
                ? (_game.Phase == GamePhase.Complete ? "HOÀN THÀNH" : "Sẵn sàng")
                : (t.Language == "vi" ? "VI • " : "EN • ") + t.DisplayText;
            _stats.Text = "Đúng " + _correct + "  •  Thử lại " + _wrong;
            _pauseButton.Text = _game.Phase == GamePhase.Paused ? "Học tiếp" : "Tạm nghỉ";
            _replayButton.Text = _game.Phase == GamePhase.Complete ? "Chơi lại" : "Ván mới";

            var strip = FindProgressStrip(this);
            if (strip != null)
            {
                var total = Math.Max(1, p.TotalCount);
                strip.Value = Math.Max(0, Math.Min(100, p.TypedCount * 100 / total));
            }

            _canvas.Phase = _game.Phase;
            _canvas.TargetText = t == null ? string.Empty : t.DisplayText;
            _canvas.TypedCount = p.TypedCount;
            _canvas.BossStep = Math.Max(1, _bossHits + 1);
            _canvas.Invalidate();

            var next = GetNextExpectedKey();
            foreach (var button in _keyboard.Controls.OfType<ChildActionButton>())
            {
                var active = string.Equals(button.Text, next, StringComparison.OrdinalIgnoreCase);
                button.FillColor = active ? Color.FromArgb(255, 231, 132) : Color.FromArgb(238, 244, 250);
                button.BorderColor = active ? Color.FromArgb(222, 172, 67) : Color.Transparent;
                button.BorderThickness = active ? 1.3f : 0f;
                button.Invalidate();
            }
        }

        private static TypingProgressStrip FindProgressStrip(Control root)
        {
            foreach (Control c in root.Controls)
            {
                var strip = c as TypingProgressStrip;
                if (strip != null && strip.Name == "typingProgressStrip") return strip;
                var nested = FindProgressStrip(c);
                if (nested != null) return nested;
            }
            return null;
        }

        private string GetNextExpectedKey()
        {
            var t = _game.CurrentTarget;
            if (t == null || t.AcceptedInputs == null || t.AcceptedInputs.Count == 0) return string.Empty;
            var typed = _game.Progress.TypedCount;
            var candidate = t.AcceptedInputs.OrderBy(x => x.Length).FirstOrDefault(x => x.Length > typed);
            if (string.IsNullOrEmpty(candidate) || typed >= candidate.Length) return string.Empty;
            var c = candidate[typed];
            if (c == ' ') return "SPACE";
            return char.IsLetter(c) ? char.ToUpperInvariant(c).ToString() : string.Empty;
        }

        internal void PauseForNavigation()
        {
            if (_game.Phase != GamePhase.Paused && _game.Phase != GamePhase.Complete && _game.Phase != GamePhase.Intro)
                TogglePause();
        }

        internal void ResumeFromNavigation()
        {
            if (_game.Phase == GamePhase.Paused) TogglePause();
        }

        private void TogglePause()
        {
            if (_game.Phase == GamePhase.Paused) _game.Resume();
            else _game.Pause();
            ProcessEvents();
            _feedback.Text = _game.Phase == GamePhase.Paused
                ? "Đang nghỉ. Tiến độ trong ván vẫn được giữ."
                : "Tiếp tục nào!";
            RefreshView();
        }

        private void Replay()
        {
            if (_game.Phase == GamePhase.Complete) _game.Replay();
            else _game.Start();
            _bossHits = 0;
            _correct = 0;
            _wrong = 0;
            _eventCursor = _events.Events.Count;
            _feedback.Text = "Ván mới bắt đầu.";
            _feedback.ForeColor = ChildVisualTheme.Ink;
            RefreshView();
        }

        private static IList<TypingTarget> BuildProductionFlow()
        {
            try
            {
                var path = FindTypingContentPath();
                var pack = new TypingSpaceContentSource().Load(path);
                var vi = pack.entries.Where(x => x.target.language == "vi" && x.target.kind != "boss")
                    .OrderBy(x => x.target.difficulty).ThenBy(x => x.target.displayText.Length).ThenBy(x => x.target.id, StringComparer.Ordinal).Take(3).ToList();
                var en = pack.entries.Where(x => x.target.language == "en" && x.target.kind != "boss")
                    .OrderBy(x => x.target.difficulty).ThenBy(x => x.target.displayText.Length).ThenBy(x => x.target.id, StringComparer.Ordinal).Take(3).ToList();
                var bosses = pack.entries.Where(x => x.target.kind == "boss")
                    .OrderBy(x => x.target.difficulty).ThenBy(x => x.target.language, StringComparer.Ordinal).ThenBy(x => x.target.id, StringComparer.Ordinal).Take(3).ToList();
                var selected = new List<TypingSpaceContentEntry>();
                for (var i = 0; i < 3; i++)
                {
                    if (i < vi.Count) selected.Add(vi[i]);
                    if (i < en.Count) selected.Add(en[i]);
                }
                selected.AddRange(bosses);
                var flow = selected.Select(ToTarget).ToList();
                if (flow.Count >= 6) return flow;
            }
            catch
            {
                // Built-in safe fallback keeps the game playable if content files are temporarily unavailable.
            }

            return new List<TypingTarget>
            {
                Target("vi-meo", "mèo", "vi", "rescue", "meo", "mèo"),
                Target("en-cat", "cat", "en", "shoot", "cat"),
                Target("vi-nha", "nhà", "vi", "unlock", "nha", "nhà"),
                Target("en-book", "book", "en", "rescue", "book"),
                Target("vi-sao", "sao", "vi", "shoot", "sao"),
                Target("en-moon", "moon", "en", "unlock", "moon"),
                Target("boss-star", "star", "en", "boss", "star"),
                Target("boss-robot", "robot", "en", "boss", "robot"),
                Target("boss-mat-troi", "mặt trời", "vi", "boss", "mat troi", "mặt trời")
            };
        }

        private static TypingTarget ToTarget(TypingSpaceContentEntry entry)
        {
            var t = entry.target;
            return new TypingTarget
            {
                Id = t.id,
                DisplayText = t.displayText,
                AcceptedInputs = new List<string>(t.acceptedInputs),
                Language = t.language,
                Difficulty = t.difficulty,
                Kind = t.kind,
                RewardValue = t.rewardValue
            };
        }

        private static TypingTarget Target(string id, string display, string language, string kind, params string[] accepted)
        {
            return new TypingTarget
            {
                Id = id,
                DisplayText = display,
                Language = language,
                Kind = kind,
                Difficulty = 1,
                RewardValue = 1,
                AcceptedInputs = new List<string>(accepted)
            };
        }

        private static string FindTypingContentPath()
        {
            const string relative = "content_packs\\typing_space_grade2_v1\\typing_content_v1.json";
            var starts = new[] { AppDomain.CurrentDomain.BaseDirectory, Environment.CurrentDirectory };
            foreach (var start in starts)
            {
                if (string.IsNullOrWhiteSpace(start)) continue;
                var dir = new DirectoryInfo(Path.GetFullPath(start));
                for (var i = 0; i < 7 && dir != null; i++, dir = dir.Parent)
                {
                    var candidate = Path.Combine(dir.FullName, relative);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            throw new FileNotFoundException("Không tìm thấy nội dung Phi thuyền gõ phím.", relative);
        }
    }

    internal sealed class TypingProgressStrip : Control
    {
        private int _value;
        public int Value
        {
            get { return _value; }
            set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public TypingProgressStrip()
        {
            DoubleBuffered = true;
            Height = 18;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(1, 2, Math.Max(1, Width - 2), Math.Max(8, Height - 4));
            using (var bg = Rounded(rect, rect.Height / 2f))
            using (var brush = new SolidBrush(Color.FromArgb(220, 230, 238))) g.FillPath(brush, bg);
            if (_value <= 0) return;
            var fill = new RectangleF(rect.X, rect.Y, Math.Max(rect.Height, rect.Width * _value / 100f), rect.Height);
            if (fill.Width > rect.Width) fill.Width = rect.Width;
            using (var path = Rounded(fill, fill.Height / 2f))
            using (var brush = new SolidBrush(Color.FromArgb(89, 185, 145))) g.FillPath(brush, path);
        }

        private static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            var p = new GraphicsPath();
            var d = radius * 2f;
            p.AddArc(rect.X, rect.Y, d, d, 180, 90);
            p.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            p.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            p.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
