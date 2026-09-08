using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WAHU.Performance;

namespace WAHUKidsLearn
{
    internal static class GameArtMotionPolicy
    {
        public static bool AllowsDecorativeMotion(RuntimePerformanceSettings performance, bool learningFocus)
        {
            if (performance == null || performance.Profile == PerformanceProfileKind.LOW || learningFocus) return false;
            return performance.DecorativeMotionAllowedOutsideLearningFocus;
        }

        public static int IntervalFor(RuntimePerformanceSettings performance, int fallbackIntervalMs)
        {
            if (performance == null || performance.MotionFpsCap <= 0) return fallbackIntervalMs;
            var capInterval = (int)Math.Ceiling(1000d / performance.MotionFpsCap);
            return Math.Max(fallbackIntervalMs, capInterval);
        }
    }

    internal sealed class GameArtVisibilityWatcher : IDisposable
    {
        private readonly Control _owner;
        private readonly EventHandler _changed;
        private readonly List<Control> _ancestors = new List<Control>();
        private bool _wasParented;

        public GameArtVisibilityWatcher(Control owner, EventHandler changed)
        {
            _owner = owner ?? throw new ArgumentNullException("owner");
            _changed = changed ?? throw new ArgumentNullException("changed");
            _wasParented = _owner.Parent != null;
            _owner.ParentChanged += OwnerParentChanged;
            RewireAncestors();
        }

        public bool IsHierarchyVisible
        {
            get
            {
                if (_owner.IsDisposed || _owner.Disposing || !_owner.Visible) return false;
                if (_wasParented && _owner.Parent == null) return false;
                for (var parent = _owner.Parent; parent != null; parent = parent.Parent)
                    if (!parent.Visible || parent.IsDisposed || parent.Disposing) return false;
                return true;
            }
        }

        private void OwnerParentChanged(object sender, EventArgs e)
        {
            if (_owner.Parent != null) _wasParented = true;
            RewireAncestors();
            _changed(sender, e);
        }

        private void AncestorVisibleChanged(object sender, EventArgs e)
        {
            _changed(sender, e);
        }

        private void AncestorParentChanged(object sender, EventArgs e)
        {
            RewireAncestors();
            _changed(sender, e);
        }

        private void RewireAncestors()
        {
            UnwireAncestors();
            for (var parent = _owner.Parent; parent != null; parent = parent.Parent)
            {
                _ancestors.Add(parent);
                parent.VisibleChanged += AncestorVisibleChanged;
                parent.ParentChanged += AncestorParentChanged;
            }
        }

        private void UnwireAncestors()
        {
            foreach (var ancestor in _ancestors)
            {
                ancestor.VisibleChanged -= AncestorVisibleChanged;
                ancestor.ParentChanged -= AncestorParentChanged;
            }
            _ancestors.Clear();
        }

        public void Dispose()
        {
            _owner.ParentChanged -= OwnerParentChanged;
            UnwireAncestors();
        }
    }

    internal sealed class RescueHeroArtControl : Control
    {
        private readonly Timer _timer;
        private readonly bool _motionAllowed;
        private readonly GameArtVisibilityWatcher _visibilityWatcher;
        private int _frame;

        public RescueHeroArtControl() : this(null) { }

        public RescueHeroArtControl(RuntimePerformanceSettings performance)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            AccessibleName = "Minh họa hành trình cứu hộ";
            AccessibleDescription = "Thỏ và rô-bốt cùng đi qua ba chặng Toán để tới rương sao.";
            _motionAllowed = GameArtMotionPolicy.AllowsDecorativeMotion(performance, false);
            _timer = new Timer { Interval = GameArtMotionPolicy.IntervalFor(performance, 90) };
            _timer.Tick += delegate { _frame = (_frame + 1) % 80; Invalidate(); };
            if (_motionAllowed) _visibilityWatcher = new GameArtVisibilityWatcher(this, delegate { UpdateAnimationState(); });
        }

