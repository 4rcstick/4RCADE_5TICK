# Game Info + Preview Panel Migration: Spec for Claude Code

Project: 4RCADE 5TICK (C#/WPF), Theme Builder rework, beta.4.
Date: 2026-10-21.
Companion doc: `themes.md` (the living spec). **Trust order: code first, themes.md second.** If this file disagrees with the code, the code wins: report the mismatch, do not guess.

---

## 0. How to work

- **Edit the repo files directly.** (The chat-session rules in themes.md section 1 about "replace X with Y" instructions do not apply here.)
- **Do the work in three passes (section 3 to 5). Stop after each pass** so the user can clean-build, test and commit. Do not start the next pass until the user says so. Do not commit anything yourself.
- **Step 0 (before any edit): verify names.** Grep the whole solution (all `.cs` and `.xaml`) for every name this file mentions, and for these usage checks:
  `ThemeMediaWallpaper`, `ThemeMediaColor`, `ThemeVideoBgColor`, `ThemeVideoBorder`, `ThemePreviewBorder`, `ThemeNavIcons`, `ThemeGameNameHeader`, `ThemePubYearRating`, `ThemeInfoHeader`, `ThemeInfoBody`, `ThemeCredits`, `ThemeBootSplash`, `ThemeMissingPreview`, `MediaWindowWallpaper`, `VideoBoxBg`, `NavIconsColorHex`, `NavIconsSize`, `BtnBrowseFile_Click`.
  Also open `ThemeSettings.cs` (the DTO; not uploaded to the chat) and check `OptionsWindow` / `ThemeBuilderWindow` / any `MediaPanelControl` leftovers.
  **If a flat property or forwarder is used anywhere outside the places listed in this file, stop and report it instead of deleting it.** Report the grep results briefly before starting Pass 1.
- Keep the project's comment style: `// [SECTION: Name]` ... `// [END SECTION: Name]` in C#, `<!-- [SECTION: Name] -->` in XAML.
- XAML: every `x:Name` and handler is referenced from code-behind. If you rewrite a whole XAML file or large block, keep them all. Prefer whole-block replacement over scattered line edits for big deletions.
- Order edits so the build is clean at the end of each pass: **add readers/storage first, repoint consumers, then remove old UI, then remove old flat properties.** Intermediate states may not compile; that is fine, but the end of each pass must build with zero errors.
- No legacy migration (themes.md decision 3). Old theme.json / settings.json keys for removed properties are simply ignored on load. The bundled `default.zip` will lose its custom splash / no-preview image until the user re-saves it. Mention that in the pass report.
- Everything styled here is stored **per resolution on the layer's entry** (decision 8), edits apply live on Enter or focus loss, invalid input reverts to the stored value (the existing pattern).
- Do not "wrap up" the session when a pass finishes. Report and wait.

---

## 1. Decisions (final, agreed with the user on 2026-10-21)

1. **Game Info text styling moves to the Advanced tab**, on the Game Info group, as four new tabs: **TITLE | PUBLISHER | INFO | NAV** (after POSITION). Tab labels and layout will be tweaked later; tab wrapping in the pane is acceptable for now. Short labels are only a convenience.
2. **Storage.** Generic `Text*` / `TextShadow*` fields (already on `LayerLayoutEntry`, added in the Search & Buttons pass) are reused:
   - Title text -> `GameTitle` entry
   - Publisher block text -> `PublisherBlock` entry
   - **Body text** -> `GameInfoBody` entry (`Text*`)
   - **Headers** -> `GameInfoBody` entry, in **new** `SectionHeader*` fields (the generic fields only fit one block per entry)
   - Game Info Nav arrows/dots -> `GameInfoNav` entry (`TextColorHex`, `TextFontSize`, `TextShadow*`)
   - **Image Previews Nav is separate** -> `ImagePreviewsNav` entry (same fields). Do not share values between the two navs.
3. **Every text block gets**: Font family (11-font list), **Bold checkbox**, Size, Color, Drop shadow toggle, Shadow color, Blur, Depth. Navs get Size, Color, Drop shadow (no font or bold; they are shapes).
4. **Title shadow**: remove the hard-coded `DropShadowEffect` in `GameInfoLayerControl.xaml`. The new Title shadow defaults to **ON** with the old values (black at 60% alpha = `#99000000`, blur 4, depth 2) so nothing changes visually. All other shadows default off.
5. **Credits/Specs** (the SPECS / CREDITS headers and bodies) stop having their own properties. They bind to the **Info Headers** and **Info Body** styles. They will visibly change (Georgia body, 16pt) and that is intended.
6. **Video BG color** is **not** removed: it moves to the Video Preview layer on a new **BACKGROUND** tab (the fill behind/around the video letterbox). Default stays `#FF000000`.
7. **Splash image/video** moves to the Splash Image layer as a new **IMAGE** tab between POSITION and BORDER.
8. **No Preview Available image** moves to the Video Preview layer as a new **FALLBACK IMAGE** tab after BORDER (and after BACKGROUND).
9. **Random spin fallback** (`RandomSpinFallback` in `MainWindow.xaml`) loses its bindings to `ThemeMediaColor` / `ThemePreviewBorderRadius`. Hard-code a black fill (`#FF000000`) and corner radius 0 for now. It gets real font/color settings later with the Random Game Display layer pass. Leave a short comment saying so.
10. Publisher block text alignment (right) and other layout properties stay hard-coded. No alignment, nudge or padding settings in this pass.

### Defaults table (authoritative; constants go in `LayerRegistry`)

| Block | Entry / fields | Font | Bold | Size | Color | Shadow |
|---|---|---|---|---|---|---|
| Title | `GameTitle` / `Text*` | Segoe UI | **true** | 32 | `#FFFFFFFF` | **on**, `#99000000`, blur 4, depth 2 |
| Publisher | `PublisherBlock` / `Text*` | Segoe UI | false | 13 | `#FFFFFFFF` | off (`#FF000000`, 3, 1) |
| Headers | `GameInfoBody` / `SectionHeader*` | Segoe UI | **true** | 20 | `#FFFFFFFF` | off |
| Body | `GameInfoBody` / `Text*` | **Georgia** | false | 16 | `#FFFFFFFF` | off |
| Game Info Nav | `GameInfoNav` / `Text*` | n/a | n/a | 9 | `#FFFFFFFF` | off |
| Previews Nav | `ImagePreviewsNav` / `Text*` | n/a | n/a | 9 | `#FFFFFFFF` | off |
| Video BG color | `VideoPreview` / `BgColorHex` | | | | `#FF000000` | |

Ranges: text size 6 to 72, nav size 4 to 40, shadow blur 0 to 30, depth 0 to 20 (same as everywhere else).
Bold unchecked = `FontWeights.Normal`, checked = `FontWeights.Bold` (plain, not the DemiBold mapping the search label uses).
Shadow built with the existing `MainViewModel.BuildShadowEffect(enabled, colorHex, blur, depth)` (alpha of the color becomes the effect Opacity; frozen, shared).

---

## 2. Shared wiring rules (apply to every new style field)

From themes.md section 7, restated because they are the usual silent-failure points:

- Every new field on `LayerLayoutEntry` needs: the property (with the `JsonIgnore(WhenWritingNull)` attribute like its neighbors), a line in `CopyStyleFrom`, defaults in `BuildDefaultLayout` / a `Fill...` helper called from **both** `BuildDefaultLayout` and `ResolveLayout`, and a mention in the matching `ApplyLayout` style key in `MainWindow.xaml.cs`, or it will not survive resolution switches / Reset Layer / live repaint.
- Forwarders in `MainViewModel` must be re-raised from a `Refresh...Bindings()` method **and** from `RefreshThemeBindings()`.
- The row/element templates bind `Theme*` forwarders (ConfigurationSettings has no change notification).

---

## 3. PASS 1: Game Info styling

### 3.1 `LayerRegistry.cs`

- Add to `LayerLayoutEntry` (all nullable, same attribute pattern, in a commented block): `SectionHeaderFontSize` (int?), `SectionHeaderColorHex` (string?), `SectionHeaderFontFamily` (string?), `SectionHeaderBold` (bool?), `SectionHeaderShadowEnabled` (bool?), `SectionHeaderShadowColorHex` (string?), `SectionHeaderShadowBlur` (int?), `SectionHeaderShadowDepth` (int?). Add all to `CopyStyleFrom`.
- Add the default constants from the table above (e.g. `DefaultTitleFontSize`, `DefaultTitleBold`, `DefaultTitleShadowColorHex`, `DefaultTitleShadowBlur`, `DefaultTitleShadowDepth`, `DefaultPublisherFontSize`, `DefaultInfoHeaderFontSize`, `DefaultInfoBodyFontFamily = "Georgia"`, `DefaultInfoBodyFontSize`, `DefaultGameInfoTextColorHex = "#FFFFFFFF"`, `DefaultNavSize = 9`, `MinNavSize`, `MaxNavSize`, ...).
- Add `private static void FillGameInfoStyle(LayerLayoutEntry entry)` modeled on `FillSearchButtonsStyle`: for `GameTitle`, `PublisherBlock`, `GameInfoBody` (both `Text*` and `SectionHeader*`), `GameInfoNav`, `ImagePreviewsNav`, set any missing value from the table. Use `??=` / `IsNullOrWhiteSpace` so saved values (including a saved `false` shadow on Title) are kept. Call it next to `FillSearchButtonsStyle` in `BuildDefaultLayout` and in `ResolveLayout`.

### 3.2 `MainViewModel.cs`

Add a section `[SECTION: Game Info Forwarders]` (model it on the Search & Buttons Forwarders section), reading through `GetLayerEntry(...)`:

- Keep these existing names but repoint them to the entries (they currently read `Configuration`):
  `ThemeGameNameHeaderFontSize` / `ThemeGameNameHeaderColor` (GameTitle), `ThemePubYearRatingFontSize` / `ThemePubYearRatingColor` (PublisherBlock), `ThemeInfoHeaderFontSize` / `ThemeInfoHeaderColor` (GameInfoBody, `SectionHeader*`), `ThemeInfoBodyFontSize` / `ThemeInfoBodyColor` (GameInfoBody, `Text*`).
- Add for each of those four blocks: `...FontFamily` (`FontFamily`, fallback `DefaultFontFamily`), `...FontWeight` (`FontWeight` from the Bold flag), `...Effect` (`Effect?` via `BuildShadowEffect`). Names follow the same stem, e.g. `ThemeGameNameHeaderFontFamily`, `ThemeGameNameHeaderFontWeight`, `ThemeGameNameHeaderEffect`.
- Navs (two separate sets; **rename**, since there are now two): `ThemeGameInfoNavColor`, `ThemeGameInfoNavSize` (double), `ThemeGameInfoNavEffect`, and `ThemePreviewsNavColor`, `ThemePreviewsNavSize`, `ThemePreviewsNavEffect`. Remove `ThemeNavIconsColor` / `ThemeNavIconsSize`.
- Remove `ThemeCreditsHeader*` / `ThemeCreditsBody*` forwarders (readers are repointed in 3.3 first).
- Add `public void RefreshGameInfoBindings()` raising every property above. In `RefreshThemeBindings()`, replace the individual `OnPropertyChanged` lines for the old/removed Game Info, Credits and NavIcons forwarders with a call to it.

### 3.3 Bindings (`GameInfoLayerControl.xaml`)

- **PartTitle**: bind `FontFamily`, `FontWeight` (remove the fixed `FontWeight="Bold"`), `FontSize`, `Foreground`, and `Effect="{Binding ThemeGameNameHeaderEffect}"`. Delete the hard-coded `<TextBlock.Effect><DropShadowEffect .../>`.
- **PartPublisher** (five TextBlocks): set `TextElement.FontFamily / FontSize / FontWeight / Foreground` once on the `StackPanel` (attached properties inherit into the TextBlocks; the two TextBlocks that have their own `Style` only set `Visibility`, so inheritance still works) and `Effect` on the panel. Remove the per-TextBlock `Foreground` / `FontSize` bindings. Keep `TextAlignment="Right"`.
- **Headers**: apply FontFamily, FontWeight, FontSize, Foreground, Effect from the `ThemeInfoHeader*` forwarders to: the four `Column2Header*` TextBlocks, the block-header TextBlock inside the `GameInfoBlocks` DataTemplate (use the existing `{Binding DataContext.X, RelativeSource={RelativeSource AncestorType=UserControl}}` form), and the SPECS / CREDITS headers (replacing `ThemeCreditsHeader*`).
- **Body**: remove the hard-coded `FontFamily="Georgia"` everywhere; apply FontFamily, FontWeight, FontSize, Foreground, Effect from `ThemeInfoBody*` to: the GameInfoBlocks body TextBlock (DataTemplate, RelativeSource form), the Trivia and Tips TextBlocks, and the Specs / Credits bodies (replacing `ThemeCreditsBody*`; keep `TextWrapping="Wrap"`).
- **Navs**: Game Info Nav (`Column2ArrowLeft/Right`, `Column2Dot0..3`) use `ThemeGameInfoNavColor` / `ThemeGameInfoNavSize`; put `Effect="{Binding ThemeGameInfoNavEffect}"` on the inner StackPanel. Previews Nav (`Row3ArrowLeft/Right`, `Row3Dot0..2`) use `ThemePreviewsNavColor` / `ThemePreviewsNavSize` / `ThemePreviewsNavEffect` (the existing `Row3NavContent` StackPanel).

### 3.4 `GameInfoLayerControl.xaml.cs`

- The dot fills are set in code. Split `ResolveThemeNavIconsBrush()` into two resolvers (one reading `ThemeGameInfoNavColor`, one `ThemePreviewsNavColor`). `UpdateColumn2Visibility()` uses the first, `UpdatePreviewVisibility()` the second.
- `DataContext_PropertyChanged`: replace the `"ThemeNavIconsColor"` branch with two: `"ThemeGameInfoNavColor"` repaints `UpdateColumn2Visibility()`, `"ThemePreviewsNavColor"` repaints `UpdatePreviewVisibility()`.

### 3.5 `MainWindow.xaml.cs` (ApplyLayout)

Add `_lastGameInfoStyleKey`, a key string over every style field of the five entries (GameTitle, PublisherBlock, GameInfoBody incl. `SectionHeader*`, GameInfoNav, ImagePreviewsNav). On change call `_viewModel.RefreshGameInfoBindings()`. Follow the pattern of `_lastSearchButtonsStyleKey`.

### 3.6 Advanced tab UI (`AdvancedThemesTabControl.xaml` / `.xaml.cs`)

- New TabItems (all `Visibility="Collapsed"`), placed after the Search & Buttons tabs and before the shape/marquee tabs: `TabGITitle` ("TITLE"), `TabGIPublisher` ("PUBLISHER"), `TabGIInfo` ("INFO"), `TabGINav` ("NAV"). Add to `AllLayerTabs`; in `GetLayerTabs` return them for `LayerRegistry.GameInfoGroup`. Position stays first.
- Layouts use the exact grid and styles of the existing Search tab (`110/8/52/6/52` columns, `GLLabelStyle`, `GLTextBoxStyle`, `GLCheckBoxStyle`, `ThemedComboBoxStyle`, `PickerPopupGridStyle`, the color-picker + hex pattern, shadow Color/Blur/Depth enabled through `IsEnabled` bound to the toggle):
  - **TITLE**: heading **"Game Name Header"**, then Font + Bold, Size | Color (side by side, as on the Basic tab), Drop shadow toggle, Shadow color / Blur / Depth.
  - **PUBLISHER**: heading **"Publisher, Year, Rating"**, same block.
  - **INFO**: heading **"Headers"** with the same block (writes `SectionHeader*`), then heading **"Body Text"** with the same block (writes `Text*`).
  - **NAV**: heading **"Game Info Nav"**: Size | Color, Drop shadow, Shadow color / Blur / Depth; then heading **"Image Previews Nav"**: the same.
- **Handlers: do not write one handler per control.** Name the controls `GI{Block}{Control}` (blocks: `Title`, `Publisher`, `InfoHeader`, `InfoBody`, `Nav`, `PreviewsNav`), set each control's `Tag` to its block key, and use **one shared handler set** that resolves the target through a small accessor table (block key -> entry + getters/setters for family, bold, size, color, shadow enabled/color/blur/depth + their defaults). Reuse the existing `ShowColor`, `ApplyPickerColor`, `ApplyHexColor` helpers (extend them if they need a tag-aware overload) and the Enter/LostFocus number-field pattern (range check per field, invalid reverts, then `LoadSelectedLayerIntoFields()`).
- Add `LoadGameInfoStyleFields()` (called from `LoadSelectedLayerIntoFields` next to `LoadSearchButtonsStyleFields`, only when the Game Info group is selected). Fill the font combos from `LayerRegistry.AvailableFonts` once, like the other tabs.
- Group **Reset Layer** already copies style via `CopyStyleFrom` for every part; verify the new fields are covered (they will be if added to `CopyStyleFrom`).

### 3.7 Remove the migrated Basic tab controls

In `ThemesTabControl.xaml` / `.xaml.cs`, remove the controls and everything that reads/writes them (suppress-sync bools, `...ColorPicker_ColorChanged` / `...Hex_LostFocus` handlers, `LoadCurrentThemeValuesIntoUi` lines, the `themeDto` lines in `BtnSaveTheme_Click`, `SyncUiEntriesToSettingsLayer` lines) for:
Game Name Header (size + color), Publisher/Year/Rating (size + color), Info Header, Info Body, Credits/Specs Header, Credits/Specs Body, Nav Icons Color, Nav Icons Size.
Leave the rest of the PREVIEW PANEL section for passes 2 and 3.

### 3.8 Remove the flat properties

From `ConfigurationSettings.cs` (property + `ApplyThemeSettings` line), `ThemeSettings.cs`, and the `LoadTheme` mapping in `MainViewModel.cs`:
`GameNameHeaderFontSize/ColorHex`, `PubYearRatingFontSize/ColorHex`, `InfoHeaderFontSize/ColorHex`, `InfoBodyFontSize/ColorHex`, `CreditsHeaderFontSize/ColorHex`, `CreditsBodyFontSize/ColorHex`, `NavIconsColorHex`, `NavIconsSize`.

### 3.9 Pass 1 test checklist (for the user; list it in your report)

1. Clean build, zero errors.
2. Defaults: with no saved Game Info values the main window looks identical to before (Title keeps its shadow), except Specs/Credits (now Georgia 16).
3. Each new tab: every control changes the main window live; invalid numbers revert; shadow fields grey out while the toggle is off.
4. Previews Nav and Game Info Nav color/size/shadow change independently.
5. Switch resolution (new size copies values), Reset Layer, Save Changes then reload the theme: all values persist.
6. Basic tab no longer shows the removed rows; Save Changes still works.

---

## 4. PASS 2: Splash image + Video Preview tabs

### 4.1 Splash Image: IMAGE tab (replaces `ThemeBootSplash`)

- Storage: the **`SplashImage` entry's `ImagePath`** (empty = the built-in splash, same fallback chain as today: `default_mediabg.mp4`, else `default_mediabg.png`). Theme save/load already bundles and rewrites every entry's `ImagePath`; no new bundling code.
- `MainViewModel.ResolveBootSplashPath()` reads `GetLayerEntry(LayerRegistry.SplashImage).ImagePath` instead of `Configuration.ThemeBootSplash` (keep the relative-to-`BaseDirectory` resolution).
- Add `RefreshBootSplash()` re-raising `ThemeBootSplashAsset`, `IsBootSplashVideo`, `ThemeBootSplashVideoPath`, `IsBootSplashAnimated`, `ThemeBootSplashAnimatedAsset`; use it from `RefreshThemeBindings()`. In `ApplyLayout` add `_lastSplashPath` (the Splash entry's `ImagePath`) that calls it. `MainWindow.ViewModel_PropertyChanged` already calls `RefreshBootSplashVideo()` when `IsBootSplashVideo` is raised and that method skips reloads for an unchanged path; confirm that switching mp4 -> png -> gif -> mp4 all show immediately.
- New TabItem `TabSplashImage` ("IMAGE"): read-only file box `TxtSplashFile`, **Browse** `BtnBrowseSplashFile` (filter: png, jpg, jpeg, gif, mp4; stores a path relative to the app folder when inside it; a cancelled dialog changes nothing), **Clear** `BtnClearSplashFile`, hint text "Leave empty to use the built-in splash." (copy the Marquee Logo tab). Place it in the XAML **before** `TabShapeBorder` so the visible order is POSITION, IMAGE, BORDER. Add to `AllLayerTabs`; `GetLayerTabs` for `SplashImage` becomes `{ TabSplashImage, TabShapeBorder }`.
- Do not set `HasFile` on the SplashImage definition (that flag is for the Position tab's file field).

### 4.2 Video Preview: BACKGROUND tab (replaces `VideoBgColorHex`)

- Storage: **`VideoPreview` entry's `BgColorHex`**, default `#FF000000` (add `DefaultVideoBgColorHex`; set in the VideoPreview branch of `BuildDefaultLayout`, fill in `ResolveLayout`'s existing SplashImage/VideoPreview block).
- `VideoPreviewLayerControl`: remove the `{Binding ThemeVideoBgColor...}` on `MediaSlot`; add `public void ApplyBackground(string colorHex)` (sets `MediaSlot.Background` via `MainViewModel.SafeConvertToBrush`). Call it from `ApplyLayout` right next to `VideoPreviewLayer.ApplyBorder(...)` (that runs on every ApplyLayout, so no style key is needed).
- New TabItem `TabVideoBackground` ("BACKGROUND"): label "Background color:", picker + hex (`VPBgColorPicker`, `TxtVPBgColorHex`) using the `ApplyPickerColor` / `ApplyHexColor` helpers against `GetEntry(LayerRegistry.VideoPreview)`. This tab and the next sit **after** `TabShapeBorder` in the XAML. (Do not confuse it with the shape layers' `TabShapeBackground`.)
- Remove `ThemeVideoBgColor` forwarder.

### 4.3 Video Preview: FALLBACK IMAGE tab (replaces `ThemeMissingPreview`)

- Storage: **`VideoPreview` entry's `ImagePath`** (empty = built-in `assets/no_preview.png`).
- `MainViewModel.ThemeMissingPreviewAsset` reads that entry. Add `RefreshMissingPreview()` (raises `ThemeMissingPreviewAsset`), call it from `RefreshThemeBindings()` and from a new ApplyLayout key `_lastVideoFallbackPath`. `ResolveOptionsFallbackImage()` keeps working unchanged (it returns `ThemeMissingPreviewAsset`).
- New TabItem `TabVideoFallback` ("FALLBACK IMAGE"): file box `TxtVPFallbackFile`, `BtnBrowseVPFallback` (images only: png, jpg, jpeg, bmp), `BtnClearVPFallback`, hint "Leave empty to use the built-in image." Visible order for Video Preview: POSITION, BORDER, BACKGROUND, FALLBACK IMAGE. `GetLayerTabs` for `VideoPreview` returns `{ TabShapeBorder, TabVideoBackground, TabVideoFallback }`.
- Reset Layer for these layers already restores `ImagePath` (non-custom layers) and `CopyStyleFrom` covers `BgColorHex`; verify.

### 4.4 Basic tab + flat removals for pass 2

Remove from the Basic tab (controls and every load/save/sync/browse line): Splash Image or Video row (`TxtThemeBootSplashPath`, `BtnBrowseThemeBootSplash`), No Preview Available Image row (`TxtThemeMissingPreviewPath`, `BtnBrowseThemeMissingPreview`), Video BG Color, Video Border Size, Video Border Color, Video Border Radius (`VideoBgColorPicker`, `TxtVideoBgColorHex`, `TxtVideoBorderSize`, `VideoBorderColorPicker`, `TxtVideoBorderColorHex`, `TxtVideoBorderRadius`).
Remove the flat properties (Configuration + `ApplyThemeSettings`, `ThemeSettings`, `LoadTheme` mapping): `ThemeBootSplash`, `ThemeMissingPreview`, `VideoBgColorHex`, `VideoBorderSize`, `VideoBorderColorHex`, `VideoBorderRadius`. Also remove their `RewriteAssetPath` lines in `LoadTheme` and their `BundleThemeAsset` lines in `BtnSaveTheme_Click` (update the "7 referenced asset files" comments). Remove the `ThemeVideoBorder*` forwarders if grep shows no consumers.

### 4.5 Pass 2 test checklist

1. Clean build.
2. Splash: browse a png, a gif, an mp4; each shows on the idle screen immediately; Clear returns to the built-in; per-resolution values differ when switching sizes.
3. Video Preview: BACKGROUND color shows in the letterbox bars live; FALLBACK IMAGE shows for a game with no media; Clear restores the built-in.
4. Save Changes then reload the theme: splash and fallback files come back (bundled into the zip).
5. Basic tab rows removed, Save Changes works.

---

## 5. PASS 3: Remaining Preview Panel cleanup

- Remove the last Preview Panel Basic tab controls: **Background Image** (`TxtMediaBgPath`, `BtnBrowseMediaBg`), **Background Color** (`MediaColorPicker`, `TxtMediaColorHex`), **Border Size / Color / Radius** (`TxtPreviewBorderSize`, `PreviewBorderColorPicker`, `TxtPreviewBorderColorHex`, `TxtPreviewBorderRadius`) and all their handlers and load/save/sync lines.
- Remove flat properties and DTO/LoadTheme/ApplyThemeSettings lines: `MediaWindowWallpaper`, `VideoBoxBg`, `PreviewBorderSize`, `PreviewBorderColorHex`, `PreviewBorderRadius`, plus the forwarders `ThemeMediaWallpaper`, `ThemeMediaColor`, `ThemePreviewBorder*` (and their lines in `RefreshThemeBindings`), and the `MediaWindowWallpaper` asset lines in `LoadTheme` (`RewriteAssetPath`) and `BtnSaveTheme_Click` (`BundleThemeAsset`). Check whether `LoadThemeImage` still has any caller; do not delete it if it does.
- `MainWindow.xaml`, `RandomSpinFallback`: replace `Background="{Binding ThemeMediaColor...}"` with `#FF000000` and `CornerRadius="{Binding ThemePreviewBorderRadius...}"` with `0`; add the short "revisit with the Random Game Display pass" comment.
- If `BtnBrowseFile_Click` has no remaining button, delete it and its dialog-filter code. If a button remains, trim it.
- Delete the now-empty **PREVIEW PANEL** section (heading, comment block, `[SECTION: Preview Panel]` markers) and **one** of the two adjacent separators around it. Keep the Games List Panel and Options Menu sections intact. Also remove the stale "Old [SECTION: ...] removed" comments only if they directly mention things you deleted.
- Remove the now-unused `_suppress...Sync` bools for every control removed in the three passes.
- Final Find in Files for every removed name; report any hit.

### Pass 3 test checklist

Clean build; Basic tab shows only Themes Manager, Games List Panel (Menu Selected pair), Options Menu, Global Scrollbars, Context Menus; the random-spin "Rolling the Dice!" box still shows (black, square corners); load/save a theme; load an old theme (removed keys ignored, no crash).

---

## 6. Documentation (end of each pass, and finalize after pass 3)

Update `themes.md` (it is meant to be updated at the end of each pass). At minimum:
- Section 3.5: add the removed flat properties (per pass).
- New section **3.12 Game Info layer** (storage table from section 1 above, `SectionHeader*` fields, defaults table, forwarder names, `_lastGameInfoStyleKey`, shared Tag-keyed handler design, tabs TITLE / PUBLISHER / INFO / NAV).
- Section 3.7 tab table: Game Info tabs; Splash Image: IMAGE, BORDER; Video Preview: BORDER, BACKGROUND, FALLBACK IMAGE.
- Section 3.8: Splash/Video Preview storage of `ImagePath` / `BgColorHex`.
- Section 4: add decisions 31 to 36 (the numbered decisions in section 1 of this file, condensed).
- Section 6: open question "tab labels/wrapping and layout tweaks for the Game Info tabs; Random Game Display fonts/colors (Rolling the Dice!, spin fallback)".
- Section 8 status table: Game Info "Styling built"; Splash Image / Video Preview rows; Last-verified date.
- Section 10 change log entry per pass.

---

## 7. Out of scope (do not do)

- Alignment, padding or vertical-nudge settings for Game Info text.
- Italics, multiple shadows, ratio lock on group parts, any WYSIWYG work.
- Random Game Display fonts/colors (later pass).
- Touching the Options Menu / global button system or the Menu Selected pair.
- Anything in `OptionsWindow.xaml` tab styling.

## 8. What to report back after each pass

1. Files changed (one line each).
2. Anything that did not match this file (names, usages found by grep) and how you handled it.
3. Anything you removed that this file did not list, and anything you intentionally left.
4. The test checklist for that pass.
