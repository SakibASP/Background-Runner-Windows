"""
ScreenAwake - keeps your screen on by gently jiggling the mouse for a set time.

- Moves the cursor a couple of pixels back and forth every few seconds.
- Also tells Windows to stay awake (belt-and-suspenders, in case the mouse
  move alone isn't enough).
- No external libraries required (uses ctypes + tkinter from the standard lib).
"""

import ctypes
import time
import tkinter as tk
from tkinter import ttk, messagebox

# --- Windows API bits -------------------------------------------------------
user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32

# SetThreadExecutionState flags (keep display + system awake)
ES_CONTINUOUS = 0x80000000
ES_SYSTEM_REQUIRED = 0x00000001
ES_DISPLAY_REQUIRED = 0x00000002


def keep_awake(on: bool):
    """Ask Windows to keep the display + system on, or release the request."""
    if on:
        kernel32.SetThreadExecutionState(
            ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED
        )
    else:
        kernel32.SetThreadExecutionState(ES_CONTINUOUS)


def get_cursor_pos():
    pt = ctypes.wintypes.POINT() if hasattr(ctypes, "wintypes") else None
    # define POINT inline to avoid importing wintypes explicitly
    class POINT(ctypes.Structure):
        _fields_ = [("x", ctypes.c_long), ("y", ctypes.c_long)]
    p = POINT()
    user32.GetCursorPos(ctypes.byref(p))
    return p.x, p.y


def set_cursor_pos(x, y):
    user32.SetCursorPos(int(x), int(y))


class _LASTINPUTINFO(ctypes.Structure):
    _fields_ = [("cbSize", ctypes.c_uint), ("dwTime", ctypes.c_uint)]


def last_input_tick():
    """Tick (ms) of the last keyboard/mouse input event, per Windows.

    This is the SAME timer Microsoft Teams uses to decide you're 'Away'. Plain
    SetCursorPos does NOT update it -- only a real input event (SendInput) does.
    """
    lii = _LASTINPUTINFO()
    lii.cbSize = ctypes.sizeof(lii)
    user32.GetLastInputInfo(ctypes.byref(lii))
    return lii.dwTime


# --- SendInput: injects a *genuine* input event that resets the idle timer ---
class _MOUSEINPUT(ctypes.Structure):
    _fields_ = [
        ("dx", ctypes.c_long),
        ("dy", ctypes.c_long),
        ("mouseData", ctypes.c_ulong),
        ("dwFlags", ctypes.c_ulong),
        ("time", ctypes.c_ulong),
        ("dwExtraInfo", ctypes.c_size_t),
    ]


class _INPUTUNION(ctypes.Union):
    _fields_ = [("mi", _MOUSEINPUT)]


class _INPUT(ctypes.Structure):
    _fields_ = [("type", ctypes.c_ulong), ("u", _INPUTUNION)]


INPUT_MOUSE = 0
MOUSEEVENTF_MOVE = 0x0001


def _send_relative_move(dx, dy):
    inp = _INPUT()
    inp.type = INPUT_MOUSE
    inp.u.mi = _MOUSEINPUT(dx, dy, 0, MOUSEEVENTF_MOVE, 0, 0)
    user32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(inp))


def wake_pulse():
    """Fire a real 1px move-and-return input event.

    Unlike SetCursorPos, this counts as genuine activity, so it resets the
    Windows idle timer and keeps Teams (and Slack, etc.) showing 'Available'.
    The pointer is restored to its exact spot immediately, so it's barely
    perceptible.
    """
    x, y = get_cursor_pos()
    _send_relative_move(1, 0)
    set_cursor_pos(x, y)


