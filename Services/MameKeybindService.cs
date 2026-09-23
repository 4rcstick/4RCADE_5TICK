// Services/MameKeybindService.cs
// Maps WGI GamepadButtons/keyboard Keys to MAME JOYCODE_1_*/KEYCODE_* strings and reads/writes
// minimal (no tag/mask/defvalue) entries into default.cfg for the MAME Keybinds tab.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Windows.Gaming.Input;

namespace ArcadeStick.Services
{
    public static class MameKeybindService
    {
        // Only the buttons WGIService currently tracks via GamepadButtonDownTriggered.
        // Order here is used as the stable ordering when joining combo sequences.
        private static readonly (GamepadButtons Flag, string MameCode)[] ButtonMap = new[]
        {
            (GamepadButtons.A, "JOYCODE_1_BUTTON1"),
            (GamepadButtons.B, "JOYCODE_1_BUTTON2"),
            (GamepadButtons.X, "JOYCODE_1_BUTTON3"),
            (GamepadButtons.Y, "JOYCODE_1_BUTTON4"),
            (GamepadButtons.LeftShoulder, "JOYCODE_1_BUTTON5"),
            (GamepadButtons.RightShoulder, "JOYCODE_1_BUTTON6"),
            (GamepadButtons.View, "JOYCODE_1_SELECT"),
            (GamepadButtons.Menu, "JOYCODE_1_START"),

            // D-pad - confirmed as HAT1 in real captured .cfg data tonight.
            (GamepadButtons.DPadUp, "JOYCODE_1_HAT1UP"),
            (GamepadButtons.DPadDown, "JOYCODE_1_HAT1DOWN"),
            (GamepadButtons.DPadLeft, "JOYCODE_1_HAT1LEFT"),
            (GamepadButtons.DPadRight, "JOYCODE_1_HAT1RIGHT"),
        };

        // Stick-as-digital-direction codes, confirmed from real captured .cfg data tonight (sf2.cfg
        // for left stick, sf2um.cfg for right stick). Note the naming asymmetry is real, not a typo -
        // left stick uses RIGHT/LEFT/UP/DOWN_SWITCH, right stick uses POS/NEG_SWITCH, and right
        // stick's vertical mapping is inverted (RZAXIS_POS = Down, RZAXIS_NEG = Up) exactly as captured.
        private static readonly Dictionary<(string Stick, string Direction), string> StickDirectionMap = new()
        {
            { ("Left", "Right"), "JOYCODE_1_XAXIS_RIGHT_SWITCH" },
            { ("Left", "Left"), "JOYCODE_1_XAXIS_LEFT_SWITCH" },
            { ("Left", "Up"), "JOYCODE_1_YAXIS_UP_SWITCH" },
            { ("Left", "Down"), "JOYCODE_1_YAXIS_DOWN_SWITCH" },
            { ("Right", "Right"), "JOYCODE_1_ZAXIS_POS_SWITCH" },
            { ("Right", "Left"), "JOYCODE_1_ZAXIS_NEG_SWITCH" },
            { ("Right", "Down"), "JOYCODE_1_RZAXIS_POS_SWITCH" },
            { ("Right", "Up"), "JOYCODE_1_RZAXIS_NEG_SWITCH" },
        };

        // Resolves a (stick, direction) pair from StickDirectionTriggered into its MAME code.
        public static string? GetStickDirectionCode(string stick, string direction)
        {
            return StickDirectionMap.TryGetValue((stick, direction), out var code) ? code : null;
        }

