using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using WAHU.Learning;

namespace WAHUKidsLearn
{
    internal sealed class SegmentDrawingAnswerControl : Control
    {
        private int _maxMark = 15;
        private int _cursorMark;
        private int? _pointA;
        private int? _pointB;
        private int _hintLevel;
        private bool _locked;
        private bool? _resultCorrect;

        public event EventHandler AnswerChanged;

        public bool HasAnswer
        {
            get { return _pointA.HasValue && _pointB.HasValue && _pointA.Value != _pointB.Value; }
        }

        public int SelectedLength
        {
            get { return HasAnswer ? Math.Abs(_pointB.Value - _pointA.Value) : 0; }
        }

        public string SelectedAnswer
        {
            get { return HasAnswer ? SelectedLength.ToString(CultureInfo.InvariantCulture) : string.Empty; }
        }

        public SegmentDrawingAnswerControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            TabStop = true;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            AccessibleName = "Vẽ đoạn thẳng trên thước";
            UpdateAccessibleDescription();
        }

        public void SetQuestion(MathQuestion question)
        {
            _maxMark = ReadMaxMark(question == null ? null : question.IllustrationData);
            _cursorMark = 0;
            _pointA = null;
            _pointB = null;
            _hintLevel = 0;
            _locked = false;
            _resultCorrect = null;
            UpdateAccessibleDescription();
            Invalidate();
            OnAnswerChanged();
        }

        public void SetHintLevel(int hintLevel)
        {
            _hintLevel = Math.Max(0, Math.Min(2, hintLevel));
            UpdateAccessibleDescription();
            Invalidate();
        }

        public void MoveCursor(int delta)
        {
            if (_locked || delta == 0) return;
            _cursorMark = Math.Max(0, Math.Min(_maxMark, _cursorMark + delta));
            UpdateAccessibleDescription();
            Invalidate();
        }

        public void SelectCursor()
        {
            if (_locked) return;
            SelectMark(_cursorMark);
        }

