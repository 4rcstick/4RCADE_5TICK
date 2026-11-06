# Handoff 8: Implement the "Show Theme In" theme setting

## Goal and why
Project: 4RCADE 5TICK, a C#/WPF MAME frontend. Add a **theme-level** setting, **Show Theme In**, that decides where a theme is offered: in the Theme Browser (which does not exist yet, so for now the value is only stored and saved), in the hamburger menu's Themes list, in both, or in neither. The Theme Builder's Load Theme combo always lists every theme and is never filtered. The setting lives in the theme's Info block (Author, Description, thumbnail), one value per theme, never per resolution.

You already audited this (theme-visibility-audit.md, Handoff 7). Line numbers below come from that audit; re-verify them before editing. Use the **Grep tool** to search and the **Read tool** to read, run shell commands exactly as they are with **no `cd` prefix**, and follow CLAUDE.md.

## Decisions already made
- **Options (value stored, label shown), in this order:** `Both` / "Both" (default), `BrowserOnly` / "Theme Browser only", `MenuOnly` / "Hamburger menu only", `Neither` / "Neither".
- **Storage:** a `string` on `ThemeInfo` with named `const string` values, like `VisibilityMode`. Not an enum (it would be written as an int). Null or any unknown value is read as **Both**.
- **Hamburger rule:** the Themes list hides a theme whose setting is `BrowserOnly` or `Neither`, **except** the active theme, which is always shown. If there is **no active theme** (`ActiveThemeName` empty), apply no filter at all, so something can always be picked.
- **Approach for reading it:** read each theme zip's `theme.json` when the hamburger menu opens, through one small new helper that falls back to Both on any error. No cached index, no new data file.
- **A new file is approved:** the helper below.
- **Save timing is accepted:** the hamburger menu reads saved zips, so a change shows there only after **Save Changes**. Save Adjustments alone is not enough, and the value is dropped the next time that theme loads, exactly like Author and Description. Document this; do not try to change it.
- **Label:** "Show Theme In:" (the user may reword it later, so keep it a single string in one place).
- **Unrelated oddities found by the audit that must NOT be fixed in this pass:** the duplicated `this.TabColorHex = source.TabColorHex;` line in `ConfigurationSettings.ApplyThemeSettings`, the "star row" wording in themes.md 3.22, and the `*.zip` globbing note.

## Exact changes wanted

### 1. `Models/ThemeInfo.cs`: the field and the constants
- Add `public string ShowIn { get; set; } = ThemeShowIn.Both;` to `ThemeInfo` with a one-line comment. `Clone()` is a `MemberwiseClone`, so it needs no change.
- Add a small static class `ThemeShowIn` in the same file (not in `LayerRegistry`; avoid any name clash with `LayerRegistry.ShadowTargetBoth`) containing:
  - `public const string Both = "Both";`, `BrowserOnly = "BrowserOnly"`, `MenuOnly = "MenuOnly"`, `Neither = "Neither"`.
  - `public static string Normalize(string? value)`: returns the value if it is one of the four constants, otherwise `Both` (null, empty, unknown, different case all give `Both`; match exactly, case-sensitive, is fine).
  - `public static bool ShowsInHamburgerMenu(string? value)`: true for `Both` and `MenuOnly` after `Normalize`.
  - `public static bool ShowsInThemeBrowser(string? value)`: true for `Both` and `BrowserOnly` after `Normalize` (nothing calls it yet; it is for the future browser).
  - Every method gets a one-line `// Does ...` summary comment per CLAUDE.md.

### 2. `ViewModels/MainViewModel.cs`: normalize on load
In LoadTheme's Info handling (audit A5, around lines 1646-1657), right after the `themeInfo.Description ??= "";` line add `themeInfo.ShowIn = ThemeShowIn.Normalize(themeInfo.ShowIn);` so a JSON `null` or unknown value becomes `Both`. Leave `ApplyThemeSettings` (settings.json path) alone: readers must use `Normalize` anyway.

