using System;
using System.Drawing;
using System.Windows.Forms;

namespace WAHUKidsLearn
{
    internal static class ChildWindowSizing
    {
        public static void ApplyLearnerWindowDefaults(Form form, Size preferredClientSize, Size minimumClientSize)
        {
            if (form == null) throw new ArgumentNullException("form");

            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.MinimumSize = minimumClientSize;
            form.ClientSize = preferredClientSize;

            // Learner-facing screens are easier to use full-screen on the small 1366x768-class
            // displays that are common on older Windows 7 machines. Child screens are opened
            // modally with an Owner in production; the main learner window is CenterScreen.
            // Keep ownerless CenterParent forms at their explicit client size so deterministic
            // layout/capture tests do not enter a transient maximized 100x23 layout.
            form.Shown += delegate
            {
                var shouldMaximize = form.Owner != null || form.StartPosition == FormStartPosition.CenterScreen;
                if (shouldMaximize && form.WindowState != FormWindowState.Maximized)
                    form.WindowState = FormWindowState.Maximized;
            };
        }

        public static Padding ResponsiveHorizontalPadding(int clientWidth, int roomy, int compact)
        {
            return new Padding(clientWidth <= 1024 ? compact : roomy, 0, clientWidth <= 1024 ? compact : roomy, 0);
        }
    }
}
