# Handoff 6 of 6: Thumbnail hover and pop-out preview (Theme Browser)

## Kick-off prompt (paste this into Claude Code)

```
Read handoff-6-thumbnail-preview.md in the project folder completely, then CLAUDE.md and themes.md (sections 3.4, 3.13, 3.14, 3.22, 3.26, section 7 and the decisions that mention the Theme Browser, the Chrome Palette and thumbnails, plus Theme file safety in CLAUDE.md). Also read theme-browser-design-review-for-claude.md if it is in the project folder.

Follow the file's pass structure: do the read-only STEP 1 check first, report any mismatch and every point that needs my call, then STOP and wait for me to say "go". Only after "go" do STEP 2 (the edits). Do one pass only, stop after it, and do not commit. Do not change theme loading, the catalog cache, persistence, Smart fullscreen or thumbnail capture, do not add per-aspect-ratio thumbnails, and do not make unrelated Theme Builder changes. Do not do the two parked polish items listed in the file.
```

---

## 1. Goal and why

Handoffs 1 to 5 delivered thumbnail constants and capture, navigation, the cached catalog, working resolution buttons, and the final entry visuals. The last piece is the enlarged preview. The list tiles are decoded at most 320 x 180 for speed; this pass lets the user hover a tile to see it is clickable, click it, and see the saved thumbnail at full resolution in a pop-out over a dimmed Theme Builder window.

This is the only handoff in the series that touches window-level input, so the details of blocking, dismissing and focus matter more than the visuals.

## 2. The agreed design (settled; do not re-open)

**Hover (on each thumbnail tile)**
- A black overlay at **60% opacity** over the displayed thumbnail image, with a **centered 32 x 32 magnifier-plus glyph** (Segoe MDL2 Assets, ZoomIn, `E8A3`, color `#FFE8E8E8`, off-white).
- **Hand cursor** over the thumbnail.
- **No animation**: the overlay and glyph appear and disappear instantly.
- The overlay covers exactly the **displayed image rectangle** (inside the Options border, not the border itself, not the rest of the 160 x 90 cell). It must follow the real image bounds for 16:9 and narrower thumbnails.
- An entry with no thumbnail ("No Image" box) has **no hover overlay, no hand cursor and no click behavior**.
- The tile has no other hover effect, no tooltip, no focus visual.

**Click opens the preview**
- A left-click on a thumbnail tile (press and release on the same tile) opens the pop-out for that theme.
- Keyboard cannot open it (tiles are not focusable); that is intended.

**Pop-out**
- Image shown in a **480 x 270 DIP maximum box**, aspect ratio preserved, **never upscaled** (smaller thumbnails show at their own size), fit/centered, `Fant` scaling, device-pixel snapped. The constants already exist in `ThemeThumbnailStore` (480 x 270); reuse them.
- Frame: background = Global **Options background** (`Global.Options.Bg`), border color = Global **Options border color** (`Global.Options.BorderColor`), border **fixed 4 DIP** (NOT the Global border size), **square corners** (NOT the Global radius), **8 DIP** internal padding between border and image.
- Centered horizontally and vertically in the Theme Builder window.
- Source: the **full-resolution thumbnail decoded on demand from the saved ZIP** (section 4 explains the lock rules). The tile image is not used for the pop-out.

**Dim layer**
- Black at **30% opacity** over the **entire Theme Builder window** (the whole client area: the tabs and their content, the bottom Save Adjustments / Close Panel buttons, and the outer margins). Only the Theme Builder window is dimmed; the main launcher window is untouched.

**Blocking and dismissing**
- While the preview is open **nothing underneath can be used**: no mouse clicks or hovers on controls, no mouse wheel (the Browser list does not scroll), no keyboard activation (Tab, Space, Enter, arrows, PageUp/PageDown, typing, access keys, Ctrl+T and other shortcuts) reaches the Builder underneath.
- **Any mouse click** (left, right or middle, on the dim area or on the pop-out itself) **or Escape** dismisses the preview. The input that dismissed it is **swallowed**: it must not click, load or select anything beneath, must not reopen a preview, and an Escape must not close the Theme Builder window.
- All other keys are swallowed without dismissing.
- Opening and dismissing change nothing else: **keyboard focus returns to exactly where it was**, the **Browser scroll position is unchanged**, no theme is loaded, nothing is persisted, `theme.log` gets no line.

