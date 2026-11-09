# Handoff 1 of 6: Thumbnail constants and capture (Theme Browser prep)

## Kick-off prompt (paste this into Claude Code)

```
Read handoff-1-thumbnail-constants-capture.md in the project folder completely, then CLAUDE.md and themes.md (sections 3.22 and 3.4 at least).

Follow the file's pass structure: do the read-only STEP 1 check first, report any mismatch, then STOP and wait for me to say "go". Only after "go" do STEP 2 (the edits). Do one pass only, stop after it, and do not commit. Do not start any other Theme Browser work.
```

---

## 1. Goal and why

The Theme Browser (later handoffs) shows each theme's single stored thumbnail at three sizes. This pass prepares the thumbnail side only, and changes no Browser UI:

- New captures are scaled to **fit inside a 480 x 270 box** (preserving the layout's aspect ratio, never upscaling), instead of the current fixed 360 px height.
- All thumbnail box sizes live in **one named place**, so later handoffs and a future 640 x 360 return are one-line changes.
- The Theme Info thumbnail is held to a **320 x 180 maximum box** that never upscales, so it behaves the same for any aspect ratio.

Settled decisions (do not re-open):
- 480 x 270, 320 x 180 and 160 x 90 are all **maximum bounding boxes in WPF DIPs**. Images are fit inside and centered/aligned, never stretched, never cropped, never upscaled, for every aspect ratio (16:9, 4:3, 3:4, others).
- The capture box is **480 x 270 physical image pixels**.
- Smaller displays use `BitmapScalingMode.Fant` (in WPF, `HighQuality` is the same mode).
- **No migration** of older thumbnails (640 x 360 or "360 tall"). They just display through the normal path. Do not recapture anything, do not create variants.
- No custom per-monitor DPI logic. Sub-windows follow normal Windows scaling.

## 2. Files involved (verify names and line areas; the code wins)

- `Services/ThemeThumbnailStore.cs` (holds `FileName`, `WritePng`, `WritePending`, `RemoveUnusedFolders`)
- `MainWindow.xaml.cs`: `CaptureThemeThumbnail` (around line 339 at audit time) and its `const int ThumbnailHeight = 360`
- `ThemeThumbnailControl.xaml` / `ThemeThumbnailControl.xaml.cs` (`DisplayHeight`, `ApplyDisplayHeight`, `NoImageAspect = 320.0 / 180.0`)
- The host that creates or sizes the control on the Theme Info view (`AdvancedThemesTabControl.xaml` / `.xaml.cs`; search for `ThemeThumbnailControl`, `DisplayHeight`, `ThumbnailCaptureRunner`)
- `themes.md` (3.22 "Capture" bullet, change log), `CLAUDE.md` (Theme Info section)

## 3. Current behavior (from the audit)

- `CaptureThemeThumbnail` hides the grid for the render only, puts `RootLayoutGrid` into a `VisualBrush` over the layout rect, renders at **physical size** (`ActualWidth x DpiScaleX`), then draws it into a bitmap of **fixed height 360** (`const int ThumbnailHeight = 360`) with `HighQuality` scaling, saved at 96 DPI. The width follows the aspect ratio. A layout whose physical height is under 360 is **upscaled** today.
- `ThemeThumbnailControl` sizes by `DisplayHeight`, which sets `ImgThumbnail.MaxHeight` only. The Image is `Stretch=Uniform` with the default `StretchDirection` (Both), so it upscales, and its width is not capped (a wide image grows to the pane width). The control and the Image are left-aligned. The "No Image" box is `DisplayHeight x 320/180` inside a `DownOnly` Viewbox.
- Nothing else assumes 640 x 360. The only 16:9 assumption is `NoImageAspect`.

## 4. STEP 1: read-only check (do this first, then STOP and wait for "go")

Do not edit anything in this step. Report, briefly:

1. The exact current code of `CaptureThemeThumbnail` and where `ThumbnailHeight` is defined and used. Confirm the render-at-physical-size then draw-into-fixed-height description above. If the real technique differs in a way that makes "native render, then one high-quality scale into a fit-inside box" a larger restructure than changing the target-size computation, say so.
2. Every use of `ThemeThumbnailControl` and of `DisplayHeight` (and the literals 180 / 320 / 360 / 640 near thumbnails) across the project. Which are the Theme Info box, and which are something else.
3. The XAML of `ThemeThumbnailControl`: the Image's current `Stretch`, `StretchDirection`, `HorizontalAlignment`, `MaxHeight`, any `RenderOptions.BitmapScalingMode`, and the "No Image" box structure.
4. Every consumer of the captured image or its size (`WritePng`, `WritePending`, `SaveThemeCore`, `PendingThemeThumbnail`, the camera button handler) that assumes a size or aspect.
5. Where the constants should live (expected: `ThemeThumbnailStore`, next to `FileName`) and whether anything there conflicts.
6. Any mismatch between this file or `themes.md` 3.22 and the code.

Then stop. Edits start only after the user says "go".

## 5. STEP 2: the edits (only after "go")

### 5.1 Constants (in `ThemeThumbnailStore`, one obvious section)

Add public constants (DIP/pixel units named in comments). Names:

- `CaptureMaxWidth = 480`, `CaptureMaxHeight = 270` (physical image pixels of a new capture)
- `InfoMaxWidth = 320`, `InfoMaxHeight = 180` (Theme Info thumbnail box, DIPs)
- `BrowserMaxWidth = 160`, `BrowserMaxHeight = 90` (Theme Browser tile box, DIPs; not used until a later handoff)
- `PopOutMaxWidth = 480`, `PopOutMaxHeight = 270` (Theme Browser enlarged preview box, DIPs; not used until a later handoff)

Wrap them in `// [SECTION: Thumbnail Sizes]` / `// [END SECTION: Thumbnail Sizes]` with a short comment saying a future return to 640 x 360 changes these constants only. The Browser and pop-out constants being unused for now is intended; do not add warnings or suppressions (public consts produce none). Replace the magic 180 / 320 that refer to the Theme Info box with the Info constants wherever Step 1 showed them.

### 5.2 Capture: fit inside 480 x 270, no upscale

In `CaptureThemeThumbnail`, replace the fixed-height target with a fit-inside computation, keeping everything else as is (hiding the grid, the VisualBrush over the layout rect, the physical-size render, 96 DPI save, `HighQuality` scaling mode):

- Let `pxW`, `pxH` be the physical render size already computed.
- `scale = Math.Min(1.0, Math.Min(CaptureMaxWidth / pxW, CaptureMaxHeight / pxH))` (use doubles).
- If `scale >= 1`, the output is the physical size unchanged (no resampling, no upscale).
- Otherwise, output width and height are `Math.Max(1, (int)Math.Round(pxW * scale))` and `Math.Max(1, (int)Math.Round(pxH * scale))`, drawn with the same `HighQuality` scaling used now.
- Remove `const int ThumbnailHeight = 360` (and any now-dead variables) in the same edit that stops using it.
- Update the method's one-line summary comment to describe the fit-inside rule.

Expected results (at 100% Windows scale; physical size = layout size): 1280 x 720 -> 480 x 270; 1920 x 1080 -> 480 x 270; 1024 x 768 -> 360 x 270; 800 x 600 -> 360 x 270; a portrait 768 x 1024 -> 203 x 270; a small 400 x 225 layout -> 400 x 225. At other Windows scales the physical size differs (for example a 1280 x 720 layout at 150% renders 1920 x 1080 physical, still -> 480 x 270).

### 5.3 `ThemeThumbnailControl`: a maximum box that never upscales

Keep `DisplayHeight` and its callers working (do not rename it). Add:

- A new `MaxDisplayWidth` property (double, default `ThemeThumbnailStore.InfoMaxWidth`) next to `DisplayHeight`, applied in `ApplyDisplayHeight()` as `ImgThumbnail.MaxWidth` (update that method's summary comment).
- `DisplayHeight` default becomes `ThemeThumbnailStore.InfoMaxHeight` instead of the literal 180.
- `ImgThumbnail`: `StretchDirection="DownOnly"` (keep `Stretch="Uniform"` and the existing left alignment), and `RenderOptions.BitmapScalingMode="Fant"` (if `HighQuality` is already set there, leave it, since it is the same mode; if nothing is set, add `Fant`).
- The "No Image" box and `NoImageAspect` stay as they are (it still builds a 16:9 box from `DisplayHeight`; its width is exactly 320 at the defaults).
- The Theme Info host must keep showing the control at 320 x 180 max. If the host sets `DisplayHeight` to a literal, change it to the Info constant; do not change anything else there.

Result: a 480 x 270 capture shows at 320 x 180; a 4:3 capture (360 x 270) shows at 240 x 180; an old 640 x 360 image shows at 320 x 180; an image smaller than the box shows at its own size (no upscale).

### 5.4 One comment fix (same file you are already editing)

In the `ThemeThumbnailStore` file header comment, the text saying `LoadTheme` deletes `Themes/Extracted` is imprecise: `LoadTheme` only replaces the loaded theme's own extracted folder. Reword that one comment accurately. Change no other comments or code in that file besides the constants.

### 5.5 Documentation (end of pass, as `CLAUDE.md` requires)

- `themes.md` 3.22 "Capture": replace "returns a 360 px tall image" with the fit-inside 480 x 270 rule (aspect preserved, no upscale, the expected results above), and mention the constants section in `ThemeThumbnailStore` and the Theme Info 320 x 180 max box with `DownOnly`. Mention the control's new `MaxDisplayWidth`.
- `themes.md` section 3.22 or the file map: add the constants to the `ThemeThumbnailStore` row if it has one.
- `themes.md` change log: one dated entry for this pass (2026-11-08), listing files changed.
- `CLAUDE.md`, Theme Info (themes.md 3.22) bullets: add one short bullet: thumbnail box sizes are constants in `ThemeThumbnailStore` (`Capture*`, `Info*`, `Browser*`, `PopOut*` Max sizes); a capture fits inside the capture box and never upscales; display boxes are maximum DIP boxes with `DownOnly`.
- Keep `.md` files LF and `.cs` / `.xaml` CRLF as `CLAUDE.md` describes; check `git diff --stat` shows no whole-file change.

## 6. Constraints and things NOT to touch

- No Theme Browser files, controls, tabs or hamburger changes. This pass does not create or reference any Browser UI.
- Do not change the thumbnail file name, folder layout, PNG format, `WritePng`, `WritePending`, `RemoveUnusedFolders` or how a pending capture is saved.
- Do not recapture, migrate, rename or delete existing thumbnails, and do not add multiple asset variants.
- Do not change Smart fullscreen, the guides, the Background grid or the capture's grid-hiding logic.
- Do not touch the separate "Theme sizes:" / "Resolution:" label task.
- No new Theme Builder properties, no new Global properties, no new Chrome Palette brushes in this pass.
- Keep comment fencing and the one-line `// Does X` summary on each method you touch. No unrelated fixes bundled in.
- Never commit.

## 7. Verification

You (Claude Code):
1. `dotnet build` finishes with **0 errors and 0 warnings**. No transient errors are expected: update the `ThumbnailHeight` uses in the same edit that removes the constant, and change `ThemeThumbnailControl` code and XAML together.
2. Grep: no `ThumbnailHeight`, no `360`/`640` thumbnail literals remain in the capture or control code, and no stray literal `180`/`320` for the Theme Info box outside `ThemeThumbnailStore`. Report any you leave and why.
3. `git diff --stat` shows only the files listed in section 2 (plus the docs), with no whole-file line-ending changes.

The user (cannot be done without running the app; do not run any screen-capture or input scripts yourself):
1. Theme Builder -> Theme Info -> capture a thumbnail on a 1280 x 720 and a 1920 x 1080 size. Save Adjustments. In `Themes\Thumbnails\<guid>\theme-thumbnail.png`, the file properties show **480 x 270**.
2. Add or pick a 1024 x 768 size and capture: **360 x 270**, and Theme Info shows it at **240 x 180**, left-aligned, not stretched.
3. A 16:9 capture shows in Theme Info at **320 x 180**.
4. A theme with an old 640 x 360 thumbnail still shows correctly at 320 x 180.
5. Text edges in the 320 x 180 display look acceptable at 100% and at the user's usual Windows scale.
6. A theme with no thumbnail still shows the "No Image" box at the same size as before.

## 8. Stop conditions

Stop and report (do not push on) if any of these happen:
- STEP 1 shows `CaptureThemeThumbnail` or `ThemeThumbnailControl` works differently from section 3 in a way that needs more than the edits in section 5.
- Anything other than the capture, the control and its Theme Info host depends on the 360 height or on the control's upscaling.
- The build shows errors or warnings you cannot clear within the files in section 2.
- An edit would need a Browser file, a new property, or a change to Smart fullscreen or theme load/save code.
- You are unsure whether a literal 180 / 320 refers to the Theme Info box.

After STEP 2, report in this form: files changed, anything that did not match these instructions, anything changed beyond them, and the test checklist (section 7). Then stop. Handoff 2 (navigation) is a separate file, written only after the user reports back.
