# 4RCADE 5TICK

## Project Overview

4RCADE 5TICK is a portable, highly customizable MAME frontend launcher built in C#/WPF (.NET 10.0). It targets arcade cabinet builders and MAME enthusiasts who want a polished, config-friendly frontend without manual ini editing.

Key features:
- TreeView-based game browser
- LibVLCSharp video previews (FMV snaps)
- Windows.Gaming.Input (WGI) gamepad navigation
- Full Theme Builder system for UI customization
- MAME.ini integration (reads/writes settings without manual editing)

This is a solo-developer project, converted from a ~1600-line PowerShell prototype. It is deliberately architected — not vibe-coded. Scope and design decisions are made carefully; do not make architectural or scope decisions unilaterally. When a change has design implications beyond the immediate fix, flag it and ask rather than assuming.

Repo: `4rcstick/4RCADE_5TICK` (MIT license)

## Known Gotchas — Read Before Editing

These are recurring bug sources. Check against these before considering a change complete.

### `ApplyThemeSettings()` trap
Any new property added to `ConfigurationSettings` must also be explicitly copied in `ApplyThemeSettings()`, or it will silently revert to compiled defaults on every launch. This has caused multiple bugs. **Always check this method when adding or modifying a theme-related property.**

### Three Theme Builder sync points
When adding or modifying ANY theme property, all three of these must be updated together:
1. `LoadCurrentThemeValuesIntoUi`
2. The `themeDto` builder in `BtnSaveTheme_Click`
3. `BtnLoadTheme_Click`

Missing any one of these causes inconsistent behavior between loading, saving, and displaying theme values.

### Three-property forwarder pattern (MainViewModel)
Theme-related properties follow this pattern: `Theme*` properties → `RefreshThemeBindings()` → XAML bindings. New theme properties should follow this same forwarding pattern for consistency.

### Rompath safety rule
Boot-time sync (`SyncMameRomPathsAsync`) and user-triggered save (`MAMEiniTabControl.UpdateMameIniSettings`) are two separate writers **by design**. `BtnSaveOptions_Click` must never call the MAME.ini tab's rompath sync. Do not consolidate these without explicit approval — they're separate for a reason.

### `StopPollingLoopAsync` deadlock
The WGI polling loop fix uses `await` instead of `.Wait()`. **Do not revert to the synchronous variant** — it causes a silent UI thread deadlock in the game launch path.

### WPF `CornerRadius` on nested Grids
Nested Grids with negative margins don't respect a parent `Border`'s corner radius. Fix pattern: convert background layers to `Border` elements with matching `CornerRadius`, and set wallpaper as `ImageBrush` on `Background` rather than a separate layered element.

### `ClipToBounds` limitation
`ClipToBounds` clips to rectangular bounds only, not rounded geometry. The scrollbar corner-clip cosmetic quirk is an accepted limitation, not a bug to fix.

### `mame_cache.txt`
Not a native MAME file — it's a `-listfull` output artifact. `GenerateCacheFileAsync()` runs `mame.exe -listfull` and writes stdout to regenerate it. ROM-set drift detection triggers regeneration when ZIP names diverge from cached entries.

### Flat ROM folder fallback
Most casual users store ROMs flat (not in category subfolders). The fallback "GAMES" node for ungrouped games is essential — never silently discard ungrouped games.

### LibVLCSharp over MediaElement
`MediaElement` silently fails on non-standard codecs mid-session — this is why LibVLCSharp is used instead. `PruneLibVlcPlugins` MSBuild target removes unused plugin subfolders to reduce distribution size. Don't suggest reverting to `MediaElement`.

### Splash window video wall
The grid of "monitors" playing FMVs on the splash screen is a single composited 720p video (made in After Effects), not individual LibVLCSharp instances per tile. Keep this in mind when debugging splash panel playback issues — there's only one video stream to manage, not several.

