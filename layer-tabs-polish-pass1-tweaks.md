# Layer tabs polish, pass 1 tweaks (three small changes)

Follow-up to `layer-tabs-polish-pass1.md`, which is built and building clean (0 errors, 0 warnings). Read `CLAUDE.md` first, then `themes.md` sections 3.7 and 3.8 and decision 79, then the current `Views/AdvancedThemesTabControl.xaml`. Where the code and `themes.md` disagree, the code wins: report it and correct `themes.md` at the end.

These are three small, fully specified edits. **No audit stop: implement directly, in one pass, then stop and report.** **Never commit.** Do not touch anything outside the three tweaks.

Everything the user tested in pass 1 works; this only adjusts the look of it. No bindings, handlers, validation, ratio-lock behavior, serialization or other functionality may change, and no `x:Name` may change.

---

## Tweak 1: Position & Size row, X / Y / Width / Height expand to the available width

**Why:** the four boxes are fixed at 50 px, which leaves a lot of unused space at the right of the Position & Size tab. They should use the full width.

**Where:** the Position tab's rect row in `AdvancedThemesTabControl.xaml` (`PnlRectFields`: `TxtLayerX`, `TxtLayerY`, `TxtLayerWidth`, `TxtLayerHeight`, `BtnRatioLock`). Pass 1 built it with columns 50 / 12 / 50 / 12 / 50 / 36 / 50.

**Change:**
- The four number columns become **four equal-width star columns (`*`)**. The 50 px becomes the **minimum** width for each of the four boxes (`MinWidth`), not a fixed width.
- The ratio lock keeps **its own fixed-width column** (36), between Width and Height.
- The 12 px spacing columns stay fixed, so the spacing between the four boxes and the lock stays consistent.
- The four boxes stretch to fill their columns (check that `HorizontalAlignment` is `Stretch`, and that every parent up to the tab content lets the row fill the width; if a parent is shrinking the row, fix that parent only for this row).
- All four boxes stay on **one** row. Labels (X, Y, Width, Height) stay **centered directly underneath** their own boxes and stay **without colons**.
- The lock stays vertically aligned with the boxes exactly as it is now.
- The row fills the available content width while keeping the existing margins and padding.
- **Do not change the width of any other box in this pass** (opacity, hex, border size and radius stay fixed as they are).
- **Do not change the Theme Builder window size.**

Affects every page that uses this shared rect row (shape layers, custom layers, Games List, Marquee, Splash Image, Video Preview, Footer). The three group layers do not use it and must stay unchanged.

---

## Tweak 2: Content tab help tooltip, remove "JPEG" from the displayed text

**Change:** the tooltip on the "?" next to "Image / video file:" becomes:

```
Supported file formats:

Images: PNG, JPG, GIF
Videos: MP4
```

Remove "JPEG" from the displayed tooltip text **only**. Keep the bold header, the blank line and the existing tooltip styling. **Do NOT change the Browse dialog filter** (`BtnBrowseShapeFile_Click`, it keeps `*.png;*.jpg;*.jpeg;*.gif;*.mp4` and All files), supported formats, validation or any other behavior.

**Docs:** pass 1 added a note to `CLAUDE.md` (and probably `themes.md`) saying the tooltip list must change together with the Browse filter. Reword it: the tooltip intentionally omits JPEG (the filter still accepts `.jpeg`); this is deliberate, so do not "fix" it, and if the filter changes later, revisit the tooltip.

---

## Tweak 3: shared Border tab, even gaps between the three field groups

**Why:** on the Border tab the gap between Border color and Size looks smaller than the gap between Size and Radius. In the user's screenshot (Custom Layer, Border tab) the gap from the right edge of the hex box to the Size box is roughly 10 px, while the gap from the right edge of the Size box to the Radius box is roughly 24 px. The cause: Size and Radius sit in 52 px columns holding 36 px boxes, left-aligned, so there is about 16 px of dead space after the Size box on top of the normal gap. (Those pixel figures come from a screenshot; read the real values from the XAML.)

**Goal:** both visible gaps are the **same value G**, where **G is the current visible gap between the Size box's right edge and the Radius box's left edge** (about 24 px; compute it exactly from the XAML as it stands today). So the Border color to Size gap grows to G, and the Size to Radius gap stays G.

