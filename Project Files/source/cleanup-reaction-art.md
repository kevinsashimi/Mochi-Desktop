# Ocean Cleanup reaction artwork

`cleanup-reactions.png` contains twelve complete 192 × 208 poses in a 4 × 3 grid:

- Anger: gathering squint, fins drawing inward, determined hold, release.
- Displeasure: unimpressed eyelids, one small fin gesture, the other fin gesture, settle.
- Sadness: level head and normal-size eyes, half blink, closed blink, open eyes.

The image generator used the original idle cell as its character reference and
was asked to preserve its small black eyes, full anatomy, upright left-facing
view, and consistent head height. The sadness row specifically excludes nodding,
bowing, nose dips, tears, and enlarged eyes. The unmodified generated painting is
`../artwork/cleanup-reactions-source.png` (1448 × 1086 RGBA).

`../build-tools/PackCleanupArt.cs` packs the runtime sheet. It extracts the twelve
large connected alpha components because an opened fin crosses a grid boundary.
It discards only near-invisible/disconnected transparency dust, applies the same
0.5 scale to every pose, and registers the drawings by head coordinates. It never
centers a frame on its changing fin bounds or resizes/repaints individual eyes.
No fragments from different poses are combined. Keep the source for repacking:

```text
PackCleanupArt.exe "Project Files/artwork/cleanup-reactions-source.png" "Project Files/source/cleanup-reactions.png"
```

`CleanupReaction` in `OceanCleanup.cs` plays one opaque complete pose at a time.
Anger and displeasure have timed fin/eyelid gestures and restrained continuous
breathing/side drift; sadness has one gentle blink with no body translation,
rotation or scaling. Horizontal mirroring supplies the opposite facing. Speech
is drawn afterward. The four-second reaction and three-second closing message
are unchanged. These drawings are exclusive to Ocean Cleanup; other reactions
retain their original artwork and timing.

Both Windows and cloud builds embed the sheet as `Mochi.Cleanup`. The atlas owns
and disposes all twelve cells. Cleanup tests compare the sad eye size with the
original idle artwork, check level resting sadness, moving anger/displeasure,
blinking, and clear canvas edges at every size and in both directions.
