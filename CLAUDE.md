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
- **Look inside files with the Read and Grep tools, not shell pipelines.** To see part of a file, use Read with offset and limit; to find text, use Grep. Do not chain `sed`, `cut`, `awk`, `tail`, `head`, `wc`, `echo` or `od` to peek at a file or check its ending: each of those makes the user approve a permission prompt, and the file tools do not. Use the shell to run something (a build, git, a script), not to read a file.
- **Run shell commands as they are, with no `cd` prefix.** The shell already starts in the project folder, so do not write `cd "<project path>" && git status`. A `cd` prefix turns the command into a chain, and the permission system then asks for approval even for commands that are allowed on their own (`git status`, `git diff`, `dotnet build`). If a command fails because it ran in the wrong folder, stop and tell the user instead of adding a `cd`.
- After every pass the project must build with **0 errors** (a full rebuild currently reports 0 warnings; report any new warning). If a multi-file edit will have transient errors, order the edits so the end state is clean, and say so.
- **Build with exactly `dotnet build --no-incremental`**, run on its own: no pipe (`| Select-String`, `| Select-Object`), no output filtering, no `;` or `&&` chaining, no `2>&1`, no wrapper script. The plain command is pre-approved; any of those additions turns it into a different command and triggers a permission prompt. Read the errors and warnings from its full output.
- Keep comment fencing and style:
  - C#: `// [SECTION: ...]` / `// [END SECTION: ...]`
  - XAML: `<!-- [SECTION: ...] -->` / `<!-- [END SECTION: ...] -->`
  - Every method gets a one-line `// Does X` summary comment. Replace stale comments when changing a method; do not stack redundant ones.
