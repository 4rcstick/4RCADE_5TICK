# Handoff: Verify themes.md and CLAUDE.md against the code, then update them (docs only)

You have no memory of the chat that produced this file, so everything you need is here. Project: **4RCADE 5TICK**, a C#/WPF MAME frontend. The developer is a first-time developer working independently.

**Follow the pass structure in CLAUDE.md:** do the read-only STEP 1 audit, report the mismatches, then **stop and wait. STEP 2 (the edits) starts only after the developer says "go".** This pass changes **documentation only**: no `.cs`, `.xaml`, `.json`, theme or zip file may be modified. Do not commit.

## Goal and why

Between 2026-10-30 and 2026-11-01 a lot changed: the Theme Info view (3.22), Restore Defaults with its 720p / 1080p sets and pop-up (decision 70), and a theme file safety series (Fix 1: `LoadedThemeName` and the Save target; Fix 2: `RewriteThemeAssetPath`; Fix 3: safe Load with `ThemeFolderSwap`; Fix 4: safe Save, safe Delete Theme, `ThemeFileStore`, `ThemeLog`; decisions 71 and 72). The docs were updated along the way, but "Last verified against code" still says 2026-10-30, and the audits and test sessions turned up items that were never written down. The developer wants the docs checked against the code and brought up to date.

Trust order (from CLAUDE.md): **the code first, themes.md second.** Where they disagree, the code wins: report the mismatch and correct the doc.

## Facts from the developer (use these, they override anything the docs currently say)

