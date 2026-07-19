param(
    [string]$ConnectionString = "Host=localhost;Port=5432;Database=nextword;Username=nextword;Password=nextword"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

Write-Host "=== Bootstrap dev database ===" -ForegroundColor Cyan

& (Join-Path $PSScriptRoot "ef-migrate.ps1") -Action update -ConnectionString $ConnectionString
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Schema is up to date. Start API once to seed demo data:" -ForegroundColor Green
Write-Host "  cd Backend/NextWord.Api"
Write-Host "  dotnet run"
Write-Host ""
Write-Host "Development startup runs MigrateAsync + SeedData (1 user, 6 words, 21 articles)."
