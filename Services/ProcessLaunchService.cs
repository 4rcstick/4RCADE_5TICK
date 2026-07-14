using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    public class ProcessLaunchService
    {
        private readonly ConfigurationSettings _settings;

        // =========================================================================
        // 🏁 START: LIFECYCLE AND DEPENDENCY INJECTION
        // =========================================================================
        public ProcessLaunchService(ConfigurationSettings settings)
        {
            _settings = settings;
        }
        // =========================================================================
        // 🛑 END: LIFECYCLE AND DEPENDENCY INJECTION
        // =========================================================================

        // =========================================================================
        // 🏁 START: ASYNCHRONOUS EMULATOR EXECUTION AND LIFECYCLE TRACKER ENGINE
        // =========================================================================
        public async Task LaunchGameAsync(GameItem game, Window parentWindow, WGIService? inputService = null)
        {
            if (game == null) return;

            string mameDir = _settings.GetMamePath();
            string mameExePath = Path.Combine(mameDir, _settings.MameExeName);
            string errorLogPath = Path.Combine(_settings.GetConfigPath(), "mame_error.txt");
            string iniPath = Path.Combine(mameDir, "mame.ini");
            bool iniModified = false;

            if (!File.Exists(mameExePath))
            {
                MessageBox.Show($"MAME Executable not found at:\n{mameExePath}", "Execution Fault", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Pause physical gamepad input event processing before launching MAME
            if (inputService != null)
            {
                await inputService.StopPollingLoopAsync();
            }

            // Hide the active launcher host interface container
            parentWindow.Hide();

            // Setup process configuration arguments wrapping the target ROM name explicitly
            var startInfo = new ProcessStartInfo
            {
                FileName = mameExePath,
                Arguments = $"\"{game.RomName}\"",
                WorkingDirectory = mameDir,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };

            try
            {
                // Dynamic configuration pass: injection matching specific game mouse rules
                if (game.IsMouseSupported && File.Exists(iniPath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(iniPath);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            string trimmed = lines[i].TrimStart();
                            if (trimmed.StartsWith("mouse ", StringComparison.OrdinalIgnoreCase) ||
                                trimmed.StartsWith("mouse\t", StringComparison.OrdinalIgnoreCase))
                            {
                                lines[i] = "mouse                     1";
                                iniModified = true;
                                break;
                            }
                        }
                        if (iniModified)
                        {
                            File.WriteAllLines(iniPath, lines);
                        }
                    }
                    catch { /* Safeguard execution integrity */ }
                }

                // Run the tracking routine completely isolated inside a separate background thread context
                await Task.Run(() =>
                {
                    using (var process = Process.Start(startInfo))
                    {
                        if (process != null)
                        {
                            // Capture standard diagnostic trace streams synchronously
                            string rawErrorText = process.StandardError.ReadToEnd();
                            process.WaitForExit();

                            // Evaluate process exit signatures to parse runtime anomalies
                            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(rawErrorText))
                            {
                                string cleanMessage = rawErrorText.Replace("Optional", "").Trim();
                                try
                                {
                                    File.WriteAllText(errorLogPath, cleanMessage);
                                }
                                catch { /* Prevent logging blockades from dropping execution */ }

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    MessageBox.Show($"MAME Error Encountered for ROM [{game.RomName}]:\n\n{cleanMessage}",
                                        "MAME Execution Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                                });
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"System execution trap: {ex.Message}", "Launcher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                // Dynamic configuration cleanup pass: revert changes to pristine default states
                if (iniModified && File.Exists(iniPath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(iniPath);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            string trimmed = lines[i].TrimStart();
                            if (trimmed.StartsWith("mouse ", StringComparison.OrdinalIgnoreCase) ||
                                trimmed.StartsWith("mouse\t", StringComparison.OrdinalIgnoreCase))
                            {
                                lines[i] = "mouse                     0";
                                break;
                            }
                        }
                        File.WriteAllLines(iniPath, lines);
                    }
                    catch { /* Safeguard recovery operations */ }
                }

                // Unhide the primary container workspace viewport and request focus anchors safely via the UI dispatcher
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        parentWindow.Show();
                        parentWindow.Activate();
                    }
                    catch { /* Prevent orphaned reference updates on sudden shutdown handles */ }
                });

                // Safely wake the physical hardware polling engine back up
                inputService?.StartPollingLoop();
            }
        }
        // =========================================================================
        // 🛑 END: ASYNCHRONOUS EMULATOR EXECUTION AND LIFECYCLE TRACKER ENGINE
        // =========================================================================
    }
}