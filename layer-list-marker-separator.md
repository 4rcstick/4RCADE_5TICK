# Handoff: layer list marker and separator (pass 1 of 2)

Read `CLAUDE.md` first, then `themes.md` section 3.7 and decisions 37, 66 and 67. This handoff is self-contained, but those are the rules and the spec you work under.

## Goal and why

Two small changes to the Advanced tab's layer list (`LstLayers`), so the list explains itself:

1. **Marker.** Shape Layers 01 to 04 and custom layers are the only layers that can be renamed. Mark them in the list with a pipe (`|`) in front of the name, in a fixed light blue, so they are easy to tell apart from the built-in layers.
2. **Separator.** The movable layers sit at the top of the list and the Main Window Background sits at the bottom and cannot be moved up or down. Draw a separator line between the last movable layer and the Main Window Background, so the list shows that split.

This is **pass 1 of 2**. Pass 2 (a new pinned "Scrollbars" row below the Main Window Background, with per-resolution scrollbar overrides for the Games List and Game Info) is a separate handoff that comes later. Do not build or draft it now. Only keep this pass from making it harder: the separator should sit above the pinned block at the bottom of the list (today that is only the Main Window Background; pass 2 adds one more pinned row below it).

Final list order, top to bottom: movable layers, separator, Main Window Background (pass 2 adds Scrollbars below it).

## How to work (follow CLAUDE.md pass structure)

- **STEP 1 is a read-only audit.** Change nothing. Report findings, then **stop and wait**.
- **STEP 2 (implementation) starts only after the user says "go".**
- Never commit. The user builds, tests and commits.

## Current behavior (from themes.md, check against the code)

- `LstLayers` shows one row per layer; groups (Game Info, Search & Buttons, Random Game Display) are one row each. Rows come from `LayerRegistry`; the Main Window Background is not positionable, cannot be hidden and cannot be moved up or down.
- Rename (decision 37) works for shape layers and custom layers only (`IsBoxId`), through the Rename button or a double-click on the selected row, via `PromptForLayerName` (24 characters max). The name is `LayerLayoutEntry.Name`. `GetLayerDisplayName` is the one display rule for the list and the heading.
- The Layers header has a `?` glyph whose tooltip text is the single resource `LayersHelpText` (UserControl resources, `[SECTION: Layers Help]`), one point per line separated by `&#10;`.
- The Theme Builder's list background is hard-coded black with white text (Chrome Palette, `ChromeListBoxStyle`).

## STEP 1: read-only audit (report each item, change nothing)