        // Starter keyboard lookup - not the full Phase 1 table yet, just enough common keys to
        // prove multi-slot keyboard+controller binding works end to end.
        private static readonly Dictionary<System.Windows.Input.Key, string> KeyMap = new()
        {
            { System.Windows.Input.Key.Escape, "KEYCODE_ESC" },
            { System.Windows.Input.Key.Enter, "KEYCODE_ENTER" },
            { System.Windows.Input.Key.Space, "KEYCODE_SPACE" },
            { System.Windows.Input.Key.Tab, "KEYCODE_TAB" },
            { System.Windows.Input.Key.LeftShift, "KEYCODE_LSHIFT" },
            { System.Windows.Input.Key.RightShift, "KEYCODE_RSHIFT" },
            { System.Windows.Input.Key.LeftCtrl, "KEYCODE_LCONTROL" },
            { System.Windows.Input.Key.RightCtrl, "KEYCODE_RCONTROL" },
            { System.Windows.Input.Key.LeftAlt, "KEYCODE_LALT" },
            { System.Windows.Input.Key.RightAlt, "KEYCODE_RALT" },
            { System.Windows.Input.Key.F1, "KEYCODE_F1" },
            { System.Windows.Input.Key.F2, "KEYCODE_F2" },
            { System.Windows.Input.Key.F3, "KEYCODE_F3" },
            { System.Windows.Input.Key.F4, "KEYCODE_F4" },
            { System.Windows.Input.Key.F5, "KEYCODE_F5" },
            { System.Windows.Input.Key.F6, "KEYCODE_F6" },
            { System.Windows.Input.Key.Up, "KEYCODE_UP" },
            { System.Windows.Input.Key.Down, "KEYCODE_DOWN" },
            { System.Windows.Input.Key.Left, "KEYCODE_LEFT" },
            { System.Windows.Input.Key.Right, "KEYCODE_RIGHT" },
        };

        // Confirmed via a real generated sf2.cfg (5-button mouse mapping): buttons are 1-indexed,
        // Button3 = middle click, Button4/5 = the two thumb/side buttons.
        private static readonly Dictionary<System.Windows.Input.MouseButton, string> MouseMap = new()
        {
            { System.Windows.Input.MouseButton.Left, "MOUSECODE_1_BUTTON1" },
            { System.Windows.Input.MouseButton.Right, "MOUSECODE_1_BUTTON2" },
            { System.Windows.Input.MouseButton.Middle, "MOUSECODE_1_BUTTON3" },
            { System.Windows.Input.MouseButton.XButton1, "MOUSECODE_1_BUTTON4" },
            { System.Windows.Input.MouseButton.XButton2, "MOUSECODE_1_BUTTON5" },
        };

        // Resolves a WPF MouseButton into its MAME MOUSECODE_ string, or null if unmapped.
        public static string? GetMameCode(System.Windows.Input.MouseButton button)
        {
            return MouseMap.TryGetValue(button, out var code) ? code : null;
        }

        // Resolves a WPF Key into its MAME KEYCODE_ string, or null if not in our starter map yet.
        public static string? GetMameCode(System.Windows.Input.Key key)
        {
            if (KeyMap.TryGetValue(key, out var code)) return code;

            // Fall back to letters/digits, which follow a predictable KEYCODE_<CHAR> pattern.
            string keyName = key.ToString();
            if (keyName.Length == 1 && char.IsLetterOrDigit(keyName[0]))
            {
                return $"KEYCODE_{keyName.ToUpperInvariant()}";
            }

            return null;
        }

