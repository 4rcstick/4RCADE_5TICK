# Handoff: New Theme button, menu and name popup (Pass 1 of 2)

Read CLAUDE.md and themes.md first (themes.md 3.4, 3.19, section 7, decisions 71 to 73). Trust order: the code first, themes.md second. Report any mismatch.

**Workflow:** do the read-only STEP 1 audit, report, then **stop and wait**. STEP 2 starts only after the user says "go". Never commit.

## Goal and why

Replace the `[New Theme...]` entry in the Active Theme combo (and the inline New Theme Name box) with a **New Theme button** beside Load Theme, Save Changes and Delete Theme. The combo then only selects existing themes; buttons perform actions. This is Pass 1 of 2:

- **Pass 1 (this file):** the button, its menu, the name popup, and the **Copy Current Theme** path.
- **Pass 2 (later, separate handoff):** a `Templates` folder (`4rcade5tick_files\Templates\*.json`) whose files appear as extra menu entries. **Do not build any template code now**, but build the menu so Pass 2 can add entries without redesign.

## Decisions already made (do not reopen)

1. The New Theme button opens a context menu directly beneath the button (not at the mouse). In Pass 1 it holds one entry, **Copy Current Theme**. Pass 2 adds one entry per template file below it.
2. Choosing an entry opens a compact **name popup**: one text box, Create and Cancel. Errors show inline and the popup stays open for correction.
3. **Copy Current Theme copies what is in memory** (unsaved Advanced edits included), exactly as today's Save As New does.
4. After a valid name: save under that name (existing safe Save pipeline), show the **Theme Saved flash**, then **load the new theme** through the normal safe load path so it runs from its own Extracted folder, not the old theme's.
5. **Duplicate names are refused**, not overwritten: compare case-insensitively against existing themes, and also refuse `default` (the launch fallback). There is no overwrite prompt in this flow.
6. Cancel changes nothing: loaded theme, its properties and saved state stay as they were, and no files are written.
7. Author and description carry over, and no thumbnail or date is inherited, as the current `[New Theme...]` save does (themes.md 3.22).
8. Restore Defaults, `DefaultLayerSets.json` and the hard-coded defaults are **not touched**.

## STEP 1: read-only audit (report, then stop)

Read the code and answer:

1. **Current new-theme flow.** Where `[New Theme...]` is added to the combo, how `BtnSaveTheme_Click` handles a typed name (Check 1 validation, overwrite prompt, Info rule, `ActiveThemeName`, `LoadedThemeName`), and every other place that tests for the `[New Theme...]` string or the `TxtNewThemeName` box (Load and Delete button enabling, selection-changed handlers, `ReloadFromSettings`, ThemeBuilderWindow, hamburger).
2. **Reuse plan.** Can the existing Save path be called with a name from the popup without duplicating file logic? What would need to be factored out of `BtnSaveTheme_Click` (it is a click handler), and does the factoring keep the order of file operations unchanged?
3. **Save then load.** After the save swaps in the new zip, the in-memory image paths still point at the old theme's Extracted folder. Confirm that loading the new theme through `ThemeLoadRunner` / `RunThemeSwitch` (the same path as Load Theme) fixes this and keeps Smart fullscreen and the builder size handling correct. What happens to `LoadedThemeName`, `ActiveThemeName` and the restore points at each step?
4. **Failure states.** If the save succeeds and the load fails, what state are we in, and what message and combo state do we show? If the save fails, confirm nothing live changes. Propose the behavior; do not build it yet.
5. **Combo and list.** How the theme list is rebuilt after creation so the new name is selected, and how Cancel leaves the combo on the loaded theme.
6. **Name popup.** How `PromptForLayerName` and the other code-built prompts are built (Chrome Palette, `MakePromptButton`, `WindowFit`), so the new popup follows the same pattern. Can it validate and stay open on error, which `PromptForLayerName` may not do today?
7. **Existing-name check.** Where theme names are enumerated (`*.zip` directly in `Themes`), and how to do the case-insensitive and `default` refusal without applying Check 1 to existing names.
8. **ThemeLog.** Does it accept new action names? Propose a `Create` line on success and `SaveBlocked` lines for refused names, matching the existing style. Do not assume; report.
9. **Menu.** The builder cannot see MainWindow's implicit menu styles. Confirm the keyed `BuilderContextMenuStyle`, `BuilderMenuItemStyle` and `BuilderMenuSeparatorStyle` (App.xaml) are the right styles, that every row fills its background, and how to open the menu under the button (`PlacementTarget`, `Placement.Bottom`). Note anything that breaks if the menu has a single item.
10. **Button layout.** Confirm there is room beside the other three buttons at the current window width (717) without wrapping, at Windows scales up to 175%.
11. **Anything else** that could break: the hamburger Themes submenu, the Theme sizes row, Theme Info view, `RemoveUnusedFolders`, custom layers carried by the copy, and the first-run path.

