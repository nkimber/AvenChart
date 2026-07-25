using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

/// <summary>
/// Evaluates the explicitly supported encounter-banner rules. Rule definitions remain data-driven;
/// new rule keys need an intentional evaluator before they can affect clinical workflow.
/// </summary>
public sealed class ClinicalAlertEvaluationRepository(NpgsqlDataSource dataSource)
{
    public async Task<EncounterClinicalAlertsResponse?> GetEncounterAlertsAsync(int encounter, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var patientCommand = connection.CreateCommand();
        patientCommand.CommandText = "select pid from encounters where encounter=@encounter;";
        patientCommand.Parameters.AddWithValue("encounter", encounter);
        var legacyPid = await patientCommand.ExecuteScalarAsync(cancellationToken);
        if (legacyPid is null) return null;

        await using var allergyCommand = connection.CreateCommand();
        allergyCommand.CommandText = "select exists(select 1 from allergies where pid=@pid and type='allergy' and activity=1);";
        allergyCommand.Parameters.AddWithValue("pid", (int)legacyPid);
        var hasActiveAllergy = (bool)(await allergyCommand.ExecuteScalarAsync(cancellationToken) ?? false);

        await using var ruleCommand = connection.CreateCommand();
        ruleCommand.CommandText = """
            select rule_key,title,severity,message
            from clinical_alert_rules
            where active=true and trigger_type='encounter' and target_type='banner' and rule_key='ALLERGY_REVIEW'
            order by sequence,rule_key;
            """;
        var alerts = new List<EncounterClinicalAlertItem>();
        await using var reader = await ruleCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!hasActiveAllergy)
            {
                alerts.Add(new EncounterClinicalAlertItem(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    "No active allergy records are documented for this patient."));
            }
        }

        return new EncounterClinicalAlertsResponse(encounter, alerts);
    }
}
