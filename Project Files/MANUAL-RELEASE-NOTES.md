Mochi Desktop 1.2.16 keeps Mochi on course when Windows changes his drawing window or delays an animation frame.

- Mochi tracks his position and body size independently of his Windows drawing window, preventing unexpected window changes from sending him to the top-left corner.
- Ocean Cleanup returns to normal swimming in one frame. Temporary drawing failures preserve the previous scene until the transition succeeds.
- Normal swimming and **Come here** journeys continue smoothly after delayed frames, retaining their routes, speed, acceleration and deceleration.
- Startup and menu update checks still offer the official GitHub page for newer published releases. Drafts and previews are excluded.
- Downloads and installation remain manual. No account, API key or paid certificate is needed to use Mochi.

Download **Mochi-Desktop-1.2.16.zip** for an enclosing folder with the executable, instructions and test results, or download **Mochi.exe** on its own. Quit Mochi before replacing the executable in your existing folder. Keep your settings and `Project Files` folder.

This release is **unsigned**. Windows may show an unknown-publisher or SmartScreen warning. SHA-256 checksums identify the released files; they are not a Windows publisher signature.

Built against .NET Framework 4.8. All 69 regression groups and seven live UI checks passed under Wine/Xvfb. This is the exact executable tested in preview v2. See the included test report for validation and its executable hash; the automated checks were not run on native Windows 11.
