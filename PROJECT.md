# Mochi Desktop

## Objective

Maintain a friendly animated whale shark companion for Windows, with smooth
movement, optional Ocean Cleanup, and reliable manual-download updates.

## Requirements

- Preserve normal behind-app placement and existing companion interactions.
- Keep Ocean Cleanup props and Mochi visible through pickup, carrying, and disposal.
- Prevent unexpected native-window geometry changes and delayed frames from
  teleporting Mochi during normal or guided swimming.
- Ship an unsigned manual-download edition with startup/menu update checks;
  preserve signature verification in the separate signed updater.
- Deliver previews in an enclosing-folder ZIP with a version and preview serial.
- Follow AGENTS.md for cloud delivery, repository hygiene, and synchronization.

## Architecture

- C# Windows Forms application targeting .NET Framework 4.8.
- Source and embedded artwork: `Project Files/source/`.
- Production executable and checksum: repository-root `Mochi.exe` and
  `Mochi.exe.sha256`.
- Companion model position and body size are independent of native window bounds.
  Ocean Cleanup uses one composed scene to avoid overlapping-window flicker.
- README.md documents builds. `Project Files/build-tools/` validates and packages
  tested binaries; `.github/workflows/manual-release.yml` uploads a draft release
  from an explicitly pushed stable tag or a manual workflow invocation.

## Progress

- Repository: https://github.com/kevinsashimi/Mochi-Desktop (public).
- Default branch: `main`.
- Active branch: `main`. The documentation from `docs/workspace-instructions`
  has been merged with Kevin's approval; no pull request was needed.
- Current task completed: adopt Kevin's uploaded workspace organization instructions.
  This continues the existing Mochi Desktop cloud conversation; no shareable
  cloud-task URL is available. This document on `main` is the portable handoff.
- Current cloud checkout: `/workspace/Mochi-Desktop`.
- Windows project path: unavailable and not verified in this session. The
  designated local workspace is ChatGPT Workspace; no local mirror was created
  or modified. Local synchronization is pending until access is available.
- Latest stable release verified: [1.2.16](https://github.com/kevinsashimi/Mochi-Desktop/releases/tag/v1.2.16),
  based on commit `8c7b8268b2e3b3890835d7400179e416aaf084a7`.
- Release validation already completed: 69 regression groups and seven live UI
  checks under Wine/Xvfb, plus public download/checksum verification. These are
  historical application checks, not a new native Windows test run.
- This handoff changes documentation only. Validation: documentation review and
  `git diff --check`; the application and published release remain unchanged.

## Decisions

- Use the existing authorized cloud repository for ongoing project work.
- Store applicable standing guidance in repository-root AGENTS.md so future
  sessions do not depend on access to a Windows-only instruction file.
- Keep personal workspace paths and private task identifiers out of this public
  repository. The full supplied instructions remain in the current conversation.
- The documentation was delivered on a separate pushed branch and subsequently
  merged into `main` after Kevin's explicit approval. Future work still follows
  the review and authorization requirements in AGENTS.md.

## Outstanding tasks

- When Windows access is available, verify the existing project location and
  inspect uncommitted work before synchronizing the accepted cloud changes.
- No application feature or bug-fix request is currently pending.
