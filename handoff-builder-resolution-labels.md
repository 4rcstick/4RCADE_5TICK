# Handoff: Theme Builder resolution labels and row alignment (separate from the Theme Browser handoffs)

## Kick-off prompt (paste this into Claude Code)

```
Read handoff-builder-resolution-labels.md in the project folder completely, then CLAUDE.md and themes.md (sections 3.1, 3.22 and 3.25 at least).

Follow the file's pass structure: do the read-only STEP 1 check first, report your findings, then STOP and wait for me to say "go". Only after "go" do STEP 2 (the edits). Do one pass only, stop after it, and do not commit. The resolution combo's behavior is investigated in STEP 1 only: do not change it, in STEP 1 or STEP 2. Do not start any Theme Browser work.
```

---

## 1. Goal and why

Two labels in the Theme Builder's resolution rows are being renamed, and the two rows are being aligned:

| Row (in `AdvancedThemesTabControl.xaml`, `[SECTION: Resolution Row]`) | Current label | New label |
|---|---|---|
| The row of saved-size buttons (`PnlSavedSizes`) | `Theme sizes:` | `Resolutions:` |
| The combo row (`CboResolution`) | `Resolution:` | `New Resolution:` |

Why: the Theme Browser (later handoffs) will use "Resolutions:" for its per-theme buttons, so the Builder's wording should match. The combo is how a user adds a new size (a preset or "Custom..."), so it reads better as "New Resolution:".

Both rows must begin their controls at the **same X position**: the saved-size buttons and the combo must start at the same left edge, whatever the label text.

Settled decisions:
- Only the two label texts, the shared label column, and the minimum documentation/string updates below. No other Theme Builder changes.
- The resolution combo's **behavior is not changed** in this handoff. Whether it should rest on a `[New]` value instead of showing the active resolution is a discussion topic: STEP 1 reports findings, the user and Claude (chat) decide afterwards, and any change becomes its own handoff.
- Keep the label font and color as they are (Global Sub-Header, via `DynamicResource`).
- This task must not touch the resolution-button code (`RebuildSavedSizes`, `BtnSavedSize_Click`) beyond the label text. A shared button factory is a later handoff.

## 2. Files involved (verify names and line areas; the code wins)

- `Views/AdvancedThemesTabControl.xaml`: the `[SECTION: Resolution Row]` (around lines 100 to 150 at audit time). Row 1: a `Grid` with columns Auto / `*` / Auto holding the `Theme sizes:` TextBlock (`MinWidth="83"`, `Margin="0,0,10,0"`), `PnlSavedSizes` (WrapPanel), and `BtnThemeInfo`. Row 2: a second `Grid` with columns Auto / Auto / `*` holding the `Resolution:` TextBlock (`MinWidth="83"`, `Margin="0,0,10,0"`), `CboResolution` (`Width="140"`), the Delete/Remove Size button and `PnlCustomRes` (the "Custom size:" entry row, Grid.Row 2, spans columns).
- `Views/ThemesManagerControl.xaml`: its `Active Theme:` label, which today shares `MinWidth` 83 with the two labels above so all three controls line up (see `CLAUDE.md`, Theme Builder geometry).
- `Views/AdvancedThemesTabControl.xaml.cs`: the combo's code (`CboResolution_SelectionChanged`, `CboResolutionApply`, `BeginNewSizePick`, `RestoreResolutionComboSelection`, `UpdateCustomResUi`, `PickSizeAfterRemoval`, `_loading`, and whatever fills the combo)
- `themes.md`, `CLAUDE.md`

## 3. Current layout (from the code)

- The two labels live in **two different Grids**, each with its own Auto label column, so their widths are decided independently. Both have `MinWidth="83"` (a minimum, not `Width`, because the font size is themeable through the Global Sub-Header size).
- "Theme sizes:" and "Resolution:" are short enough to fit in 83 at the default Sub-Header size, so today the saved-size buttons, the combo and (in `ThemesManagerControl`) the Active Theme combo all start at about the same X. "New Resolution:" is longer, so it may grow the label column past 83 and push the combo to the right of the other rows. "Resolutions:" is roughly the width of "Theme sizes:".
- `CLAUDE.md` records that "Active Theme:", "Theme sizes:" and "Resolution:" labels share `MinWidth` 83, and the alignment standard (decision 82) says: do not stretch boxes, avoid layout-rounding traps (a Grid with `UseLayoutRounding`, no `*` column and spare width renders about 1 px wider per column; give such a Grid `HorizontalAlignment="Left"`, or a trailing `*` column).

## 4. STEP 1: read-only check (do this first, then STOP and wait for "go")

Do not edit anything in this step. Report, briefly:

