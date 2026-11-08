# HANDOFF: Theme Builder — Comprehensive UI Geometry & Alignment Audit

**Type:** AUDIT ONLY. You change no source file and implement nothing.
**Order of work:** Phase 1 static audit → short interim note → **wait for the user to say the Theme Builder is open and focused** → Phase 2 rendered inspection under the privacy rules in section 4 → Phases 3 to 6 → write the report file → **STOP for the user's review**.
**Never commit or push.** Do **not** edit `themes.md` or `CLAUDE.md`.

---

## 1. Why this audit exists

Several polishing passes on the Advanced Theme Builder are done and committed, but a Photoshop inspection with fixed guides shows small inconsistencies in control widths, x positions, gaps and alignment across tabs. The user does not want another series of isolated one-pixel fixes. They want **one systematic geometry audit of the whole Theme Builder** and a **single coherent alignment standard** they can approve before any implementation.

The cases below are **examples of a wider problem, not the full scope**:
1. **Game Info → Play Button → Border:** the local Border Size and Radius boxes do not appear to line up with the Blur and Depth boxes above them.
2. **Game Info → Title and Publisher:** the shadow controls appear at different horizontal positions. The cause may be the preceding color-control widths or grid spacing.
3. **Other Font / Color / Shadow combinations:** hex-field right edges versus Blur / Depth fields are inconsistent in several tabs.

Do not stop after the first few issues. The goal is comprehensive coverage, down to one pixel.

## 2. Alignment principles and standing decisions

1. **Common anchors:** equivalent control groups use consistent horizontal reference positions.
2. **Flush edges:** left and right edges line up across comparable rows and tabs wherever visually appropriate.
3. **Consistent gaps:** repeated relationships use consistent spacing; every difference needs a reason.
4. **Preserve intentional exceptions:** not every control needs identical dimensions.
5. **No cascading fixes:** do not fix one alignment by breaking another. Prefer **shared grid definitions and layout patterns** over unrelated individual offsets.
6. **Preserve functionality:** no changes to behavior, bindings, validation, visibility logic or persistence.
7. **Visual judgment:** perfect numerical uniformity is not the goal if it makes a good layout worse. Do not resize controls just to make every gap equal.
8. **Hex fields stay at about 80 px** (the user is satisfied). Gaps around color controls, and the gaps between Font Size and Color, may change if needed.
9. Some small numeric fields use 50, 52, 53 or 60 px. Whether to standardize them is **undecided**: determine the real XAML widths and rendered dimensions first. Do not automatically force Size, Radius, Blur and Depth to one width.
10. **The central gap between Width and Height in the Position & Size layouts is a visual anchor.** Preserve it, including layouts with ratio-lock buttons.
11. **Do not silently replace previously approved designs.** Examples of approved things: section divider spacing (`Margin="0,15,0,10"`), the two Favorites Star dividers, compact Drop Shadow groups, the Search and Marquee Shadow standalone-toggle exceptions, 50 px Border Size and 60 px Radius, header-row Bold/Wrap checkboxes.

## 3. Starting facts (from earlier audits; **verify, they may be stale**)

- Files: `Views/AdvancedThemesTabControl.xaml` and `.xaml.cs` (the layer tabs, mostly hand-written XAML, with some code-built UI such as the per-part Position rows and one header row near `CS:1560-1580`), `Views/GlobalThemesTabControl.xaml` (the Global tab and its subtabs), `Views/ThemesManagerControl.xaml`, `Views/ThemeBuilderWindow.xaml`, `App.xaml` (Chrome Palette, `GLLabelStyle` with default Margin `0,10,0,3`, shared styles).
- Pane: the Theme Builder is about 717 x 885; tab content is about 385 to 397 px wide, less the tab margins (10 px each side) and a vertical scrollbar where one shows.
- Compact Drop Shadow rows use columns **110 | 8 | 52 | 6 | 52**: Blur at x 118 (width 52), Depth at x 176 (width 52). x is measured from the tab content's left edge inside the tab's 10 px margin.
- Older tabs: hex boxes 80 px, swatch 26 px plus a 4 px gap (a 110 px color group); Size column 48 plus a 14 px gap. The shared Border tab was just changed to `110 / 8 / Auto / 8 / Auto` with 80 px hex, Size 50 at x 118, Radius 60 at x 176. Position & Size: boxes 50 minimum, 12 px gaps, 36 px lock column. Text Marquee Font combo 170, other Font combos 160. Search Font/Alignment grid `48 | 14 | 110 | 14 | 110`.
- Known: a 60 px field inside a 52 px grid column is clipped; a WPF element with an explicit Width wider than its slot is cut off.
- The app uses Per-Monitor V2 DPI, so physical pixels, device-independent units (DIPs) and screenshot coordinates can all differ.

