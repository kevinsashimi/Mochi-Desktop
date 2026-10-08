# Illustrated boost animations

`boost-poses.png` is a 768 × 624 RGBA sheet with three rows of four 192 × 208 cells:

| Row | Animation | Poses |
| --- | --- | --- |
| 0 | Rocket Grin | Excited launch, big grin and fin kick, opposite tail stroke, proud settle |
| 1 | Wheee | Fins opening, eyes-closed joy with fins spread, laughing downstroke, relaxed smile |
| 2 | Wink & Giggle | Little fin flourish, wink, closed-eye giggle, satisfied smile |

The built-in image generator created twelve new drawings using Mochi's original
`spritesheet.png` and `illustrated-reactions.png` as character references. The
unchanged generated source is `../artwork/boost-source.png`. These are dedicated
boost drawings, rather than reused gaze frames with an effect placed over them.

The prompt requested a transparent 4 × 3 grid, the same blue spotted whale shark,
cream belly and soft 3D shading, an upright three-quarter left-facing view in every
cell, consistent size, intact anatomy, and four successive poses for each of the
three expressions. No scenery, text, bubbles or speed streaks were painted into
the sprites. The third row's wink flows into a giggle rather than holding one eye
closed for both middle frames. All twelve complete poses were visually reviewed.

`../build-tools/PackBoostArt.cs` reproduces the runtime atlas. Compile with a C#
compiler referencing `System.Drawing.dll`, then run:

```text
PackBoostArt.exe "Project Files/artwork/boost-source.png" "Project Files/source/boost-poses.png"
```

Packing finds each pose's alpha bounds inside its source slot, uses one 0.49 scale
for all twelve drawings and centers the complete body at (96, 108) in each cell.
The source alpha and drawn expressions are preserved. No painted pixels are
replaced with fragments from another pose. The full-resolution source allows
future repacking without quality loss from repeatedly scaling the runtime atlas.

`BoostAnimation.cs` plays one complete illustrated frame at a time, with a gentle
continuous kick between keyframes. Horizontal mirroring supplies the other side;
pitch follows the smoothed heading without flattening the sprite. Text is drawn
after the body transform and stays readable. The 0.76-second visual sequence can
finish its happy settle while the 0.34-second speed impulse is already braking.
Speech and wake particles have their own lifetimes. Normal swimming resumes when
the sequence ends, and exiting Manual mode disposes all boost effects immediately.

Both Windows `build.ps1` and the cloud compiler embed the packed sheet as
`Mochi.Boost`. The source painting and packing tool are not needed to run the app.
