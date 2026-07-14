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

        public void SyncToSettings()
        {
            _settings.EnableGamepadPolling = ChkGamepad.IsChecked == true;

            if (int.TryParse(TxtDeviceID.Text, out int devId)) _settings.DirectInputDeviceId = devId;
            if (int.TryParse(TxtJoystickDeadzone.Text, out int deadzone)) _settings.JoystickDeadzonePercentage = deadzone;
            if (int.TryParse(TxtJoystickInitialDelay.Text, out int initDelay)) _settings.JoystickInitialDelayMs = initDelay;
            if (int.TryParse(TxtJoystickDelay.Text, out int repDelay)) _settings.JoystickRepeatDelayMs = repDelay;

            _settings.NavigationMode = (CboNavigationMode.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "D-Pad & Analog";
        }

        public void WireLiveDiagnostics(ArcadeStick.Services.WGIService inputService)
        {
            if (inputService == null) return;

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

            inputService.ActiveInputUpdated += (inputReadout) =>
            {
                this.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (TxtActiveInputReadout != null) TxtActiveInputReadout.Text = inputReadout;
                }));
            };
        }
    }
}