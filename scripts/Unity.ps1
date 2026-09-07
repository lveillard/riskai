param([ValidateSet('Prepare','Build','BuildWeb','Test','PlayTests','Open','Status')][string]$Action='Open')
$ErrorActionPreference='Stop'
$riskRoot=Split-Path $PSScriptRoot -Parent
$riskProject=Join-Path $riskRoot 'RiskAI'
$riskCli='C:\Program Files\Unity Hub\resources\cli\unity.exe'
if (-not (Test-Path -LiteralPath $riskCli)) { throw "Unity CLI not found at $riskCli" }
switch ($Action) {
  'Open' { & $riskCli open $riskProject }
  'Status' { & $riskCli status --json }
  'Prepare' { & $riskCli run $riskProject -- -executeMethod RiskAI.Editor.RiskProjectSetup.Prepare -logFile (Join-Path $riskProject 'Logs\prepare.log') }
  'Build' { & $riskCli run $riskProject -- -executeMethod RiskAI.Editor.RiskProjectSetup.BuildWindows -logFile (Join-Path $riskProject 'Logs\build.log') }
  'BuildWeb' { & $riskCli run $riskProject -- -buildTarget WebGL -executeMethod RiskAI.Editor.RiskProjectSetup.BuildWeb -logFile (Join-Path $riskProject 'Logs\build-web.log') }
  'Test' { & $riskCli test $riskProject --mode EditMode --output (Join-Path $riskRoot 'TestResults\editmode.xml') -- -logFile (Join-Path $riskProject 'Logs\editmode.log') }
  'PlayTests' { & $riskCli test $riskProject --mode PlayMode --output (Join-Path $riskRoot 'TestResults\playmode.xml') -- -logFile (Join-Path $riskProject 'Logs\playmode.log') }
}
exit $LASTEXITCODE
