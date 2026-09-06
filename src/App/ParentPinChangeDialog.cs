using System;
using System.Drawing;
using System.Windows.Forms;

namespace WAHUKidsLearn
{
    public sealed class ParentPinChangeDialog : Form
    {
        private readonly TextBox _current;
        private readonly TextBox _next;
        private readonly TextBox _confirm;
        private readonly Label _message;

        public ParentPinChangeDialog()
        {
            Text = "Đổi PIN phụ huynh";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(440, 355);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 11f);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 7 };
            var title = new Label { Dock = DockStyle.Fill, Height = 44, Text = "Đổi PIN phụ huynh", Font = new Font(Font.FontFamily, 16f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            _current = NewBox("PIN hiện tại");
            _next = NewBox("PIN mới");
            _confirm = NewBox("Nhập lại PIN mới");
            _message = new Label { Dock = DockStyle.Fill, AutoSize = true, ForeColor = SystemColors.HotTrack };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            var ok = new Button { Text = "Đổi PIN", Width = 110, Height = 42 };
            var cancel = new Button { Text = "Hủy", Width = 90, Height = 42, DialogResult = DialogResult.Cancel };
            ok.Click += delegate { ValidateAndClose(); };
            buttons.Controls.Add(ok); buttons.Controls.Add(cancel);

            root.Controls.Add(title); root.Controls.Add(new Label { Text = "PIN mới cần gồm 4–10 chữ số.", AutoSize = true });
            root.Controls.Add(_current); root.Controls.Add(_next); root.Controls.Add(_confirm); root.Controls.Add(_message); root.Controls.Add(buttons);
            Controls.Add(root);
            AcceptButton = ok; CancelButton = cancel;
        }

        public string CurrentPin { get { return _current.Text; } }
        public string NewPin { get { return _next.Text; } }

        private TextBox NewBox(string name)
        {
            return new TextBox { Dock = DockStyle.Top, Height = 36, MaxLength = 10, UseSystemPasswordChar = true, AccessibleName = name, Font = new Font(Font.FontFamily, 15f) };
        }

        private void ValidateAndClose()
        {
            if (!Valid(_current.Text) || !Valid(_next.Text)) { _message.Text = "PIN cần gồm 4–10 chữ số."; return; }
            if (!string.Equals(_next.Text, _confirm.Text, StringComparison.Ordinal)) { _message.Text = "Hai lần nhập PIN mới chưa giống nhau."; return; }
            if (string.Equals(_current.Text, _next.Text, StringComparison.Ordinal)) { _message.Text = "PIN mới nên khác PIN hiện tại."; return; }
            DialogResult = DialogResult.OK;
            Close();
        }

        private static bool Valid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 4 || value.Length > 10) return false;
            for (var i = 0; i < value.Length; i++) if (value[i] < '0' || value[i] > '9') return false;
            return true;
        }
    }
}
