---
name: release
description: >-
  Cuts a GitHub release for the API Tester Windows app: asks for version if
  missing, bumps ApiTester.csproj, commits/pushes main, publishes a
  self-contained exe, zips with version in the filename, creates the release
  with notes, and returns the release URL. Use when the user asks to release,
  cut a version, ship a build, publish to GitHub Releases, or run /release.
---

# API Tester release

End-to-end release for this WPF app to GitHub Releases.

Repo: `manofpants/API_Tester`  
Branch: `main`  
Asset naming: `API_Tester-win-x64-vX.Y.Z.zip` (version **required** in the zip name)  
Zip contents: `ApiTester.exe` only (self-contained single-file)

## Workflow

Copy and track:

```
Release progress:
- [ ] 1. Resolve version
- [ ] 2. Bump project version
- [ ] 3. Commit and push main
- [ ] 4. Publish + zip
- [ ] 5. Create GitHub release
- [ ] 6. Return release URL
```

### 1. Resolve version

If the user did **not** provide a version (e.g. `1.2.0` or `v1.2.0`):

1. Read current `<Version>` from `ApiTester.csproj`
2. Ask the user what the new version should be
3. **Stop and wait** — do not guess or auto-bump

Normalize input: strip a leading `v` for the project version (`1.2.0`). Use `v1.2.0` for the git tag and zip suffix.

If the version is already provided, proceed.

### 2. Bump project version

In `ApiTester.csproj` set:

```xml
<Version>X.Y.Z</Version>
<FileVersion>X.Y.Z.0</FileVersion>
<InformationalVersion>X.Y.Z</InformationalVersion>
```

Ensure working tree is on `main` and up to date (`git pull --ff-only origin main` when possible).

### 3. Commit and push main

Stage **all** non-secret project changes (including the version bump and any unfinished work meant for this release).

Commit with a concise message focused on why, e.g.:

```text
Release X.Y.Z with <short summary of changes>.
```

Push:

```powershell
git push -u origin main
```

Do not force-push. Do not create the GitHub release until `main` is pushed.

### 4. Publish + zip

From the repo root (PowerShell):

```powershell
if (Test-Path .\publish) { Remove-Item .\publish -Recurse -Force }
if (Test-Path .\API_Tester-win-x64-vX.Y.Z.zip) { Remove-Item .\API_Tester-win-x64-vX.Y.Z.zip -Force }

dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\publish

Compress-Archive -Path .\publish\ApiTester.exe `
  -DestinationPath .\API_Tester-win-x64-vX.Y.Z.zip -Force
```

Replace `X.Y.Z` with the real version.  
Do **not** use the legacy unversioned name `API_Tester-win-x64.zip`.

`publish/` and `*.zip` are gitignored — do not commit them.

### 5. Create GitHub release

Write release notes from the commits/changes since the previous tag (features, fixes, UX). Keep them scannable.

```powershell
gh release create vX.Y.Z .\API_Tester-win-x64-vX.Y.Z.zip `
  --title "API Tester vX.Y.Z" `
  --notes @"
## What's new
- ...

## Download
Download **API_Tester-win-x64-vX.Y.Z.zip**, unzip, and run ApiTester.exe.
No .NET install required. If SmartScreen appears: More info → Run anyway.
"@
```

Notes body is shown in the app’s update confirmation modal — keep it clear and useful.

### 6. Return the release URL

Always end by giving the user the link:

```text
https://github.com/manofpants/API_Tester/releases/tag/vX.Y.Z
```

Also rebuild/relaunch the local Debug app if the workspace “re-run after update” rule applies.

## Updater compatibility

The in-app updater prefers assets named:

1. `API_Tester-win-x64-vX.Y.Z.zip`
2. `API_Tester-win-x64-X.Y.Z.zip`
3. Legacy `API_Tester-win-x64.zip`

New releases must use the versioned form (1).

## Do not

- Release without asking when version is unspecified
- Ship an unversioned zip name
- Create the GitHub release before pushing `main`
- Include `.pdb` or the whole `publish` folder in the zip
- Force-push `main`
