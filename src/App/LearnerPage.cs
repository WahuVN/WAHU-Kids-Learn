using System.Windows.Forms;

namespace WAHUKidsLearn
{
    internal abstract class LearnerPage : UserControl
    {
        private LearnerLayoutProfile _layoutProfile = LearnerLayoutProfile.Standard;

        protected LearnerPage()
        {
            Dock = DockStyle.Fill;
            BackColor = ChildVisualTheme.Cream;
            Margin = Padding.Empty;
        }

        public abstract LearnerRoute Route { get; }
        public abstract string PageTitle { get; }
        public LearnerLayoutProfile LayoutProfile { get { return _layoutProfile; } }

        public void SetLayoutProfile(LearnerLayoutProfile profile)
        {
            if (_layoutProfile == profile) return;
            _layoutProfile = profile;
            ApplyLayoutProfile(profile);
        }

        protected virtual void ApplyLayoutProfile(LearnerLayoutProfile profile)
        {
        }

        public virtual void OnNavigatedTo()
        {
        }

        public virtual void OnNavigatedFrom()
        {
        }
    }
}
