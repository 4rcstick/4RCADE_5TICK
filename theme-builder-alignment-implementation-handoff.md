# HANDOFF: Theme Builder — Geometry Alignment Implementation (one coordinated pass, with checkpoints)

**Type:** implementation. UI geometry only: grid definitions, gaps, widths, alignment attributes.
**Flow:** Step 0 repo check and audit read → Step 1 confirm current geometry → **Checkpoint A (structural validation with the user, rendered)** → Checkpoints B to F (edits, one compile check each) → Step 3 final clean rebuild, static table and **one rendered verification sweep with the user** → report → **STOP for the user's visual acceptance**.
**Never commit or push.** Do **not** edit `themes.md` or `CLAUDE.md` (documentation is handled after the user accepts).

---

## 1. Goal

Make corresponding controls across the Theme Builder share flush left and right edges, using one alignment system, per the forensic audit **`theme-builder-geometry-audit.md`** (project folder root; use that exact filename; if it is missing, stop and say where you looked). **Read all of it first.** Its section 2.4 and 2.5 tables are your inventory, section 3 the matrix, and section 4 (D1 to D21) the discrepancy register: every D-number must be accounted for in your final report.

This is a geometry correction, not a redesign. The user has made the design decisions in section 2 and has **delegated the remaining minor decisions to this handoff**, so do not come back with the audit's eight questions; section 4 answers them.

## 2. The user's decisions (binding)

1. **Small numeric inputs are 52 px** where practical: Blur, Depth, Size, Nudge and comparable boxes. **Border Size and Radius become 52 / 52** (the earlier 50 / 60 is no longer protected). Keep another width only where function, content or a distinct layout needs it.
2. **Hex boxes stay 80 px** (do not change them to fix spacing). Existing hex exceptions are evaluated individually (section 5).
3. **Flush edges and shared anchors** matter most. You may adjust grid definitions, gaps, margins, numeric widths and combo widths to get them.
4. **Align related combos and color groups** (Font, Alignment, Background, hover) but do not force one width on genuinely different layout families or where it would clip.
5. **Preserve:** vertical spacing, the approved dividers (`Margin="0,15,0,10"`; the two Favorites Star dividers at `0,10,0,10`), typography, labels, section order, styling, and **all Position & Size layouts** (center gaps, ratio-lock arrangements).
6. **Priority order when in doubt:** flush left and right edges → shared anchors → 52 px small numerics → 80 px hex → consistent spacing between comparable groups → no clipping → keep intentional exceptions → least code complexity, no fragile one-off offsets. If two solutions are equal, pick the simpler.

## 3. Rules (do NOT)

- No Python. **Do not launch the application** (the user launches it and brings the Theme Builder to the front). No commits or pushes. No edits to `themes.md` / `CLAUDE.md`.
- Source inspection with Read/Grep/Glob; no shell pipelines. Shell only for `dotnet build ArcadeStick.csproj`, `dotnet build ArcadeStick.csproj --no-incremental`, and read-only `git status` / `git diff --stat`. Stay inside the project folder.
- Use the Edit tool; keep CRLF in `.cs`/`.xaml` and the `<!-- [SECTION: ... ] -->` fencing; update comments that describe changed geometry (for example the shared Border grid comment that mentions 104 px hex and `22 / 50 / 22 / 60`). After each edit group, re-check that adjacent attributes are still separated by a space.
- **Never rename or drop any `x:Name`, `Tag`, handler, binding, `IsEnabled`/`ElementName` relationship, range, validation or visibility logic.** This pass should need **no code-behind change**; if you think it does, stop and say why.
- **Do not disable layout rounding globally**, and do not remove `UseLayoutRounding` from any element.
- Do not touch Position & Size layouts, dividers, labels, typography, or anything section 5 lists as an exception.

### 3.1 Rendered-inspection gate (applies to Checkpoint A and the final sweep)
You may use the mouse and screenshots **only after the user approves** and **only on the Theme Builder window**.
1. Say exactly what you will capture and which window (and what script or tool you will run), then wait for the user's "go". The user closes personal windows first. (The user's reply "ready, go" to a message that already states the plan counts as approval.)
2. Capture **only the Theme Builder window** (as in the audit: `PrintWindow` of its own handle), never the full desktop. Verify before every capture, click or scroll that the launcher process is running and the Theme Builder window is in front; if not, **abort and report**. Never touch another app.
3. Navigation only: select layers, click tabs, scroll, hover. **No** typing, toggling, dragging, combo changes, Save, Reset, Restore Defaults, Delete, New Theme or Load.
4. Screenshot measurements are **estimates, about ±1 px**; label them so. XAML-declared numbers are authoritative.
5. Save captures only to a **new** temp folder inside the project (for example `.align-temp/`), view only what you need, and delete them when done. The audit's `.audit-temp/` folder may still exist: **do not touch it** (the user will remove it). If deleting your own folder is refused by permissions, list what remains and tell the user; do not work around the refusal.
6. If screenshot tooling is unavailable, say so and report static verification only; never claim visual verification you did not do.

