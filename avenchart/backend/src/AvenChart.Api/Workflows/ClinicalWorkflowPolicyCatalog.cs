using AvenChart.Api.Models;

namespace AvenChart.Api.Workflows;

public static class ClinicalWorkflowPolicyCatalog
{
    public const string Revision = "local-clinical-workflow-v1";
    public const string PatientAuthorizationWorkflow = "patient-authorization";
    public const string PatientReferralWorkflow = "patient-referral";
    public const string ResponsibilityTransferReasonCode = "responsibility-transfer";

    private static readonly IReadOnlyList<ClinicalWorkflowTransitionOption> AuthorizationTransitions =
    [
        new("submit", "draft", "submitted", "authorization-submitted", "Submit for review", false),
        new("cancel", "draft", "cancelled", "authorization-cancelled", "Cancel request", false),
        new("approve", "submitted", "approved", "authorization-approved", "Approve", true),
        new("deny", "submitted", "denied", "authorization-denied", "Deny", false),
        new("cancel", "submitted", "cancelled", "authorization-cancelled", "Cancel request", false),
        new("expire", "approved", "expired", "authorization-expired", "Mark expired", false),
    ];

    private static readonly IReadOnlyList<ClinicalWorkflowTransitionOption> ReferralTransitions =
    [
        new("send", "draft", "sent", "referral-sent", "Send referral", false),
        new("cancel", "draft", "cancelled", "referral-cancelled", "Cancel referral", false),
        new("receive", "sent", "received", "counter-referral-received", "Record counter-referral", false),
        new("cancel", "sent", "cancelled", "referral-cancelled", "Cancel referral", false),
        new("close", "received", "closed", "referral-closed", "Close referral", false),
    ];

    public static IReadOnlyList<ClinicalWorkflowTransitionOption> GetAvailableAuthorizationTransitions(
        string state) =>
        AuthorizationTransitions
            .Where(item => string.Equals(item.FromState, state, StringComparison.Ordinal))
            .ToArray();

    public static IReadOnlyList<ClinicalWorkflowTransitionOption> GetAvailableReferralTransitions(
        string state) =>
        ReferralTransitions
            .Where(item => string.Equals(item.FromState, state, StringComparison.Ordinal))
            .ToArray();

    public static ClinicalWorkflowTransitionOption RequireAuthorizationTransition(
        string currentState,
        string requestedState,
        string? reasonCode,
        string? reason)
    {
        var transition = AuthorizationTransitions.SingleOrDefault(item =>
            string.Equals(item.FromState, currentState, StringComparison.Ordinal)
            && string.Equals(item.ToState, requestedState, StringComparison.Ordinal));
        if (transition is null)
        {
            throw new ArgumentException(
                $"A patient authorization in {currentState} state cannot transition to {requestedState}.");
        }

        if (!string.Equals(
                transition.ReasonCode,
                NormalizeToken(reasonCode),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Transition to {requestedState} requires reason code '{transition.ReasonCode}'.");
        }

        RequireReason(reason);
        return transition;
    }

    public static ClinicalWorkflowTransitionOption RequireReferralTransition(
        string currentState,
        string requestedState,
        string? reasonCode,
        string? reason)
    {
        var transition = ReferralTransitions.SingleOrDefault(item =>
            string.Equals(item.FromState, currentState, StringComparison.Ordinal)
            && string.Equals(item.ToState, requestedState, StringComparison.Ordinal));
        if (transition is null)
        {
            throw new ArgumentException(
                $"A patient referral in {currentState} state cannot transition to {requestedState}.");
        }

        if (!string.Equals(transition.ReasonCode, NormalizeToken(reasonCode), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Transition to {requestedState} requires reason code '{transition.ReasonCode}'.");
        }

        RequireReason(reason);
        return transition;
    }

    public static void RequireAssignmentChange(
        string currentState,
        string? reasonCode,
        string? reason)
    {
        if (currentState is not ("draft" or "submitted" or "approved" or "sent" or "received"))
        {
            throw new ArgumentException(
                $"Responsibility cannot be changed while a patient authorization is {currentState}.");
        }

        if (!string.Equals(
                ResponsibilityTransferReasonCode,
                NormalizeToken(reasonCode),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Assignment changes require reason code '{ResponsibilityTransferReasonCode}'.");
        }

        RequireReason(reason);
    }

    public static string RequireReason(string? reason)
    {
        var normalized = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 500)
        {
            throw new ArgumentException(
                "A workflow reason is required and must be 500 characters or fewer.");
        }

        return normalized;
    }

    private static string? NormalizeToken(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
}

public sealed class ClinicalWorkflowVersionConflictException(
    int expectedVersion,
    int currentVersion)
    : Exception(
        $"The workflow changed after it was loaded. Expected version {expectedVersion}; current version is {currentVersion}.")
{
    public int ExpectedVersion { get; } = expectedVersion;

    public int CurrentVersion { get; } = currentVersion;
}
