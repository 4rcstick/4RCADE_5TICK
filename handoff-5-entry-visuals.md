# Handoff 5 of 6: Entry visuals and styling (Theme Browser)

## Kick-off prompt (paste this into Claude Code)

```
Read handoff-5-entry-visuals.md in the project folder completely, then CLAUDE.md and themes.md (sections 3.5, 3.13, 3.14, 3.22, 3.25, 3.26 and 7 at least, plus the decisions that mention the Theme Browser, the Chrome Palette and the alignment standard). Also read theme-browser-design-review-for-claude.md if it is in the project folder.

Follow the file's pass structure: do the read-only STEP 1 check first, report any mismatch and every point that needs my call, then STOP and wait for me to say "go". Only after "go" do STEP 2 (the edits). Do one pass only, stop after it, and do not commit. This pass is visual styling only: no hover overlay, no magnifier, no pop-out, no dim layer, no key or wheel blocking, and no changes to theme loading, the catalog reader or its cache, persistence, Smart fullscreen or thumbnail capture.
```

---

## 1. Goal and why

Handoffs 1 to 4 built the data and behavior: thumbnail constants and capture, navigation, the catalog reader with cache, and the working resolution buttons (one-operation load, active no-op, persistence). The THEME BROWSER list still looks like a bare diagnostic list. This pass gives every entry its final look, using only values that already exist in the Global tab and the Theme Builder, so the Browser restyles live when Global colors and fonts change.

Visual styling only. Behavior, data flow and the existing click, highlight and `RefreshActiveMarks` logic must keep working exactly as they do now.

## 2. The agreed design (settled; do not re-open)

**Entry layout.** Two columns per entry. Left: the thumbnail. Right: theme information.

**Left column (thumbnail)**
- Display box is the existing 160 x 90 DIP maximum (decision from Handoff 1; fit/center, `DownOnly`, `Fant`). Never upscaled, aspect ratio preserved.
- Centered **vertically** in the left column relative to the entry's height (the entry is as tall as its taller column). Horizontally: centered in the column (default, carried over from the Handoff 3 doc comment; a 4:3 thumbnail is narrower than 160 and centering keeps it balanced; STEP 1 reports what the code does now).
- Border: Global **Options border color and size** (the same Global.Options values the Theme Info and pop-out specs use), **square corners** (no radius, even if the Global Options radius is set). The border is drawn **outside** the image pixels so the image keeps its Handoff 1 fit size; it follows live Global changes. A border size of 0 shows no border.
- The text column must start at the **same x in every entry** regardless of that entry's thumbnail width. STEP 1 proposes how (for example a fixed-width left cell of 160 plus twice the border thickness, tracking the Global border size live) and states what a live border-size change does to the layout.

