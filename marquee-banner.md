# Marquee banner pass (marquee-banner.md)

Read CLAUDE.md and themes.md first (code wins over themes.md). Concise answers, no window width mentions. Run STEP 1 only, report, and stop. Do not start STEP 2 until the user says go.

## GOAL

The Marquee layer gets a BANNER tab that styles the no-art title card: the idle no-art banner (`NoMarqueeBanner`, in the Marquee layer) and the spin's no-marquee card (`RandomizerNoMarqueeFallback`) become ONE shared template and ONE style, per resolution, on the MarqueePanel entry. The card also joins the shared Marquee image shadow, and gets its own text shadow. A session-only "Preview banner" checkbox shows the banner without needing a game that has no marquee art.

## DECISIONS (from the planning conversation)

1. Tab: a BANNER tab on the Marquee layer, after Logo and Shadow (order: Position, Logo, Shadow, Banner). All banner controls, including the border, live on this tab (not on the shared Border tab).
2. Storage is per resolution on the MarqueePanel entry. Reuse the generic fields where the Marquee entry does not already use them (the audit must confirm): TextFontFamily, TextBold, TextFontSize (the MAX size), TextColorHex, BgColorHex (banner background color), BorderColorHex, BorderSize, CornerRadius. The Marquee entry's TextShadow* fields and ShadowTarget are ALREADY taken by the shared image shadow (decision 38/41), so the banner's text shadow needs its own new nullable fields: BannerShadowEnabled, BannerShadowColorHex, BannerShadowBlur, BannerShadowDepth (written only when not null; in CopyStyleFrom, the registry fill-ins and the repaint key). No new fields otherwise. No image/file property.
3. Controls on the BANNER tab (top to bottom): "Preview banner" checkbox; Font + Bold; Max size and Color; Background color; Drop shadow (text) with color / blur / depth (disabled while off); Border color, Size, Radius. Ranges: size 6 to 200, blur 0 to 30, depth 0 to 20, border size 0 to 50, radius 0 to 200. Colors apply immediately, numbers on Enter or focus loss with revert, same helpers and column grid as the Search / Game Info tabs. Follow the layout-clipping lesson: no label or row wider than the pane (stack side-by-side controls if needed).
4. One line, shrink to fit: the size is a cap. The title is as large as the max allows and shrinks (Viewbox `StretchDirection="DownOnly"`) only when it would not fit the card; nothing overflows. No wrap option in this pass. Keep the card's existing 400x100 layout box scaled by its Viewbox as today unless the audit shows a reason to change it.
5. Border sits OUTSIDE the Viewbox so its size and radius never scale with the marquee box (same fix as the Random Game Display card).
6. Defaults match today's look but as fixed constants, no longer following any other setting: size 30, bold, Segoe UI, text color and border color = the current default Header color (`#FFEF44C3`, confirm in code), background = the Main Window Background default (`#FF1C1C1E`), border size and radius = whatever the card uses today (audit reports), text shadow off (`#FF000000`, blur 3, depth 1). Set and filled by a new `FillMarqueeBannerStyle` from BuildDefaultLayout and ResolveLayout (saved values kept, including a saved `false`).
7. The card joins the shared Marquee image shadow (Marquee > Shadow tab, MarqueePanel TextShadow*, `ThemeMarqueeEffect` / `ShadowTarget`). The banner and the spin's no-marquee card stand in for a marquee, so they follow the "Marquees" side: shown with "Both" and "Marquees only", not with "Logo only". The effect goes on the card's outer Border (outside the Viewbox, so it follows the card's shape and alpha). Update the Shadow tab wording if needed (for example the "Apply shadow to" labels or hint) so it is clear that "Marquees only" includes the no-art card; keep the stored ShadowTarget values unchanged.
8. The text shadow is separate: BuildShadowEffect from the BannerShadow* fields, applied to the title TextBlock only.
9. Merge the duplicate cards into one template. The audit decides how (a shared DataTemplate / UserControl, or one element reused); the spin card must keep its current behavior (hidden/shown with the spin as today) and the idle banner its (shown only when MarqueeMissing and no spin). Retire `ThemeHeaderColor` and the flat-value exceptions if nothing else reads them (audit confirms); if something else does, keep the forwarder and say so.
10. Live repaint: a new `_lastMarqueeBannerStyleKey` in ApplyLayout (every banner field on the MarqueePanel entry) triggers `MainViewModel.RefreshMarqueeBannerBindings()`, also called from RefreshThemeBindings. Forwarders `ThemeMarqueeBanner*` following the Game Info forwarder pattern. Never bind flat settings. The Marquee shadow target is already in `_lastMarqueeShadowKey`.
11. Session-only "Preview banner": checkbox `ChkBannerPreview` on the BANNER tab, not stored anywhere. When checked, the Marquee layer shows the banner (the selected game's title when a game is selected, otherwise the sample text "Game Title") regardless of MarqueeMissing, without changing the selection or any media state. Edits repaint live through the style key. Reuse the Random Game Display preview pattern (event from the Advanced tab, forwarded by ThemeBuilderWindow to a MainWindow method, local override that is cleared on turn-off). Auto-off on: another layer or multi mode, a resolution switch, ReloadFromSettings (theme load), leaving THEME BUILDER (tab Content compared to ThemeBuilderPage, never by index), closing the builder, and a real spin starting (with a callback to uncheck the box). While a real spin runs, do nothing. The preview does not affect the spin's no-marquee card, which uses the same style anyway.
12. No legacy migration; saved themes without the new fields fill from defaults. Reset Layer on the Marquee layer already restores via CopyStyleFrom; confirm it covers the banner fields (and does not touch the logo path logic).

## STEP 1: READ-ONLY AUDIT (report, change nothing)

Report briefly:

- The markup, bindings and parents of `NoMarqueeBanner` and `RandomizerNoMarqueeFallback`: sizes, Viewbox, border, fonts, colors, text source, when each shows or hides.
- Which Marquee-entry fields are already used (TextShadow*, ShadowTarget, ImagePath, others), and confirm BgColorHex, BorderColorHex, BorderSize, CornerRadius, TextFontFamily, TextBold, TextFontSize and TextColorHex are free on MarqueePanel (nothing reads them today, including ApplyLayout, ApplyBorder or ApplyShapeLayers).
- Every reader of `ThemeHeaderColor` and `ThemeMainColor` (and the Global Header color forwarder) so the retirement is safe; list the readers that would remain.
- How `ThemeMarqueeEffect`, `ThemeMarqueeLogoEffect` and `RefreshMarqueeBindings` are bound and raised, and where the card's outer Border can take the effect.
- How the spin shows and hides the no-marquee card, and how the idle banner is gated (MarqueeMissing, spin state), so the preview override and the merge keep both behaviors.
- The Marquee layer's tab plumbing (AllLayerTabs / GetLayerTabs, LoadMarqueeShadowFields, the GI table block for Marquee) and how the Random Game Display preview event is wired (Advanced tab event, ThemeBuilderWindow forwarder, MainWindow method), so the banner preview can follow the same path.
- The current card's actual border size, radius, padding and sizes, for the defaults.
- Any conflict between the decisions above and what the code does, with the smallest proposed adjustment.

## STEP 2: IMPLEMENTATION (only after the user says go; one pass, in this order)

A. LayerRegistry: new fields (BannerShadow* on LayerLayoutEntry, copied by CopyStyleFrom), defaults, FillMarqueeBannerStyle (from BuildDefaultLayout and ResolveLayout).
B. MainViewModel: ThemeMarqueeBanner* forwarders and RefreshMarqueeBannerBindings (from RefreshThemeBindings and ApplyLayout's new key); retire ThemeHeaderColor if the audit allows.
C. MainWindow.xaml(.cs): one shared card template for the idle banner and the spin's no-marquee card (card Border with background, border, shared Marquee shadow effect; inner shrink-only Viewbox; title TextBlock with font, color and text shadow), `_lastMarqueeBannerStyleKey`, the preview method.
D. Advanced tab: BANNER TabItem (Collapsed by default, listed in AllLayerTabs and GetLayerTabs for the Marquee layer after Logo and Shadow), controls loaded when the Marquee layer is selected, handlers following the Search & Buttons / Game Info patterns, the Preview banner checkbox and event, auto-off rules, and the Shadow tab wording for "Marquees only". Keep all existing x:Names and handlers.
E. ThemeBuilderWindow: forward the preview event, turn it off when leaving THEME BUILDER and on Closed, uncheck on a real spin.
F. Build; expect 0 warnings and 0 errors. Update themes.md: section 3.9 (the banner and the merged card), 3.5 and 3.11 mentions of ThemeHeaderColor / ThemeMainColor feeding the card, decisions 23 and 24 (superseded), new decision entries, open question 8, 6b (the merge idea is done), the layer status table (Marquee), traps, the file map, a change-log entry dated 2026-10-22, and the Last verified line. Keep LF line endings.

## TEST CHECKLIST (give to the user at the end)

- Preview banner on and off; the card shows the selected game's title or "Game Title" and turns off on layer change, tab switch, resolution switch, theme load, closing the builder and a real spin.
- Font, Bold, max size (a long title shrinks, never overflows), text color, background alpha, border color / size / radius (size and radius stay fixed when the marquee box is resized).
- Text shadow on and off; the card shadow follows Marquee > Shadow in "Both" and "Marquees only" and disappears in "Logo only", for both the idle banner and the spin's no-marquee card.
- A game with no marquee art shows the styled banner; a spin that lands on a game with no marquee art shows the same styled card; no pop when the spin ends.
- Reset Layer on the Marquee layer; resolution switch copies the style; theme save/load round trip; the logo and normal marquee images are unchanged.
