# Handoff: READ-ONLY audit of ImagePath corruption, and of load/save/delete data-loss paths

You have no memory of the chat that produced this file, so everything you need is here. Project: **4RCADE 5TICK**, a C#/WPF MAME frontend. The developer is a first-time developer working independently, so explain findings in plain language.

**This task is investigation only. Do not change source code, project files, themes, zips, or anything under `F:\Arcade Stick\Data\4RCADE5TICK_files\`. Do not commit.** The fix will be a separate handoff written after the developer reviews your report. If you need to inspect a zip, extract it to a temp folder outside the data folder.

## Background

Today, a Restore Defaults feature was added (see `Models/DefaultLayerSets.cs`, `Models/DefaultLayerSets.json`, `BtnRestoreDefaults_Click` and `PromptForRestoreDefaults` in `Views/AdvancedThemesTabControl.xaml.cs`). The "Everything" mode copies all layer properties including `ImagePath`, and (per an earlier decision) points the default images at `4rcade5tick_files/assets/<file>`.

A previous read-only audit (same session family) found these facts about theme file handling, which you should re-verify rather than trust:

- The theme **zip** (`Themes\<name>.zip`) is the source of truth; `Themes\Extracted\<name>\` is a working copy.
- `LoadTheme` (`ViewModels/MainViewModel.cs` around line 1517) deletes the extracted folder, then re-extracts from the zip.
- `BtnSaveTheme_Click` (`Views/ThemesManagerControl.xaml.cs` around line 362) builds a temp folder, **deletes the old zip**, then zips the temp folder. It uses the **dropdown selection**, not necessarily the loaded theme.
- `BtnDeleteTheme_Click` (same file, around line 411) deletes the zip first, then the extracted folder, with no check that the folder delete worked.

## Evidence from today's testing

1. **Photoshop lock + Load Theme wipes the folder.** With Photoshop holding the theme's `assets` folder (it had used it as a Save for Web destination), pressing Load Theme on "Resize Test" showed:
   > Processing Failure: Error reading theme properties: The process cannot access the file '\\?\F:\Arcade Stick\Data\4rcade5tick_files\Themes\Extracted\Resize Test\assets' because it is being used by another process.

   Afterward, everything in the theme's extracted folder was gone. The zip survived. (The developer believes the same thing happened to a theme called "Electronic", whose zip was later lost too, probably via Save Changes. Electronic's end state: folder and empty `assets` folder present, no `theme.json`, zip gone, nothing in the Recycle Bin.)

2. **Stacked ImagePath in a saved theme.json.** In `Themes\Extracted\Resize Test\theme.json`, a layer has:
   `"ImagePath": "4rcade5tick_files\\Themes\\Extracted\\Resize Test\\4rcade5tick_files\\Themes\\Extracted\\Resize Test\\assets\\no_preview.png"`
   i.e. the theme's own folder path appears twice in front of `assets\no_preview.png`. The developer ran Restore Defaults ("Everything") on this theme earlier because of missing-file errors.

3. **"Some Assets Missing" on Save.** Pressing Save Changes shows:
   > Theme saved, but 1 asset file(s) could not be found and were not included: `4rcade5tick_files\Themes\Extracted\Resize Test\4rcade5tick_files\Themes\Extracted\Resize Test\4rcade5tick_files\Themes\Extracted\Resize Test\assets\no_preview.png`

   (three copies of the prefix now, so something adds another on resolve or save). Save still reports "Theme saved".

4. **Cross-theme contamination.** The developer then loaded the **Default** theme (the Active Theme dropdown showed "Default") and pressed Save Changes immediately. The same popup appeared, still naming the **Resize Test** path. So either Default's layers now hold Resize Test's stacked path, the in-memory layout carried over between themes, or Save wrote a different theme than the one shown. `Default.zip` may have just been overwritten with a damaged copy.

5. **Duplicate zips.** Some theme zips now have `_1` duplicates (for example `<name>_1.zip`) in `Themes`. Nobody knows what creates them.

## Questions to answer (Step 1: investigate)

### A. ImagePath handling, end to end
1. Trace every place `ImagePath` is **written, read, normalized, made relative/absolute, resolved, or bundled** (load, save, `BundleThemeAsset`, theme-builder image pickers, Restore Defaults, `ResolveLayout`, `BuildDefaultLayout`, theme extraction, thumbnail code). List each with file and line.
2. What **exact value** does Restore Defaults "Everything" store in `ImagePath` for the default images? Is it absolute, relative to the app folder, relative to the theme folder?
3. How do load, save and bundling interpret a stored `ImagePath`? Is there anywhere a path is combined with the theme folder when it should not be, or combined again on each pass? Show exactly how a path ends up as `<themeFolder>\<themeFolder>\assets\file.png`, and how it gains another copy on each save or load. Reproduce the logic on paper or with a throwaway console snippet in a temp folder, not in the data folder.
4. Is this stacking caused by the new Restore Defaults code, or does the underlying behavior also exist without it (for example, whenever a stored path is absolute or starts with `4rcade5tick_files`)? Be explicit about which it is.

### B. Cross-theme contamination
5. How could Default's save (or load) end up with Resize Test's path? Specifically check: static or cached state, `_currentLayout` or `_settings.Layouts` being reused between themes, **`DefaultLayerSets` caching parsed sets and handing out the same objects that later get mutated** (shared references instead of copies), the dropdown selection vs. loaded theme mismatch in `BtnSaveTheme_Click`, and the order of operations in `LoadTheme`.
6. Can Restore Defaults mutate the shared default sets? Confirm that restore copies values into the active layers and never keeps a reference to the cached default objects.

### C. Load / save / delete data loss
7. Confirm the Load behavior in evidence item 1. Which exact call fails with the locked `assets` folder, and what has already been deleted by then? Why does a failed load leave an empty working folder?
8. Explain every way a **zip can be lost** or replaced: Save's delete-then-write, Delete Theme, and whatever creates the `_1` duplicates. Find the code that produces `_1` names (search for suffix, "unique name", "copy of", `_1`, `_{n}`). Say when it triggers.
9. What does Save do when an asset file is missing? It reports "Theme saved" and carries on. What ends up in the zip and in `theme.json`? Could it write a theme that can no longer load correctly?
10. Check what happens on Save when the dropdown selection differs from the loaded theme. Show the exact variable that decides the zip name and the exact variable that decides the layouts written.

### D. Existing damage
11. Without changing anything, describe what is likely wrong in the developer's existing themes (Default, Resize Test, Electronic if anything survives): stacked `ImagePath` values, wrongly overwritten zips, `_1` duplicates. Inspect the real files read-only if useful (zips extracted to a temp folder outside the data folder) and report whether `Default.zip` currently contains stacked or Resize Test paths.

## Constraints

- No edits, no commits, no deletes, no writes under `F:\Arcade Stick\Data\4RCADE5TICK_files\`.
- Do not run the app or anything that saves, loads or deletes themes.
- Do not fix anything yet. Report it.

## Deliverable (written in the chat)

1. **Root cause(s)** of the stacked `ImagePath`, with the exact code path and a plain-language walkthrough of how each round adds another copy.
2. **Whether Restore Defaults is responsible**, partly responsible, or only exposed the bug. Own it plainly if the new code is at fault.
3. **How Default got contaminated** (or why it did not and what the popup really means).
4. **Every way a zip or the working folder gets lost**, including the `_1` duplicates, ranked by how likely each is to match what happened.
5. **A proposed fix plan** split into small independent fixes in priority order (for example: path handling, shared-state mutation, load order and failure recovery, save order and write-then-swap, delete order, dropdown vs loaded-theme mismatch, logging), each with the files it would touch and how to verify it. Mark clearly that nothing is implemented.
6. **A proposed repair approach** for already-corrupted theme.json files (how to detect and normalize stacked paths), as a proposal only.
7. Anything else risky you noticed.
