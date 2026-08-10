// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using AvenChart.Api.Configuration;
using AvenChart.Api.Data;
using AvenChart.Api.Models;
using AvenChart.Api.Security;

namespace AvenChart.Api.Infrastructure;

public sealed class AzureOperationsAccessService(
    AzureOperationsAccessRepository repository,
    IOptions<AzureOperationsOptions> options)
{
    public const string AccessHeader = "X-AvenChart-Operations-Access";
    private const int MinimumCodeLength = 12;
    private const int MaximumCodeLength = 128;
    private const int SaltLength = 16;
    private const int HashLength = 32;
    private const int TokenLength = 32;
    private readonly AzureOperationsOptions _options = options.Value;

    public async Task<AzureOperationsUnlockResponse> UnlockAsync(
        AuthSessionResponse session,
        string? code,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var sessionId = RequireSessionId(session);
        var lockout = await repository.GetLockoutAsync(sessionId, cancellationToken);
        if (lockout is { } lockedUntil)
            throw new AzureOperationsAccessLockedException(lockedUntil);

        var credential = await repository.GetCredentialAsync(cancellationToken);
        var verified = IsBoundedCode(code) && VerifyCode(code!, credential);
        if (!verified)
        {
            var failure = await repository.RecordFailedUnlockAsync(
                sessionId,
                session.Username,
                _options.UnlockMaximumFailures,
                TimeSpan.FromMinutes(_options.UnlockFailureWindowMinutes),
                TimeSpan.FromMinutes(_options.UnlockLockoutMinutes),
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString(),
                cancellationToken);
            if (failure.LockedUntil is { } nowLockedUntil)
                throw new AzureOperationsAccessLockedException(nowLockedUntil);
            throw new AzureOperationsAccessDeniedException("The Operations access code is incorrect.");
        }

        var tokenBytes = RandomNumberGenerator.GetBytes(TokenLength);
        var token = Base64UrlEncode(tokenBytes);
        var tokenHash = HashToken(token);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.AccessGrantMinutes);
        await repository.CreateGrantAsync(
            Guid.NewGuid(), tokenHash, sessionId, session.Username, credential.Version, expiresAt,
            context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers.UserAgent.ToString(), cancellationToken);
        return new(token, expiresAt, credential.RequiresChange);
    }

    public async Task<AzureOperationsGrantValidation?> ValidateGrantAsync(
        AuthSessionResponse session,
        string? token,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var sessionId = RequireSessionId(session);
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
        {
            await repository.RecordRejectedGrantAsync(
                sessionId, session.Username, context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString(), cancellationToken);
            return null;
        }
        var validation = await repository.ValidateGrantAsync(HashToken(token), sessionId, cancellationToken);
        if (validation is null)
        {
            await repository.RecordRejectedGrantAsync(
                sessionId, session.Username, context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString(), cancellationToken);
        }
        return validation;
    }

    public async Task<AzureOperationsChangeCodeResponse> ChangeCodeAsync(
        AuthSessionResponse session,
        AzureOperationsChangeCodeRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var sessionId = RequireSessionId(session);
        ValidateReplacementCode(request.NewCode);
        var credential = await repository.GetCredentialAsync(cancellationToken);
        if (!IsBoundedCode(request.CurrentCode) || !VerifyCode(request.CurrentCode, credential))
        {
            var failure = await repository.RecordFailedUnlockAsync(
                sessionId,
                session.Username,
                _options.UnlockMaximumFailures,
                TimeSpan.FromMinutes(_options.UnlockFailureWindowMinutes),
                TimeSpan.FromMinutes(_options.UnlockLockoutMinutes),
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString(),
                cancellationToken);
            if (failure.LockedUntil is { } lockedUntil)
                throw new AzureOperationsAccessLockedException(lockedUntil);
            throw new AzureOperationsAccessDeniedException("The current Operations access code is incorrect.");
        }
        if (CryptographicOperations.FixedTimeEquals(
                DeriveCodeHash(request.NewCode, credential.Salt, credential.Iterations), credential.Hash))
            throw new ArgumentException("The new Operations access code must be different from the current code.");

        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = DeriveCodeHash(request.NewCode, salt, _options.AccessCodeHashIterations);
        var changedAt = await repository.ChangeCodeAsync(
            credential.Version, salt, hash, _options.AccessCodeHashIterations, sessionId, session.Username,
            context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers.UserAgent.ToString(), cancellationToken);
        return new(true, true, changedAt);
    }

    public async Task LockAsync(
        AuthSessionResponse session,
        string token,
        CancellationToken cancellationToken)
    {
        await repository.RevokeGrantAsync(HashToken(token), RequireSessionId(session), session.Username, cancellationToken);
    }

    private static Guid RequireSessionId(AuthSessionResponse session) =>
        session.Authenticated && session.SessionId is { } sessionId
            ? sessionId
            : throw new AzureOperationsAccessDeniedException("An active administrator session is required.");

    private static bool IsBoundedCode(string? code) =>
        code is { Length: >= MinimumCodeLength and <= MaximumCodeLength } &&
        string.Equals(code, code.Trim(), StringComparison.Ordinal);

    private static bool VerifyCode(string code, AzureOperationsAccessCredential credential)
    {
        var candidate = DeriveCodeHash(code, credential.Salt, credential.Iterations);
        return CryptographicOperations.FixedTimeEquals(candidate, credential.Hash);
    }

    private static byte[] DeriveCodeHash(string code, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(code, salt, iterations, HashAlgorithmName.SHA256, HashLength);

    private static byte[] HashToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static void ValidateReplacementCode(string? code)
    {
        if (!IsBoundedCode(code))
            throw new ArgumentException($"The new Operations access code must be {MinimumCodeLength} to {MaximumCodeLength} characters with no leading or trailing spaces.");
    }
}

