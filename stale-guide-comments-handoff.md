# Handoff 6: Fix stale "solid guide line" comments (comment and doc wording only), then commit

## Goal and why
Project: 4RCADE 5TICK, a C#/WPF MAME frontend. The Theme Builder's layer guides (Edge guides and Center guides) are now **dashed** (4 px dash, 4 px gap; `GuideDashPattern` in `MainWindow.xaml.cs`). Some comments and doc lines elsewhere still describe them as solid, for example `Models/ConfigurationSettings.cs` around line 97 says "four 1px lines". Find every such spot and fix the wording, then commit the result. This pass also tests the new CLAUDE.md rule about looking inside files (see the last section).

## What to do
1. **Search for stale wording.** Use the **Grep tool** (not shell commands) over the whole project for the word "guide" (case-insensitive) in `*.cs`, `*.xaml` and `*.md` files, and read the matching spots with the **Read tool** (offset and limit) as needed. Skip `bin`, `obj` and anything git-ignored.
2. **Fix only wording that says or implies the guides are solid.** Typical fix: "four 1px lines" becomes "four dashed 1px lines" (or similar). The thickness is still 1 px, so a bare "1px" is not wrong by itself; change it only where the sentence reads as describing solid lines or does not mention dashes where it clearly should (for example a comment that lists how the guides are drawn).
3. **Where to fix:** comments in `.cs` and `.xaml` files (comment text only) and wording in `themes.md` and `CLAUDE.md`. In `CLAUDE.md` change only wording about the line style; nothing else.
4. **Report every spot you looked at and decided to leave alone**, with a one-line reason, so I can see the search was complete.

## Do NOT change
- **The Background grid.** It is a separate feature and really is solid 1 px lines. Leave every comment and doc line about `BackgroundGridLayer`, `SetBackgroundGrid`, `UpdateBackgroundGrid`, "Theme Builder Background Grid" and the Grid checkbox / spacing / offset exactly as they are, even if they say "1px" or "lines".
- Any code, XAML element or attribute, resource, or setting. Comments and doc wording only.
- The comments I already updated for the dashed guides (the `ShowGuides` comment, the `GuideDashPattern` comment, the section comment in `MainWindow.xaml`, the themes.md 3.4 paragraph and its change log entry). Leave them unless they are wrong.
- Don't stash, revert or reset anything. Leave the untracked spec files alone (never stage them). Stay inside the project folder.
- `.cs` and `.xaml` files are CRLF and `.md` files are LF. Use the Edit tool, and afterward confirm with `git diff --stat` that no file shows a whole-file change.

## Build, then commit
1. Build once: `dotnet build ArcadeStick.csproj` completes with 0 errors and 0 warnings. (A comment-only change cannot break the build, so one build at the end is enough.)
2. Check `git diff` and `git diff --stat`: every changed line must be a comment or doc wording line, with no whole-file line-ending changes.
3. **Commit, only if both checks pass and something actually changed.** This pass overrides the "Never commit" rule in CLAUDE.md, for this one commit only (I explicitly asked for it). Stage only the files you changed, by name (`git add <file> <file>`); never `git add -A`, `git add .` or `git commit -a`, and never stage the untracked spec files. Use this message:
   ```
   Comments: describe the layer guides as dashed

   Fix leftover comments and doc wording that still called the Edge and
   Center guides solid lines; no code changes.
   ```
   (If you add a Co-Authored-By trailer, as in earlier commits, that is fine.)
4. **Do NOT push.** I will push myself. If the checks fail or nothing needed fixing, do not commit; just report.
5. Expect the permission system to ask me to approve `git add` and `git commit`; that is intended and I will approve them. If anything else is refused or asks, do not look for another way around it; stop and tell me which action it was.

## Report and permission test (required)
Report the final `git diff --stat`, the commit hash and `git status` after the commit (the untracked spec files should still be untracked), and the list of spots you left alone.

CLAUDE.md now says to look inside files with the Read and Grep tools and not shell pipelines (`sed`, `cut`, `awk`, `tail`, `head`, `wc`, `echo`, `od`). Follow it. List **every shell command you ran**, in order, and say which steps you did with the Grep and Read tools instead. Expected shell commands: `git status`, `git diff`, `git diff --stat`, `dotnet build`, `git add` and `git commit`. If you needed anything else, say what and why.
