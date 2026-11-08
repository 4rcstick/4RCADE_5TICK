# Theme Builder: UI Geometry and Alignment Audit

**Date:** 2026-11-07. **Branch:** `advanced-themes` (after `213da18`, Layer Properties Polish Pass 5).
**Type:** audit only. No source file, themes.md or CLAUDE.md was changed.
**Method:** Phase 1 read every relevant XAML file and the code-built rows; Phase 2 captured the Theme Builder window only (PrintWindow of its own handle) and measured pixel edges, at 96 DPI (100% scaling), window 717 x 900, client 701 x 861.

**Notation.**
- Every x is measured from the left edge of the tab content (inside the tab frame's 1px border and the page's 10px margin). On screen that origin is client x **283** on the layer tabs and client x **43** on the Global subtabs.
- Ranges are written `left to right` as edges, so a 52px box at x 118 is `118 to 170`.
- **[X]** means calculated from the XAML (authoritative for what is declared). **[S]** means measured on a screenshot: an **estimate, about +/-1 px**. At 96 DPI one screenshot pixel is one DIP, so the screen numbers are close, but antialiasing and 1px borders make them estimates.
- "Confirmed" means XAML and screen agree, or the screen reproduced a prediction made from the XAML.

---

## 1. Executive summary

There are **two independent causes**, and the first one was not visible in the XAML alone.

1. **A layout-rounding quirk adds about 1px to every column of some grids (the biggest single cause; confirmed on screen).** A grid that has `UseLayoutRounding="True"` (set on itself or inherited from a parent grid), has **no `*` column**, and is arranged **wider than it needs** renders every pixel column about 1px wider than declared. So its controls drift right by 1px per column crossed:
   - Size column 48 becomes 49, so Color starts at 64 instead of 62.
   - Shadow rows put Blur at 120 instead of 118 and Depth at 180 instead of 176, each 53 wide.
   - The Text Marquee border row puts Size at 120 and Radius at 179.

   Grids with a `*` column, or without layout rounding, render exactly as declared. This explains your case 2: Title and Publisher are declared identically, but Title's grid has no `*` column and Publisher's does, so **Title's Blur / Depth sit 2 to 4px right of Publisher's** on screen. It also explains part of case 1, and why some rows "look" aligned on one tab and not on another. The behavior fits WPF's rounding correction in `Grid` arrange: the extra width gets handed out one pixel per column. That explanation is my inference; the effect itself is measured. **One attribute per affected grid (`HorizontalAlignment="Left"`) removes it without changing any declared size.**

2. **Declared anchors differ between control families (XAML-verified).**
   - The Size / Color rows (`48 | 14 | swatch+hex`) put the hex box at **62 to 172**. The compact shadow rows (`110 | 8 | 52 | 6 | 52`) put Blur at **118 to 170** and Depth at **176 to 228**. So, as declared, the hex right edge is **2px past** Blur's right edge (case 3). Once cause 1 is removed this is the only remaining offset on those tabs.
   - Smaller families use other anchors: Play Button Hover States at 122 / 244, Search / Buttons Background at 186, Font / Alignment rows with Alignment at 174 or 186, the Global Buttons border row starting at x 0 while its color rows start at 128, and three different left edges in the builder's header rows.

Everything else checked out. The Position & Size centre-gap anchor is intact (36px between Width and Height on single layers and ratio-lock groups). The shared Border tab, the Buttons tab, Marquee Shadow, the Scrollbars layer tabs and every Global subtab render exactly as declared.

The recommended standard (section 5) needs **one structural pass** (cause 1, no declared geometry change) and **one small value pass** (a handful of gap and width values). Both could be done as a single pass if you approve them together.

---

## 2. Complete geometry inventory

### 2.1 Shared building blocks [X]

