# Background Grid (Theme Builder editor aid)

Spec for Claude Code. Read the whole file, do STEP 1 only, report, and wait for "go" before STEP 2.

## Goal

A pixel grid on the main window for lining up layers in the Theme Builder, next to the Edge guides and Center guides. A "Grid" checkbox and a spacing box (px) sit by the guide controls. While the grid is on, the main window hides the wallpaper image / video and draws grid lines over the Main Window Background layer's color. No new color setting.

## Decisions (from the user, do not reopen)

1. **Session-only.** Not saved to settings.json, not in the theme, not on any layer entry, not in ConfigurationSettings / ThemeSettings / LayerLayoutEntry / CopyStyleFrom. Nothing is saved or reverted on close beyond turning the grid off.
2. **Line color is derived from the Main Window Background color** (`ThemeMainColor` for the active resolution), not stored and not `GuideLineColorHex`. Take the color's RGB (ignore its alpha) and compute luminance `L = 0.2126*R + 0.7152*G + 0.0722*B` (0 to 255). If `L < 128` (dark background) blend the color toward white; otherwise blend toward black. Blend amount is one named constant, start at 18% (`BackgroundGridContrast = 0.18`), tuned by eye later. The result is a solid, fully opaque line color. It must follow live when the background color is edited while the grid is on.
3. **Spacing:** one text box, default 20, whole or decimal numbers, no upper limit and no lower limit other than "greater than 0" (a non-number, 0 or a negative reverts to the stored value). Applies on Enter or focus loss, like every other number box. Not a dropdown.
4. **Draw order:** above the Main Window Background color, below every layer (shape layers, Games List, everything in LayoutCanvas). Wallpaper image and `MainBgVideo` are hidden while the grid is on; the look is the same as "Disable background image (use the color only)" plus lines.
5. **Plain 1px lines only.** No thicker line every Nth cell.
6. The grid is an editor aid like the guides: it shows only while the THEME BUILDER tab is the selected top-level tab, is hidden when leaving that tab or closing the builder, and is NOT turned off by changing layer, resolution or loading a theme (unlike the card and banner previews). The checkbox and spacing value persist for the life of the open builder window.

## STEP 1: Audit (read-only, change nothing)

Report findings for each item, then stop:

