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

@description('Container image for the emulator. azd builds its Dockerfile in ACR and injects the image on deploy; the placeholder runs until then.')
param emulatorImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

var sqlDatabaseName = 'SaasAgentSample'
var webAppName = 'app-${resourceToken}'
var emulatorName = 'emu-${resourceToken}'
var sqlServerName = 'sql-${resourceToken}'
var acrName = 'acr${resourceToken}'
var logAnalyticsName = 'log-${resourceToken}'
var acaEnvName = 'aca-${resourceToken}'
// Built-in AcrPull role.
var acrPullRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')

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
        // Demo: point the app at the deployed emulator (Microsoft's stand-in) so the flow is
        // interactive. The emulator sends unsigned webhook tokens, so signature enforcement is off.
        { name: 'Fulfillment__BaseUrl', value: 'https://${emulator.properties.configuration.ingress.fqdn}/api' }
        { name: 'Fulfillment__ApiVersion', value: '2018-08-31' }
        { name: 'Fulfillment__Webhook__Audience', value: webhookAudience }
        { name: 'Fulfillment__Webhook__ExpectedAppId', value: marketplaceAppId }
        { name: 'Fulfillment__Webhook__MetadataAddress', value: '${environment().authentication.loginEndpoint}common/v2.0/.well-known/openid-configuration' }
        { name: 'Fulfillment__Webhook__RequireSignedToken', value: 'false' }
      ]
    }
  }
}

// --- Fulfillment API Emulator: built in Azure (ACR) and run on Container Apps ---
// Microsoft's token-free marketplace stand-in. azd builds its Dockerfile remotely in ACR
// (docker.remoteBuild) and deploys the image here — no local Docker or npm is needed, and
// dependencies are baked into the image (avoids App Service Node packaging pitfalls).

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  #disable-next-line BCP334 // resourceToken (uniqueString) is always 13 chars, so acrName is well over the 5-char minimum
  name: acrName
  location: location
  tags: tags
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
  }
}

resource emulatorIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-emu-${resourceToken}'
  location: location
  tags: tags
}

// Let the emulator's identity pull images from the registry.
resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, emulatorIdentity.id, 'AcrPull')
  scope: containerRegistry
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: emulatorIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acaEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: acaEnvName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource emulator 'Microsoft.App/containerApps@2024-03-01' = {
  name: emulatorName
  location: location
  // azd matches this tag to the service named "emulator" in azure.yaml.
  tags: union(tags, { 'azd-service-name': 'emulator' })
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${emulatorIdentity.id}': {} }
  }
  properties: {
    managedEnvironmentId: acaEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 80
        transport: 'auto'
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          identity: emulatorIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'emulator'
          image: emulatorImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            // The emulator POSTs connection webhooks to the app, and its "Continue" button
            // sends the buyer to the app's landing page (carrying the purchase token).
            { name: 'WEBHOOK_URL', value: 'https://${webAppName}.azurewebsites.net/api/webhook' }
            { name: 'LANDING_PAGE_URL', value: 'https://${webAppName}.azurewebsites.net/' }
            { name: 'PUBLISHER_ID', value: 'FourthCoffee' }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 1 }
    }
  }
  dependsOn: [ acrPull ]
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
output emulatorName string = emulator.name
output emulatorUri string = 'https://${emulator.properties.configuration.ingress.fqdn}'
output containerRegistryEndpoint string = containerRegistry.properties.loginServer
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
