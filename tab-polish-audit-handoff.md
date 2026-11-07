# HANDOFF: Theme Builder Tab Polish — Comprehensive Read-Only Audit

**Type:** READ-ONLY AUDIT. You will edit nothing in this task. Not source, not `themes.md`, not `CLAUDE.md`, not settings files.
**Ends with:** a written report in the chat, then you STOP and wait. Implementation passes are written afterwards, from your findings, and each one needs the user's explicit "go".

---

## 1. Goal and why

The user has a finished design spec that polishes the layout of about 25 Theme Builder tabs (Search, Buttons, Folders, Game Titles, Marquee tabs, Splash Image, Random Game Display, Video Preview, Game Info tabs, Scrollbars, Main Window Background, shared Border widths). It is UI layout and wording only: no new properties and no behavior changes.

Before any implementation is planned, we need one complete picture of the live code: where each tab's controls live, what is shared, what is name-keyed, and where the risks are. Your report is what the pass groupings will be built from. There is **no predetermined number of passes**.

## 2. Inputs

- **The spec:** `Theme_Builder_Tab_Polish_-_Claude_Chat_Planning_Specification.md` (about 2,430 lines), expected in the project folder root. Read **all of it** before auditing. **If it is not there, stop and ask the user** (say where you looked). Do not search elsewhere and do not use another copy.
- `CLAUDE.md` and `themes.md`. **Trust order: the code first, `themes.md` second.** `themes.md` is known to lag the code on the already-finished Position & Size and Border passes (it still calls the first tab "Position" and lists the old Marquee tab order). Report mismatches; do not fix them.

## 3. Spec amendments (these override the spec text)

The user answered four questions after the spec was written. Where they conflict with the spec, these win:

1. **Local Border rows get the new widths too.** The spec said to widen the shared Border Size and Radius fields (36px → 50px for Size, 36px → 60px for Radius) and leave lookalike local rows alone. **Amended:** local copies (Text Marquee's Border row, Play Button's Border row, and any other lookalike you find) get the same 50px Size / 60px Radius, preserving their existing spacing (including the 22px group gaps) where possible. Report every one, shared or local, and say which it is.
2. **Video Preview Background Color gets a (?) tooltip.** It still moves into the Fallback Image tab, and its label gets the standard (?) help glyph. Tooltip text, exactly: `Sets the background color behind the video preview, including letterbox areas, and behind the fallback image.`
3. **Main Window Background:** the tab name **IMAGE OR VIDEO** is confirmed as specified.
4. **Game Info tab row:** existing or additional tab wrapping is acceptable as needed to avoid clipping. Keep the tab names readable (do not abbreviate to avoid wrapping).

## 4. Execution rules (apply to this audit and are to be carried into every later handoff)

- **No Python.** Not for reading, not for checking. (It always prompts the user, per `CLAUDE.md`.)
- **Do not launch the application.** The user does all visual testing. No scripts that capture the screen or send input.
- **Minimize permission prompts.** Use Read, Grep and Glob for everything. No shell pipelines (`sed`, `cut`, `awk`, `head`, `tail`, `wc`, `ls`, `cat`) to look inside files. In this audit you should need no shell commands at all. Later implementation passes need only `dotnet build` and `git diff --stat`.
  - In your report, list the **exact shell commands later passes will need** (build, `git diff --stat`, anything else) so the user can approve matching allow rules in `.claude/settings.local.json` once. **Do not edit the settings file yourself.**
- **Stay inside the project folder.** If an expected file is missing, stop and ask.
- **Never commit or push.**
- Later passes must end with a clean build (0 errors, report any new warning) and must not run the app.

## 5. What to audit

For every item, give **file, x:Name / section, and line numbers**, and say whether the spec matches the code.

### A. Per-tab inventory (every tab in the spec)
For each tab: **Search, Buttons, Folders, Game Titles, Splash Logo (today Logo), Text Marquee (today Banner), Marquee Shadow, Splash Image Image tab, Random Game Display Text, Content (today Background), Video Preview Background and Fallback Image, Game Info Title / Publisher / Game Info / Previews / Play Button / Favorites Star, Scrollbars Games List and Game Info, Main Window Background Image, and the shared Border tab.**

Report:
1. Where the tab's XAML lives (`AdvancedThemesTabControl.xaml`, the Global/scrollbar control, or elsewhere) and its `x:Name`.
2. The **current** layout in a few lines: what shares a row, where each label sits, where Bold, Wrap and the shadow checkbox are, and what dividers exist.
3. **Every text string the spec says to replace or remove**, compared with the live string. Quote the live one. Flag any mismatch (headings, glow tips, hint text, tooltips, "(units)", "Preview banner", "Drop Shadow (text)", "Page Navigation", "Hover", and so on).
4. **Media file filters** for Splash Logo, Splash Image, Video Preview Fallback, Random Game Display Content and Main Window Background, compared with the formats the spec's tooltips list. Report discrepancies; do not widen or narrow anything.
5. Whether the tab is wired through a shared helper, a template, hand-written XAML, or code-built controls.
6. Whether the tab currently scrolls, and whether the Folders tab is scrollbar-free today.
7. The specific controls the spec **moves, merges, removes, or renames**, with their handlers and any code-behind references to them. Check especially:
   - `TabVideoBackground`, `VPBgColorPicker` and `TxtVPBgColorHex` (the tab is removed and the controls move).
   - Random Game Display's `CboRGBgState` and `CboRGBorderState`, which share `_rgStatePartId`.
   - `ChkBannerPreview` and the "Preview banner" wording.
   - Every `ChkDropShadow`-style checkbox and its "Shadow color:" label.

