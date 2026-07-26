using System.Globalization;
using System.Net;
using System.Text;
using Npgsql;

namespace AvenChart.Api.Data;

public sealed class PatientPrintRepository(NpgsqlDataSource dataSource)
{
    public async Task<string?> RenderAsync(string patientId, string output, Guid? referralId, int? encounterId, int? labelCount, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, patientId, cancellationToken);
        if (patient is null) return null;

        return output.Trim().ToLowerInvariant() switch
        {
            "demographics" => Document("Patient demographics", Demographics(patient)),
            "chart-labels" => Document("Patient chart labels", ChartLabels(patient, Math.Clamp(labelCount ?? 30, 1, 60))),
            "address-label" => Document("Patient address label", AddressLabel(patient)),
            "referral" => await ReferralAsync(connection, patient, referralId, cancellationToken),
            "fee-sheet" => await FeeSheetAsync(connection, patient, encounterId, cancellationToken),
            _ => throw new ArgumentException("Output must be demographics, chart-labels, address-label, referral, or fee-sheet.")
        };
    }

    private static async Task<Patient?> GetPatientAsync(NpgsqlConnection connection, string patientId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select canonical_id, legacy_pid, pubpid, first_name, last_name, date_of_birth, sex, street, city, state, postal_code, phone_home, phone_cell, email from patients where lower(canonical_id)=lower(@id) or lower(pubpid)=lower(@id) limit 1;";
        command.Parameters.AddWithValue("id", patientId.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(reader.GetString(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetFieldValue<DateOnly>(5), Text(reader, 6), Text(reader, 7), Text(reader, 8), Text(reader, 9), Text(reader, 10), Text(reader, 11), Text(reader, 12), Text(reader, 13));
    }

    private static string Demographics(Patient p) => $"<h1>Patient demographics</h1><p class='muted'>Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC</p><section><h2>{E(p.Name)}</h2><dl><dt>Patient ID</dt><dd>{E(p.Pubpid)} ({E(p.CanonicalId)})</dd><dt>Date of birth</dt><dd>{p.DateOfBirth:yyyy-MM-dd}</dd><dt>Sex</dt><dd>{E(p.Sex)}</dd><dt>Address</dt><dd>{E(p.Address)}</dd><dt>Home phone</dt><dd>{E(p.PhoneHome)}</dd><dt>Mobile phone</dt><dd>{E(p.PhoneCell)}</dd><dt>Email</dt><dd>{E(p.Email)}</dd></dl></section>";

    private static string ChartLabels(Patient p, int count)
    {
        var label = $"<strong>{E(p.Name)}</strong><br>DOB: {p.DateOfBirth:yyyy-MM-dd}<br>Printed: {DateTime.UtcNow:yyyy-MM-dd}<br>ID: {E(p.Pubpid)}";
        return $"<h1>Patient chart labels</h1><p class='muted'>{count} labels; choose the configured label stock in the print dialog.</p><div class='labels'>{string.Concat(Enumerable.Repeat($"<div class='label'>{label}</div>", count))}</div>";
    }

    private static string AddressLabel(Patient p) => $"<h1>Patient address label</h1><div class='address-label'><strong>{E(p.Name)}</strong><br>{E(p.Street)}<br>{E(string.Join(", ", new[] { p.City, p.State }.Where(x => !string.IsNullOrWhiteSpace(x))))} {E(p.PostalCode)}</div>";

    private static async Task<string> ReferralAsync(NpgsqlConnection connection, Patient p, Guid? referralId, CancellationToken cancellationToken)
    {
        if (referralId is null) throw new ArgumentException("A referral ID is required for referral output.");
        await using var command = connection.CreateCommand();
        command.CommandText = "select destination, reason, status, external_reference, notes, requested_at from referrals where id=@id and patient_id=@patientId;"; command.Parameters.AddWithValue("id", referralId.Value); command.Parameters.AddWithValue("patientId", p.CanonicalId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new KeyNotFoundException("Referral was not found for this patient.");
        var requested = reader.GetFieldValue<DateTimeOffset>(5);
        return Document("Referral form", $"<h1>Referral form</h1><section><h2>Patient</h2><p><strong>{E(p.Name)}</strong><br>ID: {E(p.Pubpid)}<br>DOB: {p.DateOfBirth:yyyy-MM-dd}<br>Address: {E(p.Address)}<br>Phone: {E(p.PhoneCell ?? p.PhoneHome)}</p></section><section><h2>Referral</h2><dl><dt>Referred to</dt><dd>{E(reader.GetString(0))}</dd><dt>Reason</dt><dd>{E(reader.GetString(1))}</dd><dt>Status</dt><dd>{E(reader.GetString(2))}</dd><dt>Requested</dt><dd>{requested:yyyy-MM-dd}</dd><dt>Reference</dt><dd>{E(Text(reader, 3))}</dd><dt>Notes</dt><dd>{E(Text(reader, 4))}</dd></dl></section><p class='signature'>Referring clinician signature: ____________________________________</p>");
    }

    private static async Task<string> FeeSheetAsync(NpgsqlConnection connection, Patient p, int? encounterId, CancellationToken cancellationToken)
    {
        if (encounterId is null) throw new ArgumentException("An encounter ID is required for fee-sheet output.");
        await using var header = connection.CreateCommand();
        header.CommandText = "select e.encounter_date, e.reason, trim(concat(s.first_name, ' ', s.last_name)), f.name from encounters e left join staff s on s.id=e.provider_id left join facilities f on f.id=e.facility_id where e.encounter=@encounter and e.pid=@pid;"; header.Parameters.AddWithValue("encounter", encounterId.Value); header.Parameters.AddWithValue("pid", p.LegacyPid);
        await using var reader = await header.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new KeyNotFoundException("Encounter was not found for this patient.");
        var date = reader.GetFieldValue<DateOnly>(0); var reason = Text(reader, 1); var provider = Text(reader, 2); var facility = Text(reader, 3);
        await reader.DisposeAsync();
        await using var lines = connection.CreateCommand(); lines.CommandText = "select code_type, code, modifier, code_text, fee, units, justify from billing where pid=@pid and encounter=@encounter and activity=1 order by id;"; lines.Parameters.AddWithValue("pid", p.LegacyPid); lines.Parameters.AddWithValue("encounter", encounterId.Value);
        await using var lineReader = await lines.ExecuteReaderAsync(cancellationToken); var rows = new StringBuilder(); decimal total = 0;
        while (await lineReader.ReadAsync(cancellationToken)) { var fee = lineReader.IsDBNull(4) ? 0m : lineReader.GetDecimal(4); var units = lineReader.IsDBNull(5) ? 1 : lineReader.GetInt32(5); total += fee * units; rows.Append($"<tr><td>{E(Text(lineReader, 0))}</td><td>{E(Text(lineReader, 1))}</td><td>{E(Text(lineReader, 2))}</td><td>{E(Text(lineReader, 3))}</td><td>{units}</td><td>{fee.ToString("C", CultureInfo.InvariantCulture)}</td></tr>"); }
        return Document("Superbill / fee sheet", $"<h1>Superbill / fee sheet</h1><section><strong>{E(p.Name)}</strong><br>ID: {E(p.Pubpid)} · DOB: {p.DateOfBirth:yyyy-MM-dd}<br>Encounter: {encounterId} · {date:yyyy-MM-dd}<br>Provider: {E(provider)} · Facility: {E(facility)}<br>Reason: {E(reason)}</section><table><thead><tr><th>Type</th><th>Code</th><th>Modifier</th><th>Description</th><th>Units</th><th>Fee</th></tr></thead><tbody>{rows}</tbody><tfoot><tr><th colspan='5'>Total</th><th>{total.ToString("C", CultureInfo.InvariantCulture)}</th></tr></tfoot></table>");
    }

    private static string Document(string title, string body) => $"<!doctype html><html><head><meta charset='utf-8'><title>{E(title)}</title><style>body{{font:14px Arial,sans-serif;margin:30px;color:#111}}h1{{font-size:23px}}h2{{font-size:17px}}section{{border:1px solid #bbb;padding:14px;margin:14px 0}}dl{{display:grid;grid-template-columns:150px 1fr;gap:8px}}dt{{font-weight:bold}}dd{{margin:0;white-space:pre-wrap}}table{{border-collapse:collapse;width:100%;margin-top:16px}}th,td{{border:1px solid #999;padding:7px;text-align:left}}.muted{{color:#555}}.signature{{margin-top:48px}}.labels{{display:grid;grid-template-columns:repeat(3,1fr);gap:4mm}}.label{{border:1px dashed #999;min-height:22mm;padding:4mm;font-size:12px}}.address-label{{border:1px dashed #999;width:85mm;min-height:35mm;padding:10mm;font-size:16px}}@media print{{body{{margin:8mm}}h1,.muted{{display:none}}.label{{break-inside:avoid}}}}</style></head><body>{body}</body></html>";
    private static string? Text(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "—");
    private sealed record Patient(string CanonicalId, int LegacyPid, string Pubpid, string FirstName, string LastName, DateOnly DateOfBirth, string? Sex, string? Street, string? City, string? State, string? PostalCode, string? PhoneHome, string? PhoneCell, string? Email) { public string Name => $"{FirstName} {LastName}"; public string Address => string.Join(", ", new[] { Street, City, State, PostalCode }.Where(x => !string.IsNullOrWhiteSpace(x))); }
}
