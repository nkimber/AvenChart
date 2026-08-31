// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

targetScope = 'resourceGroup'

param location string
param resourceNamePrefix string
param containerRegistryName string
param keyVaultName string
param postgresServerName string
param containerAppsEnvironmentName string
param managedIdentityName string
param logAnalyticsWorkspaceName string
param databaseName string
param databaseAdministratorLogin string
param databasePasswordSecretName string
param databaseConnectionStringSecretName string
param deploymentPrincipalObjectId string
param deploymentPrincipalType string
@secure()
param databaseAdministratorPassword string
param postgresSkuName string
param postgresTier string
param postgresStorageGiB int
param backupRetentionDays int
param enableGeoRedundantBackup bool
param enableHighAvailability bool
param connectionPoolMaximum int
param vnetAddressPrefix string
param infrastructureSubnetPrefix string
param databaseSubnetPrefix string
param logRetentionDays int
param monthlyBudgetUsd int
param alertEmails array
param budgetStartDate string
param tags object

var networkName = '${resourceNamePrefix}-vnet'
var privateDnsZoneName = 'private.postgres.database.azure.com'
var postgresHost = '${postgresServerName}.postgres.database.azure.com'
var connectionString = 'Host=${postgresHost};Port=5432;Database=${databaseName};Username=${databaseAdministratorLogin};Password=${databaseAdministratorPassword};SSL Mode=VerifyFull;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=${connectionPoolMaximum};Connection Idle Lifetime=300;Timeout=15;Command Timeout=30'
var telehealthCallingConnectionStringSecretName = 'telehealth-internet-calling-connection-string'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  tags: tags
  properties: {
    retentionInDays: logRetentionDays
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: containerRegistryName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

resource deploymentIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
  tags: tags
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enablePurgeProtection: true
    softDeleteRetentionInDays: 90
    publicNetworkAccess: 'Enabled'
  }
}

resource databasePasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: databasePasswordSecretName
  properties: {
    value: databaseAdministratorPassword
    contentType: 'AvenChart PostgreSQL administrator credential'
  }
}

resource databaseConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: databaseConnectionStringSecretName
  properties: {
    value: connectionString
    contentType: 'AvenChart Npgsql connection string'
  }
}

// AvenChart uses this resource only for synthetic internet calling. It is not
// linked to a real patient identity, clinical record, or production workflow.
resource telehealthCommunication 'Microsoft.Communication/communicationServices@2025-05-01' = {
  name: '${resourceNamePrefix}-relay'
  location: 'global'
  tags: tags
  properties: {
    dataLocation: 'United States'
    publicNetworkAccess: 'Enabled'
  }
}

resource telehealthCallingConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: telehealthCallingConnectionStringSecretName
  properties: {
    value: telehealthCommunication.listKeys().primaryConnectionString
    contentType: 'Azure Communication Services synthetic calling connection string'
  }
}

resource network 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: networkName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [vnetAddressPrefix]
    }
    subnets: [
      {
        name: 'container-apps-infrastructure'
        properties: {
          addressPrefix: infrastructureSubnetPrefix
          delegations: [
            {
              name: 'container-apps-delegation'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        name: 'postgresql'
        properties: {
          addressPrefix: databaseSubnetPrefix
          delegations: [
            {
              name: 'postgresql-delegation'
              properties: {
                serviceName: 'Microsoft.DBforPostgreSQL/flexibleServers'
              }
            }
          ]
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

resource infrastructureSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' existing = {
  parent: network
  name: 'container-apps-infrastructure'
}

resource databaseSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' existing = {
  parent: network
  name: 'postgresql'
}

resource privateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: privateDnsZoneName
  location: 'global'
  tags: tags
}

resource privateDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: privateDnsZone
  name: '${resourceNamePrefix}-postgres-link'
  location: 'global'
  tags: tags
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: network.id
    }
  }
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: postgresServerName
  location: location
  tags: tags
  sku: {
    name: postgresSkuName
    tier: postgresTier
  }
  properties: {
    version: '17'
    administratorLogin: databaseAdministratorLogin
    administratorLoginPassword: databaseAdministratorPassword
    availabilityZone: '1'
    backup: {
      backupRetentionDays: backupRetentionDays
      geoRedundantBackup: enableGeoRedundantBackup ? 'Enabled' : 'Disabled'
    }
    highAvailability: {
      mode: enableHighAvailability ? 'ZoneRedundant' : 'Disabled'
    }
    network: {
      delegatedSubnetResourceId: databaseSubnet.id
      privateDnsZoneArmResourceId: privateDnsZone.id
      publicNetworkAccess: 'Disabled'
    }
    storage: {
      autoGrow: 'Enabled'
      storageSizeGB: postgresStorageGiB
      tier: 'P4'
    }
    authConfig: {
      activeDirectoryAuth: 'Disabled'
      passwordAuth: 'Enabled'
    }
  }
  dependsOn: [privateDnsLink]
}

resource applicationDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgres
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerAppsEnvironmentName
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
    vnetConfiguration: {
      infrastructureSubnetId: infrastructureSubnet.id
      internal: false
    }
    zoneRedundant: false
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, deploymentIdentity.id, 'acr-pull')
  scope: registry
  properties: {
    principalId: deploymentIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  }
}

resource keyVaultSecretsUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, deploymentIdentity.id, 'key-vault-secrets-user')
  scope: vault
  properties: {
    principalId: deploymentIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
  }
}

resource deploymentPrincipalSecretsOfficerAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, deploymentPrincipalObjectId, 'key-vault-secrets-officer')
  scope: vault
  properties: {
    principalId: deploymentPrincipalObjectId
    principalType: deploymentPrincipalType
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')
  }
}

resource resourceGroupBudget 'Microsoft.Consumption/budgets@2023-11-01' = if (monthlyBudgetUsd > 0 && length(alertEmails) > 0) {
  name: '${resourceNamePrefix}-monthly-budget'
  properties: {
    amount: monthlyBudgetUsd
    category: 'Cost'
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: budgetStartDate
      endDate: '2036-12-31'
    }
    notifications: {
      actual50: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 50
        thresholdType: 'Actual'
        contactEmails: alertEmails
      }
      actual80: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: alertEmails
      }
      forecast100: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: alertEmails
      }
    }
  }
}

output containerRegistryName string = registry.name
output containerRegistryLoginServer string = registry.properties.loginServer
output containerAppsEnvironmentName string = containerAppsEnvironment.name
output managedIdentityResourceId string = deploymentIdentity.id
output keyVaultName string = vault.name
output postgresHost string = postgresHost
output databaseConnectionSecretUri string = databaseConnectionSecret.properties.secretUri
