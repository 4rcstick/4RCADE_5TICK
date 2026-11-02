# Handoff: Templates folder and New Theme from a template (Pass 2 of 2)

Read CLAUDE.md and themes.md first (themes.md 3.4, 3.22, section 7, decisions 71 to 73). Trust order: the code first, themes.md second. Report any mismatch.

**Workflow:** do the read-only STEP 1 audit, report, then **stop and wait**. STEP 2 starts only after the user says "go". Never commit.

Pass 1 is built, tested and committed: the New Theme button, its menu (one entry, Copy Current Theme), the name popup, `SaveThemeCore`, and the save-then-load flow through `ThemeLoadRunner`. Read that code first; this pass extends it.

## Goal and why

Let the user start a new theme from a **template**: a read-only JSON file in `4rcade5tick_files\Templates\`. Each `*.json` there becomes an entry in the New Theme menu under Copy Current Theme, labeled with the file name without `.json`. Adding another starting style later (dark, light, pastel) must need no code change: drop a file in the folder.

Templates are separate from themes. They never appear in the Theme Browser, the hamburger menu or the theme combo, and the launcher never writes to or deletes a template.

## Decisions already made (do not reopen)

1. **File format:** a template is a bare `theme.json`-shaped file (the same shape as the `theme.json` inside a theme zip), not a zip. The user's first one is `Templates\template.json` (29 KB, both 1280x720 and 1920x1080 layouts, all image paths empty, "Disable background image" ticked at both sizes). The audit confirms whether the existing `ThemeSettings` DTO reads it directly or a wrapper is needed; prefer no wrapper.
2. **Menu:** Copy Current Theme stays first. If at least one valid template exists, a separator follows, then the templates sorted by name (case-insensitive). If the folder is missing, empty, or has no valid file, only Copy Current Theme shows. Do **not** create the folder.
3. **Label:** the file name without `.json`.
4. **Bad files are skipped, never fatal.** A file is skipped (with a log line) when its name fails `ThemePaths` validation, it cannot be read or parsed as theme JSON, it is over a sensible size cap (propose one, about 2 MB), or it has no layouts. A skipped file never breaks the menu.
5. **Sizes:** every layout in the file is saved into the new theme. The **active size** is 1280x720 if present; otherwise the layout whose height is closest to 720 (smallest absolute difference), ties going to the smaller size.
6. **Theme Info:** a created theme starts with a **blank Info block** (no author, description, thumbnail or date from the template).
7. **Same name popup and rules as Pass 1:** Check 1 validation, existing names refused (case-insensitive), `default` refused, errors inline, popup stays open. Cancel creates nothing.
8. **Theme Saved flash after creating from a template**, same as Copy Current Theme, then load the new theme through `ThemeLoadRunner`, same failure messages as Pass 1.
9. **Read-only:** open a template with read sharing, never write, rename or delete it.
10. Restore Defaults, `DefaultLayerSets.*` and the hard-coded defaults are **not touched**.

## STEP 1: read-only audit (report, then stop)

1. **Format fit.** Can `ThemeSettings` deserialize the user's `Templates\template.json` as is (same serializer options as `LoadTheme`)? Are any fields missing, extra or unexpected? Does a theme missing keys get the DTO defaults (which equal the ConfigurationSettings initializers) and `ResolveLayout` gap fills? Note anything in that file that would not survive (the Info block, a stale date, ActiveWidth / ActiveHeight).
2. **Creation path.** The proposed approach is: read and validate the template, deserialize it, set `ActiveWidth` / `ActiveHeight` by the size rule, blank the Info block, serialize it into a **zip containing only `theme.json`**, write it through the existing temp-zip, verify and swap helpers (`ThemeFileStore`, `ThemePaths`), then load it through the normal safe `LoadTheme` path via `ThemeLoadRunner`. Confirm this needs no new file logic and keeps the order of Load and Save operations unchanged. Report what in `ThemeFileStore` can be reused as is and what (if anything) needs a small addition. If you see a better approach, say so and why.
3. **Save pipeline independence.** Creating from a template must **not** go through `SaveThemeCore` (that saves in-memory values). Confirm nothing live (layouts, Global values, restore points, `LoadedThemeName`, `ActiveThemeName`, thumbnails) changes until the load succeeds. List exactly when each of those changes.
4. **Loader behavior.** What does `LoadTheme` do with a zip that has only `theme.json` and no `assets` folder? Does it use `ActiveWidth` / `ActiveHeight` from the file to pick the active size, and does the Theme sizes row then list every layout? What happens to `Extracted\<name>` for a theme with no assets?
5. **Asset paths in a template.** The user's template has empty image paths. What would happen to a template path starting with `assets/` (it would point into a zip with no assets) or an absolute path? Report, and propose (allow with a log warning, or skip the template). Do not assume.
6. **The Main Window Background.** An empty path falls back to the built-in `default_background.png` unless `ImageDisabled` is true. Confirm the template's `ImageDisabled` survives load per size.
7. **Menu.** Where Pass 1 builds the menu and how `MakeNewThemeMenuItem` works; how to add the separator (`BuilderMenuSeparatorStyle`, set explicitly, row filled with `Global.Menu.Bg`) and the template loop. The menu is built each time it opens, so a scan on every open is the intended behavior: report the cost for a handful of small files and whether it should be cheap enough on the UI thread (a removable drive can be slow; say if the scan should be off the UI thread or guarded).
8. **Where the Templates folder lives.** How the code resolves the data folder for `Themes` and how to build the Templates path the same way. Does `ThemePaths` have to grow a helper, or should the template path live in the new service? Keep the rule that a name from a file becomes a path only through `ThemePaths`-style checks.
9. **ThemeLog.** Propose log lines: on success `Create | ... | From template <name>`; for a skipped template one line per file naming the reason, **once per file per session** so opening the menu repeatedly doesn't spam. Confirm this fits `ThemeLog`'s style.
10. **Race and edit cases.** A template that changes, vanishes or becomes invalid between the menu opening and Create: it must be re-read at Create time and fail with a clear message, writing nothing.
11. **Anything else** that could break: `RemoveUnusedFolders`, Theme Info view, custom layers in a template, a template with a size the monitor can't show (Smart fullscreen), the hamburger Themes submenu, the first-run path.

List the files you expect to change and any decisions for the user. If the code contradicts a decision above, say so instead of working around it.

## STEP 2: implementation (only after the user says "go")

**Changes wanted:**

1. **New small service** (suggest `Services/ThemeTemplateStore.cs`, follow the audit): lists valid templates (name plus path), reads and validates one, applies the active-size rule and the blank Info block, and produces the `theme.json` bytes for the new zip. Pure helpers (size choice, name validation) stay testable. Never writes to the Templates folder. Never creates it.
2. **ThemesManagerControl.xaml.cs:** in the New Theme menu code, after Copy Current Theme add the separator and one item per valid template (the loop Pass 1 left room for). Choosing one opens the **same name popup**; Create then runs the template path: re-read and validate the template, write the zip with the existing safe helpers, show the Theme Saved flash, then load through `ThemeLoadRunner` (reuse Pass 1's captured-result handling and its failure messages). Refresh the combo with the new name selected.
3. **ThemeFileStore / ThemePaths:** only a small addition if the audit shows one is needed; do not reorder existing operations.
4. **ThemeLog:** a `Create` line with `From template <name>` on success; one skipped-template line per file per session.
5. Section fencing, one-line `// Does X` summaries, no stale comments.

