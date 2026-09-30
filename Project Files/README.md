# Mochi Desktop

A little whale shark companion for Windows. It stays on the desktop behind your apps, responds to you, and takes occasional short swims around the current monitor.

The project folder contains `Mochi.exe` and a `Project Files` folder holding the source, artwork, previews, documentation, QA reports, and settings. Run `Mochi.exe` from the project root. Users can also download and run just the executable; the supporting files are optional. Unless stated otherwise, paths in this guide are relative to `Project Files`, where this README lives.

All animations draw one crisp illustrated pose at a time, with a faster redraw loop and gentle continuous body motion. Hops, wiggles, feeding, cursor-following, and the illustrated rolls and flips retain their full pose sequences. Swimming randomly chooses straight, curved, or mixed paths, with gradual acceleration and deceleration, eased steering, and a gentle body/tail sway. Mixed paths combine a straight section and a curved section in either order, with continuous steering and speed at the join. Pose overlays and segmented sprite drawing have been removed to prevent after-images and vertical seams.

Mochi stays behind other app windows, including during greetings, swimming, and reactions. Move or minimize a covering window to see and interact with him; his animations continue while covered. His system-tray controls remain available. Settings opens as an ordinary window when requested. The companion preserves keyboard focus and regularly restores its place above the desktop background when Explorer changes its windows.

## Play

On each fresh launch, including Windows startup, Mochi appears at a random safe position on one of your monitors, randomly facing left or right in his usual idle pose. He randomly chooses one of three short introductions and keeps that idle pose through his greeting and controls hint. After four seconds, he shows **“Click to interact with me! Right-click for more.”** for another four seconds. Interacting with him takes priority and cancels the queued hint.

- **Click:** immediately play a random animation from all 32 directional variants across 16 feeding, petting, and play routines. A reaction starts on mouse release; there is no double-click action or waiting period. The same variant will not repeat immediately.
- **Drag:** move Mochi, including to another monitor. He randomly chooses from six pickup sayings and six landing sayings, without repeating the same saying consecutively. Dragging and dropping does not trigger a click reaction.
- **Move your pointer nearby:** Mochi looks toward it in all 16 directions, with gentle bobbing, breathing, and swaying. Turns ease between poses.
- **Right-click Mochi:** pet, feed, play, swim now, Come here, pause swimming, settings, Check for updates, Info, or quit.
- **System tray:** click or right-click for the same controls. Windows may put the tray icon in its hidden-icons menu.

## Come here

Choose **Come here** from Mochi's right-click menu or tray menu. Mochi asks you to pick a spot, and a temporary screen overlay shows **“Where should I swim?”** Click anywhere on a monitor to choose his destination. The overlay closes and he swims over, with his usual animated movement and an arrival message. **Escape** or **right-click** cancels the selection; switching to another app also cancels it.

Each trip randomly picks from eight swimming sayings and eight arrival sayings, including “Tiny fins, big mission!”, “Wiggle engines: full speed!”, and “Special delivery: one happy shark!” Each group remembers its previous choice while Mochi is running, so consecutive trips do not repeat the same departure or arrival line. The original two sayings remain in the selection. Previews: `previews/swim-over-sayings.png` and `previews/arrival-sayings.png`.

You can choose another monitor, and positions near an edge are adjusted to keep Mochi fully visible above the taskbar. The destination click is consumed by the picker, so it does not activate the app beneath it or trigger a random reaction. Mochi waits while you choose. An explicitly requested trip works even when automatic swimming is paused, and it preserves that setting.

Only the temporary destination picker appears above your apps. Mochi himself continues to swim behind them, so move or minimize a covering window to see him arrive.

## Idle activities

After 3–5 quiet minutes, Mochi plays a random animation from **Feed a tiny snack**. After it finishes, he waits another random 3–5 minutes and chooses one from **Pet Mochi**, then waits again and chooses one from **Play together**. The feeding → petting → play cycle repeats while Mochi is open, with the usual speech bubbles. All five feeding, six petting, and five play routines are included, with randomly selected facing directions.

