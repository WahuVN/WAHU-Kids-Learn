using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using WAHU.Data;
using WAHU.Performance;
using WAHU.Platform;
using WAHU.Security;

namespace WAHUKidsLearn
{
    public sealed class ParentDashboardForm : Form
    {
        private readonly RuntimeConfigBundle _config;
        private readonly LearningDatabase _learningDatabase;
        private readonly DatabaseBootstrapResult _database;
        private readonly PreflightReport _preflight;
        private readonly RuntimePerformanceSettings _performance;
        private readonly RuntimeBootstrapIssue _issue;
        private readonly ParentPinStore _pinStore;
        private readonly Label _summary;
        private readonly Button _updateButton;
        private readonly Button _startupButton;

        public ParentDashboardForm(RuntimeConfigBundle config, LearningDatabase learningDatabase,
            DatabaseBootstrapResult database, PreflightReport preflight, RuntimePerformanceSettings performance,
            RuntimeBootstrapIssue issue, ParentPinStore pinStore)
        {
            _config = config ?? throw new ArgumentNullException("config");
            _learningDatabase = learningDatabase ?? throw new ArgumentNullException("learningDatabase");
            _database = database;
            _preflight = preflight;
            _performance = performance;
            _issue = issue;
            _pinStore = pinStore ?? throw new ArgumentNullException("pinStore");

            Text = "WAHU Kids Learn — Phụ huynh";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(800, 600);
            ClientSize = new Size(920, 680);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10.5f);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 1, RowCount = 4 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Chế độ phụ huynh", Font = new Font(Font.FontFamily, 22f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);

