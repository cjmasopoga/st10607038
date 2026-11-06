# Grant Azure SQL data-plane access to the App Service (web) and Function App (api) managed identities.
#
# USAGE: azd postprovision hook (see azure.yaml). Safe to re-run (idempotent).
#
# ENVIRONMENT VARIABLES (sourced from azd env):
#   SQL_SERVER           - SQL server name (without .database.windows.net)
#   SQL_DATABASE         - Database name
#   AZURE_RESOURCE_GROUP - Resource group name
#   SERVICE_WEB_NAME     - App Service (MVC web app) name
#   SERVICE_API_NAME     - Function App name
#   SQL_GRANT_DDLADMIN   - Set to "true" to also grant db_ddladmin (needed for EF Core migrations)

$ErrorActionPreference = 'Stop'

# Load azd environment variables
azd env get-values | ForEach-Object {
	$name, $value = $_.Split('=', 2)
	Set-Item "env:$name" $value.Trim('"')
}

$GrantDdlAdmin = if ($null -ne $env:SQL_GRANT_DDLADMIN) { $env:SQL_GRANT_DDLADMIN -eq 'true' } else { $true }

# Ensure the rdbms-connect extension is installed (provides 'az sql db query')
az extension show --name rdbms-connect *> $null
if ($LASTEXITCODE -ne 0) {
	Write-Host "Azure CLI extension 'rdbms-connect' is not installed. Installing..."
	az extension add --name rdbms-connect --yes
	if ($LASTEXITCODE -ne 0) {
		throw "ERROR: Failed to install Azure CLI extension 'rdbms-connect'. Cannot continue because 'az sql db query' requires it."
	}
}

function Grant-SqlAccess {
	param([string]$AppName)

	if ([string]::IsNullOrWhiteSpace($AppName)) { return }

	Write-Host "Granting SQL data-plane access to managed identity: $AppName"

	$SqlQuery = @"
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = '$AppName')
  CREATE USER [$AppName] FROM EXTERNAL PROVIDER;

IF NOT EXISTS (
  SELECT 1 FROM sys.database_role_members drm
  JOIN sys.database_principals r ON drm.role_principal_id = r.principal_id
  JOIN sys.database_principals m ON drm.member_principal_id = m.principal_id
  WHERE r.name = 'db_datareader' AND m.name = '$AppName'
)
  ALTER ROLE db_datareader ADD MEMBER [$AppName];

IF NOT EXISTS (
  SELECT 1 FROM sys.database_role_members drm
  JOIN sys.database_principals r ON drm.role_principal_id = r.principal_id
  JOIN sys.database_principals m ON drm.member_principal_id = m.principal_id
  WHERE r.name = 'db_datawriter' AND m.name = '$AppName'
)
  ALTER ROLE db_datawriter ADD MEMBER [$AppName];
"@

	if ($GrantDdlAdmin) {
		$SqlQuery += @"

IF NOT EXISTS (
  SELECT 1 FROM sys.database_role_members drm
  JOIN sys.database_principals r ON drm.role_principal_id = r.principal_id
  JOIN sys.database_principals m ON drm.member_principal_id = m.principal_id
  WHERE r.name = 'db_ddladmin' AND m.name = '$AppName'
)
  ALTER ROLE db_ddladmin ADD MEMBER [$AppName];
"@
	}

	az sql db query `
	  --server $env:SQL_SERVER `
	  --database $env:SQL_DATABASE `
	  --resource-group $env:AZURE_RESOURCE_GROUP `
	  --auth-mode ActiveDirectoryDefault `
	  --queries $SqlQuery
}

Grant-SqlAccess -AppName $env:SERVICE_WEB_NAME
Grant-SqlAccess -AppName $env:SERVICE_API_NAME

Write-Host "SQL access granted successfully."
