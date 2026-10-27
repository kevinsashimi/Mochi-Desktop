# Ocean Cleanup QA (1.2.14)

## Behavior

Ocean Cleanup is enabled by default, including when loading older settings. With swimming enabled, one encounter becomes due 240–420 seconds after startup or the previous cleanup finishes/cancels. It joins the independent idle and Playful clocks, oldest first, after the normal three-second handoff. At most one cleanup is pending; resume does not produce a queue of litter.

Right-click Mochi or his tray icon and choose **Clean the ocean** for an immediate encounter, even with automatic cleanup or swimming disabled. **Settings → Ocean Cleanup** disables future automatic encounters. Opening Mochi’s menu pauses the scene; closing it resumes the same trip. Opening Settings, petting, feeding, dragging, Come here, Manual mode, display changes, and quitting interrupt the animation and clear the combined scene and restore the normal pet window. A timer gap longer than two seconds cancels a stale trip.

One randomly selected bottle, plastic bag, fishing net, or rubbish can drifts and bobs on Mochi's current monitor. Each type has three illustrated variants. Consecutive encounters avoid the same litter type, the last variant of that type, and the last Ocean Bin design. Floating litter uses an 84px surface, or 120px for an open net (44px when held), high-contrast outline art and a short arrival ripple. The story progresses through drift, sadness/displeasure/anger, courage, approach, pickup, carry, deposit, and a brief celebration. Each trip independently chooses a mouth grip or a pectoral-fin hug with equal probability, for every litter type. Nets spawn sprawled open in three color variants, gather during pickup, and remain compact during carry and deposit. Held litter stays fully visible and upright over the animated body at the same attachment points used for desktop-icon carrying, without lip or flipper masks. Both carry directions are supported. The complete trip has a finite timeout covering both travel routes plus the other story stages; longer routes are allowed to finish without speeding up.

The destination is always one of six illustrated Ocean Bins: pearl clam, coral basket, buoy barrel, salvage chest, nautilus pot, or submarine pod. A bounded randomized search chooses a safe position across the current working area, away from Mochi and the litter where space permits. The 112px bin appears during Courage and remains fixed through delivery. Each drawing has its own receiving-opening anchor. Cleanup never instantiates a desktop icon client or reads/moves Windows' Recycle Bin; Auto arrange, hidden icons, renamed icons, and Explorer availability do not affect it. Playful Mode retains its separate desktop icon behavior. Mochi, litter and bin share one transparent scene behind normal apps; only Mochi receives clicks and the window never activates from petting; native placement/presentation failures retry without an unhandled exception.

This is an animation. There is no trash file, recycle/delete operation, desktop icon move, or Shell launch. Real plastic bags and fishing nets require local disposal guidance; the dialogue does not claim they belong in ordinary household recycling.

## Automated checks

Build a staging executable with `-TestHooks -NoUpdateManifest`. Run `Mochi.exe --cleanup-self-test <result-path>` or the full `--self-test` suite. Tests inject a desktop factory that throws on any access, proving cleanup completes without touching Explorer or desktop icons.

- New/legacy settings defaults and persisted on/off preference.
- 108 full timelines: every litter type and emotion, all sizes, landscape/portrait/small working areas, including negative screen coordinates.
- All story stages, bounded motion, completion, both carry facings, transparent frame edges, readable speech.
- Dedicated reaction poses in both facings at all sizes: no sadness nod or eye enlargement, a gentle blink, and animated anger/displeasure between fin gestures.
- Real timer scheduling, older idle priority, paused/off suppression, explicit override, independent clocks, interruptions, suspension, stable randomized bin positions, all art variants without immediate repeats, and no desktop client access.
- Test-only `--cleanup-preview <directory>` writes the story contact sheet and litter artwork sheet.

## Native Windows checks before release

