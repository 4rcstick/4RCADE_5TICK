# Border ring everywhere (spec for Claude Code)

Status: rule and layer list decided by the user 2026-10-24. STEP 1 is read-only. STEP 2 starts only after the user says "go".

## Goal

One border rule for every bordered layer, and the reference is **Video Preview exactly as it works today (decision 57)**:

**The layer's X / Y / Width / Height are the CONTENT area (what is inside the border). The border is drawn outside that rect, growing outward by the border size B on every side. The border never resizes the content. The Position fields keep showing the content numbers.**

Today Video Preview does this: `ApplyLayout` places `VideoPreviewLayer` at (X - B, Y - B) sized (W + 2B, H + 2B); `ApplyClip` pulls the media slot in by max(0, B - 1) so it reaches 1px under the ring (hides a sub-pixel sliver) and the visible content is exactly W x H; inner radius = max(0, radius - (B - 1)); the alignment guides use the inflated rect (`GetGuideRect`); the decoder size comes from the entry. Copy that behavior, do not invent a variant.

## Why

- The user sized Splash, Game Info and shape areas by hand as the target size plus the border, because those layers keep the border inside their rect. Random Game Display does not line up with Video Preview at the same rect and border.
- Mixed behavior is the problem. The decision is consistency with Video Preview as the model, so a 640x480 layer is 640x480 of content on every bordered layer, whatever its border.

## Layers

