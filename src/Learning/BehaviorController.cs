using System;
using System.Collections.Generic;
using System.Linq;

namespace WAHU.Learning
{
    public sealed class BehaviorController
    {
        private readonly BehaviorThresholds _thresholds;
        private readonly Queue<BehaviorObservation> _recent = new Queue<BehaviorObservation>();
        private PersonalBehaviorBaseline _baseline;
        private BehaviorState _state = BehaviorState.READY;
        private BehaviorState _pendingCandidate = BehaviorState.READY;
        private int _pendingCandidateCount;
        private int _cooldownRemaining;

        public BehaviorController(BehaviorThresholds thresholds = null)
        {
            _thresholds = thresholds ?? new BehaviorThresholds();
        }

        public BehaviorState CurrentState { get { return _state; } }
        public PersonalBehaviorBaseline Baseline { get { return _baseline; } }

        public void SeedPersonalBaseline(IEnumerable<BehaviorObservation> observations)
        {
            if (observations == null) throw new ArgumentNullException("observations");
            var valid = observations.Where(IsUsableObservation).ToList();
            if (valid.Count == 0) throw new ArgumentException("Baseline requires at least one usable observation.");
            _baseline = new PersonalBehaviorBaseline
            {
                Accuracy = valid.Count(x => x.IsCorrect) / (double)valid.Count,
                MedianResponseMs = Median(valid.Select(x => (double)x.ResponseMs)),
                SampleCount = valid.Count
            };
        }

        public BehaviorDecision Observe(BehaviorObservation observation)
        {
            if (!IsUsableObservation(observation)) throw new ArgumentException("Invalid behavior observation.");
            _recent.Enqueue(observation);
            while (_recent.Count > _thresholds.RecentAttemptWindow) _recent.Dequeue();

            if (_baseline == null)
                _baseline = BuildConservativeBaseline(_recent.ToList());

            return EvaluateCurrentWindow();
        }

        public BehaviorDecision EvaluateCurrentWindow()
        {
            var items = _recent.ToList();
            if (items.Count < _thresholds.MinimumEvidenceAttempts)
                return MakeDecision(BehaviorState.READY, 0.20, new List<string> { "insufficient_recent_evidence" }, items, false);

            var features = Extract(items);
            var evidence = new List<string>();
            BehaviorState candidate;
            double confidence;

            if (IsFatigued(features, evidence))
            {
                candidate = BehaviorState.FATIGUED_LIKELY;
                confidence = Confidence(0.66, evidence.Count, 3);
            }
            else
            {
                evidence.Clear();
                if (IsFrustrated(features, evidence))
                {
                    candidate = BehaviorState.FRUSTRATED_LIKELY;
                    confidence = Confidence(0.68, evidence.Count, 3);
                }
                else
                {
                    evidence.Clear();
                    if (IsStrained(features, evidence))
                    {
                        candidate = BehaviorState.STRAINED;
                        confidence = Confidence(0.60, evidence.Count, 3);
                    }
                    else
                    {
                        evidence.Clear();
                        if (IsBoredOrUnderchallenged(features, evidence))
                        {
                            candidate = BehaviorState.BORED_OR_UNDERCHALLENGED;
                            confidence = Confidence(0.64, evidence.Count, 3);
                        }
                        else
                        {
                            evidence.Clear();
                            if (IsFlow(features, evidence))
                            {
                                candidate = BehaviorState.FLOW_LIKELY;
                                confidence = Confidence(0.62, evidence.Count, 4);
                            }
                            else
                            {
                                candidate = BehaviorState.READY;
                                confidence = 0.45;
                                evidence.Add("no_state_has_sufficient_combined_evidence");
                            }
                        }
                    }
                }
            }

            var changed = ApplyHysteresis(candidate);
            return MakeDecision(candidate, confidence, evidence, items, changed, features);
        }