1. Try **Clean the ocean** repeatedly at Small/Medium/Large sizes. Verify all four litter types, both carry facings, the emotional beat (level sadness with normal-size eyes and a gentle blink; animated squint/fin gestures for anger and displeasure), pickup, visible cargo, deposit fade, and one brief closing message lasting three seconds.
2. Keep an ordinary app over part of the scene. Mochi, litter, and Ocean Bin stay behind it, never grab focus, and litter/bin never block mouse input. Check Quiet Cove both on and off.
3. Test all twelve litter designs and six Ocean Bins over light/dark wallpaper and Quiet Cove. Verify the arrival ripple, readable silhouettes, correct held artwork, and disappearance into each receiving opening.
4. Try multiple cleanups with Mochi in different screen corners. Bins should reach all available screen regions, remain fixed during a trip, and stay within the working area. Hide desktop icons and restart Explorer; cleanup should remain independent. Test another monitor and removing monitors mid-trip.
5. Open Mochi’s menu and dismiss it with Escape or an outside left-click: the same cleanup resumes without losing its props, even after leaving the menu open for over 90 seconds. Then interrupt with a drag, snack, pet, Come here, Manual mode, Settings, sleep/resume, and Quit. Confirm no orphaned litter/bin windows and normal idle/swimming behavior afterward.
6. Verify the default 4–7-minute encounters, Pause swimming, Ocean Cleanup off/on across restart, and immediate menu action while automatic cleanup is off.
7. At 100%, 150%, and 200% DPI, scroll Settings to the new toggle. Confirm readable hints, working Save/Cancel, keyboard navigation, and unclipped speech.

## Delivery and menu placement correction

Mochi now stops beside the Ocean Bin with space for his full body, then sends only
the carried litter along the deposit arc into the opening. At narrow screen edges,
the route chooses another clear side. The bin, Mochi, and loose litter have one
fixed back-to-front order, committed together with DeferWindowPos. Any hidden
WinForms taskbar owner moves with the group and remains hidden. Prop windows
no longer compete with the companion's one-second placement check.

Foreground/menu and desktop reorder events request a refresh on the next UI tick.
Requests coalesce, temporary Win32 failures retain the one-second retry delay,
and Manual mode keeps its own foreground behavior. Unsolicited activation changes
cannot reorder individual scene members. Event hooks are released on disposal;
periodic placement remains available if hooks cannot be installed.

Regression coverage includes 3,888 deliveries over all six bins, four litter types,
three sizes, nine bin locations, and three monitor layouts; long menu pauses;
real native grouped-window ordering; foreground changes; refresh coalescing and
failure backoff. The rendered ocean-delivery.png shows actual handoff coordinates.

On native Windows, repeatedly right-click the desktop, another application, and
Mochi, then left-click outside their menus. Check both idle and active cleanup,
Quiet Cove on/off, normal app occlusion, both carry directions, and all six bins.
Wine tests do not reproduce Explorer's Windows compositor and cannot certify the
absence of a Windows-only single-frame flicker.

## Grip, net, and desktop menu checks

The preview filename now includes its revision: Mochi-Desktop-1.2.14-preview-v12.zip,
with a matching enclosing folder. Application version 1.2.14 remains unreleased.

The placement anchor is the lowest visible non-topmost application. Temporary
native menus, tooltip windows, hidden utility windows and Explorer backdrop
windows cannot become anchors. A correctly placed scene does not move just
because those windows appear or disappear. Visible scene windows are no longer
sent a redundant show flag during placement. Foreground/menu and shell
show/hide/reorder events post a coalesced UI refresh and allow brief follow-up
checks for 400 ms as Explorer settles. Native errors retain the one-second retry
backoff; Manual mode retains its separate foreground placement.

The native regression creates temporary #32768 menu windows above and below
the scene and checks repeated show/hide cycles cause zero scene z-order changes.
Other tests cover delayed shell notifications, random grip selection for every
litter type, whole-sprite visibility for all twelve litter designs with both grips, both facings and all
sizes, three net gathering transitions, and 3,888 complete bin deliveries.