## 4. The alignment standard

All x values are measured from the left edge of the tab content (inside the tab frame border and the 10 px page margin). Usable width is **358** with a scrollbar, **374** without.

### 4.1 Decisions on the audit's questions
- **Q1 Size/Color:** Size box **52**, gap **8**, color group **110** → color group **60 to 170** (swatch 60 to 86, hex 90 to 170). The hex right edge is flush with Blur's right edge at **170**.
- **Q2 / gap rule:** one gap value, **8**, between adjacent groups and boxes in this family (Size to color, first to second column, Blur to Depth, color group to Background group). Reason: the swatch-to-hex gap inside a group is 4, so a 6 px gap between groups would read as one run of controls; 8 separates them clearly. Consequence: **the compact shadow grids change from `110 8 52 6 52` to `110 8 52 8 52`**, so **Depth moves from x 176 to x 178** (a 2 px shift) in every compact shadow row; this is intentional.
- **Q3 Font combos:** every Font combo is **170 wide (0 to 170)**, flush with the Size/Color hex right edge, Blur's right edge, Max size and Button Text.
- **Q4 Global border boxes:** Size and Radius are 52 / 52 on the Global tab too (they already are); only their gap column changes with the family grid.
- **Q5 Global Buttons border row:** move that border block's left edge to the subtab's color column by giving the block a **left margin of 128** (keep its own family grid `110 8 52 8 52`). If a margin does not achieve this without restructuring, leave it at x 0, document it as an exception, and flag it for the user's visual review.
- **Q6 Header rows:** optional refinement in Checkpoint F.
- **Q7 Swatch edges:** `SnapsToDevicePixels="True"` on the swatch Borders, Checkpoint F.
- **Q8 Play Button Hover States:** `110 8 110 8 110` → groups at **0 / 118 / 236**.

### 4.2 Anchors and geometry (layer tabs)

| Anchor | x | Used by |
|---|---|---|
| A0 | 0 | first control of every row; Font combo and Size box left edges |
| A1 | 60 to 170 | color group after a Size box |
| A2 | 0 to 110 | first-column color group (shadow color, Border color, Selected text, Hover color group 1, Arrow color, Button Background, etc.) |
| A3 | **118 to 170** | Blur, Border Size, Max size, Text margin, Selected BG, Nudge, Play Button Hover group 2 start |
| A4 | **170** | shared right edge: Size/Color hex, Blur, Font combo, Max size, Text margin box, Button Text, Border Size |
| A5 | **178** | left edge of Depth, Border Radius (178 to 230), Alignment combo, Search/Buttons Background group (178 to 288), Hover BG, Play Button nudge (178 to 230) |
| A6 | 230 | Depth right edge, message-box right edge |
| A7 | 236 | Play Button Hover group 3 (236 to 346) |

Final grids:
- **Compact shadow / Border / Max size / Text margin / Nudge grids:** `110 8 52 8 52` (keep any trailing `*` column where one exists).
- **Size/Color sub-grids:** `52 8 Auto` (Auto hosts the 110 px color group).
- **Search** (all three sub-grids use the same columns): `52 8 110 8 110`. Font combo spans columns 0 to 2 (0 to 170); Alignment combo (width 110) in column 4 (178 to 288); Size / Color / Background row: Size 0 to 52, Color 60 to 170, Background 178 to 288.
- **Buttons:** `52 8 110 8 110`. Row 2: Hover color spans Size + Color (swatch 0 to 26, hex 30 to 170, **hex width 140**), Hover BG in the Background column at 178.
- **Publisher and Random Game Display Text Font/Alignment sub-grid:** `170 8 Auto`; Alignment combo at 178 (keep an existing 110 width; if different, set 110 unless the longest item would clip). Random Game Display Text: "Alignment:" header + Wrap Text beside it must remain fully visible (about 330 px right edge).
- **Play Button:** Button Text / Nudge sub-grid `170 8 52` (text 0 to 170, nudge 178 to 230, full label "Vertical nudge (px)" to its right, ends at about 345; verify it does not clip); Border grid `110 8 52 8 52`; Hover States `110 8 110 8 110`.
- **Text Marquee:** Font combo 170 (already); Color/Max size `110 8 52`; wrap/margin `110 8 52 *`; border sub-grid `110 8 52 8 52`.
- **Shared Border tab:** `110 8 52 8 52`; Size and Radius boxes **52 / 52**; hex boxes stay 80; the Hover border row (`PnlSBHoverBorder`) keeps its own position and 80 px hex.