## 4. Rules, safety and privacy (binding)

- **No source edits, no Python, no commits.** Use Read/Grep/Glob for source. Shell only for read-only `git status` / `git diff --stat`. Stay inside the project folder.
- **Files you may create:** the report (section 6) and temporary screenshots only, both inside the project folder (section 4.2).
- **The user launches the application and brings the Theme Builder to the front. You do not launch it.**

### 4.1 Rendered-inspection gate (Phase 2)
You are authorized to use the mouse and screenshots on the Theme Builder, **but only after** the user confirms the Theme Builder is open and focused. The user keeps personal windows open (browser, chat apps), so:
1. Before the first capture or click, say exactly **what you will capture and which window**, and wait for the user's "go". The user closes personal windows first.
2. Capture **only the launcher's own window (the Theme Builder window)**, never the full screen or a screen region. Send input only to that window.
3. **Before every capture, click or scroll:** confirm the launcher process is running and the Theme Builder window is in front. If either check fails, **abort at once**: capture nothing, send nothing, and report it. Never fall through to the next step.
4. Do not look at, describe or act on any other window or app.
5. If screenshot or mouse tooling is unavailable, or it can only capture the whole screen, **say so and complete the static audit**; do not pretend a visual inspection happened.

### 4.2 Screenshots
Save only to a temp folder inside the project (for example `.audit-temp/`), view only the ones you need, and **delete them all when finished**. Never commit them. Use zoomed captures of small regions of the Theme Builder window for edge measurement.

### 4.3 What mouse interaction may and may not do
- **Allowed (navigation and inspection only):** select layers in the layer list, click tabs and subtabs, scroll panels, hover for tooltips, switch the top-level Theme Builder tabs.
- **Not allowed:** typing into fields, toggling checkboxes, dragging, changing a combo, picking colors, changing the resolution or Theme sizes, and clicking **Save, Reset, Restore Defaults, Delete, New Theme, Load** or any other destructive or persistent control. Do not change saved settings, theme files or project data.
- If a control cannot be inspected without changing application state (for example a panel visible only while a checkbox is on), **document the limitation**; derive its geometry from XAML instead.

### 4.4 Measurement honesty
- XAML-derived numbers (calculated from the full parent layout: grid columns and spans, margins, padding, alignment) are **authoritative**. Do not infer a position from a control's Width alone.
- Rendered numbers from screenshots are **estimates, about ±1 px**, and must be labeled so. Account for DPI and display scaling, control borders, and the difference between **inclusive screenshot pixel counts and WPF control widths**. Do not mistake antialiasing or capture artifacts for layout errors.
- Clearly separate **verified in XAML** from **verified through screenshots**, and **confirmed** from **estimated**.

## 5. Audit method

### Phase 1 — Static XAML geometry audit
Inventory **every relevant tab, subtab and control group**. For each control record: name and location; declared Width/Height; effective parent grid or panel; column definitions and spans; margins and padding; Horizontal/VerticalAlignment; **calculated left and right x**; gaps to neighbors; and what else could move if it changes.

