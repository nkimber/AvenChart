// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

targetScope = 'resourceGroup'

param location string
param migrationJobName string
param containerAppsEnvironmentName string
param managedIdentityResourceId string
param containerRegistryLoginServer string
param apiImage string
param keyVaultName string
param databasePasswordSecretName string = 'avenchart-database-administrator-password'
param databaseConnectionStringSecretName string = 'avenchart-database-connection-string'
param postgresHost string
param databaseName string = 'avenchart'
param databaseAdministratorLogin string = 'avenchartadmin'
param enableDemoSeed bool = true
param tags object = {}

resource managedEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: containerAppsEnvironmentName
}

resource migrationJob 'Microsoft.App/jobs@2024-03-01' = {
  name: migrationJobName
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
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      replicaRetryLimit: 1
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
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
        {
          name: 'database-administrator-password'
          keyVaultUrl: 'https://${keyVaultName}${environment().suffixes.keyvaultDns}/secrets/${databasePasswordSecretName}'
          identity: managedIdentityResourceId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'schema-migrator'
          image: '${containerRegistryLoginServer}/${apiImage}'
          args: ['--migrate-only']
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ConnectionStrings__AvenChart'
              secretRef: 'database-connection-string'
            }
            {
              name: 'DatabaseSchema__MigrationsPath'
              value: '/app/database/migrations'
            }
            {
              name: 'DEMO_SEED_ON_STARTUP'
              value: 'false'
            }
          ]
        }
      ]
    }
  }
}

resource seedJob 'Microsoft.App/jobs@2024-03-01' = if (enableDemoSeed) {
  name: '${migrationJobName}-seed'
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
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      replicaRetryLimit: 0
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: [
        {
          server: containerRegistryLoginServer
          identity: managedIdentityResourceId
        }
      ]
      secrets: [
        {
          name: 'database-administrator-password'
          keyVaultUrl: 'https://${keyVaultName}${environment().suffixes.keyvaultDns}/secrets/${databasePasswordSecretName}'
          identity: managedIdentityResourceId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'synthetic-seed'
          image: '${containerRegistryLoginServer}/${apiImage}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'POSTGRES_HOST', value: postgresHost }
            { name: 'POSTGRES_PORT', value: '5432' }
            { name: 'POSTGRES_DB', value: databaseName }
            { name: 'POSTGRES_USER', value: databaseAdministratorLogin }
            { name: 'POSTGRES_PASSWORD', secretRef: 'database-administrator-password' }
            { name: 'DEMO_SEED_ON_STARTUP', value: 'true' }
            { name: 'DEMO_RESET_ON_STARTUP', value: 'false' }
            { name: 'DEMO_SEED_ONLY', value: 'true' }
          ]
        }
      ]
    }
  }
}

output migrationJobName string = migrationJob.name
output seedJobName string = enableDemoSeed ? seedJob!.name : ''