        private bool ApplyHysteresis(BehaviorState candidate)
        {
            if (_cooldownRemaining > 0)
            {
                _cooldownRemaining--;
                return false;
            }

            if (candidate == _state)
            {
                _pendingCandidate = candidate;
                _pendingCandidateCount = 0;
                return false;
            }

            if (candidate == BehaviorState.READY && _state != BehaviorState.READY)
            {
                _pendingCandidateCount++;
                if (_pendingCandidateCount >= _thresholds.StateExitConsecutiveEvaluations)
                    return ChangeState(candidate);
                return false;
            }

            if (_pendingCandidate != candidate)
            {
                _pendingCandidate = candidate;
                _pendingCandidateCount = 1;
            }
            else
            {
                _pendingCandidateCount++;
            }

            if (_pendingCandidateCount >= _thresholds.StateEnterConsecutiveEvaluations)
                return ChangeState(candidate);
            return false;
        }

        private bool ChangeState(BehaviorState next)
        {
            _state = next;
            _pendingCandidate = next;
            _pendingCandidateCount = 0;
            _cooldownRemaining = _thresholds.StateChangeCooldownQuestions;
            return true;
        }

        private BehaviorDecision MakeDecision(BehaviorState candidate, double confidence, IList<string> evidence, IList<BehaviorObservation> items, bool changed, Features features = null)
        {
            features = features ?? Extract(items);
            var actions = ActionsFor(_state);
            var failuresOnLastSkill = ConsecutiveFailuresOnLastSkill(items);
            var repair = failuresOnLastSkill >= _thresholds.MaxConsecutiveTargetFailuresBeforeRepair;
            if (repair && !actions.Contains("prerequisite_repair")) actions.Add("prerequisite_repair");

            return new BehaviorDecision
            {
                State = _state,
                CandidateState = candidate,
                Confidence = Clamp(confidence),
                Evidence = evidence.ToList(),
                Actions = actions,
                StateChanged = changed,
                ProtectMasteryFromNegativeUpdate = _state == BehaviorState.FATIGUED_LIKELY && !features.HasRepeatedSkillErrorEvidence,
                TriggerPrerequisiteRepair = repair,
                RecentAttemptCount = items.Count,
                DistinctRecentSkillCount = features.DistinctSkillCount,
                RecentAccuracy = features.Accuracy,
                ResponseTimeToPersonalMedianRatio = features.ResponseRatio
            };
        }

        private bool IsFlow(Features f, IList<string> e)
        {
            if (f.Accuracy < _thresholds.FlowAccuracyMin || f.Accuracy > _thresholds.FlowAccuracyMax) return false;
            if (f.HintRate > _thresholds.FlowMaxHintRate || f.RapidWrongRate > _thresholds.FlowMaxRapidWrongRate) return false;
            if (f.ResponseRatio < _thresholds.FlowResponseRatioMin || f.ResponseRatio > _thresholds.FlowResponseRatioMax) return false;
            e.Add("accuracy_in_flow_band");
            e.Add("hint_rate_low");
            e.Add("rapid_wrong_rate_low");
            e.Add("response_time_near_personal_baseline");
            return true;
        }

        private bool IsBoredOrUnderchallenged(Features f, IList<string> e)
        {
            if (f.Accuracy < _thresholds.BoredAccuracyMin || f.MasteryAverage < _thresholds.BoredMasteryMin) return false;
            var underchallenge = false;
            e.Add("very_high_accuracy");
            e.Add("stable_mastery");
            if (f.ResponseRatio <= _thresholds.BoredFastResponseRatioMax)
            {
                e.Add("fast_vs_personal_baseline");
                underchallenge = true;
            }
            if (f.MaxSameRepresentationCount >= _thresholds.BoredSameRepresentationCountMin)
            {
                e.Add("representation_repetition");
                underchallenge = true;
            }
            return underchallenge;
        }

        private bool IsStrained(Features f, IList<string> e)
        {
            var signals = 0;
            if (f.ErrorCount >= _thresholds.StrainedRecentErrorCountMin) { signals++; e.Add("recent_errors"); }
            if (f.ResponseRatio >= _thresholds.StrainedResponseRatioMin) { signals++; e.Add("response_time_rising"); }
            if (f.HintRate >= _thresholds.StrainedHintRateMin) { signals++; e.Add("hint_use_rising"); }
            return signals >= 2;
        }

