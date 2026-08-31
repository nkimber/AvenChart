// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Azure;
using Azure.Communication;
using Azure.Communication.Identity;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

/// <summary>
/// Vends a narrowly scoped, short-lived ACS VoIP credential for one authorized
/// synthetic browser participant. AvenChart never receives browser media.
/// </summary>
public interface ITelehealthInternetCallingProvider
{
    Task<TelehealthInternetCallingConfiguration> CreateConfigurationAsync(Guid grantId, CancellationToken cancellationToken);
}

public sealed record TelehealthInternetCallingConfiguration(string AccessToken, DateTimeOffset ExpiresAt);

public sealed class AzureCommunicationServicesTelehealthCallingProvider(
    IOptions<TelehealthOptions> options,
    ILogger<AzureCommunicationServicesTelehealthCallingProvider> logger) : ITelehealthInternetCallingProvider
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan IdentityCleanupDelay = TimeSpan.FromMinutes(5);
    private readonly TelehealthOptions _options = options.Value;
    private readonly ILogger<AzureCommunicationServicesTelehealthCallingProvider> _logger = logger;
    private readonly CommunicationIdentityClient? _client = CreateClient(options.Value);
    private readonly ConcurrentDictionary<Guid, Lazy<Task<TelehealthInternetCallingConfiguration>>> _configurationsByGrant = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _configurationExpiryByGrant = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _issuedIdentityExpiry = new(StringComparer.Ordinal);

    public async Task<TelehealthInternetCallingConfiguration> CreateConfigurationAsync(Guid grantId, CancellationToken cancellationToken)
    {
        if (!_options.InternetCallingPocEnabled || _client is null)
        {
            throw TelehealthProblem.NotFound();
        }

        // A valid waiting-room grant is a bearer capability. Reuse its one
        // short-lived credential so a retry cannot create an unbounded number
        // of anonymous ACS identities during the grant's lifetime.
        var configuration = _configurationsByGrant.GetOrAdd(
            grantId,
            _ => new Lazy<Task<TelehealthInternetCallingConfiguration>>(
                () => CreateConfigurationCoreAsync(grantId, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await configuration.Value;
        }
        catch
        {
            // Do not retain a failed or cancelled lazy issuance. A later
            // authorized retry can obtain a credential once the dependency is
            // healthy again.
            _configurationsByGrant.TryRemove(grantId, out _);
            throw;
        }
    }

    private async Task<TelehealthInternetCallingConfiguration> CreateConfigurationCoreAsync(
        Guid grantId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client!.CreateUserAndTokenAsync(
                [CommunicationTokenScope.VoIP],
                TokenLifetime,
                cancellationToken);
            var result = response.Value;
            _issuedIdentityExpiry.TryAdd(result.User.Id, result.AccessToken.ExpiresOn.Add(IdentityCleanupDelay));
            _configurationExpiryByGrant[grantId] = result.AccessToken.ExpiresOn;
            return new TelehealthInternetCallingConfiguration(result.AccessToken.Token, result.AccessToken.ExpiresOn);
        }
        catch (RequestFailedException exception)
        {
            // Credential material and ACS response bodies must never enter logs.
            _logger.LogWarning("Azure Communication Services calling credential issuance failed with status {Status}.", exception.Status);
            throw TelehealthProblem.ServiceUnavailable(
                "telehealth_internet_calling_unavailable",
                "The synthetic internet calling service is temporarily unavailable. End and retry both participants.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning("The synthetic internet calling credential could not be issued: {ExceptionType}.", exception.GetType().Name);
            throw TelehealthProblem.ServiceUnavailable(
                "telehealth_internet_calling_unavailable",
                "The synthetic internet calling service is temporarily unavailable. End and retry both participants.");
        }
    }

    /// <summary>
    /// Deletes only anonymous ACS identities created by this running POC after
    /// their credential has expired. Nothing is persisted, logged, or mapped
    /// to an AvenChart person. Restart recovery remains intentionally out of
    /// scope for this non-production demonstration.
    /// </summary>
    public async Task ReapExpiredIdentitiesAsync(CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var grant in _configurationExpiryByGrant.Where(entry => entry.Value <= now).ToArray())
        {
            _configurationExpiryByGrant.TryRemove(grant.Key, out _);
            _configurationsByGrant.TryRemove(grant.Key, out _);
        }

        foreach (var identity in _issuedIdentityExpiry.Where(entry => entry.Value <= now).ToArray())
        {
            try
            {
                await _client.DeleteUserAsync(new CommunicationUserIdentifier(identity.Key), cancellationToken);
                _issuedIdentityExpiry.TryRemove(identity.Key, out _);
            }
            catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
            {
                _issuedIdentityExpiry.TryRemove(identity.Key, out _);
            }
            catch (RequestFailedException exception)
            {
                _logger.LogWarning("Azure Communication Services synthetic identity cleanup failed with status {Status}.", exception.Status);
            }
            catch (Exception exception)
            {
                _logger.LogWarning("Azure Communication Services synthetic identity cleanup failed: {ExceptionType}.", exception.GetType().Name);
            }
        }
    }

    private static CommunicationIdentityClient? CreateClient(TelehealthOptions options) =>
        options.InternetCallingPocEnabled && !string.IsNullOrWhiteSpace(options.InternetCallingConnectionString)
            ? new CommunicationIdentityClient(options.InternetCallingConnectionString)
            : null;
}

public sealed class TelehealthInternetCallingIdentityReaper(
    AzureCommunicationServicesTelehealthCallingProvider provider,
    ILogger<TelehealthInternetCallingIdentityReaper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await provider.ReapExpiredIdentitiesAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError("The synthetic internet calling identity reaper stopped unexpectedly: {ExceptionType}.", exception.GetType().Name);
        }
    }
}
