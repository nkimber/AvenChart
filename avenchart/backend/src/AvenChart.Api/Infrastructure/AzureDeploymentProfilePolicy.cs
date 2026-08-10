// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using AvenChart.Api.Models;

namespace AvenChart.Api.Infrastructure;

public static partial class AzureDeploymentProfilePolicy
{
    public const string EnvironmentBoundary =
        "Azure deployment is restricted to synthetic demo, development, and test workloads. Production clinical use is blocked.";

    public static readonly IReadOnlyList<string> RequiredProviders =
    [
        "Microsoft.App",
        "Microsoft.ContainerRegistry",
        "Microsoft.DBforPostgreSQL",
        "Microsoft.KeyVault",
        "Microsoft.ManagedIdentity",
        "Microsoft.Network",
        "Microsoft.OperationalInsights",
        "Microsoft.Consumption"
    ];

    public static readonly IReadOnlyList<string> ProductionBlockers =
    [
        "The active local staff identity adapter is not approved for production.",
        "Portal and service identities do not use approved production provider adapters.",
        "Production artifact storage, malware scanning, retention, legal hold, and recovery policy are not approved.",
        "Clinical, privacy, security, accessibility, interoperability, and regulatory acceptance are incomplete.",
        "The repository explicitly permits synthetic data only."
    ];

