# Handoff: Scrollbars row with per-area overrides (pass 2 of 2)

Read `CLAUDE.md` first, then `themes.md` sections 3.2, 3.6, 3.7 and 3.15, section 6 open question 3, and decisions 66, 67 and 77. This handoff is self-contained, but those are the rules and the spec you work under.

## Goal and why

The scrollbar look is global today: one set of values on the Global tab's SCROLLBARS subtab (`Global.Scroll.*`) styles every scrollbar in the app. The user wants the Games List scrollbar and the Game Info scrollbar to be able to **override** the global look separately, per resolution, without touching the global values.

This is **pass 2 of 2**. Pass 1 (a blue `|` marker on renamable layers and a separator above the Main Window Background, decision 77) is built, tested and committed.

### What the user wants (behavior)

- A new **pinned "Scrollbars" row** at the very bottom of the Advanced tab's layer list. List order, top to bottom: movable layers, separator, Main Window Background, Scrollbars. Both pinned rows cannot be moved, hidden or renamed. The separator stays above the first pinned row (the Main Window Background).
- The Scrollbars row is **not a normal layer**: no position, no size, nothing drawn on the canvas, no Position tab, no Hide checkbox, no alignment guides, no right-click Copy / Paste, no rename marker.
- Its page has **two sections: Games List scrollbar and Game Info scrollbar**, each with its own checkbox **"Use global scrollbar"**, **checked by default**. While checked, the section's fields are grayed out. Unchecking enables the fields, **pre-filled with the current global values** so the user starts from what they see.
- The fields are the same set as the Global tab's SCROLLBARS subtab: **Width** (2 to 40, decimals accepted), **Thumb** color at Resting, Hover and Dragging, **Track** color at Resting and Hover.
- Values are stored **per resolution on layer entries** (CLAUDE.md: new styling goes on layer entries, not on `ConfigurationSettings`).
- **Reset Layer** on the Scrollbars row puts both sections back to "use global".
- Default decisions (the user can change them; flag them in your report): re-checking "Use global scrollbar" **keeps** the stored custom values (they are just not used), and only Reset Layer clears them.

## How to work (follow the CLAUDE.md pass structure)

- **STEP 1 is a read-only audit.** Change nothing, create no files and run no scripts. Report, then **stop and wait**.
- **STEP 2 (implementation) starts only after the user says "go".** Expect to adjust STEP 2 from what the audit finds; the user will answer the open questions in the "go" message.
- Never commit. The user builds, tests and commits.

## Current behavior (from themes.md; check against the code)