“Quiet” means you are not interacting with Mochi; you can keep working in other apps or step away. Clicking, dragging, requesting a swim, or opening his controls restarts the wait without changing the next automatic category. After a feeding, petting, or play animation finishes, a fresh 3–5 minute wait begins. Ordinary swimming and looking toward your pointer do not reset this timer. If an automatic activity becomes due during a swim, Mochi finishes the swim first.

Pausing swimming still allows idle activities. Automatic activities leave the manual menu cycles and click surprises independent. Each launch starts the automatic cycle with feeding; a long gap in timer updates starts just the next activity, with no burst of missed activities.

## Petting and play

Choose **Pet Mochi** in the menu to rotate through:

1. **Cheek nuzzle:** Mochi closes his eyes and leans happily into the pat.
2. **Happy wiggle:** tail swishes, flippers flutter, and his body wiggles with delight.
3. **Cozy sway:** a slow, sleepy float with a contented smile.
4. **High flipper:** a lifted flipper and a cheerful little bob.
5. **Flipper hug:** Mochi folds both flippers across his belly, closes his eyes, and opens up again.
6. **Nose boop:** Mochi leans forward into a pink boop, gives a contented blink, then settles back.

Choose **Play together** to rotate through:

1. **Barrel roll:** a fully illustrated roll showing Mochi's cream belly and spotted back as he turns, with his nose staying toward the selected side.
2. **Backflip:** Mochi lifts, tucks, passes through vertical and upside-down illustrated poses, and settles upright.
3. **Double hop:** two springy little bounces.
4. **Fin dance:** rhythmic flipper waves and side-to-side rocking.
5. **Bubble surf:** Mochi bobs and banks over a little raft of shimmering bubbles.

Petting adds a few floating hearts; play ends with tiny sparkles. Each reaction has its own speech bubbles. The right-click menu's petting, play, and feeding actions have separate cycles, and each remembers its next reaction after quitting. Single-clicking picks randomly across all 32 directional variants without advancing those menu cycles. A new interaction replaces the current reaction; dragging, opening controls, or requesting a swim can also interrupt it. Automatic roaming resumes after the reaction and its usual rest interval.

The nuzzle, flipper hug, barrel roll, and backflip use 32 new illustrated poses. Every routine has left- and right-facing versions. Three additional routines reuse the crisp artwork with new motion and effects: nose boop, bubble surf, and snack toss. Speech stays readable in both directions. Preview each reaction under `previews/`, including `illustrated-reactions.gif`, `petting-reactions.gif`, and `play-reactions.gif`. The new art and the prompts used to create it are included under `source/`.

## Feeding reactions

Choose **Feed a tiny snack** from the right-click menu. Each menu feed plays the next reaction, in this order:

1. **Gulp and wiggle:** a shrimp drifts into Mochi's open mouth, followed by puffed cheeks, a satisfied blink, a tail swish, and flipper flutter.
2. **Snack chase:** Mochi swims a short distance after the treat, catches it, then gives a happy wiggle.
3. **Tummy pat:** after a gulp, Mochi gives its belly two little flipper pats.
4. **Happy roll:** after eating, Mochi rolls playfully to the side and returns upright.
5. **Snack toss:** Mochi catches a spinning snack that arcs down into his mouth.

The cycle repeats and remembers its place after quitting. Short speech bubbles accompany the snack, gulp, and each reaction. Petting or dragging can interrupt a reaction; the next feed still advances to the next one. Pausing automatic swimming does not disable the snack chase you explicitly request by feeding.

The `previews` folder includes the original feeding previews and additional mirrored routines. New feeding artwork and its prompt notes are included under `source/`.

## Remembering his direction

After a swim, Mochi idles facing the horizontal direction of the trip. Dragging left or right sets his facing direction too, including reversing a drag; a purely vertical drag preserves his last side. Every feeding, petting, and play variant leaves him facing the side it used. Cursor-following is temporary: when your pointer leaves, he returns to his remembered direction. Startup uses the normal idle animation, randomly facing left or right.

