using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    // One entry of a Theme Builder weight combo: the OpenType weight, the label shown, and whether the family has a real
    // upright face at that weight (false = marked with * in the label). ToString is the label, so the combo shows it.
    public sealed record FontWeightChoice(int Weight, string Label, bool Installed)
    {
        public override string ToString() => Label;
    }

    // [SECTION: Font Weight Catalog]
    // The weights a font family really has, for the Theme Builder's weight combos (font-weight-pass-2.md, themes.md
    // decision 94). A weight is listed only when WPF resolves it to a real face of that family: upright, normal width,
    // with no bold or oblique simulation, at exactly that weight, and whose own family names include the requested name
    // (so "Segoe UI Black" lists only its Black face, and a family that is not installed lists nothing instead of the
    // fallback font's faces). WPF does not read OpenType font variations, so a variable font lists only the faces WPF
    // sees (its default instance). Results are cached per family name for the session (installed fonts don't change).
    public static class FontWeightCatalog
    {
        // The conventional names; any other weight shows as "Weight N"
        private static readonly Dictionary<int, string> WeightNames = new()
        {
            [100] = "Thin",
            [200] = "Extra Light",
            [300] = "Light",
            [350] = "Semilight",
            [400] = "Regular",
            [500] = "Medium",
            [600] = "Semibold",
            [700] = "Bold",
            [800] = "Extra Bold",
            [900] = "Black",
            [950] = "Extra Black"
        };

        // What a family that is not installed (or resolves to no usable face) offers: the two weights any font can be drawn
        // with, both marked as not installed
        private static readonly IReadOnlyList<int> NotInstalledWeights = new[] { LayerRegistry.RegularFontWeight, LayerRegistry.BoldFontWeight };

        public const string NotInstalledMark = "*";
        public const string NotInstalledTooltip =
            "* This weight is not an installed face of this font.\nWindows draws it with the nearest face or a simulated weight.";

        private static readonly Dictionary<string, IReadOnlyList<int>> Cache = new(StringComparer.OrdinalIgnoreCase);

        // Does the label for a weight: its conventional name, or "Weight N" for an uncommon number
        public static string GetLabel(int weight) => WeightNames.TryGetValue(weight, out string? name) ? name : $"Weight {weight}";

        // Returns the family's real upright, normal-width weights in ascending order (empty when none resolve)
        public static IReadOnlyList<int> GetInstalledWeights(string? family)
        {
            if (string.IsNullOrWhiteSpace(family)) return Array.Empty<int>();
            string name = family.Trim();
            if (Cache.TryGetValue(name, out var cached)) return cached;

            var weights = new SortedSet<int>();
            try
            {
                var fontFamily = new FontFamily(name);
                foreach (FamilyTypeface face in fontFamily.FamilyTypefaces)
                {
                    if (face.Style != FontStyles.Normal || face.Stretch != FontStretches.Normal) continue;

                    var typeface = new Typeface(fontFamily, FontStyles.Normal, face.Weight, FontStretches.Normal);
                    if (!typeface.TryGetGlyphTypeface(out GlyphTypeface glyph)) continue;
                    if (glyph.StyleSimulations != StyleSimulations.None) continue;
                    if (glyph.Weight != face.Weight || glyph.Style != FontStyles.Normal || glyph.Stretch != FontStretches.Normal) continue;
                    if (!HasFamilyName(glyph, name)) continue;

                    weights.Add(face.Weight.ToOpenTypeWeight());
                }
            }
            catch
            {
                weights.Clear();
            }

            IReadOnlyList<int> result = weights.ToList();
            Cache[name] = result;
            return result;
        }

        // Returns the weights a combo offers for the family: the installed ones, or Regular and Bold when none resolve
        public static IReadOnlyList<int> GetSelectableWeights(string? family)
        {
            var installed = GetInstalledWeights(family);
            return installed.Count > 0 ? installed : NotInstalledWeights;
        }

        // Returns the combo entries for the family. keepWeight (the saved weight on load) is added, marked, when the family
        // has no such face, so loading shows what is stored instead of silently changing it.
        public static List<FontWeightChoice> GetChoices(string? family, int? keepWeight)
        {
            bool installed = GetInstalledWeights(family).Count > 0;
            var choices = GetSelectableWeights(family)
                .Select(w => new FontWeightChoice(w, installed ? GetLabel(w) : GetLabel(w) + NotInstalledMark, installed))
                .ToList();

            if (keepWeight is int keep && !choices.Any(c => c.Weight == keep))
            {
                choices.Add(new FontWeightChoice(keep, GetLabel(keep) + NotInstalledMark, false));
                choices.Sort((a, b) => a.Weight.CompareTo(b.Weight));
            }
            return choices;
        }

        // Does the pick after a family change: the current weight when the family offers it, else the nearest offered weight
        // (on a tie, the lighter one)
        public static int PickWeight(string? family, int currentWeight)
        {
            var available = GetSelectableWeights(family);
            if (available.Contains(currentWeight)) return currentWeight;
            return available.OrderBy(w => Math.Abs(w - currentWeight)).ThenBy(w => w).First();
        }

        // Does the name check: true when the face's typographic or Win32 family names include the requested family
        private static bool HasFamilyName(GlyphTypeface glyph, string family) =>
            glyph.FamilyNames.Values.Any(n => string.Equals(n, family, StringComparison.OrdinalIgnoreCase))
            || glyph.Win32FamilyNames.Values.Any(n => string.Equals(n, family, StringComparison.OrdinalIgnoreCase));
    }
    // [END SECTION: Font Weight Catalog]
}
