# Handoff 4 of 6: Load entry point (Theme Browser)

## Kick-off prompt (paste this into Claude Code)

```
Read handoff-4-load-entry-point.md in the project folder completely, then CLAUDE.md and themes.md (sections 3.4, 3.17 to 3.19, 3.22, 3.23, 3.26 and 7 at least, plus Theme file safety and Smart fullscreen in CLAUDE.md).

Follow the file's pass structure: do the read-only STEP 1 check first, report any mismatch, then STOP and wait for me to say "go". Only after "go" do STEP 2 (the edits). Do one pass only, stop after it, and do not commit. Do not start any other Theme Browser work (no final styling, no hover overlay, no pop-out, no dim layer) and do not change the Builder's resolution controls.
```

---

## 1. Goal and why

Handoffs 1 to 3 are done: thumbnail sizing, navigation with a Browser tab hook, and a bare catalog list (reader, cache, tiles, inert resolution buttons). This pass makes the buttons **work**:

- Clicking a resolution button on a Browser entry **loads that theme at that resolution** as ONE operation (the complete saved theme, not only the resolution), through the existing theme-load, Smart fullscreen and persistence mechanisms.
- The selection is **persisted automatically to `settings.json`** (no Save Adjustments), and the theme ZIP is never written.
- The button for the **active theme at the active resolution** is highlighted and **non-actionable** (it can never reload or discard anything).
- The Browser stays open and keeps its scroll position. Its highlight follows hamburger theme swaps and hamburger layout-size picks too.

Settled decisions (do not re-open):

| Click in Browser | Result |
|---|---|
| Active theme, active resolution | Nothing at all: no load, no discard, no persistence, no log line. Highlighted, not disabled (disabled buttons dim to 50%). |
| Active theme, different resolution | Full reload of the saved theme at that resolution; unsaved Builder edits **silently discarded**; selection persisted. |
| Different theme, any of its resolutions | Same, as one operation. |

- **Silent discard** is the existing behavior of the Builder's Load button, the hamburger swap and New Theme. Do not add a prompt (an unsaved-state tracker and warnings come later).
- **One operation:** no intermediate resolution, no second refresh, no second `settings.json` write, no Smart fullscreen flash. This is why `LoadTheme` gets an optional target size (section 5.1) instead of a theme load followed by a size switch.
- **Smart fullscreen:** use the existing **theme-switch rules** (`RunThemeSwitch` / `HandleThemeSwitchForFullscreen`). The adopt/restore logic around opening the Builder is untouched.
- **Reuse over reinvention:** follow the Builder Load button's path (`ThemeLoadRunner` -> `RunThemeSwitch` -> `LoadTheme` -> refresh -> persist) rather than the hamburger's. The hamburger path refuses the active theme (its guard) and the Browser must not reuse its handler.
- **Persist failure** (decided earlier): load, refresh the Builder, then persist inside its own try/catch; if the write fails, log it and show a short message that the theme loaded but the setting could not be saved; Smart fullscreen handling still runs.
- **Failures** leave the Browser usable and persist nothing: use the established messages ("Theme Not Loaded" for `ThemeLoadException`, a generic failure box for other exceptions) and add one for "zip not found" (see 5.4), which today is silent in every caller.
- **Legacy themes with no saved layouts** (decision made here): they stay listed, show the text `No saved resolutions` instead of buttons (not loadable from the Browser), and still load from the Builder and the hamburger. Reason: the project has no legacy migration policy (`CLAUDE.md`), a layout-less zip has no resolution to highlight or to make non-actionable, and a special "Load" button would add a second load semantic for a case that current saves cannot produce. STEP 1 must confirm that no current save or template path produces a layout-less zip; if one does, stop and report.
- A temporary plain-text `(active)` marker after the active theme's name is allowed in this bare UI so the active theme is visible even when its active resolution has no button; Handoff 5 replaces it with the real check-circle glyph.

## 2. Files involved (verify names and line areas; the code wins)

Expected changed files:
- `ViewModels/MainViewModel.cs`: `LoadTheme` / `LoadThemeCore` (optional target size; around line 1513 at audit time)
- `ThemeBuilderWindow.xaml.cs`: the Browser load handler (`LoadThemeFromBrowser`), the event wiring, and two one-line hooks in `RefreshAfterExternalThemeSwitch` and `RefreshAfterExternalSizeSwitch`
- `Views/ThemeBrowserControl.xaml.cs` (and `.xaml` only if needed): button clicks, the active highlight, a `LoadRequested` event, the busy guard, `RefreshActiveMarks()`
- `themes.md`, `CLAUDE.md`

