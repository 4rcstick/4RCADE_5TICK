# 4RCADE 5TICK — Layer Toolbar + Intelligent Reset Layer

## IMPLEMENTATION — one pass, mandatory pause BEFORE GUI testing

The read-only audit was completed and reviewed. This is the authorized implementation handoff. Follow `CLAUDE.md` and `themes.md` (code wins over stale docs). Audit findings are available from the prior report; verify anchors in current source. Do not repeat the full audit.

### Mandatory stop between implementation and GUI testing

**Phase A: Implement, update docs, run build, inspect diffs, report, and STOP.** Do **not** deploy the GUI sandbox, launch the application, automate the GUI, move/click the mouse, type, capture a screenshot, or start tests involving UI interaction. At the stop, say: **“Implementation and build complete; paused before GUI testing. Please move hands off mouse and keyboard and tell me ‘start GUI testing’ when ready.”** WAIT for the user's explicit reply; do not substitute a timer or infer permission from the original implementation “go.”

**Phase B: Only after explicit “start GUI testing” authorization**, use the approved sandbox launcher testing workflow from `CLAUDE.md`. Test and report actual results, then STOP for user visual acceptance. No commit. If implementation uncovers a contradictory assumption, a scope-expanding problem, or a material design decision, stop and report before guessing.

### Permission rules — minimize approval prompts

- Native **Read, Grep, Glob, Edit** tools preferred. Avoid Python entirely; it is subject to an ask rule. Do not evade an ask rule by using `py`, alternate paths/shells or wrappers.
- Work within the repo only, except the explicitly read-only runtime folder exception documented in `CLAUDE.md`. No changes to real runtime data without separate permission.
- No `cd` prefix, shell chains, scripts for file peeking, or ad-hoc PowerShell. Keep source-file line endings **CRLF** (`.cs`, `.xaml`) and docs **LF** (`.md`); use small anchored edits, not whole-file rewrites.
- Use **exactly** `dotnet build --no-incremental` as a standalone shell command. Do not pipe, filter, redirect, or chain it. Use standalone allowed `git status`, `git diff`, `git diff --stat` for verification.
- Do not commit/push/reset/clean. User commits after testing. Never seek elevated permission or bypass project restrictions; if a necessary operation isn't authorized, report the blocker.
- After the GUI pause, test with separate calls of the authorized ` .\bin\GuiTest\gui.ps1 -Do <Action> ...` pattern (without leading space when executing), using only its closed set of actions and sandbox guards. Do not launch the Debug build; it points at real data. Do not interact with any other app or window. Follow all current `CLAUDE.md` testing rules.

## 1. Scope

Move three controls from below the properties panel to the existing toolbar next to the Layers list; implement intelligent **Reset Layer** with M/J/Z saved-state choice, support custom layers, reuse the save confirmation flash, and add a compact diff preview if choice is needed. **Reset Layout and Restore Defaults are completely out of scope**: don't change code, behavior, or notifications for either. Do not refactor theme storage/asset pipeline or unrelated UI.

## 2. Toolbar

In `Views/AdvancedThemesTabControl.xaml`, retain the vertical, 30x30 `ThemedActionButtonStyle` Segoe MDL2 Assets buttons and 6px spacing. Order: **Up**, **Down**, **Add (+)**, **Rename (pencil)**, **Remove (trash)**, **Reset (circular arrow)**. Audit suggests glyphs `&#xE70F;` (pencil), `&#xE74D;` (trash), `&#xE72C;` (reset); verify existing conventions. Move existing named controls/handlers, do not duplicate. Remove obsolete row beneath properties, associated now-empty grid row, and update Layers `?` tooltip. Preserve tabs and scrolling.

| Selection | Rename | Remove | Reset |
|---|---|---|---|
| Ordinary built-in | Collapsed | Collapsed | Visible/enabled |
| Blue-pipe renamable built-in shape | Visible | Collapsed | Visible/enabled |
| Magenta-pipe custom | Visible | Visible | Visible/enabled |
| Grouped built-in | Collapsed | Collapsed | Visible/enabled |
| None or multi-select | Collapsed | Collapsed | **Visible but disabled** |

Use `LayerRegistry.CanRename`, `IsCustomId` and existing eligibility. Reset is **static in placement and visibility**; Rename and Remove collapse without leaving gaps. Up/Down disabled with no or multiple selection; Add preserves its previous behavior. Check the selection-cleared path explicitly because formerly the properties panel hid all three buttons indirectly. Keep disabled tooltips informative.