- One scrollbar template, `ArcadeDarkScrollBar`, in App.xaml, read through `ArcadeDarkScrollViewer` and `ArcadeDarkScrollViewerThemed` (BasedOn it, adds `CanContentScroll="True"`). Values are the Global resources `Global.Scroll.*`, typed, published by `GlobalThemeResources.Apply` from the flat `ConfigurationSettings` properties `ScrollbarWidth`, `ScrollThumbColor`, `ScrollTrackColor`, `ScrollThumbHoverColor`, `ScrollTrackHoverColor`, `ScrollThumbDragColor`. `Global.Scroll.HitWidth` is the width, at least 8, used as the bar's hit width.
- The Games List's scrollbar comes from an implicit ScrollViewer style in GameTree's resources (BasedOn `ArcadeDarkScrollViewerThemed`). The Game Info layer control (`GameInfoLayerControl`) uses `ArcadeDarkScrollViewer`. The Theme Builder, Options, Sorting and the Chrome Palette lists use the same styles.
- The template has fixes that must not regress: `MinWidth="0"` on the bar style (the Windows theme's default sets 17), the transparent hit area of `Global.Scroll.HitWidth`, the thumb's fixed 4px corner radius (clamped to a pill at narrow widths). Only vertical bars exist.
- Global resources are **typed** (a wrong type fails silently). Readers use `{DynamicResource Global...}`.
- The Advanced tab's list is built from layer entries; "pinned" today means `IsBackground` (`IsPositionable == false`) via `IsPinnedRow`, which drives the sort, the move bound ("the row before the first pinned row"), the multi-move filter, the hidden Position tab and the guides. The Main Window Background is the only such layer today.
- themes.md section 6 open question 3 assumed the Games List never overrides the global scrollbar. This pass reverses that.

## STEP 1: read-only audit (report each item, change nothing)

1. **Scrollbar styles today.** The template, the two ScrollViewer styles, the `Global.Scroll.*` keys (names, types, how the template reads each one), how `HitWidth` is computed, and `GlobalThemeResources`' snapshot / restore for the Global area.
2. **The two target scroll areas.** Where the Games List and Game Info ScrollViewers live, and what else inside or under each one has a scrollbar (context menus, tooltips, popups, nested lists, the folder popups). Say which of those would or would not follow an override, and what should.
3. **Override mechanism.** Propose how an area uses its override or the global values, with trade-offs, and recommend one. One candidate to evaluate: resource keys scoped to the area's own element (the template reads `{DynamicResource Global.Scroll...}`, so a dictionary on the Games List host or the Game Info control overrides it for that subtree only, with the same keys and types), which would reuse the existing template and avoid a second copy. Check whether that actually works with the template's lookups, with popups, and with the typed-resource rule. Report any option that would duplicate the template (drift risk) and why you reject it.
4. **A second pinned, non-positionable layer.** Propose how to represent the Scrollbars row so it fits the layer-entry path (a registry layer with a never-renamed Id and its own fields on the per-resolution entry is the likely shape). Then check every place that treats `IsBackground` / `IsPositionable == false` as "the Main Window Background" or that a new entry could affect: the sort (`GetAscendingOrder`), `IsPinnedRow`, the move bound, the multi-move filter, the Position tab hiding, the guides, the `ApplyLayout` positioning loop and `PlaceOutsideBorder`, `MoveSelectedLayer`'s Z assignment (the new row must not disturb other layers' Z), Reset Layer, Reset Layout, Restore Defaults and `DefaultLayerSets.json`, Copy / Paste, `BuildDefaultLayout` / `ResolveLayout`, `CloneLayouts`, the preset copy, `LayoutScaler`, Smart fullscreen, and theme save and load. For each, say safe or needs a change.
5. **Old files.** How a theme, a template or `settings.json` without Scrollbars entries loads (`ResolveLayout` gap filling): confirm it comes up as "use global" with no change in appearance.
6. **Entry fields.** Propose the field names and types. Check the CLAUDE.md list for a new `LayerLayoutEntry` field (`JsonIgnore(WhenWritingNull)`, `CopyStyleFrom`, defaults in both `BuildDefaultLayout` and `ResolveLayout` through one `Fill...` helper, the matching style key in `ApplyLayout`, forwarders re-raised from the `Refresh...Bindings()` method and `RefreshThemeBindings()`) and say what each needs here. State how "use global" is stored so an old file and a new one agree.
7. **Live apply and revert.** How a change on the new page repaints the launcher live, and how closing the Theme Builder without saving reverts it. Layout snapshots cover layouts only (CLAUDE.md), so say what must be re-applied if the override is applied outside the layouts.
8. **The page.** How tabs and pages are added for a layer (`GetLayerTabs`, `AllLayerTabs`, `ApplyLayerTabs`, the XAML TabItems) and how a non-positionable layer hides Position. Judge whether two stacked sections (checkbox, then Width, Thumb and Track columns) fit the right pane without scrolling at the fixed window size (717 x 885), and if not, propose two tabs (Games List, Game Info) instead. Report whether the Global tab's SCROLLBARS field builder (`BuildFieldTables` in `GlobalThemesTabControl`) can be reused for the page without changing the Global tab's behavior; if not, what the smallest duplication is. Say how the gray-out and the pre-fill on uncheck would work.
9. **Reset and sizes.** What Reset Layer, Reset Layout and Restore Defaults ("Layout only" and "Everything") should do for the new row, how a new size copies the values, and whether the width should scale on a ratio-mismatched size (`LayoutScaler.ScaleStyleSizes`); recommend.
10. **Layer list.** Confirm the pinned block of two rows keeps the separator above the first pinned row, that the marker (decision 77) does not show on the Scrollbars row, and what `IsPinnedRow` and `StartsPinnedBlock` need. Say what the sort order is for the two pinned rows (Main Window Background above Scrollbars).
11. **Help text.** `LayersHelpText` has a point naming the rows that have no Copy / Paste; the Scrollbars row must join it. Say what else in the help text changes.
12. **Spec vs code.** Compare themes.md 3.2, 3.6, 3.7, 3.15, section 6 question 3 and the last decision number (77) with the code. Report mismatches (the code wins).
13. **Impact summary.** The files STEP 2 would change, the planned edits per file, the risks and any open questions.

Report in this order: findings per item, mismatches, proposed STEP 2 edit list, risks, open questions. **Then stop and wait for "go".**

## STEP 2: changes (only after "go"; refine from the audit and the user's answers)

