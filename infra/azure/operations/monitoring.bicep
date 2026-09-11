// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

// Synthetic deployment monitoring. SQL text and parameter logging are excluded.
targetScope = 'resourceGroup'

param location string
param resourceNamePrefix string
param postgresServerName string
param logAnalyticsWorkspaceName string
param applicationUrl string
param alertEmails array = []
param postgresTier string = 'Burstable'
@minValue(1)
param connectionAlertThreshold int = 28

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' existing = {
  name: postgresServerName
}
resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: logAnalyticsWorkspaceName
}
resource actions 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: '${resourceNamePrefix}-operations'
  location: 'global'
  properties: {
    groupShortName: 'AvenChart'
    enabled: true
    emailReceivers: [for (email, i) in alertEmails: {
      name: 'operator-${i}'
      emailAddress: email
      useCommonAlertSchema: true
    }]
    // Subscription owners remain the accountable fallback when no email is supplied.
    armRoleReceivers: empty(alertEmails) ? [{
      name: 'subscription-owners'
      roleId: '8e3af657-a8ff-443c-a75c-2fe8c4bcb635'
      useCommonAlertSchema: true
    }] : []
  }
}
var metricRules = concat([
  { name: 'database-cpu', metric: 'cpu_percent', aggregation: 'Average', operator: 'GreaterThan', threshold: 80, window: 'PT15M' }
  { name: 'database-connections', metric: 'active_connections', aggregation: 'Maximum', operator: 'GreaterThan', threshold: connectionAlertThreshold, window: 'PT5M' }
  { name: 'database-connection-failures', metric: 'connections_failed', aggregation: 'Total', operator: 'GreaterThan', threshold: 5, window: 'PT5M' }
], postgresTier == 'Burstable' ? [
  { name: 'database-cpu-credits', metric: 'cpu_credits_remaining', aggregation: 'Average', operator: 'LessThan', threshold: 30, window: 'PT15M' }
] : [])
resource databaseAlerts 'Microsoft.Insights/metricAlerts@2018-03-01' = [for rule in metricRules: {
  name: '${resourceNamePrefix}-${rule.name}'
  location: 'global'
  properties: {
    description: 'AvenChart database capacity warning. See the database recovery runbook.'
    severity: 2
    enabled: true
    scopes: [postgres.id]
    evaluationFrequency: 'PT1M'
    windowSize: rule.window
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [{
        name: rule.name
        metricName: rule.metric
        metricNamespace: 'Microsoft.DBforPostgreSQL/flexibleServers'
        timeAggregation: rule.aggregation
        operator: rule.operator
        threshold: rule.threshold
        criterionType: 'StaticThresholdCriterion'
      }]
    }
    actions: [{ actionGroupId: actions.id }]
  }
}]
resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'avenchart-database-operations'
  scope: postgres
  properties: {
    workspaceId: workspace.id
    logs: [
      { category: 'PostgreSQLLogs', enabled: true }
      { category: 'PostgreSQLFlexQueryStoreRuntime', enabled: true }
      { category: 'PostgreSQLFlexQueryStoreWaitStats', enabled: true }
    ]
    metrics: [{ category: 'AllMetrics', enabled: true }]
  }
  dependsOn: [safeLogging]
}
// Keep statement and bind-parameter text out of exported server error logs.
var loggingParameters = [
  { name: 'log_statement', value: 'none' }
  { name: 'log_min_duration_statement', value: '-1' }
  { name: 'log_min_error_statement', value: 'panic' }
  { name: 'log_parameter_max_length', value: '0' }
  { name: 'log_parameter_max_length_on_error', value: '0' }
]
resource safeLogging 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = [for parameter in loggingParameters: {
  parent: postgres
  name: parameter.name
  properties: { value: parameter.value, source: 'user-override' }
}]
resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${resourceNamePrefix}-availability'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    DisableLocalAuth: true
  }
}
resource readiness 'Microsoft.Insights/webtests@2022-06-15' = {
  name: '${resourceNamePrefix}-public-readiness'
  location: location
  kind: 'standard'
  tags: { 'hidden-link:${insights.id}': 'Resource' }
  properties: {
    Name: '${resourceNamePrefix}-public-readiness'
    SyntheticMonitorId: '${resourceNamePrefix}-public-readiness'
    Kind: 'standard'
    Enabled: true
    Frequency: 300
    Timeout: 30
    RetryEnabled: true
    Locations: [{ Id: 'us-va-ash-azr' }, { Id: 'us-tx-sn1-azr' }, { Id: 'us-ca-sjc-azr' }]
    Request: {
      RequestUrl: '${applicationUrl}/health/api/ready'
      HttpVerb: 'GET'
      FollowRedirects: false
      ParseDependentRequests: false
    }
    ValidationRules: {
      ExpectedHttpStatusCode: 200
      SSLCheck: true
      SSLCertRemainingLifetimeCheck: 7
      ContentValidation: {
        ContentMatch: '"status":"healthy"'
        IgnoreCase: false
        PassIfTextFound: true
      }
    }
  }
}
resource readinessAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${resourceNamePrefix}-public-readiness-failed'
  location: 'global'
  properties: {
    description: 'AvenChart public readiness failed in at least two locations.'
    severity: 1
    enabled: true
    scopes: [readiness.id, insights.id]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
      webTestId: readiness.id
      componentId: insights.id
      failedLocationCount: 2
    }
    actions: [{ actionGroupId: actions.id }]
  }
}
output actionGroupId string = actions.id