        // Friendly display names for every MAME code string we currently know how to produce.
        // Not exhaustive - only covers codes reachable through this service's capture maps above.
        private static readonly Dictionary<string, string> FriendlyNames = new()
        {
            // Gamepad
            { "JOYCODE_1_BUTTON1", "Button A" },
            { "JOYCODE_1_BUTTON2", "Button B" },
            { "JOYCODE_1_BUTTON3", "Button X" },
            { "JOYCODE_1_BUTTON4", "Button Y" },
            { "JOYCODE_1_BUTTON5", "Left Bumper" },
            { "JOYCODE_1_BUTTON6", "Right Bumper" },
            { "JOYCODE_1_SELECT", "Back/Select" },
            { "JOYCODE_1_START", "Start" },
            { "JOYCODE_1_HAT1UP", "D-Pad Up" },
            { "JOYCODE_1_HAT1DOWN", "D-Pad Down" },
            { "JOYCODE_1_HAT1LEFT", "D-Pad Left" },
            { "JOYCODE_1_HAT1RIGHT", "D-Pad Right" },

            // Keyboard
            { "KEYCODE_ESC", "Esc" },
            { "KEYCODE_ENTER", "Enter" },
            { "KEYCODE_SPACE", "Space" },
            { "KEYCODE_TAB", "Tab" },
            { "KEYCODE_LSHIFT", "Left Shift" },
            { "KEYCODE_RSHIFT", "Right Shift" },
            { "KEYCODE_LCONTROL", "Left Ctrl" },
            { "KEYCODE_RCONTROL", "Right Ctrl" },
            { "KEYCODE_LALT", "Left Alt" },
            { "KEYCODE_RALT", "Right Alt" },
            { "KEYCODE_F1", "F1" },
            { "KEYCODE_F2", "F2" },
            { "KEYCODE_F3", "F3" },
            { "KEYCODE_F4", "F4" },
            { "KEYCODE_F5", "F5" },
            { "KEYCODE_F6", "F6" },
            { "KEYCODE_UP", "Up Arrow" },
            { "KEYCODE_DOWN", "Down Arrow" },
            { "KEYCODE_LEFT", "Left Arrow" },
            { "KEYCODE_RIGHT", "Right Arrow" },

            // Mouse
            { "MOUSECODE_1_BUTTON1", "Mouse Left" },
            { "MOUSECODE_1_BUTTON2", "Mouse Right" },
            { "MOUSECODE_1_BUTTON3", "Mouse Middle" },
            { "MOUSECODE_1_BUTTON4", "Mouse Side 1" },
            { "MOUSECODE_1_BUTTON5", "Mouse Side 2" },

            // Analog axes (Rotary Axis rows - raw)
            { "JOYCODE_1_XAXIS", "Left Stick X" },
            { "JOYCODE_1_YAXIS", "Left Stick Y" },
            { "JOYCODE_1_ZAXIS", "Right Stick X" },
            { "JOYCODE_1_RZAXIS", "Right Stick Y" },

            // Analog axes (Pedal rows - _NEG)
            { "JOYCODE_1_SLIDER1_NEG", "Left Trigger" },
            { "JOYCODE_1_SLIDER2_NEG", "Right Trigger" },

            // Mouse (Rotary Axis rows - raw)
            { "MOUSECODE_1_XAXIS", "Mouse X" },

            // Stick-as-digital-direction (confirmed real codes, see StickDirectionMap above)
            { "JOYCODE_1_XAXIS_RIGHT_SWITCH", "Left Stick Right" },
            { "JOYCODE_1_XAXIS_LEFT_SWITCH", "Left Stick Left" },
            { "JOYCODE_1_YAXIS_UP_SWITCH", "Left Stick Up" },
            { "JOYCODE_1_YAXIS_DOWN_SWITCH", "Left Stick Down" },
            { "JOYCODE_1_ZAXIS_POS_SWITCH", "Right Stick Right" },
            { "JOYCODE_1_ZAXIS_NEG_SWITCH", "Right Stick Left" },
            { "JOYCODE_1_RZAXIS_POS_SWITCH", "Right Stick Down" },
            { "JOYCODE_1_RZAXIS_NEG_SWITCH", "Right Stick Up" },
        };

        // Resolves a single MAME code into its friendly display name, falling back to the raw
        // code itself (letters/digits like KEYCODE_A aren't in the table but read fine as-is).
        public static string GetFriendlyName(string mameCode)
        {
            if (FriendlyNames.TryGetValue(mameCode, out var friendly)) return friendly;

            // Fallback for untabled single-letter/digit keys, e.g. "KEYCODE_A" -> "A"
            if (mameCode.StartsWith("KEYCODE_") && mameCode.Length == 9)
            {
                return mameCode.Substring(8);
            }

            return mameCode; // last resort - show the raw code rather than nothing
        }

        // Converts one slot (which may be a multi-code AND-combo, and/or contain NOT-exclusions
        // like MAME's real Show/Hide Menu default "KEYCODE_TAB NOT KEYCODE_LALT") into a friendly
        // display string. NOT is treated as a literal keyword, not a code to look up, e.g.
        // "KEYCODE_TAB NOT KEYCODE_LALT" -> "Tab (excluding Left Alt)".
        public static string GetFriendlySlotDisplay(string slot)
        {
            var tokens = slot.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var heldCodes = new List<string>();
            var excludedCodes = new List<string>();
            bool inExclusion = false;

            foreach (var token in tokens)
            {
                if (string.Equals(token, "NOT", StringComparison.OrdinalIgnoreCase))
                {
                    inExclusion = true;
                    continue;
                }

                if (inExclusion)
                    excludedCodes.Add(GetFriendlyName(token));
                else
                    heldCodes.Add(GetFriendlyName(token));
            }

            string display = string.Join(" + ", heldCodes);
            if (excludedCodes.Count > 0)
            {
                display += " (excluding " + string.Join(", ", excludedCodes) + ")";
            }

            return display;
        }

