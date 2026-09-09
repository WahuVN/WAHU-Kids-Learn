using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace WAHU.TypingSpace.Scene
{
    public sealed class TypingSceneTarget
    {
        public string Id { get; set; }
        public string DisplayText { get; set; }
        public string Kind { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public bool Active { get; set; }
        public int TypedCount { get; set; }
        public int TotalCount { get; set; }
    }

    public sealed class TypingSpaceSceneState
    {
        public string Phase { get; set; }
        public float ShipY { get; set; }
        public bool BossVisible { get; set; }
        public float BossHealth01 { get; set; }
        public bool Paused { get; set; }
        public IList<TypingSceneTarget> Targets { get; set; }

        public TypingSpaceSceneState()
        {
            Phase = "playing";
            ShipY = 0.58f;
            BossHealth01 = 1f;
            Targets = new List<TypingSceneTarget>();
        }
    }

    public sealed class TypingSpaceSceneLayout
    {
        public Rectangle Client { get; internal set; }
        public Rectangle HudSafeZone { get; internal set; }
        public Rectangle Playfield { get; internal set; }
        public Rectangle ShipZone { get; internal set; }
        public Rectangle BossZone { get; internal set; }
        public Rectangle ShipBounds { get; internal set; }
        public Rectangle BossBounds { get; internal set; }
        public IList<Rectangle> TargetLanes { get; internal set; }
        public IDictionary<string, Rectangle> TargetBounds { get; internal set; }
    }

    public static class TypingSpaceSceneLayoutEngine
    {
        public const int MinimumTargetWidth = 118;
        public const int MinimumTargetHeight = 42;
        public const int MinimumHudHeight = 58;
        public const int MaximumHudHeight = 112;

        public static TypingSpaceSceneLayout Compute(Size size, TypingSpaceSceneState state)
        {
            var width = Math.Max(320, size.Width);
            var height = Math.Max(220, size.Height);
            var client = new Rectangle(0, 0, width, height);
            var hudHeight = Clamp((int)Math.Round(height * 0.15), MinimumHudHeight, MaximumHudHeight);
            var outer = Math.Max(10, Math.Min(28, width / 48));
            var bottomSafe = Math.Max(12, Math.Min(28, height / 28));
            var hud = new Rectangle(outer, 8, Math.Max(1, width - outer * 2), Math.Max(1, hudHeight - 12));
            var playTop = hudHeight + 8;
            var playfield = new Rectangle(
                outer,
                playTop,
                Math.Max(1, width - outer * 2),
                Math.Max(1, height - playTop - bottomSafe));

            var shipZoneWidth = Clamp((int)Math.Round(playfield.Width * 0.20), 88, 210);
            var bossZoneWidth = Clamp((int)Math.Round(playfield.Width * 0.22), 96, 250);
            var shipZone = new Rectangle(playfield.Left, playfield.Top, shipZoneWidth, playfield.Height);
            var bossZone = new Rectangle(playfield.Right - bossZoneWidth, playfield.Top, bossZoneWidth, playfield.Height);

            var lanesLeft = shipZone.Right + Math.Max(10, playfield.Width / 80);
            var lanesRight = bossZone.Left - Math.Max(10, playfield.Width / 80);
            if (lanesRight < lanesLeft + 80)
            {
                lanesLeft = playfield.Left + playfield.Width / 4;
                lanesRight = playfield.Right - playfield.Width / 4;
            }
            var laneWidth = Math.Max(80, lanesRight - lanesLeft);
            var laneGap = Math.Max(4, playfield.Height / 80);
            var laneHeight = Math.Max(44, (playfield.Height - laneGap * 4) / 3);
            var lanes = new List<Rectangle>(3);
            for (var i = 0; i < 3; i++)
            {
                var y = playfield.Top + laneGap + i * (laneHeight + laneGap);
                var bottom = Math.Min(playfield.Bottom - laneGap, y + laneHeight);
                lanes.Add(new Rectangle(lanesLeft, y, laneWidth, Math.Max(24, bottom - y)));
            }

            var shipHeight = Clamp((int)Math.Round(playfield.Height * 0.20), 48, 108);
            var shipWidth = Clamp((int)Math.Round(shipHeight * 1.35), 64, 142);
            var shipY01 = state == null ? 0.58f : Clamp01(state.ShipY);
            var shipCenterY = playfield.Top + (int)Math.Round(playfield.Height * shipY01);
            var shipBounds = new Rectangle(
                shipZone.Left + Math.Max(6, (shipZone.Width - shipWidth) / 2),
                Clamp(shipCenterY - shipHeight / 2, playfield.Top + 4, playfield.Bottom - shipHeight - 4),
                shipWidth,
                shipHeight);

            var bossSize = Clamp((int)Math.Round(Math.Min(bossZone.Width, playfield.Height) * 0.58), 64, 164);
            var bossBounds = new Rectangle(
                bossZone.Left + Math.Max(2, (bossZone.Width - bossSize) / 2),
                playfield.Top + Math.Max(4, (playfield.Height - bossSize) / 2),
                bossSize,
                bossSize);

            var targets = new Dictionary<string, Rectangle>(StringComparer.Ordinal);
            var items = state == null || state.Targets == null ? new List<TypingSceneTarget>() : state.Targets.Where(t => t != null).ToList();
            var occupied = new[] { new List<Rectangle>(), new List<Rectangle>(), new List<Rectangle>() };
            for (var i = 0; i < items.Count; i++)
            {
                var target = items[i];
                var nx = Clamp01(float.IsNaN(target.X) ? 0.5f : target.X);
                var ny = Clamp01(float.IsNaN(target.Y) ? 0.5f : target.Y);
                var preferredLane = Clamp((int)Math.Floor(ny * lanes.Count), 0, lanes.Count - 1);
                var textLength = string.IsNullOrWhiteSpace(target.DisplayText) ? 4 : target.DisplayText.Trim().Length;
                var bounds = Rectangle.Empty;
                var chosenLane = preferredLane;
                for (var laneAttempt = 0; laneAttempt < lanes.Count; laneAttempt++)
                {
                    var laneIndex = (preferredLane + laneAttempt) % lanes.Count;
                    var lane = lanes[laneIndex];
                    var desiredWidth = Clamp(MinimumTargetWidth + Math.Min(12, textLength) * 6, MinimumTargetWidth, Math.Min(238, Math.Max(MinimumTargetWidth, lane.Width)));
                    var desiredHeight = Clamp(lane.Height - 16, MinimumTargetHeight, 72);
                    desiredWidth = Math.Min(desiredWidth, lane.Width);
                    desiredHeight = Math.Min(desiredHeight, lane.Height);
                    var x = lane.Left + (int)Math.Round((lane.Width - desiredWidth) * nx);
                    var yRange = Math.Max(0, lane.Height - desiredHeight);
                    var localY = lane.Top + (int)Math.Round(yRange * ny);
                    var proposed = new Rectangle(
                        Clamp(x, lane.Left, lane.Right - desiredWidth),
                        Clamp(localY, lane.Top, lane.Bottom - desiredHeight),
                        desiredWidth,
                        desiredHeight);
                    Rectangle placed;
                    if (!TryPlaceTarget(lane, proposed, occupied[laneIndex], out placed)) continue;
                    bounds = placed;
                    chosenLane = laneIndex;
                    break;
                }
                if (bounds.IsEmpty)
                {
                    var lane = lanes[preferredLane];
                    var desiredWidth = Math.Min(lane.Width, Clamp(MinimumTargetWidth + Math.Min(12, textLength) * 6, MinimumTargetWidth, 238));
                    var desiredHeight = Math.Min(lane.Height, Clamp(lane.Height - 16, MinimumTargetHeight, 72));
                    bounds = new Rectangle(
                        lane.Left + (int)Math.Round((lane.Width - desiredWidth) * nx),
                        lane.Top + Math.Max(0, (lane.Height - desiredHeight) / 2),
                        desiredWidth,
                        desiredHeight);
                    chosenLane = preferredLane;
                }
                occupied[chosenLane].Add(bounds);

                var key = string.IsNullOrWhiteSpace(target.Id) ? "target-" + i : target.Id;
                if (targets.ContainsKey(key)) key += "-" + i;
                targets[key] = bounds;
            }

            return new TypingSpaceSceneLayout
            {
                Client = client,
                HudSafeZone = hud,
                Playfield = playfield,
                ShipZone = shipZone,
                BossZone = bossZone,
                ShipBounds = shipBounds,
                BossBounds = bossBounds,
                TargetLanes = lanes,
                TargetBounds = targets
            };
        }

        private static bool TryPlaceTarget(Rectangle lane, Rectangle proposed, IList<Rectangle> occupied, out Rectangle placed)
        {
            if (!occupied.Any(r => r.IntersectsWith(proposed)))
            {
                placed = proposed;
                return true;
            }

            var best = Rectangle.Empty;
            var bestDistance = double.MaxValue;
            var maxX = lane.Right - proposed.Width;
            var maxY = lane.Bottom - proposed.Height;
            for (var y = lane.Top; y <= maxY; y += 4)
            {
                for (var x = lane.Left; x <= maxX; x += 6)
                {
                    var candidate = new Rectangle(x, y, proposed.Width, proposed.Height);
                    if (occupied.Any(r => r.IntersectsWith(candidate))) continue;
                    var dx = candidate.X - proposed.X;
                    var dy = candidate.Y - proposed.Y;
                    var distance = dx * dx + dy * dy;
                    if (distance >= bestDistance) continue;
                    best = candidate;
                    bestDistance = distance;
                }
            }
            placed = best;
            return !best.IsEmpty;
        }

        internal static float Clamp01(float value)
        {
            if (float.IsNaN(value)) return 0.5f;
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        internal static int Clamp(int value, int min, int max)
        {
            if (max < min) return min;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    public static class TypingSpaceSceneRenderer
    {
        private static readonly Color SpaceTop = Color.FromArgb(15, 31, 68);
        private static readonly Color SpaceBottom = Color.FromArgb(7, 14, 38);
        private static readonly Color TargetFill = Color.FromArgb(25, 45, 80);
        private static readonly Color TargetText = Color.White;
        private static readonly Color TargetMuted = Color.FromArgb(184, 201, 226);

        public static double TargetTextContrastRatio
        {
            get { return ContrastRatio(TargetFill, TargetText); }
        }

        public static void Draw(Graphics graphics, Size size, TypingSpaceSceneState state, float parallaxOffset, bool lowPerformanceMode)
        {
            if (graphics == null) throw new ArgumentNullException("graphics");
            state = state ?? new TypingSpaceSceneState();
            var layout = TypingSpaceSceneLayoutEngine.Compute(size, state);
            graphics.SmoothingMode = lowPerformanceMode ? SmoothingMode.HighSpeed : SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = lowPerformanceMode ? PixelOffsetMode.HighSpeed : PixelOffsetMode.HighQuality;

            DrawBackground(graphics, layout.Client, parallaxOffset, lowPerformanceMode);
            DrawZones(graphics, layout, lowPerformanceMode);
            DrawShip(graphics, layout.ShipBounds, state.Paused);
            if (state.BossVisible || string.Equals(state.Phase, "boss", StringComparison.OrdinalIgnoreCase))
                DrawBoss(graphics, layout.BossBounds, state.BossHealth01);
            DrawTargets(graphics, layout, state);
        }

        private static void DrawBackground(Graphics g, Rectangle bounds, float offset, bool lowPerformanceMode)
        {
            using (var brush = new LinearGradientBrush(bounds, SpaceTop, SpaceBottom, 90f))
                g.FillRectangle(brush, bounds);

            var starCount = lowPerformanceMode ? 22 : 42;
            for (var i = 0; i < starCount; i++)
            {
                var layer = i % 3;
                var speed = layer == 0 ? 0.25f : layer == 1 ? 0.5f : 0.85f;
                var rawX = (i * 83 + 37) % Math.Max(1, bounds.Width);
                var x = PositiveModulo(rawX - (int)Math.Round(offset * speed), Math.Max(1, bounds.Width));
                var y = 12 + ((i * 47 + layer * 29) % Math.Max(1, bounds.Height - 24));
                var radius = layer + 1;
                var alpha = 120 + layer * 45;
                using (var b = new SolidBrush(Color.FromArgb(alpha, 229, 240, 255)))
                    g.FillEllipse(b, x, y, radius, radius);
            }

            var farPlanet = new Rectangle(bounds.Width - Math.Max(160, bounds.Width / 7), bounds.Height / 6, Math.Max(130, bounds.Width / 8), Math.Max(130, bounds.Width / 8));
            using (var b = new SolidBrush(Color.FromArgb(50, 112, 137, 185))) g.FillEllipse(b, farPlanet);
            var nearPlanet = new Rectangle(-Math.Max(90, bounds.Width / 12), bounds.Height - Math.Max(120, bounds.Height / 4), Math.Max(170, bounds.Width / 7), Math.Max(170, bounds.Width / 7));
            using (var b = new SolidBrush(Color.FromArgb(55, 103, 88, 145))) g.FillEllipse(b, nearPlanet);
        }

        private static void DrawZones(Graphics g, TypingSpaceSceneLayout layout, bool lowPerformanceMode)
        {
            using (var hud = new SolidBrush(Color.FromArgb(168, 10, 22, 49)))
                FillRounded(g, hud, layout.HudSafeZone, 14);
            using (var hudLine = new Pen(Color.FromArgb(90, 142, 183, 230), 1f))
                DrawRounded(g, hudLine, layout.HudSafeZone, 14);

            if (!lowPerformanceMode)
            {
                using (var lanePen = new Pen(Color.FromArgb(24, 176, 207, 242), 1f))
                {
                    lanePen.DashStyle = DashStyle.Dash;
                    foreach (var lane in layout.TargetLanes) g.DrawRectangle(lanePen, lane);
                }
            }

            using (var shipGlow = new SolidBrush(Color.FromArgb(28, 83, 202, 255)))
                FillRounded(g, shipGlow, Rectangle.Inflate(layout.ShipZone, -4, -4), 18);
            using (var bossGlow = new SolidBrush(Color.FromArgb(22, 255, 122, 125)))
                FillRounded(g, bossGlow, Rectangle.Inflate(layout.BossZone, -4, -4), 18);
        }

        private static void DrawShip(Graphics g, Rectangle r, bool paused)
        {
            var body = paused ? Color.FromArgb(121, 139, 166) : Color.FromArgb(70, 191, 235);
            var nose = new Point(r.Right, r.Top + r.Height / 2);
            var points = new[]
            {
                new Point(r.Left + r.Width / 5, r.Top + r.Height / 5),
                nose,
                new Point(r.Left + r.Width / 5, r.Bottom - r.Height / 5),
                new Point(r.Left, r.Bottom - r.Height / 3),
                new Point(r.Left, r.Top + r.Height / 3)
            };
            using (var b = new SolidBrush(body)) g.FillPolygon(b, points);
            using (var cockpit = new SolidBrush(Color.FromArgb(221, 240, 255)))
                g.FillEllipse(cockpit, r.Left + r.Width / 3, r.Top + r.Height / 3, Math.Max(12, r.Width / 4), Math.Max(12, r.Height / 3));
            using (var flame = new SolidBrush(Color.FromArgb(245, 189, 72)))
                g.FillEllipse(flame, r.Left - Math.Max(8, r.Width / 10), r.Top + r.Height / 2 - Math.Max(5, r.Height / 10), Math.Max(14, r.Width / 7), Math.Max(10, r.Height / 5));
        }

        private static void DrawBoss(Graphics g, Rectangle r, float health01)
        {
            health01 = TypingSpaceSceneLayoutEngine.Clamp01(health01);
            using (var halo = new SolidBrush(Color.FromArgb(42, 246, 97, 112)))
                g.FillEllipse(halo, Rectangle.Inflate(r, 8, 8));
            using (var body = new SolidBrush(Color.FromArgb(116, 74, 151))) g.FillEllipse(body, r);
            using (var eye = new SolidBrush(Color.FromArgb(255, 226, 103)))
            {
                var eyeSize = Math.Max(7, r.Width / 10);
                g.FillEllipse(eye, r.Left + r.Width / 3 - eyeSize / 2, r.Top + r.Height / 3, eyeSize, eyeSize);
                g.FillEllipse(eye, r.Left + r.Width * 2 / 3 - eyeSize / 2, r.Top + r.Height / 3, eyeSize, eyeSize);
            }
            var bar = new Rectangle(r.Left, Math.Max(2, r.Top - 15), r.Width, 8);
            using (var bg = new SolidBrush(Color.FromArgb(78, 24, 41))) FillRounded(g, bg, bar, 4);
            var fill = new Rectangle(bar.Left, bar.Top, (int)Math.Round(bar.Width * health01), bar.Height);
            if (fill.Width > 0)
                using (var hp = new SolidBrush(Color.FromArgb(238, 96, 111))) FillRounded(g, hp, fill, 4);
        }

        private static void DrawTargets(Graphics g, TypingSpaceSceneLayout layout, TypingSpaceSceneState state)
        {
            if (state.Targets == null || state.Targets.Count == 0) return;
            var targetByKey = new Dictionary<string, TypingSceneTarget>(StringComparer.Ordinal);
            for (var i = 0; i < state.Targets.Count; i++)
            {
                var t = state.Targets[i];
                if (t == null) continue;
                var key = string.IsNullOrWhiteSpace(t.Id) ? "target-" + i : t.Id;
                if (targetByKey.ContainsKey(key)) key += "-" + i;
                targetByKey[key] = t;
            }

            foreach (var pair in layout.TargetBounds)
            {
                TypingSceneTarget target;
                if (!targetByKey.TryGetValue(pair.Key, out target)) continue;
                var bounds = pair.Value;
                var accent = KindAccent(target.Kind);
                using (var shadow = new SolidBrush(Color.FromArgb(72, 0, 0, 0)))
                    FillRounded(g, shadow, new Rectangle(bounds.X + 2, bounds.Y + 3, bounds.Width, bounds.Height), 13);
                using (var fill = new SolidBrush(TargetFill)) FillRounded(g, fill, bounds, 13);
                using (var border = new Pen(target.Active ? Color.White : accent, target.Active ? 2.4f : 1.5f))
                    DrawRounded(g, border, bounds, 13);

                var progress = Math.Max(0, Math.Min(target.TotalCount <= 0 ? (target.DisplayText ?? string.Empty).Length : target.TotalCount, target.TypedCount));
                var total = Math.Max(1, target.TotalCount <= 0 ? Math.Max(1, (target.DisplayText ?? string.Empty).Length) : target.TotalCount);
                var progressWidth = (int)Math.Round((bounds.Width - 12) * (progress / (double)total));
                if (progressWidth > 0)
                {
                    var progressRect = new Rectangle(bounds.Left + 6, bounds.Bottom - 7, progressWidth, 3);
                    using (var b = new SolidBrush(accent)) FillRounded(g, b, progressRect, 2);
                }

                var text = string.IsNullOrWhiteSpace(target.DisplayText) ? "?" : target.DisplayText.Trim();
                var fontSize = bounds.Width < 140 ? 11f : text.Length > 12 ? 11f : text.Length > 8 ? 12.5f : 15f;
                using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Point))
                {
                    var textRect = Rectangle.Inflate(bounds, -9, -8);
                    textRect.Height -= 2;
                    TextRenderer.DrawText(g, text, font, textRect, TargetText,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                }
            }
        }

        private static Color KindAccent(string kind)
        {
            if (string.Equals(kind, "rescue", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(91, 220, 164);
            if (string.Equals(kind, "unlock", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(250, 200, 76);
            if (string.Equals(kind, "boss", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(244, 116, 131);
            return Color.FromArgb(91, 190, 246);
        }

        public static double ContrastRatio(Color a, Color b)
        {
            var l1 = RelativeLuminance(a);
            var l2 = RelativeLuminance(b);
            if (l1 < l2) { var t = l1; l1 = l2; l2 = t; }
            return (l1 + 0.05) / (l2 + 0.05);
        }

        private static double RelativeLuminance(Color color)
        {
            return 0.2126 * Linear(color.R / 255.0) + 0.7152 * Linear(color.G / 255.0) + 0.0722 * Linear(color.B / 255.0);
        }

        private static double Linear(double c)
        {
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        private static int PositiveModulo(int value, int modulo)
        {
            if (modulo <= 0) return 0;
            var result = value % modulo;
            return result < 0 ? result + modulo : result;
        }

        private static void FillRounded(Graphics g, Brush brush, Rectangle rect, int radius)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;
            using (var path = RoundedRect(rect, radius)) g.FillPath(brush, path);
        }

        private static void DrawRounded(Graphics g, Pen pen, Rectangle rect, int radius)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;
            using (var path = RoundedRect(rect, radius)) g.DrawPath(pen, path);
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            radius = Math.Max(1, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
            var diameter = radius * 2;
            var arc = new Rectangle(rect.Left, rect.Top, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public sealed class TypingSpaceSceneControl : Control
    {
        private TypingSpaceSceneState _state;
        private float _parallaxOffset;

        public TypingSpaceSceneControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Black;
            State = new TypingSpaceSceneState();
            ParallaxEnabled = true;
            AccessibleName = "Khung cảnh game phi thuyền gõ phím";
        }

        public TypingSpaceSceneState State
        {
            get { return _state; }
            set { _state = value ?? new TypingSpaceSceneState(); Invalidate(); }
        }

        public bool LowPerformanceMode { get; set; }
        public bool ParallaxEnabled { get; set; }
        public float ParallaxOffset { get { return _parallaxOffset; } }

        public TypingSpaceSceneLayout CurrentLayout
        {
            get { return TypingSpaceSceneLayoutEngine.Compute(ClientSize, State); }
        }

        public void AdvanceParallax(int elapsedMilliseconds)
        {
            if (elapsedMilliseconds <= 0 || !ParallaxEnabled || LowPerformanceMode || State.Paused || !Visible) return;
            var clampedMs = Math.Min(100, elapsedMilliseconds);
            _parallaxOffset += clampedMs * 0.018f;
            if (_parallaxOffset > 100000f) _parallaxOffset %= 4096f;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            TypingSpaceSceneRenderer.Draw(e.Graphics, ClientSize, State, _parallaxOffset, LowPerformanceMode);
        }
    }
}