### Known issue - RTSS/MSI Afterburner overlay conflict
Some users running MSI Afterburner / RivaTuner Statistics Server (RTSS) with "Show On-Screen Display" /
DXGI hooking enabled have experienced random crashes, particularly during video preview playback.
Root cause: RTSSHooks64.dll injects into DXGI's Present pipeline and can collide with LibVLC's D3D11
video output plugin when it creates/tears down swap chains (e.g. on preview video swap or loop).
Confirmed via crash dump: unhandled access violation in dxgi.dll with RTSSHooks64.dll present in the
call stack, immediately above libdirect3d11_plugin.dll / libvlccore.dll.
This is a native access violation (0xC0000005) - it cannot be caught with a normal try/catch, since
.NET does not allow catching AVs by design (the runtime treats the process state as unsafe to continue).
Current mitigation (beta): documented as a known issue, no code changes yet. If it recurs, workaround is
RTSS Setup -> General properties -> Application detection level -> None.
Possible future code-level mitigations (not yet implemented): detect known overlay-hook DLLs (e.g.
RTSSHooks64.dll) via Process.GetCurrentProcess().Modules on startup and warn the user; add
AppDomain.CurrentDomain.UnhandledException / Dispatcher.UnhandledException logging (still can't catch
the AV itself, but improves diagnosis of other unhandled exceptions); reduce D3D11 swap chain
create/destroy churn in the video preview pipeline to reduce the odds of hitting the race window.

### LibVLC EndReached loop race (fixed)
Both video players (VlcMediaPlayer preview + BootSplashMediaPlayer splash) had EndReached handlers that
manually seek-to-0 and replay on loop, WHILE ALSO having :input-repeat=65535 set as a media option. Two
competing loop mechanisms fighting over the same stream's end-of-playback state caused a race with the
D3D11 vout's teardown/re-init, producing an access violation in dxgi.dll (confirmed via crash dump/call
stack, separate incident from the RTSS one above). Fix: removed the manual EndReached restart logic
entirely and let :input-repeat handle looping natively at the demux level. Do not re-add manual
Stop()/Time=0/Play() restart logic in EndReached handlers if input-repeat is already set on the Media.

### HasActiveMedia staleness bug (fixed)
SelectedGame's setter updates IsGameSelected immediately (synchronous), but previously only updated
HasActiveMedia via the debounced UpdateActiveMediaPreviews() (~600ms later). Selecting a folder sets
SelectedGame = null, which (once the debounce fired) set HasActiveMedia = false. Quickly selecting a
game after a folder meant the preview panel became visible immediately (IsGameSelected = true) while
HasActiveMedia was still stale at false, flashing the "no preview" placeholder before the real video/
image loaded. Fix: added ResolveHasActiveMediaImmediate(), a fast synchronous File.Exists-only check
that runs immediately in the SelectedGame setter, so HasActiveMedia is never stale. The expensive part
(marquee bitmap load, video/image loading) stays debounced in UpdateActiveMediaPreviews() as before.

## Conventions

### Code editing / commenting
- Use sandwiched code blocks with section fencing as anchors:
  - C#: `// [SECTION: ...]` / `// [END SECTION: ...]`
  - XAML: `<!-- [SECTION: ...] -->` / `<!-- [END SECTION: ...] -->`
- Every method gets a one-line `// Does X` summary comment.
- Replace existing comments when updating a method; don't stack redundant comments.

### Grammar / naming standards
- "MAME®" is capitalized with the registered trademark symbol in user-facing text.
- Literal filenames stay lowercase even in prose: `roms`, `mame.ini`.

### Workflow expectations
- Confirm scope/intent before making non-trivial changes — don't assume the fix approach without a quick gut-check if there's ambiguity.
- One logical change at a time; avoid bundling unrelated fixes into a single edit pass.
- After any change, the project should still build cleanly — flag any change that's expected to produce transient errors in a multi-file edit.

## Current Scope Boundaries

**In scope for v1.0-final:**
- Subfolder packaging refactor (moving launcher and dependencies into a MAME subdirectory)
- ROM-path rescan button in Options that syncs `mame.ini` rompath without requiring a restart

**Scoped for v2.0 — do not implement early without explicit request:**
- `.4rctheme` self-contained theme package format (zip with custom extension) for community theme sharing
- `catver.ini`-based virtual auto-sort of flat romsets into category folders (Fighters, Shooters, etc.) — virtual only, does not move files on disk. `catver.ini` bundled with attribution to progetto-SNAPS/AntoPISA.
- Folder-level inherited mouse/lightgun support with visual indicator after folder name

**Deferred / not currently planned:**
- TextBox theming
- Linux/Android port (preliminary market exploration only; contingent on Windows v1.0 traction; would require Avalonia UI for Linux, full rewrite for Android)

## Key Libraries & Tools

- LibVLCSharp + VideoLAN.LibVLC.Windows (video preview engine)
- Windows.Gaming.Input (gamepad support)
- System.Text.Json
- Target distribution size: ~250MB uncompressed (self-contained .NET 10 + WPF runtime — framework-dependent deployment is explicitly ruled out in favor of faster boot time)