### 3. New file `Services/ThemeShowInReader.cs`
A small static class (follow the namespace and `[SECTION: ...]` style of the other `Services` files) with one public method, `public static string Read(string zipPath)`:
- Opens the zip with `ZipFile.OpenRead`, finds `theme.json`, falling back to `theme.cfg` (like `LoadSavedThemeSnapshot` does), matching entry names case-insensitively (like `ExtractAndReadTheme`).
- Reads only that one entry, parses it with `JsonDocument`, and reads `Info` then `ShowIn` as a string (property names are PascalCase, case-sensitive, as the app writes them).
- Returns `ThemeShowIn.Normalize(thatValue)`.
- **Any** exception (corrupt zip, missing entry, bad JSON, file locked) is caught and returns `ThemeShowIn.Both`. It never throws and never writes anything.
- Has a `// Does ...` summary comment on the class method and a short comment explaining why it falls back to Both (the theme must still be listed).

### 4. `MainWindow.xaml.cs`: filter the hamburger Themes list
In `PopulateHamburgerSubmenus`, the Themes block (audit D1, around lines 3509-3534):
- Keep each zip's **full path** (the list currently keeps only the name from `Path.GetFileNameWithoutExtension`), so the reader can open it.
- When building the items, skip a theme if it is **not** the active theme (`OrdinalIgnoreCase` against `config.ActiveThemeName`, as the check mark does) and `ThemeShowIn.ShowsInHamburgerMenu(ThemeShowInReader.Read(path))` is false. If `config.ActiveThemeName` is null or empty, skip the filtering entirely.
- Keep the alphabetical order and the check mark exactly as they are. Set `themesMenu.IsEnabled` from the **filtered** item count.
- Add or update the comments on this block to describe the filter, the always-shown active theme and the no-active-theme rule.
- Do NOT change `HamburgerThemeItem_Click` or any other list.

### 5. Theme Info view: the combo
Files: `Views/AdvancedThemesTabControl.xaml` and `.xaml.cs`.
- **XAML** (in `[SECTION: Theme Info View]`, inside the outer StackPanel of `PnlThemeInfo`, **between `ThemeInfoDescription` and `ThemeInfoModified`**, outside `PnlThemeInfoTop`): add a StackPanel `x:Name="PnlThemeInfoShowIn"` with `SizeChanged="ThemeInfoLayout_SizeChanged"` containing
  - a label TextBlock "Show Theme In:" with `Style="{StaticResource ThemeInfoHeadingStyle}"` (the magenta label), in a wrapper that gives the same 14 px gap above and 8 px gap below the other headings (copy the Author / Description heading pattern, without the pencil glyph), left-aligned with them; and
  - a `ComboBox x:Name="CboThemeShowIn"` with `Style="{StaticResource ChromeComboBoxStyle}"`, `Width="220"`, `Height="25"`, `HorizontalAlignment="Left"` and `SelectionChanged="CboThemeShowIn_SelectionChanged"` (the Visibility combo at `.xaml:353-357` is the pattern).
  - Keep `ThemeInfoModified` below it, with its existing margin.
- **Code-behind:**
  - Add a `(string Value, string Label)[] ThemeShowInChoices` table beside `VisibilityChoices` (same pattern and style) with the four choices above, and fill the combo from it the same way `CboVisibilityMode` is filled.
  - In `RefreshThemeInfo()` (around `.xaml.cs:1287-1295`), set the combo's selection from `ThemeShowIn.Normalize(_settings.Info.ShowIn)` inside the existing `_loading` guard so setting the selection does not write back.
  - Handler `CboThemeShowIn_SelectionChanged`: return if `_loading`; otherwise set `_settings.Info.ShowIn` to the selected choice's value. Memory only, like Author and Description (Save Adjustments / Save Changes persist it, and the Theme Builder's restore point already covers a field on `ThemeInfo`).
  - **Height rule:** `ThemeInfoLayout_SizeChanged` (around `.xaml.cs:1305-1309`) stretches the description to fill leftover height from `rest = PnlThemeInfoTop.ActualHeight + ThemeInfoModified.ActualHeight + ThemeInfoModified.Margin.Top`. Add the new panel's `ActualHeight` and its margins to `rest`, otherwise the view overflows and scrolls. Keep the minimum-height behavior.
  - Every `x:Name` and handler must be referenced from code-behind, and every method gets a `// Does ...` summary comment.
