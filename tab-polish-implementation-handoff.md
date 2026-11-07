# HANDOFF: Theme Builder Tab Polish — Implementation (single pass, four blocks)

**Type:** gated implementation. UI layout and wording only.
**Order of work:** Step 1 focused re-check (read-only) → **STOP for the user's explicit "GO"** → Step 2 implementation in four ordered blocks → Step 3 one final clean rebuild and a report → **STOP**.
**Never commit or push.** Do **not** edit `themes.md` or `CLAUDE.md` in this pass (documentation is updated after the user accepts the visuals).

---

## 1. Goal and why

Apply the approved layout polish to about 25 Theme Builder tabs. The spec is long because the pixel-level layout is precise; the individual edits are simple. Everything lives in `Views/AdvancedThemesTabControl.xaml` (written **X:** below) plus a few lines in `Views/AdvancedThemesTabControl.xaml.cs` (**CS:**). Line numbers below come from the earlier audit and **may have drifted**; find things by `x:Name` and text.

**Source of truth for layouts:** `Theme_Builder_Tab_Polish_-_Claude_Chat_Planning_Specification.md` in the project folder root (read all of it; if it is missing, stop and ask; the file may be named with spaces instead of underscores). Its ASCII diagrams and "Requirements" lists are authoritative for each tab's layout. **This handoff overrides the spec wherever they differ** (section 2).

**Preserve everything functional:** every value, binding, limit, validation, handler, enable/disable relationship, persistence path, media filter and behavior. Do not add properties, controls or behavior. Do not touch tabs or controls the spec does not list.

## 2. Decisions that override the spec

1. **Labels:** the spec's diagram labels are illustrative. **Keep the live wording and casing** ("Size:", "Background:", "Label", "Search box", "Drop shadow", "Nudge (px):" and so on). Apply only these explicit renames:
   - Tabs: LOGO → SPLASH LOGO; BANNER → TEXT MARQUEE; RG BACKGROUND → CONTENT; PREVIEWS → ARTWORK PREVIEWS; Main Window Background IMAGE → IMAGE OR VIDEO. Follow the existing tab-header casing convention.
   - "Preview banner" → "Preview text marquee"; Text Marquee "Drop shadow (text)" → "Drop shadow".
   - Play Button: "Drop shadow (text)" and "Drop shadow (button)" → "Drop shadow" (the Text and Button headings now disambiguate).
   - "Page Navigation" → "Navigation Arrows and Dots"; Play Button "Hover" → "Hover States".
   - "Solid star (game is in Favorites)" → "Solid Star (Favorited)"; "Outline star (game is not in Favorites)" → "Outline Star (Not Favorited)"; Favorites Star "Size:" → "Star size:" (label to the left of the box).
   - Media headers and hints (Block 2) and the Marquee Shadow text (Block 1).
2. **Divider:** a themed `<Separator Background="{DynamicResource Global.Options.Separator}" …/>` (as used at `ThemeBuilderWindow.xaml:38`). Same spacing everywhere (about 10 px above and below; use one value consistently). No dividers except where the spec lists them: Search 1, Folders 1, Game Titles 1, Game Info tab 2, Play Button 3, Favorites Star 2.
3. **Hex widths:** keep the **80 px** hex boxes on all existing tabs. Do not move them to 104. The shared Border tab keeps its 104.
4. **Border field widths (every consumer, shared and local):** Size box 36 → **50**; Radius box 36 → **60**.
   - Shared Border tab: `TxtShapeBorderSize`, `TxtShapeCornerRadius` (about X:2054–2057). Leave the 134 / 22 / Auto / 22 / Auto column structure; do not touch `PnlSBHoverBorder`.
   - Local rows (Text Marquee `TxtBannerBorderSize` / `TxtBannerCornerRadius`, Play Button `TxtPlayButtonBorderSize` / `TxtPlayButtonCornerRadius`): re-grid to **110 / 22 / 50 / 22 / 60** (color group, gap, Size, gap, Radius) so they match the shared Border spacing.
