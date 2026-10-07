# Quiet Cove static wallpaper — Windows checks

The live wallpaper engine and its artwork are removed. Quiet Cove is a new still painting and defaults off, including when upgrading from a live-wallpaper preview. Its full resolution is 3840 × 2160. A 4K UHD display shows the complete painting at 1:1, without a blue surround. The original cove remains at its original scale in the centre. A localized foreground repair crosses its old rectangular boundary to remove a visible overlap above the starfish; do not restore the old rectangle over this repair. Smaller displays fit the full picture proportionally; other aspect ratios/larger displays may have a blue surround. No upscaling or cropping.

## Artwork review

Inspect the rocks above the starfish at full size: there must be no rectangular cut, doubled boulder outline or abrupt horizontal edge. The surrounding sand, starfish and rest of the scene should retain their previous appearance.

## Try on Windows 10 and 11

1. Quit any older Mochi instance. Extract the preview folder and run Mochi.exe. Check that your normal wallpaper appears and Settings has Quiet Cove unchecked.
2. Repeat with a copy of old settings containing OceanWallpaper=true. The new option must still be off; other preferences must survive.
3. Check Quiet Cove in Settings and Cancel: no change. Check it again and Save: the painting appears below desktop icons and Mochi.
4. Use desktop shortcuts, select icons and open the desktop context menu. Check an app covering the wallpaper; typing/focus and task switching must work normally.
5. Save other Settings changes while it is enabled; the wallpaper stays visible. Restart Mochi: the explicit opt-in persists.
6. Disable Quiet Cove and Save, then reenable it. Quit Mochi. Each exit/disable reveals the original background without changing Windows wallpaper settings. Confirm it also restores after Task Manager ends Mochi.
7. Check 1080p, 4K, ultrawide, portrait and mixed-DPI monitors, including a monitor left of the primary display. Each image keeps its proportions and remains beneath icons.
8. Reconnect a monitor, change resolution/DPI, restart Explorer and lock/unlock Windows. Mochi should attach to the new desktop within a few seconds (up to 30 seconds after a failed attempt). Existing icons and their positions must stay unchanged.
9. Confirm Mochi still performs Idle Activities, follows the pointer, swims and uses Playful Mode normally. Check Settings and the centred update welcome at 100% and 200% scaling.

If it remains hidden, open Settings → Wallpaper status and keep the generated wallpaper-status.txt. Cloud tests use Wine and controlled native desktop fixtures; they do not certify behavior on real Windows Explorer or alongside another wallpaper app.
