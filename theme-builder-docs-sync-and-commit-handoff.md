# HANDOFF: Theme Builder — Final Documentation Sync and Commit Preparation

**Type:** documentation only, then a gated Git commit and push.
**Flow:** Step 0 Git status → Step 1 read documents and source → Step 2 stale/contradiction inventory → **STOP for the user's "GO"** → Step 3 edit `themes.md` and `CLAUDE.md` only → Step 4 verification → Step 5 report → Step 6 Git status and proposed commit scope → **STOP for the user's explicit commit approval** → Step 7 commit and push (only after that approval).

**Application source code must not be changed in this pass.**

---

## 1. Goal and why

The Advanced Theme Builder's tab-polishing passes (tab polish, visual cleanup, final spacing tweaks) and the final geometry-alignment pass are finished. The user has built them with 0 errors and 0 warnings and visually accepted the result. `themes.md` and `CLAUDE.md` still describe earlier states (for example decision 79's hex and border widths, the old Border grid columns, old tab names). Bring both documents fully in line with the **current code**, keep their useful history, and then prepare one clean commit and push.

**Trust order (from `CLAUDE.md`):** the code first, `themes.md` second. Where the geometry audit (`theme-builder-geometry-audit.md`) or the alignment handoff (`theme-builder-alignment-implementation-handoff.md`) disagree with the final source, **the source wins**. Those two files are references for what was intended and measured, not for what is true now. Use the exact filenames.

## 2. Rules

- Use the Read, Grep and Glob tools to inspect files. Shell only for `git status`, `git diff`, `git log`, `git add`, `git commit`, `git push` (and only the commit/push ones at Step 7). No Python. Do **not** launch the application. Stay inside the project folder.
- **Edit only `themes.md` and `CLAUDE.md`.** Both are LF (`*.md text eol=lf`). Use the Edit tool for targeted changes; no whole-file rewrite, no line-ending change. After editing, `git diff --stat` must show only these two files, with line counts proportionate to the edits (no whole-file change).
- Preserve each document's structure, section numbering, heading style, table formats, cross-references (for example "themes.md 3.24", "decision 77") and tone. Do not rewrite unrelated documentation. Do not reorder sections.
- **Do not silently delete history.** Where a later decision supersedes an earlier one, keep the earlier text and add a short, clearly marked supersession note in the existing style (for example the **Update 2026-10-22:** pattern already used in decision 50), pointing to the new decision number. Remove or rewrite text only when it is a *current-state description* that is simply wrong now (a layout table, a "currently" statement, a status list); the change log keeps its dated entries and gets a new one.
- **Commit exception.** `CLAUDE.md` says "Never commit". For this handoff only, committing and pushing is authorized **solely at Step 7 and only after the user's explicit approval in reply to your Step 6 report**. Do not commit or push earlier, and do not treat any other wording as approval. Do not change that "Never commit" rule in `CLAUDE.md`.
- Date entries with today's date as reported by the environment (the user is in America/Los_Angeles); if you cannot determine it, ask in the Step 2 report.

## 3. Step 0 — Git status

Run `git status`, `git log -5 --oneline`, and `git branch --show-current`. Report: current branch, upstream, anything modified or untracked. Expected: tracked source files modified by the alignment pass may be uncommitted (the user said earlier passes were committed; the alignment pass may or may not be). Untracked scratch items may exist (`.audit-temp/`, `.align-temp/`, the handoff `.md` files, the audit report). Do not modify, delete or move any of them. Do not stage anything yet. Continue to Step 1 (no stop here unless `git status` shows something alarming, such as merge conflicts or a detached HEAD, in which case stop and report).

## 4. Step 1 — Read documents and relevant source