- **Default theme data.** The last change log entry (2026-11-01, "Docs and theme data after the theme file fixes") claims damaged themes were "recreated by hand rather than repaired." That is not what happened. What happened: the developer overwrote nothing by repair. Default was fixed by **deleting the size that held the bad paths, running Restore Defaults ("Everything") for 720p and 1080p, and saving**; Default now has the two 16:9 sizes (1280x720 and 1920x1080). Every other existing theme (Electronic, Football, Resize Test, Steampunk, Arcade, Cyndi's and so on) is an **expendable test theme that was left as is; no repair was done on them and none is planned.** Rewrite that entry accordingly.
- **Shipped `default.zip` and `DefaultLayerSets.json`:** do not worry about what is in the shipped default zip, as long as it has the two 16:9 sizes. Do not modify either file or any theme or zip. If a doc line states something about the shipped default zip's contents that the code or files contradict, report it in STEP 1, but do not "fix" the file.

## STEP 1: read-only audit (report, then stop)

### A. Verify existing claims against the code
For each item, find the doc text, check the code, and report **match / mismatch / cannot tell**, with file and method for the evidence. Do not rely on the docs' own wording.

1. **Theme file safety** (themes.md 3.4, section 7, decisions 71 and 72, the file map rows for `ThemeFolderSwap`, `ThemeFileStore`, `ThemeLog`, `DefaultLayerSets`; and the whole "Theme file safety" section in CLAUDE.md):
   - Load: staging in `Themes\_staging`, rename-aside swap with rollback, `ThemeLoadException` shown as "Theme Not Loaded", `LoadedThemeName` set together with the layouts.
   - Save: Temp staging folder, ask first when files are missing (No default), `<name>.zip.tmp-<guid>` in `Themes`, verified, swapped with `ThemeFileStore.ReplaceZip`, one `<name>.zip.bak`, and layouts / restore point / names / thumbnail write / `ModifiedUtc` moving only after the swap.
   - Delete Theme: `ThemeFileStore.DeleteTheme`, working folder renamed aside first, zip moved to `Themes\Deleted` (check the exact file naming pattern the docs state, `<name>_<date>-<time>.zip`), rollback, and `LoadedThemeName` / `ActiveThemeName` cleared when the loaded theme is deleted; confirm what the next launch actually does.
   - `ThemeLog`: location (`4rcade5tick_files/logs/theme.log`), which events write lines, ordered writes on a background task, errors swallowed, roll to `theme.log.1` past 1 MB.
   - `RewriteThemeAssetPath` rule (only `assets/` or `assets\`, any case, is zip-relative) and every caller (Load, the thumbnail, the startup snapshot).
   - Theme lists read only `*.zip` directly in `Themes` (so `.bak`, `.tmp-`, `_staging`, `Deleted` stay out).
   - "Keep the active layout object across a save" (`_currentLayout`; only its `Layers` list changes).
   - `ReleaseCustomLayers` also releases the wallpaper and boot splash videos (claimed in the 2026-11-01 Safe Load change log entry).
2. **Restore Defaults** (decision 70, 3.4, file map): the pop-up (Layout only / Everything / Cancel, No default button), `GetDefaultsForHeight` (height only, under 1080 = 720p set), what Layout only vs Everything copy, Visible forced true, custom layers skipped, Main Window Background keeps the active size, `NormalizeZ` after, the embedded JSON resource, and the `assets/x` to `4rcade5tick_files/assets/<file>` resolution.
3. **Theme Info view** (3.22): the claims that can be checked cheaply (the four controls, `ThemeThumbnailStore.RemoveUnusedFolders` timing, Save Changes thumbnail recovery from the old zip, `ApplyThemeSettings` copying `Info`).
4. **Header and counts:** the "Last verified against code" line (what it covers vs. the change log), the decision numbering and formatting (decisions 70 to 72 sit after a blank line; 3.22 sits after a `---` rule and before section 4), Game Info "7 parts" and the count in section 8, and the file map for any file that exists but has no row, or a row for a file that no longer exists.
5. **CLAUDE.md** beyond Theme file safety: the Theme Builder Status block (does it mention Restore Defaults and the Theme Info view accurately?), the Architecture in Brief theme line, and anything else that now contradicts the code.

### B. Verify the items that were found in audits and never written down
For each, confirm in the code whether it is real and report the exact behavior:

1. **`_1` numbered duplicate files inside zips.** `BundleThemeAsset` numbers an asset when two sizes point at same-named files in different folders (around the "numbering" code in `ThemesManagerControl`). Does Restore Defaults "Everything" cause it (the default images are the same files at several sizes)? Is it avoidable when the files are identical?
2. **Restore Defaults resets only the active size.** Other sizes keep their paths.
3. **Ambiguous top-level `assets` path.** A file browsed from `<app folder>\assets\` that is missing at save time looks like a zip path to `RewriteThemeAssetPath`. Confirm and describe the effect.
4. **Already-stacked paths.** Confirm that Load does not repair a path that was stacked before Fix 2 (it only stops growth), and what the user sees (Some Assets Missing prompt on each Save). Note the idea of making Load tolerate stacked paths (keep only the part after the last `assets\`) as a possible future change.
5. **Theme names are not validated.** Are characters like `\`, `/`, `..` or reserved Windows names blocked anywhere for `[New Theme...]`, Save As, Rename?
6. **Swallowed errors.** The first-run `LoadTheme("default")` and `LoadSavedThemeSnapshot` hide errors; confirm what is logged, if anything, now that `ThemeLog` exists.
7. **Nothing prunes `Themes\Deleted` or the `.bak` files** (confirm, and whether anything stops them growing).
8. **`LoadedThemeName` startup trust.** At startup it is taken from `ActiveThemeName`. If `settings.json` holds another theme's layouts, Save is not blocked (this is how `Default.zip` got overwritten before the fixes). Confirm exactly where it is initialized and what is (not) checked.
9. **Lock behavior across all three operations.** Confirm what happens with a locked folder or zip for Load, Save (zip locked, folder locked) and Delete Theme, including an open File Explorer window on the theme's folder.
10. **Photoshop-style lock cause.** An image editor that remembers a theme's `assets` folder as its save destination keeps that folder locked; this is what started the data loss. Nothing to verify in code; just confirm no doc already says it.

### C. STEP 1 report
Give a table of every mismatch (doc location, what the doc says, what the code says, proposed fix), then the results of A and B, then your proposed STEP 2 edit list (below, adjusted by what you found). **Then stop and wait for the developer's "go".**

## STEP 2: edits (only after "go")

Documentation files only: `themes.md` and `CLAUDE.md` (both LF; keep their endings). Keep the existing style and section/fencing conventions. Do not rewrite sections that are correct. Make the following edits, adjusted by the STEP 1 findings:

**themes.md**
1. **Change log:** replace the last entry (2026-11-01 "Docs and theme data after the theme file fixes") with an accurate one: `CLAUDE.md` gained the Theme file safety section and its stale `LoadTheme` line was fixed; the Default theme was fixed by deleting the size with the bad paths, running Restore Defaults "Everything" for 720p and 1080p, and saving (Default now has the two 16:9 sizes); the other themes are expendable test themes left as they were, no repair was done and no repair tool was built; a path stacked before Fix 2 no longer grows and shows in the Some Assets Missing prompt. Add one entry for this docs pass.
2. **Header:** update "Last verified against code" to today's date only for what STEP 1 actually verified, and add the Theme Info, Restore Defaults and theme file safety passes to its list in the same compact style (keep it short; do not bloat it).
3. **Section 6b (parked ideas):** add entries for the confirmed items from B that are not yet recorded, each one or two lines: the `_1` numbered duplicate files (and an avoidance idea if STEP 1 found one), Restore Defaults only resetting the active size (a possible "all sizes" option), the ambiguous top-level `assets` path, Load tolerating already-stacked paths, theme name validation, swallowed errors in the first-run load and startup snapshot (log them through `ThemeLog`), pruning of `Themes\Deleted` and `.bak` files.
4. **Section 7 (traps):** add the `LoadedThemeName` startup-trust trap, and widen the existing lock note so it covers Load, Save and Delete Theme (a lock refuses the operation and leaves the old files, including an open File Explorer window), if STEP 1 confirms it.
5. **Theme author note (6b, next to the existing "Theme author note for release"):** add one line: do not export or "Save for Web" from an image editor directly into a theme's `assets` folder. An editor that remembers that folder keeps it locked, and a locked folder refuses Load and Delete. Export to a folder outside `4rcade5tick_files` and add the file to the theme from there.
6. Fix any formatting or numbering oddities STEP 1 found (for example the 3.22 placement), keeping text unchanged.

**CLAUDE.md**
1. Add a short **Restore Defaults** gotcha under Known Gotchas: the sets come from the default theme's `theme.json` via the embedded `Models/DefaultLayerSets.json` (regenerate it if the default theme changes), and `default.png`, `default_background.png`, `default_background.gif` and `no_preview.png` must ship in `4rcade5tick_files/assets` or "Everything" shows blank images.
2. Add a short **release checklist** note (can sit under Known Gotchas or Current Scope Boundaries, wherever fits best): the four default images above, the `theme.log` / `.bak` / `Deleted` files are runtime files that must not be committed or shipped, and the shipped Default theme needs the two 16:9 sizes (1280x720 and 1920x1080).
3. Fix anything else STEP 1 found stale (Theme Builder Status block, Architecture in Brief).
4. Keep CLAUDE.md concise; it is read at the start of every session.

## Constraints

- **Docs only.** No source, project, JSON, theme or zip file changes. Do not touch `DefaultLayerSets.json`, the shipped `default.zip`, or anything under `F:\Arcade Stick\Data\`.
- Do not rewrite or reflow correct text. No new decisions numbers unless STEP 1 shows a real decision missing from the log (report it instead and let the developer decide).
- No commits. After edits run `git diff --stat` and confirm only `themes.md` and `CLAUDE.md` changed, with no whole-file diffs (line endings intact).
- A build is not needed if only the two `.md` files changed; say so in your report. If a code file shows a change, stop and tell the developer.

## Final report (after STEP 2)

Files changed; every mismatch found and how it was fixed; anything that did not match these instructions; anything changed beyond them; a short list of what the developer should re-read to check (the new change log entry, the new 6b entries, the new traps, the two CLAUDE.md additions).
