# 4RCADE 5TICK — Theme Browser Design Review

**Status:** Design decisions consolidated; ready for **Claude Chat review**, **not** implementation.
**Project:** 4RCADE 5TICK, C# / WPF (.NET 10), Beta.4 Advanced Theme Builder / Theme Browser.
**Date:** 2026-11-08

> **Instructions to Claude Chat:** Review this as a proposed product/UX/behavior specification. First read the full document and challenge any omissions, inconsistencies, unsafe assumptions, or user-facing edge cases. Ask a focused set of *remaining* questions only where a real decision is needed. Do **not** write implementation code or an implementation handoff yet. Once questions are resolved, help draft a focused `hand-off-audit.md` for Claude Code to inspect the real codebase. We will review the audit findings, revise decisions where needed, and only then prepare one or more implementation handoffs.
>
> **Important:** We have deliberately settled many design questions already. Prefer checking the existing implementation before re-opening settled behaviors. Reuse working systems wherever possible, but do not assume existing handlers can be blindly copied without inspecting their preconditions and persistence behavior.

---

## 1. Purpose and guardrails

The Theme Browser is a visual catalog of installed theme ZIPs. Its users can inspect a small thumbnail, enlarge it, and click a resolution button to load that **theme at that resolution**. It lives as a tab in the same window as the Theme Builder, not as a separate window.

- Browser is for **selection and viewing**, not editing theme metadata or layer properties.
- The **master theme selector in the Theme Builder remains unrestricted**: it shows all themes, even those hidden from the Theme Browser and hamburger menu.
- Honor the existing `Show Theme In` choices: **Both / Theme Browser / Hamburger Menu / Neither**. The Browser shows only eligible themes. The hamburger menu's existing theme list retains its own visibility filtering.
- Theme discovery should read ZIP central-directory metadata and `theme.json` **without extracting the entire theme merely to build the catalog**. Actual loading should reuse existing extraction/loading behavior as needed.
- Preserve portable paths and existing theme ZIP / JSON conventions.
- No new user-editable Theme Builder style properties unless the audit finds a compelling need. Use established Global style properties.
- The existing Global Divider component/style separates entries; do not reinvent a line based only on a color value.
- Main launcher themes have resolution-specific, precise layouts; the **Theme Builder/Browser subwindow follows ordinary Windows DPI scaling**. Do not change the main launcher's sizing approach.

## 2. Window, navigation, and catalog

- The Theme Browser and Theme Builder share **the existing Theme Builder window dimensions** and tabs.
- Add a **Theme Browser** command to the hamburger menu; **no keyboard shortcut** for opening it at this time.
- Hamburger menu **Theme Builder** opens/activates the shared window on its Builder tab; hamburger menu **Theme Browser** opens/activates it on its Browser tab. If already open, reuse/foreground the same window rather than opening a duplicate.
- Switching tabs, opening the Browser, refreshing the catalog, hovering previews, and opening/closing enlarged previews **must not load a theme, save settings, or discard unsaved Builder edits**.
- On switching **to** the Browser tab, refresh/reconcile the theme catalog so newly created, saved under a new name, deleted, or visibility-changed themes appear correctly. No continuously running filesystem watcher or manual Refresh button is required for the initial design.
- Catalog order: **alphabetical by theme name**. No pinning or sorting of the active theme to the top.
- Number of themes: **no artificial cap**; scrollable list. Implementation may use performance-conscious rendering where appropriate, but no new limit should be imposed.
- Invalid/unreadable theme entries do **not** appear in the Browser. Follow established handling/logging rather than interrupting the user with errors for every invalid ZIP.
- Each theme has **one thumbnail shared by all its resolutions**; no resolution-dependent thumbnail selection.
- The Browser's visual styling is driven by the **currently active theme**. Loading a new theme updates the Browser style live along with all the other themed UI.
- Keep Browser scroll position when loading themes and applying live styling, and when reasonable when refreshing the catalog. Do not reset scroll merely because Global styling changed.
- Include the existing **Close** button at the bottom of the shared window. Loading a theme **does not close the Browser/window**.

## 3. Introductory instruction

Place this line above the scrolling catalog; it remains stationary while entries scroll:

> **Select a resolution to load a theme, and click a thumbnail to enlarge the preview.**

Use the existing Theme Builder instructional-text styling. No new font/color property.

## 4. Repeating two-column theme entry

**Reference:** The user's Photoshop layout mockups take precedence over any AI-generated illustration. No need to reproduce AI-generated mockups or their inconsistent colors/layout.

- **Left column:** 160 × 90 thumbnail, vertically centered within the full theme entry; its own bordered image, no metadata in this column.
- **Right column:** All elements left-aligned to the same metadata column edge, in this order:
  1. **THEME NAME** in all caps; active-theme checkmark immediately after the title.
  2. `Resolutions:` followed by existing-resolution buttons.
  3. `Author:` followed by value.
  4. `Description:` followed **on the same line** by body text; wrap remaining text naturally under the text flow, not with a mandatory new line after the label.
