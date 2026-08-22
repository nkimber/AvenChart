// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

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
        var allergyStateVersion = await GetAllergyReviewStateVersionAsync(connection, null, (int)legacyPid, cancellationToken);

        await using var ruleCommand = connection.CreateCommand();
        ruleCommand.CommandText = """
            select rule_key,title,severity,message,
              (select revision_id from clinical_alert_rule_revisions revision where revision.rule_key=clinical_alert_rules.rule_key order by revision_id desc limit 1) as revision_id
            from clinical_alert_rules
            where active=true and trigger_type='encounter' and target_type='banner' and rule_key='ALLERGY_REVIEW'
            order by sequence,rule_key;
            """;
        var rules = new List<(string Key, string Title, string Severity, string Message, long? RevisionId)>();
        {
            await using var reader = await ruleCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rules.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetInt64(4)));
            }
        }

        var alerts = new List<EncounterClinicalAlertItem>();
        foreach (var rule in rules)
        {
            var allergyReviewAcknowledged = rule.RevisionId is not null
                && await IsOpenAcknowledgmentAsync(connection, encounter, rule.Key, rule.RevisionId.Value, allergyStateVersion, cancellationToken);
            if (!hasActiveAllergy && !allergyReviewAcknowledged)
            {
                alerts.Add(new EncounterClinicalAlertItem(
                    rule.Key,
                    rule.Title,
                    rule.Severity,
                    rule.Message,
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
        var ruleRevisionId = await GetActiveAllergyReviewRuleRevisionAsync(connection, transaction, cancellationToken);
        if (ruleRevisionId is null) throw new InvalidOperationException("The allergy-review rule is not active.");
        var allergyStateVersion = await GetAllergyReviewStateVersionAsync(connection, transaction, legacyPid.Value, cancellationToken);
        if (await HasActiveAllergyAsync(connection, legacyPid.Value, cancellationToken, transaction)) throw new InvalidOperationException("The allergy-review condition is not active for this encounter.");

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into encounter_clinical_alert_acknowledgments(encounter,rule_key,rule_revision_id,allergy_state_version,acknowledged_at,acknowledged_by,reopened_at,reopened_by)
            values(@encounter,@rule,@revision,@stateVersion,now(),@username,null,null)
            on conflict(encounter,rule_key,rule_revision_id,allergy_state_version) do update set
              acknowledged_at=excluded.acknowledged_at,
              acknowledged_by=excluded.acknowledged_by,
              reopened_at=null,
              reopened_by=null
            where encounter_clinical_alert_acknowledgments.reopened_at is not null;
            """;
        command.Parameters.AddWithValue("encounter", encounter);
        command.Parameters.AddWithValue("rule", "ALLERGY_REVIEW");
        command.Parameters.AddWithValue("revision", ruleRevisionId.Value);
        command.Parameters.AddWithValue("stateVersion", allergyStateVersion);
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
        var legacyPid = await GetEncounterPatientAsync(connection, transaction, encounter, cancellationToken);
        if (legacyPid is null) return null;
        var ruleRevisionId = await GetActiveAllergyReviewRuleRevisionAsync(connection, transaction, cancellationToken);
        if (ruleRevisionId is null) throw new InvalidOperationException("The allergy-review rule is not active.");
        var allergyStateVersion = await GetAllergyReviewStateVersionAsync(connection, transaction, legacyPid.Value, cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "update encounter_clinical_alert_acknowledgments set reopened_at=now(),reopened_by=@username where encounter=@encounter and rule_key='ALLERGY_REVIEW' and rule_revision_id=@revision and allergy_state_version=@stateVersion and reopened_at is null;";
        command.Parameters.AddWithValue("encounter", encounter);
        command.Parameters.AddWithValue("revision", ruleRevisionId.Value);
        command.Parameters.AddWithValue("stateVersion", allergyStateVersion);
        command.Parameters.AddWithValue("username", username);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetEncounterAlertsAsync(encounter, cancellationToken);
    }

    public async Task<EncounterClinicalAlertHistoryResponse?> GetHistoryAsync(int encounter, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "select exists(select 1 from encounters where encounter=@encounter);";
        existsCommand.Parameters.AddWithValue("encounter", encounter);
        if (!(bool)(await existsCommand.ExecuteScalarAsync(cancellationToken) ?? false)) return null;

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select a.rule_key,revision.title,a.rule_revision_id,a.allergy_state_version,a.acknowledged_at,a.acknowledged_by,a.reopened_at,a.reopened_by
            from encounter_clinical_alert_acknowledgments a
            join clinical_alert_rule_revisions revision on revision.revision_id=a.rule_revision_id
            where a.encounter=@encounter
            order by a.acknowledged_at desc,a.rule_key;
            """;
        command.Parameters.AddWithValue("encounter", encounter);
        var acknowledgements = new List<EncounterClinicalAlertAcknowledgementItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            acknowledgements.Add(new EncounterClinicalAlertAcknowledgementItem(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetInt32(3),
                reader.GetFieldValue<DateTimeOffset>(4).ToString("O"),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6).ToString("O"),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return new EncounterClinicalAlertHistoryResponse(encounter, acknowledgements);
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

    private static async Task<bool> IsOpenAcknowledgmentAsync(NpgsqlConnection connection, int encounter, string ruleKey, long ruleRevisionId, int allergyStateVersion, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select exists(select 1 from encounter_clinical_alert_acknowledgments where encounter=@encounter and rule_key=@rule and rule_revision_id=@revision and allergy_state_version=@stateVersion and reopened_at is null);";
        command.Parameters.AddWithValue("encounter", encounter);
        command.Parameters.AddWithValue("rule", ruleKey);
        command.Parameters.AddWithValue("revision", ruleRevisionId);
        command.Parameters.AddWithValue("stateVersion", allergyStateVersion);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<long?> GetActiveAllergyReviewRuleRevisionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select revision.revision_id
            from clinical_alert_rules alert_rule
            join lateral (
              select revision_id
              from clinical_alert_rule_revisions revision
              where revision.rule_key=alert_rule.rule_key
              order by revision.revision_id desc limit 1
            ) revision on true
            where alert_rule.rule_key='ALLERGY_REVIEW' and alert_rule.active=true and alert_rule.trigger_type='encounter' and alert_rule.target_type='banner'
            for update of alert_rule;
            """;
        return await command.ExecuteScalarAsync(cancellationToken) is long revisionId ? revisionId : null;
    }

    private static async Task<int> GetAllergyReviewStateVersionAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, int legacyPid, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = transaction is null
            ? "select state_version from patient_allergy_review_states where pid=@pid;"
            : "select state_version from patient_allergy_review_states where pid=@pid for update;";
        command.Parameters.AddWithValue("pid", legacyPid);
        return await command.ExecuteScalarAsync(cancellationToken) is int version
            ? version
            : throw new InvalidOperationException("The allergy review state is not initialized for this patient.");
    }
}
