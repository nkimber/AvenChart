// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using AvenChart.Api.Configuration;
using AvenChart.Api.Data;
using AvenChart.Api.Models;

namespace AvenChart.Api.Infrastructure;

public sealed record AzureCliResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

public sealed class AzureCliRunner(IOptions<AzureOperationsOptions> options, ILogger<AzureCliRunner> logger)
{
    private readonly AzureOperationsOptions _options = options.Value;

    public async Task<AzureCliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken, bool sensitiveOutput = false)
    {
        var executable = ResolveAzureCliPath(_options.AzureCliPath);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(_options.CommandTimeoutMinutes));
        using var process = new Process { StartInfo = CreateStartInfo(executable, arguments), EnableRaisingEvents = true };
        logger.LogInformation("Running Azure CLI command {AzureCommand}", SafeCommandName(arguments));
        try
        {
            if (!process.Start()) return new(1, string.Empty, "Azure CLI process could not be started.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (!sensitiveOutput && process.ExitCode != 0)
                logger.LogWarning("Azure CLI command {AzureCommand} failed with exit code {ExitCode}: {Error}", SafeCommandName(arguments), process.ExitCode, Redact(stderr));
            return new(process.ExitCode, stdout.Trim(), Redact(stderr.Trim()));
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new(127, string.Empty, $"Azure CLI could not be started: {exception.Message}");
        }
    }

    private static ProcessStartInfo CreateStartInfo(string executable, IReadOnlyList<string> arguments)
    {
        if (OperatingSystem.IsWindows() && (executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)))
        {
            var python = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(executable)!, "..", "python.exe"));
            if (!File.Exists(python)) throw new InvalidOperationException("The Azure CLI Python runtime could not be located next to az.cmd.");
            var info = BaseStartInfo(python);
            info.ArgumentList.Add("-I");
            info.ArgumentList.Add("-X");
            info.ArgumentList.Add("utf8");
            info.ArgumentList.Add("-B");
            info.ArgumentList.Add("-m");
            info.ArgumentList.Add("azure.cli");
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            return info;
        }

        var startInfo = BaseStartInfo(executable);
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        return startInfo;
    }

    private static ProcessStartInfo BaseStartInfo(string executable)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        startInfo.Environment["PYTHONUTF8"] = "1";
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        return startInfo;
    }

    private static string ResolveAzureCliPath(string configured)
    {
        if (!string.Equals(configured, "az", StringComparison.OrdinalIgnoreCase) || !OperatingSystem.IsWindows()) return configured;
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(directory, "az.cmd");
            if (File.Exists(candidate)) return candidate;
        }
        return "az.cmd";
    }

    private static string SafeCommandName(IReadOnlyList<string> arguments) => string.Join(' ', arguments.Take(4).Select(value => value.StartsWith('@') ? "@<parameters>" : value));
    private static string Redact(string value) => value.Replace("databaseAdministratorPassword", "<secure-parameter>", StringComparison.OrdinalIgnoreCase);
    private static void TryKill(Process process) { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { } }
}

