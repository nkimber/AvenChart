// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

targetScope = 'subscription'

@description('Azure region used by all regional AvenChart resources.')
param location string
param resourceGroupName string
param resourceNamePrefix string
param containerRegistryName string
param keyVaultName string
param postgresServerName string
param containerAppsEnvironmentName string
param managedIdentityName string
param logAnalyticsWorkspaceName string
param databaseName string = 'avenchart'
param databaseAdministratorLogin string = 'avenchartadmin'
param databasePasswordSecretName string = 'avenchart-database-administrator-password'
param databaseConnectionStringSecretName string = 'avenchart-database-connection-string'
param deploymentPrincipalObjectId string
@allowed(['User', 'ServicePrincipal'])
param deploymentPrincipalType string
@secure()
param databaseAdministratorPassword string
param postgresSkuName string = 'Standard_B1ms'
param postgresTier string = 'Burstable'
@minValue(32)
param postgresStorageGiB int = 32
@minValue(7)
@maxValue(35)
param backupRetentionDays int = 7
param enableGeoRedundantBackup bool = false
param enableHighAvailability bool = false
@minValue(1)
@maxValue(100)
param connectionPoolMaximum int = 15
param vnetAddressPrefix string = '10.42.0.0/16'
param infrastructureSubnetPrefix string = '10.42.0.0/23'
param databaseSubnetPrefix string = '10.42.2.0/28'
@minValue(30)
@maxValue(730)
param logRetentionDays int = 30
@minValue(1)
param monthlyBudgetUsd int = 150
param alertEmails array = []
param tags object = {}
param budgetStartDate string = utcNow('yyyy-MM-01')

resource deploymentResourceGroup 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: resourceGroupName
  location: location
  tags: union(tags, {
    application: 'AvenChart'
    managedBy: 'AvenChart Azure Deployment Operations'
    dataClassification: 'synthetic-only'
  })
}

module platform './modules/platform.bicep' = {
  name: '${resourceNamePrefix}-platform'
  scope: deploymentResourceGroup
  params: {
    location: location
    resourceNamePrefix: resourceNamePrefix
    containerRegistryName: containerRegistryName
    keyVaultName: keyVaultName
    postgresServerName: postgresServerName
    containerAppsEnvironmentName: containerAppsEnvironmentName
    managedIdentityName: managedIdentityName
    logAnalyticsWorkspaceName: logAnalyticsWorkspaceName
    databaseName: databaseName
    databaseAdministratorLogin: databaseAdministratorLogin
    databasePasswordSecretName: databasePasswordSecretName
    databaseConnectionStringSecretName: databaseConnectionStringSecretName
    deploymentPrincipalObjectId: deploymentPrincipalObjectId
    deploymentPrincipalType: deploymentPrincipalType
    databaseAdministratorPassword: databaseAdministratorPassword
    postgresSkuName: postgresSkuName
    postgresTier: postgresTier
    postgresStorageGiB: postgresStorageGiB
    backupRetentionDays: backupRetentionDays
    enableGeoRedundantBackup: enableGeoRedundantBackup
    enableHighAvailability: enableHighAvailability
    connectionPoolMaximum: connectionPoolMaximum
    vnetAddressPrefix: vnetAddressPrefix
    infrastructureSubnetPrefix: infrastructureSubnetPrefix
    databaseSubnetPrefix: databaseSubnetPrefix
    logRetentionDays: logRetentionDays
    monthlyBudgetUsd: monthlyBudgetUsd
    alertEmails: alertEmails
    budgetStartDate: budgetStartDate
    tags: tags
  }
}

output resourceGroupName string = deploymentResourceGroup.name
output containerRegistryName string = platform.outputs.containerRegistryName
output containerRegistryLoginServer string = platform.outputs.containerRegistryLoginServer
output containerAppsEnvironmentName string = platform.outputs.containerAppsEnvironmentName
output managedIdentityResourceId string = platform.outputs.managedIdentityResourceId
output keyVaultName string = platform.outputs.keyVaultName
output postgresHost string = platform.outputs.postgresHost
