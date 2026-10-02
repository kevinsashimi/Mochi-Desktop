# Playful Mode QA — 1.1.4

Verified in the cloud on 2 October 2026 (Asia/Singapore) with Mono 6.12, Wine 10 / Wine Mono 9.4, and a 1920×1080 Xvfb display. **Live Windows Explorer icon movement has not been verified in this environment.**

## Behavior

- Off by default; enable and save **Playful Mode (mischievous icons)** in Settings. It is absent from both right-click menus and independent of swimming frequency.
- After a random 180–300 seconds, Mochi approaches an icon on his current monitor, grabs it with his fins or mouth, carries it on a varied route, drops it, and giggles. A fresh wait begins after the trip. Ordinary swimming can be paused.
- Only desktop icon coordinates change. File names, contents, and filesystem locations are untouched. Mochi does not change Windows' Auto arrange or grid settings.
- Manual interactions interrupt trips. An unfinished move is restored when the icon is still at Mochi's last position and its original cell is available. User moves and completed drops are preserved.
- Hidden icons, Auto arrange, an unavailable Shell view, or no suitable free destination cause the attempt to be skipped. Resume after a long pause does not queue multiple pranks.
- Checking controls or saving unchanged Settings preserves the pending countdown. Blocked attempts retry after 30 seconds. Feeding, petting, play, and swimming finish before another automatic turn starts. Pranks and the regular feed/pet/play cycle have independent deadlines; the oldest due turn runs first, with a three-second handoff. Manual interactions preserve pending deadlines. Each stream reschedules only itself on completion.
- **Playful status** in Settings reads the current desktop availability, current-monitor icon count, pending countdown, and last result. Native connection errors include the failed operation and HRESULT. `playful-status.txt` beside the preferences stores the latest result without icon identities or filenames.
- **Save & try now** requires the enabled checkbox, saves preferences, closes Settings, and requests a prank after one second. A blocked manual attempt explains the reason in a dialog. No diagnostic or test action is added to the right-click menus.
- Clicking elsewhere no longer cancels an active trip; clicks on Mochi or the selected icon still interrupt it. The previous icon can be selected again when alternatives have no usable route or artwork.

## Passed checks

The staged executable passed `--self-test` with exit code 0: ten companion groups, five updater groups, and five Playful Mode groups. The Playful Mode suite uses an in-memory desktop; **it never repositions real icons**.

- Settings defaults, Save/Cancel, persistence, old preference compatibility, and menu exclusion.
- 1,260 seeded routes across all three sizes, landscape/portrait/negative-coordinate monitors, and edge icons. Coverage includes all 16 heading sectors, straight/curved/mixed paths, varied distances, both animation variants and facings, and on-screen body/cargo bounds.
- Full pickup/carry/drop/giggle sequences; unchanged neighboring icons; interrupted-move restoration; user repositioning, deletion, occupied destinations, denied moves, grid snapping, icon-size changes, and unavailable Explorer.
- Rendering both variants and facings at all sizes, changing frames, transparent corners, and unclipped bodies.
- The companion's actual timer path: repeated trips, 180–300 second deadlines, varied speech, manual-interaction priority, disabled mode, independence from paused roaming, and long-gap cancellation.
- Deadline handoffs from all four animation categories, preservation through repeated control visits, mode enable/disable timing, opt-in immediate attempts, blocked-state explanations and 30-second recovery, read-only status, unrelated versus selected-icon mouse clicks, and fallback to the only usable icon.
- Independent deadlines in both queue orders, three-second handoffs, blocked-prank fallback, manual priority, mode toggles, bounded suspension recovery, and two simulated hours with feed, pet, play, and prank coverage and no overlapping animations.
- Native COM interop fixture with separate browser, shell-view, and folder-view vtables: the view rejects `QueryInterface(IOleWindow)` with `0x80004002`, while the corrected connection reads its window through `IShellView.GetWindow`. Tests exercise the actual CLR COM marshaler, distinguish failures at each interface step, and verify reference cleanup. No real desktop icons are accessed by this fixture.
- A companion at the opposite end of its monitor still selects the available icon and swims all the way to the pickup point before moving the icon. There is no proximity cutoff for icons on Mochi's current monitor.

A separate UI smoke check launched Settings, enabled the mode, saved, reopened Settings through the second-instance IPC path, disabled the mode, and confirmed both saved values without an application error. The [settings screenshot](previews/playful-mode-settings.png) shows the enabled checkbox.

