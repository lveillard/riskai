<#
.SYNOPSIS
  Riesgus Unity helper: open, prepare, build, test and quick-compile the RiskAI project.
.EXAMPLE
  .\scripts\Unity.ps1 -Action Test
  .\scripts\Unity.ps1 -Action PlayTests -Filter 'RiskAI.PlayTests.NavalTests' -Results playmode-naval
  .\scripts\Unity.ps1 -Action Test -Category Fast -DryRun
  .\scripts\Unity.ps1 -Action QuickCompile -Assemblies Core,Runtime
  .\scripts\Unity.ps1 -Action CleanResults -Days 14 [-Apply]
.NOTES
  Exit codes: the Unity CLI exit code for Unity actions (0 = success), 3 when the project is
  already open/in use by another Unity, 1 for other script errors. RISKAI_UNITY_CLI overrides
  the Unity CLI path. Keep this file ASCII: Windows PowerShell 5.1 reads BOM-less files as ANSI.
#>
param(
  [ValidateSet('Prepare','Build','BuildWeb','Test','PlayTests','Open','Status','QuickCompile','CleanResults')]
  [string]$Action='Open',
  # Test/PlayTests: Unity CLI --filter (test name pattern).
  [string]$Filter,
  # Test/PlayTests: run one slice N/M of the suite (Unity CLI --shard), e.g. 1/4.
  [string]$Shard,
  # Test/PlayTests: results report of a full run the shards are computed from.
  [string]$ShardInventory,
  # Test/PlayTests: Unity -testCategory (semicolon-separated NUnit categories).
  [string]$Category,
  # Test/PlayTests: writes TestResults/<Results>.xml and RiskAI/Logs/<Results>.log (default editmode/playmode).
  [string]$Results,
  # QuickCompile: subset of Core, Runtime, Tests, PlayTests, Editor.
  [string[]]$Assemblies,
  # CleanResults: age threshold in days; without -Apply it is a dry run.
  [double]$Days=14,
  [switch]$Apply,
  # Print the Unity command instead of running it (busy check still runs).
  [switch]$DryRun
)
$ErrorActionPreference='Stop'
$riskRoot=Split-Path $PSScriptRoot -Parent
$riskProject=Join-Path $riskRoot 'RiskAI'
$riskLogs=Join-Path $riskProject 'Logs'
$riskResults=Join-Path $riskRoot 'TestResults'
$riskCli=if ($env:RISKAI_UNITY_CLI) { $env:RISKAI_UNITY_CLI } else { 'C:\Program Files\Unity Hub\resources\cli\unity.exe' }

function Get-RiskVersion {
  $file=Join-Path $riskRoot 'VERSION'
  if (-not (Test-Path -LiteralPath $file)) { throw "VERSION file not found at $file" }
  return (Get-Content -LiteralPath $file -TotalCount 1).Trim()
}

function Invoke-RiskPython([string[]]$PythonArgs) {
  $python=Get-Command python -ErrorAction SilentlyContinue
  if ($python) { & $python.Source @PythonArgs | Out-Host } else { & py -3 @PythonArgs | Out-Host }
  return $LASTEXITCODE
}

# Returns a description of whatever holds the project, or $null when it is free.
function Get-ProjectBusyReason {
  $lock=Join-Path $riskProject 'Temp\UnityLockfile'
  if (Test-Path -LiteralPath $lock) {
    try { $stream=[System.IO.File]::Open($lock,'Open','ReadWrite','None'); $stream.Close() }
    catch { return "RiskAI\Temp\UnityLockfile is locked by a running editor" }
  }
  try { $processes=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction Stop) } catch { $processes=@() }
  $pattern='(^|[\s"''=])'+[regex]::Escape($riskProject.ToLowerInvariant())+'\\?($|[\s"''])'
  foreach ($process in $processes) {
    if (-not $process.CommandLine) { continue }
    $commandLine=$process.CommandLine.Replace('/','\').ToLowerInvariant()
    if ($commandLine -match $pattern) {
      $shown=$process.CommandLine; if ($shown.Length -gt 160) { $shown=$shown.Substring(0,160)+'...' }
      return "Unity.exe PID $($process.ProcessId): $shown"
    }
  }
  return $null
}

function Assert-ProjectFree {
  $reason=Get-ProjectBusyReason
  if ($reason) {
    [Console]::Error.WriteLine("ERROR: El proyecto RiskAI ya esta abierto o en uso por otro Unity. Cierralo o espera a que termine; solo puede haber un Unity por proyecto.")
    [Console]::Error.WriteLine("ERROR: The RiskAI project is already open or in use by another Unity. Close it or wait for it to finish; only one Unity per project.")
    [Console]::Error.WriteLine("       $reason")
    exit 3
  }
}

