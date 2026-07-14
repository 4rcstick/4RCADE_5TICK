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