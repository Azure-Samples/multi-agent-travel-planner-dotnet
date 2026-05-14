param planName string
param webAppName string
param location string
param tags object = {}

param appInsightsConnectionString string
param openAiEndpoint string
param openAiDeployment string

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: planName
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: 'P0v3'
    tier: 'Premium0V3'
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: webAppName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'AzureOpenAI__Endpoint'
          value: openAiEndpoint
        }
        {
          name: 'AzureOpenAI__Deployment'
          value: openAiDeployment
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        // Auto-instrumentation is performed in code via the Azure Monitor
        // OpenTelemetry distro. Disable the App Service codeless attach so
        // the two don't fight over the same activity sources.
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: 'disabled'
        }
      ]
    }
  }
}

output webAppName string = webApp.name
output webAppUri string = 'https://${webApp.properties.defaultHostName}'
output principalId string = webApp.identity.principalId
