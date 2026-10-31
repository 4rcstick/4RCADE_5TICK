# Handoff: Theme Info section (thumbnail, author, description, last saved)

You have no memory of the planning chat, so this file is everything you need. Read CLAUDE.md first (its conventions win over anything here), then read this whole file before touching code. This version incorporates the findings of your earlier read-only audit.

## Goal and why

The Theme Builder needs a "theme info" view that carries extra metadata about the active theme: a thumbnail, an author, a short description, and a last-saved date. This is the last piece of the Theme Builder feature set. A separate, later session will build the Theme Browser, which will reuse the same display pieces read-only and arrange them differently (e.g. thumbnail left, text right, no headings). So the display pieces must be small, reusable, placement-agnostic controls, not one fixed block.

## Current behavior (from your audit)

- Theme model: `ThemeSettings` (`Models/ThemeSettings.cs`) is the `theme.json` model. Live values live in `ConfigurationSettings`. Layers are `ThemeLayout` / `LayerLayoutEntry`.
- The builder is code-behind. `ThemeBuilderWindow` hosts `ThemesManagerControl` and `AdvancedThemesTabControl`. The sizes row, layer list and right-hand area (`PnlLayerFields` in `Grid.Column="2"`) are in `AdvancedThemesTabControl`.
- **Save Adjustments** (`ThemeBuilderWindow.BtnSaveAdjustments_Click`) writes `settings.json` only and moves the close-without-saving restore point. It never writes a theme file.
- **Save Changes** (`ThemesManagerControl.BtnSaveTheme_Click`) builds the DTO, bundles layer assets through `BundleThemeAsset` (stored as `assets/<filename>`, same source path bundled once, different file with the same name becomes `name_1.ext`), writes `theme.json`, recreates the zip, and runs the saved callback.
- There is NO dirty/changed mechanism. The close-without-saving restore point is the closest thing.
- `LoadTheme` (`MainViewModel`) extracts to `Themes/Extracted/<name>/`, maps `theme.json` onto settings, and rewrites asset paths relative to `BaseDirectory`. Normal launch does not call `LoadTheme`; live values come from `settings.json`.
- Switching themes means Load Theme or the hamburger menu, both of which reach `ThemeBuilderWindow.RefreshAfterThemeLoad`. Changing the Active Theme combo does NOT load anything.
- All app data is portable under `BaseDirectory\4rcade5tick_files`, via `GetArcadeStickFilesPath()`.

## Exact changes wanted

### 1. Data (info block)

Add a small, self-contained info block (one nested class, kept together so the future Theme Browser can read just this block) with:
- `Author` (string, may be empty)
- `Description` (string, may be empty)
- `ModifiedUtc` (nullable DateTime; null = never saved with Save Changes)
- `ThumbnailPath` (string, may be empty; stored relative to `BaseDirectory` so a USB drive-letter change does not break it)

The block must exist on BOTH `ThemeSettings` (theme.json) and `ConfigurationSettings` (settings.json). Map it in `LoadTheme` and in the `BtnSaveTheme_Click` DTO. In the DTO copy, `ThumbnailPath` is rewritten to the in-zip asset path the same way layer assets are rewritten (do not touch the live path). When loading a theme whose JSON has no block, use `themeData.Info ?? new()` so old themes load cleanly and the previous theme's values never leak across.

Do NOT add any other fields. No separate ID field: the unique per-theme ID lives only in the thumbnail's folder path (see section 5).

### 2. Four reusable display controls

Each control contains ONLY its content. No headings and no edit glyphs inside them. The Theme Builder layout supplies headings and glyphs around them.

**a) Thumbnail control**
- Displays the image at a fixed DISPLAY HEIGHT of 180 px (expose as a property so the browser can override). Width follows the aspect ratio; no cropping, no stretching. If the resulting width exceeds the available width, shrink the height instead. Bind to the available width; do not hard-code 410 (the pane measures about 395).
- Use `BitmapScalingMode.HighQuality` for display.
- Load images with `BitmapCacheOption.OnLoad` and `IgnoreImageCache` (as `MainViewModel` does for other images), so the file is never locked (LoadTheme deletes the extract folder) and a retake never shows a cached old image.
- Fallback when there is no image (or the file is missing or corrupt, never crash): a bordered box about 320 x 180 reading "No Image". Fully self-contained and opaque so it works over ANY theme background: dark neutral fill, 1-2 px light neutral border, light neutral text. Never transparent, never borrowing surrounding colors.