        private bool IsFrustrated(Features f, IList<string> e)
        {
            if (f.MaxSameErrorCount < _thresholds.FrustratedSameErrorPatternCountMin) return false;
            e.Add("repeated_same_error_pattern");
            var additional = 0;
            if (f.MaxHintEvents >= _thresholds.FrustratedMaxHintEventsRecentMin) { additional++; e.Add("max_hint_used"); }
            if (f.RapidWrongRate >= _thresholds.FrustratedRapidWrongRateMin) { additional++; e.Add("rapid_wrong_inputs"); }
            if (f.SkipOrExitCount >= _thresholds.FrustratedSkipOrExitSignalMin) { additional++; e.Add("skip_or_exit_signal"); }
            if (f.ResponseVariabilityRatio >= _thresholds.FrustratedResponseVariabilityRatioMin) { additional++; e.Add("response_time_variability"); }
            return additional >= 1;
        }

        private bool IsFatigued(Features f, IList<string> e)
        {
            if (f.DistinctSkillCount < _thresholds.MinimumDistinctSkillsForFatigue) return false;
            var deterioration = 0;
            if (f.AccuracyDropFromBaseline >= _thresholds.FatigueAccuracyDropMin) { deterioration++; e.Add("cross_skill_accuracy_drop"); }
            if (f.ResponseRatio >= _thresholds.FatigueResponseRatioMin) { deterioration++; e.Add("global_response_slowdown"); }
            if (f.InputMissRate >= _thresholds.FatigueInputMissRateMin) { deterioration++; e.Add("input_miss_rate_rising"); }
            if (f.SessionElapsedMinutes >= _thresholds.FatigueFocusMinutesSoftSignal) e.Add("elapsed_focus_budget_soft_signal");
            return deterioration >= 2;
        }

        private Features Extract(IList<BehaviorObservation> items)
        {
            if (items == null || items.Count == 0) return new Features();
            var response = items.Where(IsUsableObservation).Select(x => (double)x.ResponseMs).ToList();
            var median = response.Count == 0 ? 1.0 : Median(response);
            var baselineMedian = _baseline == null || _baseline.MedianResponseMs <= 0 ? median : _baseline.MedianResponseMs;
            var mean = response.Count == 0 ? 0.0 : response.Average();
            var sd = response.Count < 2 ? 0.0 : Math.Sqrt(response.Sum(x => Math.Pow(x - mean, 2)) / response.Count);
            var repeatedErrors = items.Where(x => !string.IsNullOrWhiteSpace(x.ErrorType)).GroupBy(x => x.ErrorType).Select(g => g.Count()).DefaultIfEmpty(0).Max();
            return new Features
            {
                Accuracy = items.Count(x => x.IsCorrect) / (double)items.Count,
                AccuracyDropFromBaseline = Math.Max(0, (_baseline == null ? 0 : _baseline.Accuracy) - items.Count(x => x.IsCorrect) / (double)items.Count),
                ResponseRatio = baselineMedian <= 0 ? 1.0 : median / baselineMedian,
                ResponseVariabilityRatio = median <= 0 ? 0.0 : sd / median,
                HintRate = items.Count(x => x.HintLevel > 0) / (double)items.Count,
                RapidWrongRate = items.Count(x => x.RapidWrong && !x.IsCorrect) / (double)items.Count,
                InputMissRate = items.Count(x => x.InputMiss) / (double)items.Count,
                ErrorCount = items.Count(x => !x.IsCorrect),
                MaxHintEvents = items.Count(x => x.UsedMaxHint),
                SkipOrExitCount = items.Count(x => x.SkippedOrExited),
                MaxSameErrorCount = repeatedErrors,
                HasRepeatedSkillErrorEvidence = repeatedErrors >= 2,
                DistinctSkillCount = items.Select(x => x.SkillId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).Count(),
                MaxSameRepresentationCount = items.Where(x => !string.IsNullOrWhiteSpace(x.Representation)).GroupBy(x => x.Representation).Select(g => g.Count()).DefaultIfEmpty(0).Max(),
                MasteryAverage = items.Average(x => x.MasteryScore),
                SessionElapsedMinutes = items.Max(x => x.SessionElapsedMinutes)
            };
        }

