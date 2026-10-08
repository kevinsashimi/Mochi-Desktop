# Quiet Cove artwork

quiet-cove.png is the final 3840 × 2160 RGB wallpaper. It fills a 4K UHD display at 1:1, without enlarging the source image or using a blue surround.

The 1.2.7 painting expanded the original 1672 × 941 cove with eight adjoining generated sections at their native pixel scale. In 1.2.8, a local repair removed a rectangular overlap through the foreground rocks above the starfish.

In 1.2.11, the approved single-sun preview's lighting is transferred to a native 1536 × 1024 surface patch. One larger sunlight opening sits slightly lower with a soft golden glow; the lower hotspot is replaced by blue water ripples. The generated patch is blended into the existing 4K painting at (1480, 0), at 1:1 scale, with a feathered mask following the water corridor. The smaller 1672 × 941 review preview is not enlarged to make the final wallpaper. Pixels outside the local surface repair, including the previous rock repair and the seabed, remain identical to 1.2.10.

Do not paste the old rectangular centre over the repaired artwork: that reintroduces the visible seam. The original scene retains its scale and composition, but its pixels in the repair area are intentionally retouched. Python composition was explicitly authorized by the user.

quiet-cove-provenance.json records the current PNG checksum, previous artwork checksum, native patch checksum, repair bounds and preservation outside the repair. The wallpaper is not resized. Different aspect ratios and displays larger than 3840 × 2160 may still show a blue surround; smaller screens fit it proportionally.

To reproduce the 1.2.11 composition, obtain this PNG from release 1.2.10 (commit `8fa01dd`) as `previous.png`. With Pillow and NumPy installed, run from the repository root:

```text
python3 "Project Files/build-tools/compose-quiet-cove.py" previous.png "Project Files/artwork/quiet-cove-surface-soft-glow.png" "Project Files/source/wallpaper/quiet-cove.png"
```

The patch file is the unchanged image-generator output, using a native crop of the prior 4K painting as its structural reference and the approved soft-glow preview as its lighting reference. The composition script rejects mismatched dimensions and verifies that pixels outside its mask remain unchanged. These source materials and build tools are not required to run Mochi.
