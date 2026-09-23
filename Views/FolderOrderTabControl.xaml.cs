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
        private ObservableCollection<string> _subfolderCategories = new();
        private string? _selectedRootHeader;

        public FolderOrderTabControl()
        {
            InitializeComponent();
        }

        // [SECTION: Load - Discover & Merge Folder Order]
        // Discovers all current top-level folder headers from the live tree (excluding FAVORITES, which
        // isn't user-orderable), merges them against the saved order in folder_order.cfg/virtual_order.cfg,
        // and appends any newly discovered folders (not yet in the saved order) alphabetically at the end.
        public void Initialize(ArcadeStick.ViewModels.MainViewModel viewModel, ArcadeStick.Models.ConfigurationSettings settings)
        {
            _viewModel = viewModel;
            _settings = settings;

            var discoveredRoots = new HashSet<string>();
            if (_viewModel?.TreeNodesCollection != null)
            {
                foreach (var node in _viewModel.TreeNodesCollection)
                {
                    if (node is ArcadeStick.Models.TreeCategoryNode categoryNode)
                    {
                        string folderHeader = categoryNode.HeaderText?.Trim().ToUpper() ?? "";
                        if (!string.IsNullOrEmpty(folderHeader) && folderHeader != "FAVORITES" && folderHeader != "MOST PLAYED" && folderHeader != "RECENTLY PLAYED") discoveredRoots.Add(folderHeader);
                    }
                }
            }

            var orderedRoots = DiscoverAndMergeOrder(discoveredRoots, string.Empty);

            _folderCategories = new ObservableCollection<string>(orderedRoots);
            LstFolderOrder.ItemsSource = _folderCategories;

            _selectedRootHeader = null;
            _subfolderCategories = new ObservableCollection<string>();
            LstSubfolderOrder.ItemsSource = _subfolderCategories;
            SetSubfolderControlsEnabled(false);

            if (LstFolderOrder.Items.Count > 0) LstFolderOrder.SelectedIndex = 0;
        }
        // [END SECTION: Load - Discover & Merge Folder Order]

        // [SECTION: Shared Discover & Merge Helper]
        // Merges a set of currently-discovered folder headers against the saved order file's entries for
        // a given parent path (empty = root level, a root header = that root's direct subfolders),
        // appending anything not yet listed alphabetically at the end. Shared by both the root-level list
        // (Initialize) and the subfolder-level list (LstFolderOrder_SelectionChanged).
        private List<string> DiscoverAndMergeOrder(IEnumerable<string> discoveredHeaders, string parentPathUpper)
        {
            string configDirectory = _settings.GetConfigPath();
            string activeOrderFileName = _settings.IsCategorySortEnabled ? _settings.VirtualOrderFile : _settings.FolderOrderFile;
            string folderOrderFile = Path.Combine(configDirectory, activeOrderFileName);

            var discoveredSet = new HashSet<string>(discoveredHeaders, StringComparer.OrdinalIgnoreCase);
            var orderedList = new List<string>();

            if (File.Exists(folderOrderFile))
            {
                var lines = File.ReadAllLines(folderOrderFile);
                foreach (var rawLine in lines)
                {
                    string trimmed = rawLine.Trim().ToUpper();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    int lastBackslash = trimmed.LastIndexOf('\\');
                    string lineParent = lastBackslash >= 0 ? trimmed.Substring(0, lastBackslash) : string.Empty;
                    if (!lineParent.Equals(parentPathUpper, StringComparison.OrdinalIgnoreCase)) continue;

                    string header = lastBackslash >= 0 ? trimmed.Substring(lastBackslash + 1) : trimmed;
                    if (discoveredSet.Contains(header) && !orderedList.Contains(header)) orderedList.Add(header);
                }
            }

            var remaining = discoveredSet.Where(f => !orderedList.Contains(f)).OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
            foreach (var r in remaining) orderedList.Add(r);

            return orderedList;
        }
        // [END SECTION: Shared Discover & Merge Helper]

        // [SECTION: Root Selection -> Subfolder Drill-Down]
        // Populates the subfolder list with whatever root is currently selected. Any unsaved subfolder
        // reordering for the PREVIOUSLY selected root is discarded here by design - the tab's
        // instructions warn the user to save before switching roots.
        private void LstFolderOrder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedRootHeader = LstFolderOrder.SelectedItem as string;

            if (string.IsNullOrEmpty(_selectedRootHeader))
            {
                _subfolderCategories = new ObservableCollection<string>();
                LstSubfolderOrder.ItemsSource = _subfolderCategories;
                SetSubfolderControlsEnabled(false);
                return;
            }

            var rootNode = _viewModel?.TreeNodesCollection?.FirstOrDefault(n =>
                n.HeaderText.Equals(_selectedRootHeader, StringComparison.OrdinalIgnoreCase));

            var discoveredSubsList = rootNode?.SubFolders
                .Select(sf => sf.HeaderText.Trim().ToUpper())
                .Where(h => !string.IsNullOrEmpty(h))
                .ToList() ?? new List<string>();

            if (discoveredSubsList.Count == 0)
            {
                _subfolderCategories = new ObservableCollection<string>();
                LstSubfolderOrder.ItemsSource = _subfolderCategories;
                SetSubfolderControlsEnabled(false);
                return;
            }

            var orderedSubs = DiscoverAndMergeOrder(discoveredSubsList, _selectedRootHeader);
            _subfolderCategories = new ObservableCollection<string>(orderedSubs);
            LstSubfolderOrder.ItemsSource = _subfolderCategories;
            SetSubfolderControlsEnabled(true);

            if (LstSubfolderOrder.Items.Count > 0) LstSubfolderOrder.SelectedIndex = 0;
        }

        private void SetSubfolderControlsEnabled(bool enabled)
        {
            LstSubfolderOrder.IsEnabled = enabled;
            LstSubfolderOrder.Opacity = enabled ? 1.0 : 0.05;
            BtnSubfolderUp.IsEnabled = enabled;
            BtnSubfolderDown.IsEnabled = enabled;
        }
        // [END SECTION: Root Selection -> Subfolder Drill-Down]

        // [SECTION: Reorder Controls]
        // Moves the selected list item one position up or down within the given collection, keeping
        // selection on the moved item. Shared by both root and subfolder Up/Down handlers.
        private static void MoveSelectedItem(ListBox listBox, ObservableCollection<string> collection, int direction)
        {
            int selectedIndex = listBox.SelectedIndex;
            int targetIndex = selectedIndex + direction;

            if (selectedIndex < 0 || targetIndex < 0 || targetIndex >= collection.Count) return;

            string targetItem = collection[selectedIndex];
            collection.RemoveAt(selectedIndex);
            collection.Insert(targetIndex, targetItem);
            listBox.SelectedIndex = targetIndex;
        }

        private void BtnFolderUp_Click(object sender, RoutedEventArgs e) => MoveSelectedItem(LstFolderOrder, _folderCategories, -1);
        private void BtnFolderDown_Click(object sender, RoutedEventArgs e) => MoveSelectedItem(LstFolderOrder, _folderCategories, 1);
        private void BtnSubfolderUp_Click(object sender, RoutedEventArgs e) => MoveSelectedItem(LstSubfolderOrder, _subfolderCategories, -1);
        private void BtnSubfolderDown_Click(object sender, RoutedEventArgs e) => MoveSelectedItem(LstSubfolderOrder, _subfolderCategories, 1);
        // [END SECTION: Reorder Controls]

        // [SECTION: Save Folder Order]
        // Persists both lists' current order in one commit via MainViewModel.SaveFolderOrderBulk, which
        // replaces the entire root-level group wholesale (safe - the left list is always the complete
        // discovered set) and replaces only the currently-selected root's subfolder group, leaving every
        // other root's subfolder group untouched. Triggers a live tree rebuild so the Game List panel
        // immediately reflects the new order.
        private void BtnSaveFolderOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel?.SaveFolderOrderBulk(_folderCategories.ToList(), _selectedRootHeader, _subfolderCategories.ToList());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to commit category sequence: {ex.Message}", "Storage Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // [END SECTION: Save Folder Order]
    }
}