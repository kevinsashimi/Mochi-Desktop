# Mochi Desktop 🦈

**A little whale shark. A little company. A happier desktop.**

Meet Mochi, a tiny animated companion for Windows who swims around your desktop, follows your pointer with his eyes, and perks up when you stop by to play. Give him a snack, a gentle pat, or a spot to swim to. He has plenty of wiggles to share.

Mochi stays **behind your app windows**, so he can keep you company while you work. He runs locally, with no account or API key needed. Playing with Mochi works offline; only update checks and downloads use the internet.

**[Download Mochi.exe](https://github.com/kevinsashimi/Mochi-Desktop/raw/refs/heads/main/Mochi.exe)** · [Get started](#1-bring-mochi-home) · [Learn the controls](#2-say-hello)

## A few of Mochi's moves

Snacks, happy wiggles, and a little showing off. These are some of the animations you can see in the app:

| Snack time 🍤 | Happy wiggles 💙 |
| :---: | :---: |
| <img src="Project%20Files/previews/GulpAndWiggle.gif" alt="Mochi eats a tiny snack, then gives a satisfied wiggle" width="280"> | <img src="Project%20Files/previews/HappyWiggle.gif" alt="Mochi wiggles happily as little hearts float around him" width="250"> |
| **Feed a tiny snack** | **Pet Mochi** |

| A barrel roll | Catching a bubble wave |
| :---: | :---: |
| <img src="Project%20Files/previews/BarrelRoll.gif" alt="Mochi rolls over to show his belly, then returns upright" width="250"> | <img src="Project%20Files/previews/BubbleSurf-right.gif" alt="Mochi bobs along on a little raft of bubbles" width="250"> |
| **Play together** | **Play together** |

There are **16 feeding, petting, and play routines**, each with left- and right-facing versions. Menu actions cycle through their routines, so try them more than once! You can [browse more animation previews here](Project%20Files/previews/).

## 1. Bring Mochi home

You need a **Windows PC with .NET Framework**. There is no installer to work through and nothing to compile.

1. **[Download Mochi.exe](https://github.com/kevinsashimi/Mochi-Desktop/raw/refs/heads/main/Mochi.exe).** This single file includes Mochi and his artwork.
2. **Give him a home.** Save the file in a folder you can write to and plan to keep, such as `Documents\Mochi`. This lets the app save your preferences and install updates.
3. **Double-click `Mochi.exe`.** Mochi will appear at a random safe spot on one of your monitors and say hello.
4. **Show your desktop and find him.** Minimize any windows covering him, then give him a click!

Want the source and all the previews too? On this repository, choose **Code → Download ZIP**, then **Extract All** in Windows. Open `Mochi.exe` from the extracted folder. The `Project Files` folder contains the optional source, artwork, previews, and documentation.

## 2. Say hello

Start with a click, then explore his menu:

| What you do | What happens |
| --- | --- |
| **Click Mochi** | He immediately plays a random snack, petting, or play animation. Surprise! |
| **Click and drag** | Pick him up and move him to another spot, or even another monitor. |
| **Move your pointer nearby** | He looks toward it, with gentle bobbing and swaying. |
| **Right-click Mochi** | Open his controls for feeding, petting, playing, swimming, and settings. |
| **Click or right-click his system tray icon** | Open the same controls, even when app windows cover him. Look near the clock; the icon may be inside the hidden-icons arrow. |

Try these three menu favorites:

- **Feed a tiny snack:** gulp-and-wiggle, snack chase, tummy pat, happy roll, and snack toss. Tiny shark, big appetite.
- **Pet Mochi:** cheek nuzzles, happy wiggles, cozy sways, high flippers, flipper hugs, and nose boops.
- **Play together:** barrel rolls, backflips, double hops, fin dances, and bubble surfing.

Mochi also entertains himself with **Idle Activities**. Every **1–3 minutes**, he takes a snack break, then later enjoys a pat, then plays. The cycle repeats with a fresh wait after each activity. If he is busy, his next activity waits its turn.

## 3. Send him on a little adventure

Choose **Swim now** for a spontaneous trip, or use **Come here** to pick the destination:

1. Right-click Mochi or open his tray menu.
2. Choose **Come here**.
3. When **“Where should I swim?”** appears, click a spot on any monitor.
4. Watch him swim over with a little message. “Tiny fins, big mission!”

Press **Escape** or **right-click** to cancel picking a spot. Switching to another app cancels it too. Mochi stays behind your app windows while swimming, so leave his destination visible if you want to watch him arrive.

Need him to stay nearby? Toggle **Pause swimming** in his menu. You can still feed, pet, and play with him, and **Come here** still works. His occasional idle activities continue too.

## 4. Make him feel at home

1. Open Mochi's menu and choose **Settings...**.
2. Pick **Small**, **Medium**, or **Large**.
3. Choose a swimming mood: **Calm** (45–90 seconds between swims), **Balanced** (25–55 seconds), or **Energetic** (12–25 seconds).
4. Turn **Swim around occasionally** on or off, and enable **Start with Windows** if you want him to greet you after signing in.
5. Feeling mischievous? You can also enable **Playful Mode** here. It starts off disabled.
6. Click **Save**.

Opening `Mochi.exe` again brings up Settings for your existing companion. It does not create a second Mochi.

## Playful Mode: tiny fins, a little mischief

Mochi has discovered interior decorating. Unfortunately, his decorating supplies are your desktop icons.

Turn on **Playful Mode (mischievous icons)** in **Settings...**, then click **Save**. This toggle lives only in Settings; the **Calm**, **Balanced**, and **Energetic** swimming-frequency options control how often Mochi takes an ordinary swim.

Every **3–5 minutes**, Mochi picks a desktop icon on his current monitor, swims over, and either scoops it up with his pectoral fins or gives it a gentle little bite. Then he carries it along a random straight, curved, or mixed route, drops it in a free spot, and celebrates with a giggle or a cheeky line:

> “Nothing to sea here!” · “Your icon booked a shark taxi.” · “Oops. My fins slipped. Twice.”

| A sneaky fin-grab | A gentle icon nibble |
| :---: | :---: |
| ![Mochi grabs a sample folder icon with his fins and carries it to a new spot](Project%20Files/previews/playful-fin-grab.gif) | ![Mochi carries a sample folder icon in his mouth, drops it off, and giggles](Project%20Files/previews/playful-gentle-bite.gif) |

The previews use a sample icon. In the app, Mochi rearranges the **screen positions** of your desktop icons, including shortcuts, files, and folders. Their names, contents, and locations on disk stay the same.

Before his first little heist, right-click an empty part of your **Windows desktop**, choose **View**, and turn off **Auto arrange icons**. Mochi leaves that Windows setting alone. Turning off **Align icons to grid** too makes the carrying animation smoother; with it enabled, Windows can snap the icon to its grid. Keep **Show desktop icons** enabled.

Playful Mode and the snack / pat / play cycle keep their own clocks, so you still get all of Mochi’s regular antics! If both are due, the one waiting longest goes first, with a **three-second breather** before the next queued animation. After each activity finishes, Idle Activities wait **1–3 minutes** and Playful Mode waits **3–5 minutes**. Each clock keeps its own countdown. He also waits for ongoing swims and your interactions to finish. Clicking or dragging Mochi, opening his controls, or turning the mode off interrupts a trip. Pausing ordinary swimming still allows Playful Mode. To stop future pranks, turn off **Playful Mode** and save; completed rearrangements stay where he left them.

**Waiting for the first heist?** Open Settings, enable Playful Mode, and choose **Save & try now** to request a prank straight away. If Mochi cannot start, he explains what needs attention. **Playful status** shows the countdown, the last result, and whether he can see icons on his current monitor. Checking Settings no longer restarts a pending countdown; if an attempt is blocked, he checks again in 30 seconds. The latest status is also saved as `playful-status.txt` beside your settings, without recording filenames.

## Keeping Mochi up to date

Mochi checks GitHub for updates in the background each time he starts. If a newer version is available, he asks before downloading and restarting. Choose **No** to keep playing with your current version.

You can also choose **Check for updates** from his menu, or **Info** to see your current version. Updates replace only the executable and preserve your preferences. Offline? Mochi keeps you company as usual.

If you have an older copy without **Check for updates**, quit it and replace it with the [current executable](https://github.com/kevinsashimi/Mochi-Desktop/raw/refs/heads/main/Mochi.exe) once to get the updater.

If updating from an older build fails, version **1.1.6** fixes a Windows compatibility error in the updater. Install it once by quitting Mochi and replacing only `Mochi.exe`, keeping your settings and `Project Files`. See the [update troubleshooting notes](Project%20Files/UPDATE-DIAGNOSTICS.md). Quit the existing Mochi before testing a copy from another folder; opening a second copy brings up the running instance.


## A few handy tips

- **Where did he go?** Mochi lives behind your windows. Show the desktop, or use his tray icon to reach the controls.
- **Where are my settings?** With the full project folder, they live in `Project Files/settings.xml`. With just the executable, they live in `settings.xml` beside it. The app creates the file when preferences are saved.
- **Moving his folder?** If you enabled **Start with Windows**, open Mochi from his new location, turn that option off and save, then turn it back on and save. This updates his startup shortcut.
- **Time for a nap?** Choose **Quit Mochi** from his menu. To stop him returning at sign-in, disable **Start with Windows** and save first.
- **Uninstalling?** Disable startup, quit Mochi, then delete his folder and any shortcuts you created.

## For the curious

The app is written in C# using Windows Forms. Explore the [source and artwork](Project%20Files/source/), [animation guide](Project%20Files/source/ANIMATION-GUIDE.md), [companion test report](Project%20Files/TEST-RESULTS.txt), [Playful Mode QA notes](Project%20Files/PLAYFUL-MODE-QA.md), and [update QA notes](Project%20Files/UPDATES-QA.txt).

<details>
<summary>Build Mochi and publish an update</summary>

On Windows, quit Mochi and run this from the repository root in PowerShell. It uses the C# compiler included with .NET Framework; no package restore is needed:

```powershell
& '.\Project Files\source\build.ps1'
```

The default build replaces the root `Mochi.exe` and generates `Project Files/update.xml` with its version, size, and SHA-256 checksum. For a staging build, pass both `-OutputPath '...\Mochi.exe'` and `-UpdateManifestPath '...\update.xml'`, using an existing output directory.

To publish an app update:

1. Increase the assembly, file, and informational versions in `Project Files/source/AssemblyInfo.cs`.
2. Build and test the executable, including the companion (`--self-test`) and updater (`--update-self-test`) checks. Each takes a result-file path as its next argument.
3. Commit and push the tested root `Mochi.exe`, matching `Project Files/update.xml`, and source changes together to `main`.

Never edit the checksum manually or reuse a version number for a new executable. Clients read the version record and executable from the same commit. GitHub Releases are not required, and documentation-only changes do not trigger an app update.

</details>
