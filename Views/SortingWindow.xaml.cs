using ArcadeStick.ViewModels;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ArcadeStick.Views
{
    public partial class SortingWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly Models.ConfigurationSettings _settings;

        // [SECTION: Constructor & Initialization]
        // Anchors DataContext to the live ConfigurationSettings (same object MainViewModel uses), matching
        // OptionsWindow's pattern. Scalar checkboxes/slider bind directly via TwoWay bindings in XAML;
        // the region and player-count rows represent list membership, which plain bindings can't express,
        // so those are loaded manually here and read back manually on Apply.
        public SortingWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _settings = viewModel.Configuration;

            this.DataContext = _settings;

            LoadFilterListsIntoUi();

            // Keeps the Min/Max rating sliders from crossing each other - dragging Min above Max pushes
            // Max up to match, and vice versa, rather than allowing an inverted (nonsensical) range.
            RatingSlider.ValueChanged += RatingSlider_ValueChanged;
            RatingMaxSlider.ValueChanged += RatingMaxSlider_ValueChanged;

            // Same crossing-prevention behavior for the Year range sliders.
            YearMinSlider.ValueChanged += YearMinSlider_ValueChanged;
            YearMaxSlider.ValueChanged += YearMaxSlider_ValueChanged;
        }

        private void YearMinSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (YearMinSlider.Value > YearMaxSlider.Value)
            {
                YearMaxSlider.Value = YearMinSlider.Value;
            }
        }

        private void YearMaxSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (YearMaxSlider.Value < YearMinSlider.Value)
            {
                YearMinSlider.Value = YearMaxSlider.Value;
            }
        }

        private void RatingSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (RatingSlider.Value > RatingMaxSlider.Value)
            {
                RatingMaxSlider.Value = RatingSlider.Value;
            }
        }

        private void RatingMaxSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (RatingMaxSlider.Value < RatingSlider.Value)
            {
                RatingSlider.Value = RatingMaxSlider.Value;
            }
        }

        // Pre-checks the region/player-count checkboxes based on the persisted lists, since those can't
        // be expressed with a simple property binding the way scalar fields (IsCategorySortEnabled,
        // SortIncludeRevisions, SortMinimumRating) are in the XAML.
        private void LoadFilterListsIntoUi()
        {
            ChkRegionWorld.IsChecked = _settings.SortEnabledRegions.Contains("World");
            ChkRegionUS.IsChecked = _settings.SortEnabledRegions.Contains("US");
            ChkRegionJapan.IsChecked = _settings.SortEnabledRegions.Contains("Japan");
            ChkRegionAsia.IsChecked = _settings.SortEnabledRegions.Contains("Asia");
            ChkRegionEurope.IsChecked = _settings.SortEnabledRegions.Contains("Europe");
            ChkRegionOther.IsChecked = _settings.SortEnabledRegions.Contains("Other");
            ChkRegionUnspecified.IsChecked = _settings.SortEnabledRegions.Contains("Unspecified");

            ChkPlayers2.IsChecked = _settings.SortEnabledPlayerCounts.Contains(2);
            ChkPlayers4.IsChecked = _settings.SortEnabledPlayerCounts.Contains(4);

            ChkGenreShooter.IsChecked = _settings.SortEnabledGenres.Contains("Shooter");
            ChkGenreFighter.IsChecked = _settings.SortEnabledGenres.Contains("Fighter");
            ChkGenrePlatform.IsChecked = _settings.SortEnabledGenres.Contains("Platform");
            ChkGenrePuzzle.IsChecked = _settings.SortEnabledGenres.Contains("Puzzle");
            ChkGenreSports.IsChecked = _settings.SortEnabledGenres.Contains("Sports");
            ChkGenreBallPaddle.IsChecked = _settings.SortEnabledGenres.Contains("Ball & Paddle");
            ChkGenreMaze.IsChecked = _settings.SortEnabledGenres.Contains("Maze");
            ChkGenreDriving.IsChecked = _settings.SortEnabledGenres.Contains("Driving");

            EnableFiltersCheckBox.IsChecked = _settings.IsSortFilteringEnabled;

            // SortMinimumRating/SortMaximumRating are stored on the same 0-100 scale as
            // sort_database.ini's rating bands, but the sliders now display a friendlier 1-10 range -
            // scale down on load, scale back up on Apply (see BtnApplyFilters_Click).
            RatingSlider.Value = _settings.SortMinimumRating / 10;
            RatingMaxSlider.Value = _settings.SortMaximumRating / 10;

            YearMinSlider.Value = _settings.SortMinimumYear;
            YearMaxSlider.Value = _settings.SortMaximumYear;

            // Selects the matching ComboBoxItem by Content text, falling back to "Any" (index 0) if the
            // saved value is empty or somehow doesn't match a current item (e.g. after a future roster change).
            bool foundMatch = false;
            foreach (ComboBoxItem item in CmbManufacturer.Items)
            {
                if (string.Equals(item.Content?.ToString(), _settings.SortSelectedManufacturer, StringComparison.OrdinalIgnoreCase))
                {
                    CmbManufacturer.SelectedItem = item;
                    foundMatch = true;
                    break;
                }
            }
            if (!foundMatch)
            {
                CmbManufacturer.SelectedIndex = 0;
            }
        }
        // [END SECTION: Constructor & Initialization]

        // [SECTION: Apply Button Handler]
        // Reads every control's current state directly (rather than relying on the scalar TwoWay
        // bindings having already pushed into _settings) and hands it all to MainViewModel in one call,
        // which persists to disk and rebuilds the tree exactly once.
        private void BtnApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            var regions = new List<string>();
            if (ChkRegionWorld.IsChecked == true) regions.Add("World");
            if (ChkRegionUS.IsChecked == true) regions.Add("US");
            if (ChkRegionJapan.IsChecked == true) regions.Add("Japan");
            if (ChkRegionAsia.IsChecked == true) regions.Add("Asia");
            if (ChkRegionEurope.IsChecked == true) regions.Add("Europe");
            if (ChkRegionOther.IsChecked == true) regions.Add("Other");
            if (ChkRegionUnspecified.IsChecked == true) regions.Add("Unspecified");

            var playerCounts = new List<int>();
            if (ChkPlayers2.IsChecked == true) playerCounts.Add(2);
            if (ChkPlayers4.IsChecked == true) playerCounts.Add(4);

            var genres = new List<string>();
            if (ChkGenreShooter.IsChecked == true) genres.Add("Shooter");
            if (ChkGenreFighter.IsChecked == true) genres.Add("Fighter");
            if (ChkGenrePlatform.IsChecked == true) genres.Add("Platform");
            if (ChkGenrePuzzle.IsChecked == true) genres.Add("Puzzle");
            if (ChkGenreSports.IsChecked == true) genres.Add("Sports");
            if (ChkGenreBallPaddle.IsChecked == true) genres.Add("Ball & Paddle");
            if (ChkGenreMaze.IsChecked == true) genres.Add("Maze");
            if (ChkGenreDriving.IsChecked == true) genres.Add("Driving");

            string selectedManufacturer = (CmbManufacturer.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Any";
            if (selectedManufacturer == "Any") selectedManufacturer = string.Empty;

            _viewModel.ApplySortingWindowSettings(
                EnableAutoSortCheckBox.IsChecked == true,
                EnableFiltersCheckBox.IsChecked == true,
                regions,
                playerCounts,
                genres,
                IncludeRevisionsCheckBox.IsChecked == true,
                (int)RatingSlider.Value * 10,
                (int)RatingMaxSlider.Value * 10,
                (int)YearMinSlider.Value,
                (int)YearMaxSlider.Value,
                selectedManufacturer);
        }
        // [END SECTION: Apply Button Handler]

        private void BtnCloseSorting_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}