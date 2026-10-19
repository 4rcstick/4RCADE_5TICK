# Search & Buttons layer: migration to the Advanced tab (handoff for Claude Code)

Project: 4RCADE 5TICK (C#/WPF), beta.4, Theme Builder rework. Read `themes.md` first (trust order: **code wins over themes.md**). The Games List layer is the template for everything below: per-resolution values stored on layer entries, live apply on Enter/focus loss, style key in `ApplyLayout`, tabs from the table in `AdvancedThemesTabControl`.

This file has two independent tasks. **Task A** is the Search & Buttons work. **Task B** is a small randomizer leftover cleanup. Do them as separate commits. Do not touch comment-stub cleanup (planned for later).

---

## Task A: Search & Buttons

### Decisions (locked with the user)

- No legacy migration. New entry fields start from registry defaults; old flat values in saved themes are ignored. All current theme.json values are test data.
- The four buttons (Random, Sort, Options, Hamburger) share ONE style. Shared button values live on the **RandomButton** entry. Label values on **SearchLabel**, box values on **SearchBox**. All per resolution.
- Reuse the existing entry fields `BgColorHex`, `BorderSize`, `BorderColorHex`, `CornerRadius` (SearchLabel: bg only; SearchBox: bg only; RandomButton: bg + border + radius). Add new generic text fields (names are suggestions, keep them generic so Game Info can reuse them): `TextFontSize` (int?), `TextColorHex`, `TextFontFamily`, `TextBold` (bool?), `TextAlign` ("Left"/"Center"/"Right"), `HoverColorHex`, `HoverBgColorHex`, `HoverBorderColorHex`, `TextShadowEnabled/ColorHex/Blur/Depth`. All nullable, `WhenWritingNull`, same pattern as the Games List fields.
- The search box keeps Segoe UI and keeps borrowing the button border (size + color from the RandomButton entry, resting value only, never hover). No separate box border.
- Buttons: NO bold toggle, NO font family, NO shadow, NO pressed state (icons are Segoe MDL2 glyphs plus one color emoji).
- Label: Bold checkbox (unchecked = DemiBold, which is today's look; checked = Bold). Alignment Left/Center/Right. Padding changes from `0,6,4,6` to `4,6,4,6`. Drop shadow reuses `BuildShadowEffect` (same rules as decision 12 in themes.md: black default, blur 0-30, depth 0-20, alpha = strength).
- Search box text: size + color + bg only.
- New property: hover border color (default = same as the resting border color, so nothing changes visually).
- Group-move checkbox: session-only (like the ratio lock), shared behavior for groups, offered for **Search & Buttons and Game Info only** (not Random Game Display).

### Registry defaults (from today's ConfigurationSettings defaults)

| Entry | Field | Default |
|---|---|---|
| SearchLabel | size / color / bg | 13 / `#FF00D4FF` / `#001C1C1E` |
| SearchLabel | family / bold / align | Segoe UI / false / Right |
| SearchLabel | shadow | off, `#FF000000`, blur 3, depth 1 |
| SearchBox | size / color / bg | 13 / `#FFF2F2F7` / `#FF1C1C1E` |
| RandomButton | size / color / bg | 13 / `#FF00D4FF` / `#FF1C1C1E` |
| RandomButton | hover color / hover bg | `#FF00D4FF` / `#FF2C2C2E` |
| RandomButton | border size / color / radius | 1 / `#FF00D4FF` / 0 |
| RandomButton | hover border color | `#FF00D4FF` |

Add constants in `LayerRegistry`, set them in `BuildDefaultLayout`, and fill gaps in `ResolveLayout` (same `??=` / `IsNullOrWhiteSpace` pattern as Games List). Existing saved entries for these layers must keep any saved value; only fill missing ones. Note `BgColorHex`/border fields are currently shape-layer fields: do not change shape layer logic, only add these entries to the fill-in.

### Advanced tab UI (AdvancedThemesTabControl.xaml / .cs)

The Search & Buttons group currently shows Position only (the group page is built in code by `BuildGroupParts`). `GetLayerTabs`/`AllLayerTabs` key on the unit key, so key the new tabs on `LayerRegistry.SearchAndButtonsGroup`.

1. **Position tab:** add a checkbox at the top (`ChkGroupMove`, "Move all parts together", shown only for Search & Buttons and Game Info groups, session-only field). In `ApplyPartField`, when checked and the edited box is X or Y, apply the same delta to every other part of the group. Width/Height never propagate. Guides keep following the edited part. Do not hard-wire to Search & Buttons: key it off "the selected unit is a group that allows group move" so Game Info works already.
2. **SEARCH tab (new):** Label section: font combo + Bold; size + color; background color; text alignment combo (Left/Center/Right); drop shadow toggle with color/blur/depth (enabled only while the toggle is on). Then a "Search box" heading: size + color; background color. Copy the grid/column layout from the Games List Folders tab (110/8/52/6/52) and reuse the shared styles (`GLLabelStyle`, `GLTextBoxStyle`, `GLCheckBoxStyle`, `ThemedComboBoxStyle`, `PickerPopupGridStyle`).
3. **BUTTONS tab (new):** size + color; background color; hover color; hover background color. Edits the RandomButton entry.
4. **BORDER tab:** reuse the existing shared Border tab (`TabShapeBorder`) for the group, mapped to the RandomButton entry. Add one extra row for "Hover border color" (picker + hex), collapsed unless the Search & Buttons group is selected. Note `GetSelectedBorderEntry`/`HasBorderTab` take the list unit key; for the group they must resolve to the RandomButton entry. `LoadBorderOnlyFields` runs before the group branch returns in `LoadSelectedLayerIntoFields`, so make sure the group case is covered there.
5. Tab headers: SEARCH, BUTTONS, BORDER. Add new TabItems (Visibility="Collapsed") and list them in `AllLayerTabs`.
6. Value rules: font size 6-72 int; all edits apply on Enter/focus loss with invalid input reverting; color pickers use `_suppressGLColorSync` like the other style tabs.
7. **Reset Layer (group branch)** currently copies rect + visibility only. It must also restore the new style fields for SearchLabel, SearchBox and RandomButton (from `GetRestoreLayout`).

### Entry copy spots (the known trap: new fields silently vanish if missed)

Add every new field (and make sure the reused bg/border fields are carried for these entries) in ALL of:
- `ConfigurationSettings.CloneLayouts`
- the preset path of `CboResolution_SelectionChanged` in `AdvancedThemesTabControl.xaml.cs` (builds entries by hand)
- `BtnResetLayer_Click` (single-layer path and the group branch above)
- `LayerRegistry.BuildDefaultLayout` / `ResolveLayout`

Optional (recommended if it stays low risk): replace the hand-written copy lists with one shared helper so this stops being a trap. `SwitchToResolution` already uses `CloneLayouts`.

### Main window binding and live apply (MainViewModel.cs / MainWindow.xaml / .xaml.cs)

- Keep the XAML binding names. Repoint the existing forwarders to the entries, the way `ThemeFolderFontSize` reads `GetGamesListEntry()`:
  `ThemeSearchLabelFontSize/Color/BgColor`, `ThemeSearchBoxFontSize/Color/BgColor`, `ThemeMainWinBtnFontSize/Color/BgColor/ColorHover/BgColorHover/BorderSize/BorderColor/CornerRadius`. Add forwarders for label font family, label font weight, label text alignment, label shadow effect, and button hover border color.
- `GetGamesListEntry` and `GetMarqueeEntry` are near duplicates. Generalize to one private `GetLayerEntry(id)` rather than adding a third and fourth copy.
- `MainWindow.xaml`: SearchLabel TextBlock gets `FontFamily`, `FontWeight`, `TextAlignment`, `Effect` bindings and padding `4,6,4,6`.
- **Live repaint:** add `_lastSearchButtonsStyleKey` in `ApplyLayout` built from every field above across the three entries. When it changes, raise the forwarders (add a small `RefreshSearchButtonBindings()` on the view model; a bare `RefreshThemeBindings` is too heavy) and refresh the button resting colors. Per themes.md section 3.3, a style field not in a key will not repaint live.
- `RefreshThemeBindings` must still raise the repointed forwarders (it raises `ThemeSearchBoxColor` and `ThemeSearchBoxBgColor` twice today; clean the duplicates).
- Edits in the Theme Builder go through `ApplyLayout` directly (not `ThemeBindingsRefreshed`), so the resting/hover refresh must be triggered from the style key branch too.

### Hover refactor

Today there are four duplicated pairs (`PlayRandomIcon_*`, `SortGearIcon_*`, `OptionsGearIcon_*`, `HamburgerIcon_*`) plus four `Refresh*IconRestingColors`, four `ThemeBindingsRefreshed` subscriptions and four direct startup calls. Merge into one shared MouseEnter/MouseLeave pair (sender is the Border, its Child is the icon TextBlock) and one `RefreshMainButtonsRestingColors()`. Keep `DelayedTooltip_MouseEnter/Leave` calls and the `HamburgerContextMenu_Closed` resting refresh. Update the MouseEnter/MouseLeave attributes in `MainWindow.xaml`.

Hover border color: set `BorderBrush` in both hover and resting code (like Background/Foreground already are). Beware: a locally set `BorderBrush` replaces the XAML binding, so the resting refresh must set it explicitly. The search box border binds the RandomButton *resting* border only.

### Cleanup per decision 9 (remove what migrated, in the same pass)

Remove these 14 flat properties everywhere:
`SearchBoxFontSize, SearchBoxColorHex, SearchBoxBgColorHex, SearchLabelFontSize, SearchLabelColorHex, SearchLabelBgColorHex, MainWinBtnFontSize, MainWinBtnColorHex, MainWinBtnBgColorHex, MainWinBtnColorHoverHex, MainWinBtnBgColorHoverHex, MainWinBtnBorderSize, MainWinBtnBorderColorHex, MainWinBtnCornerRadius`

- `ConfigurationSettings` (properties + `ApplyThemeSettings` lines), `ThemeSettings`, `MainViewModel.LoadTheme` mappings.
- `ThemesTabControl.xaml`: the Search Label, Search Box and Main Window Buttons blocks inside the MAIN WINDOW section. **Keep** the Main Window background image/color/disable-checkbox part of that section.
- `ThemesTabControl.xaml.cs`: the matching `_suppress...Sync` flags, picker/hex handlers, `LoadCurrentThemeValuesIntoUi` blocks, `BtnSaveTheme_Click` DTO lines, and `SyncUiEntriesToSettingsLayer` lines.
- **Do not touch** the Options Menu / global button system (`BtnBgColorHex`, `BtnBorderColorHex`, `BtnTextColorNormalHex`, `BtnBgColorHoverHex`, `OptionsBtn*`): different system, easy to confuse with `MainWinBtn*`.
- Per the user, only MainWindow reads these properties, but run a Find in Files for each removed name before deleting.
- Order for a clean build: add new fields/forwarders first, repoint bindings, then remove Basic tab controls and handlers, then the flat properties. Old keys in saved settings.json/theme files are simply ignored.

### Also fix (small)

`ConfigurationSettings.OptionsBg` default is `"#FF00000"` (5 digits, invalid). Change to `"#FF000000"`. Out of scope otherwise (Global tab later).

### Verify

1. Build clean (clean + rebuild if stale generated-XAML errors appear).
2. Theme Builder: each new control applies live, reverts on invalid input, survives resolution switch (new size copies values), Reset Layer, Reset Layout, Restore Defaults, Save Changes + reload of the theme, and close-without-saving revert.
3. Hover: all four buttons change colors and border on hover, return to resting on leave and when the hamburger menu closes. Search box border does not change on button hover.
4. Group move: checkbox moves all six parts by the same X/Y delta; Width/Height edits do not propagate; Game Info page shows the checkbox too and Random Game Display does not.
5. Default (right) alignment and DemiBold label look unchanged apart from the new padding.

### After the work

Update `themes.md`: change log entry, layer status table, section 3.5 (migrated list), 3.7 (tab table and group-move), decision list, open questions (button/Game Info bold variations, a future padding property, per-part ratio lock still open).

---

## Task B: Randomizer leftover cleanup (separate commit, independent of Task A)

The reveal/confirm popups were removed (the roll now happens on button press), but leftovers remain:
- `MainViewModel.IsRandomizerResultShowing` is never set true anymore. Remove it and simplify `IsRandomizerOverlayActive` to `IsRandomizerSpinning`. Keep the `IsRandomizerOverlayActive` property name since `MainWindow.xaml` triggers bind it.
- Remove the `IsRandomizerResultShowing` read in the die-click guard in `BtnPlayRandom_MouseLeftButtonUp`.
- Remove the temporary Ctrl+R `DebugTestRandomizerSpin` hotkey and the method.
- Leave the "removed" comment stubs alone for now.
- The Random Game Display *layer* (the spin animation layer with its two parts) stays; do not touch it.
