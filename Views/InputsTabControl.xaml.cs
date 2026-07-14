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
        public void WireLiveDiagnostics(ArcadeStick.Services.WGIService inputService)
        {
            if (inputService == null) return;

            // Updates one port's status dot + label (green/connected vs gray/disconnected)
            inputService.PortStatusUpdated += (port, friendlyName, active) =>
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

            // Updates the live "currently pressed" input readout line
            inputService.ActiveInputUpdated += (inputReadout) =>
            {
                this.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (TxtActiveInputReadout != null) TxtActiveInputReadout.Text = inputReadout;
                }));
            };
        }
        // [END SECTION: Live Diagnostics Wiring]
    }
}