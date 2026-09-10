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
            form.MinimumSize = Size.Empty;
            form.ClientSize = PositiveSize(preferredClientSize);
            ApplyMinimumClientSize(form, minimumClientSize);

            // Recalculate the non-client chrome after a DPI transition. MinimumSize is an
            // outer-window size, while every learner layout contract is expressed in client
            // pixels. Without this conversion the title bar/borders steal part of 900x640.
            form.DpiChanged += delegate { ApplyMinimumClientSize(form, minimumClientSize); };

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

        public static int AvailableVerticalFlowChildWidth(FlowLayoutPanel flow, int reservedHorizontalSpace, int minimumWidth)
        {
            if (flow == null) throw new ArgumentNullException("flow");

            var viewportWidth = Math.Max(0, flow.ClientSize.Width - flow.Padding.Horizontal);
            if (!flow.AutoScroll && flow.DisplayRectangle.Width > 0)
                viewportWidth = Math.Min(viewportWidth, flow.DisplayRectangle.Width);
            if (flow.AutoScroll && flow.VerticalScroll.Visible)
                viewportWidth = Math.Max(0, viewportWidth - SystemInformation.VerticalScrollBarWidth);

            var available = Math.Max(0, viewportWidth - Math.Max(0, reservedHorizontalSpace));
            if (available > 0) return available;
            return Math.Max(1, minimumWidth);
        }

        public static Padding ResponsiveHorizontalPadding(int clientWidth, int roomy, int compact)
        {
            return new Padding(clientWidth <= 1024 ? compact : roomy, 0, clientWidth <= 1024 ? compact : roomy, 0);
        }

        private static void ApplyMinimumClientSize(Form form, Size minimumClientSize)
        {
            if (form == null || form.IsDisposed) return;
            var requested = PositiveSize(minimumClientSize);

            // WinForms MinimumSize is the total window size. The chrome delta is DPI-aware once
            // the Form has adopted its current scaling context, and is still well-defined before
            // Show(), which keeps offscreen tests deterministic.
            var chromeWidth = Math.Max(0, form.Width - form.ClientSize.Width);
            var chromeHeight = Math.Max(0, form.Height - form.ClientSize.Height);
            form.MinimumSize = new Size(requested.Width + chromeWidth, requested.Height + chromeHeight);
        }

        private static Size PositiveSize(Size size)
        {
            return new Size(Math.Max(1, size.Width), Math.Max(1, size.Height));
        }
    }
}