5. **Random Game Display Content order** (the media file is shared by both states; only the background color is per state): file selector with ? → **State** (label **above** the combo) → Background color + alpha explanation. Do not change what State controls.
6. **Marquee Shadow combo:** change the display text "Logo only" to "Splash logo only". Display text only. Check `ShadowTargetChoices` (about CS:3913–3918) and every place that maps a label to the stored value ("Logo") or compares labels, and keep the stored value and all mapping working.
7. **Text Margin:** the box stays **disabled while Wrap Text is on** (existing rule, about CS:3636). The spec's "no enable/disable rule" line does not apply; preserve the existing one.
8. **Video Preview Background Color** gets a ? tooltip (Block 2).
9. **Game Info tab row:** extra wrapping is acceptable; keep tab names readable (do not abbreviate).
10. **Local-copy rule for shadows:** do not add implicit styles to `App.xaml`. If you want a reusable style for the shadow or header row, make it keyed and local to the UserControl resources; otherwise hand-write each group like the existing ones.

## 3. Reusable patterns (copy the existing implementations)

- **? help glyph + tooltip:** model it on the shape/custom Content tab (about X:1774–1791) and `LayersHelpGlyphStyle` (X:19–33): label Margin 0 and centered, glyph at Margin 8,0,0,0, a local `ToolTip` using the `ChromeTooltip*` brushes, a `NoWrap` TextBlock with Bold / Run / LineBreak, `InitialShowDelay` 300, `ShowDuration` 60000. Use explicit `LineBreak`s for the multi-line text. Bold the "Supported formats:" lead-in.
- **Header row with a checkbox** (Font + Bold; Alignment + Wrap Text): a horizontal StackPanel with `UseLayoutRounding="True"`, the label and checkbox both vertically centered, the checkbox at about Margin 12,0,0,0. The only existing example is code-built (about CS:1560–1580). `GLLabelStyle` carries a default Margin of 0,10,0,3, which must be overridden inside header rows. Do **not** use per-checkbox 1 px margin workarounds.
- **Compact Drop Shadow group** (15 groups): drop the separate checkbox row and the "Shadow color:" label.
  ```text
  [ ] Drop shadow       Blur:      Depth:
  [■] [#FF100B17]      [ 3 ]      [ 1 ]
  ```
  Row 1 holds the checkbox over the color swatch column (normal white checkbox text, not the orange label style), plus the Blur: and Depth: labels in their columns. Row 2 holds the swatch + hex, Blur box and Depth box. **Keep every `x:Name`, `Tag`, `Click`/handler, and the `IsEnabled="{Binding IsChecked, ElementName=…}"` bindings on the swatch + hex panel, Blur box and Depth box.** A wrong `ElementName` fails silently (controls stay always enabled). The "Shadow color:" labels have no x:Name and may be deleted.
- **Number box with its label to the right:** model on the shape opacity row (about X:1807–1812): box, then the label at Margin 8,0,0,0, centered.
- **Field sizes:** reuse existing ones (Size column 48 + 14 gap, 110 px color column = 26 swatch + 4 + 80 hex, Blur/Depth 52).

## 4. The name-keyed Game Info handlers (the one runtime-crash risk)

`GIControl` finds controls with `FindName($"GI{block}{control}")` (about CS:3976, block table `GIBlocks` about CS:3920). Lookups for `Size`, `ColorPicker`, `ColorHex`, `Shadow`, `ShadowColorPicker`, `ShadowColorHex`, `ShadowBlur`, `ShadowDepth` use `!`, so **a renamed or deleted control crashes at runtime**. `Font`, `Bold`, `Align`, `ShadowTarget`, `RightMargin` are optional, so a renamed one is **silently skipped**. A missing `Tag` makes the handler do nothing. The 9 GI shadow groups are `GITitleShadow`, `GIPublisherShadow`, `GINavShadow`, `GIInfoHeaderShadow`, `GIInfoBodyShadow`, `GIPreviewsShadow`, `GIPlayButtonShadow`, `GIFavStarShadow`, and `GIMarqueeShadow` (the last is an exception group; do not restyle it). **Only move these controls; never rename them.** Tab display order is the **XAML declaration order** (`ApplyLayerTabs` only toggles Visibility), so reordering tabs means moving the TabItem blocks.

## 5. Step 1 — Focused re-check (read-only), then STOP

Using Read/Grep/Glob only (no edits, no builds, no shell, no Python, no app launch):

