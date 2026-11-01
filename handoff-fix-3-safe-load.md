# Handoff: Make Load Theme safe (never destroy the working copy before the replacement is confirmed good)

You have no memory of the chat that produced this file, so everything you need is here. Project: **4RCADE 5TICK**, a C#/WPF MAME frontend. The developer is a first-time developer working independently, so keep changes readable and explain anything non-obvious in your final report.

This is the **third fix handoff** in a series. Fix 1 (Save writes only to the loaded theme, tracked by `LoadedThemeName`) and Fix 2 (one path rule, `RewriteThemeAssetPath`) are already built and tested. Do NOT start on anything listed under "Not in this handoff".

## Goal and why

**Load Theme destroys the working copy before it knows it can replace it.** In `LoadTheme` (`ViewModels/MainViewModel.cs`, around line 1517) the order is: release open files, `Directory.Delete` the theme's extracted folder (`Themes\Extracted\<name>\`), then extract the zip (around line 1519), then read the theme properties and apply them.

A real failure happened: another program (Photoshop) held the theme's `assets` folder open. The load showed:

> Processing Failure: Error reading theme properties: The process cannot access the file '\\?\F:\Arcade Stick\Data\4rcade5tick_files\Themes\Extracted\Resize Test\assets' because it is being used by another process.

`Directory.Delete` removes everything it can (including `theme.json` and the files inside `assets`) before it fails on the locked folder. The extract on the next line never runs, so the extracted folder was left empty. The same thing happens if the zip is corrupt: the folder is deleted before extraction fails.

There is a second hole. If the refresh at the very end of `LoadTheme` throws, the new theme's layouts are already in memory while `LoadedThemeName` still holds the old name, so a later Save could write one theme's data into another theme's zip. Also, after a failed load of the theme that is **already** the loaded one, the names match, Save is not blocked, and Save can rewrite the zip without the deleted files.

**Behavior wanted when a lock (or any failure) stops the load: refuse and explain.** Nothing on disk changes, nothing in memory changes, and the user gets a clear message. Do not fall back to extracting over the existing folder.

## Relevant background (verified by earlier audits; re-verify by name, since line numbers may have shifted)

