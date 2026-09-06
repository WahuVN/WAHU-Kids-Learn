using System;
using System.Collections.Generic;
using WAHU.Performance;
using WAHU.Platform;

namespace WAHU.PerformanceRuntimeSmoke
{
    internal static class Program
    {
        private static int _a;
        private static void Main()
        {
            var tuner = new PerformanceAutotuner();
            var normalHw = H(8192, 8);
            var normal = tuner.Select(normalHw);
            A(normal.Profile == PerformanceProfileKind.NORMAL, "normal_hardware_normal");
            var lowRam = tuner.Select(H(2048, 4));
            A(lowRam.Profile == PerformanceProfileKind.LOW && lowRam.Evidence.Contains("ram_at_or_below_3gb"), "low_ram_low");
            var lowCpu = tuner.Select(H(4096, 2));
            A(lowCpu.Profile == PerformanceProfileKind.LOW, "low_cpu_low");

            var spikeFrames = Many(10, 20); spikeFrames[19] = 60;
            var spikeOnly = new PerformanceMeasurements { RenderFrameMs = spikeFrames };
            var spike = tuner.Select(normalHw, spikeOnly);
            A(spike.Profile == PerformanceProfileKind.NORMAL, "single_frame_spike_does_not_downgrade");

            var sustainedFrames = Many(10, 20); sustainedFrames[17] = 55; sustainedFrames[18] = 55; sustainedFrames[19] = 55;
            var sustained = tuner.Select(normalHw, new PerformanceMeasurements { RenderFrameMs = sustainedFrames });
            A(sustained.Profile == PerformanceProfileKind.LOW, "sustained_render_tail_downgrades");

            var clean = new PerformanceMeasurements { RenderFrameMs = Many(25, 20), InputDelayMs = Many(20, 20), MaxWorkingSetMb = 120 };
            var cleanDecision = tuner.Select(normalHw, clean);
            A(cleanDecision.Profile == PerformanceProfileKind.NORMAL && cleanDecision.HasSufficientRuntimeEvidence, "clean_runtime_normal");

            var inputSlow = new PerformanceMeasurements { RenderFrameMs = Many(25, 20), InputDelayMs = Many(95, 20) };
            A(tuner.Select(normalHw, inputSlow).Profile == PerformanceProfileKind.LOW, "input_delay_can_downgrade");
            var memoryHigh = new PerformanceMeasurements { RenderFrameMs = Many(25, 20), MaxWorkingSetMb = 230 };
            A(tuner.Select(normalHw, memoryHigh).Profile == PerformanceProfileKind.LOW, "working_set_can_downgrade");
            var gdi = new PerformanceMeasurements { RenderFrameMs = Many(25, 20), GdiPressureEvents = 3 };
            A(tuner.Select(normalHw, gdi).Profile == PerformanceProfileKind.LOW, "gdi_pressure_can_downgrade");

            var d = new RuntimeDegradationController(6, 4);
            for (var i = 0; i < 5; i++) d.ObserveWindow(55, 20, 100);
            A(d.Stage == DegradationStage.None, "no_degrade_before_full_window");
            d.ObserveWindow(55, 20, 100);
            A(d.Stage == DegradationStage.DecorativeOff, "first_degrade_decorative");
            for (var cycle = 0; cycle < 5; cycle++)
            {
                for (var i = 0; i < 6; i++) d.ObserveWindow(55, 20, 100);
            }
            A(d.Stage == DegradationStage.TemporaryLowMotion, "degradation_order_reaches_low_motion");
            var held = d.Stage;
            for (var i = 0; i < 12; i++) d.ObserveWindow(10, 10, 80);
            A(d.Stage == held, "good_windows_do_not_randomly_toggle_profile");
            Console.WriteLine("PERFORMANCE_RUNTIME_SMOKE_PASS assertions=" + _a);
        }

        private static PreflightReport H(long ram, int cores) { return new PreflightReport { RamTotalMb = ram, LogicalCores = cores }; }
        private static IList<double> Many(double x, int count) { var a = new List<double>(); for (var i=0;i<count;i++) a.Add(x); return a; }
        private static void A(bool ok, string n) { if (!ok) throw new Exception("ASSERT_FAIL: " + n); _a++; }
    }
}
