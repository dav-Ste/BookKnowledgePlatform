<#
Start infrastructure compose with helpful environment checks and retries.

Usage:
  pwsh ./scripts/start-infrastructure.ps1            # interactive, uses defaults from .env or environment
  pwsh ./scripts/start-infrastructure.ps1 -Force     # non-interactive
  pwsh ./scripts/start-infrastructure.ps1 -PullOnly  # only pull images

This script will:
 - Load .env if present to populate environment variables
 - Ensure required env vars have defaults
 - Attempt to pull images with retries
 - Start docker compose and report helpful errors
#>

param(
	[switch]$Force,
	[switch]$PullOnly
)

function Load-EnvFile {
	$envFile = Join-Path $PSScriptRoot '..' '.env'
	if (Test-Path $envFile) {
		Write-Host "Loading environment from $envFile"
		Get-Content $envFile | ForEach-Object {
			$_ = $_.Trim()
			if (-not $_ -or $_.StartsWith('#')) { return }
			$parts = $_ -split '=', 2
			if ($parts.Count -eq 2) {
				$name = $parts[0].Trim()
				$value = $parts[1].Trim('"')
				if (-not [string]::IsNullOrEmpty($name) -and -not [string]::IsNullOrEmpty($value)) {
				if (-not (Get-ChildItem Env:$name -ErrorAction SilentlyContinue)) {
						Write-Host "Setting env $name"
						[System.Environment]::SetEnvironmentVariable($name, $value, "Process")
					}
				}
			}
		}
	}
}

Push-Location $PSScriptRoot/..
Load-EnvFile

# Ensure defaults exist
if (-not $env:MSSQL_SA_PASSWORD) { $env:MSSQL_SA_PASSWORD = 'LocalOnly!ChangeMe123'; Write-Host 'Using default MSSQL_SA_PASSWORD' }
if (-not $env:MINIO_ROOT_PASSWORD) { $env:MINIO_ROOT_PASSWORD = 'LocalOnly!ChangeMe123'; Write-Host 'Using default MINIO_ROOT_PASSWORD' }

function Retry-Command([scriptblock]$script, [int]$attempts = 3, [int]$delaySeconds = 3) {
	for ($i=1; $i -le $attempts; $i++) {
		try {
			& $script
			return $true
		} catch {
			Write-Warning "Attempt $i failed: $($_.Exception.Message)"
			if ($i -lt $attempts) { Start-Sleep -Seconds $delaySeconds }
		}
	}
	return $false
}

if ($PullOnly) {
	Write-Host 'Pulling images only...'
	$ok = Retry-Command { docker compose -f docker-compose.infrastructure.yml pull } 4 5
	if (-not $ok) { Write-Error 'Failed to pull images after retries'; exit 1 }
	Write-Host 'Pulled images successfully'; Pop-Location; exit 0
}

if (-not $Force) {
	$ans = Read-Host 'This will (re)start infrastructure. Continue? (y/n)'
	if ($ans -ne 'y') { Write-Host 'Aborted by user'; Pop-Location; exit 0 }
}

Write-Host 'Pulling images...'
$pulled = Retry-Command { docker compose -f docker-compose.infrastructure.yml pull } 4 5
if (-not $pulled) { Write-Error 'Image pull failed after retries. Check image names/tags and network access.'; Pop-Location; exit 1 }

Write-Host 'Starting infrastructure...'
try {
	docker compose -f docker-compose.infrastructure.yml up -d
} catch {
	Write-Error "Failed to start infrastructure: $($_.Exception.Message)"
	Pop-Location
	exit 1
}

Write-Host 'Waiting for SQL container to initialize (check logs with docker compose -f docker-compose.infrastructure.yml logs -f sql)'
Write-Host 'Done. If SQL login still fails, inspect SQL logs and ensure MSSQL_SA_PASSWORD matches the one used by the container.'

Pop-Location
