# New size scaler (new-size-scaler.md)

Spec for Claude Code. Work in two steps: **STEP 1 is a read-only audit, STEP 2 is implementation and starts only after the user says "go".** Trust order: the code first, themes.md second. If the code and themes.md disagree, report it in STEP 1.

## Goal

When a new layout size is created, offer to scale the copied layout instead of always making a 1:1 copy. Today a new size starts as a copy of the layout being edited (custom layers excluded, background resized to fit; themes.md 3.1), so a 1080 layout added from the 720 one needs every layer moved and resized by hand. The scaler gives a close starting point; fine-tuning by hand afterward is expected.

## Decisions (made with the user, 2026-10-28)

1. **A prompt appears when a new size is created**, from a preset pick or from Keep on a custom size (audit item 1 settles the exact moment; **superseded:** the copy and the prompt happen on Apply, and the Keep button was removed, amendments 1 and 9). No new combo, button or setting on the page.
2. **Three choices:**
   - **A. Scale everything.**
   - **B. Position only.**
   - **C. Do nothing** (keep the layout exactly as today: a 1:1 copy, background resized to fit).
3. **Any aspect ratio is allowed for A and B.** The prompt shows the factors and a warning line when the ratios differ.
4. **Factors.** `fx = newW / oldW`, `fy = newH / oldH`, `u = min(fx, fy)`. When the ratios match, `fx = fy = u`.
5. **What A scales:**
   - X and Y: X by `fx`, Y by `fy`.
   - Width and Height: Width by `fx`, Height by `fy`.
   - Font sizes, by `u`.
   - Border sizes, by `u`.
   - Everything else is untouched: corner radii, shadow blur and depth, every margin (banner text margin, Game Info body right margin), every nudge (Search label vertical nudge, Games List arrow nudge), colors, fonts' families, file paths, opacity, visibility, Z, names. The Games List row spacing, indent and arrow gap are hard-coded, so they do not scale and that is accepted.
6. **What B scales:** X and Y only (by `fx` and `fy`). Sizes and everything else stay.
7. **C changes nothing** beyond what the copy does today.
8. **Superseded 2026-10-28 (second amendment 7, final after testing): image and video layers stretch per axis like every other box.** Original text: **Image and video layers keep their own ratio** on a mismatch: Width and Height use `u` instead of `fx` / `fy` for these layers, X and Y still use `fx` / `fy`, and the box stays **top-left aligned** at its scaled X / Y (any leftover gap falls on the right or bottom). On a ratio match this is identical to the normal rule. Which layers count as media layers is audit item 4.
9. **Rounding.** Font sizes and border sizes round to whole numbers. X, Y, Width and Height round to whole pixels. All rounding is half away from zero (`MidpointRounding.AwayFromZero`; 17 x 1.5 = 25.5 becomes 26), never the default half-to-even (see the grid rounding trap in themes.md section 7). A value that was null stays null.
10. **Clamping.** A scaled font or border size is clamped to its field's existing editor range (text 6 to 72, nav 4 to 40 via `MinNavSize` / `MaxNavSize`, Random Game Display and banner max sizes 6 to 200, border 0 to 50). Use the existing constants, do not copy the numbers. A border of 0 stays 0. Positions and sizes are not clamped.
11. **Prompt content.** Title "New size layout". Lines: the sizes ("Adding 1920 x 1080 from 1280 x 720"), the factors ("Width x1.500, height x1.500"; three decimals, trailing zeros trimmed), and, only when the two factors differ, a warning line ("Aspect ratio differs: positions stretch to fill; text and borders follow x1.000"). Three radio buttons with the plain labels "Scale everything (positions, sizes, fonts, borders)", "Scale positions only", "Keep the layout as is". OK and Cancel. Built in code like `PromptForLayerName` (Chrome palette, `MakePromptButton`), no DataContext needed.
12. **Preselection.** The remembered choice is preselected, **unless the aspect ratio is far off**, in which case C is preselected. Far off means `abs((newW / newH) / (oldW / oldH) - 1) > 0.15`. (16:10 against 16:9 is 10% off and counts as close; 4:3 is 25% off and counts as far; 1366x768 is close.) First run with nothing remembered: C.
13. **Remembered choice.** A per-install editor preference in settings.json, like `ShowGuideLines` and `SmartFullscreen`: `ConfigurationSettings.NewSizeScaleChoice` (string "A" / "B" / "C", default "C"), loaded in the MainViewModel constructor, saved by `PersistConfigurationToDisk`. Not in ThemeSettings, LoadTheme, `ApplyThemeSettings`, the save DTO or GlobalThemeResources. It is saved when the user presses OK **and the ratio was close**; an OK on a far-off ratio does not overwrite it (so accepting the automatic C does not erase a remembered A).
14. **Cancel.** No size is created and nothing changes: the combo returns to the size being edited (under the loading guard so it does not re-trigger), and a pending custom size stays pending, subject to audit item 1.
15. **New sizes only.** The prompt never appears when switching to a size that already has a layout (Theme sizes buttons for a saved size, the size combo for a size with a layout), and never for Remove / Delete Size, Load Theme, or the hamburger's Layout size pick.
16. **Existing flows keep their order.** The prompt comes first, then the copy and scaling, then everything that follows a size pick today (switching the active size, the Smart fullscreen auto-enter through `RunSizePick` / `HandleLayoutSizePickForFullscreen`). The user confirmed that picking a 1080p preset auto-enters fullscreen and shows the 720 layout top-left until saved; that must not change.
17. **Nothing else changes.** The scaled layout is an ordinary working edit: not saved until Save Adjustments / Save Changes, reverted on close by the existing snapshot, bundled and saved like any layout.

