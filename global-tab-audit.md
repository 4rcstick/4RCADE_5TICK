# Global Tab: Pass 0 Audit

Project: 4RCADE 5TICK, Theme Builder, beta.4. Date: 2026-10-21.
Companion: `global-tab-plan.md` (the plan this audits), `themes.md`, `CLAUDE.md`.
Read-only pass: no source file was edited. Line numbers are from the working tree at commit `b0dcfea`.

---

## 0. Summary of what matters most

1. **The removal targets are clean.** `FolderSelected*`, `BorderColorFramework`, `Input*` and `MainBackgroundBrush` are only read in the places the plan expects, with one exception: **Manage Playlists reads `InputFontSize` / `InputColorHex` in code** (and the main window's folder-color popup reads them too, next to the folder-name popup). Both move to the palette anyway, so this is a report, not a blocker. The Games List reads its own per-entry selected colors (`MainViewModel.cs:1181-1182`), not the flat pair. Confirmed.
2. **Most `Theme*` forwarders for Global values have no consumers at all.** Only `ThemeHeaderColor`, `ThemeMainColor`, `ThemeBorderColor`, `ThemeScroll*` and `ThemeContextMenu*` are read by XAML. The other ~25 (`ThemeTab*`, `ThemeOptions*`, `ThemeSubText*`, `ThemeStandardColor`, `ThemeSubHeaderColor`, `ThemeSeparatorColor`, `ThemeFolderSelected*`, `ThemeBorderWidth/Curve`) are dead today.
3. **Several bindings are silently broken today and only look right because the fallback equals the default.** All 63 `ThemeBorderColor` bindings on the Basic tab, every `ArcadeDarkScrollViewerThemed` scrollbar on the Advanced tab (16 of them), and probably the main window's context-menu item bindings (see 8.1). DynamicResource fixes all of them. This is a strong argument for the plan's one-mechanism approach.
4. **Basic tab edits are not live today.** They only reach `_settings` on Save Adjustments / Save Changes (`SyncUiEntriesToSettingsLayer`). That is why no flat-value snapshot exists. Live Global edits make the snapshot (decision 6) necessary, as planned.
5. **The size of Passes 3 and 4 is much bigger than the plan suggests:** about 440 font/separator bindings across 13 files, and about 445 per-button bindings (`BtnBgColorHex` etc. set on each of ~100 buttons) across 13 files. Both are mechanical but need a scripted rewrite (Python) and careful review.
6. **DynamicResource cannot go through a converter.** The tab corner radius uses `RadiusConverter` with Top/Bottom; sizes are bound from `int`/`double` into `Thickness`/`FontSize`. The apply function must publish ready-typed resources (`CornerRadius` top/bottom variants, `Thickness`, `double`, `Brush`).
7. **Default mismatches** between `ConfigurationSettings` initializers and the `ThemeSettings` DTO (Header color, Standard size, Btn hover bg, OptionsBg). Restore Defaults (decision 8) would reset to values that differ from what a theme missing those keys loads with.
8. **Mismatches with the plan:** the MainWindow "local scrollbar copy" no longer exists; there are no RadioButtons; `TabHoverColorHex` is settings.json-only and read only by the Sorting window.

---

## 1. Readers of every flat property

Legend: **CS** = `Models/ConfigurationSettings.cs`, **AT** = `ConfigurationSettings.ApplyThemeSettings` (CS:361-421), **DTO** = `Models/ThemeSettings.cs`, **LT** = `MainViewModel.LoadTheme` mapping (`MainViewModel.cs:1338-1383`), **SAVE** = DTO builder in `ThemesTabControl.BtnSaveTheme_Click` (`ThemesTabControl.xaml.cs:1321-1369`), **LOAD-UI** = `LoadCurrentThemeValuesIntoUi` (`ThemesTabControl.xaml.cs:869-1211`), **SYNC** = `SyncUiEntriesToSettingsLayer` (`ThemesTabControl.xaml.cs:1523-1586`). Every property in decisions 2 and 4 has all of CS, AT, DTO, LT and SAVE unless noted.

### 1.1 Forwarders (`ViewModels/MainViewModel.cs`)

| Forwarder | Line | XAML consumers |
|---|---|---|
| ThemeMainColor | 602 | MainWindow (root fill, popups, banner, spin card). Out of scope; keep. |
| ThemeBorderColor (BorderColorFramework) | 603 | MainWindow.xaml 60, 88, 95, 749, 769, 771, 789, 796. **ThemesTabControl.xaml 63 lines: broken** (window DataContext is ConfigurationSettings, so they always show the fallback `#3A3A3C`). |
| ThemeBorderWidth / ThemeBorderCurve | 604-605 | none (the properties are "retired" per comments, but still in CS, DTO, LT, SAVE) |
| ThemeSeparatorColor | 606 | none |
| ThemeScrollTrack/TrackHover/Thumb/ThumbHover/ThumbDrag | 607-611 | App.xaml `ArcadeDarkScrollBarThemed` (90, 99, 113, 124, 127, 130) |
| ThemeFolderSelectedColor / BgColor | 624-625 | **none** |
| ThemeContextMenuFont/Icon/Bg/Hover/HoverBg | 814-818 | MainWindow.xaml 44, 53, 55, 56, 59, 71-73, 87 |
| ThemeSubTextFontSize / Color | 819-820 | none |
| ThemeOptionsMenuBgColor / BorderSize / BorderColor / BorderRadius | 821-824 | none |
| ThemeTabColorHover / TabBgColorHover | 825-826 | none |
| ThemeOptionsBtnFontSize / ColorHover / BorderSize / BorderRadius | 827-830 | none |
| ThemeTabColor / TabBgColor / TabActiveColor / TabActiveBgColor | 831-834 | none |
| ThemeStandardColor | 835 | none |
| ThemeHeaderColor | 836 | MainWindow.xaml 602-603 (no-art banner), 652-653 (spin card). Keep (decision 5). |
| ThemeSubHeaderColor | 837 | none |

All are raised in `RefreshThemeBindings` (`MainViewModel.cs:1119-1160`). There are no `ThemeHeaderFontSize`, `ThemeSubHeaderFontSize`, `ThemeStandardFontSize`, `ThemeTabFontSize` or `ThemeInput*` forwarders.

### 1.2 XAML and code readers, by property group

Counts are `{Binding X}` / `{Binding DataContext.X ...AncestorType=Window}` occurrences.

**Fonts**
- `HeaderFontSize` / `HeaderColorHex`: 23 / 22 bindings in 12 files (every Options tab, Sorting, Manage Playlists, Theme Builder Basic and Advanced). Code: `AdvancedThemesTabControl.xaml.cs:871-872` (`new Binding("HeaderColorHex")` on group-part headings). `MainWindow.xaml.cs` and `LayerRegistry.cs` hits are `SectionHeader*` entry fields, not this property.
- `SubHeaderFontSize` / `SubHeaderColorHex`: 75 / 99 bindings in 10 / 9 files. Code: `AdvancedThemesTabControl.xaml.cs:884-885`.
- `StandardFontSize` / `StandardColorHex`: 93 / 115 bindings in 12 / 13 files, including combo item foregrounds (`ThemesTabControl.xaml:79`, `AdvancedThemesTabControl.xaml:28`, `SortingWindow.xaml:264`, `ManagePlayListsWindow.xaml:26`) and checkbox labels. Code: `AdvancedThemesTabControl.xaml.cs:900-901`.
- `SubTextFontSize` / `SubTextColorHex`: 7 each, all in `AdvancedThemesTabControl.xaml` (hint lines 1441-1819).

**Backgrounds**
- `OptionsMenuBgColorHex`: `ThemeBuilderWindow.xaml:10`, `OptionsWIndow.xaml:10` (window backgrounds).
- `OptionsBg`: `App.xaml:205, 245` (tab border base, tab control content), `OptionsWIndow.xaml:21, 65`, `SortingWindow.xaml:20, 65`. Code: `ManagePlayListsWindow.xaml.cs:71-79` (window background; blank means MainBackgroundBrush).
- `SeparatorColorHex`: 26 bindings in 9 files (all `<Separator Background=...>`).
- `OptionsMenuBorderSize / ColorHex / Radius`: `App.xaml:206, 208, 246, 247, 268`, `OptionsWIndow.xaml:22, 23, 46, 66, 68`. Radius goes through `RadiusConverter` (Top/Bottom).

**Tabs**
- `TabFontSize, TabColorHex, TabBgColorHex, TabColorHoverHex, TabBgColorHoverHex, TabActiveColorHex, TabActiveBgColorHex`: `App.xaml:200-236` (shared styles), `OptionsWIndow.xaml:60-98` (inline copy), `SortingWindow.xaml:60-89` (own variant), `AdvancedThemesTabControl.xaml:99-111` (GlyphToggleStyle) and 251 (`TabBgColorHoverHex` as LstLayers hover), `ManagePlayListsWindow.xaml:43` (`TabActiveColorHex` as combo selected text).
- **Flag:** `TabBgColorHoverHex` and `TabActiveColorHex` are borrowed for non-tab controls (layer list hover, Manage Playlists combo). These move to the palette in Pass 2.

**Buttons**
- `BtnBgColorHex, BtnTextColorNormalHex, BtnBorderColorHex, OptionsBtnBorderSize`: set **per button instance** (~101 buttons, 445 bindings across 13 files: MameKeybinds 49 buttons, Advanced 23, AssetPaths 6, FolderOrder 5, SystemPaths 4, PreviewsOrder 3, ThemesTab 3, Options 2, Sorting 2, ThemeBuilder 2, Scraper 1). Code: `AdvancedThemesTabControl.xaml.cs:3142-3145` (saved-size buttons).
- `OptionsBtnFontSize, OptionsBtnBorderRadius, BtnBgColorHoverHex, OptionsBtnColorHoverHex`: inside `ThemedActionButtonStyle` (`App.xaml:283-293`) and Manage Playlists' own `StandardButtonStyle` (`ManagePlayListsWindow.xaml:55-69`).
- **Flag (borrowed):** `BtnBgColorHex` / `BtnBgColorHoverHex` / `BtnTextColorNormalHex` are also the combo box colors (ThemesTab, Advanced, Sorting, Manage Playlists combos) and `ThemedComboBoxStyle`; `BtnBgColorHoverHex` is the Sorting tab hover background (`SortingWindow.xaml:78`); `BtnTextColorNormalHex` is `ThemedTabControlStyle`'s Foreground (`App.xaml:248`). The combos move to the palette in Pass 2; the other two need a decision in Pass 4.

**Scrollbars**
- `ScrollTrackColor ... ScrollThumbDragColor`: `App.xaml:26-66` (`ArcadeDarkScrollBar`, ConfigurationSettings windows) and the `Theme*` forwarders in `ArcadeDarkScrollBarThemed` (MainViewModel windows).

**Context menus**
- `ContextMenu*ColorHex`: only through the `ThemeContextMenu*` forwarders in MainWindow.xaml (1.1).

**Removal targets (decision 4)**
- `FolderSelectedColorHex` / `FolderSelectedBgColorHex` (flat): CS 133-134, AT 376-377, DTO 27-28, LT 1348-1349, SAVE 1333-1334, LOAD-UI 1072/1082, SYNC 1540-1541, Basic tab section `ThemesTabControl.xaml:129-211`. XAML readers: `AdvancedThemesTabControl.xaml:33` (combo item hover text), 254-255 (LstLayers selected); `ThemesTabControl.xaml:85` (combo hover text); `SortingWindow.xaml:270` (combo hover text); `ManagePlayListsWindow.xaml:39, 42` (combo hover text, selected bg). Nothing else. **No unexpected reader.**
- `BorderColorFramework`: CS 59, AT 370, DTO 21, LT 1339, SAVE 1324 (no UI field, no SYNC line). XAML: `ThemeBuilderWindow.xaml:11`, `OptionsWIndow.xaml:11`, `SortingWindow.xaml:10, 21, 66, 235, 252`, `ManagePlayListsWindow.xaml:14, 99, 132, 171, 211`, `ThemesTabControl.xaml:50, 67` (+ the 63 broken `ThemeBorderColor`), `AdvancedThemesTabControl.xaml` 45 lines (combos, GlyphToggle, picker borders, LstLayers). Main window through `ThemeBorderColor` (1.1). **No unexpected reader.**
- `InputFontSize` / `InputColorHex`: CS 147-148, AT 394-395, DTO 41-42, LT 1361-1362, SAVE 1346-1347 (LOAD-UI and SYNC lines are commented out, no UI field). XAML: `MainWindow.xaml:751, 756` (folder-name popup) **and 796** (folder-color popup hex box). Code: **`ManagePlayListsWindow.xaml.cs:88-95`** (font size and color of both text boxes). **These two are outside decision 4's list.** Both are covered by Pass 2 anyway (the color popup is the same popup family; Manage Playlists moves to the palette), so I suggest simply extending decision 4 to them.
- `TabHoverColorHex` vs `TabColorHoverHex` (decision 9): **`TabColorHoverHex` is the live one** (CS 164, full wiring, Basic tab field `TxtTabColorHoverHex`, read by App.xaml 236, OptionsWindow 98, Advanced 107). **`TabHoverColorHex`** (CS 140) is in AT 387 and DTO 34 but **not in LT and not in SAVE**, so it never reaches or leaves a theme file; it only survives in settings.json. Its only reader is `SortingWindow.xaml:79` (Sorting tab hover text). Drop it and point Sorting at `TabColorHoverHex` (or at the shared tab style, see 3.4).

### 1.3 Other flat theme properties found (not in the plan)

- `BorderWidthValue`, `BorderCurveValue`: CS 150-151, AT, DTO, LT, SAVE; forwarders unused; comments call them retired. Remove in Pass 7.
- `VideoBoxRecordBg`, `TextColor`, `MainFoldersColor`, `FavoritesColor` (CS 52-56): no readers. `VirtualListsColor`: one reader in MainViewModel (not theme-related to this plan). Leave for a separate cleanup.
- `ApplyThemeSettings` assigns `TabColorHex` twice (CS 382-383). Harmless.

### 1.4 Default mismatches (CS initializer vs DTO default)

| Property | CS | DTO | Notes |
|---|---|---|---|
| HeaderColorHex | `#FFEF44C3` | `#FFFFCC00` | XAML fallbacks mostly use `#FFCC00` |
| StandardFontSize | 13 | 12 | |
| BtnBgColorHoverHex | `#FF422a3c` | `#FF2C2C2E` | XAML fallbacks use `#2C2C2E` |
| OptionsBg | `#FF000000` | `""` | blank switches Manage Playlists to MainBackgroundBrush |
| FolderSelected pair | `#FF000000` / `#FF00D4FF` | `#FF00D4FF` / `#FF2C2C2E` | removed in Pass 2, moot |

Decision 8 says Restore Defaults uses the CS initializers. A theme saved without a key gets the DTO default. **Pick one source** before Pass 1 builds the Restore Defaults helper (recommendation in section 9).

---

## 2. Theme Builder refresh, save and close flows

`Views/ThemeBuilderWindow.xaml.cs`:

- **Own chrome refresh:** `RefreshWindowBindings()` (117-121) nulls and resets `DataContext` (= ConfigurationSettings) so every `{Binding X}` re-reads. Called after a theme load (`RefreshAfterThemeLoad`, 86) and after Save Adjustments (157). There is no live path for flat values: Basic tab edits sit in the Basic tab's text boxes until a save.
- **Snapshot today:** `_layoutsAtLastSave`, `_resolutionWidthAtLastSave`, `_resolutionHeightAtLastSave` (12-14). Taken on open (63-65); moved by Save Changes through `SetThemeSavedCallback` (29-38), by Save Adjustments (148-151) and by any theme load (`RefreshAfterThemeLoad`, 90-92). **No flat value is snapshotted.** Not needed today because flat values only change on save or load.
- **Close (`Closed`, 67-77):** restores layouts and the active size, hides guides, calls `MainWindow.ApplyLayoutNow()`. It does **not** call `RefreshThemeBindings`. A Global restore must re-run the apply function here.
- **Save Adjustments (139-163):** `ThemesTab.SyncUiToSettings()` (writes every Basic tab field into `_settings`, then reloads them), persist, commit pending size, move snapshot, `RefreshThemeBindings`, folder/game color refresh, `RefreshWindowBindings`.
- **Save Changes** (`ThemesTabControl.xaml.cs:1298-1453`): `SyncUiEntriesToSettingsLayer()` first (1318), DTO from `_settings`, zip, persist, `RefreshThemeBindings`, then the saved callback (snapshot move).
- **Basic tab Load Theme** (`ThemesTabControl.xaml.cs:1229-1247`): `LoadTheme`, `LoadCurrentThemeValuesIntoUi`, then the refresh callback `RefreshAfterThemeLoad` (snapshot move).
- **External theme switch** (`RefreshAfterExternalThemeSwitch`, 97-101; called from `MainWindow.xaml.cs:2398` after `LoadTheme`): Basic tab reload, then `RefreshAfterThemeLoad`.
- **External size switch** (`RefreshAfterExternalSizeSwitch`, 106-111; called from `MainWindow.xaml.cs:2434`): Advanced tab reload and size restore point only. Global values are theme-wide, so **nothing to do here** for the Global snapshot.

**Where a Global snapshot hooks in:** one private `CaptureGlobalSnapshot()` called at the same four places the layout snapshot moves (constructor, saved callback, Save Adjustments, `RefreshAfterThemeLoad`), and one `RestoreGlobalSnapshot()` in `Closed` before `ApplyLayoutNow`, followed by the apply function. Simplest storage: a shallow copy of the Global properties (a small DTO, or reuse `ThemeSettings`, which already lists every flat theme key).

**Transition trap (decision 7, confirmed):** Save Adjustments and Save Changes both run `SyncUiEntriesToSettingsLayer`, which overwrites `_settings` from the Basic tab's text boxes. In each pass, every migrated property must leave SYNC (and LOAD-UI, and its `_suppress...Sync` bool and handlers) in the same pass, or the Basic tab's stale text will overwrite the live Global value on save.

**Apply hook:** `MainViewModel.ThemeBindingsRefreshed` (`MainViewModel.cs:1111`, raised at the end of `RefreshThemeBindings`) already fires after LoadTheme, Save Adjustments, Save Changes and the Options window's save. `MainWindow` subscribes its own refreshers there (`MainWindow.xaml.cs:110-113`), and `UpdateMainBackgroundBrush` (183-191) is the existing model. Subscribing the Global apply function to that event, plus one direct call in the MainWindow constructor (the event fires inside the view model constructor before MainWindow subscribes, the same reason `ApplyLayout()` is called directly at 119), covers startup, theme load and every save. The Global tab's own edits, Restore Defaults and close-revert call it directly.

---

## 3. App.xaml and window styles

### 3.1 App.xaml (`App.xaml`)

| Style | Lines | Binding pattern | Notes |
|---|---|---|---|
| `MainBackgroundBrush` | 15 | resource | replaced by `MainWindow.UpdateMainBackgroundBrush` |
| `ArcadeDarkScrollBar` | 22-78 | `{Binding ScrollTrackColor}` (plain DataContext = ConfigurationSettings) | header comment says MainWindow has a local copy: **stale** |
| `ArcadeDarkScrollBarThemed` | 86-142 | `{Binding ThemeScroll*}` (DataContext = MainViewModel) | |
| `ArcadeDarkScrollViewerThemed` | 144-164 | template; **`Width="10"` hard-coded at 159** | only a vertical bar; no horizontal bar in the template |
| `ArcadeDarkScrollViewer` | 169-188 | template; **`Width="10"` hard-coded at 183** | |
| `ThemedTabItemStyle` | 197-242 | `DataContext.X` + `AncestorType=Window`; FontSize `{Binding TabFontSize}`; radius through `RadiusConverter` Top | |
| `ThemedTabControlStyle` | 244-275 | plain `{Binding OptionsBg / OptionsMenuBorderColorHex / OptionsMenuBorderSize / BtnTextColorNormalHex}`; radius through `RadiusConverter` Bottom | |
| `ThemedActionButtonStyle` | 281-302 | FontSize, radius, hover bg, hover text via `DataContext.X` + `AncestorType=Window`. **Background, Foreground, BorderBrush, BorderThickness come from each button** (TemplateBinding) | |

There are no keyless (implicit) styles in App.xaml today.

### 3.2 Scrollbar usage and the width

- `ArcadeDarkScrollViewer` (plain): Basic tab (`ThemesTabControl.xaml:14`), FolderOrder list templates (63-73, 155-163), MameKeybinds, Scraper, About, Sorting (101).
- `ArcadeDarkScrollViewerThemed`: Games List (`MainWindow.xaml:299`, implicit BasedOn in TreeView.Resources), GameInfoLayerControl (101, 141, 144, 168), and **16 ScrollViewers on the Advanced tab (304, 387, 537, 686, 841, 929, 1016, 1109, 1273, 1420, 1471, 1519, 1550, 1606, 1628, 1659, 1690, 1751)**. The Advanced tab's DataContext is ConfigurationSettings, so its `ThemeScroll*` bindings fail: **the Advanced tab's scrollbars never follow the scrollbar settings** (always the fallbacks, which equal the defaults).
- **Mismatch with the plan / CLAUDE.md:** MainWindow has **no local scrollbar copy** anymore; `MainWindow.xaml:292-299` is a one-line implicit `ScrollViewer` style BasedOn the App-level Themed style. Pass 5 only has App.xaml to change.
- Not themed at all: the ScrollViewers inside every custom ComboBox dropdown (`ThemesTabControl.xaml:68`, `AdvancedThemesTabControl.xaml:62`, `SortingWindow.xaml:253`, `ManagePlayListsWindow.xaml:172`), the default ListBox ScrollViewers (`LstLayers`, `LstMediaOrder`) and system-styled combos. They show the Windows scrollbar. Worth covering in Pass 2 (palette combo/list templates use `ArcadeDarkScrollViewer`) so Pass 5's width reaches them.
- **Scrollbar Width default: 10** (both templates). New property default must be 10.

### 3.3 Templates that ignore simple Background/Foreground setters

- **CheckBox** (Aero2 template): the box, border and check glyph ignore Background/BorderBrush in practice; only the label Foreground works. Every checkbox in the app shows the white Windows box with blue hover today.
- **Slider** (4 in Sorting): track and thumb are system-drawn; no setter colors them.
- **ComboBox**: the closed box ignores Background on Windows 10 (the reason four files already carry a full custom template). Default-template combos remain in `InputsTabControl.xaml:64` and `MAMEiniTabControl.xaml:35, 50` (light Windows combos inside a dark Options window).
- **ListBoxItem**: selection highlight comes from the system template (see section 4).
- **ScrollBar**: needs the full template (already done).
- **TextBox**: Background/Foreground/BorderBrush work, but hover and focus border colors are drawn by the template (Aero2 blue). A keyed template is needed for a palette focus color.
- **Button** with no style (Name prompt, RomCopyPath window): Windows hover chrome.
- **RadioButton:** none exist in the project.

### 3.4 Per-window tab styling

- **Theme Builder top tabs and the Advanced layer tabs:** the shared App.xaml styles.
- **OptionsWindow.xaml (20-106):** an inline template and TabItem style that are a **copy of the shared styles** (same bindings, same radius converter, same hover MultiTrigger). It can switch to `ThemedTabControlStyle` with no visual change (the shared control style also sets Foreground to `BtnTextColorNormalHex`, the same value the Options TabControl sets).
- **SortingWindow.xaml (19-97):** its own variant that **differs**: tab border `BorderColorFramework` instead of `OptionsMenuBorderColorHex`, fixed `BorderThickness="1"`, no corner radius, hover background `BtnBgColorHoverHex` instead of `TabBgColorHoverHex`, hover text `TabHoverColorHex` (the dead duplicate), and hover also fires on the selected tab. Switching it to the shared style is a small visible change (hover colors, radius).

### 3.5 Window backgrounds today

| Window | Background | Border | Modal |
|---|---|---|---|
| ThemeBuilderWindow | `OptionsMenuBgColorHex` | `BorderColorFramework` | **no** (`Show`, single instance, owned by MainWindow; `MainWindow.xaml.cs:2473-2488`) |
| OptionsWindow | `OptionsMenuBgColorHex` | `BorderColorFramework` | yes (`MainWindow.xaml.cs:2209-2217`) |
| SortingWindow | `MainBackgroundBrush` | `BorderColorFramework` | yes (2246, 2453) |
| ManagePlayListsWindow | `#1E1E24` in XAML, overridden in code to `OptionsBg` or `MainBackgroundBrush` | `BorderColorFramework` | yes (2128-2129) |
| RomCopyPathWindow | `#FF000000` hard-coded, no DataContext | none | yes (2177-2178) |
| Layer name prompt (`AdvancedThemesTabControl.xaml.cs:556-594`) | system default (white), built in code | system | yes (owner Theme Builder) |

---

## 4. Folder Order list look and the proposed palette

### 4.1 What the Folder Order lists actually are

`Views/FolderOrderTabControl.xaml:57-74` (`LstFolderOrder`) and 150-164 (`LstSubfolderOrder`), same as `PreviewsOrderTabControl.xaml:35-39` (`LstMediaOrder`, without the scroll template):

- Background `#000000`, Foreground `#F2F2F7`, BorderBrush `#3A3A3C`, BorderThickness 1, Padding 5, FontSize 14, FontWeight Bold (all hard-coded).
- Template: a Border plus `ArcadeDarkScrollViewer` around the ItemsPresenter (themed scrollbar).
- Items are plain strings (`ItemsSource`, code-behind 49-53) with **no ItemContainerStyle**, so the item states are the **stock Windows 10 (Aero2) ListBoxItem** look:
  - hover: background `#1F26A0DA`, border `#A826A0DA`
  - selected (list focused): background `#3D26A0DA`, border `#FF26A0DA`
  - selected (list not focused, e.g. right after clicking the Up button): background `#3DDADADA`, border `#FFDADADA`
  - text stays `#F2F2F7`; disabled text is the system gray.

So the "Folder Order look" is: black field, thin `#3A3A3C` frame, light text, and a **translucent highlight with a 1px solid border** rather than a solid fill. Today the blue is Windows' accent, not the app's cyan.

The Advanced tab today: field `#1C1C1E`, text `White`, border `#3A3A3C`; LstLayers selection solid `FolderSelectedBgColorHex` with `FolderSelectedColorHex` text (cyan / black by default).

### 4.2 Proposed `[SECTION: Chrome Palette]` keys

Recommended values keep the Folder Order structure and swap the Windows blue for the app's cyan (`#00D4FF`) at the same Aero2 alphas, so the look carries over but matches the rest of the UI.

| Key | Value | Used for |
|---|---|---|
| ChromeWindowBg | `#FF1C1C1E` | Manage Playlists, name prompts, RomCopyPath, color picker popups (today's default MainBackground / Options window color, so default themes look unchanged) |
| ChromeBg | `#FF000000` | field background: text boxes, list boxes, closed combo box, dropdown list, checkbox box (Folder Order) |
| ChromeBgHover | `#FF1C1C1E` | closed combo box and text box under the mouse |
| ChromeBorder | `#FF3A3A3C` | every field, dropdown and checkbox border; replaces `BorderColorFramework` |
| ChromeBorderHover | `#FF636366` | field border under the mouse |
| ChromeAccent | `#FF00D4FF` | keyboard-focus border, check mark, combo arrow, slider thumb, text selection |
| ChromeText | `#FFF2F2F7` | text in fields, lists, combos; caret |
| ChromeTextMuted | `#FFA0A0A2` | placeholder text (folder-name popup uses `#808080` today) |
| ChromeItemHoverBg | `#1F00D4FF` | list and dropdown item hover |
| ChromeItemHoverBorder | `#A800D4FF` | list item hover border |
| ChromeSelectedBg | `#3D00D4FF` | selected item, list focused |
| ChromeSelectedBorder | `#FF00D4FF` | selected item border, list focused |
| ChromeSelectedText | `#FFF2F2F7` | selected item text |
| ChromeSelectedInactiveBg | `#3DDADADA` | selected item, list not focused |
| ChromeSelectedInactiveBorder | `#FFDADADA` | same |
| ChromeSliderTrack | `#FF3A3A3C` | slider track |

- **Disabled:** opacity 0.5 on the whole control (the convention `ThemedActionButtonStyle` already uses), so no extra key.
- **Inactive selection:** the two inactive keys reproduce the Folder Order grey-out. If you'd rather the selection stay cyan when focus moves to the Up/Down buttons, set them equal to the active pair.
- **Alternative, solid selection** (today's Advanced look): `ChromeSelectedBg #FF00D4FF`, `ChromeSelectedBorder #FF00D4FF`, `ChromeSelectedText #FF000000`. It's three values in the palette block, so it's easy to try both after Pass 2.
- **Font:** family and size stay themeable (decision 3). Recommend the Standard size (`Global.Font.Standard.Size`) for every palette control. Visible change: Folder Order / Previews Order lists drop from a hard-coded 14 Bold to the Standard size, and plain text boxes go from the WPF default 12 to Standard (13 by default).

---

## 5. Control inventory

T = theme-bound (flat properties), F = fixed hex, S = system default.

### Theme Builder, Basic tab (`ThemesTabControl.xaml`)
- `CboThemePresets` (40-90): custom template, T (Btn*, BorderColorFramework, StandardColorHex, FolderSelectedColorHex).
- 39 TextBoxes (hex and size fields, `TxtNewThemeName`): F (`#1C1C1E` / `White` / `#3A3A3C`).
- 31 PortableColorPickers: popup background `MainBackgroundBrush`; hex box border and picker frame `ThemeBorderColor` (broken, fallback).
- No CheckBox, ListBox, Slider.

### Theme Builder, Advanced tab (`AdvancedThemesTabControl.xaml`)
- 12 ComboBoxes + 1 (`CboResolution`, font, alignment, visibility, shadow target ...): `ThemedComboBoxStyle`, T (Btn*, BorderColorFramework, Standard, FolderSelected).
- `LstLayers` (238-263): background F `#1C1C1E`; border T (`BorderColorFramework`); text T (Standard); hover T (`TabBgColorHoverHex`); selected T (FolderSelected pair).
- 97 TextBoxes: 95 `GLTextBoxStyle` (F), 3 inline F (`TxtCustomWidth`, `TxtCustomHeight`, `TxtGuideColorHex`). Plus code-built group-part boxes (`AdvancedThemesTabControl.xaml.cs:842-853`, F).
- 24 CheckBoxes: `GLCheckBoxStyle` (label T: Standard), `ChkShowGuides` inline T; code-built "Hide" boxes (899-901, `new Binding` T). Box glyph S.
- 41 PortableColorPickers: `PickerPopupGridStyle` (MainBackgroundBrush); frame border T (`BorderColorFramework`).
- GlyphToggle (ratio lock): T (tab colors).
- Name prompt (556-594): all S (white window, system TextBox and Buttons).

### Options window and its tabs
- FolderOrder: 2 ListBoxes F (4.1).
- PreviewsOrder: `LstMediaOrder` F (no scroll template, so system scrollbar).
- MAME.ini: `CboVideoMode`, `CboFrameSkip` **S** (light Windows combos); 13 CheckBoxes label T, box S.
- MAME Keybinds: no input controls of these types (49 buttons).
- System Paths: 3 TextBoxes F.
- Asset Paths: 6 TextBoxes F.
- Inputs: `ChkGamepad` label F (`#F2F2F7`, 13); 4 TextBoxes F; `CboNavigationMode` **S**.
- Scraper: 10 CheckBoxes label T, box S.
- About: text only.

### Sorting window
- 20 CheckBoxes: label T (Standard), box S.
- `CmbManufacturer`: custom template, T (Btn*, BorderColorFramework, Standard, FolderSelected).
- 4 Sliders: **S**.

### Manage Playlists
- `FolderComboBox`: custom template, T (Btn*, BorderColorFramework, Standard, FolderSelected, TabActiveColorHex).
- `FolderNameTextBox`: background F `Black`, text set in code from `InputColorHex` / `InputFontSize`.
- `HexColorTextBox`: background F `#2D2D35`, text from `Input*` in code.
- `PlaylistColorPicker`: **no popup style** (system picker popup).
- Buttons: own `StandardButtonStyle` / `ConfirmButtonStyle` / `DangerButtonStyle` (T).

### RomCopyPath window
- `DestinationPathTextBox` F; three unstyled Buttons (S hover). Not in the plan's list; a dev tool (Shift+Ctrl+X).

### Main window
- `SearchBox`: layer-entry styled; out of scope (decision 10).
- `FolderNameInputTextBox` (751): `Input*`, transparent background.
- `FolderColorHexTextBox` (796): `Input*`, border `ThemeBorderColor`.
- `FolderColorPickerControl` (772-794): popup background `ThemeMainColor`, hex box border `ThemeBorderColor`.

---

## 6. Modality

- **Modal (`ShowDialog`)**: Options, Sorting, Manage Playlists, RomCopyPath (all owned by MainWindow); the layer name prompt and file/folder dialogs (owned by Theme Builder or Options).
- **Non-modal**: Theme Builder (`Show`, single instance).
- **At the same time as the Theme Builder:** yes. The main window stays usable, so Options, Sorting and Manage Playlists can be opened from it (hamburger, gear, context menu) while the Theme Builder is open. `ShowDialog` then disables every other window of the app, including the Theme Builder, until the dialog closes. So Global values cannot change *while* a dialog is open, but a dialog opened after live (unsaved) edits shows them, and a later close-without-saving reverts them. DynamicResource handles that with no extra work.
- The name prompt is modal over the Theme Builder.

---

## 7. MainBackgroundBrush and Menu Selected consumers

**MainBackgroundBrush** (`App.xaml:15`, replaced by `MainWindow.UpdateMainBackgroundBrush`, `MainWindow.xaml.cs:183-191`, called from `ApplyLayout` 317-323):
- `AdvancedThemesTabControl.xaml:17` (`PickerPopupGridStyle`, used by 41 pickers)
- `ThemesTabControl.xaml`: 31 inline picker popup styles (e.g. 173, 194)
- `SortingWindow.xaml:9` (window background)
- `ManagePlayListsWindow.xaml.cs:73` (window background when `OptionsBg` is blank)

All four move to the palette in Pass 2 (`ChromeWindowBg`), after which nothing reads it and it can go (resource, key constant, `UpdateMainBackgroundBrush` and its call; `RefreshMainBackgroundColor` / `ThemeMainColor` stay for the main window). The main window's own popups bind `ThemeMainColor` directly, not the brush.

**Menu Selected pair:** see 1.2. Flat pair readers are only Theme Builder / Sorting / Manage Playlists combo and list highlights; `ThemeFolderSelected*` forwarders have no readers.

---

## 8. Things that block or complicate the one-mechanism plan

1. **Context menus: the current bindings probably fail.** The main window's implicit `MenuItem` template (`MainWindow.xaml:44-73`) and `Separator` (95) use `DataContext.X` with `RelativeSource AncestorType=Window`. A ContextMenu lives in its own popup tree with no Window ancestor, so these bindings likely fall back (`#1C1C1E` bg, which matches the default; **hover background fallback is `#EF44C3` pink**, the default setting is `#2C2C2E`). Quick check: if the context-menu hover is pink, the bindings are failing; if it follows the Basic tab's Hover BG color, they work. `ContextMenu`'s own setters (87-88) use plain `{Binding}` through the inherited DataContext and do work. Either way, `{DynamicResource}` resolves to `Application.Resources` from popups and fixes this.
2. **Converters:** `RadiusConverter` (`Models/RadiusConverter.cs`, used by `App.xaml:208, 268` and `OptionsWIndow.xaml:46, 68`) splits one radius into Top or Bottom corners. DynamicResource has no converter, so publish `Global.Options.RadiusTop` / `RadiusBottom` (`CornerRadius`) from the apply function. The converter can then be deleted when the last use goes.
3. **Resource types must match the target property exactly** (no implicit conversion as with Binding): font sizes as `double` (the settings are `int`), border sizes as `Thickness` (plus `double` where a double is wanted), colors as frozen `Brush` via `SafeConvertToBrush` (values like `"Transparent"` and 6-digit `#000000` exist), radius as `CornerRadius`.
4. **Per-instance button bindings (~101 buttons):** locally set Background/Foreground/BorderBrush/BorderThickness override style setters. Pass 4 has to delete those attributes from every button and move the values into `ThemedActionButtonStyle` (Background, Foreground, BorderBrush, BorderThickness setters on DynamicResource). The per-button fallback colors (red `#FF453A` Delete, yellow `#FFD60A` Save) only show when a binding fails, which never happens in these windows, so nothing visible is lost. Manage Playlists' own button styles should be folded into the shared style at the same time.
5. **Code-built UI with bindings or colors:** `AdvancedThemesTabControl.xaml.cs` 842-853 (fixed hex on group-part boxes), 871-872 / 884-885 / 900-901 (`new Binding("HeaderColorHex")` etc.), 3137-3145 (saved-size buttons); `ManagePlayListsWindow.xaml.cs:67-98` (`ApplyThemeConfigurations`). Convert to `SetResourceReference` / palette styles. The name prompt needs the palette styles applied in code.
6. **Main window code-built menu items** (`MainWindow.xaml.cs:1387, 1438, 2346, 2371`, `CreateCheckIcon` 2322) set no colors and inherit the implicit MenuItem style, so they follow automatically. No blocker.
7. **PortableColorPicker internals** are themed through each picker's `Resources` (Grid named `MainGrid`, nested `HexColorTextBox` TextBox style). This already works with DynamicResource (MainBackgroundBrush). 73 pickers carry this block; the Basic tab ones are inline copies rather than `PickerPopupGridStyle`. Moving `PickerPopupGridStyle` to App.xaml lets every picker share one style.
8. **Order of startup:** `LoadTheme("default")` and the first `RefreshThemeBindings` run inside the `MainViewModel` constructor, before MainWindow subscribes to `ThemeBindingsRefreshed`. The apply function needs one direct call in the MainWindow constructor (pattern at `MainWindow.xaml.cs:117-125`). App resources exist by then (StartupUri).
9. **Frozen resources:** replace, never mutate (plan note confirmed; `UpdateMainBackgroundBrush` is the pattern).
10. **Volume of edits (Pass 3):** Header / SubHeader / Standard / SubText / Separator bindings total about 440 across 13 files. A scripted regex rewrite (`{Binding StandardColorHex, FallbackValue=...}` → `{DynamicResource Global.Font.Standard.Color}`, both plain and `DataContext.X ... AncestorType=Window` forms) is the realistic way; Python with `newline=''`.
11. Nothing found that sets Global colors on main-window elements directly from code (the main window buttons are layer-entry styled and out of scope).

---

## 9. Recommendations

### 9.1 Decisions to settle before Pass 1
1. **Default source.** Recommend making the DTO defaults equal the `ConfigurationSettings` initializers in Pass 1 (Header color, Standard size, Btn hover bg; leave `OptionsBg` blank-vs-black as is until Manage Playlists moves to the palette), so Restore Defaults and "old theme missing a key" agree. You choose which color wins for Header (`#FFEF44C3` pink in code vs `#FFFFCC00` yellow in the DTO and most XAML fallbacks).
2. **Sorting window background.** The plan puts Sorting on the fixed palette, but it is a tabbed sister of the Options window (themed tabs, themed content area). Recommend: Sorting follows the Options window (`OptionsMenuBgColorHex`, Global Backgrounds) and moves to the shared tab styles; only Manage Playlists, the prompts, RomCopyPath and the picker popups use `ChromeWindowBg`.
3. **Checkbox label color.** Decision 3 fixes text color inside palette controls. Checkbox labels (MAME.ini, Scraper, Sorting, Advanced) are themed with the Standard color today. Recommend: the label keeps the Standard Fonts color and size; only the box, border and check mark use the palette.
4. **RomCopyPath window:** include it in Pass 2 (it costs little and it's the last white-hover dialog), or leave it as a dev tool. Recommend include.
5. **Extend decision 4** to the folder-color popup hex box and Manage Playlists for `Input*` (1.2).

### 9.2 Pass order and scope
- **Pass 1:** as planned, plus: aligned DTO defaults; the apply function subscribed to `ThemeBindingsRefreshed` plus one direct call at MainWindow startup; typed resources (Brush, double, Thickness, CornerRadius Top/Bottom). The snapshot can reuse the `ThemeSettings` DTO shape.
- **Split Pass 2** (the largest and riskiest pass, touching every window):
  - **2a: palette and styles.** The Chrome Palette block, keyed styles (TextBox, ComboBox + item, ListBox + item, CheckBox, Slider; no RadioButton, none exist), the Advanced shared styles moved to App.xaml on the palette (`PickerPopupGridStyle` too, so all 73 pickers share it), and the Theme Builder's own controls (both tabs, code-built boxes, name prompt).
  - **2b: everything else and removals.** Options tabs, Sorting, Manage Playlists, RomCopyPath, the main window's folder popups; then remove the Menu Selected pair, `BorderColorFramework` (except the context-menu pieces, Pass 6), `Input*` and `MainBackgroundBrush`.
- **Pass 3:** as planned, but expect a scripted rewrite of about 440 bindings; review the diff per file.
- **Pass 4:** add to scope: move the Options window and the Sorting window to `ThemedTabControlStyle` (removes two inline copies and the `TabHoverColorHex` reader), strip ~445 per-button bindings into `ThemedActionButtonStyle`, fold Manage Playlists' button styles in, and decide what replaces `BtnTextColorNormalHex` as the tab control's Foreground.
- **Passes 5 and 6** are small (App.xaml only for scrollbars; MainWindow for menus). They could be merged if you want fewer stops.
- **Pass 7:** also remove the dead forwarders listed in 1.1, `BorderWidthValue` / `BorderCurveValue` (and their forwarders), and `RadiusConverter` once unused.

---

## 10. Mismatches between the plan / CLAUDE.md / themes.md and the code

| Where | Says | Code |
|---|---|---|
| Plan Pass 0 item 3, Pass 5; CLAUDE.md | MainWindow has a local scrollbar copy | No copy: an implicit BasedOn style (`MainWindow.xaml:299`); App.xaml comment at 20-21 is stale |
| Plan decision 3, Pass 2 | RadioButtons to style | None exist |
| Plan decision 4 | `Input*` only in the folder-name popup | Also the folder-color popup (`MainWindow.xaml:796`) and Manage Playlists code (`ManagePlayListsWindow.xaml.cs:88-95`) |
| Plan decision 9 | find the live tab hover property | `TabColorHoverHex` live; `TabHoverColorHex` is settings.json-only, read by `SortingWindow.xaml:79` |
| themes.md 3.5 | FolderSelected pair "still used by the Games List" | The Games List uses its own entry fields; the flat pair is only Theme Builder / dialog chrome (the same section's later sentence says so) |
| themes.md 3.6 | scrollbars from `ArcadeDarkScrollViewerThemed`, global | True for the main window; the Advanced tab uses it too, but its bindings fail there (3.2) |
| themes.md 3.11 | MainBackgroundBrush used by the Theme Builder pickers, Sorting, Manage Playlists | Confirmed (no other consumer) |
| `ThemesTabControl.xaml` | binds `ThemeBorderColor` | That forwarder is on MainViewModel; the window's DataContext is ConfigurationSettings, so all 63 bindings use the fallback |

themes.md is not updated in this pass (no code changed). Its corrections belong to the pass that changes the code.
