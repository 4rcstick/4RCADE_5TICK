# 4RCADE 5TICK — Random Video Previews
## Beta.4 implementation handoff — audit first, one pass at a time

**Status:** Approved feature design for implementation planning; technical integration **not yet audited**.  
**Destination:** Project root, beside `CLAUDE.md` and `themes.md`.  
**Feature display name:** **Random Video Previews** (21 characters).  
**Scope:** New, independently themed **splash-only** default layer. **Not Attract Mode.**  
**Authority:** Live repository code > `themes.md` > this planning document for *existing implementation facts*. This document records the user's *intended behavior*. Report any conflict before editing.

---

## 0. Operating contract — read this first

1. Read `CLAUDE.md` and `themes.md` in the actual project. Follow their permissions, line-ending, test, save, build, documentation and **never commit** rules. Do not search outside permitted directories.
2. **STEP 1 IS READ-ONLY.** Audit, produce a concise but concrete report, propose the minimum safe passes, and **STOP**. No code, XAML, configuration, JSON, docs, tests that mutate data, or generated files in Step 1. Do not begin implementation until the user explicitly says **go**.
3. After authorization, implement **one pass only**, build with exactly `dotnet build --no-incremental`, report files/diff, changes, discrepancies, tests and manual checks, and **STOP** for user review/test/commit before the next pass. Do not commit yourself.
4. For GUI/sandbox automation, follow the current authorization in `CLAUDE.md` **and pause for the user's explicit go before launching or driving any automated GUI test** so the user can get hands off keyboard/mouse. The authorization for repository edits is not permission to surprise the user with mouse/keyboard automation. Sandbox only; never launch the debug build pointed at real data.
5. No incidental refactors or improvements. Especially do not destabilize the established normal Video Preview pipeline merely to abstract shared code. Prefer narrow reusable helpers only where demonstrably safe.
6. Where this spec intentionally delegates technical detail to the audit, do not silently invent product behavior. Report a recommendation and any material tradeoff. A pass boundary may change after audit, but the feature decisions below should not be changed without asking.
7. Preserve source file CRLF (`.cs`, `.xaml`) and Markdown LF, and check `git diff --stat` for accidental whole-file rewrites.
8. **Minimize approval prompts throughout the audit and implementation.** Follow the preapproved tool and command patterns in `CLAUDE.md` wherever possible:
   - Use Read, Grep, and other approved file tools for inspection instead of shell commands.
   - Do not use shell pipelines, command chaining, `cd` prefixes, output filtering, or wrapper commands that turn preapproved operations into commands requiring permission.
   - Run `dotnet build --no-incremental` exactly as documented, as a standalone command.
   - Use the established `.\bin\GuiTest\gui.ps1 -Do <Action>` commands for authorized sandbox GUI testing, with one action per tool call.
   - Batch related read-only investigation efficiently using the available file tools, avoiding unnecessary repeated reads or permission requests.
   - Do not use Python, alternative shells, scripts, or other tools to bypass permission restrictions.
   - If an operation genuinely requires approval, explain why it is needed and request permission rather than attempting a workaround.
   - This is a workflow-efficiency requirement, not authorization to bypass safety rules, expand filesystem access, or begin GUI automation without the user's explicit go.


## 1. Feature intent and scope boundary

**Random Video Previews** is an automatic gameplay-video showcase **only while no game is selected** (the splash state). It is a full-fledged default layer in the Advanced Theme Builder: independently positionable, ordered and per-resolution themed, but its splash-only visibility is **fixed**. Its video content comes from the currently filtered *Game Pool*, limited to games with video previews.

It does **not** replace either existing layer:
- **Splash Image** already plays a chosen static/animated image or single MP4; it remains unchanged and can sit behind this layer.
- **Video Preview** shows the currently selected game's preview; it remains unchanged and is never simultaneously active with the new splash-only player under ordinary selection states.

**Future Attract Mode is separate.** Its idle timer may run on the splash screen while this layer rotates. When Attract Mode eventually fires, it will select a game and transition to the **normal selected-game layout**, displaying regular video and metadata. This new layer should disappear in that state. Attract Mode *may later* reuse eligibility/pool/order utilities, but must have independent timers and sequence state. Do not implement Attract Mode, its idle timer, or an Attract framework in these passes. Rotating previews must not count as user input/activity for a future idle timer.