## Amendments after the STEP 1 audit (user, 2026-10-28; these override the decisions above where they differ)

1. **Prompt spot.** The prompt opens **before `RunSizePick`**, only when `_settings.Layouts` has no layout at the new size, in three places: `CboResolution_SelectionChanged` (a preset), `BtnSavedSize_Click` (a Theme sizes button for a size removed but not saved) and `BtnAddCustomRes_Click` (before `SwitchToResolution`). **The custom size copy happens on Add, not on Keep** (Keep only commits the pending size; decision 1's "Keep" is replaced by "Add"). Cancel creates no size and restores the combo under `_loading = true` ("Custom..." if a pending size is active, otherwise the `ResolutionPreset` of the current layout); on Add, Cancel leaves the entry row and any pending size as they were. If the open dropdown misbehaves while the dialog is up, open the prompt through `Dispatcher.BeginInvoke`.
2. **Banner.** A leaves the Marquee entry's `TextFontSize` unchanged (400x100 reference-box units, it already follows the rect) and scales its `BorderSize` by `u` (the card's border is outside the Viewbox, in real pixels; themes.md 3.9). The Random Game Display max sizes and borders are real pixels and scale by `u`.
3. **Range constants.** Add `MinTextFontSize` / `MaxTextFontSize` (6 to 72) and `MinBorderSize` / `MaxBorderSize` (0 to 50) to `LayerRegistry` for the scaler; use the existing constants for nav (`MinNavSize` / `MaxNavSize`), Random Game Display and banner ranges. The editor's own literals stay as they are.
4. **Superseded by second amendment 7 (media layers stretch per axis; `IsScaleMediaLayer` was deleted).** **Media layers** (decision 8): `SplashImage`, `VideoPreview`, `MarqueePanel`, shape layers with a non-empty `ImagePath`, and both Random Game Display parts when the `RandomGameDisplay` entry has an `ImagePath`. Not `ImagePreviewsBody` (a Game Info group part) and not `MainBackground` (resized to fit). `LayerRegistry.IsScaleMediaLayer` decides this.
5. **Remembered choice.** OK on a close ratio sets `ConfigurationSettings.NewSizeScaleChoice` in memory only; the prompt does **not** call `PersistConfigurationToDisk`. Save Adjustments writes it with the rest of settings.json, like the guide toggles.
6. Everything else stands: factors, rounding half away from zero, clamping, nulls stay null; the scaler runs on the new copy only, after the background resize and before `ResolveAndStoreFrom`, never on `_currentLayout` or `SavedThemeLayouts`.

## Second amendments (user, 2026-10-28, after the first build; these override decision 8 and amendment 4)