- **A. The row.** Add the pinned Scrollbars row as agreed in the audit, below the Main Window Background, with the behavior in the Goal section.
- **B. The page.** Two sections (or two tabs if the audit says so), each with "Use global scrollbar" checked by default, grayed fields while checked, pre-fill with the current Global values on uncheck, values kept when re-checked.
- **C. Storage.** Per-resolution fields on the layer entry, with every item in the CLAUDE.md field checklist.
- **D. Applying it.** The Games List scrollbar and the Game Info scrollbar use their own override when "use global" is off, and the global values otherwise. Live repaint while editing; revert when the Theme Builder closes without saving.
- **E. Resets and sizes.** Reset Layer puts both sections back to "use global" and clears the custom values; Reset Layout, Restore Defaults and new sizes as decided in the audit.
- **F. Help text.** Add the Scrollbars row to the point listing rows without Copy / Paste (edit only `LayersHelpText`).
- **G. themes.md.** A new section for scrollbar overrides, 3.2 (the layer list), 3.6 and 3.15 (the Global values are now the defaults that areas may override), section 6 question 3 marked resolved, a new decision (next number after 77) and a dated change log entry. Correct any mismatch the audit found. Do not edit CLAUDE.md; say in the report whether it needs a line.

## Constraints and things NOT to touch

- The Global tab's behavior and the meaning of `Global.Scroll.*`: unchanged. Changing a global scrollbar value must still change every area that uses global.
- Every scrollbar outside the Games List and Game Info (the Theme Builder, Options, Sorting, context menus, the Chrome Palette lists) keeps following the Global values only.
- The `ArcadeDarkScrollBar` geometry and its fixes (`MinWidth="0"`, `HitWidth`, the 4px thumb radius) stay as they are.
- New styling goes on layer entries, not `ConfigurationSettings`; no legacy migration code. Do not add flat properties.
- Typed resources only (`double`, `Brush`, `Thickness`, `CornerRadius`). No hex colors inside a style (use the Chrome Palette for any fixed color). No implicit (keyless) styles in App.xaml. No `RelativeSource AncestorType` bindings for values in row templates.
- Do not change the marker, the rename rule or Copy / Paste behavior, and do not build anything for another pinned row.
- Smart fullscreen and popup scaling: do not touch.
- Comment fencing and style: `// [SECTION: ...]` / `// [END SECTION: ...]` in C#, `<!-- [SECTION: ...] -->` in XAML, and a one-line `// Does X` summary on every method you add or change. Check every `x:Name` and handler you add against the code-behind.
- Line endings: `.cs` and `.xaml` CRLF, `.md` LF. Use the Edit tool; no whole-file rewrites. After edits run `git diff --stat` and confirm no file shows a whole-file change.
- Never commit.

## Expected errors

STEP 1 changes nothing, so no errors. STEP 2 is a multi-file change. Order the edits so the end state builds with 0 errors; if a transient error is unavoidable (for example a registry Id added before its handlers), say so in the report.

## How to verify (STEP 2)

Build: 0 errors, and report any new warning. Then the user runs and checks:

1. **The row:** the list ends with the separator, Main Window Background, Scrollbars. The Scrollbars row has no `|` marker, no Position tab, no Hide checkbox and no guides, and the Up / Down arrows do nothing for it. The Main Window Background still cannot move; movable layers still cannot pass the separator.
2. **The page:** two sections, each with "Use global scrollbar" checked and its fields grayed. Unchecking enables the fields, pre-filled with the current global values.
3. **Games List override:** change Width and the colors and the Games List scrollbar repaints live; Game Info and every other window are unchanged. Repeat for Game Info.
4. **Re-check:** the area returns to the global look; unchecking again shows the previous custom values.
5. **Global still wins when checked:** change a value on the Global tab's SCROLLBARS subtab. Areas that use global follow it; areas with an override do not.
6. **Reset Layer** on the Scrollbars row puts both sections back to "use global".
7. **Persistence and sizes:** Save Adjustments, close and reopen the Theme Builder, restart the launcher: overrides persist. Switch resolution: each size has its own values. Add a new size: values copy over (and scale or not, as decided). Save the theme and reload it.
8. **Revert:** change an override, close the Theme Builder without saving: the launcher goes back to the saved look.
9. **Old files:** load a theme saved before this pass: both areas use global and nothing looks different.
10. **Nothing else changed:** the Theme Builder's own scrollbars, Options, Sorting and context menus follow the Global values; right-click Copy / Paste, multi-select, rename and the pipe marker work as before; the `?` tooltip mentions the Scrollbars row; the window is still 717 x 885.
11. **Both 1280x720 and 1920x1080** behave the same.

## Report format (STEP 2)

Files changed; anything that did not match these instructions; anything changed beyond them; the test checklist above. Then stop, so the user can build, test and commit.
