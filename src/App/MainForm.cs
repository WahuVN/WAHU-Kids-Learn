using System;
using System.Drawing;
using System.Windows.Forms;
using WAHU.Data;
using WAHU.Platform;

namespace WAHUKidsLearn
{
    public sealed class MainForm : Form
    {
        private readonly PreflightReport _report;
        private readonly DatabaseBootstrapResult _database;
        private readonly string _databaseError;
        private readonly bool _previousRunUnclean;

        public MainForm(PreflightReport report, DatabaseBootstrapResult database, string databaseError, bool previousRunUnclean)
        {
            _report = report;
            _database = database;
            _databaseError = databaseError;
            _previousRunUnclean = previousRunUnclean;
            Text = "WAHU Kids Learn — Bootstrap V1";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(800, 600);
            ClientSize = new Size(1024, 720);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point);
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

            var title = new Label { Dock = DockStyle.Fill, Text = "WAHU Kids Learn", TextAlign = ContentAlignment.BottomCenter, Font = new Font(Font.FontFamily, 28f, FontStyle.Bold), AccessibleName = "Tiêu đề WAHU Kids Learn" };
            var subtitle = new Label { Dock = DockStyle.Fill, Text = "Học Toán và Tiếng Anh theo từng nhiệm vụ nhỏ", TextAlign = ContentAlignment.MiddleCenter, Font = new Font(Font.FontFamily, 16f), AccessibleName = "Giới thiệu ứng dụng" };
            var start = new Button { Anchor = AnchorStyles.None, Size = new Size(320, 96), Text = "Bắt đầu", Font = new Font(Font.FontFamily, 20f, FontStyle.Bold), AccessibleName = "Bắt đầu học", AccessibleDescription = "Mở phiên học." };
            start.Enabled = _database != null && _database.Health != null && _database.Health.IsHealthy;
            start.Click += delegate { MessageBox.Show(this, "Nền runtime + learner database đã sẵn sàng. Learning Engine sẽ được nối vào luồng này.", "WAHU Kids Learn", MessageBoxButtons.OK, MessageBoxIcon.Information); };

            var parent = new Button { Anchor = AnchorStyles.None, Size = new Size(220, 64), Text = "Phụ huynh", AccessibleName = "Mở chế độ phụ huynh" };
            parent.Click += delegate { ShowDiagnostics(); };
            var status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopCenter, Text = BuildStatusText(), AccessibleName = "Trạng thái tương thích máy và dữ liệu" };

            root.Controls.Add(title, 0, 0); root.Controls.Add(subtitle, 0, 1); root.Controls.Add(start, 0, 2); root.Controls.Add(parent, 0, 3); root.Controls.Add(status, 0, 4);
            Controls.Add(root);
        }

        private string BuildStatusText()
        {
            var machine = _report == null ? "Máy: chưa có preflight" : string.Format("Máy: {0} · .NET {1} · {2}", _report.CompatibilityLevel, _report.NetFrameworkRelease, _report.ProcessArch);
            var db = _database != null ? string.Format("DB: OK · SQLite {0} · journal {1}", _database.SQLiteVersion, _database.JournalMode) : "DB: cần phục hồi/kiểm tra";
            var recovery = _previousRunUnclean ? "Phiên trước đóng bất thường · đã kiểm tra DB" : "Phiên trước đóng sạch";
            return machine + Environment.NewLine + db + Environment.NewLine + recovery;
        }

        private void ShowDiagnostics()
        {
            var machine = _report == null ? "Không có báo cáo preflight." : string.Format("Compatibility: {0}\r\nOS: {1} {2}\r\n.NET: {3}\r\nSHA-2: {4}\r\nDPI: {5}\r\nAudio: {6}\r\nMic: {7}", _report.CompatibilityLevel, _report.OsVersion, _report.ServicePack, _report.NetFrameworkRelease, _report.LegacySha2Readiness, _report.SystemDpi.HasValue ? _report.SystemDpi.Value.ToString("0") : "?", _report.AudioOutputAvailable ? "Có" : "Không", _report.MicrophoneAvailable ? "Có" : "Không");
            var db = _database == null ? "DB ERROR: " + (_databaseError ?? "không rõ") : string.Format("DB: {0}\r\nProvider: {1}\r\nSQLite: {2}\r\nJournal: {3}\r\nMigration: v{4}\r\nMigration SHA-256: {5}\r\nIntegrity: {6}\r\nFK issues: {7}", _database.DatabasePath, _database.ProviderVersion, _database.SQLiteVersion, _database.JournalMode, _database.Migration == null ? 0 : _database.Migration.Version, _database.Migration == null ? "?" : _database.Migration.ChecksumSha256, _database.Health.Integrity, _database.Health.ForeignKeyIssues);
            var recovery = _previousRunUnclean ? "Phiên trước: đóng bất thường" : "Phiên trước: đóng sạch";
            MessageBox.Show(this, machine + "\r\n\r\n" + db + "\r\n\r\n" + recovery, "Chẩn đoán nền tảng", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
