# ADVANCED THEME BUILDER — POST-IMPLEMENTATION VISUAL CLEANUP
## Claude Chat planning specification (for Claude Code handoff)

**Status:** Visual inspection findings agreed with the user, not yet implemented.

**Intended recipient:** Claude Chat. Review these findings against the current source and the completed initial tab-polish implementation, then prepare the smallest practical Claude Code cleanup handoff. Prefer **one coordinated cleanup pass**, with a **focused read-only re-check, findings/plan, and STOP for the user's explicit GO** before code changes. Do not treat this document as authorization to edit code.

**Scope:** UI presentation, control positions, widths, labels, and spacing only. Preserve values, bindings, `x:Name`, `Tag`, event handlers, enable/disable dependencies, file filters, media behavior, and persistence. Do not expand scope to tabs already visually accepted. The current code is the authority for implementation details, but the user's design decisions below override earlier planning and implementation documents where they disagree.

## 1. Confirmed corrections

### A. Section dividers — targeted adjustment

**Confirmed in:** Search & Buttons → Search; Games List → Folders and Game Titles; Game Info → Game Info (both dividers); Game Info → Play Button (all three dividers).

The gaps currently appear **smaller above each divider than below it**. **Increase the space ABOVE each divider to visually match the EXISTING space BELOW it. Do not reduce the bottom spacing.** Use the already satisfactory **Game Info → Favorites Star** divider spacing as a visual reference, not as a blanket numeric mandate. Apply only to the affected dividers; inspect any similar new divider before changing it. **Favorites Star dividers already look good and must remain unchanged.**

### B. Search & Buttons → Search

- Check the **Bold checkbox beside Font:** for accurate vertical centering relative to its header label. Use existing `UseLayoutRounding`/centered header conventions if an actual mismatch is confirmed. **Do not globally modify checkboxes**; those in Folders, Game Titles, Marquee, Game Info, Play Button, and Favorites Star have been accepted visually.
- Align the **Font-family dropdown's RIGHT edge** to the **Color hex field's RIGHT edge** in the Size/Color/Background row beneath.
- Align the **Alignment dropdown's LEFT edge** to the **Background color control group's LEFT edge** beneath.
- Preserve a single consistent **invisible vertical gap boundary** between the two top dropdown regions and the Size+Color / Background groups below. Adjust only local grid/column sizing; do not globally enlarge hex fields.
- Divider: apply section 1A.

**Search & Buttons → Buttons:** visually accepted, including extended Hover Color and the alignment of Background/Hover BG; leave unchanged.

### C. Marquee → Text Marquee — numeric-column alignment

Use the **Blur and Depth numeric fields in the compact Drop Shadow row** as the reference positions:

- Move **Max Size** to begin at the same X as **Blur**, and make its numeric field the same width as Blur.
- Move **Text Margin** to begin at the same X as Blur, and match Blur's numeric field width. Retain `Text margin (units)` and the existing rule that the numeric field is disabled while Wrap Text is on.
- Move **Border Size** LEFT to align its numeric field's LEFT edge with Blur.
- Move **Border Radius** LEFT to align its numeric field's LEFT edge with Depth; consequently reduce the gap between Size and Radius to match the Blur-to-Depth alignment/gap.
- Retain the previously approved **Border Size 50px / Radius 60px** numeric widths; for these two fields the **left-edge column positions** match Blur/Depth but numeric widths need not be identical. Preserve all border behavior.
- The checkboxes and the rest of this tab look good; do not move them.

### D. Marquee — approved tab and file-header wording changes

The four Marquee tab headers currently wrap across two rows. The user approved a **specific, minimal rename** to improve the fit without altering tab padding, font size, or spacing:

- Rename the tab display header **`SPLASH LOGO` → `LOGO`**. Leave the tab's internal `x:Name`, keys, values, file bindings and behavior unchanged.
- Inside that tab, rename the file-selector heading **`Logo image:` → `Splash logo image:`**. Keep the existing adjacent **?** help glyph and its tooltip, which already explains that the image is displayed only on the splash page when no game is selected.
- Keep the file path, Browse/Clear controls and **`Leave empty to show no logo.`** explanation unchanged.
- Target tab sequence: **`POSITION & SIZE | LOGO | TEXT MARQUEE | SHADOW`**. Check whether the shorter name alone brings all four tabs onto one row.
- **Do not change tab padding, header font size, spacing, or the other tab names.** If the row still wraps, report that result rather than making additional unapproved layout adjustments.

