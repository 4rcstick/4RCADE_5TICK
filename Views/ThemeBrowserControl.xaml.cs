using System.Windows.Controls;

namespace ArcadeStick.Views
{
    // The THEME BROWSER tab's content. For now a placeholder that only counts refreshes; the catalog arrives in handoff 3.
    public partial class ThemeBrowserControl : UserControl
    {
        // [SECTION: Placeholder Refresh]
        // Exists only so navigation can be tested: how many times the Browser tab has been shown
        private int _refreshCount;

        public ThemeBrowserControl()
        {
            InitializeComponent();
            ShowRefreshCount();
        }

        // Does the catalog refresh. TEMPORARY PLACEHOLDER for the real catalog refresh (handoff 3): it only counts calls.
        public void RefreshCatalog()
        {
            _refreshCount++;
            ShowRefreshCount();
        }

        // Does the placeholder text update
        private void ShowRefreshCount()
        {
            TxtPlaceholder.Text = $"Theme Browser placeholder. Refreshed {_refreshCount} times.";
        }
        // [END SECTION: Placeholder Refresh]
    }
}
