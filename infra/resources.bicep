// App resources for the cloud demo (resource-group scope).
// App Service (.NET 10) + Azure SQL (Entra-only), connected passwordless via the
// app's system-assigned managed identity. Mirrors docs/deploy.md.

@description('Azure region for all resources.')
param location string

@description('Deterministic token for globally-unique resource names.')
param resourceToken string

@description('Tags applied to all resources (includes azd-env-name).')
param tags object

@description('Object ID of the deployer; set as the Azure SQL Entra admin.')
param principalId string

@description('Display name/login for the Azure SQL Entra admin.')
param principalName string

@allowed([ 'User', 'Group', 'ServicePrincipal' ])
param principalType string

@description('Require Entra buyer sign-in (false = quick demo).')
param requireAuthentication bool

@description('Landing app client id (used only when requireAuthentication is true).')
param landingClientId string

@description('Expected webhook JWT audience (optional for the demo).')
param webhookAudience string

var sqlDatabaseName = 'SaasAgentSample'
var webAppName = 'app-${resourceToken}'
var sqlServerName = 'sql-${resourceToken}'

// Public Microsoft Commercial Marketplace app id — a documented constant, not a secret.
var marketplaceAppId = '20e940b3-4c07-4bc1-a733-45f7c7a3d0e3'

resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'plan-${resourceToken}'
  location: location
  tags: tags
  sku: {
    name: 'B1'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: webAppName
  location: location
  // azd matches this tag to the service named "web" in azure.yaml.
  tags: union(tags, { 'azd-service-name': 'web' })
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'SCM_DO_BUILD_DURING_DEPLOYMENT', value: 'false' }
        { name: 'Database__Provider', value: 'SqlServer' }
        // Passwordless: the app authenticates to Azure SQL with its managed identity.
        { name: 'Database__ConnectionString', value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;' }
        { name: 'Landing__RequireAuthentication', value: toLower(string(requireAuthentication)) }
        { name: 'AzureAd__Instance', value: environment().authentication.loginEndpoint }
        { name: 'AzureAd__TenantId', value: 'common' }
        { name: 'AzureAd__ClientId', value: landingClientId }
        { name: 'Fulfillment__BaseUrl', value: 'https://marketplaceapi.microsoft.com/api' }
        { name: 'Fulfillment__ApiVersion', value: '2018-08-31' }
        { name: 'Fulfillment__Webhook__Audience', value: webhookAudience }
        { name: 'Fulfillment__Webhook__ExpectedAppId', value: marketplaceAppId }
        { name: 'Fulfillment__Webhook__MetadataAddress', value: '${environment().authentication.loginEndpoint}common/v2.0/.well-known/openid-configuration' }
        { name: 'Fulfillment__Webhook__RequireSignedToken', value: 'true' }
      ]
    }
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    // Entra-only: no SQL admin password exists to manage. See docs/deploy.md.
    administrators: {
      administratorType: 'ActiveDirectory'
      login: principalName
      sid: principalId
      tenantId: subscription().tenantId
      principalType: principalType
      azureADOnlyAuthentication: true
    }
  }

  resource allowAzure 'firewallRules@2023-08-01-preview' = {
    name: 'AllowAllAzureServices'
    properties: {
      startIpAddress: '0.0.0.0'
      endIpAddress: '0.0.0.0'
    }
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: tags
  sku: {
    name: 'S0'
    tier: 'Standard'
  }
}

output webAppName string = webApp.name
output webAppUri string = 'https://${webApp.properties.defaultHostName}'
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