        internal bool MotionAllowed { get { return _motionAllowed; } }
        internal bool AnimationRunning { get { return _timer.Enabled; } }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); UpdateAnimationState(); }
        protected override void OnHandleDestroyed(EventArgs e) { _timer.Stop(); base.OnHandleDestroyed(e); }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); UpdateAnimationState(); }

        private void UpdateAnimationState()
        {
            var visible = _visibilityWatcher == null ? Visible : _visibilityWatcher.IsHierarchyVisible;
            _timer.Enabled = _motionAllowed && IsHandleCreated && visible && !IsDisposed && !Disposing;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_visibilityWatcher != null) _visibilityWatcher.Dispose();
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 80 || Height < 60) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var r = ClientRectangle;
            var pulse = (float)((Math.Sin(_frame * Math.PI / 20d) + 1d) * 0.5d);

            if (DrawProductionRescueArt(g, r, pulse)) return;

            using (var bg = new LinearGradientBrush(r, Color.FromArgb(238, 249, 255), Color.FromArgb(240, 250, 229), 90f))
            using (var path = ChildVisualTheme.RoundedRect(new Rectangle(1, 1, Width - 3, Height - 3), 20))
            {
                g.FillPath(bg, path);
                using (var pen = new Pen(Color.FromArgb(185, 218, 232), 1.2f)) g.DrawPath(pen, path);
            }

            DrawCloud(g, Width * 0.12f, Height * 0.17f, 0.68f);
            DrawCloud(g, Width * 0.82f, Height * 0.14f, 0.52f);
            DrawHill(g);
            DrawRoute(g, pulse);
            DrawBunny(g, new RectangleF(Width * 0.035f, Height * 0.22f, Width * 0.24f, Height * 0.68f));
            DrawRobot(g, new RectangleF(Width * 0.72f, Height * 0.25f, Width * 0.24f, Height * 0.62f));
            DrawSparkles(g, pulse);
        }

        private static bool DrawProductionRescueArt(Graphics g, Rectangle bounds, float pulse)
        {
            const string background = "03_Rescue/rescue_map_background.png";
            if (!GameAssetLibrary.HasAsset(background)) return false;

            var scene = new RectangleF(2, 2, Math.Max(1, bounds.Width - 5), Math.Max(1, bounds.Height - 5));
            var state = g.Save();
            try
            {
                using (var clip = ChildVisualTheme.RoundedRect(Rectangle.Round(scene), 20)) g.SetClip(clip);
                GameAssetLibrary.DrawCover(g, background, scene);
                var bob = (float)Math.Round(pulse * 3f);
                GameAssetLibrary.DrawContain(g, "03_Rescue/rescue_start_marker.png", Slot(scene, .04f, .62f, .16f, .28f));
                GameAssetLibrary.DrawContain(g, "03_Rescue/rescue_checkpoint_idle.png", Slot(scene, .29f, .43f, .14f, .27f));
                GameAssetLibrary.DrawContain(g, "03_Rescue/rescue_checkpoint_active.png", Slot(scene, .45f, .38f, .16f, .30f));
                GameAssetLibrary.DrawContain(g, "03_Rescue/rescue_checkpoint_done.png", Slot(scene, .61f, .43f, .14f, .27f));
                GameAssetLibrary.DrawContain(g, "03_Rescue/rescue_reward_chest_closed.png", Slot(scene, .61f, .66f, .18f, .27f));
                GameAssetLibrary.DrawContain(g, "03_Rescue/rescue_bunny_walk.png", Slot(scene, .00f, .21f - bob / Math.Max(1f, scene.Height), .28f, .68f));
                GameAssetLibrary.DrawContain(g, "03_Rescue/rescue_robot_walk.png", Slot(scene, .74f, .23f + bob / Math.Max(1f, scene.Height), .25f, .65f));
            }
            finally
            {
                g.Restore(state);
            }

            using (var borderPath = ChildVisualTheme.RoundedRect(Rectangle.Round(scene), 20))
            using (var border = new Pen(Color.FromArgb(160, 93, 122, 117), 1.2f))
                g.DrawPath(border, borderPath);
            return true;
        }

        private static RectangleF Slot(RectangleF r, float x, float y, float width, float height)
        {
            return new RectangleF(r.Left + r.Width * x, r.Top + r.Height * y, r.Width * width, r.Height * height);
        }

        private void DrawHill(Graphics g)
        {
            var hill = new RectangleF(-Width * 0.08f, Height * 0.58f, Width * 1.16f, Height * 0.55f);
            using (var b = new SolidBrush(Color.FromArgb(197, 235, 164))) g.FillEllipse(b, hill);
            using (var b = new SolidBrush(Color.FromArgb(154, 213, 132)))
                g.FillEllipse(b, Width * 0.02f, Height * 0.68f, Width * 0.95f, Height * 0.32f);
        }

        private static void DrawCloud(Graphics g, float cx, float cy, float scale)
        {
            using (var b = new SolidBrush(Color.FromArgb(225, 255, 255, 255)))
            {
                g.FillEllipse(b, cx - 34 * scale, cy - 11 * scale, 42 * scale, 24 * scale);
                g.FillEllipse(b, cx - 10 * scale, cy - 22 * scale, 45 * scale, 34 * scale);
                g.FillEllipse(b, cx + 16 * scale, cy - 12 * scale, 38 * scale, 25 * scale);
            }
        }

        private void DrawRoute(Graphics g, float pulse)
        {
            var y = Height * 0.62f;
            var xs = new[] { Width * 0.34f, Width * 0.50f, Width * 0.65f };
            using (var p = new Pen(Color.FromArgb(226, 158, 76), 4f))
            {
                p.DashStyle = DashStyle.Dot;
                g.DrawLine(p, xs[0], y, xs[2], y - Height * 0.03f);
            }
            var colors = new[] { ChildVisualTheme.PeachStrong, ChildVisualTheme.SkyStrong, ChildVisualTheme.LavenderStrong };
            for (var i = 0; i < 3; i++)
            {
                var rr = 15f + (i == (_frame / 20) % 3 ? 2f * pulse : 0f);
                using (var glow = new SolidBrush(Color.FromArgb((int)(45 + 45 * pulse), colors[i])))
                    g.FillEllipse(glow, xs[i] - rr - 5, y - rr - 5, (rr + 5) * 2, (rr + 5) * 2);
                using (var b = new SolidBrush(colors[i])) g.FillEllipse(b, xs[i] - rr, y - rr, rr * 2, rr * 2);
                ChildVisualTheme.DrawTextWithOwnedFont(g, (i + 1).ToString(), ChildVisualTheme.Font(9f, FontStyle.Bold),
                    Rectangle.Round(new RectangleF(xs[i] - rr, y - rr, rr * 2, rr * 2)), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            var chest = new RectangleF(Width * 0.61f, Height * 0.73f, Width * 0.12f, Height * 0.15f);
            using (var shadow = new SolidBrush(Color.FromArgb(38, 80, 60, 35)))
                g.FillEllipse(shadow, chest.X - 6, chest.Bottom - 5, chest.Width + 12, 10);
            using (var b = new SolidBrush(Color.FromArgb(229, 107, 49))) g.FillRectangle(b, chest);
            using (var b = new SolidBrush(ChildVisualTheme.Sun)) g.FillRectangle(b, chest.X + chest.Width * .42f, chest.Y, chest.Width * .16f, chest.Height);
            using (var pen = new Pen(Color.FromArgb(132, 73, 39), 2f)) g.DrawRectangle(pen, chest.X, chest.Y, chest.Width, chest.Height);
            DrawStar(g, chest.X + chest.Width / 2f, chest.Y + chest.Height / 2f, 8f, Color.White, ChildVisualTheme.Sun);
        }

        private static void DrawBunny(Graphics g, RectangleF r)
        {
            var cx = r.X + r.Width * .48f;
            var cy = r.Y + r.Height * .55f;
            using (var pink = new SolidBrush(Color.FromArgb(255, 183, 195)))
            using (var white = new SolidBrush(Color.FromArgb(255, 252, 249)))
            using (var outline = new Pen(Color.FromArgb(132, 105, 112), 1.5f))
            {
                g.FillEllipse(white, cx - r.Width * .23f, r.Y, r.Width * .18f, r.Height * .42f);
                g.FillEllipse(white, cx + r.Width * .05f, r.Y + r.Height * .01f, r.Width * .18f, r.Height * .42f);
                g.FillEllipse(pink, cx - r.Width * .19f, r.Y + r.Height * .05f, r.Width * .10f, r.Height * .30f);
                g.FillEllipse(pink, cx + r.Width * .09f, r.Y + r.Height * .06f, r.Width * .10f, r.Height * .30f);
                var head = new RectangleF(cx - r.Width * .27f, cy - r.Height * .24f, r.Width * .54f, r.Height * .40f);
                g.FillEllipse(white, head); g.DrawEllipse(outline, head);
                g.FillEllipse(pink, cx - r.Width * .19f, cy + r.Height * .02f, r.Width * .08f, r.Width * .055f);
                g.FillEllipse(pink, cx + r.Width * .11f, cy + r.Height * .02f, r.Width * .08f, r.Width * .055f);
                using (var eye = new SolidBrush(Color.FromArgb(63, 48, 53)))
                {
                    g.FillEllipse(eye, cx - r.Width * .13f, cy - r.Height * .06f, 5, 7);
                    g.FillEllipse(eye, cx + r.Width * .10f, cy - r.Height * .06f, 5, 7);
                }
                using (var mouth = new Pen(Color.FromArgb(140, 66, 70), 2f)) g.DrawArc(mouth, cx - 7, cy + 2, 14, 10, 0, 180);
                using (var body = new SolidBrush(Color.FromArgb(255, 251, 247))) g.FillEllipse(body, cx - r.Width * .20f, cy + r.Height * .12f, r.Width * .40f, r.Height * .34f);
                using (var scarf = new SolidBrush(Color.FromArgb(241, 112, 68))) g.FillRectangle(scarf, cx - r.Width * .20f, cy + r.Height * .14f, r.Width * .40f, r.Height * .07f);
            }
        }

        private static void DrawRobot(Graphics g, RectangleF r)
        {
            var head = new RectangleF(r.X + r.Width * .08f, r.Y + r.Height * .11f, r.Width * .84f, r.Height * .48f);
            using (var white = new SolidBrush(Color.FromArgb(247, 252, 255)))
            using (var blue = new SolidBrush(Color.FromArgb(61, 151, 224)))
            using (var screen = new SolidBrush(Color.FromArgb(38, 65, 111)))
            using (var outline = new Pen(Color.FromArgb(55, 104, 154), 1.5f))
            {
                g.FillEllipse(white, head); g.DrawEllipse(outline, head);
                var face = RectangleF.Inflate(head, -head.Width * .16f, -head.Height * .20f);
                g.FillEllipse(screen, face);
                using (var cyan = new Pen(Color.FromArgb(99, 229, 255), 2.5f))
                {
                    g.DrawArc(cyan, face.X + face.Width * .18f, face.Y + face.Height * .25f, 12, 9, 5, 170);
                    g.DrawArc(cyan, face.X + face.Width * .62f, face.Y + face.Height * .25f, 12, 9, 5, 170);
                }
                using (var body = new SolidBrush(Color.FromArgb(237, 248, 255))) g.FillEllipse(body, r.X + r.Width * .20f, r.Y + r.Height * .55f, r.Width * .60f, r.Height * .34f);
                using (var heart = new SolidBrush(Color.FromArgb(72, 210, 229)))
                {
                    var hx = r.X + r.Width * .50f; var hy = r.Y + r.Height * .69f;
                    g.FillEllipse(heart, hx - 8, hy - 6, 9, 9); g.FillEllipse(heart, hx - 1, hy - 6, 9, 9);
                    PointF[] pts = { new PointF(hx - 8, hy - 1), new PointF(hx + 8, hy - 1), new PointF(hx, hy + 9) };
                    g.FillPolygon(heart, pts);
                }
                using (var antenna = new Pen(Color.FromArgb(61, 151, 224), 2f)) g.DrawLine(antenna, r.X + r.Width * .5f, r.Y + r.Height * .12f, r.X + r.Width * .5f, r.Y + r.Height * .02f);
                g.FillEllipse(blue, r.X + r.Width * .46f, r.Y, r.Width * .08f, r.Width * .08f);
            }
        }

        private void DrawSparkles(Graphics g, float pulse)
        {
            var alpha = (int)(130 + 100 * pulse);
            DrawStar(g, Width * .28f, Height * .21f, 7 + 2 * pulse, Color.FromArgb(alpha, 255, 205, 67), Color.FromArgb(alpha, 255, 205, 67));
            DrawStar(g, Width * .66f, Height * .23f, 6 + 2 * pulse, Color.FromArgb(alpha, 250, 191, 62), Color.FromArgb(alpha, 250, 191, 62));
            DrawStar(g, Width * .57f, Height * .42f, 5 + pulse, Color.FromArgb(alpha, 255, 220, 90), Color.FromArgb(alpha, 255, 220, 90));
        }

        internal static void DrawStar(Graphics g, float cx, float cy, float radius, Color fill, Color border)
        {
            var pts = new PointF[10];
            for (var i = 0; i < 10; i++)
            {
                var a = -Math.PI / 2 + i * Math.PI / 5;
                var rr = i % 2 == 0 ? radius : radius * .46f;
                pts[i] = new PointF(cx + (float)Math.Cos(a) * rr, cy + (float)Math.Sin(a) * rr);
            }
            using (var b = new SolidBrush(fill)) g.FillPolygon(b, pts);
            using (var p = new Pen(border, 1f)) g.DrawPolygon(p, pts);
        }
    }

    internal sealed class GameFeedbackFxControl : Control
    {
        public enum Mood { Neutral, Correct, Retry, Hint }
        private readonly Timer _timer;
        private readonly bool _motionAllowed;
        private readonly GameArtVisibilityWatcher _visibilityWatcher;
        private int _frame;
        private Mood _mood;

        public Mood VisualMood { get { return _mood; } set { _mood = value; AccessibleDescription = MoodDescription(value); Invalidate(); } }

        public GameFeedbackFxControl() : this(null, true) { }

        public GameFeedbackFxControl(RuntimePerformanceSettings performance, bool learningFocus)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AccessibleName = "Hiệu ứng phản hồi học tập";
            _motionAllowed = GameArtMotionPolicy.AllowsDecorativeMotion(performance, learningFocus);
            _timer = new Timer { Interval = GameArtMotionPolicy.IntervalFor(performance, 85) };
            _timer.Tick += delegate { _frame = (_frame + 1) % 72; Invalidate(); };
            if (_motionAllowed) _visibilityWatcher = new GameArtVisibilityWatcher(this, delegate { UpdateAnimationState(); });
        }

        internal bool MotionAllowed { get { return _motionAllowed; } }
        internal bool AnimationRunning { get { return _timer.Enabled; } }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); UpdateAnimationState(); }
        protected override void OnHandleDestroyed(EventArgs e) { _timer.Stop(); base.OnHandleDestroyed(e); }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); UpdateAnimationState(); }

        private void UpdateAnimationState()
        {
            var visible = _visibilityWatcher == null ? Visible : _visibilityWatcher.IsHierarchyVisible;
            _timer.Enabled = _motionAllowed && IsHandleCreated && visible && !IsDisposed && !Disposing;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_visibilityWatcher != null) _visibilityWatcher.Dispose();
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }

        private static string MoodDescription(Mood mood)
        {
            if (mood == Mood.Correct) return "Ngôi sao vui và tia sáng chúc mừng câu trả lời đúng.";
            if (mood == Mood.Retry) return "Đám mây nhỏ dịu dàng nhắc thử lại.";
            if (mood == Mood.Hint) return "Bóng đèn nhỏ phát sáng để gợi ý.";
            return "Bạn đồng hành đang ở bên cạnh.";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var pulse = (float)((Math.Sin(_frame * Math.PI / 18d) + 1d) * .5d);
            var cx = Width / 2f; var cy = Height / 2f;
            if (DrawProductionFeedback(g)) return;
            if (_mood == Mood.Correct)
            {
                using (var halo = new SolidBrush(Color.FromArgb((int)(40 + pulse * 55), ChildVisualTheme.Sun))) g.FillEllipse(halo, cx - 30, cy - 30, 60, 60);
                RescueHeroArtControl.DrawStar(g, cx, cy, 20 + pulse * 2, ChildVisualTheme.Sun, Color.FromArgb(218, 158, 38));
                using (var pen = new Pen(Color.FromArgb(124, 88, 35), 2f)) { g.DrawArc(pen, cx - 8, cy - 1, 16, 10, 10, 160); }
            }
            else if (_mood == Mood.Retry)
            {
                using (var b = new SolidBrush(Color.FromArgb(222, 228, 241)))
                {
                    g.FillEllipse(b, cx - 25, cy - 12, 28, 25); g.FillEllipse(b, cx - 8, cy - 23, 34, 36); g.FillEllipse(b, cx + 10, cy - 10, 23, 22);
                }
                using (var pen = new Pen(Color.FromArgb(79, 86, 110), 2f)) g.DrawArc(pen, cx - 7, cy + 5, 14, 8, 195, 150);
            }
            else if (_mood == Mood.Hint)
            {
                using (var halo = new SolidBrush(Color.FromArgb((int)(38 + pulse * 60), ChildVisualTheme.Sun))) g.FillEllipse(halo, cx - 29, cy - 29, 58, 58);
                using (var b = new SolidBrush(Color.FromArgb(255, 218, 76))) g.FillEllipse(b, cx - 15, cy - 22, 30, 34);
                using (var p = new Pen(Color.FromArgb(127, 100, 58), 3f)) { g.DrawLine(p, cx - 9, cy + 13, cx + 9, cy + 13); g.DrawLine(p, cx - 7, cy + 18, cx + 7, cy + 18); }
            }
            else
            {
                using (var b = new SolidBrush(ChildVisualTheme.Sky)) g.FillEllipse(b, cx - 19, cy - 19, 38, 38);
                using (var p = new Pen(ChildVisualTheme.SkyStrong, 2f)) g.DrawEllipse(p, cx - 19, cy - 19, 38, 38);
                RescueHeroArtControl.DrawStar(g, cx, cy, 10, ChildVisualTheme.Sun, ChildVisualTheme.PeachStrong);
            }
        }

        private bool DrawProductionFeedback(Graphics g)
        {
            string asset = null;
            if (_mood == Mood.Correct) asset = "02_GameEffects/fx_correct_star.png";
            else if (_mood == Mood.Retry) asset = "02_GameEffects/fx_retry_cloud.png";
            else if (_mood == Mood.Hint) asset = "02_GameEffects/fx_hint_bulb.png";
            if (string.IsNullOrWhiteSpace(asset) || !GameAssetLibrary.HasAsset(asset)) return false;
            return GameAssetLibrary.DrawContain(g, asset,
                new RectangleF(2, 2, Math.Max(1, Width - 4), Math.Max(1, Height - 4)));
        }
    }

    internal sealed class GardenRewardArtControl : Control
    {
        private readonly Timer _timer;
        private readonly bool _motionAllowed;
        private readonly GameArtVisibilityWatcher _visibilityWatcher;
        private int _frame;

        public GardenRewardArtControl() : this(null) { }

        public GardenRewardArtControl(RuntimePerformanceSettings performance)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AccessibleName = "Minh họa vườn phần thưởng";
            AccessibleDescription = "Mầm cây lớn lên cùng sao và hoa khi hoàn thành nhiệm vụ.";
            _motionAllowed = GameArtMotionPolicy.AllowsDecorativeMotion(performance, false);
            _timer = new Timer { Interval = GameArtMotionPolicy.IntervalFor(performance, 100) };
            _timer.Tick += delegate { _frame = (_frame + 1) % 80; Invalidate(); };
            if (_motionAllowed) _visibilityWatcher = new GameArtVisibilityWatcher(this, delegate { UpdateAnimationState(); });
        }

        internal bool MotionAllowed { get { return _motionAllowed; } }
        internal bool AnimationRunning { get { return _timer.Enabled; } }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); UpdateAnimationState(); }
        protected override void OnHandleDestroyed(EventArgs e) { _timer.Stop(); base.OnHandleDestroyed(e); }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); UpdateAnimationState(); }

        private void UpdateAnimationState()
        {
            var visible = _visibilityWatcher == null ? Visible : _visibilityWatcher.IsHierarchyVisible;
            _timer.Enabled = _motionAllowed && IsHandleCreated && visible && !IsDisposed && !Disposing;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_visibilityWatcher != null) _visibilityWatcher.Dispose();
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 80 || Height < 50) return;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var pulse = (float)((Math.Sin(_frame * Math.PI / 20d) + 1d) * .5d);
            if (GameAssetLibrary.HasAsset("12_Garden/garden_mission_complete.png"))
            {
                GameAssetLibrary.DrawCover(g, "12_Garden/garden_mission_complete.png", ClientRectangle);
                if (GameAssetLibrary.HasAsset("11_Rewards/reward_happy_star.png"))
                    GameAssetLibrary.DrawContain(g, "11_Rewards/reward_happy_star.png",
                        new RectangleF(Width * .67f, Height * .03f, Width * .27f, Height * .42f));
                return;
            }
            using (var sky = new LinearGradientBrush(ClientRectangle, Color.FromArgb(231, 247, 255), Color.FromArgb(238, 250, 225), 90f)) g.FillRectangle(sky, ClientRectangle);
            using (var ground = new SolidBrush(Color.FromArgb(186, 225, 139))) g.FillEllipse(ground, -Width * .1f, Height * .60f, Width * 1.2f, Height * .55f);
            using (var soil = new SolidBrush(Color.FromArgb(135, 91, 57))) g.FillEllipse(soil, Width * .25f, Height * .69f, Width * .5f, Height * .22f);
            var cx = Width * .5f; var baseY = Height * .72f;
            using (var stem = new Pen(Color.FromArgb(65, 157, 77), Math.Max(4f, Width * .018f))) g.DrawLine(stem, cx, baseY, cx, Height * .38f);
            using (var leaf = new SolidBrush(Color.FromArgb(91, 192, 91)))
            {
                g.FillEllipse(leaf, cx - Width * .23f, Height * .38f, Width * .24f, Height * .17f);
                g.FillEllipse(leaf, cx - Width * .01f, Height * .33f, Width * .24f, Height * .17f);
            }
            using (var sun = new SolidBrush(Color.FromArgb((int)(190 + pulse * 50), ChildVisualTheme.Sun))) g.FillEllipse(sun, Width * .78f, Height * .07f, Width * .14f, Width * .14f);
            RescueHeroArtControl.DrawStar(g, Width * .28f, Height * .18f, 10 + pulse * 2, ChildVisualTheme.Sun, ChildVisualTheme.PeachStrong);
            RescueHeroArtControl.DrawStar(g, Width * .72f, Height * .31f, 7 + pulse, Color.White, ChildVisualTheme.Sun);
            DrawFlower(g, Width * .23f, Height * .72f, Color.FromArgb(244, 131, 172));
            DrawFlower(g, Width * .78f, Height * .73f, Color.FromArgb(122, 178, 239));
        }

        private static void DrawFlower(Graphics g, float cx, float cy, Color petal)
        {
            using (var b = new SolidBrush(petal))
            {
                g.FillEllipse(b, cx - 10, cy - 4, 9, 9); g.FillEllipse(b, cx + 1, cy - 4, 9, 9);
                g.FillEllipse(b, cx - 4, cy - 10, 9, 9); g.FillEllipse(b, cx - 4, cy + 1, 9, 9);
            }
            using (var c = new SolidBrush(ChildVisualTheme.Sun)) g.FillEllipse(c, cx - 3, cy - 3, 6, 6);
        }
    }
}
