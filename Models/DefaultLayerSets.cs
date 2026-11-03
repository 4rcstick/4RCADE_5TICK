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

        // Does the rest of the Everything reset: name, file and every style field, on top of ApplyLayout. A default image
        // is found by name in the assets folder (ResolveImagePath); a missing file or extra matches add a line to problems.
        public static void ApplyStyle(LayerLayoutEntry entry, LayerLayoutEntry defaults, ConfigurationSettings settings, List<string> problems)
        {
            entry.Name = defaults.Name;
            entry.ImagePath = ResolveImagePath(entry.Id, defaults.ImagePath, settings, problems);
            entry.CopyStyleFrom(defaults);
        }

        // [SECTION: Default Image Lookup]
        // The file types each layer can show (its Browse filter and display code), listed in the fixed priority order used
        // when more than one file has the default's name. A layer not listed here accepts every type in PriorityOrder.
        private static readonly string[] PriorityOrder = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".mp4" };
        private static readonly Dictionary<string, string[]> LayerExtensions = new()
        {
            [LayerRegistry.MainBackground] = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".mp4" },
            [LayerRegistry.MarqueePanel] = new[] { ".png", ".jpg", ".jpeg" },
            [LayerRegistry.SplashImage] = new[] { ".png", ".jpg", ".jpeg", ".gif", ".mp4" },
            [LayerRegistry.VideoPreview] = new[] { ".png", ".jpg", ".jpeg", ".bmp" }
        };

        // Returns the app's own 4rcade5tick_files/assets folder, where the default images live
        public static string GetAssetsFolder(ConfigurationSettings settings) => Path.Combine(settings.GetArcadeStickFilesPath(), "assets");

        // Finds a set's "assets/x.ext" default by its name x alone (the extension in the set is only a hint and is ignored):
        // the file in the assets folder named exactly x with a type the layer can show, returned relative to the base
        // directory like every other working ImagePath, so it follows a drive letter change and bundles normally on save.
        // Empty when nothing matches. Paths not starting with "assets/" (including empty) are returned unchanged.
        private static string ResolveImagePath(string layerId, string imagePath, ConfigurationSettings settings, List<string> problems)
        {
            if (!imagePath.StartsWith(AssetPrefix, StringComparison.Ordinal)) return imagePath;

            string defaultName = Path.GetFileNameWithoutExtension(imagePath.Substring(AssetPrefix.Length));
            string[] extensions = LayerExtensions.TryGetValue(layerId, out var layerList) ? layerList : PriorityOrder;
            string layerName = LayerRegistry.GetDefinition(layerId)?.DisplayName ?? layerId;

            List<string> named = FindFilesNamed(GetAssetsFolder(settings), defaultName);
            List<string> usable = named
                .Where(file => IndexOfExtension(extensions, file) >= 0)
                .OrderBy(file => IndexOfExtension(extensions, file))
                .ToList();
            List<string> unsupported = named.Except(usable).Select(Path.GetFileName).Select(name => name!).ToList();

            if (usable.Count == 0)
            {
                string line = $"{layerName}: no file named {defaultName} found in the assets folder (looked for {string.Join(", ", extensions.Select(e => e.TrimStart('.')))}). The image was left empty.";
                if (unsupported.Count > 0) line += $" Ignored, unsupported type: {string.Join(", ", unsupported)}.";
                problems.Add(line);
                return "";
            }

            if (usable.Count > 1)
            {
                problems.Add($"{layerName}: more than one file named {defaultName}. Used {Path.GetFileName(usable[0])}; not used: {string.Join(", ", usable.Skip(1).Select(Path.GetFileName))}.");
            }

            return Path.GetRelativePath(settings.BaseDirectory, usable[0]);
        }

        // Returns where file's extension sits in extensions (case-insensitive), or -1 when the layer cannot show it
        private static int IndexOfExtension(string[] extensions, string file)
        {
            string ext = Path.GetExtension(file);
            return Array.FindIndex(extensions, e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }

        // Lists the files in folder whose name without extension is exactly name (case-insensitive); none when the folder is
        // missing or cannot be read. "x2.gif", "x_old.png" and "x.png.bak" do not match "x".
        private static List<string> FindFilesNamed(string folder, string name)
        {
            try
            {
                if (!Directory.Exists(folder)) return new List<string>();
                return Directory.EnumerateFiles(folder)
                    .Where(file => Path.GetFileNameWithoutExtension(file).Equals(name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch { return new List<string>(); }
        }
        // [END SECTION: Default Image Lookup]

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
