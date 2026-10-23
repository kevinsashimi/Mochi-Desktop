# Window-placement recovery (1.2.12)

Validated on 23 October 2026. The user tested the 1.2.11 preview based on
`637885c`, reported that it looked good so far, and approved publishing the fix.
The release uses version 1.2.12.0 with matching embedded release notes,
executable and generated update manifest. The placement correction is
unchanged from the approved preview.

## Finding and correction

Version 1.2.11 still calls `Native.KeepBehindApps` directly from the companion's
Shown handler and periodic timer. A failed `SetWindowPos` throws a
`Win32Exception`, so Windows error 5 (access denied), or a stale window/desktop
anchor, can interrupt startup or escape from the timer event. The timer's old
deadline was advanced only after successful placement.

Startup, periodic placement and Manual mode exit now use a shared guarded
placement method. It catches only `Win32Exception` from that operation and
sets the next check before attempting placement. Startup, rendering and the
timer continue; another attempt runs after one second with a fresh desktop
anchor. Repeated failures neither display an exception dialog nor cause a
rapid retry loop. Missing, closing and disposed windows are skipped, and
background placement remains suspended during Manual mode.

The native desktop/WorkerW selection, behind-app anchor, non-topmost behavior,
`SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE` flags and `WM_WINDOWPOSCHANGING`
handling are unchanged. Unrelated exceptions are not suppressed.

## Validation

- Microsoft Roslyn 4.8 build with Microsoft .NET Framework 4.8 reference
  assemblies, `-noconfig`, `-nostdlib+` and explicit reference paths: passed.
- Native executable resource verification: manifest matches source;
  Windows compatibility, DPI, `asInvoker`, icon and version resources retained.
- Full release `--self-test`: 41 passing groups (10 companion, 8 Manual mode,
  7 updater, 5 Playful Mode, 5 wallpaper, 3 release notes, 3 placement).
  This includes the updater's compiled `TrimEnd(char[])` compatibility check.
- New `--placement-self-test`: injected error 5 during the actual startup
  handler; error 1400 during the timer; recovery, persistent failure cadence,
  long timer gaps, Manual mode suspension/resumption, handle lifecycle,
  unrelated exception propagation and real native success/failure checks.
- Approved-preview live UI harness under Wine 10 / Wine Mono 9.4 in an isolated virtual desktop:
  three passing checks covering real Shown/timer dispatch with injected
  failures, recovery below a normal application without taking focus, and
  access denied during actual Manual mode exit followed by timer recovery.
  No unhandled UI exceptions or startup error log occurred.

The bare Xvfb/Wine session reports a shell handle outside the tested windows'
top-level order, so its ordering assertion is unsuitable. The isolated virtual
desktop check passed and exercised the no-Explorer `HWND_BOTTOM` fallback.
During release verification, the sibling-order assertion also failed in a
virtual desktop, both for 1.2.12 and for the exact approved 1.2.11 preview.
The native topmost-bit assertion after Manual mode also failed identically
on both binaries. Both resumed successful native placement calls with no
unhandled UI exceptions, but Wine retained the topmost bit. These ordering
assertions are therefore not release pass results. The release UI check
passed startup/timer failure recovery and initial non-topmost/focus checks;
the 41 self-test groups passed independently. The normal placement code is
unchanged from the approved preview. Native Windows Explorer/WorkerW
layering is not certified by these Wine tests.

## Windows follow-up checks

Quit the existing Mochi, extract the entire enclosing preview folder, and run
its `Mochi.exe`. Confirm normal startup and animation, with no access-denied
dialog. Open and switch between applications: Mochi should remain behind them
and above the desktop. Enter and exit Manual mode, then repeat the layering
check. Also check a launch at Windows sign-in and recovery after Explorer
restarts or the display arrangement changes. The user reported a successful
preview check; the exact Windows scenarios covered were not specified.

The approved preview and its UI harness are retained in
`/workspace/mochi-cloud/placement-preview/`. Release build and test outputs
are retained separately in `/workspace/mochi-cloud/placement-release-1.2.12/`.
