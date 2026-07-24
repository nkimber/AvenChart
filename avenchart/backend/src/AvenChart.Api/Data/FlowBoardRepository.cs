using System.Globalization;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class FlowBoardRepository(NpgsqlDataSource dataSource)
{
    public async Task<FlowBoardResponse> GetAsync(string? date, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var requestedDate = DateOnly.TryParse(date, CultureInfo.InvariantCulture, out var parsedDate)
            ? parsedDate
            : await GetBaseDateAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select a.id, p.canonical_id, p.first_name || ' ' || p.last_name, a.start_time, a.title, a.room,
              s.first_name || ' ' || s.last_name, f.name, a.status
            from appointments a
            join patients p on p.legacy_pid = a.pid
            left join staff s on s.id = a.provider_id
            left join facilities f on f.id = a.facility_id
            where a.appointment_date = @date
            order by a.start_time, a.id;
            """;
        command.Parameters.AddWithValue("date", requestedDate);
        var lanes = new Dictionary<string, List<FlowBoardItem>>(StringComparer.OrdinalIgnoreCase)
        {
            ["scheduled"] = [], ["arrived"] = [], ["in-room"] = [], ["complete"] = [], ["other"] = [],
        };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var status = reader.IsDBNull(8) ? null : reader.GetString(8);
            var flowStatus = ToFlowStatus(status);
            lanes[flowStatus].Add(new FlowBoardItem(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetFieldValue<TimeOnly>(3).ToString("HH:mm", CultureInfo.InvariantCulture),
                reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7), status, flowStatus));
        }
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        return new FlowBoardResponse(metadata.DatasetId, metadata.Version, requestedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), [
            new FlowBoardLane("scheduled", "Scheduled", lanes["scheduled"]), new FlowBoardLane("arrived", "Arrived", lanes["arrived"]),
            new FlowBoardLane("in-room", "In room", lanes["in-room"]), new FlowBoardLane("complete", "Complete", lanes["complete"]),
            new FlowBoardLane("other", "Other", lanes["other"]),
        ]);
    }

    private static string ToFlowStatus(string? status) => status?.Trim() switch
    {
        "@" or "arrived" or "checked-in" => "arrived",
        ">" or "in-room" => "in-room",
        "<" or "checked-out" or "complete" => "complete",
        "-" or "scheduled" or null or "" => "scheduled",
        _ => "other",
    };

    private static async Task<DateOnly> GetBaseDateAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.CommandText = "select base_date from dataset_metadata order by generated_at desc limit 1;";
        return (DateOnly)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<(string DatasetId, string Version)> GetMetadataAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.CommandText = "select dataset_id, version from dataset_metadata order by generated_at desc limit 1;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); await reader.ReadAsync(cancellationToken);
        return (reader.GetString(0), reader.GetString(1));
    }
}
