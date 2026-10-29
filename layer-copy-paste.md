# Layer list: Layers header and right-click Copy / Paste (layer-copy-paste.md)

Two passes, in this order. Read CLAUDE.md and themes.md first (themes.md section 6b has the parked "Copy and paste layer properties" entry this spec replaces; sections 3.7 and 3.15 and decisions 40, 59 and 62 matter here).

- **Pass 1:** a "Layers" header with a circled ? glyph above the layer list, lined up with the selected layer's heading on the right.
- **Pass 2:** the right-click Copy / Paste menu on the layer list, styled from the Global tab's Context Menus values.

## How to run this

1. Do the **read-only STEP 1 audit** below and report what you found (report form at the end). Change nothing.
2. **Stop and wait.** STEP 2 starts only after the user says "go". Some audit answers (menu styles, section 3 of the audit) need a user decision first.
3. Do one pass at a time. Stop after each pass for the user to build, test and commit. Never commit.
4. After each pass: 0 errors, 0 warnings (report any new warning), `git diff --stat` shows no whole-file changes, and themes.md is updated (see "themes.md and CLAUDE.md updates").
5. Edit rules from CLAUDE.md apply: CRLF in `.cs` / `.xaml`, LF in `.md`; `[SECTION: ...]` fencing; every method gets a one-line `// Does X` summary; no unrelated fixes bundled in.

## Scope

