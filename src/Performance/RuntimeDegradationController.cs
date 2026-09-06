using System;
using System.Collections.Generic;

namespace WAHU.Performance
{
    public sealed class RuntimeDegradationController
    {
        private readonly Queue<bool> _overBudget = new Queue<bool>();
        private readonly int _window;
        private readonly int _requiredBadWindows;

        public RuntimeDegradationController(int window = 6, int requiredBadWindows = 4)
        {
            if (window < 3 || requiredBadWindows < 2 || requiredBadWindows > window) throw new ArgumentOutOfRangeException();
            _window = window;
            _requiredBadWindows = requiredBadWindows;
        }

        public DegradationStage Stage { get; private set; }
        public int DegradationCount { get; private set; }

        public DegradationStage ObserveWindow(double renderP95Ms, double inputP95Ms, double maxWorkingSetMb)
        {
            var bad = renderP95Ms > 40 || inputP95Ms > 80 || maxWorkingSetMb > 220;
            _overBudget.Enqueue(bad);
            while (_overBudget.Count > _window) _overBudget.Dequeue();
            var badCount = 0;
            foreach (var x in _overBudget) if (x) badCount++;
            if (_overBudget.Count == _window && badCount >= _requiredBadWindows)
            {
                Advance();
                _overBudget.Clear();
            }
            return Stage;
        }

        private void Advance()
        {
            if (Stage < DegradationStage.TemporaryLowMotion)
            {
                Stage = (DegradationStage)((int)Stage + 1);
                DegradationCount++;
            }
        }
    }
}