**Do NOT touch:**

- The order of file operations in Load, Save and Delete Theme; `ThemeFolderSwap` internals.
- `SaveThemeCore` behavior (Copy Current Theme must work exactly as it does now).
- Restore Defaults, `DefaultLayerSets.*`, `LayerRegistry` defaults, the Global resources.
- The user's template files: do not edit, move or create anything in the Templates folder.
- ThemeBuilderWindow unless unavoidable (then say why).

## Expected errors

A new file referenced from `ThemesManagerControl` may produce transient errors until both are in place; order the edits so the end state is **0 errors and 0 new warnings**, and say so in the report. If you build while the user's launcher is running, build to a scratch folder and tell the user to rebuild in Visual Studio before testing.

## How to verify (test checklist to report)

1. Build clean. With `Templates\template.json` present, New Theme shows Copy Current Theme, a separator, then "template". With the folder renamed away or empty, only Copy Current Theme shows and nothing is created.
2. Pick "template" > Cancel: nothing changes and no files appear.
3. Name rules: empty, invalid characters, leading dot, an existing name in different case, and `default` are each refused inline with the popup staying open.
4. Valid name: the Theme Saved flash shows; the new theme is selected and loaded; it shows the template's slate colors; **the Theme sizes row shows 1280 x 720 and 1920 x 1080**; the active size is 1280 x 720; Theme Info has no date, author or description; no background image shows.
5. Close and reopen the launcher: the new theme is active and looks the same. The old theme is untouched, and `Templates\template.json` is unchanged (date modified and size).
6. Edit and Save Changes on the new theme, then create another from the same template: the second is the original look, not the edited one.
7. Add a second file (for example a copy renamed `Copy2.json`): it appears in the menu without any code change; a file with a bad name (leading dot) and a file containing broken JSON are skipped, the menu still opens, and `logs/theme.log` has one skipped line each (not repeated on every menu open).
8. A template with only a 1080p layout opens at 1080p; one with 900p and 1080p opens at 900p; a tie between 640 and 800 high picks the smaller.
9. Delete the template file after the menu opens, then press Create: a clear message, nothing written.
10. `logs/theme.log` shows `Create | ... | From template template`.
11. Smart fullscreen on, F11, then create from a template: the screen and active size stay correct.
12. Copy Current Theme still works exactly as in Pass 1.

## Report format

Files changed, anything that did not match these instructions, anything changed beyond them, the test checklist, any new warning. Offer one combined docs update (themes.md and CLAUDE.md) covering both passes: the New Theme button and renamed buttons (New Theme, Load, Save Changes, Delete), the name rules and no-overwrite rule, the Templates folder and format, the active-size rule, the log lines, and a release checklist line that `Templates\<name>.json` ships in the data folder (outside the repo). Do not apply the docs update until asked.