The user confirmed Windows 11. Quiet Cove on/off dependence has not yet been confirmed.
Native Windows still needs the reported sequence repeated: right-click an empty
desktop area, then left-click outside the Windows context menu. Test idle and
cleanup, with Quiet Cove on and off, plus menus opened over ordinary applications.
The cloud has Wine/Xvfb rather than Windows Explorer/DWM, so these tests cannot
prove the compositor-specific flicker has disappeared on the user's machine.

## Preview v6: whole litter images while carrying

Ocean Cleanup now uses the shared Playful Mode attachment points for both mouth
and flipper carries. The full upright prop is drawn over Mochi after his animated
body, without lip/face or flipper masks and without item-specific bite offsets.
Pickup still gathers an open net into its matching bundle; disposal keeps its
shrinking/fading arc. Random grip selection and the v5 desktop placement changes
remain in place. Desktop-icon stealing itself is unchanged.

The existing grip regression now checks all twelve designs with both grips,
both facings, all three sizes and four moving carry poses (576 rendered checks):
solid sprite pixels remain visible and the full cargo canvas stays inside Mochi's
window. Review random-carry-grips.png for the new appearance.

## Preview v7: continuous litter handoffs

Historical approach: superseded by the single litter surface in preview v8 below.

Pickup previously hid the loose litter window before uploading the pet frame
that contains it. A delayed or failed bitmap update could leave neither surface
showing the litter. The handoff now presents the pet frame successfully and checks
that its window is shown before hiding the loose sprite. Both decisions use the
same animation timestamp, so a sprite-size boundary cannot disagree between them.

During deposit, the carried image remains until the outgoing litter bitmap has
been uploaded and its window has actually been shown by grouped placement.
Temporary upload or placement failures preserve the previous image and keep the
existing bounded retry delays. No handoff changes the normal background ordering,
whole-sprite appearance, grip selection, net gathering, or delivery route.

Two additional regression groups exercise the actual native surfaces for every
litter type and both grips, including delayed drawing, a different wall-clock
sample, failed bitmap uploads, failed placement, retry throttling, recovery after
a menu pause, and disposal on cancellation. Existing rendering and delivery
coverage remains. On Windows 11, repeat Clean the ocean and watch the final
approach, pickup, and deposit at all three sizes; cloud Wine checks cannot certify
every frame produced by Windows DWM.

## Preview v8: normal cruising speed and one litter surface

Historical rendering design: superseded by the combined scene in preview v10.

Historical speed choice: superseded by the fixed pace in preview v9 below.

Both cleanup routes now use `SwimPath.Duration`, exactly as ordinary roaming
does: sampled curve length, a 48–62 px/s route-average target, the 2.8-second
minimum for short trips, and the same smooth acceleration/deceleration. Easing
briefly raises instantaneous speed above the average, just as in roaming.
There is no cleanup-specific speed multiplier or upper duration cap. The finite
story timeout includes both actual route durations plus 30 seconds for the other
stages and timing slack; pauses shift the timeout along with the animation.
Litter drift settles at the pickup point as Mochi arrives, so slow approaches do
not depend on an outdated prediction of the litter's position.

Litter is now drawn on the same 120px transparent native window throughout drift,
approach, pickup, carry and deposit. Only the artwork changes size, gathers or
fades inside that fixed canvas. The native window is never hidden, replaced, or
resized at pickup, and the pet bitmap never contains a second copy of the litter.
The layer remains above Mochi and below ordinary applications. Carried litter
uses the same grip anchors and bob as before; floating tilt eases to upright
during pickup. The window hides only when disposal is complete or cleanup ends.

Updated native-surface tests monitor hide and resize messages through pickup and
deposit for all four litter types and both grips, including injected bitmap and
placement failures with bounded retries. Pixel tests cover the approach/pickup
boundary and gathering for all twelve designs. Speed tests complete 96 stories
on small, portrait, 4K and 8K layouts, check normal average/peak speed and all three
route styles, and confirm that journeys longer than 90 seconds finish normally.
The existing 576 whole-sprite render checks and 3,888 deliveries remain.

