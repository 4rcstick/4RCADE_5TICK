# HANDOFF: Theme Builder Tab Polish — Visual Cleanup Pass (single pass)

**Type:** gated implementation. UI presentation only (control positions, widths, labels, spacing).
**Order of work:** Step 1 focused re-check (read-only) → **STOP for the user's explicit "GO"** → Step 2 implementation → Step 3 one final clean rebuild, static checks and report → **STOP**.
**Never commit or push.** Do **not** edit `themes.md` or `CLAUDE.md` (the user updates documentation after visual acceptance).

---

## 1. Goal and why

The first tab-polish implementation is built and was visually inspected. This pass applies the corrections the user found. Everything is in `Views/AdvancedThemesTabControl.xaml` (**X:**) and, only if needed, `Views/AdvancedThemesTabControl.xaml.cs` (**CS:**). The **current code is the authority** for names and positions; find things by `x:Name` and text, not by remembered line numbers.

**Source of truth for the corrections:** `Theme_Builder_Visual_Inspection_-_Cleanup_Planning_Spec_Revised.md`, in the project folder root. Use that **exact filename**; do not rename, normalize or search for variants. If it is not found under that exact name, stop and tell the user where you looked. Read all of it. This handoff adds the technical interpretation, assumptions and checks; where the two differ, **this handoff's section 2 notes are binding** and everything else follows the spec.

The earlier spec and handoff files (`Theme_Builder_Tab_Polish_-_Claude_Chat_Planning_Specification.md`, `tab-polish-implementation-handoff.md`) may be read for context only. Where this cleanup disagrees with them, this cleanup wins (listed in section 2).

## 2. Decisions and interpretations (binding)

**Earlier decisions this pass supersedes:**
1. **Random Game Display Text:** the order is now Font/Alignment → Message → Max text size → Color → compact Drop Shadow. Its Drop Shadow is **no longer an exception**; it uses the compact recipe. That makes **16 compact shadow groups**. The remaining exceptions that keep a standalone toggle are only the **Search Label** shadow and the **Marquee Shadow tab**.
2. **Random Game Display Content:** State moves **below** Background color and is relabeled.
3. **Marquee tab name:** `SPLASH LOGO` becomes `LOGO`.
4. **Local Border rows (Text Marquee, Play Button):** the 110 / 22 / 50 / 22 / 60 re-grid is replaced by the Blur/Depth column alignment below. The **50 px Size / 60 px Radius widths stay**.

**Interpretations of ambiguous points (flagged to the user; implement as written, report any difficulty):**
- **Anchor concept:** in each tab the compact Drop Shadow row's **Blur** and **Depth** fields define two column positions (Blur's left/right edge, Depth's left edge). Read their actual x positions and widths from the live grid and align to those. Do not invent numbers. Do not widen hex fields; adjust only local grid/column sizing.
- **A 60 px field cannot sit in a 52 px column:** a field with an explicit Width larger than its grid slot is clipped to the slot. For the Radius fields (60 px), use a row-local grid, a `Grid.ColumnSpan`, or a wider column on that row only. Check it is not clipped.
- **Search Alignment combo width (not specified):** its left edge aligns with the Background group's left edge; make its **right edge align with the Background group's right edge** (the Background hex field's right edge) so both columns end cleanly. Report if you chose differently.
- **Random Game Display Content order:** Image or Video (file) → Background color (swatch + hex) → the existing alpha explanation, left **directly under the swatch** → State block (small gap above, label **above** the combo), so the alpha text stays attached to the color it explains and the new label states what the combo changes.
- **Dividers:** the spec's gap complaint is about the visible space. Measure the **effective** space, not just the divider's own Margin: divider Margin plus the neighbors' margins (for example `GLLabelStyle`'s default Margin of 0,10,0,3 on the label that follows, and the bottom margin of the element above).

## 3. Preservation rules (do NOT)

- No Python. **Do not launch the application** or run any UI automation (the user does the visual testing). No commits or pushes. No edits to `themes.md` / `CLAUDE.md`.
- Use Read/Grep/Glob to inspect files; no shell pipelines. Shell only for `dotnet build` and read-only `git diff --stat` / `git status`. Stay inside the project folder. Use the Edit tool; keep CRLF in `.cs` / `.xaml` and the `<!-- [SECTION: ... ] -->` fencing; replace stale comments. A trailing-space edit slip happened last time: after each edit, re-check that attributes are still separated by a space.
- **Never rename or drop any `x:Name`, `Tag`, handler, binding, `IsEnabled`/`ElementName` relationship, range, file filter or default.** The name-keyed Game Info lookups (`GI{Block}{Control}`; many use `!`) mean a renamed control crashes at runtime.
- **Leave unchanged (visually accepted):** Search & Buttons → Buttons; Folders and Game Titles (except divider spacing; their checkboxes stay); Marquee → Shadow; Splash Image; Video Preview → Fallback Image (both tooltips, Background Color); Game Info → Title, Artwork Previews, Favorites Star (including its dividers, which are the reference); Game Info → Game Info (except divider spacing); Play Button Hover States row and its compact shadow checkboxes; Main Window Background; Scrollbars; and the **shared Border tab and all its consumers**.