## 2. Settled user-facing behavior

### 2.1 Eligible game/video pool

- Begin with the **same active Game Pool underlying the existing on-screen `GAME POOL` count**, which changes as the launcher applies filters. It could contain hundreds or many thousands. Do not confuse this count with the video-eligible count.
- Candidate set = games in that **currently filtered pool** with a video preview available under the launcher's actual existing preview-media lookup rules, including any relevant clone-parent fallback if that is what regular Video Preview uses. **Audit** the exact resolver and whether the path should be a video in the normal preview-order configuration. Never include static fallback images as eligible video content.
- Build/cache video eligibility **once on launcher startup**, then **rebuild when filters change the active Game Pool** (at the established pool-rebuild completion point, not after every edit/tick). Do **not** scan the full pool at every rotation.
- Pool recomputation must not change the Game Pool itself, its visible count, sorting/filtering, or game selection.
- On a filter update, let the **currently playing preview finish its remaining configured interval**, even if its game is no longer eligible. Then use the **new pool**. If no videos remain, clear the player after that interval. If the current game is still eligible, do not restart it or reset its timer just because the pool was rebuilt.
- A video may exist but fail at open/decode/play. Skip it and exclude that game from the **current cached eligible pool** for the remainder of that pool generation. On the next pool rebuild, it may be tried again. Respect cancellation/versioning so stale asynchronous completions cannot show an obsolete video.
- Avoid blocking the UI thread while checking potentially large pools; audit existing caches/indexes and actual file-check cost first. Any async rebuild must be versioned, atomically published, and gracefully handle a game pool change while it is still scanning. Don't add a periodic filesystem crawler.

### 2.2 Count edge cases

| Eligible video games | Required behavior |
| --- | --- |
| **0** | Show **no video content and no fallback text/image**, with no title, PLAY button or speaker icon. Leave layer content transparent so the underlying splash art/CRT off-state shows as before. |
| **1** | Play that video and loop continuously. Do **not** unnecessarily reload it on the rotation interval. |
| **2** | Both modes effectively alternate; avoid immediately repeating a game. |
| **3+** | Follow the chosen order below. |

For zero available videos, transparency takes precedence over any configured video-letterbox/background fill: the user's existing underlying theme is the fallback.

### 2.3 Playback order and timing

**POSITION & SIZE** exposes:
- **Preview duration (seconds):** default **45**, proposed integer range **5–300** (validate against existing UI conventions in audit).
- **Playback order:** **Shuffle** (default) or **Random**. **No alphabetical mode**.

**Shuffle:** Shuffle the eligible list, play every game once before reshuffling, and avoid the same game at the end/start of consecutive shuffle rounds when 2+ are eligible. The pool changes require coherent new shuffle state; preserve the current video until its interval ends, then use the newly generated sequence.

**Random:** Independently pick from eligible videos at each rotation, excluding the current one whenever 2+ are eligible. Duplicates later in the sequence are allowed; this is not shuffle-without-repeats.

- A short MP4 loops until its **preview-duration** timer expires; a long MP4 can be cut off at that timer. **Direct swap** to the next video, no requested crossfade, overlap or artificial transition. The normal Video Preview's 600 ms rapid-game-scroll debounce **must not** be copied into automatic rotation. Audit whether the existing player actually fades or simply delays before showing frames; don't assume.
- Hovering the video **pauses only the rotation countdown**; the current video **continues playing and looping**. Moving the mouse off resumes the **same remaining countdown**, not a fresh one. Repeated enter/leave must not change that behavior.
- The cursor may already be over the video when the splash becomes visible: treat it as hovered immediately, reveal controls and pause the rotation countdown without requiring mouse motion.
- If the preview duration is edited live, apply it to the **next video**; do not abruptly switch the current one or reset its countdown. Similarly, ordinary styling/positioning changes should not restart video.
- On leaving splash due to selecting a game, stop/clear this splash-only player. **Every return to splash starts fresh**: choose the next game according to Shuffle/Random, start playback at 0 and reset the countdown. This includes return after MAME exits from a PLAY-button launch. Preserve shuffle progress (unless the pool changed). No separate “resume previous” preference.
- The Theme Builder can remain open while preview runs. Theme/resolution changes should preserve playback when possible; Step 1 must map real existing behavior and determine safe paths. New theme styling and whether layer is enabled must take effect.

