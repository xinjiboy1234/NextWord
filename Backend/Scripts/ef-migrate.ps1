param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("add", "update", "list")]
    [string]$Action,

    [string]$Name = "",
    [string]$ConnectionString = "Host=localhost;Port=5432;Database=nextword;Username=nextword;Password=nextword"
)

$ErrorActionPreference = "Stop"

function Test-PostgresConnection {
    param([string]$Conn)

    if ($Conn -match "Host=([^;]+).*Port=(\d+).*Database=([^;]+).*Username=([^;]+).*Password=([^;]+)") {
        $hostName = $Matches[1]
        $port = $Matches[2]
        $db = $Matches[3]
        $user = $Matches[4]
        $pass = $Matches[5]
    }
    else {
        throw "Invalid connection string. Expected Host=...;Port=...;Database=...;Username=...;Password=..."
    }

    $tcp = Test-NetConnection -ComputerName $hostName -Port $port -WarningAction SilentlyContinue
    if (-not $tcp.TcpTestSucceeded) {
        return $false
    }

    $env:PGPASSWORD = $pass
    if (Get-Command psql -ErrorAction SilentlyContinue) {
        psql -h $hostName -p $port -U $user -d $db -c "SELECT 1" | Out-Null
        return $LASTEXITCODE -eq 0
    }

    $container = docker ps --filter "publish=$port" --format "{{.Names}}" 2>$null | Select-Object -First 1
    if ($container) {
        docker exec $container psql -U $user -d $db -c "SELECT 1" | Out-Null
        return $LASTEXITCODE -eq 0
    }

    return $false
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$backend = Join-Path $repoRoot "Backend"
$env:ConnectionStrings__PostgreSql = $ConnectionString

Write-Host "Checking PostgreSQL: $ConnectionString"
if (-not (Test-PostgresConnection -Conn $ConnectionString)) {
    Write-Host ""
    Write-Host "ERROR: Cannot connect to database. Aborting." -ForegroundColor Red
    Write-Host "Start local Postgres first:" -ForegroundColor Yellow
    Write-Host "  docker compose up -d postgres"
    Write-Host "Then verify ConnectionStrings:PostgreSql in appsettings.Development.json"
    exit 1
}
Write-Host "Database connection OK." -ForegroundColor Green

Push-Location $backend
try {
    switch ($Action) {
        "add" {
            if (-not $Name) { throw "Action 'add' requires -Name, e.g. -Name AddMyFeature" }
            Write-Host "Creating migration: $Name"
            dotnet ef migrations add $Name `
                --project NextWord.Infrastructure `
                --startup-project NextWord.Api
        }
        "update" {
            Write-Host "Applying migrations..."
            dotnet ef database update `
                --project NextWord.Infrastructure `
                --startup-project NextWord.Api
        }
        "list" {
            dotnet ef migrations list `
                --project NextWord.Infrastructure `
                --startup-project NextWord.Api
        }
    }
}
finally {
    Pop-Location
}