The 1.1.4 UI check exercised **Save & try now**, verified an immediate native-failure explanation under Wine, reopened **Playful status**, and confirmed it displayed a 29-second retry countdown rather than a reset 3–5 minute wait, together with the independent regular-cycle countdown and the three-second queue explanation. Disabling the mode still persisted correctly. The Windows-specific failure shown by Wine was `Creating ShellWindows: COMException (0x80040154)`; this is an environment limitation, not evidence of the cause on a user's Windows PC.

Version 1.1.3 addressed the reported `Getting the active desktop view: InvalidCastException (0x80004002)` connection failure. `QueryActiveShellView` now returns the SDK's `IShellView` type, and the window handle is read through that interface's inherited `GetWindow` slot. The old separate `IOleWindow` cast is removed. Browser, active-view, folder-view, and window-handle checks now have distinct diagnostic labels; the old label covered several operations. The fixture reproduces a rejected legacy interface query. The user has confirmed that icon stealing now works on their Windows PC; the broader native matrix below remains pending.

The four GIFs were generated from the application's renderer with a sample folder icon and reviewed visually: [fin-grab left](previews/playful-fin-grab.gif), [fin-grab right](previews/playful-fin-grab-right.gif), [gentle bite left](previews/playful-gentle-bite-left.gif), and [gentle bite right](previews/playful-gentle-bite.gif).

The read-only native probe reports `Unavailable: 0 desktop icons. No positions changed.` under Wine, with the failing Shell connection step and HRESULT. This verifies graceful handling of an unsupported Shell; it does not validate native Explorer positioning, DPI conversion, or icon artwork sizes.

## Reproduce on Windows

Build to an existing staging directory first. These diagnostic commands run independently of a normal companion instance; wait for each process to finish before reading its result file.

```powershell
New-Item -ItemType Directory -Force .\staging | Out-Null
& '.\Project Files\source\build.ps1' -OutputPath "$PWD\staging\Mochi.exe"
Start-Process .\staging\Mochi.exe -WorkingDirectory "$PWD\staging" -ArgumentList '--self-test results.txt' -Wait
Get-Content .\staging\results.txt
Get-Content .\staging\playful-test-results.txt
Get-Content .\staging\update-test-results.txt
Start-Process .\staging\Mochi.exe -WorkingDirectory "$PWD\staging" -ArgumentList '--desktop-icons-status desktop-status.txt' -Wait
Get-Content .\staging\desktop-status.txt
```

The status probe reads availability and icon count only. It neither changes positions nor records filenames. `--playful-self-test results.txt` runs just the Playful Mode suite; `--playful-preview previews` writes sample animation PNG frames.

## Native Windows checks still pending

1. On Windows 10 and 11, prepare a few sample desktop files, folders, and shortcuts. Turn off **Auto arrange icons**, keep **Show desktop icons** on, then enable Playful Mode in Mochi's Settings. Confirm the pet and tray menus have no Playful Mode toggle.
   Use **Playful status** to check desktop access, then **Save & try now** for an immediate attempt. If blocked, retain `playful-status.txt` to identify the failed Windows operation. Check that merely viewing Settings preserves the pending countdown and that a blocked attempt recovers within 30 seconds after correcting the desktop setting.
2. Leave Mochi idle through several 3–5 minute waits. Confirm both grab/bite variants, readable speech, varied directions/routes/distances, an upright carried icon, and a free visible drop location. Confirm names, contents, and paths stay unchanged and other icons stay in place.
   With Playful Mode enabled, confirm automatic feeding, petting, and play still appear. When another turn is due during an animation, confirm it starts after a three-second pause; visiting controls or completing a prank must not reset its countdown.
3. Check with **Align icons to grid** both on and off, each desktop icon size, Mochi's three sizes, and 100%/150%/200% display scaling. Verify pickup alignment, no duplicate-looking cargo, label spacing, and unobstructed mouse input.
4. Test a second monitor to the left/above the primary monitor and monitors with different scale factors. Confirm each icon remains on its original monitor and inside the usable desktop.
5. During a carry, click or drag Mochi, open Settings, choose Come here, disable the mode, and quit. Verify unfinished moves restore when possible; completed drops and manually repositioned icons remain where the user left them.
6. During a trip, move/delete the selected sample icon, change icon size, enable Auto arrange, hide desktop icons, restart Explorer, or change display configuration. Confirm Mochi stops without moving a different icon or disrupting the desktop.
7. Pause ordinary swimming and confirm pranks continue. Disable Playful Mode and confirm no further pranks occur. Suspend/resume Windows and confirm there is no burst of overdue trips.

Publish a tested `Mochi.exe` and its generated `Project Files/update.xml` together. The root executable is version 1.1.4; the manifest records its exact size and SHA-256.
