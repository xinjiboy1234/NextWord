param(
    [string]$From = "",
    [string]$OutputDir = "",
    [switch]$IdempotentOnly
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $OutputDir) {
    $OutputDir = Join-Path $PSScriptRoot "Migrations"
}

$env:Database__Provider = "PostgreSql"

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Push-Location (Join-Path $repoRoot "Backend")
try {
    if (-not $IdempotentOnly -and $From) {
        $safeFrom = ($From -replace '[^\w]', '_')
        $incrementalPath = Join-Path $OutputDir "Upgrade_From_${safeFrom}.sql"
        Write-Host "Generating incremental migration SQL: $From -> latest"
        dotnet ef migrations script $From `
            --project NextWord.Infrastructure `
            --startup-project NextWord.Api `
            --output $incrementalPath
        Write-Host "  -> $incrementalPath"
    }

    if (-not $From -or -not $IdempotentOnly) {
        $idempotentPath = Join-Path $OutputDir "Upgrade_Idempotent.sql"
        Write-Host "Generating idempotent migration SQL (full history)"
        dotnet ef migrations script --idempotent `
            --project NextWord.Infrastructure `
            --startup-project NextWord.Api `
            --output $idempotentPath
        Write-Host "  -> $idempotentPath"
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Done. Review SQL before applying to production."
Write-Host "  psql `"`$DATABASE_URL`" -f Backend/Scripts/Migrations/Upgrade_Idempotent.sql"
