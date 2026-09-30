param([string]$OutputPath="$PSScriptRoot\..\Mochi.exe")
$ErrorActionPreference='Stop'
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.Linq.dll /reference:System.Xml.dll "/resource:$PSScriptRoot\spritesheet.png,Mochi.Atlas" "/resource:$PSScriptRoot\feeding.png,Mochi.Feeding" "/resource:$PSScriptRoot\snack.png,Mochi.Snack" "/resource:$PSScriptRoot\illustrated-reactions.png,Mochi.Illustrated" "/win32icon:$PSScriptRoot\mochi.ico" "/win32manifest:$PSScriptRoot\app.manifest" "/out:$OutputPath" "$PSScriptRoot\Mochi.cs" "$PSScriptRoot\Feeding.cs" "$PSScriptRoot\Gaze.cs" "$PSScriptRoot\Reactions.cs" "$PSScriptRoot\SmoothMotion.cs" "$PSScriptRoot\Facing.cs" "$PSScriptRoot\Destination.cs"
if($LASTEXITCODE -ne 0){throw 'Build failed'}
