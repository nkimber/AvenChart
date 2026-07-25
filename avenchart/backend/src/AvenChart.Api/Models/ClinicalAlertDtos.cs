namespace AvenChart.Api.Models;

public sealed record EncounterClinicalAlertItem(
    string Key,
    string Title,
    string Severity,
    string Message,
    string Reason);

public sealed record EncounterClinicalAlertsResponse(
    int Encounter,
    IReadOnlyList<EncounterClinicalAlertItem> Alerts);