Cover at minimum:
- **Layer editors:** Main Window Background; Games List (Folders, Game Titles); Shape Layers 01 to 04 and custom layers (Background, Border); Splash Image; Video Preview; Marquee (Splash Logo/Logo, Text Marquee, Shadow); Search & Buttons (Search, Buttons, Border); Game Info (Title, Publisher, Game Info, Artwork Previews, Play Button, Favorites Star); Random Game Display (Text, Content, Border); Scrollbars (Games List, Game Info); the shared Position & Size layouts for each layer type (including group move, ratio-lock buttons and multi-select), and the shared Border tab.
- **Theme Builder main controls:** the Themes Manager row, the resolution and sizes controls, the guides and Background grid rows, the Layers header and layer list, the Theme Info view, and the Global tab and its subtabs (Fonts, Backgrounds, Tabs, Buttons, Scrollbars, Context Menus).
- **Repeated families:** Font / Size / Color; Color / Blur / Depth; Border Color / Size / Radius; hover-state rows; numeric boxes (X, Y, Width, Height, Size, Radius, Blur, Depth, Nudge, margins); hex + swatch pairs; checkboxes and header rows; dividers and section spacing; shared styles and templates.

If this is too large for one pass, split it into sections but keep a **master inventory** so no tab or control group is skipped. After Phase 1, post a short interim note (what is covered, what the main structural findings are) and **wait for the user's go for Phase 2**.

### Phase 2 — Rendered UI inspection
Only after the gate in 4.1. Visit every accessible relevant tab and subtab; compare the rendered result with the XAML calculations; measure boundaries and compare equivalent rows across tabs; note anything static analysis missed. Mark every measurement as an estimate.

### Phase 3 — Alignment matrix
By layout family, show: current widths; current x positions; left and right edges; current gaps; observed discrepancies; recommended common anchors; the change needed. Identify each inconsistency to the nearest reliable pixel or DIP.

### Phase 4 — Root-cause analysis
Decide whether each discrepancy comes from different field widths, different grid definitions, column spacing, margins, alignment properties, different container structures, shared styles/templates, or rendering/DPI effects. **Look for a single well-chosen adjustment that resolves several discrepancies**; do not propose dozens of one-off corrections if the cause is a shared layout inconsistency.

### Phase 5 — Proposed alignment standard
Recommend one coherent system: standard x anchors and spacing per control family, preserving 80 px hex fields wherever practical; whether standardizing the small numeric widths would help (do not force Radius, Blur, Depth and Size equal if their differences are useful); every proposed change to an existing dimension shown explicitly; legitimate exceptions listed with the reason they stay.

### Phase 6 — Risk and dependency review
For every recommended adjustment: which controls move or resize; whether neighboring rows share the affected grid; whether any label or control could clip; whether another tab, layer or consumer is affected; whether an approved layout changes; whether code-behind must change. Prefer localized, predictable changes.

## 6. Deliverables

Write the full report to **`theme-builder-geometry-audit.md`** in the project folder root (a new file, LF line endings; it is the only file you create besides temporary screenshots), and post a concise summary in chat. It must contain:
1. **Executive summary:** overall findings and recurring causes.
2. **Complete geometry inventory:** by tab and layout family.
3. **Alignment matrix:** current versus recommended positions and widths.
4. **Discrepancy register:** every issue with location, magnitude and suspected cause; mark confirmed versus estimated; mark XAML-verified versus screenshot-verified.
5. **Proposed common standards:** shared anchors, widths, spacing.
6. **Dependency and regression assessment** for each adjustment.
7. **Implementation strategy:** a minimal, coordinated sequence (the user prefers the fewest practical passes).
8. **Questions requiring the user's approval:** design choices that cannot be resolved objectively.
9. **Limitations:** anything that could not be inspected and why; whether rendered inspection was possible.

## 7. Finish

Delete all temporary screenshots and say so. Confirm that no source file was modified (`git status` / `git diff --stat` shows only the new report file, plus the untracked handoff files already present). **STOP and wait for the user's review.** Do not produce or run any implementation until the user approves the proposed alignment standard.
