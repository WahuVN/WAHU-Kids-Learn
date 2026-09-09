using System;
using System.Drawing;

namespace WAHUKidsLearn
{
    internal enum LearnerLayoutProfile
    {
        Compact = 0,
        Standard = 1,
        Wide = 2
    }

    internal static class LearnerLayoutProfileResolver
    {
        public static LearnerLayoutProfile Resolve(Size clientSize, int deviceDpi)
        {
            var dpi = Math.Max(96, deviceDpi);
            var logicalWidth = (int)Math.Round(clientSize.Width * 96.0 / dpi);
            var logicalHeight = (int)Math.Round(clientSize.Height * 96.0 / dpi);

            if (logicalWidth <= 1023 || logicalHeight <= 700)
                return LearnerLayoutProfile.Compact;
            if (logicalWidth >= 1360 && logicalHeight >= 720)
                return LearnerLayoutProfile.Wide;
            return LearnerLayoutProfile.Standard;
        }
    }
}