## 3. Reset sources and source integrity

- **M (memory):** active working `ThemeLayout` in `_settings.Layouts`.
- **J (saved adjustments):** values genuinely saved to `settings.json`. Opus found `ThemeBuilderWindow._layoutsAtLastSave`, but it sometimes receives **working-only/synthetic resolution snapshots** on size switching; do not label such entries “Saved Adjustments.” Preserve existing close-without-saving semantics. Use the smallest reliable approach: independently track genuine persisted-layout snapshots on successful saves/theme loads, or safely read persisted settings, without rearchitecting Save Adjustments. Missing J layout/entry remains absent. Never mutate M when comparing.
- **Z (saved theme):** `_settings.SavedThemeLayouts` when there is an actual available loaded theme. Ignore stale Z after deleting/unloading theme; don't treat missing custom entries as registry defaults. Check active size and source presence before applying `ResolveLayout` on **clones**. A missing default layer can use registry fallback only when that is the correct meaning of that saved layout; distinguish this from absent custom entries.

Successful Save Changes should yield equivalent saved-state values for reset comparisons; Save Adjustments updates J only. Preserve existing writes/loads and correct source refresh for theme loads, resolution changes, and startup. Use audit findings to verify ownership and safety.

### Image/video path semantics

A newly chosen asset uses an original **absolute** path until the theme is reloaded. Theme ZIP stores bundled `assets/...`; loading extracts to `Themes\Extracted\...`. Therefore differently formatted paths can represent the same image **without** being logically different choices; other distinct absolute paths can represent truly different images. Do **not** change bundling, extraction, or save behavior.

Review established mapping/metadata, if any. Normalize trivial path syntax/case, use an authoritative mapping where available, but **never** assume two paths are the same solely because basenames/extensions match. Do not add routine hashing/byte comparison. If identity cannot be established reliably, conservatively treat different paths as different values; a possible additional reset choice is safer than silently suppressing a real change. Restoration uses the chosen source's **usable original path**, not the display-only filename. Document any unavoidable comparison limitation.

## 4. Common comparison/restoration scope

Use one shared scope implementation for both comparing a candidate to M and actually restoring it, so the dialog cannot claim changes that wouldn't be applied:

- Restore X, Y, Width, Height, Visible, applicable `ImagePath`, and every style field included by `CopyStyleFrom`.
- For **custom layers**, restore `Name` too; retain its ID and Z-order. Custom names are per-resolution.
- For **built-in shapes**, preserve Name (shared across resolutions, as existing behavior). Keep Z-order/stacking order unchanged for all layer types. Do not display these excluded properties as differences.
- For a **group**, include all constituent part properties and applicable image paths consistently; reflect the part names in the chooser preview.
- **Scrollbars special case:** preserve 3.24 behavior: Reset Layer returns both overrides to “Use global” and clears their custom values, rather than asking J/Z. Give a brief “restored to defaults” flash.
- Ensure pending typed edits commit before comparison; buttons currently change keyboard focus, causing LostFocus commits. Prevent stale focus-loss handlers from reapplying a prior value after reset. Preserve `RebuildLayerList`/`RaiseGuides`/`LayoutChanged` propagation and selected-layer identity.

### Candidate decision algorithm — prompt only for two effective alternatives

For each available J and Z candidate, compute the **effective resulting reset state** under the common scope; compare it to M and to the other candidate. Never compare irrelevant properties or present raw JSON differences as user-visible changes.

1. If J and Z both match M: no reset; brief “already matches saved state” flash, with truthful source wording.
2. If **one** distinct effective alternative differs from M (including M=Z but J different, or J=Z but M different), restore that source automatically, without a choice prompt.
3. If **two** different effective alternatives each differ from M and from each other, prompt for J versus Z.
4. If a custom layer is in M and J but not Z, only J is a saved candidate; don't delete it.
5. If a custom layer exists only in M, reset editable properties to the **new custom-layer creation defaults**, preserving ID, Name and Z. Prefer a small shared `LayerRegistry` helper used by Add and Reset; explicitly align optional `FileOpacity` with Resolve defaults.
6. No source available must not silently delete a layer or incorrectly claim a saved source. If any unresolved ambiguous case would alter unrelated state, stop and report.

Cancellation never changes M. Ensure a no-op does not trigger spurious saves or mutation. Keep the saved source identity for correct flash messaging.

## 5. Two-save-state chooser design