Read for reference (do not change unless STEP 1 and the user say so): `MainWindow.xaml.cs` (`RunThemeSwitch`, `HandleThemeSwitchForFullscreen`, `HamburgerThemeItem_Click`, `ReleaseCustomLayers` callers), `ThemesManagerControl.xaml.cs` (`BtnLoadTheme_Click`, `LoadCreatedTheme`), `AdvancedThemesTabControl.xaml.cs` (`RebuildSavedSizes`, `RefreshSavedSizes`), `Services/ThemeLog.cs`, `Services/ThemeTemplateStore.cs`, `Services/ThemeCatalogReader.cs`, `ConfigurationSettings`.

## 3. Current behavior and risks from the audit

- **Builder Load path:** `ThemesManagerControl.BtnLoadTheme_Click` builds `load = () => { if (!_viewModel.LoadTheme(name)) return false; _refreshOptionsBindings(); _persistToDisk(); return true; }` and runs it through `ThemeLoadRunner` -> `MainWindow.RunThemeSwitch`. Its persist (`ThemeBuilderWindow.PersistSettingsToDisk`) **throws** on IO errors, and an exception escaping `RunThemeSwitch` skips `HandleThemeSwitchForFullscreen`. `LoadTheme` returning false (zip missing) is **silent** in all current callers.
- **`RunThemeSwitch(Func<bool> doLoad)`** sets `_deferBuilderDrop`, runs the load in try/finally, then calls `HandleThemeSwitchForFullscreen` only when the load returned true. `HandleThemeSwitchForFullscreen` clears the fullscreen entry size, drops an auto-entered fullscreen when the size is not an exact fit, and otherwise reruns the Smart size test. With the Theme Builder visible, Smart fullscreen only considers the active size.
- **`LoadTheme`/`LoadThemeCore`** is synchronous: extracts into `_staging`, parses `theme.json`, swaps `Extracted\<name>` (raising `ThemeExtractionStarting` so custom layers and media are released), replaces every Global value and the Info block, sets `Layouts`, `ActiveThemeName`, `LoadedThemeName`, then the active size from the zip's `ActiveWidth/ActiveHeight` (if above 0), plus `SavedThemeLayouts` and `SavedThemeWidth/Height`; finally `RefreshThemeBindings()` and `RefreshGameColorsLive()`. It persists nothing itself. On failure memory is unchanged (`ThemeLoadException`; `ReacquireThemeFiles` puts things back).
- **If the requested size has no layout** in the theme, `ResolveDisplayLayout` / `ResolveAndStore` would silently create a registry-default layout at that size and persist it. Hence a target size must be applied **only when the theme's layouts contain it**; otherwise fall back to the zip's own size.
- **`RefreshAfterExternalThemeSwitch`** (`ThemeBuilderWindow`) = `ThemesManager.ReloadFromSettings()` (reselects the Load Theme combo) + `RefreshAfterThemeLoad()` (window bindings, clears the pending thumbnail, `AdvancedTab.ReloadFromSettings`, Global tab, Theme Info, and moves every close-without-saving restore point, including the size restore point, to the just-loaded state). Because `LoadTheme` has already set the active size to the target before this runs, **no restore-point change is needed**: closing the Builder without saving after a Browser load keeps the loaded theme and resolution.
- **Reloading the loaded theme** (same theme, other resolution) is allowed by the Builder Load button but refused by the hamburger (a guard whose stated reason looks outdated, since `ReleaseCustomLayers` now stops the wallpaper video). The animated splash (`ThemeBootSplashAnimatedAsset`, a `BitmapImage` from a file URI passed to AnimatedImage.Wpf) is **not** released and has never been tested on hardware for a reload. This pass includes that hardware test.
- **A queued second click** during the UI-blocking synchronous load runs after the first load finishes; a plain busy flag that is already cleared would not stop it. The lightest guard: set the flag, run the load, clear the flag from a `Dispatcher.BeginInvoke(..., DispatcherPriority.ApplicationIdle)` so clicks queued during the load are still ignored.
- **Stale highlight trap (from Handoff 3):** `RefreshCatalog()` skips rebuilding the list when the entry sequence is identical. After a theme swap that skip would leave the highlight stale, so the active marks must be refreshed on **every** `RefreshCatalog()` and on external swaps.

## 4. STEP 1: read-only check (do this first, then STOP and wait for "go")

Do not edit anything in this step. Report, briefly:

