# Layer properties polish, pass 2: grouped layers' Position & Size tabs

Follow-up to `layer-tabs-polish-pass1.md` and `layer-tabs-polish-pass1-tweaks.md` (both built, tested and committed). Read `CLAUDE.md` first, then `themes.md` (sections 3.7, 3.8, 3.12, 3.16, decision 79 and the pass 1 change log entries), then `Views/AdvancedThemesTabControl.xaml` and `Views/AdvancedThemesTabControl.xaml.cs`. Where the code and `themes.md` disagree, the code wins: report it and correct `themes.md` at the end.

This follows the usual pass structure: **STEP 1 is a read-only audit. Report, then stop and wait for the user to say "go".** STEP 2 (implementation) starts only after "go". One pass; stop at the end for the user to build, test and commit. **Never commit.**

---

## 1. Goal and why

Bring the Position tabs of the three grouped layers into line with the conventions set in pass 1 (tab name, labels below the boxes, Hide checkboxes handled consistently). Pass 1 left these pages exactly as they were; they are built in code by `BuildGroupParts` and are the only Position pages not yet reworked.

**Layers in scope:**
- Search & Buttons (`LayerRegistry.SearchAndButtonsGroup`)
- Game Info (`LayerRegistry.GameInfoGroup`)
- Random Game Display (`LayerRegistry.RandomGameGroup`)

This is a layout and display-text pass. **No behavior, binding, validation, range, handler, serialization or theme load/save changes.**

---

## 2. Changes wanted

### 2.1 Tab name

The Position tab header becomes **POSITION & SIZE** on all three grouped layers. Pass 1 set the header per layer type in `ApplyLayerTabs` (single layers "POSITION & SIZE", group pages "POSITION"). After this pass every page uses the same text, so that condition is no longer needed: audit how best to make the header one plain "POSITION & SIZE" (for example set it once in the XAML and remove the per-layer line in `ApplyLayerTabs`), confirm nothing else depends on it, and update that method's summary comment. Single-layer pages must keep their pass 1 layout and behavior exactly.

### 2.2 Section headers and Hide checkboxes

Each individual part (component) has a section header and a Hide checkbox.

- Move each Hide checkbox onto the **same horizontal row as its section header**, immediately to the right of the header text.
- **12 px horizontal gap** between the end of the header text and the checkbox square.
- Keep the existing spacing between the checkbox square and its "Hide" label.
- The checkbox follows the actual header width (no fixed horizontal position).
- Checkbox and its label are vertically centered with the header.
- Keep all Hide behavior, bindings, handlers and names as they are.

### 2.3 Position and Size field row (every part of the three groups)

