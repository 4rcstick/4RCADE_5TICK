# HANDOFF: Theme Builder Final Spacing Tweaks (three small adjustments)

**Type:** gated implementation. UI width/position only.
**Order of work:** Step 0 repo check → Step 1 focused read-only audit → **STOP for the user's explicit "GO"** → Step 2 implementation → Step 3 one clean rebuild, static checks and report → **STOP**.
**Never commit or push.** Do **not** edit `themes.md` or `CLAUDE.md` (the user updates them after visual acceptance).

---

## 1. Goal and why

Three small visual adjustments found during final inspection, all in `Views/AdvancedThemesTabControl.xaml` (find things by tab `x:Name`, `x:Name` and label text, not by line number). No redesign, and nothing else changes: every binding, label, value, handler, `x:Name`, `Tag`, enable/disable relationship and every accepted layout stays exactly as it is.

**Source spec:** `theme_builder_final_spacing_tweaks.md` in the project folder root. Use that **exact filename**; do not rename, normalize or search for variants. If it is not found under that exact name, stop and tell the user where you looked. Read all of it.

**This pass supersedes two earlier decisions** (the user confirmed): the shared Border tab's hex boxes were "keep at 104 px" and "do not change the shared Border tab". Both are replaced by item C below. Everything else previously accepted stays.

## 2. Rules (do NOT)

- No Python. **Do not launch the application** or run any UI automation (the user tests visually). No commits or pushes. No edits to `themes.md` / `CLAUDE.md`.
- Use Read/Grep/Glob to inspect files; no shell pipelines. Shell is only for `dotnet build` and read-only `git status` / `git diff --stat`. Stay inside the project folder.
- Use the Edit tool; keep CRLF in `.cs` / `.xaml` and the `<!-- [SECTION: ... ] -->` fencing; replace stale comments. After each edit re-check that adjacent attributes are still separated by a space (an earlier pass had edits that dropped one).
- Never rename or drop any `x:Name`, `Tag`, handler, binding or `IsEnabled`/`ElementName` relationship. This pass should need **no code-behind change**; if you think it does, stop and say why.
- **Do not touch:** Game Titles (including its Nudge-type fields); any other Nudge field; Marquee → Shadow, including its "Apply shadow to" combo (its compact width is approved); the **local Border rows** in Text Marquee (`TxtBannerBorderSize`, `TxtBannerCornerRadius`) and Play Button (`TxtPlayButtonBorderSize`, `TxtPlayButtonCornerRadius`); the global color-control style; hex widths on any other tab; the consumers of the shared Border tab or their properties.

## 3. Step 0 — Repo check (read-only)

This work is meant to start from the user's committed tab-polish state. Run `git status`. If `Views/AdvancedThemesTabControl.xaml` (or any other source file) has uncommitted changes, **stop and tell the user** which files, and do not continue.

## 4. Step 1 — Focused audit (read-only), then STOP

Report, with actual numbers measured from the XAML (x positions measured from the tab content's left edge):

**A. Folders → Nudge** (tab `TabGLFolders`; the "Nudge (px):" label and its box on the Arrow color row)
- The Nudge box's current Width, x position, grid column and the column's width; where its label sits.
- Whether setting Width to 52 needs any column change to avoid clipping or moving the box's left edge or the label. Reference: the compact shadow row's Blur and Depth boxes are 52 px.

**B. Text Marquee → Font combo** (tab `TabMarqueeBanner`)
- The Font combo's current Width, x position, and how it sits in its grid (columns, any span).
- The Max size box's x and Width in the row below (expected x 118, width 52, right edge 170). The Font combo's new width = exactly what makes **its right edge equal the Max size box's right edge**, with the left edge unchanged. Give the number and the XAML edit it implies. (The user's visual estimate was about 10 px wider.)

