using System;
using System.Drawing;
using System.Windows.Forms;

namespace WAHUKidsLearn
{
    internal sealed class TypingSpacePage : LearnerPage
    {
        private readonly LearnerShellContext _context;
        private readonly Action _back;
        private readonly TypingSpaceForm _embedded;

        public TypingSpacePage(LearnerShellContext context, Action back)
        {
            _context = context ?? throw new ArgumentNullException("context");
            _back = back ?? throw new ArgumentNullException("back");
            AccessibleName = "Phi thuyền gõ phím Việt Anh";

            if (!_context.IsLearnerReady)
            {
                Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "Phi thuyền gõ phím sẽ mở khi dữ liệu học sẵn sàng.",
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = ChildVisualTheme.MutedInk,
                    Font = ChildVisualTheme.Font(12f, FontStyle.Bold),
                    AutoEllipsis = true
                });
                return;
            }

            _embedded = new TypingSpaceForm(_context.LearningDatabase, _context.Performance)
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill,
                ShowInTaskbar = false,
                MinimumSize = Size.Empty,
                MaximumSize = Size.Empty
            };
            _embedded.ExitRequested += delegate { _back(); };
            Controls.Add(_embedded);
            _embedded.Show();
        }

        public override LearnerRoute Route { get { return LearnerRoute.TypingSpace; } }
        public override string PageTitle { get { return "Phi thuyền gõ phím"; } }

        public override void OnNavigatedTo()
        {
            if (_embedded == null) return;
            _embedded.Visible = true;
            _embedded.ResumeFromNavigation();
            _embedded.Focus();
        }

        public override void OnNavigatedFrom()
        {
            if (_embedded != null) _embedded.PauseForNavigation();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _embedded != null) _embedded.Dispose();
            base.Dispose(disposing);
        }
    }
}