# --- App --------------------------------------------------------------------
class App:
    def __init__(self, root):
        self.root = root
        self.running = False
        self.end_time = 0.0

        root.title("Screen Awake")
        root.resizable(False, False)

        frm = ttk.Frame(root, padding=20)
        frm.grid()

        ttk.Label(frm, text="Keep my screen on for:").grid(
            column=0, row=0, columnspan=3, sticky="w"
        )

        self.minutes = tk.StringVar(value="30")
        vcmd = (root.register(self._validate_int), "%P")
        self.entry = ttk.Entry(
            frm, textvariable=self.minutes, width=8,
            validate="key", validatecommand=vcmd, justify="center",
        )
        self.entry.grid(column=0, row=1, pady=(6, 0), sticky="w")
        ttk.Label(frm, text="minutes").grid(column=1, row=1, pady=(6, 0), sticky="w")

        ttk.Label(frm, text="Only move after I'm idle for:").grid(
            column=0, row=2, columnspan=3, pady=(12, 0), sticky="w"
        )
        self.idle_after = tk.StringVar(value="5")
        self.idle_entry = ttk.Entry(
            frm, textvariable=self.idle_after, width=8,
            validate="key", validatecommand=vcmd, justify="center",
        )
        self.idle_entry.grid(column=0, row=3, pady=(6, 0), sticky="w")
        ttk.Label(frm, text="seconds").grid(column=1, row=3, pady=(6, 0), sticky="w")

        self.start_btn = ttk.Button(frm, text="Start", command=self.start)
        self.start_btn.grid(column=0, row=4, pady=(16, 0), sticky="ew")
        self.stop_btn = ttk.Button(frm, text="Stop", command=self.stop, state="disabled")
        self.stop_btn.grid(column=1, row=4, columnspan=2, pady=(16, 0), sticky="ew")

        self.status = ttk.Label(frm, text="Idle.", foreground="gray")
        self.status.grid(column=0, row=5, columnspan=3, pady=(14, 0), sticky="w")

        root.protocol("WM_DELETE_WINDOW", self.on_close)

    def _validate_int(self, proposed):
        return proposed == "" or proposed.isdigit()

    def start(self):
        try:
            mins = int(self.minutes.get())
        except ValueError:
            mins = 0
        if mins <= 0:
            messagebox.showwarning("Screen Awake", "Please enter a number of minutes greater than 0.")
            return

        try:
            self.idle_threshold = max(1, int(self.idle_after.get()))
        except ValueError:
            self.idle_threshold = 5

        self.running = True
        self.moving = False
        # Timestamps (ms ticks) used to tell OUR injected input apart from the
        # user's real input, so "pause while you use the mouse" still works.
        self.last_pulse_tick = 0
        self.last_user_tick = kernel32.GetTickCount()
        self.end_time = time.time() + mins * 60
        keep_awake(True)
        self.start_btn.config(state="disabled")
        self.stop_btn.config(state="normal")
        self.entry.config(state="disabled")
        self.idle_entry.config(state="disabled")
        self._tick()
        self._jiggle()

    def stop(self):
        self.running = False
        keep_awake(False)
        self.start_btn.config(state="normal")
        self.stop_btn.config(state="disabled")
        self.entry.config(state="normal")
        self.idle_entry.config(state="normal")
        self.status.config(text="Stopped.", foreground="gray")

    def _jiggle(self):
        if not self.running:
            return
        now = kernel32.GetTickCount()
        last_input = last_input_tick()

        # If the most recent input arrived AFTER our last wake pulse, it must
        # have come from the user (keyboard or mouse) -> they're active.
        if last_input > self.last_pulse_tick + 200:
            self.last_user_tick = last_input

        user_idle = (now - self.last_user_tick) / 1000.0
        self.moving = user_idle >= self.idle_threshold

        if self.moving:
            # Real input event -> keeps Teams/Slack 'Available' AND the screen on.
            try:
                wake_pulse()
            except Exception:
                pass
            self.last_pulse_tick = kernel32.GetTickCount()

        # Poll once a second so we react quickly when you take over the mouse.
        self.root.after(1000, self._jiggle)

    def _tick(self):
        if not self.running:
            return
        remaining = int(self.end_time - time.time())
        if remaining <= 0:
            self.stop()
            self.status.config(text="Done - time's up.", foreground="green")
            return
        m, s = divmod(remaining, 60)
        if self.moving:
            msg = f"Idle - keeping you Available. {m:02d}:{s:02d} left."
        else:
            msg = f"You're active - paused. {m:02d}:{s:02d} left."
        self.status.config(text=msg, foreground="green")
        self.root.after(1000, self._tick)

    def on_close(self):
        keep_awake(False)
        self.root.destroy()


if __name__ == "__main__":
    import ctypes.wintypes  # noqa: F401  (ensures wintypes is available)
    root = tk.Tk()
    App(root)
    root.mainloop()
