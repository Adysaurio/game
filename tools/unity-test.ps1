# Windows: runs Unity EditMode tests in batch mode. Close the Unity editor first.
# Usage: .\tools\unity-test.ps1 [testFilter]
param([string]$Filter = "")
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "TrashPandas"
$Version = ((Get-Content (Join-Path $Project "ProjectSettings/ProjectVersion.txt")) -match '^m_EditorVersion: ' -replace '^m_EditorVersion: ', '').Trim()
$Unity = "C:\Program Files\Unity\Hub\Editor\$Version\Editor\Unity.exe"
if (-not (Test-Path $Unity)) { throw "Unity $Version not found at $Unity (install it from Unity Hub)" }
$Out = Join-Path $Root "test-results"
New-Item -ItemType Directory -Force $Out | Out-Null
$Results = Join-Path $Out "editmode.xml"
$Log = Join-Path $Out "editmode.log"
if (Test-Path $Results) { Remove-Item $Results }
$argsList = @("-batchmode", "-nographics", "-projectPath", "`"$Project`"", "-runTests", "-testPlatform", "EditMode", "-testResults", "`"$Results`"", "-logFile", "`"$Log`"")
if ($Filter) { $argsList += @("-testFilter", $Filter) }
$p = Start-Process -FilePath $Unity -ArgumentList $argsList -Wait -PassThru -NoNewWindow
if (-not (Test-Path $Results)) { Write-Host "No test results produced (compile error or editor open?). Last log lines:"; Get-Content $Log -Tail 40; exit 1 }
[xml]$x = Get-Content $Results
$run = $x.'test-run'
Write-Host "total=`"$($run.total)`" passed=`"$($run.passed)`" failed=`"$($run.failed)`""
if ($p.ExitCode -ne 0) { Select-String -Path $Results -Pattern 'result="Failed"' -Context 1, 6 | ForEach-Object { $_.Context.PreContext + $_.Line } }
exit $p.ExitCode
