using System;
using System.Windows;
using System.Windows.Media;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    // The Global tab's six areas, used to snapshot, restore or reset one area at a time.
    [Flags]
    public enum GlobalArea
    {
        None = 0,
        Fonts = 1,
        Backgrounds = 2,
        Tabs = 4,
        Buttons = 8,
        Scrollbars = 16,
        ContextMenus = 32,
        All = Fonts | Backgrounds | Tabs | Buttons | Scrollbars | ContextMenus
    }

    // Publishes the theme-wide (Global tab) flat values on ConfigurationSettings as app-level resources, so
    // styles read them with {DynamicResource Global.<Area>.<Name>} and follow every change live, in any window.
    // App.xaml declares a default for every key (the ConfigurationSettings initializers), so a lookup never
    // fails before Apply first runs. Apply replaces resource instances (frozen) and never mutates them.
    public static class GlobalThemeResources
    {
        // [SECTION: Resource Keys]
        // Naming: Global.<Area>.<Name>. Colors are Brush, font sizes double, border sizes Thickness, radii CornerRadius.
        public const string FontHeaderSize = "Global.Font.Header.Size";
        public const string FontHeaderColor = "Global.Font.Header.Color";
        public const string FontSubHeaderSize = "Global.Font.SubHeader.Size";
        public const string FontSubHeaderColor = "Global.Font.SubHeader.Color";
        public const string FontStandardSize = "Global.Font.Standard.Size";
        public const string FontStandardColor = "Global.Font.Standard.Color";
        public const string FontSubTextSize = "Global.Font.SubText.Size";
        public const string FontSubTextColor = "Global.Font.SubText.Color";

        public const string OptionsBg = "Global.Options.Bg";
        public const string OptionsContentBg = "Global.Options.ContentBg";
        public const string OptionsSeparator = "Global.Options.Separator";
        public const string OptionsBorderSize = "Global.Options.BorderSize";
        public const string OptionsBorderColor = "Global.Options.BorderColor";
        public const string OptionsRadiusTop = "Global.Options.RadiusTop";
        public const string OptionsRadiusBottom = "Global.Options.RadiusBottom";

        public const string TabFontSize = "Global.Tab.FontSize";
        public const string TabText = "Global.Tab.Text";
        public const string TabBg = "Global.Tab.Bg";
        public const string TabTextHover = "Global.Tab.TextHover";
        public const string TabBgHover = "Global.Tab.BgHover";
        public const string TabTextActive = "Global.Tab.TextActive";
        public const string TabBgActive = "Global.Tab.BgActive";

        public const string ButtonFontSize = "Global.Button.FontSize";
        public const string ButtonText = "Global.Button.Text";
        public const string ButtonBg = "Global.Button.Bg";
        public const string ButtonTextHover = "Global.Button.TextHover";
        public const string ButtonBgHover = "Global.Button.BgHover";
        public const string ButtonBorderSize = "Global.Button.BorderSize";
        public const string ButtonBorderColor = "Global.Button.BorderColor";
        public const string ButtonRadius = "Global.Button.Radius";

        public const string ScrollThumb = "Global.Scroll.Thumb";
        public const string ScrollThumbHover = "Global.Scroll.ThumbHover";
        public const string ScrollThumbDrag = "Global.Scroll.ThumbDrag";
        public const string ScrollTrack = "Global.Scroll.Track";
        public const string ScrollTrackHover = "Global.Scroll.TrackHover";
        public const string ScrollWidth = "Global.Scroll.Width";
        public const string ScrollHitWidth = "Global.Scroll.HitWidth";

        public const string MenuText = "Global.Menu.Text";
        public const string MenuIcon = "Global.Menu.Icon";
        public const string MenuBg = "Global.Menu.Bg";
        public const string MenuTextHover = "Global.Menu.TextHover";
        public const string MenuBgHover = "Global.Menu.BgHover";
        public const string MenuBorderColor = "Global.Menu.BorderColor";
        // The scrollbar's grab area never gets narrower than this; a narrower Scrollbar Width is drawn inside it.
        private const double MinScrollHitWidth = 8;
        // [END SECTION: Resource Keys]

        // [SECTION: Apply]
        // Rebuilds every Global resource from the settings. Runs at startup, on every ThemeBindingsRefreshed and
        // after any live Global edit, Restore Defaults or close-revert.
        public static void Apply(ConfigurationSettings s)
        {
            if (s == null || Application.Current == null) return;
            var d = Defaults.Value;

            SetBrush(FontHeaderColor, s.HeaderColorHex, d.HeaderColorHex);
            SetValue(FontHeaderSize, (double)s.HeaderFontSize);
            SetBrush(FontSubHeaderColor, s.SubHeaderColorHex, d.SubHeaderColorHex);
            SetValue(FontSubHeaderSize, (double)s.SubHeaderFontSize);
            SetBrush(FontStandardColor, s.StandardColorHex, d.StandardColorHex);
            SetValue(FontStandardSize, (double)s.StandardFontSize);
            SetBrush(FontSubTextColor, s.SubTextColorHex, d.SubTextColorHex);
            SetValue(FontSubTextSize, (double)s.SubTextFontSize);

            SetBrush(OptionsBg, s.OptionsMenuBgColorHex, d.OptionsMenuBgColorHex);
            SetBrush(OptionsContentBg, s.OptionsBg, d.OptionsBg);
            SetBrush(OptionsSeparator, s.SeparatorColorHex, d.SeparatorColorHex);
            SetValue(OptionsBorderSize, new Thickness(s.OptionsMenuBorderSize));
            SetBrush(OptionsBorderColor, s.OptionsMenuBorderColorHex, d.OptionsMenuBorderColorHex);
            SetValue(OptionsRadiusTop, new CornerRadius(s.OptionsMenuBorderRadius, s.OptionsMenuBorderRadius, 0, 0));
            SetValue(OptionsRadiusBottom, new CornerRadius(0, 0, s.OptionsMenuBorderRadius, s.OptionsMenuBorderRadius));

            SetValue(TabFontSize, (double)s.TabFontSize);
            SetBrush(TabText, s.TabColorHex, d.TabColorHex);
            SetBrush(TabBg, s.TabBgColorHex, d.TabBgColorHex);
            SetBrush(TabTextHover, s.TabColorHoverHex, d.TabColorHoverHex);
            SetBrush(TabBgHover, s.TabBgColorHoverHex, d.TabBgColorHoverHex);
            SetBrush(TabTextActive, s.TabActiveColorHex, d.TabActiveColorHex);
            SetBrush(TabBgActive, s.TabActiveBgColorHex, d.TabActiveBgColorHex);

            SetValue(ButtonFontSize, (double)s.OptionsBtnFontSize);
            SetBrush(ButtonText, s.BtnTextColorNormalHex, d.BtnTextColorNormalHex);
            SetBrush(ButtonBg, s.BtnBgColorHex, d.BtnBgColorHex);
            SetBrush(ButtonTextHover, s.OptionsBtnColorHoverHex, d.OptionsBtnColorHoverHex);
            SetBrush(ButtonBgHover, s.BtnBgColorHoverHex, d.BtnBgColorHoverHex);
            SetValue(ButtonBorderSize, new Thickness(s.OptionsBtnBorderSize));
            SetBrush(ButtonBorderColor, s.BtnBorderColorHex, d.BtnBorderColorHex);
            SetValue(ButtonRadius, new CornerRadius(s.OptionsBtnBorderRadius));

            SetBrush(ScrollThumb, s.ScrollThumbColor, d.ScrollThumbColor);
            SetBrush(ScrollThumbHover, s.ScrollThumbHoverColor, d.ScrollThumbHoverColor);
            SetBrush(ScrollThumbDrag, s.ScrollThumbDragColor, d.ScrollThumbDragColor);
            SetBrush(ScrollTrack, s.ScrollTrackColor, d.ScrollTrackColor);
            SetBrush(ScrollTrackHover, s.ScrollTrackHoverColor, d.ScrollTrackHoverColor);
            SetValue(ScrollWidth, s.ScrollbarWidth);
            SetValue(ScrollHitWidth, Math.Max(s.ScrollbarWidth, MinScrollHitWidth));

            SetBrush(MenuText, s.ContextMenuFontColorHex, d.ContextMenuFontColorHex);
            SetBrush(MenuIcon, s.ContextMenuIconColorHex, d.ContextMenuIconColorHex);
            SetBrush(MenuBg, s.ContextMenuBgColorHex, d.ContextMenuBgColorHex);
            SetBrush(MenuTextHover, s.ContextMenuHoverColorHex, d.ContextMenuHoverColorHex);
            SetBrush(MenuBgHover, s.ContextMenuHoverBgColorHex, d.ContextMenuHoverBgColorHex);
            SetBrush(MenuBorderColor, s.ContextMenuBorderColorHex, d.ContextMenuBorderColorHex);
        }

        // Replaces a brush resource with a frozen brush of the given color, unless the current one already has that color.
        // A blank or unreadable value uses the property's default (not SafeConvertToBrush's gray), the same rule the Global tab shows.
        private static void SetBrush(string key, string colorHex, string defaultHex)
        {
            Color color = ParseColor(colorHex) ?? ParseColor(defaultHex) ?? Colors.Gray;
            if (Application.Current.Resources[key] is SolidColorBrush current && current.Color == color) return;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            Application.Current.Resources[key] = brush;
        }

        // Parses a stored color (ARGB or RGB hex with or without "#", or a named color); null when blank or unreadable
        public static Color? ParseColor(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string input = value.Trim();
            try { return (Color)ColorConverter.ConvertFromString(input); } catch { }
            try { return (Color)ColorConverter.ConvertFromString("#" + input); } catch { }
            return null;
        }

        // Replaces a value resource (double, Thickness, CornerRadius), unless the current one is already equal
        private static void SetValue(string key, object value)
        {
            if (Equals(Application.Current.Resources[key], value)) return;
            Application.Current.Resources[key] = value;
        }
        // [END SECTION: Apply]

        // [SECTION: Snapshot, Restore and Defaults]
        // Code defaults: a fresh ConfigurationSettings holds the property initializers, the one source of defaults.
        private static readonly Lazy<ConfigurationSettings> Defaults = new(() => new ConfigurationSettings());

        // The code defaults, for display fallbacks (the Global tab shows a property's default when its stored color is blank or unreadable)
        public static ConfigurationSettings DefaultSettings => Defaults.Value;

        // Copies every Global value into a ThemeSettings (a shallow snapshot; used as the Theme Builder's close-revert point)
        public static ThemeSettings CaptureSnapshot(ConfigurationSettings s)
        {
            return new ThemeSettings
            {
                HeaderFontSize = s.HeaderFontSize,
                HeaderColorHex = s.HeaderColorHex,
                SubHeaderFontSize = s.SubHeaderFontSize,
                SubHeaderColorHex = s.SubHeaderColorHex,
                StandardFontSize = s.StandardFontSize,
                StandardColorHex = s.StandardColorHex,
                SubTextFontSize = s.SubTextFontSize,
                SubTextColorHex = s.SubTextColorHex,

                OptionsMenuBgColorHex = s.OptionsMenuBgColorHex,
                OptionsBg = s.OptionsBg,
                SeparatorColorHex = s.SeparatorColorHex,
                OptionsMenuBorderSize = s.OptionsMenuBorderSize,
                OptionsMenuBorderColorHex = s.OptionsMenuBorderColorHex,
                OptionsMenuBorderRadius = s.OptionsMenuBorderRadius,

                TabFontSize = s.TabFontSize,
                TabColorHex = s.TabColorHex,
                TabBgColorHex = s.TabBgColorHex,
                TabColorHoverHex = s.TabColorHoverHex,
                TabBgColorHoverHex = s.TabBgColorHoverHex,
                TabActiveColorHex = s.TabActiveColorHex,
                TabActiveBgColorHex = s.TabActiveBgColorHex,

                OptionsBtnFontSize = s.OptionsBtnFontSize,
                BtnTextColorNormalHex = s.BtnTextColorNormalHex,
                BtnBgColorHex = s.BtnBgColorHex,
                OptionsBtnColorHoverHex = s.OptionsBtnColorHoverHex,
                BtnBgColorHoverHex = s.BtnBgColorHoverHex,
                OptionsBtnBorderSize = s.OptionsBtnBorderSize,
                BtnBorderColorHex = s.BtnBorderColorHex,
                OptionsBtnBorderRadius = s.OptionsBtnBorderRadius,

                ScrollThumbColor = s.ScrollThumbColor,
                ScrollThumbHoverColor = s.ScrollThumbHoverColor,
                ScrollThumbDragColor = s.ScrollThumbDragColor,
                ScrollTrackColor = s.ScrollTrackColor,
                ScrollTrackHoverColor = s.ScrollTrackHoverColor,
                ScrollbarWidth = s.ScrollbarWidth,

                ContextMenuFontColorHex = s.ContextMenuFontColorHex,
                ContextMenuIconColorHex = s.ContextMenuIconColorHex,
                ContextMenuBgColorHex = s.ContextMenuBgColorHex,
                ContextMenuHoverColorHex = s.ContextMenuHoverColorHex,
                ContextMenuHoverBgColorHex = s.ContextMenuHoverBgColorHex,
                ContextMenuBorderColorHex = s.ContextMenuBorderColorHex
            };
        }

        // Writes the chosen areas of a snapshot back into the settings (does not apply; call Apply afterwards)
        public static void RestoreSnapshot(ThemeSettings snapshot, ConfigurationSettings s, GlobalArea areas)
        {
            if (snapshot == null || s == null) return;

            if (areas.HasFlag(GlobalArea.Fonts))
            {
                s.HeaderFontSize = snapshot.HeaderFontSize;
                s.HeaderColorHex = snapshot.HeaderColorHex;
                s.SubHeaderFontSize = snapshot.SubHeaderFontSize;
                s.SubHeaderColorHex = snapshot.SubHeaderColorHex;
                s.StandardFontSize = snapshot.StandardFontSize;
                s.StandardColorHex = snapshot.StandardColorHex;
                s.SubTextFontSize = snapshot.SubTextFontSize;
                s.SubTextColorHex = snapshot.SubTextColorHex;
            }

            if (areas.HasFlag(GlobalArea.Backgrounds))
            {
                s.OptionsMenuBgColorHex = snapshot.OptionsMenuBgColorHex;
                s.OptionsBg = snapshot.OptionsBg;
                s.SeparatorColorHex = snapshot.SeparatorColorHex;
                s.OptionsMenuBorderSize = snapshot.OptionsMenuBorderSize;
                s.OptionsMenuBorderColorHex = snapshot.OptionsMenuBorderColorHex;
                s.OptionsMenuBorderRadius = snapshot.OptionsMenuBorderRadius;
            }

            if (areas.HasFlag(GlobalArea.Tabs))
            {
                s.TabFontSize = snapshot.TabFontSize;
                s.TabColorHex = snapshot.TabColorHex;
                s.TabBgColorHex = snapshot.TabBgColorHex;
                s.TabColorHoverHex = snapshot.TabColorHoverHex;
                s.TabBgColorHoverHex = snapshot.TabBgColorHoverHex;
                s.TabActiveColorHex = snapshot.TabActiveColorHex;
                s.TabActiveBgColorHex = snapshot.TabActiveBgColorHex;
            }

            if (areas.HasFlag(GlobalArea.Buttons))
            {
                s.OptionsBtnFontSize = snapshot.OptionsBtnFontSize;
                s.BtnTextColorNormalHex = snapshot.BtnTextColorNormalHex;
                s.BtnBgColorHex = snapshot.BtnBgColorHex;
                s.OptionsBtnColorHoverHex = snapshot.OptionsBtnColorHoverHex;
                s.BtnBgColorHoverHex = snapshot.BtnBgColorHoverHex;
                s.OptionsBtnBorderSize = snapshot.OptionsBtnBorderSize;
                s.BtnBorderColorHex = snapshot.BtnBorderColorHex;
                s.OptionsBtnBorderRadius = snapshot.OptionsBtnBorderRadius;
            }

            if (areas.HasFlag(GlobalArea.Scrollbars))
            {
                s.ScrollThumbColor = snapshot.ScrollThumbColor;
                s.ScrollThumbHoverColor = snapshot.ScrollThumbHoverColor;
                s.ScrollThumbDragColor = snapshot.ScrollThumbDragColor;
                s.ScrollTrackColor = snapshot.ScrollTrackColor;
                s.ScrollTrackHoverColor = snapshot.ScrollTrackHoverColor;
                s.ScrollbarWidth = snapshot.ScrollbarWidth;
            }

            if (areas.HasFlag(GlobalArea.ContextMenus))
            {
                s.ContextMenuFontColorHex = snapshot.ContextMenuFontColorHex;
                s.ContextMenuIconColorHex = snapshot.ContextMenuIconColorHex;
                s.ContextMenuBgColorHex = snapshot.ContextMenuBgColorHex;
                s.ContextMenuHoverColorHex = snapshot.ContextMenuHoverColorHex;
                s.ContextMenuHoverBgColorHex = snapshot.ContextMenuHoverBgColorHex;
                s.ContextMenuBorderColorHex = snapshot.ContextMenuBorderColorHex;
            }
        }

        // Resets the chosen areas to the ConfigurationSettings initializers and applies them live (the subtabs' Restore Defaults)
        public static void RestoreDefaults(ConfigurationSettings s, GlobalArea areas)
        {
            RestoreSnapshot(CaptureSnapshot(Defaults.Value), s, areas);
            Apply(s);
        }
        // [END SECTION: Snapshot, Restore and Defaults]
    }
}