**b) Author control**
- Single line of text. If empty after trimming, show a blank line (keep the row height).
- Edit mode: replaces the text with a single-line TextBox pre-filled with the current value, with an Apply button beneath it. Enter applies. Apply commits, closes the box, shows the new text.

**c) Description control**
- Wrapped text in a scrollable area so long text never breaks the layout. If empty after trimming, show "No description" in a dimmer style than real text.
- Edit mode: replaces the text with a multi-line TextBox (AcceptsReturn = true, wrapping, vertical scroll) pre-filled with the current value, with an Apply button beneath it. Enter inserts a line break as normal typing would. ONLY the Apply button commits and closes the box.
- Add a `MaxDescriptionLength` constant (placeholder 500, the user will tune it later) and use it for the TextBox MaxLength.

**d) Modified-date control**
- "Last saved: <local date/time>" in a small, quiet style. If `ModifiedUtc` is null, show "Not saved yet". Never show 1/1/0001.

Shared behavior:
- Fallback strings ("No Image", "No description", "Not saved yet") go in ONE static class so the user can tweak them easily. The fixed placeholder colors go in App.xaml's Chrome Palette per CLAUDE.md, not in the static class.
- Text is trimmed before the blank check, so whitespace-only counts as blank and shows the fallback.
- Each editable control raises an event when a value is applied (or a new thumbnail is set), so the host can update the theme data.
- Each editable control exposes a method (e.g. `BeginEdit()`) the host calls when its glyph is clicked. There is no read-only flag: in the browser nothing will ever call `BeginEdit`, so those controls simply never enter edit mode.
- Unapplied text in an open edit box is simply discarded if the user selects a layer, saves, or closes. Do NOT add auto-commit or confirmation. Only Apply commits.

### 3. Theme info view in the Theme Builder (`AdvancedThemesTabControl`)

- Add the info view as a sibling of `PnlLayerFields` in `Grid.Column="2"`. It is the DEFAULT content when no layer is selected, including when the builder opens. Show it in the no-selection branch of `LoadSelectedLayerIntoFields` (around AdvancedThemesTabControl.xaml.cs:1124), and hide it in the multi-select and single-layer paths.
- Wrap the info view in an outer vertical ScrollViewer (auto) so nothing is clipped on small or scaled displays (WindowFit.KeepInWorkArea can shrink the window by about 200 px). Give the description a sensible minimum height.
- Layout, stacked top to bottom, left-aligned to a shared left edge:
  1. Heading "Thumbnail" with a camera glyph beside it, then the thumbnail control
  2. Heading "Author" with a pencil glyph beside it, then the author control
  3. Heading "Description" with a pencil glyph beside it, then the description control, taking the remaining vertical space and scrolling
  4. The modified-date control at the bottom
- Headings use the existing `Global.Font.Header.*` styles and `GLLabelStyle` as the project does elsewhere. Apply buttons use `ThemedActionButtonStyle`. Text boxes use `GLTextBoxStyle`.
- Glyphs: reuse the existing `LayersHelpGlyphStyle` (the 16 x 16 circular glyph beside the Layers heading; it is local to AdvancedThemesTabControl.xaml). Use Segoe MDL2 Assets text: camera `&#xE722;`, pencil `&#xE70F;`. Add `Cursor="Hand"` and `MouseLeftButtonUp`. A pencil click calls `BeginEdit()` on the matching control. I have not seen these two glyphs on screen, so tell me how they look and whether the codepoints rendered correctly.

### 4. "Theme Info" button

