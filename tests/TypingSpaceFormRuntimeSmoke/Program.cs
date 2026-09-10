using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WAHU.Data;
using WAHU.TypingSpace;

namespace WAHU.TypingSpaceFormRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var repo = FindRepoRoot(Environment.CurrentDirectory);
            var schema = Path.Combine(repo, "data", "schema", "001_initial.sql");
            A(File.Exists(schema), "schema_found");
            var temp = Path.Combine(Path.GetTempPath(), "wahu-typing-form-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                var database = new LearningDatabase(Path.Combine(temp, "learning.db"), schema);
                var init = database.Initialize("DELETE");
                A(init.Health != null && init.Health.IsHealthy, "database_ready");
                new LearnerSessionService(database).EnsurePrimaryChild("Bé thử Typing");

                var appAssembly = typeof(WAHUKidsLearn.MainForm).Assembly;
                var formType = appAssembly.GetType("WAHUKidsLearn.TypingSpaceForm", true);
                var ctor = formType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(x => x.GetParameters().Length == 2);
                A(ctor != null, "typing_form_ctor_available");

                using (var form = (Form)ctor.Invoke(new object[] { database, null }))
                {
                    form.Size = new Size(900, 640);
                    form.Show();
                    Application.DoEvents();
                    A(form.Width >= 900 && form.Height >= 640, "typing_form_minimum_size_honored");
                    A(form.ClientSize.Width > 820 && form.ClientSize.Height > 560, "typing_form_minimum_client_usable");

                    var keyboard = GetField<FlowLayoutPanel>(form, "_keyboard");
                    var buttons = keyboard.Controls.OfType<Button>().ToList();
                    A(buttons.Count == 27, "onscreen_keyboard_has_26_letters_plus_space");
                    var space = buttons.SingleOrDefault(x => x.Text == "SPACE");
                    A(space != null && space.Width >= 150 && space.Height >= 36, "space_key_is_large_and_present");
                    A(space != null && string.Equals(space.AccessibleName, "Phím cách", StringComparison.Ordinal), "space_key_accessible_name");
                    A(!keyboard.VerticalScroll.Visible, "keyboard_fits_without_vertical_scroll_at_900x640");

                    var pause = GetField<Button>(form, "_pauseButton");
                    var replay = GetField<Button>(form, "_replayButton");
                    var close = Descendants<Button>(form).FirstOrDefault(x => x.Text == "Đóng");
                    A(pause != null && replay != null && close != null, "critical_buttons_present");
                    A(!BoundsInForm(pause, form).IntersectsWith(BoundsInForm(close, form)), "pause_and_close_do_not_overlap");
                    A(!BoundsInForm(replay, form).IntersectsWith(BoundsInForm(space, form)), "replay_and_space_do_not_overlap");
                    foreach (var button in Descendants<Button>(form).Where(x => x.Visible))
                    {
                        var r = BoundsInForm(button, form);
                        A(r.Width >= 44 && r.Height >= 30, "button_size_" + Safe(button.Text));
                        A(r.Left >= 0 && r.Top >= 0 && r.Right <= form.ClientSize.Width + 1 && r.Bottom <= form.ClientSize.Height + 1,
                            "button_inside_client_" + Safe(button.Text));
                    }

                    var qaDir = Path.Combine(repo, "build", "qa");
                    Directory.CreateDirectory(qaDir);
                    Render(form, Path.Combine(qaDir, "typing-form-fixed-900x640.png"));

                    var game = GetField<TypingSpaceIntegrationShell>(form, "_game");
                    var feedback = GetField<Label>(form, "_feedback");
                    A(game != null && game.CurrentTarget != null && game.Phase != GamePhase.Intro, "game_started_on_show");

                    var expected = game.CurrentTarget.AcceptedInputs.OrderBy(x => x.Length).First();
                    var first = expected.Length == 0 ? 'a' : char.ToLowerInvariant(expected[0]);
                    var wrong = first == 'z' ? "q" : "z";
                    Invoke(form, "Submit", wrong);
                    A(game.Progress.ErrorCount == 1, "wrong_key_counts_one_retry");
                    A(feedback.Text.IndexOf("Thử lại", StringComparison.OrdinalIgnoreCase) >= 0, "wrong_key_feedback_is_gentle");

                    Invoke(form, "StartGame");
                    Invoke(form, "TogglePause");
                    A(game.Phase == GamePhase.Paused, "pause_enters_paused_phase");
                    Invoke(form, "TogglePause");
                    A(game.Phase != GamePhase.Paused, "resume_restores_playing_phase");

                    var phraseSeen = CompleteCurrentRun(form, game);
                    A(phraseSeen, "production_flow_includes_phrase_with_space");
                    A(game.Phase == GamePhase.Complete, "production_flow_reaches_complete");
                    A(feedback.Text.IndexOf("Khu vườn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      feedback.Text.IndexOf("Tuyệt", StringComparison.OrdinalIgnoreCase) >= 0,
                        "completion_feedback_mentions_garden_reward");

                    var world = new GameWorldRewardService(database).ReadProgress(LearnerSessionService.PrimaryChildId);
                    A(world.GrowthSteps == 1, "typing_completion_grants_one_garden_growth");
                    A(world.UnlockedItems.Contains("garden_seedling"), "typing_completion_unlocks_first_garden_milestone");
                    A(!new GameWorldRewardService(database).GrantTypingSpaceCompletion(
                        LearnerSessionService.PrimaryChildId, DateTime.Now.ToString("yyyy-MM-dd")),
                        "typing_reward_source_key_is_idempotent");
                    Render(form, Path.Combine(qaDir, "typing-form-fixed-complete-900x640.png"));

                    Invoke(form, "Replay");
                    A(game.Phase != GamePhase.Complete, "replay_starts_new_run");
                    CompleteCurrentRun(form, game);
                    var afterReplay = new GameWorldRewardService(database).ReadProgress(LearnerSessionService.PrimaryChildId);
                    A(afterReplay.GrowthSteps == 1, "replay_same_day_cannot_duplicate_garden_reward");

                    form.Hide();
                }

                Console.WriteLine("TYPING_SPACE_FORM_RUNTIME_SMOKE_PASS assertions=" + _assertions);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        private static bool CompleteCurrentRun(Form form, TypingSpaceIntegrationShell game)
        {
            var phraseSeen = false;
            var guard = 0;
            while (game.Phase != GamePhase.Complete && guard++ < 32)
            {
                var target = game.CurrentTarget;
                if (target == null) break;
                var accepted = target.AcceptedInputs.OrderBy(x => x.Length).First();
                if (accepted.IndexOf(' ') >= 0) phraseSeen = true;
                foreach (var c in accepted)
                {
                    Invoke(form, "Submit", c.ToString());
                    Application.DoEvents();
                }
            }
            A(guard < 32, "flow_guard_not_exhausted");
            return phraseSeen;
        }

        private static void Render(Form form, string path)
        {
            form.PerformLayout();
            Application.DoEvents();
            using (var bitmap = new Bitmap(Math.Max(1, form.ClientSize.Width), Math.Max(1, form.ClientSize.Height)))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                var samples = new HashSet<int>();
                var stepX = Math.Max(1, bitmap.Width / 30);
                var stepY = Math.Max(1, bitmap.Height / 20);
                for (var y = 0; y < bitmap.Height; y += stepY)
                    for (var x = 0; x < bitmap.Width; x += stepX)
                        samples.Add(bitmap.GetPixel(x, y).ToArgb());
                A(samples.Count >= 20, "render_has_visual_variety_" + Path.GetFileName(path));
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            A(new FileInfo(path).Length > 25000, "render_png_nontrivial_" + Path.GetFileName(path));
        }

        private static Rectangle BoundsInForm(Control control, Form form)
        {
            var x = control.Left;
            var y = control.Top;
            for (var p = control.Parent; p != null && p != form; p = p.Parent)
            {
                x += p.Left;
                y += p.Top;
            }
            return new Rectangle(x, y, control.Width, control.Height);
        }

        private static IEnumerable<T> Descendants<T>(Control root) where T : Control
        {
            foreach (Control child in root.Controls)
            {
                var typed = child as T;
                if (typed != null) yield return typed;
                foreach (var nested in Descendants<T>(child)) yield return nested;
            }
        }

        private static T GetField<T>(object instance, string name) where T : class
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new Exception("FIELD_NOT_FOUND: " + name);
            return field.GetValue(instance) as T;
        }

        private static object Invoke(object instance, string name, params object[] args)
        {
            var methods = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(x => x.Name == name && x.GetParameters().Length == args.Length).ToList();
            if (methods.Count != 1) throw new Exception("METHOD_NOT_UNIQUE: " + name + " count=" + methods.Count);
            return methods[0].Invoke(instance, args);
        }

        private static string FindRepoRoot(string start)
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "WAHUKidsLearn.sln"))) return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("WAHUKidsLearn.sln not found from " + start);
        }

        private static string Safe(string text)
        {
            var value = string.IsNullOrWhiteSpace(text) ? "unnamed" : text.Trim();
            foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Replace(' ', '_');
        }

        private static void A(bool ok, string name)
        {
            if (!ok) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }
    }
}