**Right column (theme information), top to bottom**
1. **Theme name:** ALL CAPS, Segoe UI **Bold**, Global **Header** color and size, left-aligned. Display text only: the stored/loaded theme name is unchanged and `LoadRequested` still receives the real name. Use an invariant upper-casing for display. Long names wrap naturally.
2. **Active marker:** a **check-circle glyph immediately after the active theme's name** (same line, directly attached, so it never wraps onto a line by itself), colored Global **Header** color, replacing the temporary ` (active)` text. Not shown for inactive themes and takes no space then. It is updated in place by `RefreshActiveMarks()` exactly like the text marker was.
3. **Resolutions row:** the label `Resolutions:` followed by the resolution buttons (section 3).
4. **Author:** `Author:` label followed inline by the value.
5. **Description:** `Description:` label followed **inline** by the description text, wrapping naturally (second and later lines wrap under the label's start or under the text, whichever is the natural result of a single wrapping text block with a label run; do not build a hanging indent unless STEP 1 shows it is trivial and you report it).

**Fonts and colors**
- Labels (`Resolutions:`, `Author:`, `Description:`): Global **Sub-Header** color and size. Weight: match the Theme Builder's own `Resolutions:` / Theme Info heading label (STEP 1 reports which style that is; reuse its weight, do not invent one).
- Values (author text, description text): Global **Standard Text** color and size.
- All of these follow live Global edits (DynamicResource / `SetResourceReference` on elements and on `Run`s). No hex colors and no new brushes except where a fallback look is copied from an existing control (see below).

**Resolution buttons.** Match the Theme Builder's existing saved-size buttons exactly: style (`ThemedActionButtonStyle`), padding, spacing between buttons, text, hover behavior, and the active styling (Background `ButtonBgHover`, Foreground `ButtonTextHover`, Bold; the `ThemedActionButtonStyle` rule that only the active button may set Background/Foreground). STEP 1 compares the Handoff 3/4 Browser buttons with `RebuildSavedSizes` button by button and reports every difference (padding, margin, min width, font size, spacing, wrap behavior). Fix the differences in the Browser by copying the values. **Do not extract a shared factory** in this pass. If the duplication is large, say so in the report and leave the decision to the user. Buttons wrap to the next line when the right column is too narrow; wrapped buttons start under the first button, not under the label (STEP 1 proposes how, for example a two-cell Grid with the label in an auto column and the buttons in a `WrapPanel` in the next).

**Fallbacks (missing data).** Use the presentation already agreed. The sources, in this order of authority:
1. the agreed Theme Browser design record (`theme-browser-design-review-for-claude.md` and `themes.md` section 3.26, if present);
2. the existing Theme Info display controls: `ThemeInfoFallbacks` has `NoImage` (`No Image`), `NoDescription` (`No description`), `NotSavedYet`; the Sub-Text color at 60% opacity on the text element for the "no value" text; the "No Image" box on its fixed Chrome Palette colors.

Expected: missing thumbnail shows the same "No Image" box look, at the 160 x 90 box size, centered like a thumbnail; empty description shows `No description` in the muted look; an empty author shows the agreed author fallback. **If the design record has no author fallback and `ThemeInfoFallbacks` has none, do not invent one: report it in STEP 1 as a point for the user's call** (and propose the least surprising option). A theme that has no saved resolutions keeps the existing text `No saved resolutions`, restyled with the same muted fallback look.

**Row dividers.** A divider between entries using the existing Global divider styling (the themed `Separator` with `Background="{DynamicResource Global.Options.Separator}"` used on the layer tabs; STEP 1 confirms the exact brush, thickness and margins used there). Between entries only: none above the first and none below the last. Entries get consistent vertical padding around the divider (see "Tunable constants").

**Instruction line.** One stationary line above the scrollable list (it does not scroll with the entries):

`Select a resolution to load a theme, and click a thumbnail to enlarge the preview.`

Default styling: Global **Sub-Text** color and size (a hint, not a heading), left-aligned with the list content, wraps if the window is narrower. (This is a styling choice; STEP 1 lists it under "choices made", the user can change it.) Note for the user: the second half of the sentence describes behavior that arrives in Handoff 6. Do not add a click handler, hand cursor or hover effect on thumbnails in this pass.

**Remove the throwaway code from Handoff 3:** the temporary diagnostics line (and the fields, counters and refresh calls that exist only to feed it), any placeholder styling, any temporary text, and the ` (active)` text marker. If a counter lives in the catalog reader or the cache and is used by something other than the diagnostics line, leave it and report it. Do not remove reader or cache behavior.

**Tunable constants.** All new layout numbers live in one clearly named constants block at the top of the Browser control (for example `RowPaddingVertical`, `ColumnGap`, `RowPaddingHorizontal`, `InstructionLineBottomGap`, `LabelToValueGap`, `ButtonRowTopGap`, `LineGap`), each with a one-line comment. Defaults (the user will fine-tune by eye after seeing it): row padding 12 above and below the content, 16 between the thumbnail column and the text column, 4 between text lines, 12 below the instruction line, left/right padding consistent with the Theme Builder tab content margin (STEP 1 reports that value).

## 3. Files involved (verify; the code wins)

Expected changed:
- `Views/ThemeBrowserControl.xaml.cs` (rows are built in code in `MakeEntryRow`/`RebuildList` since Handoff 3) and `Views/ThemeBrowserControl.xaml` (instruction line above the scroller, list host)
- `themes.md`, `CLAUDE.md`

Read for reference, do not change: `Views/AdvancedThemesTabControl.xaml(.cs)` (`RebuildSavedSizes`, Resolutions row, divider usage, label styles), `Views/ThemeThumbnailControl.xaml(.cs)` and the other Theme Info controls (fallback look; they are editing controls, do not reuse their edit glyphs and do not modify them), `App.xaml` (Chrome Palette, `ThemedActionButtonStyle`, `Global.*` styles), `Services/GlobalThemeResources.cs` (exact key names for Header, Sub-Header, Standard, Sub-Text sizes and colors, Options border color/size, Separator), `Services/ThemeThumbnailStore.cs` (the 160 x 90 constants).

Do not touch: `ThemeCatalogReader`, the cache, `ThemeBuilderWindow` load code, `MainViewModel`, `MainWindow`, `ThemeLog`, settings classes.

## 4. STEP 1: read-only check (do this first, then STOP and wait for "go")

Do not edit anything. Report briefly:

1. **Current Browser control:** how a row is built today (code structure of `MakeEntryRow`), what the root layout is (where the scroller sits, margins, padding), the exact current thumbnail element (Image sizing, how the "No Image" case is drawn now, horizontal alignment), the diagnostics line and everything that exists only for it (fields, counters, call sites), and the ` (active)` marker code (`RefreshActiveMarks` records). List what is throwaway and what is not.
2. **Global key names** you will use, confirmed in `GlobalThemeResources.cs`: Header color/size, Sub-Header color/size, Standard color/size, Sub-Text color/size, Options border color, Options border size (and its type: `Thickness` or `double`), Options separator brush. Confirm the Options border **radius** is not needed (square corners) and how to guarantee square corners on a `Border` without a radius key.
3. **Resolution button parity:** compare the Browser's buttons with `RebuildSavedSizes` buttons and the `Resolutions:` row in `AdvancedThemesTabControl.xaml` (label style, weight, padding, margin, spacing, min width, wrap behavior). List every difference and what you will copy. State whether the Builder row wraps and how.
4. **Label and value style:** which style the Theme Builder uses for its `Resolutions:` label and the Theme Info headings (`ThemeInfoHeadingStyle`, `GLLabelStyle`, `GLSubHeaderCheckBoxStyle`); weight, size source, font family. Which can be reused as-is by key (do not change shared styles) and which must be set per element.
5. **Divider styling:** the exact `Separator` usage (brush key, thickness, margins) on the layer tabs; confirm it can be used between rows unchanged.
6. **Check-circle glyph:** which icon font the builder's glyphs use (`ThemeInfoGlyphStyle`, other glyphs, the planned magnifier in Handoff 6), the exact code point you would use for a check-circle in that font (candidates in Segoe MDL2 Assets / Segoe Fluent Icons: E930 "Completed" or E73E "CheckMark"), whether it renders on a clean Windows 10 and 11 install, and how you will attach it after a wrapping name (one `TextBlock` with two `Run`s so it can never separate from the last word, or another approach) with Header size/color and correct vertical alignment next to Segoe UI Bold. Propose the glyph size relative to the Header size.
7. **Layout plan:** your proposed element structure per row (columns, how the left cell width is made constant for all rows including the border size, how the right column wraps, how label + wrapping buttons align, how the author/description inline runs wrap) and the effect of each Global change on it (font sizes larger or smaller, border size 0 and large, a long name, a 200-word description). The Browser tab's available width (window 717 x 900) and whether a horizontal scrollbar can appear (it must not).
8. **Fallbacks:** the exact agreed fallback texts and looks from the design record and from `ThemeInfoFallbacks` / the Theme Info controls (colors, opacity, box colors, text); what an empty **author** shows in the Theme Info controls today; confirm or list the gap (see section 2).
9. **Rebuild versus in-place:** confirm `RefreshActiveMarks` (highlight on the button and the glyph visibility) can keep working in place on the new row structure with the per-row records, and that a Global-resource change restyles live with no rebuild and no scroll jump. List anything in the new structure that would need a code-side `SetResourceReference` and how it is re-applied if the row is rebuilt.
10. **Docs:** the current text of `themes.md` 3.26 and decision 83's neighbors, the next free decision number (expected 84), the CLAUDE.md lines that still describe THEME BROWSER as a placeholder or bare list, and any mismatch between this file and the code.
11. **Choices made:** list every default in this file that the user may want to change (instruction-line style, horizontal thumbnail centering, paddings, glyph code point and size, label weight) so the user can approve or adjust before "go".

Then stop. Edits start only after the user says "go".

## 5. STEP 2: the edits (only after "go")

### 5.1 `ThemeBrowserControl.xaml`
- Add the instruction line above the scroller, outside the `ScrollViewer`, as a wrapping `TextBlock` with Global Sub-Text size/color via `DynamicResource`, with its bottom gap from the constants block (the XAML may reference a code-set value or a literal; whichever STEP 1 approved, with a comment). The scroller keeps `ArcadeDarkScrollViewer`, its current scroll save/restore, and `HorizontalScrollBarVisibility="Disabled"`.
- Remove the diagnostics line from the XAML.

### 5.2 `ThemeBrowserControl.xaml.cs`
- Constants block at the top (section 2, "Tunable constants").
- Rebuild `MakeEntryRow` into the two-column entry described above:
  - left cell: constant width, thumbnail (or "No Image" box) centered vertically and horizontally, wrapped in a square-cornered `Border` with Global Options border color and size (`SetResourceReference`); the image keeps its Handoff 1 fit and the existing decode path;
  - right column: name (ALL CAPS display text, Segoe UI Bold, Header) with the check-circle glyph `Run` right after it (Visibility collapsed when inactive), the Resolutions row, the inline Author, the inline Description, with the lines separated by the line-gap constant;
  - fallbacks per section 2;
  - a `Separator` (Global divider) between entries, none before the first or after the last.
- Buttons: copy the Builder's saved-size button values (per STEP 1 item 3). Keep the Click handlers, the active highlight code and the `ThemedActionButtonStyle` rule exactly as they are.
- Update the per-row record used by `RefreshActiveMarks()` for the glyph (replace the old text marker). `RefreshActiveMarks()` keeps updating in place, is still called at the end of every `RefreshCatalog()` (including the skip-rebuild path) and from the two `RefreshAfterExternal*` hooks, and still does not touch scroll or re-read zips.
- Remove the throwaway diagnostics code and the ` (active)` marker (see section 2).
- Every new `TextBlock`, `Run`, `Border`, `Separator` uses `DynamicResource` / `SetResourceReference` on Global keys so it restyles live. No hard-coded colors apart from the copied "No Image" box look.
- Each new or changed method keeps the one-line `// Does X` summary comment; keep the project's section fencing.

### 5.3 Documentation (end of pass)
- `themes.md`: rewrite 3.26's presentation part (layout, columns, fonts and colors, glyph, fallbacks, dividers, instruction line, the constants block and where to tune it); a decision (next free number) recording the final entry layout and the fixed rules (name displayed in caps only, border outside the image with square corners, check glyph replaces `(active)`, labels and values on Global Sub-Header/Standard, instruction line on Sub-Text, no shared button factory); update 3.22's remark about the Browser reusing the display controls if STEP 1 shows it is not accurate; dated change-log entry.
- `CLAUDE.md`: Theme Builder Status and any line that calls THEME BROWSER a placeholder or bare list; add that the Browser's visuals use Global resources only and that the entry layout constants live in one block.
- Keep `.md` files LF and `.cs` / `.xaml` CRLF as `CLAUDE.md` describes; confirm `git diff --stat` shows no whole-file line-ending change.

## 6. Constraints and things NOT to touch

- No hover overlay, magnifier, pop-out, dim layer, Escape handling, key or wheel blocking, hand cursor or click handler on thumbnails (Handoff 6). No Chrome Palette additions for Handoff 6.
- No change to theme loading, `LoadThemeFromBrowser`, `RunThemeSwitch`, persistence, Smart fullscreen, thumbnail capture, the catalog reader or its cache/decode path (tile decode stays at most 320 x 180 with one `DecodePixel*` dimension), filtering, or ordering.
- No per-aspect-ratio thumbnails.
- No change to the Theme Builder (its resolution controls, `RebuildSavedSizes`, labels, shared styles), the Theme Info controls, or `App.xaml` styles. If a new keyed style is truly unavoidable, stop and ask first.
- No shared button factory. No new Global or Theme Builder properties.
- Parked and out of scope: the `[New]` combo, resolution save-on-create, fixed Builder typography, Ctrl+T focus changes, F11 tab-aware behavior, an unsaved-changes tracker.
- No unrelated fixes bundled in. Never commit.

## 7. Verification

You (Claude Code):
1. `dotnet build` finishes with **0 errors and 0 warnings**. No transient errors are expected: every edit is inside the Browser control and its docs.
2. Grep: no leftover diagnostics code or text marker; no `#` hex color literals added to the Browser control (other than a copied fallback box color if STEP 1 approved it); no click handlers added on thumbnails; `LoadRequested` still raised with the real (not upper-cased) theme name.
3. `git diff --stat` shows only the files in section 3 plus the docs, with no whole-file line-ending changes.

The user (needs the running app; do not run screen-capture or input scripts yourself). Use at least: a theme with a 16:9 thumbnail and several resolutions, a theme with a 4:3 (narrower) thumbnail, a theme with no thumbnail, a theme with no author, a theme with no description, a theme with a very long name, a theme with a very long description (200 words), a theme with one resolution, a theme with many resolutions (enough to wrap), a layout-less copy of a theme (`"Layouts": []`), enough themes to scroll.

1. **Layout and alignment.** Every entry shows thumbnail left, information right. The text column starts at the same x in every entry, including the 4:3 and no-thumbnail entries. Thumbnails are vertically centered against the tallest content of their entry (short entry with a tall description, and the reverse). Row heights are consistent padding above and below.
2. **Name.** Shown in ALL CAPS, Segoe UI Bold, Header color and size, left-aligned. A theme file named in mixed case loads under its real name (hamburger, Builder Load Theme combo and `settings.json` unchanged). A long name wraps and, if it is the active theme, the check glyph stays attached to the last word.
3. **Active marker.** The check-circle glyph appears right after the active theme's name in the Header color and nowhere else; the old ` (active)` text is gone. Load another theme from the Browser, from the Builder's Load button and from the hamburger: the glyph moves each time, without a flicker and without a scroll jump. Pick a Browser-hidden theme from the hamburger: no glyph in the list.
4. **Buttons.** Side by side with the Builder's `Resolutions:` row for the same theme: same padding, height, spacing, font, hover and active look (zoom in or compare screenshots). The active resolution is highlighted and Bold; clicking it does nothing; clicking another resolution loads it exactly as in Handoff 4 (spot check: different theme, same theme other resolution, rapid clicks, a failure).
5. **Wrapping.** Many resolutions: buttons wrap to the next line and start under the first button (not under the label). Long description: wraps naturally, inline after `Description:`, with no clipping and no horizontal scrollbar. Long author: wraps without breaking the layout. A single resolution: row looks right.
6. **Labels and values.** `Resolutions:`, `Author:`, `Description:` in Sub-Header color and size (weight as the Builder's label); author and description text in Standard color and size.
7. **Thumbnails.** 16:9 shows at its fit size with the Options border (color and size) around the image and square corners; the 4:3 thumbnail keeps its aspect ratio, is not stretched, and is centered; the no-thumbnail entry shows the "No Image" box at the same box size; a very small thumbnail is not upscaled. The border sits outside the image (the image is not smaller than before).
8. **Fallbacks.** No description shows `No description` in the muted look; no author shows the agreed fallback; a layout-less theme shows `No saved resolutions` in the muted look and no buttons; none of these look like real data.
9. **Dividers.** A divider between every pair of entries using the Global divider look; none above the first or below the last; changing the divider color on the Global tab restyles it live.
10. **Live Global changes (Browser tab open, or switch tabs back and forth).** On the GLOBAL tab change: Header color and size, Sub-Header color and size, Standard color and size, Sub-Text color and size, Options border color and size, the divider color, and the button colors. Each restyles the Browser live (or on return to the tab) with no rebuild flicker and no scroll jump. Try border size 0 (no border, layout stays aligned) and a large size (text column stays aligned, no overlap, no clipping). Try a large Standard font size: nothing is clipped, rows grow, no horizontal scrollbar.
11. **Instruction line.** Shown above the list, text exactly as specified, in the Sub-Text style; it does not scroll when the list scrolls; it wraps if needed; it restyles with Global Sub-Text.
12. **Scroll preservation.** Scroll to the middle, load a theme, change Global fonts, switch to the Builder tab and back, make a hamburger swap: scroll position unchanged each time (same as Handoff 4).
13. **Diagnostics gone.** No diagnostics line; no leftover debug text anywhere in the tab.
14. **Regression pass.** Browser still lists only Both/Browser-only themes, alphabetical (case-insensitive); thumbnails still come from the cache (no visible hitch when returning to the tab); Ctrl+T and the hamburger still open the Builder on the Browser tab as in Handoff 2; closing the Builder without saving still restores as before; a theme saved, deleted or created while the Builder is open shows up or disappears on returning to the Browser tab, with its entry fully styled.
15. Windows display scaling 125% and 150%: optional, can stay deferred.

## 8. Stop conditions

Stop and report (do not push on) if any of these happen:
- A Global key you need does not exist (for example the Options border size is not published as a resource), or the Options separator brush is not what the layer tabs use, or you would need a new Global property or a new shared style to proceed.
- The design record has no author fallback and the Theme Info controls have none either (this is a STEP 1 report item; do not invent text in STEP 2 without the user's choice).
- The check-circle glyph does not render in the icon font the builder uses and no other approved approach is available.
- Achieving a constant text column, the wrapping button row, or the inline wrapping description needs a custom panel or a change outside `ThemeBrowserControl`.
- Any edit would require changing the catalog reader, the cache, the load path, `RefreshActiveMarks` semantics, or the Theme Builder / Theme Info controls.
- The diagnostics code turns out to be used by something other than the diagnostics line (report; do not remove it blindly).
- The build shows errors or warnings you cannot clear within the files in section 3.

After STEP 2, report in this form: files changed, anything that did not match these instructions, anything changed beyond them, every default constant value used, and the test checklist (section 7). Then stop. Handoff 6 (hover overlay, magnifier, pop-out, dim layer) is a separate file, written only after the user reports back.
