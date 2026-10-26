# Smart Fullscreen, Pass 1: scale calculation, canvas transform, manual toggle

Status: spec for Claude Code. Do STEP 1 (read-only) first and report. Do not start STEP 2 until the user says "go".

## 0. Goal

Let a theme designed at 720p and/or 1080p fill any common 16:9 screen with a whole-number upscale, regardless of the Windows scale setting. The launcher cancels out Windows' DPI scale and applies its own whole-number scale to the layout canvas in fullscreen. Text and shapes re-render sharp; only raster art is stretched.

## 1. Split into passes

- **Pass 1 (this file):** the pure scale calculation, the canvas transform, the "Smart fullscreen" preference, and the manual fullscreen toggle (case 2) using them. Enough to test everything with a 960x540 theme.
- **Pass 2:** menus and popups follow the factor (implicit MenuItem / ContextMenu / Separator styles, folder-name and folder-color popups, Themes / Layout size / Add to Folder submenus).
- **Pass 3:** the auto part (case 1): explicit size pick goes fullscreen when exact 1x match, auto-drop back to windowed, rerun the size test on theme switch or size pick while already fullscreen.

Pass 1 includes a minimal manual entry/exit so it is testable. If the audit shows that is not practical without part of Pass 3, say so and propose the smallest split.

## 2. Decisions (settled with the user, 2026-10-26)

1. **The rule.** For each layout the theme has (w x h), compute n = floor(min(screenW / w, screenH / h)) in **physical pixels**. Skip layouts with n < 1. Pick the candidate with the largest covered area (w*n by h*n). On a tie prefer the larger layout (4K: 1080 at 2x beats 720 at 3x). If the covered area equals the screen exactly it is edge to edge; otherwise center it (black bars). If no layout fits at all, fall back to today's fullscreen behavior.
2. Worked table, theme with both 720 and 1080: 720p screen = 720 at 1x; 1080p = 1080 at 1x; 1440p = 720 at 2x; 4K = 1080 at 2x. Theme with 720 only: 1080p = 720 at 1x centered; 1440p = 720 at 2x; 4K = 720 at 3x (edge to edge, accepted: 2px borders become 6px). Never pick the 1.5x case when a 1080 layout exists.
3. **Windows scale is overridden.** Stay per-monitor DPI aware. Physical size = WPF monitor size x DPI scale. The extra transform on the canvas = n / dpiScale, so the result is exactly n physical pixels per layout unit at any Windows scale (4K, 1080 theme: 100% = 2.0, 125% = 1.6, 150% = 1.333, 200% = 1.0). Do not use a DPI-unaware manifest. Recompute on DpiChanged and when the window moves monitors.
4. **Session-only.** The layout chosen for fullscreen is never written to the theme: not into theme.json ActiveWidth / ActiveHeight, not into the saved layouts snapshot, not into the close-revert restore point. Leaving fullscreen returns to the layout that was active before.
5. **Sub-windows stay at the Windows scale** (Theme Builder, Options, Sorting, Manage Playlists, dialogs). Menus and popups of the main window follow the launcher's factor (Pass 2).
6. **Preference:** one per-install editor setting, "Smart fullscreen", default **off**, in settings.json like the guide toggles (not part of the theme). With it off, fullscreen behaves exactly as today. With it on, a manual fullscreen toggle runs the size test (this pass); the auto part comes in Pass 3. The checkbox goes in the hamburger menu unless the audit finds a better spot.
7. Raster scaling mode (nearest vs smooth) is not decided; use WPF default for now and note it as a later setting.

## 3. STEP 1: read-only audit (no edits)

Report findings for each, with file and method names:

