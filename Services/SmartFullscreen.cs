using System;
using System.Collections.Generic;

namespace ArcadeStick.Services
{
    // The outcome of the Smart fullscreen size test. Fits is false when no layout fits the screen at 1x or more
    // (fullscreen then behaves as before). Scale is the whole-number upscale n (layout units to physical pixels),
    // Centered is true when the scaled layout does not cover the screen exactly (black bars), and TransformFactor is
    // the WPF ScaleTransform value, n / dpiScale, which cancels the Windows scale setting.
    public readonly record struct SmartFullscreenResult(bool Fits, int LayoutWidth, int LayoutHeight, int Scale, bool Centered, double TransformFactor)
    {
        public static SmartFullscreenResult NoFit => new(false, 0, 0, 0, false, 1);
    }

    // [SECTION: Smart Fullscreen Size Test]
    // Pure calculation, no UI dependencies (smart-fullscreen-pass1.md, decision 1). For each layout size w x h the
    // theme has, n = floor(min(screenW / w, screenH / h)) in physical pixels; sizes with n < 1 are skipped. The size
    // with the largest covered area (w*n by h*n) wins; on a tie the larger layout wins (4K: 1080 at 2x beats 720 at 3x).
    public static class SmartFullscreen
    {
        // Does the size test: picks the layout and whole-number scale that cover the most of the screen
        public static SmartFullscreenResult Choose(int screenWidthPx, int screenHeightPx, double dpiScale, IEnumerable<(int Width, int Height)> layoutSizes)
        {
            if (screenWidthPx < 1 || screenHeightPx < 1 || layoutSizes == null) return SmartFullscreenResult.NoFit;
            if (!(dpiScale > 0) || double.IsInfinity(dpiScale)) dpiScale = 1;

            int bestWidth = 0, bestHeight = 0, bestScale = 0;
            long bestCovered = 0;

            foreach (var (width, height) in layoutSizes)
            {
                if (width < 1 || height < 1) continue;

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
            return new SmartFullscreenResult(true, bestWidth, bestHeight, bestScale, centered, bestScale / dpiScale);
        }
    }
    // [END SECTION: Smart Fullscreen Size Test]
}
