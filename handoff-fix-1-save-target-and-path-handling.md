# Handoff: Fix 1 and Fix 2. Stop Save from overwriting the wrong theme, and stop ImagePath from growing on every save/load

You have no memory of the chat that produced this file, so everything you need is here. Project: **4RCADE 5TICK**, a C#/WPF MAME frontend. The developer is a first-time developer working independently, so keep changes readable and explain anything non-obvious in your final report.

This is the **first of several fix handoffs**. It covers only the two fixes below. Do NOT start on the other fixes listed under "Not in this handoff".

## Goal and why

A read-only audit found two bugs that corrupt themes:

1. **Save writes to the wrong theme.** `BtnSaveTheme_Click` (`Views/ThemesManagerControl.xaml.cs`, around line 214) takes the **zip name** from the dropdown selection (`CboThemePresets.SelectedItem`) but writes the layouts, info and global values from the **in-memory settings** (`_settings.Layouts` around line 323, `_settings.Info` around line 301, `_settings.*` from around line 238). Nothing checks that the dropdown theme is the theme those layouts came from. Selecting a theme in the dropdown without pressing Load, then pressing Save Changes, overwrites that theme's zip with whatever theme is actually loaded. This is how `Default.zip` got overwritten with another theme's data. Around line 382, `ActiveThemeName` is then set to the dropdown value, which hides the mismatch.