### 4.3 Small numeric widths
Set to **52**: Size boxes (font size) on the layer tabs, Search nudge, gap to scrollbar, star size, the Scrollbars layer tabs' Width box, the Global font-size boxes (Global Fonts `120 8 52 14 Auto`: keep the 14 gap there), and Border Size / Radius everywhere (layer tabs and Global). Blur, Depth, Max size, text margin and every nudge are already 52. Find any other 48 or 50 or 60 small numeric box and apply the same rule unless it is an exception.

## 5. Exceptions that stay (document each in the report)

- **Position & Size**, all layouts (center gaps, `6 12 6` non-lock groups, 36 px lock column, MinWidth 50).
- **Opacity box 43, custom resolution box 70.**
- **Shape/custom Content background hex 104** (and its 134 group).
- **Scrollbars layer tabs** (`80 8 Auto 14 Auto`, label-left family) and **Global label-left family** (`120 8 Auto 14 Auto`, color columns 128 / 252): only their small numeric boxes change to 52.
- **Favorites Star "Star size:"** (label to the left of the box).
- **Combo height 25 vs box height 27** (D17): rows are separate; leave.
- **Standalone-toggle shadows** (Search Label, Marquee Shadow tab) keep their layouts; the Marquee Shadow tab's grid still takes the gap 8 family change.
- Dividers, typography, labels, the Themes Manager / guides rows (except Checkpoint F).

## 6. Step 0 and Step 1 (before editing)

0. `git status`. The tree must be clean apart from untracked files (this handoff, the audit report, `.audit-temp/`). If any source file is modified, **stop and tell the user**.
1. Read the audit fully. Re-read the affected XAML (`Views/AdvancedThemesTabControl.xaml`, `Views/GlobalThemesTabControl.xaml`, and `Views/ThemesManagerControl.xaml` / `Views/ThemeBuilderWindow.xaml` only for Checkpoint F) and confirm the current geometry matches the audit's tables. Report any drift in a few lines, then continue (no stop needed unless a stop condition in section 9 applies). Find controls by `x:Name`, tab names (`TabGLFolders`, `TabGLGameTitles`, `TabSBSearch`, `TabSBButtons`, `TabMarqueeBanner`, `TabMarqueeShadow`, `TabShapeBorder`, `TabScrollGamesList`, `TabScrollGameInfo`, ...) and header text, not by remembered line numbers.

## 7. Checkpoints (edit, then compile; do not batch across checkpoints)

### Checkpoint A — Validate the structural correction (the user is in the loop)
The audit found that a grid with `UseLayoutRounding` (own or inherited), **no `*` column**, arranged wider than it needs, renders about 1 px wider per column; `HorizontalAlignment="Left"` is the proposed fix. This is a strongly supported **hypothesis**.
1. On the **Game Info → Title** tab only, add `HorizontalAlignment="Left"` to the compact shadow outer grid and to the Size/Color sub-grid. Change nothing else. Build (`dotnet build ArcadeStick.csproj`).
2. Post the capture plan and ask the user to launch the app, open the Theme Builder, select Game Info, open the Title tab and then the Publisher tab, bring the Theme Builder to the front, and close personal windows. Wait for "ready, go". Then (gate 3.1) measure: expected **Title** Size 0 to 48, color 62 to 172, Blur 118 to 170, Depth 176 to 228 (the old declared values, before any gap change), matching **Publisher**, which already renders exact.
3. **Success** (within about 1 px): continue. **Failure:** **STOP**. Investigate the real cause with measurements, report, and wait. Do not proceed and do not disable layout rounding.

