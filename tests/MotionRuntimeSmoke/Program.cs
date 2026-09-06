using System;
using System.Collections.Generic;
using System.Threading;
using WAHU.Motion;

namespace WAHU.MotionRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        private static void Main()
        {
            TestTween();
            TestSpritePlayer();
            TestPolicy();
            TestBudget();
            TestSchedulerCoalescingAndFinalState();
            Console.WriteLine("MOTION_RUNTIME_SMOKE_PASS assertions=" + _assertions);
        }

        private static void TestTween()
        {
            double value = -1;
            var t = new WahuTween(0, 10, 100, TweenEasing.Linear, x => value = x);
            t.StartAt(1000);
            Assert(value == 0 && t.State == TweenState.Active, "tween_start_deterministic");
            t.Tick(1050);
            Assert(Math.Abs(value - 5) < 0.001, "tween_mid_linear");
            t.Tick(1100);
            Assert(value == 10 && t.State == TweenState.Completed, "tween_final_exact");

            value = 0;
            var cancelled = new WahuTween(2, 8, 100, TweenEasing.EaseOutCubic, x => value = x);
            cancelled.StartAt(0);
            cancelled.Tick(25);
            cancelled.Cancel(false);
            var held = value;
            cancelled.Tick(100);
            Assert(cancelled.State == TweenState.Cancelled && value == held, "tween_cancel_holds_value");

            var snap = new WahuTween(2, 8, 100, TweenEasing.Linear, x => value = x);
            snap.StartAt(0);
            snap.Cancel(true);
            Assert(value == 8 && snap.State == TweenState.Cancelled, "tween_cancel_snap_end");
        }

        private static void TestSpritePlayer()
        {
            var loop = new SpriteSheetPlayer(4, 10, true);
            Assert(loop.FrameAt(0) == 0, "sprite_frame_zero");
            Assert(loop.FrameAt(250) == 2, "sprite_progress");
            Assert(loop.FrameAt(450) == 0, "sprite_loop_wrap");
            var once = new SpriteSheetPlayer(4, 10, false);
            Assert(once.FrameAt(900) == 3, "sprite_nonloop_clamp");
        }

        private static void TestPolicy()
        {
            var p = new MotionPolicyEvaluator();
            var d = p.Evaluate(R(MotionClass.DECORATIVE, question: true));
            Assert(!d.Allowed && d.Reason.Contains("learning_focus"), "decorative_off_during_question");
            d = p.Evaluate(R(MotionClass.DECORATIVE, state: "FLOW_LIKELY"));
            Assert(!d.Allowed, "decorative_off_during_flow");
            d = p.Evaluate(R(MotionClass.INSTRUCTIONAL, question: true, state: "STRAINED"));
            Assert(d.Allowed, "instructional_preserved_when_strained");
            d = p.Evaluate(R(MotionClass.SIGNAL, mode: MotionMode.Minimal));
            Assert(d.Allowed && d.PreferStaticSignal, "minimal_signal_becomes_static");
            d = p.Evaluate(R(MotionClass.NAVIGATION, profile: PerformanceProfile.LOW));
            Assert(d.FpsCap == 18 && d.MaxAnimatedRegions == 1, "low_profile_caps");
            d = p.Evaluate(R(MotionClass.NAVIGATION, profile: PerformanceProfile.NORMAL));
            Assert(d.FpsCap == 30 && d.MaxAnimatedRegions == 2, "normal_profile_caps");
            var signalBusy = R(MotionClass.SIGNAL);
            signalBusy.ActiveAnimatedRegions = 1;
            d = p.Evaluate(signalBusy);
            Assert(!d.Allowed && d.Reason == "single_signal_cue_budget_exhausted", "single_signal_cue_budget");
            d = p.Evaluate(R(MotionClass.FEEDBACK, visible: false));
            Assert(!d.Allowed && d.Reason == "offscreen_animation_unregister", "offscreen_blocked");
        }

        private static void TestBudget()
        {
            var b = new RenderBudgetMonitor();
            for (var i = 0; i < 12; i++) b.RecordFrame(i < 8 ? 10 : 55, 33.33);
            Assert(b.SampleCount == 12, "budget_samples");
            Assert(b.DroppedFrames == 4, "budget_dropped_count");
            Assert(b.ShouldDegrade(33.33), "budget_degrade_on_p95");
        }

        private static void TestSchedulerCoalescingAndFinalState()
        {
            var clock = new ManualClock();
            var context = new QueuedContext();
            double value = -1;
            using (var scheduler = new MotionScheduler(30, context, clock, false))
            {
                var tween = new WahuTween(0, 1, 100, TweenEasing.Linear, x => value = x);
                scheduler.Register(tween);
                Assert(scheduler.ActiveTrackCount == 1, "scheduler_register");
                clock.Now = 40;
                scheduler.Pulse();
                scheduler.Pulse();
                scheduler.Pulse();
                Assert(context.Pending == 1 && scheduler.PendingUiUpdateCount == 1, "scheduler_coalesces_pending_ui_post");
                context.DrainOne();
                Assert(value > 0.39 && value < 0.41 && scheduler.PendingUiUpdateCount == 0, "scheduler_uses_latest_stopwatch_elapsed");
                clock.Now = 150;
                scheduler.Pulse();
                context.DrainOne();
                Assert(value == 1 && tween.State == TweenState.Completed, "scheduler_final_state_exact");
                Assert(scheduler.ActiveTrackCount == 0, "scheduler_removes_completed");
            }
        }

        private static MotionRequest R(MotionClass c, bool question = false, string state = "READY", MotionMode mode = MotionMode.Normal, PerformanceProfile profile = PerformanceProfile.NORMAL, bool visible = true)
        {
            return new MotionRequest { MotionClass = c, QuestionActive = question, BehaviorState = state, Mode = mode, PerformanceProfile = profile, Visible = visible };
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }

        private sealed class ManualClock : IMotionClock
        {
            public double Now;
            public double ElapsedMilliseconds { get { return Now; } }
        }

        private sealed class QueuedContext : SynchronizationContext
        {
            private readonly Queue<Tuple<SendOrPostCallback, object>> _queue = new Queue<Tuple<SendOrPostCallback, object>>();
            public int Pending { get { return _queue.Count; } }
            public override void Post(SendOrPostCallback d, object state) { _queue.Enqueue(Tuple.Create(d, state)); }
            public void DrainOne()
            {
                if (_queue.Count == 0) throw new InvalidOperationException("No queued callback.");
                var item = _queue.Dequeue();
                item.Item1(item.Item2);
            }
        }
    }
}
