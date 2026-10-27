# Smart fullscreen consistency pass

Do not commit. Keep CRLF in .cs/.xaml and LF in .md. The code is the source of truth, then themes.md. Build when done.

## Goal

The two ways into fullscreen must not break each other, in any order, from the hamburger Layout size menu and from the Theme Builder (Theme sizes buttons, size dropdown), with the Builder open or closed:

1. the manual toggle (F11 / hamburger Toggle Fullscreen)
2. a size pick (which can auto-enter)

Result of the audit (states W windowed, M Smart entered by F11, A Smart auto-entered, P plain fullscreen): P is a dead end (D1), a hamburger pick with the Builder open drops A to P (D2), a pick in M does nothing visible (D3), a Builder pick in M drops to P (D4), an unsaved exact-fit dropdown size flashes fullscreen and back (D5), closing the Builder without saving after a Builder auto-enter leaves the picked size showing (D6).

## Rules to implement (user decision: option C + D1 + D2)

**R1. Every size pick writes the active size**, in every state, hamburger and Builder alike. Remove the manual-Smart early return in HamburgerLayoutSizeItem_Click (the "F11 out returns to exactly the old size" rule is dropped on purpose). Leaving an F11-entered fullscreen stays at the picked size.

**R2. After a pick in M or P (fullscreen, preference on), show the picked size in Smart fullscreen at its own best whole-number scale** (the size test limited to that one size: Choose with a single-element list, scale n = floor(min(screenW/w, screenH/h)), transform n / dpiScale, centered with black bars unless exact). If the size does not fit at n >= 1, stay in plain fullscreen at the picked size. Smart fullscreen then shows the size the user picked, not necessarily the one covering the most screen. Implement as a session-only "pinned to active size" state (or equivalent), cleared by every manual toggle and by EndSmartFullscreen from any cause, like `_smartFullscreenAutoEntered`. The display override stays session-only (never written to the active size, Layouts, SavedThemeLayouts, snapshots or theme.json).

**R3. Theme Builder visible:** Smart fullscreen candidate sizes are only the active size (the Builder edits it). This replaces the "refuse and drop to plain" rule in ApplySmartFullscreen and the mismatch drop at the top of ApplyLayout where it only protects the old rule. Keep whatever part of that check is still needed (for example a theme load that changes the active size while Smart fullscreen shows another one). Check D5 is fixed: an exact-fit size that is not in SavedThemeLayouts must not maximize and exit again; a size with a working layout at the active size is a valid Smart layout.

**R4. A (auto-entered) keeps its current rules:** an exact fit reruns the size test; anything else drops to windowed at the picked size with `_sizeBeforeAutoEnter` cleared. That must also work with the Builder open (D2).

**R5. D1: plain fullscreen with the preference on is not a dead end.** A size pick, a theme switch, and turning the preference on while in fullscreen must enter Smart fullscreen (as M, not A, so nothing auto-drops it) when the size test fits. In HandleLayoutSizePickForFullscreen and HandleThemeSwitchForFullscreen, replace the "not Smart -> return" with entering Smart fullscreen; do the same in the preference-on branch of HamburgerSmartFullscreenMenuItem_Click.

**R6. D2:** the hamburger pick and the hamburger theme switch must go through the same `_deferBuilderDrop` protection as the Builder picks. Generalize RunBuilderSizePick into a general size-pick wrapper (rename if cleaner) and use it in HamburgerLayoutSizeItem_Click; set the defer around the theme switch path (LoadTheme) when `_smartFullscreenAutoEntered`. If R3 makes the ApplyLayout drop unnecessary in these paths, say so and remove dead code instead.

**R7. D6 (only if trivial):** on Builder close, if Smart fullscreen is active and its layout no longer matches what should be shown after the restore, rerun the size test. Otherwise leave it and say so.

## Do not change

Menus, popups, tooltips, picker dropdown placement, WindowFit, the Layout size check mark logic (it must keep showing the displayed size and follow both toggles and picks), Custom Add, Remove Size, Load Theme semantics.

## Docs

Update themes.md 3.17/3.19 (the Theme Builder refinement bullet, the Layout size pick bullets, manual Smart picks, decision 63 wording if it describes the old rules), the change log, and the CLAUDE.md "Auto-entered flow" gotcha (new pinned state, wrapper name). Remove statements that are no longer true (for example "an F11-entered fullscreen is not changed by a pick").

## Report

Short: what changed per file, the final shape of the size-pick wrapper and the pinned-size state, the state table after the change (W, M, A, P with Builder open and closed: toggle result, hamburger pick result, Builder pick result), anything in this spec that the code contradicted, and a test list. Also confirm no code path writes the pinned size anywhere persistent.

## Test list (preference on, run each with the Builder closed and open)

1. Windowed, F11, then pick other saved sizes from the hamburger: each one shows (Smart at its own scale, or plain if it cannot fit); check mark follows.
2. Same with the Builder buttons and the Builder dropdown.
3. Windowed, pick an exact-fit size: auto-enters (A). From A pick a non-fitting size: back to windowed at that size. From A pick another exact fit: stays.
4. In A, F11: back to windowed at the previous size. In M, F11: windowed at the current active size.
5. Plain fullscreen (no saved size fits, or entered with the preference off then turned on): a size pick or theme switch enters Smart.
6. Theme switch from the hamburger in M, A, P.
7. Open/close the Builder while in M and A; close without saving after a Builder pick.
8. Sizes at 100/125/150/175 Windows scale still look right; menus, popups, tooltips still scale.
