using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using WAHU.TypingSpace.Scene;

namespace WAHU.TypingSpaceSceneRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        private static int Main()
        {
            try
            {
                var state = ReadMock(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mock_scene.json"));
                TestContractShape(state);
                TestResponsiveLayout(state);
                TestAdversarialLayout();
                TestSafeZones(state);
                TestTargetContrast();
                TestRender(state);
                TestLowPerformanceRender(state);
                TestParallaxPolicy(state);
                Console.WriteLine("TYPING_SPACE_SCENE_RUNTIME_SMOKE_PASS assertions=" + _assertions);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("TYPING_SPACE_SCENE_RUNTIME_SMOKE_FAIL " + ex);
                return 1;
            }
        }

        private static TypingSpaceSceneState ReadMock(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("mock_scene.json", path);
            var serializer = new DataContractJsonSerializer(typeof(MockScene));
            MockScene mock;
            using (var stream = File.OpenRead(path)) mock = (MockScene)serializer.ReadObject(stream);
            var state = new TypingSpaceSceneState
            {
                Phase = string.IsNullOrWhiteSpace(mock.Phase) ? "playing" : mock.Phase,
                ShipY = mock.ShipY,
                BossVisible = mock.BossVisible,
                BossHealth01 = mock.BossHealth01,
                Paused = mock.Paused,
                Targets = new List<TypingSceneTarget>()
            };
            if (mock.Targets != null)
            {
                foreach (var target in mock.Targets)
                {
                    state.Targets.Add(new TypingSceneTarget
                    {
                        Id = target.Id,
                        DisplayText = target.DisplayText,
                        Kind = target.Kind,
                        X = target.X,
                        Y = target.Y,
                        Active = target.Active,
                        TypedCount = target.TypedCount,
                        TotalCount = target.TotalCount
                    });
                }
            }
            return state;
        }

        private static void TestContractShape(TypingSpaceSceneState state)
        {
            Assert(state.Phase == "boss", "mock_phase_loaded");
            Assert(state.Targets.Count == 3, "mock_targets_loaded");
            Assert(state.Targets[0].Id == "vi-01" && state.Targets[0].DisplayText == "phi thuyền", "mock_utf8_vi_loaded");
            Assert(state.Targets[2].Kind == "unlock" && state.Targets[2].TotalCount == 5, "mock_target_shape_loaded");
        }

        private static void TestResponsiveLayout(TypingSpaceSceneState state)
        {
            var sizes = new[]
            {
                new Size(480, 320),
                new Size(640, 360),
                new Size(800, 600),
                new Size(1024, 576),
                new Size(1280, 720),
                new Size(1366, 768),
                new Size(1600, 900)
            };
            foreach (var size in sizes)
            {
                var layout = TypingSpaceSceneLayoutEngine.Compute(size, state);
                Assert(layout.Playfield.Width > 0 && layout.Playfield.Height > 0, "playfield_positive_" + size.Width);
                Assert(layout.TargetLanes.Count == 3, "three_lanes_" + size.Width);
                Assert(layout.ShipBounds.Width > 0 && layout.ShipBounds.Height > 0 && layout.Playfield.Contains(layout.ShipBounds), "ship_inside_playfield_" + size.Width);
                Assert(layout.BossBounds.Width > 0 && layout.BossBounds.Height > 0 && layout.Playfield.Contains(layout.BossBounds), "boss_inside_playfield_" + size.Width);
                Assert(layout.TargetBounds.Count == 3, "all_mock_targets_mapped_" + size.Width);
                foreach (var bounds in layout.TargetBounds.Values)
                    Assert(layout.Playfield.Contains(bounds), "target_inside_playfield_" + size.Width + "_" + bounds.X);
            }
        }

        private static void TestAdversarialLayout()
        {
            var state = new TypingSpaceSceneState
            {
                Phase = "boss",
                ShipY = float.NaN,
                BossVisible = true,
                BossHealth01 = float.NaN,
                Targets = new List<TypingSceneTarget>
                {
                    new TypingSceneTarget { Id = "dup", DisplayText = "một cụm từ tiếng Việt rất dài để kiểm tra", Kind = "shoot", X = -9f, Y = 0.5f, TotalCount = 40 },
                    new TypingSceneTarget { Id = "dup", DisplayText = "SPACE", Kind = "unlock", X = 9f, Y = 0.5f, TotalCount = 5 },
                    new TypingSceneTarget { Id = "nan", DisplayText = "cứu hộ", Kind = "rescue", X = float.NaN, Y = float.NaN, TotalCount = 6 },
                    new TypingSceneTarget { Id = "edge-high", DisplayText = "BOSS", Kind = "boss", X = float.PositiveInfinity, Y = float.PositiveInfinity, TotalCount = 4 }
                }
            };

            for (var i = 0; i < 48; i++)
            {
                var width = 320 + (i * 137) % 1601;
                var height = 220 + (i * 83) % 861;
                var layout = TypingSpaceSceneLayoutEngine.Compute(new Size(width, height), state);
                Assert(layout.TargetBounds.Count == 4, "adversarial_unique_target_keys_" + i);
                Assert(layout.Playfield.Contains(layout.ShipBounds), "adversarial_nan_ship_clamped_" + i);
                Assert(layout.Playfield.Contains(layout.BossBounds), "adversarial_boss_inside_" + i);
                foreach (var bounds in layout.TargetBounds.Values)
                {
                    Assert(layout.Playfield.Contains(bounds), "adversarial_target_clamped_" + i + "_" + bounds.X);
                    Assert(!layout.HudSafeZone.IntersectsWith(bounds), "adversarial_target_outside_hud_" + i + "_" + bounds.X);
                    Assert(!layout.ShipZone.IntersectsWith(bounds), "adversarial_target_outside_ship_" + i + "_" + bounds.X);
                    Assert(!layout.BossZone.IntersectsWith(bounds), "adversarial_target_outside_boss_" + i + "_" + bounds.X);
                    Assert(bounds.Width <= 238 && bounds.Height <= 72, "adversarial_target_size_capped_" + i + "_" + bounds.X);
                }
                if (width >= 800)
                {
                    var cards = new List<Rectangle>(layout.TargetBounds.Values);
                    for (var a = 0; a < cards.Count; a++)
                        for (var b = a + 1; b < cards.Count; b++)
                            Assert(!cards[a].IntersectsWith(cards[b]), "adversarial_cards_do_not_overlap_" + i + "_" + a + "_" + b);
                }
            }

            foreach (var size in new[] { new Size(480, 320), new Size(1920, 1080) })
            {
                using (var bitmap = new Bitmap(size.Width, size.Height))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    TypingSpaceSceneRenderer.Draw(graphics, size, state, 99999f, false);
                    Assert(bitmap.GetPixel(size.Width / 2, size.Height / 2).A == 255, "adversarial_render_opaque_" + size.Width);
                }
            }
        }

        private static void TestSafeZones(TypingSpaceSceneState state)
        {
            var layout = TypingSpaceSceneLayoutEngine.Compute(new Size(1024, 576), state);
            Assert(layout.HudSafeZone.Bottom < layout.Playfield.Top, "hud_safe_zone_separate_from_playfield");
            Assert(!layout.HudSafeZone.IntersectsWith(layout.ShipBounds), "hud_does_not_cover_ship");
            Assert(!layout.HudSafeZone.IntersectsWith(layout.BossBounds), "hud_does_not_cover_boss");
            foreach (var target in layout.TargetBounds.Values)
            {
                Assert(!layout.HudSafeZone.IntersectsWith(target), "target_never_enters_hud_safe_zone");
                Assert(!layout.ShipZone.IntersectsWith(target), "target_text_not_on_ship_zone");
                Assert(!layout.BossZone.IntersectsWith(target), "target_text_not_on_boss_zone");
            }
        }

        private static void TestTargetContrast()
        {
            Assert(TypingSpaceSceneRenderer.TargetTextContrastRatio >= 7.0, "target_text_contrast_aaa_like");
            Assert(TypingSpaceSceneRenderer.ContrastRatio(Color.FromArgb(25, 45, 80), Color.White) >= 7.0, "explicit_target_contrast_stable");
        }

        private static void TestRender(TypingSpaceSceneState state)
        {
            foreach (var size in new[] { new Size(480, 320), new Size(640, 360), new Size(1024, 576), new Size(1366, 768) })
            {
                using (var bitmap = new Bitmap(size.Width, size.Height))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    TypingSpaceSceneRenderer.Draw(graphics, size, state, 12.5f, false);
                    var sample = bitmap.GetPixel(size.Width / 2, size.Height / 2);
                    Assert(sample.A == 255, "normal_render_opaque_" + size.Width);
                    Assert(!(sample.R == 0 && sample.G == 0 && sample.B == 0), "normal_render_nonblank_" + size.Width);
                }
            }
        }

        private static void TestLowPerformanceRender(TypingSpaceSceneState state)
        {
            using (var bitmap = new Bitmap(640, 360))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                TypingSpaceSceneRenderer.Draw(graphics, bitmap.Size, state, 500f, true);
                Assert(bitmap.GetPixel(10, 10).A == 255, "low_perf_render_no_crash");
            }
        }

        private static void TestParallaxPolicy(TypingSpaceSceneState state)
        {
            using (var control = new TypingSpaceSceneControl())
            {
                control.Size = new Size(800, 450);
                control.State = state;
                control.Visible = true;
                var before = control.ParallaxOffset;
                control.AdvanceParallax(33);
                Assert(control.ParallaxOffset > before, "parallax_advances_when_enabled");
                var after = control.ParallaxOffset;
                control.LowPerformanceMode = true;
                control.AdvanceParallax(33);
                Assert(Math.Abs(control.ParallaxOffset - after) < 0.0001f, "parallax_stops_in_low_perf_mode");
                control.LowPerformanceMode = false;
                state.Paused = true;
                control.AdvanceParallax(33);
                Assert(Math.Abs(control.ParallaxOffset - after) < 0.0001f, "parallax_stops_when_paused");
                state.Paused = false;
                control.ParallaxEnabled = false;
                control.AdvanceParallax(33);
                Assert(Math.Abs(control.ParallaxOffset - after) < 0.0001f, "parallax_can_be_disabled");
            }
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }

        [DataContract]
        private sealed class MockScene
        {
            [DataMember(Name = "phase")] public string Phase { get; set; }
            [DataMember(Name = "shipY")] public float ShipY { get; set; }
            [DataMember(Name = "bossVisible")] public bool BossVisible { get; set; }
            [DataMember(Name = "bossHealth01")] public float BossHealth01 { get; set; }
            [DataMember(Name = "paused")] public bool Paused { get; set; }
            [DataMember(Name = "targets")] public List<MockTarget> Targets { get; set; }
        }

        [DataContract]
        private sealed class MockTarget
        {
            [DataMember(Name = "id")] public string Id { get; set; }
            [DataMember(Name = "displayText")] public string DisplayText { get; set; }
            [DataMember(Name = "kind")] public string Kind { get; set; }
            [DataMember(Name = "x")] public float X { get; set; }
            [DataMember(Name = "y")] public float Y { get; set; }
            [DataMember(Name = "active")] public bool Active { get; set; }
            [DataMember(Name = "typedCount")] public int TypedCount { get; set; }
            [DataMember(Name = "totalCount")] public int TotalCount { get; set; }
        }
    }
}