        public void ShowResult(bool isCorrect)
        {
            _locked = true;
            _resultCorrect = isCorrect;
            UpdateAccessibleDescription();
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            var key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Home || key == Keys.End || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!_locked)
            {
                if (e.KeyCode == Keys.Left)
                {
                    MoveCursor(-1);
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.Right)
                {
                    MoveCursor(1);
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.Home)
                {
                    _cursorMark = 0;
                    UpdateAccessibleDescription();
                    Invalidate();
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.End)
                {
                    _cursorMark = _maxMark;
                    UpdateAccessibleDescription();
                    Invalidate();
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.Space)
                {
                    SelectCursor();
                    e.Handled = true;
                    return;
                }
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_locked || e.Button != MouseButtons.Left) return;
            Focus();
            var ruler = RulerBounds();
            var hit = Rectangle.Inflate(ruler, 16, 42);
            if (!hit.Contains(e.Location)) return;
            var mark = MarkFromX(e.X, ruler);
            _cursorMark = mark;
            SelectMark(mark);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 120 || Height < 72) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var card = new Rectangle(3, 2, Math.Max(1, Width - 7), Math.Max(1, Height - 8));
            var borderColor = _resultCorrect.HasValue
                ? (_resultCorrect.Value ? Color.FromArgb(153, 205, 157) : Color.FromArgb(229, 171, 156))
                : (Focused ? ChildVisualTheme.SkyStrong : Color.FromArgb(214, 218, 207));
            var shadowRect = new Rectangle(card.X, card.Y + 4, card.Width, card.Height);
            using (var shadowPath = ChildVisualTheme.RoundedRect(shadowRect, 18))
            using (var shadow = new SolidBrush(Color.FromArgb(38, 61, 75, 68)))
                g.FillPath(shadow, shadowPath);
            using (var path = ChildVisualTheme.RoundedRect(card, 18))
            using (var fill = new LinearGradientBrush(card, Color.FromArgb(255, 254, 248), Color.FromArgb(248, 251, 246), 90f))
            using (var border = new Pen(borderColor, Focused ? 2f : 1.3f))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            var status = BuildStatusText();
            ChildVisualTheme.DrawTextWithOwnedFont(g, status, ChildVisualTheme.Font(10f, FontStyle.Bold),
                new Rectangle(18, 8, Math.Max(20, Width - 36), 26), ChildVisualTheme.Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            var ruler = RulerBounds();
            var bodyRect = new Rectangle(ruler.Left, ruler.Top, ruler.Width, 34);
            var bodyShadow = new Rectangle(bodyRect.X, bodyRect.Y + 3, bodyRect.Width, bodyRect.Height);
            using (var shadowPath = ChildVisualTheme.RoundedRect(bodyShadow, 9))
            using (var shadow = new SolidBrush(Color.FromArgb(34, 113, 105, 80)))
                g.FillPath(shadow, shadowPath);
            using (var bodyPath = ChildVisualTheme.RoundedRect(bodyRect, 9))
            using (var body = new LinearGradientBrush(bodyRect, Color.FromArgb(252, 243, 207), Color.FromArgb(241, 226, 174), 90f))
            using (var outline = new Pen(Color.FromArgb(151, 135, 92), 1.4f))
            {
                g.FillPath(body, bodyPath);
                g.DrawPath(outline, bodyPath);
            }

            var labelEveryMark = ruler.Width / Math.Max(1, _maxMark) >= 27;
            for (var mark = 0; mark <= _maxMark; mark++)
            {
                var x = XForMark(mark, ruler);
                var major = mark % 5 == 0 || mark == _maxMark;
                using (var tick = new Pen(Color.FromArgb(113, 105, 80), major ? 1.5f : 1f))
                    g.DrawLine(tick, x, ruler.Top, x, ruler.Top + (major ? 18 : 11));
                if (labelEveryMark || major)
                {
                    ChildVisualTheme.DrawTextWithOwnedFont(g, mark.ToString(CultureInfo.InvariantCulture), ChildVisualTheme.Font(7.5f),
                        new Rectangle(x - 15, ruler.Top + 17, 30, 16), ChildVisualTheme.MutedInk,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }

            if (_pointA.HasValue && _pointB.HasValue)
            {
                var ax = XForMark(_pointA.Value, ruler);
                var bx = XForMark(_pointB.Value, ruler);
                var y = ruler.Top - 9;
                var segmentColor = _resultCorrect.HasValue
                    ? (_resultCorrect.Value ? Color.FromArgb(79, 150, 91) : Color.FromArgb(205, 104, 88))
                    : ChildVisualTheme.SkyStrong;
                using (var segment = new Pen(segmentColor, 4f))
                {
                    segment.StartCap = LineCap.Round;
                    segment.EndCap = LineCap.Round;
                    g.DrawLine(segment, ax, y, bx, y);
                }
            }

            DrawEndpoint(g, ruler, _pointA, "A", Color.FromArgb(92, 154, 189));
            DrawEndpoint(g, ruler, _pointB, "B", Color.FromArgb(214, 137, 78));
            DrawCursor(g, ruler);

            var hint = BuildHintText();
            if (!string.IsNullOrWhiteSpace(hint))
            {
                ChildVisualTheme.DrawTextWithOwnedFont(g, hint, ChildVisualTheme.Font(8.7f, FontStyle.Bold),
                    new Rectangle(18, Math.Max(ruler.Bottom + 5, Height - 30), Math.Max(20, Width - 36), 24),
                    _hintLevel >= 2 ? ChildVisualTheme.PeachStrong : ChildVisualTheme.MutedInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private void SelectMark(int mark)
        {
            mark = Math.Max(0, Math.Min(_maxMark, mark));
            if (!_pointA.HasValue || (_pointA.HasValue && _pointB.HasValue))
            {
                _pointA = mark;
                _pointB = null;
            }
            else if (_pointA.Value == mark)
            {
                _pointB = null;
            }
            else
            {
                _pointB = mark;
            }
            UpdateAccessibleDescription();
            Invalidate();
            OnAnswerChanged();
        }

        private void OnAnswerChanged()
        {
            var handler = AnswerChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private string BuildStatusText()
        {
            if (_resultCorrect.HasValue)
                return _resultCorrect.Value ? "Đoạn thẳng đã chọn đúng độ dài" : "Đoạn thẳng này chưa đúng độ dài";
            if (HasAnswer) return "AB = " + SelectedLength + " cm · Nhấn Kiểm tra khi con sẵn sàng";
            if (_pointA.HasValue) return "Đã chọn A ở vạch " + _pointA.Value + " · Chọn tiếp điểm B";
            return "Chọn điểm A trước, rồi chọn điểm B trên thước";
        }

        private string BuildHintText()
        {
            if (_hintLevel <= 0) return "Chuột: bấm vào vạch · Bàn phím: ← → rồi Space";
            if (_hintLevel == 1) return "Đếm số khoảng 1 cm từ A đến B.";
            if (HasAnswer) return "Độ dài AB = |" + _pointB.Value + " − " + _pointA.Value + "| = " + SelectedLength + " cm.";
            return "Độ dài AB = |vị trí B − vị trí A|.";
        }

        private void DrawEndpoint(Graphics g, Rectangle ruler, int? mark, string label, Color color)
        {
            if (!mark.HasValue) return;
            var x = XForMark(mark.Value, ruler);
            var y = ruler.Top - 9;
            using (var dot = new SolidBrush(color)) g.FillEllipse(dot, x - 6, y - 6, 12, 12);
            ChildVisualTheme.DrawTextWithOwnedFont(g, label, ChildVisualTheme.Font(8.8f, FontStyle.Bold),
                new Rectangle(x - 16, y - 26, 32, 18), color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void DrawCursor(Graphics g, Rectangle ruler)
        {
            if (_locked) return;
            var x = XForMark(_cursorMark, ruler);
            var y = ruler.Top + 37;
            var color = Focused ? ChildVisualTheme.MintStrong : Color.FromArgb(145, 153, 146);
            using (var brush = new SolidBrush(color))
            {
                var points = new[]
                {
                    new Point(x, y),
                    new Point(x - 6, y + 9),
                    new Point(x + 6, y + 9)
                };
                g.FillPolygon(brush, points);
            }
        }

        private Rectangle RulerBounds()
        {
            var left = Math.Max(34, Width / 14);
            var right = Math.Max(left + 80, Width - left);
            var y = Math.Max(58, Math.Min(Height - 64, Height / 2));
            return new Rectangle(left, y, Math.Max(80, right - left), 34);
        }

        private int XForMark(int mark, Rectangle ruler)
        {
            return ruler.Left + (int)Math.Round(ruler.Width * (mark / (double)Math.Max(1, _maxMark)));
        }

        private int MarkFromX(int x, Rectangle ruler)
        {
            var ratio = (x - ruler.Left) / (double)Math.Max(1, ruler.Width);
            return Math.Max(0, Math.Min(_maxMark, (int)Math.Round(ratio * _maxMark)));
        }

        private static int ReadMaxMark(string illustrationData)
        {
            var parts = (illustrationData ?? string.Empty).Split('|');
            int max;
            if (parts.Length >= 3 && string.Equals(parts[0], "segmentdraw", StringComparison.Ordinal) &&
                int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out max))
                return Math.Max(2, Math.Min(30, max));
            return 15;
        }

        private void UpdateAccessibleDescription()
        {
            string description;
            if (_resultCorrect.HasValue)
            {
                description = _resultCorrect.Value
                    ? "Kết quả đúng. Đoạn thẳng đã chọn dài " + SelectedLength + " xăng-ti-mét."
                    : "Kết quả chưa đúng. Đoạn thẳng đã chọn dài " + SelectedLength + " xăng-ti-mét.";
            }
            else if (HasAnswer)
            {
                description = "Điểm A ở vạch " + _pointA.Value + ", điểm B ở vạch " + _pointB.Value +
                    ". Độ dài đang chọn là " + SelectedLength + " xăng-ti-mét. Nhấn Enter hoặc nút Kiểm tra.";
            }
            else if (_pointA.HasValue)
            {
                description = "Điểm A ở vạch " + _pointA.Value + ". Dùng mũi tên trái phải và nhấn Space để chọn điểm B.";
            }
            else
            {
                description = "Dùng mũi tên trái phải để di chuyển trên thước. Nhấn Space để chọn điểm A, rồi chọn điểm B.";
            }
            AccessibleDescription = description;
        }
    }
}
