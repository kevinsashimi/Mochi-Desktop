# Supplemental feeding artwork

Generated with the built-in image tool, using Mochi's canonical base, existing idle frame, and eight-slot layout guide as references. The original Codex v2 atlas is unchanged.

Shared strip requirements: eight complete separated full-body poses in one row, identical toy whale-shark identity, blue body, cream belly, white spots, dark eyes, stable scale, pure green background (#00FF00) for deterministic extraction. No text, speech bubbles, shadows, scenery, props, detached effects, guide marks, overlap, or clipping. Front three-quarter face oriented toward screen-left, tail toward screen-right.

Gulp strip: rest smile; lean/open mouth for gulp; closed mouth with gently puffed cheeks; relaxed cheeks and satisfied closed-eye blink; happy tail swish and lifted flipper; opposite tail swish and other flipper; gentle flutter; rest smile.

Tummy-pat strip: rest; near flipper raises inward to cream belly; contact/pat with contented eyes; release; second pat; belly bob and lowering flipper; blink; rest smile. Contact is on the belly, not the chin.

Happy-roll strip: rest; 15-degree sideways lean; 35-degree roll; 65-degree roll exposing cream belly; happy hold; 35 degrees back; 15 degrees back; rest. Preserve intrinsic volume and attached anatomy. No full spin.

Shrimp prop: one isolated tiny coral-pink curled shrimp, matching smooth matte toy material, thick rounded C-shaped body, simple segments and tail fan, small dark eye, no fragile antennae or legs, genuinely transparent background. No pet in this asset.

Parent processing extracts the generated poses, applies one shared scale per coherent row, registers them into 192 x 208 cells, and performs one deterministic green-edge despill pass. The app animates the separate shrimp toward the mouth and uses the original swim frames for the chase.
