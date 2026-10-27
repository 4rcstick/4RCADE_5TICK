# Smart fullscreen: entry size, Builder open, exit

Do not commit. Keep CRLF in .cs/.xaml and LF in .md. Code is the source of truth, then themes.md. Build when done.

## Bugs (preference on, 1080p monitor, theme with 720 + 1080, launcher windowed at 720)

1. Press F11 (Smart shows 1080, active size still 720, the hamburger check mark shows 1080). Open the Theme Builder: the launcher switches to a centered 720. Expected: opening the Builder changes nothing on screen.
2. Continue: click the 1080 button in the Builder (active becomes 1080), Save Adjustments, close the Builder, F11: it leaves fullscreen but the window stays at 1080 windowed (taller than the work area). Root cause (audit): an F11 entry remembers no "size before"; any change of the active size while fullscreen leaves F11-out at the new size. Save Adjustments only keeps the 1080 alive past the Builder's close.

## Decisions (user)

- D-A. Every fullscreen entry (F11, hamburger Toggle Fullscreen, auto-enter) remembers the active size at entry. F11 out returns to that size when the theme still has a layout at it (working or saved) and it differs from the active size, with or without Save Adjustments. This extends the auto-entered restore rule to all fullscreen. The "unfit" size picks made while fullscreen keep the rules already built: an exact fit stays Smart, any other size goes back to windowed at the picked size (and the remembered size is cleared for that case, as now).
- D-B. Opening the Theme Builder while Smart fullscreen is active and the shown size differs from the active size: make the shown size the active size first, directly (not through RunSizePick, because that drops to windowed on non-exact fits such as 1080 at 2x on 4K), BEFORE the Builder window is created, so the Builder's close restore point and the main window start consistent and nothing changes on screen. Use a variant of SwitchActiveLayoutSize that does NOT write settings.json (only Save Adjustments / Save Changes write). Remember the previous active size as the entry size (D-A) so F11 out returns to it. The ApplySmartFullscreen / ApplyLayout Builder rules stay for every other case.
- D-C. Closing the app while a fullscreen with a remembered entry size is active saves the remembered size (extend PersistSizeBeforeAutoEnterOnClose beyond the auto-entered flag, with a separate flag or condition if needed) so the next launch is not oversized.
- D-D. Smaller gaps from the audit, fix them too:
  - Builder Load Theme during an auto-entered fullscreen: apply the same drop as the hamburger theme switch (HandleThemeSwitchForFullscreen).
  - Builder Closed path: rerun the Smart test when the preference is on and the window is fullscreen, not only when Smart is active (a plain fullscreen left behind by a Builder custom size Add / Remove Size must come back to Smart).
  - Builder Closed changes the active size back to the restore point: clear `_smartFullscreenAutoEntered` / the remembered size when that changes the active size, so no flag points at a size that is no longer active.

## Constraints

- The display override stays session-only: never written to the active size, Layouts, SavedThemeLayouts, snapshots or theme.json. The adopted size in D-B is a real active-size change but is in-memory only until a save.
- Entry-size memory is cleared by every manual toggle and by EndSmartFullscreen from any cause, like `_smartFullscreenAutoEntered`; a later size pick that changes the active size keeps the rules built in the consistency pass (exact fit stays Smart, anything else windowed at the picked size).
- Do not change menus, popups, tooltips, the picker dropdown, WindowFit, the Layout size check mark logic, Custom Add / Remove Size semantics.

## Docs

Update themes.md 3.17 / 3.19 (entry size, F11 out, Builder open adopts the shown size, Closed paths), decision 63 wording if it states old rules, the change log, and the CLAUDE.md "Auto-entered flow" gotcha (the entry-size rule, the non-saving adopt, the app-close save). Remove statements that are no longer true.

## Report

Short: what changed per file; the final shape of the entry-size state and where it is set, cleared and used; the state table after the change (windowed, F11 Smart, auto-entered, plain; Builder open and closed; what F11, a hamburger pick and a Builder pick do); anything the code contradicted; confirm nothing here writes settings.json except the existing save paths and the app-close save.

## Test list (preference on, 1080p, theme with 720 + 1080)

1. Windowed at 720, F11, open the Builder: screen unchanged (1080), Builder shows 1080 as the active size, hamburger check mark 1080. Close without saving, F11 out: back to a 720 window.
2. Same, but Save Adjustments before closing, then F11 out: back to 720, not an oversized 1080.
3. Same, but with a Builder size button click in between (1080 button and 720 button), then F11 out.
4. Windowed at 720, F11, hamburger pick of 1080, F11 out: back to 720. Pick 720 in fullscreen: windowed at 720.
5. Auto-enter by a size pick still returns to the previous size on F11 out.
6. Hamburger theme switch and Builder Load Theme in F11 and auto-entered fullscreen.
7. Quit the app in an F11 fullscreen after the Builder adopted 1080, relaunch: windowed at 720, not oversized.
8. Builder custom size Add / Remove Size to a size bigger than the monitor, close the Builder: Smart returns.
9. Windows scales 100/125/150/175 still look right.
