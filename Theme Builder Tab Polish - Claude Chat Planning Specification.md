# ADVANCED THEME BUILDER — REMAINING TAB LAYOUT POLISH
## Final Design Specification — Claude Chat Planning Review

**Status:** Design decisions finalized for Claude Chat review and implementation planning. No code changes are authorized by this document.

**Purpose:** Provide Claude Chat with the complete agreed UI-polish requirements so it can review the design, assess dependencies and risks, decide how to organize the work into implementation passes, and prepare precise Claude Code handoff Markdown files. The completed Position & Size, Border, and shared-control passes provide the established layout conventions.

**Scope:** UI layout and presentation only. Preserve all existing property values, bindings, behavior, enable/disable logic, persistence, and functionality unless explicitly specified otherwise.

---

## RECIPIENT AND RESPONSIBILITIES

**Primary recipient: Claude Chat — reviewer, planner, and handoff author.** This document is a design source of truth, **not** a direct implementation command to Claude Code.

Claude Chat should:

1. Read and review **the entire specification**, reconciling the inventory, tab-specific requirements, shared layout conventions, and verification criteria. Flag contradictions, technical uncertainties, or decisions that need the user's input; do not quietly redesign approved layouts.
2. Assess which changes are linked by shared controls, layout helpers, tab ownership, or risk of regression. **Choose the number, grouping, and order of implementation passes** that make the most sense. One pass is acceptable if justified; multiple smaller passes are equally acceptable. No fixed pass count or all-at-once implementation is required.
3. Prepare the **actual Claude Code handoff `.md` files** for the chosen passes. Each handoff must define scope, affected areas to investigate, detailed requested edits, preservation constraints, audit-first instructions, and concrete verification. Carry forward the relevant sections of this specification without losing details.
4. Require **an audit-first stage for Claude Code**: inspect the live codebase and shared dependencies **without editing**, report findings, discrepancies, implementation risks and an intended change plan, then **stop and wait for the user's explicit GO** before implementing that pass. The user may approve, revise, or rescope the plan after each audit.
5. Plan validation and documentation updates after implementation and visual testing. **Do not ask Claude Code to commit or push automatically.** The user handles final acceptance and Git actions.

**Planning deliverable:** A recommended pass breakdown with rationale, identified questions/risks, and the corresponding ready-to-use Claude Code handoff documents. Claude Chat should remain involved in reviewing audit results and adjusting subsequent handoffs as necessary.

---

## IMPLEMENTATION SCOPE / INVENTORY

- [ ] Search & Buttons — Search, Buttons
- [ ] Games List — Folders, Game Titles
- [ ] Marquee — Splash Logo (formerly Logo), Text Marquee (formerly Banner), Shadow; reorder tabs
- [ ] Splash Image — Image
- [ ] Random Game Display — Text, Content (formerly Background)
- [ ] Video Preview — consolidate Background into Fallback Image; reorder to Position & Size, Fallback Image, Border
- [ ] Game Info — Title, Publisher, Game Info, Artwork Previews (formerly Previews), Play Button, Favorites Star
- [ ] Scrollbars — coordinated Games List and Game Info scrollbar panel polish
- [ ] Main Window Background — Image or Video (formerly Image)
- [ ] Shared Border tabs — Size and Radius input width adjustment

The following sections capture agreed design outcomes, not permission to invent additional controls or behavior. Claude Chat determines whether these should be implemented together or in multiple dependency-aware passes, with a fresh audit and approval gate for each Claude Code handoff.

---

## ESTABLISHED DESIGN CONVENTIONS

Apply these principles where appropriate, rather than enforcing identical layouts on unrelated controls.

- Maintain consistent label placement within related rows and groups.
- Avoid adjacent rows with conflicting above/below label arrangements that cause labels to visually compete.
- Related combo boxes may share a horizontal row, with their labels above.
- Checkboxes may share a header row when their association is clear.
- Standalone numeric fields may use explanatory labels to their right.
- Use dividers where they establish a meaningful functional or visual grouping.
- Preserve useful whitespace; do not force unrelated controls together merely to fill a row.
- Reuse established field widths, spacing, styling, and shared layout patterns where applicable.
- Preserve scrolling where content exceeds the available height.
- Do not change unrelated tabs or controls.
- Preserve established header/checkbox alignment, including the successful `UseLayoutRounding = true` approach for affected heading rows where appropriate; avoid per-checkbox one-pixel margin workarounds.
- Treat values in ASCII diagrams as illustrative, not default-setting changes. Keep every user's existing values, limits, bindings, enable/disable logic, media behavior, and persistence.
- Text labels and supported-media formats in this specification reflect the UI reviewed during planning. During the audit, verify these against the current code and file-dialog filters. Report any discrepancy rather than silently broadening format support or altering behavior.
- Respect the intentional exceptions: the Random Game Display Text and Search Text shadow groups keep their standalone toggles and extra spacing; the compact checkbox-in-place-of-Shadow-Color layout is used only where explicitly called for.
- Preserve distinct effects: generated Text Marquee lettering shadow, shared Marquee shadow, and each independent text/button/star shadow must not be merged.
- Where layouts are tight (especially the Play Button three-color Hover States row), implement the specified intention first and flag any constraints for visual review. Do not silently omit controls or squeeze fields to unusable sizes.

---

## SEARCH & BUTTONS

### SEARCH TAB

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Reorganize the existing Search tab properties into a consistent, compact layout while preserving the distinction between the Search Label and Search Box elements.

The Search tab controls two separate elements:

- **Search Label:** The label text displayed alongside the search field.
- **Search Box:** The text-entry field itself.

Preserve all existing properties, bindings, values, behaviors, and enable/disable relationships.

#### Search Label — Font and Alignment

Place Font and Alignment combo boxes side by side in a two-column layout.

Place the Bold checkbox immediately beside the Font header.

Target arrangement:

```text
SEARCH LABEL

Font:  [ ] Bold             Alignment:

[ Font dropdown         ]   [ Alignment dropdown ]
```

Requirements:

- Font and Alignment labels above their respective combo boxes.
- Bold checkbox beside Font, following the established header/checkbox convention.
- Both dropdowns occupy the same horizontal row.
- Preserve existing options, values, bindings, and behavior.
- Use the available panel width with appropriate spacing between columns.

#### Search Label — Size and Colors

Place Font Size, Color, and Background Color on one horizontal row.

Target arrangement:

```text
Font size:    Color:             Background color:

[ 14 ]       [■] [#FFF6E9DB]    [■] [#FE35243F]
```

Values shown are illustrative; preserve the actual existing values.

Requirements:

- Labels above their corresponding controls.
- Font Size uses a compact numeric input.
- Both color properties retain their color swatches and hex fields.
- Follow established numeric and color-control sizing conventions.
- Preserve existing bindings, validation, and behavior.
- Maintain consistent spacing between the three groups.

#### Search Label — Vertical Nudge

Place the existing Vertical Nudge numeric field on its own row, with the explanatory label immediately to its right.

Target arrangement:

```text
[ 0 ]  Vertical nudge (px)
```

Requirements:

- Numeric field on the left, explanatory label on the right.
- Use the established compact numeric-field convention.
- Preserve existing value, limits, validation, and behavior.
- This property remains part of Search Label font positioning.
- Do not introduce an additional divider around this control.

#### Search Label — Drop Shadow

Keep the existing Drop Shadow checkbox and its dependent shadow controls.

Use slightly increased vertical spacing above the Drop Shadow checkbox to distinguish the optional effect from the main text properties.

**Do not add a divider above Drop Shadow.**

Target arrangement:

```text
[ ] Drop Shadow

Shadow color:          Blur:       Depth:
[■] [#FF100B17]        [ 3 ]       [ 1 ]
```

Values shown are illustrative.

Requirements:

- Preserve the existing Drop Shadow checkbox.
- Preserve shadow color, blur, and depth controls.
- Retain their general arrangement and established field widths.
- Use spacing rather than a divider above the shadow group.
- Preserve the existing enable/disable behavior of the dependent controls.
- Preserve all shadow values, bindings, and persistence.

#### Divider — Search Label / Search Box Boundary

Insert one subtle horizontal divider **below the Search Label Drop Shadow controls and above the Search Box heading**.

Purpose:

- Visually separate the two distinct elements being edited.
- Establish a clear transition from Search Label properties to Search Box properties.

Use consistent spacing around the divider.

Do not surround Drop Shadow with two dividers.

#### Search Box — Font Size and Colors

Preserve the existing Search Box section and its properties.

Arrange Font Size, Color, and Background Color in a single horizontal row, following the same layout conventions used for the Search Label's corresponding properties.

Target arrangement:

```text
SEARCH BOX

Font size:    Color:             Background color:

[ 13 ]       [■] [#FFF6E9DB]    [■] [#FF000000]
```

Values shown are illustrative.

Requirements:

- Retain the Search Box section heading.
- Font Size, Color, and Background Color share one row.
- Labels appear above their respective controls.
- Use consistent sizing and horizontal spacing with the Search Label section.
- Preserve existing values, bindings, validation, and behavior.
- Do not introduce unrelated properties or additional controls.

#### Overall Structure

The completed Search tab should follow this visual hierarchy:

```text
SEARCH LABEL

Font / Bold                 Alignment
Font dropdown               Alignment dropdown

Font Size       Color       Background Color
Vertical Nudge

(extra vertical spacing)
Drop Shadow
Shadow Color / Blur / Depth

--------------------------------------------

SEARCH BOX

Font Size       Color       Background Color
```

Design principles:

- Keep related dropdowns together.
- Keep label placement consistent across related rows.
- Place standalone explanatory labels to the right of their numeric fields where appropriate.
- Use spacing to distinguish the optional Drop Shadow group.
- Use a divider only to separate Search Label from Search Box.
- Avoid unnecessary controls, dividers, or decorative groupings.
- Preserve existing scrolling behavior if content exceeds the available area.

