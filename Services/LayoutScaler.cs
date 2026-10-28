using System;
using System.Collections.Generic;
using System.Globalization;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    // The new size scaler's factors: Fx = newW / oldW, Fy = newH / oldH, U = min(Fx, Fy) (fonts and borders)
    public readonly record struct LayoutScaleFactors(double Fx, double Fy, double U);

    // [SECTION: New Size Layout Scaler]
    // Pure calculation, no UI dependencies (new-size-scaler.md, decision 65). When a new layout size is created from the
    // layout being edited, the copy can be scaled: A scales X / Y and Width / Height by Fx / Fy on every positionable layer,
    // and font and border sizes by U (on a different aspect ratio the Search & Buttons group only moves, as with B); B scales
    // X / Y only; C changes nothing. Rounding is half away from zero, font and border sizes are clamped to their editor
    // ranges, nulls stay null.
    // Works on the new copy only; the background (not positionable) is skipped, since the copy already resized it to fit.
    public static class LayoutScaler
    {
        public const string ChoiceScaleAll = "A";
        public const string ChoicePositionsOnly = "B";
        public const string ChoiceKeep = "C";

        // A new size whose aspect ratio is more than this far from the old one preselects ChoiceKeep in the prompt
        public const double FarOffRatioLimit = 0.15;

        // Does the choice check: true for "A", "B" or "C"
        public static bool IsChoice(string? choice) => choice == ChoiceScaleAll || choice == ChoicePositionsOnly || choice == ChoiceKeep;

        // Does the factor calculation for a copy from oldW x oldH to newW x newH
        public static LayoutScaleFactors GetFactors(int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            double fx = (double)newWidth / oldWidth;
            double fy = (double)newHeight / oldHeight;
            return new LayoutScaleFactors(fx, fy, Math.Min(fx, fy));
        }

        // Does the far-off ratio test: true when abs((newW / newH) / (oldW / oldH) - 1) is over FarOffRatioLimit
        public static bool IsFarOffRatio(int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            double oldRatio = (double)oldWidth / oldHeight;
            double newRatio = (double)newWidth / newHeight;
            return Math.Abs(newRatio / oldRatio - 1) > FarOffRatioLimit;
        }

        // Formats a factor for the prompt: three decimals at most, trailing zeros trimmed down to one decimal (1.5, 1.067, 1.0)
        public static string FormatFactor(double factor) => factor.ToString("0.0##", CultureInfo.InvariantCulture);

        // Does the aspect ratio mismatch test shared by the prompt's warning line and the scaler: true when the two factors
        // differ as shown (1366x768 from 1280x720 is x1.067 both ways, so it counts as matching)
        public static bool FactorsDiffer(LayoutScaleFactors factors) => FormatFactor(factors.Fx) != FormatFactor(factors.Fy);

        // Does the scaling of the copied entries in place for the choice; ChoiceKeep, an unknown choice or a bad size does nothing
        public static void Scale(IList<LayerLayoutEntry> layers, int oldWidth, int oldHeight, int newWidth, int newHeight, string? choice)
        {
            if (choice != ChoiceScaleAll && choice != ChoicePositionsOnly) return;
            if (oldWidth < 1 || oldHeight < 1 || newWidth < 1 || newHeight < 1) return;

            var f = GetFactors(oldWidth, oldHeight, newWidth, newHeight);
            bool scaleAll = choice == ChoiceScaleAll;
            bool mismatch = FactorsDiffer(f);

            foreach (var entry in layers)
            {
                // The background is not positionable; the copy already set it to the new size
                if (LayerRegistry.GetDefinition(entry.Id)?.IsPositionable == false) continue;

                entry.X = RoundAway(entry.X * f.Fx);
                entry.Y = RoundAway(entry.Y * f.Fy);
                if (!scaleAll) continue;

                // On a different aspect ratio some layers (the Search & Buttons group) only move, as with "positions only"
                if (mismatch && LayerRegistry.IsPositionOnlyOnMismatch(entry.Id)) continue;

                entry.Width = RoundAway(entry.Width * f.Fx);
                entry.Height = RoundAway(entry.Height * f.Fy);

                ScaleStyleSizes(entry, f.U);
            }
        }

        // Does the font and border sizes of one entry by u, each clamped to its field's editor range. The Marquee entry's
        // TextFontSize is the no-art banner's size in the 400x100 reference box, which already follows the rect, so it stays.
        private static void ScaleStyleSizes(LayerLayoutEntry entry, double u)
        {
            entry.FolderFontSize = ScaleSize(entry.FolderFontSize, u, LayerRegistry.MinTextFontSize, LayerRegistry.MaxTextFontSize);
            entry.GameFontSize = ScaleSize(entry.GameFontSize, u, LayerRegistry.MinTextFontSize, LayerRegistry.MaxTextFontSize);
            entry.SectionHeaderFontSize = ScaleSize(entry.SectionHeaderFontSize, u, LayerRegistry.MinTextFontSize, LayerRegistry.MaxTextFontSize);

            if (entry.Id == LayerRegistry.GameInfoNav || entry.Id == LayerRegistry.ImagePreviewsNav)
                entry.TextFontSize = ScaleSize(entry.TextFontSize, u, LayerRegistry.MinNavSize, LayerRegistry.MaxNavSize);
            else if (entry.Id == LayerRegistry.RandomGameDisplay || entry.Id == LayerRegistry.RandomGameDisplaySplash)
                entry.TextFontSize = ScaleSize(entry.TextFontSize, u, LayerRegistry.MinRandomDisplayFontSize, LayerRegistry.MaxRandomDisplayFontSize);
            else if (entry.Id != LayerRegistry.MarqueePanel)
                entry.TextFontSize = ScaleSize(entry.TextFontSize, u, LayerRegistry.MinTextFontSize, LayerRegistry.MaxTextFontSize);

            entry.BorderSize = ScaleSize(entry.BorderSize, u, LayerRegistry.MinBorderSize, LayerRegistry.MaxBorderSize);
        }

        // Scales one whole-number size, rounds half away from zero and clamps it; null stays null
        private static int? ScaleSize(int? value, double factor, int min, int max)
        {
            if (value is not int size) return null;
            return Math.Clamp((int)RoundAway(size * factor), min, max);
        }

        // Rounds to a whole number, half away from zero (17 x 1.5 = 25.5 becomes 26), never half to even
        private static double RoundAway(double value) => Math.Round(value, MidpointRounding.AwayFromZero);
    }
    // [END SECTION: New Size Layout Scaler]
}