### 2.4 Hover interaction

On hover, **instantly** show:
1. Current game's **display title**, above the PLAY button.
2. Centered **PLAY** button, launching **the game belonging to the video presently on screen** (not the Games List selection).
3. Existing-style speaker icon in the video content's **bottom-right corner**.

On mouse leave, hide title/button/speaker instantly and resume rotation countdown. The **PLAY button stays centered in the video**, not centered within a combined title/button stack; the title occupies a constrained region above it. Reserve the speaker corner so title/padding and button cannot obscure it. The sound glyph must remain clickable. Do not let a button click cause an accidental game selection/hover change before identifying the video game to launch.

**Small video sizes:** Fit both elements if possible. If insufficient room after respecting button, sound icon, padding, min font size, and title line cap, **hide the title** and show the PLAY button (plus speaker when available). Do not allow collisions, overflow, microscopic text, or an unreachable button. For extremely tiny video sizes where even the button cannot fit, audit/recommend the graceful minimum footprint without inventing a designer-facing toggle.

The PLAY action must use the **normal existing game launch path**, preserving launch validation, workspace hiding/restoration, game logging and post-game state. It must not fake a Games List selection to display metadata as part of this layer. See audit for how to launch an explicit game safely.

### 2.5 Audio

The new player gets its **own speaker glyph**, visually/functionally equivalent to the regular Video Preview's hover-only bottom-right glyph, but **not** an independent sound preference. Its control, the regular Video Preview control, and the hamburger menu's **Preview sound** item all use the same per-install **`PreviewSoundEnabled`** setting. Off by default. No audio properties in the theme or Builder. Audit the established sound-toggle path: per `themes.md` §3.20 it restarts the current regular preview from 0:00 to apply the audio mode; align new-player behavior and refresh both glyph states without accidentally starting a hidden/blocked player.

No two active playback audio streams in normal splash/selected states. Respect the current player lifecycle (game launch, minimize with sound enabled, restore, shutdown, error/restart policies) where applicable, without changing its proven behavior.

## 3. Builder UI — proposed final tabs

**One new standard layer row:** `Random Video Previews` (not a custom layer; do not consume one of the ten custom slots). Per-resolution placement/style, normal Z ordering, saved in theme layout entries. **Fixed splash-only** visible semantics; **no Always / Selected / No Selected / Never combo**. Audit whether the ordinary Hide checkbox should be present; default to an ordinary **Hide this layer** toggle for the theme designer while never allowing selected-game visibility (if registry conventions support it safely). Report the selected mechanism at Step 1.

### Tab 1 — POSITION & SIZE

- Existing position and ratio-lock patterns: **X, Y, Width, Height**, ratio lock; same border-ring geometry as Video Preview (X/Y = outer ring top-left; Width/Height = **video content** size; border extends outward).
- Hide this layer (subject to audit of built-in layer model); **no selected-game visibility mode**.
- Divider, **Preview duration (seconds)** (default 45, 5–300 suggested).
- **Playback order:** Shuffle / Random (default Shuffle).

### Tab 2 — CONTENT

- **Background color** behind video/letterboxing, using ARGB picker/alpha, as on the existing Video Preview. **This is NOT the hover-title panel background and NOT a dimming filter over footage.** Its effect when video is absent must be transparent to expose underlying splash art.
- No file Browse/Clear (video sources come from the eligible Game Pool, not a fixed theme asset).
- This tab is intentionally small; Step 1 may recommend merging its single color field into another tab **only if the user approves**. Do not create an extra background overlay or additional controls by assumption.

### Tab 3 — TITLE

Shown **only on hover**, and automatically populated with the **current video's game display title**.

