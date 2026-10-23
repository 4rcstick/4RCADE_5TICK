# Background Grid Offset (and a tidier guides line)

Spec for Claude Code. Read the whole file, do STEP 1 only, report, and wait for "go" before STEP 2. Builds on the Background grid already in the code (see `background-grid.md` and themes.md section 3.4); everything not mentioned here stays as it is.

## Goal

Let the grid start somewhere other than the window's top-left, so grid cells can be counted from a layer's edge when setting the space between layers. Two small boxes, X and Y offset, on the guides line. To make room, the guide color's hex box is removed (the swatch stays) and its label is shortened.

## Decisions (from the user, do not reopen)

1. **Guides line changes.** "Guide color:" becomes "Color:". `TxtGuideColorHex` is removed entirely; the swatch (`GuideColorPicker`) is the only guide color control (the picker's own popup still has a hex entry). The saved setting `GuideLineColorHex` is unchanged.
2. **Offset controls** go on the same guides line, after the Grid checkbox and the spacing box: a label "Offset:" then two narrow boxes, X and Y. No second row. No window or pane resize.
3. **Greyed placeholders:** while a box is empty it shows a muted "X" or "Y" on top of it, hidden the moment text exists (even while the box has focus). Same pattern as the main window's folder-name popup placeholder (`FolderNameInputPlaceholder`): a `TextBlock` overlaid on the box, `IsHitTestVisible="False"`, `{StaticResource ChromeTextMuted}`, visibility driven from code (a TextChanged handler plus after every programmatic redisplay), not a DataTrigger.
4. **Empty means 0.** An empty box is an offset of 0 (the grid starts at the window's top-left, today's behavior). Typing 0 clears the box back to its greyed letter. The stored value is shown as text only when it is not 0.
5. **No limits:** any finite number, positive or negative, larger or smaller than the spacing. Invalid input (text, empty is fine = 0) reverts to the stored value's display. Stored rounded to 2 decimals (`MidpointRounding.AwayFromZero`, same as the spacing box). Applies on Enter or focus loss.
6. **Session-only, same gating as the grid:** not saved anywhere, off/0 on every builder open, shown only on THEME BUILDER, not reset by layer, resolution or theme changes, never in `StopPreviews`. Offsets are in layout units, measured from the window's top-left (the same origin as LayoutCanvas), like the spacing.
7. No "snap to selected layer" button for now (parked; may be added later after using the boxes).

## STEP 1: Audit (read-only, change nothing)

Report findings for each item, then stop:

1. **Guides line fit.** In `AdvancedThemesTabControl.xaml`, the guides `StackPanel` (`ChkShowGuides`, `ChkCenterGuides`, the "Guide color:" label, the swatch Border with `GuideColorPicker`, `TxtGuideColorHex`, `ChkBgGrid`, `TxtGridSpacing`). Using your offscreen harness at the real Advanced tab width, apply the planned end state at runtime (label "Color:", no hex box, "Offset:" label, two 40px-wide offset boxes each in a Grid with an overlaid muted placeholder `TextBlock`) and report the row width, free space and height, and whether anything clips or wraps. Expected: it fits with room to spare (the removed hex box frees about 105 px on top of the 83 px that was free). If it does not fit, report by how many px and stop; do not add a row.
2. **Hex box removal.** List every reference to `TxtGuideColorHex` (XAML, `ReloadFromSettings`, `GuideColorPicker_ColorChanged`, `TxtGuideColorHex_LostFocus`, anything else, including comments and themes.md/CLAUDE.md mentions). Confirm what must stay so the swatch keeps working: `_suppressGuideSync`, the picker's `SelectedColor` being set from `_settings.GuideLineColorHex` in `ReloadFromSettings`, and `GuideColorPicker_ColorChanged` still writing `_settings.GuideLineColorHex` and calling `RaiseGuides()`. Confirm the picker's popup really has its own hex entry.
3. **Placeholder pattern.** Look at how `FolderNameInputPlaceholder` and its TextChanged handler work in MainWindow, confirm it is the pattern to copy, and say where the placeholder visibility must also be refreshed (after redisplaying a stored value, after `ReloadFromSettings`-style resets, on focus loss).
4. **Grid plumbing.** List every caller and subscriber of `GridChanged`, `RaiseGrid`, `SetBackgroundGrid` and `UpdateBackgroundGrid` (ThemeBuilderWindow forwarder, the `SetBackgroundGrid(false, 0)` calls on leaving the tab and on Closed, MainWindow). Propose the smallest signature change to carry two offsets (expected: `GridChanged(bool on, double spacing, double offsetX, double offsetY)`, and `SetBackgroundGrid(bool on, double spacing, double offsetX = 0, double offsetY = 0)` so the off-calls need no change).
5. **BackgroundGridLayer.** Read `Views/BackgroundGridLayer.cs` and describe how it computes and caches its line positions. Propose how the offset enters: add `OffsetX` / `OffsetY` properties; for each axis reduce the offset to `start = ((offset % spacing) + spacing) % spacing` (so huge or negative offsets never loop over millions of lines), then lines sit at `round((start + n * spacing) * scale) / scale` for n = 0, 1, 2, ... until past the edge; a change to either offset rebuilds the cached positions (same as a spacing change). The per-line `DrawRectangle` approach, 1/scale thickness, pixel snapping, the solid fill under 2 screen pixels and the opaque-brush color handling must not change. Confirm the combined-geometry and tiled-brush approaches stay ruled out.
6. Anything in the above that disagrees with this spec.

## STEP 2: Implementation (only after the user says "go")

### AdvancedThemesTabControl.xaml / .xaml.cs

- Guides line: the "Guide color:" `TextBlock` text becomes "Color:". Remove `TxtGuideColorHex` from the XAML and its code (the `LostFocus` handler, the `.Text` assignments in `ReloadFromSettings` and `GuideColorPicker_ColorChanged`). Keep the swatch, `_suppressGuideSync`, and the settings write in `GuideColorPicker_ColorChanged`. Fix any comment that mentions the hex box.
- After `TxtGridSpacing`, add: a small "Offset:" label (same style as the "Color:" label), then two boxes `TxtGridOffsetX` and `TxtGridOffsetY` (width 40, centered, `GLTextBoxStyle`, `ToolTip="Grid offset X (px)"` / `"Grid offset Y (px)"`, `KeyDown` / `LostFocus` handlers), each inside a `Grid` with an overlaid muted placeholder `TextBlock` (`PhGridOffsetX` / `PhGridOffsetY`, text "X" / "Y", centered, `IsHitTestVisible="False"`). Both start empty. Margins match the neighboring controls. No existing name or handler changes.
- Session fields `_gridOffsetX` and `_gridOffsetY` (double, default 0), never written to `_settings`.
- Handlers (a `Background Grid` section next to the spacing ones): parse with `NumberStyles.Float` / `CultureInfo.InvariantCulture`; an empty box = 0; valid (`double.IsFinite`) stores the value rounded to 2 decimals; invalid keeps the stored value. Then redisplay under `_loading` (stored 0 = empty text, otherwise `Fmt`), refresh both placeholders, and call `RaiseGrid()`. A TextChanged handler on each box only updates its placeholder visibility (shown when the text is empty).
- `GridChanged` carries the offsets (`Action<bool, double, double, double>`); `RaiseGrid()` passes `_gridSpacing`, `_gridOffsetX`, `_gridOffsetY`.

### ThemeBuilderWindow.xaml.cs

- Update the `GridChanged` subscription to the new signature and pass the offsets to `SetBackgroundGrid`. Keep the tab-`Content` comparison to `ThemeBuilderPage`. The existing `SetBackgroundGrid(false, 0)` calls stay valid through the default parameters.

### MainWindow.xaml.cs and Views/BackgroundGridLayer.cs

- `SetBackgroundGrid(bool on, double spacing, double offsetX = 0, double offsetY = 0)` stores the offsets and calls `UpdateBackgroundGrid()`, which passes them to `BackgroundGridLayer` along with the spacing and color.
- `BackgroundGridLayer`: add `OffsetX` / `OffsetY`, rebuild the cached positions when either changes, and start each axis' lines at the reduced offset as described in STEP 1 item 5. Everything else in the class is unchanged.
- Nothing else in ApplyLayout or the cover / video handling changes.

### Not touched (confirm in the final report)

LayerRegistry, LayerLayoutEntry, CopyStyleFrom, ConfigurationSettings (`GuideLineColorHex` stays), ThemeSettings, the ThemesManagerControl save DTO, GlobalThemeResources, MainViewModel, settings.json, theme zips, the edge / center guides code, `StopPreviews`, the wallpaper and video code paths, window size.

## Test checklist (user runs after STEP 2)

1. Guides line shows: Edge guides, Center guides, "Color:" with the swatch only, Grid, the spacing box, "Offset:" and two small boxes with a greyed X and Y. Nothing clipped or wrapped, the row height and the layer list below it unchanged.
2. The swatch still changes the guide color live (edge and center guides), and its popup's hex entry works. Closing and reopening the builder keeps the chosen guide color.
3. Offset X = 10 with spacing 20: vertical lines move 10 px right and the pattern still fills the whole window, including the strip to the left of the first line. Same for Y with horizontal lines.
4. Negative offsets (-5), offsets bigger than the spacing (45 with spacing 20), decimals (2.5) all work and always fill the whole window. A huge value (100000) does not freeze the app.
5. Type 0, or clear the box: the greyed letter returns and the grid starts at 0 again. Type `abc`: the box reverts to the stored value (greyed letter if the stored offset is 0).
6. Line up a layer: set the offset to a layer's X, Y (type the numbers from its Position tab) and check that a grid line sits on its left and top edges, and that gaps to a neighboring layer count in whole cells.
7. Lines stay sharp and evenly spaced at spacings 5, 10 and 12.5 with an offset (including a fractional one). At 125% / 150% display scaling if available.
8. Change layer, resolution, load a theme: the grid, spacing and offsets stay. Leave the tab and come back: the grid and offsets return. Close and reopen the builder: grid off, offsets back to empty.
9. F11 fullscreen with an offset: lines reach the screen edges and keep the offset from the window's top-left.
10. Nothing about the grid or offset appears in settings.json or the theme.

## themes.md updates (docs pass, after the user's test)

- 3.4 Background grid bullet: the guides line now ends "Color:" swatch (no hex box), Grid checkbox, spacing box, "Offset:" and the X / Y boxes with greyed placeholders; offsets are session-only, empty = 0, any finite number rounded to 2 decimals, in layout units from the window's top-left; the lines start at the reduced offset; the grid event and `SetBackgroundGrid` carry the offsets. Fix the "after `TxtGuideColorHex`" wording and any other mention of the removed hex box (3.4, 3.7, the file map).
- Decision 58: add the offset. Change log entry. Last verified line.