#### Verification

After implementation, verify:

- [ ] Font and Alignment dropdowns share one row.
- [ ] Bold checkbox sits beside the Font header.
- [ ] Search Label Font Size, Color, and Background Color share one row.
- [ ] Vertical Nudge has its label to the right.
- [ ] Drop Shadow has extra spacing above it, without a divider.
- [ ] Shadow controls retain their existing enable/disable behavior.
- [ ] A single divider separates Search Label from Search Box.
- [ ] Search Box Font Size, Color, and Background Color share one row.
- [ ] Labels use consistent placement within related groups.
- [ ] All properties retain their existing values, bindings, validation, and functionality.
- [ ] No clipping, overlap, or unintended changes to scrolling.
- [ ] Layout is visually consistent at the standard display size.

---

### BUTTONS TAB

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Reorganize the existing Buttons tab properties into two compact, consistently aligned rows while preserving all existing values, bindings, and functionality.

#### Normal Appearance — First Row

Place Font Size, Color, and Background Color on a single horizontal row.

Target arrangement:

```text
Font size:    Color:               Background:
[ 16 ]       [■] [#FFF6E9DB]      [■] [#FF35243F]
```

Values shown are illustrative; preserve the existing values.

Requirements:

- All three property groups share the same row.
- Labels appear above their respective controls and align horizontally.
- Numeric and color controls align vertically despite their different widths.
- Font Size retains a compact numeric field.
- Color and Background retain their swatches and hex inputs.
- Follow established sizing and spacing conventions.

#### Hover Appearance — Second Row

Place Hover Color and Hover Background on a second horizontal row, directly beneath the normal appearance controls.

Target arrangement:

```text
Font size:    Color:                    Background:
[ 16 ]       [■] [#FFF6E9DB]           [■] [#FF35243F]

Hover color:                           Hover BG:
[■] [       #FFF6E9DB       ]          [■] [#FF66435D]
```

Values shown are illustrative.

**Alignment requirement:**

The priority is maintaining consistent horizontal group boundaries and gaps between the two rows, not matching individual textbox widths.

- The Background and Hover Background groups must begin at the same horizontal position.
- The gap separating Color from Background must visually match the gap separating Hover Color from Hover Background.
- Hover Color occupies the combined horizontal space used by Font Size and Color in the first row.
- Allow the Hover Color hex field to expand as needed to achieve the intended alignment.
- Do not widen the shared color hex controls globally; any additional width belongs only to the Hover Color field in this layout.
- Do not introduce an empty placeholder field merely to reproduce the first row's three controls.

The result should create a consistent **invisible vertical alignment line** through the gaps between the property groups, even though the two rows contain different numbers of controls.

#### Spacing and Presentation

- Maintain consistent above-control label placement throughout the tab.
- Use a modest vertical gap between normal and hover appearance rows.
- No divider is required; the Hover labels already distinguish the second row.
- Preserve the existing color picker behavior, alpha support, validation, bindings, and persistence.
- Do not introduce new properties or change the behavior of existing controls.

#### Verification

After implementation, verify:

- [ ] Font Size, Color, and Background share the first row.
- [ ] Hover Color and Hover Background share the second row.
- [ ] Labels align consistently above their corresponding controls.
- [ ] Controls within each row share consistent vertical alignment.
- [ ] Background and Hover Background begin at the same horizontal position.
- [ ] The horizontal gaps between the color groups visually align across both rows.
- [ ] Hover Color expands appropriately without modifying global color-field sizing.
- [ ] No unnecessary placeholder field or divider is introduced.
- [ ] All values, bindings, validation, color picker behavior, and persistence remain unchanged.
- [ ] No clipping, overlap, or unintended scrolling changes occur.

---

## GAMES LIST

### FOLDERS TAB

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Reorganize the Folders tab to improve grouping, consistency, and vertical space efficiency while preserving all existing properties and functionality. Aim to retain the current scrollbar-free layout.

#### Font and Bold

Keep the existing Font combo box, moving the Bold checkbox beside the Font header.

Target arrangement:

```text
Font:  [ ] Bold
[ Segoe UI                        ▼ ]
```

Requirements:

- Font label and Bold checkbox share the header row.
- Bold sits immediately beside Font using the established header/checkbox alignment pattern.
- Font dropdown remains directly beneath the header.
- Preserve existing font selection, Bold behavior, and bindings.

#### Size and Color

Keep Font Size and Color together on one horizontal row.

```text
Size:        Color:
[ 14 ]       [■] [#FFF6E9DB]
```

Requirements:

- Labels remain above their controls.
- Both property groups share consistent vertical alignment.
- Preserve the existing numeric, color swatch, and hex input functionality.
- Follow established field sizing and spacing conventions.

#### Normal Folder Drop Shadow

Consolidate the existing Drop Shadow checkbox and Shadow Color controls into a single two-row arrangement.

**Replace the Shadow Color label with the existing Drop Shadow checkbox.**

```text
[✓] Drop shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]
```

Requirements:

- Remove the separate Drop Shadow checkbox row.
- Remove the redundant Shadow Color text label.
- Place the existing Drop Shadow checkbox directly above the shadow color swatch/hex control.
- Align Blur and Depth labels with the Drop Shadow checkbox row.
- Align all three property controls on the row beneath.
- Retain white checkbox text rather than styling it as an orange property label.
- Preserve the existing shadow toggle behavior and dependent-control enable/disable logic.
- Preserve all shadow values, bindings, validation, and persistence.
- Avoid introducing additional vertical spacing around this group.

#### Divider — Normal and Selected Folder Properties

Insert one subtle horizontal divider immediately below the normal folder Drop Shadow controls and above the Selected Text / Selected Background properties.

Purpose:

- Clearly distinguish normal folder appearance from selected folder appearance.
- Reinforce the transition between the two sets of related properties.
- Improve readability without introducing unnecessary section headers.

Requirements:

- Use modest vertical spacing around the divider.
- Do not add dividers above or below individual Drop Shadow groups.
- Aim to preserve the current scrollbar-free tab layout.

#### Selected Text and Background

Keep Selected Text and Selected Background together on one horizontal row.

```text
Selected text:             Selected BG:
[■] [#FFFFCC00]           [■] [#FF35243F]
```

Requirements:

- Preserve the existing labels, color swatches, and hex inputs.
- Labels remain above their controls.
- Maintain consistent vertical alignment and group spacing.
- Preserve existing values, bindings, alpha handling, and color picker behavior.

#### Selected Folder Drop Shadow

Apply the same consolidated Drop Shadow arrangement used for the normal folder appearance.

```text
[✓] Drop shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]
```

Requirements:

- Replace the existing Shadow Color label with the Drop Shadow checkbox.
- Eliminate the separate checkbox row.
- Keep the checkbox directly above the shadow color control.
- Align Blur and Depth labels and controls consistently with the normal folder shadow group.
- Preserve the independent selected-folder shadow settings.
- Preserve existing enable/disable behavior, values, bindings, and persistence.
- Do not introduce another divider.

#### Arrow Color and Nudge

Retain Arrow Color and Nudge together on the bottom row.

```text
Arrow color:               Nudge:
[■] [#FFF6E9DB]           [ 0 ]
```

Requirements:

- Keep both labels above their respective controls.
- Maintain consistent vertical alignment.
- Preserve existing color, numeric, and nudge behavior.
- Do not move the Nudge label to the right in this pass.

#### Overall Target Layout

```text
Font:  [ ] Bold
[ Segoe UI                        ▼ ]

Size:        Color:
[ 14 ]       [■] [#FFF6E9DB]

[✓] Drop shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]

────────────────────────────────────────

Selected text:             Selected BG:
[■] [#FFFFCC00]           [■] [#FF35243F]

[✓] Drop shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]

Arrow color:               Nudge:
[■] [#FFF6E9DB]           [ 0 ]
```

Values and widths shown are illustrative. Preserve existing values and apply established Theme Builder sizing conventions.

#### Verification

- [ ] Bold checkbox appears beside the Font header.
- [ ] Font dropdown remains beneath its header.
- [ ] Size and Color share one aligned row.
- [ ] Normal Drop Shadow checkbox replaces the Shadow Color label.
- [ ] Normal shadow color, Blur, and Depth controls align on one row.
- [ ] Normal shadow settings retain their enable/disable behavior.
- [ ] One divider separates normal and selected folder appearance properties.
- [ ] Selected Text and Selected Background share one row.
- [ ] Selected Drop Shadow uses the same compact layout as normal Drop Shadow.
- [ ] Selected shadow settings remain independent from normal shadow settings.
- [ ] Arrow Color and Nudge remain aligned on the bottom row.
- [ ] No unnecessary additional dividers or spacing are introduced.
- [ ] Tab remains scrollbar-free if achievable without clipping or crowding.
- [ ] All existing bindings, values, validation, color pickers, and functionality are preserved.
- [ ] No clipping, overlap, or unexpected layout changes occur.

---

### GAME TITLES TAB

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Apply the same layout improvements specified for Games List → Folders to the Game Titles tab, preserving its independent properties, values, bindings, and behavior.

#### Layout Requirements

- Follow the **Folders tab specification** for Font and Bold placement, Size and Color alignment, and both consolidated Drop Shadow groups.
- Use the same compact Drop Shadow arrangement, with the checkbox replacing the Shadow Color label.
- Insert the same subtle divider between normal and selected text appearance properties.
- Preserve the same vertical spacing, field widths, alignment, and styling conventions established for Folders.
- Retain the existing independent normal and selected Drop Shadow toggles and their dependent-control behavior.

