# Handoff 4: Final pass before commit (CLAUDE.md sync + three stale comments)

## Goal and why
Project: 4RCADE 5TICK, a C#/WPF MAME frontend. The Theme Builder tweaks are done, verified by eye, and `themes.md` is updated. This is the last pass so everything lands in ONE commit: (A) sync `CLAUDE.md` with the new conventions, and (B) fix three code comments in `App.xaml` that are now inaccurate. **Comment text only in `App.xaml`: no functional change of any kind.** Do not commit.

## Part A: CLAUDE.md edits
Find the right place for each in the existing `CLAUDE.md` structure and match its voice and formatting. Keep each addition short. Don't rewrite unrelated sections.

1. **Layer list marker (decision 77 area):** add: *The marker is blue (`ChromeLayerRenameMarker`) on shape layers and magenta (`ChromeLayerCustomMarker`, driven by `IsCustomLayer`) on custom layers, drawn 1 px higher by a render transform. The Layers "?" tooltip reuses both brush keys; change the colors only in the Chrome Palette.*
2. **Theme Builder menus (near the Smart fullscreen bullets):** add: *The Windows ContextMenu template draws a light icon gutter that filled rows don't fully hide (a 1 px left edge line, plus stubs at fractional-pixel seams). The New Theme menu hides it on `Loaded` with `HideMenuGutter`; the layer list's Copy / Paste menu still shows it (known, not applied).*
3. **Tooltips (near the Chrome Palette rule):** add: *The Layers "?" tooltip is the only styled tooltip: a local `ToolTip` using the `ChromeTooltip*` brushes with inline Bold, Italic and Run text. Never add a global or implicit ToolTip style.*
4. **Theme Builder Status:** change "a bold "|" marker on renamable layers" to "a bold "|" marker on renamable layers (blue on shape layers, magenta on custom layers)". In the New Theme item, add "a Templates heading above the templates" (normal case, not all caps).

If a statement in `CLAUDE.md` already covers one of these, update it in place instead of duplicating, and tell me.

## Part B: App.xaml comment fixes (comments only)
1. **~Line 709**, the Builder Context Menu Styles section comment: it says the styles are for "the Advanced tab's layer list Copy / Paste menu". Reword so it says they serve the Theme Builder's menus: the layer list Copy / Paste menu and the New Theme menu.
2. **~Lines 744-746**, the comment above `BuilderMenuSeparatorStyle`: it says the items cover the gutter with their own background. Reword to say that the items' background covers most of the gutter, but a 1 px left edge line and short stubs at fractional-pixel seams can still show through, and that a menu which needs it hides the gutter on `Loaded` (`HideMenuGutter`, as the New Theme menu does).
3. **~Lines 9-13**, the Chrome Palette intro: it calls the palette "the app's input chrome". Reword so it also mentions that it holds the layer list markers and the Layers help tooltip brushes (and keep whatever else it already says it holds).

Line numbers are approximate; locate by the text. Keep each comment about as short as it is now.

## Small themes.md touch
Append a short clause to the 2026-11-05 "Docs pass" change log entry saying CLAUDE.md was synced and three App.xaml comments were corrected. Make no other `themes.md` changes.

## Constraints / do NOT touch
- No changes to any XAML element, attribute, resource, or any C# code. In `App.xaml`, only the text inside `<!-- -->` comments may change.
- Don't touch `Global.Menu.Icon`, any styles, or the layer right-click menus.
- Don't delete, move or commit the untracked spec files (`audit-theme-builder-tweaks.md`, `theme-builder-tweaks-implementation.md`, `docs-pass-theme-builder-tweaks.md`, and this one).
- Don't commit, stash, revert or reset anything.

## How to verify
1. `dotnet build ArcadeStick.csproj` completes with 0 errors and 0 warnings.
2. Show the `git diff` for `App.xaml` and confirm that every changed line in this pass is inside a comment (no non-comment line changed in this pass).
3. Show the `CLAUDE.md` diff and the one-clause `themes.md` change.
4. Report `git status`. Expected: modified `App.xaml`, `Views/AdvancedThemesTabControl.xaml`, `Views/AdvancedThemesTabControl.xaml.cs`, `Views/ThemesManagerControl.xaml.cs`, `themes.md`, `CLAUDE.md`, plus the untracked spec files.
