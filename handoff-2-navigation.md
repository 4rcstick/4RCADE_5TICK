# Handoff 2 of 6: Navigation (Theme Browser prep)

## Kick-off prompt (paste this into Claude Code)

```
Read handoff-2-navigation.md in the project folder completely, then CLAUDE.md and themes.md (sections 3.4, 3.17 to 3.19 and 7 at least).

Follow the file's pass structure: do the read-only STEP 1 check first, report any mismatch, then STOP and wait for me to say "go". Only after "go" do STEP 2 (the edits). Do one pass only, stop after it, and do not commit. Do not start any other Theme Browser work (no catalog, no theme loading, no pop-out).
```

---

## 1. Goal and why

The Theme Browser will live as the THEME BROWSER tab of the existing Theme Builder window. This pass builds only the **navigation plumbing** and a **placeholder** so later handoffs (catalog, load, visuals, pop-out) have somewhere to land:

- The hamburger menu gets a **Theme Browser** item next to **Theme Builder**.
- The shared window can be opened or activated **on a chosen tab**. Never a second window.
- Hamburger **Theme Builder** and **Ctrl+T** open or activate it on the **Builder** tab. Hamburger **Theme Browser** opens or activates it on the **Browser** tab.
- The empty `ThemeBrowserTab` Grid becomes a small **placeholder control** with a **refresh hook** that runs whenever the Browser tab is shown (including when the window opens directly onto it, and when the hamburger item is chosen while the window is already on the Browser tab).

Settled decisions (do not re-open):
- No keyboard shortcut to open the Browser. Ctrl+T stays the Builder shortcut and always lands on the Builder tab.
- Switching tabs, opening the Browser tab and refreshing must **never load a theme, save settings, or discard unsaved Builder edits**.
- Opening the window onto the Browser tab uses the same Smart fullscreen adopt/restore logic as opening it onto the Builder tab, unchanged.
- The window stays 717 x 900, with the same tabs in the same order (THEME BUILDER, THEME BROWSER, GLOBAL) and the same headers.
- Compare tabs by `Content` (never by index), as `CLAUDE.md` requires.

## 2. Files involved (verify names and line areas; the code wins)

- `MainWindow.xaml.cs`: `OpenThemeBuilderWindow` (around line 3711 at audit time), the Ctrl+T handling (around line 2880), the hamburger click handlers, `PopulateHamburgerSubmenus` (around line 3493)
- `MainWindow.xaml`: the `HamburgerContextMenu` resource (around line 160), the Theme Builder item (around line 171, icon `&#xE790;`, `InputGestureText="Ctrl+T"`)
- `ThemeBuilderWindow.xaml` / `.xaml.cs`: the TabControl `ThemeBuilderTabControl`, `TabThemeBuilder`, the placeholder `<Grid x:Name="ThemeBrowserTab"/>`, the tab `SelectionChanged` handler
- New: `Views/ThemeBrowserControl.xaml` and `.xaml.cs` (placeholder)
- `themes.md`, `CLAUDE.md`

## 3. Current behavior (from the audit)

- `OpenThemeBuilderWindow` creates a **modeless** window (`Show()`, `Owner = this`), only one at a time. If it exists, it restores it from minimized, calls `Activate()` and returns. The Smart fullscreen adopt runs **before** the window is created (`SwitchActiveLayoutSize(..., persist: false)`, then `SetSizeRestorePoint`), and a fallback rerun happens after `Show()`.
- Ctrl+T calls the same method. The hamburger Theme Builder item sits in the XAML resource `HamburgerContextMenu`; the Themes and Layout size submenus are rebuilt in code in `PopulateHamburgerSubmenus`. Other hamburger handlers defer through `Dispatcher.BeginInvoke`.
- `ThemeBuilderWindow`'s tab `SelectionChanged` handler ignores events bubbling from inner controls (`e.OriginalSource` must be the TabControl), then compares the selected tab's `Content` to `ThemeBuilderPage`. On THEME BUILDER it calls `AdvancedTab.RaiseGuides()` and `RaiseGrid()`. On any other tab it hides the guides, hides the Background grid and calls `AdvancedTab.StopPreviews()`.
- A WPF TabControl keeps only the selected tab's content in the visual tree. `SelectionChanged` does **not** fire when the already-selected tab is "selected" again, so the hamburger Theme Browser item chosen while the window is on the Browser tab needs an explicit refresh call.
- No code references the `ThemeBrowserTab` Grid name (audit). The Escape key is not handled anywhere in this window.