**A. Layout and alignment**
1. The exact XAML of the two rows and of the Active Theme label in `ThemesManagerControl.xaml` (names, columns, margins, `MinWidth`, whether the Grids have `UseLayoutRounding`, `HorizontalAlignment`, or a `*` column).
2. The default Global Sub-Header font size and font family, and a reasoned estimate of the rendered width at that size of "Theme sizes:", "Resolutions:", "Resolution:" and "New Resolution:" (include how you estimated; you cannot run the app). Say whether "New Resolution:" fits in the current `MinWidth` 83 and what the smallest whole-number `MinWidth` would be that fits all three new/old labels at the default size with a few pixels of slack.
3. What happens at larger Sub-Header sizes (the Global tab lets users change it): with `MinWidth` only, which rows would drift apart.
4. Feasibility of a **truly shared label column** for the two rows inside `AdvancedThemesTabControl`: for example `Grid.IsSharedSizeScope="True"` on the `[SECTION: Resolution Row]` StackPanel and `SharedSizeGroup` on both label columns (rows currently have different column structures, so confirm this works with `MinWidth` kept, with the `*` columns, and with the layout-rounding rules). Also say whether extending the same shared scope to the Themes Manager's Active Theme label (a different UserControl) is feasible and how invasive it would be, versus keeping the three labels aligned by a common `MinWidth`.
5. Your recommended approach, with the exact `MinWidth` value and where it must be applied, so that at the default Sub-Header size the saved-size buttons, the combo and the Active Theme combo all start at the same X. If anything cannot be done safely within `AdvancedThemesTabControl.xaml`, say so.
6. The "Custom size:" label in `PnlCustomRes` (no `MinWidth`): report how it relates to the new alignment and whether leaving it unchanged looks inconsistent. Report only; it is not part of this task.

**B. Every place the old label text appears**
7. A list of every occurrence of `Theme sizes` and `Resolution:` (and similar user-facing wording such as "theme size", "Theme sizes row", "Theme sizes button") in `.xaml`, `.cs`, tooltips, `MessageBox` text, prompt text, help glyph text, and the docs. Split them into: (a) user-facing strings that quote the old label and would now be wrong or confusing, (b) code comments, (c) identifiers (`PnlSavedSizes`, `RebuildSavedSizes`, `BtnSavedSize_Click`, and so on), (d) `themes.md` / `CLAUDE.md` prose. Do not change identifiers.

**C. The resolution combo (report only; no change in this task)**
8. What `CboResolution` contains (presets, saved/custom sizes, "Custom...") and how it is filled and refilled.
9. What its selection currently means: does picking an existing size **navigate** (switch the working size), does picking a size without a layout **create** one (the New size layout prompt), and does it share the Smart fullscreen size-pick path (`RunSizePick`) with the saved-size buttons?
10. Every piece of code that reads or sets `CboResolution.SelectedItem` / `SelectedIndex` / text, and the guards (`_loading`, `RestoreResolutionComboSelection`, `BeginNewSizePick`): which assume that the combo always shows the **active** size.
11. What else is tied to the combo's selection: `UpdateCustomResUi` (Delete/Remove Size visibility, `PnlCustomRes`), `PickSizeAfterRemoval`, the "Custom..." pending-size state, and size changes coming from outside (hamburger Layout size, a theme load, `RefreshAfterExternalSizeSwitch`).
12. Is the combo **redundant** with the saved-size buttons as navigation? (The buttons list only sizes saved in the theme file; the combo also lists presets with no layout and working-only sizes.)
13. What a resting value such as `[New]` (an unselected/placeholder state that returns after each action) would require: the mechanism (placeholder item, unselected combo with a text, templated display), which of the code in items 10 and 11 would break, how the user would then know the **active** size (the saved-size buttons only show saved sizes, so an unsaved or pending size would show nowhere), and any risks.
14. Your recommendation (safe as is / safe with these changes / not advisable) with the reasons. This is for discussion; do **not** implement it.

**D. Other**
15. Any mismatch between this file or `themes.md` and the code.

Then stop. Edits start only after the user says "go".

## 5. STEP 2: the edits (only after "go"; the "go" message may adjust the approach chosen in STEP 1)

### 5.1 Labels
- `Theme sizes:` -> `Resolutions:` and `Resolution:` -> `New Resolution:` in `AdvancedThemesTabControl.xaml`. Keep font, color, vertical alignment and margins as they are.

### 5.2 Alignment
- Apply the approach approved at "go" (expected: a shared label column for the two rows inside `AdvancedThemesTabControl` via `Grid.IsSharedSizeScope` + `SharedSizeGroup`, keeping `MinWidth` on the labels, and the common `MinWidth` value from STEP 1 applied to the Active Theme label in `ThemesManagerControl.xaml` too, so all three rows still line up at the default Sub-Header size).
- Respect the alignment standard: no stretching of boxes, no turning layout rounding off, `HorizontalAlignment="Left"` or a trailing `*` column where a Grid with `UseLayoutRounding` needs it. Keep `BtnThemeInfo`'s right edge aligned with Delete Theme as the existing comment describes. Keep the combo `Width="140"` and the custom-size row as they are.
- Update the XAML comments in that section that describe the shared `MinWidth` 83 (they must match what the code now does).

