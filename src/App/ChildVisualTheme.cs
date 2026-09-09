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
        // A brighter, higher-contrast palette keeps the app gentle without looking washed out.
        public static readonly Color Ink = Color.FromArgb(39, 50, 66);
        public static readonly Color MutedInk = Color.FromArgb(91, 104, 119);
        public static readonly Color Cream = Color.FromArgb(247, 250, 246);
        public static readonly Color Card = Color.FromArgb(255, 255, 252);
        public static readonly Color Mint = Color.FromArgb(216, 242, 223);
        public static readonly Color MintStrong = Color.FromArgb(67, 157, 100);
        public static readonly Color Sky = Color.FromArgb(222, 239, 252);
        public static readonly Color SkyStrong = Color.FromArgb(64, 137, 190);
        public static readonly Color Peach = Color.FromArgb(255, 230, 202);
        public static readonly Color PeachStrong = Color.FromArgb(224, 139, 69);
        public static readonly Color Sun = Color.FromArgb(250, 199, 61);
        public static readonly Color SoftRed = Color.FromArgb(225, 99, 91);
        public static readonly Color Lavender = Color.FromArgb(234, 226, 250);
        public static readonly Color LavenderStrong = Color.FromArgb(133, 104, 184);
        public static readonly Color Line = Color.FromArgb(217, 225, 216);
        public static readonly Color Shadow = Color.FromArgb(34, 56, 72, 70);

        public static Color Blend(Color from, Color to, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            return Color.FromArgb(
                (int)Math.Round(from.A + (to.A - from.A) * amount),
                (int)Math.Round(from.R + (to.R - from.R) * amount),
                (int)Math.Round(from.G + (to.G - from.G) * amount),
                (int)Math.Round(from.B + (to.B - from.B) * amount));
        }

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

        // Fonts created only for a paint pass are caller-owned GDI objects. Keep the familiar
        // TextRenderer signature but dispose the temporary font deterministically after drawing.
        public static void DrawTextWithOwnedFont(Graphics graphics, string text, Font font, Rectangle bounds, Color color)
        {
            using (font) TextRenderer.DrawText(graphics, text, font, bounds, color);
        }

        public static void DrawTextWithOwnedFont(Graphics graphics, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags)
        {
            using (font) TextRenderer.DrawText(graphics, text, font, bounds, color, flags);
        }
    }

    internal sealed class ChildSceneLayout : TableLayoutPanel
    {
        public ChildSceneLayout()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = ChildVisualTheme.Cream;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var bounds = ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new LinearGradientBrush(bounds,
                Color.FromArgb(250, 252, 248), Color.FromArgb(239, 247, 250), 32f))
                e.Graphics.FillRectangle(bg, bounds);

            using (var sky = new SolidBrush(Color.FromArgb(70, ChildVisualTheme.Sky)))
                e.Graphics.FillEllipse(sky, Width - 245, -105, 300, 240);
            using (var peach = new SolidBrush(Color.FromArgb(55, ChildVisualTheme.Peach)))
                e.Graphics.FillEllipse(peach, Width - 120, 65, 170, 155);
            using (var mint = new SolidBrush(Color.FromArgb(62, ChildVisualTheme.Mint)))
                e.Graphics.FillEllipse(mint, -110, Height - 155, 250, 220);
        }
    }

    internal sealed class ChildCard : Panel
    {
        public Color CardColor { get; set; } = ChildVisualTheme.Card;
        public Color BorderColor { get; set; } = ChildVisualTheme.Line;
        public int Radius { get; set; } = 22;
        public bool ShowShadow { get; set; } = true;

        public ChildCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(2, 2, Math.Max(1, Width - 6), Math.Max(1, Height - 7));
            if (ShowShadow && rect.Width > 8 && rect.Height > 8)
            {
                var shadowRect = new Rectangle(rect.X + 1, rect.Y + 3, rect.Width, rect.Height);
                using (var shadowPath = ChildVisualTheme.RoundedRect(shadowRect, Radius))
                using (var shadow = new SolidBrush(ChildVisualTheme.Shadow))
                    e.Graphics.FillPath(shadow, shadowPath);
            }
            using (var path = ChildVisualTheme.RoundedRect(rect, Radius))
            using (var brush = new LinearGradientBrush(rect,
                ChildVisualTheme.Blend(CardColor, Color.White, 0.34f), CardColor, 90f))
            using (var pen = new Pen(BorderColor, 1.15f))
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
        private string _iconAssetPath;
        private int _iconSize = 26;

        public Color FillColor { get; set; } = ChildVisualTheme.MintStrong;
        public Color HoverColor { get; set; } = Color.FromArgb(92, 155, 104);
        public Color PressedColor { get; set; } = Color.FromArgb(78, 138, 91);
        public Color DisabledFillColor { get; set; } = Color.FromArgb(205, 210, 204);
        public Color TextColor { get; set; } = Color.White;
        public Color DisabledTextColor { get; set; } = Color.FromArgb(120, 126, 121);
        public Color BorderColor { get; set; } = Color.Empty;
        public Color ShadowColor { get; set; } = Color.Empty;
        public float BorderThickness { get; set; } = 1f;
        public bool ShowDepth { get; set; } = true;
        public int Depth { get; set; } = 4;
        public int Radius { get; set; } = 18;
        public string BadgeText { get; set; }
        public string IconAssetPath
        {
            get { return _iconAssetPath; }
            set
            {
                if (string.Equals(_iconAssetPath, value, StringComparison.OrdinalIgnoreCase)) return;
                _iconAssetPath = value;
                Invalidate();
            }
        }
        public int IconSize
        {
            get { return _iconSize; }
            set { _iconSize = Math.Max(12, Math.Min(40, value)); Invalidate(); }
        }

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
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { _pressed = false; Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { _pressed = false; Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var fill = Enabled ? (_pressed ? PressedColor : (_hover ? HoverColor : FillColor)) : DisabledFillColor;
            var depth = ShowDepth ? Math.Max(0, Math.Min(7, Depth)) : 0;
            var pressOffset = Enabled && _pressed ? Math.Min(3, depth) : 0;
            var surfaceHeight = Math.Max(1, Height - depth - 3);
            var rect = new Rectangle(2, 1 + pressOffset, Math.Max(1, Width - 4), surfaceHeight);

            if (depth > 0 && rect.Width > 8 && rect.Height > 8)
            {
                var shadowRect = new Rectangle(rect.X, 1 + depth, rect.Width, rect.Height);
                var shadowBase = ShadowColor.IsEmpty ? ChildVisualTheme.Blend(fill, ChildVisualTheme.Ink, Enabled ? 0.30f : 0.18f) : ShadowColor;
                var shadowFill = Color.FromArgb(Enabled ? 82 : 42, shadowBase.R, shadowBase.G, shadowBase.B);
                using (var shadowPath = ChildVisualTheme.RoundedRect(shadowRect, Radius))
                using (var shadow = new SolidBrush(shadowFill))
                    pevent.Graphics.FillPath(shadow, shadowPath);
            }

            var actualBorder = BorderColor.IsEmpty ? ChildVisualTheme.Blend(fill, ChildVisualTheme.Ink, Enabled ? 0.16f : 0.09f) : BorderColor;
            using (var path = ChildVisualTheme.RoundedRect(rect, Radius))
            using (var brush = new LinearGradientBrush(rect,
                ChildVisualTheme.Blend(fill, Color.White, Enabled ? (_hover && !_pressed ? 0.24f : 0.18f) : 0.08f), fill, 90f))
            using (var outline = new Pen(actualBorder, Math.Max(1f, BorderThickness)))
            {
                pevent.Graphics.FillPath(brush, path);
                pevent.Graphics.DrawPath(outline, path);
            }

            if (Focused && ShowFocusCues)
            {
                var focusRect = Rectangle.Inflate(rect, -3, -3);
                using (var focusPath = ChildVisualTheme.RoundedRect(focusRect, Math.Max(5, Radius - 3)))
                using (var focusPen = new Pen(Color.FromArgb(205, ChildVisualTheme.Sun), 2f))
                    pevent.Graphics.DrawPath(focusPen, focusPath);
            }

            var textRect = new Rectangle(
                rect.Left + Padding.Left,
                rect.Top + Padding.Top,
                Math.Max(1, rect.Width - Padding.Horizontal),
                Math.Max(1, rect.Height - Padding.Vertical));
            Rectangle badge = Rectangle.Empty;
            if (!string.IsNullOrWhiteSpace(BadgeText))
            {
                badge = new Rectangle(rect.Right - 45, rect.Top + 7, 34, Math.Max(24, rect.Height - 14));
                textRect.Width = Math.Max(1, badge.Left - 8 - textRect.Left);
            }
            if (!string.IsNullOrWhiteSpace(IconAssetPath) && GameAssetLibrary.HasAsset(IconAssetPath))
            {
                var iconSize = Math.Min(IconSize, Math.Max(12, rect.Height - 14));
                var iconRect = new Rectangle(textRect.Left + 4, rect.Top + (rect.Height - iconSize) / 2, iconSize, iconSize);
                if (GameAssetLibrary.DrawContain(pevent.Graphics, IconAssetPath, iconRect))
                {
                    var textLeft = iconRect.Right + 8;
                    textRect.Width = Math.Max(1, textRect.Right - textLeft);
                    textRect.X = textLeft;
                }
            }
            TextRenderer.DrawText(pevent.Graphics, Text, Font, textRect,
                Enabled ? TextColor : DisabledTextColor,
                AlignmentFlags(TextAlign) | TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

            if (!string.IsNullOrWhiteSpace(BadgeText))
            {
                using (var b = new SolidBrush(Color.FromArgb(42, 255, 255, 255)))
                using (var path = ChildVisualTheme.RoundedRect(badge, 12))
                    pevent.Graphics.FillPath(b, path);
                ChildVisualTheme.DrawTextWithOwnedFont(pevent.Graphics, BadgeText, ChildVisualTheme.Font(9.5f, FontStyle.Bold), badge,
                    Enabled ? TextColor : DisabledTextColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private static TextFormatFlags AlignmentFlags(ContentAlignment alignment)
        {
            TextFormatFlags flags;
            switch (alignment)
            {
                case ContentAlignment.TopLeft: flags = TextFormatFlags.Left | TextFormatFlags.Top; break;
                case ContentAlignment.TopCenter: flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.Top; break;
                case ContentAlignment.TopRight: flags = TextFormatFlags.Right | TextFormatFlags.Top; break;
                case ContentAlignment.MiddleLeft: flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter; break;
                case ContentAlignment.MiddleRight: flags = TextFormatFlags.Right | TextFormatFlags.VerticalCenter; break;
                case ContentAlignment.BottomLeft: flags = TextFormatFlags.Left | TextFormatFlags.Bottom; break;
                case ContentAlignment.BottomCenter: flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom; break;
                case ContentAlignment.BottomRight: flags = TextFormatFlags.Right | TextFormatFlags.Bottom; break;
                default: flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter; break;
            }
            return flags;
        }
    }

    internal sealed class ChildAssetIconControl : Control
    {
        private string _assetPath;
        private int _inset = 4;

        public string AssetPath
        {
            get { return _assetPath; }
            set
            {
                if (string.Equals(_assetPath, value, StringComparison.OrdinalIgnoreCase)) return;
                _assetPath = value;
                Invalidate();
            }
        }

        public int Inset
        {
            get { return _inset; }
            set { _inset = Math.Max(0, Math.Min(12, value)); Invalidate(); }
        }

        public ChildAssetIconControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width <= 0 || Height <= 0 || string.IsNullOrWhiteSpace(AssetPath)) return;
            var bounds = Rectangle.Inflate(ClientRectangle, -Inset, -Inset);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            GameAssetLibrary.DrawContain(e.Graphics, AssetPath, bounds);
        }
    }

    internal sealed class AnswerChoiceButton : ChildActionButton
    {
        public enum ChoiceVisualState { Idle, Correct, Incorrect, Muted }
        private ChoiceVisualState _visualState;

        public Color IdleFillColor { get; set; } = Color.White;
        public Color IdleHoverColor { get; set; } = Color.FromArgb(244, 249, 240);
        public Color IdlePressedColor { get; set; } = Color.FromArgb(232, 244, 226);

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
                    FillColor = IdleFillColor;
                    HoverColor = IdleHoverColor;
                    PressedColor = IdlePressedColor;
                    TextColor = ChildVisualTheme.Ink;
                    DisabledFillColor = ChildVisualTheme.Blend(IdleFillColor, Color.FromArgb(236, 239, 235), 0.72f);
                    DisabledTextColor = ChildVisualTheme.MutedInk;
                    break;
            }
        }
    }

    internal sealed class ProgressStrip : Control
    {
        private int _value;
        private int _maximum = 8;
        public int Value { get { return _value; } set { _value = Math.Max(0, Math.Min(value, Maximum)); UpdateAccessibleDescription(); Invalidate(); } }
        public int Maximum { get { return _maximum; } set { _maximum = Math.Max(1, value); _value = Math.Min(_value, _maximum); UpdateAccessibleDescription(); Invalidate(); } }

        public ProgressStrip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 14;
            AccessibleName = "Tiến độ buổi học";
            UpdateAccessibleDescription();
        }

        private void UpdateAccessibleDescription()
        {
            AccessibleDescription = "Đã hoàn thành " + Value + " trên " + Maximum + " chặng học.";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var trackHeight = Math.Max(8, Height - 8);
            var track = new Rectangle(1, Math.Max(2, (Height - trackHeight) / 2), Math.Max(2, Width - 2), trackHeight);
            using (var path = ChildVisualTheme.RoundedRect(track, track.Height / 2))
            using (var b = new SolidBrush(Color.FromArgb(224, 231, 222))) e.Graphics.FillPath(b, path);

            var filledWidth = (int)Math.Round(track.Width * (Value / (double)Maximum));
            if (filledWidth >= 4)
            {
                var fill = new Rectangle(track.X, track.Y, Math.Min(track.Width, filledWidth), track.Height);
                using (var path = ChildVisualTheme.RoundedRect(fill, fill.Height / 2))
                using (var b = new LinearGradientBrush(fill, ChildVisualTheme.Blend(ChildVisualTheme.MintStrong, Color.White, 0.12f), ChildVisualTheme.MintStrong, 0f))
                    e.Graphics.FillPath(b, path);
            }

            if (Maximum <= 12 && track.Width >= Maximum * 22)
            {
                for (var i = 1; i < Maximum; i++)
                {
                    var x = track.Left + (int)Math.Round(track.Width * (i / (double)Maximum));
                    var passed = i <= Value;
                    var dot = new Rectangle(x - 3, track.Top + track.Height / 2 - 3, 6, 6);
                    using (var b = new SolidBrush(passed ? Color.FromArgb(232, 249, 236) : Color.FromArgb(249, 251, 247)))
                        e.Graphics.FillEllipse(b, dot);
                }
            }
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
            if (_question.Representation == "estimate_dots")
                DrawEstimateDots(e.Graphics);
            else if (_question.Representation == "estimate_length")
                DrawEstimateLength(e.Graphics);
            else if (_question.Representation == "base10_count")
                DrawBase10Count(e.Graphics);
            else if (_question.Representation == "number_word_card")
                DrawNumberWordCard(e.Graphics);
            else if (_question.Representation == "ruler_cm")
                DrawRulerCm(e.Graphics);
            else if (_question.Representation == "common_scale")
                DrawCommonScale(e.Graphics);
            else if (_question.Representation == "measurement_calc")
                DrawMeasurementCalc(e.Graphics);
            else if (_question.Representation == "measurement_word_model")
                DrawWordProblemModel(e.Graphics);
            else if (_question.Representation == "classify_count")
                DrawClassifyCount(e.Graphics);
            else if (_question.Representation == "balance_scale")
                DrawBalanceScale(e.Graphics);
            else if (_question.Representation == "mass_kg_scale")
                DrawMassKgScale(e.Graphics);
            else if (_question.Representation == "liter_measure")
                DrawLiterMeasure(e.Graphics);
            else if (_question.Representation == "unit_relation" || _question.Representation == "time_relation")
                DrawUnitRelation(e.Graphics);
            else if (_question.Representation == "calendar")
                DrawCalendar(e.Graphics);
            else if (_question.Representation == "hundreds_blocks")
                DrawHundredsBlocks(e.Graphics);
            else if (_question.Representation == "number_line_fill")
                DrawNumberLineFill(e.Graphics);
            else if (_question.Representation == "number_cards")
                DrawNumberCards(e.Graphics);
            else if (_question.Representation == "two_step_strip")
                DrawTwoStepStrip(e.Graphics);
            else if (_question.Representation == "round_number_chunks")
                DrawRoundNumberChunks(e.Graphics);
            else if (_question.Representation == "equation_components")
                DrawEquationComponents(e.Graphics);
            else if (_question.Representation == "operation_model")
                DrawWordProblemModel(e.Graphics);
            else if (_question.TemplateId != null && _question.TemplateId.StartsWith("word_problem_", StringComparison.Ordinal))
                DrawWordProblemModel(e.Graphics);
            else if (_question.TemplateId == "clock_read_minute_hand_3_or_6")
                DrawClock(e.Graphics);
            else if (_question.TemplateId != null && _question.TemplateId.StartsWith("geometry_identify_basic__", StringComparison.Ordinal))
                DrawGeometry(e.Graphics);
            else if (_question.TemplateId != null && _question.TemplateId.StartsWith("pictograph_animals_legend1__", StringComparison.Ordinal))
                DrawPictograph(e.Graphics);
            else if (_question.TemplateId != null && _question.TemplateId.StartsWith("possible_certain_impossible_die__", StringComparison.Ordinal))
                DrawDieOutcomes(e.Graphics);
            else if (_question.TemplateId == "place_value_decompose_3digit" || _question.TemplateId == "expanded_form_3digit")
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

        private void DrawEstimateDots(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            int count, nearest;
            if (parts.Length != 3 || parts[0] != "estimatedots" || !int.TryParse(parts[1], out count) || !int.TryParse(parts[2], out nearest) || count < 1) return;
            var stage = new Rectangle(Width / 2 - 225, 8, 450, Math.Max(90, Height - 16));
            using (var path = ChildVisualTheme.RoundedRect(stage, 14))
            using (var fill = new SolidBrush(Color.FromArgb(248, 250, 244)))
            using (var border = new Pen(Color.FromArgb(214, 222, 207), 1f)) { g.FillPath(fill, path); g.DrawPath(border, path); }
            using (var dot = new SolidBrush(Color.FromArgb(122, 158, 145)))
            {
                if (_hintLevel <= 0)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var x = stage.Left + 16 + ((i * 47 + i * i * 7) % Math.Max(40, stage.Width - 38));
                        var y = stage.Top + 14 + ((i * 31 + i * i * 11) % Math.Max(30, stage.Height - 40));
                        g.FillEllipse(dot, x, y, 6, 6);
                    }
                }
                else
                {
                    var gapX = Math.Max(12, (stage.Width - 40) / 10);
                    var gapY = 14;
                    for (var i = 0; i < count; i++)
                    {
                        var col = i % 10; var row = i / 10;
                        var x = stage.Left + 20 + col * gapX;
                        var y = stage.Top + 18 + row * gapY;
                        g.FillEllipse(dot, x, y, 6, 6);
                    }
                    DrawCentered(g, "Mỗi hàng khoảng 10", new Rectangle(stage.Left, stage.Bottom - 22, stage.Width, 17), ChildVisualTheme.MutedInk, 7.8f);
                }
            }
        }

        private void DrawEstimateLength(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            int reference, target, jitter;
            if (parts.Length != 4 || parts[0] != "estimatelength" || !int.TryParse(parts[1], out reference) || !int.TryParse(parts[2], out target) ||
                !int.TryParse(parts[3], out jitter) || reference <= 0 || target <= 0) return;
            var refPx = 64;
            var targetPx = (int)Math.Round(refPx * (target / (double)reference) * (100 + jitter) / 100.0);
            targetPx = Math.Max(40, Math.Min(Width - 80, targetPx));
            var left = 40;
            var refY = 28;
            using (var referenceBrush = new SolidBrush(Color.FromArgb(214, 232, 209)))
            using (var targetBrush = new SolidBrush(Color.FromArgb(239, 184, 121)))
            {
                g.FillRectangle(referenceBrush, left, refY, refPx, 14);
                g.FillRectangle(targetBrush, left, refY + 50, targetPx, 16);
            }
            DrawCentered(g, "thanh mẫu 10 cm", new Rectangle(left - 10, refY - 22, refPx + 80, 18), ChildVisualTheme.MutedInk, 7.8f);
            DrawCentered(g, "đoạn cần ước lượng", new Rectangle(left - 10, refY + 66, Math.Min(220, targetPx + 50), 18), ChildVisualTheme.MutedInk, 7.8f);
            if (_hintLevel >= 2)
            {
                using (var pen = new Pen(Color.FromArgb(172, 188, 166), 1f))
                {
                    pen.DashStyle = DashStyle.Dash;
                    for (var x = left + refPx; x < left + targetPx; x += refPx)
                        g.DrawLine(pen, x, refY + 44, x, refY + 72);
                }
            }
        }

        private void DrawBase10Count(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            int h, t, o;
            if (parts.Length != 4 || parts[0] != "base10count" || !int.TryParse(parts[1], out h) || !int.TryParse(parts[2], out t) || !int.TryParse(parts[3], out o)) return;
            var stage = new Rectangle(Width / 2 - 235, 8, 470, Math.Max(92, Height - 16));
            using (var path = ChildVisualTheme.RoundedRect(stage, 14))
            using (var fill = new SolidBrush(Color.FromArgb(248, 250, 244)))
            using (var border = new Pen(Color.FromArgb(213, 221, 207), 1.2f))
            { g.FillPath(fill, path); g.DrawPath(border, path); }
            var colW = stage.Width / 3;
            var labels = new[] { "TRĂM", "CHỤC", "ĐƠN VỊ" };
            for (var c = 0; c < 3; c++)
                DrawCentered(g, labels[c], new Rectangle(stage.Left + c * colW, stage.Top + 5, colW, 18), ChildVisualTheme.MutedInk, 7.8f);
            using (var pen = new Pen(Color.FromArgb(134, 158, 147), 1.4f))
            using (var fill = new SolidBrush(Color.FromArgb(220, 237, 213)))
            {
                for (var i = 0; i < h; i++)
                {
                    var x = stage.Left + 18 + (i % 3) * 31;
                    var y = stage.Top + 30 + (i / 3) * 31;
                    g.FillRectangle(fill, x, y, 23, 23);
                    g.DrawRectangle(pen, x, y, 23, 23);
                    for (var k = 1; k < 5; k++)
                    { g.DrawLine(pen, x + k * 23 / 5, y, x + k * 23 / 5, y + 23); g.DrawLine(pen, x, y + k * 23 / 5, x + 23, y + k * 23 / 5); }
                }
                for (var i = 0; i < t; i++)
                {
                    var x = stage.Left + colW + 34 + (i % 5) * 17;
                    var y = stage.Top + 31 + (i / 5) * 50;
                    g.FillRectangle(fill, x, y, 10, 43);
                    g.DrawRectangle(pen, x, y, 10, 43);
                    for (var k = 1; k < 5; k++) g.DrawLine(pen, x, y + k * 43 / 5, x + 10, y + k * 43 / 5);
                }
                for (var i = 0; i < o; i++)
                {
                    var x = stage.Left + 2 * colW + 35 + (i % 3) * 28;
                    var y = stage.Top + 36 + (i / 3) * 28;
                    g.FillEllipse(fill, x, y, 15, 15);
                    g.DrawEllipse(pen, x, y, 15, 15);
                }
            }
            if (_hintLevel >= 1)
                DrawCentered(g, h + " trăm · " + t + " chục · " + o + " đơn vị", new Rectangle(stage.Left, stage.Bottom - 23, stage.Width, 18), ChildVisualTheme.PeachStrong, 8.2f);
        }

        private void DrawNumberWordCard(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            if (parts.Length < 2 || parts[0] != "numberword") return;
            var stage = new Rectangle(Width / 2 - 210, Height / 2 - 47, 420, 92);
            using (var path = ChildVisualTheme.RoundedRect(stage, 18))
            using (var fill = new SolidBrush(Color.FromArgb(246, 249, 241)))
            using (var border = new Pen(Color.FromArgb(191, 207, 183), 1.2f))
            { g.FillPath(fill, path); g.DrawPath(border, path); }
            if (parts[1] == "read" && parts.Length >= 3)
            {
                DrawCentered(g, parts[2], new Rectangle(stage.Left, stage.Top + 8, stage.Width, 46), ChildVisualTheme.Ink, 21f);
                DrawCentered(g, "ĐỌC SỐ", new Rectangle(stage.Left, stage.Bottom - 29, stage.Width, 18), ChildVisualTheme.MutedInk, 8f);
            }
            else
            {
                DrawCentered(g, "TỪ CÁCH ĐỌC → VIẾT SỐ", new Rectangle(stage.Left, stage.Top + 8, stage.Width, 22), ChildVisualTheme.Ink, 9f);
                var colW = stage.Width / 3;
                var labels = new[] { "TRĂM", "CHỤC", "ĐƠN VỊ" };
                for (var i = 0; i < 3; i++)
                {
                    var rect = new Rectangle(stage.Left + i * colW + 12, stage.Top + 37, colW - 24, 36);
                    using (var p = ChildVisualTheme.RoundedRect(rect, 10))
                    using (var b = new SolidBrush(Color.White))
                    using (var border = new Pen(Color.FromArgb(211, 218, 205), 1f)) { g.FillPath(b, p); g.DrawPath(border, p); }
                    DrawCentered(g, _hintLevel >= 2 ? labels[i] : "?", rect, _hintLevel >= 2 ? ChildVisualTheme.PeachStrong : ChildVisualTheme.Ink, 9f);
                }
            }
        }

        private void DrawRulerCm(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            int start, end;
            if (parts.Length != 3 || parts[0] != "rulercm" || !int.TryParse(parts[1], out start) || !int.TryParse(parts[2], out end) || end <= start) return;
            var max = Math.Max(15, end);
            var left = Math.Max(34, Width / 12);
            var right = Width - left;
            var rulerY = Height / 2 + 14;
            using (var body = new SolidBrush(Color.FromArgb(246, 235, 194)))
                g.FillRectangle(body, left, rulerY, right - left, 30);
            using (var basePen = new Pen(Color.FromArgb(151, 135, 92), 1.5f))
                g.DrawRectangle(basePen, left, rulerY, right - left, 30);
            for (var i = 0; i <= max; i++)
            {
                var x = left + (right - left) * i / max;
                var tickH = i % 5 == 0 ? 17 : 11;
                using (var pen = new Pen(Color.FromArgb(116, 108, 81), 1f)) g.DrawLine(pen, x, rulerY, x, rulerY + tickH);
                if (i % 5 == 0 || i == start || i == end)
                    DrawCentered(g, i.ToString(), new Rectangle(x - 18, rulerY + 13, 36, 16), ChildVisualTheme.Ink, 7f);
            }
            var xA = left + (right - left) * start / max;
            var xB = left + (right - left) * end / max;
            var segY = rulerY - 18;
            using (var pen = new Pen(ChildVisualTheme.PeachStrong, 3f)) g.DrawLine(pen, xA, segY, xB, segY);
            using (var dot = new SolidBrush(ChildVisualTheme.PeachStrong))
            { g.FillEllipse(dot, xA - 4, segY - 4, 8, 8); g.FillEllipse(dot, xB - 4, segY - 4, 8, 8); }
            DrawCentered(g, "A", new Rectangle(xA - 18, segY - 23, 36, 18), ChildVisualTheme.Ink, 8f);
            DrawCentered(g, "B", new Rectangle(xB - 18, segY - 23, 36, 18), ChildVisualTheme.Ink, 8f);
            if (_hintLevel >= 2)
                DrawCentered(g, end + " − " + start + " = ? cm", new Rectangle(0, 3, Width, 18), ChildVisualTheme.PeachStrong, 8.5f);
        }

        private void DrawCommonScale(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            int min, max, step, value;
            if (parts.Length != 5 || parts[0] != "commonscale" || !int.TryParse(parts[1], out min) || !int.TryParse(parts[2], out max) ||
                !int.TryParse(parts[3], out step) || !int.TryParse(parts[4], out value) || step <= 0 || max <= min) return;
            var stage = new Rectangle(Math.Max(18, Width / 9), 9, Math.Max(200, Width * 7 / 9), Math.Max(72, Height - 20));
            using (var path = ChildVisualTheme.RoundedRect(stage, 16))
            using (var fill = new SolidBrush(Color.FromArgb(246, 249, 242)))
            using (var border = new Pen(Color.FromArgb(214, 224, 207), 1f))
            { g.FillPath(fill, path); g.DrawPath(border, path); }
            var left = stage.Left + 28; var right = stage.Right - 28; var y = stage.Top + stage.Height / 2;
            using (var pen = new Pen(Color.FromArgb(107, 126, 121), 2f)) g.DrawLine(pen, left, y, right, y);
            var tickCount = (max - min) / step;
            for (var i = 0; i <= tickCount; i++)
            {
                var tickValue = min + i * step;
                var x = left + (right - left) * i / tickCount;
                using (var pen = new Pen(Color.FromArgb(107, 126, 121), 1.5f)) g.DrawLine(pen, x, y - 9, x, y + 9);
                if (i == 0 || i == tickCount || i % 2 == 0)
                    DrawCentered(g, tickValue.ToString(), new Rectangle(x - 28, y + 10, 56, 18), ChildVisualTheme.MutedInk, 7.5f);
            }
            var valueIndex = (value - min) / step;
            var valueX = left + (right - left) * valueIndex / tickCount;
            using (var pointer = new SolidBrush(ChildVisualTheme.PeachStrong))
            {
                var pts = new[] { new Point(valueX, y - 12), new Point(valueX - 9, y - 30), new Point(valueX + 9, y - 30) };
                g.FillPolygon(pointer, pts);
            }
            if (_hintLevel >= 2)
                DrawCentered(g, "+ " + step + " mỗi vạch", new Rectangle(0, 4, Width, 18), ChildVisualTheme.PeachStrong, 8.3f);
        }

        private void DrawMeasurementCalc(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            if (parts.Length < 4 || parts[0] != "unitcalc") return;
            string leftText, rightUnit, answerText;
            if (parts[1] == "convert_m_dm") { leftText = parts[2] + " m"; rightUnit = "dm"; answerText = parts[3] + " dm"; }
            else if (parts[1] == "convert_km_m") { leftText = parts[2] + " km"; rightUnit = "m"; answerText = parts[3] + " m"; }
            else if ((parts[1] == "add" || parts[1] == "sub") && parts.Length == 5)
            {
                var op = parts[1] == "add" ? "+" : "−";
                leftText = parts[2] + " " + parts[4] + " " + op + " " + parts[3] + " " + parts[4];
                rightUnit = parts[4];
                int a, b;
                if (!int.TryParse(parts[2], out a) || !int.TryParse(parts[3], out b)) return;
                answerText = (parts[1] == "add" ? a + b : a - b) + " " + parts[4];
            }
            else return;
            var left = new Rectangle(Width / 2 - 235, Height / 2 - 28, 200, 54);
            var right = new Rectangle(Width / 2 + 35, Height / 2 - 28, 200, 54);
            foreach (var rect in new[] { left, right })
            {
                using (var path = ChildVisualTheme.RoundedRect(rect, 14))
                using (var fill = new SolidBrush(Color.FromArgb(243, 248, 238)))
                using (var border = new Pen(Color.FromArgb(185, 202, 177), 1.2f))
                { g.FillPath(fill, path); g.DrawPath(border, path); }
            }
            DrawCentered(g, leftText, left, ChildVisualTheme.Ink, 9.5f);
            DrawCentered(g, _hintLevel >= 2 ? answerText : "? " + rightUnit, right, _hintLevel >= 2 ? ChildVisualTheme.PeachStrong : ChildVisualTheme.Ink, 10f);
            DrawCentered(g, "=", new Rectangle(Width / 2 - 30, Height / 2 - 18, 60, 36), ChildVisualTheme.PeachStrong, 13f);
        }

        private void DrawClassifyCount(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            if (parts.Length != 5 || parts[0] != "classify") return;
            int circle, square, triangle;
            if (!TryReadNamedInt(parts[1], "circle", out circle) || !TryReadNamedInt(parts[2], "square", out square) || !TryReadNamedInt(parts[3], "triangle", out triangle)) return;
            var counts = new[] { circle, square, triangle };
            var startX = Width / 2 - 210;
            var groupW = 140;
            for (var group = 0; group < 3; group++)
            {
                var rect = new Rectangle(startX + group * groupW, 10, groupW - 12, Math.Max(75, Height - 20));
                using (var path = ChildVisualTheme.RoundedRect(rect, 12))
                using (var fill = new SolidBrush(Color.FromArgb(247, 249, 242)))
                using (var border = new Pen(Color.FromArgb(214, 222, 207), 1f))
                { g.FillPath(fill, path); g.DrawPath(border, path); }
                for (var i = 0; i < counts[group]; i++)
                {
                    var col = i % 3; var row = i / 3;
                    var x = rect.Left + 18 + col * 31; var y = rect.Top + 18 + row * 30;
                    using (var pen = new Pen(Color.FromArgb(111, 135, 128), 2f))
                    {
                        if (group == 0) g.DrawEllipse(pen, x, y, 18, 18);
                        else if (group == 1) g.DrawRectangle(pen, x, y, 18, 18);
                        else g.DrawPolygon(pen, new[] { new Point(x + 9, y), new Point(x, y + 18), new Point(x + 18, y + 18) });
                    }
                }
            }
            if (_hintLevel >= 1) DrawCentered(g, "Phân loại trước · đếm sau", new Rectangle(0, Height - 20, Width, 17), ChildVisualTheme.MutedInk, 8f);
        }

        private static bool TryReadNamedInt(string part, string name, out int value)
        {
            value = 0;
            var prefix = name + "=";
            return part != null && part.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(part.Substring(prefix.Length), out value);
        }

        private void DrawBalanceScale(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            int leftWeight, rightWeight;
            if (parts.Length != 4 || parts[0] != "balance" || !int.TryParse(parts[1], out leftWeight) || !int.TryParse(parts[2], out rightWeight) || leftWeight == rightWeight) return;
            var centerX = Width / 2;
            var pivotY = Height / 2 - 4;
            var delta = leftWeight > rightWeight ? 12 : -12;
            var leftY = pivotY + delta;
            var rightY = pivotY - delta;
            using (var stand = new Pen(Color.FromArgb(105, 122, 117), 4f))
            {
                g.DrawLine(stand, centerX, pivotY - 8, centerX, Height - 18);
                g.DrawLine(stand, centerX - 42, Height - 18, centerX + 42, Height - 18);
                g.DrawLine(stand, centerX - 170, leftY, centerX + 170, rightY);
            }
            using (var rope = new Pen(Color.FromArgb(128, 139, 132), 1.5f))
            using (var pan = new SolidBrush(Color.FromArgb(239, 229, 198)))
            using (var border = new Pen(Color.FromArgb(164, 148, 112), 1.2f))
            {
                var leftX = centerX - 145; var rightX = centerX + 145;
                g.DrawLine(rope, leftX, leftY, leftX, leftY + 26);
                g.DrawLine(rope, rightX, rightY, rightX, rightY + 26);
                var lp = new Rectangle(leftX - 42, leftY + 25, 84, 22);
                var rp = new Rectangle(rightX - 42, rightY + 25, 84, 22);
                g.FillEllipse(pan, lp); g.DrawEllipse(border, lp);
                g.FillEllipse(pan, rp); g.DrawEllipse(border, rp);
                using (var obj = new SolidBrush(Color.FromArgb(218, 233, 213)))
                {
                    g.FillEllipse(obj, leftX - 17, leftY + 8, 34, 27);
                    g.FillEllipse(obj, rightX - 17, rightY + 8, 34, 27);
                }
            }
            DrawCentered(g, "trái", new Rectangle(centerX - 215, Height - 24, 140, 18), ChildVisualTheme.MutedInk, 8f);
            DrawCentered(g, "phải", new Rectangle(centerX + 75, Height - 24, 140, 18), ChildVisualTheme.MutedInk, 8f);
        }

        private void DrawMassKgScale(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            int kg;
            if (parts.Length != 2 || parts[0] != "masskg" || !int.TryParse(parts[1], out kg)) return;
            var body = new Rectangle(Width / 2 - 115, 12, 230, Math.Max(72, Height - 24));
            using (var path = ChildVisualTheme.RoundedRect(body, 22))
            using (var fill = new SolidBrush(Color.FromArgb(239, 246, 235)))
            using (var border = new Pen(Color.FromArgb(177, 199, 169), 1.4f))
            { g.FillPath(fill, path); g.DrawPath(border, path); }
            var dial = new Rectangle(body.Left + 52, body.Top + 14, body.Width - 104, 50);
            using (var path = ChildVisualTheme.RoundedRect(dial, 12))
            using (var fill = new SolidBrush(Color.White))
            using (var border = new Pen(Color.FromArgb(165, 179, 159), 1.2f))
            { g.FillPath(fill, path); g.DrawPath(border, path); }
            DrawCentered(g, kg + " kg", dial, ChildVisualTheme.Ink, 13f);
            using (var tray = new SolidBrush(Color.FromArgb(235, 220, 189)))
                g.FillEllipse(tray, body.Left + 34, body.Bottom - 27, body.Width - 68, 16);
        }

        private void DrawLiterMeasure(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            int liters;
            if (parts.Length != 2 || parts[0] != "liter" || !int.TryParse(parts[1], out liters) || liters < 1 || liters > 10) return;
            var vessel = new Rectangle(Width / 2 - 82, 9, 164, Math.Max(78, Height - 18));
            using (var fill = new SolidBrush(Color.FromArgb(249, 252, 249)))
            using (var border = new Pen(Color.FromArgb(128, 151, 146), 2f))
            { g.FillRectangle(fill, vessel); g.DrawRectangle(border, vessel); }
            var inner = Rectangle.Inflate(vessel, -12, -10);
            var levelY = inner.Bottom - (int)Math.Round(inner.Height * (liters / 10.0));
            using (var water = new SolidBrush(Color.FromArgb(207, 232, 239)))
                g.FillRectangle(water, inner.Left, levelY, inner.Width, Math.Max(2, inner.Bottom - levelY));
            for (var i = 1; i <= 10; i++)
            {
                var y = inner.Bottom - inner.Height * i / 10;
                using (var pen = new Pen(Color.FromArgb(122, 145, 140), i == liters ? 2f : 1f))
                    g.DrawLine(pen, vessel.Right - 32, y, vessel.Right - 10, y);
                if (i == liters) DrawCentered(g, i + " L", new Rectangle(vessel.Right + 8, y - 10, 58, 20), ChildVisualTheme.PeachStrong, 8.5f);
            }
        }

        private void DrawUnitRelation(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            if (parts.Length != 5 || (parts[0] != "unitrelation" && parts[0] != "timerelation")) return;
            var left = new Rectangle(Width / 2 - 220, Height / 2 - 28, 160, 54);
            var right = new Rectangle(Width / 2 + 60, Height / 2 - 28, 160, 54);
            foreach (var rect in new[] { left, right })
            {
                using (var path = ChildVisualTheme.RoundedRect(rect, 14))
                using (var fill = new SolidBrush(Color.FromArgb(243, 248, 238)))
                using (var border = new Pen(Color.FromArgb(185, 202, 177), 1.2f))
                { g.FillPath(fill, path); g.DrawPath(border, path); }
            }
            DrawCentered(g, parts[1] + " " + parts[2], left, ChildVisualTheme.Ink, 11f);
            DrawCentered(g, _hintLevel >= 2 ? parts[3] + " " + parts[4] : "? " + parts[4], right, _hintLevel >= 2 ? ChildVisualTheme.PeachStrong : ChildVisualTheme.Ink, 11f);
            using (var pen = new Pen(ChildVisualTheme.PeachStrong, 2f))
            {
                g.DrawLine(pen, left.Right + 16, Height / 2, right.Left - 16, Height / 2);
                g.DrawLine(pen, right.Left - 25, Height / 2 - 7, right.Left - 16, Height / 2);
                g.DrawLine(pen, right.Left - 25, Height / 2 + 7, right.Left - 16, Height / 2);
            }
            if (_hintLevel >= 1)
                DrawCentered(g, parts[0] == "timerelation" ? "Quan hệ thời gian" : "Quan hệ độ dài", new Rectangle(0, Height - 22, Width, 18), ChildVisualTheme.MutedInk, 8.2f);
        }

        private void DrawCalendar(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            int month, days, highlight;
            if (parts.Length != 5 || parts[0] != "calendar" || !int.TryParse(parts[1], out month) || !int.TryParse(parts[2], out days) || !int.TryParse(parts[3], out highlight)) return;
            var stage = new Rectangle(Width / 2 - 205, 5, 410, Math.Max(88, Height - 10));
            using (var path = ChildVisualTheme.RoundedRect(stage, 14))
            using (var fill = new SolidBrush(Color.FromArgb(250, 249, 242)))
            using (var border = new Pen(Color.FromArgb(214, 217, 202), 1.2f))
            { g.FillPath(fill, path); g.DrawPath(border, path); }
            DrawCentered(g, "THÁNG " + month, new Rectangle(stage.Left, stage.Top + 3, stage.Width, 18), ChildVisualTheme.Ink, 9f);
            var gridTop = stage.Top + 24;
            var cellW = stage.Width / 7;
            var rows = 5;
            var cellH = Math.Max(12, (stage.Height - 28) / rows);
            for (var day = 1; day <= days; day++)
            {
                var index = day - 1;
                var col = index % 7; var row = index / 7;
                var rect = new Rectangle(stage.Left + col * cellW + 2, gridTop + row * cellH, cellW - 4, cellH - 2);
                if (day == highlight)
                {
                    using (var path = ChildVisualTheme.RoundedRect(rect, 7))
                    using (var fill = new SolidBrush(Color.FromArgb(244, 226, 193))) g.FillPath(fill, path);
                }
                DrawCentered(g, day.ToString(), rect, day == highlight ? ChildVisualTheme.PeachStrong : ChildVisualTheme.Ink, 7.6f);
            }
        }

        private void DrawHundredsBlocks(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            int count;
            if (parts.Length != 2 || parts[0] != "hundreds" || !int.TryParse(parts[1], out count) || count < 1 || count > 9) return;
            var stage = new Rectangle(Math.Max(16, Width / 7), 7, Math.Max(180, Width * 5 / 7), Math.Max(70, Height - 20));
            using (var path = ChildVisualTheme.RoundedRect(stage, 18))
            using (var fill = new SolidBrush(Color.FromArgb(245, 249, 239)))
            using (var border = new Pen(Color.FromArgb(215, 226, 207), 1f))
            { g.FillPath(fill, path); g.DrawPath(border, path); }
            var cell = Math.Max(30, Math.Min(48, (stage.Width - 36) / Math.Max(3, Math.Min(5, count))));
            var cols = Math.Min(5, count);
            var rows = (count + cols - 1) / cols;
            var totalW = cols * cell;
            var startX = (Width - totalW) / 2;
            var startY = Math.Max(6, (Height - rows * cell) / 2 - 2);
            for (var i = 0; i < count; i++)
            {
                var x = startX + (i % cols) * cell + 3;
                var y = startY + (i / cols) * cell + 3;
                var rect = new Rectangle(x, y, cell - 7, cell - 7);
                using (var fill = new SolidBrush(Color.FromArgb(226, 239, 218)))
                using (var border = new Pen(Color.FromArgb(132, 161, 123), 1.4f))
                { g.FillRectangle(fill, rect); g.DrawRectangle(border, rect); }
                for (var k = 1; k < 5; k++)
                {
                    var gx = rect.Left + k * rect.Width / 5;
                    var gy = rect.Top + k * rect.Height / 5;
                    using (var grid = new Pen(Color.FromArgb(196, 214, 188), 0.7f))
                    { g.DrawLine(grid, gx, rect.Top, gx, rect.Bottom); g.DrawLine(grid, rect.Left, gy, rect.Right, gy); }
                }
            }
            if (_hintLevel >= 1)
                DrawCentered(g, "Mỗi ô lớn = 100", new Rectangle(0, Height - 22, Width, 18), ChildVisualTheme.MutedInk, 8.3f);
        }

        private void DrawNumberLineFill(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            int start, step, missing, count;
            if (parts.Length != 5 || parts[0] != "numberlinefill" || !int.TryParse(parts[1], out start) ||
                !int.TryParse(parts[2], out step) || !int.TryParse(parts[3], out missing) || !int.TryParse(parts[4], out count) || count < 3) return;
            var left = Math.Max(38, Width / 10);
            var right = Width - left;
            var y = Height / 2;
            using (var pen = new Pen(Color.FromArgb(107, 126, 121), 2f)) g.DrawLine(pen, left, y, right, y);
            for (var i = 0; i < count; i++)
            {
                var x = left + (right - left) * i / (count - 1);
                using (var pen = new Pen(Color.FromArgb(107, 126, 121), 2f)) g.DrawLine(pen, x, y - 8, x, y + 8);
                var label = i == missing ? "?" : (start + i * step).ToString();
                DrawCentered(g, label, new Rectangle(x - 36, y + 10, 72, 22), i == missing ? ChildVisualTheme.PeachStrong : ChildVisualTheme.Ink, i == missing ? 11f : 9f);
            }
            if (_hintLevel >= 1)
                DrawCentered(g, "Các mốc cách đều nhau", new Rectangle(0, 3, Width, 18), ChildVisualTheme.MutedInk, 8f);
            if (_hintLevel >= 2)
                DrawCentered(g, "+ " + step + " mỗi bước", new Rectangle(0, 3, Width, 18), ChildVisualTheme.PeachStrong, 8.5f);
        }

        private void DrawNumberCards(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            if (parts.Length != 6 || parts[0] != "numbercards") return;
            var values = new int[4];
            for (var i = 0; i < 4; i++) if (!int.TryParse(parts[i + 1], out values[i])) return;
            var mode = parts[5];
            var gap = 10;
            var cardW = Math.Max(68, Math.Min(105, (Width - 70 - gap * 3) / 4));
            var totalW = cardW * 4 + gap * 3;
            var startX = (Width - totalW) / 2;
            var y = Math.Max(12, Height / 2 - 24);
            for (var i = 0; i < 4; i++)
            {
                var rect = new Rectangle(startX + i * (cardW + gap), y, cardW, 46);
                using (var path = ChildVisualTheme.RoundedRect(rect, 12))
                using (var fill = new SolidBrush(Color.FromArgb(241, 245, 235)))
                using (var border = new Pen(Color.FromArgb(190, 202, 183), 1.2f))
                { g.FillPath(fill, path); g.DrawPath(border, path); }
                DrawCentered(g, values[i].ToString(), rect, ChildVisualTheme.Ink, 11f);
            }
            if (_hintLevel >= 1)
            {
                var cue = mode == "max" ? "Tìm số lớn nhất" : mode == "min" ? "Tìm số bé nhất" : mode == "asc" ? "Bé → lớn" : "Lớn → bé";
                DrawCentered(g, cue, new Rectangle(0, Height - 22, Width, 18), ChildVisualTheme.MutedInk, 8.4f);
            }
        }

        private void DrawTwoStepStrip(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            int a, b, c, middle;
            if (parts.Length != 7 || parts[0] != "twostep" || !int.TryParse(parts[1], out a) || !int.TryParse(parts[3], out b) ||
                !int.TryParse(parts[5], out c) || !int.TryParse(parts[6], out middle)) return;
            var op1 = parts[2]; var op2 = parts[4];
            var centerY = Height / 2 - 15;
            var step1 = new Rectangle(Width / 2 - 220, centerY, 180, 36);
            var step2 = new Rectangle(Width / 2 + 40, centerY, 180, 36);
            foreach (var rect in new[] { step1, step2 })
            {
                using (var path = ChildVisualTheme.RoundedRect(rect, 12))
                using (var fill = new SolidBrush(Color.FromArgb(242, 247, 239)))
                using (var border = new Pen(Color.FromArgb(179, 197, 174), 1.2f))
                { g.FillPath(fill, path); g.DrawPath(border, path); }
            }
            DrawCentered(g, a + " " + op1 + " " + b + " = " + (_hintLevel >= 2 ? middle.ToString() : "?"), step1, ChildVisualTheme.Ink, 9.5f);
            DrawCentered(g, (_hintLevel >= 2 ? middle.ToString() : "kết quả bước 1") + " " + op2 + " " + c + " = ?", step2, ChildVisualTheme.Ink, 9f);
            using (var arrow = new Pen(ChildVisualTheme.PeachStrong, 2f))
            { g.DrawLine(arrow, step1.Right + 8, centerY + 18, step2.Left - 8, centerY + 18); }
            if (_hintLevel >= 1)
                DrawCentered(g, "Làm bước 1 trước", new Rectangle(0, Height - 22, Width, 18), ChildVisualTheme.MutedInk, 8.2f);
        }

        private void DrawRoundNumberChunks(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            int a, b, unit;
            if (parts.Length != 5 || parts[0] != "roundchunks" || !int.TryParse(parts[1], out a) || !int.TryParse(parts[3], out b) || !int.TryParse(parts[4], out unit)) return;
            var op = parts[2];
            var left = new Rectangle(Width / 2 - 220, Height / 2 - 24, 150, 46);
            var right = new Rectangle(Width / 2 + 70, Height / 2 - 24, 150, 46);
            foreach (var rect in new[] { left, right })
            {
                using (var path = ChildVisualTheme.RoundedRect(rect, 13))
                using (var fill = new SolidBrush(Color.FromArgb(235, 244, 229))) g.FillPath(fill, path);
            }
            DrawCentered(g, (a / unit) + " nhóm " + unit, left, ChildVisualTheme.Ink, 9.2f);
            DrawCentered(g, (b / unit) + " nhóm " + unit, right, ChildVisualTheme.Ink, 9.2f);
            DrawCentered(g, op, new Rectangle(Width / 2 - 28, Height / 2 - 22, 56, 42), ChildVisualTheme.PeachStrong, 14f);
            if (_hintLevel >= 1)
                DrawCentered(g, "Tính số nhóm trước", new Rectangle(0, Height - 22, Width, 18), ChildVisualTheme.MutedInk, 8.2f);
        }

        private void DrawEquationComponents(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            int a, b, result, target;
            if (parts.Length < 6 || parts[0] != "equationparts" ||
                !int.TryParse(parts[2], out a) || !int.TryParse(parts[3], out b) ||
                !int.TryParse(parts[4], out result) || !int.TryParse(parts[5], out target)) return;

            var op = parts[1] == "add" ? "+" : parts[1] == "sub" ? "−" : parts[1] == "mul" ? "×" : ":";
            var stage = new Rectangle(Math.Max(12, Width / 8), 8, Math.Max(170, Width * 3 / 4), Math.Max(62, Height - 22));
            using (var path = ChildVisualTheme.RoundedRect(stage, 18))
            using (var fill = new SolidBrush(Color.FromArgb(248, 249, 242)))
            using (var border = new Pen(Color.FromArgb(218, 225, 214), 1f))
            { g.FillPath(fill, path); g.DrawPath(border, path); }

            var slots = new[] { a.ToString(), op, b.ToString(), "=", result.ToString() };
            var tokenW = Math.Max(38, Math.Min(72, (stage.Width - 28) / 5));
            var totalW = tokenW * 5;
            var startX = stage.Left + (stage.Width - totalW) / 2;
            var y = stage.Top + 16;
            for (var i = 0; i < slots.Length; i++)
            {
                var rect = new Rectangle(startX + i * tokenW, y, tokenW, 34);
                var isTarget = (target == 0 && i == 0) || (target == 1 && i == 2) || (target == 2 && i == 4);
                if (isTarget)
                {
                    using (var path = ChildVisualTheme.RoundedRect(rect, 11))
                    using (var fill = new SolidBrush(Color.FromArgb(244, 229, 199)))
                    using (var border = new Pen(ChildVisualTheme.PeachStrong, 2f))
                    { g.FillPath(fill, path); g.DrawPath(border, path); }
                }
                DrawCentered(g, slots[i], rect, isTarget ? ChildVisualTheme.PeachStrong : ChildVisualTheme.Ink, isTarget ? 12f : 11f);
            }

            DrawCentered(g, "Số cần gọi tên", new Rectangle(stage.Left, y + 38, stage.Width, 18), ChildVisualTheme.MutedInk, 8f);
            if (_hintLevel >= 1)
                DrawCentered(g, "Nhìn vị trí của số trong phép tính.", new Rectangle(stage.Left, stage.Bottom - 23, stage.Width, 18), ChildVisualTheme.MutedInk, 8f);
            if (_hintLevel >= 2)
                DrawCentered(g, "Gọi tên theo vai trò của số, không cần tính lại.", new Rectangle(stage.Left, stage.Bottom - 23, stage.Width, 18), ChildVisualTheme.PeachStrong, 8.4f);
        }

        private void DrawWordProblemModel(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            if (parts.Length < 3) return;

            var stage = new Rectangle(Math.Max(12, Width / 7), 7, Math.Max(160, Width * 5 / 7), Math.Max(62, Height - 20));
            using (var path = ChildVisualTheme.RoundedRect(stage, 18))
            using (var fill = new SolidBrush(Color.FromArgb(249, 248, 240)))
            using (var border = new Pen(Color.FromArgb(222, 226, 215), 1f))
            { g.FillPath(fill, path); g.DrawPath(border, path); }
            using (var band = new SolidBrush(Color.FromArgb(239, 245, 237)))
                g.FillRectangle(band, stage.Left + 2, stage.Top + 2, Math.Max(8, stage.Width - 4), Math.Min(16, stage.Height - 4));

            int first, second;
            if (parts[0] == "wordbar")
            {
                if (parts.Length < 4 || !int.TryParse(parts[2], out first) || !int.TryParse(parts[3], out second)) return;
                DrawWordBar(g, stage, parts[1], first, second);
            }
            else if (parts[0] == "wordgroups")
            {
                if (!int.TryParse(parts[1], out first) || !int.TryParse(parts[2], out second)) return;
                DrawWordGroups(g, stage, first, second);
            }
            else if (parts[0] == "wordshare")
            {
                if (!int.TryParse(parts[1], out first) || !int.TryParse(parts[2], out second)) return;
                DrawWordShare(g, stage, first, second);
            }
        }

        private void DrawWordBar(Graphics g, Rectangle stage, string relation, int a, int b)
        {
            var left = stage.Left + 38;
            var right = stage.Right - 38;
            var width = Math.Max(120, right - left);
            var y = stage.Top + 23;
            var h = 28;
            using (var knownA = new SolidBrush(Color.FromArgb(220, 239, 224)))
            using (var knownB = new SolidBrush(Color.FromArgb(240, 226, 196)))
            using (var unknown = new SolidBrush(Color.FromArgb(229, 239, 247)))
            using (var outline = new Pen(Color.FromArgb(128, 142, 136), 1.5f))
            {
                if (relation == "add" || relation == "more")
                {
                    var w1 = Math.Max(70, width * 3 / 5);
                    var w2 = width - w1;
                    g.FillRectangle(knownA, left, y, w1, h); g.DrawRectangle(outline, left, y, w1, h);
                    g.FillRectangle(knownB, left + w1, y, w2, h); g.DrawRectangle(outline, left + w1, y, w2, h);
                    DrawCentered(g, a.ToString(), new Rectangle(left, y, w1, h), ChildVisualTheme.Ink, 10f);
                    DrawCentered(g, b.ToString(), new Rectangle(left + w1, y, w2, h), ChildVisualTheme.Ink, 10f);
                    DrawBracket(g, left, left + width, y + h + 8, "?");
                    if (relation == "more")
                        DrawCentered(g, "phần bằng nhau     phần nhiều hơn", new Rectangle(left, stage.Bottom - 24, width, 20), ChildVisualTheme.MutedInk, 7.8f);
                }
                else
                {
                    g.FillRectangle(unknown, left, y, width, h); g.DrawRectangle(outline, left, y, width, h);
                    var removedW = Math.Max(55, width / 3);
                    g.FillRectangle(knownB, left + width - removedW, y, removedW, h);
                    g.DrawRectangle(outline, left + width - removedW, y, removedW, h);
                    DrawCentered(g, "?", new Rectangle(left, y, width - removedW, h), ChildVisualTheme.PeachStrong, 13f);
                    DrawCentered(g, b.ToString(), new Rectangle(left + width - removedW, y, removedW, h), ChildVisualTheme.Ink, 10f);
                    DrawBracket(g, left, left + width, y - 8, a.ToString());
                    var label = relation == "less" ? "ít hơn " + b : "đã bớt " + b;
                    DrawCentered(g, label, new Rectangle(left, stage.Bottom - 24, width, 20), ChildVisualTheme.MutedInk, 8f);
                }
            }
            if (_hintLevel >= 2)
            {
                var op = (relation == "add" || relation == "more") ? "+" : "−";
                DrawCentered(g, a + " " + op + " " + b + " = ?", new Rectangle(stage.Left, stage.Bottom - 22, stage.Width, 20), ChildVisualTheme.PeachStrong, 9f);
            }
        }

        private void DrawWordGroups(Graphics g, Rectangle stage, int factor, int groups)
        {
            var cols = Math.Min(5, groups);
            var rows = (groups + cols - 1) / cols;
            var cellW = Math.Max(48, Math.Min(72, (stage.Width - 24) / cols));
            var cellH = Math.Max(34, Math.Min(43, (stage.Height - 26) / Math.Max(1, rows)));
            var totalW = cols * cellW;
            var startX = stage.Left + (stage.Width - totalW) / 2;
            var startY = stage.Top + 7;
            for (var group = 0; group < groups; group++)
            {
                var col = group % cols; var row = group / cols;
                var rect = new Rectangle(startX + col * cellW + 4, startY + row * cellH, cellW - 8, cellH - 5);
                using (var path = ChildVisualTheme.RoundedRect(rect, 10))
                using (var fill = new SolidBrush(Color.FromArgb(236, 244, 228)))
                using (var border = new Pen(Color.FromArgb(185, 202, 177), 1f))
                { g.FillPath(fill, path); g.DrawPath(border, path); }
                for (var i = 0; i < factor; i++)
                {
                    var px = rect.Left + 10 + (i % 3) * 12;
                    var py = rect.Top + rect.Height / 2 - 4 + (i / 3) * 10;
                    using (var dot = new SolidBrush(ChildVisualTheme.PeachStrong)) g.FillEllipse(dot, px, py, 7, 7);
                }
            }
            DrawCentered(g, groups + " nhóm · mỗi nhóm " + factor, new Rectangle(stage.Left, stage.Bottom - 22, stage.Width, 19), ChildVisualTheme.MutedInk, 8.2f);
            if (_hintLevel >= 2)
                DrawCentered(g, groups + " × " + factor + " = ?", new Rectangle(stage.Left, stage.Bottom - 22, stage.Width, 19), ChildVisualTheme.PeachStrong, 9f);
        }

        private void DrawWordShare(Graphics g, Rectangle stage, int total, int divisor)
        {
            var boxW = Math.Max(62, Math.Min(92, (stage.Width - 30) / divisor));
            var totalW = boxW * divisor;
            var left = stage.Left + (stage.Width - totalW) / 2;
            var y = stage.Top + 20;
            for (var i = 0; i < divisor; i++)
            {
                var rect = new Rectangle(left + i * boxW + 4, y, boxW - 8, 43);
                using (var path = ChildVisualTheme.RoundedRect(rect, 12))
                using (var fill = new SolidBrush(Color.FromArgb(229, 240, 247)))
                using (var border = new Pen(Color.FromArgb(172, 196, 211), 1f))
                { g.FillPath(fill, path); g.DrawPath(border, path); }
                DrawCentered(g, "?", rect, ChildVisualTheme.SkyStrong, 13f);
            }
            DrawCentered(g, total + " chiếc bánh → " + divisor + " phần bằng nhau", new Rectangle(stage.Left, stage.Top + 1, stage.Width, 18), ChildVisualTheme.Ink, 8.2f);
            if (_hintLevel >= 2)
                DrawCentered(g, total + " : " + divisor + " = ?", new Rectangle(stage.Left, stage.Bottom - 22, stage.Width, 19), ChildVisualTheme.PeachStrong, 9f);
        }

        private static void DrawBracket(Graphics g, int left, int right, int y, string label)
        {
            using (var pen = new Pen(Color.FromArgb(126, 139, 135), 1.5f))
            {
                g.DrawLine(pen, left, y, right, y);
                g.DrawLine(pen, left, y - 4, left, y + 4);
                g.DrawLine(pen, right, y - 4, right, y + 4);
            }
            DrawCentered(g, label, new Rectangle(left, y - 19, Math.Max(1, right - left), 18), ChildVisualTheme.PeachStrong, 9f);
        }

        private static void DrawCentered(Graphics g, string text, Rectangle rect, Color color, float size)
        {
            ChildVisualTheme.DrawTextWithOwnedFont(g, text ?? string.Empty, ChildVisualTheme.Font(size, FontStyle.Bold), rect, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawClock(Graphics g)
        {
            var parts = (_question.IllustrationData ?? string.Empty).Split('|');
            int hour, minute;
            if (parts.Length != 3 || parts[0] != "clock" || !int.TryParse(parts[1], out hour) || !int.TryParse(parts[2], out minute)) return;
            if (hour < 1 || hour > 12 || (minute != 15 && minute != 30)) return;

            var radius = Math.Max(34, Math.Min(53, Math.Min(Width / 4, (Height - 8) / 2)));
            var cx = Width / 2;
            var cy = Height / 2;
            var face = new Rectangle(cx - radius, cy - radius, radius * 2, radius * 2);
            using (var fill = new SolidBrush(Color.FromArgb(253, 251, 243)))
            using (var outline = new Pen(Color.FromArgb(126, 139, 135), 2f))
            { g.FillEllipse(fill, face); g.DrawEllipse(outline, face); }

            for (var n = 1; n <= 12; n++)
            {
                var angle = (n * 30 - 90) * Math.PI / 180.0;
                var tx = cx + (int)Math.Round(Math.Cos(angle) * (radius - 13));
                var ty = cy + (int)Math.Round(Math.Sin(angle) * (radius - 13));
                ChildVisualTheme.DrawTextWithOwnedFont(g, n.ToString(), ChildVisualTheme.Font(7.2f, FontStyle.Bold),
                    new Rectangle(tx - 10, ty - 9, 20, 18), ChildVisualTheme.MutedInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            var minuteAngle = (minute * 6 - 90) * Math.PI / 180.0;
            var hourAngle = (((hour % 12) * 30) + minute * 0.5 - 90) * Math.PI / 180.0;
            var minuteEnd = new Point(cx + (int)Math.Round(Math.Cos(minuteAngle) * (radius - 14)),
                cy + (int)Math.Round(Math.Sin(minuteAngle) * (radius - 14)));
            var hourEnd = new Point(cx + (int)Math.Round(Math.Cos(hourAngle) * (radius - 25)),
                cy + (int)Math.Round(Math.Sin(hourAngle) * (radius - 25)));
            using (var minutePen = new Pen(ChildVisualTheme.SkyStrong, 2.5f))
            using (var hourPen = new Pen(ChildVisualTheme.Ink, 4f))
            {
                minutePen.StartCap = LineCap.Round; minutePen.EndCap = LineCap.Round;
                hourPen.StartCap = LineCap.Round; hourPen.EndCap = LineCap.Round;
                g.DrawLine(hourPen, new Point(cx, cy), hourEnd);
                g.DrawLine(minutePen, new Point(cx, cy), minuteEnd);
            }
            using (var hub = new SolidBrush(ChildVisualTheme.PeachStrong)) g.FillEllipse(hub, cx - 4, cy - 4, 8, 8);

            if (_hintLevel >= 1)
            {
                ChildVisualTheme.DrawTextWithOwnedFont(g, "Kim phút: số 3 = 15 phút · số 6 = 30 phút", ChildVisualTheme.Font(8.2f, FontStyle.Bold),
                    new Rectangle(0, Math.Max(0, Height - 21), Width, 19), ChildVisualTheme.MutedInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private void DrawGeometry(Graphics g)
        {
            var skill = _question.SkillId ?? string.Empty;
            var cx = Width / 2;
            var cy = Height / 2;
            var stage = new Rectangle(Math.Max(12, Width / 5), 6, Math.Max(120, Width * 3 / 5), Math.Max(58, Height - 18));
            using (var stagePath = ChildVisualTheme.RoundedRect(stage, 18))
            using (var stageFill = new SolidBrush(Color.FromArgb(248, 249, 242)))
            using (var stageBorder = new Pen(Color.FromArgb(224, 229, 218), 1f))
            { g.FillPath(stageFill, stagePath); g.DrawPath(stageBorder, stagePath); }
            using (var band = new SolidBrush(Color.FromArgb(239, 246, 241)))
                g.FillRectangle(band, stage.Left + 2, stage.Bottom - Math.Min(18, stage.Height / 4), stage.Width - 4, Math.Min(16, stage.Height / 4));
            using (var pen = new Pen(ChildVisualTheme.SkyStrong, 4f))
            using (var thin = new Pen(Color.FromArgb(112, 132, 128), 2f))
            using (var dot = new SolidBrush(ChildVisualTheme.PeachStrong))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                switch (skill)
                {
                    case "POINT_RECOGNIZE":
                        var focus = new Rectangle(cx - 55, cy - 35, 110, 70);
                        using (var focusFill = new SolidBrush(Color.FromArgb(244, 247, 238)))
                        using (var focusBorder = new Pen(Color.FromArgb(220, 226, 213), 1f))
                        using (var focusPath = ChildVisualTheme.RoundedRect(focus, 18))
                        { g.FillPath(focusFill, focusPath); g.DrawPath(focusBorder, focusPath); }
                        g.FillEllipse(dot, cx - 8, cy - 8, 16, 16);
                        ChildVisualTheme.DrawTextWithOwnedFont(g, "A", ChildVisualTheme.Font(10f, FontStyle.Bold), new Rectangle(cx + 12, cy - 14, 32, 26), ChildVisualTheme.Ink);
                        break;
                    case "LINE_SEGMENT_RECOGNIZE":
                        g.DrawLine(pen, cx - 100, cy, cx + 100, cy);
                        g.FillEllipse(dot, cx - 105, cy - 5, 10, 10); g.FillEllipse(dot, cx + 95, cy - 5, 10, 10);
                        ChildVisualTheme.DrawTextWithOwnedFont(g, "A", ChildVisualTheme.Font(8f, FontStyle.Bold), new Rectangle(cx - 118, cy + 8, 24, 20), ChildVisualTheme.Ink);
                        ChildVisualTheme.DrawTextWithOwnedFont(g, "B", ChildVisualTheme.Font(8f, FontStyle.Bold), new Rectangle(cx + 94, cy + 8, 24, 20), ChildVisualTheme.Ink);
                        break;
                    case "CURVE_RECOGNIZE":
                        g.DrawBezier(pen, cx - 130, cy + 28, cx - 45, cy - 70, cx + 50, cy + 70, cx + 130, cy - 24);
                        break;
                    case "STRAIGHT_LINE_RECOGNIZE":
                        g.DrawLine(pen, cx - 150, cy + 30, cx + 150, cy - 30);
                        g.DrawLine(thin, cx - 150, cy + 30, cx - 137, cy + 18);
                        g.DrawLine(thin, cx + 150, cy - 30, cx + 137, cy - 18);
                        break;
                    case "POLYLINE_RECOGNIZE":
                        var poly = new[] { new Point(cx - 135, cy + 25), new Point(cx - 55, cy - 35), new Point(cx + 25, cy + 28), new Point(cx + 135, cy - 20) };
                        g.DrawLines(pen, poly);
                        break;
                    case "THREE_COLLINEAR_POINTS":
                        g.DrawLine(thin, cx - 145, cy, cx + 145, cy);
                        foreach (var x in new[] { cx - 90, cx, cx + 90 }) g.FillEllipse(dot, x - 5, cy - 5, 10, 10);
                        break;
                    case "QUADRILATERAL_RECOGNIZE":
                        var quad = new[] { new Point(cx - 100, cy + 40), new Point(cx - 65, cy - 45), new Point(cx + 85, cy - 30), new Point(cx + 115, cy + 42) };
                        g.DrawPolygon(pen, quad);
                        break;
                    case "CYLINDER_RECOGNIZE":
                        var body = new Rectangle(cx - 70, cy - 42, 140, 84);
                        g.DrawEllipse(pen, body.Left, body.Top - 14, body.Width, 28);
                        g.DrawLine(pen, body.Left, body.Top, body.Left, body.Bottom);
                        g.DrawLine(pen, body.Right, body.Top, body.Right, body.Bottom);
                        g.DrawArc(pen, body.Left, body.Bottom - 14, body.Width, 28, 0, 180);
                        break;
                    case "SPHERE_RECOGNIZE":
                        var sphere = new Rectangle(cx - 58, cy - 58, 116, 116);
                        g.DrawEllipse(pen, sphere);
                        g.DrawEllipse(thin, cx - 24, cy - 58, 48, 116);
                        g.DrawEllipse(thin, cx - 58, cy - 20, 116, 40);
                        break;
                }
            }
            if (_hintLevel >= 1)
                ChildVisualTheme.DrawTextWithOwnedFont(g, "Quan sát đặc điểm của hình", ChildVisualTheme.Font(8.3f, FontStyle.Bold),
                    new Rectangle(0, Math.Max(0, Height - 21), Width, 19), ChildVisualTheme.MutedInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void DrawPictograph(Graphics g)
        {
            var labels = new[] { "Mèo", "Chó", "Thỏ" };
            var counts = new[] { 3, 2, 4 };
            var rowH = Math.Max(24, Math.Min(31, (Height - 24) / 3));
            var startY = Math.Max(1, (Height - (rowH * 3 + 20)) / 2);
            var labelW = Math.Min(82, Math.Max(58, Width / 8));
            var iconSize = Math.Max(13, Math.Min(20, rowH - 7));
            var gap = iconSize + 11;
            var iconsLeft = Math.Max(labelW + 18, Width / 2 - gap * 2);
            for (var row = 0; row < 3; row++)
            {
                var y = startY + row * rowH;
                ChildVisualTheme.DrawTextWithOwnedFont(g, labels[row], ChildVisualTheme.Font(8.7f, FontStyle.Bold),
                    new Rectangle(8, y, labelW, rowH), ChildVisualTheme.Ink,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                for (var i = 0; i < counts[row]; i++)
                    DrawAnimalMark(g, new Rectangle(iconsLeft + i * gap, y + (rowH - iconSize) / 2, iconSize, iconSize), row);
            }
            ChildVisualTheme.DrawTextWithOwnedFont(g, "1 hình = 1 con", ChildVisualTheme.Font(8f, FontStyle.Bold),
                new Rectangle(0, Math.Max(0, Height - 20), Width, 18), ChildVisualTheme.MutedInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private static void DrawAnimalMark(Graphics g, Rectangle rect, int row)
        {
            var fillColor = row == 0 ? Color.FromArgb(236, 185, 139) : row == 1 ? Color.FromArgb(151, 187, 213) : Color.FromArgb(177, 202, 151);
            using (var fill = new SolidBrush(fillColor))
            using (var outline = new Pen(Color.FromArgb(103, 120, 115), 1f))
            {
                g.FillEllipse(fill, rect); g.DrawEllipse(outline, rect);
                if (row != 1)
                {
                    var ear = Math.Max(3, rect.Width / 4);
                    g.FillEllipse(fill, rect.Left + 1, rect.Top - ear / 2, ear, ear);
                    g.FillEllipse(fill, rect.Right - ear - 1, rect.Top - ear / 2, ear, ear);
                }
                else
                {
                    g.FillRectangle(fill, rect.Left + 2, rect.Top - 2, Math.Max(3, rect.Width / 5), 5);
                    g.FillRectangle(fill, rect.Right - Math.Max(3, rect.Width / 5) - 2, rect.Top - 2, Math.Max(3, rect.Width / 5), 5);
                }
            }
        }

        private void DrawDieOutcomes(Graphics g)
        {
            var gap = Math.Max(6, Math.Min(12, Width / 70));
            var available = Math.Max(120, Width - 34 - gap * 5);
            var dieSize = Math.Min(58, Math.Max(34, available / 6));
            var total = dieSize * 6 + gap * 5;
            var left = Math.Max(8, (Width - total) / 2);
            var top = Math.Max(5, (Height - dieSize) / 2 - (_hintLevel >= 1 ? 8 : 0));
            for (var value = 1; value <= 6; value++)
            {
                var rect = new Rectangle(left + (value - 1) * (dieSize + gap), top, dieSize, dieSize);
                using (var path = ChildVisualTheme.RoundedRect(rect, Math.Max(7, dieSize / 7)))
                using (var fill = new SolidBrush(Color.FromArgb(252, 251, 244)))
                using (var border = new Pen(Color.FromArgb(185, 191, 183), 1.5f))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(border, path);
                }
                DrawDiePips(g, rect, value);
            }

            if (_hintLevel >= 1)
            {
                ChildVisualTheme.DrawTextWithOwnedFont(g, "Các kết quả có thể: 1, 2, 3, 4, 5, 6", ChildVisualTheme.Font(8.5f, FontStyle.Bold),
                    new Rectangle(0, Math.Max(0, Height - 22), Width, 20), ChildVisualTheme.MutedInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private static void DrawDiePips(Graphics g, Rectangle rect, int value)
        {
            var cx = rect.Left + rect.Width / 2;
            var cy = rect.Top + rect.Height / 2;
            var dx = Math.Max(8, rect.Width / 4);
            var dy = Math.Max(8, rect.Height / 4);
            var r = Math.Max(2, rect.Width / 13);
            var positions = new System.Collections.Generic.List<Point>();
            if (value == 1 || value == 3 || value == 5) positions.Add(new Point(cx, cy));
            if (value >= 2)
            {
                positions.Add(new Point(cx - dx, cy - dy));
                positions.Add(new Point(cx + dx, cy + dy));
            }
            if (value >= 4)
            {
                positions.Add(new Point(cx + dx, cy - dy));
                positions.Add(new Point(cx - dx, cy + dy));
            }
            if (value == 6)
            {
                positions.Add(new Point(cx - dx, cy));
                positions.Add(new Point(cx + dx, cy));
            }
            using (var pip = new SolidBrush(ChildVisualTheme.Ink))
                foreach (var p in positions) g.FillEllipse(pip, p.X - r, p.Y - r, r * 2 + 1, r * 2 + 1);
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
                ChildVisualTheme.DrawTextWithOwnedFont(g, headers[i], ChildVisualTheme.Font(8.8f, FontStyle.Bold),
                    new Rectangle(x, 14, cellW, 20), ChildVisualTheme.MutedInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                var valueText = _hintLevel >= 1 ? digits[i].ToString() : "?";
                ChildVisualTheme.DrawTextWithOwnedFont(g, valueText, ChildVisualTheme.Font(18f, FontStyle.Bold),
                    new Rectangle(x, 35, cellW, 34), ChildVisualTheme.Ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                if (_hintLevel >= 2)
                {
                    var unit = i == 0 ? digits[i] * 100 : (i == 1 ? digits[i] * 10 : digits[i]);
                    ChildVisualTheme.DrawTextWithOwnedFont(g, "= " + unit, ChildVisualTheme.Font(8.5f),
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
                    ChildVisualTheme.DrawTextWithOwnedFont(g, numbers[k].ToString(), ChildVisualTheme.Font(18f, FontStyle.Bold), box,
                        ChildVisualTheme.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    if (_hintLevel >= 1)
                    {
                        var h = (numbers[k] / 100) % 10; var t = (numbers[k] / 10) % 10; var o = numbers[k] % 10;
                        ChildVisualTheme.DrawTextWithOwnedFont(g, "T " + h + " · C " + t + " · ĐV " + o, ChildVisualTheme.Font(8.2f),
                            new Rectangle(box.Left, box.Bottom - 20, box.Width, 18), ChildVisualTheme.MutedInk,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
                }
                ChildVisualTheme.DrawTextWithOwnedFont(g, "?", ChildVisualTheme.Font(20f, FontStyle.Bold),
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
            ChildVisualTheme.DrawTextWithOwnedFont(g, n.ToString(), ChildVisualTheme.Font(15f, FontStyle.Bold),
                new Rectangle(centerX - 45, y - 45, 90, 30), ChildVisualTheme.Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            ChildVisualTheme.DrawTextWithOwnedFont(g, "−1", ChildVisualTheme.Font(9f, FontStyle.Bold),
                new Rectangle(centerX - 150, y - 40, 90, 22), ChildVisualTheme.MintStrong,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            ChildVisualTheme.DrawTextWithOwnedFont(g, "+1", ChildVisualTheme.Font(9f, FontStyle.Bold),
                new Rectangle(centerX + 60, y - 40, 90, 22), ChildVisualTheme.MintStrong,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            ChildVisualTheme.DrawTextWithOwnedFont(g, "?", ChildVisualTheme.Font(13f, FontStyle.Bold),
                new Rectangle(centerX - 135, y + 12, 60, 24), ChildVisualTheme.PeachStrong,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            ChildVisualTheme.DrawTextWithOwnedFont(g, "?", ChildVisualTheme.Font(13f, FontStyle.Bold),
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
            ChildVisualTheme.DrawTextWithOwnedFont(g, "mỗi nhóm " + divisor, ChildVisualTheme.Font(8.8f, FontStyle.Bold),
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
                ChildVisualTheme.DrawTextWithOwnedFont(g, values[i] + " cm", ChildVisualTheme.Font(9f, FontStyle.Bold),
                    new Rectangle(mx - 34, my - 24, 68, 21), ChildVisualTheme.PeachStrong,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            if (_hintLevel >= 1)
                ChildVisualTheme.DrawTextWithOwnedFont(g, "cộng 3 đoạn", ChildVisualTheme.Font(9f, FontStyle.Bold),
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
                        ChildVisualTheme.DrawTextWithOwnedFont(g, n.ToString(), ChildVisualTheme.Font(8.5f), r,
                            ChildVisualTheme.MutedInk, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
                    }
                }
            }

            var sx = left + (int)Math.Round((right - left) * (start / 20.0));
            var ex = left + (int)Math.Round((right - left) * (Math.Max(min, Math.Min(max, end)) / 20.0));
            using (var startBrush = new SolidBrush(ChildVisualTheme.PeachStrong))
                g.FillEllipse(startBrush, sx - 6, y - 6, 12, 12);
            var startLabel = new Rectangle(sx - 32, 2, 64, 22);
            ChildVisualTheme.DrawTextWithOwnedFont(g, "bắt đầu " + start, ChildVisualTheme.Font(8.5f, FontStyle.Bold), startLabel,
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
            ChildVisualTheme.DrawTextWithOwnedFont(g, label, ChildVisualTheme.Font(9f, FontStyle.Bold), labelRect,
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
            ChildVisualTheme.DrawTextWithOwnedFont(g, groups + " nhóm · mỗi nhóm " + perGroup, ChildVisualTheme.Font(8.5f), labelRect,
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
                ChildVisualTheme.DrawTextWithOwnedFont(g, headers[c], ChildVisualTheme.Font(8.5f, FontStyle.Bold),
                    new Rectangle(x, headerY, cellW, 20), ChildVisualTheme.MutedInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                ChildVisualTheme.DrawTextWithOwnedFont(g, digitsA[c].ToString(), ChildVisualTheme.Font(15f, FontStyle.Bold),
                    new Rectangle(x, rowA, cellW, 27), ChildVisualTheme.Ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                ChildVisualTheme.DrawTextWithOwnedFont(g, digitsB[c].ToString(), ChildVisualTheme.Font(15f, FontStyle.Bold),
                    new Rectangle(x, rowB, cellW, 27), ChildVisualTheme.Ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                using (var divider = new Pen(Color.FromArgb(224, 222, 210), 1f))
                    if (c > 0) g.DrawLine(divider, x, 3, x, Math.Min(Height - 3, 88));
            }
            var signRect = new Rectangle(Math.Max(0, left - 37), rowB, 34, 27);
            ChildVisualTheme.DrawTextWithOwnedFont(g, add ? "+" : "−", ChildVisualTheme.Font(15f, FontStyle.Bold), signRect,
                ChildVisualTheme.PeachStrong, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            using (var pen = new Pen(ChildVisualTheme.Ink, 2f))
                g.DrawLine(pen, left + 5, Math.Min(Height - 8, 88), left + totalW - 5, Math.Min(Height - 8, 88));

            if (_hintLevel >= 2 && cueColumn >= 0)
            {
                var cue = add ? "nhớ 1 sang trái" : "mượn 1 từ trái";
                var cueX = left + cueColumn * cellW;
                ChildVisualTheme.DrawTextWithOwnedFont(g, cue, ChildVisualTheme.Font(8.5f, FontStyle.Bold),
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
        private bool _rescueRewardMode;
        private bool _rewardChestReady;

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

        public void SetRescueRewardState(bool rescueRewardMode, bool rewardChestReady)
        {
            _rescueRewardMode = rescueRewardMode;
            _rewardChestReady = rescueRewardMode && rewardChestReady;
            AccessibleName = _rescueRewardMode ? "Kết quả nhiệm vụ cứu hộ" : "Tiến bộ khu vườn sau nhiệm vụ";
            AccessibleDescription = BuildDescription();
            Invalidate();
        }

        internal bool RescueRewardMode { get { return _rescueRewardMode; } }
        internal bool RewardChestReady { get { return _rewardChestReady; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var iconSize = Math.Min(58, Math.Max(38, Height - 16));
            var icon = new Rectangle(16, (Height - iconSize) / 2, iconSize, iconSize);
            using (var circle = new SolidBrush(_rescueRewardMode ? Color.FromArgb(255, 244, 204) : Color.FromArgb(224, 242, 220))) g.FillEllipse(circle, icon);
            var drewRescueReward = false;
            if (_rescueRewardMode)
            {
                var asset = _rewardChestReady
                    ? "13_MissionIllustrations/mission_18_reward_chest.png"
                    : "03_Rescue/rescue_reward_chest_closed.png";
                drewRescueReward = GameAssetLibrary.DrawContain(g, asset, icon);
            }
            if (!drewRescueReward) DrawRewardIcon(g, icon, _unlockedItemId);

            var textLeft = icon.Right + 16;
            var titleRect = new Rectangle(textLeft, 6, Math.Max(20, Width - textLeft - 10), 25);
            var detailRect = new Rectangle(textLeft, 31, Math.Max(20, Width - textLeft - 10), Math.Max(22, Height - 34));
            var title = _rescueRewardMode
                ? (_rewardChestReady ? "Rương sao đã mở!" : "Đã hoàn thành 3 chặng")
                : (string.IsNullOrWhiteSpace(_unlockedItemId)
                    ? "Khu vườn lớn thêm 1 bước"
                    : "Mở khóa: " + ItemName(_unlockedItemId));
            ChildVisualTheme.DrawTextWithOwnedFont(g, title, ChildVisualTheme.Font(11f, FontStyle.Bold), titleRect,
                ChildVisualTheme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            var next = _sessionsUntilNext > 0 && !string.IsNullOrWhiteSpace(_nextItemId)
                ? "Còn " + _sessionsUntilNext + " nhiệm vụ hoàn thành để tới " + ItemName(_nextItemId) + "."
                : "Các mốc khu vườn hiện tại đã được mở đủ.";
            var detail = _rescueRewardMode
                ? (_rewardChestReady
                    ? "3 sao đã được ghi nhận an toàn · Vườn: " + _growthSteps + " bước · " + next
                    : "Tiến độ đã lưu an toàn · Rương sao đang chờ đồng bộ phần thưởng.")
                : "Vườn: " + _growthSteps + " bước · " + next;
            ChildVisualTheme.DrawTextWithOwnedFont(g, detail,
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
            if (_rescueRewardMode)
                return _rewardChestReady
                    ? "Nhiệm vụ cứu hộ hoàn thành, ba sao đã được ghi nhận và rương sao đã mở an toàn."
                    : "Nhiệm vụ cứu hộ đã hoàn thành ba chặng; tiến độ đã lưu, rương sao đang chờ đồng bộ phần thưởng.";
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
                new RoadmapRow("Phép tính & bài toán", _snapshot == null ? null : _snapshot.Written1000, ChildVisualTheme.MintStrong),
                new RoadmapRow("Nhân / Chia 2 · 5", _snapshot == null ? null : _snapshot.Tables25, ChildVisualTheme.SkyStrong),
                new RoadmapRow("Hình & đo lường", _snapshot == null ? null : _snapshot.Measurement, Color.FromArgb(147, 126, 181)),
                new RoadmapRow("Dữ liệu & khả năng", _snapshot == null ? null : _snapshot.Chance, Color.FromArgb(185, 126, 157))
            };
            var rowH = Math.Max(20, Height / 6);
            for (var i = 0; i < rows.Length; i++) DrawRow(e.Graphics, rows[i], new Rectangle(0, i * rowH, Width, rowH));
        }

        private static void DrawRow(Graphics g, RoadmapRow row, Rectangle bounds)
        {
            var titleWidth = Math.Min(176, Math.Max(112, bounds.Width / 3));
            var titleRect = new Rectangle(bounds.Left + 4, bounds.Top + 2, titleWidth - 8, bounds.Height - 4);
            ChildVisualTheme.DrawTextWithOwnedFont(g, row.Title, ChildVisualTheme.Font(9.3f, FontStyle.Bold), titleRect,
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
            ChildVisualTheme.DrawTextWithOwnedFont(g, status, ChildVisualTheme.Font(8.7f, hasEvidence ? FontStyle.Bold : FontStyle.Regular),
                statusRect, hasEvidence ? row.Accent : ChildVisualTheme.MutedInk,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static string BuildAccessibleDescription(MathRoadmapSnapshot snapshot)
        {
            if (snapshot == null) return "Chưa có dữ liệu lộ trình Toán.";
            return DescribeGroup("Số đến 1000", snapshot.NumberSense) + "; " +
                   DescribeGroup("Nhẩm 0 đến 20", snapshot.Mental20) + "; " +
                   DescribeGroup("Phép tính và bài toán", snapshot.Written1000) + "; " +
                   DescribeGroup("Nhân chia bảng 2 và 5", snapshot.Tables25) + "; " +
                   DescribeGroup("Hình và đo lường", snapshot.Measurement) + "; " +
                   DescribeGroup("Dữ liệu và khả năng", snapshot.Chance) + ".";
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