Native Windows 11 validation still needs repeated pickup observations at all
sizes, light/dark wallpapers and Quiet Cove on/off. Wine cannot reproduce DWM.

## Preview v9: paint ownership and fixed cleanup speed

Historical rendering/speed choice: superseded by preview v10. Native Windows feedback still reported both bin and held litter blinking during overlaps.

A native-message probe against the v8 binary reproduced an additional clearing
path: the default `STATIC` window handler for `WM_PAINT` and `WM_PRINTCLIENT`
overwrote all 14,400 pixels of a 120px test surface with its ordinary control
background. Keeping the layered window visible and a fixed size did not prevent
that fallback painting. `CleanupWindow` now consumes background erase/print
requests and validates paint requests without invoking the default painter;
`UpdateLayeredWindow` remains the sole owner of the actual artwork. Scene updates
also draw back to front (bin, Mochi, litter), keeping litter last when surfaces
overlap during pickup or disposal. Placement order and click-through behavior
remain the same.

The new regression sends real native erase, paint and print-client messages and
checks that every test pixel survives. Repeated invalidation is validated without
replacing the last uploaded image. Existing native-surface tests additionally
assert pet-before-litter presentation order and retain failure/retry coverage.

Both routes now use **75 px/s** along the sampled curve, with no randomized speed,
ease-in/out multiplier, or upper duration cap. The existing roaming and guided
swimming behavior is unchanged. Tests measure movement throughout 96 trips and
cover straight, curved and mixed routes on small, portrait, 4K and 8K screens.
The route-aware finite timeout and menu pause behavior remain in place.

On Windows 11, watch the entire litter during pickup and disposal, especially
where it overlaps Mochi or the bin. Verify foreground applications still cover
the scene and that cleanup starts, cruises and approaches its destination at a
consistent pace. The cloud tests exercise Win32 under Wine; a full Windows DWM
visual check still needs the preview running on native Windows.


## Preview v10: one cleanup scene and faster swimming

The v9 paint guard was insufficient: the bin, Mochi and the litter still had
independent layered windows and independent position/bitmap updates. Preview v10
composes all three into Mochi's existing window (bin, pet, then litter) and
publishes position, size and pixels with one UpdateLayeredWindow call. There are
no separate native prop windows, pickup/disposal transfers, or pre-render moves
of the old scene. The native surface stays fixed at the current working area
for the whole trip; moving/resizing a tight bounding rectangle caused clipping in
a live Wine check. Two scene buffers are reused instead of allocating a new
screen-sized bitmap for every frame. Native uploads still copy the working area,
so high-resolution performance remains a native-Windows release check.

A failed upload retains the complete previous frame, its position and its pet
hit mask. Retries remain bounded. Only pixels belonging to Mochi or his speech
bubble receive mouse input; the bin, litter and empty space remain click-through.
The normal 15ms timer switches the layered click-through style according to
whether the pointer is over Mochi. This is needed because HTTRANSPARENT alone
only searches the same UI thread. A real mouse-input probe verified bin/litter
clicks reach a different thread while Mochi receives his own clicks. There can
be up to one timer interval before a hover changes input ownership.
The same background placement rule keeps the scene below normal applications.
Cancelling or completing cleanup restores the ordinary pet bounds and releases
the hit mask, preserving subsequent dragging, Settings and manual interactions.

Both travel legs now run at a fixed **100 px/s**, one-third faster than preview v9.
Reaction, pickup and deposit timing is unchanged, as are normal roaming speeds.

Regressions cover 432 deliberately overlapping scene frames (12 litter designs,
6 bins, both grips and both facings), actual native one-upload delivery sequences,
visible bin/cargo pixels, default-paint ownership, bounded failure recovery and
pet-only hit testing. Fixed-speed tests still span small, portrait, negative,
4K and 8K working areas. Cloud verification uses Wine/Xvfb; native Windows 11
DWM overlap and Explorer-menu behavior still need visual confirmation.

