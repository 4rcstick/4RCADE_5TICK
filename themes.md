# 4RCADE 5TICK - Theme Builder Spec (themes.md)

Last verified against code: 2026-10-19 (beta.4 in progress)

**Status: working draft.** Every choice in this file is provisional and the user may change or restructure any of it as the builder is reworked. Treat decisions as the current plan, not as locked. **The Games List layer is finished (including the 2026-10-16 fine-tune pass) and is the template:** whatever worked there is the pattern for later layers (the next layer is the user's choice: they may do simpler layers first and save Game Info, the biggest, for last). The tab system and shared styles built in the fine-tune pass are the template for every later layer's property tabs (section 3.7).

**Purpose of this file:** a living spec for the Theme Builder rework, so a new session starts with an accurate picture instead of guessing. Update it at the end of each layer or cleanup pass.

**Trust order:** uploaded code and data files first, this file second. If they disagree, the code wins and this file gets corrected. Never assume a feature is unbuilt or built without checking the uploaded files. (An earlier session wrongly assumed the Advanced tab was unbuilt; it is built. This file once listed shape layers as "Not started" after they were built; they are built.)

---

## 1. Working rules for this project

1. After the user explains a change or bug, summarize in 1-2 sentences what will be done and which files need edits. The user then uploads the files requested.
2. Post zero code or syntax snippets until the user says they are ready. They say so when they agree with the suggested edits; otherwise they reply with questions or changes.
3. When ready: one code block per message. Wait for confirmation of placement (or questions) before the next block.
4. Code edits use "Replace X code, with Y code" in two separate code blocks.
   - **The user's stated preference (2026-10-18): a plain "replace X with Y"**, with the filename and any instructions written **above** the code block, never inside it. Text inside a block makes lines wrap and the edit hard to read. Always tag the block with a language (`xml`, `csharp`) so it keeps syntax colors and scrolls sideways instead of wrapping.
   - **Insertions are also "replace X with Y":** replace one unique existing line with that same line plus the new code. Do not give a separate "paste after this line" instruction.
   - **Anchors:** when X is not unique on its own, start it with the method signature (C#) or the nearest distinctive line or section comment (XAML), and say which lines are context only. Do not send a snippet that appears in more than one method without its signature. The closing bracket is a fine ending anchor.
   - **Whole functions:** when an edit replaces a whole function, the X block lists the entire old function. Nothing is implied or abbreviated.
   - **Never abbreviate a line to "..."** in a delete list; list the full text so the user can search for it.
5. If errors are expected during multi-file edits, say so in advance, name the window where they appear and where they clear.
6. Do not try to wrap up the session when the initial edits are complete. The user decides when to start a new session.
7. Planning conversations: let the user give their ideas first, then fit suggestions around them. Do not hand over a full plan before they have spoken.

### 1b. Lessons for writing edit instructions (learned on the Games List and cleanup passes; guidance, not rules)

- XAML has few helpful anchors. Give a distinctive search string (a label text, an x:Name, a handler name) and say which template or section the edit belongs to. Folder and game templates look almost identical, and two edits once landed in each other's template.
- For a large pure deletion in XAML, prefer a hand-delete with a start line and an end line; whitespace-only lines inside a block make find-and-replace miss.
- For deletions spread across a file (flat property removals), a list of single lines to search for works well. The user can eyeball red squiggles to confirm a clean build.
- **The user prefers a whole-file replacement for large or restructuring edits** (sent as a file download, or inline for a short file) over find-and-replace: no chance of deleting the wrong lines.
- Order a multi-edit change so build errors appear in one short window, and name that window up front. Remove readers before the properties they read (this pass removed the Basic tab, then the settings, then the view model, in an order that kept the build clean at the end).
- After a big restructure, Visual Studio can show stale generated-file errors (for example duplicate x:Name CS0229) that a clean and rebuild clears. Re-upload files after many edits so line numbers and text do not drift.
- Before sending an edit for a line, confirm the search text is unique in the latest uploaded file.
- Visual Studio can add a stray `using static System.Net.Mime.MediaTypeNames;` that makes `Application` ambiguous (CS0104); delete that line.
- A paste can truncate mid-element and give missing-closing-tag errors (XLS0305); a whole-file replacement avoids this.
- `d:DesignWidth` / `d:DesignHeight` on a UserControl are designer-only and do nothing at runtime. Size the real window in ThemeBuilderWindow.xaml.
- A Find in Files search for the removed names is faster than guessing which other files use them; the user can run it and upload hits.

---

## 2. Goal and end state

- Build the Advanced tier of the Theme Builder layer by layer, starting with the **Games List** layer, and carry what is learned from each layer into the next. More small passes is accepted in exchange for refinement.
- End state: the Advanced tab first **mirrors** every property of the Basic tab, then fully **replaces** it. The Basic tab shrinks to load / save / delete plus the theme browser beneath. **Global** properties (options window, buttons, context menus, scrollbars, and the Theme Builder's own chrome colors) get their own new tab.
- Goal: the most customizable launcher theming on the market, entirely through UI (no file editing), with a later WYSIWYG edit mode on the main window (planned for beta.5; no drag handles or on-canvas resizing in beta.4).
- Windows only, so Windows core fonts are available. Fonts are not bundled in themes.

---

## 3. How the builder works today (verified in code)

### 3.1 Layout model
- Each theme can hold several **resolutions**. Each resolution has a layout: a list of layer entries with Id, X, Y, Width, Height, Z (draw order, higher on top), Visible, Name, ImagePath. Some entries also carry styling fields (Games List: section 5; shape, custom, Splash and Video Preview layers: section 3.8; Search & Buttons: section 3.10; Main Window Background: section 3.11).
- **Every styling field is copied in one place:** `LayerLayoutEntry.CopyStyleFrom(source)` copies everything except Id, rect, Z, Visible, Name and ImagePath. CloneLayouts, the resolution-switch copy (preset path) and both Reset Layer branches call it, so a new style field only has to be added there (plus the registry defaults/fill-ins and the style key).
- Name is used by custom layers only. ImagePath is used by shape layers, custom layers, the Marquee logo, Random Game Display and the Main Window Background wallpaper.
- A layer's placement therefore belongs to one resolution. Some layers have separate entries for two states (Random Game Display: "when a game is selected" and "when on the splash page").
- Presets in the combo box: 1280x720, 1600x900, 1920x1080, 1024x768, 800x600. Custom sizes can be entered; a custom size is "pending" until kept (Keep button, Save Adjustments, or a theme save) and is discarded if you switch away first.
- A new resolution starts as a copy of the layout being edited (custom layers excluded, background resized to fit), including all styling values.
- The window sizes itself to the active resolution; the main window's LayoutCanvas is that size.

### 3.2 Layer registry (single source of truth)
Defined in LayerRegistry. Adding a layer = adding a definition. Defaults are authored at 1280x720 and scaled by width/height ratio for other sizes (starting points only; nothing scales at runtime).

Layers (registry order), with groups shown as one row in the Advanced list:
- MainBackground (not positionable, cannot hide; holds the main window wallpaper, see 3.11)
- **Shape Layer 01 to 04** (ShapeLayer01..04): background/border layers, see 3.8
- GamesList (cannot hide), MarqueePanel, SplashImage, VideoPreview
- Group **Game Info**: GameTitle, PublisherBlock, GameInfoNav, GameInfoBody, ImagePreviewsNav, ImagePreviewsBody
- Group **Search & Buttons**: SearchLabel, SearchBox, RandomButton, SortButton, OptionsButton, HamburgerButton
- Group **Random Game Display**: RandomGameDisplay, RandomGameDisplaySplash
- Footer
- Custom layers: Ids start with "Custom_", at most 10 per resolution, png/jpg/gif/APNG/mp4. They use the same box renderer and tabs as shape layers (3.8).

The old GamesListBg and MediaPanel layers are gone from the registry (their ID constants remain unused); Shape Layers 01 and 02 took over their default rects.

Group rules: parts of a group share one Z and appear as one row; the group edit page is built in code, one section per part. The "Hide this layer" checkbox is shown only for layers that can be hidden and are not box layers (shape and custom layers use a visibility dropdown instead).

### 3.3 Applying the layout to the main window
- MainWindow.ApplyLayout resolves the active layout and maps each Id to an element: sets Canvas Left/Top, Width, Height, Z, and Visibility. It runs at startup, on every ThemeBindingsRefreshed, and after each committed Advanced edit (via ThemeBuilderWindow forwarding LayoutChanged to ApplyLayoutNow). In the Advanced tab, ApplyField ends by calling LoadSelectedLayerIntoFields, whose RaiseGuides raises LayoutChanged.
- The Games List is a Grid host (GamesListHost) holding the TreeView. Shape layers, custom layers, Splash Image and Video Preview borders are applied by ApplyShapeLayers, ApplyCustomLayers, and ApplyBorder (3.8).
- **ApplyLayout repaints only when a style value changed.** It builds key strings from the entries and compares them with the last ones: `_lastGamesListStyleKey` (folder font/colors/shadow, selected-state shadow, arrow) triggers MainViewModel.RefreshFolderColorsLive; `_lastGameTitleStyleKey` (game title font/colors/shadow, selected-state shadow) triggers RefreshGameColorsLive; `_lastMarqueeLogoPath` (the Marquee entry's ImagePath) triggers MainViewModel.RefreshMarqueeLogo; `_lastSearchButtonsStyleKey` (every style field on the SearchLabel, SearchBox and RandomButton entries) triggers MainViewModel.RefreshSearchButtonBindings plus MainWindow.RefreshMainButtonsRestingColors; `_lastMainBackgroundKey` (the MainBackground entry's ImagePath and ImageDisabled) triggers MainViewModel.RefreshMainWallpaper plus RefreshMainBgVideo (the latter only once the window has loaded, 3.11). **Any new style field must be added to the matching key (or get its own) or it will not repaint live.**

### 3.4 Theme Builder window: edit and save flow
- Advanced tab edits change Configuration.Layouts **in memory only**, applied live. Values commit on Enter or on losing focus; invalid input reverts to the stored value. The user likes this behavior and wants it on every new property.
- **Save Adjustments** (window button): syncs the Basic tab into settings, writes settings.json, commits a pending custom size, moves the close-restore point, refreshes theme bindings.
- **Save Changes** (Basic tab): writes the theme zip (theme.json plus bundled assets). Layouts, ActiveWidth and ActiveHeight are saved in theme.json. Every layout entry's ImagePath (shape, custom, Marquee logo, Random Game Display, Main Window Background wallpaper) is bundled into the zip and rewritten to an archive-relative path; LoadTheme rewrites them back to extracted paths.
- **Closing the window without saving** restores Layouts and the active resolution to the last saved snapshot and reapplies the main window.
- **Reset Layer / Reset Layout** restore from SavedThemeLayouts (the theme as last loaded or saved) with registry defaults filling gaps. **Restore Defaults** ignores the theme and uses registry defaults. Reset Layer copies ImagePath back for every non-custom layer, so the Marquee logo and the Main Window Background wallpaper are covered.
- "Theme sizes" buttons list the sizes saved in the theme file. The Resolution combo lists presets plus any size with a layout.
- The main window's hamburger menu has Themes and Layout size submenus, and Theme Builder opens with Ctrl+T.
- Alignment guides: four 1px lines at the selected layer's edges, preferences stored in settings.json (ShowGuideLines, GuideLineColorHex).
- The Theme Builder window is **693 x 885** (tuned by the user so no tabs wrap and no scrollbar appears). The window's DataContext is ConfigurationSettings.

### 3.5 Styling system today
- Remaining flat styling is still on ConfigurationSettings, mirrored in the ThemeSettings DTO, set by LoadTheme, saved by the Basic tab, and exposed to XAML through MainViewModel Theme* forwarders (ConfigurationSettings has no change notification, so XAML must bind to the forwarders, and RefreshThemeBindings must raise each one).
- **Migrated off the flat system:** Games List styling (section 5), shape/custom layer fill, border and file (3.8), Splash Image and Video Preview borders (3.8), the Marquee logo (3.9), Search & Buttons styling (3.10), and the main window wallpaper (3.11). **Removed 2026-10-19 (wallpaper pass)** from ConfigurationSettings (and ApplyThemeSettings), ThemeSettings, LoadTheme (mapping and asset path rewrite) and the Basic tab (the Background Image row TxtMainBgPath / BtnBrowseMainBg and ChkDisableMainBgImage, with their load, save DTO, bundling, sync and browse-handler lines): MainWindowWallpaper, DisableMainBgImage. **Removed 2026-10-19 (background color pass)** from ConfigurationSettings (and ApplyThemeSettings), ThemeSettings, LoadTheme and the Basic tab: BackgroundColor (with MainColorPicker / TxtMainColorHex, their handlers and load, save DTO and sync lines). That emptied the Basic tab's MAIN WINDOW section, so the section, its heading and one separator are gone. The color now lives on the Main Window Background layer (3.11); ThemeMainColor keeps its name and reads the layer. **Removed 2026-10-19 (Search & Buttons pass)** from ConfigurationSettings (and ApplyThemeSettings), ThemeSettings, LoadTheme and the Basic tab (its Search Label, Search Box and Main Window Buttons blocks; the Main Window background part stays): SearchBoxFontSize, SearchBoxColorHex, SearchBoxBgColorHex, SearchLabelFontSize, SearchLabelColorHex, SearchLabelBgColorHex, MainWinBtnFontSize, MainWinBtnColorHex, MainWinBtnBgColorHex, MainWinBtnColorHoverHex, MainWinBtnBgColorHoverHex, MainWinBtnBorderSize, MainWinBtnBorderColorHex, MainWinBtnCornerRadius. The ThemeSearch*/ThemeMainWinBtn* forwarders keep their names and now read the entries. The Options Menu / global button system (BtnBgColorHex, OptionsBtn* and so on) is a separate system and was not touched. **Removed 2026-10-18** from ConfigurationSettings, ThemeSettings, LoadTheme, the Theme* forwarders and the Basic tab: FileListBg, MarqueeBoxBg, MarqueeBorderColorHex, MarqueeBorderWidthValue, MarqueeBorderRadius, GamesListWallpaper, MarqueeWindowWallpaper, GamesBorderSize, GamesBorderColorHex, GamesBorderCornerRadius, ThemeLogo (with ThemeGamesColor, ThemeMarqueeColor, ThemeMarqueeBorder*, ThemeGamesBorder*, ThemeGamesWallpaper, ThemeMarqueeWallpaper). Old theme and settings files that still contain these keys simply have them ignored.
- Row styling is **pushed** from the view model onto objects. RefreshFolderColorsLive writes FolderColor, FolderFontSize, FolderFontFamily, FolderFontWeight, FolderSelectedBgColor, FolderSelectedColor, ArrowColor, ArrowMargin, FolderTextEffect and FolderSelectedTextEffect onto every TreeCategoryNode. RefreshGameColorsLive writes FontSize, GameFontFamily, GameFontWeight, GameColor, GameHoverColor, GameSelectedBgColor, GameSelectedColor, GameTextEffect, GameSelectedTextEffect and IsMouseSupported onto every GameItem. This avoids ancestor-walk bindings, which caused severe freezes on large expansions.
- `GameItem.ArrowColor` was removed 2026-10-18 (unused). The arrow Path in the TreeViewItem template still binds `ArrowColor`; on game rows that binding finds nothing, which is harmless because the arrow is hidden there (same for ArrowMargin and FolderTextEffect).
- **Still flat and still used by the Games List:** FolderSelectedColorHex / FolderSelectedBgColorHex, which the Theme Builder window's own chrome borrows (selected list item and combo highlight). The Basic tab's Games List Panel section now contains only these two, labeled "Menu Selected Text" and "Menu Selected BG", until the Global tab exists. A future Global tab should give the chrome its own properties.
- BorderColorFramework still works at runtime (ThemeBorderColor) but has no input field on the Basic tab (flagged in ThemesTabControl comments; give it a home in the Global tab).

### 3.6 Games List rendering today
- Folder and game text: font family from the 11-font list, bold checkbox, per-element size and colors, all per resolution. Folders have no hover state; game titles have a hover color.
- Drop shadow: two optional shadows per element type, **one for the normal state and one for the selected state** (folders; game titles and the mouse dot). Each is built by MainViewModel.BuildShadowEffect from the entry's values and pushed as one frozen shared effect (normal: FolderTextEffect / GameTextEffect; selected: FolderSelectedTextEffect / GameSelectedTextEffect). The selected-row trigger in each row template binds the selected effect. The two sets are independent (no inheritance).
- **Arrow shadow (2026-10-18):** the expand/collapse arrow Path (`ExpandArrow`) binds `Effect="{Binding FolderTextEffect}"`, with no trigger. It always uses the folder's normal-state shadow, whether the row is selected or not, because the arrow has no selected state. The Folders tab normal shadow controls and the existing style key drive it; the selected-state shadow does not affect the arrow.
- Expand/collapse arrow: a ToggleButton 16x16 in a 20-wide column; a right-pointing triangle that becomes down-pointing when expanded; fill is the arrow color; the arrow's top margin is 2 plus the vertical nudge (folders only). Row margins are tuned around it (folder rows -3, game rows -12, child items indented 12).
- Game rows show a bullet glyph for mouse support; it follows the game title's font family, weight, size, color states and shadow.
- The context menu "Text Size" feature was removed.
- Favorites folder color: a right-click "Choose folder color" option, stored in its own one-line file (`FavoritesColorFile`, default `favorites_color.cfg`), exactly like Recently Played and Most Played. RGB only; falls back to gold `#FFFFCC00`. Per install, **not** stored in the theme or per resolution.
- Scrollbars come from an implicit ScrollViewer style (ArcadeDarkScrollViewerThemed in App.xaml), which is global.

### 3.7 Advanced tab page structure (the template for later layers)
- **Right pane of the layer list** is a Grid (PnlLayerFields): layer name heading (TxtLayerName), a TabControl (TabsLayer), and the layer-wide buttons **pinned below the tab frame** (Reset Layer for every built-in layer, the Main Window Background included; Remove Layer beside it for custom layers).
- **Tabs per layer.** Position is the first tab and is shown for every layer except non-positionable fill layers (`IsBackground`: the Main Window Background), where it would be empty. When a different layer is selected, the **first visible tab** is selected (Position, or the layer's first own tab when Position is hidden). Other tabs come from a table in code-behind: `GetLayerTabs(unitKey)` and `AllLayerTabs`. Current table: **Main Window Background: Image** (background color row, then the wallpaper file and Disable checkbox); Games List: Folders, Game Titles; Shape Layers 01 to 04 and custom layers: Background, Border; Splash Image and Video Preview: Border; **Marquee: Logo**; **Search & Buttons group (keyed on `LayerRegistry.SearchAndButtonsGroup`): Search, Buttons, Border**; every other layer: none yet. `ApplyLayerTabs` sets Visibility (Position included) and picks the first visible tab when the selected layer changes (tracked by `_tabsUnitKey`); the order is the TabItem order in the XAML. To give a layer more tabs, add TabItems to the XAML (Visibility="Collapsed") and list them in GetLayerTabs and AllLayerTabs. No layer is expected to need more than 2 or 3 tabs.
- **Shared styles in App.xaml**: `ThemedTabItemStyle` and `ThemedTabControlStyle`. The top-level Basic/Advanced tab control uses them too. OptionsWindow.xaml still has its own tab styling and is a candidate to move to the shared style.
- **Shared resources in AdvancedThemesTabControl.xaml (UserControl.Resources):** PickerPopupGridStyle, ThemedComboBoxStyle, GLLabelStyle, GLTextBoxStyle, GLCheckBoxStyle, GlyphToggleStyle. Styles bind through `DataContext.X` with `RelativeSource AncestorType=Window`, because the window's DataContext is ConfigurationSettings.
- **Position tab:** X and Y on one row; Width and Height on the next with the **aspect-ratio lock** toggle (BtnRatioLock) between them; then the group-parts panel, the file field (layers that show a file), and Hide this layer (or the visibility dropdown for box layers).
- **Group move:** on a group page, a "Move all parts together" checkbox (`ChkGroupMove`) sits above the parts. It is shown for groups whose `LayerGroupDefinition.AllowGroupMove` is true (Search & Buttons and Game Info; not Random Game Display). When checked, an X or Y edit on one part (ApplyPartField) moves every other part of the group by the same delta; Width and Height never propagate; the guides keep following the edited part. Session-only, like the ratio lock.
- **Ratio lock:** session-only UI state (not saved, not per layer). **Known limit:** single layers only; the Width/Height boxes inside grouped layers (built in code by BuildGroupParts) do not have it yet. Plan: add it when Game Info is reworked.
- **Folders and Game Titles tab layout** (identical grid, columns 110 / 8 / 52 / 6 / 52): Font combo with Bold; Size and Color; Drop shadow toggle; Shadow color, Blur, Depth (disabled while the toggle is off); gap; Selected text and Selected BG; selected-state Drop shadow; gap; then Arrow color and Nudge (px) on Folders, or Hover color on Game Titles.
- **Search and Buttons tab layout** (Search & Buttons group) uses the same grid. Search tab: "Label" heading; Font combo with Bold; Size and Color; Background and Alignment; Vertical nudge (px) (a 48-wide box styled like the Arrow nudge); Drop shadow toggle with color, Blur, Depth; then a "Search box" heading with Size and Color, and Background.

### 3.8 Shape layers, custom layers, and bordered layers (built)
- **Shape Layer 01 to 04** are flat background/border layers placed anywhere in the stack. Each is a Border (fill, border, corner radius) with a host Grid holding an optional image or video, stretched to fill with a rounded clip. Defaults: 01 and 02 are black with a 1px gray border (the old Games List BG and Media Panel); 03 sits under the Marquee, transparent with no border; 04 is a spare and defaults to the Never visibility mode.
- **Entry fields (per resolution, null on other layers):** VisibilityMode ("Always", "GameSelected", "NoGameSelected", "Never"), BgColorHex, BorderSize, BorderColorHex, CornerRadius, FileOpacity (0 to 100, image or video only), plus ImagePath. Filled from defaults when missing. Copied by CopyStyleFrom (3.1).
- **Tabs:** Background (file with Browse/Clear, file opacity, background color) and Border (color, size 0 to 50, radius 0 to 200). The Position tab shows a Visibility dropdown instead of the Hide checkbox. Visibility modes re-evaluate when a game is selected or deselected (UpdateShapeVisibility).
- **Custom layers** use the same renderer, entry fields and tabs. Add Layer prompts for a name and creates a 200x200 box at the current resolution only; Browse on the Background tab resizes the layer to the file's native size (position stays, ratio lock ignored). Remove Layer deletes it at this resolution only.
- **Splash Image and Video Preview** get the Border tab only. Their border (size default 0, color, radius) is pushed in by ApplyBorder on their layer controls; the content sits inside the border ring with a concentric clip.
- Videos on shape and custom layers are muted and loop, and pause while a game runs.

### 3.9 Marquee layer: logo and no-art banner (built 2026-10-18)
- **Logo:** stored per resolution in the Marquee entry's ImagePath, edited on the Marquee layer's **Logo** tab (Browse / Clear, images only; empty means the built-in `default_marquee.png`). MainViewModel.ThemeLogoAsset reads it through GetLayerEntry(MarqueePanel) (the shared resolve-with-defaults lookup); MarqueeLogoPath exposes the path. ApplyLayout compares it to `_lastMarqueeLogoPath` and calls RefreshMarqueeLogo, so Browse and Clear show immediately. The logo shows only while no game is selected.
- **No-art banner:** when the selected game has no marquee file (own or clone parent), MarqueeImage is null and `MarqueeMissing` is true, and a bordered title card (`NoMarqueeBanner`, a 400x100 card in a Viewbox, game title text) shows in the Marquee layer instead of the logo. It is hidden while a spin runs. Colors are the existing ThemeMainColor (fill) and ThemeHeaderColor (border and text); font size 30 bold is fixed. The card is a copy of the spin's no-marquee fallback (`RandomizerNoMarqueeFallback`).
- `MarqueeMissing` is set in ResolveMarqueeImage (true when no file was found, false when one was or nothing is selected), cleared in UpdateActiveMediaPreviews when nothing is selected, and cleared immediately in the SelectedGame setter when it goes null, so neither the old marquee nor the banner lingers over the returning logo. It is a separate flag from HasMarqueeImage because MarqueeImage is also null during the 600 ms preview debounce, which would flash the banner on games that do have art.
- A marquee file that exists but cannot be read leaves the flag false (nothing shows). Decide later whether that should show the banner.

### 3.10 Search & Buttons layer (built 2026-10-19)
- **Storage (per resolution):** label values on the **SearchLabel** entry, search box values on **SearchBox**, and one style shared by all four buttons (Random, Sort, Options, Hamburger) on **RandomButton**. The other three button entries carry no style.
- **Generic text fields on LayerLayoutEntry** (named so Game Info can reuse them; all nullable, written only when not null): TextFontSize, TextColorHex, TextFontFamily, TextBold, TextAlign ("Left"/"Center"/"Right"), HoverColorHex, HoverBgColorHex, HoverBorderColorHex, TextShadowEnabled/ColorHex/Blur/Depth, TextNudgeY (vertical text offset in px, -10 to 10; SearchLabel only so far). The existing BgColorHex, BorderSize, BorderColorHex and CornerRadius are reused (SearchLabel and SearchBox: bg only; RandomButton: bg, border and radius). Shape layer logic is unchanged.
- **Defaults** (LayerRegistry constants, set and filled by `FillSearchButtonsStyle` from both BuildDefaultLayout and ResolveLayout; saved values are kept): label 13 / `#FF00D4FF` / bg `#001C1C1E`, Segoe UI, not bold, Right, shadow off (`#FF000000`, blur 3, depth 1), nudge 0; box 13 / `#FFF2F2F7` / bg `#FF1C1C1E`; buttons 13 / `#FF00D4FF` / bg `#FF1C1C1E`, hover `#FF00D4FF` / hover bg `#FF2C2C2E`, border 1 / `#FF00D4FF` / radius 0, hover border `#FF00D4FF`.
- **Tabs:** Position (with group move, 3.7) | **Search** (Label: font + Bold, size + color, background + alignment, vertical nudge, drop shadow with color/blur/depth; then Search box: size + color, background) | **Buttons** (size + color, background, hover color + hover BG) | **Border** (the shared Border tab, mapped to the RandomButton entry by `GetSelectedBorderEntry`, plus a "Hover border color" row `PnlSBHoverBorder` shown only for this group). Font size 6 to 72; shadow blur 0 to 30, depth 0 to 20; vertical nudge: any whole number, clamped to -10..10 (unlike the Arrow nudge, which reverts out-of-range values), non-numbers revert; border and radius use the Border tab's ranges.
- **Label rendering:** two elements. The outer `Border x:Name="SearchLabel"` is the positioned layer (it is what ApplyLayout's map moves and sizes) and holds the background fill. The inner `TextBlock x:Name="SearchLabelText"` stretches to fill it and carries the text, color, font, weight, alignment, padding `4,6,4,6`, font size and shadow. Bold unchecked = DemiBold (the original look), checked = Bold. The shadow uses BuildShadowEffect (decision 12 rules) and, being on the inner TextBlock, falls on the text only (a filled label background no longer casts a box shadow). XAML binds ThemeSearchLabelFontFamily, ThemeSearchLabelFontWeight, ThemeSearchLabelTextAlignment, ThemeSearchLabelEffect and ThemeSearchLabelTextNudgeY.
- **Vertical nudge (TextNudgeY):** fixes text that looks off-center after a font change (each font has its own ascent/descent). It is a `TranslateTransform` (RenderTransform) on the inner TextBlock, so it never changes layout: the fill, size and position stay put, and 0 renders exactly like the old single TextBlock. Padding cannot be negative, which is why it is not added to the padding. The outer Border does not clip, so at the extremes the text can extend a few pixels past the box, like the drop shadow.
- **Search box:** keeps Segoe UI and DemiBold; text size, color and bg only. Its border borrows the buttons' **resting** border (size and color through the ThemeMainWinBtnBorder* bindings), never the hover border.
- **Buttons:** no bold, font family, shadow or pressed state (icons are Segoe MDL2 glyphs plus one color emoji). Each button is two layers (the 2026-10-19 corner fix): the outer Border (the named button, positioned by ApplyLayout) holds the **fill** out to the full rounded edge, and an inner Border **ring** holds the **border color** and size and contains the icon TextBlock, so no sliver of the background shows at the corners. Hover is one shared MouseEnter/MouseLeave pair in MainWindow (`MainButton_MouseEnter/Leave`, sender = the outer Border) that sets the outer Background, the ring's BorderBrush and the icon Foreground. **A new button style value must be applied to the correct layer** (fill on the outer Border; border and icon on the ring) in both `MainButton_MouseEnter` and `ApplyMainButtonRestingColors`. `RefreshMainButtonsRestingColors` sets the resting values on all four; it runs at startup, on ThemeBindingsRefreshed, from the style key in ApplyLayout, after the Options window closes, and when the hamburger menu closes. A locally set BorderBrush replaces the XAML binding, so the resting refresh always sets it explicitly.
- **View model:** one private `GetLayerEntry(id)` replaces GetGamesListEntry and GetMarqueeEntry. The Search & Buttons forwarders live in their own section with `RefreshSearchButtonBindings()`, which RefreshThemeBindings also calls.
- **Reset Layer** on the group restores rect, visibility and the style fields of every part (CopyStyleFrom).

### 3.11 Main Window Background layer: color and wallpaper (built 2026-10-19)
- **Storage (per resolution, decision 8):** the MainBackground entry. The wallpaper file is in its ImagePath (empty = the built-in `assets/default_background.png`); the Disable flag is the new nullable field **ImageDisabled** (default false, written only when not null, copied by CopyStyleFrom). The **background color** is the entry's existing generic **BgColorHex** (the same fill field shape layers use; default `#FF1C1C1E`, the old flat BackgroundColor default, constant `DefaultMainBackgroundColorHex`). BuildDefaultLayout's non-positionable branch sets both defaults (that branch `continue`s before the per-layer fills) and ResolveLayout fills them for saved entries, keeping saved values. CopyStyleFrom copies both, so a new resolution, Reset Layer, Reset Layout and Restore Defaults all cover them.
- **Behavior (unchanged from the old flat settings):** the color fills the main window; an image shows on top of it as the root Grid's ImageBrush; an `.mp4` plays on top through the `MainBgVideo` MediaElement (RefreshMainBgVideo: loads paused on its first frame at startup, loops, pauses while a game runs); Disable shows the solid color only (the ImageBrush binding goes null and the video unloads). Images are static. No file opacity or other properties.
- **The color is also the app's popup and dialog background.** Everything that used the old flat BackgroundColor / ThemeMainColor follows the layer color of the **active resolution**: the Theme Builder's color-picker popups (Basic and Advanced tabs), the Sorting window, the Manage Playlists window (only when OptionsBg is blank; OptionsBg still wins when set), the main window's folder name and folder color popups (and that picker's popup), the no-art marquee banner and the randomizer's no-marquee card. Per-window control (for example a Global-tab dialog color) can be added later.
- **How each consumer refreshes:** main window bindings (root fill, popups, banner, spin card) bind `ThemeMainColor`, which reads GetLayerEntry(MainBackground).BgColorHex and is re-raised by `RefreshMainBackgroundColor()` and by RefreshThemeBindings. Windows whose DataContext is ConfigurationSettings (no change notification) use the app resource **`MainBackgroundBrush`** (`{DynamicResource MainBackgroundBrush}`: the Theme Builder picker popups through each picker's style and PickerPopupGridStyle, and the Sorting window's Background). App.xaml declares it with the default color; MainWindow.UpdateMainBackgroundBrush replaces it (frozen) whenever the color changes, and DynamicResource picks the replacement up live, including in windows that are already open. Manage Playlists sets the same resource reference in code (`SetResourceReference`).
- **Image tab** (`TabMainBgImage`, built from the Marquee Logo tab): a **Background color** row on top (`MainBgColorPicker` + `TxtMainBgColorHex`, same picker pattern and helpers as the Search & Buttons colors: picker applies live, invalid hex reverts), then the read-only file box `TxtMainBgFile`, Browse (`BtnBrowseMainBgFile`, images plus mp4: png/jpg/jpeg/bmp/mp4), Clear (`BtnClearMainBgFile`, back to the built-in image), and `ChkMainBgImageDisabled` "Disable Main Window Background Image (use solid Main Window Background Color only)". Browse stores the path relative to the app folder when inside it; a cancelled dialog changes nothing. The Position tab is hidden for this layer, so it opens on Image (3.7). Reset Layer is shown and restores ImagePath, ImageDisabled and the color. The tab label stays "Image" for now.
- **View model:** ThemeMainWallpaper, IsMainWallpaperVideo and ThemeMainWallpaperVideoPath keep their names and logic but read the entry through GetLayerEntry(MainBackground) (ResolveThemeFilePath still resolves the video path). `RefreshMainWallpaper()` re-raises the three; RefreshThemeBindings still raises them too.
- **Live apply:** ApplyLayout's `_lastMainBackgroundKey` (ImagePath + ImageDisabled) calls RefreshMainWallpaper and, once the window `IsLoaded`, RefreshMainBgVideo. Before load the key only re-raises the bindings, so the Loaded handler's first-frame startup sequence is unchanged. RefreshMainBgVideo skips a reload when the path is unchanged, so running after ThemeBindingsRefreshed as well is harmless. The color has its own check, `_lastMainBackgroundColor`, kept apart from the wallpaper key so a color change never touches the video: it calls RefreshMainBackgroundColor and UpdateMainBackgroundBrush (on the first ApplyLayout too, so the shared brush matches the active resolution from startup).
- **Theme save/load:** covered by the existing layout bundling (every entry's ImagePath is bundled on Save Changes and rewritten back by LoadTheme); there is no separate wallpaper bundling any more.
- **No legacy migration (decision 3):** themes and settings saved with the old flat MainWindowWallpaper / DisableMainBgImage keys lose their wallpaper (the keys are ignored) and show the built-in image until it is set again. Likewise the old flat BackgroundColor key is ignored: old themes and settings show the default `#FF1C1C1E` until the color is set again on the Image tab. Because the color and wallpaper are per resolution, switching layout size can now change them (and with them the popup and dialog background).
- Untouched: MediaWindowWallpaper, ThemeBootSplash, ThemeMissingPreview.

---

## 4. Design decisions (settled)

1. **Layer by layer**, Games List first (it carried font family, shadows and other styling), then reuse what works for the next layers.
2. **Backgrounds and borders are flat "shape layers"** placed under any layer. Content layers get only X, Y, Width, Height, Z-order and visibility. **No margins** on content layers. Anything inside a row's geometry stays hard-coded for now. (Built; the Basic tab's Marquee and Games List panel background, border and corner-radius fields were removed 2026-10-18.)
3. **No legacy-theme migration.** Nobody is using older themes; the schema may break freely, and the user's own themes can be deleted and rebuilt. Older saved entries are filled from registry defaults when read; a theme saved with a flat logo or panel colors simply falls back to the built-in logo and defaults.
4. **Live apply on Enter or focus change** for every Advanced property, the same as placement works now.
5. **Fonts:** a dropdown of 11 Windows core fonts, no bundled fonts. List: Segoe UI, Arial, Bahnschrift, Georgia, Times New Roman, Palatino Linotype, Impact, Arial Black, Segoe UI Black, Consolas, Courier New. Segoe UI is the fallback. Impact, Arial Black and Segoe UI Black are already heavy, so Bold makes little difference on them.
6. Layer **grouping or linking** (a panel moving with its backdrop) is a later QoL item, after all layer properties exist.
7. The Basic tab keeps working during the transition; the Advanced tab mirrors it, then replaces it.
8. **Every styling property is stored per resolution, on the layer's entry.** Reason: layouts are already per resolution, and the entry inherits live apply, save to settings.json and the zip, bundling, and revert-on-close for free. A new resolution starts as a copy of the one being edited. **The user may relax this for other layers:** pixel-based values likely need per-resolution values; colors may not. Exception: the Favorites folder color is not stored in the theme at all (decision 13).
9. **Each Basic tab control is removed as its property migrates** to the Advanced tab, in the same pass. Two places cannot own one value. This touches the Basic tab controls and code in ThemesTabControl (including load and save lines), plus the flat properties and forwarders in ConfigurationSettings, ThemeSettings and MainViewModel.
10. ~~Effects are off on the selected row.~~ **Superseded 2026-10-16** by decision 16.
11. Code files are uploaded per session (they change often). This file is updated only when a layer or pass is finished. A git checkpoint is made after each session.
12. **Effect set = Drop Shadow only.** Each shadow is: an on/off checkbox (default off), color (ARGB picker), blur and depth. The color's alpha sets the strength: BuildShadowEffect turns the alpha into the effect's Opacity and gives the effect an opaque color. No Glow type: a glow is a shadow with depth 0 and a bright color (to be explained in the docs). Direction fixed at 315. Typed ranges: blur 0 to 30, depth 0 to 20. Starting values when turned on: black `#FF000000`, blur 3, depth 1.
13. **Favorites folder color is a context-menu color, per install.** See 3.6.
14. **Games List stays locked visible through beta.4.** The Hide checkbox is not shown for layers that cannot be hidden.
15. **Skipped for the Games List:** italics, an arrow size multiplier, arrow nudge for game rows, and a context menu Text Size.
16. **Selected-state drop shadow.** Folders and game titles each get a second, independent shadow for the selected state (no inheritance).
17. **Advanced page uses tabs**, not an accordion or one flat scroll. Tabs reuse the existing themed tab properties, Position is the default, and tab sets are table-driven per layer.
18. **Shared tab styles live in App.xaml.** Repeated control markup is shared through the UserControl resources (3.7).
19. **Reset Layer is pinned below the tab frame.**
20. **Aspect-ratio lock** on the Position tab: glyph toggle, session-only. Single layers now; grouped layers later.
21. **Per-layer tabs:** a Border tab is shared by shape layers, custom layers, Splash Image and Video Preview (built). Multiple shadows per element are deferred.
22. **The arrow always uses the folder's normal drop shadow (2026-10-18).** The arrow has no selected state, so it never switches effects.
23. **The marquee logo is the idle image only (2026-10-18).** It shows when no game is selected. A game with no marquee file gets the generated title banner, not the logo. The banner keeps the default theme color bindings for now.
24. **Banner customization is planned, not built.** Likely a Banner tab on the Marquee layer, stored per resolution on the entry (fill, border, text color, font, size). Bindings will be swapped when that is built. A single shared card template for the banner and the spin fallback was deferred: its bindings would need rewriting to reach the window's DataContext, so the two cards are currently duplicated markup. Merge them when the customization pass happens.
25. **The four main window buttons share one style (2026-10-19)**, stored on the Random Game Button entry. Buttons have no bold, font family, shadow or pressed state. Hover border color defaults to the resting border color, so nothing changes visually until it is set.
26. **Generic text field names on entries (2026-10-19):** Text*, Hover* and TextShadow* fields are layer-neutral so Game Info can reuse them instead of adding per-layer copies.
27. **The search box borrows the buttons' resting border** (size and color) and keeps Segoe UI; it has no border of its own and never shows the hover border.
28. **Search label Bold:** unchecked keeps the original DemiBold; checked is Bold. Label padding changed from `0,6,4,6` to `4,6,4,6`.
29. **Group move is session-only** and offered per group through `AllowGroupMove` (Search & Buttons, Game Info; not Random Game Display). Only X/Y propagate.
30. **One copy helper for entry styling:** `LayerLayoutEntry.CopyStyleFrom` replaced the hand-written copy lists (CloneLayouts, resolution-switch copy, Reset Layer).

---

## 5. Games List layer - built property set (finished, tested)

All values are stored per resolution on the Games List layer entry (decision 8). In the Advanced tab they are split across tabs: **Position | Folders | Game Titles** (3.7). Defaults are the former compiled values.

**Placement (Position tab):** X, Y, Width, Height (with ratio lock), Z-order (layers list); visibility locked.

**Folders tab, normal state:** font family (default Segoe UI), Bold (default on), size (6 to 72, default 17), color (`#FF00D4FF`), drop shadow (default off, color, blur, depth). The normal shadow also applies to the expand/collapse arrows.

**Folders tab, selected state:** selected text (`#FF000000`), selected background (`#FF00D4FF`), drop shadow (default off; independent).

**Folders tab, arrow:** color (`#FFA0A0A2`), vertical nudge in pixels (-10 to 10, default 0; folders only).

**Game Titles tab, normal state:** font family, Bold (default on), size (6 to 72, default 15), color (`#FFA0A0A2`), drop shadow (also applies to the mouse support dot).

**Game Titles tab, selected state:** selected text (`#FF000000`), selected background (`#FF00D4FF`), drop shadow (independent), and hover color (`#FFEF44C3`, color only, no shadow).

**Stored fields on LayerLayoutEntry (written only when not null):** FolderFontSize, FolderColorHex, FolderSelectedColorHex, FolderSelectedBgColorHex, FolderFontFamily, FolderBold, ArrowColorHex, ArrowNudgeY, GameFontSize, GameColorHex, GameSelectedColorHex, GameSelectedBgColorHex, GameHoverColorHex, GameFontFamily, GameBold, FolderShadowEnabled/ColorHex/Blur/Depth, GameShadowEnabled/ColorHex/Blur/Depth, FolderSelectedShadowEnabled/ColorHex/Blur/Depth, GameSelectedShadowEnabled/ColorHex/Blur/Depth.

**Stays hard-coded for now:** row spacing and indent, arrow-to-name horizontal gap, row dividers, selection highlight shape, folder hover, shadow direction.

---

## 6. Open questions

1. Game Info (and later layers): which settings per resolution, which theme-wide. A "copy these settings to the other sizes" or "apply this color to all resolutions" action may help if per-resolution colors prove tedious.
2. Game Info's tab set (a grouped layer with six parts: how to split its options across tabs, and how the ratio lock should work per part).
3. Whether the games list may ever override the global scrollbar style (assumed no).
4. Where the Theme Builder's own chrome colors (currently borrowing the flat FolderSelected pair), scrollbars, buttons, and BorderColorFramework live: the future Global tab.
5. Layer visibility modes for non-box layers beyond a checkbox (shape and custom layers already have the four modes).
6. The mouse support dot: whether it needs a setting of its own.
7. Whether the ratio lock should reset when another layer is selected.
8. Banner customization: which properties, and whether a marquee file that cannot be read should show the banner.
9. Bold variations for the buttons and for Game Info text (the buttons have none today; the label's Bold maps DemiBold/Bold rather than Normal/Bold).
10. A padding property for text layers (the search label's padding is fixed at `4,6,4,6`).
11. Per-part ratio lock on group pages (still open; group move does not cover it).

Resolved: per-resolution storage on the entry; effect set shape; font list; arrow size multiplier (not needed); live-refresh path (style keys, 3.3); context menu Text Size (removed); tabs vs accordion (tabs); selected-state effect; shape layers (built, 3.8); marquee logo location (Marquee entry, 3.9); arrow shadow (normal shadow, always); Search & Buttons storage and tabs (3.10); grouped layers moving together (group move, 3.7).

---

## 6b. Parked ideas (not committed, may change)

- Move save / load / delete and the theme chooser into the Advanced tab, renamed Themes, and make the old Basic tab only the theme browser. Note: the window footer (Save Adjustments, Close Panel) is already visible on every tab and could host a theme save.
- Autosave every committed edit to settings.json so an accidental close loses nothing (would remove revert-on-close and make Save Adjustments redundant). Things to settle: pending custom sizes, Basic tab edits, a prompt when switching themes over unsaved work, a "modified" marker.
- Smaller alternative: a "Discard your changes?" prompt when closing with unsaved edits.
- Multiple drop shadows per element (shape layers especially).
- A "copy these settings to the other sizes" action.
- Layer grouping or linking so a panel and its backdrop move together.
- Italics (skipped for the Games List): possibly as the mouse dot's own toggle.
- Move OptionsWindow.xaml's tabs to the shared App.xaml tab styles.
- Docs note: a glow is a drop shadow with depth 0 and a bright color; on a dark theme a black shadow is invisible, so pick a light color.
- Ratio lock on grouped-layer parts; optionally reset the lock on layer change.
- Merge the banner and spin-fallback cards into one shared template (with 24).
- Drag-to-resize and the WYSIWYG edit mode (beta.5).

---

## 7. Traps and gotchas

- ConfigurationSettings has no change notification; XAML binds to MainViewModel Theme* forwarders, and RefreshThemeBindings must raise every one.
- A new flat theme property must be wired into all of these or it silently reverts: ThemeSettings DTO, ConfigurationSettings, ApplyThemeSettings (also the settings.json load in the MainViewModel constructor), LoadTheme mapping, the DTO builder in BtnSaveTheme_Click, LoadCurrentThemeValuesIntoUi, SyncUiEntriesToSettingsLayer. Storing values on layer entries avoids most of these but adds the entry-field traps below.
- Styling values go on LayerLayoutEntry. A new field must be added to `CopyStyleFrom` (which CloneLayouts, the resolution-switch copy and Reset Layer all use), to the registry's default building and ResolveLayout fill-ins, and to the matching style key in ApplyLayout (3.3), or it will not survive copies or repaint live.
- **Main window buttons:** their Background, BorderBrush and icon Foreground are set from code (hover and resting), which replaces the XAML BorderBrush binding. Each button is two layers: the outer Border holds the fill, the inner Border ring holds the border color and the icon (3.10). Any new button style value must be applied to the correct layer in both `MainButton_MouseEnter` and `ApplyMainButtonRestingColors`. Do not confuse the MainWinBtn forwarders with the Options Menu button system (BtnBgColorHex, OptionsBtn*).
- Do not use ancestor-walk (RelativeSource AncestorType) bindings in row templates for values; push them from MainViewModel onto TreeCategoryNode and GameItem. The only ancestor lookups left in the row templates are the IsSelected and IsMouseOver triggers.
- The folder and game row templates each have their own "selected row" trigger; they look alike; keep them in their own template.
- The shadow effects are built once per refresh and frozen; every row shares the same object. Do not mutate them after building.
- A new color for a static (non-custom) folder follows the Recently/Most Played/Favorites pattern: a ...ColorFile property, ReadFolderColorFile, IsCustomColor = true, a Set...Color method, a branch in FolderColorPickerPopup_Closed, and canRecolor in FolderRowContextMenu_Opened.
- Closing the Theme Builder reverts layouts to the last saved snapshot. Anything stored outside layouts is not covered unless handled separately.
- LoadTheme deletes and re-extracts the theme's Extracted folder; ThemeExtractionStarting releases open files first.
- Saved files still contain a stale VideoConvertPrompt layer from the removed video converter. It is ignored on load and drops out on the next save.
- Games List cannot be hidden (the registry forces it visible).
- Visual Studio can keep stale generated XAML files after large edits; clean and rebuild.
- Do not reopen: the single-MediaElement background video loop (a two-player swap was tried and rejected).
- **Advanced tab:** every control x:Name and handler is referenced from code-behind; a whole-file XAML replacement must keep them all. `LoadSelectedLayerIntoFields` starts with `ApplyLayerTabs(...)`, which only returns to the Position tab when the selected layer id changes. The shadow Color/Blur/Depth controls are enabled through `IsEnabled` bound to their toggle's `IsChecked`.
- **Marquee:** MarqueeImage is also null during the 600 ms preview debounce after a game is selected, so the banner must use MarqueeMissing, never HasMarqueeImage. Anything that sets MarqueeImage to null for "nothing selected" must also clear MarqueeMissing. Code reading the marquee must not assume MarqueeImage always has a value.
- The Splash Image, Video Preview and Game Info layer controls bind only Theme* properties that still exist; none used the removed panel properties (checked 2026-10-18).

---

## 8. Layer status

| Layer | Placement | Styling sections | Notes |
|---|---|---|---|
| Main Window Background | Not positionable | **Image tab built** | Background color, wallpaper (image or mp4) and Disable flag per resolution; the color is also the app's popup and dialog background (3.11) |
| Games List | Built | **Built and tested** | Template layer; see section 5. Tabs: Position, Folders, Game Titles. Locked visible through beta.4 |
| Shape Layers 01 to 04 | Built | **Built** | Background and Border tabs, visibility modes (3.8) |
| Custom layers | Built | **Built** | Same renderer and tabs as shape layers; up to 10 per resolution |
| Marquee | Built | Logo tab built | Logo per resolution; no-art banner built (3.9); banner customization not started |
| Splash Image | Built | Border tab built | |
| Video Preview | Built | Border tab built | |
| Game Info (6 parts) | Built | Not started | Biggest layer, expected to have the most options; the user may do it last; ratio lock for parts to add; group move already works; can reuse the generic Text* fields |
| Search & Buttons (6 parts) | Built | **Built** | Search, Buttons and Border tabs; shared button style on RandomButton; group move (3.10) |
| Random Game Display (2 states) | Built | File field only | Has a file field |
| Footer | Built | Not started | |
| Global tab | Not started | Not started | Options window, buttons, context menus, scrollbars, builder chrome colors, BorderColorFramework |

---

## 9. File map

| File | Role |
|---|---|
| LayerRegistry.cs | Layer definitions, groups (AllowGroupMove), defaults, font list, ResolveLayout, FillSearchButtonsStyle, shape and custom layer rules, LayerLayoutEntry.CopyStyleFrom |
| ConfigurationSettings.cs | Live settings, Layouts, snapshot, CloneLayouts, ApplyThemeSettings |
| ThemeSettings.cs | Theme file DTO (theme.json) |
| AdvancedThemesTabControl.xaml / .xaml.cs | Advanced tab: layer list, tabbed property pane (3.7), shared resources, ratio lock, resolutions, resets, guides, group move, shape/Marquee/Games List/Search & Buttons/Main Window Background tab handlers |
| ThemeBuilderWindow.xaml / .xaml.cs | Window hosting both tabs (693 x 885), save and close flow |
| ThemesTabControl.xaml / .xaml.cs | Basic tab: remaining flat styling controls, theme save/load/delete |
| MainWindow.xaml / .xaml.cs | Layout canvas, ApplyLayout and the style keys, shape/custom layer rendering, TreeView templates, marquee panel and banner, hamburger menu, main window button hover, wallpaper video (RefreshMainBgVideo) |
| MainViewModel.cs | Theme forwarders, GetLayerEntry, Search & Buttons forwarders and RefreshSearchButtonBindings, wallpaper forwarders and RefreshMainWallpaper, ThemeLogoAsset, MarqueeMissing, RefreshThemeBindings, LoadTheme, pushed row styling, BuildShadowEffect |
| TreeCategoryNode.cs / GameItem.cs | Per-row pushed styling properties |
| SplashLayerControl, VideoPreviewLayerControl, GameInfoLayerControl (.xaml/.cs) | Layer controls; the first two take their border from ApplyBorder |
| App.xaml | Global scrollbar and button styles; shared themed tab styles |
| settings.json / theme.json | Real examples of stored data |

---

## 10. Change log

- 2026-10-15: File created. Games List property set agreed. Settled: layer-by-layer, shape layers for backgrounds, live apply, no legacy migration, Windows core fonts only.
- 2026-10-15: Rule 4 expanded with anchor guidance. Added decisions 8-11, open questions, parked ideas.
- 2026-10-15: **Games List layer finished.** Font, arrow and per-state drop shadow moved onto the per-resolution Games List entry with live repaint through ApplyLayout style keys. Flat Folder/Game/Arrow properties and their Basic tab controls removed.
- 2026-10-16: **Games List fine-tune pass.** Selected-state shadows, tabbed Advanced page with shared App.xaml tab styles, aspect-ratio lock, window tuned to 693 x 885. Added section 3.7 and decisions 16 to 21.
- 2026-10-16: **Games List layer completed.** Favorites folder color moved to the context menu (per-install .cfg file).
- 2026-10-18: **Marquee logo cleanup pass.** (1) Removed the Basic tab's Marquee Panel section and the Games List Panel image, color, border size, border color and corner radius fields, and all their flat properties, DTO fields, forwarders, LoadTheme mappings, sync and save lines (list in 3.5); shape layers replace them. (2) The logo moved to the Marquee entry's ImagePath with a new Logo tab, live refresh through `_lastMarqueeLogoPath`; flat ThemeLogo removed. (3) New no-art banner: a game with no marquee file shows a bordered title card (MarqueeMissing) instead of the logo; the logo is the idle image only; stale marquee or banner is cleared the moment a folder is selected. (4) Arrow now has the folder's normal drop shadow. (5) Cleanup: unused GameItem.ArrowColor removed, doubled GameSelectedShadowDepth assignment in CloneLayouts fixed. Added sections 3.8 and 3.9 (shape/custom layers and the marquee, which were built but undocumented), decisions 22 to 24, the Layer status table update, and the code-block formatting preferences in section 1.
- 2026-10-19: **Search & Buttons layer built.** (1) New generic entry fields (Text*, Hover*, TextShadow*) with registry defaults; label on SearchLabel, box on SearchBox, one shared button style on RandomButton (3.10). (2) Advanced tab: Search, Buttons and Border tabs for the group, hover border color row, and session-only group move for Search & Buttons and Game Info (3.7). (3) Main window: label font family, weight, alignment and shadow; padding `4,6,4,6`; one shared hover handler pair with hover border; `_lastSearchButtonsStyleKey` live repaint. (4) `GetLayerEntry(id)` replaced GetGamesListEntry/GetMarqueeEntry; `CopyStyleFrom` replaced the hand-written entry copy lists; Reset Layer on a group now restores styles. (5) Removed the 14 flat SearchBox*/SearchLabel*/MainWinBtn* properties and their Basic tab controls (list in 3.5). (6) Fixed the invalid `OptionsBg` default (`#FF00000` to `#FF000000`). (7) Separate commit: randomizer leftovers removed (IsRandomizerResultShowing, the Ctrl+R debug hotkey). Added section 3.10 and decisions 25 to 30.
- 2026-10-19: **Search Label vertical nudge.** New generic entry field `TextNudgeY` (-10 to 10, default 0; SearchLabel only, filled by FillSearchButtonsStyle, copied by CopyStyleFrom, in `_lastSearchButtonsStyleKey`), shown as "Vertical nudge (px)" on the Search tab (whole numbers clamp to the range, anything else reverts). The label is now an outer Border (position, size, fill) around an inner TextBlock `SearchLabelText` moved by a TranslateTransform, so only the text moves and 0 matches the old look; a filled label background no longer casts the text shadow. Documented the two-layer main window buttons (fill on the outer Border, border and icon on the inner ring) in 3.10 and section 7.
- 2026-10-19: **Main window wallpaper moved to the Main Window Background layer.** (1) The wallpaper file is the MainBackground entry's ImagePath and the Disable flag is a new nullable entry field `ImageDisabled` (default false, set in BuildDefaultLayout's non-positionable branch, filled in ResolveLayout, copied by CopyStyleFrom), per resolution. (2) New Image tab on that layer (file, Browse with images plus mp4, Clear, Disable checkbox); the Position tab is hidden for non-positionable layers and the first visible tab is selected on layer change; Reset Layer now shows for this layer. (3) The wallpaper forwarders keep their names and read the entry; `_lastMainBackgroundKey` in ApplyLayout repaints live (RefreshMainWallpaper, plus RefreshMainBgVideo once loaded). (4) Removed the flat MainWindowWallpaper and DisableMainBgImage and their Basic tab row, checkbox, load/save/bundle/sync and browse lines (list in 3.5); Background Color stays flat. Theme save/load is covered by the existing layout ImagePath bundling. Added section 3.11.
- 2026-10-19: **Main window background color moved to the Main Window Background layer.** (1) The color is the MainBackground entry's BgColorHex (per resolution; default `#FF1C1C1E`, set in BuildDefaultLayout's non-positionable branch, filled in ResolveLayout, copied by CopyStyleFrom), edited in a new Background color row at the top of the Image tab. (2) ThemeMainColor keeps its name and reads the layer; a separate `_lastMainBackgroundColor` check in ApplyLayout re-raises it (RefreshMainBackgroundColor) and updates the new app resource `MainBackgroundBrush`, without touching the video. (3) The color is also the app's popup and dialog background: the Theme Builder picker popups and the Sorting window now use `{DynamicResource MainBackgroundBrush}`, Manage Playlists sets the same resource reference when OptionsBg is blank, and the main window popups, no-art banner and spin card keep binding ThemeMainColor. Per-window control can be added later. (4) Removed the flat BackgroundColor and the Basic tab Background Color row; the now-empty MAIN WINDOW section and its heading are gone. No legacy migration: old themes show the default color until it is set again.
