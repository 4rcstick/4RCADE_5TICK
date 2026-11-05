# Handoff 3: Docs pass for the Theme Builder tweaks (docs only)

## Goal and why
Project: 4RCADE 5TICK, a C#/WPF MAME frontend. The Theme Builder tweaks are done and verified by eye. Before moving on, make sure `themes.md` (and only `themes.md`) accurately describes the final behavior, with no stale or contradictory text left from before the tweaks. **This is a documentation pass: do not change any code, XAML, or resources.** Do not commit.

## Final state of the work (the facts the docs must match)

1. **Layer list pipes:** each pipe sits 1 px higher than before (a `TranslateTransform Y="-1"` render transform on the `RenameMarker` TextBlock in `LayerListItemTemplate`). Layout, row height and the arrows are unaffected.
2. **Pipe colors:** shape layers (the "default layers that can be renamed", which can hold images and video) keep the blue pipe, brush `ChromeLayerRenameMarker` (`#FF4FC3F7`). Custom layers (user-added with +, Ids starting `Custom_`) get a magenta pipe, brush `ChromeLayerCustomMarker` (`#FFE040FB`), driven by a new `IsCustomLayer` flag on the layer list row item. Other built-in layers (Games List, Marquee, Video Preview, etc.) have no pipe.
3. **"?" tooltip on the Layers header:** the plain-string `LayersHelpText` resource was removed. The tooltip is now rich text built in XAML (bold all-caps section headers, one italic line, blank line between sections, no wrapping, black-weight colored pipes that reuse the same two brush keys as the list), plus a final "Double-click a default or custom layer to rename it." line. It has a dark local style (dark background, white text, subtle border) using brushes in the Chrome Palette; there is no global/implicit ToolTip style. Show delay 300 ms, show duration 60000 ms (unchanged).
4. **New Theme menu header:** when valid templates exist, the menu is: Copy Current Theme, full-width separator, a **"Templates"** heading (normal case, NOT all caps), then the templates. The heading uses the existing `BuilderMenuHeadingStyle` (bold, themeable `Global.Menu.Icon` color, not clickable, not focusable, never highlighted). With no templates, neither the separator nor the heading appears.
5. **Gutter fix:** `HideMenuGutter` in `Views/ThemesManagerControl.xaml.cs` collapses the three light icon-gutter Rectangles of the Windows ContextMenu template for the New Theme menu only. This removed a thin vertical line at the menu's left edge and short stubs between items. The layer right-click menus (and any other menus using the same template) were deliberately left unchanged and likely still show the gutter edge line.

## What to do

1. Read `themes.md` fully and compare it to the five points above and to the actual code (check the code as the source of truth; don't trust the notes from earlier passes).
2. Fix anything in `themes.md` that is wrong, stale, missing or contradictory. Specifically look for:
   - Any remaining mention of `LayersHelpText` (including the parked click-to-open note, previously around line 546) that now points at a resource that no longer exists.
   - Any remaining "TEMPLATES" in all caps in relation to the New Theme menu.
   - Descriptions of the pipe that say it's one color for all renamable layers, or that don't mention the magenta custom pipe, the 1 px offset, or the shared brush keys.
   - Descriptions of the old tooltip text or its old content (including the old wrapping/380 px width).
   - The section 3.23 note and the 2026-11-05 change log entries: make sure they are accurate, not duplicated, and in the file's existing format and order. Merge duplicates if the two follow-up entries overlap.
   - Anything saying the layer right-click menus have the gutter fix, or that all builder menus are gutter-free. Add a short "known/not applied" note that the layer right-click menus still show the gutter edge line, if it isn't already recorded.
   - Any table of contents, section numbering or cross references that no longer line up.
3. Keep edits minimal and in the file's existing voice and formatting. Don't rewrite sections that are already correct.

## Also report (do NOT edit)
- Whether `CLAUDE.md` contains anything now wrong or missing because of these changes, and, if so, a short suggested wording for each point (for example, a convention for rich-text tooltips with the local dark style, or the `HideMenuGutter` approach for context menus). Do not modify `CLAUDE.md`; I'll review the suggestions first.
- Any code comments that are now inaccurate (file and line), without fixing them.

## Constraints / do NOT touch
- No changes to any code, XAML, resources or settings files. Only `themes.md` may be edited.
- Don't edit `CLAUDE.md`.
- Don't delete, move or commit the untracked spec files (`audit-theme-builder-tweaks.md`, `theme-builder-tweaks-implementation.md`, and this one).
- Don't commit, stash, revert or reset anything.

## How to verify
Report back with: the `git diff` of `themes.md`, a short list of what each edit fixed, the `CLAUDE.md` suggestions (or "nothing needed"), any inaccurate code comments found, and confirmation that `git status` shows `themes.md` as the only newly modified file from this pass.
