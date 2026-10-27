param(
    [string]$OutputPath="$PSScriptRoot\..\..\Mochi.exe",
    [string]$UpdateManifestPath="",
    [switch]$NoUpdateManifest,
    [switch]$TestHooks,
    [switch]$PublicRelease,
    [switch]$ManualRelease,
    [string]$UpdateSigningPfxPath=$env:MOCHI_UPDATE_SIGNING_PFX,
    [string]$UpdateSigningPassword=$env:MOCHI_UPDATE_SIGNING_PASSWORD,
    [string]$AuthenticodePfxPath=$env:MOCHI_AUTHENTICODE_PFX,
    [string]$AuthenticodePassword=$env:MOCHI_AUTHENTICODE_PASSWORD,
    [string]$AuthenticodeTimestampUrl=$env:MOCHI_AUTHENTICODE_TIMESTAMP_URL
)
$ErrorActionPreference='Stop'

$ExpectedUpdateModulus='xouWeDgE9/Lng9LSI/nnbajT+W7tWt0pt5K8rsyehyjkOzH6w8WNTiAkkkhVAjR6CL7DJdAxaAyucnMhp2ck8rsCuwydqpBE5JANY7rC/MHCgOE4yIxtEnn92yBp51ca7z1/GW/62MlhsUhjeQKtqnAf6Ote3pg2oCGPGsVHrjM9cVA96QA1/UkzmqrUmdHKwZBZAdAstm5tnwl+Sr4cZBR51vqa8uYXA/PbfPALa8ddj0KZrk1WxHrZPLwNf+abJOxU17p9QMfTx2D8raBmF5DmLOVXEea7yPuF4qDNUfi6reY+p+xrOs5dV0/pcSZOy8TcXCMnbEa+KDhm0cPHVTHzDTaob1i2+aod+QfYqziwxBL7CeqaW2Ceg2TR6L2CpQ3Z5ZrLm7HcFYhi+jd45UNgsyHU2vrSP+l1WDCVQEKhwmjgMZEdEskLW/eoJfiCONCTvQssm56pSMigb2KDBew4gSVxTh4fmwmkfhUJWS6YQck/tM50K0xLGXx7uzaO/5p3ZpA/P+u+aRbR7hg5JHoj1KZsCcWeRUyUawjfWbZMG8plrygXYLwP+1FmEtygsiGLRAt00N23Ju5kcwOVJqtu+OuwtM/3ciRi7MWw71qc9tJDA7orBl8LtLeyYXqkIjmKATof4wD3pcKp+TECRo56x92oBcejh63xJ2fo8Ns='
$ExpectedUpdateExponent='AQAB'

