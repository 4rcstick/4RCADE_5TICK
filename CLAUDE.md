# 4RCADE 5TICK

## Project Overview

4RCADE 5TICK is a portable, highly customizable MAME frontend launcher built in C#/WPF (.NET 10.0). It targets arcade cabinet builders and MAME enthusiasts who want a polished, config-friendly frontend without manual ini editing.

Current version: **1.0.0-beta.4** (in progress). Repo: `4rcstick/4RCADE_5TICK` (MIT license).

Key features:
- TreeView-based game browser (custom folders, Favorites, Recently/Most Played, virtual catver categories, filters)
- Video previews through an FFmpeg-based player, plus flyer / title / snap / cabinet images and history.xml / Wikipedia game info
- Windows.Gaming.Input (WGI) gamepad navigation
- A layered Theme Builder: every visual element is a layer with per-resolution position, size, order and styling, edited live from the UI (no file editing)
- MAME.ini integration (reads/writes settings without manual ini editing)
- Favorites changes update the FAVORITES folder in place and keep it alphabetical

This is a solo-developer project, converted from a ~1600-line PowerShell prototype. It is deliberately architected, not vibe-coded. Scope and design decisions are made carefully; do not make architectural or scope decisions unilaterally. When a change has design implications beyond the immediate fix, flag it and ask rather than assuming.

## Source of Truth

- **`themes.md` is the living spec for the Theme Builder** (layer model, storage, tabs, decisions, traps, file map, change log). Read it before touching anything theme related.
- **Trust order: the code first, themes.md second.** If they disagree, the code wins: report the mismatch and correct themes.md at the end of the pass. Never assume a feature is built or unbuilt without checking the files.
- When the user hands over a spec `.md` (a migration or tweak batch), follow its pass structure: read it fully, do its **read-only STEP 1 audit** (older specs call it Step 0) and report mismatches, then **stop and wait**: **STEP 2 (implementation) starts only after the user says "go"**. Do one pass at a time, **stop after each pass** for the user to build, test and commit, and update `themes.md` at the end of each pass. A small change may come as a single prompt instead of a spec; do just that change. Planning ideas the user is still discussing (themes.md section 6b, parked ideas) are not requests: do not build or draft them until the user says they want them.

## Environment and Editing Rules (read first)

- Windows, Visual Studio, PowerShell. Python 3.12 is installed: call it as `python` (not `py`, because a 3.13 is also installed and the py launcher may pick it). Use it for large scripted or generated edits instead of sed.
- **`.cs` and `.xaml` files are CRLF; `.md` files (CLAUDE.md, themes.md and the spec files) are LF, enforced by `.gitattributes` (`*.md text eol=lf`).** Use the Edit tool for normal changes. When a Python script reads or writes a source file, open it with `newline=''` and `encoding='utf-8'` so line endings pass through unchanged, and make any inserted multi-line text match the file's endings (CRLF in `.cs` / `.xaml`, LF in `.md`). Do not use `sed -i` or other whole-file rewrites that normalize line endings. After edits run `git diff --stat` and check that no file shows a whole-file change.
- **Never commit.** The user commits after testing each pass.
- After every pass the project must build with **0 errors** (a full rebuild currently reports 0 warnings; report any new warning). If a multi-file edit will have transient errors, order the edits so the end state is clean, and say so.
- Keep comment fencing and style:
  - C#: `// [SECTION: ...]` / `// [END SECTION: ...]`
  - XAML: `<!-- [SECTION: ...] -->` / `<!-- [END SECTION: ...] -->`
  - Every method gets a one-line `// Does X` summary comment. Replace stale comments when changing a method; do not stack redundant ones.