The user-provided MP4 could not be opened by the attachment transfer tool because
it exceeded that tool's 32 MiB limit. This correction was investigated from the
reported overlap behavior and the existing native rendering path, not from a
claimed viewing of that recording.

Supplementary release-binary captures compared the scene bitmap with real screen
pixels over a controlled native backdrop during carry and deposit. All four
captures retained over 99% of the bin's strongly visible pixels. The xfwm4 cloud
desktop run still omitted the bin in a screenshot despite successful lifecycle
checks; the controlled native capture did not. Treat Windows 11 compositor
confirmation as outstanding, not as established by these cloud screenshots.

## Preview v11: faster cleanup and mouth pickup chomp

The user confirmed that preview v10 resolved the blinking on their Windows 11
desktop. This preview preserves that combined scene and native placement path.
Both travel legs now run at a fixed **140 px/s**, 40% faster than v10. Reaction,
pickup, deposit and normal roaming timings stay the same.

Mouth pickups now use the desktop-icon stealing animation: the feeding atlas's
open-mouth pose for the first 0.65 seconds of pickup, then the closed gentle-grip
pose for the rest of pickup and carrying. This applies to every litter type and
both facing directions. Flipper pickups keep their existing gathering animation.
The whole litter sprite remains visible throughout, and open nets still gather
into their matching compact bundle. No occlusion masks or extra windows are added.

The existing speed regressions check 140 px/s across 96 complete journeys,
including curved paths, small/portrait layouts and long 4K/8K routes. The scene,
pickup-continuity, whole-artwork and native-failure checks remain applicable.
For visual review, start Clean the ocean until a mouth grip is chosen: Mochi
should open his mouth, close it as he gathers the litter, and carry the complete
item at the faster pace. Check both approach and return journeys, and verify
the bin and held litter remain visible through overlaps.

Validation: all 63 regression groups and five live release-mode UI checks passed
under Wine/Xvfb. A separate render probe loaded the release executable and
captured 32 pickup frames covering the four litter types in both directions;
the open-mouth and closed-grip poses were visually inspected with the whole
litter drawn over them. The preview includes the resulting mouth-pickup sheet.

## Preview v12: smooth cleanup acceleration and deceleration

Both cleanup journeys now use the same distance easing as normal swimming and
icon stealing. Mochi accelerates gently toward the litter, slows for pickup,
then accelerates toward the bin and slows for disposal. Position and approach
heading share the eased distance so his facing follows the correct part of the
curve.

The average pace remains **140 px/s**, preserving v11's journey durations. This
is now an average rather than a constant instantaneous speed: the shared curve
peaks at approximately 263 px/s halfway through each journey and approaches zero
at each endpoint. Long routes still receive their full duration; no duration cap
forces a faster trip. Menu pauses shift the timeline without restarting easing.

The speed regression exercises 96 complete stories on small, portrait, negative,
4K and 8K layouts. It samples actual positions on each leg, checks the same motion
as normal swimming, verifies gentler first/last intervals than the middle,
checks exact pickup/disposal arrival, and pauses/resumes midway through both legs.
The existing overlap, whole-artwork and failure-recovery checks remain in place.

Visual check: start Clean the ocean and watch the departure and arrival for both
litter pickup and bin delivery. Each should ease smoothly at both ends while
retaining the gentle mouth chomp and complete visible litter. The combined scene
rendering that resolved blinking in v10 is unchanged.

Validation: all 63 regression groups and five live release-mode UI checks passed
under Wine/Xvfb. The live checks include a complete cleanup, menu pause/resume,
Settings cancellation and restoration of ordinary pet bounds. The release
executable passed the native manifest/icon/version resource checks, and its
pickup frames retain the open-and-close mouth animation. Native Windows remains
the place to judge the final feel of the movement.
