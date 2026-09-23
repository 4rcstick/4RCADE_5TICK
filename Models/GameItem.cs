using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ArcadeStick.Models
{
    public class GameItem : INotifyPropertyChanged
    {
        private string _romName = string.Empty;
        private string _fullTitle = string.Empty;
        private string _folderPath = string.Empty;
        private string _displayTitle = string.Empty;
        private string _rawParentheticalInfo = string.Empty;

        // =========================================================================
        // 🏁 START: CORE DATABASE RECORD BACKING DATA PROPERTIES
        // =========================================================================
        public string RomName
        {
            get => _romName;
            set
            {
                if (_romName != value)
                {
                    _romName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FullTitle
        {
            get => _fullTitle;
            set
            {
                if (_fullTitle != value)
                {
                    _fullTitle = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayTitle));
                }
            }
        }

        public string FolderPath
        {
            get => _folderPath;
            set
            {
                if (_folderPath != value)
                {
                    _folderPath = value;
                    OnPropertyChanged();
                }
            }
        }

        // Raw parenthetical text captured from mame_cache.json's description before stripping (e.g.
        // "(Japan, Rev B)"). Used by the virtual category sort feature to differentiate clone/variant
        // entries from their parent via region/revision pattern matching.
        public string RawParentheticalInfo
        {
            get => _rawParentheticalInfo;
            set
            {
                if (_rawParentheticalInfo != value)
                {
                    _rawParentheticalInfo = value;
                    OnPropertyChanged();
                }
            }
        }

        // MAME's own cloneof attribute from -listxml - a structured parent/clone relationship, distinct
        // from (and more reliable than) sort_database.ini's Variants list, which is curated separately.
        // Empty string means this ROM is a parent (not a clone of anything).
        private string _cloneOf = string.Empty;
        public string CloneOf
        {
            get => _cloneOf;
            set
            {
                if (_cloneOf != value)
                {
                    _cloneOf = value;
                    OnPropertyChanged();
                }
            }
        }

        // Player count from -listxml's <input players="X"> attribute. 0 means unknown/not reported.
        private int _players;
        public int Players
        {
            get => _players;
            set
            {
                if (_players != value)
                {
                    _players = value;
                    OnPropertyChanged();
                }
            }
        }

        // Release year from -listxml's <year> element. Kept as a string rather than an int - MAME uses
        // non-numeric placeholders for unconfirmed dates (e.g. "19??"), so a strict numeric type would
        // either throw or lose that information. Empty string means -listxml didn't report a year at all.
        private string _year = string.Empty;
        public string Year
        {
            get => _year;
            set
            {
                if (_year != value)
                {
                    _year = value;
                    OnPropertyChanged();
                }
            }
        }

        // Manufacturer from -listxml's <manufacturer> element. Empty string means not reported.
        private string _manufacturer = string.Empty;
        public string Manufacturer
        {
            get => _manufacturer;
            set
            {
                if (_manufacturer != value)
                {
                    _manufacturer = value;
                    OnPropertyChanged();
                }
            }
        }

        // Performance fix: pushed directly from MainViewModel instead of resolved via RelativeSource
        // AncestorType=TreeView in the XAML template, to eliminate the ancestor visual-tree walk that was
        // causing severe UI freezes when generating containers for very large (30k+) folders.
        private double _fontSize = 14;
        public double FontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize != value)
                {
                    _fontSize = value;
                    OnPropertyChanged();
                }
            }
        }

        private System.Windows.Media.Brush _gameColor = System.Windows.Media.Brushes.White;
        public System.Windows.Media.Brush GameColor
        {
            get => _gameColor;
            set
            {
                if (_gameColor != value)
                {
                    _gameColor = value;
                    OnPropertyChanged();
                }
            }
        }

        private System.Windows.Media.Brush _gameHoverColor = System.Windows.Media.Brushes.White;
        public System.Windows.Media.Brush GameHoverColor
        {
            get => _gameHoverColor;
            set
            {
                if (_gameHoverColor != value)
                {
                    _gameHoverColor = value;
                    OnPropertyChanged();
                }
            }
        }

        private System.Windows.Media.Brush _gameSelectedBgColor = System.Windows.Media.Brushes.Black;
        public System.Windows.Media.Brush GameSelectedBgColor
        {
            get => _gameSelectedBgColor;
            set
            {
                if (_gameSelectedBgColor != value)
                {
                    _gameSelectedBgColor = value;
                    OnPropertyChanged();
                }
            }
        }

        private System.Windows.Media.Brush _gameSelectedColor = System.Windows.Media.Brushes.Cyan;
        public System.Windows.Media.Brush GameSelectedColor
        {
            get => _gameSelectedColor;
            set
            {
                if (_gameSelectedColor != value)
                {
                    _gameSelectedColor = value;
                    OnPropertyChanged();
                }
            }
        }

        private System.Windows.Media.Brush _arrowColor = System.Windows.Media.Brushes.Gray;
        public System.Windows.Media.Brush ArrowColor
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
        // =========================================================================
        // 🛑 END: CORE DATABASE RECORD BACKING DATA PROPERTIES
        // =========================================================================

        // =========================================================================
        // 🏁 START: DYNAMIC DISPLAY TITLE AND SELECTION STATUS PROPERTIES
        // =========================================================================
        private bool _isMouseSupported;

        public string DisplayTitle
        {
            get => string.IsNullOrEmpty(_displayTitle) ? FullTitle : _displayTitle;
            set
            {
                if (_displayTitle != value)
                {
                    _displayTitle = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsMouseSupported
        {
            get => _isMouseSupported;
            set
            {
                if (_isMouseSupported != value)
                {
                    _isMouseSupported = value;
                    OnPropertyChanged();
                }
            }
        }
        // =========================================================================
        // 🛑 END: DYNAMIC DISPLAY TITLE AND SELECTION STATUS PROPERTIES
        // =========================================================================


        // =========================================================================
        // 🏁 START: MVVM BINDING NOTIFICATION UTILITIES
        // =========================================================================
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        // =========================================================================
        // 🛑 END: MVVM BINDING NOTIFICATION UTILITIES
        // =========================================================================
    }
}