1. Read **`CLAUDE.md`** fully and **`themes.md`** fully (it is large; read in segments with Read offset/limit). Note section numbering (sections 1b, 3.x, 6b, 7, 8, file map, "Last verified" line), the decisions list (decision 79 and any after it), and the change log.
2. Read `theme-builder-geometry-audit.md` and `theme-builder-alignment-implementation-handoff.md` for the intended standard.
3. Read the **current source** that defines the Theme Builder geometry and tab structure. Find controls by `x:Name`, tab names and header text, not remembered line numbers:
   - `Views/AdvancedThemesTabControl.xaml` (and `.xaml.cs` for tab tables, `GetLayerTabs` / `AllLayerTabs`, `GIBlocks` only as needed to confirm names)
   - `Views/GlobalThemesTabControl.xaml`
   - `Views/ThemesManagerControl.xaml`, `Views/ThemeBuilderWindow.xaml` (header rows, guides line)
   - `App.xaml` only if geometry-related styles changed
   - Use `git log` / `git diff <last-documented-commit>..HEAD --stat` (read-only) to see which view files changed since the docs were last updated, if that helps scope the audit.
4. **Extract the actual implemented facts** (do not assume the handoff was applied exactly; verify each):
   - Column definitions of the compact shadow grids, Size/Color sub-grids, Search and Buttons grids, Publisher / Random Game Display Text Font-Alignment grids, Play Button text/nudge, Border and Hover States grids, Text Marquee grids, the shared Border tab, and the Global tab grids (Backgrounds, Fonts, Buttons border block, Scrollbars, Context Menus).
   - Widths of: small numeric boxes (Size, Blur, Depth, Max size, Text margin, Nudge, gap to scrollbar, star size, Scrollbars Width, Global font sizes, Border Size and Radius), hex boxes (80 and every exception), Font combos (170), Alignment combos, opacity box, resolution box, Position & Size boxes.
   - Where `HorizontalAlignment="Left"` is applied for the layout-rounding correction, which grids, and the **Text Marquee Max size exception** (whatever the source shows: which grid does *not* get it, or gets different treatment, and why, from the comments and the audit).
   - Whether `SnapsToDevicePixels="True"` is on the color swatch Borders (count them; are there any that lack it?).
   - Header-label minimum widths (Themes Manager "Active Theme:", Theme sizes, Resolution; and any other header label given a `MinWidth`/`Width`): exact values and the elements they apply to.
   - Dividers: the `Separator` margins (`0,15,0,10` and the two Favorites Star dividers at `0,10,0,10`), and the count and locations.
   - Position & Size layouts, ratio-lock arrangement (36 px lock column, MinWidth 50, `6 12 6` groups) as they stand.
   - Tab names and order per layer (including the earlier polish changes: Video Preview Background tab removal and Background color moved to Fallback Image with its (?) tooltip, Marquee tab names/"Splash logo image:", "Splash logo only", Game Info tab names/wrapping, Random Game Display Text order, "Background color state:", Content tab order, Play Button shadow-label changes, "Message (leave empty for no text):", etc.). Confirm each from the XAML, do not take this list on trust.
   - Any new tooltips (the Video Preview Background (?) tooltip) and how they are built, because `CLAUDE.md` currently says the Layers "?" tooltip is the **only** styled tooltip.

## 5. Step 2 — Inventory of stale or contradictory descriptions (read-only), then STOP

Produce a numbered inventory, grouped by document and section, of every place that is stale, contradictory, or missing. For each: where it is (section/decision/line), what it says, what the code says (with file and control names), and the proposed action (**update in place** / **add supersession note** / **add new text** / **leave, historical**). Do not edit yet.

It must at least check and report on these areas:

**A. Decisions and history**
- **Decision 79** (and every decision after it) and any decision that states small-numeric, hex, border, combo or color-group widths (for example hex 104, Border Size 50 / Radius 60, 36 px border, 22 px gaps, `110 / 22 / 50 / 22 / 60`, 48 px size boxes, 160 px combos, 6 px gaps, Depth at 176). Identify which are superseded, by which pass, and propose supersession notes rather than deletions.
- Decisions about the dividers, Marquee / Game Info naming, the Video Preview Background tab, the Random Game Display Content State, the Play Button shadow labels: confirm they match the code, note any missing decision.
- Decide whether the geometry standard needs **one new numbered decision** (recommended: a single decision "Theme Builder alignment standard", numbered next in sequence, dated, listing the rules below) or whether it belongs in an existing section only; propose, do not guess.

