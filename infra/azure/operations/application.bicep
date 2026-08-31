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
param telehealthInternetCallingPocEnabled bool = false
param telehealthInternetCallingConnectionStringSecretName string = 'telehealth-internet-calling-connection-string'
// Pass true only when upgrading an active revision that still references the
// Phase-1 network-traversal secret. It avoids a destructive secret removal
// during the same revision transition and is not exposed to the new code.
param preserveLegacyTelehealthWebRtcRelaySecret bool = false
param legacyTelehealthWebRtcRelayConnectionStringSecretName string = 'telehealth-internet-webrtc-relay-connection-string'
param telehealthBrandedHost string = ''
param customDomainName string = ''
param customDomainCertificateId string = ''
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
        customDomains: !empty(customDomainName) && !empty(customDomainCertificateId) ? [
          {
            name: customDomainName
            bindingType: 'SniEnabled'
            certificateId: customDomainCertificateId
          }
        ] : []
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
        ...(preserveLegacyTelehealthWebRtcRelaySecret ? [
          {
            name: 'telehealth-internet-webrtc-relay-connection-string'
            keyVaultUrl: 'https://${keyVaultName}${environment().suffixes.keyvaultDns}/secrets/${legacyTelehealthWebRtcRelayConnectionStringSecretName}'
            identity: managedIdentityResourceId
          }
        ] : [])
        ...(telehealthInternetCallingPocEnabled ? [
          {
            name: 'telehealth-internet-calling-connection-string'
            keyVaultUrl: 'https://${keyVaultName}${environment().suffixes.keyvaultDns}/secrets/${telehealthInternetCallingConnectionStringSecretName}'
            identity: managedIdentityResourceId
          }
        ] : [])
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
            ...(telehealthInternetCallingPocEnabled ? [
              {
                name: 'Telehealth__Enabled'
                value: 'true'
              }
              {
                name: 'Telehealth__Mode'
                value: 'Synthetic'
              }
              {
                name: 'Telehealth__PracticeId'
                value: 'avenchart-synthetic-practice'
              }
              {
                name: 'Telehealth__FacilityId'
                value: '10'
              }
              {
                name: 'Telehealth__BrandedHosts__0'
                value: telehealthBrandedHost
              }
              {
                name: 'Telehealth__SupportedStates__0'
                value: 'GA'
              }
              {
                name: 'Telehealth__SupportedStates__1'
                value: 'CA'
              }
              {
                name: 'Telehealth__SupportedStates__2'
                value: 'FL'
              }
              {
                name: 'Telehealth__ReservationLeaseSeconds'
                value: '300'
              }
              {
                name: 'Telehealth__VideoAdapterMode'
                value: 'NON_PRODUCTION'
              }
              {
                name: 'Telehealth__PharmacyDirectoryAdapterMode'
                value: 'NON_PRODUCTION'
              }
              {
                name: 'Telehealth__ProfessionalClaimAdapterMode'
                value: 'NON_PRODUCTION'
              }
              {
                name: 'Telehealth__LocalWebRtcPocEnabled'
                value: 'false'
              }
              {
                name: 'Telehealth__InternetCallingPocEnabled'
                value: 'true'
              }
              {
                name: 'Telehealth__InternetCallingConnectionString'
                secretRef: 'telehealth-internet-calling-connection-string'
              }
            ] : [])
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
        // The synthetic calling authorization service is deliberately limited
        // to one replica until a reviewed, durable participant-session design
        // exists for multi-replica operation.
        minReplicas: telehealthInternetCallingPocEnabled ? 1 : minimumReplicas
        maxReplicas: telehealthInternetCallingPocEnabled ? 1 : maximumReplicas
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
