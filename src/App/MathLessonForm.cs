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
        private readonly string _targetLessonId;
        private MathQuickRescueEventPresentation _eventPresentation;
        private MathGameEventCoordinator _gameEventCoordinator;
        private MathGameEventState _eventState;
        private DateTime _questionShownAtUtc;
        private MathSessionCoordinator _coordinator;
        private MathQuestion _question;
        private readonly AnswerChoiceButton[] _answerButtons = new AnswerChoiceButton[4];
        private TableLayoutPanel _answerGrid;
        private TableLayoutPanel _typedAnswerLayout;
        private TextBox _typedAnswerBox;
        private ChildActionButton _typedSubmitButton;
        private TableLayoutPanel _interactiveAnswerLayout;
        private SegmentDrawingAnswerControl _interactiveAnswer;
        private ChildActionButton _interactiveSubmitButton;
        private Label _sessionTitle;
        private Label _progressText;
        private ProgressStrip _progressBar;
        private QuickRescueCheckpointStrip _eventCheckpointStrip;
        private Label _prompt;
        private MathInstructionVisual _instructionVisual;
        private LessonCompletionVisual _completionVisual;
        private Label _support;
        private Label _feedback;
        private ChildCard _feedbackCard;
        private CompanionReactionControl _companion;
        private ChildActionButton _hintButton;
        private ChildActionButton _nextButton;
        private ChildActionButton _stopButton;
        private int _hintLevel;
        private int _targetQuestionCount = MathSessionCoordinator.DefaultTargetQuestionCount;
        private string _sessionNotice;
        private bool _finished;
        private bool _submitting;
        private bool _retryPending;
        private bool _completeOnNext;

        public MathLessonForm(LearningDatabase database, RuntimePerformanceSettings performance)
            : this(database, performance, null, null)
        {
        }

        internal MathLessonForm(LearningDatabase database, RuntimePerformanceSettings performance, string targetLessonId)
            : this(database, performance, targetLessonId, null)
        {
        }

        internal MathLessonForm(LearningDatabase database, RuntimePerformanceSettings performance, string targetLessonId,
            MathQuickRescueEventPresentation eventPresentation)
        {
            _database = database ?? throw new ArgumentNullException("database");
            _performance = performance;
            _targetLessonId = string.IsNullOrWhiteSpace(targetLessonId) ? null : targetLessonId.Trim();
            _eventPresentation = eventPresentation;
            Text = _eventPresentation == null ? "WAHU Kids Learn — Toán lớp 2" : "WAHU Kids Learn — " + _eventPresentation.TitleVi;
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
            Resize += delegate
            {
                if (!_finished && _question != null && _prompt != null) ApplyPromptTypography(_question.PromptVi);
            };
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
                Text = _eventPresentation == null ? "Dừng và học tiếp sau" : "Nghỉ ở đây",
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
            _sessionTitle = new Label
            {
                Dock = DockStyle.Fill,
                Text = _eventPresentation == null ? "Nhiệm vụ Toán" : "Nhiệm vụ cứu hộ",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(17f, FontStyle.Bold),
                AccessibleName = _eventPresentation == null ? "Nhiệm vụ Toán" : "Nhiệm vụ cứu hộ"
            };
            header.Controls.Add(_sessionTitle, 1, 0);
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

            var progressHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _progressBar = new ProgressStrip
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(158, 2, 170, 7),
                Maximum = MathSessionCoordinator.DefaultTargetQuestionCount,
                Visible = _eventPresentation == null
            };
            progressHost.Controls.Add(_progressBar);
            _eventCheckpointStrip = new QuickRescueCheckpointStrip
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(158, 0, 170, 2),
                Visible = _eventPresentation != null
            };
            if (_eventPresentation != null)
                _eventCheckpointStrip.SetState(0, 0, _eventPresentation.CheckpointNounsVi);
            progressHost.Controls.Add(_eventCheckpointStrip);
            _eventCheckpointStrip.BringToFront();
            root.Controls.Add(progressHost, 0, 1);

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
            var visualHost = new Panel { Dock = DockStyle.Fill, Margin = new Padding(12, 0, 12, 0), BackColor = Color.Transparent };
            _instructionVisual = new MathInstructionVisual { Dock = DockStyle.Fill };
            _completionVisual = new LessonCompletionVisual { Dock = DockStyle.Fill, Visible = false };
            visualHost.Controls.Add(_completionVisual);
            visualHost.Controls.Add(_instructionVisual);
            questionLayout.Controls.Add(visualHost, 0, 2);
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

            _answerGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(72, 4, 72, 8)
            };
            _answerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _answerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _answerGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            _answerGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
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
                _answerGrid.Controls.Add(button, i % 2, i / 2);
            }
            var answerHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            answerHost.Controls.Add(_answerGrid);

            _typedAnswerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Padding = new Padding(72, 16, 72, 12),
                Visible = false
            };
            _typedAnswerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
            _typedAnswerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
            _typedAnswerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
            _typedAnswerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            _typedAnswerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            _typedAnswerBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 12, 8, 10),
                Font = ChildVisualTheme.Font(22f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                MaxLength = 80,
                AccessibleName = "Nhập đáp án Toán",
                AccessibleDescription = "Nhập đáp án rồi nhấn Enter hoặc nút Kiểm tra đáp án."
            };
            _typedAnswerBox.TextChanged += delegate
            {
                if (_typedSubmitButton != null)
                    _typedSubmitButton.Enabled = !_submitting && !string.IsNullOrWhiteSpace(_typedAnswerBox.Text);
            };
            _typedSubmitButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 5, 8, 3),
                Text = "Kiểm tra đáp án",
                FillColor = ChildVisualTheme.MintStrong,
                HoverColor = Color.FromArgb(90, 156, 103),
                PressedColor = Color.FromArgb(75, 139, 88),
                Font = ChildVisualTheme.Font(10.5f, FontStyle.Bold),
                Radius = 16,
                AccessibleName = "Kiểm tra đáp án đã nhập",
                Enabled = false
            };
            _typedSubmitButton.Click += delegate { SubmitTypedAnswer("mouse"); };
            _typedAnswerLayout.Controls.Add(_typedAnswerBox, 1, 0);
            _typedAnswerLayout.Controls.Add(_typedSubmitButton, 1, 1);
            answerHost.Controls.Add(_typedAnswerLayout);
            _interactiveAnswerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Padding = new Padding(72, 4, 72, 8),
                Visible = false
            };
            _interactiveAnswerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            _interactiveAnswerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            _interactiveAnswerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            _interactiveAnswerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _interactiveAnswerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            _interactiveAnswer = new SegmentDrawingAnswerControl { Dock = DockStyle.Fill, Margin = new Padding(8) };
            _interactiveAnswer.AnswerChanged += delegate
            {
                if (_interactiveSubmitButton != null)
                    _interactiveSubmitButton.Enabled = !_submitting && _interactiveAnswer.HasAnswer;
            };
            _interactiveSubmitButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 5, 8, 3),
                Text = "Kiểm tra đoạn thẳng",
                FillColor = ChildVisualTheme.MintStrong,
                HoverColor = Color.FromArgb(90, 156, 103),
                PressedColor = Color.FromArgb(75, 139, 88),
                Font = ChildVisualTheme.Font(10.5f, FontStyle.Bold),
                Radius = 16,
                AccessibleName = "Kiểm tra đoạn thẳng đã vẽ",
                Enabled = false
            };
            _interactiveSubmitButton.Click += delegate { SubmitInteractiveAnswer("mouse"); };
            _interactiveAnswerLayout.Controls.Add(_interactiveAnswer, 0, 0);
            _interactiveAnswerLayout.SetColumnSpan(_interactiveAnswer, 3);
            _interactiveAnswerLayout.Controls.Add(_interactiveSubmitButton, 1, 1);
            answerHost.Controls.Add(_interactiveAnswerLayout);
            root.Controls.Add(answerHost, 0, 3);

            _feedbackCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(120, 3, 120, 3),
                Padding = new Padding(18, 6, 18, 6),
                CardColor = Color.FromArgb(245, 244, 236),
                BorderColor = Color.FromArgb(229, 227, 217),
                Radius = 18,
                Visible = false
            };
            var feedbackLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            feedbackLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            feedbackLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _companion = new CompanionReactionControl { Dock = DockStyle.Fill, Margin = new Padding(0) };
            _feedback = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(12.5f, FontStyle.Bold),
                Padding = new Padding(4, 0, 0, 0),
                AccessibleName = "Phản hồi câu trả lời"
            };
            feedbackLayout.Controls.Add(_companion, 0, 0);
            feedbackLayout.Controls.Add(_feedback, 1, 0);
            _feedbackCard.Controls.Add(feedbackLayout);
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
                MathSessionStartResult started;
                if (_eventPresentation != null)
                {
                    var eventPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        "content_packs", "math_grade2_v1", "game_events_v1.json");
                    _gameEventCoordinator = new MathGameEventCoordinator(_database, templatePath, eventPath, profile, seed,
                        _eventPresentation.Id, _targetLessonId);
                    var eventStarted = _gameEventCoordinator.Start("Bé học");
                    _coordinator = _gameEventCoordinator.LearningSession;
                    _eventState = eventStarted.EventState;
                    started = eventStarted.Session;
                    if (eventStarted.Event == null || _eventState == null || _eventState.FallbackToLessonPresentation)
                    {
                        _eventPresentation = null;
                        ApplyLessonFallbackPresentation(started);
                    }
                    else
                    {
                        _eventPresentation = MathQuickRescueEventPresentation.FromDefinition(eventStarted.Event);
                    }
                }
                else
                {
                    _coordinator = string.IsNullOrWhiteSpace(_targetLessonId)
                        ? new MathSessionCoordinator(_database, templatePath, profile, seed)
                        : new MathSessionCoordinator(_database, templatePath, profile, seed, _targetLessonId);
                    started = _coordinator.Start("Bé học");
                }
                if (_eventPresentation != null)
                    Text = "WAHU Kids Learn — " + _eventPresentation.TitleVi;
                else if (string.Equals(started.SessionMode, "lesson", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(started.TargetLessonTitleVi))
                    Text = "WAHU Kids Learn — " + started.TargetLessonTitleVi;
                _targetQuestionCount = Math.Max(1, started.TargetQuestionCount);
                _progressBar.Maximum = _targetQuestionCount;
                _progressBar.Value = Math.Min(_targetQuestionCount, Math.Max(0, started.CompletedQuestionCount));
                _retryPending = started.RetryPending;
                _sessionNotice = _eventPresentation == null
                    ? BuildSessionStartNotice(started)
                    : BuildEventSessionStartNotice(started, _eventState);
                ShowNextQuestion();
            }
            catch
            {
                ShowFatalChildMessage("Chưa thể bắt đầu buổi Toán lúc này. Nhờ người lớn mở mục Phụ huynh để kiểm tra nhé.");
            }
        }

        private void ApplyLessonFallbackPresentation(MathSessionStartResult started)
        {
            _sessionTitle.Text = "Nhiệm vụ Toán";
            _sessionTitle.AccessibleName = "Nhiệm vụ Toán";
            _stopButton.Text = "Dừng và học tiếp sau";
            _progressBar.Visible = true;
            _eventCheckpointStrip.Visible = false;
            if (started != null && string.Equals(started.SessionMode, "lesson", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(started.TargetLessonTitleVi))
                Text = "WAHU Kids Learn — " + started.TargetLessonTitleVi;
            else
                Text = "WAHU Kids Learn — Toán lớp 2";
        }

        private void ApplyPromptTypography(string promptVi)
        {
            if (_prompt == null) return;

            var text = promptVi ?? string.Empty;
            _prompt.Text = text;
            var width = _prompt.ClientSize.Width > 0
                ? _prompt.ClientSize.Width
                : Math.Max(320, ClientSize.Width - 216);
            var height = _prompt.ClientSize.Height > 0 ? _prompt.ClientSize.Height : 68;
            var candidateSizes = new[] { 30f, 26f, 22f, 18f, 16f, 14f, 12f };
            var chosenSize = 12f;

            foreach (var candidateSize in candidateSizes)
            {
                using (var candidateFont = ChildVisualTheme.Font(candidateSize, FontStyle.Bold))
                {
                    var measured = TextRenderer.MeasureText(
                        text,
                        candidateFont,
                        new Size(Math.Max(120, width - 4), 4096),
                        TextFormatFlags.WordBreak);
                    if (measured.Height <= Math.Max(20, height - 4))
                    {
                        chosenSize = candidateSize;
                        break;
                    }
                }
            }

            _prompt.Font = ChildVisualTheme.Font(chosenSize, FontStyle.Bold);
        }

        private void ShowNextQuestion()
        {
            if (_finished || _coordinator == null || !_coordinator.IsActive) return;
            try
            {
                _question = _coordinator.NextQuestion();
                if (_question == null) { CompleteSession(); return; }
                _questionShownAtUtc = DateTime.UtcNow;
                if (_gameEventCoordinator != null) _eventState = _gameEventCoordinator.CurrentState;
                _hintLevel = 0;
                _submitting = false;
                ApplyPromptTypography(_question.PromptVi);
                _completionVisual.Visible = false;
                _instructionVisual.Visible = true;
                _instructionVisual.SetQuestion(_question, 0);
                _companion.State = CompanionReactionState.Calm;
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
                UpdateProgressPresentation(summary.Attempts, questionNumber, _targetQuestionCount, _retryPending, false);
                ConfigureAnswerInput(_question);
                _support.Text = BuildEventQuestionSupport(_support.Text, questionNumber);
                if (!string.IsNullOrWhiteSpace(_sessionNotice))
                {
                    var inputSupport = _support.Text;
                    _support.Text = UsesInteractiveAnswer(_question)
                        ? _sessionNotice + " " + inputSupport
                        : _sessionNotice;
                    _sessionNotice = null;
                }
            }
            catch
            {
                FailCurrentSession("Không thể mở câu tiếp theo. Những câu con đã làm vẫn được giữ lại.");
            }
        }

        private void UpdateProgressPresentation(int completed, int questionNumber, int targetCount, bool retry, bool afterAnswer)
        {
            var target = Math.Max(1, targetCount);
            var safeCompleted = Math.Max(0, Math.Min(target, completed));
            _progressBar.Maximum = target;
            _progressBar.Value = safeCompleted;
            if (_eventPresentation == null)
            {
                _progressText.Text = afterAnswer
                    ? "Đã làm " + safeCompleted + " / " + target
                    : "Câu " + Math.Max(1, questionNumber) + " / " + target + (retry ? " · thử lại" : string.Empty);
                return;
            }

            var activeIndex = safeCompleted >= target ? target - 1 : Math.Max(0, Math.Min(target - 1, questionNumber - 1));
            _eventCheckpointStrip.SetState(safeCompleted, activeIndex, _eventPresentation.CheckpointNounsVi);
            var nounIndex = afterAnswer ? Math.Max(0, safeCompleted - 1) : activeIndex;
            var noun = _eventPresentation.CheckpointName(nounIndex);
            _progressText.Text = safeCompleted >= target
                ? target + " / " + target + " chặng đã xong"
                : (afterAnswer
                    ? "Đã xong " + safeCompleted + " / " + target + " · " + noun
                    : "Chặng " + (activeIndex + 1) + " / " + target + " · " + noun + (retry ? " · thử lại" : string.Empty));
        }

        private string BuildEventQuestionSupport(string normalSupport, int questionNumber)
        {
            if (_eventPresentation == null) return normalSupport;
            return "Chặng " + Math.Max(1, questionNumber) + " — " +
                _eventPresentation.CheckpointName(Math.Max(0, questionNumber - 1)) + ". " + normalSupport;
        }

        private string BuildOutcomeSupport(MathAnswerOutcome outcome, string fallback)
        {
            if (_eventPresentation == null || outcome == null) return fallback;
            var checkpoint = _eventPresentation.CheckpointName(Math.Max(0, outcome.CompletedQuestionCount - 1));
            var action = _eventState == null || _eventState.Action == null
                ? MathGameEventBehaviorMapper.Map(outcome.Behavior)
                : _eventState.Action;
            if (outcome.SuggestPositiveEnd || action.SuggestPositiveClose)
                return _eventPresentation.BreakCopyVi;
            if (action.MinimizeInterruptions && outcome.IsCorrect)
                return "Đã xong " + checkpoint + ".";
            if (action.UseSmallCue)
                return "Mình làm từng bước nhé. " + (outcome.IsCorrect ? "Đã xong " + checkpoint + "." : _eventPresentation.RepairCopyVi);
            if (action.UseRepair)
                return _eventPresentation.RepairCopyVi + (outcome.IsCorrect ? " " + checkpoint + " đã ổn rồi." : string.Empty);
            if (action.OfferBreak || outcome.OfferBreak)
                return (outcome.IsCorrect ? "Đã xong " + checkpoint + ". " : string.Empty) +
                    "Nếu muốn, con có thể chọn Nghỉ ở đây. Phần đã làm được lưu rồi.";
            return outcome.IsCorrect
                ? "Đã sửa xong " + checkpoint + ". Phần này đã được lưu."
                : _eventPresentation.RepairCopyVi + " " + fallback;
        }

        private string BuildEventSessionStartNotice(MathSessionStartResult started, MathGameEventState state)
        {
            if (started == null || _eventPresentation == null) return null;
            var checkpoint = state == null || string.IsNullOrWhiteSpace(state.CurrentCheckpointNounVi)
                ? _eventPresentation.CheckpointName(Math.Max(0, started.CompletedQuestionCount))
                : state.CurrentCheckpointNounVi;
            if (started.ResumedExistingSession)
            {
                if (started.RetryPending && started.RestoredOpenQuestion)
                    return "Mình quay lại đúng " + checkpoint + " đang thử lại nhé. Phần trước đã lưu; con có thể xem gợi ý rồi sửa tiếp.";
                if (started.RestoredOpenQuestion)
                    return "Mình quay lại đúng " + checkpoint + " con đang làm dở nhé. Phần trước đã được lưu.";
                if (started.DiscardedCorruptOpenQuestion)
                    return "Phần đã làm vẫn an toàn. Chặng đang mở cần làm mới nên mình tiếp tục từ " + checkpoint + " nhé.";
                return started.CompletedQuestionCount > 0
                    ? "Mình tiếp tục nhiệm vụ cứu hộ từ " + checkpoint + " nhé. Phần trước đã được lưu."
                    : "Nhiệm vụ trước vẫn còn. Mình tiếp tục từ chặng đầu nhé.";
            }
            if (started.RecoveredDanglingSessions > 0)
                return "Phần đã lưu vẫn an toàn. Mình bắt đầu lại ba chặng cứu hộ từ đầu nhé.";
            return "Nhiệm vụ này có ba chặng nhỏ. Không có giới hạn thời gian; mình làm lần lượt nhé.";
        }

        private static string BuildSessionStartNotice(MathSessionStartResult started)
        {
            if (started == null) return null;
            if (started.ResumedExistingSession)
            {
                if (started.RetryPending && started.RestoredOpenQuestion)
                    return "Mình tiếp tục lần thử lại của đúng câu này nhé. Con có thể xem gợi ý rồi sửa đáp án.";
                if (started.RestoredOpenQuestion)
                    return "Mình tiếp tục đúng câu con đang làm dở nhé.";
                if (started.DiscardedCorruptOpenQuestion)
                    return "Phần con đã làm vẫn an toàn. Câu đang mở bị lỗi nên mình tiếp tục bằng câu mới nhé.";
                return started.CompletedQuestionCount > 0
                    ? "Mình tiếp tục buổi Toán đang học dở nhé."
                    : "Buổi Toán trước vẫn còn. Mình tiếp tục từ đây nhé.";
            }
            if (started.RecoveredDanglingSessions > 0)
            {
                if (string.Equals(started.SessionMode, "lesson", StringComparison.Ordinal))
                    return "Phiên học trước cần được làm mới. Phần đã lưu vẫn được giữ; bài này có " +
                        Math.Max(1, started.TargetQuestionCount) + " câu luyện tập, mình bắt đầu lượt mới nhé.";
                return "Buổi trước đã được lưu an toàn. Mình bắt đầu nhiệm vụ mới nhé.";
            }
            if (string.Equals(started.SessionMode, "lesson", StringComparison.Ordinal))
                return "Bài này có " + Math.Max(1, started.TargetQuestionCount) + " câu luyện tập. Mình làm lần lượt nhé.";
            return null;
        }

        private static bool UsesInteractiveAnswer(MathQuestion question)
        {
            return question != null && string.Equals(question.AnswerKind, "interaction_integer", StringComparison.Ordinal);
        }

        private static bool UsesTypedAnswer(MathQuestion question)
        {
            if (question == null || UsesInteractiveAnswer(question)) return false;
            var choices = question.DisplayChoices;
            return choices == null || choices.Count == 0;
        }

        private static string TypedAnswerSupport(MathQuestion question)
        {
            if (question == null) return "Nhập đáp án rồi bấm Kiểm tra đáp án.";
            if (string.Equals(question.AnswerKind, "unit", StringComparison.Ordinal))
                return "Nhập kết quả kèm đơn vị, ví dụ: 5 kg.";
            if (string.Equals(question.AnswerKind, "expression", StringComparison.Ordinal))
                return "Nhập kết quả hoặc một biểu thức số tương đương.";
            return "Nhập đáp án rồi bấm Kiểm tra đáp án.";
        }

        private void ConfigureAnswerInput(MathQuestion question)
        {
            if (question == null) throw new ArgumentNullException("question");
            if (UsesInteractiveAnswer(question))
            {
                _answerGrid.Visible = false;
                _typedAnswerLayout.Visible = false;
                _interactiveAnswerLayout.Visible = true;
                _interactiveAnswer.SetQuestion(question);
                _interactiveAnswer.SetHintLevel(0);
                _interactiveSubmitButton.Text = "Kiểm tra đoạn thẳng";
                _interactiveSubmitButton.Enabled = false;
                _support.Text = "Chọn điểm A rồi điểm B trên thước. Hai đầu mút có thể nằm ở bất kỳ vạch nào.";
                _interactiveAnswer.Focus();
                return;
            }

            if (UsesTypedAnswer(question))
            {
                _answerGrid.Visible = false;
                _interactiveAnswerLayout.Visible = false;
                _typedAnswerLayout.Visible = true;
                _typedAnswerBox.Text = string.Empty;
                _typedAnswerBox.Enabled = true;
                _typedSubmitButton.Enabled = false;
                _support.Text = TypedAnswerSupport(question);
                _typedAnswerBox.AccessibleDescription = _support.Text + " Nhấn Enter để kiểm tra.";
                _typedAnswerBox.Focus();
                return;
            }

            _typedAnswerLayout.Visible = false;
            _interactiveAnswerLayout.Visible = false;
            _answerGrid.Visible = true;
            var displayChoices = question.DisplayChoices;
            if (displayChoices == null || displayChoices.Count < 2 || displayChoices.Count > _answerButtons.Length)
                throw new InvalidDataException("Math choice question must expose 2 to 4 display choices.");
            ConfigureAnswerLayout(displayChoices.Count);
            for (var i = 0; i < _answerButtons.Length; i++)
            {
                var visible = i < displayChoices.Count;
                _answerButtons[i].Visible = visible;
                if (!visible) continue;
                var choiceText = displayChoices[i] ?? string.Empty;
                _answerButtons[i].Text = choiceText;
                _answerButtons[i].Font = ChildVisualTheme.Font(choiceText.Length > 22 ? 10.5f : (choiceText.Length > 10 ? 13f : 21f), FontStyle.Bold);
                _answerButtons[i].AccessibleDescription = "Lựa chọn: " + choiceText;
                _answerButtons[i].BadgeText = (i + 1).ToString();
                _answerButtons[i].VisualState = AnswerChoiceButton.ChoiceVisualState.Idle;
                _answerButtons[i].Enabled = true;
            }
            _answerButtons[0].Focus();
        }

        private void ConfigureAnswerLayout(int choiceCount)
        {
            if (_answerGrid == null) return;
            _answerGrid.SuspendLayout();
            try
            {
                _answerGrid.SetColumnSpan(_answerButtons[0], 1);
                _answerGrid.SetColumnSpan(_answerButtons[1], 1);
                _answerGrid.SetColumnSpan(_answerButtons[2], 1);
                _answerGrid.SetColumnSpan(_answerButtons[3], 1);

                if (choiceCount == 2)
                {
                    _answerGrid.RowStyles[0].Height = 100f;
                    _answerGrid.RowStyles[1].Height = 0f;
                    _answerGrid.SetCellPosition(_answerButtons[0], new TableLayoutPanelCellPosition(0, 0));
                    _answerGrid.SetCellPosition(_answerButtons[1], new TableLayoutPanelCellPosition(1, 0));
                    _answerButtons[0].Margin = new Padding(9, 16, 9, 16);
                    _answerButtons[1].Margin = new Padding(9, 16, 9, 16);
                }
                else
                {
                    _answerGrid.RowStyles[0].Height = 50f;
                    _answerGrid.RowStyles[1].Height = 50f;
                    _answerGrid.SetCellPosition(_answerButtons[0], new TableLayoutPanelCellPosition(0, 0));
                    _answerGrid.SetCellPosition(_answerButtons[1], new TableLayoutPanelCellPosition(1, 0));
                    _answerGrid.SetCellPosition(_answerButtons[2], new TableLayoutPanelCellPosition(0, 1));
                    _answerGrid.SetCellPosition(_answerButtons[3], new TableLayoutPanelCellPosition(1, 1));
                    for (var i = 0; i < _answerButtons.Length; i++) _answerButtons[i].Margin = new Padding(9);
                    if (choiceCount == 3) _answerGrid.SetColumnSpan(_answerButtons[2], 2);
                }
            }
            finally { _answerGrid.ResumeLayout(true); }
        }

        private void ShowHint()
        {
            if (_question == null || _submitting || !_hintButton.Enabled) return;
            if (_hintLevel == 0)
            {
                _hintLevel = 1;
                _instructionVisual.SetQuestion(_question, _hintLevel);
                if (UsesInteractiveAnswer(_question)) _interactiveAnswer.SetHintLevel(_hintLevel);
                _support.Text = BuildEventQuestionSupport(_question.HintLevel1,
                    _coordinator == null ? 1 : Math.Max(1, _coordinator.Summary.Attempts + 1));
                _hintButton.Text = "Gợi ý thêm";
                return;
            }
            _hintLevel = 2;
            _instructionVisual.SetQuestion(_question, _hintLevel);
            if (UsesInteractiveAnswer(_question)) _interactiveAnswer.SetHintLevel(_hintLevel);
            _support.Text = BuildEventQuestionSupport(_question.HintLevel2,
                _coordinator == null ? 1 : Math.Max(1, _coordinator.Summary.Attempts + 1));
            _hintButton.Enabled = false;
            _hintButton.Text = "Đã xem đủ gợi ý";
        }

        private MathAnswerOutcome SubmitCurrentAnswer(string answer, string inputMode)
        {
            if (_coordinator == null) throw new InvalidOperationException("Math session is not ready.");
            if (_gameEventCoordinator == null)
                return _retryPending
                    ? _coordinator.SubmitRetryAnswer(answer, _hintLevel, inputMode)
                    : _coordinator.SubmitAnswerWithRetry(answer, _hintLevel, inputMode);

            var answeredAtUtc = DateTime.UtcNow;
            var responseMs = (int)Math.Min(int.MaxValue, Math.Max(0,
                (answeredAtUtc - (_questionShownAtUtc == default(DateTime) ? answeredAtUtc : _questionShownAtUtc)).TotalMilliseconds));
            var eventResult = _retryPending
                ? _gameEventCoordinator.SubmitRetryAnswerAt(answer, _hintLevel, inputMode, answeredAtUtc, responseMs)
                : _gameEventCoordinator.SubmitAnswerWithRetryAt(answer, _hintLevel, inputMode, answeredAtUtc, responseMs);
            _eventState = eventResult.EventState;
            return eventResult.Learning;
        }

        private bool PrepareRetry(MathAnswerOutcome outcome)
        {
            if (outcome == null || outcome.QuestionCompleted || !outcome.CanRetry) return false;
            _retryPending = true;
            _submitting = false;
            _feedback.Text = outcome.FeedbackVi;
            _companion.State = CompanionReactionState.TryAgain;
            _feedbackCard.CardColor = Color.FromArgb(251, 232, 222);
            _feedbackCard.BorderColor = Color.FromArgb(236, 202, 187);
            _feedbackCard.Visible = true;
            _support.Text = _eventPresentation == null
                ? "Con còn một lần thử ở chính câu này. Có thể xem gợi ý rồi sửa đáp án nhé."
                : _eventPresentation.RepairCopyVi + " Con còn một lần thử ở chính chặng này; có thể xem gợi ý rồi sửa tiếp nhé.";
            UpdateProgressPresentation(outcome.CompletedQuestionCount, outcome.CompletedQuestionCount + 1,
                outcome.TargetQuestionCount, true, false);
            _hintButton.Visible = true;
            _hintButton.Enabled = _hintLevel < 2;
            _nextButton.Visible = false;
            _completeOnNext = false;
            return true;
        }

        private bool TryRecoverAfterSubmitFailure()
        {
            try
            {
                if (_coordinator == null || !_coordinator.IsActive || !_coordinator.HasOpenQuestion || _question == null)
                    return false;
                var restored = _coordinator.NextQuestion();
                if (restored == null || !string.Equals(restored.QuestionId, _question.QuestionId, StringComparison.Ordinal))
                    return false;

                _question = restored;
                _submitting = false;
                _completeOnNext = false;
                var summary = _coordinator.Summary;
                UpdateProgressPresentation(Math.Max(0, summary.Attempts), Math.Max(0, summary.Attempts) + 1,
                    _targetQuestionCount, _retryPending, false);
                _companion.State = CompanionReactionState.TryAgain;
                _feedback.Text = "Chưa lưu được câu này. Mình thử lại chính câu này nhé.";
                _feedbackCard.CardColor = Color.FromArgb(245, 239, 224);
                _feedbackCard.BorderColor = Color.FromArgb(225, 210, 171);
                _feedbackCard.Visible = true;
                _support.Text = _eventPresentation == null
                    ? (_retryPending
                        ? "Phần đã làm trước đó vẫn an toàn. Lần thử lại này chưa được tính; con thử lưu lại nhé."
                        : "Phần đã làm trước đó vẫn an toàn. Câu này chưa được tính; con thử lại nhé.")
                    : _eventPresentation.RepairCopyVi + " Phần đã làm trước đó vẫn an toàn; chặng này chưa được tính.";
                _hintButton.Visible = true;
                _hintButton.Enabled = _hintLevel < 2;
                _nextButton.Visible = false;

                if (UsesTypedAnswer(_question))
                {
                    _typedAnswerBox.Enabled = true;
                    _typedSubmitButton.Enabled = !string.IsNullOrWhiteSpace(_typedAnswerBox.Text);
                    _typedAnswerBox.AccessibleDescription = "Câu chưa lưu được. Sửa nếu cần rồi nhấn Enter hoặc nút Kiểm tra đáp án để thử lại.";
                    _typedAnswerBox.SelectAll();
                    _typedAnswerBox.Focus();
                    return true;
                }

                if (UsesInteractiveAnswer(_question))
                {
                    _interactiveSubmitButton.Enabled = _interactiveAnswer.HasAnswer;
                    _interactiveAnswer.Focus();
                    return true;
                }

                var choices = _question.DisplayChoices;
                if (choices == null || choices.Count < 2 || choices.Count > _answerButtons.Length) return false;
                for (var i = 0; i < _answerButtons.Length; i++)
                {
                    var active = i < choices.Count;
                    _answerButtons[i].Enabled = active;
                    if (!active) continue;
                    _answerButtons[i].VisualState = AnswerChoiceButton.ChoiceVisualState.Idle;
                    _answerButtons[i].BadgeText = (i + 1).ToString();
                }
                _answerButtons[0].Focus();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void SubmitTypedAnswer(string inputMode)
        {
            if (_question == null || !UsesTypedAnswer(_question) || _submitting || string.IsNullOrWhiteSpace(_typedAnswerBox.Text)) return;
            _submitting = true;
            _typedAnswerBox.Enabled = false;
            _typedSubmitButton.Enabled = false;
            _hintButton.Enabled = false;
            try
            {
                var outcome = SubmitCurrentAnswer(_typedAnswerBox.Text.Trim(), inputMode);
                if (PrepareRetry(outcome))
                {
                    _typedAnswerBox.Enabled = true;
                    _typedSubmitButton.Enabled = !string.IsNullOrWhiteSpace(_typedAnswerBox.Text);
                    _typedAnswerBox.AccessibleDescription = "Lần thử lại. Sửa đáp án rồi nhấn Enter hoặc nút Kiểm tra đáp án.";
                    _typedAnswerBox.SelectAll();
                    _typedAnswerBox.Focus();
                    return;
                }
                _retryPending = false;
                _feedback.Text = outcome.FeedbackVi;
                _companion.State = outcome.SuggestPositiveEnd ? CompanionReactionState.Tired :
                    (outcome.IsCorrect ? CompanionReactionState.Correct : CompanionReactionState.TryAgain);
                _feedbackCard.CardColor = outcome.IsCorrect ? Color.FromArgb(226, 242, 224) : Color.FromArgb(251, 232, 222);
                _feedbackCard.BorderColor = outcome.IsCorrect ? Color.FromArgb(190, 221, 188) : Color.FromArgb(236, 202, 187);
                _feedbackCard.Visible = true;
                var typedSupport = outcome.IsCorrect
                    ? "Tốt rồi. Câu trả lời này đã được lưu để lần sau ôn đúng lúc."
                    : "Đáp án đúng: " + outcome.CorrectAnswerDisplay + ". Câu sau sẽ giúp con luyện tiếp phần này.";
                _support.Text = BuildOutcomeSupport(outcome, typedSupport);
                UpdateProgressPresentation(outcome.CompletedQuestionCount, outcome.CompletedQuestionCount,
                    outcome.TargetQuestionCount, false, true);
                _hintButton.Visible = false;
                _nextButton.Visible = true;
                _completeOnNext = outcome.SuggestPositiveEnd || outcome.CompletedQuestionCount >= outcome.TargetQuestionCount;
                _nextButton.Text = outcome.SuggestPositiveEnd ? "Nghỉ ở đây" :
                    (outcome.CompletedQuestionCount >= outcome.TargetQuestionCount ? "Xem kết quả" : "Câu tiếp theo");
                if (outcome.SuggestPositiveEnd)
                    _support.Text = _eventPresentation == null
                        ? "Con đã cố gắng đủ cho lúc này. Có thể nghỉ ở đây và học tiếp lần sau."
                        : _eventPresentation.BreakCopyVi;
                _nextButton.Focus();
            }
            catch
            {
                if (!TryRecoverAfterSubmitFailure())
                    FailCurrentSession("Không thể lưu câu vừa làm. Buổi học sẽ dừng để bảo vệ dữ liệu.");
            }
        }
        private void SubmitInteractiveAnswer(string inputMode)
        {
            if (_question == null || !UsesInteractiveAnswer(_question) || _submitting || !_interactiveAnswer.HasAnswer) return;
            _submitting = true;
            _interactiveSubmitButton.Enabled = false;
            _hintButton.Enabled = false;
            try
            {
                var outcome = SubmitCurrentAnswer(_interactiveAnswer.SelectedAnswer, inputMode);
                if (PrepareRetry(outcome))
                {
                    _interactiveSubmitButton.Enabled = _interactiveAnswer.HasAnswer;
                    _interactiveAnswer.Focus();
                    return;
                }
                _retryPending = false;
                _interactiveAnswer.ShowResult(outcome.IsCorrect);
                _feedback.Text = outcome.FeedbackVi;
                _companion.State = outcome.SuggestPositiveEnd ? CompanionReactionState.Tired :
                    (outcome.IsCorrect ? CompanionReactionState.Correct : CompanionReactionState.TryAgain);
                _feedbackCard.CardColor = outcome.IsCorrect ? Color.FromArgb(226, 242, 224) : Color.FromArgb(251, 232, 222);
                _feedbackCard.BorderColor = outcome.IsCorrect ? Color.FromArgb(190, 221, 188) : Color.FromArgb(236, 202, 187);
                _feedbackCard.Visible = true;
                var interactiveSupport = outcome.IsCorrect
                    ? "Tốt rồi. Đoạn thẳng này đã được lưu để lần sau ôn đúng lúc."
                    : "Đoạn đúng cần dài " + outcome.CorrectAnswerDisplay + " cm. Câu sau sẽ giúp con luyện tiếp phần này.";
                _support.Text = BuildOutcomeSupport(outcome, interactiveSupport);
                UpdateProgressPresentation(outcome.CompletedQuestionCount, outcome.CompletedQuestionCount,
                    outcome.TargetQuestionCount, false, true);
                _hintButton.Visible = false;
                _nextButton.Visible = true;
                _completeOnNext = outcome.SuggestPositiveEnd || outcome.CompletedQuestionCount >= outcome.TargetQuestionCount;
                _nextButton.Text = outcome.SuggestPositiveEnd ? "Nghỉ ở đây" :
                    (outcome.CompletedQuestionCount >= outcome.TargetQuestionCount ? "Xem kết quả" : "Câu tiếp theo");
                if (outcome.SuggestPositiveEnd)
                    _support.Text = _eventPresentation == null
                        ? "Con đã cố gắng đủ cho lúc này. Có thể nghỉ ở đây và học tiếp lần sau."
                        : _eventPresentation.BreakCopyVi;
                _nextButton.Focus();
            }
            catch
            {
                if (!TryRecoverAfterSubmitFailure())
                    FailCurrentSession("Không thể lưu câu vừa làm. Buổi học sẽ dừng để bảo vệ dữ liệu.");
            }
        }

        private void SubmitChoice(int index, string inputMode)
        {
            if (_question == null || _submitting || index < 0 || index >= _answerButtons.Length) return;
            var choices = _question.DisplayChoices;
            if (choices == null || index >= choices.Count) return;
            _submitting = true;
            SetAnswersEnabled(false);
            _hintButton.Enabled = false;
            try
            {
                var selected = choices[index];
                var outcome = SubmitCurrentAnswer(selected, inputMode);
                if (PrepareRetry(outcome))
                {
                    for (var i = 0; i < choices.Count && i < _answerButtons.Length; i++)
                    {
                        _answerButtons[i].VisualState = i == index
                            ? AnswerChoiceButton.ChoiceVisualState.Incorrect
                            : AnswerChoiceButton.ChoiceVisualState.Idle;
                        _answerButtons[i].BadgeText = i == index ? "×" : (i + 1).ToString();
                        _answerButtons[i].Enabled = true;
                    }
                    _answerButtons[index].Focus();
                    return;
                }
                _retryPending = false;
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
                _companion.State = outcome.SuggestPositiveEnd ? CompanionReactionState.Tired :
                    (outcome.IsCorrect ? CompanionReactionState.Correct : CompanionReactionState.TryAgain);
                _feedbackCard.CardColor = outcome.IsCorrect ? Color.FromArgb(226, 242, 224) : Color.FromArgb(251, 232, 222);
                _feedbackCard.BorderColor = outcome.IsCorrect ? Color.FromArgb(190, 221, 188) : Color.FromArgb(236, 202, 187);
                _feedbackCard.Visible = true;
                var choiceSupport = outcome.IsCorrect
                    ? "Tốt rồi. Câu này đã được lưu để lần sau ôn đúng lúc."
                    : "Mình đã chỉ ra đáp án đúng. Câu sau sẽ giúp con luyện tiếp phần này.";
                _support.Text = BuildOutcomeSupport(outcome, choiceSupport);
                UpdateProgressPresentation(outcome.CompletedQuestionCount, outcome.CompletedQuestionCount,
                    outcome.TargetQuestionCount, false, true);
                _hintButton.Visible = false;
                _nextButton.Visible = true;
                _completeOnNext = outcome.SuggestPositiveEnd || outcome.CompletedQuestionCount >= outcome.TargetQuestionCount;
                _nextButton.Text = outcome.SuggestPositiveEnd ? "Nghỉ ở đây" :
                    (outcome.CompletedQuestionCount >= outcome.TargetQuestionCount ? "Xem kết quả" : "Câu tiếp theo");
                if (outcome.SuggestPositiveEnd)
                    _support.Text = _eventPresentation == null
                        ? "Con đã cố gắng đủ cho lúc này. Có thể nghỉ ở đây và học tiếp lần sau."
                        : _eventPresentation.BreakCopyVi;
                _nextButton.Focus();
            }
            catch
            {
                if (!TryRecoverAfterSubmitFailure())
                    FailCurrentSession("Không thể lưu câu vừa làm. Buổi học sẽ dừng để bảo vệ dữ liệu.");
            }
        }

        private int FindCorrectChoiceIndex()
        {
            if (_question == null) return -1;
            var choices = _question.DisplayChoices;
            if (choices == null) return -1;
            for (var i = 0; i < choices.Count; i++)
                if (string.Equals(choices[i], _question.CorrectAnswerDisplay, StringComparison.Ordinal)) return i;
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
                MathSessionSummary summary = null;
                if (_gameEventCoordinator != null && _coordinator != null && _coordinator.IsActive)
                {
                    var completion = _gameEventCoordinator.Complete();
                    _eventState = completion.EventState;
                    summary = completion.LearningSummary;
                }
                else if (_coordinator != null && _coordinator.IsActive)
                {
                    summary = _coordinator.Complete();
                }
                _finished = true;
                ShowCompletion(summary);
            }
            catch
            {
                FailCurrentSession("Buổi học đã kết thúc. Những câu đã lưu trước đó vẫn được giữ lại.");
            }
        }

        private static string BuildCompletionPerformanceText(MathSessionSummary summary)
        {
            if (summary == null) return "Kết quả chưa sẵn sàng. Mình về thư viện Toán nhé.";
            var attempts = Math.Max(0, summary.Attempts);
            var correct = Math.Max(0, Math.Min(attempts, summary.Correct));
            var independent = Math.Max(0, Math.Min(correct, summary.IndependentCorrect));
            var hinted = Math.Max(0, Math.Min(correct, summary.HintedCorrect));
            var retriedQuestions = Math.Max(0, Math.Min(attempts, summary.RetriedQuestions));
            var retriedCorrect = Math.Max(0, Math.Min(correct, summary.RetriedCorrect));
            var needsPractice = Math.Max(0, Math.Min(Math.Max(0, attempts - correct), summary.Wrong));
            var details = "Đã làm " + attempts + " câu · Tự làm đúng " + independent +
                " · Đúng nhờ gợi ý " + hinted;
            if (retriedQuestions > 0)
                details += " · Thử lại " + retriedQuestions + " câu (đúng " + retriedCorrect + ")";
            details += " · Cần luyện lại " + needsPractice;
            if (summary.LessonCompleted && summary.LessonScorePercent.HasValue)
            {
                var score = Math.Max(0, Math.Min(100, Math.Round(summary.LessonScorePercent.Value)));
                var best = summary.LessonBestScorePercent.HasValue
                    ? Math.Max(0, Math.Min(100, Math.Round(summary.LessonBestScorePercent.Value)))
                    : score;
                return "Điểm bài " + score + "% · Tốt nhất " + best + "% · " + details;
            }
            return details;
        }

        private static string BuildCompletionSupportText(MathSessionSummary summary)
        {
            if (summary == null) return "Các câu đã làm được lưu an toàn. Mình về thư viện Toán nhé.";
            var skills = Math.Max(0, summary.DistinctSkills);
            var context = string.Equals(summary.SessionMode, "lesson", StringComparison.Ordinal) ? "bài này" : "nhiệm vụ này";
            var text = "Con đã luyện " + skills + " kỹ năng trong " + context + ".";

            if (summary.TargetSkillMasteryAfter.HasValue)
            {
                var after = (int)Math.Round(Math.Max(0, Math.Min(1, summary.TargetSkillMasteryAfter.Value)) * 100.0);
                text += " Mức thành thạo hiện tại: " + after + "%";
                if (summary.TargetSkillMasteryDelta.HasValue && summary.TargetSkillMasteryDelta.Value > 0.000000001)
                {
                    var delta = (int)Math.Round(Math.Max(0, Math.Min(1, summary.TargetSkillMasteryDelta.Value)) * 100.0);
                    if (delta > 0) text += " · tăng thêm " + delta + " điểm phần trăm";
                }
                text += ".";
            }
            else if (summary.ImprovedSkillCount > 0)
            {
                text += " Có " + summary.ImprovedSkillCount + " kỹ năng tiến bộ trong nhiệm vụ này.";
            }

            if (!string.IsNullOrWhiteSpace(summary.NextLessonId) && !string.IsNullOrWhiteSpace(summary.NextLessonTitleVi))
                text += " Bài tiếp theo đã sẵn sàng: " + summary.NextLessonTitleVi.Trim() + ".";
            if (summary.GardenGrowthSteps > 0)
                text += " Khu vườn đã ghi nhận tiến bộ của con.";
            if (!string.IsNullOrWhiteSpace(summary.GardenUnlockMessage))
                text += " " + summary.GardenUnlockMessage.Trim();
            return text;
        }

        private void ShowCompletion(MathSessionSummary summary)
        {
            _question = null;
            _instructionVisual.SetQuestion(null, 0);
            _instructionVisual.Visible = false;
            var unlockedItem = summary != null && summary.GardenUnlockedItemIds != null && summary.GardenUnlockedItemIds.Count > 0
                ? summary.GardenUnlockedItemIds[0] : null;
            _completionVisual.SetProgress(summary == null ? 0 : summary.GardenGrowthSteps, unlockedItem,
                summary == null ? 0 : summary.SessionsUntilNextGardenMilestone,
                summary == null ? null : summary.NextGardenMilestoneItemId);
            _completionVisual.Visible = true;
            _companion.State = CompanionReactionState.Celebrate;
            _prompt.Text = _eventPresentation != null
                ? "Nhiệm vụ cứu hộ hoàn thành"
                : (summary != null && string.Equals(summary.SessionMode, "lesson", StringComparison.Ordinal)
                    ? "Hoàn thành bài học" : "Hoàn thành nhiệm vụ");
            var completionSupport = BuildCompletionSupportText(summary);
            _support.Text = _eventPresentation == null
                ? completionSupport
                : _eventPresentation.CompletionVi + " " + completionSupport;
            _support.AccessibleName = "Tóm tắt tiến bộ: " + _support.Text;
            _feedback.Text = BuildCompletionPerformanceText(summary);
            _feedback.AccessibleName = "Kết quả nhiệm vụ: " + _feedback.Text;
            _feedbackCard.CardColor = Color.FromArgb(226, 242, 224);
            _feedbackCard.BorderColor = Color.FromArgb(190, 221, 188);
            _feedbackCard.Visible = true;
            foreach (var button in _answerButtons) button.Visible = false;
            _answerGrid.Visible = false;
            _typedAnswerLayout.Visible = false;
            _interactiveAnswerLayout.Visible = false;
            _hintButton.Visible = false;
            _stopButton.Visible = false;
            _nextButton.Visible = true;
            _nextButton.Text = _eventPresentation == null ? "Về thư viện Toán" : "Về nhiệm vụ cứu hộ";
            _nextButton.AccessibleName = _eventPresentation == null ? "Về thư viện Toán" : "Về nhiệm vụ cứu hộ";
            _nextButton.AccessibleDescription = _eventPresentation == null
                ? "Đóng kết quả và quay lại danh sách bài Toán."
                : "Đóng kết quả và quay lại danh sách nhiệm vụ cứu hộ.";
            _completeOnNext = false;
            if (_eventPresentation == null)
            {
                _progressText.Text = "Hoàn thành";
                _progressBar.Value = _progressBar.Maximum;
            }
            else
            {
                UpdateProgressPresentation(_targetQuestionCount, _targetQuestionCount, _targetQuestionCount, false, true);
            }
            _nextButton.Focus();
        }

        private void RequestStop()
        {
            if (_finished) { Close(); return; }
            var stopMessage = _eventPresentation == null
                ? "Dừng buổi học ở đây?\r\n\r\nNhững câu đã làm vẫn được lưu để lần sau học tiếp."
                : _eventPresentation.BreakCopyVi + "\r\n\r\nNhững chặng đã làm vẫn được lưu để lần sau quay lại đúng chỗ.";
            var answer = MessageBox.Show(this,
                stopMessage,
                _eventPresentation == null ? "Dừng buổi học" : "Nghỉ ở đây",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;
            try
            {
                if (_gameEventCoordinator != null && _coordinator != null && _coordinator.IsActive)
                    _gameEventCoordinator.SuspendForBreak("child_requested_break");
                else if (_coordinator != null && _coordinator.IsActive)
                    _coordinator.Suspend("child_requested_stop");
            }
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
            _instructionVisual.Visible = false;
            _completionVisual.Visible = false;
            _companion.State = CompanionReactionState.Tired;
            _prompt.Text = "Mình dừng ở đây nhé";
            _support.Text = message;
            _feedback.Text = "Những dữ liệu đã lưu trước đó vẫn an toàn.";
            _feedbackCard.CardColor = Color.FromArgb(245, 239, 224);
            _feedbackCard.Visible = true;
            foreach (var button in _answerButtons) button.Visible = false;
            _answerGrid.Visible = false;
            _typedAnswerLayout.Visible = false;
            _interactiveAnswerLayout.Visible = false;
            _hintButton.Visible = false;
            _stopButton.Visible = false;
            _nextButton.Visible = true;
            _nextButton.Text = "Về thư viện Toán";
            _nextButton.AccessibleName = "Về thư viện Toán";
            _nextButton.AccessibleDescription = "Đóng thông báo và quay lại danh sách bài Toán.";
            _completeOnNext = false;
            _nextButton.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_question != null && UsesInteractiveAnswer(_question) && !_submitting &&
                keyData == Keys.Enter && _interactiveSubmitButton.Visible && _interactiveSubmitButton.Enabled)
            {
                SubmitInteractiveAnswer("keyboard");
                return true;
            }
            if (_question != null && UsesTypedAnswer(_question) && !_submitting &&
                keyData == Keys.Enter && _typedSubmitButton.Visible && _typedSubmitButton.Enabled)
            {
                SubmitTypedAnswer("keyboard");
                return true;
            }
            if (keyData >= Keys.D1 && keyData <= Keys.D4)
            {
                var index = (int)keyData - (int)Keys.D1;
                if (_answerButtons[index].Visible && _answerButtons[index].Enabled) SubmitChoice(index, "keyboard");
                return true;
            }
            if (keyData >= Keys.NumPad1 && keyData <= Keys.NumPad4)
            {
                var index = (int)keyData - (int)Keys.NumPad1;
                if (_answerButtons[index].Visible && _answerButtons[index].Enabled) SubmitChoice(index, "keyboard");
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
                var closeMessage = _eventPresentation == null
                    ? "Dừng buổi học ở đây? Những câu đã làm vẫn được lưu."
                    : _eventPresentation.BreakCopyVi + " Những chặng đã làm vẫn được lưu để lần sau quay lại đúng chỗ.";
                var answer = MessageBox.Show(this,
                    closeMessage,
                    _eventPresentation == null ? "Dừng buổi học" : "Nghỉ ở đây",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes) { e.Cancel = true; return; }
            }
            try
            {
                if (_gameEventCoordinator != null && _coordinator != null && _coordinator.IsActive)
                    _gameEventCoordinator.SuspendForBreak("game_event_window_closed");
                else if (_coordinator != null && _coordinator.IsActive)
                    _coordinator.Suspend("lesson_window_closed");
            }
            catch { }
            _finished = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_gameEventCoordinator != null) _gameEventCoordinator.Dispose();
                else if (_coordinator != null) _coordinator.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
