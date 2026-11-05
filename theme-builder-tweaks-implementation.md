# Handoff 2 of 2: Implement the four Theme Builder tweaks

## Goal and why
Project: 4RCADE 5TICK, a C#/WPF MAME frontend. Four small polish tweaks to the Theme Builder, all pre-audited (your previous audit report is the basis for this file; paths and line numbers below come from it, so re-verify them before editing). Make all four changes, build, and report back.

**Terminology:** in the Theme Builder, "default layers" that show a pipe are the 4 shape layers (they can hold images and video and can be renamed). Custom layers are the user-added ones (`Custom_<guid>` Ids). Other built-in layers (Games List, Marquee, Video Preview, etc.) have NO pipe and must stay that way.

---

## Change 1: Layer list pipes up 1 px
File: `Views/AdvancedThemesTabControl.xaml` (`LayerListItemTemplate`, around lines 43-53).

On the `RenameMarker` TextBlock, add:
```
<TextBlock.RenderTransform><TranslateTransform Y="-1"/></TextBlock.RenderTransform>
```
A render transform doesn't affect layout, so row height, name text, separator and arrow buttons stay put. Keep `Margin="0,0,0,0"` and everything else on that element as is. Applies to both blue and magenta pipes.

## Change 2: Magenta pipe for custom layers
Shape layers and default layers stay blue (`ChromeLayerRenameMarker`, `#FF4FC3F7`, unchanged).

1. `App.xaml`, Chrome Palette next to `ChromeLayerRenameMarker` (around line 40): add a new brush
   `<SolidColorBrush x:Key="ChromeLayerCustomMarker" Color="#FFE040FB"/>`
   with a short comment. Update the existing comment at line 39 so it describes the blue as the marker for shape (default) layers and the new brush as the marker for custom layers.
2. `Views/AdvancedThemesTabControl.xaml.cs`: add `public bool IsCustomLayer { get; init; }` to the private `LayerListItem` class (around lines 131-141), with a one-line comment. In `RebuildLayerList` (around line 318, next to `ShowRenameMarker = LayerRegistry.CanRename(key)`), set `IsCustomLayer = LayerRegistry.IsCustomId(key)`.
3. `Views/AdvancedThemesTabControl.xaml`, in `LayerListItemTemplate.Triggers`: add a `DataTrigger` on `IsCustomLayer` = True that sets `RenameMarker`'s Foreground to `{StaticResource ChromeLayerCustomMarker}`. Keep the existing `ShowRenameMarker` trigger.
4. Verify at runtime that the custom pipe really renders magenta (if a local Foreground on the element wins over the trigger, remove the local attribute and move the blue to a default Setter in the template; report if you had to).

Do NOT change `LayerRegistry` logic, `CanRename`, or which rows show pipes.

## Change 3: New "?" tooltip (rich text, dark style)
File: `Views/AdvancedThemesTabControl.xaml`, the `Border.ToolTip` around lines 220-226.

**Replace** the current `<TextBlock Text="{StaticResource LayersHelpText}" .../>` with a `ToolTip` element whose content is a `TextBlock` built from `Run`, `Bold`, `Italic` and `LineBreak` inlines.

**Tooltip style (local to this one tooltip only; do NOT add a global/implicit ToolTip style):**
- Background: dark, `#FF1E1E1E`
- Foreground: white (also set on the TextBlock)
- BorderBrush: `#FF555555`, BorderThickness 1
- Padding: `10,8`
- Prefer existing Chrome Palette brushes if there are ones that match these closely; otherwise add new brushes in the Chrome Palette next to the pipe brushes (for example `ChromeTooltipBackground`, `ChromeTooltipBorder`) so they're easy to tweak later. The user may want to adjust these, so keep them in one place.
- **Wrapping off:** `TextWrapping="NoWrap"`, remove `MaxWidth="380"`. Line breaks must appear exactly as below.
- Keep `ToolTipService.InitialShowDelay="300"` and `ShowDuration="60000"` as they are.

**Content (exact text; fix nothing, add nothing).** Section headers are **bold**. One blank line between sections (two consecutive `LineBreak`s). The two lines marked [italic] are *italic*. In the last section, each `|` is its own `Run` with `FontWeight="Black"` and the matching brush. If the font has no Black weight, a heavy weight is fine, but try Black first. The `—` is an em dash.