- The label and combo belong in this host view. Do NOT put them inside `ThemeAuthorControl`, `ThemeDescriptionControl`, `ThemeModifiedControl` or the thumbnail control (those are content-only so the Theme Browser can reuse them), and do not create a fifth reusable control.

### 6. Docs
- `themes.md`: update 3.22 (the Data bullet: the field and its values; the View bullet: the new row and the `rest` height sum; the Saving bullet: Copy Current Theme keeps it, templates and older themes read as Both, shown in the hamburger only after Save Changes), 3.23 (the line about the template Info block, now "blanks to Both"), section 8's file map for `ThemeShowInReader.cs`, the line-3 "Last verified" status line, and a change log entry for 2026-11-05. Keep edits minimal and in the file's existing voice.
- `CLAUDE.md`: in the "Theme Info (themes.md 3.22)" gotchas section add the field, "null or unknown reads as Both", "the hamburger menu reads each theme zip through `ThemeShowInReader`, always shows the active theme, and applies no filter when no theme is active", and "the Load Theme combo and the duplicate-name check stay unfiltered". Also add the new row to the Theme Builder Status Theme Info bullet. Nothing else in `CLAUDE.md`.

## Constraints / do NOT touch
- The Theme Builder's Load Theme combo, `ThemesManagerControl.GetThemeNames()` and `FindNewThemeNameProblem` (they stay unfiltered).
- `ThemeTemplateStore` and anything under `Templates` (templates must stay read-only input; they already get a fresh `ThemeInfo`, which defaults to Both).
- `ApplyThemeSettings`, `Clone()`, the restore point code, `SaveThemeCore`, Delete Theme, the thumbnail code, `HamburgerThemeItem_Click`, `LayerRegistry`, and the Theme Browser placeholder.
- The three oddities listed under Decisions. No other "while I'm here" changes, and nothing new beyond the files named here.
- `.cs` and `.xaml` files are CRLF and `.md` files are LF. Use the Edit tool (Write for the one new file, matching the repo's line endings), and run `git diff --stat` to confirm no file shows a whole-file change. For the new `.cs` file check it uses CRLF like the others.
- **Do not commit, stash, revert or reset anything.** I will test and commit myself. Leave the untracked spec files alone. Stay inside the project folder.

## Expected errors
This edits several files that depend on each other (the model constants, the reader, the view model, the UI and the menu). **Build errors in the middle of the pass are expected** (for example the UI or menu referencing `ThemeShowIn` or the reader before they exist). Order the edits so the end state is clean (model first, then the reader, then the view model, then the UI and the menu), and build once at the end. Don't report mid-pass errors.

## How to verify
1. `dotnet build ArcadeStick.csproj` completes with **0 errors and 0 warnings**.
2. Report the `git diff --stat` and the `git diff` for every changed file (the new file in full).
3. You cannot run the UI, so say what you could not verify. I will check these by eye:
   - The Theme Info view shows "Show Theme In:" with its combo directly below the Description and above the "last saved" line; the description box still fills the leftover height and nothing scrolls at the normal size. The combo shows "Both" for an existing theme.
   - Pick "Neither", press **Save Changes**, open the hamburger menu: the active theme is still listed with its check mark. Switch to another theme: the first theme is gone from the hamburger list but still in the Theme Builder's Load Theme combo.
   - "Theme Browser only" behaves the same as "Neither" in the hamburger list; "Hamburger menu only" and "Both" keep the theme listed.
   - Copy Current Theme from a theme set to "Neither": the copy keeps "Neither".
   - Close the Theme Builder without saving: the combo's value reverts to the saved one.
4. Run shell commands with no `cd` prefix. At the end list **every shell command you ran**, in order, and say which searching and reading you did with Grep and Read. Expected shell commands: `git status`, `git diff`, `git diff --stat` and `dotnet build` only. If you needed anything else, say what and why.