#### Final Row — Hover Color

The only structural difference from Folders is the final row.

Instead of Folders' **Arrow Color + Nudge** arrangement, Game Titles has **Hover Color** alone.

```text
Hover color:
[■] [#FFF6E9DB]
```

Requirements:

- Keep the Hover Color label above its swatch and hex input.
- Preserve the existing color control sizing and behavior.
- Do not add a Nudge field or an empty placeholder column.
- Preserve the same overall row spacing as Folders.

#### Verification

- [ ] Font, Size, Color, selected appearance, and Drop Shadow groups match the Folders layout.
- [ ] Both Drop Shadow groups use the consolidated checkbox/header arrangement.
- [ ] One divider separates normal and selected appearance properties.
- [ ] Hover Color occupies the final row by itself.
- [ ] All Game Titles properties retain their independent values, bindings, and functionality.
- [ ] No clipping, overlap, or unintended scrolling changes occur.

---

## MARQUEE

### SPLASH LOGO TAB (formerly LOGO)

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Rename the existing Logo tab to Splash Logo and simplify the media-selection heading using the established help-tooltip convention. Preserve all existing functionality.

#### Tab Name

- Rename the existing **LOGO** tab to **SPLASH LOGO**.
- This is a display-label change only. Preserve existing property names, bindings, stored values, and behavior.

#### Logo Image — Header and Tooltip

Replace the existing long heading:

**"Logo image (png, jpg), shown when no game is selected:"**

With:

**Logo image:** followed immediately by a small **?** help glyph.

Use the same header/tooltip arrangement established for the Custom Layer Content tab and Random Game Display Content tab.

Tooltip content:

- **Supported formats:** PNG, JPG.
- The logo appears only on the splash page when no game is selected.
- The Splash Logo shares the Marquee layer's Position & Size settings.

Requirements:

- Keep the header and help glyph on the same horizontal row.
- Preserve the existing file path field, Browse button, and Clear button directly beneath the header.
- Keep all three file controls on one horizontal row.
- Preserve existing file filtering, media selection, clearing behavior, bindings, and persistence.
- Do not introduce video support or change the supported file types.

#### Empty Logo Explanation

Retain the existing explanatory text directly beneath the file-selection controls:

**"Leave empty to show no logo."**

Preserve its existing styling and behavior.

#### Target Layout

```text
SPLASH LOGO

Logo image: (?)
[ Image path................ ] [ Browse ] [ Clear ]

Leave empty to show no logo.
```

Follow the established Theme Builder field widths, spacing, tooltip styling, and control alignment conventions.

#### Functional Boundaries

This is a presentation-only change.

- Do not modify the shared Marquee Position & Size behavior.
- Do not modify the Splash Logo's visibility rules.
- Do not change the Random Game animation or how it temporarily replaces existing marquee-area content.
- Do not modify the separate Shadow or Text Marquee properties.
- Do not introduce independent positioning, shadow settings, or other new functionality.

#### Verification

- [ ] LOGO tab is renamed SPLASH LOGO.
- [ ] Long Logo Image heading is replaced with "Logo image:" and a ? help glyph.
- [ ] Tooltip lists PNG and JPG as supported formats.
- [ ] Tooltip explains that the logo appears only on the splash page.
- [ ] Tooltip identifies the shared Marquee positioning relationship.
- [ ] Path field, Browse, and Clear remain on one row.
- [ ] Existing file selection and clearing behavior are unchanged.
- [ ] "Leave empty to show no logo." remains below the file controls.
- [ ] No video support or unrelated functionality is introduced.
- [ ] No clipping, overlapping controls, or unintended layout changes occur.


---

### TEXT MARQUEE TAB (formerly BANNER)

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Rename the existing Banner tab to Text Marquee and reorganize its controls into a logical progression from text styling to background and border appearance. Preserve the existing generated-marquee behavior and all property bindings.

#### Tab Name and Order

- Rename **BANNER** to **TEXT MARQUEE**.
- Position Text Marquee immediately after Splash Logo and before Shadow.
- Target tab order: **POSITION & SIZE → SPLASH LOGO → TEXT MARQUEE → SHADOW**.
- Treat these as display-label and tab-order changes only. Preserve internal property names, stored values, and functionality.

#### Preview Text Marquee

Rename the existing **Preview banner** checkbox to **Preview text marquee**.

- Keep the checkbox at the top of the tab.
- Preserve its existing ability to temporarily display the generated marquee in the launcher while editing.
- Preserve how the preview temporarily replaces the normal marquee display.
- Do not modify the generated marquee's fixed 4:1 aspect ratio.
- Preserve its usefulness as a visual alignment reference for the shared Marquee layer.
- Do not change the normal fallback behavior: the generated marquee appears when a selected game has no marquee artwork.

#### Font and Bold

Place the Bold checkbox directly beside the Font header, following the established header/checkbox alignment convention.

```text
Font:  [✓] Bold
[ Arial Black                       ▼ ]
```

Requirements:

- Font label and Bold checkbox share the header row.
- Font dropdown remains directly beneath.
- Preserve font options, values, bindings, and Bold behavior.
- Reuse the established checkbox alignment pattern.

#### Font Color and Maximum Size

Place Color and Max Size together on one horizontal row, with Color first.

```text
Color:                       Max size:
[■] [#FFF6E9DB]              [ 30 ]
```

Requirements:

- Labels above their corresponding controls.
- Both controls share consistent vertical alignment.
- Preserve the existing color swatch, hex input, and numeric control.
- Retain existing Max Size limits, validation, and behavior.
- Follow established field sizing and spacing conventions.

#### Wrap Text and Text Margin

Keep Wrap Text and Text Margin together on one horizontal row.

Place the Text Margin numeric field before its explanatory label.

```text
[✓] Wrap Text       [ 5 ] Text Margin (units)
```

Requirements:

- Preserve the Wrap Text checkbox and its existing behavior.
- Keep the Text Margin numeric field and label together.
- Use the established compact numeric-field convention.
- Retain **(units)** rather than changing the label to pixels.
- Preserve existing Text Margin values, limits, calculations, and bindings.
- Preserve the current text-sizing behavior when wrapping is enabled or disabled.
- Do not introduce new dependencies or enable/disable rules between these controls.

#### Text Drop Shadow

Move the existing text Drop Shadow controls above Background Color, keeping them with the text appearance properties.

Use the compact shadow-control layout established for Games List.

**Replace the existing Shadow Color label with the Drop Shadow checkbox.**

```text
[✓] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]
```

Requirements:

- Rename **Drop Shadow (text)** to **Drop Shadow** within this tab.
- Remove its separate checkbox row.
- Remove the redundant Shadow Color label.
- Position the checkbox directly above the shadow color swatch and hex input.
- Align Blur and Depth labels with the checkbox row.
- Align all three property controls beneath their respective labels.
- Retain normal white checkbox text.
- Preserve all existing shadow values, bindings, validation, and persistence.
- Preserve the checkbox's existing enable/disable behavior for dependent shadow controls.
- This shadow affects only the generated marquee's lettering. Do not combine it with the separate Marquee Shadow tab's effect.
- No divider is required above or below this group.

#### Background Color

Move Background Color directly below the text Drop Shadow group.

Keep it on its own row.

```text
Background:
[■] [#FF1B1528]
```

Requirements:

- Preserve the existing Background label and color controls.
- Keep the label above the swatch and hex input.
- Preserve existing alpha support, values, bindings, and color picker behavior.
- Do not attempt to combine Background with the Border controls.
- Use modest vertical spacing to distinguish the transition from text properties to marquee background appearance.

#### Border Color, Size, and Radius

Keep Border Color, Size, and Radius together on one horizontal row, directly beneath Background Color.

```text
Border color:              Size:       Radius:
[■] [#FFA07780]            [ 3 ]      [ 0 ]
```

Requirements:

- Preserve the existing Border Color swatch and hex input.
- Preserve Size and Radius numeric controls.
- Keep all three labels above their controls.
- Maintain consistent vertical alignment and horizontal spacing.
- Align the left edge of the Border Color group with the Background Color group above it.
- Preserve existing border values, limits, validation, and rendering behavior.
- Do not add a divider between Background and Border.
- Any proposed changes to the shared Border tab's numeric field widths are specified in the separate Shared Border Tabs section. Do not change shared Border controls as part of this tab redesign.

#### Overall Target Layout

```text
[✓] Preview text marquee

Font:  [✓] Bold
[ Arial Black                       ▼ ]

Color:                       Max size:
[■] [#FFF6E9DB]              [ 30 ]

[✓] Wrap Text       [ 5 ] Text Margin (units)

[✓] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]

Background:
[■] [#FF1B1528]

Border color:              Size:       Radius:
[■] [#FFA07780]            [ 3 ]      [ 0 ]
```

Values shown are illustrative. Preserve the actual existing values and property behavior.

The layout should follow this visual progression:

**Font → Font Color → Text Size and Wrapping → Text Shadow → Background → Border**

Use consistent label placement, established numeric and color-control conventions, and appropriate vertical spacing. Avoid unnecessary dividers or decorative grouping.

#### Functional Boundaries

This is a layout and terminology update only.

- Do not modify the generated marquee's fixed 4:1 aspect ratio.
- Do not change how game titles are fitted, resized, wrapped, or positioned.
- Do not change when the generated marquee appears as a fallback for missing artwork.
- Do not change the shared Marquee Position & Size behavior.
- Do not change the Random Game animation.
- Do not change the independent text shadow or the separate shared Marquee Shadow system.
- Preserve all existing bindings, values, validation, enable/disable behavior, and persistence.

#### Verification

