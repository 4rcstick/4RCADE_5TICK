# Handoff 5: Dashed layer guides (4 px dash / 4 px gap)

## Goal and why
Project: 4RCADE 5TICK, a C#/WPF MAME frontend. In the Theme Builder, the layer alignment guides (Edge guides and Center guides) are currently solid 1 px lines. Make them **dashed: 4 px dash, 4 px gap**, for both guide types. Dashed lines are easier to read over a layer's own edge and content. Color, thickness and positions do not change.

This pass also doubles as a test of the project's new Claude Code permission settings (`.claude/settings.local.json`). Do the work normally; the extra reporting at the end is part of the test.

## Current behavior (verify in the code first)
- `MainWindow.xaml`, section `[SECTION: Theme Builder Alignment Guides]`: a `Canvas x:Name="GuideOverlay"` holding six `Line` elements (`GuideLeft`, `GuideRight`, `GuideTop`, `GuideBottom`, `GuideCenterV`, `GuideCenterH`), each `StrokeThickness="1"` and `SnapsToDevicePixels="True"`.
- `MainWindow.xaml.cs`, `ShowGuides(GuideInfo? info)`: a local function `Place(Line line, double x1, double y1, double x2, double y2)` sets the line's coordinates, `Stroke` and `Visibility`. All six lines go through it. Every line runs to the window edge; the right and bottom lines sit at X + Width - 0.5.
- The Background grid is a separate class (`BackgroundGridLayer`) and is NOT part of this change. It stays solid.

## Exact changes wanted

1. **`MainWindow.xaml.cs`** (inside `[SECTION: Theme Builder Alignment Guides]`):
   - Add one frozen dash pattern shared by all guide lines, with the two lengths as named constants so they are easy to change later. Intended shape:
     ```csharp
     // Guide dash pattern: 4 on, 4 off. WPF counts dash lengths in multiples of StrokeThickness (1 here), so these are 4 px.
     private const double GuideDashLength = 4;
     private const double GuideGapLength = 4;
     private static readonly DoubleCollection GuideDashPattern = CreateGuideDashPattern();
     ```
     plus a small static helper `CreateGuideDashPattern()` that builds the `DoubleCollection { GuideDashLength, GuideGapLength }`, freezes it and returns it. The helper needs a one-line `// Does ...` summary comment per the project's comment rules. If a simpler frozen-static form fits better with the file's style, use it, and say so.
   - In `Place()`, add `line.StrokeDashArray = GuideDashPattern;` (and nothing else; the default flat dash cap and a zero dash offset are what we want).
   - Update the comment above `ShowGuides` (it says "four 1px lines ... and a cross") so it says the lines are dashed 1 px lines.

2. **`MainWindow.xaml`**: update the comment in `[SECTION: Theme Builder Alignment Guides]` ("1px lines at the selected layer's edges plus a cross ...") to say dashed. Do not change any element or attribute in this file.

3. **`themes.md`**: find every place that describes the guides (search for "guide") and update the wording where it says or implies solid lines. Add a change log entry for 2026-11-05 in the file's existing format and place. Keep edits minimal.

4. **`CLAUDE.md`**: do NOT edit. Just report whether anything in it now needs to change (I expect nothing).

## Constraints / do NOT touch
- Do not touch the Background grid (`BackgroundGridLayer`, `SetBackgroundGrid`, `UpdateBackgroundGrid`), the guide colors, `StrokeThickness`, `SnapsToDevicePixels`, line positions or the `GuideInfo` type, `GuideOverlay_SizeChanged`, or Smart fullscreen code.
- No other changes. Don't commit, stash, revert or reset anything.
- `.cs` and `.xaml` files are CRLF and `.md` files are LF (CLAUDE.md). Use the Edit tool, and after editing run `git diff --stat` and confirm no file shows a whole-file change.
- Keep within the project folder. If any tool refuses or asks, do not look for another way around it; stop and tell me which action it was.
- Leave the untracked spec files alone.

## How to verify
1. `dotnet build ArcadeStick.csproj` completes with 0 errors and 0 warnings. (No intermediate build errors are expected, since this is a small edit in one file plus a comment.)
2. Report the `git diff` for each changed file and `git diff --stat`.
3. I'll check by eye: with a layer selected, tick Edge guides and Center guides; every line should be 4 px dash / 4 px gap and still run to the window edge; the Background grid should still be solid. You can't see the screen, so just say that this part is left for me.

## Permission test report (required)
End your report with a short section listing **every shell command you ran** (search, git, build, etc.), in order, so I can compare it against the permission prompts I saw. Also say whether any command or file action was refused or asked for approval, as far as you could tell. Expected: edits, searches, `git status`, `git diff` and `dotnet build` run without prompts; nothing else should be needed. If you needed to run anything outside that, say what and why.
