# Mochi Desktop working instructions

These are Kevin's standing preferences for this repository. Follow them unless
Kevin gives different instructions in chat, subject to system, developer, and
access requirements. Keep this file self-contained for cloud sessions.

## Workspace and cloud work

- Use Codex Cloud by default for substantial project work. Verify the selected
  environment and repository at the start; do not rely on a saved environment ID.
- Reuse this repository and its existing checkout. Preserve the project structure
  and existing edits; check for existing projects before creating new folders or
  repositories. Do not change this repository's ownership or visibility.
- The authorized repository is https://github.com/kevinsashimi/Mochi-Desktop.
  It is public, so review every staged file before uploading.
- Kevin's designated local project location is ChatGPT Workspace under his
  Windows My Workspaces folder. Verify access and the intended project directory
  before using it. When unavailable, explicitly report this and continue in the
  selected cloud checkout; record local synchronization as pending.
- Do not claim to have inspected or synchronized the Windows copy from cloud.
  OneDrive synchronization is separate from Git and cloud execution. Do not
  modify another tool's workspace unless Kevin requests it.
- Continue an existing cloud task when appropriate. New task briefs must include
  the objective, requirements, repository/branch, relevant files, decisions,
  acceptance criteria, validation commands, and expected deliverables. Confirm
  that a task has started before reporting that it is running.
- Report unavailable tools, dependencies, access, or inputs precisely. Use
  supported cloud secret/configuration mechanisms for missing credentials;
  do not silently move substantial work to the local PC.

## Changes, delivery, and coordination

- Read PROJECT.md and any relevant handoff before resuming work. Maintain its
  Objective, Requirements, Architecture, Progress, Decisions, and Outstanding
  tasks sections, including repository, branch/PR, task reference, working paths,
  synchronization status, and validation results when relevant.
- Keep related code, documentation, artwork, and deliverables together while
  preserving the existing repository layout. Use descriptive names.
- Use targeted file operations; avoid unnecessary recursive searches and repeated
  reads. Preserve approved requirements, existing content, and uncommitted work.
- Commit relevant changes with meaningful messages. Deliver cloud work on a
  pushed branch or pull request with validation and a current project handoff,
  unless Kevin explicitly requests a different delivery.
- Do not automatically merge, tag, publish, or deploy merely because work is
  finished. Follow the authorization given for the current task.
- Before uploads, review staged files and ignore/exclude secrets, credentials,
  personal/client data, local-only files, and unrelated workspace content. Keep
  personal Windows paths and private task identifiers out of this public repo.
- On return to local work, inspect local edits and fetch before updating the
  Windows project. Preserve changes and resolve conflicts without discarding
  another agent's work. Record synchronization that remains pending.
- For explicitly requested collaboration with Claude or Hermes, coordinate
  through shared specifications and PROJECT.md or HANDOFF.md. Record ownership
  and avoid simultaneous edits to the same files. Do not move the project to a
  shared workspace without approval or assume Windows shared-folder access.

## Mochi requirements and validation

- Preserve Mochi's usual placement behind app windows, existing animation
  behavior, and the single-window Ocean Cleanup rendering that prevents flicker.
- Distribution is currently an unsigned manual download with startup and menu
  update checks. Preserve the separate signed-update verification protections.
- Follow README.md for builds and release packaging. Use .NET Framework 4.8
  reference assemblies; isolate test builds and never ship developer test hooks.
- Run checks appropriate to the change and report their results and limitations.
  Distinguish Wine/Xvfb checks from native Windows testing. Documentation-only
  edits normally require diff and content checks, not a new application build.
- Verify production packages with:
  `python3 'Project Files/build-tools/verify-package.py' Mochi.exe 'Project Files/source/app.manifest'`.
- Preview ZIPs must contain an enclosing folder and use concise serial names,
  for example `Mochi-Desktop-1.2.17-preview-v1.zip`. Determine the actual version
  from the current task; do not reuse a published version for different bytes.
- Keep explanations concise and report unresolved issues clearly. For code-review
  findings, give file names and exact line numbers only unless Kevin requests
  explanations.
