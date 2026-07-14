// 🏁 START
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Gaming.Input;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    public class WGIService : IDisposable
    {
        private readonly ConfigurationSettings _settings;
        private readonly object _lock = new object();
        private readonly List<Gamepad> _activeGamepads = new List<Gamepad>();

        private CancellationTokenSource? _pollingCancelTokenSource;
        private Task? _pollingTask;
        private Gamepad? _trackedGamepad;

        // Tracking state for holding directions (continuous scrolling navigation)
        private GamepadReading _lastReading;
        private DateTime _lastActionTime = DateTime.MinValue;
        private bool _isInitialDelayActive = true;
        private string _lastActiveDirection = "None";

        // Keeps track of buttons held on the prior tick to identify clean single-press "down" transitions
        private GamepadButtons _previousButtonsState = GamepadButtons.None;

        // Event hooks to update your UI diagnostics window safely
        public event Action<int, string, bool>? PortStatusUpdated;
        public event Action<string>? ActiveInputUpdated;
        public event Action<string>? GamepadDirectionTriggered;

        // High-reliability discrete structural event for action execution engines
        public event Action<GamepadButtons>? GamepadButtonDownTriggered;

        public WGIService(ConfigurationSettings settings)
        {
            _settings = settings;

            // Hook into native Windows hardware attachment listeners
            Gamepad.GamepadAdded += OnGamepadAdded;
            Gamepad.GamepadRemoved += OnGamepadRemoved;

            // Enumerate any controllers already plugged in at startup
            lock (_lock)
            {
                foreach (var gpad in Gamepad.Gamepads)
                {
                    if (!_activeGamepads.Contains(gpad))
                    {
                        _activeGamepads.Add(gpad);
                    }
                }
                AssignTrackedGamepad();
            }

            // Start running the polling loop immediately if enabled
            if (_settings.EnableGamepadPolling)
            {
                StartPollingLoop();
            }
        }

        // =========================================================================
        // 🏁 START: SUB-SYSTEM BACKGROUND POLLING LOOP CORE ENGINE
        // =========================================================================
        public void StartPollingLoop()
        {
            if (_pollingTask != null) return; // Engine is already alive

            _pollingCancelTokenSource = new CancellationTokenSource();
            var token = _pollingCancelTokenSource.Token;

            _pollingTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    // Fail-safe check: If the WPF application context is tearing down or null during close,
                    // instantly break the loop to prevent thread execution exceptions.
                    if (System.Windows.Application.Current == null)
                    {
                        break;
                    }

                    // Check if ANY window in our application currently has active focus
                    bool isAppFocused = false;
                    try
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Double-check inside the dispatcher synchronization context to ensure it didn't drop out mid-flight
                            if (System.Windows.Application.Current?.Windows == null) return;

                            foreach (System.Windows.Window win in System.Windows.Application.Current.Windows)
                            {
                                if (win != null && win.IsActive)
                                {
                                    isAppFocused = true;
                                    break;
                                }
                            }
                        });
                    }
                    catch
                    {
                        // Fallback handle: If the dispatcher context terminates mid-invoke, safely drop inputs and break
                        break;
                    }

                    // Hardcoded lockdown rule: If no launcher windows are focused (e.g. MAME is running),
                    // completely drop inputs on the floor and sit idle to protect against background ghost navigation.
                    if (isAppFocused && _settings.EnableGamepadPolling)
                    {
                        PollGamepadState();
                    }
                    else if (!isAppFocused)
                    {
                        // Safely mirror a resting state label to UI diagnostics when backend tracking is out of focus
                        ActiveInputUpdated?.Invoke("Active Inputs: None (Launcher Window In Background)");
                    }

                    // Poll hardware at roughly 60Hz (~16ms intervals) to stay highly responsive
                    try
                    {
                        await Task.Delay(16, token);
                    }
                    catch (TaskCanceledException)
                    {
                        break; // Exit loop cleanly when thread cancellation token triggers
                    }
                }
            }, token);
        }

        public void StopPollingLoop()
        {
            if (_pollingCancelTokenSource != null)
            {
                _pollingCancelTokenSource.Cancel();
                try
                {
                    _pollingTask?.Wait();
                }
                catch { /* Prevent aggregation fault dumps */ }

                _pollingCancelTokenSource.Dispose();
                _pollingCancelTokenSource = null;
                _pollingTask = null;
            }
        }

        public async Task StopPollingLoopAsync()
        {
            if (_pollingCancelTokenSource != null)
            {
                _pollingCancelTokenSource.Cancel();
                try
                {
                    if (_pollingTask != null)
                    {
                        await _pollingTask;
                    }
                }
                catch { /* Prevent aggregation fault dumps */ }

                _pollingCancelTokenSource.Dispose();
                _pollingCancelTokenSource = null;
                _pollingTask = null;
            }
        }
        // 🏗️ END

        private void PollGamepadState()
        {
            Gamepad? currentGamepad = null;
            lock (_lock)
            {
                currentGamepad = _trackedGamepad;
            }

            if (currentGamepad == null)
            {
                ActiveInputUpdated?.Invoke("No Active Gamepad Tracked");
                return;
            }

            try
            {
                GamepadReading reading = currentGamepad.GetCurrentReading();

                // 1. Process standard button presses
                ParseButtons(reading);

                // 2. Process D-Pad and Analog Stick scrolling movements
                ParseNavigation(reading);

                _lastReading = reading;
            }
            catch
            {
                // Safeguard background loop tracking integrity from device timeouts
            }
        }

        // 🏁 START
        private void ParseButtons(GamepadReading reading)
        {
            var parts = new List<string>();

            // Isolate the entire hardware matrix including bumpers and D-pad directions
            GamepadButtons actionButtonsMask = reading.Buttons & (
                GamepadButtons.A |
                GamepadButtons.B |
                GamepadButtons.X |
                GamepadButtons.Y |
                GamepadButtons.Menu |
                GamepadButtons.View |
                GamepadButtons.LeftShoulder |
                GamepadButtons.RightShoulder |
                GamepadButtons.DPadUp |
                GamepadButtons.DPadDown |
                GamepadButtons.DPadLeft |
                GamepadButtons.DPadRight
            );

            // Core Buttons
            CheckButtonTransition(actionButtonsMask, GamepadButtons.A, "Button A / Fire 1", parts);
            CheckButtonTransition(actionButtonsMask, GamepadButtons.B, "Button B / Fire 2", parts);
            CheckButtonTransition(actionButtonsMask, GamepadButtons.X, "Button X", parts);
            CheckButtonTransition(actionButtonsMask, GamepadButtons.Y, "Button Y", parts);
            CheckButtonTransition(actionButtonsMask, GamepadButtons.Menu, "Menu / Start", parts);
            CheckButtonTransition(actionButtonsMask, GamepadButtons.View, "View / Select", parts);

            // Bumpers
            CheckButtonTransition(actionButtonsMask, GamepadButtons.LeftShoulder, "Left Bumper", parts);
            CheckButtonTransition(actionButtonsMask, GamepadButtons.RightShoulder, "Right Bumper", parts);

            // D-Pad Directionals
            CheckButtonTransition(actionButtonsMask, GamepadButtons.DPadUp, "D-Pad Up", parts);
            CheckButtonTransition(actionButtonsMask, GamepadButtons.DPadDown, "D-Pad Down", parts);
            CheckButtonTransition(actionButtonsMask, GamepadButtons.DPadLeft, "D-Pad Left", parts);
            CheckButtonTransition(actionButtonsMask, GamepadButtons.DPadRight, "D-Pad Right", parts);

            // Analog Triggers (Evaluated via float thresholds since they aren't standard bit flags)
            if (reading.LeftTrigger > 0.5) parts.Add("Left Trigger");
            if (reading.RightTrigger > 0.5) parts.Add("Right Trigger");

            double deadzone = _settings.JoystickDeadzonePercentage / 100.0;

            // Left Analog Joystick Directions
            if (Math.Abs(reading.LeftThumbstickX) > deadzone || Math.Abs(reading.LeftThumbstickY) > deadzone)
            {
                var stickParts = new List<string>();
                if (reading.LeftThumbstickY > deadzone) stickParts.Add("Up");
                if (reading.LeftThumbstickY < -deadzone) stickParts.Add("Down");
                if (reading.LeftThumbstickX < -deadzone) stickParts.Add("Left");
                if (reading.LeftThumbstickX > deadzone) stickParts.Add("Right");
                parts.Add($"L-Stick ({string.Join("+", stickParts)})");
            }

            // Right Analog Joystick Directions
            if (Math.Abs(reading.RightThumbstickX) > deadzone || Math.Abs(reading.RightThumbstickY) > deadzone)
            {
                var stickParts = new List<string>();
                if (reading.RightThumbstickY > deadzone) stickParts.Add("Up");
                if (reading.RightThumbstickY < -deadzone) stickParts.Add("Down");
                if (reading.RightThumbstickX < -deadzone) stickParts.Add("Left");
                if (reading.RightThumbstickX > deadzone) stickParts.Add("Right");
                parts.Add($"R-Stick ({string.Join("+", stickParts)})");
            }

            // Safely anchor the history state register for the next loop iteration pass
            _previousButtonsState = actionButtonsMask;

            string diagnosticText = parts.Count > 0 ? string.Join(" + ", parts) : "None";
            ActiveInputUpdated?.Invoke($"Active Inputs: {diagnosticText}");
        }
        // 🏗️ END

        private void CheckButtonTransition(GamepadButtons currentMask, GamepadButtons buttonFlag, string label, List<string> activePartsList)
        {
            if ((currentMask & buttonFlag) != 0)
            {
                activePartsList.Add(label);

                // Pure Edge Trigger: Fire only if it wasn't held down on the previous frame
                if ((_previousButtonsState & buttonFlag) == 0)
                {
                    GamepadButtonDownTriggered?.Invoke(buttonFlag);
                }
            }
        }
        // 🏗️ END

        private void ParseNavigation(GamepadReading reading)
        {
            string currentDirection = "None";
            double deadzone = _settings.JoystickDeadzonePercentage / 100.0;

            bool useDPad = _settings.NavigationMode.Contains("D-Pad");
            bool useAnalog = _settings.NavigationMode.Contains("Analog");

            // Check D-Pad inputs if allowed
            if (useDPad)
            {
                if ((reading.Buttons & GamepadButtons.DPadUp) != 0) currentDirection = "Up";
                else if ((reading.Buttons & GamepadButtons.DPadDown) != 0) currentDirection = "Down";
                else if ((reading.Buttons & GamepadButtons.DPadLeft) != 0) currentDirection = "Left";
                else if ((reading.Buttons & GamepadButtons.DPadRight) != 0) currentDirection = "Right";
            }

            // Check Left Analog Stick inputs if allowed and D-Pad isn't actively pressed
            if (useAnalog && currentDirection == "None")
            {
                if (reading.LeftThumbstickY > deadzone) currentDirection = "Up";
                else if (reading.LeftThumbstickY < -deadzone) currentDirection = "Down";
                else if (reading.LeftThumbstickX < -deadzone) currentDirection = "Left";
                else if (reading.LeftThumbstickX > deadzone) currentDirection = "Right";
            }

            DateTime now = DateTime.Now;

            if (currentDirection == "None")
            {
                _lastActiveDirection = "None";
                _isInitialDelayActive = true;
                return;
            }

            // Handle repeat scrolling intervals (initial delay vs continuous rapid repeat)
            if (currentDirection == _lastActiveDirection)
            {
                int requiredDelay = _isInitialDelayActive
                    ? _settings.JoystickInitialDelayMs
                    : _settings.JoystickRepeatDelayMs;

                if ((now - _lastActionTime).TotalMilliseconds >= requiredDelay)
                {
                    GamepadDirectionTriggered?.Invoke(currentDirection);
                    _lastActionTime = now;
                    _isInitialDelayActive = false; // Move into rapid repeat mode
                }
            }
            else
            {
                // First click in a brand new direction triggers immediately
                GamepadDirectionTriggered?.Invoke(currentDirection);
                _lastActionTime = now;
                _lastActiveDirection = currentDirection;
                _isInitialDelayActive = true; // Reset safety clock anchor
            }
        }

        private void OnGamepadAdded(object? sender, Gamepad gpad)
        {
            lock (_lock)
            {
                if (!_activeGamepads.Contains(gpad))
                {
                    _activeGamepads.Add(gpad);
                }
                AssignTrackedGamepad();
            }
            TriggerDiagnosticsUpdate();
        }

        private void OnGamepadRemoved(object? sender, Gamepad gpad)
        {
            lock (_lock)
            {
                if (_activeGamepads.Contains(gpad))
                {
                    _activeGamepads.Remove(gpad);
                }
                if (_trackedGamepad == gpad)
                {
                    _trackedGamepad = null;
                }
                AssignTrackedGamepad();
            }
            TriggerDiagnosticsUpdate();
        }

        private void AssignTrackedGamepad()
        {
            // Map index cleanly. If device ID is out of range, default to index 0.
            int indexToTrack = _settings.DirectInputDeviceId;
            if (indexToTrack < 0 || indexToTrack >= _activeGamepads.Count)
            {
                indexToTrack = 0;
            }

            if (_activeGamepads.Count > 0)
            {
                _trackedGamepad = _activeGamepads[indexToTrack];
            }
            else
            {
                _trackedGamepad = null;
            }
        }

        // =========================================================================
        // 🏁 START: HARDWARE SUBSYSTEM LIVE DIAGNOSTIC STREAM HOOKS
        // =========================================================================
        public void TriggerDiagnosticsUpdate()
        {
            lock (_lock)
            {
                // Pull active raw controllers directly since they cleanly provide DisplayName strings
                var rawControllers = RawGameController.RawGameControllers.ToList();

                for (int port = 0; port < 4; port++)
                {
                    if (port < _activeGamepads.Count)
                    {
                        var currentPad = _activeGamepads[port];
                        bool isCurrent = (_trackedGamepad == currentPad);

                        string controllerName = "Gamepad";

                        try
                        {
                            // Match the gamepad by checking if the RawGameController points to the same specialized gamepad object
                            var match = rawControllers.FirstOrDefault(rc => Gamepad.FromGameController(rc) == currentPad);

                            if (match != null && !string.IsNullOrWhiteSpace(match.DisplayName))
                            {
                                controllerName = match.DisplayName;
                            }
                        }
                        catch
                        {
                            controllerName = "Gamepad";
                        }

                        string statusLabel = isCurrent ? $"{controllerName} (Primary)" : controllerName;

                        PortStatusUpdated?.Invoke(port, statusLabel, true);
                    }
                    else
                    {
                        PortStatusUpdated?.Invoke(port, "Disconnected", false);
                    }
                }
            }
        }

        public void Dispose()
        {
            StopPollingLoop();
            Gamepad.GamepadAdded -= OnGamepadAdded;
            Gamepad.GamepadRemoved -= OnGamepadRemoved;
        }
    }
}
// 🏗️ END