| Block | Declared geometry | Notes |
|---|---|---|
| Color group | swatch Border 26x26, 4px gap, hex TextBox 80 = **110** wide | Everywhere except shape Content background (hex 104) and Buttons hover (hex 142) |
| `GLTextBoxStyle` box | Padding 4, border 1, Standard font 13 | Renders **27px tall** [S]; the swatch is 26 |
| Combo (`ChromeComboBoxStyle`) | Height 25 | 2px shorter than the boxes |
| Ratio lock toggle | 26x26, `SnapsToDevicePixels` | Renders crisp, top-flush in the 27px row [S] |
| `GLLabelStyle` | Margin `0,10,0,3` | Overridden to 0,0 / 0,4 / 0,6 / 0,8 / 0,12 / 0,14 / 0,16 in places (vertical rhythm, outside this audit's horizontal scope) |
| Section divider | `Margin="0,15,0,10"` | Favorites Star uses `0,10,0,10` twice (approved) |
| Tab content width | 374 without scrollbar, **358 with** (scrollbar 17 wide in the Midnight theme, plus the 10px margin) [S] | 283 to 657, or 283 to 640 |

### 2.2 Theme Builder chrome [X][S]

| Element | Geometry | Screen [S] |
|---|---|---|
| Themes Manager: "Active Theme:" label + combo 180 | label Auto + 10 | combo left at client **125** |
| Theme sizes: label + WrapPanel of buttons | label Auto + 10 | first size button at client **116** |
| Resolution: label + combo 140 | label Auto + 10 | combo at client **108** |
| Guides row | Edge 15 / Center 15 / Color: 10 / swatch 26 / Grid 15 / spacing 52 / Offset 15+10 / X 52 / 5 / Y 52 | single StackPanel, no anchor relationships |
| Layer list column | 220 (list 184 + 6 + 30 arrow buttons), 20 gap, fields at 240 | tab frame at client 272 |
| Property tab strip | wraps to two rows on Game Info (7 tabs) | the selected row moves to the front (WPF default) |
| Multi-select panel | `Margin="10,0,0,0"` on the frame grid, columns 110 / 20 / 110, boxes stretch to 110 | content at client 282, 1px left of tab content at 283 [X only, not shown: it needs Ctrl-click] |
| Theme Info view | headings + glyphs, thumbnail, Show Theme In combo 220 | not inspected on screen (opened by a button) |

### 2.3 Position & Size [X][S]

| Layout | Columns | Box widths | Gaps | Screen [S] |
|---|---|---|---|---|
| Single layer (`PnlRectFields`) | `* 12 * 12 * 36 *`, MinWidth 50 | 78.75 (374 wide) | 12 / 12 / **36** | X 283 to 361, Y 374 to 452, W 465 to 542, lock 548 to 573, H 579 to 657: **79 / 79 / 78 / 79**, gaps 12 / 12 / 36. Confirmed |
| Group with ratio lock (Random Game Display), no scrollbar | same gaps, no MinWidth | 78.75 | 12 / 12 / 36 | identical to single layer. Confirmed |
| Group without lock (Game Info, Search & Buttons), **with scrollbar** | `* 6 * 12 * 6 *` | (358 - 24) / 4 = 83.5 | 6 / **12** / 6 | 84 / 83 / 84 / 83; X 283, Y 373, W 468, H 558 to 640. Confirmed |
| Group without lock, no scrollbar | as above | 87.5 | 6 / 12 / 6 | [X only]: both non-lock groups show a scrollbar at this window size |
| Group with lock, with scrollbar | `* 12 * 12 * 36 *` | 74.75 | 12 / 12 / 36 | [X only]: Random Game Display has no scrollbar at this size |

Centre-gap anchor (decision 80 / handoff principle 10): **preserved**. Non-lock groups deliberately put the wider gap between Y and Width (decision 80), so their visual centre differs from the lock layouts. That is an intentional exception.

### 2.4 Layer styling tabs: declared vs rendered

"Rounding" = the grid is affected by cause 1 (UseLayoutRounding set or inherited, no `*` column, arranged wider than needed).

| Tab / block | Grid | Rounding? | Declared x [X] | Rendered x [S] |
|---|---|---|---|---|
| **Folders / Game Titles** outer | `110 8 52 6 52 *` | no (`*`) | shadow Blur 118 to 170, Depth 176 to 228; Selected BG 118; Nudge 118 to 170 | exact (401, 459) |
| Folders / Game Titles Size+Color sub-grid | `48 14 Auto` | **yes** (inherits) | Size 0 to 48, color 62 to 172 | Size 0 to 49, color **64 to 174** |
| **Search** Font+Alignment sub-grid | `48 14 110 14 110` | **yes** | Font combo 0 to 172, Alignment 186 | Font 0 to 172, Alignment **190** |
| Search label / box Size+Color+Background sub-grids | `48 14 Auto 14 Auto` | **yes** | Size 0 to 48, Color 62, Background 186 | Size 0 to 49, Color **64**, Background **190** |
| Search shadow (outer) | `110 8 52 6 52 *` | no | 118 / 176 | exact |
| Search nudge | StackPanel, box 48 | n/a | 0 to 48 | exact |
| **Buttons** | `48 14 110 14 110`, no UR | no | Size 0 to 48, Color 62, Background 186, hover hex 30 to 172 | exact (345, 469, hover hex ends 454 = 172) |
| **Title** outer | `110 8 52 6 52` (no `*`) | **yes** | Blur 118 to 170, Depth 176 to 228 | Blur **120 to 173**, Depth **180 to 233** |
| Title Size+Color sub-grid | `48 14 Auto` | **yes** | color 62 to 172 | **64 to 174** |
| **Publisher** outer | `110 8 52 6 52 *` | no | 118 / 176 | exact (401 to 452, 459 to 510) |
| Publisher Font+Alignment sub-grid | `160 14 Auto` | **yes** | Alignment 174 | **176** |
| Publisher Size+Color sub-grid | `48 14 Auto` | **yes** | 62 | **64** |
| **Game Info**: Nav, Headers, Body outer | `110 8 52 6 52` | **yes** | 118 / 176 | **120 / 180** (each 53 wide) |
| Game Info Size+Color sub-grids | `48 14 Auto` | **yes** | 62 | **64** |
| **Artwork Previews** | `110 8 52 6 52` | **yes** | 118 / 176 | **120 / 180**. Predicted, then confirmed |
| **Play Button** Text outer | `110 8 52 6 52 *` | no | shadow 118 / 176 | exact |
| Play Button text+nudge sub-grid | `170 6 Auto` | **yes** | text 0 to 170, nudge 176 to 228 | text 0 to **171**, nudge **178 to 230** |
| Play Button Button outer | `110 8 52 6 52` | **yes** | 118 / 176 | **120 / 180** |
| Play Button Border | `110 8 58 Auto`, no UR | no | Size 118 to 168, Radius 176 to 236 | exact |
| Play Button Hover States | `110 12 110 12 110`, no UR | no | groups at 0 / 122 / 244 | exact |
| **Favorites Star** Solid / Outline outer | `110 8 52 6 52` | **yes** | 118 / 176 | **120 / 180** |
| Favorites Star size | StackPanel, label then box 48 | n/a | label then box | (label-left, the one exception) |
| **Random Game Display Text** outer | `110 8 52 6 52 *` | no | shadow 118 / 176, message 0 to 228 | exact |
| RG Text Font+Alignment sub-grid | `160 14 Auto` | **yes** | Alignment 174 | **176** |
| RG Text max sizes | StackPanels, box 52 | n/a | 0 to 52 | not measured |
| **Text Marquee** outer | `110 8 52 6 52 *` | no | font combo 0 to 170, shadow 118 / 176 | exact |
| Text Marquee Color+Max size sub-grid | `110 8 52` | **yes** | Max size 118 to 170 | **120 to 172**. Predicted, then confirmed |
| Text Marquee wrap+margin sub-grid | `110 8 52 *` | no | margin 118 to 170 | exact |
| Text Marquee border sub-grid | `110 8 58 Auto` | **yes** | Size 118 to 168, Radius 176 to 236 | Size **120 to 170**, Radius **179 to 239**. Predicted, then confirmed |
| **Marquee Shadow** | `110 8 52 6 52`, no UR | no | 118 / 176 | exact. Predicted, then confirmed |
| **Shared Border tab** | `110 8 Auto 8 Auto`, no UR | no | Size 118 to 168, Radius 176 to 236, hover hex 30 to 110 | exact (401 to 450, 459 to 518) |
| Shape / custom Content | file row `* Auto Auto`; opacity 43; background hex **104** | no | swatch+hex 0 to 134 | not visited (no grid alignment with other rows) |
| Splash Image, Video Preview Fallback, Logo, Main Window Background | file row; Main BG / VP color group 110 | no | 0 to 110 | not visited (single-column layouts) |
| **Scrollbars** Games List / Game Info | `80 8 Auto 14 Auto`, no UR | no | Thumb 88, Track 212 | exact (371, 495) |

### 2.5 Global tab [X][S]

Content origin client x 43; width 615, so nothing is width-constrained.

| Subtab | Grid | Declared x | Rendered [S] |
|---|---|---|---|
| Fonts | `120 8 48 14 Auto` | Size 128 to 176, Color 190 to 300 | exact (171, 233) |
| Backgrounds | `110 8 52 6 52` | color groups 0 to 110; border Size 118 to 170, Radius 176 to 228 (**52 / 52**) | exact |
| Tabs, Scrollbars, Context Menus | `120 8 Auto 14 Auto` | first column 128 to 238, second 252 to 362 | exact (171, 295) |
| Buttons | same, plus a border sub-grid spanning all columns, `110 8 52 6 52` | color rows 128 / 252; **border color 0**, Size 118 to 170, Radius 176 to 228 | exact: border color at 43 vs color column at 171 (**128px apart**) |

---

## 3. Alignment matrix (current vs recommended)

Recommended values assume the section 5 standard is approved. "S1" = structural fix (cause 1), "V" = value change.

### 3.1 Color / Blur / Depth (compact shadow): anchor family A

| Item | Current declared | Current on screen | Recommended | Change |
|---|---|---|---|---|
| Shadow color group | 0 to 110 | 0 to 110 | 0 to 110 | none |
| Blur | 118 to 170 | 118 to 170, or **120 to 173** on rounding tabs | 118 to 170 | S1 only |
| Depth | 176 to 228 | 176 to 228, or **180 to 233** | 176 to 228 | S1 only |

### 3.2 Size / Color

| Item | Current declared | On screen | Recommended | Change |
|---|---|---|---|---|
| Size box | 0 to 48 | 0 to 49 | 0 to 48 | S1 |
| Gap Size to swatch | 14 | 15 | **12** | V (one value per sub-grid) |
| Color group | 62 to 172 | 64 to 174 | **60 to 170** | follows |
| Hex right vs Blur right | +2 | +4 (Publisher-type tabs), +1 (Title-type tabs) | **0 (flush at 170)** | |

### 3.3 Font / Alignment rows

| Item | Current declared | On screen | Recommended | Change |
|---|---|---|---|---|
| Font combo (Folders, Game Titles, Title, Publisher, GI blocks, Play Button, RG Text) | 160 | 160 | 160 (or 170, Q3) | none / optional |
| Font combo (Search) | 172 | 172 | follows Q3 (170 or 160) | V |
| Font combo (Text Marquee) | 170 | 170 | keep (ends at Max size / Blur right edge 170) | none |
| Alignment combo x (Publisher, RG Text) | 174 | 176 | **176** (Depth anchor) | S1 + V (gap 14 to 16) |
| Alignment combo x (Search) | 186 | 190 | **176** | S1 + V (Search grid, 3.4) |

### 3.4 Size / Color / Background (Search label, Search box, Buttons)

| Item | Current declared | On screen | Recommended | Change |
|---|---|---|---|---|
| Size | 0 to 48 | Search 0 to 49, Buttons exact | 0 to 48 | S1 (Search) |
| Color group | 62 to 172 | Search 64 to 174 | 60 to 170 | V (gap 12) |
| Background group | 186 to 296 | Search 190 to 300 | **176 to 286** (Depth anchor), Q2 | V (gap 6) |
| Buttons hover hex (spans Size + Color) | 30 to 172 (142 wide) | exact | 30 to 170 (**140** wide) | V |
| Buttons Hover BG | 186 | exact | 176 | follows |

### 3.5 Border / Size / Radius

| Item | Current declared | On screen | Recommended | Change |
|---|---|---|---|---|
| Shared Border tab, Play Button Border | Size 118 to 168, Radius 176 to 236 | exact | keep (approved 50 / 60) | none |
| Text Marquee border | same declared | Size **120**, Radius **179** | 118 / 176 | S1 |
| Global Backgrounds / Buttons border | Size 118 to 170, Radius 176 to 228 (52 / 52) | exact | 50 / 60 with `110 8 Auto 8 Auto`, Q4 | V (optional) |
| Global Buttons border row origin | 0 (color rows at 128) | 0 vs 128 | Q5 | V |

### 3.6 Other rows

| Item | Current | Recommended | Change |
|---|---|---|---|
| Play Button Hover States | 0 / 122 / 244 (gaps 12) | **0 / 118 / 236** (gaps 8) | V |
| Play Button text / nudge | text 0 to 170, nudge 176 (screen 171 / 178) | same as declared | S1 |
| Text Marquee Max size | 118 (screen 120) | 118 | S1 |
| Header rows (Active Theme / Theme sizes / Resolution) | controls at client 125 / 116 / 108 | one shared label width (Q6) | V (optional) |
| Swatch vertical | 26px centered in a 27px row, soft top and bottom edges | crisp (Q7) | V |

---

## 4. Discrepancy register

Legend: **C** = confirmed, **E** = estimate; **X** = XAML-verified, **S** = screenshot-verified.

| # | Location | Magnitude | Cause | Status |
|---|---|---|---|---|
| D1 | Title, Game Info (Nav / Headers / Body), Artwork Previews, Play Button "Button", Favorites Star (both) shadow rows | Blur +2, Depth +4, each box +1 wide | Cause 1 (UR, no `*` column) | C, S (predicted for Previews, then confirmed) |
| D2 | Every Size / Color sub-grid (Folders, Game Titles, Title, Publisher, GI blocks, Play Button text) | Size +1 wide, color group +2 | Cause 1 (inherited UR) | C, S |
| D3 | Search label / box sub-grids | Color +2, Background +4 | Cause 1 | C, S |
| D4 | Publisher and RG Text Alignment combos | +2 (174 to 176) | Cause 1 | C, S (RG E, by eye) |
| D5 | Search Alignment combo | +4 (186 to 190) | Cause 1 | C, S |
| D6 | Text Marquee Max size | +2 | Cause 1 | C, S (predicted) |
| D7 | Text Marquee border Size / Radius | +2 / +3 vs Blur / Depth above | Cause 1 | C, S (predicted) |
| D8 | Play Button button-text / nudge | text +1 wide, nudge +2 | Cause 1 | C, S |
| D9 | Title vs Publisher (your case 2) | Blur / Depth 2 / 4px apart between the two tabs | D1 (Title) vs exact (Publisher) | C, X+S |
| D10 | Size / Color hex right edge vs Blur right edge (your case 3) | +2 declared (172 vs 170) | Different families: 48 + 14 + 110 vs 110 + 8 + 52 | C, X+S (Buttons tab shows the declared 172 exactly) |
| D11 | Play Button Border vs shadow rows above (your case 1) | left edges flush (118 / 176); right edges -2 (Size 168 vs 170) and +8 (Radius 236 vs 228) | Approved 50 / 60 widths | C, X+S. Intentional |
| D12 | Play Button Hover States | groups at 122 / 244, no shared anchor | `110 12 110 12 110` | C, X+S |
| D13 | Search / Buttons Background column | 186, no shared anchor (Depth 176) | `48 14 110 14 110` | C, X+S |
| D14 | Global Buttons border row | starts at x 0; color rows at 128 | Separate sub-grid spanning the label column | C, X+S |
| D15 | Global border Size / Radius vs layer tabs | 52 / 52 vs 50 / 60 | Global grids predate the Border tab change | C, X |
| D16 | Swatch vs hex box height | box 27, swatch 26, centered at a half pixel: soft (antialiased) top and bottom edges | Swatch Border has no pixel snapping; the 27px box height comes from Padding 4 + 13px text | C, S (zoomed) |
| D17 | Combo height vs boxes | 25 vs 27 | Explicit `Height="25"` | C, X+S. Rows are separate, low impact |
| D18 | Header row left edges | 125 / 116 / 108 | Each label is Auto + 10 | C, S |
| D19 | Multi-select panel | 1px left of tab content (282 vs 283) | `Margin="10,0,0,0"` on the frame grid, outside the tab's 1px border | E, X only |
| D20 | Small numeric widths | 43, 48, 50, 52, 60, 70, plus stretched boxes | History of separate passes | C, X (see 5.3) |
| D21 | themes.md / CLAUDE.md vs code | decision 79 says hex 104 and border size / radius 36; section 3.x says Border columns `134 / 22 / Auto / 22 / Auto`; code has 80, 50 / 60 and `110 / 8 / Auto / 8 / Auto` | Docs not updated after the later tweak | C, X (report only; not edited) |

---

## 5. Proposed common standards

### 5.1 Structural rule (fixes D1 to D9)
Every Theme Builder grid that has only pixel / Auto columns gets `HorizontalAlignment="Left"`. Then it is arranged at exactly its declared width, and layout rounding has nothing to distribute. Keep `UseLayoutRounding` where it is: it was added to keep checkboxes and labels level (themes.md). Adding a trailing `*` column also works (Publisher proves it), but it is a bigger edit. Prefer the attribute.

After this, **every on-screen position equals the declared XAML value**, so the rest of this standard can be reasoned about from the XAML alone.

### 5.2 Horizontal anchors (layer tabs)

| Anchor | x | Used by |
|---|---|---|
| A0 | 0 | first control of every row |
| A1 | **60 to 170** | color group after a Size box (Size 0 to 48, gap 12) |
| A2 | 110 | right edge of a first-column color group |
| A3 | **118** | Blur, Border Size, Max size, Text margin, Selected BG, Nudge, Hover Background |
| A4 | **170** | right edge shared by Blur, the Size / Color hex, the Font combo (if Q3 = 170), the Play Button text box |
| A5 | **176** | Depth, Border Radius, Alignment combo, Search / Buttons Background, Play Button nudge |
| A6 | 228 | Depth right edge, the message box right edge |
| A7 | 236 | third Hover States group (Play Button) |

Gaps used: 4 (swatch to hex), 6 (between 52 boxes; Background after a color group), 8 (first column to second), 12 (Size to swatch; Position & Size), 36 (lock column).

### 5.3 Widths
- **Hex 80** (kept). Exceptions kept: shape Content background 104 (decision 79 capacity), Buttons hover hex 140 (spans Size + Color).
- **Small numeric boxes.** Keep the three widths that carry a meaning:
  - **48** for Size and other single standalone boxes (Search nudge, gap to scrollbar, star size, scrollbar width, Global font sizes)
  - **52** for boxes on the 52 columns (Blur, Depth, Max size, text margin, Play Button / Folders / RG nudges and max sizes, grid spacing and offsets)
  - **50 / 60** for Border Size / Radius (approved)

  Standardizing all of them to one width would move the approved border boxes and the Size column for no visual gain. Not recommended.
- Position & Size: unchanged (50 minimum on single layers, centre gaps as they are).
- Opacity 43 and custom resolution 70: unchanged (unique rows).

### 5.4 Legitimate exceptions that stay
- Border Size 50 / Radius 60: right edges differ from Blur / Depth by design (approved, labels must not overhang).
- Non-lock group Position & Size gaps `6 | 12 | 6` (decision 80).
- Scrollbars layer label column 80 (sized for "Dragging:", by design).
- Global label column 120 (Global grids are a separate family with row labels on the left).
- Favorites Star size: the label left of its box. The approved dividers and spacing are unchanged.
- Search and Marquee Shadow standalone toggles, the header-row Bold / Wrap checkboxes, compact Drop Shadow groups.

---

## 6. Dependency and regression assessment

| Change | What moves | Shared grids / neighbours | Clip risk | Other consumers | Approved layout changed? | Code-behind? |
|---|---|---|---|---|---|---|
| **S1** `HorizontalAlignment="Left"` on the affected grids (about 25 grids, listed in 7) | On screen, controls on those grids move left 1 to 4px to their declared x | None: each grid is self-contained; sub-grids inside them are fixed too | None: arranged width = declared width, and every grid fits (widest 233 < 358) | Name-keyed GI handlers unaffected (no names change) | No; it restores the declared values | No |
| **V1** Size / Color gap 14 to 12 (every `48 14 Auto` sub-grid, plus Search and Buttons) | Color group moves left 2 | Only the sub-grid row; the shadow row below is a different grid | None | Search / Buttons Background moves (V2) | No (the gap was never an approved value) | No |
| **V2** Search / Buttons third column gap 14 to 6 (Background at 176); Buttons hover hex 142 to 140 | Background / Hover BG move left 10 | Search Font / Alignment sub-grid must use the same columns (`48 12 110 6 110`), so the Alignment combo moves to 176 too | None (total width 286) | none | No | No |
| **V3** Publisher / RG Text Font+Alignment gap 14 to 16 | Alignment combo moves right 2 (to 176) | RG's Wrap text checkbox moves with it; still fits (column 5 is `*`) | None | none | No | No |
| **V4** Play Button Hover States gaps 12 to 8 | Background group moves left 4, Border color group left 8 | none | None (346 < 358) | none | It was flagged "for visual review" in the XAML comment | No |
| **V5** (Q4) Global Backgrounds / Buttons border boxes to 50 / 60 with Auto columns | Global Radius box widens to 60 | Global field tables find controls by name; names unchanged | None (Global is 615 wide) | Restore Defaults unaffected | No | No |
| **V6** (Q5) Global Buttons border row onto the 128 / 252 columns | Border row moves right | Sits inside the Buttons grid | None | none | No | No |
| **V7** (Q7) Swatch snapping | Swatch sharp, top-flush like the lock toggle | Every swatch Border (about 60 in AdvancedThemesTabControl.xaml, 25 in GlobalThemesTabControl.xaml) | None | The color picker popup is unaffected (only the Border changes) | No | No |
| **V8** (Q6) Header rows on one label width | Size buttons and Resolution combo move right to 125 | Two UserControls (ThemesManagerControl, AdvancedThemesTabControl) must use the same label width | None | none | No | No |
| **Q3** Font combos 160 to 170 | Combos widen 10 | Publisher / RG Font column 160 to 170 (then the Alignment gap is 6 to keep 176) | None | none | **Yes:** "other Font combos 160" is an existing value | No |

---

## 7. Implementation strategy (fewest passes)

**Pass A (structural, no visual decisions).** Add `HorizontalAlignment="Left"` to these grids in `Views/AdvancedThemesTabControl.xaml`:
- every Size / Color sub-grid (Folders, Game Titles, Title, Publisher, Nav, Headers, Body, Play Button text)
- the Search Font / Alignment, label and box sub-grids
- the Title, Nav, Headers, Body, Artwork Previews, Play Button "Button", Favorites Star Solid and Outline outer grids
- the Publisher and RG Text Font / Alignment sub-grids
- the Play Button text / nudge sub-grid
- the Text Marquee Color / Max size and border sub-grids

Grids with a `*` column, and grids without layout rounding, are left alone. Test: every row in section 2.4 measures its declared x.

**Pass B (values, after your answers to section 8).** V1 to V4 in the same file, plus V5 to V8 if approved. Then update themes.md: the anchors table, decision 79's text, the Border column note at line 151 and the change log.

If you approve both now, A and B can be one pass: A has no design choices, and B is a short list of value edits.

---

## 8. Questions requiring your approval

1. **Q1 Size / Color gap.** Make the gap between Size and the swatch **12** (hex right edge flush with Blur at 170; Size stays 48)? Alternative: Size 52 + gap 8 (Size matches the other 52 boxes; color group also ends at 170).
2. **Q2 Search / Buttons Background.** Move Background (and Hover BG, the Search Alignment combo) to **176** (Depth anchor, 6px after the hex box)? Alternative: keep a wider gap (12 or 14, so x 182 or 184) with no anchor.
3. **Q3 Font combo width.** Keep **160** (approved value) or make every Font combo **170**, so it ends at the shared 170 edge with the Text Marquee combo?
4. **Q4 Global border boxes.** Change Global Backgrounds / Buttons Size and Radius from 52 / 52 to the layer tabs' **50 / 60** (`110 8 Auto 8 Auto`)?
5. **Q5 Global Buttons border row.** Put it on the subtab's columns (label "Border:" on the left, color at 128, Size at 252, Radius at 310)? Or keep it as a separate block at x 0 (as on Backgrounds) with a little extra space above?
6. **Q6 Header rows.** Line up the Active Theme combo, the Theme size buttons and the Resolution combo on one left edge (a fixed label width, about 83px, in both controls)?
7. **Q7 Swatch edges.** Fix the soft top / bottom edge by snapping the swatch Border to whole pixels (it then sits top-flush, 1px short at the bottom, like the lock toggle)? Alternatives: make text boxes 26 tall (Padding `4,3,4,4`, affects every box), or swatches 27 tall (breaks the square and the 110 group).
8. **Q8 Play Button Hover States.** Use gaps of 8 (groups at 0 / 118 / 236)?

---

## 9. Limitations

- **Rendered inspection was possible**, of the Theme Builder window only, at **one setting**: 96 DPI, 717 x 900, the Midnight theme (scrollbar 17 wide), font size 13. Results at other DPI scales, window sizes or font sizes are calculated, not observed. Cause 1 depends on extra width, so it appears at any window width.
- **UI Automation cannot see inside the tab pages.** `ThemedTabControlStyle`'s ContentPresenter is not named `PART_SelectedContentHost`, so WPF does not expose the selected tab's content to automation. Navigation therefore used mouse clicks (with your approval), and layout boxes were measured from pixels, not from automation rectangles. This is an accessibility side note, not a geometry issue, and it is outside this audit's scope.
- **Not shown on screen** (needed a checkbox, keystroke or button, or did not occur at this size):
  - the multi-select panel (needs Ctrl-click)
  - the Theme Info view (opened by a button)
  - the Random Game Display Border tab with its state combo (not visited; the shared Border tab was measured on Search & Buttons)
  - the Shape / custom Content, Splash Image, Video Preview Fallback, Logo and Main Window Background tabs (single-column file rows, nothing to align across columns)
  - the Global Tabs and Context Menus subtabs (same grid as Global Scrollbars, measured)
  - the Position & Size "lock group with scrollbar" and "non-lock group without scrollbar" cases

  All of these were derived from the XAML.
- Disabled (shadow-off) boxes were measured by their fill edge (no visible border); the result matched the enabled boxes on the same tabs.
- The cause-1 explanation (WPF's layout-rounding correction in `Grid`) is an inference that fits all measurements, including four predictions made before looking. The fix in 5.1 does not depend on the exact internal reason.
- All captures (28 PNGs, Theme Builder window only) were saved in `.audit-temp/` in the project. The delete command was refused by the session's permission settings, so the folder still has to be removed (see the chat). The capture script is in the session scratchpad, outside the project.
