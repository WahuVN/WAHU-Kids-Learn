using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WAHU.Learning;
using WAHU.Performance;
using WAHU.Session;

namespace WAHUKidsLearn
{
    internal sealed class LessonPlayPage : LearnerPage
    {
        private readonly LearnerShellContext _context;
        private readonly Action _back;
        private MathSessionCoordinator _coordinator;
        private MathGameEventCoordinator _gameEventCoordinator;
        private RescueGameRuntimeBridge _rescueBridge;
        private MathQuickRescueEventPresentation _eventPresentation;
        private MathGameEventState _eventState;
        private MathQuestion _question;
        private string _loadedLessonId;
        private int _targetQuestionCount = MathSessionCoordinator.TargetedLessonQuestionCount;
        private int _hintLevel;
        private bool _retryPending;
        private bool _submitting;
        private bool _completeOnNext;
        private bool _breakOnNext;
        private bool _finished;
        private bool _suspendOnLeave;
        private DateTime _questionShownAtUtc;

        private TableLayoutPanel _body;
        private Label _sceneLabel;
        private Label _progressText;
        private ProgressStrip _progress;
        private QuickRescueCheckpointStrip _eventProgress;
        private ChildCard _visualCard;
        private MathInstructionVisual _instructionVisual;
        private LessonCompletionVisual _completionVisual;
        private CompanionReactionControl _companion;
        private GameFeedbackFxControl _feedbackFx;
        private ChildCard _questionCard;
        private Label _prompt;
        private Label _support;
        private TableLayoutPanel _answerHost;
        private TableLayoutPanel _choiceGrid;
        private readonly AnswerChoiceButton[] _choiceButtons = new AnswerChoiceButton[4];
        private TableLayoutPanel _typedLayout;
        private TextBox _typedBox;
        private ChildActionButton _typedSubmit;
        private TableLayoutPanel _interactiveLayout;
        private SegmentDrawingAnswerControl _interactiveAnswer;
        private ChildActionButton _interactiveSubmit;
        private ChildCard _feedbackCard;
        private Label _feedback;
        private ChildActionButton _hintButton;
        private ChildActionButton _nextButton;
        private ChildActionButton _pauseButton;

        public LessonPlayPage(LearnerShellContext context, Action back)
        {
            _context = context ?? throw new ArgumentNullException("context");
            _back = back ?? throw new ArgumentNullException("back");
            AccessibleName = "Màn chơi bài Toán";
            BuildUi();
        }

        public override LearnerRoute Route { get { return LearnerRoute.LessonPlay; } }
        public override string PageTitle
        {
            get
            {
                if (_eventPresentation != null) return _eventPresentation.TitleVi;
                return string.IsNullOrWhiteSpace(_loadedLessonId) ? "Bài Toán" : "Bài Toán";
            }
        }

        internal string LoadedLessonId { get { return _loadedLessonId; } }
        internal bool HasActiveSession { get { return (_rescueBridge != null && _rescueBridge.IsInteractive) || (_coordinator != null && _coordinator.IsActive); } }
        internal bool IsFinished { get { return _finished; } }
        internal int HintLevel { get { return _hintLevel; } }
        internal bool RetryPending { get { return _retryPending; } }
        internal string CurrentContentQuestionId { get { return _question == null ? null : _question.ContentQuestionId; } }

        public override void OnNavigatedTo()
        {
            var requested = _context.RequestedLessonId;
            if (string.IsNullOrWhiteSpace(requested))
            {
                ShowFatal("Chưa có bài Toán được chọn. Mình quay lại lộ trình nhé.");
                return;
            }

            if (!_finished && string.Equals(_loadedLessonId, requested, StringComparison.Ordinal))
            {
                if ((_rescueBridge != null && _rescueBridge.IsInteractive) ||
                    (_coordinator != null && _coordinator.IsActive))
                {
                    _suspendOnLeave = true;
                    FocusCurrentInput();
                    return;
                }
            }

            StartSession(requested, _context.RequestedEvent);
        }

