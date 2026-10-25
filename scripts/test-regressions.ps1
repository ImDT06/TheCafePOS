$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$testProject = Join-Path $projectRoot 'TheCafePOS_WPF/TheCafePOS.RegressionTests/TheCafePOS.RegressionTests.csproj'
# Each xUnit test runs the real services over its own temporary database; the shop database is never touched.
dotnet test $testProject -c Release --logger "console;verbosity=normal"
exit $LASTEXITCODE
