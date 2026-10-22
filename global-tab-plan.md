# Global Tab: Chrome Palette, Live Resources, Migration (revised after the Pass 0 audit)

Project: 4RCADE 5TICK (C#/WPF), Theme Builder, beta.4.
Date: 2026-10-21.
Companion docs: `global-tab-audit.md` (the Pass 0 audit; read it first, it holds file and line references), `CLAUDE.md` (environment and editing rules), `themes.md` (living spec), `game-info-migration.md` (the earlier migration; same pass-based way of working).
**Trust order: code first, then this plan, then themes.md.** Where this plan and the audit differ, this plan wins (it includes the user's decisions). If the code disagrees with either, report the mismatch, do not guess.

This is a multi-pass change. **Do one pass at a time and stop after each** so the user can build, test and commit. Do not start the next pass until told to. Do not commit anything.

Status: **Pass 0 (audit) is done.** Next is Pass 1.

---

## 0. How to work

- Follow `CLAUDE.md`: edit directly, CRLF source files, never commit, end every pass with **0 errors** (the ~50 known warnings are fine), `git diff --stat` after edits to catch whole-file changes.
- **Python 3.12 is installed** (`python`, not `py`). Use it for the scripted rewrites in Passes 3 and 4, opening files with `newline=''` and `encoding='utf-8'` so line endings pass through. Prefer the Edit tool for normal changes. No `sed -i`.
- **Scripted rewrites (Passes 3 and 4):** write the script so it first does a dry run that prints, per file, how many bindings it would change and every binding form it did **not** recognize. Report those counts, fix the script for unrecognized forms or list them for manual edit, then apply. Review the resulting diff file by file. Never force-rewrite something the script does not match.
- XAML: every `x:Name` and handler is referenced from code-behind; keep them when replacing blocks. Order edits so the end state builds clean (readers before the properties they read).
- Keep comment fencing (`// [SECTION: ...]`, `<!-- [SECTION: ...] -->`) and the one-line summary comment on every method.
- Update `themes.md` at the end of each pass, fixing the stale statements listed in audit section 10 in the pass that changes the code concerned (section numbers and decision numbering continue from what is there).
- Report after every pass: files changed (one line each), anything that did not match this plan and how you handled it, anything changed beyond it or intentionally left, and the test checklist.
- No legacy migration: removed theme.json keys are simply ignored on load. **Flat property names and theme.json keys that stay keep their names**, so old themes still load.

---

## 1. Decisions (final)

1. **A new top-level "GLOBAL" tab** in the Theme Builder window (after Basic and Advanced) with six subtabs: **Fonts, Backgrounds, Tabs, Buttons, Scrollbars, Context Menus.** Global values are **theme-wide, not per resolution**: flat properties on `ConfigurationSettings`, saved in theme.json through the existing DTO.
2. **Subtab contents** (existing property names in brackets):
   - **Fonts:** Headers `[HeaderFontSize / HeaderColorHex]`, Sub-Headers `[SubHeader...]`, Standard Text `[Standard...]`, Sub-Text `[SubText...]`: size and color only.
   - **Backgrounds:** Background Color `[OptionsMenuBgColorHex]`, Content Area Background Color `[OptionsBg]`, Separator Color `[SeparatorColorHex]`, Border Size / Color / Corner Radius `[OptionsMenuBorderSize / ColorHex / Radius]`.
   - **Tabs:** Font Size `[TabFontSize]` / Color `[TabColorHex]`, Background `[TabBgColorHex]`, Font Color (Hover) `[TabColorHoverHex]`, BG Color (Hover) `[TabBgColorHoverHex]`, Font Color (Active) `[TabActiveColorHex]`, BG Color (Active) `[TabActiveBgColorHex]`.
   - **Buttons:** Font Size `[OptionsBtnFontSize]` / Color `[BtnTextColorNormalHex]`, Background `[BtnBgColorHex]`, Font Color (Hover) `[OptionsBtnColorHoverHex]`, Background (Hover) `[BtnBgColorHoverHex]`, Border Size `[OptionsBtnBorderSize]` / Color `[BtnBorderColorHex]` / Radius `[OptionsBtnBorderRadius]`.
   - **Scrollbars:** Main `[ScrollThumbColor]`, Hover `[ScrollThumbHoverColor]`, Drag `[ScrollThumbDragColor]`, Track Background `[ScrollTrackColor]`, Track Hover `[ScrollTrackHoverColor]`, and a **new Scrollbar Width** (default **10**, the width in both current templates).
   - **Context Menus:** Font `[ContextMenuFontColorHex]`, Icon `[ContextMenuIconColorHex]`, Background `[ContextMenuBgColorHex]`, Font & Icon Hover `[ContextMenuHoverColorHex]`, Font & Icon Hover BG `[ContextMenuHoverBgColorHex]`, and a **new Border Color** `ContextMenuBorderColorHex` (default `#FF3A3A3C`; also colors the menu separators).
3. **Fixed chrome palette** for combo boxes (closed box, dropdown, items), list boxes and items, text boxes, checkboxes, sliders, and the Manage Playlists window, the layer-name prompts, the RomCopyPath window and the color picker popups. **No RadioButtons exist; none to style.**
   - **All fixed colors live in ONE `[SECTION: Chrome Palette]` block of named brush resources at the top of `App.xaml`**, and every palette style references those keys with **no hex literals**, so the user can retune everything by editing that block. Keyed styles only: **no keyless (implicit) styles in `App.xaml`** that would restyle main-window controls.
   - Starting values (the Folder Order look with the app's cyan in place of Windows blue; the user may change them later):

| Key | Value | Used for |
|---|---|---|
| ChromeWindowBg | `#FF1C1C1E` | Manage Playlists, prompts, RomCopyPath, color picker popups |
| ChromeBg | `#FF000000` | field background (text box, list box, closed combo, dropdown, checkbox box) |
| ChromeBgHover | `#FF1C1C1E` | closed combo and text box under the mouse |
| ChromeBorder | `#FF3A3A3C` | field, dropdown and checkbox borders; replaces `BorderColorFramework` |
| ChromeBorderHover | `#FF636366` | field border under the mouse |
| ChromeAccent | `#FF00D4FF` | focus border, check mark, combo arrow, slider thumb, text selection |
| ChromeText | `#FFF2F2F7` | text in fields, lists and combos; caret |
| ChromeTextMuted | `#FFA0A0A2` | placeholder text |
| ChromeItemHoverBg | `#1F00D4FF` | list and dropdown item hover |
| ChromeItemHoverBorder | `#A800D4FF` | list item hover border |
| ChromeSelectedBg | `#3D00D4FF` | selected item |
| ChromeSelectedBorder | `#FF00D4FF` | selected item border |
| ChromeSelectedText | `#FFF2F2F7` | selected item text |
| ChromeSelectedInactiveBg | `#3D00D4FF` | selected item when the list is not focused (**equal to the active value: the selection stays cyan**, unlike the stock grey-out; the user can change these two keys back to `#3DDADADA` / `#FFDADADA`) |
| ChromeSelectedInactiveBorder | `#FF00D4FF` | same |
| ChromeSliderTrack | `#FF3A3A3C` | slider track |

   - Disabled controls: opacity 0.5 (no extra key).
   - **Font:** family stays themeable; palette controls use the Standard font size from the Fonts values. The Folder Order and Previews Order lists keep their local `FontWeight="Bold"`. **Checkbox labels keep the themed Standard color and size**; only the box, border and check mark use the palette.
   - Controls whose templates ignore simple setters (audit 3.3: CheckBox, Slider, ComboBox, ListBoxItem, TextBox focus/hover, unstyled Button) need keyed templates. The default-template combos in `InputsTabControl` and `MAMEiniTabControl` and the combos in Sorting and Manage Playlists move onto the palette combo style.
   - The palette combo, list and dropdown ScrollViewers use the themed scrollbar (`ArcadeDarkScrollViewer`), so the new scrollbar width reaches them.
4. **Sorting follows the Options window's chrome:** window background from `OptionsMenuBgColorHex`, content area from `OptionsBg`, themed tabs via the shared tab style (Pass 4); only its inner controls use the palette. **Manage Playlists, the name prompts, RomCopyPath and the picker popups use `ChromeWindowBg`.** The Add Layer and Rename prompts built in code get the palette styles applied in code. Standard Windows message boxes stay system.
5. **Removed:** the Menu Selected pair (flat `FolderSelectedColorHex` / `FolderSelectedBgColorHex` and the `ThemeFolderSelected*` forwarders) and the Games List Panel section on the Basic tab; `BorderColorFramework` / `ThemeBorderColor` (becomes `ChromeBorder`; the context-menu pieces wait for Pass 5 and use the new context menu border color); `InputFontSize` / `InputColorHex` (readers: the folder-name popup, the folder-color popup hex box, and `ManagePlayListsWindow.xaml.cs`: all move to `ChromeText` and a fixed size); `MainBackgroundBrush` once nothing reads it; the duplicate `TabHoverColorHex` (the live one is `TabColorHoverHex`; point `SortingWindow` at the shared tab style); and in the cleanup pass the retired `BorderWidthValue` / `BorderCurveValue` and the ~25 dead `Theme*` forwarders (audit 1.1) and `RadiusConverter` when unused. **Keep `ThemeHeaderColor` and `ThemeMainColor`** (the no-art banner and spin card read them until the Marquee banner pass). Grep before each removal; if something outside what the audit lists reads it, stop and report.
6. **One live mechanism.** Every Global color and size becomes an **app-level resource**, rebuilt by one apply function, read with `{DynamicResource ...}` (this also fixes the silent binding failures the audit found: the 63 `ThemeBorderColor` bindings on the Basic tab, the Advanced tab's scrollbars, probably the main window's context menus). Rules:
   - Resources are **typed to match their target property** (DynamicResource has no converter): frozen `Brush`, `double` font sizes, `Thickness` border sizes, `CornerRadius` (publish tab Top and Bottom variants, replacing `RadiusConverter`).
   - **`App.xaml` also declares a default value for every Global resource key** (like `MainBackgroundBrush` today), so DynamicResource never fails before the apply function runs and the designer works.
   - Replace resource instances when a value changes; never mutate frozen ones.
   - Key naming: `Global.<Area>.<Name>` (e.g. `Global.Font.Standard.Color`, `Global.Options.RadiusTop`, `Global.Scroll.Width`).
   - The apply function is subscribed to `MainViewModel.ThemeBindingsRefreshed` (fires after theme load, Save Adjustments, Save Changes, Options save) **plus one direct call in the MainWindow constructor** (the event fires inside the view model constructor, before MainWindow subscribes; same pattern as the direct `ApplyLayout()` call). The Global tab's edits, Restore Defaults and the close-revert call it directly.
7. **Revert on close without saving.** Snapshot the flat Global values (a shallow copy, reusing the `ThemeSettings` shape) at the same four places the layout snapshot moves in `ThemeBuilderWindow` (constructor, the saved callback, Save Adjustments, `RefreshAfterThemeLoad`); restore it in `Closed` before `ApplyLayoutNow()` and re-run the apply function. The external size switch needs nothing (Global values are not per resolution). Extend the existing flow; do not build a second mechanism.
8. **Editing behavior:** Global fields apply live on Enter / focus loss (colors on change), invalid input reverts, same as the Advanced tab. They write straight into `_settings`, then call the apply function. **Transition trap:** Save Adjustments and Save Changes run `SyncUiEntriesToSettingsLayer`, which overwrites `_settings` from the Basic tab text boxes. **A migrated property must leave `SyncUiEntriesToSettingsLayer`, `LoadCurrentThemeValuesIntoUi`, its `_suppress...Sync` bool and its handlers in the same pass** it moves, or stale Basic tab text overwrites the live value on save. The DTO builder in `BtnSaveTheme_Click` keeps its lines (it reads `_settings`).
9. **Defaults come from one source: the `ConfigurationSettings` initializers.** Make the `ThemeSettings` DTO defaults equal them (audit 1.4): Header color `#FFEF44C3`, Standard size 13, Button hover background `#FF422a3c`; align `OptionsBg` (DTO `""` to `#FF000000`) in Pass 2b once Manage Playlists no longer uses the blank value. Each Global subtab gets a **Restore Defaults** button that restores that subtab's properties to those defaults and applies live (not tied to the close snapshot). New properties (width, context menu border) get the same default in the initializer and the DTO so old themes load with the current look.
10. **Out of scope:** the no-art banner and spin card (own tab on the Marquee layer later), Random Game Display fonts and colors, the main window's own controls (search box, TreeView), and the other dead settings in audit 1.3 beyond those named above.

---

## 2. Passes

### PASS 1: Foundation (no visible change)

- The global resource mechanism: one class (e.g. `GlobalThemeResources`) with the apply function, the typed resources and the default declarations in `App.xaml`, the key naming convention, the `ThemeBindingsRefreshed` subscription and the MainWindow startup call.
- The DTO default alignment (decision 9, except `OptionsBg`).
- The revert-on-close snapshot (decision 7) wired into the Theme Builder flow, and the shared Restore Defaults helper.
- **No consumer is migrated yet**, so nothing looks different. Confirm startup, loading the default theme, the hamburger theme switch, Save Adjustments and Save Changes all still work.

### PASS 2a: Chrome palette and the Theme Builder's own controls

- The `[SECTION: Chrome Palette]` block and the keyed styles (TextBox, ComboBox + items + dropdown, ListBox + items, CheckBox, Slider), including the keyed templates for the controls that ignore simple setters.
- Move the shared styles from `AdvancedThemesTabControl.xaml` (`PickerPopupGridStyle`, `ThemedComboBoxStyle`, `GLLabelStyle`, `GLTextBoxStyle`, `GLCheckBoxStyle`, `GlyphToggleStyle`) to `App.xaml`, rebuilt on the palette, so all 73 color pickers share one picker popup style (replace the 31 inline copies on the Basic tab).
- Apply to the Theme Builder: both tabs' combos, `LstLayers`, text boxes, checkboxes, the code-built boxes and checkboxes in `AdvancedThemesTabControl.xaml.cs`, and the name prompts built in code.
- Tests: the Theme Builder is fully readable; changing one hex in the palette block changes the controls everywhere; the layer list selection looks right; nothing else in the app changed.

### PASS 2b: Every other window, then the removals

- Options window tabs (MAME.ini and Inputs combos and checkboxes, the Folder Order and Previews Order lists, text boxes), Sorting (inner controls; window background from `OptionsMenuBgColorHex` for now), Manage Playlists, RomCopyPath, and the main window's folder-name and folder-color popups (they lose `Input*` and `ThemeBorderColor`).
- Then the removals in decision 5 that belong to this pass (Menu Selected pair and its Basic tab section, `BorderColorFramework` except the context-menu pieces, `Input*`, `MainBackgroundBrush` and `UpdateMainBackgroundBrush` once unused), including every ConfigurationSettings, `ApplyThemeSettings`, ThemeSettings, `LoadTheme`, forwarder, `RefreshThemeBindings` and Basic tab line. Align the `OptionsBg` default (decision 9). Update `themes.md` 3.11.
- Tests: every window and dialog readable; the combos and lists match the palette; the Games List selection still works; the main window popups work; an old theme loads.

### PASS 3: Global shell, Fonts, Backgrounds

- New `GlobalThemesTabControl` (XAML + code-behind) as the third top-level tab, with a subtab TabControl using the shared tab styles and the Advanced tab's grid and shared styles. Build the Fonts and Backgrounds subtabs, each with Restore Defaults.
- Migrate those controls off the Basic tab (decision 8 trap). Convert their consumers (about 440 bindings across 13 files: label sizes and colors, separators, Options window and content area backgrounds, borders, `RadiusConverter` uses) to `{DynamicResource}` with the scripted dry-run process from section 0. The Global tab reloads in `RefreshAfterExternalThemeSwitch`.
- Tests: editing a Fonts or Backgrounds value changes the Theme Builder live and, after opening them, the Options and other windows; Close without saving reverts; Save Changes then reload keeps it; Restore Defaults works; an old theme loads.

### PASS 4: Tabs and Buttons

- The Tabs and Buttons subtabs (including Tab BG Color Hover).
- Convert `ThemedTabItemStyle`, `ThemedTabControlStyle` and `ThemedActionButtonStyle` to dynamic resources. **Move the Options window and the Sorting window onto the shared tab styles** (removing the two inline copies and the `TabHoverColorHex` reader; the Sorting tab hover and radius change slightly, which is accepted).
- **Strip the per-button bindings** (about 445 bindings on roughly 100 buttons: Background, Foreground, BorderBrush, BorderThickness) into `ThemedActionButtonStyle` setters, using the scripted dry-run process; fold Manage Playlists' own button styles into the shared style; convert the code-built saved-size buttons.
- Decide and report what replaces `BtnTextColorNormalHex` as `ThemedTabControlStyle`'s Foreground (recommended: the Standard Fonts color).
- Remove the Basic tab controls and lines.

### PASS 5: Scrollbars and Context Menus

- **Scrollbars subtab** with the five colors and the new width (add it to `ConfigurationSettings`, `ApplyThemeSettings`, the DTO, `LoadTheme` and the apply function; default 10). Both scrollbar templates in `App.xaml` (the only place: MainWindow has no local copy, fix the stale comments) and both `ScrollViewer` styles read the dynamic resources, so the Games List, the Advanced tab (which never followed these settings before) and the palette lists and dropdowns all follow.
- **Context Menus subtab** with the five colors and the new border color (same wiring as the width). The main window's implicit `MenuItem`, `ContextMenu` and `Separator` styles, the hamburger menu and the code-built menu items read the dynamic resources; the menu border and the separators use the new border color. Note for the report: if the menu hover was pink before (the failed-binding fallback), it will now follow the setting.
- Remove the Basic tab controls and lines for both.
- Tests: scrollbar colors and width change live on the Games List, a flyout list and the Advanced tab; context menus (right-click a folder and a game, the hamburger menu) follow every setting; Restore Defaults; old theme loads.

### PASS 6: Cleanup and docs

- Remove what is left of the Options Menu section and any empty Basic tab sections so the Basic tab is Themes Manager only. Remove the dead forwarders (audit 1.1), `BorderWidthValue` / `BorderCurveValue`, `RadiusConverter` and anything else nothing reads. Final grep for every removed name.
- Update `themes.md` (sections 3.5, 3.7, 3.11, 4, 6, 7, 8, 9, 10, fixing the audit 10 statements) and the Theme Builder status in `CLAUDE.md`.

---

## 3. Notes for every pass

- A shared mistake to avoid: wiring a new flat property into only some of ConfigurationSettings, `ApplyThemeSettings`, the ThemeSettings DTO (default equal to the code default), the `LoadTheme` mapping, the DTO builder in `BtnSaveTheme_Click` and the apply function.
- `DynamicResource` inside templates and popups resolves through the logical tree up to `App.xaml`, so it works in dropdowns and context menus.
- If any step is much bigger or riskier than described, stop and report before continuing.
