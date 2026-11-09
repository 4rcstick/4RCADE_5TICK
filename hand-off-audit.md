# Hand-off: Theme Browser — read-only audit (STEP 1 only)

**Read this whole file first. This is an investigation. Do NOT edit, create, rename or delete any file, do NOT commit, and do NOT write implementation code.** Report your findings, then **stop and wait**. Implementation handoffs come later, after the user and Claude (chat) have reviewed your report.

Follow `CLAUDE.md` and `themes.md` as usual (code wins over themes.md where they disagree: report each mismatch). Stay inside the project folder.

---

## 1. Goal and why

4RCADE 5TICK's THEME BROWSER tab (today an empty `<Grid x:Name="ThemeBrowserTab"/>` in `ThemeBuilderWindow.xaml`) will become a scrolling, alphabetical catalog of installed theme ZIPs. Each entry has a thumbnail, the theme name, resolution buttons, author and description. A resolution button loads **that theme at that resolution** and persists the choice. Clicking a thumbnail opens a dimmed, modal enlarged preview.

We want to reuse the existing, tested systems (theme load and persist, Theme Info display controls, Global resources, Smart fullscreen rules) rather than build parallel ones. This audit tells us exactly what exists, what can be reused as is, what needs a small seam, and what the risks are, so the implementation handoffs can be precise.

## 2. Settled decisions (do not re-open; audit against them)

**Catalog**
- Lives as a tab in the existing Theme Builder window (717 x 900). The hamburger menu gets a **Theme Browser** command that opens or activates the shared window on the Browser tab. Hamburger **Theme Builder** opens or activates it on the Builder tab. Never a duplicate window. No keyboard shortcut to open.
- Alphabetical by theme name, no cap, scrollable, no pinning of the active theme.
- Built from `*.zip` directly in `Themes`, reading the central directory and `theme.json` **without extracting** the theme. Invalid or unreadable entries are skipped (log through the existing handling; no per-entry dialogs).
- **Saved-ZIP data only.** The Browser shows saved `theme.json` metadata, resolutions and thumbnail, never unsaved working state (even for the active theme).
- Respects **Show Theme In** (`Both / BrowserOnly / MenuOnly / Neither`). Unlike the hamburger menu, the Browser does **not** force-show the active theme: if the active theme is hidden from the Browser, no entry is marked.
- Refresh the catalog when the Browser tab is switched to (and when opened onto it). No file watcher, no Refresh button. Switching tabs, refreshing, hovering and the enlarged preview must never load a theme, save anything or discard Builder edits.
- Keep scroll position across a theme load, live restyling and (where reasonable) catalog refresh.
- Browser styling follows the **currently active theme's Global resources**, live.
- Existing bottom button row (Save Adjustments / Close Panel) stays window-level and shared; loading never closes the window.