            _summary = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(4), AutoEllipsis = true };
            root.Controls.Add(_summary, 0, 1);

            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, Padding = new Padding(0, 8, 0, 8) };
            for (var i = 0; i < 4; i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            AddAction(actions, "Sao lưu thủ công", 0, 0, ManualBackup);
            AddAction(actions, "Phục hồi dữ liệu", 1, 0, OpenRecovery);
            _updateButton = AddAction(actions, "Kiểm tra cập nhật", 2, 0, CheckForUpdates);
            _startupButton = AddAction(actions, "Khởi động cùng Windows", 3, 0, ToggleStartup);
            AddAction(actions, "Chẩn đoán nâng cao", 0, 1, ShowDiagnostics);
            AddAction(actions, "Đổi PIN", 1, 1, ChangePin);
            AddAction(actions, "Mở thư mục backup", 2, 1, OpenBackupFolder);
            AddAction(actions, "Làm mới", 3, 1, RefreshSummary);
            root.Controls.Add(actions, 0, 2);

            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var close = new Button { Text = "Đóng", Width = 110, Height = 42, DialogResult = DialogResult.OK };
            footer.Controls.Add(close); root.Controls.Add(footer, 0, 3);
            Controls.Add(root); AcceptButton = close;
            Shown += delegate { RefreshSummary(); };
        }

        private Button AddAction(TableLayoutPanel panel, string text, int col, int row, EventHandler action)
        {
            var button = new Button { Dock = DockStyle.Fill, Margin = new Padding(6), Text = text, AccessibleName = text };
            button.Click += action;
            panel.Controls.Add(button, col, row);
            return button;
        }

        private void RefreshSummary(object sender = null, EventArgs e = null)
        {
            var issueText = _issue == null || _issue.Kind == RuntimeIssueKind.None
                ? "Hệ thống: sẵn sàng"
                : "Hệ thống: " + _issue.ChildMessage;
            var device = _preflight == null ? "Thiết bị: chưa có dữ liệu" :
                "Thiết bị: " + _preflight.ProcessArch + " · RAM " + (_preflight.RamTotalMb.HasValue ? _preflight.RamTotalMb.Value + " MB" : "?") +
                " · Audio " + (_preflight.AudioOutputAvailable ? "có" : "không");
            var perf = _performance == null ? "Hiệu năng: chưa xác định" :
                "Hiệu năng: " + _performance.Profile + " · " + _performance.MotionFpsCap + " FPS cap";

            string learning;
            if (_database == null || _database.Health == null || !_database.Health.IsHealthy)
            {
                learning = "Học tập: dữ liệu chưa được mở an toàn; dashboard không tự chạm DB cho tới khi phục hồi/khởi động lại.";
            }
            else
            {
                try
                {
                    var summary = ParentSummaryService.Read(_learningDatabase);
                    learning = summary.SessionCount == 0
                        ? "Học tập: chưa có phiên học nào được ghi nhận."
                        : string.Format(CultureInfo.InvariantCulture,
                            "Học tập: {0} phiên · {1} lượt trả lời · {2} kỹ năng có trạng thái. Vững {3} · Đang học {4} · Cần ôn {5}.",
                            summary.SessionCount, summary.AttemptCount, summary.SkillCount,
                            summary.StableSkillCount, summary.LearningSkillCount, summary.ReviewSkillCount);
                }
                catch { learning = "Học tập: dữ liệu đang cần phục hồi/kiểm tra."; }
            }

            var backups = BackupRecoveryService.FindVerifiedBackups(_config.BackupsDirectory).Count;
            var update = UpdateStatusService.Read(_config, Application.ExecutablePath);
            var updateText = DescribeUpdate(update);
            var startupText = _config.PortableMode ? "Khởi động cùng Windows: không áp dụng ở Portable" :
                "Khởi động cùng Windows: " + (update.StartupEnabled ? "BẬT" : "TẮT");
            if (_startupButton != null)
            {
                _startupButton.Enabled = !_config.PortableMode;
                _startupButton.Text = _config.PortableMode ? "Startup: Portable" : (update.StartupEnabled ? "Tắt khởi động cùng Windows" : "Bật khởi động cùng Windows");
            }
            if (_updateButton != null) _updateButton.Enabled = !_config.PortableMode;

            _summary.Text = issueText + Environment.NewLine + Environment.NewLine + learning + Environment.NewLine +
                "Backup VERIFIED: " + backups + Environment.NewLine + updateText + Environment.NewLine + startupText + Environment.NewLine + Environment.NewLine +
                device + Environment.NewLine + perf;
        }

        private static string DescribeUpdate(UpdateRuntimeStatus status)
        {
            if (status == null) return "Cập nhật: chưa có trạng thái";
            if (status.PortableMode) return "Cập nhật: Portable dùng ZIP thủ công, không tự thay binary";
            if (!string.IsNullOrWhiteSpace(status.StagedVersion))
                return "Cập nhật: đã tải " + status.StagedVersion + " · sẵn sàng cài";
            if (!string.IsNullOrWhiteSpace(status.LastResult) && status.LastResult.StartsWith("update_check_failed:", StringComparison.Ordinal))
                return "Cập nhật: lần kiểm gần nhất chưa kết nối được; app vẫn dùng offline bình thường";
            if (!string.IsNullOrWhiteSpace(status.LastResult) && status.LastResult.StartsWith("updated_to:", StringComparison.Ordinal))
                return "Cập nhật: đã cập nhật thành công lên " + status.CurrentVersion;
            if (!string.IsNullOrWhiteSpace(status.LastResult) && status.LastResult.StartsWith("current:", StringComparison.Ordinal))
                return "Cập nhật: đang ở bản mới nhất " + status.CurrentVersion;
            var checkedAt = status.LastCheckUtc.HasValue ? status.LastCheckUtc.Value.ToLocalTime().ToString("g") : "chưa kiểm";
            return "Cập nhật: " + status.CurrentVersion + " · lần kiểm " + checkedAt;
        }

        private void CheckForUpdates(object sender, EventArgs e)
        {
            if (_config.PortableMode)
            {
                MessageBox.Show(this, "Bản Portable không tự thay file ứng dụng. Hãy tải ZIP Portable mới khi cần cập nhật.", "Cập nhật", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_updateButton != null)
            {
                _updateButton.Enabled = false;
                _updateButton.Text = "Đang kiểm tra...";
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                string result;
                try { new GitHubUpdateClient().TryCheckAndStage(_config, _config.AppVersion, true, out result); }
                catch (Exception ex) { result = "update_check_failed:" + ex.GetType().Name; }
                if (IsDisposed || Disposing) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (_updateButton != null) { _updateButton.Enabled = true; _updateButton.Text = "Kiểm tra cập nhật"; }
                        RefreshSummary();
                        HandleUpdateResult(result);
                    });
                }
                catch { }
            });
        }

        private void HandleUpdateResult(string result)
        {
            result = result ?? string.Empty;
            if (result.StartsWith("staged:", StringComparison.Ordinal) || result.StartsWith("already_staged:", StringComparison.Ordinal))
            {
                var version = result.Substring(result.IndexOf(':') + 1);
                var choice = MessageBox.Show(this,
                    "Đã tải và xác minh bản " + version + ".\r\n\r\nCài ngay? Ứng dụng sẽ sao lưu dữ liệu, đóng lại, cập nhật rồi mở lại tự động.",
                    "Cập nhật đã sẵn sàng", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (choice == DialogResult.Yes) ApplyStagedUpdateNow();
                return;
            }
            if (result.StartsWith("current:", StringComparison.Ordinal))
            {
                MessageBox.Show(this, "WAHU Kids Learn đang ở bản mới nhất.", "Cập nhật", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (result == "portable_manual_only") return;
            if (result.StartsWith("update_check_failed:", StringComparison.Ordinal))
            {
                MessageBox.Show(this, "Chưa kiểm tra được bản mới lúc này. Có thể đang mất mạng hoặc GitHub chưa truy cập được. Việc học offline vẫn hoạt động bình thường.", "Cập nhật", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            MessageBox.Show(this, "Chưa có bản cập nhật mới cần cài.", "Cập nhật", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ApplyStagedUpdateNow()
        {
            if (_database == null || _database.Health == null || !_database.Health.IsHealthy)
            {
                MessageBox.Show(this, "Dữ liệu học chưa ở trạng thái đủ an toàn để cập nhật. Hãy phục hồi dữ liệu trước.", "Cập nhật", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            StagedUpdate staged;
            string error;
            if (!UpdateApplyCoordinator.TryLaunch(_config, _learningDatabase, AppDomain.CurrentDomain.BaseDirectory, Application.ExecutablePath, out staged, out error))
            {
                MessageBox.Show(this, "Chưa thể bắt đầu cập nhật an toàn. Bản hiện tại vẫn được giữ nguyên.", "Cập nhật", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Close();
            Application.Exit();
        }

        private void ToggleStartup(object sender, EventArgs e)
        {
            if (_config.PortableMode) return;
            try
            {
                var enabled = StartupRegistrationService.IsEnabled(Application.ExecutablePath);
                StartupRegistrationService.SetEnabled(Application.ExecutablePath, !enabled);
                RefreshSummary();
            }
            catch
            {
                MessageBox.Show(this, "Không thể thay đổi thiết lập khởi động cùng Windows trên tài khoản này.", "Khởi động cùng Windows", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ManualBackup(object sender, EventArgs e)
        {
            if (_database == null || _database.Health == null || !_database.Health.IsHealthy)
            {
                MessageBox.Show(this, "Database chưa ở trạng thái đủ an toàn để tạo backup mới. Hãy phục hồi dữ liệu trước.", "Sao lưu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using (var picker = new FolderBrowserDialog())
            {
                picker.Description = "Chọn thư mục lưu backup WAHU Kids Learn";
                picker.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var artifact = _learningDatabase.CreateManualBackup(picker.SelectedPath, _config.AppVersion);
                    MessageBox.Show(this, "Backup đã được tạo và xác minh SHA-256 + integrity.\r\n\r\n" + artifact.MetadataPath,
                        "Sao lưu hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshSummary();
                }
                catch
                {
                    MessageBox.Show(this, "Không thể tạo backup an toàn ở thư mục đã chọn. Dữ liệu học hiện tại không bị thay đổi.", "Sao lưu không thành công", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void OpenRecovery(object sender, EventArgs e)
        {
            using (var form = new RecoveryForm(_config, _learningDatabase)) form.ShowDialog(this);
            RefreshSummary();
        }

        private void ChangePin(object sender, EventArgs e)
        {
            using (var dialog = new ParentPinChangeDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    if (_pinStore.ChangePin(dialog.CurrentPin, dialog.NewPin))
                        MessageBox.Show(this, "Đã đổi PIN phụ huynh.", "PIN phụ huynh", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else
                        MessageBox.Show(this, "PIN hiện tại chưa đúng hoặc đang tạm khóa.", "PIN phụ huynh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch
                {
                    MessageBox.Show(this, "Không thể cập nhật PIN. PIN cũ vẫn được giữ nếu thao tác ghi không hoàn tất.", "PIN phụ huynh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void OpenBackupFolder(object sender, EventArgs e)
        {
            try
            {
                Directory.CreateDirectory(_config.BackupsDirectory);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + _config.BackupsDirectory + "\"");
            }
            catch { MessageBox.Show(this, "Không thể mở thư mục backup trên máy này.", "Backup", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }

        private void ShowDiagnostics(object sender, EventArgs e)
        {
            var technical = _issue == null ? "Không có runtime issue." : (_issue.TechnicalMessage ?? "Không có chi tiết.");
            var machine = _preflight == null ? "Preflight: chưa có" :
                "Compatibility: " + _preflight.CompatibilityLevel + "\r\nOS: " + _preflight.OsVersion + " " + _preflight.ServicePack +
                "\r\n.NET: " + _preflight.NetFrameworkRelease + "\r\nDPI: " + (_preflight.SystemDpi.HasValue ? _preflight.SystemDpi.Value.ToString("0") : "?") +
                "\r\nAudio/Mic: " + (_preflight.AudioOutputAvailable ? "có" : "không") + "/" + (_preflight.MicrophoneAvailable ? "có" : "không");
            var db = _database == null ? "DB: unavailable" : "DB: " + _database.DatabasePath + "\r\nSQLite: " + _database.SQLiteVersion + "\r\nIntegrity: " + _database.Health.Integrity + "\r\nFK: " + _database.Health.ForeignKeyIssues;
            MessageBox.Show(this, machine + "\r\n\r\n" + db + "\r\n\r\nRuntime issue:\r\n" + technical,
                "Chẩn đoán nâng cao", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

    }
}