**B. Layout tables and current-state descriptions**
- Any table or prose in `themes.md` that gives tab grid columns, widths, anchors or Border column definitions (including the shared Border tab grid comment `110/8/50/8/60`-style text, the Global Backgrounds/Buttons border rows, the Play Button and Text Marquee border grids).
- The per-layer tab lists in section 3 (3.x) and the Theme Builder Status block in `CLAUDE.md`: tab names, order, which tabs exist.
- The file map and the "Last verified" line.
- Section 7 (traps): anything about width, spacing or layout rounding that should now include the `HorizontalAlignment="Left"` trap.

**C. `CLAUDE.md`**
- Any input-width or spacing guidance (search for 48, 50, 52, 60, 80, 104, 160, 170, "px", "width", "hex", "Blur", "Depth", "Border"). If none exists, say so and propose where the short geometry rules belong ("Known Gotchas" → a new "Theme Builder geometry (alignment standard)" subsection is the likely home; propose it).
- The tooltip rule ("the Layers ? tooltip is the only styled tooltip") versus the current tooltip(s) in the XAML.
- "Theme Builder Status (beta.4)" tab names and the Video Preview "(border, background, fallback image)" wording.
- The editing rules: confirm nothing there contradicts this pass's workflow; do not change the Never-commit rule.