    public static AzureDeploymentProfileAssessment Assess(AzureDeploymentProfileDocument document)
    {
        var issues = new List<AzureDeploymentValidationIssue>();
        var environmentKind = Normalize(document.EnvironmentKind);
        if (environmentKind is not ("demo" or "development" or "test"))
        {
            issues.Add(Error(nameof(document.EnvironmentKind), "production-blocked",
                "Only demo, development, and test Azure deployments are permitted."));
        }

        if (!document.AcknowledgedSyntheticOnly)
        {
            issues.Add(Error(nameof(document.AcknowledgedSyntheticOnly), "synthetic-acknowledgement-required",
                "Confirm that the deployment contains synthetic data only."));
        }

        RequireGuid(document.TenantId, nameof(document.TenantId), issues);
        RequireGuid(document.SubscriptionId, nameof(document.SubscriptionId), issues);
        RequireMatch(document.Location, nameof(document.Location), AzureLocationPattern(),
            "Use a valid lower-case Azure region identifier.", issues);
        RequireMatch(document.ResourceGroupName, nameof(document.ResourceGroupName), ResourceGroupPattern(),
            "Resource group names may contain letters, numbers, periods, underscores, parentheses, and hyphens.", issues);
        RequireMatch(document.ResourceNamePrefix, nameof(document.ResourceNamePrefix), PrefixPattern(),
            "Use 3-18 lower-case letters, numbers, or hyphens for the resource prefix.", issues);
        RequireMatch(document.ContainerRegistryName, nameof(document.ContainerRegistryName), RegistryPattern(),
            "Container registry names must contain 5-50 lower-case letters or numbers.", issues);
        RequireMatch(document.KeyVaultName, nameof(document.KeyVaultName), KeyVaultPattern(),
            "Key Vault names must contain 3-24 letters, numbers, or hyphens and start with a letter.", issues);
        RequireMatch(document.PostgresServerName, nameof(document.PostgresServerName), DnsResourcePattern(),
            "PostgreSQL server names must use lower-case letters, numbers, or hyphens.", issues);

        foreach (var (value, field) in new[]
        {
            (document.ContainerAppsEnvironmentName, nameof(document.ContainerAppsEnvironmentName)),
            (document.ManagedIdentityName, nameof(document.ManagedIdentityName)),
            (document.LogAnalyticsWorkspaceName, nameof(document.LogAnalyticsWorkspaceName)),
            (document.ContainerAppName, nameof(document.ContainerAppName)),
            (document.MigrationJobName, nameof(document.MigrationJobName))
        })
        {
            RequireMatch(value, field, AzureResourceNamePattern(),
                "Use lower-case letters, numbers, or single hyphens and begin with a letter.", issues);
        }

        if (document.MigrationJobName?.Length > 27)
            issues.Add(Error(nameof(document.MigrationJobName), "migration-job-name-length",
                "Migration job names must be 27 characters or fewer so the optional seed-job suffix remains within Azure's 32-character limit."));

        RequireMatch(document.DatabaseName, nameof(document.DatabaseName), DatabaseIdentifierPattern(),
            "Database names must begin with a letter and contain letters, numbers, or underscores.", issues);
        RequireMatch(document.DatabaseAdministratorLogin, nameof(document.DatabaseAdministratorLogin), DatabaseIdentifierPattern(),
            "Database administrator names must begin with a letter and contain letters, numbers, or underscores.", issues);
        RequireMatch(document.DatabasePasswordSecretName, nameof(document.DatabasePasswordSecretName), SecretNamePattern(),
            "Key Vault secret names may contain letters, numbers, or hyphens.", issues);

        if (document.ExpectedNamedUsers is < 1 or > 500)
            issues.Add(Error(nameof(document.ExpectedNamedUsers), "named-users-range", "Expected named users must be between 1 and 500."));
        if (document.ExpectedConcurrentUsers is < 1 or > 250 || document.ExpectedConcurrentUsers > document.ExpectedNamedUsers)
            issues.Add(Error(nameof(document.ExpectedConcurrentUsers), "concurrent-users-range", "Concurrent users must be between 1 and the named-user count."));
        if (document.MinimumReplicas is < 0 or > 1)
            issues.Add(Error(nameof(document.MinimumReplicas), "minimum-replicas-range", "The small Consumption profile permits zero or one minimum replica."));
        if (document.MaximumReplicas is < 1 or > 10 || document.MaximumReplicas < document.MinimumReplicas)
            issues.Add(Error(nameof(document.MaximumReplicas), "maximum-replicas-range", "Maximum replicas must be 1-10 and not below minimum replicas."));
        if (document.HttpConcurrency is < 1 or > 1000)
            issues.Add(Error(nameof(document.HttpConcurrency), "http-concurrency-range", "HTTP concurrency must be between 1 and 1,000."));

        ValidateCpuMemory(document.ApiCpu, document.ApiMemoryGiB, nameof(document.ApiCpu), nameof(document.ApiMemoryGiB), issues);
        ValidateCpuMemory(document.UiCpu, document.UiMemoryGiB, nameof(document.UiCpu), nameof(document.UiMemoryGiB), issues);

        var connectionLimit = DatabaseUserConnectionLimit(document.PostgresSkuName);
        var potentialConnections = document.ConnectionPoolMaximum * document.MaximumReplicas;
        if (document.ConnectionPoolMaximum is < 1 or > 100)
            issues.Add(Error(nameof(document.ConnectionPoolMaximum), "pool-range", "Maximum pool size must be between 1 and 100 per API replica."));
        if (potentialConnections > connectionLimit)
            issues.Add(Error(nameof(document.ConnectionPoolMaximum), "pool-exceeds-database",
                $"The configured replicas can request {potentialConnections} pooled connections, but {document.PostgresSkuName} exposes approximately {connectionLimit} user connections."));
        else if (potentialConnections > Math.Floor(connectionLimit * 0.85m))
            issues.Add(Warning(nameof(document.ConnectionPoolMaximum), "pool-headroom-low",
                "Reserve at least 15% of database connections for migrations, diagnostics, and operator access."));
        if (document.ConnectionPoolMaximum > Math.Max(15, document.ExpectedConcurrentUsers * 2))
            issues.Add(Warning(nameof(document.ConnectionPoolMaximum), "pool-larger-than-workload",
                "The connection pool is large relative to expected concurrency; load-test before retaining this value."));

        if (document.PostgresStorageGiB is < 32 or > 65536)
            issues.Add(Error(nameof(document.PostgresStorageGiB), "storage-range", "PostgreSQL storage must be between 32 GiB and 64 TiB."));
        if (document.EnableHighAvailability && Normalize(document.PostgresTier) == "burstable")
            issues.Add(Error(nameof(document.EnableHighAvailability), "high-availability-tier",
                "PostgreSQL high availability requires a General Purpose or Memory Optimized compute tier."));
        if (document.BackupRetentionDays is < 7 or > 35)
            issues.Add(Error(nameof(document.BackupRetentionDays), "backup-retention-range", "Backup retention must be between 7 and 35 days."));
        if (document.LogRetentionDays is < 30 or > 730)
            issues.Add(Error(nameof(document.LogRetentionDays), "log-retention-range", "Log retention must be between 30 and 730 days."));
        if (document.MonthlyBudgetUsd is < 1 or > 1_000_000)
            issues.Add(Error(nameof(document.MonthlyBudgetUsd), "budget-range", "Monthly budget must be a positive whole-dollar amount."));
        if (document.RateLimitPermitLimit is < 20 or > 10000)
            issues.Add(Error(nameof(document.RateLimitPermitLimit), "rate-limit-range", "Rate-limit permits must be between 20 and 10,000 per minute."));

        var vnet = ValidateCidr(document.VnetAddressPrefix, nameof(document.VnetAddressPrefix), 8, 29, issues);
        var infrastructureSubnet = ValidateCidr(
            document.InfrastructureSubnetPrefix,
            nameof(document.InfrastructureSubnetPrefix),
            8,
            23,
            issues,
            "The Container Apps infrastructure subnet must be a valid IPv4 CIDR prefix between /8 and /23.");
        var databaseSubnet = ValidateCidr(
            document.DatabaseSubnetPrefix,
            nameof(document.DatabaseSubnetPrefix),
            8,
            28,
            issues,
            "The PostgreSQL delegated subnet must be a valid IPv4 CIDR prefix between /8 and /28.");
        if (vnet is not null && infrastructureSubnet is not null && !Contains(vnet.Value, infrastructureSubnet.Value))
            issues.Add(Error(nameof(document.InfrastructureSubnetPrefix), "subnet-outside-vnet",
                "The Container Apps infrastructure subnet must be contained by the deployment virtual network."));
        if (vnet is not null && databaseSubnet is not null && !Contains(vnet.Value, databaseSubnet.Value))
            issues.Add(Error(nameof(document.DatabaseSubnetPrefix), "subnet-outside-vnet",
                "The PostgreSQL delegated subnet must be contained by the deployment virtual network."));
        if (infrastructureSubnet is not null && databaseSubnet is not null && Overlaps(infrastructureSubnet.Value, databaseSubnet.Value))
            issues.Add(Error(nameof(document.DatabaseSubnetPrefix), "subnets-overlap",
                "The Container Apps and PostgreSQL delegated subnets must not overlap."));

        if (document.EnableDemoReset)
            issues.Add(Error(nameof(document.EnableDemoReset), "demo-reset-prohibited", "Automatic demo reset is prohibited in persistent Azure environments."));
        if (!string.IsNullOrWhiteSpace(document.CustomDomain))
            issues.Add(Warning(nameof(document.CustomDomain), "custom-domain-dns-required",
                "The custom domain is stored, but DNS ownership and managed-certificate issuance must succeed before traffic can use it."));
        var allowedIpRanges = document.AllowedIpRanges ?? [];
        if (allowedIpRanges.Count > 0)
            issues.Add(Warning(nameof(document.AllowedIpRanges), "ip-restrictions-review",
                "Review allowed IP ranges whenever office or VPN egress addresses change."));
        var alertEmails = document.AlertEmails ?? [];
        if (alertEmails.Count == 0)
            issues.Add(Warning(nameof(document.AlertEmails), "alert-recipient-missing", "Add at least one cost and operational alert recipient."));
        foreach (var email in alertEmails)
        {
            if (string.IsNullOrWhiteSpace(email) || !EmailPattern().IsMatch(email.Trim()))
                issues.Add(Error(nameof(document.AlertEmails), "invalid-alert-email", $"'{email}' is not a valid alert email address."));
        }

        if (string.IsNullOrWhiteSpace(document.Owner))
            issues.Add(Error(nameof(document.Owner), "owner-required", "An accountable deployment owner is required."));
        if (string.IsNullOrWhiteSpace(document.SourceRevision))
            issues.Add(Warning(nameof(document.SourceRevision), "source-revision-recommended", "Pin deployments to an immutable source revision or image tag."));

        var hasErrors = issues.Any(issue => issue.Severity == "error");
        return new AzureDeploymentProfileAssessment(
            Valid: !hasErrors,
            DeploymentReady: !hasErrors && environmentKind is "demo" or "development" or "test",
            MaximumPotentialDatabaseConnections: potentialConnections,
            DatabaseUserConnectionLimit: connectionLimit,
            CostPosture: CostPosture(document),
            Issues: issues,
            ProductionBlockers: ProductionBlockers,
            PlannedResources:
            [
                "Resource group",
                "Virtual network with delegated Container Apps and PostgreSQL subnets",
                "Azure Container Registry Basic",
                "Key Vault Standard with purge protection",
                "User-assigned managed identity and least-privilege role assignments",
                "Log Analytics workspace",
                $"PostgreSQL Flexible Server {document.PostgresSkuName} with {document.PostgresStorageGiB} GiB storage",
                "Azure Container Apps Consumption environment",
                "One-shot schema migration job",
                "Multi-container AvenChart UI and API application",
                "Monthly Azure cost budget"
            ],
            PricingCalculatorUrl: "https://azure.microsoft.com/pricing/calculator/");
    }

