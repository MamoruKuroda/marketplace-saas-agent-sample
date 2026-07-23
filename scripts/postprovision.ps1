#!/usr/bin/env pwsh
# azd postprovision hook (Windows / pwsh).
# Azure SQL cannot create a contained user for a managed identity via ARM/Bicep, so we do it
# here once, as the Entra admin (you, the deployer). This is the automated form of docs/deploy.md
# section 2. Requires the Azure CLI (az) and sqlcmd; if either is missing, run that manual step.
$ErrorActionPreference = 'Stop'

# azd surfaces provisioning outputs as environment variables.
$server        = $env:AZURE_SQL_SERVER_FQDN
$serverName    = $env:AZURE_SQL_SERVER_NAME
$database      = $env:AZURE_SQL_DATABASE_NAME
$resourceGroup = $env:AZURE_RESOURCE_GROUP
$appName       = $env:SERVICE_WEB_NAME   # = the web app's managed-identity display name

if (-not $server -or -not $database -or -not $appName -or -not $serverName -or -not $resourceGroup) {
  Write-Error "Missing azd outputs (AZURE_SQL_* / SERVICE_WEB_NAME / AZURE_RESOURCE_GROUP). Run 'azd provision' first."
}

Write-Host "Granting the app's managed identity [$appName] a database user on $server/$database ..."

# The server firewall only allows Azure services by default; briefly allow this machine to run T-SQL.
$clientIp = (Invoke-RestMethod -Uri 'https://api.ipify.org').Trim()
$ruleName = "azd-postprovision-$([int](Get-Date -UFormat %s))"
az sql server firewall-rule create -g $resourceGroup -s $serverName -n $ruleName `
  --start-ip-address $clientIp --end-ip-address $clientIp | Out-Null

$tsql = @"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$appName')
    CREATE USER [$appName] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [$appName];
ALTER ROLE db_datawriter ADD MEMBER [$appName];
ALTER ROLE db_ddladmin  ADD MEMBER [$appName];  -- needed for EF Core Migrate() on startup
"@

try {
  # -G = Microsoft Entra auth (uses your az/azd login).
  sqlcmd -S $server -d $database -G -l 60 -Q $tsql
  if ($LASTEXITCODE -ne 0) { throw "sqlcmd exited with code $LASTEXITCODE" }
  Write-Host "Done: [$appName] granted db_datareader / db_datawriter / db_ddladmin."
}
catch {
  Write-Warning "Automatic DB-user creation failed: $_"
  Write-Warning "Run the manual step in docs/deploy.md (section 2) once, then 'azd deploy'."
  throw
}
finally {
  az sql server firewall-rule delete -g $resourceGroup -s $serverName -n $ruleName 2>$null | Out-Null
}
