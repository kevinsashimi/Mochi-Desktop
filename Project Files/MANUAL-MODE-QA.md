# Manual mode

Manual mode is a temporary action in the pet/tray right-click menu. It is not a
saved preference. A focused instruction overlay receives WASD and arrow-key
messages, and Mochi becomes visible above open windows for this session. Normal
background placement returns on exit. No global keyboard hook is installed.

Movement uses the current monitor's full Bounds, including the taskbar strip.
Only the rendered body limits travel: the transparent window/speech padding can
extend beyond the screen. Body alpha bounds update with each pose and are measured
using one reusable pixel buffer. The cooldown badge shifts inside the visible
area at edges; speech is omitted when its usual area would be cut off. Leaving
Manual mode restores the normal whole-window working-area placement.

The controls card lists WASD / Arrows, Spacebar with the five-second cooldown,
and Esc/right-click on separate lines. It stays visible until the first keyboard
control, holds for three seconds, then fades over 1.5 seconds. Mouse movement
restores it over 0.18 seconds and restarts the hold/fade. Held keys and spurious
mouse messages from windows moving under a parked pointer do not reset fading.
A nearly invisible full-screen input shield retains keyboard focus and consumes
right-clicks even while the controls card is hidden. The card shares that
layered surface, below Mochi, so native popup ordering cannot hide his body.
The cached card area is blended at up to 30 Hz only during fades; the input
shield stays at a constant, nearly invisible alpha. Both image buffers are
released on exit. The card moves to the opposite edge if Mochi approaches it,
keeping his swimming path visible even while the instructions are on screen.

Tap Space for a 0.34-second burst at up to three times normal swimming speed.
The five-second cooldown starts when a boost is accepted and survives re-entering
Manual mode. A press toward a blocked edge does not waste a charge. Boosting
without a direction uses the last swimming heading, initially Mochi's facing.
Held Space never queues a future boost; release it and press again after recharging.

A native, nonactivating surface draws a bounded bubble wake behind Mochi. Bubbles
rise, wobble and fade within 1.35 seconds, with occasional heart shapes. The wake
never handles clicks or keyboard input and is disposed on leaving Manual mode.
A small meter beneath Mochi shows readiness and the recharge countdown. Speech
has a 45% chance per accepted boost, without consecutive repeated spoken lines.

Each accepted boost selects one of three newly illustrated animations: Rocket Grin,
Wheee, or Wink & Giggle. Twelve original frames form three four-pose sequences.
The same expression cannot repeat immediately, including across mode re-entry.
The animation lasts 0.76 seconds (launch, kick, happy settle), while movement still
boosts for only 0.34 seconds. This visual timeline never blocks input, recharges
early, or restarts an activity timer. Rejected presses do not restart it. One full
sprite is drawn per frame, mirrored for the current side and pitched toward the
smoothed heading; no crossfade, zero-width turn, or overlapping sprite layers.
See `source/boost-art.md` for artwork provenance and the reproducible packing step.

## Automated checks

Build with `source/build.ps1`, then run `Mochi.exe --manual-self-test <result-file>`.
The full `--self-test` also includes these checks and writes a separate
`manual-test-results.txt` beside its other reports.

- Both key sets, mixed diagonals, opposite directions, key repeat and independently held aliases.
- Equal speed across 30/60/120 Hz, smooth release, capped delayed frames and working-area bounds at every Mochi size, including portrait and negative-coordinate screens.
- Actual companion activity scheduling: manual steering preempts reactions and destination selection, preserves pending idle/prank deadlines, and hands back control after three seconds. Roaming can stay paused throughout.
- Queued update prompts, remembered facing, safe takeover from a prank, explicit interactions, and cleanup on disposal.
- Short boost distance and identical travel across timer rates, eight directions, resting dashes, wall checks, delay protection, exact cooldown boundary and cooldown retention across sessions.
- Space press/release edges, held-key behavior, silent/spoken variety, bounded transparent wake, expiry, clipping and all boost poses and sizes.
- All twelve illustrated frames, all three styles without immediate repeats, timeline/settle boundaries, mirror geometry, full silhouettes in all 16 headings and three sizes, and actual companion rendering/cleanup.
- Visible-body bounds on landscape, portrait and negative-coordinate displays; actual normal/boosted movement to all edges and corners at every size, onscreen feedback, and safe exit.
- Stacked card text at narrow/desktop/4K widths, first-use hold, smooth fade, stationary/held-key behavior, mouse recall and repeat fade.

## Windows desktop checks

1. Right-click Mochi, then choose **Manual mode**. Repeat through his tray menu.
   Check that the instruction card and Mochi are visible above open windows.
2. Hold each arrow/WASD key; try W+D and Up+A. Release one key, then both. Movement
   should ease into swimming and gently stop without a diagonal speed boost.
3. Hold opposing directions, or W+Up then release W. Check cancellation and that
   a remaining alias continues to work.
4. Swim to each edge and corner. Mochi's visible body reaches the current monitor's
   boundaries, including the very top and the taskbar strip. Empty speech/window
   padding must not block him. Check the recharge meter near each edge. Drag him
   to another monitor and start again there.
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
9. Tap Space while steering and while resting. The short dash should leave bubbles,
   show a rocket grin, closed-eye laugh or wink/giggle, and sometimes include a line. Try an early second press,
   hold Space past five seconds, then release and tap after recharge. Re-entering
   Manual mode should retain an unfinished recharge.
10. Escape, right-click over the wake, or switch apps during the dash. All effects
    should disappear, focus should stay in the app you selected, and Space should
    type normally outside Manual mode.
11. Boost repeatedly left, right, diagonally, up and down at Small/Medium/Large.
    All three expressions should appear, without an immediate repeat. Watch the
    fins kick, then relax after the dash. The entire body stays visible and clear
    of the speech bubble and recharge meter. Steering during the happy settle
    should keep working. Check with speech and with a silent boost.
12. Read the three stacked controls, then begin steering without moving the mouse.
    The card should hold for three seconds, then fade out gently. Keep steering
    and boosting while it is hidden. Move the mouse: the card returns without
    taking focus or stopping Mochi, then fades again when the pointer is still.
    Confirm Escape and right-click still exit while the card is hidden, without
    opening a desktop menu. Switching apps or quitting also removes the card.
