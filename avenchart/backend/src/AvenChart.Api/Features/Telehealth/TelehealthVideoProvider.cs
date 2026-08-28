// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthVideoProvision(
    string AdapterMode,
    string ProviderInstanceId,
    string ProviderSessionReference,
    string JoinCredential,
    string JoinCredentialHash);

public interface ITelehealthVideoProvider
{
    TelehealthVideoProvision Prepare(
        Guid sessionId,
        Guid grantId,
        string participantRole,
        DateTimeOffset expiresAt);
}

public sealed class SyntheticTelehealthVideoProvider : ITelehealthVideoProvider
{
    public const string AdapterMode = "NON_PRODUCTION";
    private readonly byte[] _processKey = RandomNumberGenerator.GetBytes(32);
    private readonly string _providerInstanceId;

    public SyntheticTelehealthVideoProvider()
    {
        _providerInstanceId = Convert.ToHexStringLower(SHA256.HashData(_processKey));
    }

    public TelehealthVideoProvision Prepare(
        Guid sessionId,
        Guid grantId,
        string participantRole,
        DateTimeOffset expiresAt)
    {
        var role = participantRole is "patient" or "physician"
            ? participantRole
            : throw new ArgumentOutOfRangeException(nameof(participantRole));
        var material = Encoding.UTF8.GetBytes(
            $"avenchart-video-simulator-v1\u001f{sessionId:D}\u001f{grantId:D}\u001f{role}");
        var credential = Base64UrlEncode(HMACSHA256.HashData(_processKey, material));
        var credentialHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(credential)));
        var providerReference = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes($"avenchart-opaque-session-v1\u001f{sessionId:D}")));
        return new TelehealthVideoProvision(
            AdapterMode,
            _providerInstanceId,
            providerReference,
            credential,
            credentialHash);
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
