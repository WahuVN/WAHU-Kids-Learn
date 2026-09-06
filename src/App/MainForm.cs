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
        private GardenWorldControl _garden;
        private MathRoadmapControl _mathRoadmap;
        private Label _gardenProgress;
        private Label _missionSummary;
        private ChildActionButton _mathButton;
        private Label _safeIssue;

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
            MinimumSize = new Size(900, 640);
            ClientSize = new Size(1080, 720);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = ChildVisualTheme.Font(11f);
            BackColor = ChildVisualTheme.Cream;
            KeyPreview = true;
            DoubleBuffered = true;
            BuildUi();
            Shown += delegate { RefreshHomeProgress(); };
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = ChildVisualTheme.Cream,
                Padding = new Padding(26, 20, 26, 18),
                ColumnCount = 2,
                RowCount = 3
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

            var brand = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(6, 0, 8, 0) };
            brand.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            brand.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            brand.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "WAHU Kids Learn",
                TextAlign = ContentAlignment.BottomLeft,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(25f, FontStyle.Bold),
                AccessibleName = "WAHU Kids Learn"
            }, 0, 0);
            brand.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Mỗi ngày một nhiệm vụ nhỏ, học chắc rồi mới đi tiếp.",
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(10.5f),
                AccessibleName = "Lời chào"
            }, 0, 1);
            root.Controls.Add(brand, 0, 0);

            var topActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 10, 4, 8)
            };
            var parent = new ChildActionButton
            {
                Size = new Size(144, 44),
                Text = _issue != null && _issue.CanRecoverDatabase ? "Phụ huynh" : "Phụ huynh",
                Font = ChildVisualTheme.Font(10.5f, FontStyle.Bold),
                FillColor = Color.FromArgb(232, 230, 220),
                HoverColor = Color.FromArgb(220, 218, 207),
                PressedColor = Color.FromArgb(208, 205, 194),
                TextColor = ChildVisualTheme.Ink,
                Radius = 16,
                AccessibleName = "Mở chế độ phụ huynh"
            };
            parent.Click += delegate { OpenParentMode(); };
            topActions.Controls.Add(parent);
            root.Controls.Add(topActions, 1, 0);

            var gardenCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 8, 14, 8),
                Padding = new Padding(12),
                CardColor = Color.FromArgb(253, 251, 242),
                BorderColor = Color.FromArgb(222, 220, 207),
                Radius = 26
            };
            var gardenLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            gardenLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            gardenLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            gardenLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
            gardenLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            gardenLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Khu vườn của bé",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Font = ChildVisualTheme.Font(16f, FontStyle.Bold),
                ForeColor = ChildVisualTheme.Ink,
                AccessibleName = "Khu vườn của bé"
            }, 0, 0);
            _garden = new GardenWorldControl { Dock = DockStyle.Fill, Margin = new Padding(3), GrowthLevel = 1 };
            gardenLayout.Controls.Add(_garden, 0, 1);

            var roadmapCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 4, 6, 6),
                Padding = new Padding(12, 7, 12, 7),
                CardColor = Color.FromArgb(248, 247, 239),
                BorderColor = Color.FromArgb(228, 225, 212),
                Radius = 18
            };
            var roadmapLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            roadmapLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            roadmapLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            roadmapLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Lộ trình Toán",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(10.5f, FontStyle.Bold),
                AccessibleName = "Lộ trình Toán lớp 2"
            }, 0, 0);
            _mathRoadmap = new MathRoadmapControl { Dock = DockStyle.Fill };
            roadmapLayout.Controls.Add(_mathRoadmap, 0, 1);
            roadmapCard.Controls.Add(roadmapLayout);
            gardenLayout.Controls.Add(roadmapCard, 0, 2);

            _gardenProgress = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Làm vài câu Toán để khu vườn lớn dần nhé.",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(10f),
                AccessibleName = "Tiến bộ khu vườn"
            };
            gardenLayout.Controls.Add(_gardenProgress, 0, 3);
            gardenCard.Controls.Add(gardenLayout);
            root.Controls.Add(gardenCard, 0, 1);

            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(2, 8, 4, 8)
            };
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 27));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 15));

            var mission = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(22, 18, 22, 18),
                CardColor = Color.FromArgb(255, 253, 246),
                Radius = 24
            };
            var missionLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            missionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            missionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            missionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            missionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            missionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            missionLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "NHIỆM VỤ HÔM NAY",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.MintStrong,
                Font = ChildVisualTheme.Font(9.5f, FontStyle.Bold)
            }, 0, 0);
            missionLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "8 câu Toán vừa sức",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(19f, FontStyle.Bold),
                AccessibleName = "Nhiệm vụ Toán hôm nay"
            }, 0, 1);
            _missionSummary = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Ứng dụng sẽ chọn câu dựa trên phần bé đang cần luyện và lần ôn đã tới.",
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(10.5f),
                Padding = new Padding(0, 8, 0, 0),
                AccessibleName = "Mô tả nhiệm vụ"
            };
            missionLayout.Controls.Add(_missionSummary, 0, 2);
            _mathButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 6),
                Text = "Bắt đầu Toán",
                BadgeText = "8",
                Font = ChildVisualTheme.Font(16f, FontStyle.Bold),
                FillColor = ChildVisualTheme.MintStrong,
                HoverColor = Color.FromArgb(90, 156, 103),
                PressedColor = Color.FromArgb(75, 139, 88),
                Radius = 20,
                AccessibleName = "Bắt đầu học Toán",
                AccessibleDescription = "Mở phiên học Toán lớp 2 gồm khoảng tám câu."
            };
            _mathButton.Enabled = IsLearnerReady();
            _mathButton.Click += delegate { OpenMathLesson(); };
            missionLayout.Controls.Add(_mathButton, 0, 3);
            missionLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Không cần học thật lâu. Làm chắc từng chút là được.",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9.5f)
            }, 0, 4);
            mission.Controls.Add(missionLayout);
            right.Controls.Add(mission, 0, 0);

            var english = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(18, 12, 18, 12),
                CardColor = Color.FromArgb(237, 246, 250),
                BorderColor = Color.FromArgb(210, 229, 239),
                Radius = 22
            };
            var englishLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            englishLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
            englishLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            englishLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            englishLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            englishLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Tiếng Anh",
                TextAlign = ContentAlignment.BottomLeft,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(14f, FontStyle.Bold)
            }, 0, 0);
            englishLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Sắp mở",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ChildVisualTheme.SkyStrong,
                Font = ChildVisualTheme.Font(9.5f, FontStyle.Bold)
            }, 1, 0);
            englishLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Từ vựng và thứ trong tuần đang được chuẩn bị.",
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9.5f)
            }, 0, 1);
            englishLayout.SetColumnSpan(englishLayout.GetControlFromPosition(0, 1), 2);
            english.Controls.Add(englishLayout);
            right.Controls.Add(english, 0, 1);

            _safeIssue = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = _issue == null ? ChildVisualTheme.MutedInk : ChildVisualTheme.SoftRed,
                Font = ChildVisualTheme.Font(9.5f, _issue == null ? FontStyle.Regular : FontStyle.Bold),
                Text = BuildChildSafeFooter(),
                AccessibleName = "Thông báo ứng dụng"
            };
            right.Controls.Add(_safeIssue, 0, 2);
            root.Controls.Add(right, 1, 1);

            var footer = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Học nhẹ • Không mất chuỗi ngày • Có thể dừng bất cứ lúc nào",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9.5f),
                AccessibleName = "Nguyên tắc buổi học"
            };
            root.Controls.Add(footer, 0, 2);
            root.SetColumnSpan(footer, 2);
            Controls.Add(root);
        }

        private void OpenMathLesson()
        {
            if (!IsLearnerReady()) return;
            try
            {
                using (var lesson = new MathLessonForm(_learningDatabase, _performance))
                    lesson.ShowDialog(this);
                RefreshHomeProgress();
            }
            catch
            {
                MessageBox.Show(this,
                    "Chưa thể mở buổi Toán lúc này. Nhờ người lớn mở mục Phụ huynh để kiểm tra nhé.",
                    "WAHU Kids Learn", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void RefreshHomeProgress()
        {
            if (!IsLearnerReady() || _garden == null) return;
            try
            {
                var summary = ParentSummaryService.Read(_learningDatabase);
                var world = new GameWorldRewardService(_learningDatabase).ReadProgress(LearnerSessionService.PrimaryChildId);
                var roadmap = new MathRoadmapService(_learningDatabase).Read(LearnerSessionService.PrimaryChildId);
                _mathRoadmap.SetSnapshot(roadmap);
                var growth = Math.Min(8, Math.Max(1, 1 + world.GrowthSteps));
                _garden.GrowthLevel = growth;
                _garden.HasSeedling = world.UnlockedItems.Contains("garden_seedling");
                _garden.HasFlowerPatch = world.UnlockedItems.Contains("garden_flower_patch");
                _garden.HasLantern = world.UnlockedItems.Contains("garden_lantern");
                _garden.HasBench = world.UnlockedItems.Contains("garden_bench");
                _garden.Invalidate();
                if (summary.AttemptCount == 0)
                {
                    _gardenProgress.Text = "Khu vườn đang chờ nhiệm vụ đầu tiên của bé.";
                    _missionSummary.Text = "Ứng dụng sẽ chọn câu dựa trên phần bé đang cần luyện và lần ôn đã tới.";
                }
                else
                {
                    _gardenProgress.Text = world.GrowthSteps > 0
                        ? "Khu vườn đã lớn " + world.GrowthSteps + " bước." + BuildNextGardenMilestoneText(world)
                        : "Bé đã làm " + summary.AttemptCount + " câu. Hoàn thành một nhiệm vụ để khu vườn lớn thêm nhé.";
                    if (summary.ReviewSkillCount > 0)
                        _missionSummary.Text = "Có " + summary.ReviewSkillCount + " phần đã tới lúc ôn. Buổi Toán sẽ ưu tiên chúng trước.";
                    else if (summary.LearningSkillCount > 0)
                        _missionSummary.Text = "Bé đang xây chắc " + summary.LearningSkillCount + " kỹ năng. Mình tiếp tục đúng chỗ nhé.";
                    else
                        _missionSummary.Text = "Buổi Toán sẽ trộn câu quen và câu mới vừa sức để nhớ lâu hơn.";
                }
            }
            catch
            {
                _gardenProgress.Text = "Khu vườn vẫn an toàn. Tiến bộ sẽ hiện lại khi dữ liệu sẵn sàng.";
            }
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

        private static string BuildNextGardenMilestoneText(GameWorldProgress world)
        {
            if (world == null || world.SessionsUntilNextMilestone <= 0 || string.IsNullOrWhiteSpace(world.NextMilestoneItemId))
                return " Các mốc hiện tại đã mở đủ.";
            return " Còn " + world.SessionsUntilNextMilestone + " nhiệm vụ tới " + GardenItemName(world.NextMilestoneItemId) + ".";
        }

        private static string GardenItemName(string itemId)
        {
            switch (itemId)
            {
                case "garden_seedling": return "Mầm cây mới";
                case "garden_flower_patch": return "Bồn hoa";
                case "garden_lantern": return "Đèn vườn";
                case "garden_bench": return "Ghế nhỏ";
                default: return "mốc khu vườn";
            }
        }

        private string BuildChildSafeFooter()
        {
            if (_issue != null && _issue.Kind != RuntimeIssueKind.None)
                return (_issue.ChildMessage ?? "Ứng dụng cần người lớn kiểm tra.") + " Nhờ người lớn mở Phụ huynh nhé.";
            if (_previousRunUnclean)
                return "Phiên trước đóng bất ngờ, dữ liệu đã được kiểm tra an toàn.";
            return "Sẵn sàng cho một nhiệm vụ nhỏ.";
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
                RefreshHomeProgress();
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
