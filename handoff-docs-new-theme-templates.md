# Handoff: docs update for the New Theme button and Templates (Passes 1 and 2)

Docs only. **Change no code.** Read CLAUDE.md and themes.md in full first. Trust order: the code first, themes.md second. Both .md files are LF; keep their endings and run `git diff --stat` afterwards to check for whole-file changes. Never commit.

**Workflow:** do the read-only STEP 1 verification, report, then **stop and wait**. STEP 2 (applying the edits) starts only after the user says "go".

## Background

Two passes are built, tested and committed:

- **Pass 1:** a **New Theme** button replaces the `[New Theme...]` combo entry and the inline New Theme Name box. The row is now New Theme, Load, Save Changes, Delete (tooltips on all four; the intro line mentions creating a theme). The button opens a menu built in code under the button; Copy Current Theme opens a code-built name popup (Create / Cancel, inline errors in the new `ChromeError` brush, popup stays open until valid). Create validates with `ThemePaths.TryValidateNewName` (Check 1), **refuses an existing name (case-insensitive) and `default` instead of overwriting**, then saves through `SaveThemeCore` (Save logic factored out of `BtnSaveTheme_Click`, no reordering), shows the Theme Saved flash and loads the new theme through `ThemeLoadRunner`. The combo is **empty** when no theme is active (no more fallback selection); Save Changes with no selection says "Select a theme, or use New Theme." ThemeLog gained `Create` lines and `SaveBlocked` lines for refused names (empty names included).
- **Pass 2:** **Templates.** `4rcade5tick_files\Templates\*.json` files (bare `theme.json`-shaped, not zips) appear in the New Theme menu under Copy Current Theme, after a separator, sorted by name, labeled with the file name without `.json`. New `Services/ThemeTemplateStore.cs` lists and validates them (read-only, never creates or writes the folder, BOM-safe read, 2 MB cap, name checked with `TryValidateNewName`, skipped when unreadable, no layouts, a layout with no layers or an empty layer entry, or a layout width or height of 0 or less). Creating from a template re-reads it, sets the active size (1280x720 if present, else the height closest to 720, ties to the smaller height, then the smaller width), blanks the Info block, writes a zip holding only `theme.json` with the existing temp-zip, verify and swap helpers, shows a **flash-only** callback (`ThemeCreatedFlash`, one line in ThemeBuilderWindow; it deliberately does not reuse the saved callback, which moves restore points and runs `RemoveUnusedFolders`), then loads through `ThemeLoadRunner`. All layouts in the file are saved. Nothing live changes until the load succeeds. A load failure after the zip is written shows "'X' was created, but couldn't be loaded. '<old>' is still the loaded theme. Press Load to try again." and selects the new name; Copy Current Theme keeps its own wording ("still using the previous theme's image files") through the shared `LoadCreatedTheme`. Template `assets/...` paths are allowed with one `TemplateWarning` per session. Log lines: `Create ... From template <name>` (with "; created, load failed" when applicable), `TemplateSkipped`, `TemplateWarning`, each skip or warning once per path and reason per session. Unsaved edits on the old theme are dropped without a prompt when creating from a template, as Load does.
- **Tested facts:** the user's first template is `Templates\template.json` (29 KB, 1280x720 and 1920x1080, all image paths empty, "Disable background image" ticked at both sizes, slate colors). Creating from it gave a zip with only `theme.json`. With Smart fullscreen on and the Theme Builder open, a create switches the display to the new theme's active size (the builder rule); closing the builder returns to the best size. F11 does not reach the main window while the builder has focus (parked).

## STEP 1: read-only verification (report, then stop)