**Visual constants.** New fixed colors become **named Chrome Palette brushes in `App.xaml`** (the project rule for fixed colors), with the alpha stated in the hex, and sizes become named constants in one block (see 5.1):
- hover overlay: black at 60% (`#99000000`)
- dim layer: black at 30% (`#4D000000`)
- glyph: `#FFE8E8E8`
- glyph box 32, pop-out border 4, pop-out padding 8

Brush names follow the existing Chrome brush naming (STEP 1 reports the convention and the proposed names). These are the only Chrome Palette additions allowed.

**Parked, do NOT implement (record in `themes.md` Parked list):**
1. Move the Browser scrollbar closer to its right border.
2. Consider a stationary Global Divider beneath the instruction line.

## 3. Defaults chosen in this handoff (STEP 1 lists them again for approval)

These were not part of the earlier design; they are the safest behavior, and the user can change any of them at STEP 1:
1. **Dismiss on window deactivation and on close.** If the Theme Builder window loses activation (the user clicks the main window or the hamburger, or switches apps) or is closing (X, Alt+F4, Close Panel is dimmed but reachable via Alt+F4), the preview is dismissed first, so no stale dim or preview can remain behind.
2. **Preview failure is silent.** If the ZIP is gone, unreadable, or has no readable thumbnail at click time, nothing opens (no dim, no dialog), and the Browser refreshes its catalog (scroll position kept) so a vanished theme drops out.
3. **Tiny thumbnails.** If a displayed image is smaller than the 32 x 32 glyph box in either direction, the dark overlay is still shown (clipped to the image) but the glyph is hidden.
4. **Oversize files.** The full decode applies the same size guard the tile decode uses for a hand-edited ZIP (STEP 1 reports the guard and mirrors it). A rejected file counts as a failure under default 2.
5. **Dismiss mouse pairing.** The overlay swallows the mouse-down, captures the mouse, and closes on the matching mouse-up (or on lost capture), so the release can never land on a tile or button underneath and reopen a preview or activate a control.
6. **No caching of the full-size image.** It is decoded per open and released on dismiss. Thumbnails are at most 480 x 270 px by construction, so this is cheap.

## 4. Critical rules (from the project and earlier handoffs)

- **ZIP lock rule:** never keep a `ZipArchive`, `FileStream` or `Stream` open on a theme ZIP. Read the thumbnail entry's bytes into a `MemoryStream`, dispose the archive and file stream, then decode (`BitmapCacheOption.OnLoad`, frozen) from the memory copy. A lingering lock blocks Save's `File.Replace` retry and Delete's `File.Move` (which does not retry).
- The Browser **never reads `Themes\Extracted` or `Themes\Thumbnails`**; the pop-out source is the saved ZIP only (the same entry lookup the catalog reader uses for the tile; do not write a second, different lookup).
- Full decode must produce a bitmap whose DIP size equals its pixel size (96 DPI) like the capture, so the 480 x 270 DIP box math in Handoff 1 holds.
- The decode may run on the UI thread (the image is small); do not add threading in this pass. Do not add disk caching.
- Chrome Palette named brushes for new fixed colors; Global values by `DynamicResource` / `SetResourceReference`; no hard-coded hex in code.
- Hover and the pop-out must not change `RefreshActiveMarks`, `SizeButton_Click`, the busy guard, the scroll save/restore, `LoadRequested` or the catalog cache.

## 5. Files involved (verify; the code wins)

