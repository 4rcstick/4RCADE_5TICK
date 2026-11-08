# HANDOFF PLANNING SPEC — Theme Builder Final Spacing Tweaks

**Recipient:** Claude Chat for review and preparation of one concise Claude Code implementation handoff.

**Timing:** This is a **separate, post-commit change**. Do not include it in the current tab-polish commit. The user will finish reviewing and commit the existing tab-polish work first.

## Goal

Make three small, localized visual adjustments. Keep the existing behavior, bindings, labels, control names, values, and previously accepted layouts intact. No general redesign.

## 1. Games List → Folders: Nudge width

- Change the **Nudge (px)** numeric input width to **52px**, matching the existing Blur and Depth numeric fields.
- Keep its current left position, label, value, handlers, validation, and behavior.
- Do not touch Game Titles or other Nudge fields.

## 2. Marquee → Text Marquee: Font dropdown width

- Extend the **Font** combo box just enough that its **right edge aligns with the right edge of Max size** below it.
- The visual estimate was about **10px wider**, but derive the precise width from the current XAML rather than assuming an absolute measurement.
- Keep the Font field's left edge, font/header/checkbox placement, and all behavior unchanged.
- Do not apply this width change to Marquee → Shadow's **Apply shadow to** combo: its present compact width is approved.

## 3. Shared BORDER tab: standardize hex widths and numeric alignment

**Confirmed design decision:** Reduce the shared BORDER tab's color hex input from **104px to 80px**, matching the established hex width in the other styling tabs. This replaces the earlier instruction to preserve 104px. Keep the color swatch at its current size. Include the shared Border tab's **Hover border color** hex input if it also uses the older 104px width, so the hex boxes within this tab remain consistent; retain its existing position and behavior.

Use the compact **Blur / Depth** columns as the alignment reference for the shared BORDER tab's **Border color / Size / Radius** row:

- **Border Color** group remains left-aligned at x0, with its hex box at **80px**.
- **Size** numeric input begins at **x118** (Blur's left edge), width **50px**.
- **Radius** numeric input begins at **x176** (Depth's left edge), width **60px**.
- Use a **110 / 8 / 50 / 8 / 60** arrangement where appropriate, based on the current XAML: 110px color group, 8px gap, Size 50px, 8px gap, Radius 60px. Match the actual positions after inspecting the code; don't introduce overlap or clipping.
- The Radius input extends 8px beyond Depth's right edge because Radius is 60px versus Depth's 52px. **Left-edge alignment** is the goal, not matching right edges.
- Keep labels aligned over their respective controls, as they are now.

**Scope:** Adjust only the shared Border tab (`TxtShapeBorderSize`, `TxtShapeCornerRadius`, and its relevant color hex controls). Preserve its consumers (shapes, custom layers, Splash Image, Video Preview, Search & Buttons, and Random Game Display) and all underlying properties and behavior.

Do **not** alter the already-approved **local Border rows** in Text Marquee or Play Button, the global color-control style, or other tabs' hex widths. Preserve Border values, color swatch behavior, hover-border controls, names, bindings, persistence, and rendering.

## Workflow

1. Claude Chat reviews this planning spec and produces **one short Claude Code handoff**. Include an initial focused, read-only audit of the three locations and the shared Border geometry, then STOP for user GO.
2. After approval, make only the approved tweaks, then run a clean .NET build and static checks.
3. **Do not use Python, launch the application, do UI automation, or commit/push.** The user performs visual acceptance and Git operations.
4. Do not update `themes.md` or `CLAUDE.md` before visual acceptance. No unrelated changes.

## Visual acceptance checklist

- [ ] Folders Nudge box width matches Blur and Depth, without shifting its left edge.
- [ ] Text Marquee Font right edge equals Max size right edge; Marquee Shadow combo remains unchanged.
- [ ] Shared Border Color hex width is 80px (and Hover Border Color hex, if applicable), without changing swatch size, values, or behavior.
- [ ] Shared Border Size x118 / Radius x176 align with Blur / Depth left edges; widths remain 50/60, with no clipping or overlap.
- [ ] Text Marquee and Play Button local Border rows and other approved layouts are unchanged.
- [ ] All existing functionality, bindings, and settings persist as before; clean build.