- [ ] BANNER tab is renamed TEXT MARQUEE.
- [ ] Text Marquee appears between Splash Logo and Shadow.
- [ ] Preview banner checkbox is renamed Preview text marquee.
- [ ] Preview behavior and fixed 4:1 aspect ratio remain unchanged.
- [ ] Bold checkbox appears beside the Font header.
- [ ] Color appears before Max Size on a shared row.
- [ ] Wrap Text and Text Margin share a row.
- [ ] Text Margin retains its existing units and behavior.
- [ ] Text Drop Shadow appears above Background Color.
- [ ] Drop Shadow checkbox replaces the Shadow Color label.
- [ ] Shadow Color, Blur, and Depth controls share one aligned row.
- [ ] Text shadow enable/disable behavior remains unchanged.
- [ ] Background Color occupies its own row beneath text styling.
- [ ] Background Color and Border Color groups share the same left alignment.
- [ ] Border Color, Size, and Radius share one row.
- [ ] No unnecessary dividers are introduced.
- [ ] All values, bindings, validation, persistence, and functionality are preserved.
- [ ] No clipping, overlap, or unintended scrolling changes occur.

---

### SHADOW TAB

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Simplify the shared Marquee Shadow tab, remove unnecessary explanatory text, and standardize terminology while preserving the existing shadow/glow functionality.

#### Tab Position

- Move **SHADOW** to the last tab in the Marquee layer.
- Target order: **POSITION & SIZE → SPLASH LOGO → TEXT MARQUEE → SHADOW**.
- Preserve all existing property bindings, values, and behavior.

#### Remove Redundant Text

Remove the following heading:

**"Logo and marquee image shadow"**

Remove the Glow tip:

**"Glow: set Depth to 0 and pick a bright color."**

Do not replace these with a tooltip or additional explanatory text.

#### Drop Shadow Toggle

Keep the existing **Drop Shadow** checkbox at the top of the tab.

- Preserve its existing label.
- Preserve the existing enable/disable behavior of all dependent shadow controls.
- Do not move the checkbox into the Shadow Color group.
- Do not change how the shadow effect is rendered.

#### Apply Shadow To

Keep the existing **Apply shadow to:** dropdown directly below the Drop Shadow checkbox.

- Label remains above the combo box.
- Preserve all existing choices and their underlying behavior.
- Preserve the ability to apply the shared effect to the Splash Logo, Marquees, both, or neither, as currently supported.

Replace the existing explanatory text:

**"Marquees includes the no-art title card."**

With:

**"Marquees include the Text Marquee."**

Keep this explanation directly beneath the dropdown.

#### Shadow Color, Blur, and Depth

Keep the existing three property groups on one horizontal row beneath the Apply Shadow To explanation.

```text
Shadow color:          Blur:       Depth:
[■] [#FF100B17]        [ 3 ]       [ 1 ]
```

Requirements:

- Labels remain above their controls.
- Preserve the existing color swatch, hex field, Blur, and Depth controls.
- Maintain consistent horizontal spacing and vertical alignment.
- Preserve all values, limits, validation, bindings, and persistence.
- Do not introduce new dividers or additional controls.
- Do not change the independent text-only Drop Shadow setting in the Text Marquee tab.

#### Overall Target Layout

```text
[ ] Drop Shadow

Apply shadow to:
[ Both                  ▼ ]
Marquees include the Text Marquee.

Shadow color:          Blur:       Depth:
[■] [#FF100B17]        [ 3 ]       [ 1 ]
```

Values shown are illustrative. Preserve the existing selected values and behavior.

The intended reading order is:

**Enable Effect → Select Target → Configure Shadow**

Use established Theme Builder spacing and styling conventions.

#### Functional Boundaries

This is strictly a layout, tab-order, and terminology update.

- Do not introduce separate shadow settings for the Splash Logo, regular Marquee, or Text Marquee.
- Do not change the shared shadow system or how the target selection is interpreted.
- Do not change the independent Drop Shadow applied to the generated Text Marquee's lettering.
- Do not change the Random Game animation or shared Marquee Position & Size behavior.
- Do not introduce additional glow controls or alter the existing glow capability.

#### Verification

- [ ] SHADOW appears last in the Marquee tab order.
- [ ] Redundant heading is removed.
- [ ] Glow tip is removed without replacement.
- [ ] Drop Shadow checkbox remains at the top.
- [ ] Apply Shadow To dropdown remains beneath the checkbox.
- [ ] All existing target choices work unchanged.
- [ ] Explanation reads "Marquees include the Text Marquee."
- [ ] Shadow Color, Blur, and Depth remain aligned on one row.
- [ ] Shared shadow enable/disable behavior is preserved.
- [ ] Text Marquee's independent text shadow remains unaffected.
- [ ] All existing values, bindings, validation, and persistence are preserved.
- [ ] No clipping, overlap, or unintended layout changes occur.

---

## SPLASH IMAGE

### IMAGE TAB

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Simplify the existing Splash Image media-selection heading using the established help-tooltip convention. Preserve all existing functionality.

#### Image or Video — Header and Tooltip

Replace the existing long heading:

**"Splash image or video (png, jpg, gif, mp4), shown when no game is selected:"**

With:

**Splash image or video:** followed immediately by a small **?** help glyph.

Use the same header/tooltip arrangement established for the other media-selection tabs.

Tooltip content:

- **Supported formats:** PNG, JPG, GIF, MP4.
- Displayed when no game is selected, if a splash image or video is configured.

Requirements:

- Keep the header and help glyph on the same horizontal row.
- Preserve the existing file path field, Browse button, and Clear button directly beneath the header.
- Keep all three file controls on one horizontal row.
- Preserve existing file filtering, media selection, clearing behavior, bindings, and persistence.
- Do not introduce new controls or change supported file types.

#### Empty Splash Explanation

Retain the existing explanatory text directly beneath the file-selection controls:

**"Leave empty to show no splash."**

Preserve its existing styling.

#### Target Layout

```text
Splash image or video: (?)
[ Image/video path.......... ] [ Browse ] [ Clear ]

Leave empty to show no splash.
```

Follow established Theme Builder field widths, spacing, tooltip styling, and control alignment conventions.

#### Functional Boundaries

This is strictly a UI presentation change.

- Do not modify when the Splash Image is displayed.
- Do not modify existing image or video playback behavior.
- Do not introduce a Visibility selector or other new properties.
- Do not change the behavior when the media path is empty.
- Preserve all existing bindings, values, file selection behavior, and persistence.
- Do not modify the separate Splash Logo or Marquee functionality.

#### Verification

- [ ] Heading shortened and ? tooltip displays the correct information.
- [ ] Path field, Browse, Clear, and empty-path explanation remain unchanged.
- [ ] Existing media selection, playback, and visibility behavior is preserved.
- [ ] No clipping, overlap, or unintended layout changes.

---

## RANDOM GAME DISPLAY

### TEXT TAB

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

#### Message

Preserve the existing Message property and its functionality.

No specific layout change has been agreed upon for this control. Retain its current arrangement unless a minor adjustment is necessary to accommodate the revised sections.

#### Maximum Text Size

Replace the current arrangement of the two maximum text size controls with a shared heading.

Heading:

**Max text size (px):**

Place two compact rows beneath it:

```text
Max text size (px):

[ 40  ]  When a game is selected
[ 100 ]  When on the splash page
```

The numbers above are illustrative; retain the actual existing values and bindings.

Requirements:

- Shared heading above both rows.
- Numeric input field on the left.
- Explanatory label immediately to the right.
- Consistent input widths and horizontal alignment.
- Preserve existing limits, validation, values, and behavior.
- Do not introduce additional controls.

#### Font and Alignment

Place Font and Alignment combo boxes side by side in a two-column layout.

Place the Bold checkbox beside the Font header.

Place the Wrap Text checkbox beside the Alignment header.

Target arrangement:

```text
Font:  [ ] Bold          Alignment:  [ ] Wrap Text

[ Font dropdown      ]  [ Alignment dropdown     ]
```

Requirements:

- Font and Alignment headers above their respective combo boxes.
- Bold immediately beside Font.
- Wrap Text immediately beside Alignment.
- Dropdowns occupy the same horizontal row.
- Preserve existing options, bindings, and checkbox behavior.
- Use the established header/checkbox alignment pattern.

#### Color

Keep the existing text Color property on its own row beneath Font and Alignment.

- Preserve the existing color swatch and hex input.
- Follow the established color-control layout conventions.
- Do not introduce an unrelated property merely to fill the remaining horizontal space.

#### Drop Shadow

Use a slightly larger vertical gap above the existing Drop Shadow checkbox/group to visually distinguish the optional shadow effect from the main text properties.

**Do not insert a horizontal divider.**

Preserve the existing shadow controls and their general layout.

Requirements:

- Use extra vertical spacing above Drop Shadow rather than a divider.
- Drop Shadow checkbox remains the controlling toggle for its dependent properties.
- Existing shadow color, blur, and depth controls remain associated with that toggle.
- When Drop Shadow is unchecked, its dependent controls remain disabled, exactly as they are currently.
- Do not change shadow behavior, values, bindings, or persistence.

#### Overall Structure

The revised tab should read as three logical sections:

**Text content and sizing**
- Message
- Max text size (px), with two context-specific values

**Text appearance**
- Font + Bold
- Alignment + Wrap Text
- Color

**Optional shadow effect**
- Extra vertical spacing above Drop Shadow
- Drop Shadow checkbox
- Existing dependent shadow controls

Avoid unnecessary additional dividers or decorative grouping.

#### Verification

After implementation, verify:

- [ ] Font and Alignment combo boxes share one row.
- [ ] Bold sits beside the Font header.
- [ ] Wrap Text sits beside the Alignment header.
- [ ] Both maximum text sizes share one heading with context labels to the right.
- [ ] Color remains clearly associated with text appearance.
- [ ] Extra spacing distinguishes Drop Shadow from the main text properties; no divider is present.
- [ ] Shadow properties still enable/disable correctly.
- [ ] All controls retain their original bindings, limits, and behavior.
- [ ] No clipping, overlapping controls, or unintended changes to scrolling.
- [ ] Layout is visually consistent at the standard display size.

---

### CONTENT TAB (formerly BACKGROUND)

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

#### Tab Name

- Rename the existing **BACKGROUND** tab to **CONTENT**.
- This is a display-label change only; preserve all existing functionality and stored values.

#### State — Move to the Top

- Move the existing **State:** dropdown to the top of the Content tab, above the media file controls.
- Keep its label above the combo box.
- Preserve both existing state choices and the behavior that determines which state-specific media/background settings are being edited.
- The currently selected state must continue to control which image or video and background color values are displayed and edited, exactly as before.

#### Image or Video — Short Header and Tooltip

- Replace the current two-line heading, **“Image or video (png, jpg, gif, mp4), drawn over the color and under the message:”**, with the shorter **Image or Video:** header.
- Place a small **?** help glyph immediately beside the header, following the same pattern already established on the custom-layer Content tab.
- Tooltip should communicate both:
  - Supported formats: PNG, JPG, GIF, MP4.
  - The image or video is drawn over the background color and beneath the message text.
- Keep the existing file path field, **Browse**, and **Clear** together on their current horizontal row beneath the header.
- Preserve media selection, file filtering, Browse/Clear behavior, and state-specific binding and persistence.

#### Background Color — Separate Row

- Retain **Background color:** on its own row/group beneath the image/video selector.
- Keep the label above the existing swatch and hex input.
- Preserve the color control's existing value, alpha support, bindings, and behavior for each state.
- Retain the explanation below the color input: **“Lower the alpha to let the layers behind the card show through.”**
- Do not force Background Color to share a row with State or another unrelated control simply to balance the layout.

#### Target Layout

```text
State:
[ When a game is selected       v ]

Image or Video: (?)
[ File path........................ ] [ Browse ] [ Clear ]

Background color:
[ swatch ] [ #D21B1528 ]
Lower the alpha to let the layers behind the card show through.
```

The state selection and color value shown above are illustrative; preserve actual selected values and field behavior. Exact widths and vertical spacing should follow the established Theme Builder conventions and be visually verified.

#### Verification

- [ ] BACKGROUND tab is renamed CONTENT.
- [ ] State dropdown appears at the top with its label above it.
- [ ] Switching State still selects the corresponding state-specific values without mixing them.
- [ ] Image or Video header is shortened and has the ? tooltip.
- [ ] Tooltip mentions supported media formats and visual layering order.
- [ ] Path input, Browse, and Clear stay on a single row and work unchanged.
- [ ] Background Color occupies its own labeled group below the media selector.
- [ ] Alpha explanation remains visible beneath the color control.
- [ ] Existing file, color, and state behavior, bindings, and persistence are unchanged.
- [ ] No clipping, overlaps, or unintended tab/scrolling changes.

---

## VIDEO PREVIEW

### FALLBACK IMAGE TAB — BACKGROUND CONSOLIDATION

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Consolidate the existing Background Color property into the Fallback Image tab, eliminate the redundant Background tab, and simplify the image-selection heading. Preserve all existing functionality.

#### Tab Consolidation and Order

The current Video Preview layer contains four tabs:

**POSITION & SIZE | BORDER | BACKGROUND | FALLBACK IMAGE**

Consolidate these into three tabs in the following order:

**POSITION & SIZE → FALLBACK IMAGE → BORDER**

Requirements:

- Move the existing Background Color control from BACKGROUND into FALLBACK IMAGE.
- Remove the now-empty BACKGROUND tab.
- Reorder the remaining tabs exactly as specified.
- All three tabs should occupy a single tab row.
- Preserve the existing Position & Size and Border controls and their functionality.
- Do not modify shared Border field widths in this pass. Those adjustments are specified in the separate Shared Border Tabs section of this handoff.
- Preserve all existing property values, bindings, and persistence.

#### Fallback Image — Header and Tooltip

Replace the existing long heading:

**"Image (png, jpg, bmp), shown when the selected game has no preview:"**

With:

**Fallback image:** followed immediately by a small **?** help glyph.

Use the established media-selector header and tooltip convention.

Tooltip content:

- **Supported formats:** PNG, JPG, BMP.
- The fallback image is displayed when the selected game has no video preview.

Requirements:

- Keep the header and help glyph on the same horizontal row.
- Preserve the existing path field, Browse, and Clear controls directly beneath the header.
- Keep all three file controls on a single horizontal row.
- Preserve existing file filtering, image selection, clearing behavior, bindings, and persistence.
- Do not introduce video support or modify supported file types.

#### Empty Path Explanation

Change the existing explanatory text beneath the image-selection controls to:

**"Leave empty to use the default image."**

Requirements:

- Preserve the existing default fallback image behavior.
- Do not change the default or configured image-resolution logic.
- Keep the explanation directly beneath the file-selection row.

#### Background Color — Relocation

Move the existing Background Color property into the Fallback Image tab, beneath the empty-path explanation.

Keep Background Color on its own row/group.

```text
Background color:
[■] [#FF1B1528]
```

Requirements:

- Keep the Background Color label above its swatch and hex input.
- Preserve the existing color control, including alpha support.
- Preserve all values, bindings, color picker behavior, and persistence.
- Do not introduce additional background color settings.
- Do not change how the background color interacts with video playback or fallback imagery.
- Use established Theme Builder spacing and color-field sizing conventions.

#### Overall Target Layout

```text
POSITION & SIZE | FALLBACK IMAGE | BORDER
                 ^ active tab

Fallback image: (?)
[ Image path................ ] [ Browse ] [ Clear ]

Leave empty to use the default image.

Background color:
[■] [#FF1B1528]
```

The values shown are illustrative. Preserve actual configured values.

Use consistent label placement, established field widths, and appropriate vertical spacing. No dividers or additional grouping controls are required.

#### Functional Boundaries

This is strictly a tab consolidation, layout, and terminology update.

- Do not modify Video Preview Position & Size or aspect-ratio behavior.
- Do not alter the video playback system.
- Do not modify the conditions under which fallback imagery appears.
- Do not remove or change the built-in fallback image.
- Do not modify the underlying Background Color property, only its UI location.
- Do not alter shared Border functionality.
- Preserve all existing bindings, values, validation, file handling, and persistence.

#### Verification

- [ ] BACKGROUND tab is removed after its color control is relocated.
- [ ] POSITION & SIZE, FALLBACK IMAGE, and BORDER appear in that exact order on one row.
- [ ] Fallback Image heading is shortened and includes the ? help glyph.
- [ ] Tooltip lists PNG, JPG, and BMP and explains when fallback imagery appears.
- [ ] Image path, Browse, and Clear remain aligned on one row and work unchanged.
- [ ] Explanation beneath image controls reads "Leave empty to use the default image."
- [ ] Background Color appears beneath the image settings, with its label above the swatch and hex field.
- [ ] Existing background color values and behavior remain unchanged.
- [ ] Empty image path still uses the built-in fallback image.
- [ ] Video playback, fallback behavior, and aspect-ratio handling remain unchanged.
- [ ] Position & Size and Border controls remain unaffected.
- [ ] All existing bindings, persistence, and functionality are preserved.
- [ ] No clipping, overlap, or unexpected layout changes occur.

---

## GAME INFO

### TITLE TAB

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Apply the established font and compact Drop Shadow layout conventions to the Game Name Header controls. Preserve all existing functionality.

#### Font and Bold

Move the existing Bold checkbox beside the Font header.

```text
Font:  [✓] Bold
[ Segoe UI                  ▼ ]
```

- Font label and Bold checkbox share the header row.
- Font dropdown remains directly beneath.
- Use the established header/checkbox alignment pattern, including layout rounding where appropriate.
- Preserve existing font options, values, and bindings.

#### Size and Color

Retain the existing Size and Color controls on one horizontal row.

```text
Size:        Color:
[ 32 ]       [■] [#FFF6E9DB]
```

- Labels remain above their respective controls.
- Preserve existing alignment, field widths, values, and behavior.

#### Drop Shadow

Use the compact Drop Shadow arrangement established for Games List and Text Marquee.

Replace the existing **Shadow color:** label with the Drop Shadow checkbox, eliminating its separate row.

```text
[ ] Drop Shadow       Blur:       Depth:
[■] [#99100B17]      [ 4 ]       [ 2 ]
```

- Checkbox sits directly above the shadow color swatch and hex field.
- Blur and Depth labels align with the checkbox row.
- All three property controls align beneath their labels.
- Preserve the existing shadow enable/disable behavior, alpha support, values, and bindings.
- Do not introduce a divider.

#### Overall Target Layout

```text
GAME NAME HEADER

Font:  [✓] Bold
[ Segoe UI                  ▼ ]

Size:        Color:
[ 32 ]       [■] [#FFF6E9DB]

[ ] Drop Shadow       Blur:       Depth:
[■] [#99100B17]      [ 4 ]       [ 2 ]
```

Values shown are illustrative. Preserve actual configured values.

#### Verification

- [ ] TITLE tab retains its existing name.
- [ ] Game Name Header remains the section heading.
- [ ] Bold checkbox appears beside Font.
- [ ] Size and Color remain aligned on one row.
- [ ] Drop Shadow checkbox replaces the Shadow Color label.
- [ ] Shadow Color, Blur, and Depth controls remain properly aligned.
- [ ] Shadow enable/disable behavior and all existing values, bindings, and persistence are preserved.
- [ ] No unnecessary dividers, clipping, or unintended layout changes occur.

