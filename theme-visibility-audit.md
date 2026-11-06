# Handoff 7 of 2: READ-ONLY AUDIT, "Show Theme In" setting for themes

## Goal and why
Project: 4RCADE 5TICK, a C#/WPF MAME frontend. I'm planning a new **theme-level** setting, **Show Theme In**, edited on the Theme Builder's Theme Info view (the default view while no layer is selected, with the thumbnail, Author and Description; themes.md 3.22). Before any edits, I need an audit of how the Theme Info data is wired end to end and how the hamburger menu lists themes, so the implementation handoff is exact.

**THIS IS AN AUDIT ONLY. Do NOT edit, create, rename or delete any file, and do not commit, stash, revert or reset anything.** Building once to confirm a clean baseline is fine.

## The planned feature (for context, do not implement)
- A combo box labeled **Show Theme In:** placed **directly below the Description** on the Theme Info view, with breathing room, left-aligned with the existing magenta labels (Author, Description) and the combo styled like the app's other combos.
- Four options, exactly these labels, in this order:
  1. **Both** (default): shows in the Theme Browser and in the hamburger menu's Themes list.
  2. **Theme Browser only**
  3. **Hamburger menu only**
  4. **Neither**
- It is **theme-level metadata**, like name, author, description and thumbnail: one value per theme, never per resolution. It travels with Copy Current Theme (the copy keeps the setting). A theme with no stored value (every existing theme, and templates) behaves as **Both**.
- The Theme Builder's Load Theme combo always lists **every** installed theme and is never filtered.
- The hamburger menu's Themes list will hide themes whose setting is "Theme Browser only" or "Neither", **except the active theme, which is always shown there** (with its check mark), so the menu never looks like nothing is selected.
- The Theme Browser itself does not exist yet (the THEME BROWSER tab is an empty placeholder). For now the value is stored and saved, and the hamburger menu is the only place it changes anything visible. The browser will read it later.

## What to audit and report back
For each item give **file path + line numbers** and the **verbatim current code** for the relevant section. Use the **Grep tool** to search and the **Read tool** (offset and limit) to read; do not use shell pipelines to look inside files.

**A. The ThemeInfo data path (the model for where the new field goes).**
Follow Author and Description through every stage and list each place a new field would need a line. At minimum: the `ThemeInfo` class and where it lives on `ThemeSettings` (the DTO / theme.json) and on `ConfigurationSettings.Info`; how `theme.json` is written and read (serializer options, null handling, `JsonIgnore` conventions); `LoadTheme` (including "a theme without a ThemeInfo block gets a blank block"); the line in `ApplyThemeSettings`; `SaveThemeCore` and its DTO builder; the Theme Builder's restore point copy and its snapshot / restore code; Copy Current Theme (`CopyCurrentTheme`) and how it carries Info; and anything else that copies or resets Info (New Theme, templates, Restore Defaults, theme delete).
- Which of these happen on Save Adjustments (in memory only, kept for next launch) versus Save Changes (writes the zip)? Describe how an edited Author or Description behaves, because the hamburger menu will read from the saved zip.
- How would an older `theme.json` with no value for the new field load? What default results?
- How are other enum-like or choice values stored in theme.json today (string constants, enum as string, enum as int)? Recommend the representation that best matches the existing convention and survives adding a fifth option later.

**B. Templates.**
`ThemeTemplateStore` and `Templates\*.json`: do templates carry a ThemeInfo block? Does validation reject unknown or missing fields? Confirm a template with no value for the new field creates a theme that behaves as "Both", and that nothing writes to the template files.

**C. The Theme Info view (UI).**
- Where Author and Description are laid out (XAML), their label style (the magenta labels), their edit pattern (pencil glyph or similar), and how the free space below Description looks. Note the project rule that the four Theme Info display controls hold content only, with no headings or glyphs, so the Theme Browser can reuse them. Say where a new editor control and its label should live so that rule is respected.
- An existing combo box style to reuse, and whether any combo already sits on this view.
- How the view loads values and raises changes: `x:Name`s, handlers, the Load...Fields-style method, and any "dirty" tracking that a new control would need to hook into.

**D. The hamburger menu's Themes list.**
`PopulateHamburgerSubmenus` in `MainWindow.xaml.cs` lists every `*.zip` in the Themes folder and puts a check mark on the active theme. Report:
- Exactly how it builds the list, and everything else that lists themes (the Theme Builder's Load Theme combo in `ThemesManagerControl`, anything else), so I know which lists must stay unfiltered.
- What it would take to filter by the new setting: the menu would have to read each zip's `theme.json` (or just its Info block) every time it opens. Is there an existing helper that opens a theme zip and reads `theme.json`? How expensive is it for a typical theme zip (size of `theme.json`, whether the whole zip is read or just one entry)? What happens for a corrupt zip or one without a `theme.json` (the menu must still list it, treating it as "Both")?
- Compare two approaches and recommend one: (1) read each zip's `theme.json` on menu open; (2) a small cached index of each theme's setting, kept up to date on Save, Delete and Copy. Include the failure modes of each (stale data, extra files in the data folder, rules about only reading `*.zip` directly in `Themes`).
- The active theme: how the menu finds it (`ActiveThemeName`), and confirm the "always show the active theme" rule is easy to apply.

**E. Edge cases to check in the code.**
- The active theme set to "Neither" or "Theme Browser only": does anything else assume the active theme appears in a list?
- Theme delete and rename paths: any place that would need to keep an index in sync, if approach (2) were chosen.
- `theme.log` entries or other places that print theme Info.

**F. General.**
- Every file you expect the real change to touch.
- Anything surprising, fragile or duplicated that could trip up the edits.
- The places in `themes.md` and `CLAUDE.md` that describe Theme Info (3.22 and the "Theme Info" gotchas section) that will need updating.
- Whether the project builds cleanly right now (`dotnet build ArcadeStick.csproj`).

## Constraints / do NOT touch
- No edits of any kind, no new files, no "while I'm here" fixes. Don't implement anything from the planned-feature section.
- Stay inside the project folder, and do not commit.
- Run shell commands as they are, with no `cd` prefix, per CLAUDE.md. If a command fails because it ran in the wrong folder, stop and tell me.

## How to verify
You're done when you've returned one written report covering A to F, with file paths, line numbers and verbatim code, and `git status` shows no changes from this audit (the untracked spec files may still be listed). End the report by confirming that no files were modified, and by listing **every shell command you ran**, in order. Expected: `git status` and `dotnet build` only. Also say which searching and reading you did with the Grep and Read tools.
