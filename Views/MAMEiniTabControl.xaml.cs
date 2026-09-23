using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ArcadeStick.Views
{
    public partial class MAMEiniTabControl : UserControl
    {
        private ArcadeStick.Models.ConfigurationSettings _settings;

        // Captures whatever artpath value mame.ini actually has on load (the real bezel folder),
        // so it can be restored on Save if the user re-checks ChkEnableBezels after disabling.
        // Falls back to "artwork" if artpath was never set to a real path to begin with.
        private string _realArtPath = "artwork";

        public MAMEiniTabControl()
        {
            InitializeComponent();
        }

        private string MameDirectory => _settings.GetMamePath();
        private string MameIniPath => Path.Combine(MameDirectory, "mame.ini");

        // skip_warnings lives in ui.ini, not mame.ini - separate file, same directory, same
        // "key value" line format, so it's read/written alongside mame.ini's own settings below.
        private string UiIniPath => Path.Combine(MameDirectory, "ui.ini");

        // [SECTION: Load mame.ini Values]
        // Reads mame.ini directly and maps recognized keys onto this tab's controls.
        public void Initialize(ArcadeStick.Models.ConfigurationSettings settings)
        {
            _settings = settings;
            LoadMameIniSettings();
            LoadUiIniSettings();
        }

        // Parses ui.ini the same way LoadMameIniSettings parses mame.ini - separate file, same
        // "key value" line format. Only skip_warnings is recognized here for now.
        private void LoadUiIniSettings()
        {
            if (string.IsNullOrEmpty(UiIniPath) || !File.Exists(UiIniPath)) return;
            try
            {
                string[] lines = File.ReadAllLines(UiIniPath);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                    string[] parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    if (parts[0] == "skip_warnings")
                    {
                        ChkSkipWarnings.IsChecked = (parts[1] == "1");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading ui.ini: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Parses mame.ini line-by-line ("key value" pairs, # comments skipped) and populates the
        // matching ComboBox/CheckBox. Note "throttle" and "disable throttling" are inverted (throttle=0 means
        // throttling IS disabled, so ChkDisableThrottle is checked when the raw value is "0").
        private void LoadMameIniSettings()
        {
            if (string.IsNullOrEmpty(MameIniPath) || !File.Exists(MameIniPath)) return;
            try
            {
                string[] lines = File.ReadAllLines(MameIniPath);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                    string[] parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    string key = parts[0];
                    string value = parts[1];

                    switch (key)
                    {
                        case "video": SetComboBoxValue(CboVideoMode, value); break;
                        case "frameskip": SetComboBoxValue(CboFrameSkip, value); break;
                        case "window": ChkWindow.IsChecked = (value == "1"); break;
                        case "cheat": ChkCheat.IsChecked = (value == "1"); break;
                        case "hlsl_enable": ChkHlslEnable.IsChecked = (value == "1"); break;
                        case "keepaspect": ChkKeepAspect.IsChecked = (value == "1"); break;
                        case "skip_gameinfo": ChkSkipGameInfo.IsChecked = (value == "1"); break;
                        case "throttle": ChkDisableThrottle.IsChecked = (value == "0"); break;
                        case "filter": ChkFilter.IsChecked = (value == "1"); break;
                        case "waitvsync": ChkVsync.IsChecked = (value == "1"); break;
                        case "triplebuffer": ChkTripleBuffer.IsChecked = (value == "1"); break;
                        case "syncrefresh": ChkSyncRefresh.IsChecked = (value == "1"); break;
                        case "autoframeskip": ChkAutoFrameSkip.IsChecked = (value == "1"); break;
                        case "artpath":
                            if (value == "nobezels")
                            {
                                ChkEnableBezels.IsChecked = false;
                            }
                            else
                            {
                                _realArtPath = value;
                                ChkEnableBezels.IsChecked = true;
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading mame.ini: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // [END SECTION: Load mame.ini Values]

        // [SECTION: Save mame.ini Values]
        // Writes this tab's own control values back into mame.ini in place, preserving all other lines
        // including rompath. NOTE: rompath is intentionally NOT touched here - it is only written by
        // MainViewModel.SyncMameRomPathsAsync's boot-time sync. This method used to also rebuild and
        // overwrite rompath on every save via BuildPortableRomPathString, which clobbered manually-organized
        // category subfolders (Fighters/Shooters/Maze/etc.) any time the user saved unrelated MAME.ini tab
        // settings. Don't reintroduce a rompath write in this method without re-checking that history.
        public void UpdateMameIniSettings()
        {
            if (string.IsNullOrEmpty(MameIniPath) || !File.Exists(MameIniPath))
            {
                MessageBox.Show("mame.ini not found in your execution environment.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                string selectedVideo = (CboVideoMode.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "d3d";
                string selectedFrameSkip = (CboFrameSkip.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "auto";
                string valWindow = (ChkWindow.IsChecked == true) ? "1" : "0";
                string valCheat = (ChkCheat.IsChecked == true) ? "1" : "0";
                string valHlsl = (ChkHlslEnable.IsChecked == true) ? "1" : "0";
                string valAspect = (ChkKeepAspect.IsChecked == true) ? "1" : "0";
                string valSkipInfo = (ChkSkipGameInfo.IsChecked == true) ? "1" : "0";
                string valThrottle = (ChkDisableThrottle.IsChecked == true) ? "0" : "1";
                string valFilter = (ChkFilter.IsChecked == true) ? "1" : "0";
                string valVsync = (ChkVsync.IsChecked == true) ? "1" : "0";
                string valTriple = (ChkTripleBuffer.IsChecked == true) ? "1" : "0";
                string valSyncRefresh = (ChkSyncRefresh.IsChecked == true) ? "1" : "0";
                string valAutoFrameSkip = (ChkAutoFrameSkip.IsChecked == true) ? "1" : "0";
                string valArtPath = (ChkEnableBezels.IsChecked == true) ? _realArtPath : "nobezels";

                List<string> fileLines = File.ReadAllLines(MameIniPath).ToList();

                for (int i = 0; i < fileLines.Count; i++)
                {
                    string currentLine = fileLines[i].Trim();
                    if (string.IsNullOrEmpty(currentLine) || currentLine.StartsWith("#")) continue;
                    string[] parts = currentLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) continue;

                    string key = parts[0];
                    if (key == "video") fileLines[i] = FormatIniLine(key, selectedVideo);
                    else if (key == "frameskip") fileLines[i] = FormatIniLine(key, selectedFrameSkip);
                    else if (key == "window") fileLines[i] = FormatIniLine(key, valWindow);
                    else if (key == "cheat") fileLines[i] = FormatIniLine(key, valCheat);
                    else if (key == "hlsl_enable") fileLines[i] = FormatIniLine(key, valHlsl);
                    else if (key == "keepaspect") fileLines[i] = FormatIniLine(key, valAspect);
                    else if (key == "skip_gameinfo") fileLines[i] = FormatIniLine(key, valSkipInfo);
                    else if (key == "throttle") fileLines[i] = FormatIniLine(key, valThrottle);
                    else if (key == "filter") fileLines[i] = FormatIniLine(key, valFilter);
                    else if (key == "waitvsync") fileLines[i] = FormatIniLine(key, valVsync);
                    else if (key == "triplebuffer") fileLines[i] = FormatIniLine(key, valTriple);
                    else if (key == "syncrefresh") fileLines[i] = FormatIniLine(key, valSyncRefresh);
                    else if (key == "autoframeskip") fileLines[i] = FormatIniLine(key, valAutoFrameSkip);
                    else if (key == "artpath") fileLines[i] = FormatIniLine(key, valArtPath);
                }
                File.WriteAllLines(MameIniPath, fileLines);

                // skip_warnings lives in ui.ini, not mame.ini - written here as a second pass so this
                // remains the single Save entry point OptionsWindow calls, rather than adding a second
                // public method OptionsWindow would need to know to call separately.
                if (!string.IsNullOrEmpty(UiIniPath) && File.Exists(UiIniPath))
                {
                    string valSkipWarnings = (ChkSkipWarnings.IsChecked == true) ? "1" : "0";
                    List<string> uiFileLines = File.ReadAllLines(UiIniPath).ToList();

                    for (int i = 0; i < uiFileLines.Count; i++)
                    {
                        string currentLine = uiFileLines[i].Trim();
                        if (string.IsNullOrEmpty(currentLine) || currentLine.StartsWith("#")) continue;
                        string[] parts = currentLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 0) continue;

                        if (parts[0] == "skip_warnings")
                        {
                            uiFileLines[i] = FormatIniLine("skip_warnings", valSkipWarnings);
                        }
                    }
                    File.WriteAllLines(UiIniPath, uiFileLines);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to write layout configurations: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Builds the rompath value: bios folder, base roms folder, plus every immediate subfolder of the
        // roms folder, all as paths relative to the MAME directory (portable-friendly). Called only from
        // MainViewModel.SyncMameRomPathsAsync's boot-time sync, not from this tab's Save.
        public string BuildPortableRomPathString()
        {
            List<string> relativePaths = new List<string>();
            string biosDir = string.IsNullOrEmpty(_settings.BiosPath) ? "bios" : _settings.BiosPath;
            relativePaths.Add(biosDir);

            string romsBaseDir = string.IsNullOrEmpty(_settings.RomsSubFolder) ? "roms" : _settings.RomsSubFolder;
            relativePaths.Add(romsBaseDir);

            try
            {
                string fullRomsPath = Path.Combine(MameDirectory, romsBaseDir);
                if (Directory.Exists(fullRomsPath))
                {
                    string[] subDirectories = Directory.GetDirectories(fullRomsPath);
                    foreach (string dir in subDirectories)
                    {
                        relativePaths.Add(Path.Combine(romsBaseDir, Path.GetFileName(dir)));
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to scan subfolders: {ex.Message}"); }
            return string.Join(";", relativePaths);
        }
        // [END SECTION: Save mame.ini Values]

        // [SECTION: Helpers]
        // Formats a single mame.ini "key value" line with consistent key column padding.
        private string FormatIniLine(string key, string value) => $"{key.PadRight(26)}{value}";

        // Selects the ComboBoxItem whose Content matches the given raw ini value
        private void SetComboBoxValue(ComboBox box, string value)
        {
            foreach (ComboBoxItem item in box.Items)
            {
                if (item.Content.ToString() == value) { box.SelectedItem = item; break; }
            }
        }
        // [END SECTION: Helpers]
    }
}