        public override void OnNavigatedFrom()
        {
            if (!_suspendOnLeave || _finished) return;
            try
            {
                if (_rescueBridge != null && _rescueBridge.IsInteractive)
                    _rescueBridge.SuspendForBreak("learner_shell_navigation");
                else if (_gameEventCoordinator != null && _coordinator != null && _coordinator.IsActive)
                    _gameEventCoordinator.SuspendForBreak("learner_shell_navigation");
                else if (_coordinator != null && _coordinator.IsActive)
                    _coordinator.Suspend("learner_shell_navigation");
            }
            catch { }
            _suspendOnLeave = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_finished)
                {
                    try
                    {
                        if (_rescueBridge != null && _rescueBridge.IsInteractive) _rescueBridge.SuspendForBreak("learner_shell_dispose");
                        else if (_gameEventCoordinator != null && _coordinator != null && _coordinator.IsActive) _gameEventCoordinator.SuspendForBreak("learner_shell_dispose");
                        else if (_coordinator != null && _coordinator.IsActive) _coordinator.Suspend("learner_shell_dispose");
                    }
                    catch { }
                }
                if (_rescueBridge != null) _rescueBridge.Dispose();
                else if (_gameEventCoordinator != null) _gameEventCoordinator.Dispose();
                else if (_coordinator != null) _coordinator.Dispose();
            }
            base.Dispose(disposing);
        }

        private void BuildUi()
        {
            var root = new ChildSceneLayout
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = LearnerDesignTokens.PagePadding(LearnerLayoutProfile.Standard)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));

            var progressRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Margin = Padding.Empty };
            progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            progressRow.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
            progressRow.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
            _sceneLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "BÀI TOÁN",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.MintStrong,
                Font = ChildVisualTheme.Font(9.2f, FontStyle.Bold),
                AccessibleName = "Trạng thái câu hỏi"
            };
            progressRow.Controls.Add(_sceneLabel, 0, 0);
            _progress = new ProgressStrip { Dock = DockStyle.Fill, Maximum = 3, Value = 0, Margin = new Padding(8, 7, 8, 2) };
            progressRow.Controls.Add(_progress, 1, 0);
            _progressText = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Câu 1 / 3",
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9.2f, FontStyle.Bold),
                AccessibleName = "Tiến độ bài học"
            };
            progressRow.Controls.Add(_progressText, 2, 0);
            _eventProgress = new QuickRescueCheckpointStrip { Dock = DockStyle.Fill, Visible = false, Margin = new Padding(0, 1, 0, 0) };
            progressRow.Controls.Add(_eventProgress, 0, 1);
            progressRow.SetColumnSpan(_eventProgress, 3);
            root.Controls.Add(progressRow, 0, 0);

            _body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            _body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            _body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            _body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _visualCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 8, 4),
                Padding = new Padding(10),
                CardColor = Color.FromArgb(247, 250, 244),
                BorderColor = Color.FromArgb(216, 226, 208),
                Radius = LearnerDesignTokens.RadiusCard
            };
            var visualLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            visualLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
            visualLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            visualLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 2));
            _instructionVisual = new MathInstructionVisual { Dock = DockStyle.Fill, Margin = new Padding(4), Visible = true };
            _completionVisual = new LessonCompletionVisual { Dock = DockStyle.Fill, Margin = new Padding(4), Visible = false };
            var visualHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = Color.Transparent };
            visualHost.Controls.Add(_completionVisual);
            visualHost.Controls.Add(_instructionVisual);
            visualLayout.Controls.Add(visualHost, 0, 0);
            var reaction = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            reaction.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
            reaction.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            _companion = new CompanionReactionControl { Dock = DockStyle.Fill, Margin = Padding.Empty };
            _feedbackFx = new GameFeedbackFxControl { Dock = DockStyle.Fill, Margin = Padding.Empty, VisualMood = GameFeedbackFxControl.Mood.Neutral };
            reaction.Controls.Add(_companion, 0, 0);
            reaction.Controls.Add(_feedbackFx, 1, 0);
            visualLayout.Controls.Add(reaction, 0, 1);
            _visualCard.Controls.Add(visualLayout);
            _body.Controls.Add(_visualCard, 0, 0);

            _questionCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 4, 0, 4),
                Padding = new Padding(18, 14, 18, 14),
                CardColor = Color.FromArgb(255, 253, 247),
                BorderColor = Color.FromArgb(230, 218, 191),
                Radius = LearnerDesignTokens.RadiusHero
            };
            var questionLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Margin = Padding.Empty };
            questionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 27));
            questionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 16));
            questionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            questionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 17));
            questionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            _prompt = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Đang chuẩn bị câu hỏi…",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(21f, FontStyle.Bold),
                AccessibleName = "Câu hỏi Toán"
            };
            questionLayout.Controls.Add(_prompt, 0, 0);
            _support = new Label
            {
                Dock = DockStyle.Fill,
                Text = "",
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9.8f),
                AccessibleName = "Hỗ trợ câu hỏi"
            };
            questionLayout.Controls.Add(_support, 0, 1);
            _answerHost = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, Margin = Padding.Empty };
            _answerHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _answerHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            BuildAnswerSurfaces();
            questionLayout.Controls.Add(_answerHost, 0, 2);
            _feedbackCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 0),
                Padding = new Padding(10, 5, 10, 5),
                CardColor = Color.FromArgb(245, 244, 236),
                BorderColor = Color.FromArgb(226, 222, 207),
                Radius = 14,
                ShowShadow = false,
                Visible = false
            };
            _feedback = new Label
            {
                Dock = DockStyle.Fill,
                Text = "",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(9.6f, FontStyle.Bold),
                AccessibleName = "Phản hồi câu trả lời"
            };
            _feedbackCard.Controls.Add(_feedback);
            questionLayout.Controls.Add(_feedbackCard, 0, 3);
            _questionCard.Controls.Add(questionLayout);
            _body.Controls.Add(_questionCard, 1, 0);
            root.Controls.Add(_body, 0, 1);

            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(0, 6, 0, 0) };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _pauseButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 6, 0),
                Text = "Nghỉ ở đây",
                FillColor = Color.FromArgb(239, 238, 230),
                HoverColor = Color.FromArgb(228, 226, 216),
                PressedColor = Color.FromArgb(216, 214, 203),
                TextColor = ChildVisualTheme.Ink,
                Radius = LearnerDesignTokens.RadiusButton,
                Font = ChildVisualTheme.Font(9.7f, FontStyle.Bold),
                AccessibleName = "Nghỉ và lưu bài để học tiếp sau"
            };
            _pauseButton.Click += delegate { PauseAndBack(); };
            actions.Controls.Add(_pauseButton, 0, 0);
            _hintButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 6, 0),
                Text = "Gợi ý",
                IconAssetPath = "10_UIIcons/icon_hint.png",
                IconSize = 22,
                FillColor = Color.FromArgb(255, 242, 194),
                HoverColor = Color.FromArgb(250, 232, 164),
                PressedColor = Color.FromArgb(242, 219, 139),
                TextColor = Color.FromArgb(107, 82, 35),
                Radius = LearnerDesignTokens.RadiusButton,
                Font = ChildVisualTheme.Font(10f, FontStyle.Bold),
                AccessibleName = "Xem gợi ý cho câu hiện tại"
            };
            _hintButton.Click += delegate { ShowHint(); };
            actions.Controls.Add(_hintButton, 1, 0);
            _nextButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 0, 0),
                Text = "Câu tiếp theo",
                FillColor = ChildVisualTheme.MintStrong,
                HoverColor = Color.FromArgb(90, 156, 103),
                PressedColor = Color.FromArgb(75, 139, 88),
                TextColor = Color.White,
                Radius = LearnerDesignTokens.RadiusButton,
                Font = ChildVisualTheme.Font(10f, FontStyle.Bold),
                Visible = false,
                AccessibleName = "Đi tới bước tiếp theo"
            };
            _nextButton.Click += delegate { HandleNext(); };
            actions.Controls.Add(_nextButton, 2, 0);
            root.Controls.Add(actions, 0, 2);
            Controls.Add(root);
        }

        private void BuildAnswerSurfaces()
        {
            _choiceGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty, Padding = new Padding(4) };
            _choiceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _choiceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _choiceGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            _choiceGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            for (var i = 0; i < _choiceButtons.Length; i++)
            {
                var index = i;
                var button = new AnswerChoiceButton
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(5),
                    Text = "",
                    BadgeText = (i + 1).ToString(),
                    Radius = 16,
                    Font = ChildVisualTheme.Font(14f, FontStyle.Bold),
                    Visible = false,
                    AccessibleName = "Lựa chọn " + (i + 1)
                };
                button.Click += delegate { SubmitChoice(index, "button"); };
                _choiceButtons[i] = button;
                _choiceGrid.Controls.Add(button, i % 2, i / 2);
            }
            _answerHost.Controls.Add(_choiceGrid, 0, 0);

            _typedLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, Padding = new Padding(12, 8, 12, 8), Visible = false };
            _typedLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            _typedLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            _typedBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(16, 8, 16, 8),
                TextAlign = HorizontalAlignment.Center,
                Font = ChildVisualTheme.Font(20f, FontStyle.Bold),
                AccessibleName = "Nhập đáp án Toán"
            };
            _typedBox.TextChanged += delegate { _typedSubmit.Enabled = !_submitting && !string.IsNullOrWhiteSpace(_typedBox.Text); };
            _typedBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && _typedSubmit.Enabled)
                {
                    SubmitTyped("keyboard");
                    e.SuppressKeyPress = true;
                }
            };
            _typedLayout.Controls.Add(_typedBox, 0, 0);
            _typedSubmit = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(54, 3, 54, 3),
                Text = "Kiểm tra đáp án",
                FillColor = ChildVisualTheme.SkyStrong,
                HoverColor = Color.FromArgb(75, 143, 181),
                PressedColor = Color.FromArgb(63, 127, 163),
                TextColor = Color.White,
                Radius = 16,
                Font = ChildVisualTheme.Font(10f, FontStyle.Bold),
                Enabled = false,
                AccessibleName = "Kiểm tra đáp án đã nhập"
            };
            _typedSubmit.Click += delegate { SubmitTyped("button"); };
            _typedLayout.Controls.Add(_typedSubmit, 0, 1);
            _answerHost.Controls.Add(_typedLayout, 0, 0);

            _interactiveLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, Padding = new Padding(6), Visible = false };
            _interactiveLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 72));
            _interactiveLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
            _interactiveAnswer = new SegmentDrawingAnswerControl { Dock = DockStyle.Fill, Margin = new Padding(4) };
            _interactiveAnswer.AnswerChanged += delegate { _interactiveSubmit.Enabled = !_submitting && _interactiveAnswer.HasAnswer; };
            _interactiveLayout.Controls.Add(_interactiveAnswer, 0, 0);
            _interactiveSubmit = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(58, 2, 58, 2),
                Text = "Kiểm tra đoạn thẳng",
                FillColor = ChildVisualTheme.SkyStrong,
                HoverColor = Color.FromArgb(75, 143, 181),
                PressedColor = Color.FromArgb(63, 127, 163),
                TextColor = Color.White,
                Radius = 16,
                Font = ChildVisualTheme.Font(9.5f, FontStyle.Bold),
                Enabled = false,
                AccessibleName = "Kiểm tra đoạn thẳng đã vẽ"
            };
            _interactiveSubmit.Click += delegate { SubmitInteractive("button"); };
            _interactiveLayout.Controls.Add(_interactiveSubmit, 0, 1);
            _answerHost.Controls.Add(_interactiveLayout, 0, 0);
        }

        private void StartSession(string lessonId, MathQuickRescueEventPresentation requestedEvent)
        {
            DisposeCoordinator();
            _loadedLessonId = lessonId;
            _eventPresentation = requestedEvent;
            _eventState = null;
            _question = null;
            _hintLevel = 0;
            _retryPending = false;
            _submitting = false;
            _completeOnNext = false;
            _breakOnNext = false;
            _finished = false;
            _suspendOnLeave = true;
            _pauseButton.Visible = true;
            _hintButton.Visible = true;
            _nextButton.Visible = false;
            _completionVisual.Visible = false;
            _instructionVisual.Visible = true;
            _feedbackCard.Visible = false;
            try
            {
                var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "content_packs", "math_grade2_v1", "verified_templates_v1.json");
                var profile = _context.Performance == null ? "LOW" : _context.Performance.Profile.ToString();
                var seed = unchecked(Environment.TickCount ^ DateTime.UtcNow.Millisecond ^ GetHashCode());
                MathSessionStartResult started;
                if (_eventPresentation != null)
                {
                    var eventPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "content_packs", "math_grade2_v1", "game_events_v1.json");
                    var learningContentPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "content_packs", "math_quick_rescue_v1", "learning_content_v1.json");
                    var audioRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio", "rescue");
                    _rescueBridge = new RescueGameRuntimeBridge(_context.LearningDatabase, templatePath, eventPath, profile, seed,
                        _eventPresentation.Id, lessonId, learningContentPath, audioRoot);
                    var rescueStarted = _rescueBridge.Start("Bé học");
                    var eventStarted = rescueStarted.Learning;
                    _eventState = rescueStarted.State.EventState;
                    started = eventStarted.Session;
                    if (eventStarted.Event == null || _eventState == null || _eventState.FallbackToLessonPresentation)
                        _eventPresentation = null;
                    else
                        _eventPresentation = MathQuickRescueEventPresentation.FromDefinition(eventStarted.Event);
                }
                else
                {
                    _coordinator = new MathSessionCoordinator(_context.LearningDatabase, templatePath, profile, seed, lessonId);
                    started = _coordinator.Start("Bé học");
                }
                _targetQuestionCount = Math.Max(1, started.TargetQuestionCount);
                _progress.Maximum = _targetQuestionCount;
                _progress.Value = Math.Min(_targetQuestionCount, Math.Max(0, started.CompletedQuestionCount));
                _retryPending = started.RetryPending;
                _eventProgress.Visible = _eventPresentation != null;
                ShowNextQuestion(started);
            }
            catch
            {
                ShowFatal("Chưa thể bắt đầu bài Toán lúc này. Nhờ người lớn mở mục Phụ huynh để kiểm tra nhé.");
            }
        }

        private void ShowNextQuestion(MathSessionStartResult started)
        {
            if (_finished || (_rescueBridge == null && (_coordinator == null || !_coordinator.IsActive))) return;
            try
            {
                MathRescueGameplaySnapshot rescueState = null;
                if (_rescueBridge != null)
                {
                    rescueState = _rescueBridge.EnsureQuestionActive();
                    if (rescueState.Completed)
                    {
                        _finished = true;
                        _suspendOnLeave = false;
                        ShowCompletion(rescueState.CompletionSummary);
                        return;
                    }
                    _question = rescueState.CurrentQuestion;
                    _eventState = rescueState.EventState;
                    _retryPending = rescueState.Phase == MathRescueGameplayPhase.REPAIR;
                }
                else
                {
                    _question = _coordinator.NextQuestion();
                    if (_gameEventCoordinator != null) _eventState = _gameEventCoordinator.CurrentState;
                }
                if (_question == null) { CompleteSession(); return; }
                _questionShownAtUtc = DateTime.UtcNow;
                _hintLevel = 0;
                _submitting = false;
                _completeOnNext = false;
                _instructionVisual.Visible = true;
                _completionVisual.Visible = false;
                _instructionVisual.SetQuestion(_question, 0);
                _companion.State = CompanionReactionState.Calm;
                _feedbackFx.VisualMood = GameFeedbackFxControl.Mood.Neutral;
                _feedbackCard.Visible = false;
                _feedback.Text = string.Empty;
                _hintButton.Visible = true;
                _hintButton.Enabled = true;
                _hintButton.Text = "Gợi ý";
                _nextButton.Visible = false;
                _pauseButton.Visible = true;
                var completedForUi = rescueState == null ? _coordinator.Summary.Attempts : rescueState.Progress.CompletedCheckpoints;
                var number = completedForUi + 1;
                _sceneLabel.Text = _eventPresentation == null
                    ? "CÂU " + number + " • BÀI TOÁN"
                    : "CHẶNG " + number + " • " + _eventPresentation.CheckpointName(Math.Max(0, number - 1));
                UpdateProgress(completedForUi, number, _retryPending, false);
                ApplyPromptTypography(_question.PromptVi);
                ConfigureAnswerInput(_question);
                if (_rescueBridge != null)
                {
                    var decision = _rescueBridge.CurrentSupportDecision();
                    if (decision != null && !string.IsNullOrWhiteSpace(decision.SupportVi))
                    {
                        _support.Text = decision.SupportVi;
                        if (decision.RecommendedHintLevel > 0)
                        {
                            _hintLevel = Math.Max(_hintLevel, Math.Min(2, decision.RecommendedHintLevel));
                            _instructionVisual.SetQuestion(_question, _hintLevel);
                            if (UsesInteractiveAnswer(_question)) _interactiveAnswer.SetHintLevel(_hintLevel);
                            _hintButton.Text = _hintLevel < 2 ? "Gợi ý thêm" : "Đã xem đủ gợi ý";
                            _hintButton.Enabled = _hintLevel < 2;
                        }
                    }
                }
                if (started != null)
                {
                    if (started.ResumedExistingSession)
                        _support.Text = started.RestoredOpenQuestion ? "Mình tiếp tục đúng câu đang làm dở nhé." : "Mình tiếp tục buổi học đã lưu nhé.";
                    started = null;
                }
            }
            catch
            {
                ShowFatal("Không thể mở câu tiếp theo. Những câu đã làm vẫn được giữ lại.");
            }
        }

        private void ShowNextQuestion()
        {
            ShowNextQuestion(null);
        }

        private void ConfigureAnswerInput(MathQuestion question)
        {
            _choiceGrid.Visible = false;
            _typedLayout.Visible = false;
            _interactiveLayout.Visible = false;
            if (UsesInteractiveAnswer(question))
            {
                _interactiveLayout.Visible = true;
                _interactiveAnswer.SetQuestion(question);
                _interactiveAnswer.SetHintLevel(0);
                _interactiveSubmit.Enabled = false;
                _support.Text = "Chọn điểm A rồi điểm B trên thước. Hai đầu mút có thể nằm ở bất kỳ vạch nào.";
                _interactiveAnswer.Focus();
                return;
            }
            if (UsesTypedAnswer(question))
            {
                _typedLayout.Visible = true;
                _typedBox.Text = string.Empty;
                _typedBox.Enabled = true;
                _typedSubmit.Enabled = false;
                _support.Text = TypedAnswerSupport(question);
                _typedBox.AccessibleDescription = _support.Text + " Nhấn Enter để kiểm tra.";
                _typedBox.Focus();
                return;
            }
            _choiceGrid.Visible = true;
            var choices = question.DisplayChoices;
            if (choices == null || choices.Count < 2 || choices.Count > 4)
                throw new InvalidDataException("Math choice question must expose 2 to 4 display choices.");
            ConfigureChoiceLayout(choices.Count);
            for (var i = 0; i < 4; i++)
            {
                var visible = i < choices.Count;
                _choiceButtons[i].Visible = visible;
                if (!visible) continue;
                var text = choices[i] ?? string.Empty;
                _choiceButtons[i].Text = text;
                _choiceButtons[i].Font = ChildVisualTheme.Font(text.Length > 22 ? 9.5f : (text.Length > 10 ? 11.5f : 16f), FontStyle.Bold);
                _choiceButtons[i].BadgeText = (i + 1).ToString();
                _choiceButtons[i].VisualState = AnswerChoiceButton.ChoiceVisualState.Idle;
                _choiceButtons[i].Enabled = true;
                _choiceButtons[i].AccessibleDescription = "Lựa chọn: " + text;
            }
            _support.Text = "Chọn đáp án con thấy đúng nhất.";
            _choiceButtons[0].Focus();
        }

        private void ConfigureChoiceLayout(int count)
        {
            _choiceGrid.SuspendLayout();
            try
            {
                for (var i = 0; i < 4; i++) _choiceGrid.SetColumnSpan(_choiceButtons[i], 1);
                if (count == 2)
                {
                    _choiceGrid.RowStyles[0].Height = 100;
                    _choiceGrid.RowStyles[1].Height = 0;
                    _choiceGrid.SetCellPosition(_choiceButtons[0], new TableLayoutPanelCellPosition(0, 0));
                    _choiceGrid.SetCellPosition(_choiceButtons[1], new TableLayoutPanelCellPosition(1, 0));
                }
                else
                {
                    _choiceGrid.RowStyles[0].Height = 50;
                    _choiceGrid.RowStyles[1].Height = 50;
                    _choiceGrid.SetCellPosition(_choiceButtons[0], new TableLayoutPanelCellPosition(0, 0));
                    _choiceGrid.SetCellPosition(_choiceButtons[1], new TableLayoutPanelCellPosition(1, 0));
                    _choiceGrid.SetCellPosition(_choiceButtons[2], new TableLayoutPanelCellPosition(0, 1));
                    _choiceGrid.SetCellPosition(_choiceButtons[3], new TableLayoutPanelCellPosition(1, 1));
                    if (count == 3) _choiceGrid.SetColumnSpan(_choiceButtons[2], 2);
                }
            }
            finally { _choiceGrid.ResumeLayout(true); }
        }

        private void ShowHint()
        {
            if (_question == null || _submitting || !_hintButton.Enabled) return;
            _hintLevel = Math.Min(2, _hintLevel + 1);
            _instructionVisual.SetQuestion(_question, _hintLevel);
            if (UsesInteractiveAnswer(_question)) _interactiveAnswer.SetHintLevel(_hintLevel);
            _support.Text = _hintLevel == 1 ? _question.HintLevel1 : _question.HintLevel2;
            _feedbackFx.VisualMood = GameFeedbackFxControl.Mood.Hint;
            _hintButton.Text = _hintLevel < 2 ? "Gợi ý thêm" : "Đã xem đủ gợi ý";
            _hintButton.Enabled = _hintLevel < 2;
        }

        private MathAnswerOutcome SubmitCurrent(string answer, string inputMode)
        {
            if (_rescueBridge != null)
            {
                var rescueNow = DateTime.UtcNow;
                var rescueResponseMs = (int)Math.Min(int.MaxValue, Math.Max(0, (rescueNow - (_questionShownAtUtc == default(DateTime) ? rescueNow : _questionShownAtUtc)).TotalMilliseconds));
                var outcome = _rescueBridge.Submit(answer, _hintLevel, inputMode, rescueNow, rescueResponseMs);
                _eventState = _rescueBridge.CurrentState.EventState;
                return outcome;
            }
            if (_coordinator == null) throw new InvalidOperationException("Math session is not ready.");
            if (_gameEventCoordinator == null)
                return _retryPending
                    ? _coordinator.SubmitRetryAnswer(answer, _hintLevel, inputMode)
                    : _coordinator.SubmitAnswerWithRetry(answer, _hintLevel, inputMode);
            var now = DateTime.UtcNow;
            var responseMs = (int)Math.Min(int.MaxValue, Math.Max(0, (now - (_questionShownAtUtc == default(DateTime) ? now : _questionShownAtUtc)).TotalMilliseconds));
            var result = _retryPending
                ? _gameEventCoordinator.SubmitRetryAnswerAt(answer, _hintLevel, inputMode, now, responseMs)
                : _gameEventCoordinator.SubmitAnswerWithRetryAt(answer, _hintLevel, inputMode, now, responseMs);
            _eventState = result.EventState;
            return result.Learning;
        }

        private bool PrepareRetry(MathAnswerOutcome outcome)
        {
            if (outcome == null || outcome.QuestionCompleted || !outcome.CanRetry) return false;
            _retryPending = true;
            _submitting = false;
            _feedback.Text = outcome.FeedbackVi;
            _feedbackCard.CardColor = Color.FromArgb(251, 232, 222);
            _feedbackCard.BorderColor = Color.FromArgb(236, 202, 187);
            _feedbackCard.Visible = true;
            _companion.State = CompanionReactionState.TryAgain;
            _feedbackFx.VisualMood = GameFeedbackFxControl.Mood.Retry;
            _support.Text = _eventPresentation == null
                ? "Con còn một lần thử ở chính câu này. Có thể xem gợi ý rồi sửa đáp án nhé."
                : _eventPresentation.RepairCopyVi + " Con còn một lần thử ở chính chặng này.";
            UpdateProgress(outcome.CompletedQuestionCount, outcome.CompletedQuestionCount + 1, true, false);
            _hintButton.Visible = true;
            _hintButton.Enabled = _hintLevel < 2;
            _nextButton.Visible = false;
            return true;
        }

        private void SubmitChoice(int index, string inputMode)
        {
            if (_question == null || _submitting || index < 0 || index >= 4) return;
            var choices = _question.DisplayChoices;
            if (choices == null || index >= choices.Count) return;
            _submitting = true;
            SetChoiceEnabled(false);
            _hintButton.Enabled = false;
            try
            {
                var outcome = SubmitCurrent(choices[index], inputMode);
                if (PrepareRetry(outcome))
                {
                    for (var i = 0; i < choices.Count; i++)
                    {
                        _choiceButtons[i].Enabled = true;
                        _choiceButtons[i].VisualState = i == index ? AnswerChoiceButton.ChoiceVisualState.Incorrect : AnswerChoiceButton.ChoiceVisualState.Idle;
                        _choiceButtons[i].BadgeText = i == index ? "×" : (i + 1).ToString();
                    }
                    _choiceButtons[index].Focus();
                    return;
                }
                ApplyFinalOutcome(outcome, index);
            }
            catch { RecoverOrFail(); }
        }

        private void SubmitTyped(string inputMode)
        {
            if (_question == null || !UsesTypedAnswer(_question) || _submitting || string.IsNullOrWhiteSpace(_typedBox.Text)) return;
            _submitting = true;
            _typedBox.Enabled = false;
            _typedSubmit.Enabled = false;
            _hintButton.Enabled = false;
            try
            {
                var outcome = SubmitCurrent(_typedBox.Text.Trim(), inputMode);
                if (PrepareRetry(outcome))
                {
                    _typedBox.Enabled = true;
                    _typedSubmit.Enabled = true;
                    _typedBox.SelectAll();
                    _typedBox.Focus();
                    return;
                }
                ApplyFinalOutcome(outcome, -1);
            }
            catch { RecoverOrFail(); }
        }

        private void SubmitInteractive(string inputMode)
        {
            if (_question == null || !UsesInteractiveAnswer(_question) || _submitting || !_interactiveAnswer.HasAnswer) return;
            _submitting = true;
            _interactiveSubmit.Enabled = false;
            _hintButton.Enabled = false;
            try
            {
                var outcome = SubmitCurrent(_interactiveAnswer.SelectedAnswer, inputMode);
                if (PrepareRetry(outcome))
                {
                    _interactiveSubmit.Enabled = _interactiveAnswer.HasAnswer;
                    _interactiveAnswer.Focus();
                    return;
                }
                _interactiveAnswer.ShowResult(outcome.IsCorrect);
                ApplyFinalOutcome(outcome, -1);
            }
            catch { RecoverOrFail(); }
        }

        private void ApplyFinalOutcome(MathAnswerOutcome outcome, int selectedChoiceIndex)
        {
            if (outcome == null) throw new InvalidOperationException("Missing Math answer outcome.");
            _retryPending = false;
            if (_question != null && !UsesTypedAnswer(_question) && !UsesInteractiveAnswer(_question))
            {
                var correctIndex = FindCorrectChoiceIndex();
                for (var i = 0; i < 4; i++)
                {
                    if (!_choiceButtons[i].Visible) continue;
                    if (i == correctIndex)
                    {
                        _choiceButtons[i].VisualState = AnswerChoiceButton.ChoiceVisualState.Correct;
                        _choiceButtons[i].BadgeText = "✓";
                    }
                    else if (i == selectedChoiceIndex && !outcome.IsCorrect)
                    {
                        _choiceButtons[i].VisualState = AnswerChoiceButton.ChoiceVisualState.Incorrect;
                        _choiceButtons[i].BadgeText = "×";
                    }
                    else
                    {
                        _choiceButtons[i].VisualState = AnswerChoiceButton.ChoiceVisualState.Muted;
                        _choiceButtons[i].BadgeText = string.Empty;
                    }
                }
            }
            _feedback.Text = outcome.FeedbackVi;
            _feedbackCard.CardColor = outcome.IsCorrect ? Color.FromArgb(226, 242, 224) : Color.FromArgb(251, 232, 222);
            _feedbackCard.BorderColor = outcome.IsCorrect ? Color.FromArgb(190, 221, 188) : Color.FromArgb(236, 202, 187);
            _feedbackCard.Visible = true;
            _companion.State = outcome.SuggestPositiveEnd ? CompanionReactionState.Tired : (outcome.IsCorrect ? CompanionReactionState.Correct : CompanionReactionState.TryAgain);
            _feedbackFx.VisualMood = outcome.IsCorrect ? GameFeedbackFxControl.Mood.Correct : GameFeedbackFxControl.Mood.Retry;
            _support.Text = BuildOutcomeSupport(outcome);
            UpdateProgress(outcome.CompletedQuestionCount, outcome.CompletedQuestionCount, false, true);
            _hintButton.Visible = false;
            _nextButton.Visible = true;
            _breakOnNext = _rescueBridge != null && outcome.SuggestPositiveEnd && outcome.CompletedQuestionCount < outcome.TargetQuestionCount;
            _completeOnNext = outcome.CompletedQuestionCount >= outcome.TargetQuestionCount || (_rescueBridge == null && outcome.SuggestPositiveEnd);
            _nextButton.Text = outcome.SuggestPositiveEnd ? "Nghỉ ở đây" : (outcome.CompletedQuestionCount >= outcome.TargetQuestionCount ? "Xem kết quả" : "Câu tiếp theo");
            _nextButton.Focus();
        }

        private string BuildOutcomeSupport(MathAnswerOutcome outcome)
        {
            if (_eventPresentation == null)
                return outcome.IsCorrect ? "Tốt rồi. Câu này đã được lưu để lần sau ôn đúng lúc." : "Đáp án đúng: " + outcome.CorrectAnswerDisplay + ". Câu sau sẽ giúp con luyện tiếp.";
            if (outcome.SuggestPositiveEnd) return _eventPresentation.BreakCopyVi;
            if (outcome.IsCorrect)
                return "Đã sửa xong " + _eventPresentation.CheckpointName(Math.Max(0, outcome.CompletedQuestionCount - 1)) + ". Phần này đã được lưu.";
            return _eventPresentation.RepairCopyVi + " Đáp án đúng: " + outcome.CorrectAnswerDisplay + ".";
        }

        private void UpdateProgress(int completed, int questionNumber, bool retry, bool afterAnswer)
        {
            var target = Math.Max(1, _targetQuestionCount);
            var safeCompleted = Math.Max(0, Math.Min(target, completed));
            _progress.Maximum = target;
            _progress.Value = safeCompleted;
            if (_eventPresentation == null)
            {
                _progress.Visible = true;
                _eventProgress.Visible = false;
                _progressText.Text = afterAnswer ? "Đã làm " + safeCompleted + " / " + target : "Câu " + Math.Max(1, questionNumber) + " / " + target + (retry ? " • thử lại" : string.Empty);
                return;
            }
            _progress.Visible = false;
            _eventProgress.Visible = true;
            var activeIndex = safeCompleted >= target ? target - 1 : Math.Max(0, Math.Min(target - 1, questionNumber - 1));
            var rewardChestReady = _rescueBridge != null && _rescueBridge.RewardSnapshot != null &&
                _rescueBridge.RewardSnapshot.FinalChestUnlocked;
            _eventProgress.SetState(safeCompleted, activeIndex, _eventPresentation.CheckpointNounsVi, rewardChestReady);
            _progressText.Text = safeCompleted >= target ? target + " / " + target + " chặng đã xong" : "Chặng " + (activeIndex + 1) + " / " + target + (retry ? " • thử lại" : string.Empty);
        }

        private void HandleNext()
        {
            if (_finished) { _back(); return; }
            if (_breakOnNext) { PauseAndBack(); return; }
            if (_completeOnNext) CompleteSession();
            else ShowNextQuestion();
        }

        private void CompleteSession()
        {
            if (_finished) return;
            try
            {
                MathSessionSummary summary;
                if (_rescueBridge != null)
                {
                    summary = _rescueBridge.CompleteGame();
                    _eventState = _rescueBridge.CurrentState.EventState;
                }
                else if (_gameEventCoordinator != null && _coordinator != null && _coordinator.IsActive)
                {
                    var completion = _gameEventCoordinator.Complete();
                    _eventState = completion.EventState;
                    summary = completion.LearningSummary;
                }
                else
                {
                    summary = _coordinator == null || !_coordinator.IsActive ? null : _coordinator.Complete();
                }
                _finished = true;
                _suspendOnLeave = false;
                ShowCompletion(summary);
            }
            catch { ShowFatal("Buổi học đã kết thúc. Những câu đã lưu trước đó vẫn được giữ lại."); }
        }

        private void ShowCompletion(MathSessionSummary summary)
        {
            _question = null;
            _instructionVisual.SetQuestion(null, 0);
            _instructionVisual.Visible = false;
            var unlocked = summary != null && summary.GardenUnlockedItemIds != null && summary.GardenUnlockedItemIds.Count > 0 ? summary.GardenUnlockedItemIds[0] : null;
            _completionVisual.SetProgress(summary == null ? 0 : summary.GardenGrowthSteps, unlocked,
                summary == null ? 0 : summary.SessionsUntilNextGardenMilestone,
                summary == null ? null : summary.NextGardenMilestoneItemId);
            _completionVisual.Visible = true;
            _companion.State = CompanionReactionState.Celebrate;
            _feedbackFx.VisualMood = GameFeedbackFxControl.Mood.Correct;
            _sceneLabel.Text = _eventPresentation == null ? "BÀI HỌC HOÀN THÀNH" : "NHIỆM VỤ HOÀN THÀNH";
            _prompt.Text = _eventPresentation == null ? "Hoàn thành bài học" : "Nhiệm vụ cứu hộ hoàn thành";
            _support.Text = BuildCompletionSupport(summary);
            _feedback.Text = BuildCompletionPerformance(summary);
            _feedbackCard.CardColor = Color.FromArgb(226, 242, 224);
            _feedbackCard.BorderColor = Color.FromArgb(190, 221, 188);
            _feedbackCard.Visible = true;
            _choiceGrid.Visible = false;
            _typedLayout.Visible = false;
            _interactiveLayout.Visible = false;
            _pauseButton.Visible = false;
            _hintButton.Visible = false;
            _nextButton.Visible = true;
            _nextButton.IconAssetPath = "10_UIIcons/icon_reward.png";
            _nextButton.IconSize = 22;
            _nextButton.Text = _eventPresentation == null ? "Về thế giới Toán" : "Về bản đồ cứu hộ";
            _nextButton.AccessibleName = _nextButton.Text;
            _completeOnNext = false;
            UpdateProgress(_targetQuestionCount, _targetQuestionCount, false, true);
            _nextButton.Focus();
        }

        private string BuildCompletionPerformance(MathSessionSummary summary)
        {
            if (summary == null) return "Kết quả chưa sẵn sàng. Phần đã làm vẫn được lưu.";
            var attempts = Math.Max(0, summary.Attempts);
            var independent = Math.Max(0, Math.Min(attempts, summary.IndependentCorrect));
            var hinted = Math.Max(0, Math.Min(attempts, summary.HintedCorrect));
            var retried = Math.Max(0, Math.Min(attempts, summary.RetriedCorrect));
            var text = "Đã làm " + attempts + " câu • tự làm đúng " + independent + " • đúng nhờ gợi ý " + hinted;
            if (retried > 0) text += " • sửa lại đúng " + retried;
            if (summary.LessonScorePercent.HasValue)
                text = "Điểm bài " + Math.Round(summary.LessonScorePercent.Value) + "% • " + text;
            return text;
        }

        private string BuildCompletionSupport(MathSessionSummary summary)
        {
            if (_eventPresentation != null)
            {
                var text = _eventPresentation.CompletionVi;
                if (summary != null && summary.GardenGrowthSteps > 0) text += " Khu vườn đã ghi nhận tiến bộ của con.";
                if (summary != null && !string.IsNullOrWhiteSpace(summary.GardenUnlockMessage)) text += " " + summary.GardenUnlockMessage.Trim();
                return text;
            }
            if (summary == null) return "Các câu đã làm được lưu an toàn.";
            var result = "Con đã luyện " + Math.Max(0, summary.DistinctSkills) + " kỹ năng trong bài này.";
            if (!string.IsNullOrWhiteSpace(summary.NextLessonTitleVi)) result += " Bài tiếp theo: " + summary.NextLessonTitleVi + ".";
            if (summary.GardenGrowthSteps > 0) result += " Khu vườn đã ghi nhận tiến bộ của con.";
            return result;
        }

        private void PauseAndBack()
        {
            if (_finished) { _back(); return; }
            try
            {
                if (_rescueBridge != null && _rescueBridge.IsInteractive)
                    _rescueBridge.SuspendForBreak("child_requested_break");
                else if (_gameEventCoordinator != null && _coordinator != null && _coordinator.IsActive)
                    _gameEventCoordinator.SuspendForBreak("child_requested_break");
                else if (_coordinator != null && _coordinator.IsActive)
                    _coordinator.Suspend("child_requested_stop");
            }
            catch { }
            _suspendOnLeave = false;
            _back();
        }

        private void RecoverOrFail()
        {
            try
            {
                if (_coordinator == null || !_coordinator.IsActive || !_coordinator.HasOpenQuestion || _question == null)
                {
                    ShowFatal("Không thể lưu câu vừa làm. Buổi học sẽ dừng để bảo vệ dữ liệu.");
                    return;
                }
                var restored = _coordinator.NextQuestion();
                if (restored == null || !string.Equals(restored.QuestionId, _question.QuestionId, StringComparison.Ordinal))
                {
                    ShowFatal("Không thể lưu câu vừa làm. Buổi học sẽ dừng để bảo vệ dữ liệu.");
                    return;
                }
                _question = restored;
                _submitting = false;
                _feedback.Text = "Chưa lưu được câu này. Mình thử lại chính câu này nhé.";
                _feedbackCard.CardColor = Color.FromArgb(245, 239, 224);
                _feedbackCard.Visible = true;
                _support.Text = "Phần đã làm trước đó vẫn an toàn. Câu này chưa được tính.";
                _hintButton.Visible = true;
                _hintButton.Enabled = _hintLevel < 2;
                if (UsesTypedAnswer(_question))
                {
                    _typedBox.Enabled = true;
                    _typedSubmit.Enabled = !string.IsNullOrWhiteSpace(_typedBox.Text);
                    _typedBox.Focus();
                }
                else if (UsesInteractiveAnswer(_question))
                {
                    _interactiveSubmit.Enabled = _interactiveAnswer.HasAnswer;
                    _interactiveAnswer.Focus();
                }
                else
                {
                    SetChoiceEnabled(true);
                    _choiceButtons[0].Focus();
                }
            }
            catch { ShowFatal("Không thể lưu câu vừa làm. Buổi học sẽ dừng để bảo vệ dữ liệu."); }
        }

        private void ShowFatal(string message)
        {
            try
            {
                if (_rescueBridge != null && _rescueBridge.IsInteractive) _rescueBridge.SuspendForBreak("runtime_ui_error");
                else if (_coordinator != null && _coordinator.IsActive) _coordinator.Abort("runtime_ui_error");
            }
            catch { }
            _finished = true;
            _suspendOnLeave = false;
            _question = null;
            _instructionVisual.SetQuestion(null, 0);
            _instructionVisual.Visible = false;
            _completionVisual.Visible = false;
            _companion.State = CompanionReactionState.Tired;
            _feedbackFx.VisualMood = GameFeedbackFxControl.Mood.Retry;
            _sceneLabel.Text = "MÌNH DỪNG Ở ĐÂY NHÉ";
            _prompt.Text = "Mình dừng ở đây nhé";
            _support.Text = message;
            _feedback.Text = "Những dữ liệu đã lưu trước đó vẫn an toàn.";
            _feedbackCard.Visible = true;
            _choiceGrid.Visible = false;
            _typedLayout.Visible = false;
            _interactiveLayout.Visible = false;
            _pauseButton.Visible = false;
            _hintButton.Visible = false;
            _nextButton.Visible = true;
            _nextButton.Text = _eventPresentation == null ? "Về thế giới Toán" : "Về bản đồ cứu hộ";
            _completeOnNext = false;
        }

        private void DisposeCoordinator()
        {
            if (_rescueBridge != null)
            {
                _rescueBridge.Dispose();
                _rescueBridge = null;
            }
            if (_gameEventCoordinator != null)
            {
                _gameEventCoordinator.Dispose();
                _gameEventCoordinator = null;
                _coordinator = null;
            }
            else if (_coordinator != null)
            {
                _coordinator.Dispose();
                _coordinator = null;
            }
        }

        private int FindCorrectChoiceIndex()
        {
            if (_question == null || _question.DisplayChoices == null) return -1;
            for (var i = 0; i < _question.DisplayChoices.Count; i++)
                if (string.Equals(_question.DisplayChoices[i], _question.CorrectAnswerDisplay, StringComparison.Ordinal)) return i;
            return -1;
        }

        private void SetChoiceEnabled(bool enabled)
        {
            for (var i = 0; i < _choiceButtons.Length; i++)
                if (_choiceButtons[i].Visible) _choiceButtons[i].Enabled = enabled;
        }

        private void FocusCurrentInput()
        {
            if (_question == null) return;
            if (UsesTypedAnswer(_question)) _typedBox.Focus();
            else if (UsesInteractiveAnswer(_question)) _interactiveAnswer.Focus();
            else if (_choiceButtons[0].Visible) _choiceButtons[0].Focus();
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
            if (string.Equals(question.AnswerKind, "unit", StringComparison.Ordinal)) return "Nhập kết quả kèm đơn vị, ví dụ: 5 kg.";
            if (string.Equals(question.AnswerKind, "expression", StringComparison.Ordinal)) return "Nhập kết quả hoặc một biểu thức số tương đương.";
            return "Nhập đáp án rồi bấm Kiểm tra đáp án.";
        }

        private void ApplyPromptTypography(string text)
        {
            _prompt.Text = text ?? string.Empty;
            var width = Math.Max(220, _prompt.ClientSize.Width > 0 ? _prompt.ClientSize.Width : 520);
            var height = Math.Max(54, _prompt.ClientSize.Height > 0 ? _prompt.ClientSize.Height : 96);
            var sizes = new[] { 24f, 21f, 18f, 16f, 14f, 12f };
            var selected = 12f;
            foreach (var size in sizes)
            {
                using (var font = ChildVisualTheme.Font(size, FontStyle.Bold))
                {
                    var measured = TextRenderer.MeasureText(_prompt.Text, font, new Size(width - 4, 4096), TextFormatFlags.WordBreak);
                    if (measured.Height <= height - 4) { selected = size; break; }
                }
            }
            _prompt.Font = ChildVisualTheme.Font(selected, FontStyle.Bold);
        }

        protected override void ApplyLayoutProfile(LearnerLayoutProfile profile)
        {
            if (_body == null || _questionCard == null || _visualCard == null) return;
            if (profile == LearnerLayoutProfile.Compact)
            {
                _body.ColumnStyles[0].Width = 31;
                _body.ColumnStyles[1].Width = 69;
                _questionCard.Padding = new Padding(12, 9, 12, 9);
                _visualCard.Padding = new Padding(6);
            }
            else if (profile == LearnerLayoutProfile.Wide)
            {
                _body.ColumnStyles[0].Width = 42;
                _body.ColumnStyles[1].Width = 58;
                _questionCard.Padding = new Padding(22, 18, 22, 18);
                _visualCard.Padding = new Padding(12);
            }
            else
            {
                _body.ColumnStyles[0].Width = 38;
                _body.ColumnStyles[1].Width = 62;
                _questionCard.Padding = new Padding(18, 14, 18, 14);
                _visualCard.Padding = new Padding(10);
            }
        }
    }
}