### 5.3 User-facing strings
- Update only the user-facing strings found in STEP 1 B(a) that quote the old label text, changing the label words only. List each change in the report. Leave identifiers, behavior and anything unrelated as is.

### 5.4 Combo behavior
- **No change** to the combo's items, selection, handlers or `[New]` idea. Do not add a placeholder item.

### 5.5 Documentation (end of pass)
- `themes.md`: in 3.1 (or where the combo and saved-size row are described) add a short terminology note: on-screen labels are `Resolutions:` (was `Theme sizes:`) and `New Resolution:` (was `Resolution:`) since 2026-11-08, while this document and the code still call them the Theme sizes row and the size combo. Update the 3.25 alignment text about the shared label `MinWidth` to the new rule and value. Add the combo findings from STEP 1 (items 8 to 14) to a "parked ideas" entry (6b) as "Resting `[New]` value for the resolution combo: findings, not built". Change log: one dated entry (2026-11-08).
- `CLAUDE.md`: update the Theme Builder geometry bullet that says "Active Theme:", "Theme sizes:" and "Resolution:" share `MinWidth` 83 to the new labels and the new rule/value. Do not rewrite other mentions of "Theme sizes".
- Keep `.md` files LF and `.cs` / `.xaml` CRLF as `CLAUDE.md` describes; check `git diff --stat` shows no whole-file change.

## 6. Constraints and things NOT to touch

- No change to the resolution combo's behavior, contents or selection logic, and no `[New]` placeholder.
- No change to `RebuildSavedSizes`, `BtnSavedSize_Click`, the size-pick or Smart fullscreen code, the theme load code or any Theme Browser file.
- No rename of identifiers, x:Names, handlers or comments other than the XAML comment in 5.2.
- No other alignment, font or spacing changes anywhere else in the Theme Builder; the listed "deliberately left" alignment exceptions in `CLAUDE.md` stay as they are.
- No new Global or Theme Builder properties, no new Chrome Palette brushes.
- Keep comment fencing and the one-line `// Does X` summary on any method you touch (none expected). No unrelated fixes bundled in.
- Never commit.

## 7. Verification

You (Claude Code):
1. `dotnet build` finishes with **0 errors and 0 warnings**. No transient errors are expected.
2. Grep: no remaining on-screen `Theme sizes:` or `Resolution:` label text in XAML, and the strings you changed are listed.
3. `git diff --stat` shows only the files in section 2 (plus the docs), with no whole-file line-ending changes.

The user (cannot be done without running the app; do not run any screen-capture or input scripts yourself):
1. The Theme Builder shows `Resolutions:` above `New Resolution:`, and no `Theme sizes:` / `Resolution:` label remains anywhere on screen (check tooltips, the Layers help, the New size layout prompt and delete-size confirmations too).
2. The first saved-size button and the combo start at the **same X**, and both line up with the Active Theme combo in the Themes Manager row.
3. Open the Global tab, set the Sub-Header size to a small value (for example 10), a normal value, and larger values (for example 18 and 24): the two rows stay aligned with each other and nothing is clipped or overlapping. Report whether the Active Theme row still lines up at those sizes.
4. Repeat 2 at Windows scales 100%, 125% and 150% (or whatever is available).
5. With many saved sizes, the buttons still wrap in their WrapPanel and `Theme Info` keeps its right edge aligned with Delete Theme.
6. Choose "Custom..." in the combo: the "Custom size:" row appears under the combo as before. Pick a preset, Apply a custom size, Remove a size, and click saved-size buttons: all behave exactly as before this change (the combo behavior is untouched).
7. Restore Defaults on the Global tab (fonts) returns the labels to the default size and the alignment still holds.

## 8. Stop conditions

Stop and report (do not push on) if any of these happen:
- STEP 1 shows a shared label column cannot be done safely in `AdvancedThemesTabControl.xaml` without restructuring either row, or the three-row alignment would need changes to `ThemesManagerControl` beyond its label's `MinWidth`.
- A string that quotes the old label is in code that also holds behavior you would have to change to reword it.
- Any edit would need a change to the combo's behavior, the size-pick code, or a Theme Browser file.
- The build shows errors or warnings you cannot clear within the files in section 2.

After STEP 2, report in this form: files changed, anything that did not match these instructions, anything changed beyond them, and the test checklist (section 7). Then stop. Handoff 3 of the Theme Browser series is written only after this task is tested and committed.