public sealed class AzureOperationsAccessFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var identity = context.HttpContext.RequestServices.GetRequiredService<IStaffIdentityAdapter>();
        var access = context.HttpContext.RequestServices.GetRequiredService<AzureOperationsAccessService>();
        var session = await identity.ResolveAsync(context.HttpContext, context.HttpContext.RequestAborted);
        var token = context.HttpContext.Request.Headers[AzureOperationsAccessService.AccessHeader].ToString();
        var validation = await access.ValidateGrantAsync(session, token, context.HttpContext, context.HttpContext.RequestAborted);
        if (validation is null)
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return Results.Json(new
            {
                error = "operations_access_required",
                detail = "Enter the Azure Operations access code to continue."
            }, statusCode: StatusCodes.Status403Forbidden);
        }
        if (validation.RequiresCodeChange &&
            context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<AzureOperationsBootstrapAccessAllowed>() is null)
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return Results.Json(new
            {
                error = "operations_code_change_required",
                detail = "Replace the bootstrap Operations access code before viewing or changing Azure information."
            }, statusCode: StatusCodes.Status403Forbidden);
        }
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        context.HttpContext.Items["azureOperationsGrant"] = validation;
        return await next(context);
    }
}

public sealed class AzureOperationsEnabledFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var options = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<AzureOperationsOptions>>()
            .Value;
        if (!options.Enabled)
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return ValueTask.FromResult<object?>(Results.Json(new
            {
                error = "azure_operations_disabled",
                detail = "Azure deployment operations are disabled on this host."
            }, statusCode: StatusCodes.Status503ServiceUnavailable));
        }

        return next(context);
    }
}

public sealed class AzureOperationsBootstrapAccessAllowed;

public sealed class AzureOperationsAccessDeniedException(string message) : UnauthorizedAccessException(message);

public sealed class AzureOperationsAccessLockedException(DateTimeOffset lockedUntil)
    : UnauthorizedAccessException("Too many incorrect Operations access-code attempts.")
{
    public DateTimeOffset LockedUntil { get; } = lockedUntil;
}