| Property | Default / behavior |
| --- | --- |
| Font family | **Segoe UI**, themeable; use upgraded family selector if implemented by then |
| Font weight | **Bold**, themeable; use upgraded weight selector if implemented by then |
| Font size / maximum | **20 px** |
| Minimum font size | **12 px** |
| Font color | ARGB picker; propose white if no established better default |
| Wrap text | **On** |
| Scale to fit | **On**; wrapping and scaling **work together**, unlike Random Game Display's single-line scale mode |
| Maximum lines | **2**, proposed allowed 1–5; irrelevant/disabled when wrap is off |
| Background color | ARGB picker: applies **only to the rectangle around title text**, not the whole video or the PLAY button; suggest semi-transparent dark neutral and verify by preview |
| Padding (px) | Uniform padding, suggested **8 px**, constrained inside video content bounds |

Text-fitting contract: available title rectangle is derived **after** reserving video boundaries, sound-glyph corner, centered PLAY button and separation, and title padding. At configured font size, wrap if enabled; scale down as needed but **never below min size**; after reaching min size, clamp to Max Lines/height and **truncate the last visible line with a true ellipsis** if content still does not fit. If wrap off, one-line shrink-then-ellipsis. If scale-to-fit off, no shrink; wrap and ellipsis still work. Text layout should be measured in the **selected font's actual metrics** (including weight) and recompute for resize/DPI. Title background grows only around final laid-out text + padding, and clips/limits to video content and its rounded inner corner. It **must not** escape outside the video frame.

**No independent TITLE border properties.** The user explicitly rejected an additional title-panel border; only the existing **video BORDER tab** and **PLAY button border** exist. No title shadow requirement. No full-video hover dimming.

### Tab 4 — PLAY BUTTON

Shown instantly on hover, **centered in the video content**. Automatic width based on label + padding, limited by video width. Font **fixed Segoe UI Bold**, scaled automatically from button height; no font family/weight control, no drop shadows.

| Property | Default |
| --- | --- |
| Button text | `PLAY`, editable, max **24** characters; action unchanged |
| Button height (%) | **10% of video content height**, designer-adjustable. Audit sensible min/max input and pixel clamps so tiny and large layers remain usable |
| Text color | `#FFFFFFFF` |
| Background | `#B4000000` |
| Border color | `#CCFFFFFF` |
| Border size | **1 px**; editor range **0–50** |
| Corner radius | **4 px**; keep familiar editor range **0–200** |
| Hover text color | `#FFFFFFFF` |
| Hover background | `#DD333333` |
| Hover border color | `#FFFFFFFF` |

Allow alpha on colors. Padding and measured text must remain inside the percentage-based button size; do not assume a 24-character label fits on a tiny display. Use natural text-fit/truncation if needed, preserve click target. No separate button file. Use existing button styling conventions where safe, but **do not inherit Global Button colors**.

### Tab 5 — BORDER

The **standard shared video BORDER tab**, for the **outer video player frame only**: border color (ARGB), border size **0–50**, corner radius **0–200**. Default border **size 0** (classroom requires none); choose a neutral default color and radius 0. Follow the existing outside-border ring, inset video, rounded clipping and guide calculations. Do not add a second video border or a title border.

### Styling/save rules

- Every new themeable field lives on the **new layer's per-resolution `LayerLayoutEntry`** (nullable where consistent), not flat `ConfigurationSettings` and not the per-install sound preference.
- Follow `LayerRegistry` defaults/fill helpers for both default layout build and layout resolution; add every field to `CopyStyleFrom`, live style-key/refresh path, resolution duplication/scaling as warranted, Reset Layer, Restore Defaults, Save Adjustments and theme zip Save Changes/load. For fonts, inspect the **actual current** font-family/weight system: the separate family and weight combo upgrade may have landed since the uploaded snapshot; never regress it to a Bold checkbox.
- Do not add fixed video paths or default assets for this player. A newly added default layer should get a sensible position/size from existing Video Preview conventions, **but no assumptions about classroom-specific geometry**. Report chosen defaults in the audit.
- Hover controls are part of the new layer's own clipped media content; video border is separate and remains visible if configured (except the no-videos transparent-content fallback; audit the intended visibility of an empty border and report any ambiguity).

