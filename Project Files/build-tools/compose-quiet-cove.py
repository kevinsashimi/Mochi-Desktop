#!/usr/bin/env python3
"""Blend the generated surface repair into the existing 4K painting at 1:1.

Requires Pillow and NumPy. Arguments: previous PNG, surface patch, output PNG.
The previous PNG is quiet-cove.png from release 1.2.10 (commit 8fa01dd).
"""
import hashlib
import json
from pathlib import Path
import sys

import numpy as np
from PIL import Image


def compose(previous, patch_path, output):
    base = np.asarray(Image.open(previous).convert("RGB"))
    patch = np.asarray(Image.open(patch_path).convert("RGB"))
    if base.shape != (2160, 3840, 3) or patch.shape != (1024, 1536, 3):
        raise ValueError("Use the native 3840x2160 painting and 1536x1024 patch.")

    # Follow the water corridor, feathering toward the kelp and distant reef.
    # No top fade: the old top-edge light must be replaced as well.
    y, x = np.mgrid[:1024, :1536]
    rows = [0, 200, 400, 700, 1024]
    left = np.interp(y, rows, [0, 20, 100, 130, 150])
    right = np.interp(y, rows, [1536, 1270, 1160, 950, 800])

    def smooth(value):
        t = np.clip(value, 0, 1)
        return t * t * (3 - 2 * t)

    mask = smooth((x - left) / 150) * smooth((right - x) / 150)
    mask *= smooth((1023 - y) / 240)
    mask[:, -1] = 0
    result = base.copy()
    region = base[:1024, 1480:3016].astype(np.float64)
    result[:1024, 1480:3016] = np.clip(np.rint(
        region * (1 - mask[:, :, None]) + patch * mask[:, :, None]
    ), 0, 255).astype(np.uint8)

    changed = np.any(result != base, axis=2)
    allowed = np.zeros(base.shape[:2], dtype=bool)
    allowed[:1024, 1480:3016] = mask > 0
    if changed[~allowed].any() or not changed.any():
        raise ValueError("Unexpected changes outside the surface repair.")
    Image.fromarray(result).save(output)
    yy, xx = np.where(changed)
    info = {
        "dimensions": [3840, 2160],
        "sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
        "base_artwork_sha256": hashlib.sha256(previous.read_bytes()).hexdigest(),
        "base_release": "1.2.10",
        "surface_patch_sha256": hashlib.sha256(patch_path.read_bytes()).hexdigest(),
        "patch_rectangle": [1480, 0, 1536, 1024],
        "changed_bounds": [int(xx.min()), int(yy.min()), int(xx.max() + 1), int(yy.max() + 1)],
        "changed_pixels": int(changed.sum()),
        "unchanged_outside_local_repair": True,
        "resized": False,
        "method": "Native-pixel image-generated surface correction with a feathered water-corridor mask; one larger, lower sunlight opening with a soft glow. Original reef, seabed and prior rock repair retained outside the mask."
    }
    output.with_name("quiet-cove-provenance.json").write_text(json.dumps(info, indent=2) + "\n")
    print(json.dumps(info, indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 4:
        sys.exit("Usage: compose-quiet-cove.py previous.png surface-patch.png output.png")
    compose(*(Path(arg) for arg in sys.argv[1:]))
