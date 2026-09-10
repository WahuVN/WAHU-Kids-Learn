using System;
using System.Drawing;
using System.Windows.Forms;
using WAHU.Data;

namespace WAHUKidsLearn
{
    internal sealed class LearnerHomePage : LearnerPage
    {
        private readonly LearnerShellContext _context;
        private readonly Action<LearnerRoute> _navigate;
        private ChildSceneLayout _root;
        private ChildCard _heroCard;
        private TableLayoutPanel _heroLayout;
        private RescueHeroArtControl _heroArt;
        private Label _missionSummary;
        private ChildActionButton _primaryMissionButton;
        private ChildActionButton _mathWorldButton;
        private ChildActionButton _typingSpaceButton;
        private ChildCard _gardenCard;
        private GardenWorldControl _garden;
        private Label _gardenProgress;
        private Label _safeStatus;

        public LearnerHomePage(LearnerShellContext context, Action<LearnerRoute> navigate)
        {
            _context = context ?? throw new ArgumentNullException("context");
            _navigate = navigate ?? throw new ArgumentNullException("navigate");
            AccessibleName = "Trang chủ học tập";
            BuildUi();
        }

        public override LearnerRoute Route { get { return LearnerRoute.Home; } }
        public override string PageTitle { get { return "Hôm nay"; } }

        public override void OnNavigatedTo()
        {
            RefreshProgress();
            if (_primaryMissionButton != null && _primaryMissionButton.Enabled)
                _primaryMissionButton.Focus();
        }

