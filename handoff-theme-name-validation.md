# Handoff: Theme name validation and a path backstop

You have no memory of the chat that produced this file, so everything you need is here. Project: **4RCADE 5TICK**, a C#/WPF MAME frontend. The developer is a first-time developer working independently.

**Follow the pass structure in CLAUDE.md:** do the read-only STEP 1 audit, report the findings, then **stop and wait. STEP 2 (implementation) starts only after the developer says "go".** Do not commit.

## Goal and why

Theme names become file and folder names, but nothing validates them. The earlier audits found that `[New Theme...]` only trims the name and rejects an empty one. A name like `a\b` creates a subfolder under `Themes`, and `..\..\x` can write outside it. Reserved Windows names (`CON`, `NUL` and so on) are not blocked either. The same kind of mistake already cost the developer a theme, so this pass closes it.

## The design (decided with the developer)

**One shared helper turns a theme name into every path the app uses for it, and two kinds of check protect it.** The helper is the only place a theme name is combined with a folder.

### Paths the helper provides
Every path derived from a theme name:
- the zip: `Themes\<name>.zip`
- the working folder: `Themes\Extracted\<name>\`
- the temp zip: `Themes\<name>.zip.tmp-<guid>`
- the backup: `Themes\<name>.zip.bak`
- the deleted copy: `Themes\Deleted\<name>_<date>-<time>.zip` (keep the existing naming, including the `_N` suffix on a same-second clash)
- the staging folders in `Themes\_staging` (the existing `<name>.loading-<guid>` and `<name>.old-<guid>` patterns)

Keep each path exactly as the code builds it today. The helper only centralizes the construction and adds the checks.

### Check 1: name rules, for NEW names only (friendly message)
Applied to a name the user types (`[New Theme...]`) before anything touches the disk, before the overwrite prompt, and before staging:
- Trim. Reject empty.
- Reject any character in `Path.GetInvalidFileNameChars()` and these explicitly: `\ / : * ? " < > |`, plus control characters.
- Reject `..` anywhere in the name.
- Reject a name ending in a dot or a space (Windows silently strips them).
- Reject reserved Windows device names: `CON`, `PRN`, `AUX`, `NUL`, `COM1` to `COM9`, `LPT1` to `LPT9`. Windows also treats `CON.zip` or `CON.anything` as the device, so test the part of the name **before the first dot**, ignoring case.
- Maximum length **50** characters.
- **Must still allow** spaces, apostrophes, parentheses, hyphens and ordinary letters and digits, including non-English letters. A theme called `Cyndi's Theme` already exists and must stay valid.
- Show one plain message in the app's message box style, title "Invalid Theme Name", for example: "Theme names can't contain \ / : * ? " < > | or "..", can't end with a dot or a space, can't be a reserved Windows name like CON, and can be at most 50 characters." Keep the New Theme Name box as it was and change nothing on disk.
- Log it through `ThemeLog` as a `SaveBlocked` line with the reason (do not log the whole message).

### Check 2: resolved-path backstop, for EVERY name (including names from the dropdown)
Every path the helper returns is resolved with `Path.GetFullPath`, then checked to sit **directly inside** its expected folder (`Themes` for the zip, `.bak`, temp zip; `Themes\Extracted` for the working folder; `Themes\Deleted`; `Themes\_staging`). Compare against the folder path **with a trailing separator, ignoring case**, so a sibling like `Themes_x` cannot pass a plain "starts with" test, and require that the parent directory of the resolved path equals the expected folder (a name with a separator would resolve one level deeper). On failure throw a clear exception (`ThemeFileException` or whatever the code uses for refused theme file operations) and do nothing on disk.

**Names that already exist (from the dropdown, the hamburger list, `settings.json`) must not be locked out by Check 1's stricter rules.** Only Check 2 applies to them, so a legacy theme named, for example, `v1..2` can still be loaded and deleted. Check 1 is for creating new names only.

## STEP 1: read-only audit (report, then stop)