## 4. STEP 1: read-only check (do this first, then STOP and wait for "go")

Do not edit anything in this step. Report, briefly:

1. The exact current `OpenThemeBuilderWindow` (both branches: already open, and new window), who calls it (Ctrl+T, the hamburger item, anything else), and where `_themeBuilderWindow` is set and cleared.
2. Whether selecting a tab programmatically **before** `Show()` is safe: what the `SelectionChanged` handler touches (`Owner as MainWindow`, `AdvancedTab`, guides) and whether `Owner` is set at that point. If it is not safe, say what is.
3. How the hamburger Theme Builder item is defined and wired (`Click` handler name and body, whether it defers with `Dispatcher.BeginInvoke`, any `IsEnabled`/visibility logic, any state that disables it), and how `PopulateHamburgerSubmenus` relates to the static items around it.
4. How Ctrl+T is handled (key handling location, any toggle/close behavior, any condition that blocks it).
5. The `ThemeBuilderWindow.xaml` tab definitions: the `TabItem` for THEME BROWSER (any `x:Name`, header template), and confirm its `Content` can be a `UserControl` instance so it can be compared with `ReferenceEquals`.
6. Which Segoe MDL2 Assets icon approach the hamburger items use (glyph text in a header/icon element, resource, or style), so the new item matches.
7. Any mismatch between this file or `themes.md` and the code.

Then stop. Edits start only after the user says "go".

## 5. STEP 2: the edits (only after "go")

### 5.1 Placeholder control `ThemeBrowserControl` (new, `Views/ThemeBrowserControl.xaml` and `.xaml.cs`)

A UserControl in the project's usual namespace (`ArcadeStick.Views`), with `[SECTION: ...]` fencing and a one-line `// Does X` comment per method.

- Content: one wrapping `TextBlock` using the **Global Standard Text** resources (`SetResourceReference` / `DynamicResource` for size and color, as `ThemesManagerControl` does for its prompt label; no implicit styles, no new brushes) that reads `Theme Browser placeholder. Refreshed N times.`
- `public void RefreshCatalog()`: increments a private counter and updates the text. A comment states this is a **temporary placeholder** for the real catalog refresh, replaced in handoff 3, and that the counter exists only so navigation can be tested.
- Nothing else: no catalog, no thumbnails, no loading, no persistence, no events.

### 5.2 `ThemeBuilderWindow.xaml`

- Replace `<Grid x:Name="ThemeBrowserTab"/>` (and its comment) with `<views:ThemeBrowserControl x:Name="ThemeBrowser"/>` inside the same TabItem.
- Give the THEME BROWSER `TabItem` an `x:Name="TabThemeBrowser"`, next to the existing `TabThemeBuilder`.
- Keep the header text `THEME BROWSER`, tab order and everything else unchanged. Update the section comment above the tab pages (it currently says the Browser is an empty placeholder).

### 5.3 `ThemeBuilderWindow.xaml.cs`

- Add `public void ShowBuilderTab()` and `public void ShowBrowserTab()`. Each selects its tab by setting `ThemeBuilderTabControl.SelectedItem` to the named `TabItem` (never by index).
- **Refresh rule:** `RefreshCatalog()` runs **exactly once** each time the Browser tab becomes shown:
  - In the `SelectionChanged` handler (after the existing guards), when the selected tab's `Content` is `ThemeBrowser` (compare with `ReferenceEquals`), call `ThemeBrowser.RefreshCatalog()`. Keep the existing hide-guides / hide-grid / `StopPreviews` behavior for non-Builder tabs exactly as it is.
  - In `ShowBrowserTab()`: if the Browser tab is **already selected**, call `ThemeBrowser.RefreshCatalog()` explicitly (no `SelectionChanged` will fire); otherwise just select it and let the handler refresh. Never both.
  - The window opened directly onto the Browser tab must therefore show "Refreshed 1 times" (not 2, not 0).