7. **Media layers stretch per axis.** The keep-your-ratio rule (decision 8, amendment 4) is removed: under A every positionable layer's Width scales by `fx` and Height by `fy`, like any other box, so backdrops and frames stay aligned with their content on a mismatch. Fonts and `BorderSize` still scale by `u` (same exclusions). `LayerRegistry.IsScaleMediaLayer` and its `randomDisplayImagePath` argument are deleted.
8. **Search & Buttons on a mismatch.** When the factors differ (the prompt's warning-line test, `LayoutScaler.FactorsDiffer`, so 1366x768 still counts as matching), A treats the Search & Buttons group (SearchLabel, SearchBox, RandomButton, SortButton, OptionsButton, HamburgerButton) like B: X and Y scale by `fx` / `fy`; Width, Height, TextFontSize and BorderSize stay. On a matching ratio the group scales like every other layer. B and C are unchanged. The rule is `LayerRegistry.IsPositionOnlyOnMismatch(id)`, called from `LayoutScaler.Scale`.
9. **Keep button removed; Apply commits.** The custom size row has only Apply (`BtnAddCustomRes`). Apply switches to the typed size (with the prompt for a new size) and then calls `CommitPendingSize`, so the size joins the combo and the row closes at once; Apply on the current size commits a pending size and closes the row. `BtnKeepCustomRes` and `BtnKeepCustomRes_Click` are gone. `CommitPendingSize`, `DropPendingSize` and the pending state stay: Save Adjustments and the theme save still call `CommitPendingSize`, and a Theme sizes button for a custom size removed but not saved still makes a pending size (Apply, Save Adjustments or a theme save commits it).

## Final behavior after testing (user, 2026-10-28)

- **Mismatched ratios stretch per axis (final decision).** Choice A scales every positionable layer's Width by `fx` and Height by `fy`, image layers and shape layers with a file included; fonts and borders follow `u`. The user tested 1024x768 and kept this: the layout fills the window, and keeping each layer's ratio would leave the Marquee and Games List too small anyway. Decision 8 and amendment 4 are superseded.
- **Search & Buttons on a mismatch:** the group (SearchLabel, SearchBox, RandomButton, SortButton, OptionsButton, HamburgerButton) moves to its scaled positions but keeps its size, fonts and borders (`LayerRegistry.IsPositionOnlyOnMismatch`, mismatch = `LayoutScaler.FactorsDiffer`). On a matching ratio it scales like every other layer.
- **Persistence:** Apply (and a preset pick) adds the size to the Resolution combo and makes it the working layout. The Theme sizes row reads the saved theme, so it changes only after Save Changes. Save Adjustments writes the working layouts to settings.json, so the size stays in the combo after closing and reopening the builder and after restarting the launcher. Closing the builder without saving removes the new size (close-revert snapshot), and picking it again shows the prompt again. Picking a size that already has a layout never prompts.
- **Known limits (parked in themes.md 6b):** the Search & Buttons group can overlap when scaling down on a mismatch (at 1024x768 the search box partly covers the search label and the four buttons touch), because positions shrink while sizes do not; image layers (Splash Image, shape layers with a file) distort on a mismatch; a 1px border stays 1px at x1.25 (1.25 rounds down) while a 2px border becomes 3px.

## STEP 1: read-only audit (no edits)

Report findings, with file and method names, then stop and wait for "go".

1. **Where a new layout is created from a copy.** Find every path that builds a layout for a size from the layout being edited: the preset path in `CboResolution_SelectionChanged` / `CboResolutionApply`, the custom size Keep / Save Adjustments / theme save commit of a pending size, and anything else (including picking a removed-but-unsaved preset, and a Theme sizes button for a size with no layout; themes.md 3.1). For each, say when the copy happens (on entry of a pending custom size, or on Keep), whether it runs inside `RunSizePick` / `SizePickRunner`, and whether a modal dialog can safely open at that point (the `_deferBuilderDrop` flag, the `_loading` guard, the combo's selection handler). Propose the single spot for the prompt and how Cancel restores the combo.
2. **Which entries and fields get scaled.** List every `LayerLayoutEntry` field that is a font size (expected: FolderFontSize, GameFontSize, TextFontSize, SectionHeaderFontSize, plus any other) and every border size field (BorderSize and any other), and the ranges and constants the editor uses for each. Confirm the rect fields' types (int or double).
3. **Banner double-scaling check.** The no-art banner's TextFontSize and BorderSize (Marquee entry) are in 400x100 reference-box units and the card already scales with the Marquee rect (themes.md 3.9). Scaling them with A would scale them twice. Confirm from the code, and recommend whether A should leave them alone (expected recommendation: leave them alone). The user decides before "go". Do the same check for the Random Game Display card's per-state max size and any other value measured inside a scaled box.
4. **Media layers.** List the entries that hold an image or video (non-empty ImagePath or a video slot): shape layers, Splash Image, Video Preview, Random Game Display, the Marquee logo, and anything else. Propose which count as media layers for decision 8 (the box keeps its ratio on a mismatch), and note which of them are skipped by the existing copy (custom layers are excluded from the copy).
5. **Existing copy details.** What the copy does today for the Main Window Background ("resized to fit"), custom layers (excluded), `Name`, and `CopyStyleFrom`; make sure the scaler runs after the copy and does not disturb those.
6. **The ring rule.** Confirm that scaling the raw X / Y / Width / Height is correct for bordered layers under decisions 59 and 62 (X / Y are the ring's outer corner, Width / Height the content size, outer box W + 2B), and say how the scaled border size interacts (the ring grows with the scaled border; nothing is clamped to the canvas).
7. **Settings wiring.** Where `ShowGuideLines` / `SmartFullscreen` are loaded and saved, so `NewSizeScaleChoice` can follow the same pattern.
8. **Anything in themes.md that is wrong or missing** for this feature.

End the audit with a short plan: the new files and methods, the edit list, and the test checklist below adjusted for what you found.

## STEP 2: implementation (only after the user says "go")

- **Pure scaler** in a new `Services/LayoutScaler.cs` (like `Services/SmartFullscreen.cs`): takes the copied entries, the old and new sizes and the choice, and returns the scaled entries (or scales in place on the copy, never on the source or on `SavedThemeLayouts`). Includes the factor calculation, the far-off ratio test (decision 12), and the rounding and clamping rules. Reuse the editor's range constants.
- **Prompt** in the Advanced tab code (next to `PromptForLayerName`), returning the choice or cancel.
- **Hook** at the spot the audit proposes, before the copy; Cancel restores the combo as decision 14.
- **Settings**: `NewSizeScaleChoice` as decision 13.
- Keep each file's line endings (`.cs` and `.xaml` CRLF, `.md` LF).
- Do not touch: Smart fullscreen code, the Theme sizes row, Remove Size, Load Theme, defaults in `LayerRegistry`, any saved-data format.
- When done, update themes.md (3.1 new-size paragraph, a decision 65 for this feature, the 6b entry marked done, a trap note, the file map, the change log, the Last verified line) and CLAUDE.md if it lists these flows.

## Test checklist (adjust after STEP 1)

1. 1280x720 to 1920x1080 preset, A: every layer X / Y / Width / Height x1.5 (rounded); Games List folder font 17 becomes 26, game font 15 becomes 23 (22.5 rounds up), a 1px border becomes 2, a 2px border becomes 3; corner radii, shadow blur and depth, margins and nudges unchanged; Games List row spacing unchanged.
2. Same size, B: only X and Y change.
3. Same size, C: identical to today's behavior.
4. Cancel on a preset pick: the combo returns to the old size, no new size appears, no auto-fullscreen.
5. OK on a preset pick: the new size shows as before, Smart fullscreen auto-enters as today (after the prompt, not before).
6. Custom 1366x768 typed and Add pressed (close ratio, factors about 1.067 and 1.067): the remembered choice is preselected; warning line absent or minimal.
7. Custom 1280x800 typed and Add pressed (16:10, 10% off): remembered choice preselected, warning line shown, fonts and borders use u = 1.000, Y scales x1.111 and X x1.0.
8. Custom 1024x768 typed and Add pressed (4:3, 25% off): C preselected; picking A anyway works; the remembered choice is not overwritten by an OK on C.
9. Remembered choice survives closing and reopening the app (settings.json), and is not in the theme zip.
10. On a mismatch (1024x768, 1280x800) with A, a shape layer and the image layer behind or over the same rectangle still line up, and nothing keeps a separate ratio.
11. Grouped layers (Search & Buttons, Game Info, Random Game Display): every part scaled, relative layout kept.
12. Switching to a size that already has a layout, Remove Size, Load Theme, and the hamburger Layout size pick: no prompt.
13. Save Changes, reload the theme, reopen: the scaled layout persists; closing the builder without saving reverts it.
14. Banner (per audit item 3): no double scaling; with A the banner's max text size is unchanged and its border scales by u.
15. Clamping: a size near the top of a range does not exceed it (for example a 60 pt font at x2).
16. A Theme sizes button for a size removed but not saved shows the prompt.
17. Cancel on Add leaves the entry row and any pending size as they were.
18. With a pending custom size active, Cancel on a preset pick leaves the combo on "Custom...".
19. The remembered choice reaches settings.json only through Save Adjustments (or another existing settings save), not from the prompt.
20. 720 to 1080 and 720 to 1366x768 with A: the Search & Buttons group scales like everything else.
21. 720 to 1024x768 and 720 to 1280x800 with A: the Search & Buttons group moves to its scaled positions and keeps its 720 size, fonts and borders. B and C unchanged.
22. The custom size row shows only Apply; Apply still opens the prompt and commits the size (it joins the combo, the row closes); Save Adjustments and Save Changes still store it; Delete Size and Remove Size still work.
