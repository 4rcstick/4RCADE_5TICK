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
        private ArcadeStick.Models.ConfigurationSettings _settings = null!;
        private ArcadeStick.ViewModels.MainViewModel _viewModel = null!;
        private ObservableCollection<string> _folderCategories = new();
        private ObservableCollection<string> _subfolderCategories = new();
        private string? _selectedRootHeader;

        // The orders as last loaded from the live tree and order file (the root list, and the selected root's subfolders).
        // The lists differ from these only by the user's unapplied moves; the live state differs from these when folders
        // changed in the main window (or through Sorting) after loading.
        private List<string> _loadedRoots = new();
        private List<string> _loadedSubs = new();

        public FolderOrderTabControl()
        {
            InitializeComponent();
        }

        // [SECTION: Load - Discover & Merge Folder Order]
        // Stores the view model and settings, then loads both lists from the live tree (LoadFromLive)
        public void Initialize(ArcadeStick.ViewModels.MainViewModel viewModel, ArcadeStick.Models.ConfigurationSettings settings)
        {
            _viewModel = viewModel;
            _settings = settings;
            LoadFromLive(null);
        }

        // Returns the current root order: every top-level folder header from the live tree (excluding FAVORITES, MOST PLAYED
        // and RECENTLY PLAYED, which aren't user-orderable), merged against the saved order in folder_order.cfg /
        // virtual_order.cfg, with any folder not yet in the saved order appended alphabetically at the end
        private List<string> BuildRootOrder()
        {
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

            return DiscoverAndMergeOrder(discoveredRoots, string.Empty);
        }

        // Returns the current subfolder order of one root from the live tree and the order file (empty when it has none)
        private List<string> BuildSubOrder(string rootHeader)
        {
            var rootNode = _viewModel?.TreeNodesCollection?.FirstOrDefault(n =>
                n.HeaderText.Equals(rootHeader, StringComparison.OrdinalIgnoreCase));

            var discoveredSubsList = rootNode?.SubFolders
                .Select(sf => sf.HeaderText.Trim().ToUpper())
                .Where(h => !string.IsNullOrEmpty(h))
                .ToList() ?? new List<string>();

            return discoveredSubsList.Count == 0 ? new List<string>() : DiscoverAndMergeOrder(discoveredSubsList, rootHeader);
        }

        // Does the (re)load of both lists from the live state, dropping any unapplied moves; selects selectRoot when it still
        // exists, otherwise the first root, and hides the changed-folders notice
        private void LoadFromLive(string? selectRoot)
        {
            var orderedRoots = BuildRootOrder();
            _loadedRoots = orderedRoots.ToList();

            _folderCategories = new ObservableCollection<string>(orderedRoots);
            LstFolderOrder.ItemsSource = _folderCategories;

            _selectedRootHeader = null;
            _subfolderCategories = new ObservableCollection<string>();
            _loadedSubs = new List<string>();
            LstSubfolderOrder.ItemsSource = _subfolderCategories;
            SetSubfolderControlsEnabled(false);

            int selectIndex = selectRoot == null ? -1 : _folderCategories.IndexOf(selectRoot);
            if (selectIndex >= 0) LstFolderOrder.SelectedIndex = selectIndex;
            else if (LstFolderOrder.Items.Count > 0) LstFolderOrder.SelectedIndex = 0;

            TxtFolderOrderChanged.Visibility = Visibility.Collapsed;
        }
        // [END SECTION: Load - Discover & Merge Folder Order]

        // [SECTION: Live Refresh]
        // The Options window is modeless, so folders can be created, renamed, deleted or regrouped (Sorting) in the main
        // window while this tab holds its lists. Unapplied moves are never dropped silently and a stale list is never applied.

        // Returns true when the user has moved anything since the lists were loaded
        private bool HasUnappliedMoves() =>
            !_folderCategories.SequenceEqual(_loadedRoots) || !_subfolderCategories.SequenceEqual(_loadedSubs);

        // Returns true when the live folders or order file no longer match what the lists were loaded from
        private bool HasExternalChange()
        {
            if (!BuildRootOrder().SequenceEqual(_loadedRoots)) return true;
            return _selectedRootHeader != null && !BuildSubOrder(_selectedRootHeader).SequenceEqual(_loadedSubs);
        }

        // Does the catch-up when the Options window is activated: with no outside change nothing happens; with no unapplied
        // moves the lists simply reload (keeping the selected root); with both, the lists are kept and the notice shows
        public void RefreshFromLive()
        {
            if (_settings == null || _viewModel == null) return;

            if (!HasExternalChange())
            {
                TxtFolderOrderChanged.Visibility = Visibility.Collapsed;
                return;
            }

            if (!HasUnappliedMoves())
            {
                LoadFromLive(_selectedRootHeader);
                return;
            }

            TxtFolderOrderChanged.Visibility = Visibility.Visible;
        }
        // [END SECTION: Live Refresh]

        // [SECTION: Shared Discover & Merge Helper]
        // Merges a set of currently-discovered folder headers against the saved order file's entries for
        // a given parent path (empty = root level, a root header = that root's direct subfolders),
        // appending anything not yet listed alphabetically at the end. Shared by both the root-level list
        // (BuildRootOrder) and the subfolder-level list (BuildSubOrder).
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
            _loadedSubs = new List<string>();

            if (string.IsNullOrEmpty(_selectedRootHeader))
            {
                _subfolderCategories = new ObservableCollection<string>();
                LstSubfolderOrder.ItemsSource = _subfolderCategories;
                SetSubfolderControlsEnabled(false);
                return;
            }

            var orderedSubs = BuildSubOrder(_selectedRootHeader);

            if (orderedSubs.Count == 0)
            {
                _subfolderCategories = new ObservableCollection<string>();
                LstSubfolderOrder.ItemsSource = _subfolderCategories;
                SetSubfolderControlsEnabled(false);
                return;
            }

            _loadedSubs = orderedSubs.ToList();
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
        // immediately reflects the new order. Lists loaded before an outside folder change are never applied: with
        // unapplied moves it asks to reload first (No keeps the moves and saves nothing), without them it just reloads.
        private void BtnSaveFolderOrder_Click(object sender, RoutedEventArgs e)
        {
            if (HasExternalChange())
            {
                if (HasUnappliedMoves())
                {
                    var answer = MessageBox.Show(
                        "The folders changed in the main window after this list was loaded, so this order can't be applied as it is.\n\nReload the current folders? Your unapplied order changes will be discarded.",
                        "Folder Order", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                    if (answer != MessageBoxResult.Yes)
                    {
                        TxtFolderOrderChanged.Visibility = Visibility.Visible;
                        return;
                    }
                }

                LoadFromLive(_selectedRootHeader);
                return;
            }

            try
            {
                _viewModel?.SaveFolderOrderBulk(_folderCategories.ToList(), _selectedRootHeader, _subfolderCategories.ToList());

                // What was just applied is the new loaded state
                _loadedRoots = _folderCategories.ToList();
                _loadedSubs = _subfolderCategories.ToList();
                TxtFolderOrderChanged.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to commit category sequence: {ex.Message}", "Storage Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // [END SECTION: Save Folder Order]
    }
}