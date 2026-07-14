using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ArcadeStick.Views
{
    public partial class FolderOrderTabControl : UserControl
    {
        private ArcadeStick.Models.ConfigurationSettings _settings;
        private ArcadeStick.ViewModels.MainViewModel _viewModel;
        private ObservableCollection<string> _folderCategories = new();

        public FolderOrderTabControl()
        {
            InitializeComponent();
        }

        // [SECTION: Load - Discover & Merge Folder Order]
        // Discovers all current top-level folder headers from the live tree (excluding FAVORITES, which
        // isn't user-orderable), merges them against the saved order in folder_order.cfg, and appends any
        // newly discovered folders (not yet in the saved order) alphabetically at the end.
        public void Initialize(ArcadeStick.ViewModels.MainViewModel viewModel, ArcadeStick.Models.ConfigurationSettings settings)
        {
            _viewModel = viewModel;
            _settings = settings;

            var discoveredFolders = new HashSet<string>();
            if (_viewModel?.TreeNodesCollection != null)
            {
                foreach (var node in _viewModel.TreeNodesCollection)
                {
                    if (node is ArcadeStick.ViewModels.TreeCategoryNode categoryNode)
                    {
                        string folderHeader = categoryNode.HeaderText?.Trim().ToUpper() ?? "";
                        if (!string.IsNullOrEmpty(folderHeader) && folderHeader != "FAVORITES") discoveredFolders.Add(folderHeader);
                    }
                }
            }

            string configDirectory = _settings.GetConfigPath();
            string folderOrderFile = Path.Combine(configDirectory, "folder_order.cfg");
            var orderedList = new List<string>();

            if (File.Exists(folderOrderFile))
            {
                var lines = File.ReadAllLines(folderOrderFile);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim().ToUpper();
                    if (!string.IsNullOrEmpty(trimmed) && discoveredFolders.Contains(trimmed) && !orderedList.Contains(trimmed)) orderedList.Add(trimmed);
                }
            }

            var remainingFolders = discoveredFolders.Where(f => !orderedList.Contains(f)).OrderBy(f => f);
            foreach (var remaining in remainingFolders) orderedList.Add(remaining);

            _folderCategories = new ObservableCollection<string>(orderedList);
            LstFolderOrder.ItemsSource = _folderCategories;
            if (LstFolderOrder.Items.Count > 0) LstFolderOrder.SelectedIndex = 0;
        }
        // [END SECTION: Load - Discover & Merge Folder Order]

        // [SECTION: Reorder Controls]
        // Moves the selected list item one position up or down within _folderCategories, keeping selection
        // on the moved item.
        private void BtnFolderUp_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = LstFolderOrder.SelectedIndex;
            if (selectedIndex > 0)
            {
                string targetItem = _folderCategories[selectedIndex];
                _folderCategories.RemoveAt(selectedIndex);
                _folderCategories.Insert(selectedIndex - 1, targetItem);
                LstFolderOrder.SelectedIndex = selectedIndex - 1;
            }
        }

        private void BtnFolderDown_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = LstFolderOrder.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _folderCategories.Count - 1)
            {
                string targetItem = _folderCategories[selectedIndex];
                _folderCategories.RemoveAt(selectedIndex);
                _folderCategories.Insert(selectedIndex + 1, targetItem);
                LstFolderOrder.SelectedIndex = selectedIndex + 1;
            }
        }
        // [END SECTION: Reorder Controls]

        // [SECTION: Save Folder Order]
        // Persists the current _folderCategories order to folder_order.cfg and triggers a live tree
        // rebuild so the Game List panel immediately reflects the new order.
        private void BtnSaveFolderOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string configDirectory = _settings.GetConfigPath();
                if (!Directory.Exists(configDirectory)) Directory.CreateDirectory(configDirectory);
                string folderOrderFile = Path.Combine(configDirectory, "folder_order.cfg");
                File.WriteAllLines(folderOrderFile, _folderCategories);
                _viewModel?.UpdateLiveTreeDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to commit category sequence: {ex.Message}", "Storage Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // [END SECTION: Save Folder Order]
    }
}