function Invoke-RiskUnity([string[]]$UnityArgs) {
  if (-not (Test-Path -LiteralPath $riskCli)) { throw "Unity CLI not found at $riskCli (set RISKAI_UNITY_CLI to override)" }
  if ($DryRun) {
    Write-Host ("DRY RUN: `"$riskCli`" "+(($UnityArgs | ForEach-Object { if ($_ -match '\s') { "`"$_`"" } else { $_ } }) -join ' '))
    return 0
  }
  & $riskCli @UnityArgs | Out-Host
  return $LASTEXITCODE
}

function Write-TestSummary([string]$ResultsPath,[string]$LogPath,[datetime]$StartedAt) {
  if (-not (Test-Path -LiteralPath $ResultsPath)) { Write-Host "No results file was written: $ResultsPath"; Write-Host "Log: $LogPath"; return }
  $item=Get-Item -LiteralPath $ResultsPath
  if ($item.LastWriteTime -lt $StartedAt) { Write-Host "WARNING: $ResultsPath predates this run (stale results)." }
  try { [xml]$report=Get-Content -LiteralPath $ResultsPath -Raw -Encoding UTF8 }
  catch { Write-Host "Could not parse results XML $ResultsPath : $($_.Exception.Message)"; Write-Host "Log: $LogPath"; return }
  $run=$report.SelectSingleNode('/test-run')
  if (-not $run) { Write-Host "Results file has no <test-run>: $ResultsPath"; Write-Host "Log: $LogPath"; return }
  $duration=0.0; [void][double]::TryParse($run.GetAttribute('duration'),[System.Globalization.NumberStyles]::Float,[System.Globalization.CultureInfo]::InvariantCulture,[ref]$duration)
  Write-Host ("Tests {0}: total {1}, passed {2}, failed {3}, skipped {4}, duration {5:N1}s" -f `
    $run.GetAttribute('result'),$run.GetAttribute('total'),$run.GetAttribute('passed'),$run.GetAttribute('failed'),$run.GetAttribute('skipped'),$duration)
  $failed=@($report.SelectNodes("//test-case[starts-with(@result,'Failed')]"))
  if ($failed.Count -gt 0) {
    Write-Host "Failed tests (first $([Math]::Min(20,$failed.Count)) of $($failed.Count)):"
    $failed | Select-Object -First 20 | ForEach-Object { Write-Host "  - $($_.GetAttribute('fullname'))" }
  }
  Write-Host "Results: $ResultsPath"
  Write-Host "Log: $LogPath"
}

function Invoke-RiskTests([string]$Mode,[string]$DefaultName) {
  $name=if ($Results) { [System.IO.Path]::GetFileNameWithoutExtension($Results) } else { $DefaultName }
  if (-not $name) { throw "-Results must be a file name such as playmode-naval" }
  $resultsPath=Join-Path $riskResults "$name.xml"
  $logPath=Join-Path $riskLogs "$name.log"
  # The Unity CLI owns filtering and sharding; only editor flags go after `--`.
  $unityArgs=@('test',$riskProject,'--mode',$Mode,'--output',$resultsPath)
  if ($Filter) { $unityArgs+=@('--filter',$Filter) }
  if ($Shard) { $unityArgs+=@('--shard',$Shard); if ($ShardInventory) { $unityArgs+=@('--shard-inventory',$ShardInventory) } }
  $unityArgs+=@('--','-logFile',$logPath)
  if ($Category) { $unityArgs+=@('-testCategory',$Category) }
  if (-not $DryRun) { New-Item -ItemType Directory -Force -Path $riskResults | Out-Null }
  $startedAt=Get-Date
  $code=Invoke-RiskUnity $unityArgs
  if (-not $DryRun) { Write-TestSummary $resultsPath $logPath $startedAt } else { Write-Host "Results: $resultsPath"; Write-Host "Log: $logPath" }
  return $code
}

function Invoke-RiskMethod([string]$Method,[string]$LogName,[string[]]$Extra=@()) {
  $logPath=Join-Path $riskLogs $LogName
  $code=Invoke-RiskUnity (@('run',$riskProject,'--')+$Extra+@('-executeMethod',"RiskAI.Editor.RiskProjectSetup.$Method",'-logFile',$logPath))
  Write-Host "Log: $logPath"
  return $code
}

$unityActions='Open','Prepare','Build','BuildWeb','Test','PlayTests'
if ($unityActions -contains $Action) { Assert-ProjectFree }
$exitCode=0
switch ($Action) {
  'Open' { $exitCode=Invoke-RiskUnity @('open',$riskProject) }
  'Status' { $exitCode=Invoke-RiskUnity @('status','--json') }
  'Prepare' { $exitCode=Invoke-RiskMethod 'Prepare' 'prepare.log' }
  'Build' {
    $exitCode=Invoke-RiskMethod 'BuildWindows' 'build.log'
    Write-Host ("Output: "+(Join-Path $riskRoot ("Builds\Windows-v"+(Get-RiskVersion)+"\RiskAI.exe")))
  }
  'BuildWeb' {
    $exitCode=Invoke-RiskMethod 'BuildWeb' 'build-web.log' @('-buildTarget','WebGL')
    Write-Host ("Output: "+(Join-Path $riskRoot ("Builds\Web-v"+(Get-RiskVersion))))
  }
  'Test' { $exitCode=Invoke-RiskTests 'EditMode' 'editmode' }
  'PlayTests' { $exitCode=Invoke-RiskTests 'PlayMode' 'playmode' }
  'QuickCompile' { $exitCode=Invoke-RiskPython (@((Join-Path $PSScriptRoot 'quick_compile.py'))+@($Assemblies | Where-Object { $_ })) }
  'CleanResults' {
    $cleanArgs=@((Join-Path $PSScriptRoot 'clean_test_results.py'),'--days',$Days.ToString([System.Globalization.CultureInfo]::InvariantCulture))
    if ($Apply) { $cleanArgs+='--apply' }
    $exitCode=Invoke-RiskPython $cleanArgs
  }
}
exit $exitCode