function Open-SigningCertificate([string]$Path,[string]$Password,[string]$Purpose) {
    if ([String]::IsNullOrWhiteSpace($Path)) { throw "$Purpose PFX path is required." }
    if ([String]::IsNullOrEmpty($Password)) { throw "$Purpose PFX password is required." }
    $full=[IO.Path]::GetFullPath($Path)
    if (-not [IO.File]::Exists($full)) { throw "$Purpose PFX was not found." }
    try {
        $certificate=New-Object Security.Cryptography.X509Certificates.X509Certificate2(
            $full,$Password,[Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
    } catch {
        throw "$Purpose PFX could not be opened. Check the configured path and password."
    }
    if (-not $certificate.HasPrivateKey) { $certificate.Dispose(); throw "$Purpose certificate has no private key." }
    return $certificate
}

function Open-RsaPrivateKey($Certificate,[string]$Purpose) {
    $rsa=[Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($Certificate)
    if ($null -eq $rsa) { throw "$Purpose certificate must contain an RSA private key." }
    return $rsa
}

function Protect-ReleaseDirectory([string]$Path) {
    # Public release staging must not inherit broad write permissions from a
    # workspace. The release identity, SYSTEM, and local administrators retain
    # full control, and created files inherit the same protection.
    $security=New-Object Security.AccessControl.DirectorySecurity
    $security.SetAccessRuleProtection($true,$false)
    $inheritance=[Security.AccessControl.InheritanceFlags]::ContainerInherit -bor
        [Security.AccessControl.InheritanceFlags]::ObjectInherit
    $propagation=[Security.AccessControl.PropagationFlags]::None
    $allow=[Security.AccessControl.AccessControlType]::Allow
    $full=[Security.AccessControl.FileSystemRights]::FullControl
    $identities=@(
        [Security.Principal.WindowsIdentity]::GetCurrent().User,
        (New-Object Security.Principal.SecurityIdentifier(
            [Security.Principal.WellKnownSidType]::LocalSystemSid,$null)),
        (New-Object Security.Principal.SecurityIdentifier(
            [Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid,$null))
    )
    foreach ($identity in $identities) {
        $rule=New-Object Security.AccessControl.FileSystemAccessRule(
            $identity,$full,$inheritance,$propagation,$allow)
        [void]$security.AddAccessRule($rule)
    }
    (New-Object IO.DirectoryInfo($Path)).SetAccessControl($security)
}

function Assert-ReleaseAuthenticode([string]$Path,$ExpectedCertificate,[bool]$RequireTimestamp) {
    $signature=Get-AuthenticodeSignature -FilePath $Path
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Authenticode trust validation failed for $Path`: $($signature.Status)."
    }
    if ($null -eq $signature.SignerCertificate -or
        -not [String]::Equals($signature.SignerCertificate.Thumbprint,
            $ExpectedCertificate.Thumbprint,[StringComparison]::OrdinalIgnoreCase)) {
        throw "Authenticode publisher validation failed for $Path."
    }
    if ($RequireTimestamp -and $null -eq $signature.TimeStamperCertificate) {
        throw "The Authenticode signature on $Path is not timestamped."
    }
}

$OutputPath=[IO.Path]::GetFullPath($OutputPath)
if ([IO.Path]::GetFileName($OutputPath) -ne 'Mochi.exe') {
    throw 'Use a staging directory with the filename Mochi.exe so its assembly identity stays consistent.'
}
$repositoryOutput=[IO.Path]::GetFullPath("$PSScriptRoot\..\..\Mochi.exe")
# Unsigned distribution is an explicit manual-download mode, never an updater
# package. Omitting that switch keeps the signed-release requirements intact.
if ($ManualRelease) {
    if ($PublicRelease -or $TestHooks -or -not [String]::IsNullOrWhiteSpace($UpdateManifestPath)) {
        throw 'ManualRelease cannot be combined with PublicRelease, TestHooks, or an update manifest.'
    }
    if (-not [String]::IsNullOrWhiteSpace($AuthenticodePfxPath) -or
        -not [String]::IsNullOrWhiteSpace($UpdateSigningPfxPath)) {
        throw 'ManualRelease must not use signing certificates. Use PublicRelease for signed updates.'
    }
    $NoUpdateManifest=$true
}
if ($OutputPath -eq $repositoryOutput -and -not $ManualRelease) { $PublicRelease=$true }
if ($NoUpdateManifest -and -not [String]::IsNullOrWhiteSpace($UpdateManifestPath)) {
    throw 'NoUpdateManifest and UpdateManifestPath cannot be used together.'
}
if ($PublicRelease -and $NoUpdateManifest) {
    throw 'A public release must include a signed update manifest.'
}
if ($PublicRelease -and $TestHooks) {
    throw 'A public release cannot include developer test hooks.'
}
if (-not $NoUpdateManifest -and [String]::IsNullOrWhiteSpace($UpdateManifestPath) -and
    $OutputPath -eq $repositoryOutput) {
    $UpdateManifestPath="$PSScriptRoot\..\update.xml"
}
$writeManifest=-not [String]::IsNullOrWhiteSpace($UpdateManifestPath)
$outputDirectory=[IO.Path]::GetDirectoryName($OutputPath)
if ($TestHooks -and $writeManifest) {
    throw 'A test-hook build cannot generate an update manifest. Build it in an isolated staging folder.'
}
if ($writeManifest -and -not $PublicRelease) {
    throw 'Production-key update manifests require PublicRelease so the executable is also Authenticode-signed.'
}
if (-not [IO.Directory]::Exists($outputDirectory)) { throw 'The output directory does not exist.' }
if ($writeManifest) {
    $manifest=[IO.Path]::GetFullPath($UpdateManifestPath)
    if ([IO.Path]::GetFileName($manifest) -cne 'update.xml') { throw 'The update manifest filename must be update.xml.' }
    if ($manifest -eq $OutputPath) { throw 'The executable and update manifest paths must be different.' }
    $manifestDirectory=[IO.Path]::GetDirectoryName($manifest)
    if (-not [IO.Directory]::Exists($manifestDirectory)) { throw 'The update manifest directory does not exist.' }
}
if ($PublicRelease -and -not $writeManifest) { throw 'A public release must include a signed update manifest.' }
if ($PublicRelease -and [String]::IsNullOrWhiteSpace($AuthenticodePfxPath)) {
    throw 'A public release requires an Authenticode PFX from a publicly trusted code-signing issuer.'
}
if ($PublicRelease -and [String]::IsNullOrWhiteSpace($AuthenticodeTimestampUrl)) {
    throw 'A public release requires an Authenticode timestamp URL.'
}
$updateCertificate=$null
$updateRsa=$null
$authenticodeCertificate=$null
$stageDirectory=$null
try {
    # Validate signing configuration before replacing any output. A public update
    # manifest is never emitted without the private key pinned by the app.
    if ($writeManifest) {
        $updateCertificate=Open-SigningCertificate $UpdateSigningPfxPath $UpdateSigningPassword 'Update signing'
        $updateRsa=Open-RsaPrivateKey $updateCertificate 'Update signing'
        $public=$updateRsa.ExportParameters($false)
        if ([Convert]::ToBase64String($public.Modulus) -cne $ExpectedUpdateModulus -or
            [Convert]::ToBase64String($public.Exponent) -cne $ExpectedUpdateExponent) {
            throw 'Update signing PFX does not match the public key pinned by Mochi.'
        }
    }
    if (-not [String]::IsNullOrWhiteSpace($AuthenticodePfxPath)) {
        $authenticodeCertificate=Open-SigningCertificate $AuthenticodePfxPath $AuthenticodePassword 'Authenticode signing'
    }

    $stageDirectory=Join-Path $outputDirectory ('.mochi-build-'+[Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($stageDirectory) | Out-Null
    if ($PublicRelease -or $ManualRelease) { Protect-ReleaseDirectory $stageDirectory }
    $buildOutput=Join-Path $stageDirectory 'Mochi.exe'
    $compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $compileOptions=@()
    if ($TestHooks) { $compileOptions+='/define:MOCHI_TESTS' }
    if ($ManualRelease) { $compileOptions+='/define:MOCHI_MANUAL_RELEASE' }
    & $compiler "/resource:$PSScriptRoot\wallpaper\quiet-cove.png,Mochi.Wallpaper.QuietCove" /nologo /target:winexe /optimize+ /platform:anycpu $compileOptions /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.Linq.dll /reference:System.Xml.dll /reference:System.Runtime.Serialization.dll "/resource:$PSScriptRoot\..\..\CHANGELOG.md,Mochi.ReleaseNotes" "/resource:$PSScriptRoot\spritesheet.png,Mochi.Atlas" "/resource:$PSScriptRoot\feeding.png,Mochi.Feeding" "/resource:$PSScriptRoot\snack.png,Mochi.Snack" "/resource:$PSScriptRoot\illustrated-reactions.png,Mochi.Illustrated" "/resource:$PSScriptRoot\boost-poses.png,Mochi.Boost" "/resource:$PSScriptRoot\cleanup-reactions.png,Mochi.Cleanup" "/resource:$PSScriptRoot\ocean-litter.png,Mochi.OceanLitter" "/resource:$PSScriptRoot\ocean-bins.png,Mochi.OceanBins" "/resource:$PSScriptRoot\ocean-open-nets.png,Mochi.OpenNets" "/win32icon:$PSScriptRoot\mochi.ico" "/win32manifest:$PSScriptRoot\app.manifest" "/out:$buildOutput" "$PSScriptRoot\Mochi.cs" "$PSScriptRoot\BackgroundPlacementTests.cs" "$PSScriptRoot\Feeding.cs" "$PSScriptRoot\Gaze.cs" "$PSScriptRoot\Reactions.cs" "$PSScriptRoot\SmoothMotion.cs" "$PSScriptRoot\Facing.cs" "$PSScriptRoot\Destination.cs" "$PSScriptRoot\ManualSwimming.cs" "$PSScriptRoot\ManualBoost.cs" "$PSScriptRoot\BoostAnimation.cs" "$PSScriptRoot\ManualGuide.cs" "$PSScriptRoot\ManualSwimTests.cs" "$PSScriptRoot\AssemblyInfo.cs" "$PSScriptRoot\Updates.cs" "$PSScriptRoot\UpdateTests.cs" "$PSScriptRoot\DesktopIcons.cs" "$PSScriptRoot\Playful.cs" "$PSScriptRoot\PlayfulTests.cs" "$PSScriptRoot\ReleaseNotes.cs" "$PSScriptRoot\ReleaseNotesTests.cs" "$PSScriptRoot\StaticWallpaper.cs" "$PSScriptRoot\DesktopWallpaperHost.cs" "$PSScriptRoot\StaticWallpaperTests.cs" "$PSScriptRoot\SecurityHardeningTests.cs" "$PSScriptRoot\OceanCleanup.cs" "$PSScriptRoot\CleanupTests.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

    # Manual releases omit Authenticode and update metadata. Signed updater
    # releases retain their publisher and timestamp checks.
    if ($null -ne $authenticodeCertificate) {
        $authenticodeArguments=@{FilePath=$buildOutput;Certificate=$authenticodeCertificate;HashAlgorithm='SHA256';IncludeChain='All'}
        if (-not [String]::IsNullOrWhiteSpace($AuthenticodeTimestampUrl)) {
            $authenticodeArguments.TimestampServer=$AuthenticodeTimestampUrl
        }
        $signed=Set-AuthenticodeSignature @authenticodeArguments
        Assert-ReleaseAuthenticode $buildOutput $authenticodeCertificate $PublicRelease
        Write-Output 'Authenticode signature and trust validation passed.'
    }

    if ($writeManifest) {
        # Revalidate publisher identity immediately before authenticating the
        # executable with the offline update key.
        Assert-ReleaseAuthenticode $buildOutput $authenticodeCertificate $true
        $assemblyVersion=[Reflection.AssemblyName]::GetAssemblyName($buildOutput).Version
        $version='{0}.{1}.{2}.{3}' -f $assemblyVersion.Major,$assemblyVersion.Minor,$assemblyVersion.Build,[Math]::Max(0,$assemblyVersion.Revision)
        $hash=(Get-FileHash -LiteralPath $buildOutput -Algorithm SHA256).Hash.ToLowerInvariant()
        $size=(Get-Item -LiteralPath $buildOutput).Length
        $canonical="MochiUpdate`nVersion=$version`nFile=Mochi.exe`nSize=$size`nSha256=$hash`n"
        $utf8=New-Object Text.UTF8Encoding($false)
        $digest=[Security.Cryptography.SHA256]::Create()
        try { $payloadHash=$digest.ComputeHash($utf8.GetBytes($canonical)) } finally { $digest.Dispose() }
        $formatter=New-Object Security.Cryptography.RSAPKCS1SignatureFormatter($updateRsa)
        $formatter.SetHashAlgorithm('SHA256')
        $signature=[Convert]::ToBase64String($formatter.CreateSignature($payloadHash))
        $xml=@"
<?xml version="1.0" encoding="utf-8"?>
<MochiUpdate>
  <Version>$version</Version>
  <File>Mochi.exe</File>
  <Size>$size</Size>
  <Sha256>$hash</Sha256>
  <Signature>$signature</Signature>
</MochiUpdate>
"@
        $pendingManifest=Join-Path $manifestDirectory ('.mochi-manifest-'+[Guid]::NewGuid().ToString('N')+'.tmp')
        [IO.File]::WriteAllText($pendingManifest,$xml,$utf8)
        $pendingManifestHash=(Get-FileHash -LiteralPath $pendingManifest -Algorithm SHA256).Hash
    }

    # Publish only after compilation and every signature operation succeeds. If
    # manifest installation fails, restore the previous executable.
    $suffix=[Guid]::NewGuid().ToString('N')
    $outputBackup=$OutputPath+'.mochi-build-backup-'+$suffix
    $outputExisted=[IO.File]::Exists($OutputPath)
    $outputInstalled=$false
    $manifestInstalled=$false
    $preserveOutputBackup=$false
    $preserveManifestBackup=$false
    if ($writeManifest) {
        $manifestBackup=$manifest+'.mochi-build-backup-'+$suffix
        $manifestExisted=[IO.File]::Exists($manifest)
    }
    try {
        if ($PublicRelease) {
            Assert-ReleaseAuthenticode $buildOutput $authenticodeCertificate $true
        }
        if ($outputExisted) { [IO.File]::Replace($buildOutput,$OutputPath,$outputBackup) }
        else { [IO.File]::Move($buildOutput,$OutputPath) }
        $outputInstalled=$true
        if ($PublicRelease) {
            Assert-ReleaseAuthenticode $OutputPath $authenticodeCertificate $true
        }
        if ($writeManifest) {
            # A replacement between hashing and publication must fail before
            # its update metadata becomes visible.
            if ((Get-Item -LiteralPath $OutputPath).Length -ne $size -or
                (Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $hash) {
                throw 'The executable changed after its update metadata was signed.'
            }
            if ($manifestExisted) { [IO.File]::Replace($pendingManifest,$manifest,$manifestBackup) }
            else { [IO.File]::Move($pendingManifest,$manifest) }
            $manifestInstalled=$true
            if ((Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash -cne $pendingManifestHash) {
                throw 'The update manifest changed during publication.'
            }
        }
        if ($PublicRelease) {
            # Validate the final pathname too; public release checks should
            # describe the files that were actually published.
            Assert-ReleaseAuthenticode $OutputPath $authenticodeCertificate $true
        }
        if ($writeManifest) {
            # Keep the detached signature tied to the exact final executable and
            # manifest paths, including if another validly signed Mochi binary was
            # substituted during publication.
            if ((Get-Item -LiteralPath $OutputPath).Length -ne $size -or
                (Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $hash) {
                throw 'The published executable does not match its signed update metadata.'
            }
            if ((Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash -cne $pendingManifestHash) {
                throw 'The published update manifest does not match the signed staging copy.'
            }
        }
    } catch {
        $publishError=$_
        $rollbackErrors=New-Object Collections.Generic.List[string]
        try {
            if ($manifestInstalled) {
                if ($manifestExisted -and [IO.File]::Exists($manifestBackup)) {
                    if ([IO.File]::Exists($manifest)) { [IO.File]::Replace($manifestBackup,$manifest,$null) }
                    else { [IO.File]::Move($manifestBackup,$manifest) }
                } elseif (-not $manifestExisted -and [IO.File]::Exists($manifest)) {
                    [IO.File]::Delete($manifest)
                }
            }
        } catch {
            $preserveManifestBackup=$true
            $rollbackErrors.Add("manifest rollback: $($_.Exception.Message)")
        }
        try {
            if ($outputInstalled) {
                if ($outputExisted -and [IO.File]::Exists($outputBackup)) {
                    if ([IO.File]::Exists($OutputPath)) { [IO.File]::Replace($outputBackup,$OutputPath,$null) }
                    else { [IO.File]::Move($outputBackup,$OutputPath) }
                } elseif (-not $outputExisted -and [IO.File]::Exists($OutputPath)) {
                    [IO.File]::Delete($OutputPath)
                }
            }
        } catch {
            $preserveOutputBackup=$true
            $rollbackErrors.Add("executable rollback: $($_.Exception.Message)")
        }
        if ($rollbackErrors.Count -ne 0) {
            throw "Build publication failed and the previous release could not be fully restored. Recovery backups were retained. $([String]::Join('; ',$rollbackErrors))"
        }
        throw $publishError
    } finally {
        if (-not $preserveOutputBackup -and [IO.File]::Exists($outputBackup)) { [IO.File]::Delete($outputBackup) }
        if ($writeManifest -and -not $preserveManifestBackup -and
            [IO.File]::Exists($manifestBackup)) { [IO.File]::Delete($manifestBackup) }
        if ($writeManifest -and [IO.File]::Exists($pendingManifest)) { [IO.File]::Delete($pendingManifest) }
    }
    Write-Output "Build written to $OutputPath"
    if ($ManualRelease) { Write-Output 'Unsigned manual-download release. No update manifest was generated.' }
    if ($writeManifest) { Write-Output "Signed update manifest written to $manifest" }
} finally {
    if ($null -ne $updateRsa) { $updateRsa.Dispose() }
    if ($null -ne $updateCertificate) { $updateCertificate.Dispose() }
    if ($null -ne $authenticodeCertificate) { $authenticodeCertificate.Dispose() }
    if ($null -ne $stageDirectory -and [IO.Directory]::Exists($stageDirectory)) {
        [IO.Directory]::Delete($stageDirectory,$true)
    }
}
