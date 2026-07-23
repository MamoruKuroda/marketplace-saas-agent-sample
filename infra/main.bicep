// Subscription-scoped entry point for `azd up`.
// Creates the resource group, then provisions the app resources in it.
// Automated equivalent of docs/deploy.md (App Service + Azure SQL, passwordless).
targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the azd environment; used to derive resource names and the resource-group name.')
param environmentName string

@minLength(1)
@description('Azure region for all resources. Defaults to West US 3 (used for prior integration testing in this sample).')
param location string = 'westus3'

@description('Object ID of the deployer (azd sets AZURE_PRINCIPAL_ID). Becomes the Azure SQL Entra admin so the postprovision hook can create the app\'s DB user.')
param principalId string

@description('Display name/login for the Azure SQL Entra admin (the deployer).')
param principalName string = 'azd-deployer'

@allowed([ 'User', 'Group', 'ServicePrincipal' ])
@description('Type of the deployer principal (User for an interactive azd login).')
param principalType string = 'User'

@description('Require Microsoft Entra buyer sign-in for the landing/admin pages. Default false = quick, touchable demo (no Entra app registration needed). Set true for a production-shaped deploy and supply landingClientId.')
param requireAuthentication bool = false

@description('Landing app (multitenant) client id. Only used when requireAuthentication is true.')
param landingClientId string = ''

@description('Expected JWT audience for the connection webhook (publisher app client id). Optional for the demo.')
param webhookAudience string = ''

// A short, deterministic token to keep globally-unique names (SQL server, web app) stable per environment.
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = { 'azd-env-name': environmentName }

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module resources './resources.bicep' = {
  name: 'resources'
  scope: rg
  params: {
    location: location
    resourceToken: resourceToken
    tags: tags
    principalId: principalId
    principalName: principalName
    principalType: principalType
    requireAuthentication: requireAuthentication
    landingClientId: landingClientId
    webhookAudience: webhookAudience
  }
}

// Consumed by scripts/postprovision.* (azd surfaces outputs as environment variables).
output AZURE_LOCATION string = location
output AZURE_RESOURCE_GROUP string = rg.name
output SERVICE_WEB_NAME string = resources.outputs.webAppName
output SERVICE_WEB_URI string = resources.outputs.webAppUri
output SERVICE_EMULATOR_NAME string = resources.outputs.emulatorName
output SERVICE_EMULATOR_URI string = resources.outputs.emulatorUri
output AZURE_SQL_SERVER_NAME string = resources.outputs.sqlServerName
output AZURE_SQL_SERVER_FQDN string = resources.outputs.sqlServerFqdn
output AZURE_SQL_DATABASE_NAME string = resources.outputs.sqlDatabaseName
