# Handoff: Restore Defaults for 720p and 1080p, with a Layout-only / Everything choice

You have no memory of the chat that produced this file, so everything you need is here. Project: **4RCADE 5TICK**, a C#/WPF MAME frontend. The developer is a first-time developer working independently, so keep changes readable and explain anything non-obvious in your final report.

## Goal and why

The launcher has default theme layer values hard-coded so the **Restore Defaults** button in the theme builder has something to load. Right now only a 720p set exists (and it is out of date). We want:

1. The 720p defaults updated to match the new reference values below.
2. A new 1080p defaults set.
3. Restore Defaults to pick the right set based on the active theme's resolution.
4. A pop-up when Restore Defaults is pressed, letting the user choose **Layout only**, **Everything**, or **Cancel**.

We do NOT need a defaults set for every resolution. Two sets are enough. Odd sizes just use the nearest of the two. Scaling the defaults to fit odd sizes is a **future** feature, so structure the code so scaling can plug in later at the set-selection step.

## Reference data

The reference is `theme.json` (the current default theme, which the user will attach or has in the repo; look for `theme.json` in the default theme folder or ask the user to attach it if you can't find it). It contains three layouts in `Layouts`:

- 334 x 1000 (portrait): **DO NOT TOUCH, ignore entirely.**
- 1920 x 1080: source for the new **1080p defaults**.
- 1280 x 720: source for the updated **720p defaults**.

Copy the per-layer values **exactly** from those two layouts (every property on every layer: Id, X, Y, Width, Height, Z, Visible, Name, ImagePath, VisibilityMode, colors, fonts, borders, corner radius, shadows, opacity, etc.). Do not retype from memory or round anything. Parse or copy from the actual file contents. If the cleanest approach is to embed those two layouts as an embedded resource or JSON string constant instead of hand-written C# objects, that is fine, as long as it fits how the project already stores its current defaults.

Layer IDs in both layouts include: MainBackground, ShapeLayer01-04, GamesList, MarqueePanel, SplashImage, VideoPreview, GameTitle, PublisherBlock, GameInfoNav, GameInfoBody, ImagePreviewsBody, PlayButton, FavoritesStar, SearchLabel, SearchBox, RandomButton, SortButton, OptionsButton, HamburgerButton, RandomGameDisplay, RandomGameDisplaySplash, Footer. (The 720p layout does not have a "Custom_..." layer. The portrait layout does, which is another reason to ignore it.)

## Step 1: Investigate first (before changing anything)

Find and report back to yourself:

- Where the current hard-coded 720p defaults live.
- Where the Restore Defaults button is handled (handler, command, or view model) and what it currently does.
- How the active theme's resolution is stored (the theme has `ActiveWidth` / `ActiveHeight` and a `Layouts` list with `Width`/`Height` per layout; confirm how the app models this).
- How existing dialogs/pop-ups in the app are built and styled, so the new pop-up can match.

## Step 2: Exact changes wanted

### A. Defaults data

- Replace the 720p defaults with the 1280x720 layer values from `theme.json`.
- Add a 1080p defaults set from the 1920x1080 layer values.
- Both sets are keyed by layer **Id**.

### B. Set selection (single small function)

Create one small, clearly named function, for example `GetDefaultsForHeight(int height)`:

- **Height only** decides the set (ignore width).
- `height < 1080` returns the 720p set.
- `height >= 1080` returns the 1080p set.

Add a short comment noting this is the place where scaling of defaults for non-base resolutions can be added later. Do not implement scaling now.

"Height" means the height of the **active layout/theme resolution** being edited (the same value that decides which layout in the theme is active).

### C. Restore Defaults pop-up

When Restore Defaults is pressed, show a dialog with three choices:

- **Layout only**
- **Everything**
- **Cancel** (does nothing)

Match the look and feel of the app's existing dialogs and theme styling. Short explanatory text on each choice, for example:

- Layout only: "Resets position, size, layer order and visibility. Keeps your colors, fonts, borders and images."
- Everything: "Resets all layer settings to the defaults."

### D. Restore behavior

Applies to the **active layout only**, using the defaults set chosen in Step B.

- Match layers by **Id**. For each layer in the active layout that has a matching default, apply the default.
- **Layout only** copies exactly these properties: `X`, `Y`, `Width`, `Height`, `Z`, `Visible`. It does NOT touch `VisibilityMode` or any style properties.
- **Everything** copies all layer properties from the default, including `VisibilityMode`.
- **Visible is forced to true** on every restored layer in both modes. (Restoring is meant to make every default layer visible; the user can re-hide layers afterward.) Note this means Visible comes from "force true," not from the default value. In the 720p and 1080p reference layouts every layer is already Visible = true anyway.
- **Custom layers** (any layer whose Id has no matching default, such as ones starting with `Custom_`) are **skipped, not modified and never deleted.**
- If a default layer is missing from the active layout, leave it missing (do not add it) unless the existing code already re-adds missing default layers; if it does, keep that existing behavior and mention it in your report.
- Refresh the UI/preview after restoring using whatever mechanism the app already uses after layer edits, and respect undo if the app has undo support for layer edits.

## Constraints: things NOT to touch

- **Do not change theme-wide settings** (OptionsBg, ScrollTrack*, ScrollThumb*, ScrollbarWidth, Tab*, Header*, SubHeader*, Standard*, Separator*, ContextMenu*, OptionsMenu*, Btn*, Info, etc.). Restore never resets these.
- **Do not touch the 334x1000 portrait layout**, neither in the defaults nor during restore.
- Do not rename properties or change the theme.json schema/version.
- Do not implement scaling.
- Do not refactor unrelated code. Keep the diff focused.

## How to verify

1. Build the solution. It must compile with no new warnings caused by these changes.
2. Run the app and open a **1280x720** theme. Move/resize a few layers and change a color on one. Press Restore Defaults:
   - Choose **Layout only**: positions/sizes/Z/visibility return to the 720p defaults, the changed color stays.
   - Change things again, press Restore Defaults, choose **Everything**: the color also resets.
   - Press **Cancel**: nothing changes.
3. Hide a layer, restore (either mode): it becomes visible again.
4. Open a **1920x1080** theme and repeat. Values should match the 1080p layout from `theme.json` (for example GamesList at X 27, Y 80, 423 x 972; MarqueePanel at X 474, Y 35, 1424 x 191).
5. Open a theme at an odd size, such as 1600x900 (height < 1080), and restore. It should load the 720p set. At 2560x1440 (height >= 1080) it should load the 1080p set. (Layouts won't fill the canvas at those sizes. That is expected until scaling exists.)
6. Add a custom layer, restore: the custom layer is still there and unchanged.
7. Confirm theme-wide settings (scrollbar color, tab colors, etc.) are unchanged after restore.

## Final report

When done, report: which files you changed, where the defaults now live, how the set-selection function and pop-up are wired, anything you had to decide that this file didn't cover, and any build warnings or errors.