```
LAYER ORDER & CUSTOM LAYERS                                   <- bold
Use the arrows beside the layer list to move layers forward or backward in the Z-order.
Use the + button to add a custom layer.

MOVE MULTIPLE LAYERS AT ONCE                                  <- bold
Ctrl-click or Shift-click to select multiple layers,
then move them together using the Move X / Move Y boxes to the right.

COPY LAYER PROPERTIES TO OTHER LAYERS                         <- bold
Right-click a layer to copy its position, size, or border properties.
Right-click another layer, or multiple selected layers, to paste them.
Search & Buttons, Game Info, Footer, Main Window Background,  <- italic
and Scrollbars do not support Copy & Paste.                   <- italic

DEFAULT & CUSTOM LAYER HELPERS                                <- bold
Blue | — Default layer; can be renamed.                       <- "|" in ChromeLayerRenameMarker, Black weight
Magenta | — Custom layer; can be renamed or deleted.          <- "|" in ChromeLayerCustomMarker, Black weight
Double-click a default or custom layer to rename it.
```
(The arrows and `<-` notes are annotations for you; do not include them in the UI. Remember `&` must be written as `&amp;` in XAML. The final "Double-click" line directly follows the Magenta line with no blank line.)

The pipes in the tooltip must reference the same two brush keys as the layer list (`ChromeLayerRenameMarker` and `ChromeLayerCustomMarker`) so the list and tooltip always match.

**Cleanup:** `LayersHelpText` (the string resource, around lines 16-19) is only used by the old tooltip. Remove it once the tooltip no longer references it. Keep the `[SECTION: Layers Help]` marker and `LayersHelpGlyphStyle`. Leave the `sys` namespace declaration alone unless it is completely unused and removing it is clearly safe.

## Change 4: TEMPLATES header in the New Theme menu
File: `Views/ThemesManagerControl.xaml.cs`, `BtnNewTheme_Click` (around lines 485-514).

Right after the separator is added (only when `templates.Count > 0`) and before the template items loop, add a heading item:
```
new MenuItem { Header = "TEMPLATES", Style = (Style)FindResource("BuilderMenuHeadingStyle") }
```
Result: Copy Current Theme, separator, TEMPLATES, then the template items. The header appears only when there are templates (same condition as the separator).

`BuilderMenuHeadingStyle` (App.xaml, around lines 752-769) already makes it non-clickable (`IsHitTestVisible=False`), non-focusable (keyboard nav skips it), never highlighted, and bold in the themeable `Global.Menu.Icon` color. That is the same color as the Random Game Display heading in the layer right-click menus. Reuse the style as is; do NOT create a new style or hardcode a color.

Update the comments: the method comment above `BtnNewTheme_Click` (around line 485) should mention the TEMPLATES header, and the comment above `BuilderMenuHeadingStyle` in `App.xaml` should say it's also used for the TEMPLATES heading in the New Theme menu.

## Docs
Update `themes.md` so it matches the new behavior. The audit found relevant mentions around lines 133, 134, 451, 546, 668, 786 and 811, plus the change log. Cover: custom layers get a magenta pipe (shape layers stay blue), the "?" tooltip is now rich-text with a dark style and no longer uses the `LayersHelpText` resource (line 546 refers to it by name), and the TEMPLATES heading in the New Theme menu. Add a change log entry following the file's existing format. Keep edits minimal and only where the text is now wrong or incomplete.

---

## Constraints: do NOT touch
- Pipes on the other built-in layers (they must still have none).
- `LayerRegistry` logic, `CanRename`, `IsCustomId`, saved theme data or theme.json formats.
- `Global.Menu.Icon` or any other themeable Global color.
- Any global or implicit ToolTip style (none should be added; the dark style is local to the "?" tooltip).
- `audit-theme-builder-tweaks.md` and this file (leave untracked; don't delete or move them).
- No unrelated refactors, renames, or formatting changes. Do not commit; leave all changes uncommitted so the diff can be reviewed.

## How to verify
1. `dotnet build ArcadeStick.csproj` completes with 0 errors and 0 warnings. (Build once all edits are in; a build partway through, such as after adding `IsCustomLayer` but before the trigger, is fine to skip.)
2. Run the app and open the Theme Builder, then check:
   - Shape layers show a blue pipe; add a custom layer with the + button and confirm its pipe is magenta. Other built-in layers show no pipe.
   - Pipes sit 1 px higher than before and nothing else in the row moved.
   - Hover the "?" glyph: dark tooltip, white text, no line wrapping, a blank line between the four sections, bold headers, the Search & Buttons sentence in italic, bold black-weight blue and magenta pipes matching the list, and the double-click line last.
   - New Theme menu: with templates present, TEMPLATES appears below the separator and above the template items in the menu icon color; it doesn't highlight on hover, doesn't respond to clicks, and arrow-key navigation skips it. With no templates, neither the separator nor the header appears.
3. Report back with: the full `git diff` (or per-file summary plus the diff for the XAML and the code-behind), what you saw when running the app (or say if you could only build and not run it), and anything you changed differently from this file and why.