**Marquee → Shadow:** accepted; no changes. **Marquee → Logo:** only the two explicit display-text changes above; the remaining layout and functionality are accepted.

### E. Random Game Display → TEXT — revised design (supersedes initial handoff)

The prior implementation handoff ordered Message → Max Text Sizes → Font/Alignment and deliberately left this Drop Shadow as a standalone-toggle exception. **The user explicitly superseded both decisions during visual inspection.** The new intended order and layout are:

```text
Font: [ ] Bold                    Alignment: [ ] Wrap Text
[ Font dropdown               ]   [ Alignment dropdown        ]

Message:
[ existing message input                                  ]

Max text size (px):
[ 40  ] When a game is selected
[ 100 ] When on the splash page

Color:
[ swatch ] [ hex ]

[ ] Drop shadow                  Blur:       Depth:
[ swatch ] [ shadow hex ]         [ 3 ]       [ 1 ]
```

- Put **Font/Alignment first**, with **Bold beside Font** and **Wrap Text beside Alignment**, using centered header+checkbox rows.
- **Restore full visibility** of the Alignment dropdown, including its right edge/arrow, and of Wrap Text. The current screenshot shows the Alignment field clipped and Wrap Text absent from the visible row. Size the two columns to fit the pane; preserve readable labels.
- **Message second**, **shared Max text size (px) heading and two rows third**, **Color fourth**, **compact Drop Shadow last**.
- **Replace the separate Drop Shadow toggle row and `Shadow color:` label** with the compact pattern: checkbox directly above the shadow swatch/hex, Blur and Depth labels beside the checkbox and numeric controls underneath. This makes **16 compact shadow groups rather than 15**. Preserve this group's actual checkbox name, handlers, and its dependent controls' `ElementName` bindings; this is a layout change, not a shadow behavior change.
- Preserve per-state text-size values and exact settings behavior, including existing text wrapping and enabled states.

### F. Random Game Display → CONTENT — clarify color state

The media path is **shared across both states**; only the background color/alpha varies by state. To avoid implying the dropdown controls the file:

1. Keep **Image or Video:** with its ? tooltip and shared file row at the top.
2. Keep **Background color:** with its swatch/hex beneath the file row.
3. **Move the State dropdown below Background Color**, closely grouped with that control.
4. Rename the dropdown label **`State:` → `Background color state:`**.
5. Preserve both state-specific background colors/alpha values, the state switching behavior, and the shared media path. Do not make media state-specific.

Existing alpha explanation and media tooltip should be preserved. Ensure the state selector remains clear that it changes the color immediately above it.

### G. Game Info → PUBLISHER — clipped Alignment dropdown

The Font and Alignment dropdowns now share a row, but **Alignment is visibly clipped**. Adjust only local column sizing/spacing so the entire dropdown and arrow fit inside the pane while remaining beside Font. Preserve font, alignment, Bold functionality and existing bindings.

**Game Info → TITLE:** accepted. **Game Info → GAME INFO:** only divider adjustment in 1A. **ARTWORK PREVIEWS:** accepted, including its compact Drop Shadow. **FAVORITES STAR:** accepted, including its dividers.

### H. Game Info → PLAY BUTTON — local numeric-column and text layout

Use **Blur and Depth fields in the compact shadow rows** as anchor positions:

- Extend the **Button Text input** so its **RIGHT edge** aligns with **Blur field's RIGHT edge**.
- Move the **Vertical Nudge numeric input** to align its **LEFT edge with Depth's LEFT edge**, and match its numeric **width to Depth**.
- Show the full label **`Vertical nudge (px)`** to the right of the numeric field, never clipped or shortened to `Vert`. The user expects it to fit; verify actual panel width and handle locally if it does not.
- Move **Border Size** to begin directly under Blur and **Border Radius** to begin directly under Depth. Preserve Size 50px and Radius 60px widths; change **only this local Play Button Border row**, not the shared Border tab.
- Apply section 1A to all three Play Button dividers.
- **Hover States' three-color row and the compact Drop Shadow checkboxes look good**; do not modify them.