1. Confirm the line references and names in this handoff still match the code. List any drift.
2. Confirm: tab-header casing convention; the live label text for each Vertical nudge box, the RG Text per-state max-size labels, and the Play Button / Search / Text Marquee fields you will move; `ShadowTargetChoices` and its label-to-value mapping; every code-behind reference to controls you will move, delete or re-grid.
3. Confirm nothing outside `AdvancedThemesTabControl.xaml` / `.xaml.cs` needs to change (do **not** touch `GlobalThemesTabControl.xaml`).
4. Post a **concise edit plan per block** (what you will move or change, in what order) and anything in this handoff that does not match the code. Then **STOP and wait for "GO"**.

## 6. Step 2 — Implementation (only after GO)

Edit in this order. Use the Edit tool. Preserve CRLF in `.cs` / `.xaml`, and keep the `<!-- [SECTION: ... ] -->` fencing and replace stale comments (for example the comments that say the two Scrollbars grids match, or that describe the old Random Game Display order). New methods, if any, get a one-line `// Does X` summary.

### Block 1 — Tab structure, shared Border, Marquee Shadow, Scrollbars
- Rename the tab headers listed in section 2.1 (display only).
- Reorder TabItems in XAML: **Marquee** = Position & Size → Splash Logo → Text Marquee → Shadow; **Video Preview** = Position & Size → Fallback Image → Border (the Background tab is removed in Block 2).
- Shared Border Size/Radius widths (section 2.4, shared part). Report every consumer (shapes 01 to 04, custom layers, Splash Image, Video Preview, Search & Buttons, Random Game Display states) and confirm no clipping or wrapping.
- **Marquee Shadow tab** (`TabMarqueeShadow`): remove the heading "Logo and marquee image shadow" and the Glow tip; keep the Drop Shadow checkbox at the top (not moved into the color group), "Apply shadow to:" combo below it with its label above; the hint becomes **"Marquees include the Text Marquee."** directly under the combo; Shadow color / Blur / Depth stay on one row beneath. The combo's "Logo only" display text becomes "Splash logo only" (section 2.6).
- **Scrollbars (Games List and Game Info tabs, both identical):** reduce the width of the left label column (grid column 0, currently 120) to what the longest label needs (about 80, "Dragging:"). Keep Width/Resting/Hover/Dragging labels aligned, the Width box and Thumb column starting at the same x, the Thumb/Track spacing, and all box/swatch/hex widths unchanged. Keep the disabled-state behavior. Replace the explanation text (keep its styling and wrapping):
  - Games List: "These settings control the Games List scrollbar. When Use global scrollbar is checked, these settings follow the Global tab and cannot be edited here."
  - Game Info: "These settings control the Game Info, Trivia, Tips & Tricks, and Specs & Credits scrollbars. When Use global scrollbar is checked, these settings follow the Global tab and cannot be edited here."

### Block 2 — Media-selector tabs (then a structural build checkpoint)
Each "Header: (?)" keeps the file path box, Browse and Clear together on one row directly under the header, with the existing filters and handlers untouched. Tooltips (Bold lead-in, one item per line):
- **Splash Logo** — header **"Logo image:"** + ?. Tooltip: "Supported formats: PNG, JPG." / "The logo appears only on the splash page, when no game is selected." / "The Splash Logo shares the Marquee layer's Position & Size settings." Keep the hint "Leave empty to show no logo."
- **Splash Image** — header **"Splash image or video:"** + ?. Tooltip: "Supported formats: PNG, JPG, GIF, MP4." / "Displayed when no game is selected, if a splash image or video is configured." Keep "Leave empty to show no splash."
- **Video Preview Fallback Image** — header **"Fallback image:"** + ?. Tooltip: "Supported formats: PNG, JPG, BMP." / "The fallback image is displayed when the selected game has no video preview." Hint becomes **"Leave empty to use the default image."** Beneath it, **move the Background Color control here** (`VPBgColorPicker` and `TxtVPBgColorHex`, keeping their names and handlers, `BgColorHex` and default untouched): "Background color:" label with a ? glyph, swatch + hex beneath. Tooltip text, exactly: `Sets the background color behind the video preview, including letterbox areas, and behind the fallback image.` Then **remove `TabVideoBackground`** (about X:2079–2099) **and** its entries in `AllLayerTabs` (about CS:1202) and `GetLayerTabs` (about CS:1236) in the same edit group. Do not change fallback logic or `ThemeMissingPreviewAsset`.
- **Random Game Display Content** (`TabRGBackground`) — order per section 2.5: header **"Image or Video:"** + ? (tooltip: "Supported formats: PNG, JPG, GIF, MP4." / "Drawn over the background color and beneath the message text."), file row, then **"State:"** with the label above the combo (it is currently to the left in `PnlRGBgState`), then Background color with its alpha explanation ("Lower the alpha to let the layers behind the card show through."). `CboRGBgState` / `CboRGBorderState`, `_rgStatePartId` and `CboRGState_SelectionChanged` stay as they are.
- **Main Window Background** (`TabMainBgImage`, header IMAGE OR VIDEO) — new order: header **"Image / video file:"** + ? (tooltip: "Supported formats: PNG, JPG, BMP, MP4."), file row, the Disable checkbox (wording unchanged: "Disable background image (use the color only)"), then "Background color:" with swatch + hex. **Remove** the old long heading and the text "Leave empty to use the background color only." Preserve the empty-path behavior.

