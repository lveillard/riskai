param(
  [ValidateSet('Check','DeployPreview','DeployProduction')]
  [string]$Action='Check'
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$versionFile=Join-Path $root 'VERSION'
if (-not (Test-Path -LiteralPath $versionFile)) { throw "VERSION file not found at $versionFile" }
$version=(Get-Content -LiteralPath $versionFile -TotalCount 1).Trim()
$webName="Web-v$version"
$workerRoot=Join-Path $root 'deploy\cloudflare'
$worker=Join-Path $workerRoot 'src\worker.js'
if (-not (Test-Path -LiteralPath $worker)) { throw "Worker not found at $worker" }

Push-Location $root
try {
  node --check $worker
  if ($LASTEXITCODE -ne 0) { throw "Node syntax check failed with code $LASTEXITCODE" }
  node (Join-Path $root 'scripts\test_cloudflare_worker.mjs')
  if ($LASTEXITCODE -ne 0) { throw "Worker tests failed with code $LASTEXITCODE" }
  if ($Action -eq 'Check') {
    $build=Join-Path $root "Builds\$webName"
    if (Test-Path -LiteralPath $build) {
      $files=Get-ChildItem -LiteralPath $build -Recurse -File
      $bytes=($files | Measure-Object -Property Length -Sum).Sum
      $largest=$files | Sort-Object Length -Descending | Select-Object -First 1
      Write-Host ("${webName}: {0} files, {1:N0} bytes; largest {2} ({3:N0} bytes)" -f $files.Count,$bytes,$largest.FullName,$largest.Length)
      if ($largest.Length -gt 25MB) { Write-Host 'Static Workers Assets is not selected; deploy the streaming origin proxy.' }
    } else {
      Write-Host "Builds/$webName is not present yet; run Unity BuildWeb before production verification."
    }
    Write-Host 'Checks passed. No Wrangler command was run.'
    return
  }

  Push-Location $workerRoot
  try {
    $wranglerArgs=@('--yes','--package','wrangler@4.136.1','wrangler','deploy','--config','wrangler.toml')
    if ($Action -eq 'DeployProduction') { $wranglerArgs += '--env'; $wranglerArgs += 'production' }
    & npx @wranglerArgs
    if ($LASTEXITCODE -ne 0) { throw "Wrangler exited with code $LASTEXITCODE" }
  } finally {
    Pop-Location
  }
} finally {
  Pop-Location
}