- XAML: every `x:Name` and handler is referenced from code-behind. A control with a wrong name builds fine and fails at runtime, so verify names after adding controls (especially the Advanced tab's name-keyed Game Info handlers).
- Confirm scope before non-trivial changes, one logical change at a time, and no unrelated fixes bundled into an edit pass. Report in this form after a pass: files changed, anything that did not match the instructions, anything changed beyond them, and the test checklist.
- Grammar: "MAME®" is capitalized with the registered trademark symbol in user-facing text. Literal filenames stay lowercase in prose: `roms`, `mame.ini`.

## Architecture in Brief

Settings and themes
- `ConfigurationSettings` holds live settings (loaded from `4rcade5tick_files/settings.json`) and the per-resolution `Layouts`. It has **no change notification**; layer values reach XAML through `MainViewModel` `Theme*` forwarders, and the flat Global values through `{DynamicResource Global...}` resources published by `GlobalThemeResources.Apply`.
- A theme is a **zip** (`4rcade5tick_files/Themes/<name>.zip`) containing `theme.json` plus bundled `assets/`; loading extracts to `Themes/Extracted/<name>/`. `LoadTheme` deletes and re-extracts that folder (`ThemeExtractionStarting` releases open files first).
- `LayerRegistry` is the single source of truth for layers, groups, defaults and fonts. Each resolution has a `ThemeLayout` of `LayerLayoutEntry` items (rect, Z, visibility, ImagePath and styling fields).
- **New styling goes on layer entries, per resolution**, not on `ConfigurationSettings`. Old keys in saved files are ignored; there is no legacy migration (the user rebuilds themes).

Windows and views
- `MainWindow` (layout canvas, `ApplyLayout` and its style keys, TreeView templates, hamburger menu), `ThemeBuilderWindow` (hosts the THEME BUILDER, THEME BROWSER and GLOBAL tabs), `AdvancedThemesTabControl` (layer list, tabbed property pages), `GlobalThemesTabControl` (Global tab: Fonts, Backgrounds, Tabs, Buttons, Scrollbars, Context Menus), `ThemesManagerControl` (the Themes Manager row at the top of THEME BUILDER: theme load/save/delete only), the layer controls (`SplashLayerControl`, `VideoPreviewLayerControl`, `GameInfoLayerControl`).
- `MainViewModel` holds the forwarders, `GetLayerEntry`, the `Refresh...Bindings` methods, theme load, tree building, and media loading.

Media
- Game-preview video plays through the FFmpeg player (`PreviewPlayerHost`, native FFmpeg DLLs in `4rcade5tick_files/ffmpeg`) drawing into an `Image` in `VideoPreviewLayer`. Sound is an optional per-install setting, off by default (`PreviewSoundEnabled`): off opens no audio session at all (stopwatch clock), on uses the WASAPI path and the audio clock. A failed preview is logged to `4rcade5tick_files/logs/preview.log` and restarted up to 3 times (themes.md 3.20). Wallpaper video, boot splash video and shape/custom layer videos use WPF `MediaElement`. Animated gif/APNG use AnimatedImage.Wpf.
- Do not reopen: the single-`MediaElement` wallpaper loop (a two-player swap was tried and rejected for a visible flash).

## Known Gotchas (check before considering a change complete)

### Flat theme properties (`ConfigurationSettings`)
The remaining flat properties are the Global tab's theme-wide values (fonts, Options window backgrounds and border, separator, tabs, buttons, scrollbars, context menus). Each needs all of these wired or it silently reverts:
1. The property in `ConfigurationSettings` **and its line in `ApplyThemeSettings()`** (also applied at settings.json load in the `MainViewModel` constructor)
2. The `ThemeSettings` DTO
3. The `LoadTheme` mapping in `MainViewModel`
4. The DTO builder in `BtnSaveTheme_Click` in `ThemesManagerControl`
5. `GlobalThemeResources` (key constant, typed resource in `Apply`, `CaptureSnapshot` / `RestoreSnapshot` in its area), its default in App.xaml's `[SECTION: Global Theme Resources]`, and a field in the Global tab's `BuildFieldTables`
Readers use `{DynamicResource Global...}`, never a binding or a `Theme*` forwarder (no flat forwarder is left; `ThemeHeaderColor` was retired in the Marquee banner pass). Do not add value fields to the Themes Manager.
Prefer layer-entry storage for anything new; it avoids most of this.

### Layer entry styling (the usual silent failures)
A new field on `LayerLayoutEntry` needs all of: the property (`JsonIgnore(WhenWritingNull)` like its neighbors), a line in **`CopyStyleFrom`**, defaults in `BuildDefaultLayout` / the `Fill...` helper called from **both** `BuildDefaultLayout` and `ResolveLayout`, and a mention in the matching **style key in `ApplyLayout`**, or it will not survive resolution switches or Reset Layer, or will not repaint live. Forwarders must be re-raised from a `Refresh...Bindings()` method **and** from `RefreshThemeBindings()` (`Theme*` forwarder pattern: `Theme*` property, then `RefreshThemeBindings()`, then XAML binding).

### Row templates and ancestor bindings
Do not use `RelativeSource AncestorType` bindings in TreeView row templates for values; row styling is **pushed** from the view model onto `TreeCategoryNode` / `GameItem` (ancestor walks froze the UI on 30k+ row folders). The folder and game row templates each have their own selected-row trigger; keep them in their own template. Shadow effects are built once per refresh, frozen and shared; never mutate them.

### Main window buttons
Each of the four buttons is two layers: the outer Border holds the fill, the inner ring holds the border color and icon. Hover and resting colors are set from code; a new button style value must be applied to the correct layer in both `MainButton_MouseEnter` and `ApplyMainButtonRestingColors`.

### Game Info and other name-keyed handlers
The Advanced tab's Game Info (and Marquee shadow) controls are found **by name** (`GI{Block}{Control}`) with a Tag-keyed shared handler table, gated per block. Follow the existing naming exactly.

### Adding a new Game Info part (template: Play Button; simpler: Favorites Star)
- Registry: `const` Id (never renamed), last in `GameInfoGroup` `PartIds`, `AllLayers` entry with the group's Z, default constants, a `Fill...Style` called from both `BuildDefaultLayout` and `ResolveLayout`. Use `??=` for text where empty is a saved choice.
- New entry fields: nullable, `JsonIgnore(WhenWritingNull)`, added to `CopyStyleFrom`. Check `LayoutScaler` and report; it only needs a change for a size outside the general 6 to 72 font rule.
- XAML: last child of `HostCanvas`, in the `map` in `ApplyLayout`. Resting and hover values are Style setters bound to forwarders, never local values.
- View model: `[SECTION: ... Forwarders]` with a `Refresh...Bindings()` raised from `RefreshThemeBindings()`, and the part's own `_last...StyleKey` in `ApplyLayout` (not `gameInfoStyleKey`).
- Tab: add to `AllLayerTabs` and `GetLayerTabs`; a `GIBlocks` entry for the shared text and shadow controls (`GI{Block}{Control}`, `Tag` = block key; check every name, a typo crashes at runtime), a `[SECTION: ... Styling Fields]` region for the rest, its `Load...Fields()` called from `LoadSelectedLayerIntoFields()`.
- Actions: the control raises an event and holds no logic; `MainWindow` handles it.
- Update the part list in themes.md 3.2 and the count in section 8.

### Theme Builder window behavior
Closing without saving restores layouts to the last saved snapshot; anything stored outside layouts is not covered. Advanced-tab edits are in memory only until Save Adjustments / Save Changes. Layer files (images, video) are bundled into the theme zip through each entry's `ImagePath`. Closing the builder or any modal child of MainWindow calls `MainWindow.BringToFrontAfterChildClose` so the launcher returns to the front (never make it Topmost).

### Theme Builder window traps (details in themes.md section 7)
- **Never test the top-level Theme Builder tab by index.** Compare the selected tab's `Content` to `ThemeBuilderPage` (the guides and the Background grid show only on THEME BUILDER). `AdvancedTab` now sits inside `ThemeBuilderPage`, so comparing to it is always false.
- **Border ring rule (decisions 57, 59 and 62).** On Video Preview, Splash Image, Shape Layers 01 to 04, custom layers and both Random Game Display states, Width / Height are the content size and the border is drawn **outside** it (grows outward by `BorderSize`); X / Y are the ring's **outer top-left corner**, so the content sits at (X + B, Y + B) and the outer box is (X, Y, W + 2B, H + 2B). Code that needs the content area must read the entry (W / H, origin X + B, Y + B), never the control's Canvas size; the guides use the outer box (`GetGuideRect`, `LayerRegistry.HasOutsideBorder`). Use `PlaceOutsideBorder` / `ApplyRingBox` in `MainWindow.xaml.cs` for a new bordered layer instead of copying the arithmetic; do not move it into the shared positioning loop. The no-art banner card has no rect of its own (its ring is centered in the Marquee rect; the Marquee guides stay on the Marquee rect), and the Search & Buttons row is exempt (own border handling).
- **Background grid (session-only editor aid, decision 58):** `BackgroundGridLayer` draws one `DrawRectangle` per line, snapped to whole physical pixels and rounded half-up. Never switch to a combined geometry or a tiled `DrawingBrush` (both failed on screen). While on it owns local values on `RootLayoutGrid.Background` and `MainBgVideoHost.Visibility`; set them nowhere else. It is deliberately not in `StopPreviews`. Offscreen renders use the software renderer and do not clear a drawing change; the on-screen test decides.
- **Random Game Display message:** an empty `TextMessage` is a saved choice, so fill it with `??=`, never `IsNullOrWhiteSpace`. Background, border, clip and wrapped width come from the **active state's** part, and code picking the state uses `RandomDisplayFromSplash`, not `_randomSpinFromSplash`.
- **Marquee banner card** is a `ControlTemplate` (`MarqueeBannerCardTemplate`): its elements bind view model forwarders directly and read the title through `TemplatedParent`; a DataTemplate would lose the DataContext.
- **Global resources are typed** (font sizes `double`, border sizes `Thickness`, radii `CornerRadius`, colors `Brush`); a wrong type fails silently. **Do not set Background, Foreground, BorderBrush or BorderThickness on a `ThemedActionButtonStyle` button** (the one intended exception is the active saved-size button). **Chrome Palette:** a new fixed color is a named brush in `[SECTION: Chrome Palette]`, never a hex inside a style, and App.xaml gets no implicit (keyless) styles.

### Smart fullscreen (themes.md 3.17 to 3.19)
- **app.manifest is Per-Monitor V2.** Do not remove it or go DPI-unaware: physical sizes and DpiChanged depend on it.
- **Display override.** Code that shows the layout uses `ResolveDisplayLayout` / `DisplayLayoutWidth/Height`; code that edits, saves or snapshots uses `ActiveResolution*`. Never write the override to settings, layouts, snapshots or theme.json. Never hand `SavedThemeLayouts` to `ResolveLayout` (it fills gaps in place).
- **Local values** on `RootLayoutGrid` (LayoutTransform, Width/Height, alignment, rounding) and the Window background are cleared in `EndSmartFullscreen` before `SizeToContent` returns.
- **Popup scale.** Menus and popups use m = max(1, floor(s)) / dpiScale (`SmartFullscreen.PopupFactor`, the whole-number part of the canvas scale), the canvas keeps f = s / dpiScale; they differ only for a fractional fallback s. A Popup inside a visual tree inherits its ancestors' transform, so submenus, folder popups and the picker dropdown must not add `Launcher.PopupScale` (it doubles, or gives f times m). Only parentless popups set it: top-level ContextMenus and hand-opened or code-built tooltips (`DelayedTooltipTimer_Tick`, the ROM copy tooltip). The one exception: the two folder popup Borders (inside RootLayoutGrid, inheriting f) read `Launcher.InheritedPopupScale` (m / f) so they land on m, and Create folder's offsets are multiplied by `_inheritedPopupFactor`; nothing inside them gets a scale. Only `ApplySmartFullscreen` / `EndSmartFullscreen` write these resources. Never scale elements of a third-party control by Name.
- **Auto-entered flow and entry size.** `_smartFullscreenAutoEntered` is cleared by every manual toggle and by `EndSmartFullscreen` from any cause. `_fullscreenEntrySize` is the active size when fullscreen was entered (set by `EnterFullscreen` after the size test, by the auto-enter with the size before its pick, and by the Theme Builder open's adopt); a pick that stays fullscreen keeps it, a theme switch and `EndSmartFullscreen` clear it; F11 out restores it and the app close in fullscreen saves it to settings.json. Opening the Theme Builder while Smart fullscreen shows another size first makes that size the active size with `SwitchActiveLayoutSize(..., persist: false)`, before the builder exists (no settings.json write, not a `RunSizePick`), then sets the builder's size restore point back to the pre-adopt size (`ThemeBuilderWindow.SetSizeRestorePoint`) so closing without saving returns to it. In any fullscreen a size pick stays Smart only on an exact fit for the monitor, otherwise it drops to windowed at the picked size. Every size pick (hamburger Layout size, the builder's Theme sizes buttons and size combo through `SizePickRunner`) goes through `MainWindow.RunSizePick`, which sets `_deferBuilderDrop` and then runs `HandleLayoutSizePickForFullscreen`; every theme switch (hamburger, the builder's Load Theme through `ThemeLoadRunner`) goes through `MainWindow.RunThemeSwitch`, which sets the defer too. A visible Theme Builder limits Smart fullscreen to the active size: the check at the top of `ApplyLayout` reruns `ApplySmartFullscreen` on a mismatch (keep the re-entrant `return`), skipped while `_deferBuilderDrop` is set.
- **Sub-windows** call `WindowFit.KeepInWorkArea` so they fit the monitor's work area at any Windows scale.
- **Theme Builder menus** (the layer list's Copy / Paste, decision 67) use the keyed App.xaml styles `BuilderContextMenuStyle` / `BuilderMenuItemStyle` / `BuilderMenuSeparatorStyle` (`Global.Menu.*` only, no `Launcher.*`), set explicitly on the menu, every item and every separator (a menu restyles a Separator from `MenuItem.SeparatorStyleKey` otherwise); MainWindow's implicit menu styles never reach the builder. The Random Game Display row copies and pastes on `_guidePartId` (first part as fallback).
- **Color picker dropdown** is PortableColorPicker's internal popup (`FindName("popup")`): measure `DesiredSize`, not `ActualHeight` (clamped to the screen), and re-measure after moving it.

### Media loading (decision 42, 2026-10-21)
The `SelectedGame` setter runs the instant part (marquee, `MarqueeMissing`, flyer/title/snap/cabinet, the video slot's static image, `HasActiveMedia`): file checks on the UI thread, decoding on a background task guarded by `_mediaSelectionVersion`, plus a 30-entry decoded-image cache. **Only `VideoSourcePath` stays behind the 600 ms debounce.** `ResolvePreviewMedia()` is the single two-pass preview resolver. Do not move `HasActiveMedia` or `MarqueeMissing` back behind the debounce (that was the original "no preview flash" bug). Anything that sets `MarqueeImage` to null for "nothing selected" must also clear `MarqueeMissing`; use `MarqueeMissing`, never `HasMarqueeImage`, for the no-art banner.

### Rompath safety rule
Boot-time sync (`SyncMameRomPathsAsync`) and user-triggered save (`MAMEiniTabControl.UpdateMameIniSettings`) are two separate writers **by design**. `BtnSaveOptions_Click` must never call the MAME.ini tab's rompath sync. Do not consolidate these without explicit approval.

### `StopPollingLoopAsync` deadlock
The WGI polling loop fix uses `await` instead of `.Wait()`. **Do not revert to the synchronous variant**; it causes a silent UI thread deadlock in the game launch path.

### WPF rendering notes
- Nested Grids with negative margins don't respect a parent `Border`'s corner radius. Pattern: background layers as `Border` elements with matching `CornerRadius`, wallpaper as `ImageBrush` on `Background`.
- `ClipToBounds` clips to rectangular bounds only. The scrollbar corner-clip quirk is an accepted limitation, not a bug to fix. Rounded clips use a `RectangleGeometry` (see the Splash and Video Preview layer controls).

### ROM list and cache
- `mame_cache.json` (config/database) is derived from `mame.exe -listfull` output via `CacheScannerService`; ROM-set drift detection regenerates it when ZIP names diverge from cached entries.
- Most casual users keep ROMs flat. The fallback "GAMES" node for ungrouped games is essential; never silently discard ungrouped games.

### Favorites changes (themes.md 3.21)
- A favorite add or remove updates the tree through `ApplyFavoriteChangeInPlace`. Do not add a full rebuild to this path; `UpdateLiveTreeDisplay` runs only when the consistency check fails or the change throws.
- The rebuild's FAVORITES block, the sorted insert and the consistency check must agree on the order (FullTitle, then RomName). Change them together.
- Code that changes `FavoriteRoms` must raise `IsSelectedGameFavorite`.
- A live change to a node's `ChildGames` needs `RefreshDisplayItems()`, and `GuardFavoritesTreeChange` if the selected row can go away.

### Historical notes: LibVLC era (status to confirm with the user before relying on or deleting)
Earlier betas used LibVLCSharp for previews. Two incidents from that era: an RTSS / MSI Afterburner overlay hook (`RTSSHooks64.dll`) colliding with LibVLC's D3D11 output (native access violation in `dxgi.dll`; workaround RTSS application detection level None), and a loop race from combining a manual `EndReached` restart with `:input-repeat`. If LibVLC is fully gone, these no longer apply; do not add LibVLC back or re-add manual loop restarts without asking.

## Theme Builder Status (beta.4)

Built: Main Window Background (color, wallpaper), Games List (folders, game titles), Shape Layers 01 to 04 and custom layers, Marquee (logo, shadow, no-art banner with one shared card for the idle banner and the spin's no-marquee card), Splash Image, Video Preview (border, background, fallback image), Search & Buttons, Game Info (title, publisher, info headers and body, nav, previews shadow), Random Game Display (text, background, border, overlay card in both states, per-part ratio lock). Also: a Layers header with a ? help tooltip above the layer list, right-click Copy / Paste of position, size and border between layers (decisions 66 and 67), layer rename, multi-select nudge, group move, alignment guides (Edge guides and Center guides, running edge to edge of the window), and a session-only Background grid (spacing and X / Y offset) for lining layers up; every bordered layer draws its border outside its Width / Height with X / Y at the ring's outer corner (Video Preview as the model). **Global tab** (theme-wide, not per resolution): Fonts, Backgrounds, Tabs, Buttons, Scrollbars (colors and width), Context Menus (colors and border), each with Restore Defaults, live through Global resources, reverted on close without saving. The input chrome (text boxes, combos, lists, checkboxes, sliders, picker popups, small dialogs) uses the fixed Chrome Palette in App.xaml. The Themes Manager (theme load/save/delete) is a row at the top of THEME BUILDER; THEME BROWSER is an empty Grid placeholder awaiting the theme browser (Global tab migration Passes 1 to 6, `global-tab-plan.md`, then 2026-10-22).
- Play Button (Game Info part): launches the selected game; text, fill, border, hover colors and separate text and button shadows.
- Favorites Star (Game Info part): solid or outline glyph that toggles the selected game's favorite; size and each state's color and shadow.

Smart fullscreen (themes.md 3.17 to 3.19, decisions 63 and 64) is built and tested on a 1080p monitor at 100/125/150/175%; 1440p, 4K and mixed-DPI monitor moves are untested on hardware. Remaining: a theme browser on the THEME BROWSER tab, then replacing registry defaults with the finished default theme. The Footer is intentionally not themed (it stays as is). A WYSIWYG edit mode is planned for beta.5 (no drag handles in beta.4).

## Current Scope Boundaries

**In scope for v1.0-final:**
- Subfolder packaging refactor (moving launcher and dependencies into a MAME subdirectory)
- ROM-path rescan so `mame.ini` rompath syncs without a restart

**Scoped for v2.0: do not implement early without explicit request:**
- `.4rctheme` community theme package format (themes are already zip bundles as of beta.4; the dedicated extension and sharing flow is the v2.0 piece)
- `catver.ini`-based virtual auto-sort of flat romsets into category folders (virtual only, does not move files on disk). `catver.ini` bundled with attribution to progetto-SNAPS/AntoPISA.
- Folder-level inherited mouse/lightgun support with visual indicator after folder name

**Deferred / not currently planned:**
- TextBox theming
- Linux/Android port (preliminary market exploration only; contingent on Windows v1.0 traction; would require Avalonia UI for Linux, full rewrite for Android)

## Key Libraries & Tools

- FFmpeg native DLLs (preview player), loaded from `4rcade5tick_files/ffmpeg`
- AnimatedImage.Wpf (gif / APNG), PortableColorPicker (`ColorPicker` assembly)
- Windows.Gaming.Input (gamepad support)
- System.Text.Json
- Target distribution: self-contained .NET 10 + WPF runtime (about 250MB uncompressed); framework-dependent deployment is explicitly ruled out in favor of faster boot time
