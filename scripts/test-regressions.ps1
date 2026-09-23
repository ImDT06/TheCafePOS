$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$testProject = Join-Path $projectRoot 'TheCafePOS_WPF/TheCafePOS.RegressionTests/TheCafePOS.RegressionTests.csproj'
dotnet build $testProject -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$testAssembly = Join-Path $projectRoot 'TheCafePOS_WPF/TheCafePOS.RegressionTests/bin/Release/net10.0-windows/TheCafePOS.RegressionTests.dll'
$testDataDirectory = Join-Path $projectRoot ('artifacts/regression-' + [guid]::NewGuid().ToString('N'))
dotnet $testAssembly $testDataDirectory create
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet $testAssembly $testDataDirectory reload
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet $testAssembly $testDataDirectory finance
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet $testAssembly $testDataDirectory finance-reload
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$sessionDataDirectory = Join-Path $testDataDirectory 'session'
dotnet $testAssembly $sessionDataDirectory session
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet $testAssembly $sessionDataDirectory session-reload
exit $LASTEXITCODE
