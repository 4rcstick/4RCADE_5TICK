# Handoff: small docs wording fixes after theme name validation (docs only)

You have no memory of the chat that produced this file, so everything you need is here. Project: **4RCADE 5TICK**, a C#/WPF MAME frontend.

**Docs only.** Edit `themes.md` and `CLAUDE.md` (both LF; keep their endings). No code, project, JSON, theme or zip file may change. Do not commit. This is a small change, so there is no STEP 1 audit: make the edits below, then report.

## Why

The theme name validation passes (decision 73, `ThemePaths`, the leading-dot follow-up) are already documented, but a review of the pasted docs found four small wording gaps. Each edit below gives the **exact old text** to find and the **exact new text** to put in its place. Find each old string first. If an old string is not found exactly once, **do not guess**: stop on that edit and report what you found instead.

## Edits

### themes.md

**1. Header ("Last verified against code" line, line 3).** Add theme name validation to the 2026-11-01 list.

Old:
```
Last verified against code: 2026-11-01 for theme file safety (3.4, section 7, decisions 71 and 72), Restore Defaults (decision 70) and the Theme Info view (3.22);
```
New:
```
Last verified against code: 2026-11-01 for theme file safety (3.4, section 7, decisions 71 and 72), theme name validation (section 7, decision 73), Restore Defaults (decision 70) and the Theme Info view (3.22);
```

**2. Section 3.4, the Save Changes bullet.** Mention that a typed new name is validated before the overwrite prompt.

Old (a fragment inside the bullet that starts "**Save Changes** (Themes Manager row..."):
```
and `[New Theme...]` with an existing name asks first, No by default.
```
New:
```
and a typed `[New Theme...]` name is validated first (`ThemePaths` Check 1, decision 73); a valid name that already exists asks first, No by default.
```

**3. Section 6b, the `LoadSavedThemeSnapshot` entry.** One refusal case is now logged.

Old:
```
A failed startup snapshot logs nothing; it could log through `ThemeLog`.
```
New:
```
A failed startup snapshot logs nothing, except a refused path, which writes a `Startup:` `PathRefused` line (decision 73); its other failures could log through `ThemeLog`.
```

**4. Section 10 (change log), append one new entry at the very end of the file** (after the last bullet, `2026-11-01: **Theme name follow-up ...`), as a new line in the same style:
```
- 2026-11-01: **Docs wording after theme name validation (`handoff-docs-name-validation-wording.md`).** Docs only. The Last verified header now lists theme name validation (decision 73); section 3.4 says a typed new name is validated before the overwrite prompt; the 6b `LoadSavedThemeSnapshot` entry notes the `Startup:` `PathRefused` line; the `CLAUDE.md` Theme file safety heading now points at decisions 71 to 73.
```

### CLAUDE.md

**5. The "Theme file safety" section heading.** It should point at decision 73 as well.

Old:
```
### Theme file safety (themes.md 3.4, section 7, decisions 71 and 72)
```
New:
```
### Theme file safety (themes.md 3.4, section 7, decisions 71 to 73)
```

(CLAUDE.md already lists `PathRefused` in its theme.log bullet and has the `ThemePaths` bullet, so nothing else changes there.)

## After the edits

- Run `git diff --stat` and confirm only `themes.md` and `CLAUDE.md` changed, with no whole-file diffs. The developer has committed the earlier passes, so the stat should show only these two files with a few lines each. If other files show changes, tell me which.
- No build is needed. If any code file shows a change, stop and tell the developer.

## Report

Say which of the five edits were made, any whose old text was not found exactly, and the `git diff --stat` output.
