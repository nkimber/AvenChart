// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

targetScope = 'resourceGroup'

param location string
param containerAppName string
param containerAppsEnvironmentName string
param managedIdentityResourceId string
param containerRegistryLoginServer string
param apiImage string
param uiImage string
param keyVaultName string
param databaseConnectionStringSecretName string = 'avenchart-database-connection-string'
@allowed([0, 1])
param minimumReplicas int = 1
@minValue(1)
@maxValue(10)
param maximumReplicas int = 2
@minValue(1)
@maxValue(1000)
param httpConcurrency int = 20
@allowed(['0.25', '0.5', '0.75', '1', '1.25', '1.5', '1.75', '2'])
param apiCpu string = '0.5'
param apiMemory string = '1Gi'
@allowed(['0.25', '0.5', '0.75', '1'])
param uiCpu string = '0.25'
param uiMemory string = '0.5Gi'
param rateLimitPermitLimit int = 300
param tags object = {}

resource managedEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: containerAppsEnvironmentName
}

resource application 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityResourceId}': {}
    }
  }
  properties: {
    environmentId: managedEnvironment.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Multiple'
      ingress: {
        external: true
        allowInsecure: false
        targetPort: 8080
        transport: 'http'
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      registries: [
        {
          server: containerRegistryLoginServer
          identity: managedIdentityResourceId
        }
      ]
      secrets: [
        {
          name: 'database-connection-string'
          keyVaultUrl: 'https://${keyVaultName}${environment().suffixes.keyvaultDns}/secrets/${databaseConnectionStringSecretName}'
          identity: managedIdentityResourceId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'ui'
          image: '${containerRegistryLoginServer}/${uiImage}'
          resources: {
            cpu: json(uiCpu)
            memory: uiMemory
          }
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 2
              timeoutSeconds: 2
              failureThreshold: 30
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 20
              timeoutSeconds: 3
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/api/ready'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 10
              timeoutSeconds: 5
              failureThreshold: 12
            }
          ]
        }
        {
          name: 'api'
          image: '${containerRegistryLoginServer}/${apiImage}'
          resources: {
            cpu: json(apiCpu)
            memory: apiMemory
          }
          env: [
            // This Container App is a synthetic-only staging deployment. Declare
            // that explicitly so the API applies its staging safety policy rather
            // than the production-only host and data-protection requirements.
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Staging'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8081'
            }
            {
              name: 'ConnectionStrings__AvenChart'
              secretRef: 'database-connection-string'
            }
            {
              name: 'DatabaseSchema__MigrationsPath'
              value: '/app/database/migrations'
            }
            {
              name: 'RuntimeSafety__RequireHttps'
              value: 'false'
            }
            {
              name: 'RuntimeSafety__RateLimitPermitLimit'
              value: string(rateLimitPermitLimit)
            }
            {
              name: 'DEMO_SEED_ON_STARTUP'
              value: 'false'
            }
            {
              name: 'DEMO_RESET_ON_STARTUP'
              value: 'false'
            }
          ]
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/health/live'
                port: 8081
                scheme: 'HTTP'
              }
              periodSeconds: 2
              timeoutSeconds: 2
              failureThreshold: 60
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8081
                scheme: 'HTTP'
              }
              periodSeconds: 20
              timeoutSeconds: 3
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8081
                scheme: 'HTTP'
              }
              periodSeconds: 10
              timeoutSeconds: 5
              failureThreshold: 12
            }
          ]
        }
      ]
      scale: {
        minReplicas: minimumReplicas
        maxReplicas: maximumReplicas
        rules: [
          {
            name: 'http-concurrency'
            http: {
              metadata: {
                concurrentRequests: string(httpConcurrency)
              }
            }
          }
        ]
      }
    }
  }
}

output applicationName string = application.name
output applicationFqdn string = application.properties.configuration.ingress.fqdn