### B. The compact Drop Shadow recipe (15 groups)
The spec replaces the "Shadow color:" label with the Drop Shadow checkbox in 15 groups: Folders 2, Game Titles 2, Text Marquee 1, Title 1, Publisher 1, Game Info tab 3, Artwork Previews 1, Play Button 2, Favorites Star 2. Report:
- How each group is built today (hand-written copies, or one shared structure). Is there any reusable style or template?
- For each group, the checkbox name, its handlers, and which controls it enables or disables.
- The name-keyed Game Info handler table (`GI{Block}{Control}`): how it finds the shadow controls, which blocks use it, and **what would break if a checkbox moved or a label were deleted**. Also check controls found by name that sit on labels being removed.
- Whether any shadow group is built in code rather than XAML.
- The three intentional exceptions (Search Label, Random Game Display Text, Marquee Shadow tab): confirm their current layout so they stay untouched.

### C. Established conventions to reuse
Find the existing implementations and report their names and locations so the later passes copy them instead of inventing:
- The **(?) help glyph + tooltip** pattern (Layers header glyph, `LayersHelpGlyphStyle`; the **Custom Layer Content tab**; any Random Game Display Content usage). Report how the tooltip is built and styled.
- The **header + checkbox alignment** pattern, including the `UseLayoutRounding = true` approach, and where it is already applied.
- **Divider** usage: any existing subtle divider style or key.
- **Field width and gap constants** for numeric fields, color swatch + hex pairs and the 22px group gap, plus where Position & Size and Border set them.
- The "numeric field with the label to its right" pattern, if one already exists.

### D. Shared Border tab and every local Border lookalike
- Every layer that consumes the shared Border tab (including Search & Buttons via `GetSelectedBorderEntry` and the Hover border row `PnlSBHoverBorder`), and the Size and Radius field names and widths.
- Every **local** Border Color / Size / Radius row (Text Marquee, Play Button, others), with its current field widths and gaps.
- For each: can the amended widths (50 / 60) fit without clipping at the 385 to 397px pane width? Give your measurement or reasoning.

### E. Tab tables, order and naming
- `GetLayerTabs`, `AllLayerTabs` and `ApplyLayerTabs`: exactly what must change to rename, reorder and remove tabs (Marquee order, Video Preview consolidation to Position & Size → Fallback Image → Border, Random Game Display Content, Artwork Previews, Image or Video).
- Anything that selects or remembers a tab **by index** or by the name `TabVideoBackground` and so on, which a reorder or removal could break.
- Where tab header text is set (the displayed label, separate from `x:Name`s and stored keys). Confirm the renames are display-only.
- The Game Info layer tab row: how many tabs, current wrapping, and the likely effect of the longer "ARTWORK PREVIEWS" label.

### F. Behavior-preservation checks
- **Random Game Display Content:** does the media file field (Browse/Clear, `BrowseLayerFile`) follow the **selected State** today, i.e. does each state's part have its own `ImagePath`? The spec moves State above the file selector on the assumption that it does. **If the file is shared across states, say so loudly**: that would be a design question for the user, not an implementation detail.
- **Video Preview:** confirm the Background Color control moves without any change to `BgColorHex`, the letterbox behavior or the `ThemeMissingPreviewAsset` / fallback logic.
- **Text Marquee:** confirm the generated marquee's text shadow (`BannerShadow*`) stays separate from the shared Marquee shadow (`TextShadow*`), and that "Preview text marquee" is a label change only.
- **Scrollbars:** confirm the grid column structure that controls the label-to-control gap, that the Width and Thumb columns can be tightened without touching control widths, and that the "Use global scrollbar" disabled state is not affected.
- **Play Button Hover States:** measure whether three color groups (swatch + hex each, with gaps) fit the pane without squeezing fields, and propose local adjustments if not. Do not implement them.
- **Buttons tab:** how the Hover Color field can expand locally while the Background and Hover Background groups start at the same x, without changing shared color-field widths.
- **Folders / Game Titles:** whether the new layout (including the divider and the consolidated shadow groups) is likely to stay scrollbar-free.

### G. `themes.md` / `CLAUDE.md`
List every place in `themes.md` and `CLAUDE.md` that will need updating once these changes land (tab names and order, Video Preview's Background tab, Marquee tab names, Game Info tab names, and so on). **List only. Edit nothing.**

## 6. Report format (post in chat)

1. **Spec vs code mismatches** (a table: spec item → what the code actually has → suggested handling). Include text strings and file filters.
2. **Per-area findings** (A through G above), concise, with file/line/x:Name references.
3. **Risks**, ranked: what could fail silently (name-keyed handlers, tab-table edits, runtime-only failures) versus what a build will catch.
4. **Shared-component map:** which areas share code or controls (the compact shadow recipe, the shared Border tab, the help-glyph convention, the tab tables) and so should be edited together or in a deliberate order.
5. **Recommended pass groupings** with rationale, what each pass includes and excludes, the order, and which passes depend on others. This is your recommendation only; the user decides.
6. **Questions that need the user's decision** (separate from optional suggestions).
7. **Shell commands later passes will need**, for the user's allow-rule setup.
8. **Confirmation** that you edited nothing.

## 7. Constraints (what NOT to do)

- No edits of any kind, no builds, no app launch, no Python, no commits.
- Do not redesign the spec. Report conflicts and uncertainties instead of resolving them.
- Do not look outside the project folder.
- Do not start any implementation.

## 8. Verify (for this audit)

The audit is done when the report covers every tab named in section 5A, every item in B through G, and ends with the confirmation that no files were changed. Then **STOP and wait** for the user.