1. **Every place a theme name becomes a path.** Search the whole codebase (not only the Theme Builder) for each use of the zip path, `Extracted` path, `Themes` folder, `.bak`, `.tmp-`, `Deleted`, `_staging`, and any `Path.Combine` with a theme name: `ThemeFileStore`, `ThemeFolderSwap`, `ThemesManagerControl`, `MainViewModel` (`LoadTheme`, `LoadThemeCore`, `LoadSavedThemeSnapshot`, the theme list refresh, the first-run load), the hamburger theme list in `MainWindow`, `ThemeThumbnailStore`, anything else. List each with file and method.
2. **Every way a new name can reach the app.** Confirm `[New Theme...]` (the New Theme Name box) is the only entry point, and whether there is any rename, import or Save As anywhere (the docs say there is no rename or import).
3. **Where the checks should live.** Propose the helper's name, location (for example `Services/ThemePaths.cs`) and API, matching the existing style of `ThemeFileStore` and `ThemeFolderSwap`. Say which existing methods would change and whether any callers pass folders or paths instead of names (those need handling too).
4. **Existing behavior to preserve.** Confirm how the existing overwrite check compares names (case-insensitive) and where the name is trimmed today. Confirm what name `LoadTheme("default")` uses and that a lowercase `default` still works.
5. **Edge cases to confirm on the real Windows rules:** that `Path.GetFullPath` collapses `..` and removes trailing dots and spaces, how it treats device names, and that the trailing-separator comparison handles a drive root, a path with a trailing separator already, and case differences. A throwaway console snippet in a temp folder (outside the repo and outside `F:\Arcade Stick\Data\`) can confirm these. Do not use the real Themes folder.
6. **Proposed test names** you would use, and a proposed message text if you would word it differently.
7. Anything you find that is risky, even if outside this pass.

## STEP 2: implementation (only after "go")

1. Add the shared helper with Check 1 (`TryValidateNewName` or similar, returning the problem) and Check 2 (applied inside every path it returns).
2. Route every place found in STEP 1 through it. Do not change any path's shape, the order of any operation, or any behavior other than refusing bad names.
3. In `BtnSaveTheme_Click`, validate a typed `[New Theme...]` name first (Check 1), before the overwrite prompt and before any file work, with the message above and a `SaveBlocked` log line.
4. Make sure a rejected name from Check 2 shows the app's usual "not saved / not loaded / not deleted" message for that operation (it should be effectively unreachable for real names) instead of an unhandled exception.
5. Update the docs: `themes.md` (the 6b "Theme name validation" entry becomes built and moves to the change log with a new entry; a section 7 note that every theme path goes through the helper; the file map row; a decision entry only if you find a real decision missing from the log, otherwise none) and one short bullet in the "Theme file safety" section of `CLAUDE.md`.

## Constraints

- Do not change Load, Save or Delete ordering or any safety behavior from the earlier passes.
- Do not read, write, delete or run anything against `F:\Arcade Stick\Data\4RCADE5TICK_files\Themes` or any theme zip while building or testing. Use temp folders and string-only checks. The developer tests in the app with throwaway theme names.
- Do not repair or rename any existing theme.
- No new package dependencies. Keep the diff focused. No commits.
- After the edits the project must build with 0 errors (report any new warning). If a multi-file edit has transient errors while call sites move to the helper, order the edits so the end state is clean and say so.

## How to verify (give the developer this checklist after STEP 2)

Using the app with throwaway themes only:
1. **Rejected names** (each should show the "Invalid Theme Name" message, create nothing in `Themes`, and write a `SaveBlocked` line to `theme.log`): `a\b`, `a/b`, `..\..\x`, `a..b`, `CON`, `con.txt`, `NUL`, `COM1`, `name.` , `name ` (trailing space is trimmed, so also try `name .`), `a:b`, `a*b`, `a?b`, `a"b`, `a<b`, `a|b`, a 51-character name.
2. **Accepted names** (save works, the zip and working folder appear in the right places): `My Theme 2`, `Cyndi's Theme 2`, `Theme (new)`, `Café`, a 50-character name.
3. After the rejected attempts, look in `Themes` and `Themes\Extracted`: no stray folders, files or subfolders were created.
4. **Existing behavior unchanged:** the overwrite prompt for an existing name (No by default), Load, Save and Delete on an existing throwaway theme, and the dropdown list. A theme whose name contains a double dot (create one by renaming a zip in Explorer, for example `test..old.zip`) can still be loaded and deleted.
5. `theme.log` has a sensible line for each blocked attempt.

## Final report

Files changed and added; the helper's API; every call site moved; how the two checks differ; every edge case you tested and the result; anything that did not match this file; anything changed beyond it; build result; the test checklist above.
