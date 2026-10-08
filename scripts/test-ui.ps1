$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    dotnet run --project scripts/ui-smoke/UiSmoke.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw "UI smoke checks failed (exit $LASTEXITCODE)." }
} finally { Pop-Location }
