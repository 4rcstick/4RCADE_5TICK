# Handoff 1: Play Button layer (Game Info group)

You have no memory of the planning chat, so everything you need is in this file.

## Goal and why

Add a new layer part, **Play Button**, to the existing **Game Info** group (the group in `LayerRegistry.AllGroups` with Id `GameInfoGroup`). It is a clickable button on the main window that launches the selected game, exactly like double-clicking the game's row in the games list. It must be fully themeable from the Theme Builder (Advanced tab): it gets its own X/Y/Width/Height block on the group's POSITION page and its own sub tab named **PLAY BUTTON**.

This is the first of two handoffs. A second layer (Favorites Star) comes later and is NOT part of this task. Do not build anything for it.

## What the button is

A text button with a fill, a border and rounded corners. No image mode (explicitly cut).

Properties (all stored on the Play Button's own `LayerLayoutEntry`, per resolution, like every other layer):

| Group | Field | Stored in | New? |
|---|---|---|---|
| Text | Button text (max 24 chars, default `PLAY`) | `ButtonText` | **NEW** |
| Text | Font family | `TextFontFamily` | existing |
| Text | Size | `TextFontSize` | existing |
| Text | Bold | `TextBold` | existing |
| Text | Color | `TextColorHex` | existing |
| Text | Vertical nudge (-10 to 10) | `TextNudgeY` | existing |
| Text | Text drop shadow (enabled, color, blur, depth) | `TextShadowEnabled/ColorHex/Blur/Depth` | existing |
| Background | Resting color | `BgColorHex` | existing |
| Button shadow | Box drop shadow (enabled, color, blur, depth), separate from the text shadow | `BoxShadowEnabled/ColorHex/Blur/Depth` | **NEW** |
| Border | Color, size (0-50), corner radius (0-200) | `BorderColorHex`, `BorderSize`, `CornerRadius` | existing |
| Hover | Text color, background color, border color | `HoverColorHex`, `HoverBgColorHex`, `HoverBorderColorHex` | existing |

New fields on `LayerLayoutEntry` (follow the exact pattern of the existing nullable fields, including the `[JsonIgnore(Condition = WhenWritingNull)]` attribute on each, and add all of them to `CopyStyleFrom`):

- `string? ButtonText`
- `bool? BoxShadowEnabled`, `string? BoxShadowColorHex`, `int? BoxShadowBlur`, `int? BoxShadowDepth`

Precedent to copy: the Marquee banner does the same thing with `BannerShadow*` and `FillMarqueeBannerStyle`.

## Files involved

1. `LayerRegistry.cs` (Models)
2. `GameInfoLayerControl.xaml` and `GameInfoLayerControl.xaml.cs` (Views)
3. `MainViewModel.cs` (ViewModels)
4. `MainWindow.xaml.cs`
5. `AdvancedThemesTabControl.xaml` and `AdvancedThemesTabControl.xaml.cs` (Views)
6. Check, and edit only if needed: `LayoutScaler.cs` and anything else found by the search step below.

## Step 0: find every place new fields must be mirrored

Before editing, search the whole codebase for a comparable existing field, for example `BannerShadowBlur`, `BannerTextMargin` and `HoverBgColorHex`, and list every file that references them. Anything that enumerates entry fields (the new-size scaler `LayoutScaler`, theme save/load, clone/copy code) may need the new fields too. `ConfigurationSettings.CloneLayouts` already routes through `CopyStyleFrom`, so adding the fields there covers it. Open `LayoutScaler.cs` specifically and check whether it scales font sizes, border sizes, radii and shadow values per field or per layer id. If it does, make the Play Button's fields (text size, border size, corner radius, nudge, text shadow blur/depth, box shadow blur/depth) scale the same way a similar existing layer's do. Report what you found.

## Step 1: LayerRegistry.cs

- Add `public const string PlayButton = "PlayButton";` (Id is used as a key in theme files, so do not rename it later).
- Add `PlayButton` to the `GameInfoGroup` `PartIds` array, **last** in the list (so it is the last section on the Position page).
- Add an `AllLayers` entry: `DisplayName = "Play Button"`, `GroupId = GameInfoGroup`, `Z = 34` (same as the other Game Info parts), default rect `X = 765, Y = 348, Width = 100, Height = 32` (a placeholder; the user will reposition it).
- Add default constants (grouped in a commented block like the others):
  - `DefaultPlayButtonText = "PLAY"`, `MaxPlayButtonTextLength = 24`
  - `DefaultPlayButtonFontSize = 16`, `DefaultPlayButtonBold = true`
  - `DefaultPlayButtonColorHex = "#FF00D4FF"`, `DefaultPlayButtonBgColorHex = "#FF1C1C1E"`
  - `DefaultPlayButtonBorderSize = 1`, `DefaultPlayButtonBorderColorHex = "#FF00D4FF"`, `DefaultPlayButtonCornerRadius = 0`
  - `DefaultPlayButtonHoverColorHex = "#FF00D4FF"`, `DefaultPlayButtonHoverBgColorHex = "#FF2C2C2E"`, `DefaultPlayButtonHoverBorderColorHex = "#FF00D4FF"`
  - Text nudge default 0 (reuse `DefaultSearchLabelNudgeY` or add `DefaultPlayButtonNudgeY = 0`), min/max reuse `MinTextNudgeY` / `MaxTextNudgeY`.
  - Text shadow and box shadow reuse `DefaultShadowEnabled/ColorHex/Blur/Depth` (off by default).
- Add `private static void FillPlayButtonStyle(LayerLayoutEntry entry)` (only acts when `entry.Id == PlayButton`; fills each missing value from the defaults, keeping saved values, a saved `false` included). Model it on `FillMarqueeBannerStyle`. Call it in **both** `BuildDefaultLayout` and `ResolveLayout`, next to the other `Fill...Style` calls. This is what makes older themes pick the new part up automatically.
- Do NOT add `PlayButton` to `HasBorderTab`, `HasOutsideBorder`, `CanCopyRect` or `CanCopyBorder`. The border is drawn **inside** the box (X/Y/W/H is the exact visible size), and the guides frame the box itself.

## Step 2: GameInfoLayerControl.xaml

Add a new part, **last child of `HostCanvas`** (so it draws above the other parts if they overlap). `MainWindow.ApplyLayout` will set its Canvas position, size and visibility, so give it placeholder values matching the registry default. The mapped element is named `PartPlayButton`.

Structure (this exact layering avoids the dark corner slivers on hover that rounded buttons get; it mirrors the main window buttons' two-border approach):

```
Grid x:Name="PartPlayButton"   (Canvas.Left/Top/Width/Height placeholders, Cursor="Hand", MouseLeftButtonUp handler)
 ├─ Border x:Name="PlayButtonBox"   fill + full rounded edge + the BOX shadow Effect. No children.
 ├─ Border x:Name="PlayButtonRing"  Background Transparent, draws only the border ring on top, IsHitTestVisible="False"
 └─ TextBlock x:Name="PlayButtonText"  centered, TextTrimming CharacterEllipsis, small left/right margin, text Effect = text shadow,
                                       RenderTransform TranslateTransform Y = nudge, IsHitTestVisible="False"
```

Rules:
- The box shadow Effect goes ONLY on `PlayButtonBox`, never on `PartPlayButton`, or the text would pick it up too. The text shadow Effect goes only on the TextBlock.
- The ring and the text are not hit-testable, so only the rounded box takes clicks.
- Hover: `PlayButtonBox.Background`, `PlayButtonRing.BorderBrush` and the text `Foreground` swap to the hover values while the mouse is over the button. Do this with Style triggers (for example DataTriggers on `{Binding IsMouseOver, ElementName=PartPlayButton}`) whose Setter values are bindings to the view model, **not** with code that sets local values. Reason: local values permanently replace bindings, and the hover/resting colors must keep updating live from the Theme Builder.
- All values bind to new `ThemePlayButton*` forwarders (Step 3), with `FallbackValue`s matching the defaults, the same way the other parts do.
- The part is hidden while no game is selected automatically (the whole `HostCanvas` collapses). Do not add extra logic for that.

## Step 3: MainViewModel.cs

Add a section **`[SECTION: Play Button Forwarders]`** after the Game Info forwarders, following the Marquee Banner forwarders as the model. Read from `GetLayerEntry(LayerRegistry.PlayButton)`:

`ThemePlayButtonText`, `ThemePlayButtonFontFamily` (use the existing `ToFontFamily`), `ThemePlayButtonFontWeight`, `ThemePlayButtonFontSize`, `ThemePlayButtonColor`, `ThemePlayButtonTextNudgeY` (double), `ThemePlayButtonTextEffect` (via `BuildTextShadowEffect`), `ThemePlayButtonBgColor`, `ThemePlayButtonBoxEffect` (via `BuildShadowEffect` with the `BoxShadow*` fields), `ThemePlayButtonBorderColor`, `ThemePlayButtonBorderThickness` (Thickness), `ThemePlayButtonCornerRadius` (CornerRadius), `ThemePlayButtonHoverColor`, `ThemePlayButtonHoverBgColor`, `ThemePlayButtonHoverBorderColor`.

Colors are returned as hex strings like the other forwarders. The XAML binds them to brush properties the same way the existing parts do (they already bind hex strings to `Foreground`/`Background`/`BorderBrush`); follow that convention.

Add `public void RefreshPlayButtonBindings()` that raises every one of them, and:
- call it from `RefreshThemeBindings()` (this is the sync-point warning in that method's comment: a forwarder missing there will not update after theme load/save).

## Step 4: GameInfoLayerControl.xaml.cs and MainWindow.xaml.cs

- In `GameInfoLayerControl`, add `public event EventHandler? PlayRequested;` and the click handler for `PartPlayButton` that raises it. Keep the control free of launch logic.
- In `MainWindow`'s constructor, subscribe: `GameInfoLayer.PlayRequested += (s, e) => TriggerActiveSelectionLaunch();` (same pattern as `VideoPreviewLayer.SoundToggleRequested`). Do not change `TriggerActiveSelectionLaunch` itself. It already checks for a selected game and `CanExecute`. Do not add extra guards; a double-click on a row has none either.
- In `ApplyLayout`:
  - add `{ Models.LayerRegistry.PlayButton, GameInfoLayer.PartPlayButton }` to the `map` dictionary (position, size, Z and visibility then come from the existing loop);
  - add a separate style key `_lastPlayButtonStyleKey` (new field, next to the other `_last...StyleKey` fields) built from every Play Button entry value that affects its look (`ButtonText`, all `Text*` it uses incl. `TextNudgeY`, `BgColorHex`, `BoxShadow*`, `BorderSize/ColorHex`, `CornerRadius`, `Hover*`), and call `_viewModel.RefreshPlayButtonBindings()` only when the key changes. Do not fold it into `gameInfoStyleKey`; that key only covers the `Text*`/`SectionHeader*` fields of the other parts.

## Step 5: Theme Builder tab (AdvancedThemesTabControl.xaml / .xaml.cs)

Add a **PLAY BUTTON** tab (`x:Name="TabGIPlayButton"`, `Visibility="Collapsed"`, after the PREVIEWS tab). Register it: add to `AllLayerTabs`, and add it to the `GameInfoGroup` list in `GetLayerTabs`. The tab will wrap onto a second row in the tab strip. That is expected and accepted.

Reuse the existing Game Info block system for the text style. It is generic: controls are named `GI{Block}{Control}` and carry the block key in `Tag`, and the shared handlers (`GIFont_SelectionChanged`, `GIBold_Click`, `GIField_*`, `GIColorPicker_ColorChanged`, `GIColorHex_LostFocus`, `GIShadow_Click`, `GIShadowColorPicker_ColorChanged`, `GIShadowColorHex_LostFocus`) resolve everything through the `GIBlocks` dictionary.

1. Add a `["PlayButton"]` entry to `GIBlocks` (EntryId `LayerRegistry.PlayButton`, `UsesSectionHeader = false`, `HasFont = true`, defaults from the new constants, size range 6-72, shadow defaults off).
2. Build the **text section** of the tab as a copy of the Publisher tab's layout (font + bold; size / color; drop shadow with color / blur / depth) with these control names and `Tag="PlayButton"` on each: `GIPlayButtonFont`, `GIPlayButtonBold`, `GIPlayButtonSize`, `GIPlayButtonColorPicker`, `GIPlayButtonColorHex`, `GIPlayButtonShadow`, `GIPlayButtonShadowColorPicker`, `GIPlayButtonShadowColorHex`, `GIPlayButtonShadowBlur`, `GIPlayButtonShadowDepth`. (These names are looked up with `FindName` and dereferenced with `!`, so a typo crashes when the Game Info group is selected. Double-check every name.)
3. Add the controls the block system does not cover, with their own handlers in a new region **`[SECTION: Play Button Styling Fields]`** modeled on the Marquee Banner region (use the existing `ApplyPickerColor`, `ApplyHexColor`, `ShowColor` helpers, and a `PlayButtonEntry` accessor that returns the entry only while the Game Info group is selected):
   - Button text box (`MaxLength` = `MaxPlayButtonTextLength`; applies on Enter / focus loss; empty is allowed and means no text)
   - Vertical nudge box (-10 to 10, clamp like `ApplySearchButtonsFields` does)
   - Background color
   - Button shadow: checkbox, color, blur (0-30), depth (0-20); color/blur/depth disabled while unchecked (same `IsEnabled` binding trick the other shadow rows use)
   - Border: color, size (0-50), radius (0-200)
   - Hover: text color, background, border color
   - Group the sections with bold sub-labels ("Text", "Button", "Border", "Hover") like the SEARCH tab does, and use the same column grid (110 / 8 / 52 / 6 / 52) so everything lines up with the other tabs.
4. Add a `LoadPlayButtonFields()` that fills all of these when the Game Info group is selected, call it from `LoadSelectedLayerIntoFields()` next to `LoadGameInfoStyleFields()`. (The block-system controls are filled by the existing `LoadGIBlockFields` once the block is in `GIBlocks`.) Number/text fields that revert on invalid input must end by calling `LoadSelectedLayerIntoFields()` like the other Apply methods.

The Position page needs no work: `BuildGroupParts` builds one X/Y/Width/Height section per entry in the group's `PartIds`, so registering the part in Step 1 gives it its own block automatically. "Move all parts together" will include it. Reset Layer on the group resets it through `CopyStyleFrom`, which is why the new fields must be added there.

## Constraints: do NOT touch

- The four main window buttons (Random, Sort, Options, Hamburger), their handlers, or `RefreshMainButtonsRestingColors`.
- The other Game Info parts, their forwarders, or the existing `gameInfoStyleKey`.
- `TriggerActiveSelectionLaunch`, `ToggleFavorite`, or any favorites code.
- Smart fullscreen, the guides code, or the shared Border tab (`TabShapeBorder`).
- Any Theme Builder window files (`ThemeBuilderWindow.*`); nothing is needed there.
- No new NuGet packages.
- Match the codebase's comment style: a short descriptive comment on every method, and `[SECTION: ...]` / `[END SECTION: ...]` markers around new regions.

## How to verify

1. Build the solution. It must compile with no new warnings from the touched files. (Mid-task errors while files are half-edited are normal; the final state must be clean.)
2. Run the app and select a game. A **PLAY** button should appear at about (765, 348), cyan text and border on a dark fill.
3. Click it: the game launches exactly as a row double-click does. Return from MAME; the button is still there.
4. Hover: text, fill and border colors change. With the corner radius at 10+, no dark slivers show at the corners.
5. Press Ctrl+T to open the Theme Builder, select **Game Info**: a new **Play Button** block appears on POSITION, and a **PLAY BUTTON** tab exists. Change the text to `LAUNCH`, the font, size, nudge, every color, the border (size and radius), both shadows. Each change must show live on the main window. The button shadow must not affect the text, and the text shadow must not affect the box.
6. Turn the button shadow on with a large blur: it draws outside the button box. That is expected.
7. Click Save Changes, load another theme and then load the saved one back: all Play Button values survive. Then open an **older theme** that was saved before this change: the button appears with the defaults.
8. Press "Reset Layer" on the Game Info group: the Play Button returns to its defaults.
9. Report: files changed, what Step 0 found (especially `LayoutScaler`), and anything you did differently from this file and why.
