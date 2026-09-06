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
        private readonly AnswerChoiceButton[] _answerButtons = new AnswerChoiceButton[4];
        private Label _progressText;
        private ProgressStrip _progressBar;
        private Label _prompt;
        private MathInstructionVisual _instructionVisual;
        private Label _support;
        private Label _feedback;
        private ChildCard _feedbackCard;
        private ChildActionButton _hintButton;
        private ChildActionButton _nextButton;
        private ChildActionButton _stopButton;
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
            MinimumSize = new Size(900, 640);
            ClientSize = new Size(1080, 720);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = ChildVisualTheme.Font(11f);
            BackColor = ChildVisualTheme.Cream;
            KeyPreview = true;
            DoubleBuffered = true;
            BuildUi();
            Shown += delegate { StartSession(); };
            FormClosing += OnFormClosing;
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = ChildVisualTheme.Cream,
                Padding = new Padding(30, 22, 30, 22),
                ColumnCount = 1,
                RowCount = 6
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            _stopButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 18, 4),
                Text = "Dừng ở đây",
                FillColor = Color.FromArgb(232, 230, 220),
                HoverColor = Color.FromArgb(220, 217, 207),
                PressedColor = Color.FromArgb(207, 204, 193),
                TextColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(10f, FontStyle.Bold),
                Radius = 15,
                AccessibleName = "Dừng buổi học"
            };
            _stopButton.Click += delegate { RequestStop(); };
            header.Controls.Add(_stopButton, 0, 0);
            header.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Nhiệm vụ Toán",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(17f, FontStyle.Bold),
                AccessibleName = "Nhiệm vụ Toán"
            }, 1, 0);
            _progressText = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(10.5f, FontStyle.Bold),
                AccessibleName = "Tiến độ buổi học"
            };
            header.Controls.Add(_progressText, 2, 0);
            root.Controls.Add(header, 0, 0);

            _progressBar = new ProgressStrip
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(158, 2, 170, 7),
                Maximum = MathSessionCoordinator.DefaultTargetQuestionCount
            };
            root.Controls.Add(_progressBar, 0, 1);

            var questionCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(50, 10, 50, 12),
                Padding = new Padding(28, 20, 28, 18),
                CardColor = Color.FromArgb(255, 253, 246),
                Radius = 26
            };
            var questionLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            questionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            questionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            questionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            questionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            questionLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "CHỌN ĐÁP ÁN ĐÚNG",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.MintStrong,
                Font = ChildVisualTheme.Font(9.5f, FontStyle.Bold)
            }, 0, 0);
            _prompt = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(30f, FontStyle.Bold),
                AutoEllipsis = true,
                AccessibleName = "Câu hỏi Toán"
            };
            questionLayout.Controls.Add(_prompt, 0, 1);
            _instructionVisual = new MathInstructionVisual
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(12, 0, 12, 0)
            };
            questionLayout.Controls.Add(_instructionVisual, 0, 2);
            _support = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(11f),
                AccessibleName = "Gợi ý học tập"
            };
            questionLayout.Controls.Add(_support, 0, 3);
            questionCard.Controls.Add(questionLayout);
            root.Controls.Add(questionCard, 0, 2);

            var answerGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(72, 4, 72, 8)
            };
            answerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            answerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            answerGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            answerGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            for (var i = 0; i < _answerButtons.Length; i++)
            {
                var index = i;
                var button = new AnswerChoiceButton
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(9),
                    Font = ChildVisualTheme.Font(21f, FontStyle.Bold),
                    BadgeText = (i + 1).ToString(),
                    AccessibleName = "Đáp án " + (i + 1),
                    TabIndex = i
                };
                button.Click += delegate { SubmitChoice(index, "mouse"); };
                _answerButtons[i] = button;
                answerGrid.Controls.Add(button, i % 2, i / 2);
            }
            root.Controls.Add(answerGrid, 0, 3);

            _feedbackCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(120, 7, 120, 8),
                Padding = new Padding(18, 10, 18, 10),
                CardColor = Color.FromArgb(245, 244, 236),
                BorderColor = Color.FromArgb(229, 227, 217),
                Radius = 18,
                Visible = false
            };
            _feedback = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(12.5f, FontStyle.Bold),
                AccessibleName = "Phản hồi câu trả lời"
            };
            _feedbackCard.Controls.Add(_feedback);
            root.Controls.Add(_feedbackCard, 0, 4);

            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(100, 4, 100, 0) };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            _hintButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8),
                Text = "Gợi ý",
                FillColor = ChildVisualTheme.Peach,
                HoverColor = Color.FromArgb(245, 214, 178),
                PressedColor = Color.FromArgb(236, 200, 159),
                TextColor = Color.FromArgb(120, 79, 44),
                Font = ChildVisualTheme.Font(10.5f, FontStyle.Bold),
                Radius = 16,
                AccessibleName = "Xem gợi ý"
            };
            _hintButton.Click += delegate { ShowHint(); };
            _nextButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8),
                Text = "Câu tiếp theo",
                FillColor = ChildVisualTheme.MintStrong,
                HoverColor = Color.FromArgb(90, 156, 103),
                PressedColor = Color.FromArgb(75, 139, 88),
                Font = ChildVisualTheme.Font(11f, FontStyle.Bold),
                Radius = 16,
                AccessibleName = "Chuyển sang câu tiếp theo",
                Visible = false
            };
            _nextButton.Click += delegate { HandleNextButton(); };
            actions.Controls.Add(new Panel(), 0, 0);
            actions.Controls.Add(_hintButton, 1, 0);
            actions.Controls.Add(_nextButton, 2, 0);
            root.Controls.Add(actions, 0, 5);
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
                    _support.Text = "Buổi trước đã được lưu an toàn. Mình bắt đầu nhiệm vụ mới nhé.";
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
                if (_question == null) { CompleteSession(); return; }
                _hintLevel = 0;
                _submitting = false;
                _prompt.Text = _question.PromptVi;
                _instructionVisual.SetQuestion(_question, 0);
                _support.Text = "Chọn đáp án con thấy đúng nhất.";
                _feedback.Text = string.Empty;
                _feedbackCard.Visible = false;
                _hintButton.Text = "Gợi ý";
                _hintButton.Enabled = true;
                _hintButton.Visible = true;
                _nextButton.Visible = false;
                _completeOnNext = false;
                var summary = _coordinator.Summary;
                var questionNumber = summary.Attempts + 1;
                _progressText.Text = "Câu " + questionNumber + " / " + MathSessionCoordinator.DefaultTargetQuestionCount;
                _progressBar.Value = summary.Attempts;
                for (var i = 0; i < _answerButtons.Length; i++)
                {
                    _answerButtons[i].Text = _question.Choices[i].ToString();
                    _answerButtons[i].BadgeText = (i + 1).ToString();
                    _answerButtons[i].VisualState = AnswerChoiceButton.ChoiceVisualState.Idle;
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
                _instructionVisual.SetQuestion(_question, _hintLevel);
                _support.Text = _question.HintLevel1;
                _hintButton.Text = "Gợi ý thêm";
                return;
            }
            _hintLevel = 2;
            _instructionVisual.SetQuestion(_question, _hintLevel);
            _support.Text = _question.HintLevel2;
            _hintButton.Enabled = false;
            _hintButton.Text = "Đã xem đủ gợi ý";
        }

        private void SubmitChoice(int index, string inputMode)
        {
            if (_question == null || _submitting || index < 0 || index >= _answerButtons.Length) return;
            _submitting = true;
            SetAnswersEnabled(false);
            _hintButton.Enabled = false;
            try
            {
                var selected = _question.Choices[index];
                var outcome = _coordinator.SubmitAnswer(selected, _hintLevel, inputMode);
                var correctIndex = FindCorrectChoiceIndex();
                for (var i = 0; i < _answerButtons.Length; i++)
                {
                    if (i == correctIndex)
                    {
                        _answerButtons[i].VisualState = AnswerChoiceButton.ChoiceVisualState.Correct;
                        _answerButtons[i].BadgeText = "✓";
                    }
                    else if (i == index && !outcome.IsCorrect)
                    {
                        _answerButtons[i].VisualState = AnswerChoiceButton.ChoiceVisualState.Incorrect;
                        _answerButtons[i].BadgeText = "×";
                    }
                    else
                    {
                        _answerButtons[i].VisualState = AnswerChoiceButton.ChoiceVisualState.Muted;
                        _answerButtons[i].BadgeText = string.Empty;
                    }
                }
                _feedback.Text = outcome.FeedbackVi;
                _feedbackCard.CardColor = outcome.IsCorrect ? Color.FromArgb(226, 242, 224) : Color.FromArgb(251, 232, 222);
                _feedbackCard.BorderColor = outcome.IsCorrect ? Color.FromArgb(190, 221, 188) : Color.FromArgb(236, 202, 187);
                _feedbackCard.Visible = true;
                _support.Text = outcome.IsCorrect
                    ? "Tốt rồi. Câu này đã được lưu để lần sau ôn đúng lúc."
                    : "Mình đã chỉ ra đáp án đúng. Câu sau sẽ giúp con luyện tiếp phần này.";
                _progressText.Text = "Đã làm " + outcome.CompletedQuestionCount + " / " + outcome.TargetQuestionCount;
                _progressBar.Value = outcome.CompletedQuestionCount;
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

        private int FindCorrectChoiceIndex()
        {
            if (_question == null || _question.Choices == null) return -1;
            for (var i = 0; i < _question.Choices.Count; i++)
                if (_question.Choices[i] == _question.CorrectAnswer) return i;
            return -1;
        }

        private void HandleNextButton()
        {
            if (_finished) { DialogResult = DialogResult.OK; Close(); return; }
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
            _instructionVisual.SetQuestion(null, 0);
            _prompt.Text = "Hoàn thành nhiệm vụ";
            _support.Text = summary == null
                ? "Các câu đã làm được lưu để lần sau tiếp tục đúng chỗ."
                : "Khu vườn vừa lớn thêm một chút. Con đã luyện " + summary.DistinctSkills + " kỹ năng."
                    + (string.IsNullOrWhiteSpace(summary.GardenUnlockMessage) ? string.Empty : " " + summary.GardenUnlockMessage);
            _feedback.Text = summary == null ? "Mình về màn hình chính nhé." :
                "Đã làm " + summary.Attempts + " câu · Tự làm đúng " + Math.Max(0, summary.Correct - summary.HintedCorrect) + " câu";
            _feedbackCard.CardColor = Color.FromArgb(226, 242, 224);
            _feedbackCard.BorderColor = Color.FromArgb(190, 221, 188);
            _feedbackCard.Visible = true;
            foreach (var button in _answerButtons) button.Visible = false;
            _hintButton.Visible = false;
            _stopButton.Visible = false;
            _nextButton.Visible = true;
            _nextButton.Text = "Về khu vườn";
            _completeOnNext = false;
            _progressText.Text = "Hoàn thành";
            _progressBar.Value = _progressBar.Maximum;
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
            _instructionVisual.SetQuestion(null, 0);
            _prompt.Text = "Mình dừng ở đây nhé";
            _support.Text = message;
            _feedback.Text = "Những dữ liệu đã lưu trước đó vẫn an toàn.";
            _feedbackCard.CardColor = Color.FromArgb(245, 239, 224);
            _feedbackCard.Visible = true;
            foreach (var button in _answerButtons) button.Visible = false;
            _hintButton.Visible = false;
            _stopButton.Visible = false;
            _nextButton.Visible = true;
            _nextButton.Text = "Về khu vườn";
            _completeOnNext = false;
            _nextButton.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData >= Keys.D1 && keyData <= Keys.D4)
            {
                var index = (int)keyData - (int)Keys.D1;
                if (_answerButtons[index].Visible) SubmitChoice(index, "keyboard");
                return true;
            }
            if (keyData >= Keys.NumPad1 && keyData <= Keys.NumPad4)
            {
                var index = (int)keyData - (int)Keys.NumPad1;
                if (_answerButtons[index].Visible) SubmitChoice(index, "keyboard");
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
