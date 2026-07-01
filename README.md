# Background Runner (Screen Awake)

A tiny Windows app that keeps your screen on and your **Teams / Slack status "Available"** while you're away from the keyboard — without fighting you for the mouse.

### ⬇️ [Download the installer](https://github.com/SakibASP/Background-Runner-Windows/releases/latest)

Grab `Screen-Awake-Installer.exe` from the latest release and run it — per-user install, no admin required.

## How it works

When you've been idle past a threshold you set, the app fires a **tiny, genuine input pulse** once per second. That pulse resets the Windows idle timer — the *same* timer Microsoft Teams uses to decide you're "Away" — and keeps the display awake.

The moment you actually touch the mouse or keyboard, it detects the real input and **pauses**, so it never interferes while you work. When you go idle again, it resumes.

> Note: A plain `SetCursorPos` cursor nudge is **not** enough — Windows doesn't count it as input, so Teams still goes Away. The app uses `SendInput` (a real 1px move-and-return event) specifically to reset the idle timer.

## App

A **C# / WPF** app with a modern dark UI. It's self-contained (bundles the .NET runtime) and ships as a real per-user installer. Source is in [`ScreenAwakeApp/`](ScreenAwakeApp/).

## Build

### C# app
```powershell
cd ScreenAwakeApp
dotnet publish -c Release -r win-x64
```

### Installer (single Setup.exe)
Compile [`ScreenAwake-Setup/ScreenAwake.iss`](ScreenAwake-Setup/ScreenAwake.iss) with [Inno Setup](https://jrsoftware.org/isinfo.php). It produces a per-user installer (no admin required) that registers in **Settings → Apps** for clean uninstall.

> Built binaries (`*.exe`) are git-ignored — attach them to a GitHub **Release** rather than committing them.

## Usage

1. Launch **Screen Awake**.
2. Set how long to stay awake, and how long you must be idle before it starts.
3. Click **Start**. Click **Stop** (or close the window) any time.

## Limitation

This only works while your session is **unlocked**. If the PC locks (Win+L or a policy auto-lock), Teams shows Away regardless — no app can inject input into a locked session.