public sealed class AzureOperationsService(
    AzureCliRunner cli,
    IHttpClientFactory httpClientFactory,
    IOptions<AzureOperationsOptions> options)
{
    private readonly AzureOperationsOptions _options = options.Value;

    public async Task<AzureOperationsCapabilityResponse> GetCapabilitiesAsync(CancellationToken cancellationToken)
    {
        var version = await cli.RunAsync(["version", "--output", "json"], cancellationToken);
        if (!version.Succeeded)
            return Capability(false, false, null, null, null, version.StandardError);
        var account = await cli.RunAsync(["account", "show", "--output", "json"], cancellationToken);
        if (!account.Succeeded)
            return Capability(true, false, null, null, null, "Azure CLI is installed but no active account is authenticated.");
        using var json = JsonDocument.Parse(account.StandardOutput);
        var root = json.RootElement;
        var identity = root.TryGetProperty("user", out var user) && user.TryGetProperty("name", out var name) ? name.GetString() : null;
        var tenant = root.TryGetProperty("tenantId", out var tenantId) ? tenantId.GetString() : null;
        var subscription = root.TryGetProperty("id", out var subscriptionId) ? subscriptionId.GetString() : null;
        var versionText = ParseCliVersion(version.StandardOutput);
        return new(_options.Enabled, _options.Enabled && _options.AllowPlanExecution, _options.Enabled && _options.AllowDeploymentExecution,
            true, versionText, true, identity, tenant, subscription, AzureDeploymentProfilePolicy.EnvironmentBoundary,
            AzureDeploymentProfilePolicy.RequiredProviders, AzureDeploymentProfilePolicy.ProductionBlockers);
    }

    public async Task<AzureAccessValidationResponse> ValidateAccessAsync(AzureDeploymentProfileDocument profile, CancellationToken cancellationToken)
    {
        var checks = new List<AzureAccessValidationCheck>();
        var version = await cli.RunAsync(["version", "--output", "json"], cancellationToken);
        checks.Add(new("Azure CLI", version.Succeeded ? "passed" : "failed", version.Succeeded ? $"Azure CLI {ParseCliVersion(version.StandardOutput)} is available." : version.StandardError));
        if (!version.Succeeded) return new(false, DateTimeOffset.UtcNow, checks);
        var account = await cli.RunAsync(["account", "show", "--subscription", profile.SubscriptionId, "--output", "json"], cancellationToken);
        checks.Add(new("Subscription access", account.Succeeded ? "passed" : "failed", account.Succeeded ? "The configured subscription is accessible." : account.StandardError));
        if (!account.Succeeded) return new(false, DateTimeOffset.UtcNow, checks);
        string? principalObjectId = null;
        using (var accountJson = JsonDocument.Parse(account.StandardOutput))
        {
            var tenantId = accountJson.RootElement.GetProperty("tenantId").GetString();
            checks.Add(new("Tenant match", string.Equals(tenantId, profile.TenantId, StringComparison.OrdinalIgnoreCase) ? "passed" : "failed",
                string.Equals(tenantId, profile.TenantId, StringComparison.OrdinalIgnoreCase) ? "The subscription belongs to the configured tenant." : "The configured tenant does not own the selected subscription."));
            var user = accountJson.RootElement.GetProperty("user");
            var principalName = user.GetProperty("name").GetString();
            var principalType = user.GetProperty("type").GetString();
            if (!string.IsNullOrWhiteSpace(principalName))
            {
                var principal = string.Equals(principalType, "servicePrincipal", StringComparison.OrdinalIgnoreCase)
                    ? await cli.RunAsync(["ad", "sp", "show", "--id", principalName, "--query", "id", "--output", "tsv"], cancellationToken)
                    : await cli.RunAsync(["ad", "signed-in-user", "show", "--query", "id", "--output", "tsv"], cancellationToken);
                if (principal.Succeeded && !string.IsNullOrWhiteSpace(principal.StandardOutput))
                {
                    principalObjectId = principal.StandardOutput;
                    checks.Add(new("Deployment principal", "passed", "The signed-in principal can be resolved for deployment role assignments."));
                }
                else
                {
                    checks.Add(new("Deployment principal", "failed", "The signed-in principal could not be resolved in Microsoft Entra ID."));
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(principalObjectId))
        {
            var roleScope = $"/subscriptions/{profile.SubscriptionId}";
            var roles = await cli.RunAsync(["role", "assignment", "list", "--subscription", profile.SubscriptionId, "--assignee-object-id", principalObjectId,
                "--scope", roleScope, "--include-inherited", "--query", "[].roleDefinitionName", "--output", "json"], cancellationToken);
            var roleNames = ParseStringArray(roles.StandardOutput);
            var hasOwner = roleNames.Contains("Owner", StringComparer.OrdinalIgnoreCase);
            var hasContributor = roleNames.Contains("Contributor", StringComparer.OrdinalIgnoreCase);
            var hasRoleAdministrator = roleNames.Any(role => role is not null && (role.Equals("User Access Administrator", StringComparison.OrdinalIgnoreCase)
                || role.Equals("Role Based Access Control Administrator", StringComparison.OrdinalIgnoreCase)));
            var roleStatus = roles.Succeeded && (hasOwner || hasContributor && hasRoleAdministrator) ? "passed" : "warning";
            var roleMessage = !roles.Succeeded
                ? "Role assignments could not be listed; confirm resource-creation and role-assignment permissions manually."
                : hasOwner ? "Owner is assigned at or above the subscription scope."
                : hasContributor && hasRoleAdministrator ? "Contributor and role-administration permissions are assigned at or above the subscription scope."
                : $"Observed subscription roles: {(roleNames.Count == 0 ? "none" : string.Join(", ", roleNames))}. A custom role may still be sufficient; confirm resource and role-assignment actions.";
            checks.Add(new("Subscription roles", roleStatus, roleMessage));
        }
        foreach (var provider in AzureDeploymentProfilePolicy.RequiredProviders)
        {
            var result = await cli.RunAsync(["provider", "show", "--namespace", provider, "--subscription", profile.SubscriptionId, "--query", "registrationState", "--output", "tsv"], cancellationToken);
            var registered = result.Succeeded && string.Equals(result.StandardOutput, "Registered", StringComparison.OrdinalIgnoreCase);
            checks.Add(new($"Provider {provider}", registered ? "passed" : "warning", registered ? "Registered." : "Not registered; deployment will attempt registration and requires permission."));
        }
        var assessment = AzureDeploymentProfilePolicy.Assess(profile);
        checks.Add(new("Deployment profile", assessment.DeploymentReady ? "passed" : "failed", assessment.DeploymentReady ? "The non-secret deployment profile passes local policy." : "Resolve profile validation errors before deployment."));
        return new(!checks.Any(check => check.Status == "failed"), DateTimeOffset.UtcNow, checks);
    }

    public async Task<AzureDeploymentHealthResponse> GetHealthAsync(AzureDeploymentProfileDocument profile, CancellationToken cancellationToken)
    {
        var show = await cli.RunAsync(["containerapp", "show", "--subscription", profile.SubscriptionId, "--resource-group", profile.ResourceGroupName,
            "--name", profile.ContainerAppName, "--output", "json"], cancellationToken);
        if (!show.Succeeded)
            return new(false, null, null, null, "unknown", "unknown", "unknown", DateTimeOffset.UtcNow, ["The Azure Container App could not be found or queried."]);
        using var document = JsonDocument.Parse(show.StandardOutput);
        var properties = document.RootElement.GetProperty("properties");
        var fqdn = properties.GetProperty("configuration").GetProperty("ingress").GetProperty("fqdn").GetString();
        var revision = properties.TryGetProperty("latestRevisionName", out var revisionElement) ? revisionElement.GetString() : null;
        var healthState = properties.TryGetProperty("runningStatus", out var statusElement) ? statusElement.GetString() : null;
        var url = string.IsNullOrWhiteSpace(fqdn) ? null : $"https://{fqdn}";
        if (url is null) return new(true, null, revision, healthState, "unknown", "unknown", "unknown", DateTimeOffset.UtcNow, ["Container App ingress has no FQDN."]);
        var client = httpClientFactory.CreateClient("azure-deployment-health");
        var ui = await ProbeAsync(client, $"{url}/health", cancellationToken);
        var live = await ProbeAsync(client, $"{url}/health/api/live", cancellationToken);
        var ready = await ProbeAsync(client, $"{url}/health/api/ready", cancellationToken);
        var messages = new List<string>();
        if (ui != "healthy") messages.Add("UI health probe did not succeed.");
        if (live != "healthy") messages.Add("API liveness probe did not succeed.");
        if (ready != "healthy") messages.Add("API readiness or database schema validation did not succeed.");
        if (!string.IsNullOrWhiteSpace(profile.CustomDomain)) messages.Add("Custom-domain DNS and certificate state require separate ownership validation.");
        return new(true, url, revision, healthState, ui, live, ready, DateTimeOffset.UtcNow, messages);
    }

    private AzureOperationsCapabilityResponse Capability(bool cliAvailable, bool authenticated, string? identity, string? tenant, string? subscription, string versionOrMessage) =>
        new(_options.Enabled, _options.Enabled && _options.AllowPlanExecution, _options.Enabled && _options.AllowDeploymentExecution,
            cliAvailable, cliAvailable ? ParseCliVersion(versionOrMessage) : versionOrMessage, authenticated, identity, tenant, subscription,
            AzureDeploymentProfilePolicy.EnvironmentBoundary, AzureDeploymentProfilePolicy.RequiredProviders, AzureDeploymentProfilePolicy.ProductionBlockers);

    private static string ParseCliVersion(string json)
    {
        try { using var document = JsonDocument.Parse(json); return document.RootElement.GetProperty("azure-cli").GetString() ?? "unknown"; }
        catch { return json.Length > 120 ? json[..120] : json; }
    }

    private static IReadOnlyList<string> ParseStringArray(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString()!).ToArray()
                : [];
        }
        catch { return []; }
    }

    private static async Task<string> ProbeAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        try { using var response = await client.GetAsync(url, cancellationToken); return response.IsSuccessStatusCode ? "healthy" : $"http-{(int)response.StatusCode}"; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return "timeout"; }
        catch (HttpRequestException) { return "unreachable"; }
    }
}