1. **Guide controls layout.** In `AdvancedThemesTabControl.xaml`, the guides `StackPanel` (Grid.Row 1, ColumnSpan 3, holding `ChkShowGuides`, `ChkCenterGuides`, the Guide color picker and `TxtGuideColorHex`). The user measured and says a "Grid" checkbox plus a small spacing box (no label, tooltip only) fits on that same line, so the plan is to add them to that line, no new row, no layout shift. Using your offscreen measuring harness at the real pane width (397), confirm nothing clips or wraps. If it does not fit, report by how many px and stop; do not add a row without asking. Do not change the window size.
2. **Wallpaper hiding.** Confirm the root `RootLayoutGrid` Background comes only from its Style (ImageBrush of `ThemeMainWallpaper`, or SolidColorBrush of `ThemeMainColor` when that is null), and that a local `Background` value on `RootLayoutGrid` overrides it and `ClearValue` restores it. Confirm `MainBgVideoHost` (the Canvas holding `MainBgVideo`) can be collapsed independently of `RefreshMainBgVideo` (which sets `MainBgVideo.Visibility`, not the host's), so collapsing the host never fights it. Report any other thing that draws wallpaper (the Splash Image layer is a normal layer and stays).
3. **Draw order slot.** Confirm the root grid's child order (MainBgVideoHost, LayoutCanvas, GuideOverlay, popups) and that a new element inserted right after `MainBgVideoHost` sits below LayoutCanvas. Confirm it can be `IsHitTestVisible="False"` and fill the whole window, including at fullscreen (F11, where the canvas stays top-left anchored and the root grid fills the screen). Grid lines are measured from the window's top-left, the same origin as LayoutCanvas.
4. **Color-change hook.** In `MainWindow.ApplyLayout`, confirm `_lastMainBackgroundColor` is the place that notices a Main Window Background color change, so the grid can re-derive its line color and re-apply the solid cover color from there.
5. **Tab gating.** In `ThemeBuilderWindow.xaml.cs`, confirm the guides forwarder and the `ThemeBuilderTabControl.SelectionChanged` handler compare the selected tab's `Content` to `ThemeBuilderPage` (never an index), and that the grid should follow the same two places. Check there is no place where `StopPreviews()` is the right home for the grid (it is not: `StopPreviews` runs on every layer change and must NOT turn the grid off).
6. **Crisp lines.** Propose the drawing approach for 1px lines that stay sharp at any integer spacing and at 100% / 125% / 150% DPI (expected: a `DrawingBrush` tile with `ViewportUnits="Absolute"`, `TileMode="Tile"`, a 1px-tall rectangle along the tile's top and a 1px-wide one along its left, `RenderOptions.EdgeMode="Aliased"`, `SnapsToDevicePixels`, `UseLayoutRounding`). Note what happens with a fractional spacing and tell me if any value (very small, like 1 or 2) causes a performance problem.
7. Anything in the above that disagrees with this spec.

## STEP 2: Implementation (only after the user says "go")

### AdvancedThemesTabControl.xaml / .xaml.cs

- New controls on the existing guides line, after `TxtGuideColorHex` (names fixed): `ChkBgGrid` (content "Grid", style `GLCheckBoxStyle`, `Click="ChkBgGrid_Click"`, left margin to match the other gaps) and `TxtGridSpacing` (no label; width 52, centered, `GLTextBoxStyle`, `ToolTip="Grid spacing (px)"`, `KeyDown` / `LostFocus` handlers, text "20"). No existing name or handler changes.
- Session fields: `private double _gridSpacing = 20;`. Never written to `_settings`.
- New event: `public event Action<bool, double>? GridChanged;` (on, spacing). New method `public void RaiseGrid()` that invokes it with `ChkBgGrid.IsChecked == true` and `_gridSpacing`.
- `ChkBgGrid_Click` calls `RaiseGrid()`. The spacing handlers parse with `NumberStyles.Float` / `CultureInfo.InvariantCulture`; valid (`double.IsFinite` and `> 0`) stores `_gridSpacing`; then re-display the stored value (`Fmt`, under `_loading` like the other boxes so the redisplay raises nothing), then `RaiseGrid()`. Invalid input reverts to the stored value.
- Do NOT touch `ReloadFromSettings`' guide-preference block, `StopPreviews`, or anything that resets on layer/resolution change. Do not reset the checkbox in `ReloadFromSettings`.
- Add a small `// [SECTION: Background Grid]` block for the handlers.

### ThemeBuilderWindow.xaml.cs

- Subscribe: `AdvancedTab.GridChanged += (on, spacing) => { bool onBuilder = ReferenceEquals((ThemeBuilderTabControl.SelectedItem as System.Windows.Controls.TabItem)?.Content, ThemeBuilderPage); (Owner as MainWindow)?.SetBackgroundGrid(on && onBuilder, spacing); };`
- In the existing `ThemeBuilderTabControl.SelectionChanged` handler: when THEME BUILDER is selected, also call `AdvancedTab.RaiseGrid()` (alongside `RaiseGuides()`); in the other branch call `(Owner as MainWindow)?.SetBackgroundGrid(false, 0)` (alongside `ShowGuides(null)`; do NOT add it to `StopPreviews`).
- In `Closed`: `main?.SetBackgroundGrid(false, 0);` next to `main?.ShowGuides(null);`.
- Compare tab `Content` to `ThemeBuilderPage`, never an index (see themes.md section 7).

### MainWindow.xaml

- New element right after `MainBgVideoHost`, before `LayoutCanvas`: `<Rectangle x:Name="BackgroundGridLayer" IsHitTestVisible="False" Visibility="Collapsed" SnapsToDevicePixels="True" UseLayoutRounding="True" RenderOptions.EdgeMode="Aliased"/>` filling the root grid (default stretch). Comment it in the same style as the guides overlay.

### MainWindow.xaml.cs

- Fields: `private bool _backgroundGridOn; private double _backgroundGridSpacing = 20; private const double BackgroundGridContrast = 0.18;`
- `public void SetBackgroundGrid(bool on, double spacing)`: store both, call `UpdateBackgroundGrid()`. Safe to call before the window is loaded.
- `private void UpdateBackgroundGrid()`:
  - Off (or spacing not `> 0`): `BackgroundGridLayer.Visibility = Collapsed`, `BackgroundGridLayer.Fill = null`, `RootLayoutGrid.ClearValue(Grid.BackgroundProperty)`, `MainBgVideoHost.ClearValue(UIElement.VisibilityProperty)`, return.
  - On: read the Main Window Background color (`_viewModel.ThemeMainColor`, parsed with `MainViewModel.SafeConvertToBrush`), set `RootLayoutGrid.Background` locally to that solid brush (this hides the wallpaper image and matches the Disable-image look), set `MainBgVideoHost.Visibility = Collapsed` (the video layer is hidden; `RefreshMainBgVideo` is untouched), derive the line color per decision 2, build the frozen `DrawingBrush` tile (viewport `0,0,spacing,spacing` absolute, tile mode Tile; 1px top line and 1px left line in the derived color), set `BackgroundGridLayer.Fill`, show the layer.
  - A solid frozen brush for the root background and a frozen `DrawingBrush` are rebuilt only when the color or spacing changes.
- In `ApplyLayout`, inside the existing `_lastMainBackgroundColor` change block, after `RefreshMainBackgroundColor()`: `if (_backgroundGridOn) UpdateBackgroundGrid();` so the cover color and line color follow a live color edit.
- Nothing else in ApplyLayout, no layout key changes, no changes to the wallpaper / video code paths.

### Not touched (confirm in the final report)

LayerRegistry, LayerLayoutEntry, CopyStyleFrom, ConfigurationSettings, ThemeSettings, ThemesManagerControl save DTO, GlobalThemeResources, MainViewModel, settings.json, theme zips, the guides code.

## Test checklist (user runs after STEP 2)

1. Open Theme Builder: the Grid checkbox and spacing box are on the guides line, nothing clipped or wrapped, window not resized. Check it is off by default and the box shows 20.
2. Check Grid: wallpaper image disappears, the Main Window Background color fills the window, 1px lines every 20px from the window's top-left. Layers still draw above the lines; the grid never covers any layer and never blocks clicks.
3. Change spacing to 10, 50, 7, 1000, 12.5: lines update on Enter and on focus loss. Try `abc`, `0`, `-5`, empty: the box reverts to the last valid value.
4. Edit the Main Window Background color while the grid is on (Main Window Background layer, Image tab): lines and fill follow live. Try a very dark, a mid-gray and a very light color: the lines stay visible but quiet in each; report whether 18% needs tuning.
5. Use a video wallpaper: the video is hidden while the grid is on and returns (still playing) when it is off.
6. Select other layers, switch resolution (preset and custom), load another theme: the grid stays on and keeps its spacing. Edge guides and Center guides still work on top of it.
7. Switch to the THEME BROWSER or GLOBAL tab: grid and guides hide; back to THEME BUILDER: both return. Close the builder: grid is gone and the wallpaper is back; reopen: the box is unchecked and 20 again.
8. F11 fullscreen with the grid on: lines run to the screen edges, aligned from the same top-left origin as the layout.
9. Check at 100%, 125% and 150% display scaling if available: lines stay one sharp pixel.
10. Save Adjustments / Save Changes: nothing about the grid appears in settings.json or the theme.
11. Real spin, card preview and banner preview behave as before with the grid on.

## themes.md updates (docs pass, after the user's test)

- 3.4 alignment guides bullet: add the Background grid (session-only, shown only on THEME BUILDER, hides wallpaper image / video, line color derived from the Main Window Background color, controls `ChkBgGrid` / `TxtGridSpacing`, `MainWindow.SetBackgroundGrid`, `BackgroundGridLayer`).
- Section 7: grid is not in `StopPreviews` on purpose; compare tab `Content` to `ThemeBuilderPage`.
- Change log entry, Last verified line, and the decisions list (new decision: session-only grid editor aid, color derived from the background).
