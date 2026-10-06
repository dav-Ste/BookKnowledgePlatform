<#
Reset SQL data volume used by docker-compose.infrastructure.yml and restart infrastructure.

WARNING: This will delete the SQL Server data volume and DATA WILL BE LOST. Use only for local development.

Usage:
  # interactive (will prompt for confirmation)
  pwsh ./scripts/reset-sql-data.ps1

  # non-interactive (force)
  pwsh ./scripts/reset-sql-data.ps1 -AutoConfirm
#>

param(
	[switch]$AutoConfirm
)

Write-Host "Stopping infrastructure compose..."
docker compose -f docker-compose.infrastructure.yml down

Write-Host "Searching for Docker volumes that look like SQL data volumes..."
$volumes = docker volume ls --format "{{.Name}}" | Where-Object { $_ -match "(?i)sql-?data|sql-data|bookknowledge.*sql" }

if (-not $volumes -or $volumes.Count -eq 0) {
	Write-Warning "No volumes matching 'sql-data' found. Listing all volumes for inspection:"
	docker volume ls
	exit 1
}

Write-Host "Found the following candidate volumes:" -ForegroundColor Cyan
$volumes | ForEach-Object { Write-Host " - $_" }

if (-not $AutoConfirm) {
	$answer = Read-Host "Delete these volumes and reinitialize SQL (THIS WILL DELETE DATA). Type 'yes' to continue"
	if ($answer -ne 'yes') {
		Write-Host "Aborting. No volumes were removed." -ForegroundColor Yellow
		exit 0
	}
}

foreach ($v in $volumes) {
	Write-Host "Removing volume: $v"
	docker volume rm $v
}

Write-Host "Starting infrastructure (will reinitialize SQL with MSSQL_SA_PASSWORD from env or docker-compose)..."
docker compose -f docker-compose.infrastructure.yml up -d

Write-Host "Done. Verify SQL container logs and that SA login works with the configured password." -ForegroundColor Green