**Change to the rule (audit, then fix if they differ from the reference):**
1. Shape Layer 01 to 04 and custom layers (Border element, host Grid with image/video, rounded clip).
2. Splash Image (`SplashLayerControl`, `ApplyBorder`; its content is currently inset inside its rect).
3. Random Game Display card, both states (`RandomSpinCard`, `RandomSpinInner`, `RandomSpinFileHost`, `ApplyRandomGameDisplayRect`, `ApplyRandomGameDisplayCardSize`). The border is per state (each part's own BorderSize / BorderColorHex / CornerRadius), so each state inflates by its own B.
4. Marquee no-art banner card (`MarqueeBannerCardTemplate`, `NoMarqueeBanner`, `RandomizerNoMarqueeFallback`). It has no rect of its own: it is a 4:1 card centered inside the Marquee layer's rect. The user wants it to follow the rule for consistency (not needed for alignment). See the banner note in STEP 1.

**Reference (verify only, change nothing):** Video Preview.

**Exempt (audit may read them, STEP 2 must not touch them):** the Search & Buttons row. The four buttons already have special border handling (the fill runs edge to edge under the border), and the search box borrows the buttons' resting border. The search label has no border.

**Expected to have no border of their own (confirm and report):** Games List, Marquee panel, Game Info parts, Main Window Background, Footer. Search the whole project for BorderSize / BorderColorHex / CornerRadius readers in case another bordered element exists; do not trust this list.

## STEP 1: audit (READ-ONLY, change nothing)

First, report the reference: the exact code that makes Video Preview behave as above (ApplyLayout inflation, `ApplyClip`, `ApplyBorder`, `UpdatePreviewTargetSize`, `GetGuideRect` and any other place that reads the inflated or inner rect), with function names and the formulas as implemented. STEP 2 will copy this.

Then, for each layer in "Change to the rule", a table row:
- Where the border is drawn now (which element, which brush and thickness).
- Where the entry rect sits today: is it the OUTER size (content inset by B) or the content area?
- The control's placement code in ApplyLayout and how its size is derived.
- The clip for image/video content and its corner radius (outer, outer minus B, none).
- What fills under the border and how far (background fill, file, letterbox), and what a translucent border color or fill shows through.
- Everything else that depends on the content rect: wrapped text width, the Viewbox box and its 10px margin, the file host and its rounded clip, first-frame display, the card preview (`SetRandomCardPreview`), the banner preview, the file's native-size resize on Browse (custom layers), the ratio lock, the guides rect for single and multi selection (`GetGuideRect`), and the Center guides.
- Verdict: MATCHES THE REFERENCE, OUTLIER or MIXED.

Banner card note: report how the card's size is derived today (the outer Border sized by its child Viewbox inside the Marquee rect) and where its border lands relative to the Marquee rect. Proposed rule for the card: the 4:1 card content fits the Marquee layer's rect and the border is drawn outside it, so the border may extend past the Marquee rect by B. Say whether following the rule cleanly is straightforward or would need a redesign; if it needs a redesign, stop and report instead of proposing one.

Also report:
- Edge cases: B larger than the radius, B larger than half the rect, radius 0, border 0 (nothing may change at border 0).
- Which saved layouts change visibly: any layer in the list with BorderSize above 0 grows outward by B on every side at the same saved numbers (no migration, decision 3). List the default entries affected (for example Shape Layer 01 and 02 default to a 1px border).
- The exact files and functions each outlier would touch (expected: `MainWindow.xaml.cs` ApplyLayout, `ApplyShapeLayers` / `ApplyCustomLayers`, `ApplyRandomGameDisplayRect` / `ApplyRandomGameDisplayCardSize`; `SplashLayerControl` ApplyBorder; `AdvancedThemesTabControl` `GetGuideRect`; `MainWindow.xaml` for the card and banner template).
- Anything in the audit that disagrees with themes.md or CLAUDE.md (the code wins; list the corrections to make).

Do not propose fixes beyond a short per-outlier plan. Stop after the report and wait for the user.

## STEP 2: implement (only after the user says "go")

Fix only the layers the audit marked OUTLIER or MIXED, to the reference behavior.
- Prefer one shared way to inflate a layer by its border (a helper used by ApplyLayout for Video Preview and the new layers) over copies of the arithmetic, if the audit shows that fits.
- Guides: `GetGuideRect` inflates for every layer on the rule, single and multi-select, and the Center guides follow the same rect.
- The Position fields, ratio lock, Browse-to-native-size, multi-select nudge and group parts keep working on the content numbers.
- Random Game Display: each state inflates by its own border; the text width, Viewbox box and file clip use the content rect (rect width minus padding, no longer minus the border); the card preview (`SetRandomCardPreview`) uses the same placement.
- Do not add settings, entry fields, UI or an "inside / outside" option. Defaults stay as they are.
- Keep CRLF in `.cs` and `.xaml`, LF in `.md`. Keep every x:Name and handler name. Build with 0 errors.
- Do not touch the Search & Buttons row.

## Test checklist (for the user)

1. Border 0 on every listed layer: looks exactly as before.
2. Random Game Display card (both states) and Video Preview with the same X / Y / Width / Height, border size, radius: the outer edges and the content edges line up pixel for pixel, with the card preview on and with the video playing.
3. A shape layer at a given rect with border 3: the content (fill, image or video) is exactly that rect, the border sits 3px outside it; a neighbor aligned to the rect edge touches the content, not the border.
4. Splash Image: same check, with an image and with an mp4.
5. Rounded corners: radius 20 with border 3, and radius 2 with border 6: the content's corners follow the inner curve with no gap or sliver; radius 0 and a very large border behave.
6. Wrapped message in the card, both states, with different borders per state: no overflow, shrinks only when taller than the content area.
7. Banner card (idle banner, the spin's no-marquee card, the Banner tab preview): border outside, no clipping at the Marquee rect edge, shadow still follows the card.
8. Translucent border color and translucent card background: report what shows under the ring (the reference's 1px overlap is the expected behavior).
9. Custom layer Browse to a file: the layer takes the file's native size as the content; the border sits outside it.
10. Alignment guides (Edge and Center, single and multi-select) frame the outer ring on every listed layer, like Video Preview; Position numbers still show the content rect.
11. The Search & Buttons row looks exactly as before (buttons, hover, search box).
12. 1920x1080 and 1280x720, theme save / load, Reset Layer, close without saving: nothing regresses. Expect to hand-adjust the areas you sized as target plus border (reduce Width and Height by 2 x border).
13. Build is clean, no new warnings.

## Docs (after testing, not before)

themes.md: new decision 59 (one rule for every bordered layer, Video Preview as the model; this replaces the "Video Preview only" wording of decision 57), sections 3.8, 3.9, 3.10 (note the exemption), 3.16 and the guides text corrected, the section 7 trap "Video Preview rect is the inner (video) rect" rewritten for all listed layers, the 6b entry marked done, the status table and the change log. CLAUDE.md: the "Video Preview's rect is the video area" gotcha rewritten to the general rule and the "border behavior is not unified" sentence removed. If the audit finds that the docs claimed a ring where the code differs (or the reverse), list those corrections in the audit report.