## 4. Step 1 — Focused re-check (read-only), then STOP

Using Read/Grep/Glob only, report on each item, with actual numbers:

1. **Dividers (1A):** for Search (1), Folders (1), Game Titles (1), Game Info tab (2), Play Button (3): the effective space above and below each (divider Margin plus neighbor margins), and the two Favorites Star dividers' effective above/below as the reference. State the change needed so above equals below (raise the top; never reduce the bottom).
2. **Search (1B):** how the Font header row and Bold are built compared with the accepted Folders/Game Titles header row (structure, margins, `UseLayoutRounding`); whether a real centering mismatch exists. The current column layout of the Font/Alignment row versus the Size/Color/Background row beneath; the x positions of Color hex right edge, Background group left/right edges; the gap used between Color and Background.
3. **Text Marquee (1C):** x position and width of Blur and Depth in the compact shadow row; the current x of Max size, Text margin box, Border Size and Radius; the plan for each (including the 60 px Radius clipping concern and how Radius's left edge lands on Depth's left edge).
4. **Marquee tab row (1D):** the tab-header text edit location; an estimate of whether the four headers `POSITION & SIZE | LOGO | TEXT MARQUEE | SHADOW` fit on one row at the current font/padding (you cannot render; give your reasoning and say it needs the user's visual check). Where `Logo image:` lives and that its ? glyph and tooltip are separate and untouched.
5. **Random Game Display Text (1E):** current structure and why Alignment is clipped and Wrap Text is not visible; the available pane width (about 385 to 397 px, less a vertical scrollbar, less the tab's 10 px margins); a column plan for Font and Alignment that shows the **full Alignment dropdown arrow and the "Wrap Text" checkbox** beside the Alignment header (the two columns may be unequal); the checkbox name and the `ElementName` bindings of the existing shadow group, and that they survive the compact conversion.
6. **Random Game Display Content (1F):** `PnlRGBgState`, `CboRGBgState`, its label, and any code-behind references to them; confirm `CboRGBorderState` (the Border tab's own "State:" combo) is separate and stays as is.
7. **Publisher (1G):** why Alignment clips; the column plan.
8. **Play Button (1H):** x/width of Blur and Depth in its compact shadow rows; whether Button Text can extend so its right edge meets Blur's right edge; Nudge left edge at Depth's left edge and width equal to Depth; **whether the full label "Vertical nudge (px)" fits to its right** (give the pixel estimate against the pane width); Border Size/Radius placement under Blur/Depth with the 50/60 widths.
9. Anything in the spec or in this handoff that conflicts with the code, or that would force a layout decision the user has not made.

Post a concise per-item plan and any conflicts, then **STOP and wait for "GO"**. If the Play Button label cannot fit by local adjustment, say so with options; do not shorten the label.

## 5. Step 2 — Implementation (only after GO)

Apply the corrections in this order. Preserve all existing behavior.

**A. Dividers.** For each affected divider, raise the space **above** so the effective gap above equals the effective gap below. Do not reduce the bottom. Do not touch the Favorites Star dividers. Report old and new values per divider.

**B. Search tab.**
- Bold beside "Font:": adjust only if the audit proved an actual mismatch; use the accepted header-row pattern. Do not change checkboxes elsewhere.
- Font dropdown: its **right edge = the Color hex field's right edge** in the Size/Color/Background row.
- Alignment dropdown: **left edge = the Background group's left edge**, right edge = the Background group's right edge (section 2).
- One consistent gap boundary between the two dropdown regions and the groups beneath; use the same column definitions for both rows. No global hex-width changes. The label shadow (standalone toggle exception) is unchanged.

**C. Text Marquee tab.** Using Blur/Depth in the compact shadow row as the reference:
- **Max size** begins at Blur's x with Blur's width (label above starts at the same x).
- **Text margin** box begins at Blur's x with Blur's width; keep the label `Text margin (units)` and the existing rule that the box is **disabled while Wrap Text is on**.
- **Border Size** field's left edge = Blur's left edge; **Border Radius** field's left edge = Depth's left edge. Keep their **50 px / 60 px** widths; the gap between them follows from the alignment. Labels above start at their field's left edge. Keep the Border Color group and its left alignment with the Background group.
- Checkboxes and everything else on the tab stay as they are.

**D. Marquee tab and Logo heading.**
- Tab display header `SPLASH LOGO` → `LOGO` (display only; `x:Name`, keys, bindings unchanged). Target sequence `POSITION & SIZE | LOGO | TEXT MARQUEE | SHADOW`.
- Inside that tab, the file-selector heading `Logo image:` → `Splash logo image:`; keep the ? glyph, its tooltip text and the hint `Leave empty to show no logo.`
- Change **no** tab padding, header font size or spacing, and no other tab name. If the row still looks like it will wrap, say so in the report; do not make unapproved adjustments.

**E. Random Game Display → TEXT.** New order:
```text
Font: [ ] Bold                    Alignment: [ ] Wrap Text
[ Font dropdown               ]   [ Alignment dropdown        ]

Message:
[ existing message input                                  ]

Max text size (px):
[ 40  ] When a game is selected
[ 100 ] When on the splash page

Color:
[ swatch ] [ hex ]

[ ] Drop shadow                  Blur:       Depth:
[ swatch ] [ shadow hex ]         [ 3 ]       [ 1 ]
```
- Header rows with centered label + checkbox and `UseLayoutRounding="True"` (the accepted pattern); Bold beside Font, Wrap Text beside Alignment. Both combos on one row, unequal columns allowed so the **whole Alignment dropdown (arrow included) and Wrap Text are visible**; keep the labels readable.
- Keep the existing Message input, per-state max-size boxes (live labels, limits, bindings) and Color control unchanged.
- Convert the shadow to the **compact recipe**: remove the standalone toggle row and the `Shadow color:` label; checkbox ("Drop shadow", normal white text) directly above the shadow swatch/hex, Blur and Depth labels on the checkbox row, the three controls beneath. Keep the checkbox's `x:Name`/handlers and the `IsEnabled` `ElementName` bindings on the color panel, Blur and Depth. Same spacing as the accepted compact groups (for example the Title tab); no divider.

**F. Random Game Display → CONTENT.** Order and labels per section 2: file (unchanged, shared, with its ? tooltip) → Background color + swatch/hex → alpha explanation → State block with the label **`Background color state:`** above the combo. Preserve `CboRGBgState`, `PnlRGBgState`, `_rgStatePartId`, `CboRGState_SelectionChanged`, both states' color/alpha values and the shared media path. The Border tab's "State:" label is not changed.

**G. Game Info → Publisher.** Adjust only local column sizing/spacing so the entire Alignment dropdown, arrow included, fits beside Font. Keep control names, bindings and Bold.

**H. Game Info → Play Button.** Using Blur/Depth in the compact shadow rows as anchors:
- **Button Text** input: right edge aligned to **Blur's right edge**; still shows "Launch Game".
- **Vertical Nudge** numeric field: left edge = **Depth's left edge**, width = Depth's width; the full label **`Vertical nudge (px)`** to its right, not clipped or shortened.
- **Border Size** begins under Blur, **Border Radius** under Depth, widths **50 / 60 unchanged**; local Play Button row only.
- Dividers per A. Hover States row and the compact shadow checkboxes unchanged.

## 6. Step 3 — Verification and report

1. **One final clean rebuild:** `dotnet build ArcadeStick.csproj --no-incremental`; 0 errors; report any warning (a full rebuild previously reported 0). Intermediate builds only if structurally warranted or for troubleshooting.
2. **Static checks (no app launch):**
   - Every `x:Name`/`Tag`/handler referenced from code-behind still exists, above all the Game Info `GI{Block}{Control}` names, the Random Game Display controls, and the scrollbar field table.
   - In the new Random Game Display Text compact shadow group, and still in the other 15 groups plus the two exceptions, the color panel and the Blur and Depth boxes are bound to **their own** checkbox.
   - Attributes are properly space-separated (grep for broken adjacent attributes); `git diff --stat` shows no whole-file or line-ending rewrites.
   - List the computed widths/x positions for the tight rows (Search, Text Marquee, Random Game Display Text, Publisher, Play Button) so the user can compare with what they see.
3. **Report in this form:** files changed · anything that did not match the instructions · anything changed beyond them · the test checklist below · things to check visually.
4. **User's visual checklist:**
   - Search: Font right edge = Color hex right edge; Alignment left edge = Background group left edge; Bold level with its label; divider balanced.
   - Folders / Game Titles: balanced dividers, no scrollbar, checkboxes untouched.
   - Text Marquee: Max size, Text margin and Border Size left edges at Blur; Radius at Depth; Text margin box still disabled while Wrap is on.
   - Marquee tabs: `POSITION & SIZE | LOGO | TEXT MARQUEE | SHADOW` (one row or not), `Splash logo image:` with its ? tooltip.
   - Random Game Display Text: order above, full Font/Alignment/Wrap row, compact shadow enables/disables its color, Blur and Depth.
   - Random Game Display Content: file shared, `Background color state:` below the color, each state keeps its own color.
   - Publisher: Alignment fully visible.
   - Game Info and Play Button dividers balanced; Button Text and Nudge and local Border aligned to Blur/Depth; full `Vertical nudge (px)` visible.
   - Unchanged: shared Border tab, Favorites Star dividers, Hover States, accepted tooltips and tabs.
5. **STOP.** Small follow-up tweaks come as short prompts after the user's visual review.