## Settings

Right-click Mochi and choose **Settings...**. Change size, swimming frequency, or pause swimming. Turn **Start with Windows** off to use manual startup, or turn it on to start after signing in. Click **Save**.

Calm swims happen after 45–90 seconds; Balanced after 25–55 seconds; Playful after 12–25 seconds. Mochi keeps taking scheduled swims while your pointer is still or you are away. Swimming past the pointer does not interrupt a trip or prevent the next one. Clicking, dragging, feeding, opening controls, or choosing Pause swimming still takes priority. It stays inside the current monitor's work area and avoids the taskbar.

Swimming chooses random headings all around the compass, including vertical and diagonal trips, and Mochi faces the direction of travel. Straight, curved, and mixed paths stay within the screen throughout the trip. Curves have a visible bend; mixed paths can glide straight and then turn, or turn and then glide straight. Joined sections share a heading, and distance-based progress keeps speed continuous. After roughly 25 seconds lingering near an edge, Mochi can leave before his usual swim interval and chooses a random destination toward the interior. Briefly moving away from an edge reduces the recent border history. He finishes an ongoing swim or interaction before departing; pausing swimming also pauses this behavior. Your 3–5 minute feeding → petting → play cycle continues independently.

Run **Mochi.exe** to launch manually. Launching it again opens Settings for the existing companion. Use **Quit Mochi** to close it for the current session; disable **Start with Windows** if you also want it to stay closed after your next sign-in.

## Local and portable

The executable includes the artwork and runs offline without an account or API key. Only update checks and downloads connect to GitHub. It uses Windows' built-in .NET Framework. When a `Project Files` folder exists beside the executable, preferences are stored in `Project Files/settings.xml` to keep the project root tidy. When using the executable alone, preferences are stored in `settings.xml` beside it. Missing settings use defaults and are created when preferences are saved. Keep the chosen folder writable to retain preferences. The existing Codex pet stays separate.

The Startup shortcut points to this folder, so keep it in place. If you move the folder, open the moved executable and toggle **Start with Windows** off then on to update the shortcut. To remove the companion, disable startup, quit Mochi, and remove its folder and desktop shortcut.

## Updates and app information

