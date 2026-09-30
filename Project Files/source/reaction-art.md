# Petting and play animation sources

`illustrated-reactions.png` contains 32 newly generated poses in four coherent eight-frame rows, with 192 x 208 pixel cells:

0. Cheek nuzzle: chin/cheek lift, closed-eye contentment, small fin gestures, and return to rest.
1. Flipper hug: both attached flippers close across the cream belly, hold, and open again.
2. Barrel roll: nose remains toward screen-left while belly and spotted back rotate around the long body axis.
3. Backflip: nose travels left, up, right while inverted, down, and left again.

The built-in image tool generated each full row using the canonical Mochi reference, the existing idle frame, and an eight-slot layout guide. `illustrated-prompts.md` records the prompts and targeted refinements. No API fallback was used.

Deterministic processing removes the chroma background where present, extracts connected pose groups, applies one shared scale per coherent row, centers complete poses within their cells, and performs one edge-color cleanup pass preserving alpha. Every final cell has clear margins. Original and feeding atlases are unchanged.

`Reactions.cs` plays the new illustrated frames directly, with gentle movement and timed speech. Barrel and backflip orientation comes from the artwork, without rotating a flat sprite to supply missing poses. A small variation in barrel-roll angular spacing was accepted after independent review confirmed a complete continuing longitudinal roll and a clean upright return.

Petting cycles: cheek nuzzle, happy wiggle, cozy sway, high flipper, flipper hug. Existing counters keep their original ordering, with the hug appended.
Play cycles: barrel roll, backflip, double hop, fin dance.
