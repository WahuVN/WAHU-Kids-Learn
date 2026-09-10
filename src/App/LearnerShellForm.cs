using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WAHU.Data;
using WAHU.Performance;
using WAHU.Platform;
using WAHU.Security;

namespace WAHUKidsLearn
{
    internal sealed class LearnerShellForm : Form
    {
        private readonly LearnerShellContext _context;
        private readonly ParentPinStore _pinStore;
        private readonly Panel _pageHost;
        private readonly ChildActionButton _backButton;
        private readonly ChildActionButton _parentButton;
        private readonly Label _routeTitle;
        private readonly Label _routeSubtitle;
        private readonly LearnerRouter _router;
        private LearnerLayoutProfile _layoutProfile;

        public LearnerShellForm(RuntimeConfigBundle config, LearningDatabase learningDatabase, PreflightReport report,
            DatabaseBootstrapResult database, RuntimeBootstrapIssue issue, bool previousRunUnclean,
            RuntimePerformanceSettings performance)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (learningDatabase == null) throw new ArgumentNullException("learningDatabase");

            _context = new LearnerShellContext
            {
                Config = config,
                LearningDatabase = learningDatabase,
                Preflight = report,
                Database = database,
                Issue = issue,
                PreviousRunUnclean = previousRunUnclean,
                Performance = performance
            };
            _pinStore = new ParentPinStore(Path.Combine(config.UserRoot, "security", "parent_pin.json"));

