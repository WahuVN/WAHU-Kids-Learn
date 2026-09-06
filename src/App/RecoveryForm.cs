using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WAHU.Data;
using WAHU.Platform;

namespace WAHUKidsLearn
{
    public sealed class RecoveryForm : Form
    {
        private readonly RuntimeConfigBundle _config;
        private readonly LearningDatabase _learningDatabase;
        private readonly ListBox _list;
        private readonly Label _status;
        private IList<RecoveryBackupCandidate> _candidates;

        public RecoveryForm(RuntimeConfigBundle config, LearningDatabase learningDatabase)
        {
            _config = config ?? throw new ArgumentNullException("config");
            _learningDatabase = learningDatabase ?? throw new ArgumentNullException("learningDatabase");
            Text = "Phục hồi dữ liệu học";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(700, 500);
            ClientSize = new Size(780, 560);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10.5f);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 5 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

            root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Phục hồi từ bản sao lưu đã xác minh", Font = new Font(Font.FontFamily, 18f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Ứng dụng chỉ hiển thị backup qua SHA-256 + SQLite integrity + foreign-key gate. Bản dữ liệu hiện tại sẽ được giữ lại trước khi thay thế.", TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            _list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, AccessibleName = "Danh sách bản sao lưu đã xác minh" };
            root.Controls.Add(_list, 0, 2);
            _status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            root.Controls.Add(_status, 0, 3);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var restore = new Button { Text = "Phục hồi bản đã chọn", Width = 190, Height = 42 };
            var refresh = new Button { Text = "Làm mới", Width = 105, Height = 42 };
            var close = new Button { Text = "Đóng", Width = 90, Height = 42, DialogResult = DialogResult.Cancel };
            restore.Click += delegate { RestoreSelected(); };
            refresh.Click += delegate { LoadCandidates(); };
            buttons.Controls.Add(restore); buttons.Controls.Add(refresh); buttons.Controls.Add(close);
            root.Controls.Add(buttons, 0, 4);
            Controls.Add(root);
            CancelButton = close;
            Shown += delegate { LoadCandidates(); };
        }

        private void LoadCandidates()
        {
            _list.Items.Clear();
            _candidates = BackupRecoveryService.FindVerifiedBackups(_config.BackupsDirectory);
            foreach (var candidate in _candidates) _list.Items.Add(new CandidateItem(candidate));
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;
            _status.Text = _list.Items.Count == 0
                ? "Chưa tìm thấy backup VERIFIED hợp lệ. Có thể dùng backup thủ công từ thư mục khác trong bản sau."
                : "Tìm thấy " + _list.Items.Count + " bản sao lưu đã xác minh.";
        }

        private void RestoreSelected()
        {
            var item = _list.SelectedItem as CandidateItem;
            if (item == null) { _status.Text = "Hãy chọn một bản sao lưu."; return; }
            var answer = MessageBox.Show(this,
                "Phục hồi dữ liệu về bản " + item.Candidate.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm") + "?\r\n\r\nDữ liệu hiện tại sẽ được giữ lại thành bản pre-restore để không mất dấu.",
                "Xác nhận phục hồi", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;

            try
            {
                var result = _learningDatabase.RestoreManagedBackup(item.Candidate.MetadataPath);
                if (result.Health == null || !result.Health.IsHealthy) throw new InvalidDataException("Restored DB failed health gate.");
                _status.Text = "Phục hồi thành công. Hãy đóng và mở lại ứng dụng trước khi học tiếp.";
                MessageBox.Show(this,
                    "Đã phục hồi dữ liệu và kiểm tra integrity thành công.\r\n\r\nVui lòng đóng rồi mở lại WAHU Kids Learn để nạp trạng thái mới.",
                    "Phục hồi hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
                _status.Text = "Không thể phục hồi bản này. Dữ liệu hiện tại chưa bị xóa.";
                MessageBox.Show(this,
                    "Bản sao lưu không thể phục hồi an toàn. Ứng dụng đã dừng thao tác trước khi xóa dữ liệu hiện tại.",
                    "Phục hồi không thành công", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private sealed class CandidateItem
        {
            public CandidateItem(RecoveryBackupCandidate candidate) { Candidate = candidate; }
            public RecoveryBackupCandidate Candidate { get; private set; }
            public override string ToString()
            {
                return Candidate.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm") +
                    "  ·  " + Candidate.BackupType + "  ·  schema v" + Candidate.SchemaVersion;
            }
        }
    }
}