**D. Change log**
- Which passes already have change-log entries (tab polish, visual cleanup, final spacing tweaks, geometry audit, geometry alignment) and which are missing. Propose entries (one per pass or one combined, matching the log's existing granularity and style).

**E. Documented-but-not-implemented, or implemented-but-not-documented**
- Anything from the alignment handoff's optional Checkpoint F (swatch `SnapsToDevicePixels`, header-row label widths, multi-select panel margin) that was or was not actually done. Document only what the source shows.
- Any deviation of the final source from the alignment handoff's anchors (x0, x60, x118, x170, x178, x230, x236) or grids; report each so the docs follow the source.

Also list any **unresolved inconsistencies** you cannot settle from the source (the user decides). Then **STOP and wait for the user's "GO"**. Do not proceed to Step 3 without it.

## 6. Step 3 — What the documentation must say (after GO)

Document the standards **as implemented** (verify every number against the source from Step 1; if the source differs from the numbers below, write the source's numbers and say so in the report). Put the geometry standard once in `themes.md` (a dedicated subsection or the new decision plus the layout table) and a short, rule-style summary in `CLAUDE.md` pointing to it ("details in themes.md ...").

Content to cover:

1. **Small numeric fields are 52 px** (Blur, Depth, Size, Nudge, Max size, Text margin, gap to scrollbar, star size, Scrollbars Width, Global font sizes, **Border Size and Radius at 52 px each**), with the justified exceptions (opacity box 43, custom resolution box 70, Position & Size boxes, and any other exception the source shows).
2. **Hex fields are 80 px**, with exceptions (shape/custom Content background hex 104 and its related group, Buttons hover hex 140, any other the source shows).
3. **Font combo boxes are 170 px** (x0 to x170); Alignment combos (110) at x178; combo height 25 versus box height 27 left as is.
4. **Alignment anchors** (x from tab content left; usable width 358 with a scrollbar, 374 without): x0, x60, x118, x170, x178, x230, x236, with what each is used for (for example: x60 color group after a Size box; x118 Blur/Border Size/Max size start; x170 shared right edge of Size/Color hex, Blur, Font combo, Max size and Button Text; x178 Depth, Border Radius, Alignment combo, Background group; x230 right edge of Depth/Radius/nudge; x236 Play Button hover group 3). State that screenshots are ±1 px estimates and XAML is authoritative.
5. **Spacing rule:** 8 px between groups and boxes in the compact family (`110 8 52 8 52`), 4 px between a color swatch and its hex box (110 px color group = 26 swatch + 4 gap + 80 hex). The supersession of the old 6 px gap and Depth at 176.
6. **Final grid definitions** per family (compact shadow / Border / Max size / Text margin / Nudge; Size/Color `52 8 Auto`; Search and Buttons `52 8 110 8 110`; Publisher / RG Text `170 8 Auto`; Play Button text/nudge `170 8 52`, Border `110 8 52 8 52`, Hover States `110 8 110 8 110`; Text Marquee; shared Border tab `110 8 52 8 52`; Global Fonts `120 8 52 14 Auto`; Global Buttons border block margin 128), **as the source actually has them**.
7. **Layout-rounding correction:** the audit's root cause (a grid with `UseLayoutRounding` own or inherited, no `*` column, arranged wider than needed, renders about 1 px wider per column), the fix (`HorizontalAlignment="Left"`), the rule for where it applies and where it must **not** (grids with a `*` column or stretch-dependent children), the **Text Marquee Max size exception** exactly as the source implements it, and the instruction to apply the same rule to any new grid. Add it to `themes.md` section 7 (traps) and as a short line in `CLAUDE.md`.
8. **Pixel snapping on color swatches** (`SnapsToDevicePixels="True"`), as implemented, plus the rule for new swatches.
9. **Header-label minimum widths** (exact elements and values from the source) and the intent (controls share a left edge).
10. **Preserved items:** all Position & Size layouts and ratio-lock relationships (36 px lock column, MinWidth 50, `6 12 6` non-lock groups, center gaps), the approved dividers (`Margin="0,15,0,10"`; the two Favorites Star dividers at `0,10,0,10`), standalone-toggle shadows, Scrollbars and Global label-left families (their colour columns at 128 / 252), Favorites Star "Star size:" label-left, and the other intentional exceptions the source shows. State they were deliberately left, so a future pass does not "fix" them.
11. **Earlier tab-polish changes** not yet documented (names, order, wrapping, compact Drop Shadow recipe with the checkbox replacing the "Shadow color:" label and Blur/Depth on the same row, "Background color state:", Video Preview Background tab removal with the (?) tooltip, Marquee/Game Info names, the themed `Separator` dividers, Content tab order, and so on), added to the matching 3.x sections and the change log.
12. **Tooltip rule in `CLAUDE.md`:** if the XAML now contains a second styled tooltip (the Video Preview Background (?) tooltip), widen the "only styled tooltip" sentence to match the code exactly, without adding a global or implicit ToolTip style.
13. **Supersession of decision 79 and the Border column notes:** keep the original, add the marked update with the new widths (hex 80, Border Size/Radius 52/52, column `110 8 52 8 52`), and fix any current-state table or comment.
14. **Change log:** new dated entries for the passes that lack them, in the log's existing style (what changed and where, no code excerpts), including the geometry audit and the alignment pass, with counts only if you verified them in the source (for example the swatch count).
15. Update the **file map** and the **"Last verified"** line only for things you actually verified in this pass.

Do not invent rationale, measurements or counts that are not in the source or the audit. If something is uncertain, mark it in your report instead of writing it.

## 7. Step 4 — Verification (after editing)

1. **Match against code:** for every geometry statement you wrote, re-check it against the XAML and list any you could not confirm (do not leave unconfirmed numbers in the docs).
2. **Structure check:** headings, numbering, tables, cross-references (decision numbers, "themes.md 3.x") still resolve; no duplicated decision numbers; supersession notes point to the right decision numbers; no broken markdown tables.
3. **Contradiction sweep:** Grep both documents for the old values you superseded (`104`, `50px`, `60px`, `48`, `160`, `22`, `176`, `110/22/50/22/60`, "three Favorites Star dividers") and confirm every remaining hit is either historical (and marked as superseded) or a legitimate exception.
4. **Diff review:** `git diff --stat` and `git diff -- themes.md CLAUDE.md`. Confirm: only these two files changed by you; no accidental deletions of useful content; LF line endings intact; no whole-file changes; no trailing-whitespace or encoding damage. Confirm no `.cs` / `.xaml` file was modified by you in this pass (if source files show as modified, they are from the earlier alignment pass; say so, do not touch them).
5. Build is **not** required for a documentation-only change, but if any source file shows in the diff that you did not touch, report it and do not proceed to Step 6 without the user's decision.

## 8. Step 5 — Report (then continue to Step 6 in the same message)

Report: sections updated in each document (by section/decision number); decisions superseded and the notes added; change-log entries added; places where code and the audit/handoff differed and which you followed; **unresolved inconsistencies**; anything that did not match these instructions; anything changed beyond them.

## 9. Step 6 — Final Git status and proposed commit scope, then STOP

**Do not commit or push.** Run `git status` (full), `git diff --stat`, and `git log -3 --oneline`, and report:

1. The branch, the upstream, and whether the push target is correct.
2. A table of every changed or untracked path, each with a proposed disposition:
   - **Include:** intended source changes (the `Views/*.xaml` and any `.cs` changed by the alignment and polish passes that are not yet committed), `themes.md`, `CLAUDE.md`.
   - **Exclude:** temporary screenshots and capture folders (`.audit-temp/`, `.align-temp/`, any `*.png` scratch), the audit and handoff scratch files (`theme-builder-geometry-audit.md`, `theme-builder-*-handoff.md`, `tab-polish-*`, spec `.md` files, `*-audit-report*` and similar), logs, `bin/`, `obj/`, `.vs/`, `.claude/settings.local.json`, and anything else that is not part of the intended source and documentation. If you are unsure whether a file is intended (for example an untracked `.md` the repo may track), list it under "needs your decision" instead of choosing.
   - Check `.gitignore` and report whether the exclusions are covered by it or must simply be left unstaged.
3. A **proposed commit message** (one commit, summary line plus a short body listing: tab polish and cleanup passes, final spacing tweaks, geometry alignment standard, docs sync), ending with the attribution line the session instructs (see section 10).
4. The exact commands you would run (`git add` with explicit paths, never `git add -A` or `git add .`; `git commit`; `git push`).

Then **STOP and wait for the user's explicit approval** ("approved, commit and push" or equivalent). Questions, partial feedback or silence are not approval. If the user changes the scope, restate the final scope and wait again.

## 10. Step 7 — Commit and push (only after explicit approval)

1. Re-run `git status` and confirm nothing changed since the approved report (new files, new modifications). If anything differs, stop and report instead of committing.
2. Stage **only** the approved paths by name. Verify with `git diff --cached --stat` that the staged set equals the approved set and contains no screenshots, scratch or temp files.
3. Commit as **one commit** with the approved message. Use the attribution line from the session instructions: `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` and `Claude-Session: https://claude.ai/code/session_01DGziRtcR4M3RgxfZNUvC1j`, each on its own line after a blank line at the end of the message.
4. Push to the approved upstream with a plain `git push` (no `--force`, no `--no-verify`). If the push is rejected or needs anything beyond a plain push, stop and report.
5. Report: commit hash, files in the commit (`git show --stat`), push result, and the final `git status` (which should show only the intentionally excluded untracked items). **STOP.** Do not start any other work.

## 11. Stop conditions (stop and report; do not improvise)

- `git status` shows conflicts, a detached HEAD, or an unexpected branch.
- A file needed (`themes.md`, `CLAUDE.md`, the two reference `.md` files, the view XAML files) is missing: say which and where you looked; do not search elsewhere or use an older copy.
- A documented fact cannot be confirmed from the source and the docs would have to guess.
- Your edits would change a decision's meaning rather than annotate it, or would require deleting historical content.
- `git diff` shows changes to files other than the two documents that you made, or any whole-file/line-ending change.
- Any step that would stage, commit or push before the user's explicit approval.
- Staged set differs from the approved set.
