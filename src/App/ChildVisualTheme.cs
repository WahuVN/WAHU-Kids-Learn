using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WAHU.Data;
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
            if (_question.TemplateId == "place_value_decompose_3digit" || _question.TemplateId == "expanded_form_3digit")
                DrawPlaceValueConcept(e.Graphics, values);
            else if (_question.TemplateId == "predecessor_successor" || _question.TemplateId == "compare_two_numbers_1000")
                DrawNumberOrder(e.Graphics, values);
            else if (_question.TemplateId == "mental_add_within_20" || _question.TemplateId == "mental_sub_within_20")
                DrawNumberRay(e.Graphics, values);
            else if (_question.TemplateId == "times_table_2" || _question.TemplateId == "times_table_5")
                DrawGroups(e.Graphics, values);
            else if (_question.TemplateId == "divide_table_2_exact" || _question.TemplateId == "divide_table_5_exact")
                DrawDivisionGroups(e.Graphics, values);
            else if (_question.TemplateId != null && (_question.TemplateId.StartsWith("add_within_1000", StringComparison.Ordinal) ||
                     _question.TemplateId.StartsWith("subtract_within_1000", StringComparison.Ordinal)))
                DrawPlaceValue(e.Graphics, values);
            else if (_question.TemplateId == "polyline_length")
                DrawPolyline(e.Graphics, values);
        }

        private void DrawPlaceValueConcept(Graphics g, int[] values)
        {
            if (values.Length < 1) return;
            var n = Math.Max(0, Math.Min(999, values[0]));
            var digits = new[] { (n / 100) % 10, (n / 10) % 10, n % 10 };
            var headers = new[] { "Trăm", "Chục", "Đơn vị" };
            var cellW = Math.Min(115, Math.Max(78, Width / 4));
            var totalW = cellW * 3;
            var left = (Width - totalW) / 2;
            for (var i = 0; i < 3; i++)
            {
                var x = left + i * cellW;
                using (var fill = new SolidBrush(i == 0 ? Color.FromArgb(241, 230, 198) : (i == 1 ? Color.FromArgb(221, 239, 224) : Color.FromArgb(220, 236, 248))))
                using (var pen = new Pen(Color.FromArgb(204, 204, 191), 1f))
                {
                    var rect = new Rectangle(x + 5, 12, cellW - 10, Math.Min(72, Math.Max(50, Height - 24)));
                    g.FillRectangle(fill, rect);
                    g.DrawRectangle(pen, rect);
                }
                TextRenderer.DrawText(g, headers[i], ChildVisualTheme.Font(8.8f, FontStyle.Bold),
                    new Rectangle(x, 14, cellW, 20), ChildVisualTheme.MutedInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                var valueText = _hintLevel >= 1 ? digits[i].ToString() : "?";
                TextRenderer.DrawText(g, valueText, ChildVisualTheme.Font(18f, FontStyle.Bold),
                    new Rectangle(x, 35, cellW, 34), ChildVisualTheme.Ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                if (_hintLevel >= 2)
                {
                    var unit = i == 0 ? digits[i] * 100 : (i == 1 ? digits[i] * 10 : digits[i]);
                    TextRenderer.DrawText(g, "= " + unit, ChildVisualTheme.Font(8.5f),
                        new Rectangle(x, 67, cellW, 19), ChildVisualTheme.PeachStrong,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }

        private void DrawNumberOrder(Graphics g, int[] values)
        {
            if (_question.TemplateId == "compare_two_numbers_1000")
            {
                if (values.Length < 2) return;
                var a = values[0]; var b = values[1];
                var numbers = new[] { a, b };
                var boxW = Math.Min(190, Math.Max(120, Width / 3));
                var gap = 36;
                var total = boxW * 2 + gap;
                var left = (Width - total) / 2;
                for (var k = 0; k < 2; k++)
                {
                    var box = new Rectangle(left + k * (boxW + gap), 18, boxW, Math.Min(72, Height - 30));
                    using (var brush = new SolidBrush(k == 0 ? Color.FromArgb(238, 245, 221) : Color.FromArgb(224, 239, 249)))
                    using (var pen = new Pen(Color.FromArgb(205, 209, 195)))
                    { g.FillRectangle(brush, box); g.DrawRectangle(pen, box); }
                    TextRenderer.DrawText(g, numbers[k].ToString(), ChildVisualTheme.Font(18f, FontStyle.Bold), box,
                        ChildVisualTheme.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    if (_hintLevel >= 1)
                    {
                        var h = (numbers[k] / 100) % 10; var t = (numbers[k] / 10) % 10; var o = numbers[k] % 10;
                        TextRenderer.DrawText(g, "T " + h + " · C " + t + " · ĐV " + o, ChildVisualTheme.Font(8.2f),
                            new Rectangle(box.Left, box.Bottom - 20, box.Width, 18), ChildVisualTheme.MutedInk,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
                }
                TextRenderer.DrawText(g, "?", ChildVisualTheme.Font(20f, FontStyle.Bold),
                    new Rectangle(left + boxW, 30, gap, 36), ChildVisualTheme.PeachStrong,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            if (values.Length < 1) return;
            var n = values[0];
            var centerX = Width / 2;
            var y = Height / 2 + 10;
            var ticks = new[] { centerX - 105, centerX, centerX + 105 };
            for (var i = 0; i < ticks.Length; i++)
            {
                var card = new Rectangle(ticks[i] - 34, y - 31, 68, 48);
                var fillColor = i == 1 ? Color.FromArgb(232, 242, 224) : Color.FromArgb(240, 239, 229);
                using (var path = ChildVisualTheme.RoundedRect(card, 12))
                using (var brush = new SolidBrush(fillColor))
                using (var border = new Pen(Color.FromArgb(212, 214, 201)))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(border, path);
                }
            }
            using (var pen = new Pen(Color.FromArgb(128, 139, 137), 2f)) g.DrawLine(pen, centerX - 150, y, centerX + 150, y);
            foreach (var x in ticks)
            {
                using (var pen = new Pen(Color.FromArgb(128, 139, 137), 2f)) g.DrawLine(pen, x, y - 10, x, y + 10);
            }
            TextRenderer.DrawText(g, n.ToString(), ChildVisualTheme.Font(15f, FontStyle.Bold),
                new Rectangle(centerX - 45, y - 45, 90, 30), ChildVisualTheme.Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "−1", ChildVisualTheme.Font(9f, FontStyle.Bold),
                new Rectangle(centerX - 150, y - 40, 90, 22), ChildVisualTheme.MintStrong,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "+1", ChildVisualTheme.Font(9f, FontStyle.Bold),
                new Rectangle(centerX + 60, y - 40, 90, 22), ChildVisualTheme.MintStrong,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "?", ChildVisualTheme.Font(13f, FontStyle.Bold),
                new Rectangle(centerX - 135, y + 12, 60, 24), ChildVisualTheme.PeachStrong,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "?", ChildVisualTheme.Font(13f, FontStyle.Bold),
                new Rectangle(centerX + 75, y + 12, 60, 24), ChildVisualTheme.PeachStrong,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void DrawDivisionGroups(Graphics g, int[] values)
        {
            if (values.Length < 2) return;
            var dividend = Math.Max(1, Math.Min(50, values[0]));
            var divisor = Math.Max(1, Math.Min(5, values[1]));
            var cols = Math.Min(10, dividend);
            var rows = (int)Math.Ceiling(dividend / (double)cols);
            var spacingX = Math.Max(16, Math.Min(28, (Width - 60) / Math.Max(1, cols)));
            var spacingY = Math.Max(18, Math.Min(26, (Height - 30) / Math.Max(1, rows)));
            var startX = Math.Max(25, (Width - (cols - 1) * spacingX) / 2);
            var startY = 20;
            using (var dot = new SolidBrush(ChildVisualTheme.SkyStrong))
            {
                for (var i = 0; i < dividend; i++)
                {
                    var col = i % cols; var row = i / cols;
                    g.FillEllipse(dot, startX + col * spacingX - 4, startY + row * spacingY - 4, 8, 8);
                }
            }
            if (_hintLevel >= 1)
            {
                var groupCount = dividend / divisor;
                using (var ring = new Pen(Color.FromArgb(138, 180, 122), 2f))
                {
                    for (var group = 0; group < groupCount; group++)
                    {
                        var first = group * divisor;
                        var last = first + divisor - 1;
                        var row1 = first / cols; var col1 = first % cols;
                        var row2 = last / cols; var col2 = last % cols;
                        if (row1 == row2)
                        {
                            var x1 = startX + col1 * spacingX - 10;
                            var x2 = startX + col2 * spacingX + 10;
                            g.DrawEllipse(ring, x1, startY + row1 * spacingY - 12, Math.Max(20, x2 - x1), 24);
                        }
                    }
                }
            }
            TextRenderer.DrawText(g, "mỗi nhóm " + divisor, ChildVisualTheme.Font(8.8f, FontStyle.Bold),
                new Rectangle(0, Math.Max(0, Height - 24), Width, 22), ChildVisualTheme.MutedInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void DrawPolyline(Graphics g, int[] values)
        {
            if (values.Length < 3) return;
            var points = new[]
            {
                new Point(55, Math.Max(55, Height - 28)),
                new Point(Math.Max(150, Width / 3), 24),
                new Point(Math.Max(270, Width * 2 / 3), Math.Max(62, Height - 24)),
                new Point(Math.Max(360, Width - 55), 30)
            };
            using (var pen = new Pen(ChildVisualTheme.SkyStrong, 4f))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                for (var i = 0; i < 3; i++) g.DrawLine(pen, points[i], points[i + 1]);
            }
            for (var i = 0; i < 3; i++)
            {
                var mx = (points[i].X + points[i + 1].X) / 2;
                var my = (points[i].Y + points[i + 1].Y) / 2;
                TextRenderer.DrawText(g, values[i] + " cm", ChildVisualTheme.Font(9f, FontStyle.Bold),
                    new Rectangle(mx - 34, my - 24, 68, 21), ChildVisualTheme.PeachStrong,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            if (_hintLevel >= 1)
                TextRenderer.DrawText(g, "cộng 3 đoạn", ChildVisualTheme.Font(9f, FontStyle.Bold),
                    new Rectangle(0, Math.Max(0, Height - 22), Width, 20), ChildVisualTheme.MintStrong,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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

    internal enum CompanionReactionState
    {
        Calm,
        Correct,
        TryAgain,
        Tired,
        Celebrate
    }

    internal sealed class CompanionReactionControl : Control
    {
        private CompanionReactionState _state;

        public CompanionReactionState State
        {
            get { return _state; }
            set { _state = value; AccessibleDescription = Describe(value); Invalidate(); }
        }

        public CompanionReactionControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            AccessibleName = "Bạn đồng hành";
            State = CompanionReactionState.Calm;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var cx = Width / 2;
            var cy = Height / 2 + 2;
            var size = Math.Max(34, Math.Min(58, Math.Min(Width - 6, Height - 6)));
            var face = new Rectangle(cx - size / 2, cy - size / 2, size, size - 4);
            var fill = State == CompanionReactionState.Tired
                ? Color.FromArgb(238, 235, 221) : Color.FromArgb(248, 240, 220);
            using (var body = new SolidBrush(fill))
            using (var outline = new Pen(Color.FromArgb(105, 116, 111), 2f))
            {
                var earL = new Point[]
                {
                    new Point(face.Left + 8, face.Top + 10), new Point(face.Left + 3, face.Top - 8), new Point(face.Left + 20, face.Top + 2)
                };
                var earR = new Point[]
                {
                    new Point(face.Right - 8, face.Top + 10), new Point(face.Right - 3, face.Top - 8), new Point(face.Right - 20, face.Top + 2)
                };
                g.FillPolygon(body, earL); g.DrawPolygon(outline, earL);
                g.FillPolygon(body, earR); g.DrawPolygon(outline, earR);
                g.FillEllipse(body, face); g.DrawEllipse(outline, face);
            }

            DrawEyes(g, cx, cy, size);
            DrawMouth(g, cx, cy, size);
            if (State == CompanionReactionState.Celebrate)
            {
                using (var star = new SolidBrush(ChildVisualTheme.Sun))
                {
                    g.FillEllipse(star, face.Right - 3, face.Top - 9, 8, 8);
                    g.FillEllipse(star, face.Left - 5, face.Top + 4, 6, 6);
                }
            }
        }

        private void DrawEyes(Graphics g, int cx, int cy, int size)
        {
            var eyeY = cy - size / 10;
            var dx = size / 6;
            using (var pen = new Pen(ChildVisualTheme.Ink, 2f))
            using (var brush = new SolidBrush(ChildVisualTheme.Ink))
            {
                if (State == CompanionReactionState.Correct || State == CompanionReactionState.Celebrate)
                {
                    g.DrawArc(pen, cx - dx - 5, eyeY - 2, 10, 8, 5, 170);
                    g.DrawArc(pen, cx + dx - 5, eyeY - 2, 10, 8, 5, 170);
                }
                else if (State == CompanionReactionState.Tired)
                {
                    g.DrawLine(pen, cx - dx - 5, eyeY, cx - dx + 5, eyeY);
                    g.DrawLine(pen, cx + dx - 5, eyeY, cx + dx + 5, eyeY);
                }
                else
                {
                    g.FillEllipse(brush, cx - dx - 3, eyeY - 2, 6, 7);
                    g.FillEllipse(brush, cx + dx - 3, eyeY - 2, 6, 7);
                }
            }
        }

        private void DrawMouth(Graphics g, int cx, int cy, int size)
        {
            using (var pen = new Pen(Color.FromArgb(173, 105, 91), 2f))
            {
                if (State == CompanionReactionState.Correct || State == CompanionReactionState.Celebrate)
                    g.DrawArc(pen, cx - 9, cy + size / 10, 18, 10, 10, 160);
                else if (State == CompanionReactionState.TryAgain)
                    g.DrawArc(pen, cx - 7, cy + size / 8, 14, 7, 190, 160);
                else
                    g.DrawLine(pen, cx - 6, cy + size / 7, cx + 6, cy + size / 7);
            }
        }

        private static string Describe(CompanionReactionState state)
        {
            switch (state)
            {
                case CompanionReactionState.Correct: return "Bạn đồng hành vui nhẹ vì câu trả lời đúng.";
                case CompanionReactionState.TryAgain: return "Bạn đồng hành bình tĩnh động viên thử tiếp.";
                case CompanionReactionState.Tired: return "Bạn đồng hành gợi ý nghỉ ngơi.";
                case CompanionReactionState.Celebrate: return "Bạn đồng hành chúc mừng hoàn thành nhiệm vụ.";
                default: return "Bạn đồng hành đang bình tĩnh chờ bé suy nghĩ.";
            }
        }
    }

    internal sealed class LessonCompletionVisual : Control
    {
        private int _growthSteps;
        private string _unlockedItemId;
        private int _sessionsUntilNext;
        private string _nextItemId;

        public LessonCompletionVisual()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            AccessibleName = "Tiến bộ khu vườn sau nhiệm vụ";
        }

        public void SetProgress(int growthSteps, string unlockedItemId, int sessionsUntilNext, string nextItemId)
        {
            _growthSteps = Math.Max(0, growthSteps);
            _unlockedItemId = unlockedItemId;
            _sessionsUntilNext = Math.Max(0, sessionsUntilNext);
            _nextItemId = nextItemId;
            AccessibleDescription = BuildDescription();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var iconSize = Math.Min(58, Math.Max(38, Height - 16));
            var icon = new Rectangle(16, (Height - iconSize) / 2, iconSize, iconSize);
            using (var circle = new SolidBrush(Color.FromArgb(224, 242, 220))) g.FillEllipse(circle, icon);
            DrawRewardIcon(g, icon, _unlockedItemId);

            var textLeft = icon.Right + 16;
            var titleRect = new Rectangle(textLeft, 6, Math.Max(20, Width - textLeft - 10), 25);
            var detailRect = new Rectangle(textLeft, 31, Math.Max(20, Width - textLeft - 10), Math.Max(22, Height - 34));
            var title = string.IsNullOrWhiteSpace(_unlockedItemId)
                ? "Khu vườn lớn thêm 1 bước"
                : "Mở khóa: " + ItemName(_unlockedItemId);
            TextRenderer.DrawText(g, title, ChildVisualTheme.Font(11f, FontStyle.Bold), titleRect,
                ChildVisualTheme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            var next = _sessionsUntilNext > 0 && !string.IsNullOrWhiteSpace(_nextItemId)
                ? "Còn " + _sessionsUntilNext + " nhiệm vụ hoàn thành để tới " + ItemName(_nextItemId) + "."
                : "Các mốc khu vườn hiện tại đã được mở đủ.";
            TextRenderer.DrawText(g, "Vườn: " + _growthSteps + " bước · " + next,
                ChildVisualTheme.Font(9.5f), detailRect, ChildVisualTheme.MutedInk,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        }

        private static void DrawRewardIcon(Graphics g, Rectangle r, string itemId)
        {
            var cx = r.Left + r.Width / 2;
            var bottom = r.Bottom - 10;
            if (itemId == "garden_lantern")
            {
                using (var pole = new Pen(Color.FromArgb(107, 91, 72), 3f)) g.DrawLine(pole, cx, bottom, cx, r.Top + 10);
                using (var lamp = new SolidBrush(ChildVisualTheme.Sun)) g.FillRectangle(lamp, cx + 1, r.Top + 10, 13, 15);
                return;
            }
            if (itemId == "garden_bench")
            {
                using (var wood = new SolidBrush(Color.FromArgb(176, 126, 82)))
                {
                    g.FillRectangle(wood, r.Left + 9, r.Top + 17, r.Width - 18, 7);
                    g.FillRectangle(wood, r.Left + 12, r.Top + 30, r.Width - 24, 7);
                }
                return;
            }
            if (itemId == "garden_flower_patch")
            {
                for (var i = 0; i < 3; i++)
                {
                    var x = r.Left + 15 + i * 13;
                    using (var stem = new Pen(Color.FromArgb(88, 145, 88), 2f)) g.DrawLine(stem, x, bottom, x, r.Top + 19);
                    using (var bloom = new SolidBrush(i == 1 ? ChildVisualTheme.PeachStrong : ChildVisualTheme.Sun))
                        g.FillEllipse(bloom, x - 5, r.Top + 13, 10, 10);
                }
                return;
            }
            using (var stem = new Pen(Color.FromArgb(88, 145, 88), 3f)) g.DrawLine(stem, cx, bottom, cx, r.Top + 14);
            using (var leaf = new SolidBrush(ChildVisualTheme.MintStrong))
            {
                g.FillEllipse(leaf, cx - 16, r.Top + 19, 16, 9);
                g.FillEllipse(leaf, cx, r.Top + 27, 16, 9);
            }
        }

        private string BuildDescription()
        {
            var unlocked = string.IsNullOrWhiteSpace(_unlockedItemId) ? "khu vườn lớn thêm một bước" : "mở khóa " + ItemName(_unlockedItemId);
            var next = _sessionsUntilNext > 0 && !string.IsNullOrWhiteSpace(_nextItemId)
                ? ", còn " + _sessionsUntilNext + " nhiệm vụ tới " + ItemName(_nextItemId) : string.Empty;
            return unlocked + next + ".";
        }

        private static string ItemName(string itemId)
        {
            switch (itemId)
            {
                case "garden_seedling": return "Mầm cây mới";
                case "garden_flower_patch": return "Bồn hoa";
                case "garden_lantern": return "Đèn vườn";
                case "garden_bench": return "Ghế nhỏ";
                default: return "mốc khu vườn";
            }
        }
    }

    internal sealed class MathRoadmapControl : Control
    {
        private MathRoadmapSnapshot _snapshot;

        public MathRoadmapControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            AccessibleName = "Lộ trình Toán";
        }

        public void SetSnapshot(MathRoadmapSnapshot snapshot)
        {
            _snapshot = snapshot;
            AccessibleDescription = BuildAccessibleDescription(snapshot);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rows = new[]
            {
                new RoadmapRow("Số đến 1000", _snapshot == null ? null : _snapshot.NumberSense, ChildVisualTheme.Sun),
                new RoadmapRow("Nhẩm 0–20", _snapshot == null ? null : _snapshot.Mental20, ChildVisualTheme.PeachStrong),
                new RoadmapRow("Cộng / Trừ đến 1000", _snapshot == null ? null : _snapshot.Written1000, ChildVisualTheme.MintStrong),
                new RoadmapRow("Nhân / Chia 2 · 5", _snapshot == null ? null : _snapshot.Tables25, ChildVisualTheme.SkyStrong),
                new RoadmapRow("Đo lường", _snapshot == null ? null : _snapshot.Measurement, Color.FromArgb(147, 126, 181))
            };
            var rowH = Math.Max(20, Height / 5);
            for (var i = 0; i < rows.Length; i++) DrawRow(e.Graphics, rows[i], new Rectangle(0, i * rowH, Width, rowH));
        }

        private static void DrawRow(Graphics g, RoadmapRow row, Rectangle bounds)
        {
            var titleWidth = Math.Min(176, Math.Max(112, bounds.Width / 3));
            var titleRect = new Rectangle(bounds.Left + 4, bounds.Top + 2, titleWidth - 8, bounds.Height - 4);
            TextRenderer.DrawText(g, row.Title, ChildVisualTheme.Font(9.3f, FontStyle.Bold), titleRect,
                ChildVisualTheme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            var statusWidth = Math.Min(96, Math.Max(72, bounds.Width / 5));
            var barLeft = bounds.Left + titleWidth;
            var barRight = Math.Max(barLeft + 24, bounds.Right - statusWidth - 6);
            var track = new Rectangle(barLeft, bounds.Top + bounds.Height / 2 - 5, Math.Max(20, barRight - barLeft), 10);
            using (var path = ChildVisualTheme.RoundedRect(track, 5))
            using (var bg = new SolidBrush(Color.FromArgb(229, 229, 220))) g.FillPath(bg, path);

            var hasEvidence = row.Progress != null && row.Progress.HasEvidence;
            var score = hasEvidence ? Math.Max(0, Math.Min(1, row.Progress.MasteryAverage)) : 0.0;
            var fillWidth = (int)Math.Round(track.Width * score);
            if (fillWidth >= 4)
            {
                var fill = new Rectangle(track.Left, track.Top, Math.Min(track.Width, fillWidth), track.Height);
                using (var path = ChildVisualTheme.RoundedRect(fill, 5))
                using (var brush = new SolidBrush(row.Accent)) g.FillPath(brush, path);
            }

            var status = !hasEvidence ? "Chưa bắt đầu" :
                (score >= 0.80 ? "Vững" : (score >= 0.55 ? "Vững dần" : "Đang học"));
            var statusRect = new Rectangle(barRight + 5, bounds.Top + 2, statusWidth, bounds.Height - 4);
            TextRenderer.DrawText(g, status, ChildVisualTheme.Font(8.7f, hasEvidence ? FontStyle.Bold : FontStyle.Regular),
                statusRect, hasEvidence ? row.Accent : ChildVisualTheme.MutedInk,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static string BuildAccessibleDescription(MathRoadmapSnapshot snapshot)
        {
            if (snapshot == null) return "Chưa có dữ liệu lộ trình Toán.";
            return DescribeGroup("Số đến 1000", snapshot.NumberSense) + "; " +
                   DescribeGroup("Nhẩm 0 đến 20", snapshot.Mental20) + "; " +
                   DescribeGroup("Cộng trừ đến 1000", snapshot.Written1000) + "; " +
                   DescribeGroup("Nhân chia bảng 2 và 5", snapshot.Tables25) + "; " +
                   DescribeGroup("Đo lường", snapshot.Measurement) + ".";
        }

        private static string DescribeGroup(string name, MathRoadmapGroupProgress progress)
        {
            if (progress == null || !progress.HasEvidence) return name + " chưa bắt đầu";
            var score = Math.Max(0, Math.Min(1, progress.MasteryAverage));
            var stage = score >= 0.80 ? "đã vững" : (score >= 0.55 ? "đang vững dần" : "đang học");
            return name + " " + stage;
        }

        private sealed class RoadmapRow
        {
            public RoadmapRow(string title, MathRoadmapGroupProgress progress, Color accent)
            {
                Title = title;
                Progress = progress;
                Accent = accent;
            }

            public string Title { get; private set; }
            public MathRoadmapGroupProgress Progress { get; private set; }
            public Color Accent { get; private set; }
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
