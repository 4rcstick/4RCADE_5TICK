# Tweak Batch: Image Shadows, Publisher Alignment, Multi-Select Shift

Project: 4RCADE 5TICK (C#/WPF), Theme Builder, beta.4.
Date: 2026-10-21.
Companion docs: `themes.md` (living spec) and `game-info-migration.md` (the earlier migration; its working rules in section 0 still apply).
**Trust order: code first, themes.md second.** If this file disagrees with the code, the code wins: report the mismatch, do not guess.

Three parts, done in order. **Stop after each part** so the user can build, test and commit. Do not start the next part until told to. Do not commit anything.

---

## 0. How to work

- Edit the repo files directly. Keep the project's comment style (`// [SECTION: Name]` ... `// [END SECTION: Name]`, `<!-- [SECTION: Name] -->`).
- XAML: every `x:Name` and handler is referenced from code-behind; keep them all when replacing blocks.
- Order edits so the end of each part builds clean: storage and defaults first, then forwarders, then bindings, then UI, then keys.
- **Step 0 (before any edit): read the current state.** Do not assume it matches the earlier specs. Open and read:
  - the Game Info handler implementation in `AdvancedThemesTabControl.xaml.cs` from the earlier migration (the shared Tag-keyed handlers, the accessor table, `LoadGameInfoStyleFields`, the `GI{Block}{Control}` control names) and the Rename Layer work done afterwards;
  - `FillGameInfoStyle` and the `AllLayers` / `CopyStyleFrom` code in `LayerRegistry.cs`;
  - the Game Info and Marquee forwarders in `MainViewModel.cs`, and `_lastGameInfoStyleKey` in `MainWindow.xaml.cs`;
  - `MarqueePanelBorder` in `MainWindow.xaml`, `PartPublisher` and `PartPreviewsBody` in `GameInfoLayerControl.xaml`.
  Report in two or three lines anything that differs from the descriptions below, then continue.
- **Runtime trap (themes.md section 7):** the shared Game Info handlers find controls **by name**. A control with a wrong name builds fine and fails only at runtime. Follow the existing naming exactly and, after adding controls, verify every expected name exists in the XAML (the earlier migration did this check).
- Everything is stored per resolution on the layer's entry (decision 8). Edits apply live on Enter or focus loss; invalid input reverts to the stored value.
- No legacy migration. Old themes simply get the registry defaults for any new field.

---

## PART 1: Image shadow / glow (Marquee layer + Image Previews)

### 1.1 What it is

An optional drop shadow on the **images** of two layers. A glow is just a shadow with depth 0 and a bright color (themes.md decision 12); there is no separate glow type. The shadow follows the image's alpha, so transparent PNG areas and letterbox bars cast nothing.

- **Marquee layer** (`MarqueePanel`): the idle logo **and** the game's marquee image share one shadow. The random-spin marquee frames (`RandomizerSpinMarqueeImage`) use the same shadow so nothing pops in or out when the spin ends. The **no-art banner** (`NoMarqueeBanner`) and the spin's **no-marquee fallback card** (`RandomizerNoMarqueeFallback`) get **no** shadow.
- **Image Previews** (`ImagePreviewsBody`): the flyer, title screen and snap images only. The cabinet image on the Specs & Credits page is a separate element and gets **nothing** here.

### 1.2 Storage and defaults

- Reuse the existing generic fields `TextShadowEnabled`, `TextShadowColorHex`, `TextShadowBlur`, `TextShadowDepth` (themes.md decision 26) on the **`MarqueePanel`** entry and on the **`ImagePreviewsBody`** entry. They are already copied by `CopyStyleFrom`, so no new fields and no copy changes.
- Defaults: **off**, `#FF000000`, blur 3, depth 1 (`DefaultShadowEnabled`, `DefaultShadowColorHex`, `DefaultShadowBlur`, `DefaultShadowDepth` already exist). Extend `FillGameInfoStyle` (or add a sibling helper) to fill these for both entries; it must be called from both `BuildDefaultLayout` and `ResolveLayout`, with `??=` / `IsNullOrWhiteSpace` so saved values are kept.
- Ranges as everywhere: blur 0 to 30, depth 0 to 20.

### 1.3 View model and rendering

- `MainViewModel`: add `ThemeMarqueeEffect` and `ThemePreviewsEffect` (`Effect?`, built with the existing `BuildShadowEffect`, read through `GetLayerEntry`). Add `RefreshMarqueeBindings()` raising `ThemeMarqueeEffect`, and add `ThemePreviewsEffect` to the existing Game Info refresh method. Both must also be raised from `RefreshThemeBindings()`.
- `MainWindow.xaml`: bind `Effect="{Binding ThemeMarqueeEffect}"` on the logo `Image`, `MarqueeDisplay` and `RandomizerSpinMarqueeImage`. Nothing else in the marquee panel.
- `GameInfoLayerControl.xaml`: bind `Effect="{Binding ThemePreviewsEffect}"` once on the `PartPreviewsBody` Grid (the shadow is computed from whichever single image is visible).
- **Check that no ancestor clips the effect** (`ClipToBounds` on `MarqueePanelBorder`'s ancestors, `LayoutCanvas`, the Game Info host). A shadow that extends beyond the layer rect must not be cut off. Report what you find; do not add or remove clipping elsewhere.
- Live repaint: add the `ImagePreviewsBody` shadow fields to the existing `_lastGameInfoStyleKey`. Add a new `_lastMarqueeShadowKey` over the four `MarqueePanel` shadow fields that calls `RefreshMarqueeBindings()` (same pattern as the other keys in `ApplyLayout`).

### 1.4 UI

- **Marquee layer**: a new tab `TabMarqueeShadow`, header **"SHADOW"**, after `TabMarqueeLogo`. Add to `AllLayerTabs`; `GetLayerTabs` for `MarqueePanel` returns `{ TabMarqueeLogo, TabMarqueeShadow }`.
- **Game Info group**: a new tab `TabGIPreviews`, header **"PREVIEWS"**, after the NAV tab; add it to the group's tab list.
- Both tabs use the standard shadow block, copied from the existing tabs (same grid, `GLLabelStyle` etc.): heading ("Logo and marquee image shadow" / "Image previews shadow"), a short hint line ("Glow: set Depth to 0 and pick a bright color."), the Drop shadow toggle, then Shadow color (picker + hex) / Blur / Depth, enabled through `IsEnabled` bound to the toggle.
- Hook the controls into the **existing shared Tag-keyed handler design** by adding two blocks to the accessor table: `Previews` (entry `ImagePreviewsBody`, `TextShadow*`) and `Marquee` (entry `MarqueePanel`, `TextShadow*`). Only the shadow accessors are needed for these blocks. Name controls `GI{Block}{Control}` exactly like the existing ones.
- **Gating:** the existing loaders and handlers are probably gated on "Game Info group selected". The `Marquee` block must be gated on the **Marquee layer** being selected instead (add a `LoadMarqueeShadowFields()` call in `LoadSelectedLayerIntoFields` next to the existing Marquee logo branch). Adapt the gating per block; do not make the Marquee tab depend on the Game Info group.
- Reset Layer already restores style via `CopyStyleFrom` for the Marquee layer and the Game Info group parts; verify these shadow fields are covered.

### 1.5 Part 1 tests (list them in your report)

1. Clean build.
2. Marquee SHADOW tab: toggle on with a white color, depth 0, blur 10: the idle logo gets a glow; select a game and the marquee image has the same glow; spin the die and the frames have it too; the no-art banner has none.
3. PREVIEWS tab: shadow on flyer, title screen and snap (cycle with the arrows); the cabinet image on the Specs & Credits page has none; transparent letterbox space casts nothing.
4. Both shadows default off; shadow fields grey out while the toggle is off; invalid numbers revert.
5. Resolution switch copies the values; Reset Layer restores; Save Changes and reload keep them.
6. The shadow is not cut off at the layer's edge.

---

## PART 2: Publisher block text alignment

- Add a **Left / Center / Right** dropdown on the PUBLISHER tab, label "Alignment:", placed under the Size / Color row (before the shadow block, like the Search label's Alignment control).
- Storage: the existing `TextAlign` field on the **`PublisherBlock`** entry. Default **"Right"** (today's hard-coded look), filled by `FillGameInfoStyle`. Reuse `TextAlignChoices`.
- `MainViewModel`: add `ThemePubYearRatingTextAlignment` (`TextAlignment`, same switch as `ThemeSearchLabelTextAlignment`), raised from the Game Info refresh method and `RefreshThemeBindings()`.
- `GameInfoLayerControl.xaml`: bind `TextAlignment` on each of the five Publisher TextBlocks (publisher, year, clone of, rating, play stats), replacing the hard-coded `TextAlignment="Right"`. Bind directly on each TextBlock; do not rely on inheritance from the StackPanel.
- Add `PublisherBlock`'s `TextAlign` to `_lastGameInfoStyleKey`.
- UI control name follows the pattern (e.g. `GIPublisherAlign`, Tag = the Publisher block key). Extend the shared handler table with an optional alignment accessor, or add one dedicated combo handler that follows the same rules (ignored while loading, writes to the entry, `RaiseGuides()` for the live repaint). Fill the combo once from `TextAlignChoices` in `LoadGameInfoStyleFields`.
- Out of scope: alignment for the Title, Headers or Body (the table design should make adding it later easy, but do not add it now).

### Part 2 tests

1. Clean build; default looks identical (right-aligned).
2. Left, Center and Right each change all five publisher lines live; the play stats and "Clone of" lines follow too.
3. Resolution switch, Reset Layer, Save Changes and reload all keep the choice.

---

## PART 3: Multi-select X/Y shift in the layer list

### 3.1 Behavior

- `LstLayers` becomes a multi-select list (`SelectionMode="Extended"`: Ctrl-click and Shift-click). With **one** row selected, everything behaves exactly as now.
- With **two or more** rows selected ("multi mode"), the right column shows only X and Y:
  - The heading shows "N layers selected".
  - The tab control (`TabsLayer`) is hidden and a new panel `PnlMultiSelect` takes its place in the same grid cell (row 1 of `PnlLayerFields`). It holds two labeled boxes, "Move X by (px):" (`TxtMultiX`) and "Move Y by (px):" (`TxtMultiY`), using `GLLabelStyle` / `GLTextBoxStyle`, and a hint line: "Several layers selected. Type an amount to shift every selected layer together: positive X moves right, positive Y moves down. The boxes return to 0 after each move." (use the existing hint text style).
  - Reset Layer, Remove Layer and Rename are hidden. The up/down arrow buttons are disabled. The Add button stays enabled.
- **The X and Y boxes are nudge amounts, not positions.** They show **0** every time the selection changes into (or within) multi mode. Typing a number and pressing Enter or leaving the field shifts every moving entry by that amount (`entry.X += dx` for the X box, `entry.Y += dy` for the Y box). Positive X moves right, negative left; positive Y moves down, negative up. Decimals are allowed, like the single-layer X/Y fields. Each box applies on its own.
  - **After a nudge is applied the box goes back to 0** (set under the `_loading` guard). This is required, not cosmetic: otherwise focus loss would apply the same amount a second time.
  - A blank, non-numeric or 0 entry applies nothing and the box returns to 0.
  - Relative positions between the selected layers never change. Hidden (`Visible = false`) layers move too.
- What moves: for each selected row, every entry of that row (a group row moves **all** of its parts; a single layer moves itself). The **Main Window Background is skipped** (it is not positionable; use `IsBackground`). If nothing movable is selected, show the hint and keep X/Y disabled.
- The "Move all parts together" group checkbox is irrelevant in multi mode (a selected group row always moves all its parts). Leave that checkbox and its behavior unchanged for single selection.
- Nothing about the selection is saved.

### 3.2 Alignment guides

In multi mode draw the guides around the **bounding box** of all moving entries (`GuideInfo(minX, minY, maxRight - minX, maxBottom - minY, color)`), respecting the Show guides preference. `RaiseGuides()` needs a multi branch that runs before its single-selection logic. All X/Y edits in multi mode end with `RaiseGuides()` (which also raises `LayoutChanged`, so the main window moves live).

### 3.3 Code notes

- Add one helper (e.g. `IsMultiSelect` / `GetSelectedUnitKeys()`) and use it everywhere. With Extended mode, `LstLayers.SelectedItem` is just the first selected item, so **grep every use of `LstLayers.SelectedItem`** and decide for each: guard it (return in multi mode), or make it multi-aware. At least `MoveSelectedLayer`, `BtnResetLayer_Click`, `BtnRemoveLayer_Click`, the Rename button and the row double-click rename, `LoadSelectedLayerIntoFields`, `RaiseGuides` and `ApplyField` are affected.
- `LstLayers_SelectionChanged` and `LoadSelectedLayerIntoFields` decide between: no selection, single selection (existing code unchanged), multi mode (new `LoadMultiSelection()`).
- When entering multi mode set `_tabsUnitKey = null`, so that returning to a single selection re-selects that layer's first tab and restores `TabsLayer` visibility and the button row.
- `RebuildLayerList(selectId)` selecting a single row (after rename, resolution switch, reorder, add) simply ends multi mode; that is fine.
- The existing list item style already highlights each selected item; check that multiple highlighted rows look right.
- Double-click rename must do nothing in multi mode.
- Out of scope: Width / Height / Z-order / visibility for multiple layers, drag-select on the main window canvas, persisting selections.

### Part 3 tests

1. Clean build.
2. Single-click behavior is unchanged everywhere (tabs, Reset Layer, Remove, Rename, up/down, guides).
3. Ctrl-click two layers (e.g. Shape Layer 01 + Games List): the panel shows N layers selected with both boxes at 0. Typing 10 in X and pressing Enter moves both right by 10 and the main window follows live; the box returns to 0. Typing -10 moves them back. The same for Y (positive = down). Their relative offset never changes.
4. Shift-click a range that includes the Main Window Background: it is ignored, the others move.
5. Select a group row plus a shape layer: all six group parts and the shape layer move.
6. Guides surround the whole selection; Show guides off hides them.
7. Invalid or blank input applies nothing and the box returns to 0; pressing Enter and then clicking away does not apply the amount twice. Changing the selection (adding or removing a layer with Ctrl-click) resets both boxes to 0. Clicking one row afterwards restores the normal tabs on the first tab.
8. Up/down, Reset, Remove and Rename are unavailable in multi mode; Add still works.
9. Close the Theme Builder without saving: all moves revert (they are ordinary layout edits).

---

## Documentation (after each part)

Update `themes.md` as in the earlier passes:
- Part 1: section 3.7 tab table (Marquee: LOGO, SHADOW; Game Info: TITLE, PUBLISHER, INFO, NAV, PREVIEWS), the Marquee section (shared shadow on logo, marquee image and spin frames; no-art banner excluded), section 3.12 (Previews shadow), the traps section (the Marquee block's gating), a decision for the shared Marquee shadow and the Previews shadow, change log.
- Part 2: section 3.12 (Publisher alignment, `TextAlign` on `PublisherBlock`), change log; remove "alignment hard-coded" wording.
- Part 3: section 3.7 (multi mode, nudge X/Y that resets to 0, what is disabled, guides), a decision, change log; update the parked-ideas list (layer linking is partly covered).
- Continue the numbering after the last decision in section 4. Update the status table and the last-verified date.

## What to report after each part

1. Files changed (one line each).
2. Anything that did not match this file, and how you handled it.
3. Anything you changed or removed beyond what is listed, and anything intentionally left.
4. The test checklist for that part.