List the files you expect to change and any decision for the user. If the code contradicts a decision above, say so instead of working around it.

## STEP 2: implementation (only after the user says "go")

**Changes wanted:**

1. **ThemesManagerControl.xaml:** add a **New Theme** button (`ThemedActionButtonStyle`, no local Background, Foreground, BorderBrush or BorderThickness) next to the existing three, placed so the row still fits. Remove the inline New Theme Name box (`TxtNewThemeName`) and its label. Keep every other `x:Name` and handler.
2. **ThemesManagerControl.xaml.cs:**
   - Stop adding `[New Theme...]` to the combo and remove code that tests for it; make sure Load Theme, Save Changes and Delete Theme behave correctly with a real theme always selected.
   - New button handler builds the menu in code each time it opens (keyed builder styles, set explicitly on the menu, every item and every separator; separators fill their row with `Global.Menu.Bg`; never open an empty menu) and shows it under the button. One item now: Copy Current Theme. Structure the code so adding template entries later is one extra loop.
   - Name popup: code-built like the other prompts, Chrome Palette, title "Create New Theme", label "Theme Name", one text box, Create and Cancel. Create validates with `ThemePaths.TryValidateNewName` (Check 1), then refuses an existing name (case-insensitive, `*.zip` in `Themes`) and `default`. Errors show inline and the popup stays open. Cancel and closing the popup change nothing.
   - Create runs the save under the new name through the **existing** safe Save pipeline (factor the work out of `BtnSaveTheme_Click` only as far as needed; do not reorder file operations), then the Theme Saved flash, then loads the new theme through `ThemeLoadRunner`, then refreshes the combo with the new name selected. Apply the failure behavior from the audit.
   - Log a `Create` line on success and `SaveBlocked` for refused names, if the audit confirms ThemeLog supports it with no structural change.
3. **ThemeBuilderWindow(.xaml.cs):** only what the audit finds necessary (flash, runner, restore points).
4. Remove dead code left by the retired box and combo entry. Update comments; every method keeps its one-line `// Does X` summary and section fencing.

**Do NOT touch:**

- The order of file operations in Load, Save and Delete Theme; `ThemeFileStore`, `ThemeFolderSwap` internals.
- `ThemePaths` rules (Check 1 only for typed names, never for existing names).
- Restore Defaults, `DefaultLayerSets.*`, `LayerRegistry` defaults.
- The Global resources, Chrome Palette values, other windows.
- Anything about templates: no Templates folder code, no scanning, no JSON reading in this pass.

## Expected errors

Removing `TxtNewThemeName` and the `[New Theme...]` entry will cause transient build errors in `ThemesManagerControl` until its code-behind references are removed. Order the edits so the end state builds clean, and say so in the report. The project must build with **0 errors** and no new warnings.

## How to verify (test checklist to report)

1. Build clean. Open the Theme Builder: New Theme button sits beside the other three, no wrapping; the combo lists only real themes; no inline name box.
2. New Theme > Copy Current Theme > name popup. Cancel: nothing changes, no files appear.
3. Empty name, invalid characters, leading dot, an existing name in different case, and `default`: each is refused inline and the popup stays open; the existing theme is untouched.
4. Valid name with **unsaved Advanced edits**: the new theme contains those edits; the old theme's zip is unchanged; the Theme Saved flash shows; the new theme is selected and loaded; Theme sizes row shows the saved sizes.
5. Close and reopen the launcher: the new theme is the active theme and looks right. Delete the old theme's Extracted folder contents scenario: the new theme still shows its images (it runs from its own folder).
6. Check `logs/theme.log` for the `Create` line and any `SaveBlocked` lines.
7. Smart fullscreen on, F11, then create a theme: the screen and active size stay correct.
8. Check the button row at 125%, 150% and 175% Windows scale.

## Report format

Files changed, anything that did not match these instructions, anything changed beyond them, the test checklist, and any new warning. Offer a short CLAUDE.md and themes.md update (the New Theme flow, name rules, no overwrite in this flow) but do not apply it until asked.
