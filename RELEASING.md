# Releasing a new version

Steps to cut a new release of **Screen Awake**. Replace `X.Y.Z` with the new
version (e.g. `1.1.0`) throughout.

## 1. Bump the version in two places

- `ScreenAwakeApp/ScreenAwake.csproj` → `<Version>X.Y.Z</Version>`
- `ScreenAwake-Setup/ScreenAwake.iss` → `#define MyAppVersion "X.Y.Z"`

Keep them identical. Commit the bump:

```powershell
git commit -am "Bump version to X.Y.Z"
```

## 2. Publish the self-contained app

```powershell
cd ScreenAwakeApp
dotnet publish ScreenAwake.csproj -c Release -r win-x64
cd ..
```

Output: `ScreenAwakeApp/bin/Release/net10.0-windows/win-x64/publish/ScreenAwake.exe`

## 3. Stage the exe the installer bundles, then build the installer

```powershell
copy ScreenAwakeApp\bin\Release\net10.0-windows\win-x64\publish\ScreenAwake.exe ScreenAwake-Setup\
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" ScreenAwake-Setup\ScreenAwake.iss
```

Output: `Screen-Awake-Installer.exe` (in the repo root; it is git-ignored).

> If Inno Setup isn't installed: `winget install --id JRSoftware.InnoSetup -e`

## 4. Push, then create the GitHub release

```powershell
git push origin main

gh release create vX.Y.Z `
  "Screen-Awake-Installer.exe#Screen-Awake-Installer.exe (Windows x64)" `
  --repo SakibASP/Background-Runner-Windows `
  --title "Screen Awake X.Y.Z" `
  --notes "Release notes here..." `
  --latest
```

> Requires the GitHub CLI (`winget install --id GitHub.cli`) and an
> authenticated session (`gh auth login`, or an existing GitHub credential
> from a prior `git push`).

The `README.md` download link points at `/releases/latest`, so it updates
automatically once the new release is marked latest.

## Notes

- Built binaries (`*.exe`) are intentionally **not** committed — they ship only
  as release assets. See `.gitignore`.
- Keep the `AppId` GUID in `ScreenAwake.iss` **unchanged** across versions so
  Windows treats each installer as an upgrade of the same app rather than a
  separate install.
