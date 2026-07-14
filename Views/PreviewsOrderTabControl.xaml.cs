using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ArcadeStick.Views
{
    public partial class PreviewsOrderTabControl : UserControl
    {
        private ArcadeStick.Models.ConfigurationSettings _settings;
        private ArcadeStick.ViewModels.MainViewModel _viewModel;
        private ObservableCollection<string> _mediaCategories = new();

        public PreviewsOrderTabControl()
        {
            InitializeComponent();
        }

        // [SECTION: Load - Merge Saved Order with Defaults]
        // Builds the displayed order from preview_order.cfg on disk, falling back to the view model's
        // current PreviewPriorityOrder (or a hardcoded default list) for any category not yet saved.
        // Displayed as uppercase in the ListBox; stored/compared lowercase internally.
        public void Initialize(ArcadeStick.ViewModels.MainViewModel viewModel, ArcadeStick.Models.ConfigurationSettings settings)
        {
            _viewModel = viewModel;
            _settings = settings;

            string configDirectory = _settings.GetConfigPath();
            string mediaOrderFile = Path.Combine(configDirectory, "preview_order.cfg");
            List<string> orderedList = new();

            var defaultOrder = new List<string> { "videos", "flyers", "screenshots", "titlescreens", "cabinets" };
            var activeOrder = (_viewModel != null && _viewModel.PreviewPriorityOrder.Count > 0)
                ? _viewModel.PreviewPriorityOrder
                : defaultOrder;

            if (File.Exists(mediaOrderFile))
            {
                var lines = File.ReadAllLines(mediaOrderFile);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim().ToLower();
                    if (!string.IsNullOrEmpty(trimmed) && !orderedList.Contains(trimmed))
                    {
                        orderedList.Add(trimmed);
                    }
                }
            }

            foreach (var defaultCategory in activeOrder)
            {
                string lowerDefault = defaultCategory.Trim().ToLower();
                if (!orderedList.Contains(lowerDefault)) orderedList.Add(lowerDefault);
            }

            _mediaCategories = new ObservableCollection<string>(orderedList.Select(c => c.ToUpper()));
            LstMediaOrder.ItemsSource = _mediaCategories;
            if (LstMediaOrder.Items.Count > 0) LstMediaOrder.SelectedIndex = 0;
        }
        // [END SECTION: Load - Merge Saved Order with Defaults]

        // [SECTION: Reorder Controls]
        // Moves the selected list item one position up or down within _mediaCategories, keeping selection
        // on the moved item.
        private void BtnMediaUp_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = LstMediaOrder.SelectedIndex;
            if (selectedIndex > 0)
            {
                string targetItem = _mediaCategories[selectedIndex];
                _mediaCategories.RemoveAt(selectedIndex);
                _mediaCategories.Insert(selectedIndex - 1, targetItem);
                LstMediaOrder.SelectedIndex = selectedIndex - 1;
            }
        }

        private void BtnMediaDown_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = LstMediaOrder.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _mediaCategories.Count - 1)
            {
                string targetItem = _mediaCategories[selectedIndex];
                _mediaCategories.RemoveAt(selectedIndex);
                _mediaCategories.Insert(selectedIndex + 1, targetItem);
                LstMediaOrder.SelectedIndex = selectedIndex + 1;
            }
        }
        // [END SECTION: Reorder Controls]

        // [SECTION: Save Preview Order]
        // Persists the current order to preview_order.cfg and pushes it live into
        // MainViewModel.PreviewPriorityOrder. The SelectedGame null-then-restore toggle below is a
        // deliberate trick to force UpdateActiveMediaPreviews to re-run immediately with the new order -
        // don't remove it as unnecessary, it's what makes the change visible without reselecting the game.
        private void BtnSaveMediaOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string configDirectory = _settings.GetConfigPath();
                if (!Directory.Exists(configDirectory)) Directory.CreateDirectory(configDirectory);

                string mediaOrderFile = Path.Combine(configDirectory, "preview_order.cfg");

                var formattedCategories = _mediaCategories.Select(c => c.ToLower()).ToList();
                File.WriteAllLines(mediaOrderFile, formattedCategories);

                if (_viewModel != null)
                {
                    _viewModel.PreviewPriorityOrder.Clear();
                    _viewModel.PreviewPriorityOrder.AddRange(formattedCategories);

                    // Toggle selection to force the UI to instantly refresh using the new priority order
                    var tempGame = _viewModel.SelectedGame;
                    _viewModel.SelectedGame = null;
                    _viewModel.SelectedGame = tempGame;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to commit media sequence: {ex.Message}", "Storage Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // [END SECTION: Save Preview Order]
    }
}