**Expected transient errors:** none if the `TabVideoBackground` XAML removal and both table edits are done together. If you build here and see errors about `TabVideoBackground`, one of the three was missed.
**Build checkpoint after Block 2** (structural change): `dotnet build ArcadeStick.csproj`. Fix before continuing.

### Block 3 — Header/shadow/divider recipe tabs
Apply the header-row, compact-shadow and divider patterns (section 3). Do the hand-wired groups first (Folders, Game Titles), then the name-keyed Game Info ones (read section 4 again before starting those).
- **Folders** (`TabGLFolders`) and **Game Titles** (`TabGLGameTitles`): Font header row with Bold beside it, combo beneath → Size + Color → normal compact shadow → **divider** → Selected text + Selected BG row → selected compact shadow (independent) → Folders: Arrow color + "Nudge (px):" on the last row; Game Titles: Hover color alone. Reduce the old 20 px top margins as needed. Goal: stay scrollbar-free.
- **Title:** Font header row with Bold; Size + Color; compact shadow. Heading "Game Name Header" stays.
- **Publisher:** Font and Alignment combos on one row (move the Alignment combo out of the nested size grid, keep its name), Bold beside the Font header; Size + Color; compact shadow. Heading stays.
- **Game Info tab:** rename the heading to "Navigation Arrows and Dots": Size + Color, compact shadow → divider → **Headers**: Font header row with Bold, Size + Color, compact shadow → divider → **Body Text**: Font header row with Bold, Size + Color, compact shadow, then extra vertical space and **Gap to scrollbar**: the numeric box on the left and the label "Gap to scrollbar (px)" to its right, no divider. Three independent shadows.
- **Artwork Previews:** remove the heading "Image previews shadow" and the Glow tip; compact shadow; no divider.
- **Favorites Star:** "Star size:" label left of its box; remove the sentence "A glow is a shadow with Depth 0…"; divider; "Solid Star (Favorited)" with Color and compact shadow; divider; "Outline Star (Not Favorited)" with Color and compact shadow (independent values).

