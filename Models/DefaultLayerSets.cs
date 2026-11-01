using System.IO;
using System.Reflection;
using System.Text.Json;

namespace ArcadeStick.Models
{
    // What the Restore Defaults pop-up can reset
    public enum RestoreDefaultsMode
    {
        LayoutOnly,
        Everything
    }

    // [SECTION: Default Layer Sets]
    // The layer values Restore Defaults applies: a 720p and a 1080p set, copied from the default theme's theme.json into
    // the embedded DefaultLayerSets.json. Only Restore Defaults reads them; the registry defaults still fill gaps on load.
    public static class DefaultLayerSets
    {
        private const string ResourceName = "DefaultLayerSets.json";
        private const string AssetPrefix = "assets/";
        private const int Set720Height = 720;
        private const int Set1080Height = 1080;

        private static readonly Lazy<List<ThemeLayout>> Sets = new(LoadSets);

        // Returns the set for the active layout's height (width is ignored): under 1080 the 720p set, otherwise the 1080p set.
        // Scaling the defaults for non-base resolutions can be added here later.
        public static ThemeLayout GetDefaultsForHeight(int height)
        {
            int setHeight = height < Set1080Height ? Set720Height : Set1080Height;
            return Sets.Value.First(set => set.Height == setHeight);
        }

        // Does the Layout only reset: position, size and layer order from the default, and always visible. The Main Window
        // Background is a fill layer, so it keeps the size of the layout it fills instead of the set's size.
        public static void ApplyLayout(LayerLayoutEntry entry, LayerLayoutEntry defaults, ThemeLayout activeLayout)
        {
            bool isFill = LayerRegistry.GetDefinition(entry.Id)?.IsPositionable == false;
            entry.X = defaults.X;
            entry.Y = defaults.Y;
            entry.Width = isFill ? activeLayout.Width : defaults.Width;
            entry.Height = isFill ? activeLayout.Height : defaults.Height;
            entry.Z = defaults.Z;
            entry.Visible = true;
        }

        // Does the rest of the Everything reset: name, file and every style field, on top of ApplyLayout
        public static void ApplyStyle(LayerLayoutEntry entry, LayerLayoutEntry defaults, ConfigurationSettings settings)
        {
            entry.Name = defaults.Name;
            entry.ImagePath = ResolveImagePath(defaults.ImagePath, settings);
            entry.CopyStyleFrom(defaults);
        }

        // Points a set's "assets/x.png" at the app's own 4rcade5tick_files/assets folder, relative to the base directory
        // like every other working ImagePath, so it follows a drive letter change and bundles normally on save
        private static string ResolveImagePath(string imagePath, ConfigurationSettings settings)
        {
            if (!imagePath.StartsWith(AssetPrefix, StringComparison.Ordinal)) return imagePath;

            string fileName = imagePath.Substring(AssetPrefix.Length);
            string fullPath = Path.Combine(settings.GetArcadeStickFilesPath(), "assets", fileName);
            return Path.GetRelativePath(settings.BaseDirectory, fullPath);
        }

        // Reads both sets from the embedded resource (once, on the first restore)
        private static List<ThemeLayout> LoadSets()
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"Missing embedded resource {ResourceName}");
            return JsonSerializer.Deserialize<List<ThemeLayout>>(stream)
                ?? throw new InvalidOperationException($"Empty embedded resource {ResourceName}");
        }
    }
    // [END SECTION: Default Layer Sets]
}
