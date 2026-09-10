using System.Drawing;
using System.Windows.Forms;

namespace WAHUKidsLearn
{
    internal static class LearnerDesignTokens
    {
        public const int SpaceXs = 4;
        public const int SpaceS = 8;
        public const int SpaceM = 12;
        public const int SpaceL = 16;
        public const int SpaceXl = 24;
        public const int SpaceXxl = 32;

        public const int RadiusSmall = 12;
        public const int RadiusButton = 16;
        public const int RadiusCard = 20;
        public const int RadiusHero = 28;

        public const int TouchMinimum = 48;
        public const int TouchPrimary = 56;
        public const int ContentMaxWidth = 1180;
        public const int ContentMinimumWidth = 220;
        public const int ArtMinimumWidth = 144;
        public const int ArtMaximumWidth = 320;
        public const int ArtPreferredPercent = 34;

        public static Padding PagePadding(LearnerLayoutProfile profile)
        {
            switch (profile)
            {
                case LearnerLayoutProfile.Compact:
                    return new Padding(SpaceL, SpaceM, SpaceL, SpaceM);
                case LearnerLayoutProfile.Wide:
                    return new Padding(SpaceXxl, SpaceXl, SpaceXxl, SpaceXl);
                default:
                    return new Padding(SpaceXl, SpaceL, SpaceXl, SpaceL);
            }
        }

        public static Size PrimaryButtonSize(LearnerLayoutProfile profile)
        {
            return profile == LearnerLayoutProfile.Compact
                ? new Size(260, TouchPrimary)
                : new Size(320, 60);
        }
    }
}
