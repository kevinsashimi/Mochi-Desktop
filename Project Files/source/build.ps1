param(
    [string]$OutputPath="$PSScriptRoot\..\..\Mochi.exe",
    [string]$UpdateManifestPath=""
)
$ErrorActionPreference='Stop'
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if ([IO.Path]::GetFileName($OutputPath) -ne 'Mochi.exe') {
    throw 'Use a staging directory with the filename Mochi.exe so its assembly identity stays consistent.'
}
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.Linq.dll /reference:System.Xml.dll /reference:System.Runtime.Serialization.dll "/resource:$PSScriptRoot\..\..\CHANGELOG.md,Mochi.ReleaseNotes" "/resource:$PSScriptRoot\spritesheet.png,Mochi.Atlas" "/resource:$PSScriptRoot\feeding.png,Mochi.Feeding" "/resource:$PSScriptRoot\snack.png,Mochi.Snack" "/resource:$PSScriptRoot\illustrated-reactions.png,Mochi.Illustrated" "/win32icon:$PSScriptRoot\mochi.ico" "/win32manifest:$PSScriptRoot\app.manifest" "/out:$OutputPath" "$PSScriptRoot\Mochi.cs" "$PSScriptRoot\Feeding.cs" "$PSScriptRoot\Gaze.cs" "$PSScriptRoot\Reactions.cs" "$PSScriptRoot\SmoothMotion.cs" "$PSScriptRoot\Facing.cs" "$PSScriptRoot\Destination.cs" "$PSScriptRoot\AssemblyInfo.cs" "$PSScriptRoot\Updates.cs" "$PSScriptRoot\UpdateTests.cs" "$PSScriptRoot\DesktopIcons.cs" "$PSScriptRoot\Playful.cs" "$PSScriptRoot\PlayfulTests.cs" "$PSScriptRoot\ReleaseNotes.cs" "$PSScriptRoot\ReleaseNotesTests.cs"
if($LASTEXITCODE -ne 0){throw 'Build failed'}
if (-not $UpdateManifestPath -and $OutputPath -eq [IO.Path]::GetFullPath("$PSScriptRoot\..\..\Mochi.exe")) {
    $UpdateManifestPath = "$PSScriptRoot\..\update.xml"
}
if ($UpdateManifestPath) {
    $version = [Reflection.AssemblyName]::GetAssemblyName($OutputPath).Version.ToString()
    $hash = (Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $size = (Get-Item -LiteralPath $OutputPath).Length
    $xml = @"
<?xml version="1.0" encoding="utf-8"?>
<MochiUpdate>
  <Version>$version</Version>
  <File>Mochi.exe</File>
  <Size>$size</Size>
  <Sha256>$hash</Sha256>
</MochiUpdate>
"@
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($UpdateManifestPath), $xml, (New-Object Text.UTF8Encoding($false)))
    Write-Output "Update manifest written to $UpdateManifestPath"
}