---

### PUBLISHER TAB

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Apply the established font, alignment, and compact Drop Shadow layout conventions to the Publisher, Year, Rating properties. Preserve all existing functionality.

#### Font, Bold, and Alignment

Place Font and Alignment combo boxes side by side on one horizontal row.

Move the Bold checkbox beside the Font header.

```text
Font:  [ ] Bold             Alignment:
[ Segoe UI          ▼ ]     [ Right       ▼ ]
```

Requirements:

- Font and Alignment labels appear above their respective combo boxes.
- Bold checkbox sits immediately beside the Font header.
- Both dropdowns share the same horizontal row.
- Reuse the established header/checkbox alignment pattern, including layout rounding where appropriate.
- Preserve existing font options, alignment options, values, and bindings.

#### Size and Color

Retain Size and Color together on their existing horizontal row.

```text
Size:        Color:
[ 13 ]       [■] [#FFCEB8CB]
```

Requirements:

- Labels remain above their corresponding controls.
- Preserve existing field widths, vertical alignment, values, and behavior.
- Follow established Theme Builder spacing conventions.

#### Drop Shadow

Use the same compact Drop Shadow arrangement established for the Title tab.

Replace the existing **Shadow color:** label with the Drop Shadow checkbox, eliminating its separate row.

```text
[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]
```

Requirements:

- Checkbox sits directly above the shadow color swatch and hex field.
- Blur and Depth labels align with the checkbox row.
- All three property controls align on the row below.
- Preserve the existing shadow enable/disable behavior, color alpha support, values, and bindings.
- Do not introduce a divider.

#### Overall Target Layout

```text
PUBLISHER, YEAR, RATING

Font:  [ ] Bold             Alignment:
[ Segoe UI          ▼ ]     [ Right       ▼ ]

Size:        Color:
[ 13 ]       [■] [#FFCEB8CB]

[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]
```

Values shown are illustrative. Preserve actual configured values and behavior.

#### Verification

- [ ] PUBLISHER tab retains its existing name and Publisher, Year, Rating section heading.
- [ ] Font and Alignment combo boxes share one row.
- [ ] Bold checkbox appears beside the Font header.
- [ ] Size and Color remain aligned on one row.
- [ ] Drop Shadow checkbox replaces the Shadow Color label.
- [ ] Shadow Color, Blur, and Depth controls align correctly beneath their labels.
- [ ] Shadow enable/disable behavior remains unchanged.
- [ ] All existing values, bindings, validation, and persistence are preserved.
- [ ] No clipping, overlap, or unintended layout changes occur.

---

### GAME INFO TAB

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Reorganize the Game Info tab into three clearly separated styling sections, using the established compact Drop Shadow layout and consistent font-property arrangements.

#### Navigation Arrows and Dots

Rename the existing **Page Navigation** section heading to **Navigation Arrows and Dots**.

Keep Size and Color on their existing shared row.

Replace the separate Drop Shadow checkbox row and Shadow Color label with the compact checkbox/header arrangement.

```text
Navigation Arrows and Dots

Size:       Color:
[ 12 ]      [■] [#FFCEB8CB]

[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]
```

Requirements:

- Preserve Size and Color controls, their labels, and alignment.
- Place the Drop Shadow checkbox directly above the shadow color swatch and hex field.
- Align Blur and Depth labels with the checkbox row.
- Align all three shadow property controls on the row beneath.
- Preserve the existing shadow enable/disable behavior and property bindings.

#### Divider — Navigation / Headers

Insert a subtle horizontal divider below the Navigation Arrows and Dots shadow controls.

Use comfortable but consistent spacing around the divider.

#### Headers

Keep the existing **Headers** section heading.

Move Bold beside the Font header, with the Font dropdown beneath it.

Keep Size and Color on one row.

Use the same compact Drop Shadow arrangement as Navigation Arrows and Dots.

```text
Headers

Font:  [✓] Bold
[ Segoe UI                  ▼ ]

Size:       Color:
[ 20 ]      [■] [#FFF1BB85]

[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]
```

Requirements:

- Use the established Font/Bold header alignment pattern.
- Preserve existing font options and Bold behavior.
- Retain Size and Color alignment.
- Replace the separate Drop Shadow row and Shadow Color label.
- Preserve all existing shadow behavior and settings.

#### Divider — Headers / Body Text

Insert a second subtle horizontal divider beneath the Headers shadow controls, separating Headers from Body Text.

Match the first divider's styling and spacing.

#### Body Text

Keep the existing **Body Text** section heading.

Use the same layout conventions established for Headers:

```text
Body Text

Font:  [ ] Bold
[ Georgia                   ▼ ]

Size:       Color:
[ 16 ]      [■] [#FFF6E9DB]

[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]
```

Requirements:

- Bold checkbox moves beside Font.
- Font dropdown remains beneath the header.
- Size and Color remain on one row.
- Drop Shadow checkbox replaces the Shadow Color label.
- Preserve the independent Body Text styling and shadow settings.

#### Gap to Scrollbar

Keep **Gap to scrollbar (px)** within the Body Text section, beneath its shadow controls.

Move the explanatory label to the right of the numeric field.

Use a modest additional vertical gap above this property.

**Do not insert a divider above Gap to Scrollbar.**

```text
[ 20 ] Gap to scrollbar (px)
```

Preserve existing numeric limits, values, validation, and scrollbar-spacing behavior.

#### Overall Target Layout

```text
Navigation Arrows and Dots

Size:       Color:
[ 12 ]      [■] [#FFCEB8CB]

[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]

────────────────────────────────────────

Headers

Font:  [✓] Bold
[ Segoe UI                  ▼ ]

Size:       Color:
[ 20 ]      [■] [#FFF1BB85]

[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]

────────────────────────────────────────

Body Text

Font:  [ ] Bold
[ Georgia                   ▼ ]

Size:       Color:
[ 16 ]      [■] [#FFF6E9DB]

[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]


[ 20 ] Gap to scrollbar (px)
```

Values shown are illustrative. Preserve all existing configured values.

#### Functional Boundaries

- Layout and heading changes only.
- Preserve independent navigation, header, and body text properties.
- Preserve all three independent Drop Shadow toggles and their dependent controls.
- Do not change any font rendering, text formatting, navigation behavior, or scrollbar calculations.
- Retain the tab's existing scrolling capability.
- Reuse existing shared layout conventions and checkbox alignment patterns.

#### Verification

- [ ] Page Navigation is renamed Navigation Arrows and Dots.
- [ ] Navigation Size and Color remain aligned.
- [ ] All three Drop Shadow groups use the compact checkbox/header arrangement.
- [ ] Two dividers separate Navigation, Headers, and Body Text.
- [ ] Bold sits beside Font in both Headers and Body Text.
- [ ] Size and Color share a row in all three sections.
- [ ] Gap to Scrollbar has its label to the right, with extra spacing above and no divider.
- [ ] All independent property values, bindings, and enable/disable behavior are preserved.
- [ ] Scrolling works correctly without clipping or overlapping controls.

---

### ARTWORK PREVIEWS TAB (formerly PREVIEWS)

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Rename the Previews tab to better identify the game artwork being displayed and simplify its optional Drop Shadow controls.

#### Tab Name

Rename **PREVIEWS** to **ARTWORK PREVIEWS**.

The artwork preview primarily displays game flyers, with title screens and screencaps used as fallbacks.

- Change the tab's display label only.
- Preserve all existing artwork selection, fallback behavior, positioning, sizing, bindings, and persistence.
- Do not introduce additional artwork properties.

#### Remove Redundant Text

Remove the existing heading:

**"Image previews shadow"**

Remove the Glow explanation:

**"Glow: set Depth to 0 and pick a bright color."**

Neither requires replacement.

#### Drop Shadow Layout

Replace the separate Drop Shadow checkbox row and Shadow Color label with the established compact arrangement.

```text
[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]
```

Requirements:

- Position the existing Drop Shadow checkbox directly above the shadow color swatch and hex field.
- Remove the redundant Shadow Color label.
- Align Blur and Depth labels with the checkbox row.
- Align all three property controls horizontally beneath their respective labels.
- Use the established checkbox alignment and field sizing conventions.
- Preserve the existing shadow toggle, dependent-control enable/disable behavior, values, and bindings.
- Do not add dividers or additional spacing groups.

#### Functional Boundaries

This is strictly a tab-label and layout change.

- Preserve existing artwork preview behavior.
- Preserve the independent Drop Shadow settings.
- Do not modify the shared Game Info Position & Size controls.
- Do not introduce Border properties; those may be considered in a future feature update.

#### Verification

- [ ] PREVIEWS tab is renamed ARTWORK PREVIEWS.
- [ ] Redundant heading and Glow tip are removed.
- [ ] Drop Shadow checkbox replaces the Shadow Color label.
- [ ] Shadow color, Blur, and Depth controls align correctly.
- [ ] Shadow enable/disable behavior remains unchanged.
- [ ] Artwork selection and fallback behavior remain unchanged.
- [ ] Existing bindings, values, persistence, and functionality are preserved.
- [ ] No clipping, overlap, or unintended tab wrapping occurs.

---

### PLAY BUTTON TAB

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Reorganize the Play Button properties into four logical sections—Text, Button, Border, and Hover States—using established Theme Builder layout conventions. Preserve all existing functionality.

#### Text — Font and Bold

Keep the existing **Text** section heading.

Place the Bold checkbox beside the Font header, with the Font dropdown directly beneath it.

```text
Font:  [✓] Bold
[ Segoe UI                       ▼ ]
```

- Use the established header/checkbox alignment pattern.
- Preserve existing font options, values, and Bold behavior.

#### Text — Size and Color

Keep Size and Color together on one horizontal row.

