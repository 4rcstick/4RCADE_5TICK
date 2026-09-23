using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcadeStick.Views
{
    public partial class InputsTabControl : UserControl
    {
        private ArcadeStick.Models.ConfigurationSettings _settings;

        public InputsTabControl()
        {
            InitializeComponent();
        }

        // [SECTION: Load / Save Sync]
        // Load side: populates gamepad settings fields from ConfigurationSettings, including mapping the
        // NavigationMode string to the correct ComboBox index.
        public void Initialize(ArcadeStick.Models.ConfigurationSettings settings)
        {
            _settings = settings;

            ChkGamepad.IsChecked = _settings.EnableGamepadPolling;
            TxtDeviceID.Text = _settings.DirectInputDeviceId.ToString();
            TxtJoystickDeadzone.Text = _settings.JoystickDeadzonePercentage.ToString();
            TxtJoystickInitialDelay.Text = _settings.JoystickInitialDelayMs.ToString();
            TxtJoystickDelay.Text = _settings.JoystickRepeatDelayMs.ToString();

            CboNavigationMode.SelectedIndex = _settings.NavigationMode switch
            {
                "D-Pad Only" => 1,
                "Analog Only" => 2,
                _ => 0
            };
        }

        // Save side: writes gamepad settings fields back into ConfigurationSettings. Numeric fields are
        // parsed defensively - an invalid/empty TextBox value simply leaves the existing setting unchanged.
        public void SyncToSettings()
        {
            _settings.EnableGamepadPolling = ChkGamepad.IsChecked == true;

            if (int.TryParse(TxtDeviceID.Text, out int devId)) _settings.DirectInputDeviceId = devId;
            if (int.TryParse(TxtJoystickDeadzone.Text, out int deadzone)) _settings.JoystickDeadzonePercentage = deadzone;
            if (int.TryParse(TxtJoystickInitialDelay.Text, out int initDelay)) _settings.JoystickInitialDelayMs = initDelay;
            if (int.TryParse(TxtJoystickDelay.Text, out int repDelay)) _settings.JoystickRepeatDelayMs = repDelay;

            _settings.NavigationMode = (CboNavigationMode.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "D-Pad & Analog";
        }
        // [END SECTION: Load / Save Sync]

        // [SECTION: Live Diagnostics Wiring]
        // Subscribes to WGIService's PortStatusUpdated and ActiveInputUpdated events to drive the
        // diagnostics panel in real time. Called by MainWindow.OpenOptionsWindow when this tab's parent
        // Options window is opened. All UI updates are marshaled back to the UI thread via Dispatcher.
        //
        // Handlers are stored in named fields (not inline lambdas) so UnwireLiveDiagnostics can actually
        // remove them later - a brand new InputsTabControl is created every time the Options window opens,
        // but WGIService itself lives for the entire app session, so an un-removed subscription here keeps
        // this whole control (and everything above it in the Options window's visual tree) alive forever,
        // once per open/close cycle. OptionsWindow's Closed handler calls UnwireLiveDiagnostics to prevent
        // exactly that.
        private ArcadeStick.Services.WGIService? _wiredInputService;
        private Action<int, string, bool>? _portStatusHandler;
        private Action<string>? _activeInputHandler;

        public void WireLiveDiagnostics(ArcadeStick.Services.WGIService inputService)
        {
            if (inputService == null) return;

            _wiredInputService = inputService;

            // Updates one port's status dot + label (green/connected vs gray/disconnected)
            _portStatusHandler = (port, friendlyName, active) =>
            {
                this.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    TextBlock? targetBlock = port switch { 0 => TxtPort0Status, 1 => TxtPort1Status, 2 => TxtPort2Status, 3 => TxtPort3Status, _ => null };
                    System.Windows.Shapes.Ellipse? targetDot = port switch { 0 => DotPort0, 1 => DotPort1, 2 => DotPort2, 3 => DotPort3, _ => null };

                    if (targetBlock != null && targetDot != null)
                    {
                        if (active)
                        {
                            targetBlock.Text = $"[{friendlyName}]";
                            targetBlock.Foreground = Brushes.LimeGreen;
                            targetDot.Fill = Brushes.LimeGreen;
                        }
                        else
                        {
                            targetBlock.Text = "No device detected";
                            var grayBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0A0A2"));
                            targetBlock.Foreground = grayBrush;
                            targetDot.Fill = grayBrush;
                        }
                    }
                }));
            };
            inputService.PortStatusUpdated += _portStatusHandler;

            // Updates the live "currently pressed" input readout line
            _activeInputHandler = (inputReadout) =>
            {
                this.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (TxtActiveInputReadout != null) TxtActiveInputReadout.Text = inputReadout;
                }));
            };
            inputService.ActiveInputUpdated += _activeInputHandler;
        }

        // Reverses WireLiveDiagnostics - called by OptionsWindow.Closed so every open/close cycle
        // properly releases its subscriptions instead of accumulating on the long-lived WGIService.
        public void UnwireLiveDiagnostics()
        {
            if (_wiredInputService == null) return;

            if (_portStatusHandler != null)
            {
                _wiredInputService.PortStatusUpdated -= _portStatusHandler;
                _portStatusHandler = null;
            }

            if (_activeInputHandler != null)
            {
                _wiredInputService.ActiveInputUpdated -= _activeInputHandler;
                _activeInputHandler = null;
            }

            _wiredInputService = null;
        }
        // [END SECTION: Live Diagnostics Wiring]
    }
}