Mochi checks the public [GitHub repository](https://github.com/kevinsashimi/Mochi-Desktop) in the background on every fresh launch, including Windows startup. When a newer version is available, a popup asks whether to download and install it. Choose **No** to keep using the current version, or **Yes** to download the update and restart Mochi. The download can be cancelled. Startup checks stay quiet when there is no update or GitHub cannot be reached.

Choose **Check for updates** from Mochi's right-click menu or tray menu to check at any time. Manual checks also report when you are up to date or the check could not finish. Choose **Info** to see the version of the executable you are currently running.

Updates download only `Mochi.exe`. The updater reads a small version record from GitHub, verifies the executable's size, SHA-256 hash, and embedded version, then replaces the executable in its existing location. It preserves settings, startup shortcuts, and supporting files. If replacement fails, the existing executable remains; if launching the replacement fails, the updater restores the previous executable. The app folder must be writable. No repository clone, account, or separate updater installation is needed.

## Publishing an update

The initial version with automatic updates is **1.0.0**. Earlier executables need to be replaced manually once to gain the updater. Future versions are announced by the repository's `main` branch; GitHub Releases are not required.

1. Increase the assembly, file, and informational versions in `source/AssemblyInfo.cs`.
2. Build and test the executable. The default `source/build.ps1` command rebuilds the root `Mochi.exe` and generates `update.xml` in `Project Files` with its version, size, and checksum. For a staging build, pass both `-OutputPath '...\Mochi.exe'` and `-UpdateManifestPath '...\update.xml'`; promote both tested files together.
3. Commit and push the root `Mochi.exe` and `Project Files/update.xml` together to `main`, along with the matching source changes. Never edit the checksum manually or reuse a version number for a new release.

Clients read the version record and executable from the same Git commit so a concurrent publication cannot mix files from different builds. Documentation-only changes do not trigger an update. Until the first version record is published, automatic checks remain quiet and manual checks say that update information is not available yet. The version record is read remotely; users still only need to download the executable.

## Source and checks

Source, artwork, icon, manifest, and a rebuild script are in `source/` inside `Project Files`. From the project root, run `& '.\Project Files\source\build.ps1'` in PowerShell after quitting Mochi to rebuild the root `Mochi.exe` with the C# compiler included in Windows. Use `-OutputPath` to build to a staging location for validation. No package restore is needed.

Automated checks cover all 16 direction mappings, 1,000 screen-boundary cases including negative monitor coordinates, movement endpoints, animation frame bounds, 73 original plus 24 feeding sprite cells, transparent window corners, and render geometry. Feeding checks cover the five-reaction cycle, persistence, old settings compatibility, timing, snack disappearance, chase direction at screen edges, and timed speech bubbles at all three sizes.

Gaze checks verify changing rendered pixels in all 16 directions at all three sizes, unclipped motion, smooth turns through the up direction, and reversals. Tests also exercise the companion's actual timer logic with repeated trips and a stationary or distant pointer, while preserving pause, drag, settings, feeding, and held-click priority. See `TEST-RESULTS.txt`. The UI automation tool cannot target the nonactivating borderless pet itself, so complete automated click-and-drag UI testing is not available.

Petting and play checks cover all eleven petting and play timelines at all three sizes, all 32 illustrated pose cells and their ordered playback, changing body pixels, clear window edges, timed speech, independent saved cycles, old preference files, interruption, and returning to idle through the actual timer logic. Illustrated motion semantics were also reviewed visually; the barrel roll has slightly uneven angular spacing but reads as a complete continuing roll.

Single-click checks exercise 500 seeded requests through the actual handler and timer path: all 32 directional variants are reachable, none repeats back-to-back, each mouse release responds immediately, drags stay separate from clicks, and menu cycle positions are preserved.

Idle activity checks advance the actual timer through 100 full feeding → petting → play cycles. They cover all 16 routines, randomized 180–300 second waits, exact deadlines, waiting for animation and swim completion, stationary-pointer and gaze behavior, manual interaction priority, preserved menu counters, drag and controls priority, and resuming after a long gap without a burst of activities.

Smooth motion checks cover single-pose rendering, uninterrupted opaque body pixels during swimming, swimming artwork in all 16 directions at all three sizes, 2,000 straight/curved/mixed routes on landscape/portrait/small/negative-coordinate screens, all 16 heading sectors, 320 inward border escapes, smooth launch/landing, and border timing and interaction priorities through the actual companion timer. See `SMOOTH-SWIMMING-QA.txt`, `RENDERING-ARTIFACTS-QA.txt`, and `previews/swimming-all-directions.gif`. The redraw loop targets roughly 60 updates per second; actual cadence depends on Windows scheduling and load.

Directional checks cover all 32 variants, readable unmirrored speech, left- and right-facing render bounds at every size, 3,000 random placements across mixed monitor layouts, both idle greeting directions, both swim directions, drag reversals and releases, and preserving facing after cursor gaze. See `FACING-QA.txt` and `previews/facing-comparison.png`.

New animation additions automatically enter the random interaction list in both directions. Both-direction render and facing checks apply to every enumerated routine, including future additions. The project instructions in `source/ANIMATION-GUIDE.md` record this requirement. Updated previews: `previews/swim-styles.gif`, `previews/all-reactions-both-sides.png`, and `previews/new-hint-and-sayings.png`.

Come here checks cover selection and cancellation, consuming the opening and destination clicks correctly, pausing other activities while choosing, animated travel with automatic roaming paused, arrival facing, 900 safe placements/routes, and cross-monitor travel through the real timer logic. Rendered prompt previews are in `previews/destination-picker.png`, `previews/come-here-speech.png`, and `previews/come-here-sizes.png`. See `COME-HERE-QA.txt` for validation details.