## 4. STEP 1 — mandatory read-only architecture audit (STOP afterward)

**Read files; do not modify anything.** Return a report organized exactly as follows. Include concrete source files, method names and enough references to make each finding verifiable; mark assumptions, current code behavior and conflicts separately.

### A. Existing player and independent-instance feasibility

1. Trace `PreviewPlayerHost`, FFmpeg session/presenter/player lifecycle, video surface binding, frame sizing, looping and threading. Can **two independent instances** exist without shared-state conflicts, native DLL/session problems or excessive CPU/memory? Confirm whether the normal player can remain on the selected-game side as-is.
2. Map 600ms selection debounce, visual first-frame handling and the perceived “fade.” No claims that a fade exists unless code confirms it.
3. Trace audio initialization, `PreviewSoundEnabled`, speaker glyph placement/sizing/visibility and hamburger toggle, minimize audio pause/resume, error/restart/versioning, log path, `StopPreviews` / workspace / game launch cleanup. Identify the **minimum safe integration points** for a second player and shared toggle without regressions.
4. Estimate resource costs of a second player (especially concurrent hidden/background decoding); determine whether splash-only visibility fully stops/disposes the idle player when not visible. Check how the current player handles a theme switch and missing files/locked theme extract paths.

### B. Actual Game Pool and eligibility

1. Identify the exact collection that determines **GAME POOL** count, and events/calls for startup generation and filter-apply rebuilds. Distinguish working list, cached ROM metadata, display TreeView and any non-filtered source. Verify `GameItem` identity and launchable game object.
2. Trace `ResolvePreviewMedia`, preview priority/fallback, clone-parent resolution, per-game video path selection; identify precisely what “has video” means. Do **not** mistake static fallback images or non-video preview slots for a valid clip.
3. How to build eligible video list efficiently at thousands/30k+ games without UI freeze? Check available indexes/path caches, duplicate games, stale async scans, file availability and filter changes during playback.
4. Identify safe explicit-game launch entry point for the PLAY button (normal launch validations and lifecycle), without forced selection causing unwanted UI state change.
5. Design an independently owned shuffle/random sequence with pool revisions, 0/1/2 counts, video failures, end-of-cycle no-repeat and persistence of sequence position through splash return.

### C. Layer, layout, Theme Builder and persistence

1. Identify the exact files/classes and conventions to register a **new default layer** (`LayerRegistry`, `ThemeLayout`, entry fields, group/order/Z, MainWindow layer control, `ApplyLayout`, live bindings/keys).
2. Verify splash-state visibility/selection transitions, Splash Image layering, hover hit-testing and mouse pointer already inside an activated layer; ensure the new layer doesn't block unrelated controls beyond its visible rectangle.
3. Inspect the per-resolution outside-border ring, positioning helpers and guide bounds; clipping, video letterbox fill and title overlay clipping.
4. Map advanced tab list, property tab registration, shared Border tab, field validation, themeable font family/weight current status, defaults/scaling, reset/restore and copy/paste capability.
5. Map theme save/load safety and bundling (`theme.json`, settings.json, restore points), including adding entries to existing themes, missing-default behavior and `DefaultLayerSets.json`/Restore Defaults. Avoid accidentally embedding video media into theme zips.
6. Confirm layout/theme switch playback behavior and whether changing resolution should re-create a player. Map builder live edit without restarts, closing without save, and modeless workspace behavior.

### D. Visual / behavioral feasibility

1. Propose exact geometry for centered PLAY button, reserved sound-icon area, title max rectangle and dynamic hide-on-small-screen threshold.
2. Recommend reliable WPF text measurement/layout to combine **wrapping + shrink-to-min + line cap + final ellipsis** with font family/weight, margin/padding and rounded clip. Explain how you will avoid clipped glyphs and avoid recalculating on each video frame.
3. Confirm the difference between: video/letterbox background (CONTENT), title-only ARGB panel (TITLE), PLAY button fill, and **one video frame border**. No whole-video hover dimmer and no title-panel border.
4. Identify whether any design property/default/range is technically incompatible with established editor conventions. Present small, specific decisions for the user if unavoidable; do not silently substitute another feature.