Expected changed:
- `App.xaml`: the three named Chrome brushes (Chrome Palette section, with the section's comment style).
- `Views/ThemeBrowserControl.xaml.cs` (and `.xaml` only if needed): the hover overlay on each tile, hand cursor, left-click detection, and a new event `PreviewRequested(string themeName)` (default; STEP 1 may propose passing the entry). No Theme Info control is modified.
- New `Views/ThemePreviewOverlay.xaml` + `.xaml.cs` (default): one self-contained user control holding the dim layer, the pop-out frame, the image, the open/close API, the input blocking and the focus restore. STEP 1 may propose a different shape (for example putting it in `ThemeBuilderWindow`), but it must stay small and isolated.
- `ThemeBuilderWindow.xaml` / `.xaml.cs`: host the overlay above everything (so it covers the outer 15 margin, tabs and bottom buttons), wire `ThemeBrowser.PreviewRequested`, dismiss on `Deactivated` / `Closing` (default 1). Minimal changes only.
- The catalog reader file (`Services/ThemeCatalogReader.cs`) **only to add** one new method for the on-demand full-size decode, sharing the existing thumbnail-entry lookup and size guard; the existing methods, cache and tile decode stay byte-for-byte as they are. If STEP 1 proposes a small separate helper instead, that is acceptable.
- `themes.md`, `CLAUDE.md`.

Read for reference, do not change: `ThemeThumbnailControl` and the other Theme Info controls, `ThemeThumbnailStore` (only reuse its constants; add none unless STEP 1 shows one is needed), `MainWindow.xaml.cs`, `MainViewModel`, `GlobalThemeResources`, the Builder tabs.

## 6. STEP 1: read-only check (do this first, then STOP and wait for "go")

Do not edit anything. Report briefly:

1. **Window structure:** the root layout of `ThemeBuilderWindow.xaml` (what holds the outer Grid with `Margin=15`, the TabControl row and the bottom button row) and the least invasive way to host a full-window overlay that covers all of it including the margin. Existing window-level handlers: `PreviewKeyDown` / `KeyDown`, shortcuts, `Deactivated`, `Closing`, `IsCancel` / `IsDefault` buttons (does Escape currently close the window? How?), and anything that reads Escape or Tab at window level.
2. **Tile structure after Handoff 5:** how the thumbnail cell is built (the `ThemeThumbnailControl` instance, its wrapper `Border`, the shared size group) and how to attach the hover overlay **without modifying the control**: how to get the displayed image rectangle (does the control expose the rendered image bounds, or must the overlay be sized from the bitmap size and the Handoff 1 fit rules?), where the overlay sits relative to the Options border, how hover/cursor/click will be detected and how the "No Image" entry is excluded.
3. **Catalog reader:** the exact thumbnail-entry lookup (which entry name or `Info.ThumbnailPath`, case rules), the tile decode (size guard, byte copy, decode options, 96 DPI handling), and the proposed signature and placement of the new full-size decode method. Confirm it can reuse the lookup and guard without changing the existing methods or cache, and that no stream stays open.
4. **Where lock-sensitive code runs today:** the other code that reads a theme ZIP's thumbnail (for example Save Changes recovering `assets/theme-thumbnail.png`) and whether one of those helpers should be reused instead of a new method. Confirm Save's `File.Replace` retry and Delete's `File.Move` are the operations the lock rule protects.
5. **Input blocking plan:** the exact mechanism proposed for mouse (overlay hit-testable, capture, closing on the mouse-up), keyboard (window-level `PreviewKeyDown` / `PreviewKeyUp` / `PreviewTextInput` handled while open, access keys, Tab navigation, the Escape key-up), wheel (`PreviewMouseWheel`), plus context menus, drag and touch/stylus if the window handles them. Which Builder or MainWindow shortcuts can still fire (Ctrl+T, F11) while the Builder has focus, and how each is blocked. How focus is moved to the overlay and restored (what `Keyboard.FocusedElement` can be on the Browser tab, and what restore does if that element is gone).
6. **Deactivation and closing:** how the Builder window is deactivated/closed today (Owner = MainWindow, modeless), what `Deactivated` / `Closing` already do, and confirmation that dismissing the preview there is safe (default 1).
7. **Chrome Palette:** the brush naming convention in `App.xaml`, the exact proposed names and values, and the Segoe MDL2 Assets reference used by existing glyphs (confirm `E8A3` is the ZoomIn glyph and renders on clean Windows 10 and 11). Where the 480 x 270 constants live and their names.
8. **Pop-out layout plan:** the proposed element structure for the dim layer and frame (outer dim Border, centered frame Border with `BorderThickness 4`, `Padding 8`, Options background and border brushes, the image with a 480 x 270 maximum, `Uniform` + `DownOnly`, `UseLayoutRounding`), the effect of an image smaller than the box, and confirmation that nothing here reads the Global Options border size or radius.
9. **Risks to confirm:** the hover state after the preview closes (WPF only refreshes `IsMouseOver` on mouse movement or a render pass: is a stuck hover overlay possible after a dismiss, and what is the fix); the open-then-reopen trap from the dismissing click's release; the double-open from rapid clicks; any chance the overlay steals focus into a state the Builder cannot recover from.
10. **Docs:** the current text of `themes.md` 3.26 and 3.14 (Chrome Palette list) and the Parked list; the next free decision number (expected 85); the CLAUDE.md lines that still describe the Browser as pending a hover/pop-out; any mismatch between this file and the code.
11. **Choices made:** list every default in section 3 and every choice in the plan that the user may want to change.

Then stop. Edits start only after the user says "go".

## 7. STEP 2: the edits (only after "go")

Expected order (so the build stays clean): App.xaml brushes, the reader method, the overlay control, the Browser changes, the Builder window wiring, the docs. If a transient build error appears between steps (for example the window referencing the overlay control before it exists), finish the sequence before reporting it.

### 7.1 Constants and brushes
- `App.xaml` Chrome Palette: three named brushes (hover overlay `#99000000`, dim layer `#4D000000`, glyph `#FFE8E8E8`), each with a one-line comment saying where it is used.
- One constants block (in the overlay control or the Browser control, per STEP 1) for: glyph box 32, pop-out border 4, pop-out padding 8. The 480 x 270 box uses the existing `ThemeThumbnailStore` constants.

### 7.2 Full-size decode
- The new method returns a frozen 96 DPI `BitmapSource` or null. It copies the entry bytes to a `MemoryStream`, closes the archive and file stream before decoding, uses `BitmapCacheOption.OnLoad`, applies the size guard from STEP 1, and never throws to the caller (null on any failure). It does not touch the catalog cache.

### 7.3 Overlay control (`ThemePreviewOverlay`)
- Public API (names per STEP 1): `Show(BitmapSource image)`, `Hide()` (also used by deactivation/closing), `bool IsOpen`.
- Visuals as in section 2. Collapsed when not open and takes no input then.
- Input blocking and dismissal exactly as in section 2 and section 3, default 5 (mouse-down swallowed and captured, closed on mouse-up or lost capture, any button; Escape closes; Escape key-up swallowed; all other keys, text, access keys and wheel swallowed while open). The handlers are attached to the window only while the overlay is open and detached on close.
- Focus: remember `Keyboard.FocusedElement` on open, focus the overlay, restore the element on close if it is still part of the live visual tree (otherwise leave focus where WPF puts it), without scrolling the Browser list.
- Releases the image reference on `Hide()`.

### 7.4 Browser control
- Per tile (only entries that have a thumbnail): the hover overlay (hover brush, glyph `Run`/`TextBlock` with Segoe MDL2 Assets in the glyph brush, size per the constants block, collapsed by default, shown on mouse enter, hidden on mouse leave; hidden when the glyph box does not fit, default 3), `Cursor = Hand` on the tile, and click detection (press and release on the same tile, left button) raising `PreviewRequested`.
- Must not change: the cache, `RefreshActiveMarks`, `SizeButton_Click`, the busy guard, `RefreshCatalog`, scroll save/restore, or the active highlight logic.
- After the preview closes, any hover overlay that should no longer show must be cleared (STEP 1 item 9).

### 7.5 Theme Builder window
- Host the overlay above everything, wire `ThemeBrowser.PreviewRequested` to: decode the full-size image (7.2), `Show` it, or on null do nothing and call `ThemeBrowser.RefreshCatalog()` (default 2). Call `Hide()` from `Deactivated` and `Closing` (default 1). Nothing else in the window changes.

### 7.6 Documentation (end of pass)
- `themes.md`: rewrite 3.26's pending hover/pop-out text to the built behavior (hover, click, pop-out geometry and colors, dim layer, input blocking and dismissal rules, focus restore, on-demand full decode and the lock rule, the failure behavior, the defaults in section 3); add the three brushes to the 3.14 Chrome Palette list; add a decision (next free number) recording that the preview is a window-hosted overlay with fixed Chrome colors, fixed 4 DIP square frame, no animation, no cache, and that the dismissing input is swallowed; add the two parked polish items to the Parked list; mark the Theme Browser feature set complete in the status lines; dated change-log entry.
- `CLAUDE.md`: the Theme Browser status lines (hover/pop-out built; remaining: only the parked polish items), the Theme file safety lock rule covering the full-size decode, and the "Remaining" list in the status paragraph (the Browser is no longer outstanding).
- Keep `.md` files LF and `.cs` / `.xaml` CRLF as `CLAUDE.md` describes; confirm `git diff --stat` shows no whole-file line-ending change.

## 8. Constraints and things NOT to touch

- No change to theme loading (`LoadThemeFromBrowser`, `RunThemeSwitch`, `LoadTheme`), persistence, Smart fullscreen, thumbnail capture, the catalog cache or the tile decode path, filtering, ordering, or the Handoff 5 layout and styling (including the constants block, except to add the hover/click layer).
- No per-aspect-ratio thumbnails; no multi-size thumbnail files.
- No modification of `ThemeThumbnailControl` or other Theme Info controls, no change to shared styles, `App.xaml` styles, or the Theme Builder tabs. The only `App.xaml` change is the three brushes.
- No new Global or Theme Builder properties; no use of Global Options border size or radius for the pop-out.
- No animation of any kind (no fades, no transitions).
- No dismissal button or caption in the pop-out; no theme name or text in it.
- Do not implement the parked polish items: Browser scrollbar position, a stationary divider under the instruction line.
- Also still parked and out of scope: the `[New]` combo, resolution save-on-create, fixed Builder typography, Ctrl+T focus improvements, F11 tab-aware behavior, an unsaved-changes tracker, moving cold catalog reads to a background thread.
- No unrelated fixes bundled in. Never commit.

## 9. Verification

You (Claude Code):
1. `dotnet build` finishes with **0 errors and 0 warnings**.
2. Grep: no `ZipFile.Open*` / `ZipArchive` / `FileStream` held beyond the method that reads the bytes in the new decode; no `File.*` access to `Extracted` or `Thumbnails` from the Browser or overlay; no hex color literal in code for the overlay colors; no `Storyboard`, `DoubleAnimation` or transition in the new code; no change to `LoadRequested`.
3. `git diff --stat` shows only the files in section 5 plus the docs, with no whole-file line-ending changes.

The user (cannot be done without running the app; do not run screen-capture or input scripts yourself). Have: a theme with a normal 16:9 thumbnail; one with a 4:3 (narrower) thumbnail; one with a small thumbnail (so it is not upscaled; a copy with a 200 x 112 image is fine); one with no thumbnail; a copy of a theme whose zip is deleted after the list is showing; a copy with a corrupt thumbnail entry; enough themes to scroll.

1. **Hover.** Over a thumbnail: black 60% overlay exactly over the image (not the border, not the rest of the cell), centered 32 x 32 off-white magnifier-plus glyph, hand cursor; instant on and off, no fade. Check the 16:9 and the 4:3 thumbnail (overlay hugs the image). The no-thumbnail entry: no overlay, arrow cursor, clicking does nothing. Hover a tile, then scroll the list with the wheel and move the mouse: the overlay follows the pointer correctly.
2. **Open.** Click a 16:9 thumbnail: the Theme Builder window dims at 30% black across the whole window (tabs, content, bottom buttons and the outer margin); the pop-out appears centered with a 4 DIP square border in the Global Options border color, 8 DIP padding and the Global Options background; the image is full resolution, at 480 x 270 for a max-size thumbnail, with no blur and not upscaled (check the small thumbnail: it stays small in the frame). The 4:3 thumbnail keeps its aspect ratio. The main launcher window is not dimmed.
3. **Live Global styling.** Change Global Options background, border color, border size and radius on the GLOBAL tab, then open a preview: colors follow; the frame stays 4 DIP and square regardless of the Global border size and radius.
4. **Blocking.** With the preview open, try: clicking tab headers, resolution buttons, Save Adjustments, Close Panel, scrolling with the wheel over the list and over the dim area, Tab / Shift+Tab, Space and Enter with a button focused, arrow keys, PageUp/PageDown, typing, Alt+letter access keys, Ctrl+T. Nothing underneath reacts; the Browser scroll position does not move; no theme loads.
5. **Dismissal.** Dismiss with a left, right and middle click on the pop-out, on the dim area, and over a spot where a thumbnail, a resolution button and a tab header sit underneath. Each click only closes the preview: no theme loads, no tab changes, no second preview opens (especially when clicking directly over another thumbnail). Escape closes it and the Theme Builder window stays open; pressing Escape again afterwards behaves as it did before this handoff. Other keys do not dismiss.
6. **Focus.** Put focus on a text box on another tab, switch to the Browser tab without clicking into the list, open a preview, dismiss it: the focus and caret are where they were (type a character to confirm). Repeat with focus on a button and on the tab header.
7. **Scroll preservation.** Scroll to the middle and bottom of a long list, open and dismiss a preview from each position: the scroll position is identical and the thumbnails are not reloaded (no hitch).
8. **Loading unaffected.** After dismissing, resolution buttons behave exactly as in Handoff 4 (load another theme, other resolution of the same theme, active button inert, rapid clicks, failure messages). The active marker and highlight still move correctly after hamburger swaps.
9. **No lingering locks.** Open and dismiss the preview of a theme, then in Explorer rename the zip and rename it back; then in the Builder do Save Changes on that same theme and then Delete it (use a copy): both succeed with no "in use" message. Repeat 20 quick open/close cycles on one thumbnail: no growth in memory you can see in Task Manager beyond normal, and still no locks.
10. **Failures.** Delete a listed theme's zip in Explorer, then click its thumbnail: nothing opens, no dim, no dialog, and the entry drops out of the list with the scroll position kept. A zip with a corrupt thumbnail entry: nothing opens, no crash. A hand-made zip with a very large thumbnail (for example 4000 x 2250) behaves per the size guard STEP 1 reported (never freezes the app).
11. **Deactivation and closing.** With the preview open, click the main launcher window, then use the hamburger to switch theme: the preview dismisses on deactivation, the theme switch works, and the Browser marks update. Alt+Tab away and back: no stuck dim. Close the Builder with Alt+F4 while a preview is open: it closes cleanly; reopen it: no dim, no pop-out, no stuck focus.
12. **Rapid clicks.** Double-click and repeated clicks on a thumbnail: one pop-out at a time; no flicker loop; dismissing still works.
13. **Smart fullscreen and navigation.** With Smart fullscreen on (and plain fullscreen), open and dismiss previews and load a theme: the main window is unaffected by previews. Ctrl+T / hamburger navigation to the Browser tab works as in Handoff 2 after a dismiss.
14. **Regression pass.** Entry visuals from Handoff 5 are unchanged (layout, glyph, dividers, instruction line, fallbacks); the diagnostics line is still gone; live Global restyling still works; closing the Builder without saving still restores as before.
15. Windows display scaling 125% and 150%: optional, can stay deferred.

## 10. Stop conditions

Stop and report (do not push on) if any of these happen:
- The overlay cannot cover the whole Theme Builder window without restructuring more than the root of `ThemeBuilderWindow.xaml` (for example the TabControl or the bottom buttons would need to move).
- Blocking keyboard input would need changes to Builder tabs, shared styles or `MainWindow` shortcuts, or a shortcut can still fire while the preview is open and cannot be blocked from the Builder window.
- The hover overlay cannot be sized to the displayed image without modifying `ThemeThumbnailControl` or the Handoff 3/5 decode or cache code.
- The only way to decode full-size changes the existing catalog reader behavior, keeps a stream open, or needs the extracted folder.
- Escape closes the Theme Builder window despite the swallow, or the dismissing click's release reaches a control beneath.
- A hover overlay can get stuck visible after dismiss and no clean fix exists inside the Browser control.
- Any edit would need a change to loading, persistence, Smart fullscreen, thumbnail capture, the catalog cache, or an `App.xaml` change other than the three brushes.
- The build shows errors or warnings you cannot clear within the files in section 5.

After STEP 2, report in this form: files changed, anything that did not match these instructions, anything changed beyond them, every constant and brush value used, and the test checklist (section 9). Then stop. This is the last handoff of the Theme Browser series; the two parked polish items (Browser scrollbar position, stationary divider under the instruction line) are a separate pass, written only if the user asks.
