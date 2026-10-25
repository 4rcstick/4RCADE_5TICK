# Text margins (spec for Claude Code)

Status: requested by the user 2026-10-24. STEP 1 is read-only. STEP 2 starts only after the user says "go", and the plan may change after the audit.

## Goal

Two small margin values the theme maker can tune from the Theme Builder, per resolution, on layer entries (no flat settings, no new window).

1. **No-art banner card, one-line title margin.** With wrap off, the one-line title has no left or right margin inside the card and runs edge to edge. Add a **Text margin** value (default 5 px) on the Marquee layer's Banner tab. It insets the title on the left and right of the card. Applies to **one-line mode only**: with wrap on the title never reaches the edges and must look exactly as it does now. The one-line shrink-to-fit width must account for the margin, so a long title shrinks before it reaches the margin.
2. **Game Info, Info text right margin.** The Info body text has a fixed gap on its right, between the text and the scrollbar, that the theme maker cannot change. (With no scrollbar the width controls it, so it is only a problem when the scrollbar shows.) Add an editable value for that gap on the Info tab, per resolution. The default must equal what the code uses today, so existing themes do not shift.

Not wanted: a left margin on the Info text (X / Y covers it). The gap between the cabinet image and the text on the specs page is **not** to be changed; STEP 1 only reports where it comes from.

## Constraints

- New styling goes on `LayerLayoutEntry`, per resolution. A new field needs all of: the property (`JsonIgnore(WhenWritingNull)` like its neighbors), a line in `CopyStyleFrom`, defaults in `BuildDefaultLayout` and the `Fill...` helper called from both `BuildDefaultLayout` and `ResolveLayout`, and a mention in the matching style key in `ApplyLayout`. Forwarders are re-raised from the matching `Refresh...Bindings()` and from `RefreshThemeBindings()`.
- The Advanced tab's Game Info controls are found by name (`GI{Block}{Control}`) with a Tag-keyed shared handler table, gated per block: follow the existing naming exactly. The Marquee banner controls have their own naming; follow it.
- Every x:Name and handler is referenced from code-behind; verify names after adding controls.
- Do not touch the Search & Buttons row, the border ring code (decision 59) or the Global tab. Do not change defaults of anything else.
- Keep CRLF in `.cs` and `.xaml`, LF in `.md`. Keep each file's comments and `// [SECTION]` fencing. Build with 0 errors. Never commit.

## STEP 1: audit (READ-ONLY, change nothing)

**A. Banner.**
- How the one-line title is laid out today: the `MarqueeBannerCardTemplate` elements, the 400 x 100 reference box, the shrink-only Viewbox (`StretchDirection="DownOnly"`) and what limits its width (`ThemeMarqueeBannerTextWidth` or other). Is there any existing padding or inset on the title or the card? If the Banner tab already has a padding or inset value, say so (it may be reusable).
- Where the wrap switch lives (the field and forwarder) and how one-line vs wrapped mode changes the layout.
- Proposed place for a margin that applies in one-line mode only, and how the shrink width accounts for it.
- The Banner tab's controls and handler naming, and the exact list of files and functions a new `BannerTextMargin` (name to confirm) would touch, following the wiring list above.

**B. Game Info Info text.**
- Which part holds the Info body (text block, ScrollViewer, scrollbar) and the XAML / code that places them. Where the gap between the text and the scrollbar comes from: a Margin, Padding, a column, the ScrollViewer's own template, or the scrollbar width. Give the number in px as implemented and every place it appears.
- Does the Global tab's scrollbar width change the gap? Say how the two interact, so that a new "text to scrollbar" value does not mix with the Global width.
- Is the same fixed margin used by the other Game Info text blocks (the title, publisher block, Info header, Credits / Specs)? List them. Which would change if the Info body's margin became editable, and which should stay as they are?
- The Info tab's controls and handler naming, and the exact list of files and functions a new value (name to confirm) would touch.

**C. Specs page (report only).** Where the gap between the cabinet image and the text comes from, whether it is the same setting as B or a separate one, and whether making B editable would change it by accident.

Also report: edge cases (margin 0, a margin larger than half the width, a very small banner), what existing saved themes would show after the change (the default must reproduce today's look), and anything in the audit that disagrees with themes.md or CLAUDE.md (the code wins; list the corrections).

Do not propose fixes beyond a short per-item plan. Stop after the report and wait for the user.

## STEP 2: implement (only after the user says "go")

Implement what the audit confirmed and the user approved, to the constraints above. Keep the two items separate in the report so each can be tested on its own.

## Test checklist (for the user)

1. Banner, wrap off, a short title: the title sits at the same place as before, centered; margin 0 looks like today.
2. Banner, wrap off, a very long title: it shrinks before it reaches the margin, never touching the card edge; change the margin live on the Banner tab and the text re-fits.
3. Banner, wrap on: looks exactly as before. Idle banner, the spin's no-marquee card and the Banner tab preview all follow.
4. Game Info Info text with a scrollbar: the default gap equals today's; change the value and the gap changes live; no scrollbar: nothing changes.
5. Other Game Info blocks and the specs page: unchanged, including the cabinet image gap.
6. 1920x1080 and 1280x720: each keeps its own value; theme save / load, Reset Layer (back to the default), close without saving: nothing regresses.
7. Build is clean, no new warnings.

## Docs (after testing, not before)

themes.md: a decision for each new value, sections 3.9 (banner) and 3.12 (Game Info) corrected, the 6b Game Info margin note marked done, the status table and the change log.