- XAML: every `x:Name` and handler is referenced from code-behind. A control with a wrong name builds fine and fails at runtime, so verify names after adding controls (especially the Advanced tab's name-keyed Game Info handlers).
- Confirm scope before non-trivial changes, one logical change at a time, and no unrelated fixes bundled into an edit pass. Report in this form after a pass: files changed, anything that did not match the instructions, anything changed beyond them, and the test checklist.
- Grammar: "MAME®" is capitalized with the registered trademark symbol in user-facing text. Literal filenames stay lowercase in prose: `roms`, `mame.ini`.

## Boundaries and Privacy (read first)

The user keeps personal windows open (a browser, chat apps) while Claude Code works. Nothing in this project may read, capture or type into anything outside the launcher.

### Stay inside the project folder
- Work only inside the project folder. Never look in a parent folder, a sibling folder or anywhere else on the machine unless the user names that folder and asks.
- **If an expected file is missing, stop and ask.** Say which file, where you looked, and wait. Do not search other locations, and do not use an older copy found elsewhere (a copy in a parent folder, an old backup, a previous version) in its place, even if it looks identical.
- If a tool refuses a path outside the project (the settings block the file tools there, but not shell commands), do not work around it with another command or a script. Tell the user which folder is needed and let them add it.
- Those settings do not fence shell commands, so the rule is yours to keep: never run a shell command (ls, grep, rm, Remove-Item, mv, cp and the rest) that reads, lists, moves, copies or deletes anything outside the project folder unless the user names that path in the request.
- **Standing exception, read-only: `F:\Arcade Stick\Data`** (granted 2026-11-09). This is the launcher's real runtime data folder (MAME, ROMs, media, `4RCADE5TICK_files` with the real themes and settings.json). You may list and read anything in it for development and testing without asking (the file tools are fenced there, so use `Get-ChildItem` / `Get-Content` / `Select-String`). **Never create, modify, overwrite, rename, move or delete anything in it** without the user's explicit authorization for that specific change. Copying out of it into the project (for the GUI test sandbox) is reading and is allowed.
- The debug build's `bin\Debug\...\boot.cfg` points at that real folder, so **never launch the debug build for testing**; launch only the sandbox copy below.

### GUI testing (standing authorization, 2026-11-09)
You may launch, drive, close and restart 4RCADE 5TICK and its own windows and dialogs for testing without asking each time, under these rules:
- **Sandbox only.** Test against the disposable copy: `bin\GuiTest\app\` (a copy of the build output whose `boot.cfg` points at `bin\GuiTest\Data`) and `bin\GuiTest\Data\` (a copy of `4RCADE5TICK_files` and `mame.ini`; ROMs and media are not copied). Before any destructive test, confirm the running process's path is under `bin\GuiTest\app` and its `boot.cfg` names `bin\GuiTest\Data`; if not, stop. Refresh the sandbox from the real folder (read-only copy) when a clean state is needed. `bin\` is gitignored, so nothing there is committed.
- **One runner, one command shape.** Run every step as its own `.\bin\GuiTest\gui.ps1 -Do <Action> ...` call (one per tool call: no inline PowerShell blocks, no chaining with `;`, no `cd` prefix), so the allow rule in `.claude/settings.local.json` matches and no prompt appears. The actions are a closed list (`-Do Help`): after `dotnet build` use `-Do Deploy`; for a clean sandbox `-Do ResetData` (with the launcher closed); then `Start`, `OpenBuilder`, `Invoke` / `Select` / `SelectIndex` / `SetText` / `Answer`, `State`, `Stop`. If a test needs something the list lacks, add a small action to the script's `[SECTION: Actions]` (with the same sandbox and path guards) instead of running ad-hoc code.
- **Launcher only.** Drive the launcher's own windows and dialogs (UI Automation by its process id, or input sent to its window). Never read, capture, click or type into any other application's window. A capture is of a launcher window only, never the full screen or a screen region.
- **Check before every action.** Confirm the sandbox launcher process is running and the target window belongs to it before each capture, click or keystroke. If the check fails, abort: capture nothing, send nothing, and report it.
- **Delete what you captured.** Screenshots go only to `bin\GuiTest\shots\`, view only the ones you need, and delete them when the test ends.
- Report exactly which tests ran and their results; never claim a test that did not run.
- Do not use computer use or any browser tool to look at the user's other windows. The built-in browser pane may be used for tasks the user asks for.
- Python runs always prompt the user (an ask rule in `.claude/settings.local.json`); never avoid that prompt by launching Python another way (a full path, `py`, a different shell or a wrapper). GUI test drivers are PowerShell (`bin\GuiTest\gui.ps1`), so they do not need Python.

## Architecture in Brief

Settings and themes
- `ConfigurationSettings` holds live settings (loaded from `4rcade5tick_files/settings.json`) and the per-resolution `Layouts`. It has **no change notification**; layer values reach XAML through `MainViewModel` `Theme*` forwarders, and the flat Global values through `{DynamicResource Global...}` resources published by `GlobalThemeResources.Apply`.
- A theme is a **zip** (`4rcade5tick_files/Themes/<name>.zip`) containing `theme.json` plus bundled `assets/`; loading extracts to `Themes/Extracted/<name>/`. `LoadTheme` extracts to a staging folder first and swaps it in by renaming, and Save and Delete Theme follow the same rule (see Theme file safety under Known Gotchas).
- `LayerRegistry` is the single source of truth for layers, groups, defaults and fonts. Each resolution has a `ThemeLayout` of `LayerLayoutEntry` items (rect, Z, visibility, ImagePath and styling fields).
- **New styling goes on layer entries, per resolution**, not on `ConfigurationSettings`. Old keys in saved files are ignored; there is no legacy migration (the user rebuilds themes).

Windows and views
- `MainWindow` (layout canvas, `ApplyLayout` and its style keys, TreeView templates, hamburger menu), `ThemeBuilderWindow` (hosts the THEME BUILDER and GLOBAL tabs), `ThemeBrowserWindow` (the Theme Browser, the only one since Pass 7: a standalone workspace window opened by the hamburger item through `MainWindow.OpenThemeBrowserWindow`, holding `ThemeBrowserControl`, the styled theme catalog, and `ThemePreviewOverlay` for the enlarged thumbnail preview in a one-tab frame; it loads through `MainWindow.LoadThemeFromBrowser`, themes.md 3.29), `AdvancedThemesTabControl` (layer list, tabbed property pages), `GlobalThemesTabControl` (Global tab: Fonts, Backgrounds, Tabs, Buttons, Scrollbars, Context Menus), `ThemesManagerControl` (the Themes Manager row at the top of THEME BUILDER: theme create/load/save/delete only), the layer controls (`SplashLayerControl`, `VideoPreviewLayerControl`, `GameInfoLayerControl`).
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
4. The DTO builder in `SaveThemeCore` in `ThemesManagerControl`
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
Closing without saving restores layouts to the last saved snapshot; anything stored outside layouts is not covered. Advanced-tab edits are in memory only until Save Adjustments / Save Changes, with two exceptions: creating a genuinely new resolution (no working layout, not in the restore point or the theme file) warns in the New size layout prompt, and removing a resolution warns in the Remove Resolution popup; both discard the unsaved edits and save the loaded theme at once through the shared `RunAutoSavedSizeChange` (themes.md 3.1 New size auto-save and Removing a size, decisions 87 and 88). Layer files (images, video) are bundled into the theme zip through each entry's `ImagePath`. The workspace windows (Options, Sorting, Theme Builder, Theme Browser) have no owner (themes.md 3.4): the builder reaches MainWindow through its `_main` field, never `Owner`. Closing one uses `TrackWorkspaceClose`, which activates MainWindow only when the closed window was active and nothing else of the app took over (never during shutdown or a game, never restoring a minimized MainWindow). Only the modal task dialogs (Manage Custom Folders, ROM copy path) call `MainWindow.BringToFrontAfterChildClose`. Never make any window Topmost. Entering or leaving fullscreen activates MainWindow (`WindowState`), so `RunSizePick` and `RunThemeSwitch` hand the focus back to the workspace window that started them (`RestoreWorkspaceFocus`, themes.md 3.29); a new path that can enter or leave fullscreen from a workspace window must go through one of them. Theme and size changes reach the other open windows only through `MainWindow.NotifyActiveThemeOrSizeChanged` (an outside change reloads the builder; `fromBuilder` refreshes only the Browser's marks) and `NotifySavedThemesChanged` (catalog re-read only while the Browser shows); they only refresh views and never load, save or switch (themes.md 3.28, decision 89).

### Theme Builder window traps (details in themes.md section 7)
- **Never test the top-level Theme Builder tab by index.** Compare the selected tab's `Content` to `ThemeBuilderPage` (the guides and the Background grid show only on THEME BUILDER). `AdvancedTab` now sits inside `ThemeBuilderPage`, so comparing to it is always false.
- **Opening and selecting tabs.** The builder has two tabs, THEME BUILDER and GLOBAL (the THEME BROWSER tab was removed in window management Pass 7). Open or activate the one window through `MainWindow.OpenThemeBuilderWindow()`, which selects THEME BUILDER through `ShowBuilderTab` (also on an open builder, so Ctrl+T returns it from GLOBAL); never select a tab by index. The Theme Browser is its own window (`MainWindow.OpenThemeBrowserWindow`); do not add a Browser back into the builder.
- **Border ring rule (decisions 57, 59 and 62).** On Video Preview, Splash Image, Shape Layers 01 to 04, custom layers and both Random Game Display states, Width / Height are the content size and the border is drawn **outside** it (grows outward by `BorderSize`); X / Y are the ring's **outer top-left corner**, so the content sits at (X + B, Y + B) and the outer box is (X, Y, W + 2B, H + 2B). Code that needs the content area must read the entry (W / H, origin X + B, Y + B), never the control's Canvas size; the guides use the outer box (`GetGuideRect`, `LayerRegistry.HasOutsideBorder`). Use `PlaceOutsideBorder` / `ApplyRingBox` in `MainWindow.xaml.cs` for a new bordered layer instead of copying the arithmetic; do not move it into the shared positioning loop. The no-art banner card has no rect of its own (its ring is centered in the Marquee rect; the Marquee guides stay on the Marquee rect), and the Search & Buttons row is exempt (own border handling).
- **Background grid (session-only editor aid, decision 58):** `BackgroundGridLayer` draws one `DrawRectangle` per line, snapped to whole physical pixels and rounded half-up. Never switch to a combined geometry or a tiled `DrawingBrush` (both failed on screen). While on it owns local values on `RootLayoutGrid.Background` and `MainBgVideoHost.Visibility`; set them nowhere else. It is deliberately not in `StopPreviews`. Offscreen renders use the software renderer and do not clear a drawing change; the on-screen test decides.
- **Random Game Display message:** an empty `TextMessage` is a saved choice, so fill it with `??=`, never `IsNullOrWhiteSpace`. Background, border, clip and wrapped width come from the **active state's** part, and code picking the state uses `RandomDisplayFromSplash`, not `_randomSpinFromSplash`.
- **Marquee banner card** is a `ControlTemplate` (`MarqueeBannerCardTemplate`): its elements bind view model forwarders directly and read the title through `TemplatedParent`; a DataTemplate would lose the DataContext.
- **Global resources are typed** (font sizes `double`, border sizes `Thickness`, radii `CornerRadius`, colors `Brush`); a wrong type fails silently. **Do not set Background, Foreground, BorderBrush or BorderThickness on a `ThemedActionButtonStyle` button** (the one intended exception is the active saved-size button, which may set Background and Foreground, the hover pair, and nothing else). **Chrome Palette:** a new fixed color is a named brush in `[SECTION: Chrome Palette]`, never a hex inside a style, and App.xaml gets no implicit (keyless) styles.
- **Tooltips:** the styled tooltips are the Layers "?" and the media tabs' "?" glyphs (the file "?" on the shape / custom Content, Splash Image, Random Game Display Content, Marquee Logo and Main Window Background tabs, and the file and Background color "?" on Video Preview's Fallback Image tab): each a local `ToolTip` on its `LayersHelpGlyphStyle` glyph, using the `ChromeTooltip*` brushes with inline Bold, Italic and Run text. Never add a global or implicit ToolTip style. The file "?" deliberately omits JPEG although `BtnBrowseShapeFile_Click` still accepts `.jpeg`; do not "fix" it, and revisit the tooltip if the filter changes.
- **Theme Builder geometry (alignment standard, decision 82; details in themes.md 3.25 and section 7):**
  - Widths: small numeric boxes 52 (Border Size and Radius included), hex boxes 80, Font combos 170, Alignment combos 110 at x 178. Exceptions: opacity 43, custom resolution 70, the Content tab's background hex 104, the Buttons hover hex 140, and the Position & Size boxes (star columns, `MinWidth` 50, decisions 79 and 80). Never stretch any other box to fill a column.
  - Anchors from the tab content's left edge: 0, 60 (color group after a Size box), 118, 170 (shared right edge), 178 (Depth, Radius, Alignment, Background), 230, 236. Compact family grid `110 8 52 8 52` (8px gaps); a color group is 26 + 4 + 80 = 110.
  - Layout rounding: a Grid with `UseLayoutRounding` (own or inherited), no `*` column and spare width renders about 1px wider per column. Give it `HorizontalAlignment="Left"`, or a trailing `*` column when a label must run past the last fixed column (the Text Marquee Color / Max size grid). Never on a grid with a `*` column or stretching children, and never turn layout rounding off.
  - New color swatch Borders get `SnapsToDevicePixels="True"`. The "Active Theme:", "Resolutions:" and "New Resolution:" labels share `MinWidth` 100 (not `Width`: the font size is themeable); the last two also share one column (`SharedSizeGroup="ResolutionLabel"`), so they stay aligned at any Sub-Header size.
  - Deliberately left: Position & Size layouts, the dividers (`0,15,0,10`; Favorites Star `0,10,0,10`), the standalone Search Label and Marquee Shadow toggles, the label-left Scrollbars row and Global grids, "Star size:" label-left, the multi-select panel margin.

### Scrollbar overrides (themes.md 3.24)
- The Games List and Game Info scrollbars can override `Global.Scroll.*` by putting the same keys, with the same types, in `GamesListHost` and `GameInfoLayer.PartInfoBody` resources, through `GlobalThemeResources.ApplyScrollOverride` only. Never copy the scrollbar template, and never put these keys anywhere else.
- **Scrollbar clearance (themes.md 3.27):** a new scrollbar inside a `TabContentBorder` host (Options, Theme Builder, Filter) opts in with `services:ScrollbarClearance.Enabled="True"`, which keeps its track off the rounded corner live (bar margin only). A container margin around the ScrollViewer counts as an existing gap (a 2 px margin already meets the baseline), so keep layout margins as they are, but never add a hand-tuned gap to clear the rounded corner. MainWindow scrollbars never opt in. Do not edit the shared scrollbar templates for clearance.
- The Scrollbars layer is settings-only (`IsSettingsOnly`): it is pinned below the Main Window Background, draws nothing and takes no Z (`NormalizeZ` and `MoveSelectedLayer` skip it). A new non-positionable layer must not pick up the Main Window Background's fill values in `BuildDefaultLayout`.

### Layer list marker and separator (decision 77)
- The bold "|" on renamable rows is drawn by `LayerListItemTemplate` from `ShowRenameMarker` (`LayerRegistry.CanRename`) and is never part of `DisplayName` (the Remove Layer prompt reads `DisplayName`). The marker is blue (`ChromeLayerRenameMarker`) on shape layers and magenta (`ChromeLayerCustomMarker`, driven by `IsCustomLayer`) on custom layers, drawn 1 px higher by a render transform. The Layers "?" tooltip reuses both brush keys; change the colors only in the Chrome Palette.
- The separator is a line in the shared `ChromeListBoxItemStyle`, shown by Tag `PinnedBlockStart` on the first pinned row; the list has no extra separator item. Pinned rows (Main Window Background, then Scrollbars) cannot move and layers cannot pass them.

### Theme Info (themes.md 3.22)
- The `ThemeInfo` block lives on both `ThemeSettings` and `ConfigurationSettings.Info`, and the Theme Builder's restore point holds a copy. `LoadTheme` uses a blank block for a theme without one, so values never carry over. A normal launch gets the block through its line in `ApplyThemeSettings`.
- A capture stays in memory (`PendingThemeThumbnail`) until Save Adjustments or Save Changes writes it. Only Save Changes sets `ModifiedUtc`, and the live value moves only after the zip is written.
- `ThemeThumbnailStore.RemoveUnusedFolders` runs only after a successful save, right after the restore point moves, and keeps the live and restore point folders. Do not call it from a load or close.
- The four display controls hold content only (no headings or glyphs) so the Theme Browser can reuse them.
- Thumbnail box sizes are constants in `ThemeThumbnailStore` `[SECTION: Thumbnail Sizes]` (`Capture*`, `Info*`, `Browser*`, `PopOut*` Max sizes). A capture fits inside the capture box (physical pixels, 96 DPI) and never upscales; display boxes are maximum DIP boxes shown with `StretchDirection="DownOnly"`.
- **Show Theme In** is `ThemeInfo.ShowIn`, text with the `ThemeShowIn` constants (`Both`, `BrowserOnly`, `MenuOnly`, `Neither`); null or unknown reads as Both, so readers go through `ThemeShowIn.Normalize`. Its label and combo live in the host view, not in a display control.
- The hamburger menu reads each theme zip through `ThemeShowInReader` (saved values only, so a change counts after Save Changes), always shows the active theme, and applies no filter when no theme is active. The Load Theme combo and the duplicate-name check (`GetThemeNames`, `FindNewThemeNameProblem`) stay unfiltered.

### Smart fullscreen (themes.md 3.17 to 3.19)
- **app.manifest is Per-Monitor V2.** Do not remove it or go DPI-unaware: physical sizes and DpiChanged depend on it.
- **Display override.** Code that shows the layout uses `ResolveDisplayLayout` / `DisplayLayoutWidth/Height`; code that edits, saves or snapshots uses `ActiveResolution*`. Never write the override to settings, layouts, snapshots or theme.json. Never hand `SavedThemeLayouts` to `ResolveLayout` (it fills gaps in place).
- **Local values** on `RootLayoutGrid` (LayoutTransform, Width/Height, alignment, rounding) and the Window background are cleared in `EndSmartFullscreen` before `SizeToContent` returns.
- **Popup scale.** Menus and popups use m = max(1, floor(s)) / dpiScale (`SmartFullscreen.PopupFactor`, the whole-number part of the canvas scale), the canvas keeps f = s / dpiScale; they differ only for a fractional fallback s. A Popup inside a visual tree inherits its ancestors' transform, so submenus, folder popups and the picker dropdown must not add `Launcher.PopupScale` (it doubles, or gives f times m). Only parentless popups set it: top-level ContextMenus and hand-opened or code-built tooltips (`DelayedTooltipTimer_Tick`, the ROM copy tooltip). The one exception: the two folder popup Borders (inside RootLayoutGrid, inheriting f) read `Launcher.InheritedPopupScale` (m / f) so they land on m, and Create folder's offsets are multiplied by `_inheritedPopupFactor`; nothing inside them gets a scale. Only `ApplySmartFullscreen` / `EndSmartFullscreen` write these resources. Never scale elements of a third-party control by Name.
- **Auto-entered flow and entry size.** `_smartFullscreenAutoEntered` is cleared by every manual toggle and by `EndSmartFullscreen` from any cause. `_fullscreenEntrySize` is the active size when fullscreen was entered (set by `EnterFullscreen` after the size test, by the auto-enter with the size before its pick, and by the Theme Builder open's adopt); a pick that stays fullscreen keeps it, a theme switch and `EndSmartFullscreen` clear it; F11 out restores it and the app close in fullscreen saves it to settings.json. Opening the Theme Builder while Smart fullscreen shows another size first makes that size the active size with `SwitchActiveLayoutSize(..., persist: false)`, before the builder exists (no settings.json write, not a `RunSizePick`), then sets the builder's size restore point back to the pre-adopt size (`ThemeBuilderWindow.SetSizeRestorePoint`) so closing without saving returns to it. In any fullscreen a size pick stays Smart only on an exact fit for the monitor, otherwise it drops to windowed at the picked size. Every size pick (hamburger Layout size, the builder's Theme sizes buttons and size combo through `SizePickRunner`) goes through `MainWindow.RunSizePick`, which sets `_deferBuilderDrop` and then runs `HandleLayoutSizePickForFullscreen`; every theme switch (hamburger, the builder's Load button through `ThemeLoadRunner`, the Theme Browser's resolution buttons through `ThemeBuilderWindow.LoadThemeFromBrowser`) goes through `MainWindow.RunThemeSwitch`, which sets the defer too. Theme-only loads (hamburger Themes, Load, New Theme) use the theme switch rules (`HandleThemeSwitchForFullscreen`: never enter fullscreen from a window). A Browser load picks a size, so it passes `explicitSize: true` and gets the size pick rules (`HandleExplicitSizeLoadForFullscreen` → the unchanged `HandleLayoutSizePickForFullscreen`): an exact 1x monitor fit enters or keeps Smart fullscreen, anything else ends windowed at that size; after an auto-enter the F11-out size must exist in the loaded theme (else its saved size, `LoadedThemeSavedSize`, else none). Do not route theme-only loads through the size pick rules. A visible Theme Builder limits Smart fullscreen to the active size: the check at the top of `ApplyLayout` reruns `ApplySmartFullscreen` on a mismatch (keep the re-entrant `return`), skipped while `_deferBuilderDrop` is set.
- **Sub-windows** call `WindowFit.KeepInWorkArea` so they fit the monitor's work area at any Windows scale.
- **Theme Builder menus** (the layer list's Copy / Paste, decision 67) use the keyed App.xaml styles `BuilderContextMenuStyle` / `BuilderMenuItemStyle` / `BuilderMenuSeparatorStyle` (`Global.Menu.*` only, no `Launcher.*`), set explicitly on the menu, every item and every separator (a menu restyles a Separator from `MenuItem.SeparatorStyleKey` otherwise); MainWindow's implicit menu styles never reach the builder. The Random Game Display row copies and pastes on `_guidePartId` (first part as fallback).
- **Menu icon gutter.** The Windows ContextMenu template draws a light icon gutter that filled rows don't fully hide (a 1 px left edge line, plus stubs at fractional-pixel seams). The New Theme menu hides it on `Loaded` with `HideMenuGutter`; the layer list's Copy / Paste menu still shows it (known, not applied).
- **Color picker dropdown** is PortableColorPicker's internal popup (`FindName("popup")`): measure `DesiredSize`, not `ActualHeight` (clamped to the screen), and re-measure after moving it.

### Media loading (decision 42, 2026-10-21)
The `SelectedGame` setter runs the instant part (marquee, `MarqueeMissing`, flyer/title/snap/cabinet, the video slot's static image, `HasActiveMedia`): file checks on the UI thread, decoding on a background task guarded by `_mediaSelectionVersion`, plus a 30-entry decoded-image cache. **Only `VideoSourcePath` stays behind the 600 ms debounce.** `ResolvePreviewMedia()` is the single two-pass preview resolver. Do not move `HasActiveMedia` or `MarqueeMissing` back behind the debounce (that was the original "no preview flash" bug). Anything that sets `MarqueeImage` to null for "nothing selected" must also clear `MarqueeMissing`; use `MarqueeMissing`, never `HasMarqueeImage`, for the no-art banner.

### Theme file safety (themes.md 3.4, section 7, decisions 71 to 73)
- **Nothing is deleted or replaced until its replacement is confirmed good, and a failure leaves the old files and memory as they were.** Do not add a delete-first path to Load, Save or Delete Theme.
- **Load:** `LoadTheme` checks the zip, extracts and parses it in `Themes\_staging`, then swaps the folder with `ThemeFolderSwap` (rename aside, rename into place, rollback). A failure throws `ThemeLoadException`; callers show its message as "Theme Not Loaded". An open file in the theme's folder refuses the load, including an open File Explorer window, so a new layer that holds a file open must be released in `ReleaseCustomLayers`.
- **Save:** bundles into a staging folder in Temp, asks first if files are missing (No by default), writes `<name>.zip.tmp-<guid>` in `Themes`, verifies it, then swaps it in with `ThemeFileStore.ReplaceZip`, keeping `<name>.zip.bak`. Layouts, the restore point, theme names, the thumbnail write and `ModifiedUtc` move only after the swap (the one earlier change: `ResolveLayout` fills missing values on the live active layout, which changes nothing visible).
- **Delete Theme:** `ThemeFileStore.DeleteTheme` renames the working folder aside, then moves the zip to `Themes\Deleted` (not the Recycle Bin: removable drives have none), with rollback. Deleting the loaded theme clears `LoadedThemeName` and `ActiveThemeName`, so the next launch loads `default`.
- **`LoadedThemeName`** (`MainViewModel`) names the theme whose values are in memory. Save refuses any other existing theme, because the zip name comes from the dropdown but the data comes from memory. Set it only in the same step as the layouts.
- **Theme names become paths only through `ThemePaths`** (decision 73). A typed new name gets Check 1 (strict rules); every name gets Check 2 (resolved-path backstop) inside each path. Never apply Check 1 to an existing theme's name.
- **A typed name never overwrites** (decision 74). The New Theme name popup refuses an existing name in any case and `default`, with no overwrite prompt; it is the only place that refuses `default`.
- **Templates are read-only input** (themes.md 3.23, decision 75). `4rcade5tick_files\Templates\*.json` are never created, written, renamed or deleted by the app. A template creation never goes through `SaveThemeCore` or the saved callback (`ThemeCreatedFlash` shows its flash): nothing live changes until its load succeeds.
- **Stored asset paths:** only a path starting with `assets/` or `assets\` is inside the zip (`RewriteThemeAssetPath`). Anything else stays exactly as stored; prefixing it is what made paths grow on every save and load.
- **Keep the active layout object across a save.** `_currentLayout` adds and removes custom layers through it; only its `Layers` list may change.
- **Theme lists read only `*.zip` directly in `Themes`,** which keeps `.bak` and `.tmp-` files, `_staging` and `Deleted` out of them. Keep it that way.
- **`4rcade5tick_files/logs/theme.log`** (`ThemeLog`) has one line per Load, Save, Delete, SaveBlocked, SwapRollback, PathRefused or Create, plus TemplateSkipped and TemplateWarning once per session and BrowserSkipped once per zip file state per session (a rollback that succeeds during Delete logs as "Delete failed"). Check it first after any theme file incident.
- **Theme and size in one load** (themes.md decision 83): `LoadTheme(name, targetSize)` takes an optional target size, applied only when the theme has a layout at that exact size, so a Theme Browser click loads the theme and its resolution as one operation (no size switch afterwards). A Browser click on the active theme at the active size does nothing (no load, save or log; decision 83). After a Browser load, a failed settings.json write is reported and logged separately and does not skip the Smart fullscreen handling. A theme-only load takes the theme's saved size and the theme switch fullscreen rules; a Browser load with an explicit size takes the size pick fullscreen rules (see Smart fullscreen).
- **Theme Browser catalog** (`ThemeCatalogReader`, themes.md 3.26) reads zips only through short-lived `ZipFile.OpenRead` calls: never keep an archive or entry stream open, because it blocks Save's `File.Replace` and Delete's `File.Move` (which does not retry). Thumbnail bytes are copied to memory and the zip closed before decoding, for the list tiles and for the enlarged preview's on-demand full-size read (`ReadFullThumbnail`: the same entry lookup, a 16 MB limit, never cached). It never reads `Themes\Extracted` or `Themes\Thumbnails` (saved-zip data only), never follows a rooted or `..` thumbnail path, and logs an unreadable zip as `BrowserSkipped` once per file state.

### Restore Defaults (decision 70)
The sets come from the default theme's `theme.json` through the embedded `Models/DefaultLayerSets.json`. If the default theme changes, regenerate that file by hand (no tool exists). Everything needs exactly one file in `4rcade5tick_files/assets` for each of the names `default_background`, `default_logo`, `default_splash` and `no_preview`, in any type that layer supports; a missing one leaves that image empty and is named in the warning.

### Default images (themes.md decision 70, change log 2026-11-03)
- An empty image path shows nothing (the background shows its color; the logo and splash show nothing). Do not add built-in fallback images for theme layers. The one exception is `no_preview.png` on the Video Preview layer (`ThemeMissingPreviewAsset`).
- Restore Defaults finds each default by exact name (case-insensitive, name part only, so `default_splash2.gif` is ignored) among the types that layer supports. Several matches: it uses the fixed priority order png, jpg, jpeg, gif, bmp, mp4 and warns. No match: the path stays empty and one warning lists everything missing. Layout only never touches image paths.
- Never hard-code a default image file name in code. The extension in a `DefaultLayerSets.json` value (for example `assets/default_splash.gif`) is only a hint and is stripped.
- A default image for a new layer needs its `assets/<name>.<ext>` value in both sets of `DefaultLayerSets.json` and the layer's supported types in `[SECTION: Default Image Lookup]` in `Models/DefaultLayerSets.cs`.

### Release checklist
- `4rcade5tick_files/assets` has exactly one file, in any supported type, for each of `default_background`, `default_logo`, `default_splash` and `no_preview` (Restore Defaults), plus `bmac.png` (the About tab button image).
- The shipped Default theme has the two 16:9 sizes (1280x720 and 1920x1080).
- `Templates\<name>.json` ships in the data folder, outside the repo.
- The first template, `template.json`, is renamed before release, because the menu label is the file name (for example `Blank.json`).
- The shipped template has all image paths empty and "Disable background image" ticked at every size.
- Runtime files are not shipped: `theme.log`, `.bak` files and `Themes\Deleted`. `*.log` is already in `.gitignore`; `.bak` and `Deleted` live in the data folder outside the repo, so they are a shipping concern, not a commit risk.

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

Built: Main Window Background (Image or Video tab: wallpaper and color), Games List (folders, game titles), Shape Layers 01 to 04 and custom layers, Marquee (Logo, Text Marquee and Shadow tabs: splash logo, the no-art title card shared by the idle banner and the spin's no-marquee card, image shadow), Splash Image, Video Preview (Fallback Image tab with the background color, Border tab), Search & Buttons, Game Info (title, publisher, info headers and body, navigation arrows and dots, artwork previews shadow), Random Game Display (text, background, border, overlay card in both states, per-part ratio lock). Also: a Layers header with a ? help tooltip above the layer list, right-click Copy / Paste of position, size and border between layers (decisions 66 and 67), layer rename, multi-select nudge, group move, alignment guides (Edge guides and Center guides, running edge to edge of the window), and a session-only Background grid (spacing and X / Y offset) for lining layers up; every bordered layer draws its border outside its Width / Height with X / Y at the ring's outer corner (Video Preview as the model). **Global tab** (theme-wide, not per resolution): Fonts, Backgrounds, Tabs, Buttons, Scrollbars (colors and width), Context Menus (colors and border), each with Restore Defaults, live through Global resources, reverted on close without saving. The input chrome (text boxes, combos, lists, checkboxes, sliders, picker popups, small dialogs) uses the fixed Chrome Palette in App.xaml. The Themes Manager (theme create/load/save/delete) is a row at the top of THEME BUILDER; the standalone Theme Browser window (`ThemeBrowserWindow`, window management Passes 6 and 7; the builder no longer has a Browser tab) hosts `ThemeBrowserControl`, which shows the catalog of the saved theme zips as styled two-column entries (a resolution button loads that theme at that size, a click on the active theme and resolution does nothing; a thumbnail click opens an enlarged preview over the dimmed Browser window, closed by any click or Escape). A Browser load still drops unsaved builder edits without a prompt; whether to confirm first is a deferred design decision (themes.md 3.28). Its visuals use Global resources only (Header, Sub-Header, Standard, Sub-Text, Options border, Separator, Buttons), and every entry layout number lives in one constants block in `ThemeBrowserControl.xaml.cs` (themes.md 3.26, decision 84) (Global tab migration Passes 1 to 6, `global-tab-plan.md`, then 2026-10-22).
- Play Button (Game Info part): launches the selected game; text, fill, border, hover colors and separate text and button shadows.
- Favorites Star (Game Info part): solid or outline glyph that toggles the selected game's favorite; size and each state's color and shadow.
- Theme Info view (themes.md 3.22): the default content while no layer is selected, with a captured thumbnail, author, description, a Show Theme In combo (Both, Theme Browser only, Hamburger menu only, Neither; filters the hamburger Themes list) and last saved date.
- Restore Defaults on the layer list (decision 70): a pop-up with Layout only, Everything and Cancel, applying the 720p or 1080p set to the active size only.
- Safe theme file handling (see Theme file safety): Load, Save and Delete Theme never remove the old files before the replacement is confirmed, with `theme.log` recording each action.
- New Theme (themes.md 3.23): the button's menu offers Copy Current Theme and any `Templates\*.json` (a Templates heading above the templates), through a name popup that never overwrites.
- Layer list (decision 77): a bold "|" marker on renamable layers (blue on shape layers, magenta on custom layers) and a separator line above the pinned rows.
- Scrollbars row (themes.md 3.24, decision 78): a pinned, settings-only row with per-resolution scrollbar overrides for the Games List and Game Info.
- Tab polish (decision 81): renamed and reordered tabs, the compact Drop shadow recipe, themed dividers and the media tabs' "?" tooltips.
- Alignment standard (themes.md 3.25, decision 82): one set of widths, gaps and anchors across the layer and Global tabs.

Smart fullscreen (themes.md 3.17 to 3.19, decisions 63 and 64) is built and tested on a 1080p monitor at 100/125/150/175%; 1440p, 4K and mixed-DPI monitor moves are untested on hardware. Remaining: replacing registry defaults with the finished default theme. The Footer is intentionally not themed (it stays as is). A WYSIWYG edit mode is planned for beta.5 (no drag handles in beta.4).

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
