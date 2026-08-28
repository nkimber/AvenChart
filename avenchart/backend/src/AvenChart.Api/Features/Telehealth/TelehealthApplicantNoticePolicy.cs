// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantNoticeDefinition(
    string NoticeKey,
    int NoticeVersion,
    string StateCode,
    string Title,
    string Summary,
    string SourceTitle,
    string SourceUrl,
    IReadOnlyList<string> Disclosures,
    IReadOnlyList<string> DeferredRequirements);

public sealed record NormalizedTelehealthApplicantNoticeAcknowledgment(
    int ExpectedVersion,
    string NoticeKey,
    int NoticeVersion,
    string CurrentLocationStateCode,
    bool CurrentLocationConfirmed,
    bool ModeOfCareAcknowledged,
    bool PrivacyLimitationsAcknowledged,
    bool EmergencyInstructionsAcknowledged,
    bool InPersonOptionAcknowledged,
    bool ClinicianReconfirmationRequiredAcknowledged,
    bool SyntheticDataConfirmed);

public static class TelehealthApplicantNoticePolicy
{
    public const string PolicyKey = "SYNTHETIC_TELEHEALTH_NOTICE_ACKNOWLEDGMENT";
    public const int PolicyVersion = 1;
    public const string ResultingStatus = "SyntheticTelehealthNoticeAcknowledged";
    public const string EvidenceType = "STATE_NOTICE_FIXTURE_AND_PATIENT_ACKNOWLEDGMENTS_ONLY";
    public const string LegalReviewStatus = "PendingIndependentReview";

    private static readonly IReadOnlyDictionary<string, TelehealthApplicantNoticeDefinition> Notices =
        new Dictionary<string, TelehealthApplicantNoticeDefinition>(StringComparer.Ordinal)
        {
            ["GA"] = new(
                "GA_TELEHEALTH_NOTICE_V1",
                1,
                "GA",
                "Georgia synthetic telehealth notice",
                "Telehealth uses electronic communications. A later qualified Georgia clinician must determine whether the available technology and examination are adequate for your situation.",
                "Georgia Composite Medical Board Rule 360-3-.07",
                "https://rules.sos.ga.gov/gac/360-3-.07",
                [
                    "Electronic care can have technology, privacy, and examination limitations.",
                    "Call 911 or seek emergency care for emergency symptoms; this acknowledgment does not contact a clinician.",
                    "You may need an in-person examination or follow-up when telehealth is not adequate."
                ],
                [
                    "A later treating clinician must provide identity, credentials, and emergency contact information.",
                    "A later treating clinician must provide appropriate follow-up and emergency-care instructions and document the evaluation and treatment.",
                    "Clinician licensure, history, examination adequacy, standard of care, and consent remain separate gates."
                ]),
            ["CA"] = new(
                "CA_TELEHEALTH_NOTICE_V1",
                1,
                "CA",
                "California synthetic telehealth notice",
                "Telehealth is a mode of delivering health care by communication technology. Before care, the initiating provider must inform you about telehealth, obtain verbal or written consent, and document that consent.",
                "California Business and Professions Code § 2290.5",
                "https://leginfo.legislature.ca.gov/faces/codes_displaySection.xhtml?lawCode=BPC&sectionNum=2290.5.",
                [
                    "Electronic care can have technology, privacy, and examination limitations.",
                    "Call 911 or seek emergency care for emergency symptoms; this acknowledgment does not contact a clinician.",
                    "Agreeing to telehealth does not prevent you from receiving in-person care during a course of treatment."
                ],
                [
                    "The initiating provider must later inform you about telehealth, obtain verbal or written consent, and document it before delivery of care.",
                    "Confidentiality, professional responsibility, scope, and standard-of-care requirements remain applicable.",
                    "This synthetic acknowledgment is not the provider-obtained consent required before care."
                ]),
            ["FL"] = new(
                "FL_TELEHEALTH_NOTICE_V1",
                1,
                "FL",
                "Florida synthetic telehealth notice",
                "Telehealth uses communications technology to provide health services. A later Florida-authorized clinician must meet the prevailing professional standard that applies to in-person care.",
                "Florida Statutes § 456.47",
                "https://leg.state.fl.us/statutes/index.cfm?App_mode=Display_Statute&URL=0400-0499/0456/Sections/0456.47.html",
                [
                    "Electronic care can have technology, privacy, and examination limitations.",
                    "Call 911 or seek emergency care for emergency symptoms; this acknowledgment does not contact a clinician.",
                    "The legal location of telehealth care follows where the patient is located, so your current state must be reconfirmed."
                ],
                [
                    "A later clinician must satisfy applicable licensure or registration, scope, evaluation, records, confidentiality, and standard-of-care requirements.",
                    "Treatment-specific informed consent and clinician disclosures remain separate gates.",
                    "This fixture does not claim that Florida law imposes or waives a separate telehealth-consent form."
                ])
        };

    public static TelehealthApplicantNoticeDefinition ForState(string stateCode) =>
        Notices.TryGetValue(stateCode, out var notice)
            ? notice
            : throw TelehealthProblem.Conflict(
                "telehealth_applicant_notice_state_unsupported",
                "The current synthetic location does not have an approved notice fixture.");

    public static NormalizedTelehealthApplicantNoticeAcknowledgment Normalize(
        AcknowledgeTelehealthApplicantNoticeRequest request,
        TelehealthApplicantNoticeDefinition expectedNotice)
    {
        if (request.ExpectedVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_version_invalid",
                "ExpectedVersion must be positive.");
        }
        var state = request.CurrentLocationStateCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!string.Equals(state, expectedNotice.StateCode, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_notice_location_changed",
                "Current location changed. Start a fresh safety and location process before continuing.");
        }
        if (!string.Equals(request.NoticeKey?.Trim(), expectedNotice.NoticeKey, StringComparison.Ordinal)
            || request.NoticeVersion != expectedNotice.NoticeVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_notice_version_conflict",
                "The state notice changed. Reload it before acknowledging.");
        }
        if (!request.CurrentLocationConfirmed
            || !request.ModeOfCareAcknowledged
            || !request.PrivacyLimitationsAcknowledged
            || !request.EmergencyInstructionsAcknowledged
            || !request.InPersonOptionAcknowledged
            || !request.ClinicianReconfirmationRequiredAcknowledged
            || !request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_notice_acknowledgments_required",
                "Confirm current location and every notice limitation before continuing.");
        }
        return new(
            request.ExpectedVersion,
            expectedNotice.NoticeKey,
            expectedNotice.NoticeVersion,
            state,
            request.CurrentLocationConfirmed,
            request.ModeOfCareAcknowledged,
            request.PrivacyLimitationsAcknowledged,
            request.EmergencyInstructionsAcknowledged,
            request.InPersonOptionAcknowledged,
            request.ClinicianReconfirmationRequiredAcknowledged,
            request.SyntheticDataConfirmed);
    }
}