1. **List construction.** How `LstLayers` is filled: item type, item template, where display text comes from, how groups appear as one row, and where the Main Window Background is placed so it stays at the bottom.
2. **Move up / down.** The arrow buttons' code: what stops the Main Window Background from moving, what stops other layers from passing it, and how list order maps to Z order.
3. **Index and position assumptions.** Everything that would break if the list gained an extra element or a second look for one row: "reselect after rebuild" reads, multi-select (`PnlMultiSelect`), right-click Copy / Paste (decision 67), double-click rename, nudge, group move, keyboard navigation. List each place that uses an index, `SelectedItem`, `Items.Count` or item order.
4. **Renamable set.** Confirm that the rows to mark are exactly the shape layers (01 to 04) and custom layers (`Custom_*`), find every place that decides "can be renamed" (`IsBoxId` or any other), and say whether one rule can drive the marker so it cannot drift from the rename rule.
5. **Other places the display name appears.** The heading `TxtLayerName`, the multi-select panel, prompts, tooltips, undo or copy text. The marker is for the list rows only; report anywhere it could leak in.
6. **Colors.** The `[SECTION: Chrome Palette]` naming pattern and its existing brushes; the unselected, hover and selected row colors of `ChromeListBoxStyle` and its item container. State whether `#FF4FC3F7` is clearly legible on all three (you cannot see the screen: judge from the actual color values and flag any doubt).
7. **Pinned concept.** Whether the registry or list code already has a notion of "pinned / not movable" that the separator can key on, so pass 2 can add a second pinned row without reworking it.
8. **Separator options.** Propose how to draw it, with trade-offs, and recommend one. **Preference: an approach that adds no extra list item** (for example a top border or margin on the Main Window Background row's container), so item counts, indexes and selection logic stay unchanged. If a separate item is the only way, say what it would break and how to make it non-selectable, non-focusable and skipped by keyboard navigation.
9. **Spec vs code.** Compare themes.md 3.7 and decisions 37, 66, 67 with the code; report mismatches (the code wins).
10. **Impact summary.** The list of files STEP 2 would change, the planned edits per file, and any risk.

Report the audit in this order: findings per item, mismatches, proposed STEP 2 edit list, risks, open questions. **Then stop and wait for "go".**

## STEP 2: changes (only after "go")

### A. Marker
- A `|` in front of the display name on shape layer and custom layer rows only, in the list only.
- The pipe is **display-only**: it is not stored in `Name`, does not count toward the 24-character limit, never appears in the rename prompt's pre-filled text, and never reaches theme.json.
- Only the pipe takes the new color; the name keeps the row's normal text color. Leave a small gap between the pipe and the name, consistent with the row template.
- Color: `#FF4FC3F7` as **one named brush in `[SECTION: Chrome Palette]`** following that section's naming pattern. Never a hex inside a style. One brush for both kinds of layer, so the color can be changed in one place.
- Drive the marker from the same rule as renaming (see audit item 4) so the two cannot drift apart.

### B. Separator
- A thin horizontal line between the last movable layer and the Main Window Background, using the approach agreed in the audit.
- It is not selectable, not focusable and cannot be moved past. No movable layer may be moved down past it into the pinned rows, and the Main Window Background stays unmovable.
- Pick a line color from the existing Chrome Palette if a suitable brush exists (report which); otherwise add one named brush there. No new hex inside a style.
- It must not noticeably change the list's height. The window stays 717 x 885 and the list stays star-sized.

### C. Help tooltip
Add one point to `LayersHelpText` (edit only that resource): a `|` before a layer's name marks shape and custom layers, the only layers that can be renamed. Keep the existing format (one point per line, `&#10;` separators, `xml:space="preserve"`).

### D. themes.md
Update at the end of the pass: section 3.7 (marker and separator), a new numbered decision after the last one, and a dated change log entry (2026-11-04). Correct any mismatch found in the audit. Do not touch other sections.

## Constraints and things NOT to touch

- Do not build, draft or stub the Scrollbars row or any scrollbar override (pass 2).
- Do not change how names are stored, renamed or copied (`LayerLayoutEntry.Name`, `CopyStyleFrom`, `GetLayerDisplayName` output for the heading and prompts), `LayerRegistry` order or Z mapping, or the move up / down rules, other than keeping layers from passing the separator.
- Do not add implicit (keyless) styles to App.xaml. Do not use `RelativeSource AncestorType` bindings for values in list row templates.
- Do not change the window size, the Layers header, or the Copy / Paste behavior.
- Keep comment fencing and style: `// [SECTION: ...]` / `// [END SECTION: ...]` in C#, `<!-- [SECTION: ...] -->` in XAML, and a one-line `// Does X` summary on every method you add or change. Check every `x:Name` and handler you add against the code-behind.
- Line endings: `.cs` and `.xaml` CRLF, `.md` LF. Use the Edit tool; no whole-file rewrites. After edits run `git diff --stat` and confirm no file shows a whole-file change.

## Expected errors

STEP 1 changes nothing, so no errors. STEP 2 should build clean at every step. If you need a multi-file edit that is transient, order the edits so the end state is clean, and say so.

## How to verify (STEP 2)

Build: 0 errors, and report any new warning. Then the user runs and checks:

1. **Marker:** Shape Layers 01 to 04 and any custom layer show a light blue `|` before the name; no other row does (check the Game Info, Search & Buttons and Random Game Display group rows, Games List, Marquee and the Main Window Background). The name stays white.
2. **Legibility:** the pipe is clearly visible on an unselected, a hovered and a selected row.
3. **Rename:** Rename a shape layer and a custom layer. The prompt pre-fills the name **without** a pipe; after renaming, the list shows the pipe and the new name; the heading above the tabs has no pipe; Reset Layer keeps the name. Save the theme and reload it: names carry no pipe.
4. **Separator:** a thin line sits between the last movable layer and the Main Window Background. It cannot be clicked or selected, and Up / Down keys step over it. Moving the lowest movable layer down stops above the separator. The Main Window Background still cannot be moved.
5. **Nothing else changed:** multi-select, right-click Copy / Paste, double-click rename, group move and selection after a resolution switch all behave as before; Add Layer and Remove Layer work; the window size is unchanged.
6. **Help:** hover the `?` glyph; the new line about the pipe is there.

## Report format (STEP 2)

Files changed; anything that did not match these instructions; anything changed beyond them; the test checklist above. Then stop, so the user can build, test and commit.
