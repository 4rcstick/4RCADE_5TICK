# Layer tabs polish, pass 1: Position & Size, Content, Border (shape layers, custom layers and the other single-layer Position tabs)

Read `CLAUDE.md` and `themes.md` (sections 3.7 and 3.8 first) before anything else. This spec follows the usual pass structure: **STEP 1 is a read-only audit. Report, then stop and wait for the user to say "go".** STEP 2 (implementation) starts only after "go". Do this one pass, stop at the end for the user to build, test and commit. **Never commit.**

---

## 1. Goal and why

Final polish of the Theme Builder's layer property tabs, starting with the Shape Layers 01 to 04 and custom layers, which share three tabs: Position, Background, Border. The layout is being tightened so the X / Y / Width / Height inputs sit on one row with their labels underneath, the Background tab becomes a clearer "Content" tab with a help tooltip on the file picker, and every numeric / hex input box gets a consistent character-based width.

This is a layout and display-text pass. **No behavior, binding, serialization, validation or range changes.**

---

## 2. Scope

### In scope
- **Shape Layers 01 to 04 and custom layers:** all three tabs (Position & Size, Content, Border).
- **The Position tab on every other single-layer page that shares the same Position controls** (expected: Games List, Marquee, Splash Image, Video Preview, and any other non-group layer that shows the Position tab; confirm in the audit). They get the new X / Y / Width / Height row layout **and** the tab header text "Position & Size".
- **Border tab on shape and custom layers:** labels and properties do not move; only the text box widths change (see section 5).

### Out of scope (do not touch)
- The Position tab pages of the three **group layers**: Search & Buttons, Game Info, Random Game Display. Their group-parts panel (built in code by `BuildGroupParts`), "Move all parts together", per-part ratio locks, and the tab header text "Position" stay exactly as they are.
- The Main Window Background (no Position tab) and the Scrollbars row (settings-only).
- Any tab that is not Position, Background or Border on the shape / custom layers (e.g. Splash Image's Image tab, Video Preview's Fallback Image tab, Marquee tabs, Game Info tabs).
- Border tabs on layers other than shape and custom layers, **unless they are the very same TabItem** as the shape / custom Border tab (see audit question 4).
- Validation, allowed ranges, MaxLength, handlers, `x:Name` values, bindings, theme.json, `LayerLayoutEntry`, `LayerRegistry`.

---

## 3. Current behavior (from themes.md; verify against the code)

- Right pane of the layer list is `PnlLayerFields`: layer name heading, `TabsLayer` (TabControl), then Rename / Reset Layer / Remove Layer pinned below the tab frame.
- **Tabs per layer:** `GetLayerTabs(unitKey)` and `AllLayerTabs` in `AdvancedThemesTabControl.xaml.cs`; `ApplyLayerTabs` sets each TabItem's Visibility and picks the first visible tab when the selected layer changes (`_tabsUnitKey`). Tab order is the TabItem order in the XAML.
- **Position tab:** X and Y on one row; Width and Height on the next with the aspect-ratio lock (`BtnRatioLock`) between them; then the group-parts panel and "Hide this layer" (or the Visibility dropdown for box layers, i.e. shape and custom layers).
- **Shape / custom layers:** Background tab (`TabShapeBackground`): file with Browse / Clear, file opacity, background color. Border tab: color, size (0 to 50), radius (0 to 200). The Position tab shows a Visibility dropdown (Always, GameSelected, NoGameSelected, Never) instead of the Hide checkbox.
- Shared styles live in App.xaml `[SECTION: Theme Builder Shared Styles]`: `GLLabelStyle`, `GLTextBoxStyle`, `GLCheckBoxStyle`, `PickerPopupGridStyle`, `GlyphToggleStyle`, plus `ThemedTabItemStyle` / `ThemedTabControlStyle` and the Chrome Palette.
- The Layers "?" glyph (`LayersHelpGlyphStyle`, UserControl resources `[SECTION: Layers Help]`) is a 16x16 Border, CornerRadius 8, 1px `Global.Font.SubText.Color` ring with a bold 11 "?"; hover turns both to `Global.Font.SubHeader.Color`. Its tooltip is a **local** `ToolTip` element (`ChromeTooltipBackground`, `ChromeTooltipText`, `ChromeTooltipBorder`, Padding 10,8, `InitialShowDelay="300"`, `ShowDuration="60000"`) containing a `TextBlock` (`TextWrapping="NoWrap"`) written from `Bold`, `Italic`, `Run`, `LineBreak` inlines.

