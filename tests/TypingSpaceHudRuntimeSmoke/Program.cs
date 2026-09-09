using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace WAHU.TypingSpaceHudRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var app = Assembly.LoadFrom(Path.Combine(Directory.GetCurrentDirectory(), "src", "App", "bin", "Release", "WAHUKidsLearn.exe"));
            TestHud(app);
            TestKeyboard(app);
            TestCompositeResponsive(app);
            Console.WriteLine("TYPING_SPACE_HUD_RUNTIME_SMOKE_PASS assertions=" + _assertions);
        }

        private static void TestHud(Assembly app)
        {
            var stateType = app.GetType("WAHUKidsLearn.TypingSpaceHudState", true);
            var feedbackType = app.GetType("WAHUKidsLearn.TypingSpaceHudFeedbackState", true);
            var hudType = app.GetType("WAHUKidsLearn.TypingSpaceHudControl", true);
            using (var hud = (Control)Activator.CreateInstance(hudType, true))
            {
                hud.Size = new Size(760, 230);
                var state = Activator.CreateInstance(stateType, true);
                Set(state, "TargetId", "hud_en_01");
                Set(state, "DisplayText", "apple");
                Set(state, "Language", "en");
                Set(state, "NextKey", "p");
                Set(state, "LastRawKey", "o");
                Set(state, "TypedCount", 1);
                Set(state, "TotalCount", 5);
                Set(state, "ErrorCount", 2);
                Set(state, "Feedback", Enum.Parse(feedbackType, "Wrong"));
                Invoke(hud, "SetState", state);
                LayoutTree(hud);
                A(Get<string>(hud, "LanguageText") == "ENGLISH", "hud_en_language_badge");
                A(Get<int>(hud, "ProgressValue") == 20, "hud_progress_1_of_5");
                A(Get<string>(hud, "FeedbackText").IndexOf("phím P", StringComparison.OrdinalIgnoreCase) >= 0,
                    "hud_hint_after_two_errors_names_next_key");
                A(Get<int>(hud, "PauseTouchHeight") >= 48, "hud_pause_touch_target_at_least_48px");
                A(hud.AccessibleDescription.IndexOf("Phím tiếp theo", StringComparison.OrdinalIgnoreCase) >= 0,
                    "hud_accessible_state_describes_next_key");
                A(hud.BackColor != Color.Red && hud.BackColor != Color.FromArgb(255, 0, 0),
                    "hud_wrong_feedback_never_turns_full_screen_red");
                Render(hud, 760, 230);

                Set(state, "Language", "vi");
                Set(state, "DisplayText", "mây");
                Set(state, "NextKey", "a");
                Set(state, "LastRawKey", "");
                Set(state, "TypedCount", 1);
                Set(state, "TotalCount", 3);
                Set(state, "ErrorCount", 0);
                Set(state, "Feedback", Enum.Parse(feedbackType, "Correct"));
                Invoke(hud, "SetState", state);
                A(Get<string>(hud, "LanguageText") == "TIẾNG VIỆT", "hud_vi_language_badge");
                A(Get<string>(hud, "FeedbackText").Length <= 40, "hud_success_feedback_stays_short");
            }
        }

        private static void TestKeyboard(Assembly app)
        {
            var feedbackType = app.GetType("WAHUKidsLearn.TypingSpaceHudFeedbackState", true);
            var keyboardType = app.GetType("WAHUKidsLearn.TypingSpaceKeyboardControl", true);
            using (var keyboard = (Control)Activator.CreateInstance(keyboardType, true))
            {
                keyboard.Size = new Size(620, 210);
                Invoke(keyboard, "SetVisualState", "a", Enum.Parse(feedbackType, "Wrong"), "s", 2);
                LayoutTree(keyboard);
                A(Get<string>(keyboard, "HighlightedKey") == "A", "keyboard_highlights_next_key");
                A(Get<bool>(keyboard, "HintVisible"), "keyboard_hint_visible_after_two_errors");
                A(Get<int>(keyboard, "KeyCount") == 26, "keyboard_renders_26_letter_keys");
                A(Get<int>(keyboard, "MinimumRenderedKeyWidth") >= 48, "keyboard_touch_key_width_at_least_48px");
                A(Get<int>(keyboard, "MinimumRenderedKeyHeight") >= 48, "keyboard_touch_key_height_at_least_48px");
                A(keyboard.MinimumSize.Width >= 560 && keyboard.MinimumSize.Height >= 200, "keyboard_declared_minimum_preserves_48px_touch_targets");
                A(keyboard.TabStop, "keyboard_is_focusable");
                A(keyboard.AccessibleDescription.IndexOf("Phím tiếp theo", StringComparison.OrdinalIgnoreCase) >= 0,
                    "keyboard_accessibility_announces_hint");
                Render(keyboard, 620, 210);

                Invoke(keyboard, "SetVisualState", "p", Enum.Parse(feedbackType, "Correct"), "", 0);
                A(!Get<bool>(keyboard, "HintVisible"), "keyboard_hint_hides_without_repeated_error");
                Render(keyboard, 900, 220);
            }
        }

        private static void TestCompositeResponsive(Assembly app)
        {
            var panelType = app.GetType("WAHUKidsLearn.TypingSpaceHudDemoPanel", true);
            foreach (var size in new[] { new Size(900, 640), new Size(1180, 760), new Size(1125, 800) })
            {
                using (var panel = (Control)Activator.CreateInstance(panelType, true))
                {
                    panel.Size = size;
                    LayoutTree(panel);
                    A(panel.Controls.Count == 1, "typing_demo_has_single_root_" + size.Width);
                    AssertBoundsInside(panel);
                    AssertSiblingNoOverlap(panel);
                    Render(panel, size.Width, size.Height);
                }
            }
        }

        private static void AssertBoundsInside(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (child.Visible)
                    A(child.Left >= 0 && child.Top >= 0 && child.Right <= root.ClientSize.Width + 1 && child.Bottom <= root.ClientSize.Height + 1,
                        "control_inside_parent_" + child.GetType().Name);
                AssertBoundsInside(child);
            }
        }

        private static void AssertSiblingNoOverlap(Control root)
        {
            for (var i = 0; i < root.Controls.Count; i++)
            for (var j = i + 1; j < root.Controls.Count; j++)
            {
                var a = root.Controls[i];
                var b = root.Controls[j];
                if (!a.Visible || !b.Visible) continue;
                var intersection = Rectangle.Intersect(a.Bounds, b.Bounds);
                A(intersection.Width == 0 || intersection.Height == 0,
                    "siblings_no_overlap_" + a.GetType().Name + "_" + b.GetType().Name);
            }
            foreach (Control child in root.Controls) AssertSiblingNoOverlap(child);
        }

        private static void LayoutTree(Control root)
        {
            root.CreateControl();
            root.PerformLayout();
            foreach (Control child in root.Controls) LayoutTree(child);
        }

        private static void Render(Control control, int width, int height)
        {
            control.Size = new Size(width, height);
            LayoutTree(control);
            using (var bitmap = new Bitmap(width, height))
            {
                control.DrawToBitmap(bitmap, new Rectangle(0, 0, width, height));
                A(bitmap.Width == width && bitmap.Height == height, "render_" + control.GetType().Name + "_" + width + "x" + height);
            }
        }

        private static void Invoke(object target, string method, params object[] args)
        {
            MethodInfo info = null;
            foreach (var candidate in target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!string.Equals(candidate.Name, method, StringComparison.Ordinal)) continue;
                var parameters = candidate.GetParameters();
                if (parameters.Length != args.Length) continue;
                var compatible = true;
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (args[i] == null) continue;
                    if (!parameters[i].ParameterType.IsInstanceOfType(args[i])) { compatible = false; break; }
                }
                if (!compatible) continue;
                info = candidate;
                break;
            }
            if (info == null) throw new MissingMethodException(target.GetType().FullName, method);
            info.Invoke(target, args);
        }

        private static void Set(object target, string property, object value)
        {
            var info = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) throw new MissingMemberException(target.GetType().FullName, property);
            info.SetValue(target, value, null);
        }

        private static T Get<T>(object target, string property)
        {
            var info = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) throw new MissingMemberException(target.GetType().FullName, property);
            return (T)info.GetValue(target, null);
        }

        private static void A(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("ASSERT_FAIL: " + name);
            _assertions++;
        }
    }
}