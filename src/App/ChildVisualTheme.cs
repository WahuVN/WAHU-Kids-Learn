using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WAHU.Learning;

namespace WAHUKidsLearn
{
    internal static class ChildVisualTheme
    {
        public static readonly Color Ink = Color.FromArgb(46, 56, 61);
        public static readonly Color MutedInk = Color.FromArgb(99, 111, 116);
        public static readonly Color Cream = Color.FromArgb(249, 247, 238);
        public static readonly Color Card = Color.FromArgb(255, 253, 246);
        public static readonly Color Mint = Color.FromArgb(211, 239, 215);
        public static readonly Color MintStrong = Color.FromArgb(105, 172, 116);
        public static readonly Color Sky = Color.FromArgb(216, 237, 248);
        public static readonly Color SkyStrong = Color.FromArgb(92, 154, 189);
        public static readonly Color Peach = Color.FromArgb(250, 226, 199);
        public static readonly Color PeachStrong = Color.FromArgb(214, 137, 78);
        public static readonly Color Sun = Color.FromArgb(246, 204, 92);
        public static readonly Color SoftRed = Color.FromArgb(232, 113, 100);
        public static readonly Color Line = Color.FromArgb(224, 222, 210);

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var d = Math.Max(2, radius * 2);
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Font Font(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font("Segoe UI", size, style, GraphicsUnit.Point);
        }
    }

    internal sealed class ChildCard : Panel
    {
        public Color CardColor { get; set; } = ChildVisualTheme.Card;
        public Color BorderColor { get; set; } = ChildVisualTheme.Line;
        public int Radius { get; set; } = 22;

