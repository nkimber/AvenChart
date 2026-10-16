// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;

namespace AvenChart.Api.Features.Telehealth;

public static class TelehealthDemoPolicy
{
    public static string RequireRole(string role) => role is "patient" or "physician" ? role : throw TelehealthProblem.NotFound();

    public static string RequireKey(string? key) => key is { Length: 64 } && key.All(char.IsAsciiHexDigit)
        ? key.ToLowerInvariant() : throw TelehealthProblem.NotFound();

    public static string Hash(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(RequireKey(key))));

    // Regenerable for an idempotent create retry; only the hashes are stored.
    public static string Invitation(string observerKey, Guid runId, string role) => Convert.ToHexStringLower(
        HMACSHA256.HashData(Convert.FromHexString(RequireKey(observerKey)), Encoding.UTF8.GetBytes($"synthetic-demo-v1:{runId:D}:{RequireRole(role)}")));
}

public sealed record CreateTelehealthDemoRequest(bool SyntheticDataConfirmed, string ObserverKey);
public sealed record RedeemTelehealthDemoRequest(string Invitation, string SessionKey);
public sealed record TelehealthDemoHeartbeatRequest(string Stage);
public sealed record TelehealthDemoCreateResponse(Guid RunId, DateTimeOffset ExpiresAt, string PatientInvitation, string PhysicianInvitation, string Transport);
public sealed record TelehealthDemoParticipant(Guid ParticipantId, Guid RunId, string Role, DateTimeOffset ExpiresAt, string Transport);
public sealed record TelehealthDemoStatus(Guid RunId, string Status, DateTimeOffset ExpiresAt, string PatientStatus, string PhysicianStatus, string Transport);
public sealed record TelehealthDemoSignalRequest(string? Kind, string? Payload, int AfterSequence = 0);