**Entry layout** (user's Photoshop mockup wins over any description here)
- Left column: thumbnail, vertically centered in the entry. Right column, left-aligned to one edge, in order: THEME NAME (display-only ALL CAPS, Global Header) with a non-interactive check-circle glyph immediately **after** the name for the active theme; `Resolutions:` + buttons; `Author:` + value; `Description:` + body on the same line, wrapping naturally. Labels use Global Sub-Header; values and body use Global Standard Text. Global Divider between entries (none above the first or below the last).
- Missing author: blank. Missing description: the Builder's "No description" fallback with its existing sub-text look. Missing thumbnail: the Builder's existing No Image box. Description rules: exactly the Theme Builder's existing ones.
- One intro line above the list (stationary): *"Select a resolution to load a theme, and click a thumbnail to enlarge the preview."* in the existing Theme Builder instructional-text style.

**Resolution buttons**
- Same dimensions and Global Button look and hover as the Theme Info resolution buttons. One button per resolution stored in that theme. Only the button for the active theme AND active resolution uses the existing selected styling.
- A click is a **load command**, never a no-op:

| Current | Click | Result |
|---|---|---|
| A / 720p | B / 720p | load B, keep 720p |
| A / 720p | B / 1080p | load B, switch to 1080p |
| A / 720p | A / 1080p | reload A (saved) at 1080p, discard unsaved edits |
| A / 720p | A / 720p | reload saved A/720p, discard unsaved edits |

- On click: load the saved theme config for the chosen resolution, replacing the whole working state (all layers, all Global values, Info), **silently discarding unsaved edits** (decided: consistent with Load, hamburger swap and New Theme; a dirty-state tracker and warnings come later). Change runtime resolution if needed through the existing Smart fullscreen rules. **Persist the chosen theme and resolution to `settings.json` automatically** (no Save Adjustments needed). Never write the ZIP or its `theme.json` from a Browser load. Update the check, the selected button and the Browser styling immediately. Keep the Browser open. A failed load must not be saved or presented as successful (use the existing error handling).

**Thumbnails**
- One thumbnail per theme (stored once, shared by all resolutions). New capture dimensions change to a **bounding box of 480 x 270 physical pixels**.
- **480 x 270 (pop-out), 320 x 180 (Theme Info), 160 x 90 (Browser) are all maximum bounding boxes in WPF DIPs.** Images are fit inside and centered, **never stretched, cropped or upscaled**, for every aspect ratio (16:9, 4:3, 3:4, others). The capture itself is scaled to fit within 480 x 270 keeping the layout's aspect.
- Smaller displays use `BitmapScalingMode.Fant`. No migration of older 640 x 360 (or 360-tall) thumbnails; they just display through the normal path. No custom per-monitor DPI logic (sub-windows follow normal Windows DPI scaling). Capture and display sizes must be named constants in one obvious place.

**Browser thumbnail and pop-out**
- Browser thumbnail: border = Global Border Color, thickness = Global Border Size, radius 0, no extra padding. Hover: the image area gets a black 60% overlay, a centered 32 x 32 magnifier-plus glyph in `#FFE8E8E8`, a hand cursor, removed on leave, no animation. Click opens the pop-out.
- Pop-out: image fit inside 480 x 270 DIPs, 8 DIP padding, fill = Global Background Color, outer border = Global Border Color / Size / Radius (radius on the outer frame only). Floats above the list (no row reflow), horizontally centered over the metadata-column area (vertical placement is for a live visual polish pass; default to vertically centered in the visible viewport).
- Full-window dim layer from day one: 30% black initially, opacity easy to change (including 0%) in code, covering the whole window content (tab strip, list, Save Adjustments, Close Panel), preview itself undimmed. While open: no click-through, no mouse-wheel scrolling of the list, **no keyboard activation of the controls beneath** (a focused resolution button must not fire on Enter or Space). Closes on any mouse click (swallowed) and on Escape. No X button. Closing never closes the window, loads or saves anything, and keeps scroll.
- Any new fixed color (the 60% hover black, the 30% dim, `#FFE8E8E8`) must be a **named brush in the Chrome Palette**, never a hex inside a style (see `CLAUDE.md`).

**Out of scope for the whole feature (do not audit or redesign):** main-launcher startup windowed/fullscreen resolution fallback; the separate "Theme sizes:" to "Resolutions:" and "Resolution:" to "New Resolution:" Theme Builder label task (just note anything in your findings that touches it); any new user-editable Theme Builder style property.

## 3. What to investigate

For every item report: **what exists** (file, class, method, line area), **what the behavior really is**, **reuse verdict** (as is / needs a small seam / not reusable), **risks or traps**, and any **mismatch with `themes.md` or the spec above**. Quote exact signatures and the few lines that matter; do not paste whole files.

### A. Window, tabs and hamburger navigation
1. `MainWindow.OpenThemeBuilderWindow` and the hamburger menu wiring for Theme Builder: how the window is created and shown (modeless `Show` or `ShowDialog`), how `_themeBuilderWindow` is tracked, what happens when it is already open, and how the Smart fullscreen adopt/restore logic hangs off open and close. Can it be asked to open or activate on a given tab without breaking that logic?
2. Where hamburger items are built (the Themes list, `HamburgerThemeItem_Click`, Layout size) and the cleanest place for a new "Theme Browser" item next to "Theme Builder".
3. `ThemeBuilderWindow` tab handling: the `SelectionChanged` handler and its guards (guides, grid, `StopPreviews`), `ThemeBrowserTab` placeholder, how selecting the Browser tab interacts with them, and what a "tab shown" hook for catalog refresh should attach to (also when the window is opened directly onto the Browser tab, and when the hamburger Theme Browser item is chosen while the window is already open on that tab).
4. Does the window handle Escape anywhere today (`KeyDown`/`PreviewKeyDown`, `IsCancel` on a button, `InputBindings`)? List every handler the pop-out's Escape could conflict with, including MainWindow's fullscreen Escape.
5. The window's root layout: the outer `Grid` with the TabControl in row 0 and the Save Adjustments / Close Panel row in row 1. Confirm a window-level overlay spanning both rows is feasible, and how `ThemedTabControlStyle` tab content, `Global.Options.BorderSize` and the 15 px margin affect usable list width (state the real usable content width in DIPs at 100%).
6. What the Save Adjustments button does when the Browser tab is selected (it saves Builder state to settings.json), and whether anything on that path is a problem if a user clicks it there. Report only; do not propose to change it.

### B. Theme load and persistence (most important)
7. Trace the three existing paths end to end, with the exact order of operations, and tabulate their differences:
   - Builder **Load** button: `ThemesManagerControl.BtnLoadTheme_Click` -> `ThemeLoadRunner` -> `MainWindow.RunThemeSwitch` -> `MainViewModel.LoadTheme` -> `_refreshOptionsBindings` (`RefreshAfterThemeLoad`) -> `_persistToDisk` (`PersistSettingsToDisk`).
   - **Hamburger** theme item: `HamburgerThemeItem_Click`, including how it persists and how it calls `ThemeBuilderWindow.RefreshAfterExternalThemeSwitch`.
   - **New Theme** `LoadCreatedTheme`.
   Include `HandleThemeSwitchForFullscreen`, `_deferBuilderDrop`, `ThemeLoadException` and generic-exception handling, and what the user sees on failure.
8. `MainViewModel.LoadTheme` / `LoadThemeCore`: what it replaces (layouts, Global values, Info, `SavedThemeLayouts`, custom layers, media), what it sets for `ActiveThemeName`, `LoadedThemeName` and the active size (`ActiveWidth/ActiveHeight` from the zip), and what it persists itself (if anything).
9. **Reloading the already-loaded theme** (A -> A, same or different size). Does `LoadTheme` work when the target is the theme whose files are currently in use (staging, folder swap, "an open file in the theme's folder refuses the load", `ReleaseThemeFiles` / `ReleaseCustomLayers`)? Is there any existing precedent or guard for it? This must work as an intentional full reload.
10. **Theme + resolution as one operation.** `LoadTheme` sets the active size to the theme's saved size. Determine the least invasive way to load theme B directly at resolution R: a new optional target-size parameter, or `RunThemeSwitch` followed by a size switch, or something else. Examine `RunSizePick`, `SwitchActiveLayoutSize` (and its `persist` parameter), `HandleLayoutSizePickForFullscreen`, and how `_deferBuilderDrop` / Smart fullscreen would behave for the combined case (flash at an intermediate size? wrong auto-drop to windowed?). Identify what happens if the requested resolution has no layout in the loaded theme (it should not happen from the Browser, but state the behavior).
11. **Restore points.** Trace `_layoutsAtLastSave`, `_resolutionWidthAtLastSave/HeightAtLastSave`, `_globalAtLastSave`, `_infoAtLastSave`, `SetSizeRestorePoint`, `RefreshAfterThemeLoad`, and the Closed handler. Confirm that after a Browser load at resolution R (different from the theme's saved size), closing the window without saving would currently revert the active size, and describe exactly what must change so the persisted selection survives. Also confirm the Closed handler's revert is harmless right after a Browser load.
12. `settings.json` persistence: what exactly `PersistSettingsToDisk` serializes, whether any unsaved Builder state (pending thumbnail, pending custom size, Info edits) could leak into settings.json on a Browser load, and how a persisted active theme plus resolution is read back at the next launch. Confirm which fields mean "selected theme" and "selected resolution" (`ActiveThemeName`, `ActiveResolutionWidth/Height`, anything else).
13. Is `LoadTheme` synchronous on the UI thread? Roughly how long can it block? What stops a double click from firing two loads? Recommend the lightest guard (ignore clicks while a load is running).
14. After any load: which UI must be refreshed so the Builder side is not left stale (Themes Manager combo `CboThemePresets`, Advanced tab, Global tab, Theme Info, Theme Saved flash) and whether `RefreshAfterExternalThemeSwitch` already covers it when the load starts from the Browser tab.

### C. Catalog reading
15. Existing zip-reading code to reuse: `ThemeShowInReader.Read`, `ThemeFileStore.VerifyZip`, `ExtractThumbnailFromZip`, `ThemePaths`, `ThemeLog`, the `theme.json` / legacy `theme.cfg` entry names. How does the app deserialize `theme.json` (`ThemeSettings`, `JsonSerializerOptions`), and what is the cheapest way to read only Info + layout sizes + active size from a zip entry stream without a full parse of everything, or is a full parse of one JSON entry per zip acceptable? State how the existing code names the entry and handles a missing or malformed one.
16. The data needed per entry: display name (zip file name vs anything in `theme.json`; confirm the theme's identity is the file name), `Info.Author`, `Info.Description`, `Info.ThumbnailPath`, `Info.ShowIn` (via `ThemeShowIn.Normalize` / `ShowsInThemeBrowser`), and the list of saved resolutions (`Layouts` widths and heights). Confirm what "all resolutions contained in the theme" means in the file (is every layout in `Layouts` a real size? any placeholder or settings-only entries?).
17. How the Theme Builder's **Theme sizes** row (`PnlSavedSizes` / `BtnSavedSize_Click` / `RefreshSavedSizes`) orders and labels its buttons (format such as `1280x720`, ordering, custom sizes, the active/selected styling and how it is applied, given the "do not set Background/Foreground on a `ThemedActionButtonStyle` button except the active saved-size button" rule). The Browser must produce identical labels, order and dimensions. Say whether that code can be shared or only copied in pattern.
18. Invalid entry handling precedent: what does the hamburger list do with a zip it cannot read (Both on any error), and what does `ThemeLog` offer for a skipped zip (log once per session? per refresh?). Recommend the quietest consistent behavior that does not spam the log on every tab switch.
19. Thumbnail from the zip: the entry is `assets/theme-thumbnail.png` (`ThemeThumbnailStore.FileName`). Confirm the `Info.ThumbnailPath` format inside a saved zip. Verify non-loaded themes have no reliable extracted folder (`Themes\Extracted\<name>` lifetime) and that `Themes\Thumbnails\<guid>` only covers the live/restore point theme, so the Browser must read from the zip. Describe how to decode into a frozen in-memory `BitmapSource` with `CacheOption.OnLoad` and no file lock (compare with `ThemeThumbnailControl.LoadImage`).
20. Cost and caching: with, say, 50 themes, what does a refresh cost (zip open, JSON, PNG decode)? Recommend a cache keyed on (zip path, last-write time, length), what to decode at (full size so the same bitmap serves both the 160 x 90 tile and the 480 x 270 pop-out, or `DecodePixelWidth`), and whether reading can run off the UI thread without breaking the "must not stall the tab switch" goal. Note any existing background-task precedent in the codebase (the media loading in `MainViewModel` uses a version guard).
21. Case and name rules: theme-list name comparison elsewhere is case-insensitive; confirm how "is this entry the active theme" should be decided (`ActiveThemeName` vs `LoadedThemeName`, and what each means after delete, Save As New Name and a failed load).

### D. Theme Info display controls (reuse)
22. `ThemeThumbnailControl`, `ThemeAuthorControl`, `ThemeDescriptionControl`, `ThemeInfoFallbacks`: which are truly "content only" and reusable read-only in a list entry. For each: what styling it hard-codes versus takes from Global resources, its sizing model (the thumbnail uses `DisplayHeight` and `NoImageAspect = 320/180`), and whether it can be used at 160 x 90 and 480 x 270 maximum boxes with fit-and-center for any aspect. If not, say what a small seam would be, or whether a separate light-weight image element is cleaner for the Browser tile (the Browser tile needs its own hover overlay and click, so check this explicitly).
23. The description rules in the Builder: length cap, line/height limits, scrolling, trimming, newlines, and how "No description" is shown (text, `ThemeInfoFallbacks`, the Sub-Text color at 60% opacity). Confirm the Browser can apply exactly the same rules for an inline `Description:` + body in a wrapping text flow (e.g. one `TextBlock` with two `Run`s, different Global styles), and flag any rule that only makes sense in the Builder's fixed-height box.
24. The "No Image" box: its text, fixed Chrome Palette colors and size. Confirm it can be shown inside a 160 x 90 maximum box and the pop-out (the pop-out for a theme without a thumbnail: should it be clickable at all? report what is simplest and consistent).

### E. Thumbnail capture
25. `MainWindow.CaptureThemeThumbnail` and `AdvancedTab.ThumbnailCaptureRunner`: exactly how the image is produced (render target, transform, scaling, which grid is hidden, what size it renders at before scaling) and where the "360 px tall" is set. Describe how the output size depends on the layout size and aspect today.
26. Find every consumer of the captured image and its size: `ThemeThumbnailStore.WritePng` / `WritePending` / `FileName`, `PendingThemeThumbnail`, `SaveThemeCore`, the Theme Info display and the camera button. Anything that assumes 640 x 360, 360 tall or 16:9.
27. Design the minimal change to "fit inside 480 x 270, preserve aspect, never upscale": where the new constants should live (one named place for capture box, Theme Info box, Browser box, pop-out box), how to keep downscaling quality high (render at native then scale with Fant / high-quality, versus rendering with a scale transform; test which the current code does and whether text edges look soft), and what happens for layouts smaller than 480 x 270 (800 x 600 is fine, but confirm no upscale path exists).
28. Confirm Theme Info stays at a 320 x 180 maximum box after the capture change, and how the display currently scales an image larger than that (`MaxHeight` only, or width too; Stretch mode; `BitmapScalingMode`/`RenderOptions` already set anywhere).

### F. Styling and resources
29. Produce a **mapping table** from the spec's names to the real resource keys and types (see `GlobalThemeResources`, App.xaml `[SECTION: Global Theme Resources]`, the Global tab field tables): Global Header, Sub-Header, Standard Text, Sub-Text (font size, color, font family/style if present), Global Button (normal / hover / selected, border size, radius, font size), Global Border Color / Size / Radius, Global Background Color, Divider / Separator, and the instructional-text style used in the Builder. Where two keys could match the spec's wording (for example `Global.Options.Bg` versus the menu background, tab frame border versus `OptionsMenuBorder*`, `Global.Options.Separator`), list both and say which the current Theme Info / Builder UI actually uses, so the user can pick.
30. How the **selected** Theme sizes button is implemented (the one allowed exception to the `ThemedActionButtonStyle` rule). Can the Browser's per-entry buttons use the same mechanism, per button, without leaking state between entries when the list is rebuilt?
31. The Global divider: the shared `Separator` + `Global.Options.Separator` and margins used in the Builder; confirm it is the "Global Divider component" the spec means and whether a keyed style exists (decision 81 says dividers are themed Separators with listed margins).
32. Chrome Palette: list the existing named brushes relevant to overlays, glyphs and No Image, and confirm where new named brushes for the hover overlay, dim layer and magnifier glyph would go and their naming pattern.
33. The magnifier-plus glyph: what icon font approach the app already uses (Segoe Fluent Icons / MDL2, as for the Favorites Star and the Theme Info camera/pencil via `ThemeInfoGlyphStyle`) and whether a suitable glyph exists there at 32 x 32.
34. Are there any all-caps display precedents, or any text transform helper? (The spec wants display-only upper-casing, storing the real name; note culture issues if any, e.g. use invariant upper-casing.)

### G. Overlay, pointer and keyboard behavior
35. Where an overlay layer spanning the whole window content should live in `ThemeBuilderWindow.xaml` (a `Grid.RowSpan="2"` sibling of the TabControl is the likely idea; verify against the Theme Saved Flash grid and anything with `Panel.ZIndex`). Confirm a sibling overlay can capture mouse and swallow wheel events over the whole window, including the tab headers, and how to prevent keyboard activation underneath (`IsEnabled`/`IsHitTestVisible` on the TabControl and button row, focus management, `PreviewKeyDown`). Note any existing patterns in the project for modal-ish overlays or popups.
36. Whether a WPF `Popup` is needed or avoidable (the project has strict rules about Popups and Smart fullscreen scaling in `CLAUDE.md`; sub-windows are not scaled by Smart fullscreen). The preference is a plain in-window element, not a `Popup`; confirm that works with the window's DPI behavior and with `UseLayoutRounding`/`SnapsToDevicePixels` conventions.
37. What the dim layer must do when the window loses focus, the tab changes by keyboard (Ctrl+Tab), or a hamburger swap/refresh happens while it is open (suggest: close the pop-out on any of these).
38. Scroll position: which element would host the list (`ScrollViewer` with the themed scrollbar, or `ItemsControl` / `ListBox` / virtualizing panel). Report the scrollbar styling mechanism (`Global.Scroll.*`, `ApplyScrollOverride` rules, "never copy the scrollbar template") and how to preserve and restore the scroll offset across a rebuild, a load, and live restyling. Comment on whether virtualization is worth it for an unbounded list given each entry has variable height (wrapping description), and on the cost of plain non-virtualized rendering at, say, 100 entries with bitmaps.

### H. Interactions with other windows and state
39. A hamburger theme swap while the Browser tab is visible: does `RefreshAfterExternalThemeSwitch` run and what would the Browser need to update its check and selected button? Same question for a hamburger Layout size pick (the selected resolution button must move), the Builder's own size buttons (edit-session size switches), Save Changes under a new name, Delete Theme and Show Theme In changes. List every event after which the visible Browser could be stale, and the one cheap hook per case.
40. Whether the Browser's active-resolution indicator should read `ActiveResolutionWidth/Height` or the Smart fullscreen display override (`ResolveDisplayLayout` / `DisplayLayoutWidth/Height`). The rule in `CLAUDE.md`: code that edits/saves/snapshots uses `ActiveResolution*`. Confirm this is the right source for "active resolution" and what shows when Smart fullscreen is displaying a different size.
41. A pending (unsaved) custom size or an unsaved removed size in the active theme: the Browser shows the saved zip's sizes, so confirm what the Browser shows as selected when the active in-memory size is not in the zip (expectation: no button selected) and that nothing breaks.
42. Guides, Background grid, previews: what the `SelectionChanged` handler does when moving Builder -> Browser -> Builder, to confirm those return correctly and nothing in the Browser tab changes Builder state.
43. Thread/lifetime concerns: `ThemeFolderSwap` and open files (a reload while a pop-out image or a catalog bitmap holds a zip or file open must not block a load; confirm that reading zips via short-lived streams and frozen bitmaps cannot hold a lock on `Themes\*.zip`, which Save/Delete swap).

### I. Traps and rules to check the plan against
44. Walk through the `CLAUDE.md` Known Gotchas and list each one that the Browser could touch (Theme file safety, `Themes` zip enumeration rules, Smart fullscreen/popup scale rules, Global resources are typed, no implicit styles in App.xaml, tooltip rules, layout rounding rules, name-keyed handlers, Row templates/ancestor binding warnings for any list template, sub-window `WindowFit`, `BringToFrontAfterChildClose`). State which apply and what each demands.
45. Anything else you notice that the spec above gets wrong, misses, or contradicts in the real code: explicitly list these, including anything that makes a settled decision unsafe.

## 4. Constraints and things NOT to touch
- No edits of any kind: this audit is read-only. No new files in the project (do not create your report as a repo file; put it in your reply). No commits.
- Do not audit or redesign launch-time resolution fallback or Smart fullscreen entry logic beyond how a Browser load would pass through it.
- Do not plan label renames for the Builder's `Theme sizes:` / `Resolution:` controls; just note shared code you encounter.
- Do not invent new Theme Builder properties or new Global properties.
- Do not run any screen-capturing or input-sending scripts; this audit needs none (see `CLAUDE.md` Boundaries).

## 5. How to report

Reply in this structure, then **stop and wait for the user**:

1. **Summary** (10 lines or fewer): can the spec be built mostly by reuse? What are the 3 biggest risks?
2. **Findings by item number** (A1 to I45 as numbered above; group short ones). Each: exists / behavior / reuse verdict / risks / mismatches.
3. **Reuse map**: a table of "Browser need -> existing code -> as is / small seam / new".
4. **Proposed seams**: the smallest changes you would make to existing code (signatures only, no implementation), especially the single "load theme X at resolution R and persist" entry point and the restore-point fix.
5. **Resource mapping table** (item 29) with the ambiguous cases flagged for the user.
6. **Mismatches with `themes.md`** (code wins; list what themes.md should say).
7. **Open questions for the user**, only the ones the code cannot answer, each with your recommended default.
8. **Suggested split into implementation handoffs** (ordered, each independently buildable and testable, e.g. thumbnail capture/constants; shared load+persist entry point and restore-point fix; hamburger command and tab activation; catalog service and cache; Browser tab UI; pop-out overlay), with expected transient build errors, if any, called out in advance.

Do not start implementation after the report. STEP 2 happens only after the user says "go", through a separate handoff.