- `ShowBuilderTab()`: selects the Builder tab; if it is already selected do nothing extra (the Builder's own tab-selected logic already runs on selection).
- Add a one-line summary comment to each new method. Do not touch `RefreshAfterExternalThemeSwitch`, `RefreshAfterExternalSizeSwitch`, `RefreshAfterThemeLoad`, the restore points, the Closed handler or the Save Adjustments code.

### 5.4 `MainWindow.xaml.cs`: open on a tab

- Add a small enum (suggested: `ThemeBuilderStartTab { Builder, Browser }`, placed where Claude Code sees fit, public or internal as needed) and change the signature to `OpenThemeBuilderWindow(ThemeBuilderStartTab startTab = ThemeBuilderStartTab.Builder)`.
- **Already-open branch:** after the existing restore-from-minimized and `Activate()`, call `ShowBuilderTab()` or `ShowBrowserTab()` on the existing window.
- **New-window branch:** after the window is constructed and `Owner` is set, and **before** `Show()`, call `ShowBuilderTab()` or `ShowBrowserTab()` (only if STEP 1 confirmed this is safe; otherwise follow what STEP 1 reported and tell the user).
- The Smart fullscreen adopt, `SetSizeRestorePoint`, the after-`Show()` fallback rerun and `Closing`/`Closed` handling stay **exactly as they are**.
- Ctrl+T and the hamburger Theme Builder item call it with the Builder tab (explicitly, or via the default). Ctrl+T while the window is open on the Browser tab therefore switches it to the Builder tab; while it is already on the Builder tab it only activates, as before.
- Add the one-line summary comment update for the method.

### 5.5 Hamburger menu: Theme Browser item

- In `HamburgerContextMenu` (`MainWindow.xaml`), add a static `MenuItem Header="Theme Browser"` **immediately after** Theme Builder, using the same icon approach as its neighbors with a fitting Segoe MDL2 Assets glyph (candidates: `&#xE8B9;` or `&#xE91B;`; verify the glyph renders and report which you used), **no** `InputGestureText`, and a `Click` handler.
- In `MainWindow.xaml.cs`, add the handler next to the Theme Builder one, following the same pattern (including deferring through `Dispatcher.BeginInvoke` if the neighbors do) and calling `OpenThemeBuilderWindow(ThemeBuilderStartTab.Browser)`.
- **Edit order (to avoid a transient build error):** add the code-behind handler first, then the XAML item that references it.
- Do not change the Themes or Layout size flyouts or `PopulateHamburgerSubmenus`.

### 5.6 Documentation (end of pass)

- `themes.md`:
  - Where the Theme Builder window and its tabs are described (section 2 / 3.4 / section 7): the THEME BROWSER tab now hosts `ThemeBrowserControl` (named `ThemeBrowser`; the old `ThemeBrowserTab` Grid is gone, update the tab trap note that mentions that name), the tab selection API (`ShowBuilderTab`, `ShowBrowserTab`), the refresh-once rule, and that the control is still a placeholder.
  - Hamburger and Ctrl+T: Theme Builder and Ctrl+T open or activate the Builder tab; Theme Browser opens or activates the Browser tab; one window only.
  - File map: add `Views/ThemeBrowserControl.xaml/.cs`.
  - Status table row for "Theme Browser (top-level tab)": from "Not started" to "Navigation and placeholder only".
  - Change log: one dated entry (2026-11-08) listing files changed.
- `CLAUDE.md`: in "Windows and views" and "Theme Builder Status", change the sentence that says THEME BROWSER is an empty Grid placeholder to say it hosts the placeholder `ThemeBrowserControl`; add one short bullet to the Theme Builder window traps: open or select tabs through `OpenThemeBuilderWindow(startTab)` / `ShowBuilderTab` / `ShowBrowserTab`, never by index, and the Browser refresh runs once per show.
- Keep `.md` files LF and `.cs` / `.xaml` CRLF as `CLAUDE.md` describes; check `git diff --stat` shows no whole-file change.

## 6. Constraints and things NOT to touch

- No catalog reader, no zip reading, no thumbnails, no list UI, no resolution buttons, no theme loading or persistence, no pop-out or overlay, no new Chrome Palette brushes, no new Global or Theme Builder properties.
- No keyboard shortcut for the Browser. Do not change Ctrl+T's key binding or the Theme Builder item's `InputGestureText`.
- Do not change Smart fullscreen logic, `SetSizeRestorePoint`, the Closed/Closing handlers, `BringToFrontAfterChildClose`, Save Adjustments, or any `RefreshAfterExternal*` / `RefreshAfterThemeLoad` method.
- Do not change tab order, tab headers, window size, or other tabs' content.
- Do not touch the separate Theme Builder label task ("Theme sizes:" / "Resolution:").
- Keep comment fencing and the one-line `// Does X` summary on each method. No unrelated fixes bundled in.
- Never commit.

## 7. Verification

You (Claude Code):
1. `dotnet build` finishes with **0 errors and 0 warnings**. The only transient error to expect: the hamburger XAML item referencing a handler that does not exist yet (follow the edit order in 5.5).
2. Grep: no remaining references to `ThemeBrowserTab` in code or in `CLAUDE.md` / `themes.md` (except the change-log history if it must keep the old name).
3. `git diff --stat` shows only the files in section 2 (plus the docs), with no whole-file line-ending changes.

The user (cannot be done without running the app; do not run any screen-capture or input scripts yourself):
1. Hamburger -> Theme Builder: the window opens on the **Builder** tab.
2. Close it. Hamburger -> **Theme Browser**: the window opens on the **Browser** tab, placeholder says "Refreshed 1 times".
3. With the window open on the Builder tab, hamburger -> Theme Browser: the **same** window switches to the Browser tab and comes to the front (no second window); the counter goes up by exactly 1.
4. With the Browser tab showing, hamburger -> Theme Browser again: it stays, the counter goes up by exactly 1 (not 2).
5. With the Browser tab showing, hamburger -> Theme Builder, and Ctrl+T: both switch to the Builder tab; guides and the Background grid return as usual. With no window open, Ctrl+T opens it on the Builder tab.
6. Minimize the window, then use either hamburger item: it restores and shows the right tab.
7. In the Builder, make an unsaved edit (for example move a layer), then go Builder -> Browser -> Builder both by clicking tabs and by hamburger items: the edit is still there, `settings.json` is unchanged (check its timestamp), and nothing was loaded.
8. Click tabs Builder -> Browser -> Global -> Browser: the counter increments only when the Browser tab is shown. The Global tab and the Builder tab work as before.
9. In fullscreen (F11, Smart fullscreen on), opening the window onto either tab behaves as it did before this pass (no change in size or scale).
10. Close the window without saving: unsaved edits are reverted as before.

## 8. Stop conditions

Stop and report (do not push on) if any of these happen:
- STEP 1 shows `OpenThemeBuilderWindow`, Ctrl+T or the hamburger menu working differently from section 3 in a way that needs more than the edits in section 5 (for example, Ctrl+T toggles or closes the window).
- Selecting a tab before `Show()` throws or misbehaves, and a safe alternative is not clear.
- The hamburger menu cannot take a static item next to Theme Builder without changing how the submenus are populated.
- Any edit would need a catalog, loading, persistence or pop-out change, a new property, or a change to Smart fullscreen or theme load/save code.
- The build shows errors or warnings you cannot clear within the files in section 2.

After STEP 2, report in this form: files changed, anything that did not match these instructions, anything changed beyond them, and the test checklist (section 7). Then stop. Handoff 3 (catalog reader and bare list) is a separate file, written only after the user reports back and the Theme Builder label task is done.
