# Release and update signing

Mochi supports unsigned manual-download releases and signed automatic updates.
Version 1.2.14 is distributed as an **unsigned manual download**.

Signed automatic-update releases use two independent signatures:

- **Authenticode** identifies the publisher of the downloaded `Mochi.exe` and
  protects the first download. Use a currently valid certificate from a trusted
  Windows code-signing issuer and an RFC 3161 timestamp service.
- **Pinned update metadata** protects installed copies even if the GitHub
  repository or release account is later compromised. `update.xml` is signed by
  a separate offline RSA key whose public half is compiled into Mochi.

The private keys and their passwords must never be stored in this repository,
in a build artifact, or in a command transcript. Keep the update-signing PFX in
an offline vault or hardware-backed CI secret. Restrict the Authenticode key to
the release job. Losing the update key requires a manually installed version to
establish a new trust root; rotate it through an update signed by the old key
whenever possible.

## Unsigned manual-download releases

The project owner has chosen unsigned manual distribution for version 1.2.14.
No signing certificate or update-signing key is needed for this mode:

```powershell
& '.\Project Files\source\build.ps1' -ManualRelease
```

This explicitly allows an unsigned repository-root `Mochi.exe` and compiles
`MOCHI_MANUAL_RELEASE`. The app makes no startup update request. **Check for
updates** offers to open the official GitHub download page; it does not download
or install executable code. Users quit Mochi and replace only `Mochi.exe` in the
same folder to preserve settings. Windows may show an unknown-publisher warning.

The mode rejects `-PublicRelease`, `-TestHooks`, an update-manifest path, and
signing PFX configuration. It generates no update manifest. The obsolete 1.2.12
metadata has been removed from the repository so it cannot be mistaken for
metadata matching the new executable. Publish only the executable, a ZIP with
an enclosing folder, manual installation notes and SHA-256 checksums. Checksums
identify the exact download; they do not substitute for a publisher signature.

Verify the executable with `verify-package.py` without `--update-manifest` or
`--require-certificate-table`. Verify the managed/native version, no test hooks,
and downloaded asset hashes. No signature-validation code is bypassed: signed
updates continue to reject missing, forged or mismatched metadata.

## Build modes

Developer self-tests live behind the `MOCHI_TESTS` compiler symbol. Build a test
executable into an isolated folder without any update manifest:

```powershell
& '.\Project Files\source\build.ps1' `
  -OutputPath "$PWD\test-build\Mochi.exe" -NoUpdateManifest -TestHooks
```

A normal staging build omits `-TestHooks` and uses `-NoUpdateManifest`. The
production update key is available only to `-PublicRelease` builds, which also
require trusted Authenticode signing. Internal previews never receive a
production update signature.

Writing the repository-root `Mochi.exe` without `-ManualRelease` automatically
enables signed-public-release rules. The build then fails before compilation unless both signing identities
are configured. Prefer process-scoped environment variables so passwords are
not placed in shell history:

```powershell
$env:MOCHI_UPDATE_SIGNING_PFX='D:\secure\mochi-update-signing.pfx'
$env:MOCHI_UPDATE_SIGNING_PASSWORD='<from the secret store>'
$env:MOCHI_AUTHENTICODE_PFX='D:\secure\trusted-code-signing.pfx'
$env:MOCHI_AUTHENTICODE_PASSWORD='<from the secret store>'
$env:MOCHI_AUTHENTICODE_TIMESTAMP_URL='https://timestamp.example'

& '.\Project Files\source\build.ps1' -PublicRelease
```

The script verifies that the update PFX matches Mochi's pinned public key. It
compiles into a private staging directory, Authenticode-signs and validates the
executable, calculates the final size and SHA-256, signs the canonical update
record with RSA PKCS#1 v1.5 and SHA-256, and only then replaces the release
files. A failure leaves the prior release in place. The exact signed bytes are:

```text
MochiUpdate
Version=<four numeric components>
File=Mochi.exe
Size=<decimal byte count>
Sha256=<lowercase hexadecimal SHA-256>
```

The final line also ends with `\n`. The XML `Signature` element contains the
base64-encoded signature of those UTF-8 bytes.

## Signed-release verification

Run the package verifier against the finished executable and signed manifest:

```powershell
python3 '.\Project Files\build-tools\verify-package.py' `
  .\Mochi.exe '.\Project Files\source\app.manifest' `
  --update-manifest '.\Project Files\update.xml' --require-certificate-table

$signature=Get-AuthenticodeSignature .\Mochi.exe
if($signature.Status -ne 'Valid' -or $null -eq $signature.TimeStamperCertificate){
  throw 'Authenticode trust or timestamp validation failed.'
}
```

The verifier rejects developer test hooks, checks that a PE certificate table is
present, verifies the embedded process manifest and native resources, enforces
the updater's 50 MiB limit, matches the managed assembly name and version to the
manifest, matches the manifest size and SHA-256 to the executable, and verifies
the detached signature with the same pinned public key used by the app.
Windows trust, publisher identity and the RFC 3161 timestamp are established by
the `Get-AuthenticodeSignature` check immediately below it.

Publish the exact verified files as immutable GitHub Release assets, protect the
release environment with required reviewers, and create a signed version tag.
Do not replace an existing version's assets or reuse a version number. Keep
branch protection and signed commits as additional controls; neither substitutes
for the two release signatures.
