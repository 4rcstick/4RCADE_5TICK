# Handoff: READ-ONLY audit of every code path that can delete or empty theme files

You have no memory of the chat that produced this file, so everything you need is here. Project: **4RCADE 5TICK**, a C#/WPF MAME frontend. The developer is a first-time developer working independently, so explain findings in plain language.

## Goal and why

A theme was silently destroyed while the developer was testing, and they did not (knowingly) press Delete Theme. We need to find out which code could have done it. **This task is an investigation only. Do not change any code, files or themes.**

## What happened (the evidence)

- Theme name: **Electronic**.
- After the incident: its folder under `F:\Arcade Stick\Data\4RCADE5TICK_files\Themes\Extracted\Electronic` still exists, and its `assets` subfolder still exists, but **`theme.json` is gone, the `assets` folder is empty, and the theme's zip file is gone**. Nothing was in the Recycle Bin, so it was a direct delete, not a shell delete.
- The Electronic folder's Modified time is **10/31/2026 9:11 PM**. Nothing else in `Extracted` changed near that time. (A `testing` folder changed at 9:15 PM, which was the developer creating a test theme afterward.)
- The developer was testing the new **Restore Defaults** feature at the time, plus normal theme builder use. They do not remember the exact steps.
- For comparison, the normal **Delete Theme** button (with its confirmation prompt) was tested afterward and removes the zip AND all the folders completely. So the incident was NOT a plain Delete Theme. The leftover empty folders suggest something that removed files without removing folders, or a step that cleared contents and then failed or stopped partway.

## Recent changes that must be checked too

The Restore Defaults feature was added today. Confirm whether any of it can touch disk or theme files:

- `Models/DefaultLayerSets.cs`, `Models/DefaultLayerSets.json`
- `BtnRestoreDefaults_Click` and `PromptForRestoreDefaults` in `Views/AdvancedThemesTabControl.xaml.cs`
- Anything it calls (`StoreLayout`, `NormalizeZ`, `RebuildLayerList`, and so on).

Also note that Restore Defaults "Everything" mode sets image paths to files in `4rcade5tick_files/assets` (default.png, default_background.png, default_background.gif, no_preview.png). Check whether a later save, bundle, or cleanup step could treat those paths in a way that deletes or moves files, or clears a theme's assets folder.

## Step 1: Find every place that removes, clears, moves or overwrites theme files

Search the whole codebase (not only the theme builder) for calls such as: `File.Delete`, `Directory.Delete`, `FileSystem.DeleteDirectory/DeleteFile`, `File.Move`, `Directory.Move`, `File.Copy` with overwrite, `File.WriteAllText/WriteAllBytes` on theme files, `ZipFile.ExtractToDirectory`, `ZipFile.CreateFromDirectory`, `ZipArchive` Update/Delete entries, `Directory.CreateDirectory` followed by clears, and any "clean up", "purge", "sync", "temp", "backup" or "orphan" routines. Include code that runs at **app startup, theme load, theme switch, app close, save, Save As, rename, import/export, new theme, new size layout, reset, and Default theme extraction**.

For each hit, record:

1. File and line (and method name).
2. What it deletes or overwrites, and where.
3. **What triggers it** (button, startup, theme switch, timer, background task, etc.).
4. Whether it asks the user to confirm.
5. Whether it can run against the **currently active/loaded theme**.
6. What happens if it **fails partway** (exceptions swallowed? try/catch with empty catch? file locked by Explorer, antivirus, or the app's own image/video handles?). Does it leave a half-emptied folder?
7. Whether it runs on a background thread or async and could race with another save/load.

## Step 2: Understand the zip/folder relationship

Explain how the app treats a theme's **zip** versus its **extracted folder**:

- Which is the source of truth, and which is a cache?
- When is the extracted folder wiped and re-extracted from the zip?
- When is the zip rewritten from the extracted folder (Save)? What is the exact order of operations (delete old zip first? write temp zip then swap? clear extracted folder then re-extract?).
- Is there any code that removes a theme's zip when the extracted folder looks "empty" or "invalid", or removes an extracted folder when its zip is missing (or the reverse)? A cascade like that would explain losing both.
- Where do the zip files live (path), and how does the app decide a theme's name/ID from them?

## Step 3: Rank the suspects

Using the evidence above (zip gone, `theme.json` gone, assets emptied, folders left behind, modified 9:11 PM while testing Restore Defaults and normal theme builder use), rank the code paths from most to least likely to have caused it. For the top suspects, explain the exact sequence of user actions that would trigger them, in plain steps the developer could try to reproduce. Be honest about uncertainty. If nothing in the code explains it, say so rather than inventing a cause.

## Constraints: things NOT to do

- **Do not modify any source file, project file, theme, zip, or anything in `F:\Arcade Stick\Data\4RCADE5TICK_files\`.**
- Do not run the app or any script that deletes, moves or rewrites files. Reading code and listing/reading files is fine.
- Do not "fix" anything you find. Report it.
- Do not commit anything.

## Deliverable

A report back to the developer (written in the chat; do not create new project files) with:

1. A table of every delete/clear/overwrite path (columns per Step 1, kept brief).
2. The zip/folder lifecycle explanation (Step 2).
3. The ranked suspect list with reproduction steps (Step 3).
4. A short list of **proposed** safeguards, clearly marked as proposals only, for example: write-then-swap instead of delete-then-write, refusing to delete a zip if the extracted folder is empty, checking return values and not swallowing exceptions, and logging every delete with the caller and path to a log file so the next incident is traceable. Do not implement them.
5. Anything you noticed that was surprising or risky, even if unrelated to this incident.
