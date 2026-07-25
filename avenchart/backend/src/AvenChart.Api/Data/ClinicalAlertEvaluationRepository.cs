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

        var hasActiveAllergy = await HasActiveAllergyAsync(connection, (int)legacyPid, cancellationToken);
        var allergyReviewAcknowledged = await IsOpenAcknowledgmentAsync(connection, encounter, "ALLERGY_REVIEW", cancellationToken);

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
            if (!hasActiveAllergy && !allergyReviewAcknowledged)
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

    public async Task<EncounterClinicalAlertsResponse?> AcknowledgeAsync(int encounter, string ruleKey, string username, CancellationToken cancellationToken)
    {
        EnsureAllergyReviewRule(ruleKey);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var legacyPid = await GetEncounterPatientAsync(connection, transaction, encounter, cancellationToken);
        if (legacyPid is null) return null;
        if (!await IsActiveAllergyReviewRuleAsync(connection, transaction, cancellationToken)) throw new InvalidOperationException("The allergy-review rule is not active.");
        if (await HasActiveAllergyAsync(connection, legacyPid.Value, cancellationToken, transaction)) throw new InvalidOperationException("The allergy-review condition is not active for this encounter.");

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into encounter_clinical_alert_acknowledgments(encounter,rule_key,acknowledged_at,acknowledged_by,reopened_at,reopened_by)
            values(@encounter,@rule,now(),@username,null,null)
            on conflict(encounter,rule_key) do update set
              acknowledged_at=excluded.acknowledged_at,
              acknowledged_by=excluded.acknowledged_by,
              reopened_at=null,
              reopened_by=null
            where encounter_clinical_alert_acknowledgments.reopened_at is not null;
            """;
        command.Parameters.AddWithValue("encounter", encounter);
        command.Parameters.AddWithValue("rule", "ALLERGY_REVIEW");
        command.Parameters.AddWithValue("username", username);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetEncounterAlertsAsync(encounter, cancellationToken);
    }

    public async Task<EncounterClinicalAlertsResponse?> ReopenAsync(int encounter, string ruleKey, string username, CancellationToken cancellationToken)
    {
        EnsureAllergyReviewRule(ruleKey);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (await GetEncounterPatientAsync(connection, transaction, encounter, cancellationToken) is null) return null;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "update encounter_clinical_alert_acknowledgments set reopened_at=now(),reopened_by=@username where encounter=@encounter and rule_key='ALLERGY_REVIEW' and reopened_at is null;";
        command.Parameters.AddWithValue("encounter", encounter);
        command.Parameters.AddWithValue("username", username);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetEncounterAlertsAsync(encounter, cancellationToken);
    }

    private static void EnsureAllergyReviewRule(string ruleKey)
    {
        if (!string.Equals(ruleKey, "ALLERGY_REVIEW", StringComparison.Ordinal)) throw new ArgumentException("This clinical alert does not support acknowledgement.");
    }

    private static async Task<int?> GetEncounterPatientAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int encounter, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select pid from encounters where encounter=@encounter;";
        command.Parameters.AddWithValue("encounter", encounter);
        return await command.ExecuteScalarAsync(cancellationToken) is int legacyPid ? legacyPid : null;
    }

    private static async Task<bool> HasActiveAllergyAsync(NpgsqlConnection connection, int legacyPid, CancellationToken cancellationToken, NpgsqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select exists(select 1 from allergies where pid=@pid and type='allergy' and activity=1);";
        command.Parameters.AddWithValue("pid", legacyPid);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<bool> IsOpenAcknowledgmentAsync(NpgsqlConnection connection, int encounter, string ruleKey, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select exists(select 1 from encounter_clinical_alert_acknowledgments where encounter=@encounter and rule_key=@rule and reopened_at is null);";
        command.Parameters.AddWithValue("encounter", encounter);
        command.Parameters.AddWithValue("rule", ruleKey);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<bool> IsActiveAllergyReviewRuleAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select exists(select 1 from clinical_alert_rules where rule_key='ALLERGY_REVIEW' and active=true and trigger_type='encounter' and target_type='banner');";
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }
}