```text
Size:       Color:
[ 16 ]      [■] [#FFF6E9DB]
```

- Labels remain above their respective controls.
- Preserve established field widths and vertical alignment.

#### Text — Button Text and Vertical Nudge

Move Button Text beneath the Size and Color row.

Place Button Text and Vertical Nudge together on one horizontal row.

```text
Button text:
[ Launch Game        ]  [ -1 ] Vertical nudge (px)
```

Requirements:

- Button Text retains its label above the textbox.
- Vertical Nudge uses a compact numeric field with its explanatory label immediately to the right.
- Size the Button Text field to comfortably display **Launch Game**.
- Longer custom text does not need to be fully visible without horizontal scrolling.
- Preserve existing text editing, values, validation, and nudge behavior.

#### Text — Drop Shadow

Use the compact Drop Shadow arrangement established in the other tabs.

Replace the existing Shadow Color label with the text Drop Shadow checkbox, eliminating its separate row.

```text
[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]
```

- Position the checkbox directly above the shadow color swatch and hex field.
- Align Blur and Depth labels with the checkbox row.
- Preserve the text shadow's independent settings and enable/disable behavior.
- Do not combine this shadow with the separate Button shadow.

#### Divider — Text / Button

Insert one subtle horizontal divider below the Text Drop Shadow controls.

Use consistent spacing around the divider.

#### Button — Background

Keep the existing **Button** section heading.

Place Background Color on its own row.

```text
Button

Background:
[■] [#FF493251]
```

Preserve the existing background color control, values, and behavior.

#### Button — Drop Shadow

Use the same compact Drop Shadow layout as the Text section.

```text
[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]
```

Requirements:

- Replace the separate checkbox row and Shadow Color label.
- Keep this shadow independent from the Text shadow.
- Preserve existing toggle behavior, dependent controls, values, and bindings.

#### Divider — Button / Border

Insert a second subtle divider beneath the Button Drop Shadow controls.

#### Border

Keep the existing **Border** section heading.

Place Border Color, Size, and Radius together on one horizontal row.

```text
Border

Color:                  Size:       Radius:
[■] [#FF79566E]         [ 1 ]      [ 0 ]
```

Requirements:

- Keep labels above their respective controls.
- Maintain consistent vertical alignment and horizontal spacing.
- Preserve existing border values, limits, and behavior.
- Follow established sizing conventions.
- Do not alter the shared Border tab controls as part of this layout change.

#### Divider — Border / Hover States

Insert a third subtle divider beneath the Border controls.

#### Hover States

Change the **Hover** section heading to **Hover States**.

Arrange Hover Text Color, Hover Background, and Hover Border Color together on a single horizontal row.

```text
Hover States

Text color:          Background:          Border color:
[■] [#FFF6E9DB]      [■] [#FF66435D]      [■] [#FFFFD4A8]
```

Requirements:

- All three color groups share one horizontal row.
- Labels appear above their corresponding swatch and hex controls.
- Use consistent vertical alignment and horizontal gaps.
- Preserve the established color control widths where practical.
- Do not reduce fields to unusable widths merely to force the arrangement.
- Preserve all existing hover colors, alpha support, bindings, and behavior.

**Visual review required:** The three-color Hover States row must be inspected in the running Theme Builder. Adjust local spacing or widths if necessary to achieve a balanced layout without clipping.

#### Overall Target Layout

```text
TEXT

Font:  [✓] Bold
[ Segoe UI                       ▼ ]

Size:       Color:
[ 16 ]      [■] [#FFF6E9DB]

Button text:
[ Launch Game        ]  [ -1 ] Vertical nudge (px)

[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]

────────────────────────────────────────

BUTTON

Background:
[■] [#FF493251]

[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]

────────────────────────────────────────

BORDER

Color:                  Size:       Radius:
[■] [#FF79566E]         [ 1 ]      [ 0 ]

────────────────────────────────────────

HOVER STATES

Text color:          Background:          Border color:
[■] [#FFF6E9DB]      [■] [#FF66435D]      [■] [#FFFFD4A8]
```

Values shown are illustrative. Preserve the actual configured values.

The intended progression is:

**Text Styling → Text Positioning → Text Shadow → Button Background and Shadow → Border → Hover Appearance**

#### Functional Boundaries

- Presentation changes only; preserve all existing button behavior.
- Keep Text and Button Drop Shadows independent.
- Preserve text formatting, background, border, hover, and click functionality.
- Preserve all bindings, numeric limits, validation, and persistence.
- Retain existing scrolling as necessary.
- Do not introduce new properties or change button rendering logic.

#### Verification

- [ ] Font and Bold share a header row.
- [ ] Size and Color remain aligned.
- [ ] Button Text and Vertical Nudge share one row.
- [ ] Button Text comfortably displays "Launch Game".
- [ ] Both Drop Shadow groups use the compact layout and remain independent.
- [ ] Three dividers separate Text, Button, Border, and Hover States.
- [ ] Border Color, Size, and Radius remain aligned on one row.
- [ ] Three Hover color controls share one row without clipping.
- [ ] Existing control values, bindings, toggles, and behavior remain unchanged.
- [ ] Scrolling works correctly.
- [ ] Final layout is visually reviewed at the standard display size.

---

### FAVORITES STAR TAB

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Reorganize the Favorites Star properties into one shared sizing control and two independently styled star states. Apply the established compact Drop Shadow layout and improve visual grouping without changing functionality.

#### Star Size — Shared Property

Keep the existing Star Size property at the top of the tab.

Move its label to the left of the numeric field, placing both on one horizontal row.

```text
Star size: [  ]
```

Requirements:

- Rename the displayed label from **Size:** to **Star size:**.
- Place the label immediately before the numeric input.
- Retain the existing numeric field width and styling.
- Preserve the current value, limits, validation, and behavior.
- This size setting continues to apply to both star appearances.

#### Remove Glow Explanation

Remove the existing explanatory text:

**"A glow is a shadow with Depth 0 and a bright color."**

Do not replace it with a tooltip or additional explanation.

#### Divider — Shared Size / Solid Star

Insert a subtle horizontal divider below Star Size and above the Solid Star section.

Use modest, consistent vertical spacing around the divider.

#### Solid Star — Favorited State

Rename the section heading:

**"Solid star (game is in Favorites)"**

To:

**"Solid Star (Favorited)"**

Keep the existing Color property beneath the heading.

Use the compact Drop Shadow arrangement established in the other tabs.

```text
Solid Star (Favorited)

Color:
[■] [#FFF6E9DB]

[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]
```

Requirements:

- Keep the Color label above its existing swatch and hex input.
- Replace the separate Drop Shadow checkbox row and Shadow Color label with the compact arrangement.
- Position the Drop Shadow checkbox directly above the shadow color control.
- Align Blur and Depth labels with the checkbox row.
- Align the shadow color, Blur, and Depth controls on the row beneath.
- Preserve all existing color values, alpha support, shadow settings, and toggle behavior.
- Do not change when the solid star is displayed.

#### Divider — Solid Star / Outline Star

Insert a second subtle horizontal divider below the Solid Star shadow controls.

Match the first divider's styling and spacing.

#### Outline Star — Not Favorited State

Rename the section heading:

**"Outline star (game is not in Favorites)"**

To:

**"Outline Star (Not Favorited)"**

Apply the same compact Color and Drop Shadow layout used for Solid Star.

```text
Outline Star (Not Favorited)

Color:
[■] [#FFCEB8CB]

[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]
```

Requirements:

- Preserve the existing Color swatch and hex input.
- Replace the separate Drop Shadow checkbox row and Shadow Color label.
- Match the Solid Star section's field widths, spacing, and alignment.
- Preserve the Outline Star's independent color and shadow values.
- Preserve existing enable/disable behavior.
- Do not change when the outline star is displayed.

#### Overall Target Layout

```text
Star size: [  ]

────────────────────────────────────────

Solid Star (Favorited)

Color:
[■] [#FFF6E9DB]

[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]

────────────────────────────────────────

Outline Star (Not Favorited)

Color:
[■] [#FFCEB8CB]

[ ] Drop Shadow       Blur:       Depth:
[■] [#FF100B17]      [ 3 ]       [ 1 ]
```

Color values are illustrative. Preserve all actual configured values.

The intended organization is:

**Shared Star Size → Solid Star Appearance → Outline Star Appearance**

#### Functional Boundaries

- This is a UI layout and terminology update only.
- Preserve the existing Favorites toggle behavior.
- Preserve the shared star size setting.
- Keep Solid Star and Outline Star styling independent.
- Preserve both independent Drop Shadow toggles and their dependent controls.
- Do not modify star glyphs, rendering, or positioning.
- Do not introduce additional star properties.
- Preserve all existing bindings, validation, values, and persistence.

#### Verification

- [ ] Star Size label and numeric field share one row.
- [ ] Label reads "Star size:".
- [ ] Glow explanation is removed.
- [ ] Two dividers separate the shared size and individual star appearances.
- [ ] Solid Star heading reads "Solid Star (Favorited)".
- [ ] Outline Star heading reads "Outline Star (Not Favorited)".
- [ ] Both Drop Shadow groups use the established compact layout.
- [ ] Solid and Outline colors and shadows remain independent.
- [ ] Existing Favorites toggle and star rendering behavior are unchanged.
- [ ] All existing values, bindings, validation, and persistence are preserved.
- [ ] No clipping, overlap, or unintended scrolling changes occur.

---

## SCROLLBARS — GAMES LIST & GAME INFO

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Tighten the horizontal spacing between labels and controls in both Scrollbars tabs, and simplify their explanatory text. Both tabs should use the same layout adjustments.

### Horizontal Spacing

Reduce the width of the left label column so the controls sit closer to their labels.

Requirements:

