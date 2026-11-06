// NOTE: This environment's Entra account only has a custom role scoped to a single
// pre-existing resource group (no Microsoft.Resources/subscriptions/resourceGroups/write
// permission), so this template deploys directly at resource-group scope instead of
// creating a new resource group at subscription scope.
targetScope = 'resourceGroup'

@minLength(1)
@maxLength(64)
@description('Name of the environment, used to derive a deterministic resource name suffix.')
param environmentName string

@minLength(1)
@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Object (principal) id of the Entra ID user/group/app that becomes the SQL Server Entra admin.')
param principalId string

@description('Display name / UPN of the Entra ID principal set as SQL Server Entra admin.')
param principalName string

@allowed(['User', 'Group', 'Application'])
param principalType string = 'User'

var tags = {
  'azd-env-name': environmentName
  project: 'agristream-iot'
}

module resources './modules/resources.bicep' = {
  name: 'resources'
  params: {
    environmentName: environmentName
    location: location
    tags: tags
    principalId: principalId
    principalName: principalName
    principalType: principalType
  }
}

output AZURE_RESOURCE_GROUP string = resourceGroup().name
output WEB_URL string = resources.outputs.webUrl
output FUNCTION_APP_URL string = resources.outputs.functionAppUrl
output SQL_SERVER string = resources.outputs.sqlServerName
output SQL_DATABASE string = resources.outputs.sqlDatabaseName
output STORAGE_ACCOUNT_NAME string = resources.outputs.storageAccountName
output SERVICE_WEB_NAME string = resources.outputs.webAppName
output SERVICE_API_NAME string = resources.outputs.functionAppName