- Add a "Theme Info" button in the Theme sizes row, in the XAML StackPanel immediately AFTER `PnlSavedSizes` (NOT inside it, because `RebuildSavedSizes` clears that panel every time). It uses the same button style as the size buttons.
- Click calls `LstLayers.UnselectAll()` so the existing selection handler brings the info view back.
- Active look: bold text ONLY while the info view is showing (no layer selected), no background. CLAUDE.md allows setting Background only on the active size button, so do not set one here.
- Also check that removing a layer, which rebuilds the list, lands on the info view. Report what happens.
- Known and accepted: deselecting calls `StopPreviews`, so Random Game Display card/banner previews are stopped when the info view shows. That is fine; authors will time their captures.

### 5. Thumbnail capture (camera glyph)

- Clicking the camera captures what the main window preview currently shows.
- Add a public capture method on `MainWindow` next to `ShowGuides` / `SetBackgroundGrid`, reached from the builder via `Owner as MainWindow`, the same way the guides are.
- Guides: the camera only appears while no layer is selected, and with no layer selected `RaiseGuides` already sends null, so guides are already off at capture time. Only the GRID needs hiding: turn it off via `UpdateBackgroundGrid` / `SetBackgroundGrid`, render, then restore its previous state, all inside one UI-thread call so nothing paints in between and there is no flicker. The Theme Builder window must not appear in the capture.
- Render the main window preview (`RootLayoutGrid`: background, `MainBgVideoHost`, `BackgroundGridLayer`, `LayoutCanvas`, `GuideOverlay`) through a `VisualBrush` in a `DrawingVisual`, sizing pixels with `VisualTreeHelper.GetDpi`. Smart fullscreen puts a `LayoutTransform` on `RootLayoutGrid`, so check the capture size comes out right with it on and off.
- Scale to 360 px tall, keeping the aspect ratio, using WPF's `BitmapScalingMode.HighQuality` (the project does not use System.Drawing; do not add it). Output PNG.
- RISK TO TEST: `RenderTargetBitmap` renders in software, so `MediaElement` video (wallpaper, splash, shape layers) may come out blank. The preview video uses `WriteableBitmap` through `Playback/VideoPresenter.cs` and should capture fine. If video wallpaper or video shape layers capture blank, add a short, unobtrusive note near the camera glyph telling the author the capture may not include video, and tell me.
- After capture the image is held IN MEMORY only (the "pending thumbnail") and shown immediately in the thumbnail control. Nothing is written to disk at capture time.

### 6. Persistence flow

- Inline Apply buttons and capture only update in-memory state. They write nothing to disk.
- **Pending thumbnail file location:** `4rcade5tick_files/Themes/Thumbnails/<guid>/theme-thumbnail.png` (use `GetArcadeStickFilesPath()`). NOT under `Themes/Extracted` (that gets deleted on load). `RefreshThemeList` only lists `*.zip`, so this folder will not show as a theme. The GUID comes from the existing `ThumbnailPath` folder if there is one, otherwise generate a new one. Retakes overwrite the same file. Store `ThumbnailPath` relative to `BaseDirectory`.
- **Save Adjustments:** write the info block into `settings.json`. If there is a pending thumbnail, write it to the file above first and set `ThumbnailPath`. Do NOT stamp `ModifiedUtc`.
- **Save Changes:**
  1. If a pending thumbnail exists, write it to disk first (a user may capture and Save Changes without ever pressing Save Adjustments).
  2. Add ONE `BundleThemeAsset` call for the thumbnail, made BEFORE the layer loop, so a layer image with the same name cannot push it to `theme-thumbnail_1.png`. Do not otherwise change the zipper.
  3. In the DTO copy only, rewrite `ThumbnailPath` to the in-zip `assets/theme-thumbnail.png` path.
  4. Stamp `ModifiedUtc` (UTC now) ONLY after the zip has been written successfully, on both the DTO and the live settings.
