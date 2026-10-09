# Windows: runs a static editor method in batch mode. Close the Unity editor first.
# Usage: .\tools\unity-run.ps1 TrashPandas.EditorTools.GreyboxSceneBuilder.BuildAll
param([Parameter(Mandatory = $true)][string]$Method)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "TrashPandas"
$Version = ((Get-Content (Join-Path $Project "ProjectSettings/ProjectVersion.txt")) -match '^m_EditorVersion: ' -replace '^m_EditorVersion: ', '').Trim()
$Unity = "C:\Program Files\Unity\Hub\Editor\$Version\Editor\Unity.exe"
if (-not (Test-Path $Unity)) { throw "Unity $Version not found at $Unity (install it from Unity Hub)" }
New-Item -ItemType Directory -Force (Join-Path $Root "test-results") | Out-Null
$Log = Join-Path $Root "test-results/run.log"
$p = Start-Process -FilePath $Unity -ArgumentList @("-batchmode", "-nographics", "-quit", "-projectPath", "`"$Project`"", "-executeMethod", $Method, "-logFile", "`"$Log`"") -Wait -PassThru -NoNewWindow
if ($p.ExitCode -ne 0) { Get-Content $Log -Tail 40 }
exit $p.ExitCode
