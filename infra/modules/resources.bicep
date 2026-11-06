targetScope = 'resourceGroup'

@description('Name of the azd environment.')
param environmentName string

param location string = resourceGroup().location

param tags object = {}

@description('Object (principal) id of the Entra ID user/group/app that becomes the SQL Server Entra admin.')
param principalId string

@description('Display name / UPN of the Entra ID principal set as SQL Server Entra admin.')
param principalName string

@allowed(['User', 'Group', 'Application'])
param principalType string = 'User'

var resourceSuffix = take(uniqueString(subscription().id, resourceGroup().id, environmentName, location), 6)

var storageAccountName = toLower('stagri${resourceSuffix}')
var sqlServerName = toLower('sql-agristream-${resourceSuffix}')
var sqlDatabaseName = 'appdb'
var webPlanName = 'plan-web-agristream-${resourceSuffix}'
var webAppName = 'st10507038'
var funcPlanName = 'plan-func-agristream-${resourceSuffix}'
var functionAppName = 'func-agristream-${resourceSuffix}'

// Shared secret used by the web app and function app to authenticate HTTP calls between the
// two services (both applications receive the same value as an app setting).
var internalApiKey = uniqueString(subscription().id, resourceGroup().id, environmentName, 'internal-api-key')

// ---------------------------------------------------------------------------
// Monitoring
// ---------------------------------------------------------------------------
// NOTE: Application Insights / Log Analytics are NOT deployed in this environment. The
// 'microsoft.insights' and 'Microsoft.OperationalInsights' resource providers are
// NotRegistered on this subscription, and the deployment principal's custom RBAC role
// cannot self-register providers (no .../register/action permission), so any
// Microsoft.Insights/* resource fails at deployment time with MissingSubscriptionRegistration.
// Diagnostics are instead available via the App Service / Function App's built-in Log Stream
// and Kudu console.

// ---------------------------------------------------------------------------
// Storage: Blob (drone images + function deployment package), Table (telemetry / drone image
// log), Queue (irrigation commands)
// ---------------------------------------------------------------------------
// NOTE: The deployment principal's custom RBAC role does not include
// Microsoft.Authorization/roleAssignments/write, so managed-identity-based RBAC access to
// storage cannot be granted. Shared key (account key / connection string) access is used
// instead for both the Function App runtime storage and the web app's blob/table clients.
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource droneImagesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'drone-images'
  properties: { publicAccess: 'None' }
}

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource irrigationQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  parent: queueService
  name: 'irrigation-commands'
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource telemetryTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-01-01' = {
  parent: tableService
  name: 'TelemetryData'
}

resource droneImageLogTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-01-01' = {
  parent: tableService
  name: 'DroneImageLog'
}

var storageAccountKey = storageAccount.listKeys().keys[0].value
var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccountKey};EndpointSuffix=${environment().suffixes.storage}'

// ---------------------------------------------------------------------------
// Azure SQL Database (Entra-only authentication) � Users + DeviceRegistry tables
// ---------------------------------------------------------------------------
resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: principalType
      login: principalName
      sid: principalId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
    minimalTlsVersion: '1.2'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
    // This subscription's policy requires locally-redundant (LRS) backup storage.
    requestedBackupStorageRedundancy: 'Local'
  }
}

resource sqlFirewallAzure 'Microsoft.Sql/servers/firewallRules@2022-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

var sqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabase.name};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;'

// ---------------------------------------------------------------------------
// Web App (MVC) � App Service, Linux, .NET 10
// ---------------------------------------------------------------------------
resource webPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: webPlanName
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  tags: union(tags, { 'azd-service-name': 'web' })
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: webPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'Storage__ConnectionString', value: storageConnectionString }
        { name: 'Storage__DroneImagesContainer', value: 'drone-images' }
        { name: 'Storage__TelemetryTable', value: 'TelemetryData' }
        { name: 'FunctionsApi__BaseUrl', value: 'https://${functionApp.properties.defaultHostName}' }
        { name: 'InternalApiKey', value: internalApiKey }
        { name: 'SCM_DO_BUILD_DURING_DEPLOYMENT', value: 'false' }
        // NOTE: WEBSITE_RUN_FROM_PACKAGE is intentionally NOT set for the web app — it conflicts
        // with the sync zip-deploy (`az webapp deploy --type zip`) flow used to publish this app,
        // causing the site to serve the default placeholder page instead of the deployed content.
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: sqlConnectionString
          type: 'SQLAzure'
        }
      ]
    }
  }
}

// ---------------------------------------------------------------------------
// Function App � Consumption plan, 4 functions (Blob trigger, HTTP GET, HTTP POST, Queue trigger)
// ---------------------------------------------------------------------------
// NOTE: Dynamic/Consumption SKUs (Y1, FC1) are blocked by this subscription's SKU policy, so the
// Function App runs on a Basic (B1) App Service Plan, same tier as the web app's plan.
resource funcPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: funcPlanName
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  tags: union(tags, { 'azd-service-name': 'api' })
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: funcPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|10.0'
      alwaysOn: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        { name: 'AzureWebJobsStorage', value: storageConnectionString }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'SqlConnectionString', value: sqlConnectionString }
        { name: 'InternalApiKey', value: internalApiKey }
        { name: 'SCM_DO_BUILD_DURING_DEPLOYMENT', value: 'false' }
        { name: 'WEBSITE_RUN_FROM_PACKAGE', value: '1' }
      ]
    }
  }
}

output webUrl string = 'https://${webApp.properties.defaultHostName}'
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output storageAccountName string = storageAccount.name
output webAppName string = webApp.name
output functionAppName string = functionApp.name