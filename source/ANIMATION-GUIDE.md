# Mochi desktop companion

- Every animation in Feed a tiny snack, Pet Mochi, and Play together must have both left-facing and right-facing versions, including all future additions. Use the common mirroring renderers; mirror effects with the body while keeping speech readable.
- Enumerate both facing variants automatically. Keep tests covering both directions for every animation; do not introduce one-direction exceptions.
- Preserve the last interaction or movement facing when returning to idle.
- A single click on Mochi selects a random animation across all categories. Dragging must not trigger a click reaction. No double-click interaction. Come here opens a cancellable screen destination picker and swims to the selected spot; it must not teleport Mochi.
- Keep Mochi behind other apps and above the desktop background. Preserve settings and the Windows startup toggle when installing updates.
- Source and build script: `outputs/Mochi Desktop/source/`. Build to a staging path, run the executable's `--self-test` checks, and review relevant rendered previews before replacing the installed executable.
