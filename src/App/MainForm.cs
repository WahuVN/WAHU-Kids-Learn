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
    public sealed class MainForm : Form
    {
        private readonly RuntimeConfigBundle _config;
        private readonly LearningDatabase _learningDatabase;
        private readonly PreflightReport _report;
        private readonly DatabaseBootstrapResult _database;
        private readonly RuntimeBootstrapIssue _issue;
        private readonly bool _previousRunUnclean;
        private readonly RuntimePerformanceSettings _performance;
        private readonly ParentPinStore _pinStore;

        public MainForm(RuntimeConfigBundle config, LearningDatabase learningDatabase, PreflightReport report,
            DatabaseBootstrapResult database, RuntimeBootstrapIssue issue, bool previousRunUnclean,
            RuntimePerformanceSettings performance)
        {
            _config = config ?? throw new ArgumentNullException("config");
            _learningDatabase = learningDatabase ?? throw new ArgumentNullException("learningDatabase");
            _report = report;
            _database = database;
            _issue = issue;
            _previousRunUnclean = previousRunUnclean;
            _performance = performance;
            _pinStore = new ParentPinStore(Path.Combine(_config.UserRoot, "security", "parent_pin.json"));

            Text = "WAHU Kids Learn";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(800, 600);
            ClientSize = new Size(1024, 720);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point);
            KeyPreview = true;
            BuildUi();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(36) };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 16));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 18));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 14));

            var title = new Label
            {
                Dock = DockStyle.Fill,
                Text = "WAHU Kids Learn",
                TextAlign = ContentAlignment.BottomCenter,
                Font = new Font(Font.FontFamily, 28f, FontStyle.Bold),
                AccessibleName = "Tiêu đề WAHU Kids Learn"
            };
            var subtitle = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Học Toán và Tiếng Anh theo từng nhiệm vụ nhỏ",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font.FontFamily, 16f),
                AccessibleName = "Giới thiệu ứng dụng"
            };
            var start = new Button
            {
                Anchor = AnchorStyles.None,
                Size = new Size(320, 96),
                Text = "Bắt đầu",
                Font = new Font(Font.FontFamily, 20f, FontStyle.Bold),
                AccessibleName = "Bắt đầu học",
                AccessibleDescription = "Mở phiên học."
            };
            start.Enabled = IsLearnerReady();
            start.Click += delegate
            {
                MessageBox.Show(this,
                    "Nền học tập đã sẵn sàng. Phần phiên học Toán/Tiếng Anh đang được nối vào nút này.",
                    "WAHU Kids Learn", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            var parent = new Button
            {
                Anchor = AnchorStyles.None,
                Size = new Size(180, 52),
                Text = _issue != null && _issue.CanRecoverDatabase ? "Phụ huynh · Phục hồi" : "Phụ huynh",
                AccessibleName = "Mở chế độ phụ huynh"
            };
            parent.Click += delegate { OpenParentMode(); };
            var status = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopCenter,
                Text = BuildStatusText(),
                AccessibleName = "Trạng thái ứng dụng"
            };

            root.Controls.Add(title, 0, 0);
            root.Controls.Add(subtitle, 0, 1);
            root.Controls.Add(start, 0, 2);
            root.Controls.Add(parent, 0, 3);
            root.Controls.Add(status, 0, 4);
            Controls.Add(root);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Shift | Keys.P))
            {
                OpenParentMode();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool IsLearnerReady()
        {
            return (_issue == null || _issue.Kind == RuntimeIssueKind.None) &&
                _database != null && _database.Health != null && _database.Health.IsHealthy;
        }

        private string BuildStatusText()
        {
            if (_issue != null && _issue.Kind != RuntimeIssueKind.None)
            {
                var adult = _issue.CanRecoverDatabase
                    ? "Nhờ người lớn mở Phụ huynh → Phục hồi dữ liệu."
                    : "Nhờ người lớn mở chế độ Phụ huynh để kiểm tra.";
                return (_issue.ChildMessage ?? "Ứng dụng cần người lớn kiểm tra.") + Environment.NewLine + adult;
            }

            var machine = _report == null ? "Máy: đang kiểm tra" :
                string.Format("Máy: {0} · {1}", _report.ProcessArch, _report.AudioOutputAvailable ? "có âm thanh" : "không có âm thanh");
            var db = _database != null ? "Dữ liệu học: sẵn sàng" : "Dữ liệu học: cần kiểm tra";
            var recovery = _previousRunUnclean ? "Phiên trước đóng bất thường · dữ liệu đã được kiểm tra" : "Phiên trước đóng sạch";
            var perf = _performance == null ? "Hiệu năng: đang xác định" :
                string.Format("Hiệu năng: {0} · {1} FPS", _performance.Profile, _performance.MotionFpsCap);
            return machine + Environment.NewLine + db + " · " + perf + Environment.NewLine + recovery;
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

                using (var dashboard = new ParentDashboardForm(_config, _learningDatabase, _database, _report,
                    _performance, _issue, _pinStore))
                    dashboard.ShowDialog(this);
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