- The theme title is **not centered**. It is left-aligned with metadata. Place the checkmark **after** the theme name so the name's left edge is never shifted.
- Theme entry height can grow with metadata. Thumbnail remains vertically centered regardless of entry height.
- Existing **Global Divider** spans both columns between entries, with appropriate consistent margins/spacing, and scrolls with the list; none above first/below last.

### Text/style mapping

| UI element | Source of styling / behavior |
|---|---|
| Theme name | Existing **Global Header** font/style/color; **ALL CAPS for display only**, preserve stored name |
| Active check-circle glyph | Existing **Global Header Color**, immediately after name; no click action |
| `Resolutions:` | Existing **Global Sub-Header** font/style/color |
| `Author:` | Existing **Global Sub-Header** font/style/color |
| `Description:` | Existing **Global Sub-Header** font/style/color |
| Author value | Existing **Global Standard Text** font/style/color |
| Description body | Existing **Global Standard Text** font/style/color |
| Resolution buttons | Existing **Global Button** properties including normal, hover, and selected states |
| Instruction line | Existing instructional-text styling |
| Entry separators | Existing **Global Divider** styling and implementation |

Missing metadata/description/thumbnail should **reuse existing Theme Builder behavior**:

- Missing author: leave its value blank.
- Missing description: show the Builder's **“No description”** fallback with its existing **sub-text** styling (not standard body text).
- Description length/limits and content behavior: use **exactly the existing Theme Builder rules** rather than inventing a new character or height limit.
- Missing thumbnail: reuse the Builder's current **no-thumbnail** presentation/state.

## 5. Resolution buttons: visuals and state

- Same button **dimensions** as resolution buttons on Theme Info; use the established Global Button styling and hover behavior.
- Each entry has buttons for **all resolutions contained in that theme**.
- Only the button corresponding to the **currently active theme AND currently active resolution** uses the existing selected styling. All others have their normal state until hover.
- Exactly one active theme has the **check-circle** next to its name; if it is hidden from the Browser, none is marked.
- The checkmark is noninteractive. Existing selected resolution remains clickable.

### Crucial distinction between Builder and Browser buttons

The Browser resolution button is a **theme-and-resolution load command**, not merely the Builder's edit-session resolution switch.

| Current | Click in Browser | Behavior |
|---|---|---|
| Theme A / 720p | Theme B / 720p | Load Theme B; keep 720p |
| Theme A / 720p | Theme B / 1080p | Load Theme B; switch to 1080p |
| Theme A / 1080p | Theme B / 720p | Load Theme B; switch to 720p |
| Theme A / 720p | Theme A / 1080p | **Reload Theme A at 1080p**; discard unsaved edits |
| Theme A / 720p | Theme A / 720p | **Reload saved Theme A/720p**; discard unsaved edits, even though selection is unchanged |

The explicit selection is a **load**, even if theme or resolution is unchanged. Do not optimize it into a no-op if that would preserve unsaved edits contrary to the intended behavior.

### Reuse and persistence (critical)

The **Theme Builder master theme selector** already loads a different theme and **automatically saves that choice into `settings.json`**. The Theme Builder's ordinary resolution buttons switch working resolution live but **do not automatically save that edit-session resolution change** without **Save Adjustments**.

The Theme Browser must combine:

1. **Theme load + automatic persistence** behavior of the Builder's **master theme selector** (similar to hamburger-menu theme swaps).
2. Resolution-target selection and live resolution switching already handled by the current theme infrastructure.

When any Browser resolution is clicked:

1. Load the selected theme's **saved** configuration for the chosen resolution, **replacing the complete current working theme state** and dropping unsaved Builder edits (whether the theme is different or the same).
2. Apply **ALL** theme properties live, not merely Global values: layer geometry, text, shadows, borders, media assets, visibility, custom layers, and other defined settings.
3. Change runtime resolution **if needed**; rely on existing Smart Screen/fullscreen behavior, not a new implementation.
4. **Automatically persist the selected theme/resolution to `settings.json`** using the existing theme-selection persistence pathway; **Save Adjustments is not required** just for a Browser load.
5. Never save unsaved edits into the theme ZIP or modify the ZIP's `theme.json` merely from a Browser load.
6. Update the active theme checkmark, selected resolution button, and Browser styling immediately.
7. **Keep the Browser open** and preserve catalog scroll position.

**Tab switch ≠ load**: Simply moving Builder → Browser → Builder, or interacting with previews, preserves ongoing unsaved Builder edits. They are discarded only upon an intentional Browser theme/resolution load (as with existing theme swaps).

