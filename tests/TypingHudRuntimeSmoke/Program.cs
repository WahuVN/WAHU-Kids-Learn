using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows.Forms;

namespace WAHU.TypingHudRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;
        private static string _capturedRawKey;
        private static string _capturedSource;
        private static long _capturedTimestamp;
        private static string _captureDirectory;

        [STAThread]
        private static void Main(string[] args)
        {
            _captureDirectory = ResolveCaptureDirectory(args);
            if (!string.IsNullOrWhiteSpace(_captureDirectory)) System.IO.Directory.CreateDirectory(_captureDirectory);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var repo = Environment.CurrentDirectory;
            var appPath = System.IO.Path.Combine(repo, "src", "App", "bin", "Release", "WAHUKidsLearn.exe");
            A(System.IO.File.Exists(appPath), "app_release_binary_available");
            var app = Assembly.LoadFrom(appPath);

            TestDefaultHud(app);
            TestWrongAndHint(app);
            TestCorrectAndCompleted(app);
            TestPauseProjection(app);
            TestOnScreenInputEvent(app);
            TestResponsiveLayouts(app);
            TestContractBoundary(app, repo);

            Console.WriteLine("TYPING_HUD_RUNTIME_SMOKE_PASS assertions=" + _assertions);
        }

        private static void TestDefaultHud(Assembly app)
        {
            using (var hud = CreateControl(app, "WAHUKidsLearn.TypingHudPanel"))
            {
                ApplyDemo(app, hud, "Default");
                A(Get<string>(hud, "CurrentTargetText") == "hành tinh", "hud_default_target_text");
                A(Get<string>(hud, "CurrentLanguageText").StartsWith("VI", StringComparison.Ordinal), "hud_default_language_vi");
                A(Get<string>(hud, "CurrentProgressText").Contains("0 / 8"), "hud_default_progress_copy");
                A(Get<string>(hud, "CurrentFeedbackText").IndexOf("ánh sáng", StringComparison.OrdinalIgnoreCase) >= 0,
                    "hud_default_feedback_short_instruction");
                var keyboard = Get<object>(hud, "Keyboard");
                A(Get<int>(keyboard, "KeyCount") == 37, "keyboard_has_numbers_qwerty_and_space");
                A(Get<string>(keyboard, "HighlightedKey") == "A", "keyboard_highlights_mock_next_key_a");
                var aKey = InvokeResult(keyboard, "FindKey", "a") as Control;
                A(aKey != null && aKey.TabStop && aKey.AccessibleDescription.IndexOf("cần bấm tiếp theo", StringComparison.OrdinalIgnoreCase) >= 0,
                    "keyboard_next_key_is_focusable_and_accessible");
                RenderAndValidate(hud, 640, 540, "hud_default_min");
                A(Get<int>(keyboard, "MinimumRenderedKeyHeight") >= 44, "keyboard_min_touch_height_at_min_layout");
                A(Get<int>(keyboard, "MinimumRenderedKeyWidth") >= 44, "keyboard_min_touch_width_at_min_layout");
            }
        }

        private static void TestWrongAndHint(Assembly app)
        {
            using (var hud = CreateControl(app, "WAHUKidsLearn.TypingHudPanel"))
            {
                ApplyDemo(app, hud, "Wrong");
                var feedback = Get<string>(hud, "CurrentFeedbackText");
                A(feedback.IndexOf("Thử lại", StringComparison.OrdinalIgnoreCase) >= 0, "wrong_feedback_invites_retry");
                A(feedback.IndexOf("sai", StringComparison.OrdinalIgnoreCase) < 0 &&
                  feedback.IndexOf("lỗi", StringComparison.OrdinalIgnoreCase) < 0 &&
                  feedback.IndexOf("thua", StringComparison.OrdinalIgnoreCase) < 0,
                    "wrong_feedback_avoids_shaming_language");
                A(Get<bool>(hud, "HintVisible") && Get<string>(hud, "CurrentHintText").IndexOf("N", StringComparison.OrdinalIgnoreCase) >= 0,
                    "hint_appears_after_two_errors_with_next_key");
                var keyboard = Get<object>(hud, "Keyboard");
                A(Get<string>(keyboard, "HighlightedKey") == "N", "wrong_state_keeps_correct_next_key_highlighted");
                A(Get<string>(keyboard, "TransientFeedbackKey") == "M" && Get<bool>(keyboard, "TransientFeedbackIsWrong"),
                    "wrong_raw_key_gets_short_soft_feedback");
                var mKey = InvokeResult(keyboard, "FindKey", "m");
                A(Get<bool>(mKey, "IsWrongPulse"), "wrong_key_visual_pulse_active");
                RenderAndValidate(hud, 700, 560, "hud_wrong_hint");
            }
        }

        private static void TestCorrectAndCompleted(Assembly app)
        {
            using (var hud = CreateControl(app, "WAHUKidsLearn.TypingHudPanel"))
            {
                ApplyDemo(app, hud, "Correct");
                A(Get<string>(hud, "CurrentFeedbackText").IndexOf("Đúng rồi", StringComparison.OrdinalIgnoreCase) >= 0,
                    "correct_feedback_is_short_positive");
                var keyboard = Get<object>(hud, "Keyboard");
                A(Get<string>(keyboard, "HighlightedKey") == "N", "correct_state_advances_highlight");
                var aKey = InvokeResult(keyboard, "FindKey", "a");
                A(Get<bool>(aKey, "IsCorrectPulse"), "correct_raw_key_gets_short_success_pulse");

                ApplyDemo(app, hud, "Completed");
                A(Get<string>(hud, "CurrentLanguageText").StartsWith("EN", StringComparison.Ordinal), "completed_demo_switches_en_mode");
                A(Get<string>(hud, "CurrentFeedbackText").IndexOf("Tuyệt", StringComparison.OrdinalIgnoreCase) >= 0,
                    "completed_feedback_is_positive_and_short");
                A(!Get<bool>(hud, "PauseEnabled"), "completed_state_disables_pause_action");
                A(!((Control)Get<object>(hud, "Keyboard")).Enabled, "completed_state_disables_keyboard_input");
                A(string.IsNullOrEmpty(Get<string>(Get<object>(hud, "Keyboard"), "HighlightedKey")),
                    "completed_state_clears_next_key_highlight");
                A(string.IsNullOrEmpty(Get<string>(Get<object>(hud, "Keyboard"), "TransientFeedbackKey")),
                    "completed_state_clears_transient_key_feedback");
                RenderAndValidate(hud, 900, 620, "hud_completed_en");
            }
        }

        private static void TestPauseProjection(Assembly app)
        {
            using (var hud = CreateControl(app, "WAHUKidsLearn.TypingHudPanel"))
            {
                var snapshot = Demo(app, "Default");
                Set(snapshot, "Phase", "paused");
                Invoke(hud, "ApplySnapshot", snapshot);
                A(Get<string>(hud, "CurrentPhaseText") == "Đang nghỉ", "paused_phase_copy");
                A(Get<string>(hud, "PauseButtonText") == "Tiếp tục" && Get<bool>(hud, "PauseEnabled"),
                    "paused_state_offers_resume_without_losing_progress");
                A(!((Control)Get<object>(hud, "Keyboard")).Enabled, "paused_state_blocks_typing_without_destroying_ui");
                A(string.IsNullOrEmpty(Get<string>(Get<object>(hud, "Keyboard"), "HighlightedKey")),
                    "paused_state_temporarily_clears_highlight");
                A(string.IsNullOrEmpty(Get<string>(Get<object>(hud, "Keyboard"), "TransientFeedbackKey")),
                    "paused_state_clears_transient_feedback");

                var pauseCount = 0;
                var pauseEvent = hud.GetType().GetEvent("PauseToggleRequested", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                A(pauseEvent != null, "pause_toggle_event_available");
                EventHandler handler = delegate { pauseCount++; };
                pauseEvent.AddEventHandler(hud, handler);
                var pauseButton = GetField<Button>(hud, "_pauseButton");
                pauseButton.PerformClick();
                A(pauseCount == 1, "pause_resume_button_emits_single_toggle_request");
                pauseEvent.RemoveEventHandler(hud, handler);
            }
        }

        private static void TestOnScreenInputEvent(Assembly app)
        {
            using (var hud = CreateControl(app, "WAHUKidsLearn.TypingHudPanel"))
            {
                ApplyDemo(app, hud, "Default");
                var keyboard = Get<object>(hud, "Keyboard");
                var keyEvent = keyboard.GetType().GetEvent("KeyInvoked", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                A(keyEvent != null, "keyboard_key_invoked_event_available");
                var invoke = keyEvent.EventHandlerType.GetMethod("Invoke");
                var parameters = invoke.GetParameters();
                var sender = Expression.Parameter(parameters[0].ParameterType, "sender");
                var args = Expression.Parameter(parameters[1].ParameterType, "args");
                var capture = typeof(Program).GetMethod("CaptureKeyEvent", BindingFlags.Static | BindingFlags.NonPublic);
                var body = Expression.Call(capture, Expression.Convert(args, typeof(object)));
                var lambda = Expression.Lambda(keyEvent.EventHandlerType, body, sender, args).Compile();
                keyEvent.AddEventHandler(keyboard, lambda);

                _capturedRawKey = null;
                _capturedSource = null;
                _capturedTimestamp = 0;
                var aButton = InvokeResult(keyboard, "FindKey", "A") as Button;
                A(aButton != null && aButton.Enabled, "onscreen_a_key_clickable");
                aButton.PerformClick();
                A(_capturedRawKey == "a", "onscreen_event_emits_raw_key_only");
                A(_capturedSource == "onscreen", "onscreen_event_source_matches_frozen_contract");
                A(_capturedTimestamp > 0, "onscreen_event_has_timestamp");
                A(parameters[1].ParameterType.GetProperty("NormalizedKey") == null,
                    "ai05_does_not_claim_normalized_key_field_from_ai07");
                keyEvent.RemoveEventHandler(keyboard, lambda);
            }
        }

        private static void TestResponsiveLayouts(Assembly app)
        {
            foreach (var size in new[] { new Size(640, 540), new Size(900, 640), new Size(1125, 800) })
            {
                using (var hud = CreateControl(app, "WAHUKidsLearn.TypingHudPanel"))
                {
                    ApplyDemo(app, hud, "Wrong");
                    RenderAndValidate(hud, size.Width, size.Height, "hud_responsive_" + size.Width + "x" + size.Height);
                    var keyboard = Get<object>(hud, "Keyboard");
                    A(Get<int>(keyboard, "MinimumRenderedKeyHeight") >= 44,
                        "keyboard_touch_height_44px_" + size.Width + "x" + size.Height);
                    A(Get<int>(keyboard, "MinimumRenderedKeyWidth") >= 44,
                        "keyboard_touch_width_44px_" + size.Width + "x" + size.Height);
                    var buttonCount = CountButtons((Control)keyboard); A(buttonCount == 37, "keyboard_all_keys_present_" + size.Width + "x" + size.Height);
                }
            }

            using (var hud = CreateControl(app, "WAHUKidsLearn.TypingHudPanel"))
            {
                var snapshot = Demo(app, "Default");
                Set(snapshot, "TargetDisplayText", "interstellar navigation station");
                Set(snapshot, "Language", "en");
                Set(snapshot, "TotalCount", 31);
                Set(snapshot, "TypedCount", 17);
                Set(snapshot, "NextKey", "space");
                Invoke(hud, "ApplySnapshot", snapshot);
                RenderAndValidate(hud, 700, 560, "hud_long_target_space");
                A(Get<string>(Get<object>(hud, "Keyboard"), "HighlightedKey") == "SPACE", "space_alias_highlights_space_key");
                A(Get<string>(hud, "CurrentProgressText").Contains("17 / 31"), "long_target_progress_not_truncated_semantically");
            }
        }

        private static void TestContractBoundary(Assembly app, string repo)
        {
            var snapshotType = app.GetType("WAHUKidsLearn.TypingHudSnapshot", true);
            foreach (var name in new[] { "TargetId", "TargetDisplayText", "Language", "TypedCount", "TotalCount", "Completed", "ErrorCount", "NextKey", "Phase" })
                A(snapshotType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null,
                    "snapshot_projection_has_" + name);
            var source = System.IO.File.ReadAllText(System.IO.Path.Combine(repo, "src", "App", "TypingHudKeyboardControls.cs"));
            A(source.IndexOf("WAHU.Session", StringComparison.Ordinal) < 0 && source.IndexOf("RescueGameReward", StringComparison.Ordinal) < 0,
                "ai05_module_does_not_take_core_or_reward_dependency");
            A(source.IndexOf("Telex", StringComparison.OrdinalIgnoreCase) < 0 && source.IndexOf("VNI", StringComparison.OrdinalIgnoreCase) < 0,
                "ai05_module_does_not_implement_telex_or_vni_logic");
        }

        private static void CaptureKeyEvent(object args)
        {
            _capturedRawKey = Get<string>(args, "RawKey");
            _capturedSource = Get<string>(args, "Source");
            _capturedTimestamp = Get<long>(args, "Timestamp");
        }

        private static object Demo(Assembly app, string method)
        {
            var type = app.GetType("WAHUKidsLearn.TypingHudDemoSnapshots", true);
            var info = type.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) throw new MissingMethodException(type.FullName, method);
            return info.Invoke(null, null);
        }

        private static void ApplyDemo(Assembly app, Control hud, string method)
        {
            Invoke(hud, "ApplySnapshot", Demo(app, method));
        }

        private static Control CreateControl(Assembly app, string typeName)
        {
            var value = Activator.CreateInstance(app.GetType(typeName, true), true) as Control;
            if (value == null) throw new InvalidOperationException("Could not create " + typeName);
            return value;
        }

        private static void RenderAndValidate(Control child, int width, int height, string name)
        {
            A(width >= child.MinimumSize.Width && height >= child.MinimumSize.Height, name + "_meets_component_minimum");
            using (var host = new Panel { BackColor = Color.White, Size = new Size(width, height) })
            {
                child.Dock = DockStyle.Fill;
                host.Controls.Add(child);
                CreateTree(host);
                host.PerformLayout();
                child.PerformLayout();
                AssertTreeBounds(host, name);
                AssertTableSiblingsDoNotOverlap(host, name);
                using (var bitmap = new Bitmap(width, height))
                {
                    host.DrawToBitmap(bitmap, new Rectangle(0, 0, width, height));
                    var colors = new HashSet<int>();
                    var stepX = Math.Max(1, width / 28);
                    var stepY = Math.Max(1, height / 20);
                    for (var y = 0; y < height; y += stepY)
                        for (var x = 0; x < width; x += stepX)
                            colors.Add(bitmap.GetPixel(x, y).ToArgb());
                    A(colors.Count >= 12, name + "_renders_nontrivial_visual");
                    if (!string.IsNullOrWhiteSpace(_captureDirectory))
                    {
                        var capturePath = System.IO.Path.Combine(_captureDirectory, name + ".png");
                        bitmap.Save(capturePath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                host.Controls.Remove(child);
            }
        }

        private static void CreateTree(Control root)
        {
            root.CreateControl();
            root.PerformLayout();
            foreach (Control child in root.Controls) CreateTree(child);
        }

        private static void AssertTreeBounds(Control parent, string name)
        {
            var client = parent.ClientRectangle;
            foreach (Control child in parent.Controls)
            {

                A(child.Width > 0 && child.Height > 0, name + "_positive_" + child.GetType().Name);
                var bounds = child.Bounds;
                A(bounds.Right >= client.Left - 2 && bounds.Bottom >= client.Top - 2 &&
                  bounds.Left <= client.Right + 2 && bounds.Top <= client.Bottom + 2,
                    name + "_intersects_parent_" + child.GetType().Name);
                AssertTreeBounds(child, name);
            }
        }

        private static void AssertTableSiblingsDoNotOverlap(Control parent, string name)
        {
            if (parent is TableLayoutPanel || parent is FlowLayoutPanel)
            {
                var visible = parent.Controls.Cast<Control>().Where(x => (x.Visible || x is Button) && x.Width > 0 && x.Height > 0).ToList();
                for (var i = 0; i < visible.Count; i++)
                {
                    for (var j = i + 1; j < visible.Count; j++)
                    {
                        var overlap = Rectangle.Intersect(visible[i].Bounds, visible[j].Bounds);
                        A(overlap.Width <= 2 || overlap.Height <= 2,
                            name + "_no_overlap_" + visible[i].GetType().Name + "_" + visible[j].GetType().Name);
                    }
                }
            }
            foreach (Control child in parent.Controls) AssertTableSiblingsDoNotOverlap(child, name);
        }

        private static int CountButtons(Control root)
        {
            var count = root is Button ? 1 : 0;
            foreach (Control child in root.Controls) count += CountButtons(child);
            return count;
        }

        private static string ResolveCaptureDirectory(string[] args)
        {
            if (args == null) return null;
            for (var i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "--capture-dir", StringComparison.OrdinalIgnoreCase)) continue;
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                    throw new ArgumentException("--capture-dir requires a path.");
                return System.IO.Path.GetFullPath(args[i + 1]);
            }
            return null;
        }

        private static object InvokeResult(object target, string method, params object[] args)
        {
            var info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) throw new MissingMethodException(target.GetType().FullName, method);
            return info.Invoke(target, args);
        }

        private static void Invoke(object target, string method, params object[] args)
        {
            InvokeResult(target, method, args);
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

        private static T GetField<T>(object target, string field)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) throw new MissingFieldException(target.GetType().FullName, field);
            return (T)info.GetValue(target);
        }

        private static void A(bool condition, string name)
        {
            _assertions++;
            if (!condition) throw new Exception("ASSERT_FAIL: " + name);
        }
    }
}
