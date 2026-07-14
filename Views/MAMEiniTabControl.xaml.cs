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

        public MAMEiniTabControl()
        {
            InitializeComponent();
        }

        private string MameDirectory => _settings.GetMamePath();
        private string MameIniPath => Path.Combine(MameDirectory, "mame.ini");

        public void Initialize(ArcadeStick.Models.ConfigurationSettings settings)
        {
            _settings = settings;
            LoadMameIniSettings();
        }

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
                        case "multithreading": ChkMultithreading.IsChecked = (value == "1"); break;
                        case "vsync": ChkVsync.IsChecked = (value == "1"); break;
                        case "triplebuffer": ChkTripleBuffer.IsChecked = (value == "1"); break;
                        case "syncrefresh": ChkSyncRefresh.IsChecked = (value == "1"); break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading mame.ini: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void UpdateMameIniAndSyncPaths()
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
                string valMulti = (ChkMultithreading.IsChecked == true) ? "1" : "0";
                string valVsync = (ChkVsync.IsChecked == true) ? "1" : "0";
                string valTriple = (ChkTripleBuffer.IsChecked == true) ? "1" : "0";
                string valSyncRefresh = (ChkSyncRefresh.IsChecked == true) ? "1" : "0";

                string finalizedRomPath = BuildPortableRomPathString();
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
                    else if (key == "multithreading") fileLines[i] = FormatIniLine(key, valMulti);
                    else if (key == "vsync") fileLines[i] = FormatIniLine(key, valVsync);
                    else if (key == "triplebuffer") fileLines[i] = FormatIniLine(key, valTriple);
                    else if (key == "syncrefresh") fileLines[i] = FormatIniLine(key, valSyncRefresh);
                    else if (key == "rompath") fileLines[i] = FormatIniLine(key, finalizedRomPath);
                }
                File.WriteAllLines(MameIniPath, fileLines);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to write layout configurations: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string BuildPortableRomPathString()
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

        private string FormatIniLine(string key, string value) => $"{key.PadRight(26)}{value}";

        private void SetComboBoxValue(ComboBox box, string value)
        {
            foreach (ComboBoxItem item in box.Items)
            {
                if (item.Content.ToString() == value) { box.SelectedItem = item; break; }
            }
        }
    }
}