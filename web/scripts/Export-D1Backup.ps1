param(
  [string]$Database = "pronostico-abasto",
  [string]$OutputDirectory = ".\backups"
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$output = Join-Path $OutputDirectory "$Database-$timestamp.sql"

npx wrangler d1 export $Database --remote --output $output -y
Write-Host "Backup generado: $output"
