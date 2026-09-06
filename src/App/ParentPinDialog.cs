using System;
using System.Drawing;
using System.Windows.Forms;

namespace WAHUKidsLearn
{
    public enum ParentPinDialogMode
    {
        Setup,
        Unlock
    }

    public sealed class ParentPinDialog : Form
    {
        private readonly ParentPinDialogMode _mode;
        private readonly TextBox _pin;
        private readonly TextBox _confirm;
        private readonly Label _message;

        public ParentPinDialog(ParentPinDialogMode mode)
        {
            _mode = mode;
            Text = mode == ParentPinDialogMode.Setup ? "Tạo PIN phụ huynh" : "Mở chế độ phụ huynh";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(430, mode == ParentPinDialogMode.Setup ? 310 : 245);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 11f);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                ColumnCount = 1,
                RowCount = mode == ParentPinDialogMode.Setup ? 7 : 5
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var title = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Height = 42,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font.FontFamily, 16f, FontStyle.Bold),
                Text = mode == ParentPinDialogMode.Setup ? "Tạo PIN chỉ người lớn biết" : "Nhập PIN phụ huynh"
            };
            var hint = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Text = mode == ParentPinDialogMode.Setup
                    ? "Dùng 4–10 chữ số. PIN được lưu dưới dạng băm, không lưu nguyên văn."
                    : "PIN bảo vệ cài đặt, backup và thông tin chẩn đoán."
            };
            _pin = NewPinBox("PIN phụ huynh");
            _confirm = NewPinBox("Nhập lại PIN");
            _message = new Label { Dock = DockStyle.Fill, AutoSize = true, ForeColor = SystemColors.HotTrack };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            var ok = new Button { Text = mode == ParentPinDialogMode.Setup ? "Tạo PIN" : "Mở", Width = 110, Height = 42, DialogResult = DialogResult.None };
            var cancel = new Button { Text = "Hủy", Width = 90, Height = 42, DialogResult = DialogResult.Cancel };
            ok.Click += delegate { ValidateAndClose(); };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);

            root.Controls.Add(title);
            root.Controls.Add(hint);
            root.Controls.Add(_pin);
            if (mode == ParentPinDialogMode.Setup) root.Controls.Add(_confirm);
            root.Controls.Add(_message);
            root.Controls.Add(buttons);
            Controls.Add(root);
            AcceptButton = ok;
            CancelButton = cancel;
            Shown += delegate { _pin.Focus(); };
        }

        public string PinValue { get { return _pin.Text; } }

        public void SetMessage(string text)
        {
            _message.Text = text ?? string.Empty;
        }

        private TextBox NewPinBox(string accessibleName)
        {
            return new TextBox
            {
                Dock = DockStyle.Top,
                Height = 36,
                MaxLength = 10,
                UseSystemPasswordChar = true,
                AccessibleName = accessibleName,
                Font = new Font(Font.FontFamily, 15f)
            };
        }

        private void ValidateAndClose()
        {
            var pin = _pin.Text ?? string.Empty;
            if (pin.Length < 4 || pin.Length > 10 || !AllDigits(pin))
            {
                _message.Text = "PIN cần gồm 4–10 chữ số.";
                _pin.Focus();
                return;
            }
            if (_mode == ParentPinDialogMode.Setup && !string.Equals(pin, _confirm.Text, StringComparison.Ordinal))
            {
                _message.Text = "Hai lần nhập PIN chưa giống nhau.";
                _confirm.Focus();
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private static bool AllDigits(string value)
        {
            for (var i = 0; i < value.Length; i++)
                if (value[i] < '0' || value[i] > '9') return false;
            return true;
        }
    }
}
