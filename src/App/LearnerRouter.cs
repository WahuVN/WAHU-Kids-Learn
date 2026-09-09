using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WAHUKidsLearn
{
    internal sealed class LearnerRouter
    {
        private readonly Panel _host;
        private readonly Func<LearnerRoute, LearnerPage> _pageFactory;
        private readonly Dictionary<LearnerRoute, LearnerPage> _cache = new Dictionary<LearnerRoute, LearnerPage>();
        private readonly Stack<LearnerRoute> _backStack = new Stack<LearnerRoute>();
        private LearnerPage _current;
        private LearnerLayoutProfile _layoutProfile = LearnerLayoutProfile.Standard;

        public LearnerRouter(Panel host, Func<LearnerRoute, LearnerPage> pageFactory)
        {
            _host = host ?? throw new ArgumentNullException("host");
            _pageFactory = pageFactory ?? throw new ArgumentNullException("pageFactory");
        }

        public event EventHandler RouteChanged;

        public LearnerRoute CurrentRoute { get { return _current == null ? LearnerRoute.Home : _current.Route; } }
        public string CurrentTitle { get { return _current == null ? "WAHU Kids Learn" : _current.PageTitle; } }
        public string CurrentPageTypeName { get { return _current == null ? string.Empty : _current.GetType().Name; } }
        public LearnerPage CurrentPage { get { return _current; } }
        public bool CanGoBack { get { return _backStack.Count > 0; } }

        public void Navigate(LearnerRoute route)
        {
            if (_current != null && _current.Route == route)
            {
                _current.OnNavigatedTo();
                return;
            }

            if (_current != null)
                _backStack.Push(_current.Route);
            Show(route);
        }

        public bool GoBack()
        {
            if (_backStack.Count == 0) return false;
            var route = _backStack.Pop();
            Show(route);
            return true;
        }

        public void ResetTo(LearnerRoute route)
        {
            _backStack.Clear();
            Show(route);
        }

        public void SetLayoutProfile(LearnerLayoutProfile profile)
        {
            if (_layoutProfile == profile) return;
            _layoutProfile = profile;
            foreach (var page in _cache.Values)
                page.SetLayoutProfile(profile);
        }

        private void Show(LearnerRoute route)
        {
            LearnerPage next;
            if (!_cache.TryGetValue(route, out next))
            {
                next = _pageFactory(route);
                if (next == null) throw new InvalidOperationException("Learner page factory returned null for " + route + ".");
                next.SetLayoutProfile(_layoutProfile);
                _cache[route] = next;
                _host.Controls.Add(next);
            }

            _host.SuspendLayout();
            try
            {
                if (_current != null)
                {
                    _current.OnNavigatedFrom();
                    _current.Visible = false;
                }
                _current = next;
                _current.Visible = true;
                _current.BringToFront();
                _current.OnNavigatedTo();
            }
            finally
            {
                _host.ResumeLayout(true);
            }

            var handler = RouteChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
