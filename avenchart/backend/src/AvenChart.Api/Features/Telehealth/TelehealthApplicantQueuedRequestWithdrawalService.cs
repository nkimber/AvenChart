// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantQueuedRequestWithdrawalService(
    TelehealthApplicantQueuedRequestWithdrawalRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantQueuedRequestWithdrawalResponse> WithdrawAsync(
        HttpContext httpContext,
        Guid applicantId,
        Guid requestId,
        WithdrawTelehealthApplicantQueuedRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        if (request.ExpectedRequestVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_queued_withdrawal_version_required",
                "A current synthetic request version is required.");
        }
        if (!request.SyntheticWithdrawalConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_queued_withdrawal_confirmation_required",
                "Confirm that this is a synthetic queued-request withdrawal.");
        }

        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var accessKeyHash = TelehealthProspectiveApplicantPolicy.Hash(key);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "synthetic-applicant-queued-request-withdrawal-v1",
            applicantId, requestId, request.ExpectedRequestVersion, request.SyntheticWithdrawalConfirmed);
        return await repository.WithdrawAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            accessKeyHash,
            TelehealthApplicantConnectionPolicy.CreateParticipantSubjectHash(applicantId, accessKeyHash),
            requestId,
            request.ExpectedRequestVersion,
            semanticKey,
            fingerprint,
            cancellationToken);
    }

    private void RequireConfiguredHost(HostString host)
    {
        if (!_options.BrandedHosts.Contains(host.Host, StringComparer.OrdinalIgnoreCase))
        {
            throw new TelehealthProblem(
                StatusCodes.Status404NotFound,
                "telehealth_practice_not_found",
                "Telehealth practice was not found",
                "This host is not configured for the synthetic telehealth practice.");
        }
    }
}
