# Handoff 3 of 6: Catalog reader and bare list (Theme Browser)

## Kick-off prompt (paste this into Claude Code)

```
Read handoff-3-catalog-reader-bare-list.md in the project folder completely, then CLAUDE.md and themes.md (sections 3.4, 3.22, 3.23 and 7 at least, plus the Theme file safety notes).

Follow the file's pass structure: do the read-only STEP 1 check first, report any mismatch, then STOP and wait for me to say "go". Only after "go" do STEP 2 (the edits). Do one pass only, stop after it, and do not commit. Do not load or select themes, do not persist anything, and do not start any other Theme Browser work (no final styling, no hover overlay, no pop-out, no resolution-button factory).
```

---

## 1. Goal and why

Handoffs 1 and 2 are done: thumbnails are captured and displayed in fit-inside boxes, and the THEME BROWSER tab exists as a placeholder (`ThemeBrowserControl`) whose `RefreshCatalog()` runs once each time the tab is shown. This pass replaces the placeholder with the real **catalog**:

1. A **catalog reader** that lists installed theme ZIPs from their central directory and `theme.json` **without extracting anything**, applies Show Theme In filtering, sorts them alphabetically, tolerates bad archives, and decodes a small thumbnail per theme.
2. An **in-memory cache** so refreshing (every time the tab is shown) is cheap.
3. A deliberately **bare, scrollable list** in the Browser tab so the whole data path can be tested before any styling work.

The look of the list is throwaway. Handoffs 4 to 6 add loading, the real layout and styling, and the pop-out.