1. Read the code for both passes and check the Background above against it. Report anything that is wrong or that I got wrong (labels, messages, log text, rules, file names, the flash callback, `ChromeError` value).
2. Search CLAUDE.md and themes.md for every place that is now stale. At least check: the Themes Manager description in 3.4 and in the tab list ("Active Theme combo, inline New Theme Name box, Load Theme, Save Changes, Delete Theme"); the `[New Theme...]` mentions in 3.4 (Save target and typed-name wording), 3.22 (the "`[New Theme...]` save" Info rule), section 7 (the `ThemePaths` trap and decision 73's "typed new name" wording), the `ThemesManagerControl` file map row and the new `ThemeTemplateStore` row, the `ThemeLog` file map row (action names), 3.14's Chrome Palette brush list (`ChromeError`), the Last verified header, the change log, 6b, CLAUDE.md's Architecture line for `ThemesManagerControl`, its "Theme file safety" section (Save, `LoadedThemeName`, `ThemePaths`, `theme.log` lines) and its release checklist. Report the stale places you find beyond this list.
3. Check the "Load Theme" / "Delete Theme" wording everywhere. The buttons now read **Load** and **Delete** (Save Changes is unchanged). Decide per mention whether to reword (user-facing button references) or keep (it names the action, not the label). List them for the user.
4. Check whether themes.md section 3.4's "Theme Saved flash" and the saved-callback wording needs the flash-only callback described.
5. Propose the new decision numbers (74 onward) and a short wording for each: (a) New Theme button, menu, name popup, no overwrite, `default` reserved, empty combo with no active theme; (b) Templates folder, format, read-only, skip rules, active-size rule, blank Info, flash-only callback, failure messages; (c) anything else you think is a real decision.
6. Propose the 6b parked entries (listed below) and any you found.
7. List what you plan to change in each file, then **stop**.

## STEP 2: apply (only after the user says "go")

Edit only the two .md files. Follow the existing style. Add:

- **themes.md:** updated 3.4 (Themes Manager row, New Theme flow, name rules, empty combo, tooltips, flash behavior); a new subsection (suggest **3.23 New Theme and Templates**) with the Background facts above, written as verified against the code; decisions 74 onward; section 7 traps (templates are read-only and never created; creating from a template must not use `SaveThemeCore` or the saved callback; the New Theme name popup is the only place that refuses `default`; a typed name never overwrites; the Templates folder lives in the data folder outside the repo); file map rows (`Services/ThemeTemplateStore.cs`, updated `ThemesManagerControl` and `ThemeLog` rows, `Templates\` folder note); 3.14 `ChromeError`; updated Last verified header; change log entries for both passes; 6b entries.
- **CLAUDE.md:** the New Theme and Templates conventions in the right place (Theme file safety: names only become paths through `ThemePaths`, templates are read-only input, a template path never goes through `SaveThemeCore`); the Architecture line for `ThemesManagerControl`; release checklist lines.
- **Release checklist (both files):** `Templates\<name>.json` ships in the data folder (outside the repo, like `Themes\Deleted` and the five assets), the first template `template.json` should be **renamed** before release (the menu label is the file name, for example `Blank.json`), and the shipped template must have all image paths empty and "Disable background image" ticked at every size.

**6b parked entries to add:**

- Rename "Save Changes" (for example to "Save") in one pass so every mention changes together; it is paired with Save Adjustments, which is why it stays for now.
- Forward F11 from the Theme Builder to the main window (today the user must click outside the builder to toggle fullscreen).
- The stale `.bak` risk: a leftover `<name>.zip.bak` with no matching zip can be renamed into place as `<name>.zip` if a new theme of that name hits a move failure in `ReplaceZip`. It is rare and already applies to Copy Current Theme; fixing it means `ThemeFileStore`.
- Creating from a template drops the old theme's unsaved edits without a prompt (as Load does); a "discard unsaved edits?" prompt would need an unsaved-state tracker.
- A template file can carry a display name of its own instead of using the file name; not built.
- More templates (dark, light, pastel) need no code: drop a file in `Templates`.

Also correct any other statement you find that the code contradicts.

## Report format

Files changed, every stale place found and what it was changed to, anything in the Background that did not match the code, the "Load Theme" / "Delete Theme" decisions made, and `git diff --stat`.