## 2. Confirmed passes — exclude from cleanup

- Search & Buttons → Buttons.
- Games List → Folders and Game Titles **except their divider spacing**. Their checkboxes are correct and they remain scrollbar-free.
- Marquee → Shadow and Logo tab controls, **except the two approved Logo display-text changes in section 1D**. Text Marquee checkboxes are correct.
- Splash Image → IMAGE, including its tooltip.
- Video Preview → FALLBACK IMAGE, including both tooltips and Background Color relocation.
- Game Info → TITLE; ARTWORK PREVIEWS; FAVORITES STAR (including dividers).
- Game Info → GAME INFO **except divider spacing**.
- Main Window Background → IMAGE OR VIDEO: keep the apparently repetitive tab name and `Image / video file:` header since the header carries the ? tooltip.
- **Shared BORDER tab:** its current 50px Size / 60px Radius fields and current wider spacing are visually accepted. **Do not change the shared Border tab or its consumers.** Text Marquee and Play Button contain independent local Border rows; only these are to be realigned.
- Scrollbars → Games List and Game Info: replacement explanatory text is confirmed. The intended label-to-control gap was already changed by the initial pass; no further change is currently requested. Confirm visually if convenient, but do not redesign.

## 3. Claude Chat / Claude Code workflow

1. Claude Chat should review the existing implementation and this cleanup list, distinguish unambiguous instructions from items needing technical feasibility checks, and create **one concise Claude Code cleanup handoff** if practical.
2. Claude Code starts with a **read-only focused audit**, checks current XAML anchors and whether the proposed alignments physically fit the panel. Report any conflicts (especially RG Text, Play Button full Nudge label, and one-row Marquee tabs) and **STOP for the user's GO**.
3. On approval, implement in one coordinated pass. **No Python. No app launch or automated UI interaction. No commits or pushes.** User conducts visual acceptance. Use file Read/Grep/Glob rather than shell for source inspection; minimize permission prompts with approved settings only.
4. A **final clean `dotnet build ArcadeStick.csproj --no-incremental`** should report 0 errors; report any warnings. Intermediate builds only if structurally warranted/troubleshooting. Check XAML references, `x:Name`/`Tag`/handlers, shadow `IsEnabled` bindings and line endings; report file changes and risk spots.
5. Leave `themes.md` and `CLAUDE.md` unchanged until the user accepts the cleanup visually. User controls documentation updates and commits afterward.

**For Marquee, only the approved `SPLASH LOGO` → `LOGO` tab rename and `Logo image:` → `Splash logo image:` heading change are authorized.** Do not modify tab padding, font size or spacing; report if the shorter name alone does not fit on one row.

## 4. Visual acceptance checks after cleanup

- Search: Font right edge → Color hex right edge; Alignment left edge → Background group left edge; Bold alignment; divider balanced above/below.
- Games List Folders/Titles: balanced dividers, checkboxes untouched, no scrollbar.
- Text Marquee: Max Size / Text Margin / Border Size left edges align to Blur; Radius aligns to Depth; widths and state dependencies preserved.
- Marquee tabs: `POSITION & SIZE | LOGO | TEXT MARQUEE | SHADOW`; `Splash logo image:` header retains the existing ? tooltip. Check whether the rename alone fits on one row; no tab padding/font/spacing changes.
- RG Text: correct revised order; full Font/Alignment/Wrap row; compact shadow toggles enable/disable its color, Blur, Depth.
- RG Content: file shared, color selector below swatch and explicitly labeled `Background color state:`.
- Publisher: Alignment combo fully visible.
- Game Info/Play Button: divider top gap matched to existing bottom gap; Button Text/Nudge and local Border aligned to Blur/Depth; full `Vertical nudge (px)` label displayed.
- Shared Border, Favorites Star dividers, Hover States, already-accepted tooltips and tabs unchanged.

**End of planning specification.**