---

## 4. Changes wanted

The ASCII diagrams below are **conceptual, not pixel-accurate**. Use WPF Grids (nested where appropriate) with column definitions and `Grid.ColumnSpan` for consistent alignment. Align corresponding controls across rows, group related controls, keep horizontal and vertical spacing consistent, and do not force unrelated controls into identical column widths. Numeric boxes use their specified character-based widths and are **never stretched** to fill a column.

### 4.1 Tab names (display text only)

For every page covered by the scope above:

| Current | New |
|---|---|
| Position | **Position & Size** |
| Background (shape / custom layers only) | **Content** |
| Border | Border (unchanged) |

Final order: POSITION & SIZE | CONTENT | BORDER.

The Position TabItem is one shared tab, and the three group layers must keep the header "Position". Work out in the audit how the header text will differ per layer (set in `ApplyLayerTabs` per layer type, or another way) and report the approach. Escape the ampersand properly in XAML (`&amp;`) if the text is written in XAML.

### 4.2 Position & Size layout

```
Visibility:
┌──────────────────────────────────┐
│ Always                         ▼ │
└──────────────────────────────────┘

┌──────────┐   ┌──────────┐   ┌──────────┐     ┌──────────┐
│    20    │   │    60    │   │   332    │  🔗 │   636    │
└──────────┘   └──────────┘   └──────────┘     └──────────┘
     X              Y           Width             Height
```

- Visibility stays **above** the position / size row; its label ("Visibility:", with the colon) sits above the combo, left-aligned.
- X, Y, Width and Height are on **one horizontal row**.
- Their labels move **below** their boxes, **centered horizontally** under each box, with **no colons**.
- The aspect-ratio lock (`BtnRatioLock`) stays between Width and Height, **vertically centered with the input boxes** (not with the box + label block).
- This below-the-box label convention applies **only** to these four fields in this pass. All other labels stay above their controls unless stated here.
- Layers that show the "Hide this layer" checkbox instead of the Visibility dropdown (the non-shape single-layer pages): see audit question 3. Do not decide that placement alone.

### 4.3 Content tab layout (shape and custom layers; the former Background tab)

```
Image / video file: (?)
┌──────────────────────────────┐  ┌────────┐ ┌───────┐
│                              │  │ Browse │ │ Clear │
└──────────────────────────────┘  └────────┘ └───────┘

┌───────┐
│  100  │  Image / video opacity (0 to 100):
└───────┘

Background color:
┌───┐ ┌──────────────┐
│   │ │ #FF35243F    │
└───┘ └──────────────┘
```

- Order: file row, opacity row, background color row.
- The opacity box is on the **left** and its label ("Image / video opacity (0 to 100):") to its **right**. That is the one specified exception to "labels above"; the file label and the background color label stay above their controls.
- Keep the existing read-only file text box, Browse and Clear buttons, color picker button and hex box, and all their handlers. Do not modify the file picker or its functionality.
- The file text box may stretch to the available width (it is not a numeric box).

### 4.4 "?" help tooltip on the file label (Content tab)

- Add a small "?" glyph **immediately to the right of the "Image / video file:" label**, reusing `LayersHelpGlyphStyle` (same file, so it is in reach) and the same tooltip behavior as the Layers "?" (local `ToolTip`, same `ChromeTooltip*` brushes, Padding, `InitialShowDelay="300"`, `ShowDuration="60000"`, hover only, informational only, not focusable).
- Tooltip text:

```
Supported file formats:

Images: PNG, JPG, GIF
Videos: MP4
```

  Use a bold "Supported file formats:" header line, then a blank line, then the two lines, built from `Bold` / `Run` / `LineBreak` inlines like the Layers tooltip.
- **The tooltip must match the real accepted file types.** The audit must report the exact filter the shape / custom Browse dialog uses (via `BrowseLayerFile` or similar). If the filter accepts anything beyond png, jpg, gif and mp4 (for example jpeg or bmp), **stop and report; do not change either the filter or the tooltip text without the user's decision.**
- No global or implicit ToolTip style (CLAUDE.md rule). This tooltip is local to the glyph.

### 4.5 Border tab

Labels and properties do not move. Only the text box widths change, per section 5.

---

## 5. Input box sizing rules (all three tabs, shape and custom layers; X / Y / Width / Height also on the other covered Position tabs)

Character capacities (each includes one extra character of breathing room beyond typical maximum values):

| Field | Capacity |
|---|---|
| X / Y / Width / Height | 5 characters |
| Image / video opacity | 4 characters |
| Hex color fields (#AARRGGBB) | 10 characters |
| Border size | 3 characters |
| Border radius | 3 characters |

- Translate each capacity into a WPF pixel width using the **existing font, padding, border and control style** (`GLTextBoxStyle`). Measure from the style rather than guessing; report the resulting pixel widths in the audit.
- **The same capacity gets the same pixel width everywhere** (for example the hex box on the Content tab and the hex box on the Border tab are identical; the four position boxes are identical).
- Do not change validation, allowed value ranges or MaxLength.
- Do not widen a box beyond its width just to fill a grid column.

---

## 6. Horizontal fit rule for the Position & Size row

If the X, Y, Width, Height boxes, the lock and the required spacing do not fit the available property panel width:

- Do **not** shrink the boxes below their character-based widths.
- Do **not** wrap onto multiple rows.
- Do **not** remove or reposition the lock.
- Do **not** squeeze spacing excessively to force a fit.
- Do **not** resize the Theme Builder window (it is 717 x 885).

Keep all four boxes on one row with labels centered underneath. If it does not fit, **report the minimum additional panel / window width required** and let the user decide. In STEP 1, estimate this from the measured widths and the panel's actual available width.

---

## 7. STEP 1: read-only audit (report, then stop)

Do not edit anything. Report:

1. **Layers affected by the Position tab change:** every layer whose page shows the shared Position tab controls (and is not a group layer). For each, note whether it has the Visibility dropdown or the Hide checkbox, and any extra controls on that tab.
2. **How the tab header text will differ** between those layers and the three group layers (proposed approach), and confirm no `x:Name` or code reference depends on the header strings "Position" or "Background".
3. **Hide checkbox placement:** where "Hide this layer" sits today relative to the X / Y / Width / Height rows, and your proposal for keeping it consistent with "Visibility above the row". Wait for the user's decision before building this part.
4. **Border tab sharing:** whether the shape / custom Border tab is its own TabItem, or the same TabItem used by Splash Image, Video Preview or others. If shared, list who else would change.
5. **Browse filter and tooltip text:** the exact file filter on the shape / custom layers' Browse dialog, and whether the tooltip text matches it (section 4.4).
6. **Measured pixel widths** for the five capacities (section 5) with how you derived them, and the **fit estimate** for the Position & Size row: available panel width, needed width, any shortfall.
7. **Controls to rename or re-parent:** a list of the `x:Name` controls on the three tabs and the Position tab that the layout rework moves, confirming each keeps its name and its handler wiring.
8. **Mismatches** between this spec and the code, and between the code and themes.md. Code wins; list what themes.md sections 3.7, 3.8 and the change log will need.
9. Anything in this spec you consider ambiguous or in conflict with CLAUDE.md.

Then stop and wait for "go".

---

## 8. STEP 2: implementation (only after "go")

Order the edits so the end state builds clean (no transient errors expected; this is mostly XAML plus the tab-header logic). Keep the existing comment fencing: XAML `<!-- [SECTION: ...] -->` / `<!-- [END SECTION: ...] -->`, C# `// [SECTION: ...]` with a one-line `// Does X` summary on every method you add or touch.

---

## 9. Constraints and things NOT to touch

- **Names:** every `x:Name` and handler is referenced from code-behind. A wrong name builds fine and fails at runtime. After the rework, verify every moved control still has its exact original name and handler.
- **Chrome Palette:** a new fixed color is a named brush in `[SECTION: Chrome Palette]`, never a hex inside a style. No implicit (keyless) styles in App.xaml.
- **Tooltips:** local only; never add a global or implicit ToolTip style.
- **Line endings:** `.cs` and `.xaml` are CRLF; `.md` are LF. Use the Edit tool; if a script is used, open files with `newline=''` and `encoding='utf-8'`. After edits run `git diff --stat` and confirm no file shows a whole-file change.
- **No unrelated fixes** bundled into this pass. If you notice something, report it; do not fix it.
- Do not change group layer pages, validation, ranges, serialization, `LayerLayoutEntry`, `LayerRegistry`, or the ring / border arithmetic.
- Do not resize the Theme Builder window.
- Stay inside the project folder. Do not run capture / input scripts or any browser / computer-use tool against other windows.
- Never commit.

---

## 10. Docs to update at the end of the pass

- **themes.md:** section 3.7 ("Tabs per layer": new tab names and the per-layer header text, Position tab description), section 3.8 (Tabs: Background is now Content, Position & Size), the input box width rule (new decision; use the next free decision number), and a change log entry for this pass.
- **CLAUDE.md:** the line "the Layers '?' tooltip is the only styled tooltip" is no longer true. Update it to say that the Layers "?" and the Content tab's file "?" are the only styled tooltips, both local `ToolTip` elements on the `ChromeTooltip*` brushes, with still no global or implicit ToolTip style. Add a short note of the shared pixel widths per character capacity if that helps future passes.

---

## 11. How to verify (the user builds and tests; give this checklist in your report)

Build: **0 errors, 0 warnings** (report any new warning).

Run the launcher, open the Theme Builder, and check:

1. **Shape Layer 01:** tabs read POSITION & SIZE | CONTENT | BORDER. Position & Size shows Visibility above; X, Y, Width, Height on one row with labels centered underneath and no colons; the lock is vertically centered between Width and Height and still locks the ratio.
2. **Content tab:** "Image / video file:" with a "?" right of it; hovering shows the styled tooltip after about 300 ms with the right text. Browse and Clear still work; the opacity box (left) with its label (right); the background color picker and hex still apply live.
3. **Border tab:** same labels and positions as before; the boxes are narrower / wider per the rules; color, size and radius still apply live.
4. **A custom layer:** same as above (add one if none exists).
5. **Other covered layers** (Games List, Marquee, Splash Image, Video Preview, and the others the audit listed): the tab reads "Position & Size" and the new row layout works; their other tabs are unchanged.
6. **Group layers** (Search & Buttons, Game Info, Random Game Display): the tab still reads "Position" and the page is unchanged.
7. Switch between layers and between resolutions; the first tab is picked correctly each time; Reset Layer, Save Adjustments, Close without saving behave as before.
8. Width / Height with the ratio lock on and off; typing values still validates exactly as before.
9. Multi-select mode still shows the nudge panel and returns to the tabs cleanly.
10. If the audit reported a fit shortfall, confirm the Position & Size row is visually clean at the current window size and note the extra width needed.

---

## 12. Report format after the pass

Files changed; anything that did not match these instructions; anything changed beyond them; the measured pixel widths used; any fit shortfall and the extra width needed; the test checklist above.
