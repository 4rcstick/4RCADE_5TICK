# Random Game Display pass (random-game-display.md)

Read CLAUDE.md and themes.md first (code wins over themes.md). Concise answers, no window width mentions. Run STEP 1 only, report, and stop. Do not start STEP 2 until the user says go.

## GOAL

The Random Game Display group (two parts: RandomGameDisplay "when a game is selected", RandomGameDisplaySplash "when on the splash page") gets Text, Background and Border tabs, a user-changeable message (default "Rolling the Dice!"), a ratio lock on its Position tab, and both states become overlays whose background color alpha controls how much of what is behind them shows through. The hard-coded RandomSpinFallback card (black, square corners) becomes styled by these settings.

## DECISIONS (from the planning conversation)

1. Tabs on the group (after Position): TEXT, BACKGROUND, BORDER. Position keeps per-part X/Y/Width/Height/Hide and loses the file field (it moves to Background).
2. Storage is per resolution on the entries. The shared style lives on the first part (RandomGameDisplay), like the buttons' style on RandomButton; the file already lives there (ImagePath). Both states use the shared style.
3. TEXT tab: Message (new nullable entry field, default "Rolling the Dice!", shared by both states), font family + Bold, color, alignment (Left/Center/Right), "Wrap text" checkbox (shared), drop shadow (color/blur/depth), Max text size (px) stored PER STATE on each part's own entry. Reuse the generic Text*/TextShadow*/TextAlign fields for font, bold, color, alignment, shadow and the per-state size (TextFontSize on each part). Message and wrap flag are the only new fields (TextMessage, TextWrap; nullable, written only when not null).
4. Text fitting: Max text size is the cap. One line (wrap off): as large as the max allows, shrinking only if wider than the box. Wrapped: wraps at the box width at the max size, shrinking only if the block is taller than the box. Nothing overflows in either mode. Max size range 6 to 200. Message max length 60. Applies to the spin card and to wherever the group's text shows on the splash state, whichever the audit finds.
5. BACKGROUND tab: the file (Browse/Clear, png/jpg/gif/mp4, via the BrowseLayerFile helper, stored in the first part's ImagePath; PnlFileField is no longer shown for this group) and the background color (BgColorHex on the first part; default opaque black #FF000000 so existing themes look the same). No file opacity. Draw order inside the card: background color, then the file at full strength, then the text. The color's alpha controls how much of the layer behind shows through.
6. BORDER tab: color, size, radius (reuse the shared Border tab mapped to the first part's entry via GetSelectedBorderEntry, like Search & Buttons). Defaults: size 0, radius 0 (the current card is square and unbordered). The border must sit OUTSIDE any Viewbox so its size does not scale with the box.
7. Both states overlay: the splash-state card must no longer hide the Splash Image layer; it draws over it and the background alpha decides visibility. The game-selected state already behaves this way over the video preview (shows above a static preview; the video area goes black); keep that and make both states follow the same rule through the background color. Z-order is controlled by the layer list: do NOT add any z-order logic or defaults.
8. Ratio lock: session-only toggle (GlyphToggleStyle, same behavior as BtnRatioLock) between each part's Width and Height boxes on the group page. BuildGroupParts is shared, so add LayerGroupDefinition.AllowRatioLock (true only for Random Game Display), like AllowGroupMove. Search & Buttons and Game Info do not get it. Update open question 11 in themes.md accordingly (the lock exists where parts hold media; others deliberately have none).
9. Defaults live in LayerRegistry constants, set and filled by a new FillRandomGameDisplayStyle (from BuildDefaultLayout and ResolveLayout, saved values kept). Font Segoe UI, bold true, color #FFFFFFFF, alignment Center, shadow off (#FF000000, blur 3, depth 1), max size 30 for the game-selected part and 48 for the splash part, wrap false, message "Rolling the Dice!". Adjust to match the current look if the audit shows different values.
10. New fields go in CopyStyleFrom, the registry fill-ins and a new live-repaint key in ApplyLayout (_lastRandomDisplayStyleKey: every style field on both parts) that triggers MainViewModel.RefreshRandomDisplayBindings(). Forwarders: ThemeRandomDisplay* following the Game Info forwarder pattern. Do not bind flat settings.
11. No legacy migration; saved themes without the new fields fill from defaults.

## STEP 1: READ-ONLY AUDIT (report, change nothing)

Report briefly:

- How RandomSpinFallback and the two states render today: elements, parents, bindings, what the file does, how ApplyLayout positions them.
- What scales (Viewbox or otherwise) and what is hard-coded (size, font, color, text). Is the border inside any Viewbox?
- What gets hidden during a spin: does the splash-state card hide the Splash Image layer, and where is that done? Same question for the game-selected state and the video preview. What does the video do while the card shows (playing, paused, unloaded)?
- Where the spin text "Rolling the Dice!" and any other text on these cards is set, and whether the splash state shows text at all.
- How the file (ImagePath) is used by each state today.
- Whether the no-marquee card (RandomizerNoMarqueeFallback) shares markup or bindings with RandomSpinFallback (out of scope; do not merge them now).
- The existing AllowGroupMove plumbing and BuildGroupParts structure, to place AllowRatioLock and the per-part lock cleanly.
- Any conflict between the decisions above and what the code actually does, with the smallest proposed adjustment.

## STEP 2: IMPLEMENTATION (only after the user says go; one pass, in this order)

A. LayerRegistry: new fields (TextMessage, TextWrap on LayerLayoutEntry, copied by CopyStyleFrom), defaults, FillRandomGameDisplayStyle, AllowRatioLock on LayerGroupDefinition.
B. MainViewModel: forwarders and RefreshRandomDisplayBindings, called from RefreshThemeBindings and ApplyLayout's new key.
C. MainWindow.xaml(.cs): restyle the card(s) per the decisions (background, file under text, border outside any Viewbox, wrap/max-size fitting, overlay behavior on the splash state), plus the ApplyLayout key.
D. Advanced tab: TEXT and BACKGROUND TabItems (Collapsed by default, listed in AllLayerTabs and GetLayerTabs for LayerRegistry.RandomGameGroup, with the shared Border tab). Controls load when the group is selected. Handlers follow the Search & Buttons / Game Info patterns: immediate for colors, checkbox and combos; Enter or focus loss for numbers with revert; the message box applies on Enter or focus loss, trimmed, max 60, empty reverts to the default. PnlFileField hidden for this group. Per-part ratio lock in BuildGroupParts behind AllowRatioLock. Confirm Reset Layer on the group restores the new fields through CopyStyleFrom.
E. Build; expect 0 warnings and 0 errors. Update themes.md (new section for the layer, layer status table, decision entries, open questions 11 and 12, traps, file map, change log dated 2026-10-22, Last verified). Keep LF line endings.

## TEST CHECKLIST (give to the user at the end)

- Change the message and see it on a spin.
- Wrap on and off with a long message; max size per state.
- Font, color, alignment, shadow.
- Background color opaque vs semi-transparent vs transparent, over the Splash Image and over a video preview.
- A transparent PNG file under the text.
- Border color, size and radius (size stays fixed when the box is resized).
- Ratio lock on each part (and absent on Search & Buttons and Game Info).
- Reset Layer; resolution switch copies the style; theme save/load round trip.
- No regressions on the marquee spin frames or the no-marquee card.
