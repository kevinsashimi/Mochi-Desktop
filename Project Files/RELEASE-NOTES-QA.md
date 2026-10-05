# Release notes — 1.1.9

The small “Fresh out of the tide!” card appears once after an upgrade. It opens
without keyboard focus, stays until dismissed, and links to the full reader.
Settings → What's New opens the same offline history, with a version picker and
a return button that preserves unsaved Settings choices.

The root `CHANGELOG.md` is embedded as `Mochi.ReleaseNotes`. Its newest entry must
match the app version; its first two bullets supply the card's highlights.

## Startup and persistence

- A fresh install records the current version quietly.
- Existing installations without the new marker get one welcome when migrating.
- The updater's `--cleanup-update` restart also requests a welcome when there are
  no existing preferences. Later manual upgrades are detected by version comparison.
- `LastReleaseNotesVersion` is saved with preferences when the card appears.
  Normal restarts and downgrades do not repeat a previously seen release.
- An unread, pending welcome waits for open controls, held mouse buttons,
  destination picking, swims, reactions, feeding, and Playful Mode trips to finish.
  Showing it does not change any activity deadline.
- Like other preferences, persistence is best effort in a read-only folder.

## Verification

All 25 self-test groups passed: 10 companion, 7 updater, 5 Playful Mode, and
3 release-notes groups. The staged executable, root executable, and preview ZIP
match, and the update manifest records the verified 1.1.9 version, size, and hash.

- The release-notes self-tests cover the embedded catalog, version comparison,
  legacy settings, fresh installs, persisted markers, deferred notices, unchanged
  clocks, offline selection, and the Settings entry.
- Live Wine UI checks at 96, 144, and 192 DPI (100%, 150%, and 200%) verify complete
  control/text bounds, screen fit, version selection, both entry points,
  dismissal, and reopening the app. The reader's text area scrolls and its layout
  adapts to the available screen height.
- The welcome leaves Notepad's keyboard focus intact. Reading notes from Settings
  retains unsaved size and Playful Mode choices; Cancel still discards those choices.
- The real program entry point was launched with updater-restart arguments in a
  disposable folder with no settings. It showed the card and persisted the marker;
  the next normal launch stayed quiet.
- Built against Microsoft .NET Framework 4.8 reference assemblies. These checks
  run under Wine; the preview can be checked on a Windows desktop before publishing.

## Try the preview

Quit any running Mochi, extract the preview ZIP, and start its `Mochi.exe`.
The ZIP includes fresh demo settings marked as the previous version so the welcome
appears on that first run. It uses the normal upgrade-detection path. Dismiss it,
restart to confirm it stays quiet, then open Settings → What's New whenever you like.
The demo settings are only in the preview ZIP and do not replace repository preferences.