If a load fails, do not falsely present/save an unsuccessful selection. Use established error handling and leave the Browser usable. Claude should audit the existing transaction/persistence behavior rather than invent an entirely separate loader.

The existing main launcher startup windowed/fullscreen behavior is **out of scope**. Do **not** audit or redesign launch-time resolution fallback as part of this Browser feature.

## 6. Thumbnail capture and display sizing

Use a **single thumbnail asset per theme**. Change new thumbnail **capture dimensions** to **480 × 270 physical image pixels**. This is the largest *intended image display* size, so the enlarged preview does not need to upscale a smaller capture.

| Use | Fixed WPF display size | Image scaling |
|---|---|---|
| New stored capture | **480 × 270 image px** | Capture at native dimensions |
| Theme Browser pop-out | **480 × 270 DIPs** | Normally native-size at 100% DPI |
| Theme Info thumbnail (Builder) | **320 × 180 DIPs** | High-quality downscaling |
| Theme Browser thumbnail | **160 × 90 DIPs** | High-quality downscaling |

- Use WPF **`BitmapScalingMode.Fant`** initially for both smaller displays, not NearestNeighbor (which is usually unsuitable for downscaling mixed text/graphics/screenshots). Preserve 16:9 geometry, avoid distortion.
- Subwindows already follow **Windows DPI scaling**. These are fixed WPF dimensions in **device-independent pixels**, not percentages of the source file. At 150% DPI the physical screen footprint increases as Windows scales the UI. Do not add custom per-monitor scaling logic for the initial feature.
- Keep capture/display dimensions as obvious named constants/configuration points in code, not scattered magic numbers. A future larger Browser may support returning to 640 × 360 capture/pop-out.
- **No backward-compatibility engineering or thumbnail migration** for already captured 640 × 360 images. They need not be rejected; simply use the normal display path. Users can recapture with one click. Do not silently recapture or create multiple asset variants.
- Audit where capture currently happens and where the same stored preview is used so the **Theme Info display remains 320 × 180** while the capture changes to 480 × 270.

## 7. Browser thumbnail border and hover

### Normal thumbnail

- Image: **160 × 90 DIPs** (image-only dimensions, border outside where appropriate).
- Border **color = existing Global Border Color** (final decision; supersedes an earlier Global Background Color experiment).
- Border thickness = **existing Global Border Size**.
- Border radius = **0 (square corners)**.
- No extra padding or unique browser-border property.

Using the same border color as the pop-out is intentional: the dimming layer behind the pop-out reduces competition when the two are onscreen at once.

### Hover effect

- Overlay thumbnail **image area**, not outer border, with **black at 60% opacity**.
- Center a **magnifying glass with plus** glyph at **32 × 32 DIPs**.
- Glyph color: **off-white `#FFE8E8E8`**.
- Use **hand cursor** on hover.
- On mouse leave, remove the overlay/glyph and return to normal; no animation needed initially.
- Clicking opens enlarged preview.

## 8. Enlarged preview pop-out

- Preview image: **480 × 270 DIPs**; these measurements **exclude** background padding and border.
- **Background/fill:** existing **Global Background Color**.
- **Actual outer border color:** existing **Global Border Color**.
- **Outer border thickness:** existing **Global Border Size** (initial design).
- **Outer border radius:** existing **Global Border Radius** (applies only to outer frame; image itself stays rectangular).
- **Padding around image:** fixed **8 DIPs initially**, subject to visual tuning. No user-facing property.
- Pop-out floats **above** the list without reflowing/resizing rows.
- Initial position: **centered over the metadata-column area**, not anchored to the clicked thumbnail; it may vertically overlap other theme entries. The precise placement is intentionally subject to a live visual polish pass.
- The user's Photoshop test fit the expected Browser window, so no bespoke handling for higher Windows scaling or tiny windows is requested yet. Audit should flag only real layout/clipping issues, not invent a separate positioning system without evidence.

### Full-window dim layer

Implement the dimming infrastructure **from the outset** (not deferred), so its opacity can be tuned after seeing it in WPF.

- Initially **30% opaque black** behind the enlarged pop-out, above Browser UI.
- Preview frame/image stays undimmed.
- Cover the relevant full shared-window/Browser surface without hand-calculating pixel dimensions if normal WPF stretch/grid layout suffices.
- Make opacity easy to adjust in code, including **0%** for comparison; no Theme Builder UI property required.
- Block pointer interaction and mouse-wheel scrolling on the Browser beneath while open; don't allow click-through to resolution buttons.
- Close preview on **any mouse click**, whether on preview/frame or outside, and on **Escape**. No X close button.
- After dismissing, restore interaction and retain the prior scroll position. Closing preview **must not close the Browser window** or apply/save a theme.

## 9. Keyboard behavior