            Text = "WAHU Kids Learn";
            AccessibleName = "WAHU Kids Learn";
            StartPosition = FormStartPosition.CenterScreen;
            ChildWindowSizing.ApplyLearnerWindowDefaults(this, new Size(1180, 760), new Size(900, 640));
            Font = ChildVisualTheme.Font(11f);
            BackColor = ChildVisualTheme.Cream;
            KeyPreview = true;
            DoubleBuffered = true;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = ChildVisualTheme.Cream
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var topBar = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(12, 10, 12, 2),
                Padding = new Padding(12, 8, 12, 8),
                CardColor = Color.FromArgb(252, 250, 242),
                BorderColor = Color.FromArgb(226, 222, 207),
                Radius = LearnerDesignTokens.RadiusCard,
                ShowShadow = false
            };
            var topLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146));
            topLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _backButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 1, 10, 1),
                Text = "← Quay lại",
                FillColor = Color.FromArgb(236, 233, 223),
                HoverColor = Color.FromArgb(224, 220, 208),
                PressedColor = Color.FromArgb(211, 207, 194),
                TextColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(9.8f, FontStyle.Bold),
                Radius = LearnerDesignTokens.RadiusButton,
                AccessibleName = "Quay lại màn trước"
            };
            _backButton.Click += delegate { _router.GoBack(); };
            topLayout.Controls.Add(_backButton, 0, 0);

            var titleLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            titleLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            titleLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
            _routeTitle = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Hôm nay",
                TextAlign = ContentAlignment.BottomCenter,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(16f, FontStyle.Bold),
                AccessibleName = "Màn hình hiện tại"
            };
            _routeSubtitle = new Label
            {
                Dock = DockStyle.Fill,
                Text = "WAHU Kids Learn",
                TextAlign = ContentAlignment.TopCenter,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(8.8f)
            };
            titleLayout.Controls.Add(_routeTitle, 0, 0);
            titleLayout.Controls.Add(_routeSubtitle, 0, 1);
            topLayout.Controls.Add(titleLayout, 1, 0);

            _parentButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(10, 1, 0, 1),
                Text = "Phụ huynh",
                FillColor = Color.FromArgb(232, 230, 220),
                HoverColor = Color.FromArgb(220, 218, 207),
                PressedColor = Color.FromArgb(208, 205, 194),
                TextColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(9.8f, FontStyle.Bold),
                Radius = LearnerDesignTokens.RadiusButton,
                AccessibleName = "Mở chế độ phụ huynh"
            };
            _parentButton.Click += delegate { OpenParentMode(); };
            topLayout.Controls.Add(_parentButton, 2, 0);
            topBar.Controls.Add(topLayout);
            root.Controls.Add(topBar, 0, 0);

            _pageHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = ChildVisualTheme.Cream };
            root.Controls.Add(_pageHost, 0, 1);
            Controls.Add(root);

            _router = new LearnerRouter(_pageHost, CreatePage);
            _router.RouteChanged += delegate { RefreshChrome(); };
            _layoutProfile = LearnerLayoutProfileResolver.Resolve(ClientSize, DeviceDpi);
            _router.SetLayoutProfile(_layoutProfile);
            // Materialize the first page before the window is shown so the first painted frame is never an empty shell.
            _router.ResetTo(LearnerRoute.Home);

            Shown += delegate { _router.ResetTo(LearnerRoute.Home); };
            Resize += delegate { UpdateLayoutProfile(); };
        }

        internal string CurrentRouteName { get { return _router.CurrentRoute.ToString(); } }
        internal string CurrentPageTypeName { get { return _router.CurrentPageTypeName; } }
        internal Control CurrentPageControl { get { return _router.CurrentPage; } }
        internal string CurrentLayoutProfileName { get { return _layoutProfile.ToString(); } }

        internal void NavigateToRouteName(string routeName)
        {
            LearnerRoute route;
            if (!Enum.TryParse(routeName, true, out route)) throw new ArgumentException("Unknown learner route: " + routeName);
            _router.Navigate(route);
        }

        private LearnerPage CreatePage(LearnerRoute route)
        {
            switch (route)
            {
                case LearnerRoute.Home:
                    return new LearnerHomePage(_context, delegate(LearnerRoute next) { _router.Navigate(next); });
                case LearnerRoute.MathWorld:
                    return new MathWorldPage(_context, delegate(LearnerRoute next) { _router.Navigate(next); });
                case LearnerRoute.RescueMap:
                    return new RescueMapPage(_context, delegate(LearnerRoute next) { _router.Navigate(next); });
                case LearnerRoute.LessonPlay:
                    return new LessonPlayPage(_context, delegate { _router.GoBack(); });
                case LearnerRoute.TypingSpace:
                    return new TypingSpacePage(_context, delegate { _router.GoBack(); });
                default:
                    throw new NotSupportedException("Unknown learner route: " + route + ".");
            }
        }

        private void RefreshChrome()
        {
            _routeTitle.Text = _router.CurrentTitle;
            _backButton.Enabled = _router.CanGoBack;
            _backButton.Visible = _router.CurrentRoute != LearnerRoute.Home;
            _routeSubtitle.Text = _router.CurrentRoute == LearnerRoute.Home
                ? "WAHU Kids Learn"
                : "Học trong một cửa sổ • phần đã làm luôn được lưu";
        }

        private void UpdateLayoutProfile()
        {
            var next = LearnerLayoutProfileResolver.Resolve(ClientSize, DeviceDpi);
            if (next == _layoutProfile) return;
            _layoutProfile = next;
            _router.SetLayoutProfile(next);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && _router.CanGoBack)
            {
                _router.GoBack();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.P))
            {
                OpenParentMode();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OpenParentMode()
        {
            try
            {
                if (!_pinStore.IsConfigured)
                {
                    using (var setup = new ParentPinDialog(ParentPinDialogMode.Setup))
                    {
                        if (setup.ShowDialog(this) != DialogResult.OK) return;
                        _pinStore.SetPin(setup.PinValue);
                    }
                }
                else
                {
                    using (var unlock = new ParentPinDialog(ParentPinDialogMode.Unlock))
                    {
                        if (unlock.ShowDialog(this) != DialogResult.OK) return;
                        var result = _pinStore.Verify(unlock.PinValue);
                        if (!result.Success)
                        {
                            MessageBox.Show(this,
                                result.Locked
                                    ? "Chế độ phụ huynh đang tạm khóa. Thử lại sau khoảng " + result.RemainingSeconds + " giây."
                                    : "PIN phụ huynh chưa đúng.",
                                "Chế độ phụ huynh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                }

                using (var dashboard = new ParentDashboardForm(_context.Config, _context.LearningDatabase, _context.Database,
                    _context.Preflight, _context.Performance, _context.Issue, _pinStore))
                    dashboard.ShowDialog(this);

                if (_router.CurrentRoute == LearnerRoute.Home)
                    _router.ResetTo(LearnerRoute.Home);
            }
            catch (InvalidDataException)
            {
                MessageBox.Show(this,
                    "Dữ liệu khóa phụ huynh bị lỗi. Ứng dụng sẽ không bỏ qua PIN. Hãy dùng bản backup hoặc cài đặt hỗ trợ để sửa file khóa.",
                    "Bảo vệ phụ huynh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch
            {
                MessageBox.Show(this,
                    "Không thể mở chế độ phụ huynh lúc này. Dữ liệu học không bị thay đổi.",
                    "Chế độ phụ huynh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
