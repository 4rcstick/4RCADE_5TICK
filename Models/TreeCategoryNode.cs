using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ArcadeStick.Models
{
    // [SECTION: TreeCategoryNode Model]
    // Represents a folder/category row in the TreeView (e.g. SHOOTERS, FAVORITES, a custom playlist).
    // Holds child games, nested sub-folders, expand state (single-branch-open enforced here), and color.
    public class TreeCategoryNode : INotifyPropertyChanged
    {
        private bool _isNodeExpanded;
        public string HeaderText { get; set; } = string.Empty;
        private Brush _folderColor = Brushes.Cyan;
        public Brush FolderColor
        {
            get => _folderColor;
            set
            {
                if (_folderColor != value)
                {
                    _folderColor = value;
                    OnPropertyChanged();
                }
            }
        }
        // Performance fix: pushed directly from MainViewModel instead of resolved via RelativeSource
        // AncestorType=TreeView/Window in the XAML templates, to eliminate the ancestor visual-tree walk
        // that was causing severe UI freezes when generating containers for very large (30k+) folders.
        private double _folderFontSize = 16;
        public double FolderFontSize
        {
            get => _folderFontSize;
            set
            {
                if (_folderFontSize != value)
                {
                    _folderFontSize = value;
                    OnPropertyChanged();
                }
            }
        }

        private Brush _folderSelectedBgColor = Brushes.Black;
        public Brush FolderSelectedBgColor
        {
            get => _folderSelectedBgColor;
            set
            {
                if (_folderSelectedBgColor != value)
                {
                    _folderSelectedBgColor = value;
                    OnPropertyChanged();
                }
            }
        }

        private Brush _folderSelectedColor = Brushes.Cyan;
        public Brush FolderSelectedColor
        {
            get => _folderSelectedColor;
            set
            {
                if (_folderSelectedColor != value)
                {
                    _folderSelectedColor = value;
                    OnPropertyChanged();
                }
            }
        }

        private Brush _arrowColor = Brushes.Gray;
        public Brush ArrowColor
        {
            get => _arrowColor;
            set
            {
                if (_arrowColor != value)
                {
                    _arrowColor = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsCustomColor { get; set; } = false;
        public ObservableCollection<GameItem> ChildGames { get; set; } = new ObservableCollection<GameItem>();
        public ObservableCollection<TreeCategoryNode> SubFolders { get; set; } = new ObservableCollection<TreeCategoryNode>();

        // Tracks ROM names already added to ChildGames for O(1) dedupe checks. ChildGames.Contains(game)
        // was an O(n) linear scan repeated per-item during tree-building, which became quadratic and
        // caused multi-minute UI freezes on large single-bucket folders (confirmed via stress-testing the
        // virtual category sort feature on a 30k+ ROM set). Use TryAddChildGame instead of touching
        // ChildGames.Add directly wherever duplicate-checking is needed.
        private readonly HashSet<string> _childGameRomNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        // Adds a game to ChildGames only if its RomName hasn't already been added to this node.
        // Returns true if it was added, false if it was already present.
        public bool TryAddChildGame(GameItem game)
        {
            if (_childGameRomNames.Add(game.RomName))
            {
                ChildGames.Add(game);
                return true;
            }
            return false;
        }

        // Combines SubFolders + ChildGames into a single display sequence for the TreeView's HierarchicalDataTemplate
        public System.Collections.IEnumerable DisplayItems
        {
            get
            {
                foreach (var folder in SubFolders) yield return folder;
                foreach (var game in ChildGames) yield return game;
            }
        }

        // Collapsing this node also collapses all of its sub-folders (prevents stale expanded state underneath)
        public bool IsNodeExpanded
        {
            get => _isNodeExpanded;
            set
            {
                if (_isNodeExpanded != value)
                {
                    _isNodeExpanded = value;
                    OnPropertyChanged();

                    if (!_isNodeExpanded)
                    {
                        foreach (var sub in SubFolders)
                        {
                            sub.IsNodeExpanded = false;
                        }
                    }
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    // [END SECTION: TreeCategoryNode Model]
}