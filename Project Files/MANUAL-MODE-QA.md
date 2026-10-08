# Manual mode

Manual mode is a temporary action in the pet/tray right-click menu. It is not a
saved preference. A focused instruction overlay receives WASD and arrow-key
messages, and Mochi becomes visible above open windows for this session. Normal
background placement returns on exit. No global keyboard hook is installed.

## Automated checks

Build with `source/build.ps1`, then run `Mochi.exe --manual-self-test <result-file>`.
The full `--self-test` also includes these checks and writes a separate
`manual-test-results.txt` beside its other reports.

- Both key sets, mixed diagonals, opposite directions, key repeat and independently held aliases.
- Equal speed across 30/60/120 Hz, smooth release, capped delayed frames and working-area bounds at every Mochi size, including portrait and negative-coordinate screens.
- Actual companion activity scheduling: manual steering preempts reactions and destination selection, preserves pending idle/prank deadlines, and hands back control after three seconds. Roaming can stay paused throughout.
- Queued update prompts, remembered facing, safe takeover from a prank, explicit interactions, and cleanup on disposal.

## Windows desktop checks

1. Right-click Mochi, then choose **Manual mode**. Repeat through his tray menu.
   Check that the instruction card and Mochi are visible above open windows.
2. Hold each arrow/WASD key; try W+D and Up+A. Release one key, then both. Movement
   should ease into swimming and gently stop without a diagonal speed boost.
3. Hold opposing directions, or W+Up then release W. Check cancellation and that
   a remaining alias continues to work.
4. Swim to each edge. Mochi's whole window stays inside the current monitor's
   usable desktop. Drag him to another monitor and start again there.
5. Exit with Escape and with a full right-click, including over Mochi. The exit
   click should not select a file or open a desktop menu. A leftover mouse-up
   from choosing the menu item should do nothing.
6. Hold a key and Alt+Tab to another app. Steering should stop, the overlay should
   close, and ordinary typing should work. Re-enter without touching a key: Mochi
   should stay still. Repeat after changing display arrangement and when quitting.
7. Try with **Pause swimming** selected and with Playful Mode both on and off.
   Remain in Manual mode until activities are due. Nothing interrupts steering;
   due activities resume in their existing order after the handoff pause.
8. Check the overlay on Windows 10/11 with your display scaling and taskbar setup.
   Cloud UI checks use Wine and do not replace testing a real Windows desktop.