- **Saving through `[New Theme...]`:** the new theme inherits Author and Description, but its thumbnail and date are cleared (it gets no inherited `ThumbnailPath`, and `ModifiedUtc` is stamped fresh by the save). If there is a pending capture, it becomes the new theme's thumbnail under its own NEW GUID folder. Never overwrite or reuse the previous theme's thumbnail file.
- **Load (`LoadTheme` / `RefreshAfterThemeLoad`):** resolve the thumbnail from `ThumbnailPath` (after the usual relative-path rewrite to the extracted assets). As a fallback only, look for `theme-thumbnail.png` in the extracted assets. If missing, show the "No Image" placeholder. Note that on a normal launch the extract folder may not exist, so a missing file must degrade silently to the placeholder.
- **Pending state:** clear any pending thumbnail in `RefreshAfterThemeLoad`, NOT `ReloadFromSettings` (a hamburger size switch also runs `ReloadFromSettings`). Make sure the info view refreshes with the loaded theme's values.
- **Restore point:** since there is no dirty mechanism, extend the Theme Builder's close-without-saving restore point to include the info block and the pending thumbnail, at each place the restore point is set or applied: the constructor, the saved callback, `RefreshAfterThemeLoad`, `BtnSaveAdjustments_Click`, and the `Closed` handler. Closing without saving discards unsaved info edits and any pending thumbnail.

## Constraints and things NOT to touch

- Do NOT build or modify the Theme Browser tab or the Global tab. Only make sure the four controls could be dropped into the browser later (no dependency on the builder's layout, no headings or glyphs inside).
- Do NOT change how layers, layer settings, resolutions, guides, grid, or offsets behave (other than the brief grid hide during capture).
- Do NOT touch the video preview / audio code.
- Do NOT add tags, version, license, an ID field, or any other metadata. Only the four fields.
- Do NOT fix or change the existing oddity where Save Changes writes to whichever theme the combo shows even if it was not loaded. Just mention it again in your report if you hit it.
- Do NOT rewrite or reorganize unrelated code. Match existing style, naming and patterns. No new NuGet packages; if you think one is needed, stop and tell me first.
- Existing theme files must still load and save without errors.

## Working order and expected errors

Build after each step:
1. Data model (both classes), mapping in LoadTheme and the DTO, fallback strings class, Chrome Palette colors
2. The four display controls
3. The info view and the Theme Info button
4. Capture method on MainWindow and the camera wiring
5. Persistence (Save Adjustments, Save Changes, New Theme behavior, Load, restore point)

Intermediate build errors while wiring across files are expected and fine. Only the FINAL build needs to be clean. Do not stop to report partway for intermediate errors.

## How to verify (run these and tell me what you saw)

1. Build with zero errors.
2. Launch, open the Theme Builder: the info view shows by default with the "No Image" box, a blank author line, "No description", and "Not saved yet".
3. Edit author (Enter applies) and description (Enter makes a line break; only Apply commits). Whitespace-only input falls back to the placeholder. Leaving an edit box open and selecting a layer discards the unapplied text.
4. Select a layer: its settings replace the info view. Click "Theme Info": the info view returns, and the button shows bold text only while it is active.
5. Capture a thumbnail with the grid turned on: the grid must not appear in the image and must be back on afterward with no visible flicker. Retake it and confirm the new image replaces the first. Test with a video wallpaper and report whether it captured.
6. Save Adjustments, close and relaunch: author, description and thumbnail persist.
7. Save Changes, then inspect the saved zip: `theme-thumbnail.png` is in assets (not renamed with `_1`), the JSON has the info block with a rewritten thumbnail path, and "Last saved" shows a date.
8. Save as a `[New Theme...]`: it keeps author and description, has no inherited thumbnail or date, and the original theme's thumbnail file is untouched.
9. Load an old theme with no info block: it loads cleanly with all fallbacks, and shows none of the previous theme's values.
10. Load a different theme, then close the builder without saving after editing info: edits and pending thumbnail are discarded.
11. Shrink the window or use a scaled display: the info view scrolls instead of clipping.

## Report back with

- Every file created or changed, with a one-line summary of each change
- How the capture behaves: grid hide result, video wallpaper result, whether the fallback note was needed, any fullscreen/DPI oddities
- How the camera and pencil glyphs look
- What happens on layer removal (does it land on the info view?)
- Anything you were unsure about, any deviations from this file, and any bugs or oddities noticed along the way