        // Splits a full newseq string (which may contain multiple OR'd slots, each possibly a
        // multi-code combo) into a list of individual slot strings, e.g.
        // "KEYCODE_ESC OR JOYCODE_1_SELECT JOYCODE_1_START" -> ["KEYCODE_ESC", "JOYCODE_1_SELECT JOYCODE_1_START"]
        public static List<string> ParseSlots(string? newseq)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(newseq)) return result;

            var parts = newseq.Split(new[] { " OR " }, StringSplitOptions.None);
            foreach (var part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0) result.Add(trimmed);
            }
            return result;
        }

        // Reads the current slot list for a given port type directly from default.cfg, or an
        // empty list if the file/port doesn't exist yet.
        public static List<string> ReadExistingSlots(string defaultCfgPath, string portType)
        {
            if (!File.Exists(defaultCfgPath)) return new List<string>();

            var doc = XDocument.Load(defaultCfgPath);
            var port = doc.Root?.Element("system")?.Element("input")?.Elements("port")
                .FirstOrDefault(p => (string?)p.Attribute("type") == portType);

            string? sequence = port?.Element("newseq")?.Value?.Trim();
            return ParseSlots(sequence);
        }

        // Returns the MAME code for a single flag, or null if it's not in our test map.
        public static string? GetMameCode(GamepadButtons flag)
        {
            foreach (var (Flag, MameCode) in ButtonMap)
            {
                if (Flag == flag) return MameCode;
            }
            return null;
        }

        // Builds a MAME newseq string from a set of simultaneously-held flags (a combo),
        // e.g. { View, Menu } -> "JOYCODE_1_SELECT JOYCODE_1_START"
        public static string BuildComboSequence(IEnumerable<GamepadButtons> flags)
        {
            return string.Join(" ", BuildComboCodes(flags));
        }

        // Same as above but returns the raw ordered code list instead of a pre-joined string,
        // so callers can merge it with other code sources (e.g. keyboard) before joining themselves.
        public static List<string> BuildComboCodes(IEnumerable<GamepadButtons> flags)
        {
            var codes = new List<string>();
            foreach (var (Flag, MameCode) in ButtonMap)
            {
                if (flags.Contains(Flag)) codes.Add(MameCode);
            }
            return codes;
        }

        // [SECTION: Analog Axis Mapping]
        // Physical axes reachable via WGIService.GetCurrentAnalogReading(). Left stick X is confirmed
        // via a real captured Arkanoid dial (JOYCODE_1_XAXIS); the rest follow the same naming
        // convention MAME uses (Y/Z/RZ per axis, SLIDER1/2 for triggers) and RT->SLIDER1, LT->SLIDER2
        // is confirmed via a real captured Crusin' USA pedal binding.
        public enum AnalogAxis { LeftStickX, LeftStickY, RightStickX, RightStickY, LeftTrigger, RightTrigger }

        private static readonly Dictionary<AnalogAxis, string> AxisRawCodeMap = new()
        {
            { AnalogAxis.LeftStickX, "JOYCODE_1_XAXIS" },
            { AnalogAxis.LeftStickY, "JOYCODE_1_YAXIS" },
            { AnalogAxis.RightStickX, "JOYCODE_1_ZAXIS" },
            { AnalogAxis.RightStickY, "JOYCODE_1_RZAXIS" },
            { AnalogAxis.LeftTrigger, "JOYCODE_1_SLIDER1" },
            { AnalogAxis.RightTrigger, "JOYCODE_1_SLIDER2" },
        };

        // Raw/no-suffix code - used for Rotary Axis rows (Dial/Paddle), confirmed correct via
        // Arkanoid's real captured dial binding.
        public static string GetRawAxisCode(AnalogAxis axis) => AxisRawCodeMap[axis];

        // Mouse X axis code for Rotary Axis capture - confirmed via Arkanoid's real captured dial
        // binding ("JOYCODE_1_XAXIS OR MOUSECODE_1_XAXIS"). Raw/no-suffix, same as the stick codes.
        public const string MouseXAxisCode = "MOUSECODE_1_XAXIS";

        // _NEG-suffixed code - used for Pedal rows, confirmed correct via two real captured
        // driving games (Crusin' USA, Pole Position) where only _NEG made the pedal register.
        public static string GetPedalAxisCode(AnalogAxis axis) => AxisRawCodeMap[axis] + "_NEG";

        // Compares two analog readings and returns whichever single axis moved the most, if any
        // moved past the given threshold. Used during capture to identify which physical
        // stick/trigger the user actually touched.
        public static AnalogAxis? DetectMovedAxis(
            (double LeftX, double LeftY, double RightX, double RightY, double LeftTrigger, double RightTrigger) baseline,
            (double LeftX, double LeftY, double RightX, double RightY, double LeftTrigger, double RightTrigger) current,
            double threshold = 0.5)
        {
            var deltas = new (AnalogAxis Axis, double Delta)[]
            {
                (AnalogAxis.LeftStickX, Math.Abs(current.LeftX - baseline.LeftX)),
                (AnalogAxis.LeftStickY, Math.Abs(current.LeftY - baseline.LeftY)),
                (AnalogAxis.RightStickX, Math.Abs(current.RightX - baseline.RightX)),
                (AnalogAxis.RightStickY, Math.Abs(current.RightY - baseline.RightY)),
                (AnalogAxis.LeftTrigger, Math.Abs(current.LeftTrigger - baseline.LeftTrigger)),
                (AnalogAxis.RightTrigger, Math.Abs(current.RightTrigger - baseline.RightTrigger)),
            };

            var best = deltas.OrderByDescending(d => d.Delta).First();
            return best.Delta >= threshold ? best.Axis : (AnalogAxis?)null;
        }
        // [END SECTION: Analog Axis Mapping]

        // Writes (or overwrites) a single <port type="..."> entry in default.cfg, joining the
        // given slot list with " OR ". Uses the minimal global shape confirmed from the reference
        // file (no tag/mask/defvalue). Creates a fresh minimal default.cfg if one doesn't exist yet.
        // Passing an empty slot list removes the port entry entirely (Clear All -> Apply -> no binding).
        public static void WriteBinding(string defaultCfgPath, string portType, List<string> slots)
        {
            XDocument doc;

            if (File.Exists(defaultCfgPath))
            {
                doc = XDocument.Load(defaultCfgPath);
            }
            else
            {
                doc = new XDocument(
                    new XComment(" This file is autogenerated; comments and unknown tags will be stripped "),
                    new XElement("mameconfig",
                        new XAttribute("version", "10"),
                        new XElement("system",
                            new XAttribute("name", "default"),
                            new XElement("input")
                        )
                    )
                );
            }

            // MAME can generate a default.cfg with a <system> element but no <input> child at all
            // if there's nothing to report yet (e.g. right after a fresh regenerate with no prior
            // customizations) - create the missing pieces rather than treating this as fatal.
            var systemElement = doc.Root?.Element("system");
            if (systemElement == null)
            {
                systemElement = new XElement("system", new XAttribute("name", "default"));
                doc.Root?.Add(systemElement);
            }

            var inputElement = systemElement.Element("input");
            if (inputElement == null)
            {
                inputElement = new XElement("input");
                systemElement.Add(inputElement);
            }

            // Look for an existing <port type="..."> entry to overwrite, or create a new one.
            var existingPort = inputElement.Elements("port")
                .FirstOrDefault(p => (string?)p.Attribute("type") == portType);

            if (existingPort != null)
            {
                existingPort.Remove();
            }

            // Empty slot list means the user cleared all bindings for this function - leave it
            // absent from the file entirely rather than writing an empty <newseq>.
            if (slots.Count > 0)
            {
                var newPort = new XElement("port",
                    new XAttribute("type", portType),
                    new XElement("newseq",
                        new XAttribute("type", "standard"),
                        string.Join(" OR ", slots)
                    )
                );

                inputElement.Add(newPort);
            }

            doc.Save(defaultCfgPath);
        }
    }
}