    public static int DatabaseUserConnectionLimit(string skuName) => Normalize(skuName) switch
    {
        "standard_b1ms" => 35,
        "standard_b2s" => 414,
        "standard_b2ms" => 844,
        "standard_b4ms" => 1703,
        _ => 400
    };

    private static string CostPosture(AzureDeploymentProfileDocument document)
    {
        var extras = document.EnableHighAvailability || document.EnableGeoRedundantBackup || document.MaximumReplicas > 2;
        return extras ? "elevated-resilience" : Normalize(document.PostgresTier) == "burstable" ? "small-non-production" : "small-predictable";
    }

    private static void ValidateCpuMemory(decimal cpu, decimal memory, string cpuField, string memoryField, List<AzureDeploymentValidationIssue> issues)
    {
        if (cpu is < 0.25m or > 2m || cpu * 4 != decimal.Truncate(cpu * 4))
            issues.Add(Error(cpuField, "cpu-allocation", "CPU must use supported 0.25-vCPU increments between 0.25 and 2."));
        if (memory is < 0.5m or > 4m || memory * 2 != decimal.Truncate(memory * 2))
            issues.Add(Error(memoryField, "memory-allocation", "Memory must use supported 0.5-GiB increments between 0.5 and 4 GiB."));
        if (memory < cpu * 2 || memory > cpu * 4)
            issues.Add(Error(memoryField, "cpu-memory-ratio", "Consumption memory must be between two and four GiB per vCPU."));
    }