### E. Audit deliverable and pass proposal

Return:
- **Verified findings** vs **assumptions/unverified items**, with source locations.
- **Risks** rated high/medium/low and mitigation (particularly player isolation, threading, file locks, responsive UI and compatibility with current preview sound).
- **Exact anticipated files** by pass (not a blanket list without purpose).
- Proposed **minimum safe implementation passes** and the acceptance gate/checklist for each. Aim for **3–4 implementation passes if evidence supports it**, not an arbitrary six. Don't combine incompatible high-risk work just to lower the count.
- Any unavoidable user decisions or spec/code contradictions, without implementing them.
- Explicit ending: **`STEP 1 COMPLETE — WAITING FOR USER GO FOR PASS 1`**.

## 5. Suggested passes — AUDIT MAY REFINE THESE

These are **provisional**, not authorization to proceed. One pass at a time, with user test/commit between passes.

### Pass 1 — Layer foundation + persistence + visual frame

Register `Random Video Previews`; add per-resolution style fields and defaults; safe layout placement, Z and splash-only gating; shared video border, optional letterbox background and transparent no-video content; hook into layout/guide/restore/size scaling and Save Changes/Adjustments/Load. Build/verify no impact on existing players. A dummy/empty media surface is acceptable here; do not sneak in a full playback engine.

**Gate:** compiles, appears in layer list, per-resolution placement/border and splash-only display correct; no overlap with Video Preview when game selected; save/reload/reset/close-revert work; existing theme files remain safe.

### Pass 2 — Pool and playback core

Use actual filtered Game Pool and video resolver; async/cached eligibility built on startup/filter refresh; 0/1/2 counts; Shuffle/Random, rotation timing, hover pause/resume, fresh-on-return, failure skipping, clean shutdown, minimize and selected-game transitions. Independent FFmpeg player/audio coupling only as audit recommends; keep normal player behavior unchanged. Include direct-swaps and per-player logging/diagnostics as appropriate.

**Gate:** startup with 0/1/2/many, filtered change mid-video, corrupted clip, repeat avoidance and timing; no UI freeze, no unnecessary debounce, no leaked decoder/file handles; sound-off silent and sound-on operational; no overlap with selected-game player.

### Pass 3 — Hover title, PLAY and shared sound UI

Hover detection (including stationary mouse), instantly show/hide current title and centered PLAY, strict layout bounds, wrap/scale/min/max-lines/ellipsis and small-screen fallback, title ARGB/padding; themed button styling, explicit game launch and normal return-to-splash; mirrored sound icon and globally synchronized toggle/hamburger. Keep separate from selected-game player styling.

**Gate:** long titles in different fonts and at tiny/large/4:3 sizes, speaker hit-test clearance, button launch of **displayed** game and post-game fresh preview; no phantom clicks; global sound toggle from all three entry points.

### Pass 4 — Builder tabs, polish and regressions (only if not safe to combine earlier)

Wire POSITION & SIZE, CONTENT, TITLE, PLAY BUTTON, BORDER fields into live editor and serialization, exact defaults, reset/restore, browse-free content; test 720p/1080p/non-16:9, DPI/fullscreen, theme/resolution switching, builder stay-open, saving/closing, perf/memory and launcher gameplay lifecycle. If earlier passes already included UI, use this for focused tests/polish, **not** unrelated refactors.

**Gate:** full feature acceptance checklist below, zero build errors/new warnings, complete `themes.md` documentation updated for implemented behavior, and a tight final diff.

## 6. Acceptance checklist (test across passes; do not claim unrun tests)

