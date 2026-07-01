Screen Awake  -  keeps your screen on and your Teams/Slack status "Available".

HOW IT WORKS
  When you're idle it fires a tiny, genuine input pulse once a second, which
  resets the Windows idle timer (the same one Teams uses to mark you "Away")
  and keeps the display on. The moment you touch the mouse or keyboard it
  detects the real activity and pauses, so it never fights you for the cursor.

INSTALL
  Double-click  "Screen-Awake-Installer.exe"  (in the parent folder).
  - No administrator rights needed; it installs just for your user.
  - Optional checkboxes let you add a desktop shortcut and/or start the app
    automatically when you sign in to Windows.

USE
  - Open "Screen Awake" from the Start Menu or Desktop.
  - Set how long to stay awake and how long you must be idle before it starts.
  - Click Start. Click Stop (or close the window) any time.

UNINSTALL
  Windows Settings > Apps > Installed apps > Screen Awake > Uninstall.
  (Everything it added is removed cleanly.)

NOTE
  This only works while your session is unlocked. If the PC locks (Win+L or a
  policy auto-lock), Teams will show Away regardless - no app can inject input
  into a locked session.

---
This folder also contains the build sources (ScreenAwake.iss, ScreenAwake.exe,
icon.ico) used to produce the installer. To rebuild the installer, compile
ScreenAwake.iss with Inno Setup.