1. **How fullscreen works today.** Which code toggles it (window style, state, topmost, key binding, menu item), what it does to LayoutCanvas / RootLayoutGrid sizing, and what happens to the canvas size and ApplyLayout.
2. **DPI awareness.** Is the app manifest per-monitor aware (V2)? Is `app.manifest` present? What does `VisualTreeHelper.GetDpi` return on the main window, and is DpiChanged handled anywhere?
3. **Where to put the transform.** Propose the host: which element gets the ScaleTransform (LayoutCanvas, its parent, or a new wrapper), whether to use LayoutTransform or RenderTransform, how to center with black bars, and how to keep `UseLayoutRounding` / `SnapsToDevicePixels` from blurring at exact multiples. Check that LayoutCanvas' size (the active resolution) stays the logical size.
4. **Layout selection vs. state.** How the active resolution is held (`ActiveWidth` / `ActiveHeight`, `Configuration.Layouts`, `SavedThemeLayouts`, the close-revert snapshot, pending custom sizes). Propose how to apply a fullscreen-only layout switch without touching any of them (e.g. a view-only "effective layout" used by ApplyLayout and nothing else). List every place that would otherwise save or snapshot it.
5. **GuideOverlay and previews.** The guide overlay is a full-window Canvas offset by LayoutCanvas' origin. Will edge and center guides still line up under the transform and centering? Same for the Random Game Display card preview and the Marquee banner preview (`SetRandomCardPreview`, `SetBannerPreview`).
6. **Theme Builder interaction.** What should happen if the Theme Builder is open while smart fullscreen is active (it opens as a child window; BringToFrontAfterChildClose exists). Does Ctrl+T still work in fullscreen? Anything that reads window size or canvas size (resolution combo, "Theme sizes" buttons, Add Layer, Keep Custom Size) that would see a wrong value.
7. **Multi-monitor.** How to find the monitor the window is on and its physical size (use the monitor bounds in physical pixels; say whether Screen / MonitorFromWindow / GetMonitorInfo is the cleanest here).
8. **Other readers of window size or mouse position** on the main window that a scale transform could break: the TreeView and scrollbars (Global.Scroll.HitWidth), hit testing, drag handling, video MediaElements, the wallpaper video, popups placed with PlacementTarget, ToolTips.
9. **Settings wiring** for the new `SmartFullscreen` bool: ConfigurationSettings, settings.json load/save in the MainViewModel constructor, and where the hamburger menu builds its items. Confirm it must NOT go through ThemeSettings / LoadTheme / the save DTO.
10. Anything in the spec above that the code contradicts, and risks you see.

End STEP 1 with: the proposed file list for STEP 2, the proposed signature of the pure function, and any questions for the user. Then stop and wait for "go".

## 4. STEP 2: implementation (only after "go")

Outline; adjust to the audit:

1. **Pure function** (own static class, e.g. `Services/SmartFullscreen.cs`, no UI dependencies): input = physical screen size, DPI scale, list of the theme's layout sizes; output = chosen layout size, whole-number scale n, centered flag, WPF-unit transform factor (n / dpiScale), and a "no fit" result. Implement decision 1 exactly, including the tie rule. Keep it testable.
2. **Preference:** `SmartFullscreen` (default false) in ConfigurationSettings and the settings.json load/save; hamburger menu checkbox.
3. **Canvas transform and centering** per the audit's chosen host. Black bars when centered.
4. **Manual toggle:** when the preference is on, entering fullscreen runs the function against the active theme's layouts, applies the effective layout and transform; leaving restores the previous layout and removes the transform. With the preference off, behavior is unchanged.
5. **Session-only:** nothing from the effective layout reaches theme.json, the saved snapshot or the close-revert (decision 4).
6. **Recompute** on DpiChanged and monitor change while in smart fullscreen.
7. Keep guides, previews and the Theme Builder working per the audit's findings.
8. Update `themes.md` (a new section, the file map, change log, Last verified line, and a trap note if the audit found one). Keep `.cs` / `.xaml` CRLF and `.md` LF.

Out of scope for Pass 1: menus and popups (Pass 2), auto fullscreen on size pick and auto-drop and theme-switch rerun (Pass 3), image scaling mode setting, drag handles, WYSIWYG.

## 5. Test checklist

Make a test theme at 960x540 (custom size; the look does not matter) and, if you want, a copy with 1280x720 only.

**A. Windows scale override (1080p monitor, 960x540 theme, Smart fullscreen on, toggle fullscreen):**
- [ ] At Windows scale 100%, 125%, 150% and 200% the result looks identical: the canvas fills the screen edge to edge at exactly 2x. (200% is the control; the extra transform should be 1.0.) Restart the launcher after changing the Windows scale if DPI is not picked up live.
- [ ] No bars, no blur on text or borders beyond the expected 2px to 4px doubling.

**B. Layout choice:**
- [ ] 1280x720 only theme on a 1080p screen: 720 at 1x, centered with black bars (the 1.5x case never happens).
- [ ] Theme with both 720 and 1080 on a 1080p screen: 1080 at 1x, edge to edge.
- [ ] If a 1440p or 4K monitor is available: 720 only = 2x / 3x edge to edge; 720 + 1080 on 4K = 1080 at 2x.

**C. Exit and persistence:**
- [ ] Leaving fullscreen returns to the previous layout and size, and the transform is gone.
- [ ] Save the theme while in smart fullscreen, then reopen it: ActiveWidth / ActiveHeight is the pre-fullscreen size, not the fullscreen layout.
- [ ] Close the Theme Builder without saving while fullscreen: nothing odd is restored.

**D. Preference off:** fullscreen behaves exactly as before this pass.

**E. Regression:**
- [ ] Games List scroll, selection and the right-click menu work (menus will not match the scale until Pass 2; note how they look).
- [ ] Wallpaper video, preview video and marquee still play and fit.
- [ ] Guides, the Random Game Display card preview and the Marquee banner preview line up (or are reported as known issues).
- [ ] Moving the window between monitors with different Windows scales recomputes correctly (if two monitors are available).
- [ ] Windowed mode and the normal resolution switching are unchanged.

Report anything odd; tweaks come after testing.
