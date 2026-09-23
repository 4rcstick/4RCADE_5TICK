using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcadeStick.Views
{
    public partial class MameKeybindsTabControl : UserControl
    {
        private ArcadeStick.Models.ConfigurationSettings _settings;

        // [SECTION: TEMP Keybind Test State]
        // Temporary state for the Phase 1 multi-slot proof-of-concept. Not the final architecture -
        // will be replaced when the real row-based Phase 1 editor is built out.
        private ArcadeStick.Services.WGIService? _wgiService;

        // Pending (unsaved) slot lists per MAME port type. Populated from the file on load, then
        // mutated locally by Capture/Clear All - nothing touches disk until Apply is clicked.
        private readonly Dictionary<string, List<string>> _pendingSlots = new();

        // Per-row UI element references, wired up in the constructor for easy lookup by port type.
        private Dictionary<string, TextBlock> _summaryLabels = new();

        // Active capture session state - null when no capture is in progress.
        private string? _capturingPortType;
        private readonly HashSet<Windows.Gaming.Input.GamepadButtons> _captureGamepadFlags = new();
        private readonly HashSet<Key> _captureKeys = new();
        private readonly HashSet<System.Windows.Input.MouseButton> _captureMouseButtons = new();
        private Action<Windows.Gaming.Input.GamepadButtons>? _activeGamepadHandler;
        private KeyEventHandler? _activeKeyHandler;
        private System.Windows.Input.MouseButtonEventHandler? _activeMouseHandler;
        private Action<string, string>? _activeStickHandler;
        private System.Windows.Threading.DispatcherTimer? _captureDebounceTimer;

        // Resolves to <MameRoot>\cfg\default.cfg via ConfigurationSettings.GetMamePath(), matching
        // the portable relative-path architecture. Requires Initialize() to have run first.
        private string TestDefaultCfgPath =>
            System.IO.Path.Combine(_settings.GetMamePath(), "cfg", "default.cfg");
        // [END SECTION: TEMP Keybind Test State]

        // Maps each row key to the real MAME port type(s) it writes to. Most rows are 1:1, but
        // Rotary Axis dual-writes the same captured axis into both P1_DIAL and P1_PADDLE so one
        // capture covers spinner/dial games and wheel/paddle games at once. The first entry in each
        // list is treated as canonical for reading/display purposes (both are always kept identical).
        private readonly Dictionary<string, List<string>> _rowPortTypes = new()
        {
            // Stick 1 dual-writes to both the single-stick port type (P1_JOYSTICK_*, most games)
            // and the twin-stick "first stick" port type (P1_JOYSTICKLEFT_*, confirmed via real
            // Robotron capture) - a game only ever has one or the other, so writing both is safe.
            { "STICK1_UP", new List<string> { "P1_JOYSTICK_UP", "P1_JOYSTICKLEFT_UP" } },
            { "STICK1_DOWN", new List<string> { "P1_JOYSTICK_DOWN", "P1_JOYSTICKLEFT_DOWN" } },
            { "STICK1_LEFT", new List<string> { "P1_JOYSTICK_LEFT", "P1_JOYSTICKLEFT_LEFT" } },
            { "STICK1_RIGHT", new List<string> { "P1_JOYSTICK_RIGHT", "P1_JOYSTICKLEFT_RIGHT" } },
            // Stick 2 only applies to twin-stick games - no single-stick equivalent to dual-write to.
            { "STICK2_UP", new List<string> { "P1_JOYSTICKRIGHT_UP" } },
            { "STICK2_DOWN", new List<string> { "P1_JOYSTICKRIGHT_DOWN" } },
            { "STICK2_LEFT", new List<string> { "P1_JOYSTICKRIGHT_LEFT" } },
            { "STICK2_RIGHT", new List<string> { "P1_JOYSTICKRIGHT_RIGHT" } },
            { "P1_BUTTON1", new List<string> { "P1_BUTTON1" } },
            { "P1_BUTTON2", new List<string> { "P1_BUTTON2" } },
            { "P1_BUTTON3", new List<string> { "P1_BUTTON3" } },
            { "P1_BUTTON4", new List<string> { "P1_BUTTON4" } },
            { "P1_BUTTON5", new List<string> { "P1_BUTTON5" } },
            { "P1_BUTTON6", new List<string> { "P1_BUTTON6" } },
            { "COIN1", new List<string> { "COIN1" } },
            { "START1", new List<string> { "START1" } },
            { "UI_CANCEL", new List<string> { "UI_CANCEL" } },
            { "UI_MENU", new List<string> { "UI_MENU" } },
            { "UI_PAUSE", new List<string> { "UI_PAUSE" } },
            { "TOGGLE_FULLSCREEN", new List<string> { "TOGGLE_FULLSCREEN" } },
            { "RENDER_SNAP", new List<string> { "RENDER_SNAP" } },
            { "ROTARY_AXIS", new List<string> { "P1_DIAL", "P1_PADDLE" } },
            { "GAS_PEDAL", new List<string> { "P1_PEDAL" } },
            { "BRAKE_PEDAL", new List<string> { "P1_PEDAL2" } },
        };

        // Slots that ClearRow can never remove for a given row - currently just UI_MENU's default
        // Tab binding (both NOT-clause alternates, confirmed via real captured .cfg data), since
        // losing menu access entirely is a hard failure a user could get stuck in.
        private readonly Dictionary<string, List<string>> _protectedSlots = new()
        {
            { "UI_MENU", new List<string> { "KEYCODE_TAB NOT KEYCODE_LALT", "KEYCODE_TAB NOT KEYCODE_RALT" } },
        };

        public MameKeybindsTabControl()
        {
            InitializeComponent();

            _summaryLabels = new Dictionary<string, TextBlock>
            {
                { "STICK1_UP", TxtP1JoyUpSummary },
                { "STICK1_DOWN", TxtP1JoyDownSummary },
                { "STICK1_LEFT", TxtP1JoyLeftSummary },
                { "STICK1_RIGHT", TxtP1JoyRightSummary },
                { "STICK2_UP", TxtP2JoyUpSummary },
                { "STICK2_DOWN", TxtP2JoyDownSummary },
                { "STICK2_LEFT", TxtP2JoyLeftSummary },
                { "STICK2_RIGHT", TxtP2JoyRightSummary },
                { "P1_BUTTON1", TxtP1Button1Summary },
                { "P1_BUTTON2", TxtP1Button2Summary },
                { "P1_BUTTON3", TxtP1Button3Summary },
                { "P1_BUTTON4", TxtP1Button4Summary },
                { "P1_BUTTON5", TxtP1Button5Summary },
                { "P1_BUTTON6", TxtP1Button6Summary },
                { "COIN1", TxtCoin1Summary },
                { "START1", TxtStart1Summary },
                { "UI_CANCEL", TxtUiCancelSummary },
                { "UI_MENU", TxtUiMenuSummary },
                { "UI_PAUSE", TxtUiPauseSummary },
                { "TOGGLE_FULLSCREEN", TxtFullscreenSummary },
                { "RENDER_SNAP", TxtSnapshotSummary },
                { "ROTARY_AXIS", TxtRotaryAxisSummary },
                { "GAS_PEDAL", TxtGasPedalSummary },
                { "BRAKE_PEDAL", TxtBrakePedalSummary },
            };

        }

        // [SECTION: Init]
        // Stores the settings reference and reads each row's current slots straight from
        // default.cfg so the UI reflects what's actually on disk when the tab is opened.
        public void Initialize(ArcadeStick.Models.ConfigurationSettings settings)
        {
            _settings = settings;

            foreach (var rowKey in _summaryLabels.Keys)
            {
                // Read from the canonical (first) port type only - dual-write rows are always kept
                // in sync, so reading either port type would give the same result.
                string canonicalPortType = _rowPortTypes[rowKey][0];
                var slots = ArcadeStick.Services.MameKeybindService.ReadExistingSlots(TestDefaultCfgPath, canonicalPortType);

                // Guarantee protected slots are present even on a fresh install where this port
                // type doesn't exist in default.cfg yet - a missing UI_MENU entry means MAME falls
                // back to its own internal default, but we want the protection visible/enforced
                // in our UI from the start rather than only after a user's first Apply.
                if (_protectedSlots.TryGetValue(rowKey, out var required))
                {
                    foreach (var requiredSlot in required)
                    {
                        if (!slots.Contains(requiredSlot)) slots.Add(requiredSlot);
                    }
                }

                _pendingSlots[rowKey] = slots;
                RenderRow(rowKey);
            }

            TxtApplyStatus.Text = "";
        }
        // [END SECTION: Init]

        // [SECTION: Live Diagnostics Wiring]
        public void WireLiveDiagnostics(ArcadeStick.Services.WGIService inputService)
        {
            if (inputService == null) return;
            _wgiService = inputService;
        }
        // [END SECTION: Live Diagnostics Wiring]

        // [SECTION: TEMP Keybind Test - Row Rendering]
        // Updates a row's compact summary line using friendly names via MameKeybindService.
        // Called after any change to that row's pending slots. Multiple slots wrap onto additional
        // lines (TextWrapping="Wrap" on the summary TextBlock) rather than hiding behind an expander.
        // For rows with protected slots (currently just UI_MENU's Tab exclusions), the underlying
        // config keeps both real newseq entries untouched - this only simplifies the display, collapsing
        // them into a single plain "Tab" label so the user doesn't need to know about the Alt exclusion,
        // just that it can't be removed. Any additional slots the user adds still display normally.
        private void RenderRow(string portType)
        {
            var slots = _pendingSlots[portType];
            var summaryLabel = _summaryLabels[portType];

            if (slots.Count == 0)
            {
                summaryLabel.Text = "—";
                return;
            }

            bool hasProtected = _protectedSlots.TryGetValue(portType, out var protectedSlots);
            var displayParts = new List<string>();
            bool protectedLabelShown = false;

            foreach (var slot in slots)
            {
                if (hasProtected && protectedSlots!.Contains(slot))
                {
                    if (!protectedLabelShown)
                    {
                        displayParts.Add("Tab");
                        protectedLabelShown = true;
                    }
                    continue; // remaining protected slots are already represented by the single "Tab" label
                }

                displayParts.Add(ArcadeStick.Services.MameKeybindService.GetFriendlySlotDisplay(slot));
            }

            summaryLabel.Text = string.Join("  |  ", displayParts);
        }
        // [END SECTION: TEMP Keybind Test - Row Rendering]

        // [SECTION: TEMP Keybind Test - Capture Logic]
        // Unified capture: listens for gamepad buttons, keyboard keys, AND mouse buttons simultaneously.
        // Any input(s) pressed within the debounce window are combined into one AND-combo slot
        // (space-joined codes) and appended to the row's pending list. Does not write to disk - that
        // happens on Apply.
        private string? _capturedStickCode; // set by a stick-direction firing during digital capture
        private bool _captureAcceptsSticks;  // true for directional rows - any stick, D-pad, or keyboard all valid

        private void StartCapture(string portType, bool acceptsSticks = false)
        {
            if (_wgiService == null) return;
            CancelActiveCapture();
            CancelAnalogCapture();

            _capturingPortType = portType;
            _captureGamepadFlags.Clear();
            _captureKeys.Clear();
            _captureMouseButtons.Clear();
            _capturedStickCode = null;
            _captureAcceptsSticks = acceptsSticks;

            _captureDebounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _captureDebounceTimer.Tick += (s, e) => FinalizeCapture();

            _activeGamepadHandler = (flag) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (ArcadeStick.Services.MameKeybindService.GetMameCode(flag) == null) return;
                    _captureGamepadFlags.Add(flag);
                    RestartDebounce();
                }));
            };
            _wgiService.GamepadButtonDownTriggered += _activeGamepadHandler;

            _activeKeyHandler = (s, e) =>
            {
                // Alt (and other "system keys") route through e.SystemKey rather than e.Key in WPF.
                Key actualKey = e.Key == Key.System ? e.SystemKey : e.Key;

                if (ArcadeStick.Services.MameKeybindService.GetMameCode(actualKey) == null) return;
                _captureKeys.Add(actualKey);
                e.Handled = true;
                RestartDebounce();
            };

            var window = Window.GetWindow(this);
            if (window != null) window.PreviewKeyDown += _activeKeyHandler;

            _activeMouseHandler = (s, e) =>
            {
                if (ArcadeStick.Services.MameKeybindService.GetMameCode(e.ChangedButton) == null) return;
                _captureMouseButtons.Add(e.ChangedButton);
                e.Handled = true;
                RestartDebounce();
            };

            if (window != null) window.PreviewMouseDown += _activeMouseHandler;

            // Stick direction is single-shot, not combo-capable (a stick can't meaningfully report
            // two directions as one bind the way button combos can) - the first firing during this
            // capture window wins and is folded in as its own code alongside whatever else was pressed.
            _activeStickHandler = (stick, direction) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!_captureAcceptsSticks) return; // this row doesn't accept stick input at all (buttons, UI hotkeys, etc.)
                    var code = ArcadeStick.Services.MameKeybindService.GetStickDirectionCode(stick, direction);
                    if (code == null || _capturedStickCode != null) return; // already have one, ignore further stick input this capture
                    _capturedStickCode = code;
                    RestartDebounce();
                }));
            };
            _wgiService.StickDirectionTriggered += _activeStickHandler;
        }

        private void RestartDebounce()
        {
            _captureDebounceTimer?.Stop();
            _captureDebounceTimer?.Start();
        }

        private void FinalizeCapture()
        {
            _captureDebounceTimer?.Stop();

            var portType = _capturingPortType;
            DetachCaptureHandlers();

            if (portType == null) return;

            var codes = new List<string>();
            codes.AddRange(ArcadeStick.Services.MameKeybindService.BuildComboCodes(_captureGamepadFlags));
            foreach (var key in _captureKeys)
            {
                var code = ArcadeStick.Services.MameKeybindService.GetMameCode(key);
                if (code != null) codes.Add(code);
            }
            foreach (var mouseButton in _captureMouseButtons)
            {
                var code = ArcadeStick.Services.MameKeybindService.GetMameCode(mouseButton);
                if (code != null) codes.Add(code);
            }
            if (_capturedStickCode != null)
            {
                codes.Add(_capturedStickCode);
            }

            if (codes.Count == 0)
            {
                _capturingPortType = null;
                return; // nothing valid was pressed before the debounce timed out
            }

            string newSlot = string.Join(" ", codes);
            _pendingSlots[portType].Add(newSlot);
            RenderRow(portType);

            _capturingPortType = null;
        }

        private void CancelActiveCapture()
        {
            _captureDebounceTimer?.Stop();
            DetachCaptureHandlers();
            _capturingPortType = null;
        }

        private void DetachCaptureHandlers()
        {
            if (_activeGamepadHandler != null && _wgiService != null)
            {
                _wgiService.GamepadButtonDownTriggered -= _activeGamepadHandler;
                _activeGamepadHandler = null;
            }

            if (_activeKeyHandler != null)
            {
                var window = Window.GetWindow(this);
                if (window != null) window.PreviewKeyDown -= _activeKeyHandler;
                _activeKeyHandler = null;
            }

            if (_activeMouseHandler != null)
            {
                var window = Window.GetWindow(this);
                if (window != null) window.PreviewMouseDown -= _activeMouseHandler;
                _activeMouseHandler = null;
            }

            if (_activeStickHandler != null && _wgiService != null)
            {
                _wgiService.StickDirectionTriggered -= _activeStickHandler;
                _activeStickHandler = null;
            }
        }

        private void ClearRow(string portType)
        {
            if (_protectedSlots.TryGetValue(portType, out var required))
            {
                // Reset to exactly the protected slots, discarding anything else the user added -
                // Clear All still works as "wipe everything," it just can't go below the floor.
                _pendingSlots[portType] = new List<string>(required);
            }
            else
            {
                _pendingSlots[portType].Clear();
            }

            RenderRow(portType);
        }
        // [END SECTION: TEMP Keybind Test - Capture Logic]

        // [SECTION: TEMP Keybind Test - Analog Capture]
        // Polls WGIService.GetCurrentAnalogReading() on a timer, comparing against a baseline taken
        // at capture start, until one axis moves past threshold. Unlike digital capture (event-driven,
        // combo-capable), analog capture is single-axis and single-slot per capture action - each
        // press appends one new slot, same as digital, but there's no combo concept for a raw axis.
        private System.Windows.Threading.DispatcherTimer? _analogCaptureTimer;
        private (double LeftX, double LeftY, double RightX, double RightY, double LeftTrigger, double RightTrigger) _analogBaseline;
        private System.Windows.Input.MouseEventHandler? _activeAnalogMouseHandler;
        private System.Windows.Point _mouseBaselinePosition;
        private const double MouseXCaptureThreshold = 15; // pixels of accumulated X movement to count as intentional

        // isPedalRow selects the _NEG suffix (Gas/Brake). allowMouseX enables the mouse-move race
        // alongside gamepad axis detection - only Rotary Axis accepts mouse, confirmed via Arkanoid's
        // real "JOYCODE_1_XAXIS OR MOUSECODE_1_XAXIS" dial binding. Whichever source (gamepad axis or
        // mouse X movement) crosses its threshold first wins and cancels the other.
        private void StartAnalogCapture(string rowKey, bool isPedalRow, bool allowMouseX = false)
        {
            if (_wgiService == null) return;
            CancelActiveCapture();
            CancelAnalogCapture();

            var baselineReading = _wgiService.GetCurrentAnalogReading();
            if (baselineReading == null) return; // no gamepad connected - nothing to capture from

            _analogBaseline = baselineReading.Value;

            _analogCaptureTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _analogCaptureTimer.Tick += (s, e) =>
            {
                var currentReading = _wgiService.GetCurrentAnalogReading();
                if (currentReading == null) return;

                var movedAxis = ArcadeStick.Services.MameKeybindService.DetectMovedAxis(_analogBaseline, currentReading.Value);
                if (movedAxis == null) return; // nothing moved past threshold yet - keep polling

                CancelAnalogCapture();

                string code = isPedalRow
                    ? ArcadeStick.Services.MameKeybindService.GetPedalAxisCode(movedAxis.Value)
                    : ArcadeStick.Services.MameKeybindService.GetRawAxisCode(movedAxis.Value);

                _pendingSlots[rowKey].Add(code);
                RenderRow(rowKey);
            };
            _analogCaptureTimer.Start();

            if (allowMouseX)
            {
                var window = Window.GetWindow(this);
                if (window != null)
                {
                    _mouseBaselinePosition = Mouse.GetPosition(window);
                    _activeAnalogMouseHandler = (s, e) =>
                    {
                        var currentPos = e.GetPosition(window);
                        double deltaX = Math.Abs(currentPos.X - _mouseBaselinePosition.X);
                        if (deltaX < MouseXCaptureThreshold) return; // hasn't moved enough yet - keep tracking

                        CancelAnalogCapture();

                        _pendingSlots[rowKey].Add(ArcadeStick.Services.MameKeybindService.MouseXAxisCode);
                        RenderRow(rowKey);
                    };
                    window.PreviewMouseMove += _activeAnalogMouseHandler;
                }
            }
        }

        private void CancelAnalogCapture()
        {
            _analogCaptureTimer?.Stop();
            _analogCaptureTimer = null;

            if (_activeAnalogMouseHandler != null)
            {
                var window = Window.GetWindow(this);
                if (window != null) window.PreviewMouseMove -= _activeAnalogMouseHandler;
                _activeAnalogMouseHandler = null;
            }
        }
        // [END SECTION: TEMP Keybind Test - Analog Capture]

        // [SECTION: TEMP Keybind Test - Apply]
        // Writes every row's current pending slot list to default.cfg in one pass.
        private void BtnApplyKeybinds_Click(object sender, RoutedEventArgs e)
        {
            foreach (var kvp in _pendingSlots)
            {
                // Write the same pending slot list to every port type this row maps to - this is
                // where Rotary Axis's dual-write to P1_DIAL + P1_PADDLE actually happens.
                foreach (var portType in _rowPortTypes[kvp.Key])
                {
                    ArcadeStick.Services.MameKeybindService.WriteBinding(TestDefaultCfgPath, portType, kvp.Value);
                }
            }

            TxtApplyStatus.Text = $"Applied at {DateTime.Now:T}";
        }
        // [END SECTION: TEMP Keybind Test - Apply]

        // [SECTION: TEMP Keybind Test - Button Handlers]
        private void BtnCaptureP1Button1_Click(object sender, RoutedEventArgs e) => StartCapture("P1_BUTTON1");
        private void BtnCaptureP1Button2_Click(object sender, RoutedEventArgs e) => StartCapture("P1_BUTTON2");
        private void BtnCaptureP1Button3_Click(object sender, RoutedEventArgs e) => StartCapture("P1_BUTTON3");
        private void BtnCaptureP1Button4_Click(object sender, RoutedEventArgs e) => StartCapture("P1_BUTTON4");
        private void BtnCaptureUiCancel_Click(object sender, RoutedEventArgs e) => StartCapture("UI_CANCEL");

        private void BtnClearP1Button1_Click(object sender, RoutedEventArgs e) => ClearRow("P1_BUTTON1");
        private void BtnClearP1Button2_Click(object sender, RoutedEventArgs e) => ClearRow("P1_BUTTON2");
        private void BtnClearP1Button3_Click(object sender, RoutedEventArgs e) => ClearRow("P1_BUTTON3");
        private void BtnClearP1Button4_Click(object sender, RoutedEventArgs e) => ClearRow("P1_BUTTON4");
        private void BtnClearUiCancel_Click(object sender, RoutedEventArgs e) => ClearRow("UI_CANCEL");

        private void BtnCaptureUiMenu_Click(object sender, RoutedEventArgs e) => StartCapture("UI_MENU");
        private void BtnClearUiMenu_Click(object sender, RoutedEventArgs e) => ClearRow("UI_MENU");

        private void BtnCaptureUiPause_Click(object sender, RoutedEventArgs e) => StartCapture("UI_PAUSE");
        private void BtnClearUiPause_Click(object sender, RoutedEventArgs e) => ClearRow("UI_PAUSE");

        private void BtnCaptureFullscreen_Click(object sender, RoutedEventArgs e) => StartCapture("TOGGLE_FULLSCREEN");
        private void BtnClearFullscreen_Click(object sender, RoutedEventArgs e) => ClearRow("TOGGLE_FULLSCREEN");

        private void BtnCaptureSnapshot_Click(object sender, RoutedEventArgs e) => StartCapture("RENDER_SNAP");
        private void BtnClearSnapshot_Click(object sender, RoutedEventArgs e) => ClearRow("RENDER_SNAP");

        
        // Stick 1 - dual-writes to P1_JOYSTICK_* (single-stick games) and P1_JOYSTICKLEFT_*
        // (twin-stick games). Accepts D-pad, either physical stick, or keyboard as alternates.
        private void BtnCaptureP1JoyUp_Click(object sender, RoutedEventArgs e) => StartCapture("STICK1_UP", acceptsSticks: true);
        private void BtnCaptureP1JoyDown_Click(object sender, RoutedEventArgs e) => StartCapture("STICK1_DOWN", acceptsSticks: true);
        private void BtnCaptureP1JoyLeft_Click(object sender, RoutedEventArgs e) => StartCapture("STICK1_LEFT", acceptsSticks: true);
        private void BtnCaptureP1JoyRight_Click(object sender, RoutedEventArgs e) => StartCapture("STICK1_RIGHT", acceptsSticks: true);
        private void BtnClearP1JoyUp_Click(object sender, RoutedEventArgs e) => ClearRow("STICK1_UP");
        private void BtnClearP1JoyDown_Click(object sender, RoutedEventArgs e) => ClearRow("STICK1_DOWN");
        private void BtnClearP1JoyLeft_Click(object sender, RoutedEventArgs e) => ClearRow("STICK1_LEFT");
        private void BtnClearP1JoyRight_Click(object sender, RoutedEventArgs e) => ClearRow("STICK1_RIGHT");

        // Stick 2 - twin-stick games only (P1_JOYSTICKRIGHT_*). Accepts D-pad, either physical
        // stick, or keyboard as alternates - same as Stick 1, no hardcoded side restriction.
        private void BtnCaptureP2JoyUp_Click(object sender, RoutedEventArgs e) => StartCapture("STICK2_UP", acceptsSticks: true);
        private void BtnCaptureP2JoyDown_Click(object sender, RoutedEventArgs e) => StartCapture("STICK2_DOWN", acceptsSticks: true);
        private void BtnCaptureP2JoyLeft_Click(object sender, RoutedEventArgs e) => StartCapture("STICK2_LEFT", acceptsSticks: true);
        private void BtnCaptureP2JoyRight_Click(object sender, RoutedEventArgs e) => StartCapture("STICK2_RIGHT", acceptsSticks: true);
        private void BtnClearP2JoyUp_Click(object sender, RoutedEventArgs e) => ClearRow("STICK2_UP");
        private void BtnClearP2JoyDown_Click(object sender, RoutedEventArgs e) => ClearRow("STICK2_DOWN");
        private void BtnClearP2JoyLeft_Click(object sender, RoutedEventArgs e) => ClearRow("STICK2_LEFT");
        private void BtnClearP2JoyRight_Click(object sender, RoutedEventArgs e) => ClearRow("STICK2_RIGHT");

        private void BtnCaptureP1Button5_Click(object sender, RoutedEventArgs e) => StartCapture("P1_BUTTON5");
        private void BtnCaptureP1Button6_Click(object sender, RoutedEventArgs e) => StartCapture("P1_BUTTON6");
        private void BtnClearP1Button5_Click(object sender, RoutedEventArgs e) => ClearRow("P1_BUTTON5");
        private void BtnClearP1Button6_Click(object sender, RoutedEventArgs e) => ClearRow("P1_BUTTON6");

        private void BtnCaptureCoin1_Click(object sender, RoutedEventArgs e) => StartCapture("COIN1");
        private void BtnClearCoin1_Click(object sender, RoutedEventArgs e) => ClearRow("COIN1");

        private void BtnCaptureStart1_Click(object sender, RoutedEventArgs e) => StartCapture("START1");
        private void BtnClearStart1_Click(object sender, RoutedEventArgs e) => ClearRow("START1");

        private void BtnCaptureRotaryAxis_Click(object sender, RoutedEventArgs e) => StartAnalogCapture("ROTARY_AXIS", isPedalRow: false, allowMouseX: true);
        private void BtnCaptureGasPedal_Click(object sender, RoutedEventArgs e) => StartAnalogCapture("GAS_PEDAL", isPedalRow: true);
        private void BtnCaptureBrakePedal_Click(object sender, RoutedEventArgs e) => StartAnalogCapture("BRAKE_PEDAL", isPedalRow: true);

        private void BtnClearRotaryAxis_Click(object sender, RoutedEventArgs e) => ClearRow("ROTARY_AXIS");
        private void BtnClearGasPedal_Click(object sender, RoutedEventArgs e) => ClearRow("GAS_PEDAL");
        private void BtnClearBrakePedal_Click(object sender, RoutedEventArgs e) => ClearRow("BRAKE_PEDAL");
        // [END SECTION: TEMP Keybind Test - Button Handlers]
    }
}