using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WAHU.Data;
using WAHU.Learning;
using WAHU.Performance;
using WAHU.Session;

namespace WAHUKidsLearn
{
    public sealed class MathLessonForm : Form
    {
        private readonly LearningDatabase _database;
        private readonly RuntimePerformanceSettings _performance;
        private MathSessionCoordinator _coordinator;
        private MathQuestion _question;
        private readonly Button[] _answerButtons = new Button[4];
        private Label _progress;
        private Label _prompt;
        private Label _support;
        private Label _feedback;
        private Button _hintButton;
        private Button _nextButton;
        private Button _stopButton;
        private int _hintLevel;
        private bool _finished;
        private bool _submitting;
        private bool _completeOnNext;

        public MathLessonForm(LearningDatabase database, RuntimePerformanceSettings performance)
        {
            _database = database ?? throw new ArgumentNullException("database");
            _performance = performance;
            Text = "WAHU Kids Learn — Toán lớp 2";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(800, 600);
            ClientSize = new Size(1024, 720);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point);
            KeyPreview = true;
            BuildUi();
            Shown += delegate { StartSession(); };
            FormClosing += OnFormClosing;
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(34, 26, 34, 26),
                ColumnCount = 1,
                RowCount = 7
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            var title = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Toán lớp 2",
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font.FontFamily, 18f, FontStyle.Bold),
                AccessibleName = "Môn Toán lớp 2"
            };
            _progress = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
                AccessibleName = "Tiến độ buổi học"
            };
            header.Controls.Add(title, 0, 0);
            header.Controls.Add(_progress, 1, 0);
            root.Controls.Add(header, 0, 0);

            _prompt = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font.FontFamily, 28f, FontStyle.Bold),
                AutoEllipsis = true,
                AccessibleName = "Câu hỏi Toán"
            };
            root.Controls.Add(_prompt, 0, 1);

            _support = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font.FontFamily, 13f),
                AccessibleName = "Gợi ý học tập"
            };
            root.Controls.Add(_support, 0, 2);

            var answerGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(80, 4, 80, 4) };
            answerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            answerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            answerGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            answerGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            for (var i = 0; i < _answerButtons.Length; i++)
            {
                var index = i;
                var button = new Button
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(10),
                    Font = new Font(Font.FontFamily, 21f, FontStyle.Bold),
                    AccessibleName = "Đáp án " + (i + 1),
                    TabIndex = i
                };
                button.Click += delegate { SubmitChoice(index); };
                _answerButtons[i] = button;
                answerGrid.Controls.Add(button, i % 2, i / 2);
            }
            root.Controls.Add(answerGrid, 0, 3);

            _feedback = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font.FontFamily, 14f, FontStyle.Bold),
                AccessibleName = "Phản hồi câu trả lời"
            };
            root.Controls.Add(_feedback, 0, 4);

            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(90, 4, 90, 4) };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            _stopButton = new Button { Dock = DockStyle.Fill, Margin = new Padding(8), Text = "Dừng buổi học", AccessibleName = "Dừng buổi học" };
            _hintButton = new Button { Dock = DockStyle.Fill, Margin = new Padding(8), Text = "Gợi ý", AccessibleName = "Xem gợi ý" };
            _nextButton = new Button { Dock = DockStyle.Fill, Margin = new Padding(8), Text = "Câu tiếp theo", AccessibleName = "Chuyển sang câu tiếp theo", Visible = false };
            _stopButton.Click += delegate { RequestStop(); };
            _hintButton.Click += delegate { ShowHint(); };
            _nextButton.Click += delegate { HandleNextButton(); };
            actions.Controls.Add(_stopButton, 0, 0);
            actions.Controls.Add(_hintButton, 1, 0);
            actions.Controls.Add(_nextButton, 2, 0);
            root.Controls.Add(actions, 0, 5);

            var footer = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Phím 1–4: chọn đáp án · Enter: câu tiếp theo · Esc: dừng",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font.FontFamily, 9.5f),
                AccessibleName = "Hướng dẫn phím tắt"
            };
            root.Controls.Add(footer, 0, 6);
            Controls.Add(root);
        }

        private void StartSession()
        {
            try
            {
                var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "content_packs", "math_grade2_v1", "verified_templates_v1.json");
                var profile = _performance == null ? "LOW" : _performance.Profile.ToString();
                var seed = unchecked(Environment.TickCount ^ DateTime.UtcNow.Millisecond ^ GetHashCode());
                _coordinator = new MathSessionCoordinator(_database, templatePath, profile, seed);
                var started = _coordinator.Start("Bé học");
                if (started.RecoveredDanglingSessions > 0)
                    _support.Text = "Buổi trước bị đóng dở đã được lưu an toàn. Mình bắt đầu buổi mới nhé.";
                ShowNextQuestion();
            }
            catch
            {
                ShowFatalChildMessage("Chưa thể bắt đầu buổi Toán lúc này. Nhờ người lớn mở mục Phụ huynh để kiểm tra nhé.");
            }
        }

        private void ShowNextQuestion()
        {
            if (_finished || _coordinator == null || !_coordinator.IsActive) return;
            try
            {
                _question = _coordinator.NextQuestion();
                if (_question == null)
                {
                    CompleteSession();
                    return;
                }
                _hintLevel = 0;
                _submitting = false;
                _prompt.Text = _question.PromptVi;
                _support.Text = "Chọn đáp án con thấy đúng nhất.";
                _feedback.Text = string.Empty;
                _hintButton.Text = "Gợi ý";
                _hintButton.Enabled = true;
                _hintButton.Visible = true;
                _nextButton.Visible = false;
                _completeOnNext = false;
                var summary = _coordinator.Summary;
                _progress.Text = "Câu " + (summary.Attempts + 1) + " / " + MathSessionCoordinator.DefaultTargetQuestionCount;
                for (var i = 0; i < _answerButtons.Length; i++)
                {
                    _answerButtons[i].Text = _question.Choices[i].ToString();
                    _answerButtons[i].Enabled = true;
                    _answerButtons[i].Visible = true;
                }
                _answerButtons[0].Focus();
            }
            catch
            {
                FailCurrentSession("Không thể mở câu tiếp theo. Những câu con đã làm vẫn được giữ lại.");
            }
        }

        private void ShowHint()
        {
            if (_question == null || _submitting || !_hintButton.Enabled) return;
            if (_hintLevel == 0)
            {
                _hintLevel = 1;
                _support.Text = _question.HintLevel1;
                _hintButton.Text = "Gợi ý thêm";
                return;
            }
            _hintLevel = 2;
            _support.Text = _question.HintLevel2;
            _hintButton.Enabled = false;
            _hintButton.Text = "Đã xem đủ gợi ý";
        }

        private void SubmitChoice(int index)
        {
            if (_question == null || _submitting || index < 0 || index >= _answerButtons.Length) return;
            _submitting = true;
            SetAnswersEnabled(false);
            _hintButton.Enabled = false;
            try
            {
                var selected = _question.Choices[index];
                var outcome = _coordinator.SubmitAnswer(selected, _hintLevel, "mouse");
                _feedback.Text = outcome.FeedbackVi;
                _support.Text = outcome.IsCorrect
                    ? "Mỗi câu con làm giúp ứng dụng chọn lần ôn phù hợp hơn."
                    : "Không sao, câu tiếp theo sẽ được chọn để con luyện đúng phần đang cần.";
                _progress.Text = "Đã làm " + outcome.CompletedQuestionCount + " / " + outcome.TargetQuestionCount;
                _hintButton.Visible = false;
                _nextButton.Visible = true;
                _completeOnNext = outcome.SuggestPositiveEnd || outcome.CompletedQuestionCount >= outcome.TargetQuestionCount;
                _nextButton.Text = outcome.SuggestPositiveEnd ? "Nghỉ ở đây" :
                    (outcome.CompletedQuestionCount >= outcome.TargetQuestionCount ? "Xem kết quả" : "Câu tiếp theo");
                if (outcome.SuggestPositiveEnd)
                    _support.Text = "Con đã cố gắng đủ cho lúc này. Có thể nghỉ ở đây và học tiếp lần sau.";
                _nextButton.Focus();
            }
            catch
            {
                FailCurrentSession("Không thể lưu câu vừa làm. Buổi học sẽ dừng để bảo vệ dữ liệu.");
            }
        }

        private void HandleNextButton()
        {
            if (_finished)
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            if (_completeOnNext) CompleteSession();
            else ShowNextQuestion();
        }

        private void SetAnswersEnabled(bool enabled)
        {
            foreach (var button in _answerButtons) button.Enabled = enabled;
        }

        private void CompleteSession()
        {
            if (_finished) return;
            try
            {
                var summary = _coordinator != null && _coordinator.IsActive ? _coordinator.Complete() : null;
                _finished = true;
                ShowCompletion(summary);
            }
            catch
            {
                FailCurrentSession("Buổi học đã kết thúc. Những câu đã lưu trước đó vẫn được giữ lại.");
            }
        }

        private void ShowCompletion(MathSessionSummary summary)
        {
            _question = null;
            _prompt.Text = "Hoàn thành buổi Toán";
            _support.Text = summary == null
                ? "Các câu đã làm được lưu để lần sau tiếp tục đúng chỗ."
                : "Con đã luyện " + summary.DistinctSkills + " kỹ năng. Các câu đã được lưu để lần sau ôn đúng lúc.";
            _feedback.Text = summary == null ? string.Empty :
                "Hoàn thành " + summary.Attempts + " câu · Tự làm đúng " + Math.Max(0, summary.Correct - summary.HintedCorrect) + " câu";
            foreach (var button in _answerButtons) button.Visible = false;
            _hintButton.Visible = false;
            _stopButton.Visible = false;
            _nextButton.Visible = true;
            _nextButton.Text = "Về màn hình chính";
            _completeOnNext = false;
            _progress.Text = "Đã hoàn thành";
            _nextButton.Focus();
        }

        private void RequestStop()
        {
            if (_finished) { Close(); return; }
            var answer = MessageBox.Show(this,
                "Dừng buổi học ở đây?\r\n\r\nNhững câu đã làm vẫn được lưu để lần sau học tiếp.",
                "Dừng buổi học", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;
            try { if (_coordinator != null && _coordinator.IsActive) _coordinator.Abort("child_requested_stop"); }
            catch { }
            _finished = true;
            Close();
        }

        private void FailCurrentSession(string message)
        {
            try { if (_coordinator != null && _coordinator.IsActive) _coordinator.Abort("runtime_ui_error"); }
            catch { }
            _finished = true;
            ShowFatalChildMessage(message);
        }

        private void ShowFatalChildMessage(string message)
        {
            _finished = true;
            _question = null;
            _prompt.Text = "Mình dừng ở đây nhé";
            _support.Text = message;
            _feedback.Text = "Những dữ liệu đã lưu trước đó không bị xóa.";
            foreach (var button in _answerButtons) button.Visible = false;
            _hintButton.Visible = false;
            _stopButton.Visible = false;
            _nextButton.Visible = true;
            _nextButton.Text = "Về màn hình chính";
            _completeOnNext = false;
            _nextButton.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData >= Keys.D1 && keyData <= Keys.D4)
            {
                var index = (int)keyData - (int)Keys.D1;
                if (_answerButtons[index].Enabled && _answerButtons[index].Visible) SubmitChoice(index);
                return true;
            }
            if (keyData >= Keys.NumPad1 && keyData <= Keys.NumPad4)
            {
                var index = (int)keyData - (int)Keys.NumPad1;
                if (_answerButtons[index].Enabled && _answerButtons[index].Visible) SubmitChoice(index);
                return true;
            }
            if (keyData == Keys.Enter && _nextButton.Visible && _nextButton.Enabled)
            {
                _nextButton.PerformClick();
                return true;
            }
            if (keyData == Keys.Escape)
            {
                RequestStop();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_finished) return;
            if (e.CloseReason == CloseReason.UserClosing)
            {
                var answer = MessageBox.Show(this,
                    "Dừng buổi học ở đây? Những câu đã làm vẫn được lưu.",
                    "Dừng buổi học", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes) { e.Cancel = true; return; }
            }
            try { if (_coordinator != null && _coordinator.IsActive) _coordinator.Abort("lesson_window_closed"); }
            catch { }
            _finished = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _coordinator != null) _coordinator.Dispose();
            base.Dispose(disposing);
        }
    }
}
