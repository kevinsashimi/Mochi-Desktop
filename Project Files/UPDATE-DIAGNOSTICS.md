# Windows update compatibility fix — 1.1.6

The diagnostic report confirmed this failure while reading the updater handoff:

```
System.MissingMethodException:
Method not found: 'System.String System.String.TrimEnd(Char)'.
```

The cloud compiler had referenced Mono's implementation of `String`, which offers `TrimEnd(char)`. Windows .NET Framework provides `TrimEnd(char[])` instead. The updater's temporary-directory check therefore failed before it could load the installation request. Wine Mono supports that extra overload, which is why the previous cloud upgrade tests passed. Moving the executable between OneDrive and Downloads could not resolve this API mismatch.

Version 1.1.6 explicitly uses `TrimEnd(new[] { Path.DirectorySeparatorChar })` in both checks. Cloud builds now use Microsoft's .NET Framework 4.8 reference assemblies with `-noconfig -nostdlib` and explicit DLL paths, preventing unsupported Mono APIs from leaking into Windows binaries. The reference package is Microsoft.NETFramework.ReferenceAssemblies.net48 1.0.3, downloaded from NuGet and checked against its official catalog SHA-512.

## Installing the fix

The old executable also supplies its update helper, so publishing a new download does not repair a helper that cannot finish the installation. Install 1.1.6 manually once:

1. Quit Mochi through its tray menu.
2. Extract the provided 1.1.6 ZIP and copy only `Mochi.exe` over the executable in your normal installation folder.
3. Keep `settings.xml` and the `Project Files` folder.
4. Launch Mochi and confirm **Info** shows 1.1.6 and the expected path.

The fixed helper will be used for future updates. Opening a second copy while Mochi is running reopens the existing instance's Settings, so quit the old instance before testing another folder. The earlier 1.1.4.1 diagnostic build is superseded; installing the uncorrected published 1.1.5 would retain this updater bug.

## Error reporting

Failures show the failed operation, exception type, HRESULT, underlying message, and native Windows error code when available. `update-error.txt` records the full exception and running/target paths next to preferences (`Project Files` when present), with `%LOCALAPPDATA%\Mochi` as a fallback. If both locations are blocked, the dialog retains the error details. Reports stay on the PC and are not uploaded.

Download verification, version checks, parent-process identity checks, waiting for exit, atomic replacement, rollback, and settings preservation remain in place. Mochi closes only after the helper starts successfully.

## Verification

The 1.1.6 build compiles entirely against Microsoft .NET Framework 4.8 reference assemblies. A regression check reads the compiled updater's method references and requires `TrimEnd(char[])`; it rejects the old published 1.1.5 binary under Mono and accepts the corrected binary. This catches the mismatch even on a runtime where the unsupported overload exists.

All 22 self-test groups passed under Wine on 4 October 2026 (Asia/Singapore): ten companion, seven updater, and five Playful Mode groups. Coverage includes valid/invalid temporary-directory checks, integrity checks, waiting for exit, actual helper replacement/restart, rollback, locked files, blocked helper launch, error-report fallback, and preserved settings. Info and Settings UI checks passed. An isolated older-numbered test build with the same corrected updater completed a real GitHub download, shutdown, replacement, restart, and Settings IPC check; that internal build is not distributed. A live check of 1.1.6 on the reporting Windows PC remains pending.