**How (suggested, use your judgment inside these limits):** size each group's column to its content and put the gaps in explicitly (for example a fixed-width gap column of G between groups), instead of relying on leftover space in oversized columns. The visible gap is measured from the right edge of the previous group's box to the left edge of the next group.

**Requirements:**
- Keep the existing single-row layout and the current control widths: Border color is still the swatch plus hex box group (the group is 134 px wide, keep it), Size and Radius keep their 36 px boxes.
- Labels stay above the controls, left-aligned with their boxes as they are now.
- Do not change the order of the three groups.
- Make sure a label is never clipped: the Radius label is wider than its 36 px box, so size that group's column to the wider of its label and its box.
- Expected result: Size and Radius move right (roughly 14 px and 16 px). That is intended.

**Shared tab:** `TabShapeBorder` is shared by shape and custom layers, Splash Image, Video Preview, Search & Buttons and Random Game Display. They all get the new spacing. Check that the extra panels on that tab still look right and stay aligned with the main row: `PnlRGBorderState` (Random Game Display's State combo) and `PnlSBHoverBorder` (Search & Buttons hover border color, `TxtSBHoverBorderColorHex`). Their own controls and widths do not change.

---

## Constraints

- **Names and handlers:** keep every `x:Name` and handler exactly as is; after the edits, verify each moved control still has its original name.
- **Line endings:** `.cs` and `.xaml` are CRLF; `.md` are LF. Use the Edit tool; if a script is used, open files with `newline=''` and `encoding='utf-8'`. After edits run `git diff --stat` and confirm no file shows a whole-file change.
- **Comment fencing and style:** keep `<!-- [SECTION: ...] -->` / `<!-- [END SECTION: ...] -->`; every method you touch keeps an accurate one-line `// Does X` summary (you are likely not touching any C#).
- No implicit (keyless) styles in App.xaml; no new fixed color outside the Chrome Palette; no global ToolTip style.
- No unrelated fixes. Report anything you notice instead.
- Stay inside the project folder. No capture / input scripts, no browser or computer-use tools against other windows.
- Never commit.

---

## Docs to update at the end

- **themes.md:** update 3.7 / 3.8 wording for the stretching position boxes (4 equal star columns, 50 px minimum, lock column fixed), the tooltip text, and the Border tab spacing (gap G). Amend decision 79 (the width rule) so it says the four position boxes are a 50 px **minimum** and stretch, while every other capacity stays fixed. Only add a new decision number if amending would make 79 unclear; say which you chose. Add a change log entry for this tweak pass.
- **CLAUDE.md:** the box-width note should say the X / Y / Width / Height boxes are a 50 px minimum that stretches; reword the tooltip / filter note as described in Tweak 2.

---

## Verify (the user builds and tests; include this checklist in your report)

Build: **0 errors, 0 warnings**.

1. **Shape Layer 01 > Position & Size:** the four boxes fill the row; they are equal width; the lock sits in its own column, vertically centered on the boxes; labels are centered under their boxes without colons; the right edge of the row lines up with the panel's content edge.
2. Typing in X / Y / Width / Height still works and validates as before; ratio lock on and off still behaves as before.
3. **Other single-layer pages** (custom layer, Games List, Marquee, Splash Image, Video Preview, Footer): same row behavior.
4. **Group layers** (Search & Buttons, Game Info, Random Game Display): the Position page is unchanged.
5. **Content tab:** the "?" tooltip reads "Images: PNG, JPG, GIF" and "Videos: MP4"; Browse still offers `.jpeg` and All files.
6. **Border tab (Custom Layer):** the gap from the hex box to the Size box equals the gap from the Size box to the Radius box; the labels are not clipped; color, size and radius still apply live.
7. **Border tab on Splash Image, Video Preview, Search & Buttons (with its hover border color) and Random Game Display (with its State combo):** spacing is even and nothing is misaligned or clipped.
8. Multi-select mode still shows the nudge panel and returns to the tabs cleanly; switching layers and resolutions still picks the first tab.

## Report format

Files changed; anything that did not match these instructions; anything changed beyond them; the value of G you measured and used; the test checklist above.