- **Pass 1 files:** `AdvancedThemesTabControl.xaml` only (and its code-behind only if the audit finds the header needs a name referenced there).
- **Pass 2 files:** `AdvancedThemesTabControl.xaml.cs`, `AdvancedThemesTabControl.xaml`, `LayerRegistry.cs` (two small predicates), `App.xaml` (keyed menu styles, if the audit's recommendation is approved). `ThemeBuilderWindow(.xaml/.cs)` should not need changes: the clipboard lives in the tab control, which exists once per builder window.
- **Nothing is stored.** No new `LayerLayoutEntry` field, no ConfigurationSettings value, nothing in theme.json or settings.json. So the entry-field traps (CopyStyleFrom, style keys, ResolveLayout fill-ins) and the flat-property checklist do not apply. Do not add any.

## Decisions (user, 2026-10-29)

Numbering assumes the last decision in themes.md is 65 (new size scaler). Check, and renumber if it moved.

**66. Layers header (Pass 1).** A "Layers" header sits above the layer list with a ? glyph inside a circle. Hovering the glyph shows an instruction tooltip (right-click copy and paste, multi-select, the nudge boxes, the arrows). Click-to-open may be added later; build it so adding that is easy, but do not build it now. The Layers header and the selected-layer heading (`TxtLayerName`) on the right must line up; whatever sits below them may shift. The list, its arrow / add buttons and everything under the headers adjust as needed.

**67. Copy / Paste on the layer list (Pass 2).**

| Selection | Menu |
|---|---|
| No row under the mouse (empty list space) | No menu |
| One layer, nothing copied | Copy items |
| One layer, it is the copied source | Copy items (no Paste) |
| One layer, different from the source, and it can take the copied properties | Copy items + Paste |
| Several layers | Paste only (Copy items hidden, not greyed) |
| Several layers, nothing copied (or no selected layer can take it) | No menu |

- **Right-click selects.** Right-clicking a row that is not in the current selection selects it (replacing the selection) and then shows the menu for it. Right-clicking a row that is already part of a multi-selection keeps the whole selection. Keyboard menu key / Shift+F10: use the selection as it is.
- **Copy items** (single layer only): Copy X, Copy Y, Copy X / Y, Copy Width, Copy Height, Copy Width / Height, Copy Position + Size (all four), Copy Border (size, color and radius). Group related items with separators. Labels are adjustable; keep them short.
- **Paste is labeled with what it carries:** Paste X, Paste Y, Paste X / Y, Paste Width, Paste Height, Paste Width / Height, Paste Position + Size, Paste Border.
- **Raw numbers.** Copy the values exactly as the Theme Builder fields show them (the entry's X, Y, Width, Height, BorderSize, BorderColorHex, CornerRadius). Never convert for the ring rule (decisions 59 and 62) or the target's border size: a Video Preview at 320 x 240 pastes 320 x 240 onto anything, a Random Game Display included. Paste Width / Height ignores the ratio lock (like Browse-to-native-size does). Pasting Width or Height onto a custom layer does not look at its file.
- **Which layers.** Position and size (copy and paste): Shape Layers 01 to 04, custom layers, Games List, Marquee, Splash Image, Video Preview, and both Random Game Display states. Border (copy and paste): Shape, custom, Splash Image, Video Preview and both Random Game Display states, that is `LayerRegistry.HasBorderTab` plus the two Random Game Display parts. Not offered: Marquee border (its border fields belong to the banner card), Footer, Main Window Background, and every other group (Search & Buttons, Game Info). Add two small predicates to `LayerRegistry` next to `HasOutsideBorder` (suggested names `CanCopyRect(id)` and `CanCopyBorder(id)`); the audit confirms nothing existing fits.
- **Paste applies only what each target supports.** In a multi-selection a row that cannot take the clipboard is skipped silently. If the source is inside the selection, pasting onto it is harmless (the same values); do whichever is simpler.
- **Random Game Display has two states in one list row.** Copy takes from, and Paste writes to, the **part the user last clicked into on the Position page** (`_guidePartId` today, the part the guides follow). If `_guidePartId` is not one of the group's parts, use the first part (`RandomGameDisplay`). With a single Random Game Display row selected, put the state name in the item text, for example "Copy Width / Height (When a game is selected)" and "Paste X / Y (When on the splash page)". Source versus target is judged per part, so copying "When a game is selected" and pasting onto "When on the splash page" is allowed. In a multi-selection the Random Game Display row takes the paste on the same part, with no suffix. Known wonkiness, accepted for now: `_guidePartId` resets to the first part whenever another group's page is built, so the "last clicked" memory is lost when you visit another group in between. Report in the audit whether `_rgStatePartId` (the State combos' part) would be a more stable source, but use `_guidePartId` unless the user says otherwise.
- **Clipboard:** one in-memory value (what was copied, the numbers, the source layer / part and the layout size it came from). Session-only; it lives and dies with the builder window (nothing static). A new Copy replaces it. It is not cleared by a resolution switch, a theme load or Reset Layer (decided: clears on builder close only). "Is this the source" means same layer (and part) **and** same layout size, so the same layer at another size can take a paste.
- **Applying a paste** is an ordinary working edit, like every other Advanced tab edit: set the entry values, then `RaiseGuides()` (which raises `LayoutChanged`) and, for a single selection, `LoadSelectedLayerIntoFields()` so the fields refresh. Guard with `_loading`. No undo exists; do not add one. Multi mode refreshes through `RaiseGuides()` only. Nothing is saved until Save Adjustments / Save Changes.
- **The menu uses the Global tab's Context Menus values** (`Global.Menu.*`: text, hover text, background, hover background, icon, border color for the frame and the separators), so editing the Global tab's Context Menus subtab changes it live.

## STEP 1: read-only audit (both passes)

Do not change any file. Report each item under the heading with the same number. Quote code (a few lines) where the answer is a name or a value; say "not found" rather than guessing.

### A. Layers header (Pass 1)

1. The current structure of `AdvancedThemesTabControl.xaml` `[SECTION: Layer List + Fields]`: the grid that holds the list column (list plus the arrow / add button column) and `PnlLayerFields` (with `TxtLayerName` in its row 0). Say exactly what has to move for a "Layers" header to line up with `TxtLayerName`, and what the list's top edge then lines up with.
2. `TxtLayerName`'s font, size, color, weight and margin (it reads the Global Header resources). The Layers header must use identical values so the two match at every Windows scale. `TxtLayerName` is also the "N layers selected" heading in multi mode and is inside `PnlLayerFields`, which is Collapsed with nothing selected: confirm the Layers header must live outside it so it always shows.
3. The Random Game Display preview row (`PnlRandomPreview`) sits under `TxtLayerName` and pushes that layer's tabs down one row: confirm it does not affect the heading alignment.
4. Existing tooltips in the Theme Builder (for example `BtnRatioLock`, the grid boxes, `TxtBannerTextMargin`): are they default Windows tooltips or is there a keyed ToolTip style anywhere (App.xaml, the Global resources)? Report what a tooltip on this window looks like today (dark or light) and whether `ToolTipService.InitialShowDelay` / `ShowDuration` are set anywhere. A multi-line instruction tooltip needs a long ShowDuration.
5. Recommend how to draw the circled ? from the Chrome Palette only (an Ellipse or a Border with a CornerRadius, a "?" TextBlock, `ChromeBorder` / `ChromeTextMuted`, `ChromeAccent` on hover), and whether it needs to be focusable. No new hex values; a new fixed color would be a named brush in `[SECTION: Chrome Palette]`.

### B. Right-click menu (Pass 2)

1. **Main window menu styles.** In `MainWindow.xaml` `Window.Resources`, find the implicit `ContextMenu`, `MenuItem` and `Separator` styles (the ones on `Global.Menu.*`). Report, for each: the full template structure, every resource it reads, fonts and font size (fixed or Global), padding, the check / icon column, the submenu arrow, and **every Smart fullscreen hook** (`Launcher.PopupScale`, `Launcher.PopupLayoutRounding`, `Launcher.InheritedPopupScale`; section 3.18). Say plainly what would break if the same template were used in the Theme Builder window, which cannot see MainWindow's Window resources.
2. **Does anything in the Theme Builder window already get a menu style?** Confirm App.xaml has no implicit `ContextMenu` / `MenuItem` / `Separator` style (CLAUDE.md says App.xaml has no implicit styles) and that a `ContextMenu` on `LstLayers` would therefore come up in plain Windows styling.
3. **Options for the styling, with a recommendation.** Compare at least: (a) new keyed styles in App.xaml that duplicate the look and read only `Global.Menu.*`, MainWindow left untouched; (b) extract the shared template into keyed App.xaml styles and make MainWindow's implicit styles `BasedOn` them, keeping the Smart fullscreen scale hooks only in MainWindow. Give the risk to the main window's menus for each (the hamburger and every Games List menu use the implicit styles). Do not pick unilaterally: list your recommendation and wait for the user.
4. **Font size and plainness:** the builder's menu needs only plain text items and separators (no icons, check marks or submenus). Report what a minimal `MenuItem` template would need to read to match the main window's look (text, hover text, background, hover background, border, separator color, padding, font size).
5. **Selection on right-click.** Confirm how `LstLayers` (`SelectionMode="Extended"`, `ChromeListBoxStyle` / `ChromeListBoxItemStyle`) behaves on a right-click today (WPF normally does not change the selection on right-click). Propose where to put the row lookup (a `PreviewMouseRightButtonDown` handler walking up to the `ListBoxItem`, like `LstLayers_MouseDoubleClick` does) and how the menu is opened or cancelled (`ContextMenuOpening`) for: no row under the mouse, a row outside the selection, a row inside a multi-selection, the keyboard menu key.
6. **Menu build.** Since labels and visibility change per selection, recommend building the items in code each time the menu opens versus XAML items with code toggling visibility; match the project's style.
7. **Random Game Display part.** Trace the lifecycle of `_guidePartId`: every place it is set or reset (`LoadGroupIntoFields`, `PartField_GotFocus`, `ApplyPartField`, anything else), when it can hold a part of a different group, and what `_builtGroupKey` does to it. Compare with `_rgStatePartId` and say which is more stable for "last clicked into". Report what the Position page's X / Y / Width / Height boxes of each part do on focus.
8. **Clipboard lifetime.** Confirm `AdvancedThemesTabControl` is created once per `ThemeBuilderWindow` (the XAML element `AdvancedTab`) and that no static field would let a clipboard outlive the window.
9. **Eligibility.** Confirm against `LayerRegistry` that the lists in decision 67 are right: which ids have a rect (`IsPositionable`), which have `BorderSize` / `BorderColorHex` / `CornerRadius` filled by `ResolveLayout` (and their null fallbacks, `BorderSize ?? 0` on Splash / Video / Random Game Display versus `DefaultShapeBorderSize` on Shape and custom layers), that Games List and Marquee have no layer border, and that nothing else (for example a custom layer's file, the Marquee banner border) is written by Paste Border.
10. **Edit paths.** Confirm which existing handlers a paste must stay consistent with: `ApplyField`, `ApplyPartField`, `ApplyMultiNudge` (X / Y writes), `ApplyShapeBorderFields` (border writes and ranges). Confirm a paste needs no Z, visibility or ImagePath change and that `RaiseGuides()` plus `LoadSelectedLayerIntoFields()` is enough to repaint the main window and refresh the fields in both single and multi mode.
11. **Interactions.** Anything that could fire while the menu is open or because of it: `SelectionChanged` re-entrancy when the right-click selects a row, `StopPreviews()` / `ApplyLayerTabs` on selection change (the card and banner previews turn off when the layer changes), and `_tabsUnitKey` (the first tab is re-selected when the layer changes).
12. **Themes.md vs code.** Report any place where themes.md 3.7, 3.15 or the file map disagrees with the code you read (the code wins; correct themes.md at the end of the pass).

### Report form (STEP 1)

For each numbered item: the answer in a few lines, then mismatches with themes.md or this spec, then questions for the user. End with the recommended plan for Pass 1 and Pass 2 (files, what each edit does, the build order, whether any transient errors are expected) and **stop**.

## STEP 2, Pass 1: Layers header (after "go")

- Add the "Layers" header and the circled ? glyph above the list, outside `PnlLayerFields`, using the same font values and margin as `TxtLayerName` so the two line up. The list and its arrow / add buttons move down with it; fix anything under them that the shift pushes out (the window stays 717 x 885; the list must not clip at 100%, 125% or 150%).
- The glyph: a circle with a ?, Chrome Palette colors, hover highlight, a tooltip on hover (not click). Give the tooltip a long `ShowDuration` and a short `InitialShowDelay`. Keep the tooltip text in one place so it is easy to edit.
- Draft tooltip text (tune after the audit; keep it short, plain text, one line per point):
  - Right-click a layer to Copy its position, size or border, then right-click another layer (or several) to Paste.
  - Ctrl-click or Shift-click to select several layers and move them together with the Move X / Move Y boxes.
  - Use the arrows beside the list to bring a layer forward or send it backward. Double-click a shape or custom layer to rename it.
- No behavior change anywhere else. Update themes.md (see below). Stop.

### Pass 1 test checklist

1. THEME BUILDER tab: the "Layers" header and the selected layer's heading share the same top edge and text height, with: nothing selected (only the Layers header shows), one layer, a group, Random Game Display (preview row), and several layers ("N layers selected").
2. Check at Windows 100%, 125% and 150%; the Layers header and heading stay level, the list is not clipped, its scrollbar works, the arrow and add buttons sit beside the list as before, nothing below is cut off, and the buttons under the right pane (Reset Layer and the others) still show.
3. Hovering the circled ? shows the tooltip within about half a second, readable, and it stays up long enough to read; moving off hides it. The glyph highlights on hover.
4. Edit the Global tab's Fonts (Header size and color): both headings follow together.
5. Everything else on the tab behaves as before: selecting, multi-select, the arrows, Add Layer, resolution combo, guides, grid.
6. Build: 0 errors, 0 warnings.

## STEP 2, Pass 2: Copy / Paste menu (after "go")

- Add the two `LayerRegistry` predicates, the clipboard (in `[SECTION: Layer Copy and Paste]`, a small flags enum or record is fine), the right-click selection and menu code, the Copy and Paste actions, and the menu styling approved after audit item B3.
- Menu items are built (or toggled) when the menu opens, from the rules in decision 67. Do not open an empty menu.
- Keyed menu styles (if approved for App.xaml) read only `Global.Menu.*` resources and hold no Smart fullscreen scale hooks; MainWindow's own menus must look and behave exactly as before. If option (b) is approved, say so before editing and confirm the main window's menus afterwards.
- Do not touch the Games List, Marquee, Splash or any other layer's own tabs. No new Advanced tab fields, no changes to Reset Layer, Rename, multi-select nudge or the guides beyond what a paste needs to refresh.
- Update themes.md and CLAUDE.md (see below). Stop.

### Pass 2 test checklist

1. **Menu rules** (the decision 67 table), each case: right-click empty list space (no menu); one layer, nothing copied (Copy items only); copy, then right-click the same layer (no Paste); copy, then another layer (Copy items + Paste); two or more layers selected (Paste only, no Copy items); two or more selected with nothing copied (no menu).
2. **Right-click selection:** with layer A selected, right-click layer B: B becomes the only selection and the menu shows B's items. With several selected, right-click one of them: the whole selection stays. The keyboard menu key opens the menu for the current selection.
3. **Each copy / paste kind** between a Shape Layer and a custom layer and Video Preview: X, Y, X / Y, Width, Height, Width / Height, Position + Size, Border. The menu label always names what is pasted. A Video Preview 320 x 240 pastes 320 x 240 onto a shape layer and onto Random Game Display, with the ratio lock on and off.
4. **Border:** Paste Border copies size, color and radius raw (a 2px border from Video Preview onto a shape layer reads 2px in its Border tab, and X / Y are not touched). Border is not offered on Games List, Marquee, Footer or Search & Buttons / Game Info. A border paste onto a selection that includes a layer without a border skips that layer silently.
5. **Not offered:** Footer, Main Window Background, Search & Buttons and Game Info rows show no Copy or Paste.
6. **Random Game Display:** click into the "When on the splash page" part's X box, then copy from the Random Game Display row: the item text names "When on the splash page"; paste onto a shape layer works; paste onto the Random Game Display row while the other state is the last clicked part works and targets that state. Visit another group and come back: confirm and note what the last-clicked part is (known wonkiness).
7. **Multi-select paste:** select three shape / custom layers plus the source: all but the source change, the source keeps its values, a skipped row is silent, and the main window repaints at once.
8. **Live and saved:** after a paste the main window updates live, the guides follow, the fields refresh; Reset Layer restores the saved values; Save Adjustments keeps the pasted numbers.
9. **Resolution:** copy at 1280 x 720, switch to 1920 x 1080 (the clipboard stays), the same layer there is not the source and can take Paste with the 720 numbers.
10. **Lifetime:** close the builder and reopen it: no Paste until something is copied.
11. **Styling:** the menu uses the Global Context Menus colors: edit them live on the Global tab (text, hover text, background, hover background, border) and reopen the menu; the separator follows the border color. The main window's own menus (a Games List right-click, the hamburger menu) look and behave exactly as before, in windowed and Smart fullscreen.
12. **Dismissal:** Esc and clicking elsewhere close the menu; nothing changes. Right-click during an active Random Game Display card preview or banner preview behaves as selecting another layer does today (previews turn off when the layer changes).
13. Build: 0 errors, 0 warnings.

## themes.md and CLAUDE.md updates (at the end of each pass)

- **Last verified line:** add this work.
- **3.7** (Advanced tab page structure): Pass 1 adds the Layers header and glyph; Pass 2 adds a "Layer copy and paste" paragraph (the menu rules, the labels, the eligible layers, raw numbers, the Random Game Display part rule, the clipboard lifetime, no undo).
- **3.15 / 3.4** only if the menu styling touches them (keyed `Global.Menu.*` styles used by the Theme Builder window).
- **Decisions:** add 66 and 67 as written above (final wording after testing).
- **6b:** mark the "Copy and paste layer properties through a right-click menu" entry done and point to decision 67; add the parked ideas below.
- **Section 7 traps:** the Random Game Display part comes from `_guidePartId` and can reset when another group's page is built; a menu in the Theme Builder window must use the keyed Global menu styles (the implicit ones live in MainWindow's Window resources and never reach it).
- **File map:** the Advanced tab row (Layers header, copy / paste), `LayerRegistry.cs` (the two predicates), `App.xaml` (the keyed menu styles).
- **Change log:** one entry per pass.
- **CLAUDE.md:** one line under Known Gotchas about the builder's menu styles if App.xaml gets keyed menu styles; update the Theme Builder Status paragraph.

## Notes

- Why a menu and not paste buttons: the user wants to line layers up by copying exact numbers between layers (a 320 x 240 video size onto the Random Game Display, a border from one box layer onto another); a right-click menu keeps the Position page unchanged.
- The Theme Builder is a separate window at the Windows scale, so none of the Smart fullscreen popup scale (3.18) applies to this menu; do not add `Launcher.*` resources to it.
- The clipboard holds raw numbers from one layout size; pasting across sizes is allowed on purpose.
- Parked, not in this spec:
  - Click-to-open on the ? glyph (the hover tooltip is the first version).
  - Showing the copied values in the Paste label (for example "Paste Width / Height (320 x 240)").
  - Undo for pastes (there is no undo in the builder today).
  - A more stable "last clicked" Random Game Display part (see audit item B7).
  - Paste for Search & Buttons and Game Info parts (their parts never share borders, sizes or positions).
