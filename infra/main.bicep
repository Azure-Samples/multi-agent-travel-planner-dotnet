targetScope = 'resourceGroup'

@minLength(1)
@maxLength(64)
@description('Name of the azd environment. Used as a resource name prefix.')
param environmentName string

@minLength(1)
@description('Primary location for all resources.')
param location string

@description('Azure OpenAI model to deploy.')
param openAiModelName string = 'gpt-4o'

@description('Azure OpenAI model version.')
param openAiModelVersion string = '2024-08-06'

@description('Capacity (TPM in thousands) for the model deployment.')
param openAiModelCapacity int = 30

@description('Optional principal ID of the user/identity that will run the app locally. Granted Cognitive Services OpenAI User on the AOAI account so DefaultAzureCredential works without keys.')
param principalId string = ''

var abbrs = {
  appServicePlan: 'asp'
  webApp: 'app'
  appInsights: 'appi'
  logAnalytics: 'log'
  openAi: 'aoai'
}

var resourceToken = uniqueString(subscription().id, resourceGroup().id, environmentName)
var tags = {
  'azd-env-name': environmentName
  workload: 'multi-agent-travel-planner'
}

module logAnalytics 'modules/loganalytics.bicep' = {
  name: 'logAnalytics'
  params: {
    name: '${abbrs.logAnalytics}-${resourceToken}'
    location: location
    tags: tags
  }
}

module appInsights 'modules/appinsights.bicep' = {
  name: 'appInsights'
  params: {
    name: '${abbrs.appInsights}-${resourceToken}'
    location: location
    tags: tags
    workspaceId: logAnalytics.outputs.id
  }
}

module openAi 'modules/openai.bicep' = {
  name: 'openAi'
  params: {
    name: '${abbrs.openAi}-${resourceToken}'
    location: location
    tags: tags
    modelName: openAiModelName
    modelVersion: openAiModelVersion
    modelCapacity: openAiModelCapacity
  }
}

module appService 'modules/appservice.bicep' = {
  name: 'appService'
  params: {
    planName: '${abbrs.appServicePlan}-${resourceToken}'
    webAppName: '${abbrs.webApp}-${resourceToken}'
    location: location
    tags: union(tags, { 'azd-service-name': 'web' })
    appInsightsConnectionString: appInsights.outputs.connectionString
    openAiEndpoint: openAi.outputs.endpoint
    openAiDeployment: openAi.outputs.deploymentName
  }
}

module roleAssignments 'modules/roles.bicep' = {
  name: 'roleAssignments'
  params: {
    openAiAccountName: openAi.outputs.name
    webAppPrincipalId: appService.outputs.principalId
    userPrincipalId: principalId
  }
}

output AZURE_LOCATION string = location
output WEB_APP_NAME string = appService.outputs.webAppName
output WEB_APP_URI string = appService.outputs.webAppUri
output APPLICATIONINSIGHTS_CONNECTION_STRING string = appInsights.outputs.connectionString
output AZURE_OPENAI_ENDPOINT string = openAi.outputs.endpoint
output AZURE_OPENAI_DEPLOYMENT string = openAi.outputs.deploymentName