For two meaningful alternatives, a compact, theme-styled WPF prompt based on the existing code-built dialog style:

**Reset Layer — [display name]**

“Two different saved states are available for this layer. Choose which version to restore.”

Show two clear choices, **Saved Adjustments** (`settings.json`) and **Saved Theme** (`<theme-name>.zip`), plus **Cancel**. For **each** candidate, show **at most five properties/groups that would actually change compared with M**, not all properties and not just J-vs-Z differences. Prioritize:

1. **Image/video filename first when changed**, showing current → restored filename or “No image assigned.”
2. Custom-layer Name, when changed.
3. Position (X/Y, preferably grouped), Size (Width/Height grouped), Visible.
4. Style settings (font/color/border/shadow/etc.) in a stable readable order.

For groups, identify the affected part. If more changed fields/groups exist, display `+ N more properties will be restored`. No expandable details or giant scroll area. Only preview display is limited; **all reset-scope properties are restored**.

Show a timestamp only if available reliably; label a filesystem timestamp accurately as **file last modified**, not “this layer saved at.” Do not invent per-layer timestamps or add broad filesystem scans. No timestamp is acceptable. Respect cancel/Escape/keyboard use and window bounds. If candidates become invalid while dialog is open, cancel safely.

## 6. Flash reuse

Reuse the existing **green-on-black 2-second top-right** `BrdThemeSavedFlash` / `TxtThemeSavedFlash` in `ThemeBuilderWindow`. Make a parameterized display method, e.g. `ShowBuilderFlash(string message)`, and keep existing “Theme Saved” behavior through a wrapper. Forward a small event from Advanced tab to builder; do not duplicate timers/animation. Messages:

- `Layer restored from saved theme`
- `Layer restored from saved adjustments`
- `Layer restored to defaults` (unsaved custom or Scrollbars)
- `Layer already matches saved state` (no-op, adjust wording if necessary)

Check long text versus THEME BUILDER / GLOBAL tab headers. **No flash added to Reset Layout or Restore Defaults**.

## 7. Files, docs, implementation checks

Likely files: `Views/AdvancedThemesTabControl.xaml`, `.xaml.cs`, `Views/ThemeBuilderWindow.xaml.cs`, perhaps `ThemeBuilderWindow.xaml` for flash comments/width, `Models/LayerRegistry.cs`, and `themes.md`. Modify others only if essential to the specified work and explain. Relevant anchors from audit: toolbar around XAML 273, old row ~2761, selection logic code-behind ~386, ~1450–1513, Reset Layer ~4981, flash window ~446, save snapshot ~491–503, and `themes.md` sections 3.4, 3.7, 3.8, decision 19, change log. Verify against actual checkout.

Update stale documentation (especially group image-path mismatch and button location), new state logic, custom reset semantics, popup and flash. Do not update unrelated features.

At Phase A end: run standalone `dotnet build --no-incremental` with 0 errors; report new warnings. Run standalone `git diff --stat` and inspect relevant diffs for whole-file line-ending changes or unrelated edits. **Then STOP before any GUI deploy or testing** and await the user's explicit **“start GUI testing”**.

### Phase B GUI checklist — only after separate user approval

Follow `CLAUDE.md` standing authorization and sandbox guards: `bin\GuiTest\app`, `bin\GuiTest\Data`, runner actions one per call; confirm target process path, `boot.cfg`, and PID/window ownership for every input/capture. Never launch Debug, touch real runtime data, or interact with other windows. Clean up permitted screenshots afterwards.

- Toolbar icon spacing/order/tooltips, plain/shape/custom/group/background/scrollbars/multi/none visibility and disabled state.
- Existing Up/Down/Add/Rename/Remove workflows unaffected.
- One saved version, no changes, two meaningful alternatives, and M=Z!=J; single and group layers.
- Custom M/J/Z, M/J-only, M-only creation defaults; custom name and media path; preserve ID/Z, no deletion.
- Popup displays up to five **real** differences per version, image first, correct `+ N more`, correct source filename, cancel safety and available timestamp.
- Focus-loss on pending numeric edit immediately before Reset.
- Image path states around theme save/reload where safely testable without touching real assets.
- Scrollbars special reset; shape naming across sizes; theme and size switch; close without save.
- Correct top-right messages/timer and original `Theme Saved` message; no changed behavior to Reset Layout/Restore Defaults.

Report **only tests actually run**, plus runner limitations; do not fabricate results. Stop and request user visual sign-off. **Never commit**.