        public ChildCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = ChildVisualTheme.RoundedRect(new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3)), Radius))
            using (var brush = new SolidBrush(CardColor))
            using (var pen = new Pen(BorderColor))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    internal class ChildActionButton : Button
    {
        private bool _hover;
        private bool _pressed;

        public Color FillColor { get; set; } = ChildVisualTheme.MintStrong;
        public Color HoverColor { get; set; } = Color.FromArgb(92, 155, 104);
        public Color PressedColor { get; set; } = Color.FromArgb(78, 138, 91);
        public Color DisabledFillColor { get; set; } = Color.FromArgb(205, 210, 204);
        public Color TextColor { get; set; } = Color.White;
        public Color DisabledTextColor { get; set; } = Color.FromArgb(120, 126, 121);
        public int Radius { get; set; } = 18;
        public string BadgeText { get; set; }

        public ChildActionButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs mevent) { if (mevent.Button == MouseButtons.Left) _pressed = true; Invalidate(); base.OnMouseDown(mevent); }
        protected override void OnMouseUp(MouseEventArgs mevent) { _pressed = false; Invalidate(); base.OnMouseUp(mevent); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
            var fill = Enabled ? (_pressed ? PressedColor : (_hover ? HoverColor : FillColor)) : DisabledFillColor;
            using (var path = ChildVisualTheme.RoundedRect(rect, Radius))
            using (var brush = new SolidBrush(fill))
                pevent.Graphics.FillPath(brush, path);

            var textRect = ClientRectangle;
            if (!string.IsNullOrWhiteSpace(BadgeText)) textRect.Width -= 52;
            TextRenderer.DrawText(pevent.Graphics, Text, Font, textRect,
                Enabled ? TextColor : DisabledTextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (!string.IsNullOrWhiteSpace(BadgeText))
            {
                var badge = new Rectangle(Width - 48, 8, 36, Math.Max(24, Height - 16));
                using (var b = new SolidBrush(Color.FromArgb(42, 255, 255, 255)))
                using (var path = ChildVisualTheme.RoundedRect(badge, 12))
                    pevent.Graphics.FillPath(b, path);
                TextRenderer.DrawText(pevent.Graphics, BadgeText, ChildVisualTheme.Font(9.5f, FontStyle.Bold), badge,
                    Enabled ? TextColor : DisabledTextColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    internal sealed class AnswerChoiceButton : ChildActionButton
    {
        public enum ChoiceVisualState { Idle, Correct, Incorrect, Muted }
        private ChoiceVisualState _visualState;

        public ChoiceVisualState VisualState
        {
            get { return _visualState; }
            set
            {
                _visualState = value;
                ApplyState();
                Invalidate();
            }
        }

        public AnswerChoiceButton()
        {
            FillColor = Color.White;
            HoverColor = Color.FromArgb(244, 249, 240);
            PressedColor = Color.FromArgb(232, 244, 226);
            TextColor = ChildVisualTheme.Ink;
            DisabledFillColor = Color.FromArgb(241, 241, 236);
            DisabledTextColor = ChildVisualTheme.MutedInk;
            Radius = 20;
        }

        private void ApplyState()
        {
            switch (_visualState)
            {
                case ChoiceVisualState.Correct:
                    FillColor = Color.FromArgb(204, 237, 205);
                    HoverColor = FillColor;
                    PressedColor = FillColor;
                    TextColor = Color.FromArgb(45, 111, 57);
                    DisabledFillColor = FillColor;
                    DisabledTextColor = TextColor;
                    break;
                case ChoiceVisualState.Incorrect:
                    FillColor = Color.FromArgb(250, 219, 213);
                    HoverColor = FillColor;
                    PressedColor = FillColor;
                    TextColor = Color.FromArgb(145, 68, 58);
                    DisabledFillColor = FillColor;
                    DisabledTextColor = TextColor;
                    break;
                case ChoiceVisualState.Muted:
                    FillColor = Color.FromArgb(241, 241, 236);
                    HoverColor = FillColor;
                    PressedColor = FillColor;
                    TextColor = ChildVisualTheme.MutedInk;
                    DisabledFillColor = FillColor;
                    DisabledTextColor = TextColor;
                    break;
                default:
                    FillColor = Color.White;
                    HoverColor = Color.FromArgb(244, 249, 240);
                    PressedColor = Color.FromArgb(232, 244, 226);
                    TextColor = ChildVisualTheme.Ink;
                    DisabledFillColor = Color.FromArgb(241, 241, 236);
                    DisabledTextColor = ChildVisualTheme.MutedInk;
                    break;
            }
        }
    }

    internal sealed class ProgressStrip : Control
    {
        private int _value;
        private int _maximum = 8;
        public int Value { get { return _value; } set { _value = Math.Max(0, Math.Min(value, Maximum)); Invalidate(); } }
        public int Maximum { get { return _maximum; } set { _maximum = Math.Max(1, value); _value = Math.Min(_value, _maximum); Invalidate(); } }

        public ProgressStrip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 14;
            AccessibleName = "Tiến độ buổi học";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var track = new Rectangle(0, 2, Math.Max(2, Width - 1), Math.Max(6, Height - 5));
            using (var path = ChildVisualTheme.RoundedRect(track, track.Height / 2))
            using (var b = new SolidBrush(Color.FromArgb(226, 231, 221))) e.Graphics.FillPath(b, path);
            var filledWidth = (int)Math.Round(track.Width * (Value / (double)Maximum));
            if (filledWidth < 4) return;
            var fill = new Rectangle(track.X, track.Y, Math.Min(track.Width, filledWidth), track.Height);
            using (var path = ChildVisualTheme.RoundedRect(fill, fill.Height / 2))
            using (var b = new SolidBrush(ChildVisualTheme.MintStrong)) e.Graphics.FillPath(b, path);
        }
    }

    internal sealed class MathInstructionVisual : Control
    {
        private MathQuestion _question;
        private int _hintLevel;

        public MathInstructionVisual()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            AccessibleName = "Minh họa câu Toán";
        }

        public void SetQuestion(MathQuestion question, int hintLevel)
        {
            _question = question;
            _hintLevel = Math.Max(0, Math.Min(2, hintLevel));
            AccessibleDescription = question == null ? string.Empty : "Minh họa trực quan cho " + question.PromptVi;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_question == null || Width < 80 || Height < 40) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var values = ExtractNumbers(_question.PromptVi);
            if (_question.TemplateId == "mental_add_within_20" || _question.TemplateId == "mental_sub_within_20")
                DrawNumberRay(e.Graphics, values);
            else if (_question.TemplateId == "times_table_2" || _question.TemplateId == "times_table_5")
                DrawGroups(e.Graphics, values);
            else if (_question.TemplateId != null && (_question.TemplateId.StartsWith("add_within_1000", StringComparison.Ordinal) ||
                     _question.TemplateId.StartsWith("subtract_within_1000", StringComparison.Ordinal)))
                DrawPlaceValue(e.Graphics, values);
        }

        private void DrawNumberRay(Graphics g, int[] values)
        {
            if (values.Length < 2) return;
            var start = values[0];
            var amount = values[1];
            var subtract = _question.TemplateId == "mental_sub_within_20";
            var end = subtract ? start - amount : start + amount;
            const int min = 0, max = 20;
            var left = 38;
            var right = Math.Max(left + 40, Width - 38);
            var y = Height / 2 + 8;
            using (var line = new Pen(Color.FromArgb(128, 139, 137), 2f))
            {
                g.DrawLine(line, left, y, right, y);
                for (var n = min; n <= max; n++)
                {
                    var x = left + (int)Math.Round((right - left) * (n / 20.0));
                    var tick = n % 5 == 0 ? 9 : 5;
                    g.DrawLine(line, x, y - tick, x, y + tick);
                    if (n % 5 == 0)
                    {
                        var r = new Rectangle(x - 18, y + 10, 36, 19);
                        TextRenderer.DrawText(g, n.ToString(), ChildVisualTheme.Font(8.5f), r,
                            ChildVisualTheme.MutedInk, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
                    }
                }
            }

            var sx = left + (int)Math.Round((right - left) * (start / 20.0));
            var ex = left + (int)Math.Round((right - left) * (Math.Max(min, Math.Min(max, end)) / 20.0));
            using (var startBrush = new SolidBrush(ChildVisualTheme.PeachStrong))
                g.FillEllipse(startBrush, sx - 6, y - 6, 12, 12);
            var startLabel = new Rectangle(sx - 32, 2, 64, 22);
            TextRenderer.DrawText(g, "bắt đầu " + start, ChildVisualTheme.Font(8.5f, FontStyle.Bold), startLabel,
                ChildVisualTheme.PeachStrong, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            var arrowY = y - 25;
            using (var arrow = new Pen(ChildVisualTheme.MintStrong, 4f))
            {
                arrow.StartCap = LineCap.Round;
                arrow.EndCap = LineCap.ArrowAnchor;
                g.DrawLine(arrow, sx, arrowY, ex, arrowY);
            }
            var label = (subtract ? "lùi " : "tiến ") + amount + " bước";
            var mid = (sx + ex) / 2;
            var labelRect = new Rectangle(mid - 55, arrowY - 25, 110, 20);
            TextRenderer.DrawText(g, label, ChildVisualTheme.Font(9f, FontStyle.Bold), labelRect,
                ChildVisualTheme.MintStrong, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            if (_hintLevel >= 2)
            {
                using (var endBrush = new SolidBrush(Color.FromArgb(118, 172, 116)))
                    g.FillEllipse(endBrush, ex - 5, y - 5, 10, 10);
            }
        }

        private void DrawGroups(Graphics g, int[] values)
        {
            if (values.Length < 2) return;
            var perGroup = values[0];
            var groups = Math.Max(1, Math.Min(10, values[1]));
            var cols = Math.Min(5, groups);
            var rows = (int)Math.Ceiling(groups / (double)cols);
            var cellW = Math.Max(44, Width / Math.Max(1, cols));
            var cellH = Math.Max(42, Height / Math.Max(1, rows));
            for (var i = 0; i < groups; i++)
            {
                var col = i % cols;
                var row = i / cols;
                var cx = col * cellW + cellW / 2;
                var cy = row * cellH + cellH / 2;
                using (var ring = new Pen(Color.FromArgb(179, 201, 176), 2f))
                    g.DrawEllipse(ring, cx - 20, cy - 16, 40, 32);
                using (var dot = new SolidBrush(ChildVisualTheme.MintStrong))
                {
                    if (perGroup <= 5)
                    {
                        var span = 9;
                        var startX = cx - ((perGroup - 1) * span) / 2;
                        for (var d = 0; d < perGroup; d++) g.FillEllipse(dot, startX + d * span - 3, cy - 3, 7, 7);
                    }
                }
            }
            var labelRect = new Rectangle(0, Math.Max(0, Height - 22), Width, 20);
            TextRenderer.DrawText(g, groups + " nhóm · mỗi nhóm " + perGroup, ChildVisualTheme.Font(8.5f), labelRect,
                ChildVisualTheme.MutedInk, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void DrawPlaceValue(Graphics g, int[] values)
        {
            if (values.Length < 2) return;
            var a = values[0];
            var b = values[1];
            var add = _question.TemplateId.StartsWith("add_", StringComparison.Ordinal);
            var headers = new[] { "Trăm", "Chục", "Đơn vị" };
            var digitsA = new[] { (a / 100) % 10, (a / 10) % 10, a % 10 };
            var digitsB = new[] { (b / 100) % 10, (b / 10) % 10, b % 10 };
            var cellW = Math.Min(105, Math.Max(74, Width / 4));
            var totalW = cellW * 3;
            var left = (Width - totalW) / 2;
            var headerY = 1;
            var rowA = 27;
            var rowB = 58;
            var cueColumn = FindCarryBorrowColumn(digitsA, digitsB, add);

            for (var c = 0; c < 3; c++)
            {
                var x = left + c * cellW;
                if (_hintLevel >= 1 && c == cueColumn)
                {
                    using (var hi = new SolidBrush(Color.FromArgb(65, ChildVisualTheme.Sun)))
                        g.FillRectangle(hi, x + 3, 0, cellW - 6, Math.Min(Height - 2, 88));
                }
                TextRenderer.DrawText(g, headers[c], ChildVisualTheme.Font(8.5f, FontStyle.Bold),
                    new Rectangle(x, headerY, cellW, 20), ChildVisualTheme.MutedInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, digitsA[c].ToString(), ChildVisualTheme.Font(15f, FontStyle.Bold),
                    new Rectangle(x, rowA, cellW, 27), ChildVisualTheme.Ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, digitsB[c].ToString(), ChildVisualTheme.Font(15f, FontStyle.Bold),
                    new Rectangle(x, rowB, cellW, 27), ChildVisualTheme.Ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                using (var divider = new Pen(Color.FromArgb(224, 222, 210), 1f))
                    if (c > 0) g.DrawLine(divider, x, 3, x, Math.Min(Height - 3, 88));
            }
            var signRect = new Rectangle(Math.Max(0, left - 37), rowB, 34, 27);
            TextRenderer.DrawText(g, add ? "+" : "−", ChildVisualTheme.Font(15f, FontStyle.Bold), signRect,
                ChildVisualTheme.PeachStrong, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            using (var pen = new Pen(ChildVisualTheme.Ink, 2f))
                g.DrawLine(pen, left + 5, Math.Min(Height - 8, 88), left + totalW - 5, Math.Min(Height - 8, 88));

            if (_hintLevel >= 2 && cueColumn >= 0)
            {
                var cue = add ? "nhớ 1 sang trái" : "mượn 1 từ trái";
                var cueX = left + cueColumn * cellW;
                TextRenderer.DrawText(g, cue, ChildVisualTheme.Font(8.5f, FontStyle.Bold),
                    new Rectangle(cueX - 25, Math.Min(Height - 25, 92), cellW + 50, 22),
                    ChildVisualTheme.PeachStrong, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private int FindCarryBorrowColumn(int[] a, int[] b, bool add)
        {
            if (_question == null || (!_question.TemplateId.EndsWith("one_carry", StringComparison.Ordinal) &&
                !_question.TemplateId.EndsWith("one_borrow", StringComparison.Ordinal))) return -1;
            if (add)
            {
                if (a[2] + b[2] >= 10) return 2;
                if (a[1] + b[1] >= 10) return 1;
            }
            else
            {
                if (a[2] < b[2]) return 2;
                var borrowed = a[2] < b[2] ? 1 : 0;
                if (a[1] - borrowed < b[1]) return 1;
            }
            return -1;
        }

        private static int[] ExtractNumbers(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new int[0];
            var list = new System.Collections.Generic.List<int>();
            var current = -1;
            foreach (var ch in text)
            {
                if (ch >= '0' && ch <= '9')
                {
                    if (current < 0) current = 0;
                    current = current * 10 + (ch - '0');
                }
                else if (current >= 0)
                {
                    list.Add(current);
                    current = -1;
                }
            }
            if (current >= 0) list.Add(current);
            return list.ToArray();
        }
    }

    internal sealed class GardenWorldControl : Control
    {
        public int GrowthLevel { get; set; }
        public bool CalmMode { get; set; }
        public bool HasSeedling { get; set; }
        public bool HasFlowerPatch { get; set; }
        public bool HasLantern { get; set; }
        public bool HasBench { get; set; }

        public GardenWorldControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AccessibleName = "Khu vườn học tập";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var w = Width;
            var h = Height;
            using (var sky = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, w), Math.Max(1, h)),
                Color.FromArgb(224, 241, 247), Color.FromArgb(248, 245, 219), 90f))
                g.FillRectangle(sky, ClientRectangle);

            using (var sun = new SolidBrush(Color.FromArgb(245, 204, 93)))
                g.FillEllipse(sun, Math.Max(8, w - 90), 18, 48, 48);

            var hillY = (int)(h * .58);
            using (var hill = new SolidBrush(Color.FromArgb(196, 224, 177)))
                g.FillEllipse(hill, -70, hillY - 8, w + 150, h - hillY + 90);
            using (var soil = new SolidBrush(Color.FromArgb(216, 195, 151)))
                g.FillEllipse(soil, (int)(w * .08), (int)(h * .72), (int)(w * .84), (int)(h * .19));

            DrawTree(g, (int)(w * .20), hillY + 10, GrowthLevel >= 3 ? 1.15f : .9f);
            DrawTree(g, (int)(w * .78), hillY + 22, GrowthLevel >= 6 ? 1.0f : .72f);
            DrawPlant(g, (int)(w * .42), (int)(h * .78), HasSeedling || GrowthLevel >= 2 ? 3 : 1);
            DrawPlant(g, (int)(w * .56), (int)(h * .79), HasFlowerPatch || GrowthLevel >= 5 ? 4 : 2);
            if (HasFlowerPatch) DrawFlowerPatch(g, (int)(w * .30), (int)(h * .80));
            if (HasLantern) DrawLantern(g, (int)(w * .70), (int)(h * .70));
            if (HasBench) DrawBench(g, (int)(w * .13), (int)(h * .68));
            DrawCompanion(g, (int)(w * .61), (int)(h * .57));
        }

        private static void DrawFlowerPatch(Graphics g, int x, int y)
        {
            var colors = new[] { ChildVisualTheme.Sun, Color.FromArgb(231, 151, 149), Color.FromArgb(154, 179, 226) };
            for (var i = 0; i < 5; i++)
            {
                var px = x + i * 12;
                using (var stem = new Pen(Color.FromArgb(86, 145, 88), 2f)) g.DrawLine(stem, px, y, px, y - 14 - (i % 2) * 4);
                using (var bloom = new SolidBrush(colors[i % colors.Length])) g.FillEllipse(bloom, px - 5, y - 21 - (i % 2) * 4, 10, 10);
            }
        }

        private static void DrawLantern(Graphics g, int x, int y)
        {
            using (var pole = new Pen(Color.FromArgb(104, 92, 78), 4f))
            {
                g.DrawLine(pole, x, y, x, y - 70);
                g.DrawLine(pole, x, y - 70, x + 18, y - 70);
            }
            using (var glow = new SolidBrush(Color.FromArgb(225, 246, 206, 98)))
                g.FillEllipse(glow, x + 3, y - 76, 30, 30);
            using (var lamp = new SolidBrush(Color.FromArgb(246, 206, 98)))
            using (var outline = new Pen(Color.FromArgb(123, 101, 68), 2f))
            {
                var r = new Rectangle(x + 8, y - 70, 20, 22);
                g.FillRectangle(lamp, r);
                g.DrawRectangle(outline, r);
            }
        }

        private static void DrawBench(Graphics g, int x, int y)
        {
            using (var wood = new SolidBrush(Color.FromArgb(176, 126, 82)))
            using (var dark = new Pen(Color.FromArgb(119, 91, 69), 3f))
            {
                g.FillRectangle(wood, x, y - 28, 74, 10);
                g.FillRectangle(wood, x + 4, y - 14, 66, 9);
                g.DrawLine(dark, x + 12, y - 5, x + 8, y + 18);
                g.DrawLine(dark, x + 60, y - 5, x + 64, y + 18);
            }
        }

        private static void DrawTree(Graphics g, int x, int groundY, float scale)
        {
            var trunkW = (int)(18 * scale);
            var trunkH = (int)(54 * scale);
            using (var trunk = new SolidBrush(Color.FromArgb(164, 120, 78)))
                g.FillRectangle(trunk, x - trunkW / 2, groundY - trunkH, trunkW, trunkH);
            using (var leaves = new SolidBrush(Color.FromArgb(105, 172, 116)))
            {
                var r = (int)(42 * scale);
                g.FillEllipse(leaves, x - r, groundY - trunkH - r, r * 2, r * 2);
                g.FillEllipse(leaves, x - r - 18, groundY - trunkH - r / 2, r + 18, r + 8);
                g.FillEllipse(leaves, x + 5, groundY - trunkH - r / 2, r + 18, r + 8);
            }
        }

        private static void DrawPlant(Graphics g, int x, int y, int stage)
        {
            using (var stem = new Pen(Color.FromArgb(86, 145, 88), 4f))
                g.DrawLine(stem, x, y, x, y - 12 - stage * 5);
            using (var leaf = new SolidBrush(Color.FromArgb(115, 181, 106)))
            {
                g.FillEllipse(leaf, x - 14, y - 17 - stage * 3, 16, 9);
                if (stage >= 2) g.FillEllipse(leaf, x + 1, y - 24 - stage * 3, 16, 9);
            }
            if (stage >= 3)
            {
                using (var flower = new SolidBrush(ChildVisualTheme.Sun))
                    g.FillEllipse(flower, x - 7, y - 37 - stage * 3, 14, 14);
            }
        }

        private static void DrawCompanion(Graphics g, int x, int y)
        {
            using (var body = new SolidBrush(Color.FromArgb(245, 238, 220)))
            using (var outline = new Pen(Color.FromArgb(102, 115, 112), 2f))
            {
                var bodyRect = new Rectangle(x - 24, y - 18, 48, 42);
                g.FillEllipse(body, bodyRect);
                g.DrawEllipse(outline, bodyRect);
                var earL = new Point[] { new Point(x - 18, y - 12), new Point(x - 28, y - 35), new Point(x - 5, y - 22) };
                var earR = new Point[] { new Point(x + 18, y - 12), new Point(x + 28, y - 35), new Point(x + 5, y - 22) };
                g.FillPolygon(body, earL); g.DrawPolygon(outline, earL);
                g.FillPolygon(body, earR); g.DrawPolygon(outline, earR);
            }
            using (var eye = new SolidBrush(ChildVisualTheme.Ink))
            {
                g.FillEllipse(eye, x - 10, y - 2, 4, 5);
                g.FillEllipse(eye, x + 6, y - 2, 4, 5);
            }
            using (var nose = new SolidBrush(Color.FromArgb(204, 132, 116)))
                g.FillEllipse(nose, x - 3, y + 6, 6, 5);
        }
    }
}
