# Swimming continuity

A frame-delay reproduction against the released 1.2.15 executable found that
normal swimming advanced directly to its wall-clock route position after a
missed timer interval. On one fixed route, 0.5 seconds skipped 43.27 pixels,
1.5 seconds skipped 89.63 pixels, and 10 seconds jumped 109 pixels to the end.
The corrected production preview, exercised through the actual window timer
and checked against native window coordinates, moved 3.61 pixels after each of
those delays and continued swimming.
This reproduces a possible cause of the reported teleport during normal swims;
it does not establish which event delayed frames on the user's Windows PC.

The 1.2.16 preview limits each rendered timer step to 50 milliseconds of swim
progress. Missed time extends the current route's deadline instead of being
replayed on later frames. The destination and easing remain unchanged. Normal
20/30/60/120 fps keep the previous speed and arrival time. Come here uses the
same protection; Ocean Cleanup's movement is unchanged.

The full test suite includes `--swim-continuity-self-test`, available only in
MOCHI_TESTS builds. It covers all three sizes, landscape/portrait screens and
negative monitor coordinates; delays from 120ms through one hour; repeated
ordinary frames after a delay; modal pauses; exact arrivals; and a fresh clock
when the next swim begins. Existing controller tests can still fast-forward
animation state through `Advance`; the new regression exercises `AdvanceFrame`,
which is the timing path used by the real timer.

For Windows 11 preview testing, allow several normal swims, including while
other applications are busy. If a jump remains, record the time, approximate
distance, screen arrangement, and whether Mochi changed screens or a window
opened/closed. Test the preview in its own folder or quit Mochi before replacing
the executable; keep settings files when upgrading an existing folder.

## Preview v2: screen-origin relocation

The reported jump specifically to the top-left corner still occurred with v1.
A second reproduction changes the native window's size during a normal swim.
In v1, a 250 x 280 window was resized to the screen's 1280 x 1024 bounds. The
next swim tick used that native size in Motion.Clamp and moved Mochi from
approximately (648, 298) to (0, 0). A separate native move also visibly displaced
him to (0, 0) before the next frame. These fixtures do not depend on cleanup
being enabled. They inject the window change; the precise Windows event on the
user's PC that originally caused it has not been identified.

V2 owns the character's screen position and footprint separately from the
Windows form. Swimming, feeding, dragging, Manual mode, icon stealing, screen
selection and saved position all use that character geometry. The form can
cover a whole screen during cleanup without changing Mochi's own coordinates.
Only frame presentation may move or resize a visible companion window.
UpdateLayeredWindow's native bounds are refreshed into WinForms' cache after a
successful draw, and the next frame publishes position, size and pixels together.

Cleanup cancellation no longer separately moves/resizes the old scene bitmap.
If the replacement frame fails, its old pixels and matching hit mask stay intact
until a retry succeeds. Tests cover the failed transition into a normal swim,
including monitors with negative coordinates. Native placement tests still
verify background order, focus and non-topmost behavior.