- The theme **zip** (`Themes\<name>.zip`) is the source of truth. `Themes\Extracted\<name>\` is a working copy. Load never modifies the zip.
- `LoadTheme` is reached from: the Load Theme button, the hamburger-menu theme switch (queued), and first run with no active theme (`LoadTheme("default")`, which swallows errors around line 206).
- `LoadSavedThemeSnapshot` (startup restore point) reads the zip but does not extract. Leave it alone.
- `RewriteThemeAssetPath` (Fix 2) turns `assets/<file>` paths into paths under the theme's final extracted folder. That final folder path stays the same after this change.
- `LoadedThemeName` lives on `MainViewModel`. Invariant to enforce: **it always names the theme whose layouts are currently in `_settings`.**
- Before deleting the folder, `LoadTheme` calls a release step (`ReleaseCustomLayers` and related) to drop open file handles. Keep it.
- Some themes are in an older format with `theme.cfg` instead of `theme.json` (for example Arcade, Cyndi's Theme, Steampunk, Football). Whatever formats Load accepts today, it must still accept, with the same parsing. Do not change parsing or the theme.json schema.

## Exact changes wanted

Restructure the load so every step that can fail happens **before** the live extracted folder is touched, and the swap itself is reversible.

1. **Check the zip first.** Open it read-only and confirm it is a readable zip with the entries the current load requires (reuse the existing checks and format detection). If not, stop with the failure message below. Nothing is deleted.

2. **Extract to a staging folder.** Use a staging area on the same drive as `Themes` so the folder move is a rename (for example a hidden-looking folder such as `Themes\_staging\<name>.loading-<guid>`, but choose whatever is least likely to be mistaken for a theme or enumerated by other code; check that nothing lists the `Themes` or `Extracted` folders in a way that would pick it up). If extraction fails, delete the staging folder and stop with the failure message.

3. **Parse and validate the staged copy** using the existing code that reads the theme properties, **before** swapping. Where the existing code needs the final extracted-folder path (for example `RewriteThemeAssetPath`), give it the final path (`Themes\Extracted\<name>\`), not the staging path, since that is where the files will end up. If parsing or validation fails, delete the staging folder and stop. Nothing live changed.

4. **Release open files** (existing release step), then **swap**:
   - If the live extracted folder exists, **rename** it aside to a unique name inside the staging area (`<name>.old-<guid>`). A rename of a folder that contains a locked subfolder fails on Windows, which is exactly the protection wanted: if the rename fails, delete the new staging folder, change nothing else, and show the failure message.
   - Rename the staged folder into place as `Themes\Extracted\<name>\`.
   - If that second rename fails, **roll back**: rename the old folder back. If rollback also fails, show a loud, specific message naming both folders so the developer can recover by hand.
   - Only after the swap succeeded, delete the `.old-` folder (best effort; if it fails, leave it and keep going).

5. **Apply to memory and refresh the UI only after a successful swap.** Keep the memory state consistent: update `LoadedThemeName` at the same point the new layouts replace the old ones in `_settings`, so the invariant above can never be broken by an exception in the later UI refresh. Wrap the UI refresh so an exception there cannot leave layouts and `LoadedThemeName` disagreeing. Decide the cleanest way and describe it in your report.

6. **Failure message** (use the app's existing message box style, title "Theme Not Loaded"). Plain-language text, for example: "Couldn't load '<name>'. Another program (for example an image editor like Photoshop) may be using that theme's folder. Close it and try again. Nothing was changed." Add the underlying error text briefly at the end. Use wording appropriate to the actual failure when it is clearly something else (unreadable zip, invalid theme file), but always include "Nothing was changed."

7. **Stale staging cleanup.** At the start of each load, delete leftover staging folders from earlier failed attempts, **only inside the staging parent folder and only names matching this feature's exact pattern**. The cleanup must be impossible to point at a real theme folder or a zip. Ignore errors while cleaning.

8. **All callers use the new path.** The Load Theme button, the hamburger switch, and the first-run default load must all go through the safe version. Keep the first-run load tolerant of failure exactly as today (it should not crash the app).

9. **After a failed load**, keep the current behavior that `LoadedThemeName` stays on the previous theme (so Save is blocked while the dropdown shows a different theme). Optional: if you can safely set the dropdown back to the loaded theme after a failure without triggering another load or a selection-changed loop, do it and report it. If it is risky, leave the dropdown alone and say so.

10. **Wallpaper video check.** The release step (`ReleaseCustomLayers` and related) may not cover the theme's own wallpaper video. Check which files it releases. If something clearly missing is trivial and safe to add, add it. Otherwise just report what you found.

## Constraints: things NOT to touch

- **Do not change Save, Delete Theme, or Restore Defaults** and do not touch `DefaultLayerSets.*`. Their safety work is separate.
- Do not change the theme.json schema, version, or the parsing of either format.
- Do not modify the zip during Load. Do not add any writes to existing theme zips.
- **Do not read, write, delete or run anything against `F:\Arcade Stick\Data\4RCADE5TICK_files\Themes` or any theme zip while building or testing.** If you want to exercise the swap-and-rollback logic, do it in a throwaway console snippet or a temp-folder test using fake folders and a temp zip you create. The developer tests in the running app with throwaway themes.
- No new package dependencies. Keep the diff focused. No commits unless the developer asks.

## Expected build errors

If you move logic into new methods or change a signature used by several callers (button, hamburger, first-run), the build may fail between edits. Finish all edits before building. Report any errors that remain after that.

## How to verify

Build first (0 errors; report any new warnings). Then write a numbered checklist for the developer, who will run it in the app. Include at least these, and say clearly which themes are safe to use:

- **Use throwaway themes, plus Electronic (healthy) and an old-format theme such as Arcade or Cyndi's Theme (backed up elsewhere).** Do NOT load Default or Football during testing: their zips are damaged and loading them would replace their good extracted folders.

1. **Normal load works.** Load a healthy throwaway theme and Electronic: images and layout appear, `Themes\Extracted\<name>` is fully populated, and no leftover staging folders remain.
2. **Old format loads.** Load Arcade or Cyndi's Theme: it loads as before.
3. **Locked folder is survivable.** On a throwaway theme: open a Command Prompt, run `cd /d "<...>\Themes\Extracted\<name>\assets"` and leave the window open (this locks the folder). Press Load Theme on that theme. Expected: the "Theme Not Loaded" message, the extracted folder is **completely intact** (`theme.json` and every asset still there), no leftover staging folders, and the app still shows the previously loaded theme. Then close the Command Prompt and Load again: it works.
4. **Failed reload of the already-loaded theme is safe.** Load throwaway theme A, lock its `assets` folder the same way, press Load on A again (message appears), then Save Changes. Expected: Save works and the saved zip contains all of A's assets, with no "Some Assets Missing" popup.
5. **Corrupt zip.** Create a fake file (for example a text file renamed to `BadTest.zip`) in `Themes`. Select it and press Load. Expected: the message, nothing else touched, and no `Extracted\BadTest` created. Delete the fake zip afterward.
6. **Hamburger switch.** Switch themes from the hamburger menu: works. Repeat with a locked folder: the message appears, no crash.
7. **Restart.** Close and reopen the app: it starts normally with the active theme.
8. **Staging area check.** After every test above, look in the staging parent folder: it should be empty (or contain only a leftover from a deliberately forced failure that the next load cleans up).

## Final report

When done, report: which files you changed; where the staging area lives and why you chose it; the exact load sequence and how rollback works; how the `LoadedThemeName` invariant is now guaranteed; what the release step covers and what you found about the wallpaper video; what the dropdown does after a failed load; how legacy (`theme.cfg`) themes are handled; anything you decided that this file did not cover; build result. Suggest what the next handoff should cover.

## Not in this handoff (do not do these)

Safe Save (write to a temp zip and swap, warn before saving with missing assets, move the restore point only after success), Delete Theme order and prompt, Restore Defaults messaging and `_1` duplicate files inside zips, a theme log file, and repair of the already-damaged themes (Default, Football, Resize Test). These come later, one handoff at a time.
