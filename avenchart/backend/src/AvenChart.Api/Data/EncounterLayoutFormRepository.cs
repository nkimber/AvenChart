// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class EncounterLayoutFormRepository(NpgsqlDataSource dataSource)
{
    public async Task<EncounterLayoutFormCatalogResponse?> GetAvailableAsync(int encounter, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (!await EncounterExistsAsync(connection, encounter, cancellationToken)) return null;
        await using var command = connection.CreateCommand(); command.CommandText = "select layout_key,title from form_layouts where active=true and lower(mapping)='encounter' order by sequence,layout_key;";
        var forms = new List<EncounterLayoutFormCatalogItem>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) forms.Add(new(reader.GetString(0), reader.GetString(1)));
        return new(encounter, forms);
    }

    public async Task<EncounterLayoutFormResponse?> GetAsync(int encounter, string layoutKey, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (!await EncounterExistsAsync(connection, encounter, cancellationToken)) return null;
        var definition = await LoadDefinitionAsync(connection, layoutKey, cancellationToken);
        if (definition is null) return null;
        return await AttachLatestRecordAsync(connection, encounter, definition, cancellationToken);
    }

    public async Task<EncounterLayoutFormResponse?> SaveAsync(int encounter, string layoutKey, EncounterLayoutFormSaveRequest request, string username, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (!await EncounterExistsAsync(connection, encounter, cancellationToken, transaction)) return null;
        if (await IsEncounterLockedAsync(connection, encounter, cancellationToken, transaction))
        {
            throw new EncounterLockConflictException(
                "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
        }
        var definition = await LoadDefinitionAsync(connection, layoutKey, cancellationToken, transaction);
        if (definition is null) return null;
        var values = ValidateAndNormalize(definition, request.Values);
        var recordId = Guid.NewGuid();
        await using (var insertRecord = connection.CreateCommand())
        {
            insertRecord.Transaction = transaction;
            insertRecord.CommandText = "insert into encounter_layout_form_records(record_id,encounter,layout_key,revision,saved_at,saved_by) values(@id,@encounter,@layout,(select coalesce(max(revision),0)+1 from encounter_layout_form_records where encounter=@encounter and layout_key=@layout),now(),@user);";
            insertRecord.Parameters.AddWithValue("id", recordId); insertRecord.Parameters.AddWithValue("encounter", encounter); insertRecord.Parameters.AddWithValue("layout", definition.LayoutKey); insertRecord.Parameters.AddWithValue("user", username);
            await insertRecord.ExecuteNonQueryAsync(cancellationToken);
        }
        foreach (var field in definition.Groups.SelectMany(group => group.Fields))
        {
            await using var insertValue = connection.CreateCommand(); insertValue.Transaction = transaction;
            insertValue.CommandText = "insert into encounter_layout_form_values(record_id,field_key,field_label,field_value) values(@record,@field,@label,@value);";
            insertValue.Parameters.AddWithValue("record", recordId); insertValue.Parameters.AddWithValue("field", field.Key); insertValue.Parameters.AddWithValue("label", field.Label); insertValue.Parameters.AddWithValue("value", values[field.Key]); await insertValue.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(encounter, definition.LayoutKey, cancellationToken);
    }

    private static async Task<bool> EncounterExistsAsync(NpgsqlConnection connection, int encounter, CancellationToken cancellationToken, NpgsqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "select exists(select 1 from encounters where encounter=@encounter);"; command.Parameters.AddWithValue("encounter", encounter); return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<bool> IsEncounterLockedAsync(NpgsqlConnection connection, int encounter, CancellationToken cancellationToken, NpgsqlTransaction transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select exists(select 1 from encounter_signatures where encounter=@encounter and is_lock);";
        command.Parameters.AddWithValue("encounter", encounter);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static string NormalizeLayoutKey(string key)
    {
        var value = key.Trim().ToUpperInvariant();
        if (value.Length is < 2 or > 32 || !value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_')) throw new ArgumentException("Form key is invalid.");
        return value;
    }

    private static async Task<FormDefinition?> LoadDefinitionAsync(NpgsqlConnection connection, string requestedLayoutKey, CancellationToken cancellationToken, NpgsqlTransaction? transaction = null)
    {
        var layoutKey = NormalizeLayoutKey(requestedLayoutKey);
        await using var layoutCommand = connection.CreateCommand(); layoutCommand.Transaction = transaction; layoutCommand.CommandText = "select title from form_layouts where layout_key=@key and active=true;"; layoutCommand.Parameters.AddWithValue("key", layoutKey);
        var title = await layoutCommand.ExecuteScalarAsync(cancellationToken) as string; if (title is null) return null;
        await using var fieldCommand = connection.CreateCommand(); fieldCommand.Transaction = transaction;
        fieldCommand.CommandText = "select g.group_key,g.title,f.field_key,f.label,f.field_type,f.required,f.max_length,f.list_id,f.default_value from form_layout_groups g join form_layout_fields f on f.layout_key=g.layout_key and f.group_key=g.group_key where g.layout_key=@key and g.active=true and f.active=true order by g.sequence,g.group_key,f.sequence,f.field_key;"; fieldCommand.Parameters.AddWithValue("key", layoutKey);
        var fields = new List<FieldDefinition>(); await using var reader = await fieldCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) fields.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetBoolean(5), reader.GetInt32(6), reader.IsDBNull(7) ? "" : reader.GetString(7), reader.IsDBNull(8) ? "" : reader.GetString(8), []));
        await reader.CloseAsync();
        foreach (var field in fields.Where(field => !string.IsNullOrWhiteSpace(field.ListId)))
        {
            await using var optionCommand = connection.CreateCommand(); optionCommand.Transaction = transaction; optionCommand.CommandText = "select option_key,title,option_value,is_default from form_option_values where list_key=@list and active=true order by sequence,option_key;"; optionCommand.Parameters.AddWithValue("list", field.ListId);
            await using var optionReader = await optionCommand.ExecuteReaderAsync(cancellationToken); while (await optionReader.ReadAsync(cancellationToken)) field.Options.Add(new(optionReader.GetString(0), optionReader.GetString(1), optionReader.GetString(2), optionReader.GetBoolean(3)));
        }
        var groups = fields.GroupBy(field => new { field.GroupKey, field.GroupTitle }).Select(group => new EncounterLayoutFormGroup(group.Key.GroupKey, group.Key.GroupTitle, group.Select(field => new EncounterLayoutFormField(field.Key, field.GroupKey, field.Label, field.Type, field.Required, field.MaxLength, field.DefaultValue, field.Options)).ToList())).ToList();
        return new(layoutKey, title, groups);
    }

    private static Dictionary<string, string> ValidateAndNormalize(FormDefinition definition, IReadOnlyDictionary<string, string?> requestedValues)
    {
        var fields = definition.Groups.SelectMany(group => group.Fields).ToDictionary(field => field.Key, StringComparer.Ordinal);
        if (requestedValues.Keys.Any(key => !fields.ContainsKey(key))) throw new ArgumentException("Form submission contains an unknown field.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in fields.Values)
        {
            var value = requestedValues.TryGetValue(field.Key, out var submitted) ? submitted?.Trim() ?? "" : field.DefaultValue;
            if (field.Required && string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{field.Label} is required.");
            if (value.Length > field.MaxLength && field.MaxLength > 0) throw new ArgumentException($"{field.Label} exceeds its maximum length.");
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (field.FieldType == "date" && !DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new ArgumentException($"{field.Label} must be a date.");
                if (field.FieldType == "number" && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) throw new ArgumentException($"{field.Label} must be a number.");
                if (field.FieldType == "checkbox" && value is not ("true" or "false")) throw new ArgumentException($"{field.Label} must be true or false.");
                if (field.FieldType == "select" && !field.Options.Any(option => option.Key == value)) throw new ArgumentException($"{field.Label} has an invalid option.");
            }
            values[field.Key] = value;
        }
        return values;
    }

    private static async Task<EncounterLayoutFormResponse> AttachLatestRecordAsync(NpgsqlConnection connection, int encounter, FormDefinition definition, CancellationToken cancellationToken)
    {
        await using var recordCommand = connection.CreateCommand(); recordCommand.CommandText = "select record_id,revision,saved_at,saved_by from encounter_layout_form_records where encounter=@encounter and layout_key=@layout order by revision desc limit 1;"; recordCommand.Parameters.AddWithValue("encounter", encounter); recordCommand.Parameters.AddWithValue("layout", definition.LayoutKey);
        await using var recordReader = await recordCommand.ExecuteReaderAsync(cancellationToken); if (!await recordReader.ReadAsync(cancellationToken)) return new(encounter, definition.LayoutKey, definition.Title, definition.Groups, null);
        var recordId = recordReader.GetGuid(0); var revision = recordReader.GetInt32(1); var savedAt = recordReader.GetFieldValue<DateTimeOffset>(2).ToString("O"); var savedBy = recordReader.GetString(3); await recordReader.CloseAsync();
        await using var valueCommand = connection.CreateCommand(); valueCommand.CommandText = "select field_key,field_value from encounter_layout_form_values where record_id=@record;"; valueCommand.Parameters.AddWithValue("record", recordId);
        var values = new Dictionary<string, string>(StringComparer.Ordinal); await using var valueReader = await valueCommand.ExecuteReaderAsync(cancellationToken); while (await valueReader.ReadAsync(cancellationToken)) values[valueReader.GetString(0)] = valueReader.GetString(1);
        return new(encounter, definition.LayoutKey, definition.Title, definition.Groups, new(recordId, revision, savedAt, savedBy, values));
    }

    private sealed class FieldDefinition(string groupKey, string groupTitle, string key, string label, string type, bool required, int maxLength, string listId, string defaultValue, List<EncounterLayoutFormOption> options)
    {
        public string GroupKey { get; } = groupKey; public string GroupTitle { get; } = groupTitle; public string Key { get; } = key; public string Label { get; } = label; public string Type { get; } = type; public bool Required { get; } = required; public int MaxLength { get; } = maxLength; public string ListId { get; } = listId; public string DefaultValue { get; } = defaultValue; public List<EncounterLayoutFormOption> Options { get; } = options;
    }
    private sealed record FormDefinition(string LayoutKey, string Title, IReadOnlyList<EncounterLayoutFormGroup> Groups);
}
