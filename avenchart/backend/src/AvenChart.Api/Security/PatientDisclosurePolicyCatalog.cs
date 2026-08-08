// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;

namespace AvenChart.Api.Security;

public static class PatientDisclosurePolicyCatalog
{
    public const string Revision = "local-disclosure-authority-v1";

    public static readonly IReadOnlyList<string> ScopeKeys =
    [
        "clinical-summary",
        "encounter-notes",
        "laboratory-results",
        "medications",
        "billing-records",
        "documents",
    ];

    public static readonly IReadOnlyList<string> VerificationMethods =
    [
        "in-person",
        "portal-authenticated",
        "documented-authority",
        "other",
    ];

    public static PatientDisclosurePolicyResponse Build() => new(
        Revision,
        "local-foundation-owner-gated",
        [
            new("patient", "Patient authority"),
            new("proxy", "Proxy authority"),
        ],
        [
            new("in-person", "Verified in person"),
            new("portal-authenticated", "Authenticated portal request"),
            new("documented-authority", "Documented authority reviewed"),
            new("other", "Other recorded verification"),
        ],
        [
            new("clinical-summary", "Clinical summary", "Problems, allergies, immunizations, and summary facts."),
            new("encounter-notes", "Encounter notes", "Selected encounter documentation."),
            new("laboratory-results", "Laboratory results", "Selected laboratory orders and results."),
            new("medications", "Medications", "Medication and prescription history."),
            new("billing-records", "Billing records", "Selected charges, claims, statements, and payments."),
            new("documents", "Documents", "Selected patient document content and metadata."),
        ],
        new(
            Enabled: false,
            State: "disabled-owner-gated",
            Reason: "No emergency-access or break-glass policy has been selected.",
            RequiredDecisions:
            [
                "Whether emergency access is permitted at all.",
                "Eligible identities, patient scope, purpose, reason, and maximum duration.",
                "Automatic expiry, elevated audit, notification, review, and escalation.",
                "Explicit prohibition on bulk export and configuration access.",
            ]),
        [
            "A recorded local authority is staff-entered evidence, not a legal determination.",
            "Guardian or representative contact facts do not automatically establish proxy authority.",
            "Purpose and recipient must match exactly; requested scope must be a subset of active authority scope.",
            "Expired or revoked authority blocks new disclosure requests and approval.",
            "Approval records a local decision only; it does not package, download, transmit, or deliver records.",
            "Retention, legal hold, release-package content, delivery channels, and production disclosure policy remain owner-gated.",
        ]);
}