1. `LoadTheme` / `LoadThemeCore`: the exact signature and the exact code that chooses the active size and sets `SavedTheme*`; every caller of `LoadTheme` (so a new optional parameter with a default of null provably changes nothing for them); what is logged to `theme.log` by a load and with which action name and result values; behavior when the zip has no layouts or `ActiveWidth/Height` is 0.
2. `RunThemeSwitch` and `HandleThemeSwitchForFullscreen`: exact code and accessibility (can `ThemeBuilderWindow` call them the way `ThemeLoadRunner` does today, with no change to `MainWindow`?). Confirm that a `LoadTheme` that sets the target size directly goes through the theme-switch rules and never through `RunSizePick` / `HandleLayoutSizePickForFullscreen`, and describe what the user sees in each fullscreen mode (windowed, plain fullscreen, Smart fullscreen on an exact-fit size, Smart fullscreen on a non-fit size, auto-entered fullscreen) when the target size equals, or differs from, the current size.
3. `ThemeBuilderWindow.RefreshAfterExternalThemeSwitch` / `RefreshAfterExternalSizeSwitch` / `RefreshAfterThemeLoad`: confirm they work when the Browser tab (not the Builder tab) is the visible tab (anything that assumes the Advanced tab is visible), and who calls each (`HamburgerThemeItem_Click`, the Layout size handler, `SwitchActiveLayoutSize`, F11-out). Confirm the restore points end at the loaded theme and size.
4. The two persist methods (`ThemeBuilderWindow.PersistSettingsToDisk` and `MainViewModel.PersistConfigurationToDisk`): behavior on failure, and which fields are serialized. Confirm which one to call so a write failure can be caught and reported (expected: the window's throwing one in its own try/catch), and that no unsaved Builder state (pending custom size, unsaved edits) can reach `settings.json` through it after `LoadTheme` has replaced the state, beyond what the Builder's Load button already does.
5. `RebuildSavedSizes`: the exact code that builds the **active** saved-size button (which `SetResourceReference` keys for Background and Foreground, the Bold weight, the hover handling) and how it respects the `ThemedActionButtonStyle` rule. The Browser's highlight must copy this exactly, per button, without extracting a factory.
6. The current `ThemeBrowserControl` (Handoff 3): how rows and buttons are held, `RebuildList`, the skip-rebuild logic in `RefreshCatalog`, how scroll is saved and restored, and the best way to add `RefreshActiveMarks()` (update the highlight and the `(active)` marker **in place** without rebuilding rows or re-reading zips, so scroll position is untouched). If updating in place is not safe with `ThemedActionButtonStyle`, say how you would rebuild only the buttons panels.
7. Layout-less zips: can the current save path (`SaveThemeCore`), Copy Current Theme, template creation (`CreateFromTemplate`, `ThemeTemplateStore.ListValid`) or anything else produce a zip with no `Layouts`? If yes, stop and report.
8. The same-theme reload path again: what `ThemeExtractionStarting` / `ReleaseCustomLayers` releases today (boot splash video, shape and custom files, Random Game Display file, `MainBgVideo`, the Theme Info thumbnail, etc.), whether the animated splash and any other image or media is still held after the audit, and any code added since the audit that changes this.
9. The existing user-facing failure messages for a theme load (titles and texts) to reuse, how the Builder Load button reports a `ThemeLoadException` and a generic exception, and how `ThemeLog` should record a persist failure after a successful load without inventing a new action name (propose using the existing `Load` action with a detail text, or say if a new name is needed).
10. The next free decision number in `themes.md`, the sections that describe the theme-switch rules (3.19) and the list of callers of `RunThemeSwitch`, and any mismatch between this file or `themes.md` and the code.

Then stop. Edits start only after the user says "go".

## 5. STEP 2: the edits (only after "go")

### 5.1 `MainViewModel`: optional target size

- `public bool LoadTheme(string themeName, (int Width, int Height)? targetSize = null)` and the same optional parameter on `LoadThemeCore` (and anything between). When `targetSize` is null, behavior is **identical to today** for every existing caller.
- In the place where the active size is chosen from the zip, use `targetSize` instead of the zip's `ActiveWidth/ActiveHeight` **only if the theme's layouts contain that exact size**; otherwise keep the existing choice (the zip's size). Everything after it (`SavedThemeLayouts`, `SavedThemeWidth/Height`, the refreshes) stays as is. Update the method summary comments.
- No other change to load behavior, staging, swap, logging or messages.

### 5.2 `ThemeBuilderWindow`: the Browser load entry point

- A private method `LoadThemeFromBrowser(string themeName, int width, int height)` (with a one-line summary comment), wired in the constructor to `ThemeBrowser.LoadRequested`. It mirrors the Builder Load button's runner pattern:
  - `Func<bool> load = () => { if (!_viewModel.LoadTheme(themeName, (width, height))) return false; RefreshAfterExternalThemeSwitch(); /* persist in its own try/catch, see below */ return true; };`
  - run it through `MainWindow.RunThemeSwitch(load)` when `Owner is MainWindow`, else call `load()`.
  - **Persist** (after the Builder refresh, inside the same load func): call `PersistSettingsToDisk()` in a try/catch. On failure: record it in `theme.log` (per STEP 1 item 9), show a short message such as "'X' was loaded, but the setting couldn't be saved. It will not be remembered the next time you start the app.\n\nDetails: ..." (title "Theme Loaded"), and still return true so Smart fullscreen handling runs.
  - **Failure handling** around the runner: `ThemeLoadException` -> "Theme Not Loaded" with its message (as the Builder Load button does); any other exception -> a generic failure message in the same style as the Builder Load button; **`LoadTheme` returned false** (zip not found) -> a message "'X' couldn't be found. It may have been moved or deleted." (title "Theme Not Loaded"). In every failure case: nothing is persisted, the previous theme stays active, the Browser stays open, and the Browser's catalog is refreshed (`ThemeBrowser.RefreshCatalog()`) so a vanished or broken theme drops out of the list, with the scroll position preserved.
- `RefreshAfterExternalThemeSwitch()` and `RefreshAfterExternalSizeSwitch()` each get one added line calling `ThemeBrowser.RefreshActiveMarks()` (it must be safe when the Browser has not been shown yet). Do **not** add it to `RefreshAfterThemeLoad` (the Builder Load button works on the Builder tab and the tab-show refresh covers it). Do not touch the restore-point code, the Closed handler or Save Adjustments.

### 5.3 `ThemeBrowserControl`: clicks, highlight, guard

- `public event Action<string, int, int>? LoadRequested;`
- Each size button gets a Click handler. The handler first checks **at click time**: if the entry's theme is the active theme (`_settings.ActiveThemeName`, case-insensitive) **and** the size equals `_settings.ActiveResolutionWidth/Height`, it returns immediately (no event, no guard, no flicker, no log). Then the busy guard: if a load is running or was just finished and the idle clear has not happened yet, ignore the click. Otherwise set the guard, raise `LoadRequested(themeName, width, height)`, and clear the guard through `Dispatcher.BeginInvoke(..., DispatcherPriority.ApplicationIdle)` in a `finally` so queued clicks are ignored but the Browser is never left locked.
- The **highlight** on the button of the active theme at the active resolution copies exactly what `RebuildSavedSizes` does for its active button (same keys, bold, nothing else on the style; read STEP 1 item 5). Non-active buttons stay on the plain `ThemedActionButtonStyle`. The active button is **not** disabled, and shows no special cursor.
- Next to the active theme's name add the temporary text ` (active)` (a comment says Handoff 5 replaces it with the check-circle glyph). The active theme is matched with `ActiveThemeName`, case-insensitive; if the active theme is not in the list (hidden for the Browser, or deleted) nothing is marked.
- `public void RefreshActiveMarks()`: updates the highlight and the marker on the existing rows **in place** (or rebuilds only each row's buttons panel if STEP 1 says that is the only safe way) without re-reading zips, without touching the diagnostics counters, and without changing the scroll position. Call it at the end of **every** `RefreshCatalog()` (including the path that skips rebuilding the list) and from the two `RefreshAfterExternal*` hooks.
- A theme entry with no sizes shows the text `No saved resolutions` in place of the buttons (replacing the empty "Resolutions:" row content), with no click behavior; keep the active marker working for it. Comment why (legacy themes, decision in this handoff).
- Text for all new elements uses the Global Standard Text resource references already used in this control. No new brushes, no hex colors, no new Global or Theme Builder properties.

### 5.4 Messages

Keep all new user-facing strings in the same style as neighbors (titles "Theme Not Loaded" and "Theme Loaded"). No new dialog types.

### 5.5 Documentation (end of pass)

- `themes.md`: update 3.26 (Browser catalog) with the load behavior, the click table, the non-actionable active button, the busy guard, `RefreshActiveMarks` and when it runs, the layout-less decision, the failure handling, and the temporary `(active)` marker; add a decision (next free number) summarizing the one-operation load with the optional `LoadTheme` target size and the "persist after refresh, caught separately" rule; update 3.19 (theme-switch rules) so the list of `RunThemeSwitch` callers includes the Browser; update 3.22's remark on the Browser; add a dated change-log entry (2026-11-09 or the date of the pass). Note that the hamburger's refusal to reload the active theme is unchanged.
- `CLAUDE.md`: in "Smart fullscreen" (theme switch paragraph) add the Browser as another caller through `ThemeBuilderWindow.LoadThemeFromBrowser`; in "Theme file safety" add: `LoadTheme` takes an optional target size, applied only if the theme contains that layout, so a theme and resolution load is one operation; a Browser click on the active theme and resolution is a no-op; a persist failure after a successful load is reported separately and does not skip the fullscreen handling.
- Keep `.md` files LF and `.cs` / `.xaml` CRLF as `CLAUDE.md` describes; check `git diff --stat` shows no whole-file change.

## 6. Constraints and things NOT to touch

- No Smart fullscreen code changes (`RunThemeSwitch`, `HandleThemeSwitchForFullscreen`, `RunSizePick`, the adopt/restore logic, `ApplyLayout` checks) and no change to the hamburger's theme or Layout size handlers (including its refusal to reload the active theme).
- No change to the Builder's resolution controls, `RebuildSavedSizes`, `BtnSavedSize_Click`, the combo, `ThemesManagerControl`, Save/Delete/Create flows or `SaveThemeCore`.
- No unsaved-changes prompt or tracker. No new `theme.log` action names unless STEP 1 item 9 shows one is unavoidable (then ask).
- No final Browser styling: no layout rework, divider, ALL CAPS name, check glyph, instruction line, hover overlay, magnifier, pop-out, dim layer or Chrome Palette additions (Handoffs 5 and 6). No resolution-button factory.
- No per-aspect-ratio thumbnails; do not change the catalog reader or its cache, except if STEP 1 proves it must (then ask).
- Never write to a theme ZIP, never write a theme file from a Browser load. `settings.json` is written exactly once per successful load by the persist call, never on a no-op click or a failed load.
- Parked and out of scope: the `[New]` combo, resolution save-on-create, fixed Builder typography, Ctrl+T focus improvements.
- Keep comment fencing and the one-line `// Does X` summary on each method you touch or add. No unrelated fixes bundled in.
- Never commit.

## 7. Verification

You (Claude Code):
1. `dotnet build` finishes with **0 errors and 0 warnings**. No transient errors are expected if `LoadTheme`'s optional parameter is added before the Browser code that uses it.
2. Grep: every existing `LoadTheme(` caller still compiles unchanged and passes no target size; no new call to `RunSizePick`/`SwitchActiveLayoutSize` from the Browser path; no write to a theme zip from the Browser path.
3. `git diff --stat` shows only the files in section 2 (plus the docs), with no whole-file line-ending changes.

The user (cannot be done without running the app; do not run any screen-capture or input scripts yourself). Use a copy of the data folder if you want to be careful with the failure tests. Have at least two themes with different sizes (for example A with 720p and 1080p, B with 720p and 1080p, ideally one 4:3 size), one theme with an animated GIF splash, and one with a video wallpaper.
1. **Different theme, same resolution.** A/720p active. In the Browser click B/720p: B loads, the window stays open, the Builder's Load Theme combo now shows B, B's 720p button is highlighted with `(active)` next to B and A's marks are gone, the scroll position did not change. `settings.json` now names B and 720p; the theme zips' timestamps did not change. Close and restart the app: B at 720p.
2. **Different theme, different resolution.** A/720p -> B/1080p: one load, B appears at 1080p with no visible flash of 720p on the main window, only B's 1080p button is highlighted, `settings.json` names B and 1080p (written once; check the timestamp moves once). Restart: B at 1080p.
3. **Same theme, other resolution.** With B active at 1080p, click B/720p: a full reload at 720p. First make unsaved Builder edits (a layer move at 1080p, a layer move at 720p if available, a Global value change, a Theme Info description edit): all are gone after the load, with no prompt, and the Global values are back to the saved ones.
4. **Active button is inert.** With unsaved edits made, click the highlighted button (mouse, then Space/Enter via keyboard focus): nothing happens, the edits remain, no flicker, `settings.json` timestamp unchanged, and `theme.log` shows no new Load line.
5. **Browsing preserves edits.** Make an unsaved edit, visit the Browser tab, enlarge nothing, scroll, switch back to the Builder: the edit is still there (regression check). Only a real load discards.
6. **Rapid clicks.** Double-click and click-click on non-active buttons: exactly one load happens (one Load line in `theme.log`), the Browser is not stuck afterwards, and a later click works.
7. **Scroll and live styling.** With enough themes to scroll, scroll to the middle and load a theme whose Global fonts/colors differ: the Browser's text restyles live and the scroll position stays.
8. **Closing the Builder after a load.** After loading B at 1080p (theme saved at 720p), close the Builder without saving: B stays loaded at 1080p (not reverted to 720p or to A). Reopen the Builder: it shows B at 1080p.
9. **Hamburger interaction while the Browser tab is showing.** Pick another theme from the hamburger Themes list: the Browser's highlight and `(active)` marker move without flicker or scroll jump. Pick a Layout size from the hamburger: the highlighted button moves to that size (or none is highlighted if the active size is not saved in the theme). Pick a theme that is hidden from the Browser (Hamburger menu only): no entry is marked.
10. **Smart fullscreen.** Repeat tests 1 to 3 in each of: windowed; plain fullscreen (Smart fullscreen preference off); Smart fullscreen on with a size that fits exactly; Smart fullscreen with a size that does not fit; a fullscreen entered automatically. The behavior matches what the Builder's Load button does today in the same situations (auto-drop to windowed only when auto-entered and not an exact fit; otherwise the Smart size test reruns); no flash of an intermediate size. Then press F11 out: the same result as after a Builder Load. Open the Builder in fullscreen after a Browser load: the usual adopt behavior.
11. **GIF splash and video reload.** With a theme that has an animated GIF splash, make it active and click another resolution of the **same** theme in the Browser, several times back and forth. Then repeat with a theme that has a video wallpaper (and a video shape/custom layer if you have one). Expected: no "may be using that theme's folder" message. If you see one, stop testing and report exactly which theme/asset and the message; do not work around it.
12. **Failures.** (a) With the list showing, delete B's zip in Explorer, then click B/720p: "Theme Not Loaded", the previous theme stays active and highlighted, `settings.json` is unchanged, the list drops B and keeps its scroll position. (b) With a **copy** of a theme, replace its zip with a text file of the same name after the list is showing, click it: "Theme Not Loaded" with a message, nothing persisted, old theme intact, Browser usable. (c) Open `Themes\Extracted\B` in a File Explorer window and click B in the Browser: the existing "may be using that theme's folder" refusal, state unchanged. (d) Optional: make `settings.json` read-only and load a theme: "loaded, but the setting couldn't be saved" message, the theme is loaded and Smart fullscreen handled; restore the file attribute afterward.
13. **Failure leaves the app usable.** After each failure in 12, loading a good theme from the Browser still works.
14. **Legacy/layout-less entry (optional, copies only).** Copy a theme zip, edit the copy's `theme.json` to have `"Layouts": []`, drop it in `Themes`: it is listed with `No saved resolutions`, no buttons, no crash; it still loads from the Builder's Load Theme and from the hamburger, after which the Browser shows `(active)` on it.
15. **No re-reads.** The diagnostics line does not report new zip reads after a theme load or a hamburger swap (marks update, catalog does not re-read).
16. Windows display scaling 125% and 150%: optional, can stay deferred.

## 8. Stop conditions

Stop and report (do not push on) if any of these happen:
- STEP 1 shows `LoadTheme`, `RunThemeSwitch` or the Builder refresh methods working differently from section 3 in a way that needs more than the edits in section 5, or `ThemeBuilderWindow` cannot call `RunThemeSwitch` the way `ThemeLoadRunner` does today.
- Applying a target size needs more than choosing the active size inside `LoadThemeCore`.
- Any current save, copy or template path can produce a layout-less zip.
- A same-theme resolution reload fails with a "may be using that theme's folder" refusal for a GIF splash, video wallpaper or other asset (report the asset; do not add workarounds in this pass).
- Persistence after the refresh writes more than one `settings.json` per load, or unsaved Builder state could reach `settings.json` beyond the Builder Load button's precedent.
- An edit would need a change to Smart fullscreen code, the hamburger handlers, the Builder's resolution controls, the catalog reader, or a Browser element reserved for Handoffs 5 and 6.
- The build shows errors or warnings you cannot clear within the files in section 2.

After STEP 2, report in this form: files changed, anything that did not match these instructions, anything changed beyond them, and the test checklist (section 7). Then stop. Handoff 5 (entry visuals) is a separate file, written only after the user reports back.
