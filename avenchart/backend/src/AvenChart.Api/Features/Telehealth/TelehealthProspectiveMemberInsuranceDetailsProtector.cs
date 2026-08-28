// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthProspectiveMemberInsuranceDetailsProtector
{
    public const string Scheme = "ASP.NET_CORE_DATA_PROTECTION";
    public const string Purpose = "AvenChart.Telehealth.ProspectiveMemberInsuranceDetails.v1";
    public const int Version = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector _protector;

    public TelehealthProspectiveMemberInsuranceDetailsProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(TelehealthProtectedMemberInsurancePayload payload) =>
        _protector.Protect(JsonSerializer.Serialize(payload, JsonOptions));

    public TelehealthProtectedMemberInsurancePayload Unprotect(string protectedPayload)
    {
        try
        {
            var json = _protector.Unprotect(protectedPayload);
            return JsonSerializer.Deserialize<TelehealthProtectedMemberInsurancePayload>(json, JsonOptions)
                ?? throw new CryptographicException("Protected synthetic member details were empty.");
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_member_details_protection_invalid",
                "The protected synthetic member-details receipt cannot be validated. Start again with a new synthetic applicant.");
        }
    }

    public bool Matches(string protectedPayload, TelehealthProtectedMemberInsurancePayload supplied)
    {
        var existing = Unprotect(protectedPayload);
        var existingBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(existing, JsonOptions));
        var suppliedBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(supplied, JsonOptions));
        return existingBytes.Length == suppliedBytes.Length
            && CryptographicOperations.FixedTimeEquals(existingBytes, suppliedBytes);
    }
}