        private static PersonalBehaviorBaseline BuildConservativeBaseline(IList<BehaviorObservation> items)
        {
            var valid = items.Where(IsUsableObservation).ToList();
            return new PersonalBehaviorBaseline
            {
                Accuracy = valid.Count == 0 ? 0.80 : valid.Count(x => x.IsCorrect) / (double)valid.Count,
                MedianResponseMs = valid.Count == 0 ? 3000 : Median(valid.Select(x => (double)x.ResponseMs)),
                SampleCount = valid.Count
            };
        }

        private static int ConsecutiveFailuresOnLastSkill(IList<BehaviorObservation> items)
        {
            if (items == null || items.Count == 0) return 0;
            var skill = items[items.Count - 1].SkillId;
            var count = 0;
            for (var i = items.Count - 1; i >= 0; i--)
            {
                var x = items[i];
                if (!string.Equals(x.SkillId, skill, StringComparison.Ordinal) || x.IsCorrect) break;
                count++;
            }
            return count;
        }

        private static List<string> ActionsFor(BehaviorState state)
        {
            switch (state)
            {
                case BehaviorState.FLOW_LIKELY: return new List<string> { "keep_challenge", "minimize_interruptions", "minimal_feedback" };
                case BehaviorState.BORED_OR_UNDERCHALLENGED: return new List<string> { "representation_transfer", "context_transfer", "fade_scaffold", "small_difficulty_increase" };
                case BehaviorState.STRAINED: return new List<string> { "reduce_extraneous_load", "small_cue", "visual_representation", "segment_task" };
                case BehaviorState.FRUSTRATED_LIKELY: return new List<string> { "neutralize_failure_feedback", "worked_example", "prerequisite_repair", "meaningful_success_recovery" };
                case BehaviorState.FATIGUED_LIKELY: return new List<string> { "protect_mastery_without_skill_error_evidence", "offer_break", "positive_session_close", "defer_due_review" };
                default: return new List<string> { "normal_instruction" };
            }
        }

        private static bool IsUsableObservation(BehaviorObservation x)
        {
            return x != null && !string.IsNullOrWhiteSpace(x.SkillId) && x.ResponseMs >= 0 && x.MasteryScore >= 0 && x.MasteryScore <= 1;
        }

        private static double Median(IEnumerable<double> values)
        {
            var a = values.OrderBy(x => x).ToArray();
            if (a.Length == 0) return 0;
            var mid = a.Length / 2;
            return a.Length % 2 == 0 ? (a[mid - 1] + a[mid]) / 2.0 : a[mid];
        }

        private static double Confidence(double baseValue, int evidenceCount, int expected)
        {
            return Clamp(baseValue + Math.Min(0.25, 0.25 * evidenceCount / Math.Max(1.0, expected)));
        }

        private static double Clamp(double x) { return Math.Max(0, Math.Min(1, x)); }

        private sealed class Features
        {
            public double Accuracy;
            public double AccuracyDropFromBaseline;
            public double ResponseRatio = 1.0;
            public double ResponseVariabilityRatio;
            public double HintRate;
            public double RapidWrongRate;
            public double InputMissRate;
            public int ErrorCount;
            public int MaxHintEvents;
            public int SkipOrExitCount;
            public int MaxSameErrorCount;
            public bool HasRepeatedSkillErrorEvidence;
            public int DistinctSkillCount;
            public int MaxSameRepresentationCount;
            public double MasteryAverage;
            public double SessionElapsedMinutes;
        }
    }
}
