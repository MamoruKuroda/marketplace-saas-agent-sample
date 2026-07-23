#!/bin/sh
# azd postprovision hook (Linux / macOS).
# Azure SQL cannot create a contained user for a managed identity via ARM/Bicep, so we do it
# here once, as the Entra admin (you, the deployer). This is the automated form of docs/deploy.md
# section 2. Requires the Azure CLI (az) and sqlcmd; if either is missing, run that manual step.
set -e

# azd surfaces provisioning outputs as environment variables.
server="$AZURE_SQL_SERVER_FQDN"
serverName="$AZURE_SQL_SERVER_NAME"
database="$AZURE_SQL_DATABASE_NAME"
resourceGroup="$AZURE_RESOURCE_GROUP"
appName="$SERVICE_WEB_NAME"   # = the web app's managed-identity display name

if [ -z "$server" ] || [ -z "$database" ] || [ -z "$appName" ] || [ -z "$serverName" ] || [ -z "$resourceGroup" ]; then
  echo "Missing azd outputs (AZURE_SQL_* / SERVICE_WEB_NAME / AZURE_RESOURCE_GROUP). Run 'azd provision' first." >&2
  exit 1
fi

echo "Granting the app's managed identity [$appName] a database user on $server/$database ..."

# azd and az authenticate separately; make sure az targets the same subscription azd used
# (otherwise the firewall/sqlcmd calls below can hit the wrong subscription/tenant).
az account set --subscription "$AZURE_SUBSCRIPTION_ID" >/dev/null

# The server firewall only allows Azure services by default; briefly allow this machine to run T-SQL.
clientIp="$(curl -s https://api.ipify.org)"
ruleName="azd-postprovision-$(date +%s)"
az sql server firewall-rule create -g "$resourceGroup" -s "$serverName" -n "$ruleName" \
  --start-ip-address "$clientIp" --end-ip-address "$clientIp" >/dev/null

cleanup() { az sql server firewall-rule delete -g "$resourceGroup" -s "$serverName" -n "$ruleName" >/dev/null 2>&1 || true; }
trap cleanup EXIT

tsql="IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$appName')
    CREATE USER [$appName] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [$appName];
ALTER ROLE db_datawriter ADD MEMBER [$appName];
ALTER ROLE db_ddladmin  ADD MEMBER [$appName];"

# -G = Microsoft Entra auth (uses your az/azd login).
if sqlcmd -S "$server" -d "$database" -G -l 60 -Q "$tsql"; then
  echo "Done: [$appName] granted db_datareader / db_datawriter / db_ddladmin."
else
  echo "Automatic DB-user creation failed. Run docs/deploy.md section 2 once, then 'azd deploy'." >&2
  exit 1
fi