2. **Stored image paths grow on every save/load round.** Save and Load disagree about what a non-rooted path means when the file is missing:
   - **Save:** `BundleThemeAsset` (`ThemesManagerControl.xaml.cs` around lines 149-153) returns the path unchanged when the file can't be found. A live path that is relative to the app folder (for example `4rcade5tick_files\assets\default.png`) is then written into `theme.json` as if it were a path inside the zip.
   - **Load:** `RewriteAssetPath` (`ViewModels/MainViewModel.cs` around lines 1534-1540) treats **every** non-rooted path as a path inside the zip and puts the theme's extracted-folder path in front of it. Its comment assumes a missing file always has a rooted path, which is wrong.
   - **Startup:** `LoadSavedThemeSnapshot` (`MainViewModel.cs` around lines 241-244) makes the same mistake for the restore point.
   - Result: each save+load round adds one more copy of `4rcade5tick_files\Themes\Extracted\<name>\` in front of the path, so a missing file can never be found and the path keeps growing. Real example found in a saved theme.json: `4rcade5tick_files\Themes\Extracted\Resize Test\4rcade5tick_files\Themes\Extracted\Resize Test\assets\no_preview.png`.

Line numbers come from an audit done earlier today and may have shifted. Re-locate each piece by name before editing.

## Relevant background (verified in the audit)

- The theme **zip** (`Themes\<name>.zip`) is the source of truth. `Themes\Extracted\<name>\` is a working copy.
- Normal, healthy theme.json image paths are either empty, or `assets/<file>` (a path inside the zip), or a rooted path. Load converts `assets/<file>` to the extracted-folder path.
- Save only bundles files that are referenced by an `ImagePath`. It shows a "Some Assets Missing" warning when a file can't be found, then still saves.
- The thumbnail path goes through the same rewrite, but has a fallback near `MainViewModel.cs` line 1591.
- Restore Defaults (added earlier today) stores default image paths as `4rcade5tick_files\assets\<file>`, relative to the app folder. That is a valid, intended value. It must keep working.

## Exact changes wanted

### Fix 2 (path handling). Do this first.

Make Load, the startup snapshot, and any other place that interprets a stored path agree on one rule:

- A stored path is a **zip-relative asset path** only if it starts with `assets/` (accept `assets\` as well, and be case-insensitive). Those get the extracted-folder prefix on load, as today.
- Any other non-empty path (rooted, or relative to the app folder such as `4rcade5tick_files\assets\default.png`) is **left exactly as stored**. Do not prefix it.
- Fix both `RewriteAssetPath` and the same logic in `LoadSavedThemeSnapshot`, and check for any other copy of this logic (search for other callers and for places that combine a path with the extracted-theme folder). Include the thumbnail path handling so it follows the same rule.
- In `BundleThemeAsset`, keep the current behavior of recording a missing file for the "Some Assets Missing" warning, and leave the stored value unchanged. With the Load rule above, an unchanged missing path no longer grows. Do not blank it.
- Update the misleading comment that says a missing file always has a rooted path.
- Do not try to repair already-damaged theme.json files in this handoff. A separate repair step comes later. (Optional: if there is a cheap, safe way to make Load tolerate an already-stacked path without modifying files, mention it in your report as a suggestion, but do not implement it.)

### Fix 1 (Save target)

Add a record of **which theme the in-memory layouts actually came from**, and make Save refuse to write to a different theme.

- Add a field such as `LoadedThemeName` (put it wherever fits the existing structure, e.g. `MainViewModel`). It must be set **only when a theme load completes successfully**, and set to the new theme's name when Save succeeds under a new name (Save As flow via `[New Theme...]`). If a load **fails**, it must not change.
- Work out what it should be at app startup. The in-memory layouts at startup come from `settings.json` and the restore-point snapshot, which belong to `ActiveThemeName`. Initialize it from that, and report in your final summary what you chose and why.
- In `BtnSaveTheme_Click`: if the dropdown selection is an existing theme that is **not** equal to `LoadedThemeName`, **do not save**. Show a clear message such as: "The theme list shows '<dropdown>' but the loaded theme is '<loaded>'. Press Load Theme on '<dropdown>' first, or select '<loaded>', so nothing gets overwritten." Match the app's existing message box style.
- The `[New Theme...]` flow stays: it asks for a name and creates a new theme. **If the typed name already exists, ask for confirmation to overwrite, with No as the default button.** (Currently it overwrites silently.)
- Make sure `ActiveThemeName` (around line 382) is set from the theme that was **actually saved**, not blindly from the dropdown.
- If a Load fails after wiping folders, `LoadedThemeName` must keep the previous value. That means the mismatch check will block a Save while the dropdown shows the theme that failed to load, which is the desired protection. Check what the dropdown shows after a failed load and describe it in your report.
- Check any other code that uses the dropdown selection as "the current theme" for writing (search for other uses of `CboThemePresets.SelectedItem`). **Do not change Delete Theme in this handoff** (it is covered later), but list in your report any other write paths that have the same mismatch problem.

## Constraints: things NOT to touch

- **Do not change Load's delete/extract order, Save's delete-then-write order, or Delete Theme's order.** Making those safe is a later handoff. Only change what is described above.
- Do not change Restore Defaults, `DefaultLayerSets.*`, or the default layer data.
- Do not change the theme.json schema or version.
- Do not repair or modify any existing theme data. **Do not read, write, delete or run anything against `F:\Arcade Stick\Data\4RCADE5TICK_files\Themes` or any theme zip.** If you want to test path logic, use a throwaway console snippet or unit-style check that works on strings only, or temp files in a temp folder.
- No new package dependencies. Keep the diff focused. No commits unless the developer asks.

## Expected build errors

None expected if done in one pass. If you change a method signature or add a field across `MainViewModel` and `ThemesManagerControl`, the build may fail between edits. Finish all edits before building.

## How to verify

Build first (0 errors; report any new warnings). Then tell the developer exactly what to test. The developer will test in the running app with **throwaway themes only**, so write the checklist for them:

1. **Mismatch is blocked.** Create two throwaway themes A and B. Load A. In the dropdown select B **without** pressing Load. Press Save Changes. Expected: the message appears and `B.zip` is unchanged (check its Modified time).
2. **Normal save still works.** Load A, change something, Save Changes. Expected: it saves and `A.zip` updates.
3. **Save As and overwrite prompt.** Use `[New Theme...]` with a new name: it saves. Use it again with an existing name: it asks to overwrite with No as the default; choosing No changes nothing.
4. **Failed-load protection.** (Optional, only if easy.) Cause a load to fail and confirm Save is blocked afterward.
5. **Path growth is gone.** On a throwaway theme: run Restore Defaults "Everything" so layers point at `4rcade5tick_files\assets\...` images. Temporarily rename one of those files in `F:\Arcade Stick\Data\4RCADE5TICK_files\assets` (for example `default.png`). Save Changes (expect the "Some Assets Missing" popup), then Load that theme, Save, Load, Save again. Open that theme's `theme.json` and confirm the `ImagePath` for that layer is the same each round and does **not** gain copies of `Themes\Extracted\<name>\`. **Rename the file back afterward.**
6. **Existing good behavior.** Save a theme whose images all exist, load it, and confirm images show and bundled paths are `assets/<file>`.

## Final report

When done, report: which files you changed; the exact rule now used for zip-relative vs. other paths; where `LoadedThemeName` is set and what it initializes to at startup; what the dropdown shows after a failed load; any other write paths that use the dropdown as the target; anything you decided that this file did not cover; build result. Suggest what the next fix handoff should cover based on what you saw.

## Not in this handoff (do not do these)

Safe Save (write temp zip then swap), safe Load (extract to temp first, then swap), Delete Theme order and prompt, warning before saving with missing assets, Restore Defaults messaging and `_1` duplicates, a theme log, and repair of already-damaged themes. These come later, one handoff at a time.