- [ ] New default **Random Video Previews** row; not a custom slot; splash-only regardless of layer Z or stored theme data; no selected-game overlay.
- [ ] Title displays the correct ROM's **display title** on hover; instantly disappears on leave; stationary cursor recognized on splash entry.
- [ ] Hover **pauses rotation countdown but does not pause the MP4**; resume uses exact time remaining.
- [ ] Configured 45 sec default, edits apply from next preview; MP4 loops within interval; instant swap without added effect/debounce.
- [ ] Startup and filter rebuild use same Game Pool as count; no static fallback eligibility, no scanning every timer tick, responsive at large pool sizes.
- [ ] 0: transparent fallback showing theme behind; 1: continuous loop; 2: alternating; Shuffle exhausts pool before repeat, Random may revisit except immediately.
- [ ] Mid-preview filter update finishes existing interval, then uses updated pool; if empty, clears content; stale rebuild doesn't override latest pool.
- [ ] Bad video excluded for current pool generation, retried after rebuild; no crash, tight retry loop, or stale frame.
- [ ] Returning to splash from selection or game launch starts next Shuffle/Random game at 0 with fresh timer, not previous playback.
- [ ] Title fits 1-line or up to max wrapped lines, shrinks only to min, truncates ellipsis; never overlaps center PLAY / bottom-right sound / content border; hides on very small videos.
- [ ] Title-only ARGB background and padding constrained inside frame; **no title border**, **no whole-video hover dimmer**.
- [ ] PLAY button size percentage editable; fixed Segoe UI Bold, editable text and colors/hover/border; no shadows; launches exactly the video game via normal path.
- [ ] Outer video Border tab follows existing ring geometry; zero border works for classroom TV; other themes can add border.
- [ ] New speaker icon bottom-right; regular video icon and hamburger preference all synchronized; no separate theme-level sound setting, no competing sound.
- [ ] Existing Video Preview, Splash Image, Random Game Display, randomizer, wallpaper MP4, game launch and theme browser remain unchanged in behavior.
- [ ] Theme Builder live properties update without restarting active MP4 for mere styling/position; resolution/theme switch correctly rebinds without leaked files or losing save integrity.
- [ ] Per-resolution fields survive CopyStyleFrom, new resolution creation/scaling, Reset Layer, Restore Defaults, Save Adjustments, Save Changes/Load, builder close-without-save and theme switch.
- [ ] Test 1280x720 and 1920x1080; small content rectangles, long titles, 4:3, fullscreen DPI and shadowed/rounded video clipping; inspect actual physical rendering.
- [ ] Minimize/restore (sound on/off), game start/finish, shutdown, theme unload/error cleanup do not leak a player or hold a theme-extracted MP4 open.
- [ ] `dotnet build --no-incremental`: zero errors, no new warnings; `git diff --stat` sensible, CRLF/LF rules upheld; **no commit by Opus**.

## 7. Deliberate exclusions / traps

- **No Attract Mode or idle trigger**; no automatic navigation to selected-game metadata from this feature.
- No alphabetical playback mode; no per-theme sound toggle or audio settings; no user-chosen fixed video file for this layer.
- No video crossfade, fade-in setting or selection debounce; no fallback placeholder text/image; no title shadow, title border or additional full-screen dimming overlay.
- No separate title X/Y/W/H, no hidden extra border/property tab. Title panel sizes from final rendered text + padding within video content limits.
- Do not replace or refactor the existing normal Video Preview just to share playback internals. Audited safe reuse beats premature generic infrastructure.
- Do not force an existing theme to add user media files or silently alter other default layer identities/visibility.
- Do not treat an open Theme Builder as a reason to stop the new preview; do not accidentally persist unsaved builder edits via hamburger/sound or unrelated settings writes.
- Respect the repo-specific GUI sandbox permissions. **Ask for go before actually driving GUI tests**, as required above.

## 8. Completion reporting format for every authorized pass

1. **Scope performed** — what was and wasn't done.
2. **Files changed** — each file with reason; any departure from the approved spec.
3. **Build** — full output summary from exact `dotnet build --no-incremental`, errors/warnings.
4. **Tests actually run** — commands/results, differentiate code inspection, automated tests and manual verification still needed.
5. **Risks/open questions** — including anything needing user judgment.
6. **User test checklist + STOP** — do not start next pass, edit spec or commit without explicit instruction.

After the last implementation pass, synchronize the relevant `themes.md` sections and decision/change log with **verified code**, not aspirational claims. End with the real test results and the user-facing configuration/defaults.