**C. Shared Border tab** (tab `TabShapeBorder`)
- The grid's column definitions (expected 134 / 22 / Auto / 22 / Auto), which row each control lives in, and **every other row or control that uses these columns** (so nothing else shifts unintentionally). Include `PnlSBHoverBorder`, which spans all columns, the Random Game Display state panel (`PnlRGBorderState`, `CboRGBorderState`), and any other row in the tab.
- The names and current widths of **every color hex box in this tab** (the Border color hex, and the Hover border color hex if it exists in the tab), and the swatch size. Confirm which are 104 px.
- The plan to reach: Border color group 110 px (26 swatch + 4 + **80 hex**), 8 px gap, **Size 50 px at x 118**, 8 px gap, **Radius 60 px at x 176**, i.e. `110 / 8 / 50 / 8 / 60`, with labels still above their controls. Show how the other rows keep their current positions and behavior if they share those columns.
- Confirm no consumer's behavior is affected (shapes 01 to 04, custom layers, Splash Image, Video Preview, Search & Buttons, Random Game Display).

**D.** Anything in the spec or this handoff that conflicts with the code or would force a layout decision the user has not made.

Post a concise per-item plan, then **STOP and wait for "GO"**.

## 5. Step 2 — Implementation (only after GO)

**A. Folders → Nudge:** set the Nudge numeric input's Width to **52**. Keep its left edge, label, value, handlers, validation and behavior. Adjust a column only if the audit showed it is needed to avoid clipping, and only locally.

**B. Text Marquee → Font combo:** widen it by the derived amount so its **right edge aligns with the Max size box's right edge**. Left edge, Bold placement, header placement and behavior unchanged.

**C. Shared Border tab:**
- Border color hex box **104 → 80 px**. If the tab's **Hover border color** hex box is also 104, set it to **80** too (keep its position and behavior). Swatch size unchanged. Do not alter the global color-control style or any other tab's hex width.
- Realign the Border color / Size / Radius row to `110 / 8 / 50 / 8 / 60`: Border color group at x 0 (hex 80), **Size** box (Width 50) at **x 118**, **Radius** box (Width 60) at **x 176**. Radius extends 8 px past Depth's right edge by design; the goal is left-edge alignment. Labels stay above their controls. No overlap, no clipping.
- Any other row that shares these column definitions must keep its current position and behavior (use a row-local grid or span if needed).
- `TxtShapeBorderSize`, `TxtShapeCornerRadius`, the Border tab's other control names, bindings, ranges and `GetSelectedBorderEntry` stay untouched.

## 6. Step 3 — Verification and report

1. **One clean rebuild:** `dotnet build ArcadeStick.csproj --no-incremental`; **0 errors**; report any warning (a full rebuild previously reported 0).
2. **Static checks (no app launch):**
   - Every `x:Name`/`Tag`/handler touched is unchanged (grep the names above and the Border tab's control names).
   - Attributes properly separated; no broken XAML.
   - `git diff --stat` shows only the intended region of `Views/AdvancedThemesTabControl.xaml`, and no whole-file or line-ending rewrite (CRLF intact).
   - Confirm the local Border rows, Marquee Shadow and Game Titles are unchanged.
3. **Report in this form:** files changed · anything that did not match the instructions · anything changed beyond them · the computed new numbers (Nudge width, Font combo width, Border tab column widths and x positions) · the list below.
4. **User's visual checklist:**
   - Folders Nudge box is 52 px wide like Blur/Depth; its left edge did not move.
   - Text Marquee Font right edge = Max size right edge; Marquee Shadow's combo unchanged.
   - Shared Border: color hex 80 px (and Hover border hex 80 px if it was 104), swatch size unchanged; Size left edge at Blur's x, Radius left edge at Depth's x (Size 50, Radius 60); no clipping or overlap; the Hover border row and Random Game Display's state combo still look right.
   - Text Marquee and Play Button local Border rows and all other accepted layouts unchanged; settings still persist.
5. List (only list, do not edit) the `themes.md` / `CLAUDE.md` places that change with this pass (shared Border widths, hex 80, and decision 79).
6. **STOP.**