- Apply the same changes to both **GAMES LIST** and **GAME INFO** tabs.
- Keep Width, Resting, Hover, and Dragging labels vertically aligned.
- Maintain one consistent horizontal gap between the label column and the first control column.
- Keep the Width numeric field and Thumb color controls aligned at the same horizontal starting position.
- Preserve the existing spacing between Thumb and Track columns.
- Preserve all existing numeric, swatch, and hex field widths. Do not stretch controls to fill the available panel width.
- Maintain the current disabled-state appearance and behavior when Use global scrollbar is checked.

### Explanatory Text

Replace the existing explanation beneath the controls in each tab with the following.

**GAMES LIST:**

> These settings control the Games List scrollbar. When Use global scrollbar is checked, these settings follow the Global tab and cannot be edited here.

**GAME INFO:**

> These settings control the Game Info, Trivia, Tips & Tricks, and Specs & Credits scrollbars. When Use global scrollbar is checked, these settings follow the Global tab and cannot be edited here.

Keep the explanation beneath the existing controls, using its current styling and text wrapping.

### Functional Boundaries

- Do not change scrollbar width, colors, rendering, or behavior.
- Preserve the independent Games List and Game Info scrollbar settings.
- Preserve the Use global scrollbar checkbox behavior and inheritance from Global settings.
- Do not change any property bindings, stored values, or persistence.
- Do not introduce new controls or additional dividers.

### Verification

- [ ] Both tabs use the same reduced label-to-control gap.
- [ ] Label and control columns remain consistently aligned.
- [ ] Numeric, swatch, and hex field widths remain unchanged.
- [ ] Thumb and Track column spacing remains unchanged.
- [ ] Both explanations display the approved wording.
- [ ] Use global scrollbar continues to enable/disable local controls correctly.
- [ ] Existing scrollbar settings, bindings, and behavior are preserved.
- [ ] No clipping, overlapping controls, or unintended layout changes occur.

---

## MAIN WINDOW BACKGROUND

### IMAGE OR VIDEO TAB (formerly IMAGE)

**Status:** Agreed design specification; pending Claude Chat planning and Claude Code audit/approval.

**Purpose:** Reorganize the Main Window Background controls to match the established media-selection layout conventions, improve terminology, and eliminate redundant explanatory text.

#### Tab Name

Rename the existing **IMAGE** tab to **IMAGE OR VIDEO**.

This is a display-label change only. Preserve all existing functionality, bindings, and stored values.

#### Image / Video File

Move the existing image/video file selector to the top of the tab.

Replace the current heading:

**"Background image or video (png, jpg, bmp, mp4):"**

With:

**Image / video file:** followed immediately by the established **?** help glyph.

Requirements:

- Reuse the media-selection header and tooltip styling already established for the other background layers.
- Tooltip lists the supported formats: PNG, JPG, BMP, MP4, matching the current Main Window Background file selector.
- Keep the file path, Browse, and Clear controls together on one horizontal row.
- Preserve existing file filtering, selection, clearing behavior, and persistence.
- Do not introduce new media formats or change playback behavior.

#### Disable Background Image

Move the existing checkbox directly beneath the file-selection controls.

Preserve its current wording:

**Disable background image (use the color only)**

Requirements:

- Retain the existing checkbox styling and behavior.
- Preserve the ability to disable the background image/video while retaining the configured file path.
- Keep the checkbox logically associated with the media selector.
- Do not change how the background color is displayed when media is disabled.

#### Remove Redundant Explanation

Remove the existing text:

**"Leave empty to use the background color only."**

The checkbox and Background Color control already communicate this behavior.

Preserve the existing empty-path behavior.

#### Background Color

Move Background Color beneath the Disable Background Image checkbox.

```text
Background color:
[■] [#FF241A32]
```

Requirements:

- Keep the label above the swatch and hex field.
- Preserve existing field widths, alpha support, color picker behavior, bindings, and values.
- Do not add an opacity control or any other new properties.

#### Overall Target Layout

```text
IMAGE OR VIDEO TAB

Image / video file: (?)
[ File path................ ] [ Browse ] [ Clear ]

[✓] Disable background image (use the color only)

Background color:
[■] [#FF241A32]
```

Values shown are illustrative. Preserve actual configured settings.

The intended order is:

**Media Selection → Media Visibility Toggle → Background Color**

Follow established Theme Builder spacing and alignment conventions. No dividers are required.

#### Functional Boundaries

- UI layout and terminology changes only.
- Preserve existing image and video loading behavior.
- Preserve media visibility and empty-path behavior.
- Preserve all existing property bindings, values, validation, and persistence.
- Do not modify other background layers or their controls.
- Do not introduce new functionality.

#### Verification

- [ ] IMAGE tab is renamed IMAGE OR VIDEO.
- [ ] Media selector appears at the top with the standard ? tooltip.
- [ ] Tooltip accurately lists supported formats.
- [ ] Path, Browse, and Clear remain aligned on one row.
- [ ] Disable Background Image checkbox appears beneath the file selector.
- [ ] Redundant empty-path explanation is removed.
- [ ] Background Color appears beneath the checkbox.
- [ ] Existing media, visibility, and color behavior remains unchanged.
- [ ] No clipping, overlap, or unintended layout changes occur.

---

## SHARED BORDER TABS — NUMERIC FIELD WIDTHS

**Status:** Agreed shared layout adjustment; Claude Chat should assign it to a suitable pass after checking shared-control dependencies.

**Purpose:** Prevent the Size and Radius labels from visibly overhanging their numeric input fields on the established shared Border layout.

- Increase the shared Border **Size** numeric textbox width from **36px to 50px**.
- Increase the shared Border **Radius** numeric textbox width from **36px to 60px**.
- Preserve Border Color swatch/hex widths, the existing **22px group gaps**, label-above-field placement, and all behavior/values/validation.
- Apply this to every consumer of that **same shared Border layout**. Audit the affected layers first and report them. Do not assume that a lookalike local Border section (such as Text Marquee or Play Button) is implemented by this shared control; leave unrelated local sections unchanged unless the audit confirms they share it.
- Do not alter the separate shared Position & Size numeric widths, shadow fields, or other controls.

**Verification:** Check Size/Radius label fit, 22px group spacing, color-field alignment, and no clipping or wrapping regression in each affected Border tab.

---

## CLAUDE CHAT WORKFLOW — REVIEW, PLAN, AUTHOR HANDOFFS

### Phase A — Review the design (Claude Chat)

- Treat all preceding sections as the agreed **design specification**. Review the entire document, including dependencies and exception cases, before proposing a work breakdown.
- Confirm the scope of the remaining tabs and shared Border widths without redoing the already completed Position & Size and grouped-control passes.
- Identify possible conflicts, ambiguous requirements, code-dependent assumptions, tight layouts, and regression risks. Raise questions only where resolving them materially affects implementation; distinguish questions from optional suggestions.
- Preserve the approved terminology, text, tooltip behavior, tab order, specific alignments, and independent settings. Do not silently simplify or substitute a new design.

### Phase B — Select implementation passes (Claude Chat)

- Recommend **one or more passes**, ordered and grouped according to shared implementation paths, testing practicality, and risk. There is **no mandatory number of passes**, and this file does **not** require one giant coordinated code edit.
- Identify what is included and excluded in every proposed pass, plus any prerequisites and likely shared-control impacts.
- Prefer cohesive, independently verifiable passes over arbitrary equal-sized slices. Minimize repeatedly editing the same shared controls where practical.
- Keep the user involved in reviewing the plan and any proposed changes before implementation is authorized.

### Phase C — Create Claude Code handoff `.md` files (Claude Chat)

For **each** implementation pass, produce a concrete, self-contained Markdown handoff that includes:

1. **Goal and scope:** exact layers/tabs and edits included, relevant desired layout and wording, and explicit exclusions.
2. **Audit-first gate — mandatory:** Claude Code must inspect affected XAML/code-behind, shared layout components, tab construction/order, binding and persistence paths, media file filters/tooltips, and interactions **without editing any source**. It must report actual code locations, differences from the specification, constraints, and proposed edit strategy, then **STOP for the user's explicit GO**. No speculative edits during the audit.
3. **Approved implementation instructions:** detail the corresponding requirements from this document, including ASCII layouts where useful, without replacing independent settings or altering behavior. Implementation happens **only after GO** for that pass.
4. **Verification:** clean build; relevant behavior and visual checks (including disabled controls, shadow groups, file selection, state-specific values, scrolling and tab wrapping where applicable); report what was and was not verified.
5. **Review and follow-through:** user visually tests the live WPF result; any corrections are scoped before proceeding to later passes. Update project documentation after acceptance. **No automatic commit/push**.

Preserve each pass's relevant specifications in the handoff rather than referring vaguely to a giant document. If an implementation question cannot be resolved from the design alone, identify it for code audit or user decision instead of guessing.

### Phase D — Supervise the audit and execution (Claude Chat + Claude Code + user)

1. Claude Chat provides the selected handoff for the next pass.
2. Claude Code conducts its **read-only code audit**, reports findings and proposed edits, and waits.
3. The user reviews the findings, potentially with Claude Chat, and issues an explicit **GO** or requests revisions.
4. Claude Code implements the **approved** pass, rebuilds, and reports verification results. No undisclosed design substitutions or unrelated feature changes.
5. The user visually tests the WPF interface; Claude Chat can help refine targeted tweaks and prepare the next pass.
6. After acceptance, update relevant documentation and let the user manage Git commits/pushes.

**Decision priority when code and design differ:** preserve functionality and stored values first; preserve agreed layout and wording second; reuse established spacing and shared-component patterns third. Report meaningful conflicts rather than quietly guessing.

**End of Claude Chat Planning Specification.**
