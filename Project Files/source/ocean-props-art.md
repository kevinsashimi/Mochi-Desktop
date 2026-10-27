# Illustrated ocean litter and Ocean Bins

The image generator created two original transparent sprite sheets for Ocean
Cleanup, with saturated color, chunky silhouettes, a navy contour and a light
outer edge. It was asked for isolated complete game-style objects with no text,
scenery, or floating pieces. Original paintings are preserved unchanged:

- `../artwork/ocean-litter-source.png`: 1536 × 1024, four columns (bottle,
  plastic bag, net, rubbish can) and three rows of distinct designs.
- `../artwork/ocean-bins-source.png`: 1536 × 1024, three columns and two rows:
  pearl clam, coral basket, buoy barrel, salvage chest, nautilus pot, submarine pod.

`../build-tools/PackOceanProps.cs` extracts the large alpha-connected objects,
removes only disconnected/near-invisible transparency noise, preserves aspect
ratio, and packs them with clear margins. It does not recolor or repaint them.

```text
PackOceanProps.exe litter "Project Files/artwork/ocean-litter-source.png" "Project Files/source/ocean-litter.png"
PackOceanProps.exe bins "Project Files/artwork/ocean-bins-source.png" "Project Files/source/ocean-bins.png"
```

Runtime sheets are 512 × 384 (twelve 128px litter cells) and 480 × 320 (six
160px bin cells). The build embeds them as `Mochi.OceanLitter` and
`Mochi.OceanBins`; Atlas owns and disposes every cell. A trip keeps one color variant through floating, pickup, carry and deposit.
Preview v5 adds `../artwork/ocean-open-nets-source.png` (three open nets in one
row), packed by `PackOceanProps.exe nets` into `ocean-open-nets.png` (480 × 160,
160px cells), embedded as `Mochi.OpenNets`. The generated artwork is preserved
unchanged except for alpha cleanup, crop and aspect-preserving scaling.
The open net narrows and crossfades to its matching bundle during pickup.
Mouth/flipper grip is independently random for each trip. Bin receiving
anchors match the visible openings, with a small shrinking/fading deposit arc.

The floating surface is 84px (120px for sprawled nets), held litter 44px, and Ocean Bin 112px. Light/dark
outlines provide contrast; a brief two-wave arrival ripple adds motion without
leaving permanent effects. Props stay behind normal apps and never capture input.
Every cleanup uses an Ocean Bin at a randomized safe spot on the current monitor.
The app neither finds nor uses the Windows Recycle Bin for cleanup.

Preview v6 renders the complete upright held prop above the animated body without
mouth or flipper occlusion masks. Both grips use Playful Mode's attachment points,
centered for the 44px litter canvas. The prop is drawn without an extra resampling
pass while carrying. The open-to-gathered net transition and deposit fade remain.

Preview v8 keeps this appearance on one persistent 120px transparent native
surface. The 84px floating sprite (120px for an open net) and 44px carried sprite
are centered inside it; only the artwork scales during pickup and deposit. The
surface does not resize, hide, or switch into the pet bitmap during pickup.
Floating tilt eases to upright as the grip closes. Attachment points and the
carrying bob remain shared with the pet pose.

Preview v9 makes the layered bitmap the only paint source: native paint and
print-client requests cannot fall back to the STATIC control's background/text
painter. Presentation runs back to front, updating litter after Mochi so that
overlapping pickup and disposal frames do not repaint underneath it afterward.


Preview v10 supersedes the separate native litter/bin surfaces described above.
All objects now share one companion scene, composited bin -> pet -> litter and
uploaded atomically. The 120px prop bitmap remains a drawing tile only; it is no
longer a window. Sprite sizing, net gathering, grips and opacity are unchanged.