- No new keyboard shortcut to **open** the Browser yet.
- Arrow keys should scroll the list where normal WPF focus/scroll behavior allows.
- Preserve normal WPF keyboard focus behavior: Tab can naturally move through buttons and Enter/Space can activate them if applicable. No custom focus/navigation system is requested for the first pass.
- Escape dismisses an **open enlarged preview**. Otherwise preserve the shared window's existing Escape handling; avoid conflicts with launcher fullscreen Escape semantics.
- No mouse-wheel scroll-through while the enlarged preview is open.

## 10. Refresh versus load: important examples

- Open Builder, make unsaved edits, switch to Browser: **edits remain**; Browser displays catalog and thumbnails; `settings.json` unchanged.
- Enlarge/dismiss thumbnail: **edits remain**; no loading/saving.
- Return to Builder without clicking a resolution: **edits remain**.
- In Builder, modify theme and create/save under new name; switch to Browser: **catalog refresh** inserts the new eligible theme alphabetically, without implicitly loading the theme or discarding any working state beyond what the Builder's own actions already do.
- Delete or hide theme in Builder; return to Browser: refreshed list reflects change.
- Click another theme at same resolution: load full saved configuration and persist selected theme/resolution to `settings.json`.
- Click active theme at another resolution: **full saved configuration reload**, discard unsaved edits, persist target selection.
- Click active theme at current resolution: **intentional full reload**, discard unsaved edits; not a no-op.
- Successfully load a different theme: Browser appearance follows newly active theme and active selection indicators move; **window remains open and list remains at same scroll position**.

## 11. Separate Theme Builder polish task — DO NOT FOLD INTO BROWSER IMPLEMENTATION

This terminology/layout cleanup is a **separate focused edit** to be handed off independently, not part of this Browser work:

| Current Builder label | Desired label |
|---|---|
| `Theme sizes:` | `Resolutions:` |
| `Resolution:` (add-resolution combo) | `New Resolution:` |

- Recheck geometry: make both rows share a sufficiently wide label column so resolution buttons and combo begin at the same X; avoid shifting downstream controls or reopening unrelated alignment work.
- Investigate a resting combo value such as `[New]` rather than showing the active resolution, **but first verify what the combo currently contains and whether it also navigates existing resolutions**. Do not assume the behavior.
- No code changes requested in this document; track for its own handoff.

## 12. What Claude Chat should review before preparing the audit handoff

Check **for genuine omissions or contradictions**, particularly:

1. Existing Builder **master theme selector** and hamburger-menu theme swap: exact load/persist-to-`settings.json` flow and how to reuse it when target **theme + resolution** are supplied together.
2. Existing Builder **resolution buttons**: distinguish edit-session resolution changes from intentional Browser full reloads, including current theme/current resolution re-click.
3. Reuse of existing theme metadata, `Show Theme In` filtering, missing thumbnail/description/author handling, description length/formatting, Global style resources, and shared Global Divider.
4. Capture-to-480×270 change, fixed smaller display sizes, Fant interpolation, WPF DPI scaling, shared thumbnail asset, and lack of unnecessary migration.
5. Shared window navigation/hamburger tab activation; catalog refresh on tab activation; maintain scroll and unsaved Builder state until deliberate load.
6. Preview modal overlay, dim/hover layers, click/Escape capture, no click-through, no mouse-wheel scroll-through, and positioning relative to metadata column.
7. Theme Browser button selected/checkmark state after load and when active theme is filtered out.
8. Error behavior for invalid catalog entries and failed theme loads, using existing logging/handling.

**Review output requested:** Give a concise summary of any unresolved *user-facing choices*, ask targeted questions **only where the answer cannot responsibly be inferred from this document or the existing implementation**, and identify what Claude Code needs to inspect. Do not expand scope to unrelated startup fullscreen/resolution selection or introduce new style controls without cause.

### Agreed collaboration workflow

1. Human + ChatGPT design requirements and edge cases (this document).
2. Claude Chat reviews specification and asks any truly outstanding questions.
3. After answers, Claude Chat drafts focused `hand-off-audit.md` for Claude Code.
4. Claude Code audits actual C#/XAML and reports existing code paths, reuse candidates, constraints, and risks — **no premature implementation**.
5. Human and assistants review findings, decide necessary revisions, and update implementation plan.
6. Claude Chat writes one or more focused implementation handoffs; Claude Code implements, builds, and user tests/polishes.

---

**Design intent in one sentence:** A neatly styled, alphabetically scrolling two-column theme catalog, with one thumbnail per theme, direct theme+resolution buttons that **reload and persist** the selection, a hover-to-enlarge modal preview with dimming, and maximum reuse of the existing Theme Builder's tested behavior—without disrupting unsaved edits merely by opening or browsing the tab.