        private void BuildUi()
        {
            _root = new ChildSceneLayout
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = LearnerDesignTokens.PagePadding(LearnerLayoutProfile.Standard)
            };
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));

            var intro = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            intro.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            intro.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
            intro.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Nhiệm vụ hôm nay",
                TextAlign = ContentAlignment.BottomLeft,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(24f, FontStyle.Bold),
                AccessibleName = "Nhiệm vụ hôm nay"
            }, 0, 0);
            intro.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Một việc nhỏ, học chắc rồi mới đi tiếp.",
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(10.5f),
                AccessibleName = "Lời nhắc học tập"
            }, 0, 1);
            _root.Controls.Add(intro, 0, 0);

            _heroCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, LearnerDesignTokens.SpaceS, 0, LearnerDesignTokens.SpaceM),
                Padding = new Padding(LearnerDesignTokens.SpaceXl),
                CardColor = Color.FromArgb(255, 253, 246),
                BorderColor = Color.FromArgb(236, 213, 177),
                Radius = LearnerDesignTokens.RadiusHero
            };
            _heroLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            _heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            _heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            _heroLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var copy = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Margin = Padding.Empty };
            copy.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            copy.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            copy.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            copy.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            copy.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            copy.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "TOÁN • 3 CHẶNG • KHÔNG ĐẾM GIỜ",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.MintStrong,
                Font = ChildVisualTheme.Font(9f, FontStyle.Bold)
            }, 0, 0);
            copy.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Rừng Toán đang cần bé giúp",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.Ink,
                Font = ChildVisualTheme.Font(20f, FontStyle.Bold),
                AccessibleName = "Tên nhiệm vụ được đề xuất"
            }, 0, 1);
            _missionSummary = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Bắt đầu một nhiệm vụ ngắn hoặc tiếp tục đúng phần đang cần luyện.",
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, LearnerDesignTokens.SpaceS, LearnerDesignTokens.SpaceM, 0),
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(11f),
                AutoEllipsis = true,
                UseMnemonic = false,
                AccessibleName = "Mô tả nhiệm vụ hôm nay"
            };
            copy.Controls.Add(_missionSummary, 0, 2);
            _primaryMissionButton = new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, LearnerDesignTokens.SpaceS, LearnerDesignTokens.SpaceL, LearnerDesignTokens.SpaceXs),
                Text = "Bắt đầu cứu hộ",
                BadgeText = "3",
                IconAssetPath = "10_UIIcons/icon_reward.png",
                IconSize = 28,
                FillColor = Color.FromArgb(232, 174, 93),
                HoverColor = Color.FromArgb(220, 158, 77),
                PressedColor = Color.FromArgb(204, 142, 64),
                TextColor = Color.FromArgb(79, 55, 31),
                BorderColor = Color.FromArgb(210, 145, 65),
                BorderThickness = 1.2f,
                Depth = 5,
                Radius = LearnerDesignTokens.RadiusButton,
                Font = ChildVisualTheme.Font(12f, FontStyle.Bold),
                AccessibleName = "Mở nhiệm vụ cứu hộ hôm nay",
                AccessibleDescription = "Mở ba chặng Toán. Không có đồng hồ đếm ngược và có thể nghỉ bất cứ lúc nào."
            };
            _primaryMissionButton.Enabled = _context.IsLearnerReady;
            _primaryMissionButton.Click += delegate { _navigate(LearnerRoute.RescueMap); };
            copy.Controls.Add(_primaryMissionButton, 0, 3);
            copy.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Phần đã làm luôn được lưu.",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9f)
            }, 0, 4);
            _heroLayout.Controls.Add(copy, 0, 0);

            _heroArt = new RescueHeroArtControl(_context.Performance)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(LearnerDesignTokens.SpaceM, 0, 0, 0),
                AccessibleName = "Minh họa nhiệm vụ Toán hôm nay"
            };
            _heroLayout.Controls.Add(_heroArt, 1, 0);
            _heroCard.Controls.Add(_heroLayout);
            _root.Controls.Add(_heroCard, 0, 1);

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _mathWorldButton = BuildBottomButton("Học theo bài", "10_UIIcons/icon_math.png", ChildVisualTheme.Sky, ChildVisualTheme.SkyStrong);
            _mathWorldButton.AccessibleName = "Mở thế giới Toán và học theo bài";
            _mathWorldButton.Click += delegate { _navigate(LearnerRoute.MathWorld); };
            _mathWorldButton.Enabled = _context.IsLearnerReady;
            bottom.Controls.Add(_mathWorldButton, 0, 0);

            _typingSpaceButton = BuildBottomButton("Gõ phím Việt – Anh", "02_game_effects_v2/icon_typing.png",
                Color.FromArgb(205, 229, 247), ChildVisualTheme.SkyStrong);
            _typingSpaceButton.AccessibleName = "Mở Phi thuyền gõ phím Việt Anh";
            _typingSpaceButton.Click += delegate { _navigate(LearnerRoute.TypingSpace); };
            _typingSpaceButton.Enabled = _context.IsLearnerReady;
            bottom.Controls.Add(_typingSpaceButton, 1, 0);

            _gardenCard = new ChildCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(LearnerDesignTokens.SpaceS, LearnerDesignTokens.SpaceS, 0, 0),
                Padding = new Padding(LearnerDesignTokens.SpaceS),
                CardColor = Color.FromArgb(248, 251, 244),
                BorderColor = Color.FromArgb(214, 229, 208),
                Radius = LearnerDesignTokens.RadiusCard,
                AccessibleName = "Khu vườn tiến bộ của bé"
            };
            var gardenLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
            gardenLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            gardenLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            gardenLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            gardenLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            gardenLayout.Controls.Add(new ChildAssetIconControl
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(2),
                AssetPath = "10_UIIcons/icon_garden.png",
                Inset = 3,
                AccessibleName = "Biểu tượng khu vườn"
            }, 0, 0);
            gardenLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Khu vườn của bé",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.MintStrong,
                Font = ChildVisualTheme.Font(10f, FontStyle.Bold),
                AccessibleName = "Khu vườn của bé"
            }, 1, 0);
            _garden = new GardenWorldControl { Dock = DockStyle.Fill, Margin = Padding.Empty, GrowthLevel = 1 };
            gardenLayout.Controls.Add(_garden, 0, 1);
            _gardenProgress = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Khu vườn đang chờ nhiệm vụ đầu tiên.",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ChildVisualTheme.MutedInk,
                Font = ChildVisualTheme.Font(9f),
                Padding = new Padding(8, 0, 2, 0),
                AutoEllipsis = true,
                UseMnemonic = false,
                AccessibleName = "Tiến bộ khu vườn"
            };
            gardenLayout.Controls.Add(_gardenProgress, 1, 1);
            _gardenCard.Controls.Add(gardenLayout);
            bottom.Controls.Add(_gardenCard, 2, 0);

            _root.Controls.Add(bottom, 0, 2);
            Controls.Add(_root);

            _safeStatus = new Label
            {
                AutoSize = true,
                Visible = false
            };
            ApplyLayoutProfile(LayoutProfile);
        }

        private ChildActionButton BuildBottomButton(string text, string icon, Color fill, Color accent)
        {
            return new ChildActionButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, LearnerDesignTokens.SpaceS, LearnerDesignTokens.SpaceS, 0),
                Text = text,
                IconAssetPath = icon,
                IconSize = 26,
                FillColor = ChildVisualTheme.Blend(fill, Color.White, 0.35f),
                HoverColor = ChildVisualTheme.Blend(fill, Color.White, 0.16f),
                PressedColor = ChildVisualTheme.Blend(fill, accent, 0.10f),
                TextColor = accent,
                BorderColor = ChildVisualTheme.Blend(fill, accent, 0.30f),
                BorderThickness = 1.2f,
                Radius = LearnerDesignTokens.RadiusCard,
                Font = ChildVisualTheme.Font(10.5f, FontStyle.Bold)
            };
        }

        protected override void ApplyLayoutProfile(LearnerLayoutProfile profile)
        {
            if (_root == null || _heroLayout == null || _heroCard == null) return;
            _root.SuspendLayout();
            _heroLayout.SuspendLayout();
            try
            {
                _root.Padding = LearnerDesignTokens.PagePadding(profile);
                if (profile == LearnerLayoutProfile.Compact)
                {
                    _root.RowStyles[0].Height = 54;
                    _root.RowStyles[2].Height = 126;
                    _heroCard.Padding = new Padding(LearnerDesignTokens.SpaceL, LearnerDesignTokens.SpaceM,
                        LearnerDesignTokens.SpaceL, LearnerDesignTokens.SpaceM);
                    _heroLayout.ColumnStyles[0].Width = 56;
                    _heroLayout.ColumnStyles[1].Width = 44;
                }
                else if (profile == LearnerLayoutProfile.Wide)
                {
                    _root.RowStyles[0].Height = 70;
                    _root.RowStyles[2].Height = 164;
                    _heroCard.Padding = new Padding(LearnerDesignTokens.SpaceXxl);
                    _heroLayout.ColumnStyles[0].Width = 48;
                    _heroLayout.ColumnStyles[1].Width = 52;
                }
                else
                {
                    _root.RowStyles[0].Height = 62;
                    _root.RowStyles[2].Height = 150;
                    _heroCard.Padding = new Padding(LearnerDesignTokens.SpaceXl);
                    _heroLayout.ColumnStyles[0].Width = 52;
                    _heroLayout.ColumnStyles[1].Width = 48;
                }
            }
            finally
            {
                _heroLayout.ResumeLayout(false);
                _root.ResumeLayout(true);
            }
        }

        private void RefreshProgress()
        {
            if (!_context.IsLearnerReady || _garden == null) return;
            try
            {
                var summary = ParentSummaryService.Read(_context.LearningDatabase);
                var childId = LearnerSessionService.PrimaryChildId;
                var worldService = new GameWorldRewardService(_context.LearningDatabase);
                try { worldService.ReconcileMissingCompletedMathSessionRewards(childId); } catch { }
                var world = worldService.ReadProgress(childId);
                var growth = Math.Min(8, Math.Max(1, 1 + world.GrowthSteps));
                _garden.GrowthLevel = growth;
                _garden.HasSeedling = world.UnlockedItems.Contains("garden_seedling");
                _garden.HasFlowerPatch = world.UnlockedItems.Contains("garden_flower_patch");
                _garden.HasLantern = world.UnlockedItems.Contains("garden_lantern");
                _garden.HasBench = world.UnlockedItems.Contains("garden_bench");
                _garden.Invalidate();

                if (summary.AttemptCount == 0)
                {
                    _gardenProgress.Text = "Khu vườn đang chờ nhiệm vụ đầu tiên.";
                    _missionSummary.Text = "Bắt đầu ba chặng ngắn. Ứng dụng sẽ chọn phần vừa sức và phần cần ôn.";
                }
                else
                {
                    _gardenProgress.Text = world.GrowthSteps > 0
                        ? "Khu vườn đã lớn " + world.GrowthSteps + " bước."
                        : "Bé đã làm " + summary.AttemptCount + " câu.";
                    if (summary.ReviewSkillCount > 0)
                        _missionSummary.Text = "Có " + summary.ReviewSkillCount + " phần đã tới lúc ôn. Nhiệm vụ hôm nay sẽ ưu tiên chúng trước.";
                    else if (summary.LearningSkillCount > 0)
                        _missionSummary.Text = "Bé đang xây chắc " + summary.LearningSkillCount + " kỹ năng. Mình tiếp tục đúng chỗ nhé.";
                    else
                        _missionSummary.Text = "Mình sẽ trộn câu quen và câu mới vừa sức để nhớ lâu hơn.";
                }
            }
            catch
            {
                _gardenProgress.Text = "Khu vườn vẫn an toàn. Tiến bộ sẽ hiện lại khi dữ liệu sẵn sàng.";
            }
        }
    }
}