Settled decisions (do not re-open):
- **Saved-ZIP data only.** The Browser shows what is saved in each theme ZIP (`theme.json` and the bundled thumbnail), never unsaved working state, even for the active theme. A Show Theme In change counts only after Save Changes.
- Catalog = the `*.zip` files **directly** in the `Themes` folder (the existing rule; `.bak`, `.tmp-` files, `_staging`, `Deleted` and `Extracted` are never listed). Alphabetical by theme name, case-insensitive (the same ordering as the hamburger's Themes list: ordinal, ignore case). No cap, no pinning of the active theme.
- A theme's identity and display name is its **ZIP file name without `.zip`** (theme.json has no name field).
- Honor Show Theme In: `Both` and `BrowserOnly` appear; `MenuOnly` and `Neither` do not. Use the existing `ThemeShowIn` API (`Normalize`, `ShowsInThemeBrowser`). Unlike the hamburger menu, the Browser does **not** force-show the active theme. Null or unknown values read as `Both`.
- Invalid, unreadable or corrupt archives are **skipped quietly** (no dialog). They are logged to `theme.log` **once per (path, write time, length) per session**, not on every refresh.
- Thumbnail strategy: **tile-size decode** now (full-size decode on demand is Handoff 6). A tile is decoded to fit within **320 x 180** (never upscaled, aspect preserved, one dimension set for `DecodePixel*`), frozen, held in memory, and read through a **short-lived** zip stream and a `MemoryStream`, so **no ZIP or file lock lingers**. It displays in a **160 x 90 maximum box** (`ThemeThumbnailStore.BrowserMaxWidth/Height`), fit and aligned, no stretching or upscaling. No per-aspect variants. No migration. The Browser never reads from `Themes\Extracted` or `Themes\Thumbnails`.
- A narrow thumbnail is centered in its 160-wide slot in the **final** layout (Handoff 5). In this bare list, any simple placement is fine.
- Browser lists **all resolutions stored in the theme** (every item of `Layouts`), de-duplicated, ordered by width then height, labeled `W x H` (with spaces), exactly like the Theme Builder's saved-size row.
- Reading runs **synchronously on the UI thread** in this pass (the cache makes warm refreshes cheap). Moving the cold read to a background task is a possible later optimization; do not add it now.

## 2. Files involved (verify names and line areas; the code wins)

Expected new files:
- `Services/ThemeCatalogReader.cs`: enumerates and reads zips, owns the cache and the skip logging (a static class or a small instance class, whichever matches the `Services` style; `ThemeTemplateStore` and `ThemeShowInReader` are the precedents)
- `Models/ThemeCatalogEntry.cs` (or in the reader file if that matches project habits): an immutable record for one catalog entry

Expected changed files:
- `Views/ThemeBrowserControl.xaml` / `.xaml.cs`: the placeholder becomes the bare list
- `ThemeBuilderWindow.xaml.cs`: hand the control the settings (one line in the constructor, same pattern as `GlobalTab.Initialize(_settings)`)
- `themes.md`, `CLAUDE.md`

Read for reference (do not change): `Services/ThemeShowInReader.cs`, `Services/ThemeTemplateStore.cs` (cache key pattern, once-per-session logged lines), `Services/ThemeLog.cs`, `Models/ThemeInfo.cs` (`ThemeInfoFallbacks`, `ThemeShowIn`), `Services/ThemePaths.cs`, `MainViewModel.LoadThemeCore` (the thumbnail fallback to `assets/theme-thumbnail.png`), `AdvancedThemesTabControl.xaml.cs` `RebuildSavedSizes` (label format, ordering, button look), `MainWindow.xaml.cs` hamburger Themes list (enumeration and sort), `GlobalThemeResources`.

## 3. Current behavior and risks from the audit

- `ThemeShowInReader.Read` is the pattern: `ZipFile.OpenRead`, entry names `theme.json` then legacy `theme.cfg` (matched case-insensitively), `JsonDocument`, case-sensitive PascalCase property names, never throws, returns `Both` on any error.
- A saved zip's `Info.ThumbnailPath` is `assets/theme-thumbnail.png` or `""`. `LoadTheme` falls back to the `assets/theme-thumbnail.png` entry. `Themes\Extracted\<name>` can be stale and `Themes\Thumbnails` covers only the live theme and its restore point, so the Browser must read from the zip.
- **Lock risk (important):** `ZipFile.OpenRead` opens with `FileShare.Read`. That does not block `LoadTheme`'s own read. It **does block Save's `File.Replace`** (which retries 5 x 200 ms) and **Delete Theme's `File.Move` (which does not retry)** while a zip is open. So never keep a `ZipArchive` or stream open: open, read what is needed, copy the thumbnail bytes into a `MemoryStream`, dispose, then decode from memory. Frozen `BitmapSource`s created from memory streams hold nothing.
- A WPF TabControl keeps only the selected tab's content in the visual tree. The `ThemeBrowserControl` instance (and its `ScrollViewer`) survives tab switches, but its visual tree is detached and re-attached, so scroll position needs deliberate handling.
- The hamburger lists a zip it cannot read (treating Show Theme In as `Both`); the Browser must instead skip such an archive.

## 4. STEP 1: read-only check (do this first, then STOP and wait for "go")

Do not edit anything in this step. Report, briefly:

1. `ThemeShowInReader`: its exact code (entry lookup, JSON handling, property names and casing, error handling). The real property names in `theme.json` for `Info` (`Author`, `Description`, `ThumbnailPath`, `ShowIn`) and `Layouts` (`Width`, `Height`), and whether any older zips could lack `Info` or have `Layouts` entries without a size.
2. The `ThemeShowIn` API: exact names for normalizing a raw value and for the Browser check (`ShowsInThemeBrowser`), and the `ThemeInfoFallbacks` constants (`NoImage`, the "No description" text).
3. `ThemeLog.Write`: exact signature, how the existing actions fill its arguments, and the existing "log once per session" pattern in `ThemeTemplateStore` (`LoggedLines`) that this pass should copy for a new action name `BrowserSkipped`.
4. The canonical way to get the `Themes` directory and enumerate theme zips (`ConfigurationSettings`, `ThemePaths`, `GetThemeNames`, the hamburger's list), including how names are derived and how the hamburger sorts.
5. How `LoadThemeCore` resolves the thumbnail inside the zip (exact entry names, slash handling, fallback) so the reader matches it.
6. `ArcadeDarkScrollViewer` (the style the audit suggested for a pixel-scrolling list): where it is defined, its key, an example usage, and whether it follows `Global.Scroll.*`. Confirm there is no better-suited existing style, and that no custom scrollbar template is needed.
7. How `RebuildSavedSizes` builds its buttons (style, `Padding`, `Margin`, label format, ordering) so this pass can copy the pattern without extracting anything; and the `GlobalThemeResources` constants for Global Standard text size and color.
8. The current `ThemeBrowserControl` from Handoff 2 and exactly where `ThemeBuilderWindow` calls `RefreshCatalog()` and where `GlobalTab.Initialize(_settings)` sits relative to wiring the tab `SelectionChanged` handler, so `Initialize` for the Browser runs before any refresh.
9. How to read an image's pixel size cheaply from a `MemoryStream` (for example `BitmapFrame.Create` with `DelayCreation` / `BitmapCacheOption.None`, or a `BitmapDecoder`) so a single `DecodePixelWidth` **or** `DecodePixelHeight` can be chosen to fit 320 x 180 without distorting or upscaling, and confirm this works for PNG with `BitmapImage` + `StreamSource` + `CacheOption.OnLoad` in this project's .NET/WPF version.
10. Any other code that opens theme zips and could contend with the reader (hamburger Show Theme In reads on every open, `SaveThemeCore`'s `ExtractThumbnailFromZip`, Delete, Load), and anything that watches the `Themes` folder.
11. Any mismatch between this file or `themes.md` and the code.

Then stop. Edits start only after the user says "go".

## 5. STEP 2: the edits (only after "go")

### 5.1 `ThemeCatalogEntry` (immutable)

Holds, for one valid, visible theme: `Name` (string), `ZipPath`, `Author` (string, may be empty), `Description` (string, may be empty), `ShowIn` (normalized text), `Sizes` (read-only list of (Width, Height), de-duplicated, ordered by width then height), `Tile` (frozen `BitmapSource`, null when the theme has no usable thumbnail), plus the cache key parts (`LastWriteUtc`, `Length`). Instances are shared by the cache: an unchanged zip must return the **same instance** every refresh.

### 5.2 `ThemeCatalogReader`

A small service with `BuildCatalog()` (or similar) returning the visible entries sorted by `Name` (ordinal, ignore case), and a per-call statistics object or out values (`Read`, `FromCache`, `Skipped`, `Hidden` counts) for the temporary diagnostics line.

Behavior:
- Enumerate `*.zip` directly in the Themes directory the canonical way (STEP 1 item 4). A missing folder returns an empty list.
- For each zip, get `FileInfo` (`LastWriteTimeUtc`, `Length`). The cache key is (full path ignoring case, write time, length). A hit returns the cached result (a valid entry **or** a remembered "invalid" marker, so a corrupt zip is not re-read on every refresh until the file changes). A miss reads the zip as below.
- Reading one zip, fully inside a `using` that is disposed before any decoding: open with `ZipFile.OpenRead`; find `theme.json`, then `theme.cfg`, case-insensitively (same as `ThemeShowInReader`); parse with `JsonDocument` (read only `Info.Author`, `Info.Description`, `Info.ThumbnailPath`, `Info.ShowIn` and each `Layouts[].Width/Height`; do **not** deserialize layers); locate the thumbnail entry the way `LoadThemeCore` does and copy its bytes into a `MemoryStream`; dispose the archive.
- Tile decode (after the archive is closed): read the pixel size without a full decode (STEP 1 item 9); `scale = min(1, 320/w, 180/h)`; if `scale < 1`, set **only one** of `DecodePixelWidth` / `DecodePixelHeight` (the one that makes the image fit, rounded, at least 1) so aspect is preserved; if `scale >= 1`, no decode scaling (no upscale). `BitmapImage` with `StreamSource`, `CacheOption.OnLoad`, `CreateOptions.IgnoreImageCache`, then `Freeze()`. Use a shared constant for the 320 x 180 tile bound in `ThemeThumbnailStore` (for example `BrowserTileDecodeMaxWidth/Height`), next to the existing size constants, with a comment saying it is about twice the 160 x 90 display box so it stays sharp at 150% to 200% Windows scaling.
- A thumbnail failure (missing entry, corrupt PNG) leaves `Tile` null and the entry **valid**. A failure to read or parse the theme JSON, an archive that cannot be opened, or a missing JSON entry makes the zip **invalid**: skip it.
- Skipping: log once per (path, write time, length) per session to `theme.log` with the new action name `BrowserSkipped` and a short reason (no exception stack), following the `ThemeTemplateStore` pattern. No dialog, no exception to the caller.
- Filtering: after the cache lookup, drop entries whose normalized `ShowIn` does not satisfy `ThemeShowIn.ShowsInThemeBrowser`. The cache keeps all valid entries so a Show Theme In change (a new write time after Save Changes) is simply re-read.
- Pruning: remove cache keys whose files are no longer in the enumeration.
- All on the UI thread; no locks needed. Never keep a `ZipArchive`, `ZipArchiveEntry` stream or `FileStream` open past the read of one zip. Never modify, create or delete any file.
- Do not modify `ThemeShowInReader` or the hamburger code.

### 5.3 `ThemeBrowserControl`: the bare list

Replace the placeholder text with:
- `public void Initialize(ConfigurationSettings settings)` (store the settings; `RefreshCatalog()` does nothing harmful if called before it).
- `RefreshCatalog()` (same name and call sites as Handoff 2): builds the catalog through the reader. **If the resulting sequence is identical (same entry instances in the same order) to what is already displayed, do not rebuild the list**; just update the diagnostics line. Otherwise rebuild the list and restore the vertical scroll offset (save it first, restore it once layout has run, for example through `Dispatcher.BeginInvoke` at `Loaded` priority; clamp to the new extent). Also remember the offset when the control unloads (tab switched away) and re-apply it on the next show if the list did not change, so leaving and returning to the tab keeps the scroll position.
- Layout: a one-line **temporary diagnostics** TextBlock at the top, outside the scrolling area (for example `12 themes shown | read 0, from cache 12, skipped 1, hidden 2`), clearly commented as temporary and removed in Handoff 5; below it, a vertical `ScrollViewer` (style from STEP 1 item 6) holding a plain, non-virtualized panel of entries built in code (a `RebuildList(entries)` method). When no entry is shown, display `No themes found.`
- Each entry (bare, no final styling): a row with the tile image on the left (an `Image` with `Stretch=Uniform`, `StretchDirection=DownOnly`, `RenderOptions.BitmapScalingMode=Fant`, `MaxWidth/MaxHeight` = `ThemeThumbnailStore.BrowserMaxWidth/Height`, left-aligned; when `Tile` is null a small text `ThemeInfoFallbacks.NoImage` instead) and, to its right, a vertical stack with: the theme name (Bold), `Resolutions:` followed by a `WrapPanel` of one plain button per size (`ThemedActionButtonStyle`, `Padding 7,3`, `Margin 0,0,6,4`, label `W x H`), `Author: <author>`, and `Description: <description>` (or `ThemeInfoFallbacks`' "No description" when empty). Wrap long text. A little spacing between entries; no divider yet.
- Text uses the Global Standard Text size and color through the existing resource-reference pattern (`SetResourceReference` with the `GlobalThemeResources` constants, as `ThemesManagerControl` does for its prompt label). No new brushes, no hex colors, no implicit styles, no new Global or Theme Builder properties.
- **The resolution buttons are inert in this pass**: no Click handler, no tag wiring, and they must not be disabled (disabled buttons dim to 50%). Handoff 4 wires them. Add a comment saying so.
- Do not show an active-theme marker or selected-button state yet (Handoff 4).

### 5.4 `ThemeBuilderWindow.xaml.cs`

Add one line in the constructor calling `ThemeBrowser.Initialize(_settings)` **before** the tab `SelectionChanged` handler can run (follow STEP 1 item 8). No other change: do not touch `RefreshAfterExternal*`, `RefreshAfterThemeLoad`, the restore points or the Closed handler.

### 5.5 Documentation (end of pass)

- `themes.md`: a new subsection (for example 3.26 "Theme Browser catalog") describing: the reader and cache (key, instance sharing, pruning, invalid markers), what is read from each zip, tile decode rule, filtering and sorting rules, skip logging (`BrowserSkipped`, once per session per file state), the saved-zip-only rule, the lock rule, and the bare list with its temporary diagnostics line. Update the file map (new `Services/ThemeCatalogReader.cs`, entry file, `ThemeBrowserControl`), the status table row for the Theme Browser ("Catalog and bare list only; loading, styling and pop-out not built"), 3.22's sentence that the Browser "will read `ShowsInThemeBrowser`", the `theme.log` action list (add `BrowserSkipped`), and a dated change-log entry (2026-11-09 or today's date).
- `CLAUDE.md`: in "Theme file safety" add short bullets: the Browser catalog reads zips only through short-lived `ZipFile.OpenRead` calls (never keep an archive or stream open: it blocks Save's `File.Replace` and Delete's `File.Move`, which does not retry), copies thumbnail bytes to memory before decoding, never reads `Extracted` or `Thumbnails`, and logs `BrowserSkipped` once per file state; in "Windows and views" / "Theme Builder Status" say THEME BROWSER now shows a bare catalog list. Also note the new `theme.log` action in that file's list of actions.
- Keep `.md` files LF and `.cs` / `.xaml` CRLF as `CLAUDE.md` describes; check `git diff --stat` shows no whole-file change.

## 6. Constraints and things NOT to touch

- No theme loading, no `LoadTheme` calls, no selection or `settings.json` persistence, no active-theme marker or selected state, no click behavior on resolution buttons (Handoff 4).
- No final layout or styling, no divider, no ALL CAPS name, no check glyph, no instruction line, no hover overlay, magnifier, pop-out or dim layer, no Chrome Palette additions (Handoffs 5 and 6).
- No extraction of the Theme Builder's resolution-button code into a shared factory, and no change to the Builder's resolution controls, `RebuildSavedSizes`, `BtnSavedSize_Click` or the resolution combo. Copy the button pattern only.
- No per-aspect-ratio thumbnails or variants, no migration, no recapture, no background threading.
- Do not change Smart fullscreen, theme save/load/delete, `ThemeShowInReader`, the hamburger menu or `ThemeThumbnailStore`'s existing members (adding the tile-decode constants is fine).
- Keep the parked ideas parked: the `[New]` combo idea, save-on-create resolution behavior and fixed Theme Builder typography.
- Never modify, create or delete theme or thumbnail files, and never leave a zip or file open.
- Keep comment fencing (`// [SECTION: ...]` / `<!-- [SECTION: ... ] -->`) and a one-line `// Does X` summary on every method. No unrelated fixes bundled in.
- Never commit.

## 7. Verification

You (Claude Code):
1. `dotnet build` finishes with **0 errors and 0 warnings**. No transient errors are expected if the control gains `Initialize` before `ThemeBuilderWindow` calls it.
2. Grep: no `ZipArchive`, `ZipFile.Open*` or `FileStream` left open past a using in the reader; no reference to `Extracted` or `Thumbnails` in the reader; no call to `LoadTheme`; the placeholder counter text is gone.
3. `git diff --stat` shows only the files in section 2 (plus the docs), with no whole-file line-ending changes.

The user (cannot be done without running the app; do not run any screen-capture or input scripts yourself). Use a copy of the data folder if you want to be careful with the corrupt-zip tests:
1. **Listing.** Open the Browser tab: every valid theme in `Themes` appears once, alphabetical ignoring case, named exactly like its zip. `.bak` files, `Deleted`, `_staging` and `Extracted` never appear. The diagnostics line says the first view read N and skipped 0.
2. **Warm refresh.** Switch away and back: the diagnostics line says read 0, from cache N. The list does not visibly flicker.
3. **Show Theme In.** On a theme set Show Theme In to Hamburger menu only, Save Changes: after returning to the Browser it is gone. Set Neither: gone. Set Theme Browser only and Both: present. An **unsaved** change in the combo does not affect the Browser until Save Changes. If the active theme is hidden for the Browser, it is simply absent (and still in the Builder's Load Theme combo).
4. **Resolutions.** Each entry's buttons match that theme's saved sizes, ordered by width then height, labeled `1280 x 720` style. Clicking a button does nothing (and loads nothing).
5. **Text.** A theme with no author shows `Author:` with nothing after it. A theme with no description shows `No description`. A long description wraps without clipping.
6. **Thumbnails.** A 16:9 thumbnail shows inside the 160 x 90 box; a 4:3 one (360 x 270 capture) shows at 120 x 90, not stretched; an old 640 x 360 one shows at 160 x 90; a theme with no thumbnail shows `No Image`. They look reasonable (not blurry) at your Windows scale.
7. **Corrupt archives.** Drop into `Themes`: (a) a text file renamed `broken.zip`, (b) a zero-byte `empty.zip`, (c) a valid zip without `theme.json`. None appears, no dialog appears, and `theme.log` gets one `BrowserSkipped` line each. Switching tabs several times adds no further lines. Remove them afterwards; the list updates on the next visit.
8. **Change detection.** In the Builder, edit a theme's description and Save Changes: back in the Browser the diagnostics line shows one zip re-read and the entry shows the new description. Use Copy Current Theme to create a new theme: it appears at the right alphabetical position. Delete a theme: it disappears.
9. **No lingering locks.** Keep the Browser tab visited, then in the Builder run Save Changes on the loaded theme and Delete Theme on a non-loaded theme: both succeed with no "another program may be using it" message. In Explorer, rename or delete a test zip while the app is running: no "file in use" error.
10. **Scroll.** With enough themes to scroll (make copies with Copy Current Theme if needed): scroll down, go to the Builder and back: the scroll position is kept. Save a theme (which changes the list) and return: the list is updated and the scroll position stays close to where it was, with no jump to the top.
11. **No side effects.** Make an unsaved edit in the Builder, visit the Browser and return: the edit is still there. `settings.json` is unchanged (check its timestamp). No theme was loaded.
12. **Speed.** With your real theme set, the first view of the Browser tab feels acceptable (note roughly how long it takes) and later visits feel instant.
13. **Empty folder (optional).** If you can test safely with a copy of the data folder containing no theme zips: the Browser shows `No themes found.` and nothing else breaks.
14. Windows display scaling 125% and 150% for thumbnail sharpness: optional, can stay deferred.

## 8. Stop conditions

Stop and report (do not push on) if any of these happen:
- STEP 1 shows `theme.json` / `Info` / `Layouts` or the thumbnail entries are laid out differently from section 3 in a way that needs more than the edits in section 5.
- Reading a theme's JSON without a full `ThemeSettings` deserialization is not reliable with the project's JSON conventions.
- A tile cannot be decoded without distortion or upscaling using one `DecodePixel*` value, or a decode path would need a file URI or leave a lock.
- A cold read of your real theme set takes noticeably long (about a second or more): report the timing and wait before adding any threading.
- Any edit would need a change to loading, persistence, Smart fullscreen, the hamburger menu, the Builder's resolution controls, or a Browser UI element reserved for Handoffs 4 to 6.
- The build shows errors or warnings you cannot clear within the files in section 2.

After STEP 2, report in this form: files changed, anything that did not match these instructions, anything changed beyond them, and the test checklist (section 7). Then stop. Handoff 4 (load entry point) is a separate file, written only after the user reports back, and after the animated GIF splash reload test is done.
