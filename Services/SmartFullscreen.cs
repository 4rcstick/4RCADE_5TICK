using System;
using System.Collections.Generic;

namespace ArcadeStick.Services
{
    // The outcome of the Smart fullscreen size test. Fits is false when no layout fits the screen at 1x or more
    // (fullscreen then behaves as before). Scale is the upscale (layout units to physical pixels): a whole number n, or a
    // fractional s when FromFallback is true. Centered is true when the scaled layout does not cover the screen (black
    // bars), and TransformFactor is the WPF ScaleTransform value, Scale / dpiScale, which cancels the Windows scale setting.
    public readonly record struct SmartFullscreenResult(bool Fits, int LayoutWidth, int LayoutHeight, double Scale, bool Centered, double TransformFactor, bool FromFallback = false)
    {
        public static SmartFullscreenResult NoFit => new(false, 0, 0, 0, false, 1);
    }

    // [SECTION: Smart Fullscreen Size Test]
    // Pure calculation, no UI dependencies (smart-fullscreen-pass1.md, decision 1). Whole-number pass first: for each layout
    // size w x h the theme has, n = floor(min(screenW / w, screenH / h)) in physical pixels; sizes with n < 1 are skipped.
    // The size with the largest covered area (w*n by h*n) wins; on a tie the larger layout wins (4K: 1080 at 2x beats 720
    // at 3x). Fractional fallback (2026-10-28): only when that pick leaves bars and allowFractional is true, the same sizes
    // at s = min(screenW / w, screenH / h), scale-up only (s >= 1), largest covered area by rounded whole-pixel areas, same
    // tie rule; a pick within 1 physical pixel of covering the screen is snapped to cover it (the window clips the overflow).
    public static class SmartFullscreen
    {
        // Largest overflow (physical pixels, per axis) the seam snap accepts to cover the whole screen
        private const double SeamTolerancePx = 1.0;

        // Does the size test: picks the layout and whole-number scale that cover the most of the screen, then, when that
        // leaves bars and allowFractional is true, the fractional fallback
        public static SmartFullscreenResult Choose(int screenWidthPx, int screenHeightPx, double dpiScale, IEnumerable<(int Width, int Height)> layoutSizes, bool allowFractional = true)
        {
            if (screenWidthPx < 1 || screenHeightPx < 1 || layoutSizes == null) return SmartFullscreenResult.NoFit;
            if (!(dpiScale > 0) || double.IsInfinity(dpiScale)) dpiScale = 1;

            var sizes = new List<(int Width, int Height)>();
            foreach (var size in layoutSizes)
                if (size.Width >= 1 && size.Height >= 1) sizes.Add(size);

            int bestWidth = 0, bestHeight = 0, bestScale = 0;
            long bestCovered = 0;

            foreach (var (width, height) in sizes)
            {
                int scale = Math.Min(screenWidthPx / width, screenHeightPx / height);
                if (scale < 1) continue;

                long covered = (long)width * scale * height * scale;
                bool larger = (long)width * height > (long)bestWidth * bestHeight;
                if (covered > bestCovered || (covered == bestCovered && larger))
                {
                    bestWidth = width;
                    bestHeight = height;
                    bestScale = scale;
                    bestCovered = covered;
                }
            }

            if (bestScale < 1) return SmartFullscreenResult.NoFit;

            bool centered = bestWidth * bestScale != screenWidthPx || bestHeight * bestScale != screenHeightPx;
            var whole = new SmartFullscreenResult(true, bestWidth, bestHeight, bestScale, centered, bestScale / dpiScale);
            if (!centered || !allowFractional) return whole;

            // s >= 1 exactly when n >= 1, so the fallback always finds a fit here; it counts only if it changed something
            var fallback = ChooseFractional(screenWidthPx, screenHeightPx, dpiScale, sizes);
            bool changed = fallback.LayoutWidth != whole.LayoutWidth || fallback.LayoutHeight != whole.LayoutHeight || fallback.Scale != whole.Scale;
            return fallback.Fits && changed ? fallback : whole;
        }

        // Does the fractional fallback: picks the layout and scale-up factor s that cover the most of the screen (rounded
        // whole-pixel areas, ties to the larger layout), then snaps s up to cover the screen when that overflows by at most
        // SeamTolerancePx on each axis, so a layout that almost spans the screen leaves no sub-pixel seam
        private static SmartFullscreenResult ChooseFractional(int screenWidthPx, int screenHeightPx, double dpiScale, List<(int Width, int Height)> sizes)
        {
            int bestWidth = 0, bestHeight = 0;
            double bestScale = 0;
            long bestCovered = 0;

            foreach (var (width, height) in sizes)
            {
                double scale = Math.Min((double)screenWidthPx / width, (double)screenHeightPx / height);
                if (scale < 1) continue;

                long covered = (long)Math.Round(width * scale) * (long)Math.Round(height * scale);
                bool larger = (long)width * height > (long)bestWidth * bestHeight;
                if (covered > bestCovered || (covered == bestCovered && larger))
                {
                    bestWidth = width;
                    bestHeight = height;
                    bestScale = scale;
                    bestCovered = covered;
                }
            }

            if (bestScale < 1) return SmartFullscreenResult.NoFit;

            double cover = Math.Max((double)screenWidthPx / bestWidth, (double)screenHeightPx / bestHeight);
            if (bestWidth * cover - screenWidthPx <= SeamTolerancePx && bestHeight * cover - screenHeightPx <= SeamTolerancePx)
                bestScale = cover;

            bool centered = Math.Round(bestWidth * bestScale) < screenWidthPx || Math.Round(bestHeight * bestScale) < screenHeightPx;
            return new SmartFullscreenResult(true, bestWidth, bestHeight, bestScale, centered, bestScale / dpiScale, FromFallback: true);
        }

        // Does the exact-fit check (Pass 3 auto fullscreen): true when a layout of width x height fills the screen at exactly
        // 1x in physical pixels, whatever the Windows scale
        public static bool IsExactFit(int screenWidthPx, int screenHeightPx, int width, int height) =>
            width > 0 && height > 0 && width == screenWidthPx && height == screenHeightPx;

        // Returns the menus' and popups' WPF scale: the whole-number part of the canvas scale, max(1, floor(scale)) / dpiScale,
        // so a fractional fallback scale (1.5, 1.25) leaves them at 1x while a whole-number one (2, 3) is unchanged. The tiny
        // tolerance keeps a whole number that arrives as 1.9999999 at 2.
        public static double PopupFactor(double scale, double dpiScale)
        {
            if (!(dpiScale > 0) || double.IsInfinity(dpiScale)) dpiScale = 1;
            if (double.IsNaN(scale) || double.IsInfinity(scale)) scale = 1;

            return Math.Max(1, Math.Floor(scale + 1e-9)) / dpiScale;
        }
    }
    // [END SECTION: Smart Fullscreen Size Test]
}
