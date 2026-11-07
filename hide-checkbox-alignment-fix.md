# Fix: Hide checkbox vertical alignment on the grouped layers' part headings

Follow-up to pass 2 (`layer-tabs-polish-pass2.md`, built and tested; decision 80 in `themes.md`). Read `CLAUDE.md` first (especially **Boundaries and Privacy**, which governs everything in section 4 below), then `themes.md` decision 80 and 3.7, then `BuildGroupParts` in `Views/AdvancedThemesTabControl.xaml.cs` and `GLCheckBoxStyle` in `App.xaml`.

---

## 1. The problem

Pass 2 moved each part's Hide checkbox onto its section heading's row: a horizontal `StackPanel` holding the heading `TextBlock` and the existing `CheckBox` (`Margin="12,0,0,0"`, both `VerticalAlignment=Center`). On screen, the checkbox square and its "Hide" label are **not consistently centered on the heading text**: some rows look right and some are off, by roughly a pixel. It is the **same pattern on all three grouped layers** (Search & Buttons, Game Info, Random Game Display), which all use the one `BuildGroupParts` code path.

Where to look: Theme Builder > a grouped layer > POSITION & SIZE tab. Game Info is the best test page (headings "Game Title", "Publisher Block", "Game Info Nav", "Game Info", "Image Previews", and more below the fold when scrolled). The user's launcher was at the 1280x720 layout, 100% Windows scale on a 1080p monitor.

The user's suspicions, to test and not to assume:
- The checkbox square (with its template) is **taller than the heading text's line box**, so centering one on the other is not a clean pixel match.
- It looks like **sub-pixel rounding**: some rows land better than others, which points at fractional Y positions (the heading line box is a fractional height, so each part starts at a different fractional offset down the page and snaps differently).
- The "Hide" label uses a different font size / weight from the Bold Header-size heading, so centering line boxes is not the same as centering the visible glyphs.

The user's zoomed screenshots also showed a thin 1 px light horizontal line across the full width at the bottom of a header row. Check whether it is real (a leftover Border / separator / template element) or just an artifact of the zoom tool; look at a native-resolution capture.

---

## 2. Goal

The checkbox square and its "Hide" label look **vertically centered on the heading text, identically on every part row**, on all three layers. Define the target precisely while investigating (for example: the square's vertical center sits on the center of the heading's cap-height, and the "Hide" label's text sits visually level with the heading) and report which definition you used and why.

The fix must keep working when the theme's Header font size changes (it is themeable, 6 to 72) and at other Windows scale factors; reason about both and say what you could and could not verify (only 100% on 1080p is available to test).

---

## 3. What to do

**A. Find the cause (no edits yet).**
1. Read the code and the `GLCheckBoxStyle` template: the checkbox's real height (square size, padding, any `MinHeight`), the label's font resources, and how `VerticalAlignment=Center` resolves against the heading's line box.
2. **Measure layout positions without any screenshots first.** For several parts on each layout, get the fractional Y of the heading, the checkbox square and the label (for example, an offscreen load of the page and `TransformToAncestor` / `ActualHeight` readings, or a small throwaway test harness outside the project folder tree's committed files). This will show whether the offsets are fractional and whether they differ part to part. Note: offscreen renders use the software renderer, so use them for layout numbers only; the on-screen result is the authority for how it looks.
3. Then confirm on screen with native-resolution captures (section 4): measure the pixel rows of the heading text (cap top, baseline), the checkbox square (top and bottom edges) and the "Hide" glyph rows, for each part on each of the three layers. Find the pattern behind the "some rows good, some off" differences.

**B. Fix it (smallest local change).** Candidates, in the order to try:
- `UseLayoutRounding="True"` (and/or `SnapsToDevicePixels`) on the heading row or `PnlGroupParts` so fractional positions snap identically for every part. Check it does not change the field boxes or spacing.
- Give the heading row an explicit fixed height, or the checkbox a fixed height with a matching offset, so the geometry is the same for every part.
- Align by baseline or by an explicit top margin computed from the real heights (for example a Grid row with the text bottom-aligned and the checkbox offset), if centering line boxes can never match the glyphs.
Pick the simplest option that gives **identical results on every row**, and verify it with the same pixel measurements as step A3.

**Scope limits.** Change only how the heading row is built in `BuildGroupParts`. **Do not** edit `GLCheckBoxStyle` or any other shared style (other tabs use them); if the cure needs a shared-style or other-page change, **stop and report** instead of making it. Keep the 12 px gap, the Tag, the `PartHide_Click` handler, the `_partControls` entry, the order parts are built in and the 18 px section spacing. Nothing else changes: no field rows, no labels, no other tabs, no single-layer pages.

---

## 4. Permission to run and capture (this task only)

The user **explicitly authorizes** you to run the launcher and to capture screenshots of it for this task, under the CLAUDE.md rules for screen-capturing and input scripts. Those rules still apply in full:

- **Tell the user first**, in your own message, exactly what you will capture and send and which window it targets, then wait for their go-ahead. Python runs always prompt the user; that prompt is their chance to close personal windows, so never avoid it (no other launch method, no wrapper).
- **Launcher window only.** Capture only the launcher's own window (its window handle), never the full screen or a screen region; send clicks or keys only to that window, never to whatever is on top. Capture at native physical pixel size; do not scale or resample.
- **Check before every action:** confirm the launcher process is running and its window is in front before each capture, click or keystroke. If either check fails, abort at once: capture nothing, send nothing, report it.
- **Delete what you captured.** Save screenshots only in a temp folder inside the project, view only the ones you need, and delete them when the test ends. Never commit them.
- Do **not** use computer use or any browser tool to look at the user's other windows. Do not read or touch anything outside the project folder.
- **If the launcher is already running and locks the exe, do not kill it yourself:** stop and ask the user to close it, then build and run.
- Navigation is minimal: open the Theme Builder from the launcher, select Game Info, Search & Buttons and Random Game Display in turn, on the POSITION & SIZE tab; scroll the Game Info page if needed. Do not change, save or load any theme; close the Theme Builder with Close Panel without saving when done, and close the launcher you started.

---

## 5. Constraints (as always)

- Keep every `x:Name`, handler, Tag and binding as is; no unrelated fixes (report anything you notice).
- `.cs` and `.xaml` are CRLF; `.md` are LF. Use the Edit tool; after edits run `git diff --stat` and confirm no file shows a whole-file change.
- Keep comment fencing, and an accurate one-line `// Does X` summary on `BuildGroupParts` or any method you touch.
- After the fix the project must build with **0 errors and 0 warnings**.
- **Never commit.**

---

## 6. Docs

Update `themes.md`: a line in decision 80 (or 3.7) describing how the heading row is aligned and why (rounding / height cause and the fix), and a change log entry. `CLAUDE.md`: only if the finding is a gotcha future passes need (for example "set UseLayoutRounding on X"); otherwise leave it.

---

## 7. Report

- **Root cause**, with the measured numbers (the fractional offsets, heights, pixel rows) that show it, and which of the three suspicions were true.
- **The fix** you chose and why, plus any option you tried and rejected.
- **Before and after** pixel measurements for each part on each of the three layers (a compact table is fine), showing every row now matches.
- Whether the thin horizontal line in the zoomed crops was real.
- What you could not verify (other Windows scale factors, large Header sizes) and your reasoning for them.
- Files changed, anything that did not match these instructions, anything changed beyond them.
- Confirmation that all captures were deleted and the launcher you started was closed.
- A short test checklist for the user: the three layers' Position & Size tabs, Hide still hides and shows each part, group move and ratio locks unchanged, other tabs unchanged, 0 errors / 0 warnings.