### Checkpoint B — Structural fix everywhere
Add `HorizontalAlignment="Left"` to every Grid that has `UseLayoutRounding` (set on it or inherited), **no `*` column**, and fixed/Auto-only columns (the audit's section 2.4 "Rounding = yes" rows plus section 7 Pass A list: Size/Color sub-grids, Search sub-grids, Title/Nav/Headers/Body/Artwork Previews/Play Button "Button"/Favorites Star outer grids, Publisher and RG Text Font/Alignment sub-grids, Play Button text/nudge, Text Marquee Color/Max size and border). Rules: **never** on a grid that has a `*` column or whose children rely on stretching (file rows, message rows); not needed on grids without layout rounding. Any grid you create or reshape later in this pass follows the same rule. Build.

### Checkpoint C — Compact family, Size/Color rows, Border boxes
- Change the gap column in every `110 8 52 6 52 ...` grid to 8 (layer tabs: Folders, Game Titles, Search label shadow, Title, Publisher, Nav, Headers, Body, Artwork Previews, Play Button text and button, Favorites Star, Random Game Display Text, Text Marquee, Marquee Shadow; Global Backgrounds and Global Buttons border). Move anything positioned at the old Depth x (176) to the new anchor 178 via the grid, not margins.
- Size/Color sub-grids `48 14 Auto` → `52 8 Auto`; Size boxes 48 → 52.
- Border grids to `110 8 52 8 52` with Size/Radius 52 / 52: shared Border tab, Text Marquee, Play Button (also fixes D11 and D7). Update the stale comment.
Build.

### Checkpoint D — Combos, third columns, Play Button
- Font combos → 170 everywhere (Folders, Game Titles, Title, Publisher, Game Info blocks, Play Button, Random Game Display Text, Search spans 0 to 170); Publisher / RG Text Font+Alignment grids `170 8 Auto`.
- Search and Buttons grids per 4.2 (`52 8 110 8 110`, Alignment at 178, Background / Hover BG at 178, Buttons hover hex 140).
- Play Button: text/nudge `170 8 52`, Hover States `110 8 110 8 110`.
Build.

### Checkpoint E — Remaining small numerics and Global
Search nudge, gap to scrollbar, star size, Scrollbars Width box, Global font-size boxes → 52 (4.3). Global Buttons border block left margin 128 (Q5). Check every Global subtab against the audit's 2.5 table. Build.

### Checkpoint F — Optional refinements (each independent; do one only if it needs no restructuring; otherwise skip it and say why)
- **Swatches (Q7):** add `SnapsToDevicePixels="True"` to the swatch Borders (about 85 across the two XAML files). Use `replace_all` only on exactly identical markup; edit variants individually; report the count.
- **Header rows (Q6):** one shared label width (derive from the widest label, about 83 px) for the Themes Manager "Active Theme:", Theme sizes and Resolution labels so their controls share a left edge.
- **Multi-select panel (D19):** if the XAML confirms its margin puts the content 1 px left of the tab content, correct the margin by 1.
Build.

## 8. Step 3 — Final verification and report

1. **Final clean rebuild:** `dotnet build ArcadeStick.csproj --no-incremental`; **0 errors**; report any warning (previous full rebuilds reported 0).
2. **Static verification (authoritative):** a table of every row family from the audit's section 2.4 and 2.5 with the **declared** left/right x after your edits against the section 4 anchors. Confirm every Size/Color hex right edge, Blur right edge, Font combo right edge, Max size right edge and Button Text right edge equals 170; every Depth / Radius / Alignment / Background left edge equals 178; no control's right edge exceeds 356.
3. **Name and binding checks:** every `x:Name`/`Tag`/handler touched is unchanged; the Game Info name-keyed controls (`GI{Block}{Control}`) all still exist; shadow `IsEnabled`/`ElementName` bindings untouched; attributes properly spaced; `git diff --stat` shows only `Views/*.xaml` changes with no whole-file or line-ending rewrite (CRLF intact).
4. **Rendered verification sweep:** after the clean rebuild, post the capture plan (gate 3.1) and ask the user to launch the app and focus the Theme Builder. Then, Theme Builder window only, cover every tab family in the audit's 2.4 and 2.5 tables plus the shared Border tab on Search & Buttons, comparing rendered x with the declared values (estimates ±1 px). Clean up your temp folder.
5. **Report in this form:** files changed · edits by checkpoint · **final alignment table** (declared and rendered) · **D1 to D21 disposition** (fixed / kept as exception / not applicable, with reasons) · exceptions kept · anything that did not match the instructions · anything changed beyond them · screenshot status and cleanup · the user's visual checklist · `themes.md` / `CLAUDE.md` places that will need updating (list only; include decision 79 and the Border column note).
6. **User's visual checklist:** Title and Publisher shadow rows identical; Size/Color hex right edge flush with Blur right edge; Border Size/Radius flush with Blur/Depth (both edges); Search and Buttons Background at the Depth edge; Alignment combos at 178; Font combos ending at 170; Play Button Button Text, Nudge, Border and Hover States rows; Text Marquee Max size / Text margin / Border; Global tab Backgrounds, Fonts and Buttons border block; Random Game Display Text Font/Alignment/Wrap fully visible; swatch edges crisp; no clipping on any tab, with and without a scrollbar.
7. **STOP.**

## 9. Stop conditions (stop and report; do not improvise)

- Step 0 finds modified source files.
- **Checkpoint A fails** (Title does not render at the declared x after the change).
- A build error or new warning that is not an obvious slip from your last edit.
- Any control would clip or overlap, or a right edge would exceed 356 px; any label would truncate (especially "Vertical nudge (px)", "Alignment:" + Wrap Text, "Text margin (units)").
- Any change would need code-behind edits, a renamed/removed control, or a binding change.
- A `HorizontalAlignment="Left"` change collapses or shifts a row that relied on stretching.
- The rendered sweep shows an edge more than 1 px from its anchor: you may make **one** corrective round for an edit you got wrong; anything else gets reported for the user's decision.
- A capture precondition fails (process not running, Theme Builder not in front).