public sealed class AzureDeploymentCoordinator(
    IServiceScopeFactory scopeFactory,
    AzureCliRunner cli,
    IOptions<AzureOperationsOptions> options,
    ILogger<AzureDeploymentCoordinator> logger) : IHostedService, IDisposable
{
    private readonly AzureOperationsOptions _options = options.Value;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _operations = new();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var recovered = await WithRepository(repository => repository.FailInterruptedExecutionsAsync(cancellationToken));
            if (recovered > 0)
                logger.LogWarning("Marked {ExecutionCount} interrupted Azure operations as failed after operator-host startup.", recovered);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Azure operation recovery could not run. Apply the current database migrations before using deployment operations.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var operation in _operations.Values) operation.Cancel();
        return Task.CompletedTask;
    }

    public void Queue(Guid executionId)
    {
        var cancellation = new CancellationTokenSource();
        if (!_operations.TryAdd(executionId, cancellation)) return;
        _ = Task.Run(() => ExecuteAsync(executionId, cancellation.Token));
    }

    public void Cancel(Guid executionId)
    {
        if (_operations.TryGetValue(executionId, out var cancellation)) cancellation.Cancel();
    }

    private async Task ExecuteAsync(Guid executionId, CancellationToken cancellationToken)
    {
        string phase = "starting";
        try
        {
            var workItem = await WithRepository(repository => repository.GetExecutionWorkItemAsync(executionId, cancellationToken));
            await WithRepository(repository => repository.StartExecutionAsync(executionId, phase, cancellationToken));
            EnsureAllowed(_options.Enabled, "Azure deployment operations are disabled on this host.");
            switch (workItem.Kind)
            {
                case "plan":
                    EnsureAllowed(_options.AllowPlanExecution, "Azure what-if execution is disabled by host policy.");
                    await PlanAsync(workItem, cancellationToken);
                    break;
                case "deploy":
                    EnsureAllowed(_options.AllowDeploymentExecution, "Azure deployment execution is disabled. Set AzureOperations__AllowDeploymentExecution=true on the operator host.");
                    await DeployAsync(workItem, cancellationToken);
                    break;
                case "rollback":
                    EnsureAllowed(_options.AllowDeploymentExecution, "Azure rollback execution is disabled by host policy.");
                    await RollbackAsync(workItem, cancellationToken);
                    break;
                case "verify":
                    await VerifyAsync(workItem, cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown Azure operation '{workItem.Kind}'.");
            }
        }
        catch (OperationCanceledException)
        {
            await SafeRepository(repository => repository.MarkExecutionCancelledAsync(executionId, phase, CancellationToken.None));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Azure deployment operation {ExecutionId} failed during {Phase}", executionId, phase);
            await SafeRepository(repository => repository.FailExecutionAsync(executionId, phase, SafeError(exception), CancellationToken.None));
        }
        finally
        {
            if (_operations.TryRemove(executionId, out var cancellation)) cancellation.Dispose();
        }

        async Task PlanAsync(AzureDeploymentExecutionWorkItem item, CancellationToken token)
        {
            phase = "azure-access";
            await SetPhase(executionId, phase, "Selecting the configured Azure subscription.", token);
            await RequireSuccess(["account", "set", "--subscription", item.Document.SubscriptionId], token);
            var principal = await ResolveDeploymentPrincipalAsync(item.Document.SubscriptionId, token);
            var password = await ResolveDatabasePasswordAsync(item.Document, token);
            var root = ResolveRepositoryRoot();
            using var parameters = await WriteParameterFileAsync(PlatformParameters(item.Document, password, principal.ObjectId, principal.PrincipalType), token);
            phase = "what-if-platform";
            await SetPhase(executionId, phase, "Running Azure Resource Manager what-if for the platform.", token);
            var result = await RequireSuccess(["deployment", "sub", "what-if", "--subscription", item.Document.SubscriptionId,
                "--location", item.Document.Location, "--name", DeploymentName(item, "plan"), "--template-file", Path.Combine(root, "infra", "azure", "operations", "main.bicep"),
                "--parameters", $"@{parameters.Path}", "--result-format", "ResourceIdOnly", "--no-pretty-print", "--output", "json", "--only-show-errors"], token);
            var summary = SummarizeWhatIf(result.StandardOutput);
            await WithRepository(repository => repository.CompleteExecutionAsync(executionId, summary, null, DeploymentName(item, "plan"), token));
        }

        async Task DeployAsync(AzureDeploymentExecutionWorkItem item, CancellationToken token)
        {
            var profile = item.Document;
            phase = "azure-access";
            await SetPhase(executionId, phase, "Selecting the Azure subscription and registering required resource providers.", token);
            await RequireSuccess(["account", "set", "--subscription", profile.SubscriptionId], token);
            foreach (var provider in AzureDeploymentProfilePolicy.RequiredProviders)
                await RequireSuccess(["provider", "register", "--subscription", profile.SubscriptionId, "--namespace", provider, "--wait", "--only-show-errors"], token);
            var principal = await ResolveDeploymentPrincipalAsync(profile.SubscriptionId, token);
            var password = await ResolveDatabasePasswordAsync(profile, token);
            var root = ResolveRepositoryRoot();
            using (var parameters = await WriteParameterFileAsync(PlatformParameters(profile, password, principal.ObjectId, principal.PrincipalType), token))
            {
                phase = "deploy-platform";
                await SetPhase(executionId, phase, "Creating or updating the Azure platform and private PostgreSQL network.", token);
                await RequireSuccess(["deployment", "sub", "create", "--subscription", profile.SubscriptionId, "--location", profile.Location,
                    "--name", DeploymentName(item, "platform"), "--template-file", Path.Combine(root, "infra", "azure", "operations", "main.bicep"),
                    "--parameters", $"@{parameters.Path}", "--output", "none", "--only-show-errors"], token);
            }

            phase = "build-api-image";
            await SetPhase(executionId, phase, $"Building {profile.ApiImage} in Azure Container Registry.", token);
            await RequireSuccess(["acr", "build", "--subscription", profile.SubscriptionId, "--registry", profile.ContainerRegistryName,
                "--image", profile.ApiImage, "--file", "infra/azure/demo/avenchart-api-demo.Dockerfile", root, "--only-show-errors", "--output", "none"], token);
            phase = "build-ui-image";
            await SetPhase(executionId, phase, $"Building {profile.UiImage} in Azure Container Registry.", token);
            await RequireSuccess(["acr", "build", "--subscription", profile.SubscriptionId, "--registry", profile.ContainerRegistryName,
                "--image", profile.UiImage, "--file", "infra/azure/demo/avenchart-ui-demo.Dockerfile", "--build-arg", "AVENCHART_BASE_URL=http://127.0.0.1:8081",
                root, "--only-show-errors", "--output", "none"], token);

            using (var parameters = await WriteParameterFileAsync(MigrationParameters(profile), token))
            {
                phase = "deploy-migration-jobs";
                await SetPhase(executionId, phase, "Creating the governed synthetic seed and schema migration jobs.", token);
                await RequireSuccess(["deployment", "group", "create", "--subscription", profile.SubscriptionId, "--resource-group", profile.ResourceGroupName,
                    "--name", DeploymentName(item, "migration"), "--template-file", Path.Combine(root, "infra", "azure", "operations", "migration.bicep"),
                    "--parameters", $"@{parameters.Path}", "--output", "none", "--only-show-errors"], token);
            }

            if (profile.EnableDemoSeed)
            {
                phase = "seed-synthetic-data";
                await SetPhase(executionId, phase, "Seeding the deterministic synthetic dataset without resetting an existing dataset.", token);
                await StartAndWaitForJobAsync(profile, $"{profile.MigrationJobName}-seed", token);
            }
            phase = "migrate-database";
            await SetPhase(executionId, phase, "Applying versioned database migrations before application traffic is enabled.", token);
            await StartAndWaitForJobAsync(profile, profile.MigrationJobName, token);

            using (var parameters = await WriteParameterFileAsync(ApplicationParameters(profile), token))
            {
                phase = "deploy-application";
                await SetPhase(executionId, phase, "Creating a new multi-container AvenChart application revision.", token);
                await RequireSuccess(["deployment", "group", "create", "--subscription", profile.SubscriptionId, "--resource-group", profile.ResourceGroupName,
                    "--name", DeploymentName(item, "application"), "--template-file", Path.Combine(root, "infra", "azure", "operations", "application.bicep"),
                    "--parameters", $"@{parameters.Path}", "--output", "none", "--only-show-errors"], token);
            }

            phase = "verify-health";
            await SetPhase(executionId, phase, "Checking UI, API liveness, and API readiness through public ingress.", token);
            var health = await GetHealthInScope(profile, token);
            if (health.UiHealth != "healthy" || health.ApiLiveness != "healthy" || health.ApiReadiness != "healthy")
                throw new InvalidOperationException($"Post-deployment verification failed: UI={health.UiHealth}, API live={health.ApiLiveness}, API ready={health.ApiReadiness}.");
            var summary = "Azure platform, images, synthetic seed, schema migrations, application revision, and health verification completed successfully.";
            if (!string.IsNullOrWhiteSpace(profile.CustomDomain)) summary += " Custom-domain DNS and certificate activation remain pending validation.";
            await WithRepository(repository => repository.CompleteExecutionAsync(executionId, summary, health.ApplicationUrl, DeploymentName(item, "application"), token));
        }

        async Task RollbackAsync(AzureDeploymentExecutionWorkItem item, CancellationToken token)
        {
            var profile = item.Document;
            phase = "select-rollback-revision";
            await SetPhase(executionId, phase, "Selecting the previous healthy active Container Apps revision.", token);
            var revisions = await RequireSuccess(["containerapp", "revision", "list", "--subscription", profile.SubscriptionId, "--resource-group", profile.ResourceGroupName,
                "--name", profile.ContainerAppName, "--output", "json", "--only-show-errors"], token);
            var target = SelectPreviousRevision(revisions.StandardOutput);
            phase = "shift-traffic";
            await SetPhase(executionId, phase, $"Shifting all application traffic to revision {target}.", token);
            await RequireSuccess(["containerapp", "ingress", "traffic", "set", "--subscription", profile.SubscriptionId, "--resource-group", profile.ResourceGroupName,
                "--name", profile.ContainerAppName, "--revision-weight", $"{target}=100", "--only-show-errors", "--output", "none"], token);
            var health = await GetHealthInScope(profile, token);
            if (health.ApiReadiness != "healthy") throw new InvalidOperationException("The rollback revision did not pass API readiness verification.");
            await WithRepository(repository => repository.CompleteExecutionAsync(executionId, $"Traffic shifted to {target} and readiness passed.", health.ApplicationUrl, target, token));
        }

        async Task VerifyAsync(AzureDeploymentExecutionWorkItem item, CancellationToken token)
        {
            phase = "verify-health";
            await SetPhase(executionId, phase, "Checking the deployed revision and all health endpoints.", token);
            var health = await GetHealthInScope(item.Document, token);
            var healthy = health.Deployed && health.UiHealth == "healthy" && health.ApiLiveness == "healthy" && health.ApiReadiness == "healthy";
            if (!healthy) throw new InvalidOperationException($"Deployment health is not ready: UI={health.UiHealth}, API live={health.ApiLiveness}, API ready={health.ApiReadiness}.");
            await WithRepository(repository => repository.CompleteExecutionAsync(executionId, "All deployment health checks passed.", health.ApplicationUrl, health.RevisionName, token));
        }
    }

    private async Task<AzureCliResult> RequireSuccess(IReadOnlyList<string> arguments, CancellationToken cancellationToken, bool sensitiveOutput = false)
    {
        var result = await cli.RunAsync(arguments, cancellationToken, sensitiveOutput);
        if (!result.Succeeded) throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError) ? "Azure CLI command failed." : result.StandardError);
        return result;
    }

    private async Task StartAndWaitForJobAsync(AzureDeploymentProfileDocument profile, string jobName, CancellationToken cancellationToken)
    {
        var start = await RequireSuccess(["containerapp", "job", "start", "--subscription", profile.SubscriptionId, "--resource-group", profile.ResourceGroupName,
            "--name", jobName, "--output", "json", "--only-show-errors"], cancellationToken);
        var executionName = TryReadString(start.StandardOutput, "name");
        var deadline = DateTimeOffset.UtcNow.AddMinutes(_options.MigrationTimeoutMinutes);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var list = await RequireSuccess(["containerapp", "job", "execution", "list", "--subscription", profile.SubscriptionId, "--resource-group", profile.ResourceGroupName,
                "--name", jobName, "--output", "json", "--only-show-errors"], cancellationToken);
            var status = ReadJobStatus(list.StandardOutput, executionName);
            if (string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase)) return;
            if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Stopped", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Container Apps job {jobName} completed with status {status}.");
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
        throw new TimeoutException($"Container Apps job {jobName} did not complete within {_options.MigrationTimeoutMinutes} minutes.");
    }

    private async Task<(string ObjectId, string PrincipalType)> ResolveDeploymentPrincipalAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        var account = await RequireSuccess(["account", "show", "--subscription", subscriptionId, "--output", "json"], cancellationToken);
        using var document = JsonDocument.Parse(account.StandardOutput);
        var user = document.RootElement.GetProperty("user");
        var name = user.GetProperty("name").GetString() ?? throw new InvalidOperationException("Azure account has no principal name.");
        var type = user.GetProperty("type").GetString();
        if (string.Equals(type, "servicePrincipal", StringComparison.OrdinalIgnoreCase))
        {
            var servicePrincipal = await RequireSuccess(["ad", "sp", "show", "--id", name, "--query", "id", "--output", "tsv"], cancellationToken);
            return (servicePrincipal.StandardOutput, "ServicePrincipal");
        }
        var signedInUser = await RequireSuccess(["ad", "signed-in-user", "show", "--query", "id", "--output", "tsv"], cancellationToken);
        return (signedInUser.StandardOutput, "User");
    }

    private async Task<string> ResolveDatabasePasswordAsync(AzureDeploymentProfileDocument profile, CancellationToken cancellationToken)
    {
        var vault = await cli.RunAsync(["keyvault", "show", "--subscription", profile.SubscriptionId, "--name", profile.KeyVaultName, "--output", "none", "--only-show-errors"], cancellationToken);
        if (vault.Succeeded)
        {
            var secret = await cli.RunAsync(["keyvault", "secret", "show", "--subscription", profile.SubscriptionId, "--vault-name", profile.KeyVaultName,
                "--name", profile.DatabasePasswordSecretName, "--query", "value", "--output", "tsv", "--only-show-errors"], cancellationToken, sensitiveOutput: true);
            if (!secret.Succeeded || string.IsNullOrWhiteSpace(secret.StandardOutput))
                throw new InvalidOperationException("The existing Key Vault credential could not be read. Grant the deployment operator Key Vault Secrets Officer access before redeploying.");
            return secret.StandardOutput;
        }
        return GeneratePassword();
    }

    private Dictionary<string, object?> PlatformParameters(AzureDeploymentProfileDocument p, string password, string principalObjectId, string principalType) => new()
    {
        ["location"] = p.Location, ["resourceGroupName"] = p.ResourceGroupName, ["resourceNamePrefix"] = p.ResourceNamePrefix,
        ["containerRegistryName"] = p.ContainerRegistryName, ["keyVaultName"] = p.KeyVaultName, ["postgresServerName"] = p.PostgresServerName,
        ["containerAppsEnvironmentName"] = p.ContainerAppsEnvironmentName, ["managedIdentityName"] = p.ManagedIdentityName,
        ["logAnalyticsWorkspaceName"] = p.LogAnalyticsWorkspaceName, ["databaseName"] = p.DatabaseName,
        ["databaseAdministratorLogin"] = p.DatabaseAdministratorLogin, ["databasePasswordSecretName"] = p.DatabasePasswordSecretName,
        ["databaseAdministratorPassword"] = password, ["postgresSkuName"] = p.PostgresSkuName, ["postgresTier"] = p.PostgresTier,
        ["postgresStorageGiB"] = p.PostgresStorageGiB, ["backupRetentionDays"] = p.BackupRetentionDays,
        ["enableGeoRedundantBackup"] = p.EnableGeoRedundantBackup, ["enableHighAvailability"] = p.EnableHighAvailability,
        ["connectionPoolMaximum"] = p.ConnectionPoolMaximum, ["vnetAddressPrefix"] = p.VnetAddressPrefix,
        ["infrastructureSubnetPrefix"] = p.InfrastructureSubnetPrefix, ["databaseSubnetPrefix"] = p.DatabaseSubnetPrefix,
        ["logRetentionDays"] = p.LogRetentionDays, ["monthlyBudgetUsd"] = p.MonthlyBudgetUsd, ["alertEmails"] = p.AlertEmails,
        ["deploymentPrincipalObjectId"] = principalObjectId, ["deploymentPrincipalType"] = principalType,
        ["tags"] = DeploymentTags(p)
    };

    private Dictionary<string, object?> MigrationParameters(AzureDeploymentProfileDocument p) => new()
    {
        ["location"] = p.Location, ["migrationJobName"] = p.MigrationJobName, ["containerAppsEnvironmentName"] = p.ContainerAppsEnvironmentName,
        ["managedIdentityResourceId"] = ManagedIdentityId(p), ["containerRegistryLoginServer"] = $"{p.ContainerRegistryName}.azurecr.io",
        ["apiImage"] = p.ApiImage, ["keyVaultName"] = p.KeyVaultName, ["databasePasswordSecretName"] = p.DatabasePasswordSecretName,
        ["postgresHost"] = $"{p.PostgresServerName}.postgres.database.azure.com", ["databaseName"] = p.DatabaseName,
        ["databaseAdministratorLogin"] = p.DatabaseAdministratorLogin, ["enableDemoSeed"] = p.EnableDemoSeed, ["tags"] = DeploymentTags(p)
    };

    private Dictionary<string, object?> ApplicationParameters(AzureDeploymentProfileDocument p) => new()
    {
        ["location"] = p.Location, ["containerAppName"] = p.ContainerAppName, ["containerAppsEnvironmentName"] = p.ContainerAppsEnvironmentName,
        ["managedIdentityResourceId"] = ManagedIdentityId(p), ["containerRegistryLoginServer"] = $"{p.ContainerRegistryName}.azurecr.io",
        ["apiImage"] = p.ApiImage, ["uiImage"] = p.UiImage, ["keyVaultName"] = p.KeyVaultName,
        ["minimumReplicas"] = p.MinimumReplicas, ["maximumReplicas"] = p.MaximumReplicas, ["httpConcurrency"] = p.HttpConcurrency,
        ["apiCpu"] = p.ApiCpu.ToString("0.##", CultureInfo.InvariantCulture), ["apiMemory"] = $"{p.ApiMemoryGiB.ToString("0.##", CultureInfo.InvariantCulture)}Gi",
        ["uiCpu"] = p.UiCpu.ToString("0.##", CultureInfo.InvariantCulture), ["uiMemory"] = $"{p.UiMemoryGiB.ToString("0.##", CultureInfo.InvariantCulture)}Gi",
        ["rateLimitPermitLimit"] = p.RateLimitPermitLimit, ["tags"] = DeploymentTags(p)
    };

    private static IReadOnlyDictionary<string, string> DeploymentTags(AzureDeploymentProfileDocument p)
    {
        var tags = new Dictionary<string, string>(p.Tags ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
        {
            ["environment"] = p.EnvironmentKind,
            ["owner"] = p.Owner,
            ["costCenter"] = p.CostCenter,
            ["dataClassification"] = "synthetic-only",
            ["sourceRevision"] = p.SourceRevision
        };
        return tags;
    }

    private string ManagedIdentityId(AzureDeploymentProfileDocument p) => $"/subscriptions/{p.SubscriptionId}/resourceGroups/{p.ResourceGroupName}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{p.ManagedIdentityName}";
    private static string DeploymentName(AzureDeploymentExecutionWorkItem item, string phase) => $"avenchart-{phase}-{item.ExecutionId:N}"[..Math.Min(60, $"avenchart-{phase}-{item.ExecutionId:N}".Length)];

    private string ResolveRepositoryRoot()
    {
        if (!string.IsNullOrWhiteSpace(_options.RepositoryRoot)) return ValidateRoot(Path.GetFullPath(_options.RepositoryRoot));
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(start);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "infra", "azure", "operations", "main.bicep"))) return current.FullName;
                current = current.Parent;
            }
        }
        throw new InvalidOperationException("Repository root containing infra/azure/operations/main.bicep could not be located. Configure AzureOperations:RepositoryRoot.");
    }

    private static string ValidateRoot(string root) => File.Exists(Path.Combine(root, "infra", "azure", "operations", "main.bicep")) ? root : throw new InvalidOperationException("Configured AzureOperations repository root does not contain the deployment templates.");

    private static async Task<TemporaryParameterFile> WriteParameterFileAsync(Dictionary<string, object?> values, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), "avenchart-azure-operations");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.parameters.json");
        var parameters = values.ToDictionary(pair => pair.Key, pair => (object?)new Dictionary<string, object?> { ["value"] = pair.Value });
        var document = new Dictionary<string, object?>
        {
            ["$schema"] = "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
            ["contentVersion"] = "1.0.0.0",
            ["parameters"] = parameters
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(document), new UTF8Encoding(false), cancellationToken);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return new(path);
    }

    private async Task SetPhase(Guid executionId, string phase, string message, CancellationToken cancellationToken) => await WithRepository(repository => repository.SetExecutionPhaseAsync(executionId, phase, message, cancellationToken));
    private async Task<AzureDeploymentHealthResponse> GetHealthInScope(AzureDeploymentProfileDocument profile, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AzureOperationsService>().GetHealthAsync(profile, cancellationToken);
    }

    private async Task<T> WithRepository<T>(Func<AzureOperationsRepository, Task<T>> action)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AzureOperationsRepository>());
    }
    private async Task WithRepository(Func<AzureOperationsRepository, Task> action)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AzureOperationsRepository>());
    }
    private async Task SafeRepository(Func<AzureOperationsRepository, Task> action) { try { await WithRepository(action); } catch (Exception exception) { logger.LogError(exception, "Could not persist terminal Azure operation state."); } }

    private static string GeneratePassword()
    {
        Span<byte> bytes = stackalloc byte[30];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace('+', 'A').Replace('/', 'b').TrimEnd('=') + "!a7Z";
    }

    private static string SummarizeWhatIf(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var changes = document.RootElement.TryGetProperty("changes", out var array) && array.ValueKind == JsonValueKind.Array ? array : default;
            if (changes.ValueKind != JsonValueKind.Array) return "Azure what-if completed successfully.";
            var counts = changes.EnumerateArray().GroupBy(change => change.TryGetProperty("changeType", out var type) ? type.GetString() ?? "Unknown" : "Unknown").OrderBy(group => group.Key).Select(group => $"{group.Key}: {group.Count()}");
            return $"Azure what-if completed. {string.Join(", ", counts)}.";
        }
        catch { return "Azure what-if completed successfully."; }
    }

    private static string SelectPreviousRevision(string json)
    {
        using var document = JsonDocument.Parse(json);
        var candidates = document.RootElement.EnumerateArray()
            .Where(item => !item.TryGetProperty("properties", out var properties) || !properties.TryGetProperty("healthState", out var health) || !string.Equals(health.GetString(), "Unhealthy", StringComparison.OrdinalIgnoreCase))
            .Select(item => new { Name = item.GetProperty("name").GetString(), Created = item.TryGetProperty("properties", out var properties) && properties.TryGetProperty("createdTime", out var created) ? created.GetString() : null })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name)).OrderByDescending(item => item.Created, StringComparer.Ordinal).ToArray();
        if (candidates.Length < 2) throw new InvalidOperationException("No previous healthy Container Apps revision is available for rollback.");
        return candidates[1].Name!;
    }

    private static string ReadJobStatus(string json, string? executionName)
    {
        using var document = JsonDocument.Parse(json);
        var executions = document.RootElement.EnumerateArray();
        foreach (var execution in executions)
        {
            var name = execution.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            if (executionName is not null && !string.Equals(name, executionName, StringComparison.OrdinalIgnoreCase)) continue;
            if (execution.TryGetProperty("properties", out var properties) && properties.TryGetProperty("status", out var status)) return status.GetString() ?? "Unknown";
        }
        return "Pending";
    }

    private static string? TryReadString(string json, string property)
    {
        try { using var document = JsonDocument.Parse(json); return document.RootElement.TryGetProperty(property, out var value) ? value.GetString() : null; }
        catch { return null; }
    }

    private static string SafeError(Exception exception) => exception.Message.Length <= 2000 ? exception.Message : exception.Message[..2000];
    private static void EnsureAllowed(bool allowed, string message) { if (!allowed) throw new InvalidOperationException(message); }

    public void Dispose()
    {
        foreach (var cancellation in _operations.Values) { cancellation.Cancel(); cancellation.Dispose(); }
        _operations.Clear();
    }

    private sealed class TemporaryParameterFile(string path) : IDisposable
    {
        public string Path { get; } = path;
        public void Dispose() { try { if (File.Exists(Path)) File.Delete(Path); } catch { } }
    }
}