### Block 4 — Tight-layout tabs
- **Search** (`TabSBSearch`): headings stay "Label" and "Search box". Label: Font header row with Bold, then Font and Alignment combos on one row (labels above) → Size + Color + Background on one row → Vertical nudge on its own row (box left, the live label text to its right) → the existing **Drop shadow** group stays in its current layout (standalone toggle exception, no compact recipe) with a **slightly larger gap above it and no divider** → **one divider** → "Search box": Size + Color + Background on one row, same widths and gaps as the Label row.
- **Buttons** (`TabSBButtons`): one shared column grid so both rows align: Size (48) | 14 | Color (110) | gap | Background (110). Row 1 = Size, Color, Background. Row 2 = **Hover color** spanning the Size + Color columns (set the extra width **only** on `TxtSBButtonHoverColorHex`, about 142 px) and **Hover BG** in the Background column, so Background and Hover BG begin at the same x and the Color-to-Background gap matches the Hover color-to-Hover BG gap. No placeholder control, no divider, no shared width changes.
- **Text Marquee** (`TabMarqueeBanner`): "Preview text marquee" checkbox on top → Font header row with Bold → **Color then Max size** (swap) → Wrap Text + Text margin on one row, the box then the label "Text margin (units)", box disabled while Wrap is on (section 2.7) → compact shadow ("Drop shadow", directly above Background, no divider) → Background alone on its own row → Border color / Size / Radius re-gridded to 110 / 22 / 50 / 22 / 60 with its left edge aligned to the Background group. No divider between Background and Border. Keep the text shadow (`BannerShadow*`) separate from the Marquee shadow.
- **Play Button:** order Font header row with Bold → Size + Color → Button text (label above the box, wide enough for "Launch Game") and Vertical nudge (box, live label to its right) on one row → text compact shadow → divider → **Button** heading: Background on its own row, button compact shadow → divider → **Border**: Color / Size / Radius re-gridded 110 / 22 / 50 / 22 / 60 → divider → **Hover States**: Text color, Background, Border color on **one row**. Three 110 px groups with 22 px gaps do not fit (about 394 px against the 385 to 397 px pane, before any scrollbar). **Start with 12 px gaps and the 80 px hex**; do not squeeze fields below usable widths or drop a control; report the result for visual review. Text and button shadows stay independent.
- **Random Game Display Text** (`TabRGText`): keep Message as is → shared heading **"Max text size (px):"** with two rows, the existing per-state boxes on the left and the existing live labels to their right → Font header row with Bold and Alignment header with Wrap Text beside it, both combos on one row → Color on its own row → the existing Drop shadow group stays as it is (standalone toggle exception) with a slightly larger gap above it, no divider. Keep names and bindings; the tab already scrolls.

## 7. Constraints (do NOT)

- No Python. No launching the application (the user does all visual testing). No screen-capture or input scripts. No commits or pushes.
- No shell pipelines or shell file reading; use Read/Grep/Glob. Shell is for `dotnet build` and read-only `git diff --stat` / `git status` only. Stay inside the project folder.
- Do not rename any `x:Name`, `Tag` or handler; do not change binding paths, `IsEnabled` relationships, ranges, filters, defaults or stored values.
- Do not touch `GlobalThemesTabControl.xaml`, `App.xaml` (no implicit styles), other layers' tabs, the Position & Size controls, or `GIMarqueeShadow`'s behavior.
- Do not edit `themes.md` or `CLAUDE.md`.
- No unrelated fixes bundled in.

## 8. Step 3 — Verification and report

1. **One final clean rebuild:** `dotnet build ArcadeStick.csproj --no-incremental`. It must finish with **0 errors**; report any new warning (a full rebuild currently reports 0). Intermediate builds only after Block 2 (structural) or when troubleshooting.
2. **Static checks you can do without running the app:**
   - Grep that every `x:Name` and handler referenced from code-behind still exists (the Game Info `GI{Block}{Control}` names above all, `VPBgColorPicker` / `TxtVPBgColorHex`, the scrollbar field table in `BuildScrollbarFieldTable`, `CboRGBgState` / `CboRGBorderState`).
   - For every compact shadow group, confirm the `IsEnabled` `ElementName` bindings on the swatch/hex panel, Blur and Depth point at the moved checkbox.
   - `git diff --stat`: no whole-file changes (line endings intact).
3. **Report in this form:** files changed · anything that did not match the instructions · anything changed beyond them · the test checklist for the user, which must cover at least: each renamed tab and the tab order; all 15 compact shadow groups (toggle enables/disables its three dependents); Bold/Wrap header alignment; dividers; Border Size/Radius fit on every consumer and both local rows; ? tooltips and exact text; Browse/Clear on each media tab; Random Game Display State switching (color per state, file shared); Video Preview background color and the fallback image; Text Margin disabled while Wrap is on; Scrollbars label gap and the Use-global disable; Marquee Shadow target choices including "Splash logo only".
4. **Flag explicitly for the user's visual review:** the Play Button Hover States row, the Marquee tab row (it may wrap), the Game Info tab row (wrapping accepted), Folders / Game Titles staying scrollbar-free, header-checkbox alignment, and the Buttons hover alignment.
5. List the `themes.md` / `CLAUDE.md` places that will need updating (list only).
6. **STOP.** The user visually tests, and any tweaks come as small follow-up prompts.
