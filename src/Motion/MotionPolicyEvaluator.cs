using System;

namespace WAHU.Motion
{
    public sealed class MotionPolicyEvaluator
    {
        public MotionDecision Evaluate(MotionRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            var low = request.PerformanceProfile == PerformanceProfile.LOW;
            var fps = low ? 18 : 30;
            var maxRegions = low ? 1 : 2;
            var decision = new MotionDecision
            {
                Allowed = true,
                Reason = "allowed",
                FpsCap = fps,
                MaxAnimatedRegions = maxRegions,
                PreferStaticSignal = request.Mode == MotionMode.Minimal
            };

            if (!request.Visible)
                return Deny(decision, "offscreen_animation_unregister");

            if (request.ActiveAnimatedRegions >= maxRegions && request.MotionClass != MotionClass.INSTRUCTIONAL)
                return Deny(decision, "render_region_budget_exhausted");

            if (request.MotionClass == MotionClass.SIGNAL && request.ActiveAnimatedRegions >= 1)
                return Deny(decision, "single_signal_cue_budget_exhausted");

            if (request.MotionClass == MotionClass.DECORATIVE)
            {
                if (request.Mode != MotionMode.Normal)
                    return Deny(decision, "decorative_disabled_by_motion_mode");
                if (request.QuestionActive || request.ReadingOrListening)
                    return Deny(decision, "decorative_disabled_during_learning_focus");
                if (IsCalmFocusState(request.BehaviorState))
                    return Deny(decision, "decorative_disabled_by_behavior_state");
            }

            if (request.MotionClass == MotionClass.FEEDBACK && request.QuestionActive)
                return Deny(decision, "feedback_waits_until_attempt_complete");

            if (request.MotionClass == MotionClass.NAVIGATION && request.Mode == MotionMode.Minimal)
            {
                decision.Allowed = true;
                decision.Reason = "navigation_use_instant_or_static_transition";
                decision.PreferStaticSignal = true;
            }

            if (request.MotionClass == MotionClass.SIGNAL && request.Mode == MotionMode.Minimal)
            {
                decision.Allowed = true;
                decision.Reason = "signal_use_static_highlight";
                decision.PreferStaticSignal = true;
            }

            return decision;
        }

        private static bool IsCalmFocusState(string state)
        {
            return string.Equals(state, "FLOW_LIKELY", StringComparison.Ordinal) ||
                   string.Equals(state, "BORED_OR_UNDERCHALLENGED", StringComparison.Ordinal) ||
                   string.Equals(state, "STRAINED", StringComparison.Ordinal) ||
                   string.Equals(state, "FRUSTRATED_LIKELY", StringComparison.Ordinal) ||
                   string.Equals(state, "FATIGUED_LIKELY", StringComparison.Ordinal);
        }

        private static MotionDecision Deny(MotionDecision d, string reason)
        {
            d.Allowed = false;
            d.Reason = reason;
            return d;
        }
    }
}
