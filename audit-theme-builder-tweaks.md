# Handoff 1 of 2: READ-ONLY AUDIT, Theme Builder tweaks

## Goal and why
Project: 4RCADE 5TICK, a C#/WPF MAME frontend. I'm about to make four small tweaks to the **Theme Builder**. Before any edits, I need a precise audit of where each one lives and how it currently works, so the follow-up handoff is exact and doesn't break anything.

**THIS IS AN AUDIT ONLY. Do NOT edit, create, rename, or delete any file. Do not run git commits or stash anything.** Building is fine if you want to confirm the project compiles, but don't change anything to make it so.

## The four tweaks (for context, do not implement)

### 1. Layer list pipes up by 1 pixel
In the Theme Builder's layer list, each row has a colored vertical "pipe" (the `|` marker). It needs to move up exactly 1 px relative to its current position.

### 2. New magenta pipe color for custom layers
- Custom layers (user-added with the + button) get a new **magenta** pipe.
- Default layers AND shape layers stay **blue**.
- I'll pick the magenta value myself in the next handoff. Just report how the current blue is defined (resource, brush, hardcoded hex, etc.).

### 3. New text for the layer-list "?" glyph tooltip
Replace the existing tooltip on the layer list's "?" glyph (this is the help tooltip, NOT a tool for the new-layer feature). Target content, with formatting:

- Section headers: **bold, ALL CAPS**.
- Sections separated by a blank line.
- Keep the line breaks exactly as shown below.
- One line is *italic*: the "Search & Buttons, Game Info, ..." sentence (both of its lines).
- In the last section, the `|` after "Blue" and "Magenta" is rendered in the **actual layer-list pipe colors** (blue / magenta) in a **heavy/Black font weight** so it stands out. They must use the same colors as the pipes in the layer list.

```
LAYER ORDER & CUSTOM LAYERS
Use the arrows beside the layer list to move layers forward or backward in the Z-order.
Use the + button to add a custom layer.

MOVE MULTIPLE LAYERS AT ONCE
Ctrl-click or Shift-click to select multiple layers,
then move them together using the Move X / Move Y boxes to the right.

COPY LAYER PROPERTIES TO OTHER LAYERS
Right-click a layer to copy its position, size, or border properties.
Right-click another layer, or multiple selected layers, to paste them.
[italic] Search & Buttons, Game Info, Footer, Main Window Background,
[italic] and Scrollbars do not support Copy & Paste.

DEFAULT & CUSTOM LAYER HELPERS
Blue [blue | heavy] — Default layer; can be renamed.
Magenta [magenta | heavy] — Custom layer; can be renamed or deleted.
```

### 4. TEMPLATES header in the "new theme" context menu
In the context menu used to create a new theme, add a header reading **TEMPLATES** above the template items.
- It is **not clickable**: it only tells the user that the items below are templates, not themes. It should not highlight on hover, take focus, or fire any handler.
- It should not look "disabled/greyed out" in the default way. Use the **same color used for the random game layer in the layer list's right-click context menus** (find that color and report exactly how it's defined).

## What to audit and report back

For each item, give **file path + line numbers** and paste the **verbatim current code** for the relevant section (XAML and code-behind/ViewModel).

**Item 1 (pipes position):**
- How the pipe is drawn (Rectangle, Border, TextBlock glyph, template part, etc.) and where (DataTemplate, ItemContainerStyle, code-behind).
- Current Margin / Padding / alignment / RenderTransform affecting its vertical position.
- The cleanest way to shift it up exactly 1 px, and any risk (clipping, differing row heights, selected/hover states, the Z-order arrows or other elements aligned to it).

**Item 2 (magenta pipe):**
- How the pipe color is chosen today (hardcoded, resource, trigger, converter, code-behind).
- How the code tells the layer kinds apart: **default**, **shape**, and **custom** (user-added via +). Is there an existing property/flag/type for custom layers? Where is it set (including when themes are loaded from disk)?
- Whether shape layers and default layers currently share one pipe color path, and what the smallest change would be to make only custom layers magenta.
- Any other place that draws or references the pipe color (so the tooltip pipes and list pipes can share one source of truth).

**Item 3 (? glyph tooltip):**
- Where the "?" glyph is defined, and the current tooltip (plain string, ToolTip with content, code-behind).
- Current ToolTip settings: MaxWidth, font, foreground/background, ShowDuration, InitialShowDelay, placement, any custom ToolTip style/template.
- Whether the current structure can support rich text (bold, italic, colored heavy-weight pipes) and the recommended way to do it here (e.g. ToolTip containing a TextBlock with Runs/LineBreaks).
- Where the blue pipe color is defined, so the tooltip can reuse it.

**Item 4 (TEMPLATES header):**
- Where the "new theme" context menu is built (XAML or code-behind) and how its items are generated (static vs. dynamically from a templates folder).
- Existing menu item styles/templates in play (hover, highlight, separators).
- The color used for the random game layer in the layer list's right-click context menus: file, line, and how it's defined (hex, resource key, brush).
- The cleanest way to add a non-clickable, non-focusable, non-highlighting header in this menu that matches the existing styling, and any pitfalls (e.g. menu navigation by keyboard skipping it, handlers iterating `Items` that would choke on a non-template entry).

**General:**
- List every file you expect the real changes to touch.
- Flag anything surprising, fragile, or duplicated that could trip up these edits.
- Note whether the project builds cleanly right now (if you checked).

## Constraints / do NOT touch
- No edits of any kind. No new files. No formatting, refactoring, or "while I'm here" fixes.
- Don't change any behavior, settings, or theme data.

## How to verify
This is complete when you've returned a single written report covering Items 1 to 4 and General, with file paths, line numbers, and verbatim current code for each, and `git status` shows no changes. End your report by confirming that no files were modified.
