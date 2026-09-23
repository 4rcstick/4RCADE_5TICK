using System;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    public class ProcessLaunchService
    {
        private readonly ConfigurationSettings _settings;

        // [SECTION: Lifecycle & Dependency Injection]
        // Stores the shared ConfigurationSettings instance used to locate mame.exe, mame.ini, and the error log path.
        public ProcessLaunchService(ConfigurationSettings settings)
        {
            _settings = settings;
        }
        // [END SECTION: Lifecycle & Dependency Injection]

        // [SECTION: Asynchronous Emulator Execution & Lifecycle Tracker Engine]
        // Launches MAME for a given game: pauses gamepad polling, hides the launcher window, optionally
        // patches mame.ini to force mouse support on for this run, starts the process on a background
        // thread and waits for it to exit, captures stderr for error reporting, then reverts the ini
        // patch and restores the launcher window + gamepad polling in the finally block regardless of
        // success/failure. IMPORTANT: the pause/resume of inputService polling here is tied to the
        // StopPollingLoopAsync deadlock fix - don't reorder these calls without retesting that fix.
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
                // Temporarily flips the "mouse" line in mame.ini to 1 for this launch only - reverted in the finally block below
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
                var launchStartTime = DateTime.UtcNow;

                await Task.Run(() =>
                {
                    using (var process = Process.Start(startInfo))
                    {
                        if (process != null)
                        {
                            // Capture standard diagnostic trace streams synchronously
                            string rawErrorText = process.StandardError.ReadToEnd();
                            process.WaitForExit();

                            // Play history (Recently Played / Most Played) - sessions under 60 seconds
                            // don't count, filtering out accidental misclicks/immediate closes from
                            // skewing the log. WaitForExit() above already blocks until MAME has fully
                            // closed, so this is the correct moment to measure elapsed session time.
                            double sessionSeconds = (DateTime.UtcNow - launchStartTime).TotalSeconds;
                            RecordPlayHistory(game.RomName, sessionSeconds);

                            // Evaluate process exit signatures to parse runtime anomalies
                            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(rawErrorText))
                            {
                                string cleanMessage = rawErrorText.Replace("Optional", "").Trim(); try
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
                // Only runs if we actually flipped the mouse line above - restores it to 0 no matter how the launch ended
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

                // Safely wake the physical hardware polling engine back up - pairs with StopPollingLoopAsync() above
                inputService?.StartPollingLoop();
            }
        }
        // [END SECTION: Asynchronous Emulator Execution & Lifecycle Tracker Engine]

        // [SECTION: Play History Tracking]
        // Records a completed play session for Recently Played / Most Played. One line per ROM in
        // play_history.cfg: RomName|LastPlayedUnixTimestamp|PlayCount|TotalPlaySeconds. The log is
        // deliberately uncapped - Recently Played/Most Played each independently compute their own
        // top-30 slice from this full history at tree-build time, so a game falling out of the visible
        // top 30 never loses its recorded history, and the play-stats footer text (which looks up ANY
        // selected game, not just ones currently in the visible top 30) keeps working correctly.
        private void RecordPlayHistory(string romName, double sessionSeconds)
        {
            if (sessionSeconds < 60) return; // matches the same 60-second floor used elsewhere for "did they actually play this"

            try
            {
                string historyPath = Path.Combine(_settings.GetConfigPath(), _settings.PlayHistoryFile);
                var lines = File.Exists(historyPath) ? File.ReadAllLines(historyPath).ToList() : new List<string>();

                long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                bool found = false;

                for (int i = 0; i < lines.Count; i++)
                {
                    var parts = lines[i].Split('|');
                    if (parts.Length >= 1 && parts[0].Equals(romName, StringComparison.OrdinalIgnoreCase))
                    {
                        int existingCount = parts.Length > 2 && int.TryParse(parts[2], out var c) ? c : 0;
                        double existingSeconds = parts.Length > 3 && double.TryParse(parts[3], out var s) ? s : 0;

                        lines[i] = $"{romName}|{nowUnix}|{existingCount + 1}|{existingSeconds + sessionSeconds}";
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    lines.Add($"{romName}|{nowUnix}|1|{sessionSeconds}");
                }

                File.WriteAllLines(historyPath, lines);
            }
            catch
            {
                // Play history is a non-critical convenience feature - never let a logging failure
                // disrupt the launch flow itself.
            }
        }
        // [END SECTION: Play History Tracking]
    }
}