    private static CidrRange? ValidateCidr(
        string? value,
        string field,
        int minimumPrefix,
        int maximumPrefix,
        List<AzureDeploymentValidationIssue> issues,
        string? message = null)
    {
        var parts = (value ?? string.Empty).Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !IPAddress.TryParse(parts[0], out var address) ||
            address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var prefix) ||
            prefix < minimumPrefix || prefix > maximumPrefix)
        {
            issues.Add(Error(field, "invalid-cidr", message ??
                $"Use a valid IPv4 CIDR prefix between /{minimumPrefix} and /{maximumPrefix}."));
            return null;
        }

        var rawAddress = AddressValue(address);
        var mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        if ((rawAddress & mask) != rawAddress)
        {
            issues.Add(Error(field, "cidr-host-bits", "Use the network address for the CIDR prefix; host bits must be zero."));
            return null;
        }
        return new(rawAddress, prefix);
    }

    private static bool Contains(CidrRange parent, CidrRange child) =>
        parent.PrefixLength <= child.PrefixLength &&
        (child.Network & parent.Mask) == parent.Network;

    private static bool Overlaps(CidrRange left, CidrRange right) =>
        (left.Network & right.Mask) == right.Network ||
        (right.Network & left.Mask) == left.Network;

    private static uint AddressValue(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    private static void RequireGuid(string value, string field, List<AzureDeploymentValidationIssue> issues)
    {
        if (!Guid.TryParse(value, out _)) issues.Add(Error(field, "invalid-guid", $"{field} must be an Azure GUID."));
    }

    private static void RequireMatch(string value, string field, Regex regex, string message, List<AzureDeploymentValidationIssue> issues)
    {
        if (!regex.IsMatch(value?.Trim() ?? string.Empty)) issues.Add(Error(field, "invalid-name", message));
    }

    private static AzureDeploymentValidationIssue Error(string field, string code, string message) => new(field, code, "error", message);
    private static AzureDeploymentValidationIssue Warning(string field, string code, string message) => new(field, code, "warning", message);
    private static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    private readonly record struct CidrRange(uint Network, int PrefixLength)
    {
        public uint Mask => PrefixLength == 0 ? 0u : uint.MaxValue << (32 - PrefixLength);
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")] private static partial Regex AzureLocationPattern();
    [GeneratedRegex("^[A-Za-z0-9._()\\-]{1,90}$")] private static partial Regex ResourceGroupPattern();
    [GeneratedRegex("^[a-z][a-z0-9-]{1,16}[a-z0-9]$")] private static partial Regex PrefixPattern();
    [GeneratedRegex("^[a-z0-9]{5,50}$")] private static partial Regex RegistryPattern();
    [GeneratedRegex("^[a-z][a-z0-9-]{1,22}[a-z0-9]$", RegexOptions.IgnoreCase)] private static partial Regex KeyVaultPattern();
    [GeneratedRegex("^[a-z][a-z0-9-]{1,61}[a-z0-9]$")] private static partial Regex DnsResourcePattern();
    [GeneratedRegex("^[a-z][a-z0-9-]{1,58}[a-z0-9]$")] private static partial Regex AzureResourceNamePattern();
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,62}$")] private static partial Regex DatabaseIdentifierPattern();
    [GeneratedRegex("^[A-Za-z0-9-]{1,127}$")] private static partial Regex SecretNamePattern();
    [GeneratedRegex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$")] private static partial Regex EmailPattern();
}