- X, Y, Width and Height stay on **one** horizontal row.
- Labels move **below** their boxes, **centered** under each box, **without colons**.
- Keep the existing field widths and their stretch behavior (do not apply pass 1's 50 px-minimum star sizing here; report the current widths in the audit).
- **Search & Buttons and Game Info:** double **only** the horizontal gap between **Y and Width**. The X-to-Y and Width-to-Height gaps stay as they are. Adjust the grid as needed so the wider middle gap causes no horizontal overflow.
- **Random Game Display:** match the pass 1 spacing exactly and do **not** double the Y-to-Width gap. That means the same column structure as the single-layer row (`PnlRectFields`): equal spacing between X, Y and Width, 12 px spacing columns, and the part's ratio lock in its own fixed 36 px column between Width and Height, vertically centered with the boxes, exactly as the pass 1 lock sits on the single-layer pages. Keep the lock's behavior (the session-only `_partRatioLocks` state, per-part ratio keeping in `ApplyPartField`) untouched. Propose the exact columns in the audit; keep the field widths themselves as they are today.
- Keep a consistent look and spacing across all three groups. Do not change validation, value ranges, handlers or functionality.

### 2.4 "Move all parts together"

Search & Buttons and Game Info have the `ChkGroupMove` checkbox. Keep it at the top of the page with its current position, styling and behavior. Random Game Display does not get it.

### 2.5 Section spacing and separators

- Keep the existing vertical spacing between part sections.
- **Do not add separators** in this pass. The headers with their adjacent Hide checkboxes provide the grouping; the user will judge separators after seeing the rendered pages.

### 2.6 Everything else stays

- The Random Game Display preview card and "Preview as" controls above the tabs stay as they are.
- All other tabs stay unchanged. Input styles and theme colors stay.
- Do not touch the single-layer Position & Size pages from pass 1, the Border tab, or the Content tab.

---

## 3. Current behavior (from themes.md; verify against the code)

- One shared `TabPosition`. Single layers show `PnlRectFields`; group pages collapse it and use `PnlGroupParts` (built by `BuildGroupParts`), plus `ChkGroupMove` for groups whose `LayerGroupDefinition.AllowGroupMove` is true (Search & Buttons, Game Info).
- Groups with `AllowRatioLock` (Random Game Display only) get one lock per part, built by `BuildGroupParts` in a narrow column between Width and Height; lock states live in `_partRatioLocks` by part id.
- `ApplyPartField` handles part field edits, including group move (an X or Y edit moves every other part by the same delta; Width and Height never propagate) and the ratio keeping.
- Pass 1 moved `ChkLayerHidden` and `PnlVisibilityMode` above the single-layer position row; both are collapsed on group pages, so the group Hide checkboxes are the ones built per part by `BuildGroupParts`.

---

## 4. STEP 1: read-only audit (report, then stop)

Do not edit anything. Report:

1. **Current `BuildGroupParts` output** for each of the three groups: how each part's header, Hide checkbox, X/Y/Width/Height row and (Random Game Display) lock are built today, their column structure, field widths, gaps, margins and vertical spacing, and which code is shared across the three groups versus per-group.
2. **How each Hide checkbox is created and wired** (names, handlers, Tag or part id, bindings) and any name-keyed handler table or naming convention that must not break (CLAUDE.md warns about name-keyed handlers; confirm none of the Position page's controls are looked up by name from elsewhere).
3. **Proposed Hide row:** the structure you would use for header + 12 px gap + checkbox, vertically centered, following the real header width; and confirmation that the checkbox-to-"Hide"-label spacing is unchanged.
4. **Proposed field row** for Search & Buttons and Game Info (labels below, centered, no colons, doubled Y-to-Width gap) and for Random Game Display (pass 1 spacing with its lock column). Give the **horizontal fit** at the panel's available width (about 375 px per the pass 1 audit; re-measure): row width needed versus available for each group, especially Search & Buttons and Game Info with the doubled gap. If anything would overflow, report the shortfall instead of squeezing boxes or changing the window.
5. **Header rule:** how the Position tab header becomes one "POSITION & SIZE" for every layer, and what in `ApplyLayerTabs` and the XAML changes.
6. **Vertical spacing:** how the labels moving below the boxes, and the Hide checkboxes moving up beside the headers, change each part's height, so the user knows what to expect (the existing vertical spacing between part sections is to be preserved).
7. **Conflicts and mismatches** between this spec and `CLAUDE.md` / `themes.md` / the code, and anything ambiguous. Code wins; list what `themes.md` will need.
8. The docs you plan to update (see section 6).

Then stop and wait for "go".

---

## 5. STEP 2: implementation (only after "go")

Order edits so the end state builds clean (no transient errors are expected). Keep the comment fencing and style: C# `// [SECTION: ...]` / `// [END SECTION: ...]`, XAML `<!-- [SECTION: ...] -->` / `<!-- [END SECTION: ...] -->`, and an accurate one-line `// Does X` summary on every method you add or touch (replace stale comments, do not stack redundant ones).

### Constraints and things NOT to touch
- Keep every `x:Name`, event handler, binding, Tag and part id exactly as is. A control with a wrong name builds fine and fails at runtime; verify after the edits.
- No implicit (keyless) styles in App.xaml; no new fixed color outside the Chrome Palette; no global ToolTip style. Use the existing styles (`GLLabelStyle`, `GLTextBoxStyle`, `GLCheckBoxStyle`).
- Do not change the single-layer Position & Size pages, the Border or Content tabs, validation, ranges, serialization, theme loading or saving, `LayerLayoutEntry`, `LayerRegistry`, the group move logic, the ratio-lock logic, or the Theme Builder window size.
- No separators. No unrelated fixes bundled in; report anything you notice instead.
- **Line endings:** `.cs` and `.xaml` are CRLF; `.md` are LF. Use the Edit tool; if a script is used, open files with `newline=''` and `encoding='utf-8'`. After edits run `git diff --stat` and confirm no file shows a whole-file change.
- Stay inside the project folder. No capture or input scripts, and no browser or computer-use tool against other windows.
- Never commit.

---

## 6. Docs to update at the end

- **themes.md:** 3.7 (Position tab: one header "POSITION & SIZE" for every layer, the group pages' Hide-beside-header and below-the-box labels, the doubled Y-to-Width gap on Search & Buttons and Game Info, and Random Game Display's pass 1 spacing with its lock column), the relevant parts of 3.12 and 3.16 if they describe the group Position layout, a new decision (next free number) for the group Position & Size layout, and a change log entry. Remove or rewrite the pass 1 text that says the group pages keep the header "POSITION".
- **CLAUDE.md:** only if something in it describes the group Position page or the per-layer header rule; otherwise leave it.

---

## 7. How to verify (the user builds and tests; include this checklist in your report)

Build: **0 errors, 0 warnings** (report any new warning).

1. All three group layers (Search & Buttons, Game Info, Random Game Display) show the tab "POSITION & SIZE"; single-layer pages still show it too.
2. Every part's Hide checkbox sits on its section header's row, 12 px after the header text, vertically centered with it; Hide still hides and shows the part, per resolution.
3. X, Y, Width and Height are on one row for every part, with labels centered below the boxes and no colons.
4. Search & Buttons and Game Info: only the Y-to-Width gap is doubled; X-to-Y and Width-to-Height are as before. Random Game Display: spacing matches the single-layer pages, no doubled gap, and each part's ratio lock sits between Width and Height, vertically centered, still locking and unlocking per part.
5. Nothing is clipped, overflowing or misaligned on any of the three pages; the layouts look consistent across them.
6. "Move all parts together" is still at the top on Search & Buttons and Game Info, still moves all parts on an X or Y edit, and is absent on Random Game Display.
7. Random Game Display's preview card and "Preview as" controls still sit above the tabs and work.
8. Other tabs on the three layers (Search, Buttons, Border, Title, Publisher, Game Info, Previews, Play Button, Favorites Star, Text, Background, Border) are unchanged.
9. Typed values in X / Y / Width / Height validate exactly as before; the guides still follow the edited part.
10. Switching layers and resolutions picks the first tab correctly; Reset Layer, Save Adjustments and Close without saving behave as before; multi-select still shows the nudge panel.
11. Single-layer Position & Size pages (shape, custom, Games List, Marquee, Splash Image, Video Preview, Footer) are unchanged from pass 1.

---

## 8. Report format

After STEP 1: the audit report above. After STEP 2: files changed; anything that did not match these instructions; anything changed beyond them; the test checklist.
