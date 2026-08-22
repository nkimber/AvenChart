// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;
using AvenChart.Api.Security;

namespace AvenChart.Api.Data;

public sealed class PatientPortalRepository(NpgsqlDataSource dataSource)
{
    private const string InvalidCredentialsMessage = "Invalid username or password.";
    private const string SessionSource = "avenchart-portal";
    private const string GeneratedMedicalReportCreatedEventType = "generated_report";
    private const string GeneratedMedicalReportPdfDownloadedEventType = "pdf_downloaded";
    private const string GeneratedMedicalReportPackageDownloadedEventType = "package_downloaded";
    private const string PortalMessageComposedEventType = "message_composed";
    private const string PortalMessageRepliedEventType = "message_replied";
    private const string PortalMessageForwardedEventType = "message_forwarded";
    private const string PortalPrescriptionRefillRequestedEventType = "prescription_refill_requested";
    private const string PortalMessageReadEventType = "message_read";
    private const string PortalMessageArchivedEventType = "message_archived";
    private const string PortalMessagesArchivedEventType = "messages_archived";
    private const string ProtectedPortalMessageBody = "Encrypted secure message body is protected.";
    private const int MaximumPortalMessageAttachmentCount = 5;
    private const int MaximumPortalMessageAttachmentBytes = 4 * 1024 * 1024;
    private const int MaximumPortalMessageAttachmentTotalBytes = 10 * 1024 * 1024;
    private static readonly PatientPortalMessageSubjectOption[] PortalMessageSubjectOptions =
    [
        new("General", "General", true),
        new("Insurance", "Insurance", false),
        new("Prior Auth", "Prior Auth", false),
        new("Bill/Collect", "Bill/Collect", false),
        new("Referral", "Referral", false),
        new("Pharmacy", "Pharmacy", false)
    ];
    private static readonly JsonSerializerOptions GeneratedMedicalReportPackageJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private static readonly JsonSerializerOptions PortalProfileChangeJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly PatientPortalAppointmentCategoryOption[] AppointmentRequestCategoryOptions =
    [
        new(5, "Office Visit", "office_visit", 15),
        new(9, "Established Patient", "established_patient", 15),
        new(10, "New Patient", "new_patient", 30),
        new(12, "Health and Behavioral Assessment", "health_and_behavioral_assessment", 15),
        new(13, "Preventive Care Services", "preventive_care_services", 15),
        new(14, "Ophthalmological Services", "ophthalmological_services", 15)
    ];
    private static readonly PatientPortalMedicalReportSection[] MedicalReportSections =
    [
        new("demographics", "Demographics", "Core", true),
        new("history", "History", "Core", false),
        new("insurance", "Insurance", "Core", false),
        new("billing", "Billing", "Core", true),
        new("allergies", "Allergies", "Clinical", false),
        new("medications", "Medications", "Clinical", false),
        new("immunizations", "Immunizations", "Clinical", false),
        new("medical_problems", "Medical Problems", "Clinical", false),
        new("notes", "Patient Notes", "Clinical", false),
        new("transactions", "Transactions", "Clinical", false),
        new("batchcom", "Communications", "Clinical", false)
    ];

    public async Task<PatientPortalLoginResponse> LoginAsync(
        PatientPortalLoginRequest request,
        CancellationToken cancellationToken)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Failed(username, InvalidCredentialsMessage);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              p.canonical_id,
              p.legacy_pid,
              p.pubpid,
              p.first_name,
              p.last_name,
              p.portal_enabled,
              ppa.portal_username,
              ppa.portal_login_username,
              ppa.password_salt,
              ppa.password_hash,
              ppa.password_status,
              ppa.one_time_token
            from patient_portal_accounts ppa
            join patients p on p.canonical_id = ppa.patient_id
            where lower(ppa.portal_login_username) = lower(@username)
            limit 1;
            """;
        command.Parameters.AddWithValue("username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return Failed(username, InvalidCredentialsMessage);
        }

        var account = ReadPortalAccount(reader);
        await reader.DisposeAsync();

        if (string.IsNullOrWhiteSpace(account.PortalUsername)
            || string.IsNullOrWhiteSpace(account.PortalLoginUsername)
            || string.IsNullOrWhiteSpace(account.PasswordHash))
        {
            return Failed(username, InvalidCredentialsMessage);
        }

        if (!string.IsNullOrWhiteSpace(account.OneTimeToken))
        {
            return Failed(username, "One-time reset pending.");
        }

        if (account.PasswordStatus != 1)
        {
            return Failed(username, "Patient portal account is pending password setup.");
        }

        if (!account.PortalEnabled)
        {
            return Failed(username, "Patient portal access is disabled.");
        }

        if (!PasswordHashing.Verify(account.PasswordHash, account.PasswordSalt, request.Password))
        {
            return Failed(username, InvalidCredentialsMessage);
        }

        if (PasswordHashing.RequiresUpgrade(account.PasswordHash))
        {
            await UpgradePasswordHashAsync(connection, account.CanonicalId, request.Password, cancellationToken);
        }

        var session = await CreateSessionAsync(connection, account, cancellationToken);
        return new PatientPortalLoginResponse(
            Authenticated: true,
            Username: account.PortalLoginUsername,
            PortalUsername: account.PortalUsername,
            CanonicalId: account.CanonicalId,
            LegacyPid: account.LegacyPid,
            Pubpid: account.Pubpid,
            DisplayName: account.DisplayName,
            FailureReason: null,
            SessionId: session.SessionId,
            SessionCreatedAt: session.CreatedAt,
            SessionExpiresAt: session.ExpiresAt,
            SessionSource: SessionSource);
    }

    /// <summary>
    /// Resolves a signature-, issuer-, audience-, and lifetime-validated OIDC
    /// subject through an explicit portal mapping. The browser never supplies a
    /// patient identifier. Only the SHA-256 fingerprint of the bearer is held
    /// in the derived, token-bounded portal session so logout can prevent that
    /// same bearer from silently creating a replacement session.
    /// </summary>
    public async Task<Guid?> ResolveExternalSessionAsync(
        string providerId,
        string externalSubject,
        DateTimeOffset expiresAt,
        byte[] tokenFingerprint,
        CancellationToken cancellationToken)
    {
        var normalizedProviderId = providerId?.Trim().ToLowerInvariant();
        var normalizedSubject = externalSubject?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedProviderId) || normalizedProviderId.Length > 80 ||
            string.IsNullOrWhiteSpace(normalizedSubject) || normalizedSubject.Length > 512 ||
            normalizedSubject != externalSubject || normalizedSubject.Any(char.IsControl) ||
            tokenFingerprint is null || tokenFingerprint.Length != 32 || expiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        Guid mappingId;
        PatientPortalAccountRow account;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                  mapping.mapping_id,
                  p.canonical_id,
                  p.legacy_pid,
                  p.pubpid,
                  p.first_name,
                  p.last_name,
                  p.portal_enabled,
                  ppa.portal_username,
                  ppa.portal_login_username,
                  ppa.password_salt,
                  ppa.password_hash,
                  ppa.password_status,
                  ppa.one_time_token
                from patient_portal_external_identity_mappings mapping
                join patients p on p.canonical_id=mapping.patient_id
                join patient_portal_accounts ppa on ppa.patient_id=p.canonical_id
                where mapping.provider_id=@providerId
                  and mapping.external_subject=@externalSubject
                  and mapping.active=true
                  and p.portal_enabled=true
                  and nullif(btrim(ppa.portal_username),'') is not null
                  and nullif(btrim(ppa.portal_login_username),'') is not null
                limit 1;
                """;
            command.Parameters.AddWithValue("providerId", normalizedProviderId);
            command.Parameters.AddWithValue("externalSubject", normalizedSubject);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            mappingId = reader.GetGuid(reader.GetOrdinal("mapping_id"));
            account = ReadPortalAccount(reader);
        }

        await using (var ended = connection.CreateCommand())
        {
            ended.CommandText = """
                select exists(
                  select 1
                  from patient_portal_sessions
                  where external_identity_mapping_id=@mappingId
                    and external_token_fingerprint=@tokenFingerprint
                    and ended_at is not null);
                """;
            ended.Parameters.AddWithValue("mappingId", mappingId);
            ended.Parameters.AddWithValue("tokenFingerprint", tokenFingerprint);
            if (await ended.ExecuteScalarAsync(cancellationToken) is true)
            {
                return null;
            }
        }

        await using (var existing = connection.CreateCommand())
        {
            existing.CommandText = """
                select id
                from patient_portal_sessions
                where external_identity_mapping_id=@mappingId
                  and external_token_fingerprint=@tokenFingerprint
                  and ended_at is null
                  and expires_at > now()
                order by created_at desc
                limit 1;
                """;
            existing.Parameters.AddWithValue("mappingId", mappingId);
            existing.Parameters.AddWithValue("tokenFingerprint", tokenFingerprint);
            if (await existing.ExecuteScalarAsync(cancellationToken) is Guid existingSessionId)
            {
                return existingSessionId;
            }
        }

        var session = await CreateSessionAsync(
            connection,
            account,
            cancellationToken,
            expiresAt,
            $"oidc:{normalizedProviderId}",
            mappingId,
            tokenFingerprint);
        return session.SessionId;
    }

    public async Task<PatientPortalSessionResponse> GetCurrentSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update patient_portal_sessions session
            set last_seen_at = now()
            from patients patient
            where session.id = @session_id
              and session.patient_id = patient.canonical_id
              and patient.portal_enabled = true
              and session.ended_at is null
              and session.expires_at > now()
              and (
                session.external_identity_mapping_id is null
                or exists (
                  select 1
                  from patient_portal_external_identity_mappings mapping
                  where mapping.mapping_id=session.external_identity_mapping_id
                    and mapping.active=true
                )
              )
            returning session.id, session.patient_id, session.pid, session.portal_username, session.portal_login_username,
                      session.created_at, session.last_seen_at, session.expires_at, session.ended_at, session.session_source;
            """;
        command.Parameters.AddWithValue("session_id", sessionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return InactiveSession(sessionId, "Session is not active.");
        }

        var row = ReadSessionRow(reader);
        await reader.DisposeAsync();

        await using var patientCommand = connection.CreateCommand();
        patientCommand.CommandText = """
            select pubpid, first_name, last_name
            from patients
            where canonical_id = @patient_id
            limit 1;
            """;
        patientCommand.Parameters.AddWithValue("patient_id", row.CanonicalId);

        await using var patientReader = await patientCommand.ExecuteReaderAsync(cancellationToken);
        if (!await patientReader.ReadAsync(cancellationToken))
        {
            return row.ToResponse(authenticated: false, "Session patient was not found.", string.Empty, string.Empty);
        }

        var pubpid = patientReader.GetString(patientReader.GetOrdinal("pubpid"));
        var displayName = $"{patientReader.GetString(patientReader.GetOrdinal("last_name"))}, {patientReader.GetString(patientReader.GetOrdinal("first_name"))}";
        return row.ToResponse(authenticated: true, null, pubpid, displayName);
    }

    public async Task<PatientPortalSessionResponse> EndSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update patient_portal_sessions
            set ended_at = coalesce(ended_at, now()),
                last_seen_at = now()
            where id = @session_id
            returning id, patient_id, pid, portal_username, portal_login_username, created_at, last_seen_at, expires_at, ended_at, session_source;
            """;
        command.Parameters.AddWithValue("session_id", sessionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return InactiveSession(sessionId, "Session is not active.");
        }

        var row = ReadSessionRow(reader);
        await reader.DisposeAsync();

        await using var patientCommand = connection.CreateCommand();
        patientCommand.CommandText = """
            select pubpid, first_name, last_name
            from patients
            where canonical_id = @patient_id
            limit 1;
            """;
        patientCommand.Parameters.AddWithValue("patient_id", row.CanonicalId);

        await using var patientReader = await patientCommand.ExecuteReaderAsync(cancellationToken);
        if (!await patientReader.ReadAsync(cancellationToken))
        {
            return row.ToResponse(authenticated: false, "Session patient was not found.", string.Empty, string.Empty);
        }

        var pubpid = patientReader.GetString(patientReader.GetOrdinal("pubpid"));
        var displayName = $"{patientReader.GetString(patientReader.GetOrdinal("last_name"))}, {patientReader.GetString(patientReader.GetOrdinal("first_name"))}";
        return row.ToResponse(authenticated: false, "Session is not active.", pubpid, displayName);
    }

    public async Task<PatientPortalHomeSummaryResponse> GetHomeSummaryAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyHomeSummary(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var messages = await GetMessageSummaryAsync(connection, session.PortalUsername, cancellationToken);
        var appointments = await GetUpcomingAppointmentsAsync(
            connection,
            session.LegacyPid.Value,
            metadata.BaseDate,
            cancellationToken);
        var immunizations = await GetPortalHomeImmunizationsAsync(
            connection,
            session.LegacyPid.Value,
            cancellationToken);

        return new PatientPortalHomeSummaryResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            Messages: messages,
            UpcomingAppointmentCount: appointments.TotalCount,
            UpcomingAppointments: appointments.Items,
            ImmunizationCount: immunizations.Count,
            Immunizations: immunizations,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalProfileResponse> GetProfileAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyProfile(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var demographics = await GetPortalProfileDemographicsAsync(
            connection,
            session.CanonicalId,
            session.LegacyPid.Value,
            cancellationToken);
        if (demographics is null)
        {
            return EmptyProfile(session, "Session patient was not found.");
        }

        var insurance = await GetPortalProfileInsuranceAsync(
            connection,
            session.LegacyPid.Value,
            cancellationToken);
        var pendingChange = await GetPendingProfileChangeAsync(
            connection,
            session.CanonicalId,
            cancellationToken);

        return new PatientPortalProfileResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            HasPendingProfileChanges: pendingChange is not null,
            Demographics: demographics,
            InsuranceCount: insurance.Count,
            Insurance: insurance,
            PendingChange: pendingChange,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalProfileResponse> SubmitProfileChangeAsync(
        Guid sessionId,
        PatientPortalProfileChangeSubmitRequest request,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyProfile(session, session.FailureReason ?? "Session is not active.");
        }

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var demographics = await GetPortalProfileDemographicsAsync(
                connection,
                session.CanonicalId,
                session.LegacyPid.Value,
                cancellationToken);
            if (demographics is null)
            {
                return EmptyProfile(session, "Session patient was not found.");
            }

            var requestedDemographics = ApplyProfileChangeRequest(demographics, request);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                with existing as (
                  select id
                  from patient_portal_profile_change_requests
                  where patient_id = @patient_id
                    and activity = 'profile'
                    and require_audit = 1
                    and status = 'waiting'
                    and pending_action = 'review'
                  order by created_at, id
                  limit 1
                ),
                updated as (
                  update patient_portal_profile_change_requests
                  set session_id = @session_id,
                      portal_username = @portal_username,
                      portal_login_username = @portal_login_username,
                      narrative = @narrative,
                      table_action = '',
                      requested_changes = @requested_changes,
                      action_user = '0',
                      action_taken = '',
                      action_taken_at = null,
                      checksum = '0',
                      updated_at = now()
                  where id in (select id from existing)
                  returning id
                )
                insert into patient_portal_profile_change_requests (
                  patient_id,
                  pid,
                  session_id,
                  portal_username,
                  portal_login_username,
                  activity,
                  require_audit,
                  pending_action,
                  action_taken,
                  status,
                  narrative,
                  table_action,
                  requested_changes,
                  action_user,
                  checksum
                )
                select
                  @patient_id,
                  @pid,
                  @session_id,
                  @portal_username,
                  @portal_login_username,
                  'profile',
                  1,
                  'review',
                  '',
                  'waiting',
                  @narrative,
                  '',
                  @requested_changes,
                  '0',
                  '0'
                where not exists (select 1 from updated);
                """;
            command.Parameters.Add("patient_id", NpgsqlDbType.Text).Value = session.CanonicalId;
            command.Parameters.AddWithValue("pid", session.LegacyPid.Value);
            command.Parameters.AddWithValue("session_id", session.SessionId!.Value);
            command.Parameters.Add("portal_username", NpgsqlDbType.Text).Value = session.PortalUsername;
            command.Parameters.Add("portal_login_username", NpgsqlDbType.Text).Value = session.Username;
            command.Parameters.Add("narrative", NpgsqlDbType.Text).Value = "Patient request changes to demographics.";
            command.Parameters.Add("requested_changes", NpgsqlDbType.Jsonb).Value =
                JsonSerializer.Serialize(requestedDemographics, PortalProfileChangeJsonOptions);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return await GetProfileAsync(sessionId, cancellationToken);
    }

    public async Task<PatientPortalAppointmentsResponse> GetAppointmentsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyAppointments(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var upcomingAppointments = await GetPortalAppointmentsAsync(
            connection,
            session.LegacyPid.Value,
            metadata.BaseDate,
            PortalAppointmentWindow.Upcoming,
            cancellationToken);
        var pastAppointments = await GetPortalAppointmentsAsync(
            connection,
            session.LegacyPid.Value,
            metadata.BaseDate,
            PortalAppointmentWindow.Past,
            cancellationToken);
        var appointmentRequests = await GetPortalAppointmentRequestsAsync(
            connection,
            session.CanonicalId,
            metadata.BaseDate,
            cancellationToken);

        return new PatientPortalAppointmentsResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            UpcomingAppointmentCount: upcomingAppointments.TotalCount,
            UpcomingAppointments: upcomingAppointments.Items,
            PastAppointmentCount: pastAppointments.TotalCount,
            PastAppointments: pastAppointments.Items,
            AppointmentRequestCount: appointmentRequests.TotalCount,
            AppointmentRequests: appointmentRequests.Items,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalClinicalSummaryResponse> GetClinicalSummaryAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyClinicalSummary(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var problems = await GetPortalProblemsAsync(connection, session.LegacyPid.Value, cancellationToken);
        var allergies = await GetPortalAllergiesAsync(connection, session.LegacyPid.Value, cancellationToken);
        var medications = await GetPortalMedicationsAsync(connection, session.LegacyPid.Value, cancellationToken);
        var prescriptions = await GetPortalPrescriptionsAsync(connection, session.LegacyPid.Value, cancellationToken);

        return new PatientPortalClinicalSummaryResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            ProblemCount: problems.Count,
            Problems: problems,
            AllergyCount: allergies.Count,
            Allergies: allergies,
            MedicationCount: medications.Count,
            Medications: medications,
            PrescriptionCount: prescriptions.Count,
            Prescriptions: prescriptions,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalLabResultsResponse> GetLabResultsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyLabResults(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var orders = await GetPortalLabOrdersAsync(connection, session.LegacyPid.Value, cancellationToken);
        var orderIds = orders.Select(order => order.Id).ToArray();
        var reports = await GetPortalLabReportsAsync(connection, orderIds, cancellationToken);
        var reportIds = reports.Select(report => report.Id).ToArray();
        var results = await GetPortalLabResultsAsync(connection, reportIds, cancellationToken);

        var resultsByReport = results
            .GroupBy(result => result.ReportId)
            .ToDictionary(group => group.Key, group => group.Select(result => result.Result).ToArray());
        var reportsByOrder = reports
            .GroupBy(report => report.OrderId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(report =>
                    {
                        var reportResults = resultsByReport.GetValueOrDefault(report.Id, []);
                        return new PatientPortalLabReportItem(
                            Id: report.Id.ToString(),
                            DateCollected: report.DateCollected,
                            ReportDate: report.ReportDate,
                            SpecimenNumber: report.SpecimenNumber,
                            ReportStatus: report.ReportStatus,
                            ReviewStatus: report.ReviewStatus,
                            ResultCount: reportResults.Count(),
                            Results: reportResults);
                    })
                    .ToArray());
        var orderItems = orders
            .Select(order =>
            {
                var orderReports = reportsByOrder.GetValueOrDefault(order.Id, []);
                return new PatientPortalLabOrderItem(
                    Id: order.Id.ToString(),
                    OrderDate: order.OrderDate,
                    ProcedureCode: order.ProcedureCode,
                    ProcedureName: order.ProcedureName,
                    OrderStatus: order.OrderStatus,
                    ReportCount: orderReports.Count(),
                    ResultCount: orderReports.Sum(report => report.ResultCount),
                    Reports: orderReports);
            })
            .ToArray();

        return new PatientPortalLabResultsResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            OrderCount: orderItems.Length,
            ReportCount: orderItems.Sum(order => order.ReportCount),
            ResultCount: orderItems.Sum(order => order.ResultCount),
            Orders: orderItems,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalMedicalReportResponse> GetMedicalReportAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyMedicalReport(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var issues = await GetMedicalReportIssuesAsync(connection, session.LegacyPid.Value, cancellationToken);
        var encounters = await GetMedicalReportEncountersAsync(connection, session.LegacyPid.Value, cancellationToken);
        var procedureOrders = await GetMedicalReportProcedureOrdersAsync(connection, session.LegacyPid.Value, cancellationToken);
        var reportPreview = BuildMedicalReportPreview(session, issues, encounters, procedureOrders);

        return new PatientPortalMedicalReportResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            SectionCount: MedicalReportSections.Length,
            SelectedSectionCount: MedicalReportSections.Count(section => section.Selected),
            Sections: MedicalReportSections,
            IssueCount: issues.Count,
            Issues: issues,
            EncounterCount: encounters.Count,
            Encounters: encounters,
            ProcedureOrderCount: procedureOrders.Count,
            ProcedureOrders: procedureOrders,
            ReportPreview: reportPreview,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalGeneratedMedicalReportResponse> GenerateMedicalReportAsync(
        Guid sessionId,
        PatientPortalMedicalReportGenerationRequest request,
        CancellationToken cancellationToken) =>
        await GenerateMedicalReportCoreAsync(sessionId, request, recordGeneratedEvent: true, cancellationToken);

    private async Task<PatientPortalGeneratedMedicalReportResponse> GenerateMedicalReportCoreAsync(
        Guid sessionId,
        PatientPortalMedicalReportGenerationRequest request,
        bool recordGeneratedEvent,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyGeneratedMedicalReport(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var patient = await GetGeneratedMedicalReportPatientAsync(connection, session.CanonicalId, cancellationToken);
        var facility = await GetGeneratedMedicalReportFacilityAsync(connection, cancellationToken);
        var billing = await GetGeneratedMedicalReportBillingAsync(connection, session.LegacyPid.Value, cancellationToken);
        var issues = await GetMedicalReportIssuesAsync(connection, session.LegacyPid.Value, cancellationToken);
        var encounters = await GetMedicalReportEncountersAsync(connection, session.LegacyPid.Value, cancellationToken);
        var procedureOrders = await GetMedicalReportProcedureOrdersAsync(connection, session.LegacyPid.Value, cancellationToken);

        var report = BuildGeneratedMedicalReportResponse(
            session,
            metadata,
            patient,
            facility,
            billing,
            issues,
            encounters,
            procedureOrders,
            request);

        if (recordGeneratedEvent)
        {
            await RecordGeneratedMedicalReportAuditEventAsync(
                connection,
                report,
                GeneratedMedicalReportCreatedEventType,
                artifactName: "generated-medical-report.json",
                artifactContentType: "application/json",
                cancellationToken);
        }

        var auditEvents = await GetGeneratedMedicalReportAuditEventsAsync(connection, session.CanonicalId, cancellationToken);
        return report with
        {
            AuditEventCount = auditEvents.Count,
            AuditEvents = auditEvents
        };
    }

    public async Task<PatientPortalGeneratedMedicalReportPdfPackage> DownloadGeneratedMedicalReportPdfAsync(
        Guid sessionId,
        PatientPortalMedicalReportGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var report = await GenerateMedicalReportCoreAsync(sessionId, request, recordGeneratedEvent: false, cancellationToken);
        if (!report.Authenticated)
        {
            return GeneratedMedicalReportPdfFailure(report.FailureReason ?? "Session is not active.", report);
        }

        var content = BuildGeneratedMedicalReportPdf(report);
        var fileName = BuildGeneratedMedicalReportPdfFileName(report);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RecordGeneratedMedicalReportAuditEventAsync(
            connection,
            report,
            GeneratedMedicalReportPdfDownloadedEventType,
            fileName,
            "application/pdf",
            cancellationToken);
        var auditEvents = await GetGeneratedMedicalReportAuditEventsAsync(connection, report.CanonicalId, cancellationToken);
        report = report with
        {
            AuditEventCount = auditEvents.Count,
            AuditEvents = auditEvents
        };

        return new PatientPortalGeneratedMedicalReportPdfPackage(
            Downloadable: true,
            FileName: fileName,
            ContentType: "application/pdf",
            Content: content,
            ContentLength: content.Length,
            Report: report,
            FailureReason: null);
    }

    public async Task<PatientPortalGeneratedMedicalReportPackageDownload> DownloadGeneratedMedicalReportPackageAsync(
        Guid sessionId,
        PatientPortalMedicalReportGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var report = await GenerateMedicalReportCoreAsync(sessionId, request, recordGeneratedEvent: false, cancellationToken);
        if (!report.Authenticated)
        {
            return GeneratedMedicalReportPackageFailure(report.FailureReason ?? "Session is not active.", report);
        }

        var content = BuildGeneratedMedicalReportPackage(report);
        var fileName = BuildGeneratedMedicalReportPackageFileName(report);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RecordGeneratedMedicalReportAuditEventAsync(
            connection,
            report,
            GeneratedMedicalReportPackageDownloadedEventType,
            fileName,
            "application/zip",
            cancellationToken);
        var auditEvents = await GetGeneratedMedicalReportAuditEventsAsync(connection, report.CanonicalId, cancellationToken);
        report = report with
        {
            AuditEventCount = auditEvents.Count,
            AuditEvents = auditEvents
        };

        return new PatientPortalGeneratedMedicalReportPackageDownload(
            Downloadable: true,
            FileName: fileName,
            ContentType: "application/zip",
            Content: content,
            ContentLength: content.Length,
            Report: report,
            FailureReason: null);
    }

    public async Task<PatientPortalGeneratedMedicalReportAuditResponse> GetMedicalReportAuditAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyGeneratedMedicalReportAudit(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var auditEvents = await GetGeneratedMedicalReportAuditEventsAsync(connection, session.CanonicalId, cancellationToken);

        return new PatientPortalGeneratedMedicalReportAuditResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            AuditEventCount: auditEvents.Count,
            AuditEvents: auditEvents,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalAppointmentRequestOptionsResponse> GetAppointmentRequestOptionsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return AppointmentRequestOptionsFailure(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var patientDefaults = await GetPatientAppointmentRequestDefaultsAsync(connection, session.CanonicalId, cancellationToken);
        var providers = await GetAppointmentRequestProvidersAsync(connection, cancellationToken);
        var facilities = await GetAppointmentRequestFacilitiesAsync(connection, cancellationToken);

        var defaultCategory = AppointmentRequestCategoryOptions.First(category => category.Id == 9);
        var defaultProviderId = providers.Any(provider => provider.Id == patientDefaults.ProviderId)
            ? patientDefaults.ProviderId
            : providers.FirstOrDefault()?.Id;
        var defaultProvider = providers.FirstOrDefault(provider => provider.Id == defaultProviderId);
        var providerFacilityId = defaultProvider?.FacilityId;
        var defaultFacilityId = facilities.Any(facility => facility.Id == providerFacilityId)
            ? providerFacilityId
            : facilities.Any(facility => facility.Id == patientDefaults.FacilityId)
                ? patientDefaults.FacilityId
                : facilities.FirstOrDefault()?.Id;

        return new PatientPortalAppointmentRequestOptionsResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            Categories: AppointmentRequestCategoryOptions,
            Providers: providers,
            Facilities: facilities,
            Defaults: new PatientPortalAppointmentRequestDefaults(
                CategoryId: defaultCategory.Id,
                ProviderId: defaultProviderId,
                FacilityId: defaultFacilityId,
                DurationMinutes: defaultCategory.DurationMinutes,
                Date: metadata.BaseDate.AddDays(96).ToString("yyyy-MM-dd"),
                StartTime: "09:30"),
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalAppointmentRequestResponse> RequestAppointmentAsync(
        Guid sessionId,
        PatientPortalAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return AppointmentRequestFailure(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);

        if (!DateOnly.TryParse(request.Date, out var appointmentDate)
            || !TimeOnly.TryParse(request.StartTime, out var appointmentStart)
            || request.DurationMinutes is null
            || request.DurationMinutes <= 0)
        {
            return AppointmentRequestFailure(session, "Appointment request needs a valid date, start time, and duration.", metadata);
        }

        if (appointmentDate < metadata.BaseDate)
        {
            return AppointmentRequestFailure(session, "Appointment request date must be today or later.", metadata);
        }

        var categoryId = request.CategoryId ?? 9;
        var title = GetPortalAppointmentRequestTitle(categoryId);
        var reason = NormalizeText(request.Reason);
        var appointmentId = $"APPT-PORTAL-{Guid.NewGuid():N}";
        var reminderId = $"MSG-PORTAL-APPT-{Guid.NewGuid():N}";

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with patient_match as (
                select canonical_id, legacy_pid, provider_id, facility_id
                from patients
                where canonical_id = @patient_id
                limit 1
            ),
            provider_match as (
                select id, username, trim(concat(first_name, ' ', last_name)) as provider_name
                from staff
                where active = true
                  and id = coalesce(@provider_id, (select provider_id from patient_match))
                limit 1
            ),
            facility_match as (
                select id, name
                from facilities
                where inactive = false
                  and id = coalesce(@facility_id, (select facility_id from patient_match))
                limit 1
            ),
            inserted as (
                insert into appointments (
                    id,
                    patient_id,
                    pid,
                    provider_id,
                    facility_id,
                    billing_location_id,
                    appointment_date,
                    start_time,
                    duration_minutes,
                    category_id,
                    title,
                    status,
                    comments,
                    recurrence_type
                )
                select
                    @appointment_id,
                    patient_match.canonical_id,
                    patient_match.legacy_pid,
                    provider_match.id,
                    coalesce(facility_match.id, patient_match.facility_id),
                    coalesce(facility_match.id, patient_match.facility_id),
                    @appointment_date,
                    @start_time,
                    @duration_minutes,
                    @category_id,
                    @title,
                    '^',
                    @reason,
                    0
                from patient_match
                join provider_match on true
                left join facility_match on true
                returning *
            )
            select
                inserted.id,
                inserted.appointment_date,
                inserted.start_time,
                inserted.title,
                inserted.status,
                inserted.category_id,
                inserted.comments,
                provider_match.username as provider_username,
                provider_match.provider_name,
                facility_match.name as facility_name
            from inserted
            join provider_match on provider_match.id = inserted.provider_id
            left join facility_match on facility_match.id = inserted.facility_id;
            """;
        command.Parameters.AddWithValue("patient_id", session.CanonicalId);
        command.Parameters.Add("provider_id", NpgsqlDbType.Integer).Value = request.ProviderId is null ? DBNull.Value : request.ProviderId.Value;
        command.Parameters.Add("facility_id", NpgsqlDbType.Integer).Value = request.FacilityId is null ? DBNull.Value : request.FacilityId.Value;
        command.Parameters.AddWithValue("appointment_id", appointmentId);
        command.Parameters.Add("appointment_date", NpgsqlDbType.Date).Value = appointmentDate;
        command.Parameters.Add("start_time", NpgsqlDbType.Time).Value = appointmentStart;
        command.Parameters.AddWithValue("duration_minutes", request.DurationMinutes.Value);
        command.Parameters.AddWithValue("category_id", categoryId);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.Add("reason", NpgsqlDbType.Text).Value = reason ?? (object)DBNull.Value;

        PatientPortalHomeAppointmentSummary? appointment = null;
        var assignedTo = string.Empty;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return AppointmentRequestFailure(session, "Appointment request could not be created for the selected provider and facility.", metadata);
            }

            assignedTo = ReadNullableString(reader, "provider_username") ?? string.Empty;
            appointment = new PatientPortalHomeAppointmentSummary(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Date: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("appointment_date")).ToString("yyyy-MM-dd"),
                StartTime: reader.GetFieldValue<TimeOnly>(reader.GetOrdinal("start_time")).ToString("HH:mm"),
                Title: ReadNullableString(reader, "title") ?? title,
                Status: ReadNullableString(reader, "status"),
                CategoryId: ReadNullableInt(reader, "category_id"),
                CategoryName: GetAppointmentCategoryName(ReadNullableInt(reader, "category_id")),
                ProviderName: ReadNullableString(reader, "provider_name"),
                FacilityName: ReadNullableString(reader, "facility_name"),
                Comments: ReadNullableString(reader, "comments"));
        }

        if (appointment is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AppointmentRequestFailure(session, "Appointment request could not be created.", metadata);
        }

        var reminderBody = BuildPortalAppointmentReminderBody(session, appointment, reason);
        await using var reminderCommand = connection.CreateCommand();
        reminderCommand.Transaction = transaction;
        reminderCommand.CommandText = """
            insert into messages (
                id,
                patient_id,
                pid,
                message_date,
                title,
                body,
                status,
                assigned_to,
                portal_relation,
                is_encrypted,
                deleted,
                activity
            )
            values (
                @id,
                @patient_id,
                @pid,
                current_date,
                'Patient Reminders',
                @body,
                'New',
                @assigned_to,
                @portal_relation,
                false,
                0,
                1
            );
            """;
        reminderCommand.Parameters.AddWithValue("id", reminderId);
        reminderCommand.Parameters.AddWithValue("patient_id", session.CanonicalId);
        reminderCommand.Parameters.AddWithValue("pid", session.LegacyPid.Value);
        reminderCommand.Parameters.AddWithValue("body", reminderBody);
        reminderCommand.Parameters.AddWithValue("assigned_to", assignedTo);
        reminderCommand.Parameters.AddWithValue("portal_relation", $"portal:appointment-request:{appointment.Id}");
        await reminderCommand.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PatientPortalAppointmentRequestResponse(
            Authenticated: true,
            Created: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            Appointment: appointment,
            Reminder: new PatientPortalAppointmentReminder(
                Id: reminderId,
                Title: "Patient Reminders",
                Body: reminderBody,
                AssignedTo: assignedTo,
                Status: "New"),
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalMessagesResponse> GetMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyMessages(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var messages = await GetPortalMessagesAsync(
            connection,
            session.PortalUsername,
            PortalMessageFolder.Inbox,
            cancellationToken);
        var sentMessages = await GetPortalMessagesAsync(
            connection,
            session.PortalUsername,
            PortalMessageFolder.Sent,
            cancellationToken);
        var allMessages = await GetPortalMessagesAsync(
            connection,
            session.PortalUsername,
            PortalMessageFolder.All,
            cancellationToken);
        var deletedMessages = await GetPortalMessagesAsync(
            connection,
            session.PortalUsername,
            PortalMessageFolder.Deleted,
            cancellationToken);

        return new PatientPortalMessagesResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            MessageCount: messages.Count,
            Messages: messages,
            SentMessageCount: sentMessages.Count,
            SentMessages: sentMessages,
            AllMessageCount: allMessages.Count,
            AllMessages: allMessages,
            DeletedMessageCount: deletedMessages.Count,
            DeletedMessages: deletedMessages,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalMessageAuditResponse> GetMessageAuditAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyMessageAudit(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var auditEvents = await GetPortalMessageAuditEventsAsync(connection, session.CanonicalId, cancellationToken);

        return new PatientPortalMessageAuditResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            AuditEventCount: auditEvents.Count,
            AuditEvents: auditEvents,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalMessageRecipientsResponse> GetMessageRecipientsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyMessageRecipients(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var recipients = await GetPortalMessageRecipientOptionsAsync(connection, cancellationToken);

        return new PatientPortalMessageRecipientsResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            RecipientCount: recipients.Count,
            Recipients: recipients,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalMessageComposeOptionsResponse> GetMessageComposeOptionsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyMessageComposeOptions(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var recipients = await GetPortalMessageRecipientOptionsAsync(connection, cancellationToken);

        return new PatientPortalMessageComposeOptionsResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            DefaultSubject: PortalMessageSubjectOptions.First(option => option.Default).Value,
            SubjectCount: PortalMessageSubjectOptions.Length,
            SubjectOptions: PortalMessageSubjectOptions,
            RecipientCount: recipients.Count,
            Recipients: recipients,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalDocumentsResponse> GetDocumentsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyDocuments(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var documents = await GetPortalDocumentsAsync(connection, session.LegacyPid.Value, cancellationToken);

        return BuildDocumentsResponse(session, metadata, documents);
    }

    public async Task<PatientPortalDocumentsDownloadPackage> DownloadDocumentsAsync(
        Guid sessionId,
        PatientPortalDocumentsDownloadRequest request,
        CancellationToken cancellationToken)
    {
        var selectedDocumentIds = (request.DocumentIds ?? Array.Empty<int>())
            .Where(documentId => documentId > 0)
            .Distinct()
            .ToArray();
        if (selectedDocumentIds.Length == 0)
        {
            return DownloadFailure("Select at least one patient document to download.");
        }

        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return DownloadFailure(session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var documents = await GetPortalDownloadDocumentsAsync(
            connection,
            session.LegacyPid.Value,
            selectedDocumentIds,
            cancellationToken);
        if (documents.Count == 0)
        {
            return DownloadFailure("Selected patient documents were not found in the signed-in portal account.");
        }

        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var document in documents)
            {
                var entry = archive.CreateEntry(BuildZipEntryName(document.Item, usedEntryNames), CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(document.Content, cancellationToken);
            }
        }

        return new PatientPortalDocumentsDownloadPackage(
            Downloadable: true,
            FileName: "patient_documents.zip",
            ContentType: "application/zip",
            Content: stream.ToArray(),
            DocumentCount: documents.Count,
            Documents: documents.Select(document => document.Item).ToArray(),
            FailureReason: null);
    }

    public async Task<PatientPortalMessageThreadResponse> GetMessageThreadAsync(
        Guid sessionId,
        int messageId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return ThreadFailure(session, messageId.ToString(), 0, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var anchor = await GetPortalOwnedMessageAsync(connection, session.PortalUsername, messageId, cancellationToken);
        if (anchor is null)
        {
            return ThreadFailure(
                session,
                messageId.ToString(),
                0,
                "Secure message was not found in the signed-in portal mailbox.",
                metadata);
        }

        var threadId = ResolvePortalThreadId(anchor.Item, messageId);
        var threadMessages = await GetPortalMessageThreadAsync(
            connection,
            session.PortalUsername,
            messageId,
            threadId,
            cancellationToken);

        return new PatientPortalMessageThreadResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            MessageId: anchor.Item.Id,
            ThreadId: threadId,
            AnchorMessage: anchor.Item,
            ThreadMessageCount: threadMessages.Count,
            ThreadMessages: threadMessages,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalMessageAttachmentDownload> DownloadMessageAttachmentAsync(
        Guid sessionId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return new(false, string.Empty, "application/octet-stream", Array.Empty<byte>(), session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select attachment.file_name, attachment.content_type, attachment.content
            from patient_portal_message_attachments attachment
            inner join portal_mailbox_messages message on message.id = attachment.message_id
            where attachment.id = @attachmentId
              and message.owner = @portalUsername
              and message.deleted = 0
            limit 1;
            """;
        command.Parameters.AddWithValue("attachmentId", attachmentId);
        command.Parameters.AddWithValue("portalUsername", session.PortalUsername);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new(false, string.Empty, "application/octet-stream", Array.Empty<byte>(), "Attachment was not found in the signed-in portal mailbox.");
        }

        return new(true, reader.GetString(0), reader.GetString(1), reader.GetFieldValue<byte[]>(2), null);
    }

    public async Task<PatientPortalComposeMessageResponse> ComposeMessageAsync(
        Guid sessionId,
        PatientPortalComposeMessageRequest request,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return ComposeFailure(session, session.FailureReason ?? "Session is not active.");
        }

        var recipientId = NormalizeText(request.RecipientId) ?? "admin";
        var title = NormalizeText(request.Title);
        var body = NormalizeText(request.Body);
        IReadOnlyList<PortalAttachmentUpload> attachments;
        try
        {
            attachments = NormalizeMessageAttachments(request.Attachments);
        }
        catch (InvalidOperationException ex)
        {
            return ComposeFailure(session, ex.Message, recipientId);
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return ComposeFailure(session, "Secure message title is required.", recipientId);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return ComposeFailure(session, "Secure message body is required.", recipientId);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var recipientOptions = await GetPortalMessageRecipientOptionsAsync(connection, cancellationToken);
        var recipientOption = recipientOptions.FirstOrDefault(
            recipient => string.Equals(recipient.Id, recipientId, StringComparison.OrdinalIgnoreCase));
        if (recipientOption is null)
        {
            return ComposeFailure(session, "Secure message recipient was not found in the patient portal recipient directory.", recipientId);
        }

        recipientId = recipientOption.Id;
        var recipientName = recipientOption.DisplayName;
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var nextId = await GetNextPortalMailboxIdAsync(connection, transaction, cancellationToken);
        var sentMessage = new PatientPortalMessageItem(
            Id: nextId.ToString(),
            Type: "Message",
            Date: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            Title: title,
            Body: body,
            Status: "New",
            AssignedTo: recipientId,
            SenderId: session.PortalUsername,
            SenderName: session.DisplayName,
            RecipientId: recipientId,
            RecipientName: recipientName,
            MailChain: nextId,
            ReplyMailChain: nextId,
            PortalRelation: "portal:composed",
            IsEncrypted: false,
            AttachmentCount: attachments.Count,
            Attachments: CreateAttachmentItems(attachments));
        var recipientMessage = sentMessage with
        {
            Id = (nextId + 1).ToString(),
            MailChain = nextId + 1,
            SenderName = session.DisplayName,
            RecipientName = recipientName,
            Attachments = CreateAttachmentItems(attachments)
        };

        await InsertPortalMailboxMessageAsync(
            connection,
            session,
            sentMessage,
            owner: session.PortalUsername,
            userValue: session.PortalUsername,
            mailChain: nextId,
            replyMailChain: nextId,
            transaction: transaction,
            cancellationToken: cancellationToken);
        await InsertPortalMailboxMessageAsync(
            connection,
            session,
            recipientMessage,
            owner: recipientId,
            userValue: session.PortalUsername,
            mailChain: nextId + 1,
            replyMailChain: nextId,
            transaction: transaction,
            cancellationToken: cancellationToken);
        await InsertPortalMessageAttachmentsAsync(connection, transaction, session, sentMessage, attachments, cancellationToken);
        await InsertPortalMessageAttachmentsAsync(connection, transaction, session, recipientMessage, attachments, cancellationToken);
        await RecordPortalMessageAuditEventAsync(
            connection,
            session,
            PortalMessageComposedEventType,
            sentMessage,
            [sentMessage, recipientMessage],
            archivedMessageCount: 0,
            transaction,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var sentCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Sent, cancellationToken);
        var inboxCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Inbox, cancellationToken);

        return new PatientPortalComposeMessageResponse(
            Authenticated: true,
            Created: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            RecipientId: recipientId,
            RecipientName: recipientName,
            SentMessage: sentMessage,
            RecipientMessage: recipientMessage,
            MessageCount: inboxCount,
            SentMessageCount: sentCount,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalComposeMessageResponse> RequestPrescriptionRefillAsync(
        Guid sessionId,
        string prescriptionId,
        PatientPortalPrescriptionRefillRequest request,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return ComposeFailure(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var prescription = await GetPortalPrescriptionAsync(connection, session.LegacyPid.Value, prescriptionId, cancellationToken);
        if (prescription is null)
        {
            return ComposeFailure(session, "Active prescription was not found for the signed-in portal patient.", "admin");
        }

        var recipientOptions = await GetPortalMessageRecipientOptionsAsync(connection, cancellationToken);
        var recipientOption = recipientOptions.FirstOrDefault(
            recipient => string.Equals(recipient.Id, "admin", StringComparison.OrdinalIgnoreCase));
        if (recipientOption is null)
        {
            return ComposeFailure(session, "Secure message recipient was not found in the patient portal recipient directory.", "admin");
        }

        var requestDate = NormalizeDate(request.RequestDate) ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var note = NormalizeText(request.Note);
        var title = $"Prescription refill request - {prescription.Drug}";
        var bodyParts = new[]
        {
            $"Patient {session.DisplayName} requested a prescription refill on {requestDate}.",
            $"Prescription: {prescription.Drug}",
            $"Dosage: {prescription.Dosage ?? "Not recorded"}",
            $"Quantity: {prescription.Quantity ?? "Not recorded"}",
            $"Route: {prescription.Route ?? "Not recorded"}",
            $"Prescription ID: {prescription.Id}",
            string.IsNullOrWhiteSpace(note) ? null : $"Patient note: {note}"
        };
        var body = string.Join(Environment.NewLine, bodyParts.Where(part => !string.IsNullOrWhiteSpace(part)));

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var nextId = await GetNextPortalMailboxIdAsync(connection, transaction, cancellationToken);
        var sentMessage = new PatientPortalMessageItem(
            Id: nextId.ToString(),
            Type: "Message",
            Date: requestDate,
            Title: title,
            Body: body,
            Status: "New",
            AssignedTo: recipientOption.Id,
            SenderId: session.PortalUsername,
            SenderName: session.DisplayName,
            RecipientId: recipientOption.Id,
            RecipientName: recipientOption.DisplayName,
            MailChain: nextId,
            ReplyMailChain: nextId,
            PortalRelation: "portal:prescription-refill-request",
            IsEncrypted: false,
            AttachmentCount: 0,
            Attachments: Array.Empty<PatientPortalMessageAttachment>());
        var recipientMessage = sentMessage with
        {
            Id = (nextId + 1).ToString(),
            MailChain = nextId + 1
        };

        await InsertPortalMailboxMessageAsync(
            connection,
            session,
            sentMessage,
            owner: session.PortalUsername,
            userValue: session.PortalUsername,
            mailChain: nextId,
            replyMailChain: nextId,
            transaction: transaction,
            cancellationToken: cancellationToken);
        await InsertPortalMailboxMessageAsync(
            connection,
            session,
            recipientMessage,
            owner: recipientOption.Id,
            userValue: session.PortalUsername,
            mailChain: nextId + 1,
            replyMailChain: nextId,
            transaction: transaction,
            cancellationToken: cancellationToken);
        await RecordPortalMessageAuditEventAsync(
            connection,
            session,
            PortalPrescriptionRefillRequestedEventType,
            sentMessage,
            [sentMessage, recipientMessage],
            archivedMessageCount: 0,
            transaction,
            cancellationToken);
        await InsertPrescriptionRefillLifecycleAsync(
            connection,
            transaction,
            threadId: nextId,
            staffMessageId: nextId + 1,
            pid: session.LegacyPid.Value,
            patientId: session.CanonicalId,
            prescriptionId: prescription.Id,
            requestDate: requestDate,
            drug: prescription.Drug,
            patientNote: note,
            updatedBy: session.PortalUsername,
            cancellationToken: cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var sentCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Sent, cancellationToken);
        var inboxCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Inbox, cancellationToken);

        return new PatientPortalComposeMessageResponse(
            Authenticated: true,
            Created: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            RecipientId: recipientOption.Id,
            RecipientName: recipientOption.DisplayName,
            SentMessage: sentMessage,
            RecipientMessage: recipientMessage,
            MessageCount: inboxCount,
            SentMessageCount: sentCount,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalPrescriptionRefillHistoryResponse> GetPrescriptionRefillHistoryAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return new PatientPortalPrescriptionRefillHistoryResponse(
                Authenticated: false,
                SessionId: session.SessionId,
                Username: session.Username,
                PortalUsername: session.PortalUsername,
                CanonicalId: session.CanonicalId,
                LegacyPid: session.LegacyPid,
                Pubpid: session.Pubpid,
                DisplayName: session.DisplayName,
                DatasetId: "unseeded",
                DatasetVersion: "unknown",
                RequestCount: 0,
                Requests: Array.Empty<PatientPortalPrescriptionRefillHistoryItem>(),
                FailureReason: session.FailureReason ?? "Session is not active.",
                SessionSource: session.SessionSource);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                lifecycle.staff_message_id,
                lifecycle.thread_id,
                lifecycle.prescription_id,
                coalesce(lifecycle.drug, prescription.drug, 'Prescription') as drug,
                lifecycle.request_date,
                lifecycle.status,
                lifecycle.patient_note,
                lifecycle.staff_response,
                lifecycle.updated_at,
                lifecycle.updated_by
            from prescription_refill_request_lifecycle lifecycle
            left join prescriptions prescription
              on prescription.id::text = lifecycle.prescription_id
             and prescription.pid = lifecycle.pid
            where lifecycle.pid = @pid
              and lifecycle.patient_id = @patientId
            order by lifecycle.request_date desc nulls last,
                     lifecycle.updated_at desc,
                     lifecycle.thread_id desc;
            """;
        command.Parameters.Add("pid", NpgsqlDbType.Integer).Value = session.LegacyPid.Value;
        command.Parameters.AddWithValue("patientId", session.CanonicalId);

        var requests = new List<PatientPortalPrescriptionRefillHistoryItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var requestDateOrdinal = reader.GetOrdinal("request_date");
            var updatedBy = reader.GetString(reader.GetOrdinal("updated_by"));
            requests.Add(new PatientPortalPrescriptionRefillHistoryItem(
                MessageId: reader.GetInt32(reader.GetOrdinal("staff_message_id")),
                ThreadId: reader.GetInt32(reader.GetOrdinal("thread_id")),
                PrescriptionId: reader.GetString(reader.GetOrdinal("prescription_id")),
                Drug: reader.GetString(reader.GetOrdinal("drug")),
                RequestDate: reader.IsDBNull(requestDateOrdinal)
                    ? string.Empty
                    : reader.GetFieldValue<DateOnly>(requestDateOrdinal).ToString("yyyy-MM-dd"),
                Status: reader.GetString(reader.GetOrdinal("status")),
                PatientNote: ReadNullableString(reader, "patient_note"),
                StaffResponse: ReadNullableString(reader, "staff_response"),
                UpdatedAt: reader.GetFieldValue<DateTime>(reader.GetOrdinal("updated_at")).ToString("O"),
                UpdatedBy: string.Equals(
                    updatedBy,
                    session.PortalUsername,
                    StringComparison.OrdinalIgnoreCase)
                    ? session.DisplayName
                    : "Care team"));
        }

        return new PatientPortalPrescriptionRefillHistoryResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            RequestCount: requests.Count,
            Requests: requests,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalReplyMessageResponse> ReplyToMessageAsync(
        Guid sessionId,
        int messageId,
        PatientPortalReplyMessageRequest request,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return ReplyFailure(session, messageId.ToString(), session.FailureReason ?? "Session is not active.");
        }

        var body = NormalizeText(request.Body);
        IReadOnlyList<PortalAttachmentUpload> attachments;
        try
        {
            attachments = NormalizeMessageAttachments(request.Attachments);
        }
        catch (InvalidOperationException ex)
        {
            return ReplyFailure(session, messageId.ToString(), ex.Message);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return ReplyFailure(session, messageId.ToString(), "Secure message reply body is required.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var original = await GetPortalInboxMessageAsync(connection, session.PortalUsername, messageId, cancellationToken);
        if (original is null)
        {
            return ReplyFailure(session, messageId.ToString(), "Secure message was not found in the signed-in portal inbox.");
        }

        var recipientId = NormalizeText(original.Item.SenderId)
            ?? NormalizeText(original.Item.AssignedTo)
            ?? "admin";
        var recipientName = NormalizeText(original.Item.SenderName)
            ?? await GetRecipientDisplayNameAsync(connection, recipientId, cancellationToken);
        var replyThreadId = original.Item.ReplyMailChain > 0
            ? original.Item.ReplyMailChain
            : original.Item.MailChain > 0
                ? original.Item.MailChain
                : messageId;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var nextId = await GetNextPortalMailboxIdAsync(connection, transaction, cancellationToken);
        var messageDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var sentMessage = new PatientPortalMessageItem(
            Id: nextId.ToString(),
            Type: "Message",
            Date: messageDate,
            Title: original.Item.Title,
            Body: body,
            Status: "New",
            AssignedTo: recipientId,
            SenderId: session.PortalUsername,
            SenderName: session.DisplayName,
            RecipientId: recipientId,
            RecipientName: recipientName,
            MailChain: nextId,
            ReplyMailChain: replyThreadId,
            PortalRelation: "portal:reply",
            IsEncrypted: false,
            AttachmentCount: attachments.Count,
            Attachments: CreateAttachmentItems(attachments));
        var recipientMessage = sentMessage with
        {
            Id = (nextId + 1).ToString(),
            MailChain = nextId + 1,
            Attachments = CreateAttachmentItems(attachments)
        };

        await InsertPortalMailboxMessageAsync(
            connection,
            session,
            sentMessage,
            owner: session.PortalUsername,
            userValue: session.PortalUsername,
            mailChain: nextId,
            replyMailChain: replyThreadId,
            transaction: transaction,
            cancellationToken: cancellationToken);
        await InsertPortalMailboxMessageAsync(
            connection,
            session,
            recipientMessage,
            owner: recipientId,
            userValue: session.PortalUsername,
            mailChain: nextId + 1,
            replyMailChain: replyThreadId,
            transaction: transaction,
            cancellationToken: cancellationToken);
        await InsertPortalMessageAttachmentsAsync(connection, transaction, session, sentMessage, attachments, cancellationToken);
        await InsertPortalMessageAttachmentsAsync(connection, transaction, session, recipientMessage, attachments, cancellationToken);
        await RecordPortalMessageAuditEventAsync(
            connection,
            session,
            PortalMessageRepliedEventType,
            sentMessage,
            [original.Item, sentMessage, recipientMessage],
            archivedMessageCount: 0,
            transaction,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var sentCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Sent, cancellationToken);
        var inboxCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Inbox, cancellationToken);

        return new PatientPortalReplyMessageResponse(
            Authenticated: true,
            Created: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            OriginalMessageId: original.Item.Id,
            OriginalMessage: original.Item,
            SentMessage: sentMessage,
            RecipientMessage: recipientMessage,
            MessageCount: inboxCount,
            SentMessageCount: sentCount,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalForwardMessageResponse> ForwardMessageAsync(
        Guid sessionId,
        int messageId,
        PatientPortalForwardMessageRequest request,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return ForwardFailure(session, messageId.ToString(), session.FailureReason ?? "Session is not active.");
        }

        var body = NormalizeText(request.Body);
        if (string.IsNullOrWhiteSpace(body))
        {
            return ForwardFailure(session, messageId.ToString(), "Secure message forward body is required.");
        }

        var assignedTo = NormalizeText(request.AssignedTo) ?? "admin";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var original = await GetPortalInboxMessageAsync(connection, session.PortalUsername, messageId, cancellationToken);
        if (original is null)
        {
            return ForwardFailure(session, messageId.ToString(), "Secure message was not found in the signed-in portal inbox.");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var forwardedMessage = await InsertForwardedPatientMessageAsync(
            connection,
            session,
            original.Item,
            body,
            assignedTo,
            transaction,
            cancellationToken);
        var forwardedPortalMessage = original.Item with
        {
            Status = "Sent",
            AssignedTo = assignedTo,
            PortalRelation = "portal:forwarded"
        };

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update portal_mailbox_messages
                set message_status = 'Sent',
                    assigned_to = @assigned_to,
                    portal_relation = 'portal:forwarded',
                    activity = 1
                where deleted = 0
                  and owner = @portal_username
                  and recipient_id = @portal_username
                  and id = @message_id;
                """;
            command.Parameters.Add("assigned_to", NpgsqlDbType.Text).Value = assignedTo;
            command.Parameters.Add("portal_username", NpgsqlDbType.Text).Value = session.PortalUsername;
            command.Parameters.Add("message_id", NpgsqlDbType.Integer).Value = messageId;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await RecordPortalMessageAuditEventAsync(
            connection,
            session,
            PortalMessageForwardedEventType,
            forwardedPortalMessage,
            [forwardedPortalMessage],
            archivedMessageCount: 0,
            transaction,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var sentCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Sent, cancellationToken);
        var inboxCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Inbox, cancellationToken);

        return new PatientPortalForwardMessageResponse(
            Authenticated: true,
            Forwarded: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            OriginalMessageId: forwardedPortalMessage.Id,
            OriginalMessage: forwardedPortalMessage,
            ForwardedPatientMessage: forwardedMessage,
            MessageCount: inboxCount,
            SentMessageCount: sentCount,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalReadMessageResponse> MarkMessageReadAsync(
        Guid sessionId,
        int messageId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return ReadFailure(session, messageId.ToString(), session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var message = await GetPortalOwnedMessageAsync(connection, session.PortalUsername, messageId, cancellationToken);
        if (message is null)
        {
            return ReadFailure(
                session,
                messageId.ToString(),
                "Secure message was not found in the signed-in portal mailbox.");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            update portal_mailbox_messages
            set message_status = 'Read',
                activity = 1
            where id = @message_id
              and owner = @portal_username
              and deleted = 0
            returning id, 'Message' as type, message_date, title, body, message_status, assigned_to, portal_relation,
              mail_chain, sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain, is_encrypted;
            """;
        command.Parameters.Add("message_id", NpgsqlDbType.Integer).Value = messageId;
        command.Parameters.AddWithValue("portal_username", session.PortalUsername);

        PatientPortalMessageItem? readMessage = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                readMessage = ReadPortalMessageItem(reader);
            }
        }

        if (readMessage is not null)
        {
            await RecordPortalMessageAuditEventAsync(
                connection,
                session,
                PortalMessageReadEventType,
                readMessage,
                [readMessage],
                archivedMessageCount: 0,
                transaction: null,
                cancellationToken);
        }

        var sentCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Sent, cancellationToken);
        var inboxCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Inbox, cancellationToken);

        return new PatientPortalReadMessageResponse(
            Authenticated: true,
            MarkedRead: readMessage is not null,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            MessageId: message.Item.Id,
            Message: readMessage,
            MessageCount: inboxCount,
            SentMessageCount: sentCount,
            FailureReason: readMessage is null ? "Secure message was not marked read." : null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalDeleteMessageResponse> DeleteMessageAsync(
        Guid sessionId,
        int messageId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return DeleteFailure(session, messageId.ToString(), session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var message = await GetPortalOwnedMessageAsync(connection, session.PortalUsername, messageId, cancellationToken);
        if (message is null)
        {
            return DeleteFailure(
                session,
                messageId.ToString(),
                "Secure message was not found in the signed-in portal mailbox.");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with updated as (
              update portal_mailbox_messages
              set message_status = 'Delete',
                  activity = 1,
                  deleted = 1
              where owner = @portal_username
                and (mail_chain = @message_id or id = @message_id)
              returning id, 'Message' as type, message_date, title, body, message_status, assigned_to, portal_relation,
                mail_chain, sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain, is_encrypted
            )
            select id, type, message_date, title, body, message_status, assigned_to, portal_relation,
              mail_chain, sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain, is_encrypted
            from updated
            order by message_date asc, id asc;
            """;
        command.Parameters.AddWithValue("portal_username", session.PortalUsername);
        command.Parameters.Add("message_id", NpgsqlDbType.Integer).Value = messageId;

        var deletedMessages = new List<PatientPortalMessageItem>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                deletedMessages.Add(ReadPortalMessageItem(reader));
            }
        }

        if (deletedMessages.Count > 0)
        {
            await RecordPortalMessageAuditEventAsync(
                connection,
                session,
                PortalMessageArchivedEventType,
                message.Item,
                deletedMessages,
                archivedMessageCount: deletedMessages.Count,
                transaction,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var sentCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Sent, cancellationToken);
        var inboxCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Inbox, cancellationToken);

        return new PatientPortalDeleteMessageResponse(
            Authenticated: true,
            Deleted: deletedMessages.Count > 0,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            MessageId: message.Item.Id,
            DeletedMessage: deletedMessages.FirstOrDefault(),
            DeletedMessageCount: deletedMessages.Count,
            MessageCount: inboxCount,
            SentMessageCount: sentCount,
            FailureReason: deletedMessages.Count > 0 ? null : "Secure message was not archived.",
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalArchiveMessagesResponse> ArchiveMessagesAsync(
        Guid sessionId,
        PatientPortalArchiveMessagesRequest request,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        var requestedMessageIds = (request.MessageIds ?? Array.Empty<int>())
            .Where(messageId => messageId > 0)
            .Distinct()
            .ToArray();
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return ArchiveMessagesFailure(
                session,
                requestedMessageIds.Select(messageId => messageId.ToString()).ToArray(),
                session.FailureReason ?? "Session is not active.");
        }

        if (requestedMessageIds.Length == 0)
        {
            return ArchiveMessagesFailure(session, Array.Empty<string>(), "Select at least one secure message to archive.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with selected as (
              select distinct case when mail_chain > 0 then mail_chain else id end as archive_id
              from portal_mailbox_messages
              where deleted = 0
                and owner = @portal_username
                and (sender_id = @portal_username or recipient_id = @portal_username)
                and (id = any(@message_ids) or mail_chain = any(@message_ids))
            ),
            updated as (
              update portal_mailbox_messages messages
              set message_status = 'Delete',
                  activity = 1,
                  deleted = 1
              from selected
              where messages.owner = @portal_username
                and (messages.mail_chain = selected.archive_id or messages.id = selected.archive_id)
              returning messages.id, 'Message' as type, messages.message_date, messages.title, messages.body, messages.message_status,
                messages.assigned_to, messages.portal_relation, messages.mail_chain, messages.sender_id,
                messages.sender_name, messages.recipient_id, messages.recipient_name, messages.reply_mail_chain,
                messages.is_encrypted
            )
            select id, type, message_date, title, body, message_status, assigned_to, portal_relation,
              mail_chain, sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain, is_encrypted
            from updated
            order by message_date asc, id asc;
            """;
        command.Parameters.AddWithValue("portal_username", session.PortalUsername);
        command.Parameters.Add("message_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = requestedMessageIds;

        var archivedMessages = new List<PatientPortalMessageItem>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                archivedMessages.Add(ReadPortalMessageItem(reader));
            }
        }

        if (archivedMessages.Count > 0)
        {
            await RecordPortalMessageAuditEventAsync(
                connection,
                session,
                PortalMessagesArchivedEventType,
                archivedMessages.First(),
                archivedMessages,
                archivedMessageCount: archivedMessages.Count,
                transaction,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var sentCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Sent, cancellationToken);
        var inboxCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Inbox, cancellationToken);

        return new PatientPortalArchiveMessagesResponse(
            Authenticated: true,
            Archived: archivedMessages.Count > 0,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            MessageIds: requestedMessageIds.Select(messageId => messageId.ToString()).ToArray(),
            ArchivedMessages: archivedMessages,
            ArchivedMessageCount: archivedMessages.Count,
            MessageCount: inboxCount,
            SentMessageCount: sentCount,
            FailureReason: archivedMessages.Count > 0 ? null : "Selected secure messages were not archived.",
            SessionSource: session.SessionSource);
    }

    private static PatientPortalLoginResponse Failed(string username, string reason) => new(
        Authenticated: false,
        Username: username,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        FailureReason: reason,
        SessionId: null,
        SessionCreatedAt: null,
        SessionExpiresAt: null,
        SessionSource: SessionSource);

    private static PatientPortalHomeSummaryResponse EmptyHomeSummary(
        PatientPortalSessionResponse session,
        string reason) => new(
        Authenticated: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        Messages: new PatientPortalHomeMessageSummary(0, 0, 0, null, null),
        UpcomingAppointmentCount: 0,
        UpcomingAppointments: Array.Empty<PatientPortalHomeAppointmentSummary>(),
        ImmunizationCount: 0,
        Immunizations: Array.Empty<PatientPortalHomeImmunizationSummary>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalHomeSummaryResponse MissingSessionHomeSummary(string reason) => new(
        Authenticated: false,
        SessionId: null,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        Messages: new PatientPortalHomeMessageSummary(0, 0, 0, null, null),
        UpcomingAppointmentCount: 0,
        UpcomingAppointments: Array.Empty<PatientPortalHomeAppointmentSummary>(),
        ImmunizationCount: 0,
        Immunizations: Array.Empty<PatientPortalHomeImmunizationSummary>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalProfileResponse EmptyProfile(
        PatientPortalSessionResponse session,
        string reason) => new(
        Authenticated: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        HasPendingProfileChanges: false,
        Demographics: EmptyProfileDemographics(),
        InsuranceCount: 0,
        Insurance: Array.Empty<PatientPortalProfileInsurance>(),
        PendingChange: null,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalProfileResponse MissingSessionProfile(string reason) => new(
        Authenticated: false,
        SessionId: null,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        HasPendingProfileChanges: false,
        Demographics: EmptyProfileDemographics(),
        InsuranceCount: 0,
        Insurance: Array.Empty<PatientPortalProfileInsurance>(),
        PendingChange: null,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalProfileDemographics EmptyProfileDemographics() => new(
        FirstName: string.Empty,
        LastName: string.Empty,
        PreferredName: null,
        DateOfBirth: null,
        Sex: null,
        Email: null,
        Street: null,
        City: null,
        State: null,
        PostalCode: null,
        PhoneHome: null,
        PhoneCell: null,
        PhoneContact: null,
        HipaaAllowSms: null,
        HipaaAllowEmail: null,
        ContactRelationship: null,
        MotherName: null,
        GuardianName: null,
        GuardianRelationship: null,
        GuardianPhone: null,
        GuardianEmail: null);

    private static PatientPortalAppointmentsResponse EmptyAppointments(
        PatientPortalSessionResponse session,
        string reason) => new(
        Authenticated: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        UpcomingAppointmentCount: 0,
        UpcomingAppointments: Array.Empty<PatientPortalHomeAppointmentSummary>(),
        PastAppointmentCount: 0,
        PastAppointments: Array.Empty<PatientPortalHomeAppointmentSummary>(),
        AppointmentRequestCount: 0,
        AppointmentRequests: Array.Empty<PatientPortalAppointmentRequestHistoryItem>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalAppointmentsResponse MissingSessionAppointments(string reason) => new(
        Authenticated: false,
        SessionId: null,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        UpcomingAppointmentCount: 0,
        UpcomingAppointments: Array.Empty<PatientPortalHomeAppointmentSummary>(),
        PastAppointmentCount: 0,
        PastAppointments: Array.Empty<PatientPortalHomeAppointmentSummary>(),
        AppointmentRequestCount: 0,
        AppointmentRequests: Array.Empty<PatientPortalAppointmentRequestHistoryItem>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalAppointmentRequestOptionsResponse AppointmentRequestOptionsFailure(
        PatientPortalSessionResponse session,
        string reason,
        DatasetMetadata? metadata = null) => new(
        Authenticated: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: metadata?.DatasetId ?? "unseeded",
        DatasetVersion: metadata?.DatasetVersion ?? "unknown",
        AsOfDate: (metadata?.BaseDate ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd"),
        Categories: Array.Empty<PatientPortalAppointmentCategoryOption>(),
        Providers: Array.Empty<PatientPortalAppointmentProviderOption>(),
        Facilities: Array.Empty<PatientPortalAppointmentFacilityOption>(),
        Defaults: new PatientPortalAppointmentRequestDefaults(null, null, null, 0, string.Empty, string.Empty),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalAppointmentRequestOptionsResponse MissingSessionAppointmentRequestOptions(string reason) => new(
        Authenticated: false,
        SessionId: null,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        Categories: Array.Empty<PatientPortalAppointmentCategoryOption>(),
        Providers: Array.Empty<PatientPortalAppointmentProviderOption>(),
        Facilities: Array.Empty<PatientPortalAppointmentFacilityOption>(),
        Defaults: new PatientPortalAppointmentRequestDefaults(null, null, null, 0, string.Empty, string.Empty),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalAppointmentRequestResponse AppointmentRequestFailure(
        PatientPortalSessionResponse session,
        string reason,
        DatasetMetadata? metadata = null) => new(
        Authenticated: session.Authenticated,
        Created: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: metadata?.DatasetId ?? "unseeded",
        DatasetVersion: metadata?.DatasetVersion ?? "unknown",
        AsOfDate: (metadata?.BaseDate ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd"),
        Appointment: null,
        Reminder: null,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalAppointmentRequestResponse MissingSessionAppointmentRequest(string reason) => new(
        Authenticated: false,
        Created: false,
        SessionId: null,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        Appointment: null,
        Reminder: null,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalMessagesResponse EmptyMessages(
        PatientPortalSessionResponse session,
        string reason) => new(
        Authenticated: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        MessageCount: 0,
        Messages: Array.Empty<PatientPortalMessageItem>(),
        SentMessageCount: 0,
        SentMessages: Array.Empty<PatientPortalMessageItem>(),
        AllMessageCount: 0,
        AllMessages: Array.Empty<PatientPortalMessageItem>(),
        DeletedMessageCount: 0,
        DeletedMessages: Array.Empty<PatientPortalMessageItem>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalMessagesResponse MissingSessionMessages(string reason) => new(
        Authenticated: false,
        SessionId: null,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        MessageCount: 0,
        Messages: Array.Empty<PatientPortalMessageItem>(),
        SentMessageCount: 0,
        SentMessages: Array.Empty<PatientPortalMessageItem>(),
        AllMessageCount: 0,
        AllMessages: Array.Empty<PatientPortalMessageItem>(),
        DeletedMessageCount: 0,
        DeletedMessages: Array.Empty<PatientPortalMessageItem>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalMessageRecipientsResponse EmptyMessageRecipients(
        PatientPortalSessionResponse session,
        string reason) => new(
        Authenticated: session.Authenticated,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        RecipientCount: 0,
        Recipients: Array.Empty<PatientPortalMessageRecipientOption>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalMessageRecipientsResponse MissingSessionMessageRecipients(string reason) => new(
        Authenticated: false,
        SessionId: null,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        RecipientCount: 0,
        Recipients: Array.Empty<PatientPortalMessageRecipientOption>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalMessageComposeOptionsResponse EmptyMessageComposeOptions(
        PatientPortalSessionResponse session,
        string reason) => new(
        Authenticated: session.Authenticated,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        DefaultSubject: PortalMessageSubjectOptions.First(option => option.Default).Value,
        SubjectCount: PortalMessageSubjectOptions.Length,
        SubjectOptions: PortalMessageSubjectOptions,
        RecipientCount: 0,
        Recipients: Array.Empty<PatientPortalMessageRecipientOption>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalMessageComposeOptionsResponse MissingSessionMessageComposeOptions(string reason) => new(
        Authenticated: false,
        SessionId: null,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        DefaultSubject: PortalMessageSubjectOptions.First(option => option.Default).Value,
        SubjectCount: PortalMessageSubjectOptions.Length,
        SubjectOptions: PortalMessageSubjectOptions,
        RecipientCount: 0,
        Recipients: Array.Empty<PatientPortalMessageRecipientOption>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalMessageAuditResponse EmptyMessageAudit(
        PatientPortalSessionResponse session,
        string reason) => new(
        Authenticated: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        AuditEventCount: 0,
        AuditEvents: Array.Empty<PatientPortalMessageAuditEvent>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalMessageAuditResponse MissingSessionMessageAudit(string reason) => new(
        Authenticated: false,
        SessionId: null,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        AuditEventCount: 0,
        AuditEvents: Array.Empty<PatientPortalMessageAuditEvent>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalDocumentsResponse EmptyDocuments(
        PatientPortalSessionResponse session,
        string reason) => new(
        Authenticated: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        DocumentCount: 0,
        Categories: Array.Empty<PatientPortalDocumentCategory>(),
        Documents: Array.Empty<PatientPortalDocumentItem>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalDocumentsResponse MissingSessionDocuments(string reason) => new(
        Authenticated: false,
        SessionId: null,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        DocumentCount: 0,
        Categories: Array.Empty<PatientPortalDocumentCategory>(),
        Documents: Array.Empty<PatientPortalDocumentItem>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalClinicalSummaryResponse EmptyClinicalSummary(
        PatientPortalSessionResponse session,
        string reason) => new(
        Authenticated: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        ProblemCount: 0,
        Problems: Array.Empty<PatientPortalProblemItem>(),
        AllergyCount: 0,
        Allergies: Array.Empty<PatientPortalAllergyItem>(),
        MedicationCount: 0,
        Medications: Array.Empty<PatientPortalMedicationItem>(),
        PrescriptionCount: 0,
        Prescriptions: Array.Empty<PatientPortalPrescriptionItem>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalClinicalSummaryResponse MissingSessionClinicalSummary(string reason) => new(
        Authenticated: false,
        SessionId: null,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        ProblemCount: 0,
        Problems: Array.Empty<PatientPortalProblemItem>(),
        AllergyCount: 0,
        Allergies: Array.Empty<PatientPortalAllergyItem>(),
        MedicationCount: 0,
        Medications: Array.Empty<PatientPortalMedicationItem>(),
        PrescriptionCount: 0,
        Prescriptions: Array.Empty<PatientPortalPrescriptionItem>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalLabResultsResponse EmptyLabResults(
        PatientPortalSessionResponse session,
        string reason) => new(
        Authenticated: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        OrderCount: 0,
        ReportCount: 0,
        ResultCount: 0,
        Orders: Array.Empty<PatientPortalLabOrderItem>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalLabResultsResponse MissingSessionLabResults(string reason) => new(
        Authenticated: false,
        SessionId: null,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        OrderCount: 0,
        ReportCount: 0,
        ResultCount: 0,
        Orders: Array.Empty<PatientPortalLabOrderItem>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalMedicalReportResponse EmptyMedicalReport(
        PatientPortalSessionResponse session,
        string reason) => new(
        Authenticated: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        SectionCount: 0,
        SelectedSectionCount: 0,
        Sections: Array.Empty<PatientPortalMedicalReportSection>(),
        IssueCount: 0,
        Issues: Array.Empty<PatientPortalMedicalReportIssue>(),
        EncounterCount: 0,
        Encounters: Array.Empty<PatientPortalMedicalReportEncounter>(),
        ProcedureOrderCount: 0,
        ProcedureOrders: Array.Empty<PatientPortalMedicalReportProcedureOrder>(),
        ReportPreview: new PatientPortalGeneratedMedicalReport(
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            EmptyGeneratedMedicalReportTemplateMetadata(),
            EmptyGeneratedMedicalReportPackageMetadata(),
            0,
            Array.Empty<string>()),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalMedicalReportResponse MissingSessionMedicalReport(string reason) => new(
        Authenticated: false,
        SessionId: null,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        SectionCount: 0,
        SelectedSectionCount: 0,
        Sections: Array.Empty<PatientPortalMedicalReportSection>(),
        IssueCount: 0,
        Issues: Array.Empty<PatientPortalMedicalReportIssue>(),
        EncounterCount: 0,
        Encounters: Array.Empty<PatientPortalMedicalReportEncounter>(),
        ProcedureOrderCount: 0,
        ProcedureOrders: Array.Empty<PatientPortalMedicalReportProcedureOrder>(),
        ReportPreview: new PatientPortalGeneratedMedicalReport(
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            EmptyGeneratedMedicalReportTemplateMetadata(),
            EmptyGeneratedMedicalReportPackageMetadata(),
            0,
            Array.Empty<string>()),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalGeneratedMedicalReportResponse EmptyGeneratedMedicalReport(
        PatientPortalSessionResponse session,
        string reason) => new(
        Authenticated: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        Title: string.Empty,
        GeneratedOn: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        IncludedSectionIds: Array.Empty<string>(),
        IncludedProcedureOrderIds: Array.Empty<string>(),
        IncludedIssueIds: Array.Empty<string>(),
        IncludedEncounterFormIds: Array.Empty<string>(),
        TemplateMetadata: EmptyGeneratedMedicalReportTemplateMetadata(),
        PrintableVersionAvailable: false,
        PdfDownloadAvailable: false,
        PackageDownloadAvailable: false,
        PackageMetadata: EmptyGeneratedMedicalReportPackageMetadata(),
        ReportSectionCount: 0,
        ReportSections: Array.Empty<PatientPortalGeneratedMedicalReportSection>(),
        SummaryLineCount: 0,
        SummaryLines: Array.Empty<string>(),
        AuditEventCount: 0,
        AuditEvents: Array.Empty<PatientPortalGeneratedMedicalReportAuditEvent>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalGeneratedMedicalReportAuditResponse EmptyGeneratedMedicalReportAudit(
        PatientPortalSessionResponse session,
        string reason) => new(
        Authenticated: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        AuditEventCount: 0,
        AuditEvents: Array.Empty<PatientPortalGeneratedMedicalReportAuditEvent>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalGeneratedMedicalReportResponse MissingSessionGeneratedMedicalReport(string reason) => new(
        Authenticated: false,
        SessionId: null,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        Title: string.Empty,
        GeneratedOn: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        IncludedSectionIds: Array.Empty<string>(),
        IncludedProcedureOrderIds: Array.Empty<string>(),
        IncludedIssueIds: Array.Empty<string>(),
        IncludedEncounterFormIds: Array.Empty<string>(),
        TemplateMetadata: EmptyGeneratedMedicalReportTemplateMetadata(),
        PrintableVersionAvailable: false,
        PdfDownloadAvailable: false,
        PackageDownloadAvailable: false,
        PackageMetadata: EmptyGeneratedMedicalReportPackageMetadata(),
        ReportSectionCount: 0,
        ReportSections: Array.Empty<PatientPortalGeneratedMedicalReportSection>(),
        SummaryLineCount: 0,
        SummaryLines: Array.Empty<string>(),
        AuditEventCount: 0,
        AuditEvents: Array.Empty<PatientPortalGeneratedMedicalReportAuditEvent>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalGeneratedMedicalReportAuditResponse MissingSessionGeneratedMedicalReportAudit(string reason) => new(
        Authenticated: false,
        SessionId: null,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        AuditEventCount: 0,
        AuditEvents: Array.Empty<PatientPortalGeneratedMedicalReportAuditEvent>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    public static PatientPortalMessagesResponse MissingSessionHeaderMessages() =>
        MissingSessionMessages("Patient portal session header was not supplied.");

    public static PatientPortalMessageRecipientsResponse MissingSessionHeaderMessageRecipients() =>
        MissingSessionMessageRecipients("Patient portal session header was not supplied.");

    public static PatientPortalMessageComposeOptionsResponse MissingSessionHeaderMessageComposeOptions() =>
        MissingSessionMessageComposeOptions("Patient portal session header was not supplied.");

    public static PatientPortalMessageAuditResponse MissingSessionHeaderMessageAudit() =>
        MissingSessionMessageAudit("Patient portal session header was not supplied.");

    public static PatientPortalDocumentsResponse MissingSessionHeaderDocuments() =>
        MissingSessionDocuments("Patient portal session header was not supplied.");

    public static PatientPortalClinicalSummaryResponse MissingSessionHeaderClinicalSummary() =>
        MissingSessionClinicalSummary("Patient portal session header was not supplied.");

    public static PatientPortalLabResultsResponse MissingSessionHeaderLabResults() =>
        MissingSessionLabResults("Patient portal session header was not supplied.");

    public static PatientPortalMedicalReportResponse MissingSessionHeaderMedicalReport() =>
        MissingSessionMedicalReport("Patient portal session header was not supplied.");

    public static PatientPortalGeneratedMedicalReportResponse MissingSessionHeaderGeneratedMedicalReport() =>
        MissingSessionGeneratedMedicalReport("Patient portal session header was not supplied.");

    public static PatientPortalGeneratedMedicalReportAuditResponse MissingSessionHeaderGeneratedMedicalReportAudit() =>
        MissingSessionGeneratedMedicalReportAudit("Patient portal session header was not supplied.");

    public static PatientPortalGeneratedMedicalReportPdfPackage MissingSessionHeaderGeneratedMedicalReportPdf() =>
        GeneratedMedicalReportPdfFailure("Patient portal session header was not supplied.");

    public static PatientPortalGeneratedMedicalReportPackageDownload MissingSessionHeaderGeneratedMedicalReportPackage() =>
        GeneratedMedicalReportPackageFailure("Patient portal session header was not supplied.");

    public static PatientPortalDocumentsDownloadPackage MissingSessionHeaderDocumentsDownload() =>
        DownloadFailure("Patient portal session header was not supplied.");

    public static PatientPortalMessageThreadResponse MissingSessionHeaderMessageThread(string messageId) =>
        new(
            Authenticated: false,
            SessionId: null,
            Username: string.Empty,
            PortalUsername: string.Empty,
            CanonicalId: string.Empty,
            LegacyPid: null,
            Pubpid: string.Empty,
            DisplayName: string.Empty,
            DatasetId: "unseeded",
            DatasetVersion: "unknown",
            AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            MessageId: messageId,
            ThreadId: 0,
            AnchorMessage: null,
            ThreadMessageCount: 0,
            ThreadMessages: Array.Empty<PatientPortalMessageItem>(),
            FailureReason: "Patient portal session header was not supplied.",
            SessionSource: SessionSource);

    public static PatientPortalHomeSummaryResponse MissingSessionHeaderHomeSummary() =>
        MissingSessionHomeSummary("Patient portal session header was not supplied.");

    public static PatientPortalProfileResponse MissingSessionHeaderProfile() =>
        MissingSessionProfile("Patient portal session header was not supplied.");

    public static PatientPortalAppointmentsResponse MissingSessionHeaderAppointments() =>
        MissingSessionAppointments("Patient portal session header was not supplied.");

    public static PatientPortalAppointmentRequestOptionsResponse MissingSessionHeaderAppointmentRequestOptions() =>
        MissingSessionAppointmentRequestOptions("Patient portal session header was not supplied.");

    public static PatientPortalAppointmentRequestResponse MissingSessionHeaderAppointmentRequest() =>
        MissingSessionAppointmentRequest("Patient portal session header was not supplied.");

    public static PatientPortalComposeMessageResponse MissingSessionHeaderComposeMessage() =>
        new(
            Authenticated: false,
            Created: false,
            SessionId: null,
            Username: string.Empty,
            PortalUsername: string.Empty,
            CanonicalId: string.Empty,
            LegacyPid: null,
            Pubpid: string.Empty,
            DisplayName: string.Empty,
            RecipientId: string.Empty,
            RecipientName: string.Empty,
            SentMessage: null,
            RecipientMessage: null,
            MessageCount: 0,
            SentMessageCount: 0,
            FailureReason: "Patient portal session header was not supplied.",
            SessionSource: SessionSource);

    public static PatientPortalReplyMessageResponse MissingSessionHeaderReplyMessage(string messageId) =>
        new(
            Authenticated: false,
            Created: false,
            SessionId: null,
            Username: string.Empty,
            PortalUsername: string.Empty,
            CanonicalId: string.Empty,
            LegacyPid: null,
            Pubpid: string.Empty,
            DisplayName: string.Empty,
            OriginalMessageId: messageId,
            OriginalMessage: null,
            SentMessage: null,
            RecipientMessage: null,
            MessageCount: 0,
            SentMessageCount: 0,
            FailureReason: "Patient portal session header was not supplied.",
            SessionSource: SessionSource);

    public static PatientPortalForwardMessageResponse MissingSessionHeaderForwardMessage(string messageId) =>
        new(
            Authenticated: false,
            Forwarded: false,
            SessionId: null,
            Username: string.Empty,
            PortalUsername: string.Empty,
            CanonicalId: string.Empty,
            LegacyPid: null,
            Pubpid: string.Empty,
            DisplayName: string.Empty,
            OriginalMessageId: messageId,
            OriginalMessage: null,
            ForwardedPatientMessage: null,
            MessageCount: 0,
            SentMessageCount: 0,
            FailureReason: "Patient portal session header was not supplied.",
            SessionSource: SessionSource);

    public static PatientPortalReadMessageResponse MissingSessionHeaderReadMessage(string messageId) =>
        new(
            Authenticated: false,
            MarkedRead: false,
            SessionId: null,
            Username: string.Empty,
            PortalUsername: string.Empty,
            CanonicalId: string.Empty,
            LegacyPid: null,
            Pubpid: string.Empty,
            DisplayName: string.Empty,
            MessageId: messageId,
            Message: null,
            MessageCount: 0,
            SentMessageCount: 0,
            FailureReason: "Patient portal session header was not supplied.",
            SessionSource: SessionSource);

    public static PatientPortalDeleteMessageResponse MissingSessionHeaderDeleteMessage(string messageId) =>
        new(
            Authenticated: false,
            Deleted: false,
            SessionId: null,
            Username: string.Empty,
            PortalUsername: string.Empty,
            CanonicalId: string.Empty,
            LegacyPid: null,
            Pubpid: string.Empty,
            DisplayName: string.Empty,
            MessageId: messageId,
            DeletedMessage: null,
            DeletedMessageCount: 0,
            MessageCount: 0,
            SentMessageCount: 0,
            FailureReason: "Patient portal session header was not supplied.",
            SessionSource: SessionSource);

    public static PatientPortalArchiveMessagesResponse MissingSessionHeaderArchiveMessages() =>
        new(
            Authenticated: false,
            Archived: false,
            SessionId: null,
            Username: string.Empty,
            PortalUsername: string.Empty,
            CanonicalId: string.Empty,
            LegacyPid: null,
            Pubpid: string.Empty,
            DisplayName: string.Empty,
            MessageIds: Array.Empty<string>(),
            ArchivedMessages: Array.Empty<PatientPortalMessageItem>(),
            ArchivedMessageCount: 0,
            MessageCount: 0,
            SentMessageCount: 0,
            FailureReason: "Patient portal session header was not supplied.",
            SessionSource: SessionSource);

    private static PatientPortalComposeMessageResponse ComposeFailure(
        PatientPortalSessionResponse session,
        string reason,
        string recipientId = "") => new(
        Authenticated: session.Authenticated,
        Created: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        RecipientId: recipientId,
        RecipientName: string.Empty,
        SentMessage: null,
        RecipientMessage: null,
        MessageCount: 0,
        SentMessageCount: 0,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalReplyMessageResponse ReplyFailure(
        PatientPortalSessionResponse session,
        string originalMessageId,
        string reason) => new(
        Authenticated: session.Authenticated,
        Created: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        OriginalMessageId: originalMessageId,
        OriginalMessage: null,
        SentMessage: null,
        RecipientMessage: null,
        MessageCount: 0,
        SentMessageCount: 0,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static bool HasRequestedAttachments(IReadOnlyList<PatientPortalMessageAttachmentSubmission>? attachments) =>
        attachments is { Count: > 0 };

    private static PatientPortalForwardMessageResponse ForwardFailure(
        PatientPortalSessionResponse session,
        string originalMessageId,
        string reason) => new(
        Authenticated: session.Authenticated,
        Forwarded: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        OriginalMessageId: originalMessageId,
        OriginalMessage: null,
        ForwardedPatientMessage: null,
        MessageCount: 0,
        SentMessageCount: 0,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalReadMessageResponse ReadFailure(
        PatientPortalSessionResponse session,
        string messageId,
        string reason) => new(
        Authenticated: session.Authenticated,
        MarkedRead: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        MessageId: messageId,
        Message: null,
        MessageCount: 0,
        SentMessageCount: 0,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalDeleteMessageResponse DeleteFailure(
        PatientPortalSessionResponse session,
        string messageId,
        string reason) => new(
        Authenticated: session.Authenticated,
        Deleted: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        MessageId: messageId,
        DeletedMessage: null,
        DeletedMessageCount: 0,
        MessageCount: 0,
        SentMessageCount: 0,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalArchiveMessagesResponse ArchiveMessagesFailure(
        PatientPortalSessionResponse session,
        IReadOnlyList<string> messageIds,
        string reason) => new(
        Authenticated: session.Authenticated,
        Archived: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        MessageIds: messageIds,
        ArchivedMessages: Array.Empty<PatientPortalMessageItem>(),
        ArchivedMessageCount: 0,
        MessageCount: 0,
        SentMessageCount: 0,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalMessageThreadResponse ThreadFailure(
        PatientPortalSessionResponse session,
        string messageId,
        int threadId,
        string reason,
        DatasetMetadata? metadata = null) => new(
        Authenticated: session.Authenticated,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: metadata?.DatasetId ?? "unseeded",
        DatasetVersion: metadata?.DatasetVersion ?? "unknown",
        AsOfDate: (metadata?.BaseDate ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd"),
        MessageId: messageId,
        ThreadId: threadId,
        AnchorMessage: null,
        ThreadMessageCount: 0,
        ThreadMessages: Array.Empty<PatientPortalMessageItem>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static async Task<IReadOnlyList<PatientPortalProblemItem>> GetPortalProblemsAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id::text as id, title, problem_date as reported_date, problem_date as start_date, end_date
            from problems
            where pid = @pid
              and type = 'medical_problem'
            order by problem_date, id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<PatientPortalProblemItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new PatientPortalProblemItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Title: ReadNullableString(reader, "title") ?? string.Empty,
                ReportedDate: ReadNullableDate(reader, "reported_date"),
                StartDate: ReadNullableDate(reader, "start_date"),
                EndDate: ReadNullableDate(reader, "end_date")));
        }

        return items;
    }

    private static async Task<IReadOnlyList<PatientPortalAllergyItem>> GetPortalAllergiesAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              id::text as id,
              title,
              allergy_date as reported_date,
              allergy_date as start_date,
              end_date,
              reaction,
              severity
            from allergies
            where pid = @pid
              and type = 'allergy'
            order by allergy_date, id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<PatientPortalAllergyItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new PatientPortalAllergyItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Title: ReadNullableString(reader, "title") ?? string.Empty,
                ReportedDate: ReadNullableDate(reader, "reported_date"),
                StartDate: ReadNullableDate(reader, "start_date"),
                EndDate: ReadNullableDate(reader, "end_date"),
                ReferredBy: null,
                Reaction: ReadNullableString(reader, "reaction"),
                Severity: ReadNullableString(reader, "severity")));
        }

        return items;
    }

    private static async Task<IReadOnlyList<PatientPortalMedicationItem>> GetPortalMedicationsAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id::text as id, title, medication_date as start_date, modified_date, end_date
            from medications
            where pid = @pid
              and type = 'medication'
            order by medication_date, id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<PatientPortalMedicationItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new PatientPortalMedicationItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Title: ReadNullableString(reader, "title") ?? string.Empty,
                StartDate: ReadNullableDate(reader, "start_date"),
                ModifiedDate: ReadNullableDate(reader, "modified_date"),
                EndDate: ReadNullableDate(reader, "end_date")));
        }

        return items;
    }

    private static async Task<IReadOnlyList<PatientPortalPrescriptionItem>> GetPortalPrescriptionsAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              id::text as id,
              drug,
              coalesce(to_char(date_added, 'YYYY-MM-DD HH24:MI:SS'), start_date::text) as start_date,
              modified_date,
              end_date,
              dosage,
              quantity::text as quantity,
              route,
              note
            from prescriptions
            where pid = @pid
              and end_date is null
            order by start_date, id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<PatientPortalPrescriptionItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new PatientPortalPrescriptionItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Drug: ReadNullableString(reader, "drug") ?? string.Empty,
                StartDate: ReadNullableString(reader, "start_date"),
                ModifiedDate: ReadNullableDate(reader, "modified_date"),
                EndDate: ReadNullableDate(reader, "end_date"),
                Dosage: ReadNullableString(reader, "dosage"),
                Quantity: ReadNullableString(reader, "quantity"),
                Route: ReadNullableString(reader, "route"),
                Note: ReadNullableString(reader, "note")));
        }

        return items;
    }

    private static async Task<PatientPortalPrescriptionItem?> GetPortalPrescriptionAsync(
        NpgsqlConnection connection,
        int legacyPid,
        string prescriptionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              id::text as id,
              drug,
              coalesce(to_char(date_added, 'YYYY-MM-DD HH24:MI:SS'), start_date::text) as start_date,
              modified_date,
              end_date,
              dosage,
              quantity::text as quantity,
              route,
              note
            from prescriptions
            where pid = @pid
              and id::text = @id
              and end_date is null
            limit 1;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);
        command.Parameters.Add("id", NpgsqlDbType.Text).Value = prescriptionId.Trim();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PatientPortalPrescriptionItem(
            Id: reader.GetString(reader.GetOrdinal("id")),
            Drug: ReadNullableString(reader, "drug") ?? string.Empty,
            StartDate: ReadNullableString(reader, "start_date"),
            ModifiedDate: ReadNullableDate(reader, "modified_date"),
            EndDate: ReadNullableDate(reader, "end_date"),
            Dosage: ReadNullableString(reader, "dosage"),
            Quantity: ReadNullableString(reader, "quantity"),
            Route: ReadNullableString(reader, "route"),
            Note: ReadNullableString(reader, "note"));
    }

    private static async Task<IReadOnlyList<PortalLabOrderRow>> GetPortalLabOrdersAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, order_date, code as procedure_code, name as procedure_name, order_status
            from lab_orders
            where pid = @pid
            order by order_date, id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<PortalLabOrderRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new PortalLabOrderRow(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                OrderDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("order_date")).ToString("yyyy-MM-dd"),
                ProcedureCode: ReadNullableString(reader, "procedure_code"),
                ProcedureName: ReadNullableString(reader, "procedure_name") ?? string.Empty,
                OrderStatus: ReadNullableString(reader, "order_status")));
        }

        return items;
    }

    private static async Task<IReadOnlyList<PortalLabReportRow>> GetPortalLabReportsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<int> orderIds,
        CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return Array.Empty<PortalLabReportRow>();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, order_id, date_collected, report_date, specimen_number, status as report_status, review_status
            from lab_reports
            where order_id = any(@orderIds)
            order by order_id, id;
            """;
        command.Parameters.AddWithValue("orderIds", orderIds.ToArray());

        var items = new List<PortalLabReportRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new PortalLabReportRow(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                OrderId: reader.GetInt32(reader.GetOrdinal("order_id")),
                DateCollected: ReadNullableDate(reader, "date_collected"),
                ReportDate: ReadNullableDateTime(reader, "report_date"),
                SpecimenNumber: ReadNullableString(reader, "specimen_number"),
                ReportStatus: ReadNullableString(reader, "report_status"),
                ReviewStatus: ReadNullableString(reader, "review_status")));
        }

        return items;
    }

    private static async Task<IReadOnlyList<PortalLabResultRow>> GetPortalLabResultsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<int> reportIds,
        CancellationToken cancellationToken)
    {
        if (reportIds.Count == 0)
        {
            return Array.Empty<PortalLabResultRow>();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, report_id, code as result_code, text as result_name, abnormal, result, range, units, result_status
            from lab_results
            where report_id = any(@reportIds)
            order by report_id, id;
            """;
        command.Parameters.AddWithValue("reportIds", reportIds.ToArray());

        var items = new List<PortalLabResultRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new PortalLabResultRow(
                ReportId: reader.GetInt32(reader.GetOrdinal("report_id")),
                Result: new PatientPortalLabResultItem(
                    Id: reader.GetInt32(reader.GetOrdinal("id")).ToString(),
                    ResultCode: ReadNullableString(reader, "result_code"),
                    ResultName: ReadNullableString(reader, "result_name") ?? string.Empty,
                    Abnormal: ReadNullableString(reader, "abnormal"),
                    Value: ReadNullableString(reader, "result"),
                    Range: ReadNullableString(reader, "range"),
                    Units: ReadNullableString(reader, "units"),
                    ResultStatus: ReadNullableString(reader, "result_status"))));
        }

        return items;
    }

    private static async Task<IReadOnlyList<PatientPortalMedicalReportIssue>> GetMedicalReportIssuesAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, type, type_label, title, begin_date, end_date, status
            from (
              select id, type, 'Medical Problem' as type_label, title, problem_date as begin_date, end_date,
                case when end_date is null then 'active' else 'inactive' end as status
              from problems
              where pid = @pid and activity = 1
              union all
              select id, type, 'Allergy' as type_label, title, allergy_date as begin_date, end_date,
                case when end_date is null then 'active' else 'inactive' end as status
              from allergies
              where pid = @pid and activity = 1
              union all
              select id, type, 'Medication' as type_label, title, medication_date as begin_date, end_date,
                case when end_date is null then 'active' else 'inactive' end as status
              from medications
              where pid = @pid and activity = 1
            ) report_issues
            order by type, begin_date, id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var issues = new List<PatientPortalMedicalReportIssue>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            issues.Add(new PatientPortalMedicalReportIssue(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Type: ReadNullableString(reader, "type") ?? string.Empty,
                TypeLabel: ReadNullableString(reader, "type_label") ?? string.Empty,
                Title: ReadNullableString(reader, "title") ?? string.Empty,
                BeginDate: ReadNullableDate(reader, "begin_date"),
                EndDate: ReadNullableDate(reader, "end_date"),
                Status: ReadNullableString(reader, "status") ?? "active",
                EncounterIds: Array.Empty<int>()));
        }

        return issues;
    }

    private static async Task<IReadOnlyList<PatientPortalMedicalReportEncounter>> GetMedicalReportEncountersAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        var formsByEncounter = await GetMedicalReportEncounterFormsAsync(connection, legacyPid, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select encounter, encounter_date, reason
            from encounters
            where pid = @pid
            order by encounter_date desc, encounter desc;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var encounters = new List<PatientPortalMedicalReportEncounter>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var encounter = reader.GetInt32(reader.GetOrdinal("encounter"));
            var reason = ReadNullableString(reader, "reason");
            var forms = formsByEncounter.GetValueOrDefault(encounter, Array.Empty<PatientPortalMedicalReportEncounterForm>());
            encounters.Add(new PatientPortalMedicalReportEncounter(
                Encounter: encounter,
                Date: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("encounter_date")).ToString("yyyy-MM-dd"),
                Display: BuildEncounterDisplay(reason),
                Reason: reason,
                FormCount: forms.Count,
                Forms: forms));
        }

        return encounters;
    }

    private static async Task<Dictionary<int, IReadOnlyList<PatientPortalMedicalReportEncounterForm>>> GetMedicalReportEncounterFormsAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, encounter, form_directory, display
            from (
              select id::text as id, encounter, 'vitals' as form_directory, 'Vitals' as display, vital_datetime as sort_date
              from vitals
              where pid = @pid and encounter is not null
              union all
              select id::text as id, encounter, 'soap' as form_directory, 'SOAP' as display, note_datetime as sort_date
              from clinical_notes
              where pid = @pid and encounter is not null
              union all
              select id::text as id, encounter, 'procedure_order' as form_directory, 'Procedure Order' as display, order_date::timestamp as sort_date
              from lab_orders
              where pid = @pid and encounter is not null
            ) encounter_forms
            order by encounter, sort_date, form_directory, id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var formsByEncounter = new Dictionary<int, List<PatientPortalMedicalReportEncounterForm>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var encounter = reader.GetInt32(reader.GetOrdinal("encounter"));
            if (!formsByEncounter.TryGetValue(encounter, out var forms))
            {
                forms = [];
                formsByEncounter[encounter] = forms;
            }

            forms.Add(new PatientPortalMedicalReportEncounterForm(
                Id: reader.GetString(reader.GetOrdinal("id")),
                FormDirectory: ReadNullableString(reader, "form_directory") ?? string.Empty,
                Display: ReadNullableString(reader, "display") ?? string.Empty,
                Encounter: encounter));
        }

        return formsByEncounter.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<PatientPortalMedicalReportEncounterForm>)pair.Value);
    }

    private static async Task<IReadOnlyList<PatientPortalMedicalReportProcedureOrder>> GetMedicalReportProcedureOrdersAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              lo.id,
              0 as encounter,
              lo.order_date,
              null::date as encounter_date,
              lo.code as procedure_code,
              lo.name as procedure_name,
              case
                when nullif(btrim(coalesce(lo.diagnosis, '')), '') is null then null
                when position(':' in lo.diagnosis) > 0 then lo.diagnosis
                else 'ICD10:' || lo.diagnosis
              end as diagnosis,
              lo.order_status,
              count(distinct lr.id)::int as report_count,
              count(lres.id)::int as result_count,
              array_remove(array_agg(lres.text order by lres.id), null) as result_names
            from lab_orders lo
            left join lab_reports lr on lr.order_id = lo.id
            left join lab_results lres on lres.report_id = lr.id
            where lo.pid = @pid
            group by lo.id, lo.order_date, lo.code, lo.name, lo.diagnosis, lo.order_status
            order by lo.order_date desc, lo.id desc;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var orders = new List<PatientPortalMedicalReportProcedureOrder>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var resultNames = reader.IsDBNull(reader.GetOrdinal("result_names"))
                ? Array.Empty<string>()
                : reader.GetFieldValue<string[]>(reader.GetOrdinal("result_names"));

            orders.Add(new PatientPortalMedicalReportProcedureOrder(
                Id: reader.GetInt32(reader.GetOrdinal("id")).ToString(),
                Encounter: reader.GetInt32(reader.GetOrdinal("encounter")),
                OrderDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("order_date")).ToString("yyyy-MM-dd"),
                EncounterDate: ReadNullableDate(reader, "encounter_date"),
                ProcedureCode: ReadNullableString(reader, "procedure_code"),
                ProcedureName: ReadNullableString(reader, "procedure_name") ?? string.Empty,
                Diagnosis: ReadNullableString(reader, "diagnosis"),
                OrderStatus: ReadNullableString(reader, "order_status"),
                ReportCount: reader.GetInt32(reader.GetOrdinal("report_count")),
                ResultCount: reader.GetInt32(reader.GetOrdinal("result_count")),
                ResultNames: resultNames));
        }

        return orders;
    }

    private static async Task<GeneratedMedicalReportPatientRow> GetGeneratedMedicalReportPatientAsync(
        NpgsqlConnection connection,
        string canonicalId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              canonical_id,
              pubpid,
              first_name,
              last_name,
              sex,
              date_of_birth,
              street,
              city,
              state,
              postal_code,
              email,
              coalesce(phone_cell, phone_home, phone) as phone
            from patients
            where canonical_id = @canonicalId
            limit 1;
            """;
        command.Parameters.AddWithValue("canonicalId", canonicalId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new GeneratedMedicalReportPatientRow(
                CanonicalId: canonicalId,
                Pubpid: string.Empty,
                FirstName: string.Empty,
                LastName: string.Empty,
                Sex: null,
                DateOfBirth: null,
                Street: null,
                City: null,
                State: null,
                PostalCode: null,
                Email: null,
                Phone: null);
        }

        return new GeneratedMedicalReportPatientRow(
            CanonicalId: reader.GetString(reader.GetOrdinal("canonical_id")),
            Pubpid: ReadNullableString(reader, "pubpid") ?? string.Empty,
            FirstName: ReadNullableString(reader, "first_name") ?? string.Empty,
            LastName: ReadNullableString(reader, "last_name") ?? string.Empty,
            Sex: ReadNullableString(reader, "sex"),
            DateOfBirth: ReadNullableDate(reader, "date_of_birth"),
            Street: ReadNullableString(reader, "street"),
            City: ReadNullableString(reader, "city"),
            State: ReadNullableString(reader, "state"),
            PostalCode: ReadNullableString(reader, "postal_code"),
            Email: ReadNullableString(reader, "email"),
            Phone: ReadNullableString(reader, "phone"));
    }

    private static async Task<GeneratedMedicalReportBillingRow> GetGeneratedMedicalReportBillingAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with charges as (
              select
                count(*)::int as line_count,
                coalesce(sum(coalesce(fee, 0)), 0)::numeric(12,2) as charge_amount,
                max(billing_date) as last_billing_date
              from billing
              where pid = @pid and activity = 1
            ),
            payments as (
              select
                count(*)::int as payment_count,
                coalesce(sum(pay_amount), 0)::numeric(12,2) as payment_amount,
                coalesce(sum(adj_amount), 0)::numeric(12,2) as adjustment_amount
              from payment_activities
              where pid = @pid and deleted is null
            )
            select
              charges.line_count,
              payments.payment_count,
              charges.charge_amount,
              payments.payment_amount,
              payments.adjustment_amount,
              (charges.charge_amount - payments.payment_amount - payments.adjustment_amount)::numeric(12,2) as balance_amount,
              charges.last_billing_date
            from charges
            cross join payments;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new GeneratedMedicalReportBillingRow(0, 0, 0m, 0m, 0m, 0m, null);
        }

        return new GeneratedMedicalReportBillingRow(
            LineCount: reader.GetInt32(reader.GetOrdinal("line_count")),
            PaymentCount: reader.GetInt32(reader.GetOrdinal("payment_count")),
            ChargeAmount: reader.GetDecimal(reader.GetOrdinal("charge_amount")),
            PaymentAmount: reader.GetDecimal(reader.GetOrdinal("payment_amount")),
            AdjustmentAmount: reader.GetDecimal(reader.GetOrdinal("adjustment_amount")),
            BalanceAmount: reader.GetDecimal(reader.GetOrdinal("balance_amount")),
            LastBillingDate: ReadNullableDate(reader, "last_billing_date"));
    }

    private static async Task<GeneratedMedicalReportFacilityRow> GetGeneratedMedicalReportFacilityAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              name,
              phone,
              street,
              city,
              state,
              postal_code
            from facilities
            where inactive = false
            order by case when code = 'MAIN' then 0 else 1 end, id
            limit 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new GeneratedMedicalReportFacilityRow(
                Name: string.Empty,
                Phone: string.Empty,
                Street: string.Empty,
                City: string.Empty,
                State: string.Empty,
                PostalCode: string.Empty);
        }

        return new GeneratedMedicalReportFacilityRow(
            Name: ReadNullableString(reader, "name") ?? string.Empty,
            Phone: ReadNullableString(reader, "phone") ?? string.Empty,
            Street: ReadNullableString(reader, "street") ?? string.Empty,
            City: ReadNullableString(reader, "city") ?? string.Empty,
            State: ReadNullableString(reader, "state") ?? string.Empty,
            PostalCode: ReadNullableString(reader, "postal_code") ?? string.Empty);
    }

    private static PatientPortalGeneratedMedicalReportResponse BuildGeneratedMedicalReportResponse(
        PatientPortalSessionResponse session,
        DatasetMetadata metadata,
        GeneratedMedicalReportPatientRow patient,
        GeneratedMedicalReportFacilityRow facility,
        GeneratedMedicalReportBillingRow billing,
        IReadOnlyList<PatientPortalMedicalReportIssue> issues,
        IReadOnlyList<PatientPortalMedicalReportEncounter> encounters,
        IReadOnlyList<PatientPortalMedicalReportProcedureOrder> procedureOrders,
        PatientPortalMedicalReportGenerationRequest request)
    {
        var validSectionIds = MedicalReportSections
            .Select(section => section.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requestedSectionIds = request.SectionIds is null
            ? null
            : request.SectionIds
            .Select(sectionId => sectionId.Trim())
            .Where(sectionId => validSectionIds.Contains(sectionId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var includedSectionIds = requestedSectionIds is null
            ? MedicalReportSections.Where(section => section.Selected).Select(section => section.Id).ToArray()
            : requestedSectionIds;
        var includedSectionIdSet = includedSectionIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requestedProcedureOrderIds = request.ProcedureOrderIds is null
            ? null
            : request.ProcedureOrderIds
            .Select(orderId => orderId.Trim())
            .Where(orderId => !string.IsNullOrWhiteSpace(orderId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var requestedProcedureOrderIdSet = requestedProcedureOrderIds?.ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var includedProcedureOrders = requestedProcedureOrderIds is null
            ? procedureOrders.Take(1).ToArray()
            : procedureOrders.Where(order => requestedProcedureOrderIdSet.Contains(order.Id)).ToArray();
        var includedProcedureOrderIds = includedProcedureOrders.Select(order => order.Id).ToArray();

        var requestedIssueIds = request.IssueIds is null
            ? Array.Empty<string>()
            : request.IssueIds
                .Select(issueId => issueId.Trim())
                .Where(issueId => !string.IsNullOrWhiteSpace(issueId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        var requestedIssueIdSet = requestedIssueIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var includedIssues = issues
            .Where(issue => requestedIssueIdSet.Contains(issue.Id))
            .ToArray();
        var includedIssueIds = includedIssues.Select(issue => issue.Id).ToArray();

        var requestedEncounterFormIds = request.EncounterFormIds is null
            ? Array.Empty<string>()
            : request.EncounterFormIds
                .Select(formId => formId.Trim())
                .Where(formId => !string.IsNullOrWhiteSpace(formId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        var requestedEncounterFormIdSet = requestedEncounterFormIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var availableEncounterForms = encounters
            .SelectMany(encounter => encounter.Forms.Select(form => (
                Encounter: encounter,
                Form: form,
                SelectionId: BuildEncounterFormSelectionId(form))))
            .ToArray();
        var includedEncounterForms = availableEncounterForms
            .Where(item => requestedEncounterFormIdSet.Contains(item.SelectionId))
            .ToArray();
        var includedEncounterFormIds = includedEncounterForms.Select(item => item.SelectionId).ToArray();

        var reportSections = new List<PatientPortalGeneratedMedicalReportSection>();
        foreach (var section in MedicalReportSections.Where(section => includedSectionIdSet.Contains(section.Id)))
        {
            switch (section.Id)
            {
                case "demographics":
                    reportSections.Add(BuildGeneratedReportSection(
                        section.Id,
                        "Patient Data",
                        [
                            $"Patient: {session.DisplayName}",
                            $"Patient ID: {session.Pubpid}",
                            $"Date of birth: {patient.DateOfBirth ?? "Not recorded"}",
                            $"Sex: {patient.Sex ?? "Not recorded"}",
                            $"Address: {FormatAddress(patient)}",
                            $"Phone: {patient.Phone ?? "Not recorded"}",
                            $"Email: {patient.Email ?? "Not recorded"}"
                        ]));
                    break;
                case "billing":
                    reportSections.Add(BuildGeneratedReportSection(
                        section.Id,
                        "Billing Information",
                        [
                            $"Billing lines: {billing.LineCount}",
                            $"Payment rows: {billing.PaymentCount}",
                            $"Total charges: {FormatMoney(billing.ChargeAmount)}",
                            $"Payments: {FormatMoney(billing.PaymentAmount)}",
                            $"Adjustments: {FormatMoney(billing.AdjustmentAmount)}",
                            $"Balance: {FormatMoney(billing.BalanceAmount)}",
                            $"Last billing date: {billing.LastBillingDate ?? "Not recorded"}"
                        ]));
                    break;
                case "allergies":
                    reportSections.Add(BuildIssueGeneratedReportSection(section.Id, "Patient Allergies", issues, "allergy"));
                    break;
                case "medications":
                    reportSections.Add(BuildIssueGeneratedReportSection(section.Id, "Patient Medications", issues, "medication"));
                    break;
                case "medical_problems":
                    reportSections.Add(BuildIssueGeneratedReportSection(section.Id, "Patient Medical Problems", issues, "medical_problem"));
                    break;
                case "history":
                    reportSections.Add(BuildGeneratedReportSection(
                        section.Id,
                        "History Data",
                        [
                            $"Issues available: {issues.Count}",
                            $"Encounters available: {encounters.Count}"
                        ]));
                    break;
                default:
                    reportSections.Add(BuildGeneratedReportSection(
                        section.Id,
                        section.Label,
                        [$"{section.Label} was selected for the customized medical history report."]));
                    break;
            }
        }

        if (includedIssues.Length > 0)
        {
            reportSections.Add(BuildSelectedIssuesGeneratedReportSection(includedIssues));
        }

        if (includedEncounterForms.Length > 0)
        {
            reportSections.Add(BuildSelectedEncounterFormsGeneratedReportSection(includedEncounterForms));
        }

        foreach (var order in includedProcedureOrders)
        {
            reportSections.Add(BuildGeneratedReportSection(
                $"procedure-{order.Id}",
                "Procedure Order",
                [
                    $"Order: {order.ProcedureName}",
                    $"Order date: {order.OrderDate}",
                    $"Encounter: {(order.Encounter == 0 ? "Not linked" : order.Encounter.ToString())}",
                    $"Code: {order.ProcedureCode ?? "Not recorded"}",
                    $"Diagnosis: {order.Diagnosis ?? "Not recorded"}",
                    $"Status: {order.OrderStatus ?? "Not recorded"}",
                    $"Reports: {order.ReportCount}",
                    $"Results: {string.Join(", ", order.ResultNames)}"
                ]));
        }

        var summaryLines = new List<string>
        {
            $"Patient Data: {session.DisplayName} ({session.Pubpid})",
            $"Billing Information: {billing.LineCount} lines; balance {FormatMoney(billing.BalanceAmount)}.",
            $"Issues available: {issues.Count}; Encounters available: {encounters.Count}."
        };
        summaryLines.AddRange(includedProcedureOrders.Select(order =>
            $"Procedure Order: {order.ProcedureName} ordered {order.OrderDate} with {order.ResultCount} result rows."));
        if (includedIssues.Length > 0)
        {
            summaryLines.Add($"Issues: {includedIssues.Length} selected for this customized report.");
        }
        if (includedEncounterForms.Length > 0)
        {
            summaryLines.Add($"Encounter Forms: {includedEncounterForms.Length} selected for this customized report.");
        }

        var generatedOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var templateMetadata = BuildGeneratedMedicalReportTemplateMetadata(patient, facility, generatedOn);
        var packageMetadata = BuildGeneratedMedicalReportPackageMetadata(session.Pubpid, session.CanonicalId, generatedOn);

        return new PatientPortalGeneratedMedicalReportResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            Title: "Customized Medical History Report",
            GeneratedOn: generatedOn,
            TemplateMetadata: templateMetadata,
            IncludedSectionIds: includedSectionIds,
            IncludedProcedureOrderIds: includedProcedureOrderIds,
            IncludedIssueIds: includedIssueIds,
            IncludedEncounterFormIds: includedEncounterFormIds,
            PrintableVersionAvailable: true,
            PdfDownloadAvailable: true,
            PackageDownloadAvailable: true,
            PackageMetadata: packageMetadata,
            ReportSectionCount: reportSections.Count,
            ReportSections: reportSections,
            SummaryLineCount: summaryLines.Count,
            SummaryLines: summaryLines,
            AuditEventCount: 0,
            AuditEvents: Array.Empty<PatientPortalGeneratedMedicalReportAuditEvent>(),
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    private static PatientPortalGeneratedMedicalReportPdfPackage GeneratedMedicalReportPdfFailure(
        string reason,
        PatientPortalGeneratedMedicalReportResponse? report = null) => new(
        Downloadable: false,
        FileName: "customized-medical-history-report.pdf",
        ContentType: "application/pdf",
        Content: Array.Empty<byte>(),
        ContentLength: 0,
        Report: report,
        FailureReason: reason);

    private static PatientPortalGeneratedMedicalReportPackageDownload GeneratedMedicalReportPackageFailure(
        string reason,
        PatientPortalGeneratedMedicalReportResponse? report = null) => new(
        Downloadable: false,
        FileName: "customized-medical-history-report.zip",
        ContentType: "application/zip",
        Content: Array.Empty<byte>(),
        ContentLength: 0,
        Report: report,
        FailureReason: reason);

    private static async Task RecordPortalMessageAuditEventAsync(
        NpgsqlConnection connection,
        PatientPortalSessionResponse session,
        string eventType,
        PatientPortalMessageItem message,
        IReadOnlyList<PatientPortalMessageItem> relatedMessages,
        int archivedMessageCount,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return;
        }

        var relatedMessageIds = relatedMessages
            .Select(relatedMessage => relatedMessage.Id)
            .Where(relatedMessageId => !string.IsNullOrWhiteSpace(relatedMessageId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var status = relatedMessages.FirstOrDefault()?.Status ?? message.Status;
        var threadId = GetPortalMessageAuditThreadId(message);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into patient_portal_message_audit_events (
              patient_id,
              pid,
              session_id,
              portal_username,
              portal_login_username,
              event_type,
              event_label,
              message_id,
              related_message_ids,
              message_title,
              message_status,
              recipient_id,
              recipient_name,
              thread_id,
              archived_message_count,
              summary,
              event_source
            )
            values (
              @patient_id,
              @pid,
              @session_id,
              @portal_username,
              @portal_login_username,
              @event_type,
              @event_label,
              @message_id,
              @related_message_ids,
              @message_title,
              @message_status,
              @recipient_id,
              @recipient_name,
              @thread_id,
              @archived_message_count,
              @summary,
              @event_source
            );
            """;
        command.Parameters.Add("patient_id", NpgsqlDbType.Text).Value = session.CanonicalId;
        command.Parameters.Add("pid", NpgsqlDbType.Integer).Value = session.LegacyPid.Value;
        command.Parameters.Add("session_id", NpgsqlDbType.Uuid).Value = session.SessionId is { } sessionId
            ? sessionId
            : DBNull.Value;
        command.Parameters.Add("portal_username", NpgsqlDbType.Text).Value = session.PortalUsername;
        command.Parameters.Add("portal_login_username", NpgsqlDbType.Text).Value = session.Username;
        command.Parameters.Add("event_type", NpgsqlDbType.Text).Value = eventType;
        command.Parameters.Add("event_label", NpgsqlDbType.Text).Value = GetPortalMessageAuditEventLabel(eventType);
        command.Parameters.Add("message_id", NpgsqlDbType.Text).Value = message.Id;
        command.Parameters.Add("related_message_ids", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = relatedMessageIds;
        command.Parameters.Add("message_title", NpgsqlDbType.Text).Value = message.Title;
        command.Parameters.Add("message_status", NpgsqlDbType.Text).Value = status;
        command.Parameters.Add("recipient_id", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(message.RecipientId)
            ? DBNull.Value
            : message.RecipientId;
        command.Parameters.Add("recipient_name", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(message.RecipientName)
            ? DBNull.Value
            : message.RecipientName;
        command.Parameters.Add("thread_id", NpgsqlDbType.Integer).Value = threadId;
        command.Parameters.Add("archived_message_count", NpgsqlDbType.Integer).Value = archivedMessageCount;
        command.Parameters.Add("summary", NpgsqlDbType.Text).Value =
            BuildPortalMessageAuditSummary(eventType, message, relatedMessageIds.Length, archivedMessageCount);
        command.Parameters.Add("event_source", NpgsqlDbType.Text).Value = session.SessionSource;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<PatientPortalMessageAuditEvent>> GetPortalMessageAuditEventsAsync(
        NpgsqlConnection connection,
        string canonicalId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              id,
              event_type,
              event_label,
              created_at,
              message_id,
              related_message_ids,
              message_title,
              message_status,
              recipient_id,
              recipient_name,
              thread_id,
              archived_message_count,
              summary,
              event_source
            from patient_portal_message_audit_events
            where patient_id = @patient_id
            order by created_at, id;
            """;
        command.Parameters.Add("patient_id", NpgsqlDbType.Text).Value = canonicalId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var events = new List<PatientPortalMessageAuditEvent>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var createdAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at"));
            events.Add(new PatientPortalMessageAuditEvent(
                Id: reader.GetInt64(reader.GetOrdinal("id")),
                EventType: reader.GetString(reader.GetOrdinal("event_type")),
                EventLabel: reader.GetString(reader.GetOrdinal("event_label")),
                EventAt: createdAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'"),
                MessageId: reader.GetString(reader.GetOrdinal("message_id")),
                RelatedMessageIds: ReadStringArray(reader, "related_message_ids"),
                MessageTitle: reader.GetString(reader.GetOrdinal("message_title")),
                MessageStatus: reader.GetString(reader.GetOrdinal("message_status")),
                RecipientId: ReadNullableString(reader, "recipient_id"),
                RecipientName: ReadNullableString(reader, "recipient_name"),
                ThreadId: reader.GetInt32(reader.GetOrdinal("thread_id")),
                ArchivedMessageCount: reader.GetInt32(reader.GetOrdinal("archived_message_count")),
                Summary: reader.GetString(reader.GetOrdinal("summary")),
                EventSource: reader.GetString(reader.GetOrdinal("event_source"))));
        }

        return events;
    }

    private static string GetPortalMessageAuditEventLabel(string eventType) => eventType switch
    {
        PortalMessageComposedEventType => "Message composed",
        PortalMessageRepliedEventType => "Message replied",
        PortalMessageForwardedEventType => "Message forwarded",
        PortalPrescriptionRefillRequestedEventType => "Prescription refill requested",
        PortalMessageReadEventType => "Message marked read",
        PortalMessageArchivedEventType => "Message archived",
        PortalMessagesArchivedEventType => "Messages archived",
        _ => eventType
    };

    private static string BuildPortalMessageAuditSummary(
        string eventType,
        PatientPortalMessageItem message,
        int relatedMessageCount,
        int archivedMessageCount) => eventType switch
        {
            PortalMessageComposedEventType =>
                $"Composed secure message \"{message.Title}\" to {FormatPortalMessageRecipient(message)} with {relatedMessageCount} mailbox rows.",
            PortalMessageRepliedEventType =>
                $"Replied to secure message \"{message.Title}\" in thread {GetPortalMessageAuditThreadId(message)}.",
            PortalMessageForwardedEventType =>
                $"Forwarded secure message \"{message.Title}\" to {FormatPortalMessageForwardAssignee(message)}.",
            PortalPrescriptionRefillRequestedEventType =>
                $"Requested prescription refill through secure message \"{message.Title}\" with {relatedMessageCount} mailbox rows.",
            PortalMessageReadEventType =>
                $"Marked secure message \"{message.Title}\" read.",
            PortalMessageArchivedEventType =>
                $"Archived secure message \"{message.Title}\" with {archivedMessageCount} mailbox rows.",
            PortalMessagesArchivedEventType =>
                $"Archived {archivedMessageCount} selected secure-message mailbox rows starting from \"{message.Title}\".",
            _ =>
                $"{GetPortalMessageAuditEventLabel(eventType)} for secure message \"{message.Title}\"."
        };

    private static int GetPortalMessageAuditThreadId(PatientPortalMessageItem message)
    {
        if (message.ReplyMailChain > 0)
        {
            return message.ReplyMailChain;
        }

        if (message.MailChain > 0)
        {
            return message.MailChain;
        }

        return int.TryParse(message.Id, out var parsedId) ? parsedId : 0;
    }

    private static string FormatPortalMessageRecipient(PatientPortalMessageItem message)
    {
        if (!string.IsNullOrWhiteSpace(message.RecipientName))
        {
            return message.RecipientName;
        }

        return string.IsNullOrWhiteSpace(message.RecipientId) ? "care team" : message.RecipientId;
    }

    private static string FormatPortalMessageForwardAssignee(PatientPortalMessageItem message) =>
        string.IsNullOrWhiteSpace(message.AssignedTo) ? "care team" : message.AssignedTo;

    private static async Task RecordGeneratedMedicalReportAuditEventAsync(
        NpgsqlConnection connection,
        PatientPortalGeneratedMedicalReportResponse report,
        string eventType,
        string? artifactName,
        string? artifactContentType,
        CancellationToken cancellationToken)
    {
        if (!report.Authenticated || report.LegacyPid is null)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into patient_portal_report_audit_events (
              patient_id,
              pid,
              session_id,
              portal_username,
              portal_login_username,
              event_type,
              event_label,
              report_title,
              generated_on,
              artifact_name,
              artifact_content_type,
              included_section_ids,
              included_issue_ids,
              included_encounter_form_ids,
              included_procedure_order_ids,
              summary,
              event_source
            )
            values (
              @patient_id,
              @pid,
              @session_id,
              @portal_username,
              @portal_login_username,
              @event_type,
              @event_label,
              @report_title,
              @generated_on,
              @artifact_name,
              @artifact_content_type,
              @included_section_ids,
              @included_issue_ids,
              @included_encounter_form_ids,
              @included_procedure_order_ids,
              @summary,
              @event_source
            );
            """;
        command.Parameters.Add("patient_id", NpgsqlDbType.Text).Value = report.CanonicalId;
        command.Parameters.Add("pid", NpgsqlDbType.Integer).Value = report.LegacyPid.Value;
        command.Parameters.Add("session_id", NpgsqlDbType.Uuid).Value = report.SessionId is { } sessionId
            ? sessionId
            : DBNull.Value;
        command.Parameters.Add("portal_username", NpgsqlDbType.Text).Value = report.PortalUsername;
        command.Parameters.Add("portal_login_username", NpgsqlDbType.Text).Value = report.Username;
        command.Parameters.Add("event_type", NpgsqlDbType.Text).Value = eventType;
        command.Parameters.Add("event_label", NpgsqlDbType.Text).Value = GetGeneratedMedicalReportAuditEventLabel(eventType);
        command.Parameters.Add("report_title", NpgsqlDbType.Text).Value = report.Title;
        command.Parameters.Add("generated_on", NpgsqlDbType.Date).Value = DateOnly.TryParse(report.GeneratedOn, out var generatedOn)
            ? generatedOn
            : DateOnly.FromDateTime(DateTime.UtcNow);
        command.Parameters.Add("artifact_name", NpgsqlDbType.Text).Value = artifactName is null
            ? DBNull.Value
            : artifactName;
        command.Parameters.Add("artifact_content_type", NpgsqlDbType.Text).Value = artifactContentType is null
            ? DBNull.Value
            : artifactContentType;
        command.Parameters.Add("included_section_ids", NpgsqlDbType.Array | NpgsqlDbType.Text)
            .Value = report.IncludedSectionIds.ToArray();
        command.Parameters.Add("included_issue_ids", NpgsqlDbType.Array | NpgsqlDbType.Text)
            .Value = report.IncludedIssueIds.ToArray();
        command.Parameters.Add("included_encounter_form_ids", NpgsqlDbType.Array | NpgsqlDbType.Text)
            .Value = report.IncludedEncounterFormIds.ToArray();
        command.Parameters.Add("included_procedure_order_ids", NpgsqlDbType.Array | NpgsqlDbType.Text)
            .Value = report.IncludedProcedureOrderIds.ToArray();
        command.Parameters.Add("summary", NpgsqlDbType.Text)
            .Value = BuildGeneratedMedicalReportAuditSummary(eventType, report, artifactName);
        command.Parameters.Add("event_source", NpgsqlDbType.Text).Value = report.SessionSource;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<PatientPortalGeneratedMedicalReportAuditEvent>> GetGeneratedMedicalReportAuditEventsAsync(
        NpgsqlConnection connection,
        string canonicalId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              id,
              event_type,
              event_label,
              created_at,
              report_title,
              generated_on,
              artifact_name,
              artifact_content_type,
              included_section_ids,
              included_issue_ids,
              included_encounter_form_ids,
              included_procedure_order_ids,
              summary,
              event_source
            from patient_portal_report_audit_events
            where patient_id = @patient_id
            order by created_at, id;
            """;
        command.Parameters.Add("patient_id", NpgsqlDbType.Text).Value = canonicalId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var events = new List<PatientPortalGeneratedMedicalReportAuditEvent>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var createdAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at"));
            events.Add(new PatientPortalGeneratedMedicalReportAuditEvent(
                Id: reader.GetInt64(reader.GetOrdinal("id")),
                EventType: reader.GetString(reader.GetOrdinal("event_type")),
                EventLabel: reader.GetString(reader.GetOrdinal("event_label")),
                EventAt: createdAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'"),
                ReportTitle: reader.GetString(reader.GetOrdinal("report_title")),
                GeneratedOn: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("generated_on")).ToString("yyyy-MM-dd"),
                ArtifactName: ReadNullableString(reader, "artifact_name"),
                ArtifactContentType: ReadNullableString(reader, "artifact_content_type"),
                IncludedSectionIds: ReadStringArray(reader, "included_section_ids"),
                IncludedIssueIds: ReadStringArray(reader, "included_issue_ids"),
                IncludedEncounterFormIds: ReadStringArray(reader, "included_encounter_form_ids"),
                IncludedProcedureOrderIds: ReadStringArray(reader, "included_procedure_order_ids"),
                Summary: reader.GetString(reader.GetOrdinal("summary")),
                EventSource: reader.GetString(reader.GetOrdinal("event_source"))));
        }

        return events;
    }

    private static string GetGeneratedMedicalReportAuditEventLabel(string eventType) => eventType switch
    {
        GeneratedMedicalReportCreatedEventType => "Generated report",
        GeneratedMedicalReportPdfDownloadedEventType => "PDF downloaded",
        GeneratedMedicalReportPackageDownloadedEventType => "Package downloaded",
        _ => eventType
    };

    private static string BuildGeneratedMedicalReportAuditSummary(
        string eventType,
        PatientPortalGeneratedMedicalReportResponse report,
        string? artifactName)
    {
        var selectionSummary = $"{report.IncludedSectionIds.Count} sections, {report.IncludedIssueIds.Count} issues, "
            + $"{report.IncludedEncounterFormIds.Count} forms, {report.IncludedProcedureOrderIds.Count} procedure orders";
        return eventType switch
        {
            GeneratedMedicalReportCreatedEventType =>
                $"Generated {report.Title} with {selectionSummary}.",
            GeneratedMedicalReportPdfDownloadedEventType =>
                $"Downloaded PDF {artifactName ?? BuildGeneratedMedicalReportPdfFileName(report)} for {report.Title} with {selectionSummary}.",
            GeneratedMedicalReportPackageDownloadedEventType =>
                $"Downloaded package {artifactName ?? BuildGeneratedMedicalReportPackageFileName(report)} for {report.Title} with {selectionSummary}.",
            _ => $"{GetGeneratedMedicalReportAuditEventLabel(eventType)} for {report.Title} with {selectionSummary}."
        };
    }

    private static string BuildGeneratedMedicalReportPdfFileName(PatientPortalGeneratedMedicalReportResponse report)
    {
        return $"{BuildGeneratedMedicalReportFileStem(report.Pubpid, report.CanonicalId, report.GeneratedOn)}.pdf";
    }

    private static string BuildGeneratedMedicalReportPackageFileName(PatientPortalGeneratedMedicalReportResponse report)
    {
        return $"{BuildGeneratedMedicalReportFileStem(report.Pubpid, report.CanonicalId, report.GeneratedOn)}.zip";
    }

    private static string BuildGeneratedMedicalReportFileStem(string pubpid, string canonicalId, string generatedOn)
    {
        var patientId = string.IsNullOrWhiteSpace(pubpid) ? canonicalId : pubpid;
        var generatedDate = string.IsNullOrWhiteSpace(generatedOn)
            ? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyyMMdd")
            : generatedOn.Replace("-", string.Empty, StringComparison.Ordinal);
        return $"medical-report-{patientId}-{generatedDate}";
    }

    private static PatientPortalGeneratedMedicalReportPackageMetadata EmptyGeneratedMedicalReportPackageMetadata() => new(
        FileName: string.Empty,
        ContentType: string.Empty,
        EntryNames: Array.Empty<string>(),
        ManifestAvailable: false,
        PdfAvailable: false,
        SummaryAvailable: false);

    private static PatientPortalGeneratedMedicalReportPackageMetadata BuildGeneratedMedicalReportPackageMetadata(
        string pubpid,
        string canonicalId,
        string generatedOn)
    {
        var pdfFileName = $"{BuildGeneratedMedicalReportFileStem(pubpid, canonicalId, generatedOn)}.pdf";
        return new PatientPortalGeneratedMedicalReportPackageMetadata(
            FileName: $"{BuildGeneratedMedicalReportFileStem(pubpid, canonicalId, generatedOn)}.zip",
            ContentType: "application/zip",
            EntryNames:
            [
                "manifest.json",
                pdfFileName,
                "summary.txt"
            ],
            ManifestAvailable: true,
            PdfAvailable: true,
            SummaryAvailable: true);
    }

    private static byte[] BuildGeneratedMedicalReportPackage(PatientPortalGeneratedMedicalReportResponse report)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var timestamp = BuildGeneratedMedicalReportPackageTimestamp(report.GeneratedOn);
            AddGeneratedMedicalReportPackageEntry(
                archive,
                "manifest.json",
                Encoding.UTF8.GetBytes(BuildGeneratedMedicalReportPackageManifest(report)),
                timestamp);
            AddGeneratedMedicalReportPackageEntry(
                archive,
                BuildGeneratedMedicalReportPdfFileName(report),
                BuildGeneratedMedicalReportPdf(report),
                timestamp);
            AddGeneratedMedicalReportPackageEntry(
                archive,
                "summary.txt",
                Encoding.UTF8.GetBytes(BuildGeneratedMedicalReportPackageSummary(report)),
                timestamp);
        }

        return stream.ToArray();
    }

    private static DateTimeOffset BuildGeneratedMedicalReportPackageTimestamp(string generatedOn)
    {
        if (DateOnly.TryParse(generatedOn, out var generatedDate))
        {
            return new DateTimeOffset(generatedDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        return DateTimeOffset.UtcNow;
    }

    private static void AddGeneratedMedicalReportPackageEntry(
        ZipArchive archive,
        string entryName,
        byte[] content,
        DateTimeOffset timestamp)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        entry.LastWriteTime = timestamp;
        using var entryStream = entry.Open();
        entryStream.Write(content);
    }

    private static string BuildGeneratedMedicalReportPackageManifest(PatientPortalGeneratedMedicalReportResponse report)
    {
        var manifest = new
        {
            packageId = Path.GetFileNameWithoutExtension(BuildGeneratedMedicalReportPackageFileName(report)),
            packageFileName = BuildGeneratedMedicalReportPackageFileName(report),
            contentType = "application/zip",
            generatedOn = report.GeneratedOn,
            dataset = new
            {
                report.DatasetId,
                report.DatasetVersion,
                report.AsOfDate
            },
            patient = new
            {
                report.CanonicalId,
                report.LegacyPid,
                report.Pubpid,
                report.DisplayName,
                report.PortalUsername
            },
            report = new
            {
                report.Title,
                report.TemplateMetadata,
                report.IncludedSectionIds,
                report.IncludedProcedureOrderIds,
                report.IncludedIssueIds,
                report.IncludedEncounterFormIds,
                report.ReportSectionCount,
                report.SummaryLineCount
            },
            entries = report.PackageMetadata.EntryNames.Select(entryName => new
            {
                name = entryName,
                kind = entryName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? "generated-report-pdf"
                    : Path.GetFileNameWithoutExtension(entryName)
            }).ToArray()
        };

        return JsonSerializer.Serialize(manifest, GeneratedMedicalReportPackageJsonOptions);
    }

    private static string BuildGeneratedMedicalReportPackageSummary(PatientPortalGeneratedMedicalReportResponse report)
    {
        var summary = new StringBuilder();
        summary.AppendLine(report.Title);
        summary.AppendLine($"Patient: {report.DisplayName} ({report.Pubpid})");
        summary.AppendLine($"Generated on: {report.GeneratedOn}");
        summary.AppendLine($"Dataset: {report.DatasetId} {report.DatasetVersion}");
        summary.AppendLine($"Included sections: {string.Join(", ", report.IncludedSectionIds)}");
        summary.AppendLine($"Included procedure orders: {string.Join(", ", report.IncludedProcedureOrderIds)}");
        summary.AppendLine($"Included issues: {string.Join(", ", report.IncludedIssueIds)}");
        summary.AppendLine($"Included encounter forms: {string.Join(", ", report.IncludedEncounterFormIds)}");
        summary.AppendLine();
        summary.AppendLine("Summary");
        foreach (var line in report.SummaryLines)
        {
            summary.AppendLine(line);
        }

        return summary.ToString();
    }

    private static byte[] BuildGeneratedMedicalReportPdf(PatientPortalGeneratedMedicalReportResponse report)
    {
        var lines = new List<string>
        {
            report.Title,
            $"Patient: {report.DisplayName}",
            $"Patient ID: {report.Pubpid}",
            $"Portal username: {report.PortalUsername}",
            $"Generated on: {report.GeneratedOn}",
            $"Dataset: {report.DatasetId} {report.DatasetVersion}",
            $"Facility: {report.TemplateMetadata.FacilityName}",
            $"Facility address: {report.TemplateMetadata.FacilityStreet}; {report.TemplateMetadata.FacilityCityStatePostal}",
            $"Facility phone: {report.TemplateMetadata.FacilityPhone}",
            $"Printable patient: {report.TemplateMetadata.PrintablePatientName}",
            report.TemplateMetadata.PatientHeaderLine,
            report.TemplateMetadata.GeneratedOnLabel,
            $"Included sections: {string.Join(", ", report.IncludedSectionIds)}",
            $"Included procedure orders: {string.Join(", ", report.IncludedProcedureOrderIds)}",
            $"Included issues: {string.Join(", ", report.IncludedIssueIds)}",
            $"Included encounter forms: {string.Join(", ", report.IncludedEncounterFormIds)}",
            string.Empty,
            "Summary"
        };
        lines.AddRange(report.SummaryLines);
        if (report.TemplateMetadata.SignatureLineAvailable)
        {
            lines.Add("Signature: _______________________________");
        }

        foreach (var section in report.ReportSections)
        {
            lines.Add(string.Empty);
            lines.Add(section.Title);
            lines.AddRange(section.Lines);
        }

        return BuildSimplePdf(lines);
    }

    private static byte[] BuildSimplePdf(IReadOnlyList<string> lines)
    {
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("BT");
        contentBuilder.AppendLine("/F1 10 Tf");
        contentBuilder.AppendLine("50 760 Td");
        foreach (var line in lines)
        {
            contentBuilder.Append('(');
            contentBuilder.Append(EscapePdfText(line));
            contentBuilder.AppendLine(") Tj");
            contentBuilder.AppendLine("0 -14 Td");
        }
        contentBuilder.AppendLine("ET");

        var content = contentBuilder.ToString();
        var contentLength = Encoding.ASCII.GetByteCount(content);
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {contentLength} >>\nstream\n{content}endstream"
        };

        var pdf = new StringBuilder();
        var offsets = new List<int>();
        pdf.AppendLine("%PDF-1.4");
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(i + 1);
            pdf.AppendLine(" 0 obj");
            pdf.AppendLine(objects[i]);
            pdf.AppendLine("endobj");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {objects.Length + 1}");
        pdf.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets)
        {
            pdf.AppendLine($"{offset:0000000000} 00000 n ");
        }

        pdf.AppendLine("trailer");
        pdf.AppendLine($"<< /Size {objects.Length + 1} /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefOffset.ToString());
        pdf.Append("%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string EscapePdfText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static PatientPortalGeneratedMedicalReportSection BuildSelectedIssuesGeneratedReportSection(
        IReadOnlyList<PatientPortalMedicalReportIssue> issues)
    {
        var lines = issues
            .Select(issue =>
                $"{issue.TypeLabel}: {issue.Title} ({issue.Status}; begin {issue.BeginDate ?? "Not recorded"}; end {issue.EndDate ?? "Active"})")
            .ToArray();

        return BuildGeneratedReportSection("issues", "Issues", lines);
    }

    private static PatientPortalGeneratedMedicalReportSection BuildSelectedEncounterFormsGeneratedReportSection(
        IReadOnlyList<(PatientPortalMedicalReportEncounter Encounter, PatientPortalMedicalReportEncounterForm Form, string SelectionId)> encounterForms)
    {
        var lines = encounterForms
            .Select(item =>
                $"{item.Form.Display}: Encounter {item.Encounter.Encounter} on {item.Encounter.Date}; form {item.Form.Id}; directory {item.Form.FormDirectory}; reason {FormatEncounterReason(item.Encounter)}")
            .ToArray();

        return BuildGeneratedReportSection("encounter-forms", "Encounter Forms", lines);
    }

    private static string BuildEncounterFormSelectionId(PatientPortalMedicalReportEncounterForm form) =>
        $"{form.FormDirectory}_{form.Id}";

    private static string FormatEncounterReason(PatientPortalMedicalReportEncounter encounter) =>
        string.IsNullOrWhiteSpace(encounter.Reason) ? "Not recorded" : encounter.Reason;

    private static PatientPortalGeneratedMedicalReportSection BuildIssueGeneratedReportSection(
        string id,
        string title,
        IReadOnlyList<PatientPortalMedicalReportIssue> issues,
        string issueType)
    {
        var lines = issues
            .Where(issue => string.Equals(issue.Type, issueType, StringComparison.OrdinalIgnoreCase))
            .Select(issue => $"{issue.Title} ({issue.Status}; begin {issue.BeginDate ?? "Not recorded"})")
            .DefaultIfEmpty("None recorded")
            .ToArray();

        return BuildGeneratedReportSection(id, title, lines);
    }

    private static PatientPortalGeneratedMedicalReportSection BuildGeneratedReportSection(
        string id,
        string title,
        IReadOnlyList<string> lines) => new(
        Id: id,
        Title: title,
        LineCount: lines.Count,
        Lines: lines);

    private static string FormatAddress(GeneratedMedicalReportPatientRow patient)
    {
        var locality = string.Join(
            " ",
            new[] { patient.City, patient.State, patient.PostalCode }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var address = string.Join(
            ", ",
            new[] { patient.Street, locality }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(address) ? "Not recorded" : address;
    }

    private static string FormatMoney(decimal amount) => FormattableString.Invariant($"${amount:0.00}");

    private static PatientPortalGeneratedMedicalReportTemplateMetadata EmptyGeneratedMedicalReportTemplateMetadata() => new(
        FacilityName: string.Empty,
        FacilityStreet: string.Empty,
        FacilityCityStatePostal: string.Empty,
        FacilityPhone: string.Empty,
        PrintablePatientName: string.Empty,
        PatientHeaderLine: string.Empty,
        GeneratedOnLabel: string.Empty,
        SignatureLineAvailable: false);

    private static PatientPortalGeneratedMedicalReportTemplateMetadata BuildGeneratedMedicalReportTemplateMetadata(
        GeneratedMedicalReportPatientRow patient,
        GeneratedMedicalReportFacilityRow facility,
        string generatedOn)
    {
        var printablePatientName = string.Join(
            " ",
            new[] { patient.FirstName, patient.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var headerPatientName = string.Join(
            ", ",
            new[] { patient.LastName, patient.FirstName }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var formattedDateOfBirth = FormatGeneratedReportShortDate(patient.DateOfBirth);
        var cityStatePostal = FormatGeneratedReportCityStatePostal(facility.City, facility.State, facility.PostalCode);

        return new PatientPortalGeneratedMedicalReportTemplateMetadata(
            FacilityName: facility.Name,
            FacilityStreet: facility.Street,
            FacilityCityStatePostal: cityStatePostal,
            FacilityPhone: facility.Phone,
            PrintablePatientName: string.IsNullOrWhiteSpace(printablePatientName) ? patient.Pubpid : printablePatientName,
            PatientHeaderLine: $"PATIENT:{headerPatientName} - {formattedDateOfBirth}",
            GeneratedOnLabel: $"Generated on: {FormatGeneratedReportShortDate(generatedOn)}",
            SignatureLineAvailable: true);
    }

    private static string FormatGeneratedReportCityStatePostal(string? city, string? state, string? postalCode)
    {
        var statePostal = string.Join(" ", new[] { state, postalCode }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var value = string.Join(", ", new[] { city, statePostal }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(value) ? "Not recorded" : value;
    }

    private static string FormatGeneratedReportShortDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Not recorded";
        }

        return DateOnly.TryParse(value, out var date)
            ? $"{date.Month:D2}/{date.Day:D2}/{date.Year:D4}"
            : value;
    }

    private static PatientPortalGeneratedMedicalReport BuildMedicalReportPreview(
        PatientPortalSessionResponse session,
        IReadOnlyList<PatientPortalMedicalReportIssue> issues,
        IReadOnlyList<PatientPortalMedicalReportEncounter> encounters,
        IReadOnlyList<PatientPortalMedicalReportProcedureOrder> procedureOrders)
    {
        var includedSectionIds = MedicalReportSections
            .Where(section => section.Selected)
            .Select(section => section.Id)
            .ToArray();
        var includedProcedureOrders = procedureOrders.Take(1).Select(order => order.Id).ToArray();
        var summaryLines = new List<string>
        {
            $"Patient Data: {session.DisplayName} ({session.Pubpid})",
            $"Billing Information: {session.DisplayName} has billing detail available through the medical history report.",
            $"Issues available: {issues.Count}; Encounters available: {encounters.Count}."
        };

        foreach (var order in procedureOrders.Take(1))
        {
            summaryLines.Add($"Procedure Order: {order.ProcedureName} ordered {order.OrderDate} with {order.ResultCount} result rows.");
        }

        return new PatientPortalGeneratedMedicalReport(
            Title: "Customized Medical History Report",
            IncludedSectionIds: includedSectionIds,
            IncludedProcedureOrderIds: includedProcedureOrders,
            IncludedEncounterFormIds: Array.Empty<string>(),
            TemplateMetadata: EmptyGeneratedMedicalReportTemplateMetadata(),
            PackageMetadata: EmptyGeneratedMedicalReportPackageMetadata(),
            SummaryLineCount: summaryLines.Count,
            SummaryLines: summaryLines);
    }

    private static string BuildEncounterDisplay(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "Encounter";
        }

        return reason.Length > 20 ? $"{reason[..20]} ... " : reason;
    }

    private static async Task<PatientAppointmentRequestDefaultsRow> GetPatientAppointmentRequestDefaultsAsync(
        NpgsqlConnection connection,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select provider_id, facility_id
            from patients
            where canonical_id = @patient_id
            limit 1;
            """;
        command.Parameters.AddWithValue("patient_id", patientId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new PatientAppointmentRequestDefaultsRow(null, null);
        }

        return new PatientAppointmentRequestDefaultsRow(
            ReadNullableInt(reader, "provider_id"),
            ReadNullableInt(reader, "facility_id"));
    }

    private static async Task<IReadOnlyList<PatientPortalAppointmentProviderOption>> GetAppointmentRequestProvidersAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              s.id,
              s.username,
              trim(concat(s.last_name, ', ', s.first_name)) as display_name,
              s.facility_id,
              f.name as facility_name
            from staff s
            left join facilities f on f.id = s.facility_id
            where s.active = true
              and s.calendar = true
              and s.username <> ''
            order by s.last_name, s.first_name, s.id;
            """;

        var providers = new List<PatientPortalAppointmentProviderOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            providers.Add(new PatientPortalAppointmentProviderOption(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                Username: ReadNullableString(reader, "username") ?? string.Empty,
                DisplayName: ReadNullableString(reader, "display_name") ?? string.Empty,
                FacilityId: ReadNullableInt(reader, "facility_id"),
                FacilityName: ReadNullableString(reader, "facility_name")));
        }

        return providers;
    }

    private static async Task<IReadOnlyList<PatientPortalAppointmentFacilityOption>> GetAppointmentRequestFacilitiesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, name, code
            from facilities
            where inactive = false
            order by name, id;
            """;

        var facilities = new List<PatientPortalAppointmentFacilityOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            facilities.Add(new PatientPortalAppointmentFacilityOption(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                Name: ReadNullableString(reader, "name") ?? string.Empty,
                Code: ReadNullableString(reader, "code")));
        }

        return facilities;
    }

    private static async Task InsertPrescriptionRefillLifecycleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int threadId,
        int staffMessageId,
        int pid,
        string patientId,
        string prescriptionId,
        string requestDate,
        string drug,
        string? patientNote,
        string updatedBy,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into prescription_refill_request_lifecycle
                (thread_id, staff_message_id, pid, patient_id, prescription_id,
                 request_date, drug, patient_note, status, staff_response, updated_at, updated_by)
            values
                (@threadId, @staffMessageId, @pid, @patientId, @prescriptionId,
                 @requestDate, @drug, @patientNote, 'pending', null, @updatedAt, @updatedBy)
            on conflict (thread_id) do nothing;
            """;
        command.Parameters.Add("threadId", NpgsqlDbType.Integer).Value = threadId;
        command.Parameters.Add("staffMessageId", NpgsqlDbType.Integer).Value = staffMessageId;
        command.Parameters.Add("pid", NpgsqlDbType.Integer).Value = pid;
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.AddWithValue("prescriptionId", prescriptionId);
        command.Parameters.Add("requestDate", NpgsqlDbType.Date).Value =
            DateOnly.Parse(requestDate, CultureInfo.InvariantCulture);
        command.Parameters.AddWithValue("drug", drug);
        command.Parameters.AddWithValue("patientNote", (object?)patientNote ?? DBNull.Value);
        command.Parameters.Add("updatedAt", NpgsqlDbType.Timestamp).Value =
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        command.Parameters.AddWithValue("updatedBy", updatedBy);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<DatasetMetadata> GetMetadataAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select dataset_id, version, base_date
            from dataset_metadata
            order by generated_at desc
            limit 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new DatasetMetadata("unseeded", "unknown", DateOnly.FromDateTime(DateTime.UtcNow));
        }

        return new DatasetMetadata(
            reader.GetString(reader.GetOrdinal("dataset_id")),
            reader.GetString(reader.GetOrdinal("version")),
            reader.GetFieldValue<DateOnly>(reader.GetOrdinal("base_date")));
    }

    private static PatientPortalDocumentsResponse BuildDocumentsResponse(
        PatientPortalSessionResponse session,
        DatasetMetadata metadata,
        IReadOnlyList<PatientPortalDocumentItem> documents)
    {
        var categories = documents
            .GroupBy(document => new { document.CategoryId, document.CategoryName, document.DisplayPath })
            .OrderBy(group => group.Key.DisplayPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PatientPortalDocumentCategory(
                CategoryId: group.Key.CategoryId,
                CategoryName: group.Key.CategoryName,
                DisplayPath: group.Key.DisplayPath,
                DocumentCount: group.Count(),
                Documents: group.OrderByDescending(document => document.DocDate).ThenByDescending(document => document.Id).ToArray()))
            .ToArray();

        return new PatientPortalDocumentsResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            DocumentCount: documents.Count,
            Categories: categories,
            Documents: documents,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    private static PatientPortalDocumentsDownloadPackage DownloadFailure(string reason) => new(
        Downloadable: false,
        FileName: "patient_documents.zip",
        ContentType: "application/zip",
        Content: Array.Empty<byte>(),
        DocumentCount: 0,
        Documents: Array.Empty<PatientPortalDocumentItem>(),
        FailureReason: reason);

    private static async Task<IReadOnlyList<PatientPortalDocumentItem>> GetPortalDocumentsAsync(
        NpgsqlConnection connection,
        int pid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, document_key, category_id, category_name, name, doc_date, uploaded_at,
              mimetype, file_name, size_bytes, storage_method
            from patient_documents
            where pid = @pid and deleted = 0
            order by category_name, doc_date desc, id desc;
            """;
        command.Parameters.AddWithValue("pid", pid);

        var documents = new List<PatientPortalDocumentItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(ReadPortalDocumentItem(reader));
        }

        return documents;
    }

    private static async Task<IReadOnlyList<PortalDownloadDocumentRow>> GetPortalDownloadDocumentsAsync(
        NpgsqlConnection connection,
        int pid,
        IReadOnlyList<int> documentIds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, document_key, category_id, category_name, name, doc_date, uploaded_at,
              mimetype, file_name, size_bytes, storage_method, coalesce(content, '') as content, content_bytes
            from patient_documents
            where pid = @pid
              and deleted = 0
              and coalesce(storage_method, 'database') <> 'web_url'
              and id = any(@document_ids)
            order by category_name, doc_date desc, id desc;
            """;
        command.Parameters.AddWithValue("pid", pid);
        command.Parameters.Add("document_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = documentIds.ToArray();

        var documents = new List<PortalDownloadDocumentRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = ReadPortalDocumentItem(reader);
            var contentBytesOrdinal = reader.GetOrdinal("content_bytes");
            var contentBytes = reader.IsDBNull(contentBytesOrdinal)
                ? Encoding.UTF8.GetBytes(reader.GetString(reader.GetOrdinal("content")))
                : (byte[])reader.GetValue(contentBytesOrdinal);

            documents.Add(new PortalDownloadDocumentRow(item, contentBytes));
        }

        return documents;
    }

    private static PatientPortalDocumentItem ReadPortalDocumentItem(NpgsqlDataReader reader)
    {
        var name = reader.GetString(reader.GetOrdinal("name"));
        var mimetype = ReadNullableString(reader, "mimetype");
        var storageMethod = ReadNullableString(reader, "storage_method");
        var categoryName = reader.GetString(reader.GetOrdinal("category_name"));
        var fileName = ReadNullableString(reader, "file_name") ?? BuildPortalDocumentFileName(name, mimetype);

        return new PatientPortalDocumentItem(
            Id: reader.GetInt32(reader.GetOrdinal("id")),
            DocumentKey: reader.GetString(reader.GetOrdinal("document_key")),
            CategoryId: reader.GetInt32(reader.GetOrdinal("category_id")),
            CategoryName: categoryName,
            DisplayPath: BuildPortalDocumentDisplayPath(categoryName),
            Name: name,
            DocDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("doc_date")).ToString("yyyy-MM-dd"),
            UploadedAt: reader.GetDateTime(reader.GetOrdinal("uploaded_at")).ToString("yyyy-MM-dd HH:mm:ss"),
            Mimetype: mimetype,
            FileName: fileName,
            SizeBytes: ReadNullableInt(reader, "size_bytes"),
            StorageMethod: storageMethod,
            CanDownload: !string.Equals(storageMethod, "web_url", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildPortalDocumentDisplayPath(string categoryName)
    {
        var trimmed = categoryName.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "Unfiled" : trimmed;
    }

    private static string BuildPortalDocumentFileName(string name, string? mimetype)
    {
        var extension = mimetype switch
        {
            "application/pdf" => ".pdf",
            "application/xml" => ".xml",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "text/uri-list" => ".url",
            _ => ".txt"
        };
        var sanitized = SanitizeZipPathSegment(name);
        return sanitized.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? sanitized : $"{sanitized}{extension}";
    }

    private static string BuildZipEntryName(PatientPortalDocumentItem document, HashSet<string> usedEntryNames)
    {
        var folder = SanitizeZipPathSegment(document.DisplayPath);
        var fileName = SanitizeZipPathSegment(document.FileName);
        var candidate = $"{folder}/{fileName}";
        if (usedEntryNames.Add(candidate))
        {
            return candidate;
        }

        var extension = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var index = 2;
        while (true)
        {
            candidate = $"{folder}/{baseName}-{index}{extension}";
            if (usedEntryNames.Add(candidate))
            {
                return candidate;
            }

            index += 1;
        }
    }

    private static string SanitizeZipPathSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Trim().Length);
        foreach (var character in value.Trim())
        {
            builder.Append(character is '/' or '\\' || invalidCharacters.Contains(character) ? '-' : character);
        }

        var sanitized = builder.ToString().Trim('.', ' ');
        return string.IsNullOrWhiteSpace(sanitized) ? "document" : sanitized;
    }

    private static async Task<PatientPortalHomeMessageSummary> GetMessageSummaryAsync(
        NpgsqlConnection connection,
        string portalUsername,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              count(*)::int as total_messages,
              count(*) filter (where message_status = 'New')::int as new_messages,
              count(*) filter (where message_status = 'Done')::int as done_messages,
              (
                select title
                from portal_mailbox_messages latest
                where latest.deleted = 0
                  and latest.owner = @portal_username
                  and latest.recipient_id = @portal_username
                order by latest.message_date desc, latest.id desc
                limit 1
              ) as latest_message_title,
              (
                select message_date
                from portal_mailbox_messages latest
                where latest.deleted = 0
                  and latest.owner = @portal_username
                  and latest.recipient_id = @portal_username
                order by latest.message_date desc, latest.id desc
                limit 1
              ) as latest_message_date
            from portal_mailbox_messages
            where deleted = 0
              and owner = @portal_username
              and recipient_id = @portal_username;
            """;
        command.Parameters.AddWithValue("portal_username", portalUsername);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new PatientPortalHomeMessageSummary(0, 0, 0, null, null);
        }

        return new PatientPortalHomeMessageSummary(
            TotalMessages: reader.GetInt32(reader.GetOrdinal("total_messages")),
            NewMessages: reader.GetInt32(reader.GetOrdinal("new_messages")),
            DoneMessages: reader.GetInt32(reader.GetOrdinal("done_messages")),
            LatestMessageTitle: ReadNullableString(reader, "latest_message_title"),
            LatestMessageDate: ReadNullableDate(reader, "latest_message_date"));
    }

    private static async Task<IReadOnlyList<PatientPortalMessageItem>> GetPortalMessagesAsync(
        NpgsqlConnection connection,
        string portalUsername,
        PortalMessageFolder folder,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var folderPredicate = GetPortalMessageFolderPredicate(folder);
        var deletedPredicate = folder == PortalMessageFolder.Deleted ? "deleted = 1" : "deleted = 0";
        command.CommandText = $"""
            select id, 'Message' as type, message_date, title, body, message_status, assigned_to, portal_relation,
              mail_chain, sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain, is_encrypted
            from portal_mailbox_messages
            where {deletedPredicate} and {folderPredicate}
            order by message_date desc, id desc;
            """;
        command.Parameters.AddWithValue("portal_username", portalUsername);

        var messages = new List<PatientPortalMessageItem>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                messages.Add(ReadPortalMessageItem(reader));
            }
        }

        if (folder is PortalMessageFolder.Inbox or PortalMessageFolder.All)
        {
            var notifications = await GetPortalNotificationMessagesAsync(connection, portalUsername, cancellationToken);
            messages.AddRange(notifications);
        }

        var populatedMessages = await PopulatePortalMessageAttachmentsAsync(connection, messages, cancellationToken);
        return populatedMessages
            .OrderByDescending(message => message.Date, StringComparer.Ordinal)
            .ThenByDescending(message => int.TryParse(message.Id, out var id) ? id : 0)
            .ToArray();
    }

    private static async Task<IReadOnlyList<PatientPortalMessageItem>> GetPortalNotificationMessagesAsync(
        NpgsqlConnection connection,
        string portalUsername,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              pr.id::int as id,
              'Notification' as type,
              pr.date_created::date as message_date,
              coalesce(nullif(pr.due_status, ''), 'Reminder') as title,
              concat(
                case pr.category
                  when 'act_cat_assess' then 'Assessment'
                  when 'act_cat_edu' then 'Education'
                  when 'act_cat_exam' then 'Examination'
                  when 'act_cat_inter' then 'Intervention'
                  when 'act_cat_measure' then 'Measurement'
                  when 'act_cat_treat' then 'Treatment'
                  when 'act_cat_remind' then 'Reminder'
                  else pr.category
                end,
                ':',
                case pr.item
                  when 'act_appointment' then 'Appointment'
                  when 'act_bp' then 'Blood Pressure'
                  when 'act_influvacc' then 'Influenza Vaccine'
                  when 'act_tobacco' then 'Tobacco'
                  when 'act_wt' then 'Weight'
                  when 'act_bmi' then 'BMI'
                  when 'act_nutrition' then 'Nutrition'
                  when 'act_exercise' then 'Exercise'
                  else pr.item
                end
              ) as body,
              '' as message_status,
              '' as assigned_to,
              'portal:patient-reminder' as portal_relation,
              pr.id::int as mail_chain,
              '' as sender_id,
              'Patient Reminders' as sender_name,
              @portal_username as recipient_id,
              '' as recipient_name,
              pr.id::int as reply_mail_chain,
              false as is_encrypted
            from patient_reminders pr
            join patient_portal_accounts ppa on ppa.pid = pr.pid
            where ppa.portal_username = @portal_username
              and pr.active = 1
              and pr.date_created > current_timestamp - interval '1 month'
            order by pr.date_created desc, pr.id desc;
            """;
        command.Parameters.AddWithValue("portal_username", portalUsername);

        var notifications = new List<PatientPortalMessageItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            notifications.Add(ReadPortalMessageItem(reader));
        }

        return notifications;
    }

    private static async Task<PortalMailboxMessageRow?> GetPortalOwnedMessageAsync(
        NpgsqlConnection connection,
        string portalUsername,
        int messageId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, 'Message' as type, message_date, title, body, message_status, assigned_to, portal_relation,
              mail_chain, sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain, is_encrypted
            from portal_mailbox_messages
            where deleted = 0
              and owner = @portal_username
              and (sender_id = @portal_username or recipient_id = @portal_username)
              and id = @message_id
            limit 1;
            """;
        command.Parameters.AddWithValue("portal_username", portalUsername);
        command.Parameters.Add("message_id", NpgsqlDbType.Integer).Value = messageId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PortalMailboxMessageRow(ReadPortalMessageItem(reader))
            : null;
    }

    private static async Task<IReadOnlyList<PatientPortalMessageItem>> GetPortalMessageThreadAsync(
        NpgsqlConnection connection,
        string portalUsername,
        int messageId,
        int threadId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, 'Message' as type, message_date, title, body, message_status, assigned_to, portal_relation,
              mail_chain, sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain, is_encrypted
            from portal_mailbox_messages
            where deleted = 0
              and owner = @portal_username
              and (sender_id = @portal_username or recipient_id = @portal_username)
              and (reply_mail_chain = @thread_id or mail_chain = @thread_id or id = @message_id)
            order by message_date asc, id asc;
            """;
        command.Parameters.AddWithValue("portal_username", portalUsername);
        command.Parameters.Add("thread_id", NpgsqlDbType.Integer).Value = threadId;
        command.Parameters.Add("message_id", NpgsqlDbType.Integer).Value = messageId;

        var messages = new List<PatientPortalMessageItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(ReadPortalMessageItem(reader));
        }

        await reader.DisposeAsync();
        return await PopulatePortalMessageAttachmentsAsync(connection, messages, cancellationToken);
    }

    private static async Task<PortalMailboxMessageRow?> GetPortalInboxMessageAsync(
        NpgsqlConnection connection,
        string portalUsername,
        int messageId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, 'Message' as type, message_date, title, body, message_status, assigned_to, portal_relation,
              mail_chain, sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain, is_encrypted
            from portal_mailbox_messages
            where deleted = 0
              and owner = @portal_username
              and recipient_id = @portal_username
              and id = @message_id
            limit 1;
            """;
        command.Parameters.AddWithValue("portal_username", portalUsername);
        command.Parameters.Add("message_id", NpgsqlDbType.Integer).Value = messageId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var item = ReadPortalMessageItem(reader);

        return new PortalMailboxMessageRow(item);
    }

    private static PatientPortalMessageItem ReadPortalMessageItem(NpgsqlDataReader reader)
    {
        var isEncrypted = reader.GetBoolean(reader.GetOrdinal("is_encrypted"));
        var body = isEncrypted
            ? ProtectedPortalMessageBody
            : ReadNullableString(reader, "body") ?? string.Empty;

        return new PatientPortalMessageItem(
            Id: reader.GetInt32(reader.GetOrdinal("id")).ToString(),
            Type: ReadNullableString(reader, "type") ?? "Message",
            Date: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("message_date")).ToString("yyyy-MM-dd"),
            Title: ReadNullableString(reader, "title") ?? string.Empty,
            Body: body,
            Status: ReadNullableString(reader, "message_status") ?? string.Empty,
            AssignedTo: ReadNullableString(reader, "assigned_to") ?? string.Empty,
            SenderId: ReadNullableString(reader, "sender_id") ?? string.Empty,
            SenderName: ReadNullableString(reader, "sender_name") ?? string.Empty,
            RecipientId: ReadNullableString(reader, "recipient_id") ?? string.Empty,
            RecipientName: ReadNullableString(reader, "recipient_name") ?? string.Empty,
            MailChain: reader.GetInt32(reader.GetOrdinal("mail_chain")),
            ReplyMailChain: reader.GetInt32(reader.GetOrdinal("reply_mail_chain")),
            PortalRelation: ReadNullableString(reader, "portal_relation"),
            IsEncrypted: isEncrypted,
            AttachmentCount: 0,
            Attachments: Array.Empty<PatientPortalMessageAttachment>());
    }

    private static async Task<IReadOnlyList<PatientPortalMessageItem>> PopulatePortalMessageAttachmentsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<PatientPortalMessageItem> messages,
        CancellationToken cancellationToken)
    {
        var messageIds = messages
            .Where(message => string.Equals(message.Type, "Message", StringComparison.OrdinalIgnoreCase))
            .Select(message => int.TryParse(message.Id, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (messageIds.Length == 0)
        {
            return messages;
        }

        var attachmentsByMessage = new Dictionary<int, List<PatientPortalMessageAttachment>>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, message_id, file_name, content_type, size_bytes, source
            from patient_portal_message_attachments
            where message_id = any(@messageIds)
            order by uploaded_at, id;
            """;
        command.Parameters.AddWithValue("messageIds", NpgsqlDbType.Array | NpgsqlDbType.Integer, messageIds);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var messageId = reader.GetInt32(1);
            if (!attachmentsByMessage.TryGetValue(messageId, out var attachments))
            {
                attachments = [];
                attachmentsByMessage[messageId] = attachments;
            }

            attachments.Add(new PatientPortalMessageAttachment(
                reader.GetGuid(0).ToString(),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetString(5)));
        }

        return messages.Select(message =>
        {
            var messageId = int.TryParse(message.Id, out var id) ? id : 0;
            var attachments = attachmentsByMessage.TryGetValue(messageId, out var values)
                ? values.ToArray()
                : Array.Empty<PatientPortalMessageAttachment>();
            return message with { AttachmentCount = attachments.Length, Attachments = attachments };
        }).ToArray();
    }

    private static IReadOnlyList<PortalAttachmentUpload> NormalizeMessageAttachments(
        IReadOnlyList<PatientPortalMessageAttachmentSubmission>? submissions)
    {
        var supplied = submissions ?? Array.Empty<PatientPortalMessageAttachmentSubmission>();
        if (supplied.Count > MaximumPortalMessageAttachmentCount)
        {
            throw new InvalidOperationException($"A secure message can include at most {MaximumPortalMessageAttachmentCount} attachments.");
        }

        var attachments = new List<PortalAttachmentUpload>();
        var totalBytes = 0;
        foreach (var submission in supplied)
        {
            var fileName = Path.GetFileName(NormalizeText(submission.FileName) ?? string.Empty);
            var contentType = NormalizeText(submission.ContentType)?.ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(contentType) || string.IsNullOrWhiteSpace(submission.ContentBase64))
            {
                throw new InvalidOperationException("Each secure-message attachment requires a file name, content type, and file content.");
            }

            if (fileName.Length > 180 || !IsSupportedPortalAttachmentContentType(contentType))
            {
                throw new InvalidOperationException("Secure-message attachments must be PDF, PNG, JPEG, or plain-text files with safe names.");
            }

            byte[] content;
            try
            {
                content = Convert.FromBase64String(submission.ContentBase64);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("Secure-message attachment content is not valid base64 data.");
            }

            if (content.Length == 0 || content.Length > MaximumPortalMessageAttachmentBytes)
            {
                throw new InvalidOperationException("Each secure-message attachment must be between 1 byte and 4 MiB.");
            }

            totalBytes += content.Length;
            if (totalBytes > MaximumPortalMessageAttachmentTotalBytes)
            {
                throw new InvalidOperationException("Secure-message attachments may not exceed 10 MiB in total.");
            }

            attachments.Add(new PortalAttachmentUpload(fileName, contentType, content));
        }

        return attachments;
    }

    private static bool IsSupportedPortalAttachmentContentType(string contentType) =>
        contentType is "application/pdf" or "image/png" or "image/jpeg" or "text/plain";

    private static IReadOnlyList<PatientPortalMessageAttachment> CreateAttachmentItems(IReadOnlyList<PortalAttachmentUpload> attachments) =>
        attachments.Select(attachment => new PatientPortalMessageAttachment(
            Guid.NewGuid().ToString(),
            attachment.FileName,
            attachment.ContentType,
            attachment.Content.LongLength,
            "portal-upload")).ToArray();

    private static async Task InsertPortalMessageAttachmentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PatientPortalSessionResponse session,
        PatientPortalMessageItem message,
        IReadOnlyList<PortalAttachmentUpload> attachments,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < attachments.Count; index++)
        {
            var attachment = attachments[index];
            var item = message.Attachments[index];
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into patient_portal_message_attachments
                    (id, message_id, patient_id, pid, file_name, content_type, size_bytes, content, source)
                values
                    (@id, @messageId, @patientId, @pid, @fileName, @contentType, @sizeBytes, @content, 'portal-upload');
                """;
            command.Parameters.AddWithValue("id", Guid.Parse(item.Id));
            command.Parameters.AddWithValue("messageId", int.Parse(message.Id));
            command.Parameters.AddWithValue("patientId", session.CanonicalId);
            command.Parameters.AddWithValue("pid", session.LegacyPid ?? 0);
            command.Parameters.AddWithValue("fileName", attachment.FileName);
            command.Parameters.AddWithValue("contentType", attachment.ContentType);
            command.Parameters.AddWithValue("sizeBytes", attachment.Content.Length);
            command.Parameters.AddWithValue("content", attachment.Content);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static int ResolvePortalThreadId(PatientPortalMessageItem message, int fallbackMessageId)
    {
        if (message.ReplyMailChain > 0)
        {
            return message.ReplyMailChain;
        }

        return message.MailChain > 0 ? message.MailChain : fallbackMessageId;
    }

    private static async Task<PatientMessageItem> InsertForwardedPatientMessageAsync(
        NpgsqlConnection connection,
        PatientPortalSessionResponse session,
        PatientPortalMessageItem originalMessage,
        string body,
        string assignedTo,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var id = $"MSG-PORTAL-FWD-{Guid.NewGuid():N}";
        var messageDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into messages
                (id, patient_id, pid, message_date, title, body, status, assigned_to, portal_relation, is_encrypted, updated_by, updated_at, deleted, activity)
            values
                (@id, @patient_id, @pid, @message_date, @title, @body, 'New', @assigned_to, 'portal:forwarded', false, null, null, 0, 1);
            """;
        command.Parameters.Add("id", NpgsqlDbType.Text).Value = id;
        command.Parameters.Add("patient_id", NpgsqlDbType.Text).Value = session.CanonicalId;
        command.Parameters.Add("pid", NpgsqlDbType.Integer).Value = session.LegacyPid ?? 0;
        command.Parameters.Add("message_date", NpgsqlDbType.Date).Value = messageDate;
        command.Parameters.Add("title", NpgsqlDbType.Text).Value = originalMessage.Title;
        command.Parameters.Add("body", NpgsqlDbType.Text).Value = body;
        command.Parameters.Add("assigned_to", NpgsqlDbType.Text).Value = assignedTo;
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new PatientMessageItem(
            Id: id,
            Date: messageDate.ToString("yyyy-MM-dd"),
            Title: originalMessage.Title,
            Body: body,
            Status: "New",
            AssignedTo: assignedTo,
            PortalRelation: "portal:forwarded",
            IsEncrypted: false,
            UpdatedBy: null,
            UpdatedAt: null,
            Deleted: 0,
            AssignmentVersion: 0);
    }

    private static async Task<int> GetNextPortalMailboxIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select nextval('portal_mailbox_messages_id_seq');";

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value);
    }

    private static async Task<int> GetPortalMessageCountAsync(
        NpgsqlConnection connection,
        string portalUsername,
        PortalMessageFolder folder,
        CancellationToken cancellationToken)
    {
        if (folder is PortalMessageFolder.Inbox or PortalMessageFolder.All)
        {
            return (await GetPortalMessagesAsync(connection, portalUsername, folder, cancellationToken)).Count;
        }

        await using var command = connection.CreateCommand();
        var folderPredicate = GetPortalMessageFolderPredicate(folder);
        var deletedPredicate = folder == PortalMessageFolder.Deleted ? "deleted = 1" : "deleted = 0";
        command.CommandText = $"""
            select count(*)::int
            from portal_mailbox_messages
            where {deletedPredicate} and {folderPredicate};
            """;
        command.Parameters.AddWithValue("portal_username", portalUsername);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value);
    }

    private static string GetPortalMessageFolderPredicate(PortalMessageFolder folder) => folder switch
    {
        PortalMessageFolder.Sent => "owner = @portal_username and sender_id = @portal_username",
        PortalMessageFolder.All => "owner = @portal_username",
        PortalMessageFolder.Deleted => "owner = @portal_username",
        _ => "owner = @portal_username and recipient_id = @portal_username"
    };

    private static async Task InsertPortalMailboxMessageAsync(
        NpgsqlConnection connection,
        PatientPortalSessionResponse session,
        PatientPortalMessageItem message,
        string owner,
        string userValue,
        int mailChain,
        int replyMailChain,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into portal_mailbox_messages (
              id, patient_id, pid, message_date, body, owner, user_value, group_name,
              activity, authorized, title, assigned_to, message_status, portal_relation, mail_chain,
              sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain,
              is_encrypted, deleted
            )
            values (
              @id, @patient_id, @pid, @message_date, @body, @owner, @user_value, 'Default',
              1, 1, @title, @assigned_to, @message_status, @portal_relation, @mail_chain,
              @sender_id, @sender_name, @recipient_id, @recipient_name, @reply_mail_chain,
              @is_encrypted, 0
            );
            """;
        command.Parameters.Add("id", NpgsqlDbType.Integer).Value = int.Parse(message.Id);
        command.Parameters.Add("patient_id", NpgsqlDbType.Text).Value = session.CanonicalId;
        command.Parameters.Add("pid", NpgsqlDbType.Integer).Value = session.LegacyPid ?? 0;
        command.Parameters.Add("message_date", NpgsqlDbType.Date).Value = DateOnly.Parse(message.Date);
        command.Parameters.Add("body", NpgsqlDbType.Text).Value = message.Body;
        command.Parameters.Add("owner", NpgsqlDbType.Text).Value = owner;
        command.Parameters.Add("user_value", NpgsqlDbType.Text).Value = userValue;
        command.Parameters.Add("title", NpgsqlDbType.Text).Value = message.Title;
        command.Parameters.Add("assigned_to", NpgsqlDbType.Text).Value = message.AssignedTo;
        command.Parameters.Add("message_status", NpgsqlDbType.Text).Value = message.Status;
        command.Parameters.Add("portal_relation", NpgsqlDbType.Text).Value = message.PortalRelation is { } relation ? (object)relation : DBNull.Value;
        command.Parameters.Add("mail_chain", NpgsqlDbType.Integer).Value = mailChain;
        command.Parameters.Add("sender_id", NpgsqlDbType.Text).Value = message.SenderId;
        command.Parameters.Add("sender_name", NpgsqlDbType.Text).Value = message.SenderName;
        command.Parameters.Add("recipient_id", NpgsqlDbType.Text).Value = message.RecipientId;
        command.Parameters.Add("recipient_name", NpgsqlDbType.Text).Value = message.RecipientName;
        command.Parameters.Add("reply_mail_chain", NpgsqlDbType.Integer).Value = replyMailChain;
        command.Parameters.Add("is_encrypted", NpgsqlDbType.Boolean).Value = message.IsEncrypted;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string> GetRecipientDisplayNameAsync(
        NpgsqlConnection connection,
        string recipientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select coalesce(
              (select trim(first_name || ' ' || last_name) from staff where username = @recipient_id limit 1),
              (select display_name from auth_accounts where username = @recipient_id limit 1),
              @recipient_id
            ) as display_name;
            """;
        command.Parameters.Add("recipient_id", NpgsqlDbType.Text).Value = recipientId;

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value?.ToString() ?? recipientId;
    }

    private static async Task<IReadOnlyList<PatientPortalMessageRecipientOption>> GetPortalMessageRecipientOptionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var recipients = new List<PatientPortalMessageRecipientOption>();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              username as id,
              display_name,
              active
            from auth_accounts
            where username = 'admin'
            order by username
            limit 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetString(reader.GetOrdinal("id"));
            var displayName = ReadNullableString(reader, "display_name")?.Trim();
            recipients.Add(new PatientPortalMessageRecipientOption(
                Id: id,
                DisplayName: string.IsNullOrWhiteSpace(displayName) ? id : displayName,
                Type: "user",
                Active: reader.GetBoolean(reader.GetOrdinal("active")),
                Fallback: true));
        }

        if (recipients.Count == 0)
        {
            recipients.Add(new PatientPortalMessageRecipientOption(
                Id: "admin",
                DisplayName: "Administrator",
                Type: "user",
                Active: true,
                Fallback: true));
        }

        return recipients;
    }

    private static async Task<IReadOnlyList<PatientPortalHomeImmunizationSummary>> GetPortalHomeImmunizationsAsync(
        NpgsqlConnection connection,
        int pid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              id,
              administered_at::date as administered_date,
              to_char(administered_at::date, 'MM/DD/YYYY') as administered_formatted,
              immunization_id,
              cvx_code,
              coalesce(nullif(vaccine, ''), nullif(cvx_code, ''), '') as code_text,
              note,
              completion_status,
              coalesce(added_erroneously, 0)::int as added_erroneously
            from immunizations
            where pid = @pid
            order by administered_at desc, id desc;
            """;
        command.Parameters.AddWithValue("pid", pid);

        var items = new List<PatientPortalHomeImmunizationSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new PatientPortalHomeImmunizationSummary(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                AdministeredDate: ReadNullableDate(reader, "administered_date"),
                AdministeredFormatted: ReadNullableString(reader, "administered_formatted"),
                ImmunizationId: ReadNullableInt(reader, "immunization_id"),
                CvxCode: ReadNullableString(reader, "cvx_code"),
                CodeText: ReadNullableString(reader, "code_text") ?? string.Empty,
                Note: ReadNullableString(reader, "note"),
                CompletionStatus: ReadNullableString(reader, "completion_status"),
                AddedErroneously: reader.GetInt32(reader.GetOrdinal("added_erroneously"))));
        }

        return items;
    }

    private static async Task<PatientPortalProfileDemographics?> GetPortalProfileDemographicsAsync(
        NpgsqlConnection connection,
        string canonicalId,
        int pid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              first_name,
              last_name,
              preferred_name,
              date_of_birth,
              sex,
              email,
              street,
              city,
              state,
              postal_code,
              phone_home,
              phone_cell,
              phone as phone_contact,
              hipaa_allow_sms,
              hipaa_allow_email,
              guardian_relationship as contact_relationship,
              mother_name,
              guardian_name,
              guardian_relationship,
              guardian_phone,
              guardian_email
            from patients
            where canonical_id = @canonical_id
               or legacy_pid = @pid
            order by legacy_pid
            limit 1;
            """;
        command.Parameters.Add("canonical_id", NpgsqlDbType.Text).Value = canonicalId;
        command.Parameters.AddWithValue("pid", pid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PatientPortalProfileDemographics(
            FirstName: reader.GetString(reader.GetOrdinal("first_name")),
            LastName: reader.GetString(reader.GetOrdinal("last_name")),
            PreferredName: ReadNullableString(reader, "preferred_name"),
            DateOfBirth: ReadNullableDate(reader, "date_of_birth"),
            Sex: ReadNullableString(reader, "sex"),
            Email: ReadNullableString(reader, "email"),
            Street: ReadNullableString(reader, "street"),
            City: ReadNullableString(reader, "city"),
            State: ReadNullableString(reader, "state"),
            PostalCode: ReadNullableString(reader, "postal_code"),
            PhoneHome: ReadNullableString(reader, "phone_home"),
            PhoneCell: ReadNullableString(reader, "phone_cell"),
            PhoneContact: ReadNullableString(reader, "phone_contact"),
            HipaaAllowSms: ReadNullableString(reader, "hipaa_allow_sms"),
            HipaaAllowEmail: ReadNullableString(reader, "hipaa_allow_email"),
            ContactRelationship: ReadNullableString(reader, "contact_relationship"),
            MotherName: ReadNullableString(reader, "mother_name"),
            GuardianName: ReadNullableString(reader, "guardian_name"),
            GuardianRelationship: ReadNullableString(reader, "guardian_relationship"),
            GuardianPhone: ReadNullableString(reader, "guardian_phone"),
            GuardianEmail: ReadNullableString(reader, "guardian_email"));
    }

    private static async Task<IReadOnlyList<PatientPortalProfileInsurance>> GetPortalProfileInsuranceAsync(
        NpgsqlConnection connection,
        int pid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              type,
              provider,
              plan_name,
              policy_number,
              group_number,
              relationship,
              subscriber_first_name,
              subscriber_last_name,
              subscriber_date_of_birth
            from insurance_records
            where pid = @pid
            order by
              case type
                when 'primary' then 1
                when 'secondary' then 2
                when 'tertiary' then 3
                else 4
              end,
              id;
            """;
        command.Parameters.AddWithValue("pid", pid);

        var items = new List<PatientPortalProfileInsurance>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var subscriberFirstName = ReadNullableString(reader, "subscriber_first_name");
            var subscriberLastName = ReadNullableString(reader, "subscriber_last_name");
            var subscriberName = string.Join(
                ' ',
                new[] { subscriberFirstName, subscriberLastName }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));

            items.Add(new PatientPortalProfileInsurance(
                Type: reader.GetString(reader.GetOrdinal("type")),
                Provider: ReadNullableString(reader, "provider"),
                PlanName: ReadNullableString(reader, "plan_name"),
                PolicyNumber: ReadNullableString(reader, "policy_number"),
                GroupNumber: ReadNullableString(reader, "group_number"),
                SubscriberFirstName: subscriberFirstName,
                SubscriberLastName: subscriberLastName,
                SubscriberName: string.IsNullOrWhiteSpace(subscriberName) ? null : subscriberName,
                SubscriberRelationship: ReadNullableString(reader, "relationship"),
                SubscriberDateOfBirth: ReadNullableDate(reader, "subscriber_date_of_birth")));
        }

        return items;
    }

    private static async Task<PatientPortalProfileChangeRequest?> GetPendingProfileChangeAsync(
        NpgsqlConnection connection,
        string canonicalId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              id,
              status,
              pending_action,
              narrative,
              requested_changes::text as requested_changes,
              created_at,
              updated_at
            from patient_portal_profile_change_requests
            where patient_id = @patient_id
              and activity = 'profile'
              and require_audit = 1
              and status = 'waiting'
              and pending_action = 'review'
            order by created_at, id
            limit 1;
            """;
        command.Parameters.Add("patient_id", NpgsqlDbType.Text).Value = canonicalId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var requestedChanges = reader.GetString(reader.GetOrdinal("requested_changes"));
        var demographics = JsonSerializer.Deserialize<PatientPortalProfileDemographics>(
            requestedChanges,
            PortalProfileChangeJsonOptions) ?? EmptyProfileDemographics();
        var createdAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at"));
        var updatedAtOrdinal = reader.GetOrdinal("updated_at");
        var updatedAt = reader.IsDBNull(updatedAtOrdinal)
            ? null
            : reader.GetFieldValue<DateTimeOffset>(updatedAtOrdinal).ToString("yyyy-MM-dd HH:mm:ss");

        return new PatientPortalProfileChangeRequest(
            Id: reader.GetInt64(reader.GetOrdinal("id")),
            Status: reader.GetString(reader.GetOrdinal("status")),
            PendingAction: reader.GetString(reader.GetOrdinal("pending_action")),
            Narrative: reader.GetString(reader.GetOrdinal("narrative")),
            RequestedAt: createdAt.ToString("yyyy-MM-dd HH:mm:ss"),
            UpdatedAt: updatedAt,
            Demographics: demographics);
    }

    private static PatientPortalProfileDemographics ApplyProfileChangeRequest(
        PatientPortalProfileDemographics current,
        PatientPortalProfileChangeSubmitRequest request) =>
        current with
        {
            Email = ApplyProfileChangeValue(current.Email, request.Email),
            Street = ApplyProfileChangeValue(current.Street, request.Street),
            City = ApplyProfileChangeValue(current.City, request.City),
            State = ApplyProfileChangeValue(current.State, request.State),
            PostalCode = ApplyProfileChangeValue(current.PostalCode, request.PostalCode),
            PhoneHome = ApplyProfileChangeValue(current.PhoneHome, request.PhoneHome),
            PhoneCell = ApplyProfileChangeValue(current.PhoneCell, request.PhoneCell),
            HipaaAllowSms = ApplyProfilePermissionValue(current.HipaaAllowSms, request.HipaaAllowSms),
            HipaaAllowEmail = ApplyProfilePermissionValue(current.HipaaAllowEmail, request.HipaaAllowEmail)
        };

    private static string? ApplyProfileChangeValue(string? currentValue, string? requestedValue)
    {
        if (requestedValue is null)
        {
            return currentValue;
        }

        var trimmed = requestedValue.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? ApplyProfilePermissionValue(string? currentValue, string? requestedValue)
    {
        if (requestedValue is null)
        {
            return currentValue;
        }

        return requestedValue.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase) ? "YES" : "NO";
    }

    private static async Task<AppointmentSummaryRows> GetUpcomingAppointmentsAsync(
        NpgsqlConnection connection,
        int pid,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
        => await GetPortalAppointmentsAsync(
            connection,
            pid,
            asOfDate,
            PortalAppointmentWindow.Upcoming,
            cancellationToken);

    private static async Task<AppointmentSummaryRows> GetPortalAppointmentsAsync(
        NpgsqlConnection connection,
        int pid,
        DateOnly asOfDate,
        PortalAppointmentWindow window,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = window == PortalAppointmentWindow.Upcoming
            ? """
            select
              a.id,
              a.appointment_date,
              a.start_time,
              coalesce(a.title, 'Appointment') as title,
              a.status,
              a.category_id,
              trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
              f.name as facility_name,
              a.comments,
              count(*) over ()::int as total_count
            from appointments a
            left join staff s on s.id = a.provider_id
            left join facilities f on f.id = a.facility_id
            where a.pid = @pid
              and a.appointment_date >= @as_of_date
            order by a.appointment_date, a.start_time, a.id
            limit 10;
            """
            : """
            select
              a.id,
              a.appointment_date,
              a.start_time,
              coalesce(a.title, 'Appointment') as title,
              a.status,
              a.category_id,
              trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
              f.name as facility_name,
              a.comments,
              count(*) over ()::int as total_count
            from appointments a
            left join staff s on s.id = a.provider_id
            left join facilities f on f.id = a.facility_id
            where a.pid = @pid
              and a.appointment_date < @as_of_date
            order by a.appointment_date desc, a.start_time desc, a.id desc
            limit 10;
            """;
        command.Parameters.AddWithValue("pid", pid);
        command.Parameters.Add("as_of_date", NpgsqlDbType.Date).Value = asOfDate;

        var items = new List<PatientPortalHomeAppointmentSummary>();
        var totalCount = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            totalCount = reader.GetInt32(reader.GetOrdinal("total_count"));
            var categoryId = ReadNullableInt(reader, "category_id");
            items.Add(new PatientPortalHomeAppointmentSummary(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Date: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("appointment_date")).ToString("yyyy-MM-dd"),
                StartTime: reader.GetFieldValue<TimeOnly>(reader.GetOrdinal("start_time")).ToString("HH:mm"),
                Title: reader.GetString(reader.GetOrdinal("title")),
                Status: ReadNullableString(reader, "status"),
                CategoryId: categoryId,
                CategoryName: GetAppointmentCategoryName(categoryId),
                ProviderName: ReadNullableString(reader, "provider_name"),
                FacilityName: ReadNullableString(reader, "facility_name"),
                Comments: ReadNullableString(reader, "comments")));
        }

        return new AppointmentSummaryRows(totalCount, items);
    }

    private static async Task<AppointmentRequestRows> GetPortalAppointmentRequestsAsync(
        NpgsqlConnection connection,
        string patientId,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        var storedRows = new List<PortalAppointmentRequestRow>();
        var totalCount = 0;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                    request.appointment_id,
                    request.current_state,
                    request.requested_at,
                    request.updated_at,
                    request.version,
                    request.appointment_date,
                    request.start_time,
                    request.duration_minutes,
                    request.category_id,
                    request.provider_id,
                    trim(concat(provider.first_name, ' ', provider.last_name)) as provider_name,
                    request.facility_id,
                    facility.name as facility_name,
                    request.title,
                    request.reason,
                    request.raw_status,
                    request.evidence_source,
                    count(*) over ()::int as total_count
                from patient_portal_appointment_requests request
                left join staff provider on provider.id = request.provider_id
                left join facilities facility on facility.id = request.facility_id
                where request.patient_id = @patient_id
                order by request.requested_at desc, request.appointment_id desc
                limit 50;
                """;
            command.Parameters.AddWithValue("patient_id", patientId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                totalCount = reader.GetInt32(reader.GetOrdinal("total_count"));
                storedRows.Add(new PortalAppointmentRequestRow(
                    AppointmentId: reader.GetString(reader.GetOrdinal("appointment_id")),
                    State: reader.GetString(reader.GetOrdinal("current_state")),
                    RequestedAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("requested_at")),
                    UpdatedAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("updated_at")),
                    Version: reader.GetInt32(reader.GetOrdinal("version")),
                    Date: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("appointment_date")),
                    StartTime: reader.GetFieldValue<TimeOnly>(reader.GetOrdinal("start_time")),
                    DurationMinutes: reader.GetInt32(reader.GetOrdinal("duration_minutes")),
                    CategoryId: ReadNullableInt(reader, "category_id"),
                    ProviderId: ReadNullableInt(reader, "provider_id"),
                    ProviderName: ReadNullableString(reader, "provider_name"),
                    FacilityId: ReadNullableInt(reader, "facility_id"),
                    FacilityName: ReadNullableString(reader, "facility_name"),
                    Title: reader.GetString(reader.GetOrdinal("title")),
                    Reason: ReadNullableString(reader, "reason"),
                    RawStatus: reader.GetString(reader.GetOrdinal("raw_status")),
                    EvidenceSource: reader.GetString(reader.GetOrdinal("evidence_source"))));
            }
        }

        var eventsByAppointment = storedRows.ToDictionary(
            row => row.AppointmentId,
            _ => new List<PatientPortalAppointmentRequestHistoryEvent>(),
            StringComparer.Ordinal);
        if (storedRows.Count > 0)
        {
            await using var eventCommand = connection.CreateCommand();
            eventCommand.CommandText = """
                select
                    event_id,
                    appointment_id,
                    sequence,
                    action,
                    state,
                    raw_status,
                    occurred_at,
                    evidence_source
                from patient_portal_appointment_request_events
                where appointment_id = any(@appointment_ids)
                order by appointment_id, sequence desc;
                """;
            eventCommand.Parameters.Add(
                "appointment_ids",
                NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
                storedRows.Select(row => row.AppointmentId).ToArray();
            await using var eventReader = await eventCommand.ExecuteReaderAsync(cancellationToken);
            while (await eventReader.ReadAsync(cancellationToken))
            {
                var appointmentId = eventReader.GetString(eventReader.GetOrdinal("appointment_id"));
                eventsByAppointment[appointmentId].Add(
                    new PatientPortalAppointmentRequestHistoryEvent(
                        EventId: eventReader.GetGuid(eventReader.GetOrdinal("event_id")),
                        Sequence: eventReader.GetInt32(eventReader.GetOrdinal("sequence")),
                        Action: eventReader.GetString(eventReader.GetOrdinal("action")),
                        State: eventReader.GetString(eventReader.GetOrdinal("state")),
                        RawStatus: eventReader.GetString(eventReader.GetOrdinal("raw_status")),
                        OccurredAt: eventReader
                            .GetFieldValue<DateTimeOffset>(eventReader.GetOrdinal("occurred_at"))
                            .ToString("O"),
                        EvidenceSource: eventReader.GetString(eventReader.GetOrdinal("evidence_source"))));
            }
        }

        var items = storedRows.Select(row =>
        {
            var expired = row.State == "pending" && row.Date < asOfDate;
            var state = expired ? "expired" : row.State;
            var stateLabel = state switch
            {
                "pending" => "Pending practice review",
                "accepted" => "Accepted",
                "declined" => "Declined",
                "cancelled" => "Cancelled",
                _ => "Expired"
            };
            var nextAction = state switch
            {
                "pending" => "The practice will review this request.",
                "accepted" when row.Date >= asOfDate => "Prepare for the scheduled visit.",
                "accepted" => "No further request action is required.",
                "declined" => "Submit a new request or contact the practice if care is still needed.",
                "cancelled" => "Submit a new request if care is still needed.",
                _ => "The requested date passed before acceptance; submit a new request or contact the practice."
            };

            return new PatientPortalAppointmentRequestHistoryItem(
                AppointmentId: row.AppointmentId,
                State: state,
                StateLabel: stateLabel,
                StateSource: expired
                    ? $"derived from request date against dataset as-of {asOfDate:yyyy-MM-dd}"
                    : "stored lifecycle",
                RequestedAt: row.RequestedAt.ToString("O"),
                UpdatedAt: row.UpdatedAt.ToString("O"),
                NextAction: nextAction,
                Version: row.Version,
                Date: row.Date.ToString("yyyy-MM-dd"),
                StartTime: row.StartTime.ToString("HH:mm"),
                DurationMinutes: row.DurationMinutes,
                CategoryId: row.CategoryId,
                CategoryName: GetAppointmentCategoryName(row.CategoryId),
                ProviderId: row.ProviderId,
                ProviderName: row.ProviderName,
                FacilityId: row.FacilityId,
                FacilityName: row.FacilityName,
                Title: row.Title,
                Reason: row.Reason,
                RawStatus: row.RawStatus,
                EvidenceSource: row.EvidenceSource,
                Events: eventsByAppointment[row.AppointmentId]);
        }).ToArray();

        return new AppointmentRequestRows(totalCount, items);
    }

    private static async Task<PatientPortalSessionRow> CreateSessionAsync(
        NpgsqlConnection connection,
        PatientPortalAccountRow account,
        CancellationToken cancellationToken,
        DateTimeOffset? explicitExpiresAt = null,
        string? sessionSource = null,
        Guid? externalIdentityMappingId = null,
        byte[]? externalTokenFingerprint = null)
    {
        var expiresAt = explicitExpiresAt ?? DateTimeOffset.UtcNow.AddHours(8);
        var sessionId = Guid.NewGuid();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into patient_portal_sessions
              (id, patient_id, pid, portal_username, portal_login_username, created_at, last_seen_at, expires_at, session_source,
               external_identity_mapping_id, external_token_fingerprint)
            values
              (@id, @patient_id, @pid, @portal_username, @portal_login_username, now(), now(), @expires_at, @session_source,
               @external_identity_mapping_id, @external_token_fingerprint)
            returning id, created_at, expires_at;
            """;
        command.Parameters.AddWithValue("id", sessionId);
        command.Parameters.AddWithValue("patient_id", account.CanonicalId);
        command.Parameters.AddWithValue("pid", account.LegacyPid);
        command.Parameters.AddWithValue("portal_username", account.PortalUsername);
        command.Parameters.AddWithValue("portal_login_username", account.PortalLoginUsername);
        command.Parameters.AddWithValue("expires_at", expiresAt);
        command.Parameters.AddWithValue("session_source", sessionSource ?? SessionSource);
        command.Parameters.AddWithValue("external_identity_mapping_id", (object?)externalIdentityMappingId ?? DBNull.Value);
        command.Parameters.AddWithValue("external_token_fingerprint", (object?)externalTokenFingerprint ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Patient portal session could not be created.");
        }

        return new PatientPortalSessionRow(
            SessionId: reader.GetGuid(reader.GetOrdinal("id")),
            CreatedAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
            ExpiresAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("expires_at")));
    }

    private static PatientPortalAccountRow ReadPortalAccount(NpgsqlDataReader reader)
    {
        var firstName = reader.GetString(reader.GetOrdinal("first_name"));
        var lastName = reader.GetString(reader.GetOrdinal("last_name"));
        return new PatientPortalAccountRow(
            CanonicalId: reader.GetString(reader.GetOrdinal("canonical_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            DisplayName: $"{lastName}, {firstName}",
            PortalEnabled: reader.GetBoolean(reader.GetOrdinal("portal_enabled")),
            PortalUsername: ReadNullableString(reader, "portal_username") ?? string.Empty,
            PortalLoginUsername: ReadNullableString(reader, "portal_login_username") ?? string.Empty,
            PasswordSalt: reader.GetString(reader.GetOrdinal("password_salt")),
            PasswordHash: reader.GetString(reader.GetOrdinal("password_hash")),
            PasswordStatus: reader.GetInt32(reader.GetOrdinal("password_status")),
            OneTimeToken: ReadNullableString(reader, "one_time_token"));
    }

    private static PatientPortalSessionReadRow ReadSessionRow(NpgsqlDataReader reader) => new(
        SessionId: reader.GetGuid(reader.GetOrdinal("id")),
        Username: reader.GetString(reader.GetOrdinal("portal_login_username")),
        PortalUsername: reader.GetString(reader.GetOrdinal("portal_username")),
        CanonicalId: reader.GetString(reader.GetOrdinal("patient_id")),
        LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
        CreatedAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
        LastSeenAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("last_seen_at")),
        ExpiresAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("expires_at")),
        EndedAt: reader.IsDBNull(reader.GetOrdinal("ended_at"))
            ? null
            : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("ended_at")),
        SessionSource: reader.GetString(reader.GetOrdinal("session_source")));

    private static PatientPortalSessionResponse InactiveSession(Guid sessionId, string reason) => new(
        Authenticated: false,
        SessionId: sessionId,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        CreatedAt: null,
        LastSeenAt: null,
        ExpiresAt: null,
        EndedAt: null,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static string? ReadNullableString(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetString(ordinal).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static IReadOnlyList<string> ReadStringArray(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? Array.Empty<string>()
            : reader.GetFieldValue<string[]>(ordinal);
    }

    private static int? ReadNullableInt(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static string? ReadNullableDate(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateOnly date => date.ToString("yyyy-MM-dd"),
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd"),
            _ => value.ToString()
        };
    }

    private static string? ReadNullableDateTime(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal).ToString("yyyy-MM-dd HH:mm");
    }

    private static string? GetAppointmentCategoryName(int? categoryId) => categoryId switch
    {
        5 => "Office Visit",
        9 => "Established Patient",
        10 => "New Patient",
        12 => "Health and Behavioral Assessment",
        13 => "Preventive Care Services",
        14 => "Ophthalmological Services",
        null => null,
        _ => $"Category {categoryId.Value}"
    };

    private static string GetPortalAppointmentRequestTitle(int categoryId) => categoryId switch
    {
        5 => "Office Visit",
        9 => "Established Patient",
        10 => "New Patient",
        12 => "Health and Behavioral Assessment",
        13 => "Preventive Care",
        14 => "Ophthalmological Services",
        _ => GetAppointmentCategoryName(categoryId) ?? "Office Visit"
    };

    private static string BuildPortalAppointmentReminderBody(
        PatientPortalSessionResponse session,
        PatientPortalHomeAppointmentSummary appointment,
        string? reason)
    {
        var appointmentTime = appointment.StartTime.Length == 5
            ? $"{appointment.StartTime}:00"
            : appointment.StartTime;
        var body = new StringBuilder()
            .Append("A New Appointment request was received from portal patient ")
            .Append(session.DisplayName)
            .Append(" regarding appointment dated ")
            .Append(appointment.Date)
            .Append(' ')
            .Append(appointmentTime)
            .Append('.');

        if (!string.IsNullOrWhiteSpace(reason))
        {
            body.Append(" Reason ").Append(reason).Append('.');
        }

        body.Append(" Use Portal Dashboard to confirm with patient.");
        return body.ToString();
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeDate(string? value)
    {
        var normalized = NormalizeText(value);
        return DateOnly.TryParse(normalized, out var date)
            ? date.ToString("yyyy-MM-dd")
            : null;
    }

    private static async Task UpgradePasswordHashAsync(
        NpgsqlConnection connection,
        string patientId,
        string password,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update patient_portal_accounts
            set password_hash = @password_hash
            where patient_id = @patient_id;
            """;
        command.Parameters.AddWithValue("password_hash", PasswordHashing.Hash(password));
        command.Parameters.AddWithValue("patient_id", patientId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private enum PortalMessageFolder
    {
        Inbox,
        Sent,
        All,
        Deleted
    }

    private enum PortalAppointmentWindow
    {
        Upcoming,
        Past
    }

    private sealed record PortalDownloadDocumentRow(PatientPortalDocumentItem Item, byte[] Content);

    private sealed record PortalMailboxMessageRow(PatientPortalMessageItem Item);

    private sealed record PatientPortalAccountRow(
        string CanonicalId,
        int LegacyPid,
        string Pubpid,
        string DisplayName,
        bool PortalEnabled,
        string PortalUsername,
        string PortalLoginUsername,
        string PasswordSalt,
        string PasswordHash,
        int PasswordStatus,
        string? OneTimeToken);

    private sealed record PatientPortalSessionRow(
        Guid SessionId,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt);

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private sealed record PatientAppointmentRequestDefaultsRow(int? ProviderId, int? FacilityId);

    private sealed record PortalLabOrderRow(
        int Id,
        string OrderDate,
        string? ProcedureCode,
        string ProcedureName,
        string? OrderStatus);

    private sealed record PortalLabReportRow(
        int Id,
        int OrderId,
        string? DateCollected,
        string? ReportDate,
        string? SpecimenNumber,
        string? ReportStatus,
        string? ReviewStatus);

    private sealed record PortalLabResultRow(
        int ReportId,
        PatientPortalLabResultItem Result);

    private sealed record GeneratedMedicalReportPatientRow(
        string CanonicalId,
        string Pubpid,
        string FirstName,
        string LastName,
        string? Sex,
        string? DateOfBirth,
        string? Street,
        string? City,
        string? State,
        string? PostalCode,
        string? Email,
        string? Phone);

    private sealed record GeneratedMedicalReportFacilityRow(
        string Name,
        string Phone,
        string Street,
        string City,
        string State,
        string PostalCode);

    private sealed record GeneratedMedicalReportBillingRow(
        int LineCount,
        int PaymentCount,
        decimal ChargeAmount,
        decimal PaymentAmount,
        decimal AdjustmentAmount,
        decimal BalanceAmount,
        string? LastBillingDate);

    private sealed record AppointmentSummaryRows(
        int TotalCount,
        IReadOnlyList<PatientPortalHomeAppointmentSummary> Items);

    private sealed record AppointmentRequestRows(
        int TotalCount,
        IReadOnlyList<PatientPortalAppointmentRequestHistoryItem> Items);

    private sealed record PortalAppointmentRequestRow(
        string AppointmentId,
        string State,
        DateTimeOffset RequestedAt,
        DateTimeOffset UpdatedAt,
        int Version,
        DateOnly Date,
        TimeOnly StartTime,
        int DurationMinutes,
        int? CategoryId,
        int? ProviderId,
        string? ProviderName,
        int? FacilityId,
        string? FacilityName,
        string Title,
        string? Reason,
        string RawStatus,
        string EvidenceSource);

    private sealed record PortalAttachmentUpload(string FileName, string ContentType, byte[] Content);

    private sealed record PatientPortalSessionReadRow(
        Guid SessionId,
        string Username,
        string PortalUsername,
        string CanonicalId,
        int LegacyPid,
        DateTimeOffset CreatedAt,
        DateTimeOffset LastSeenAt,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? EndedAt,
        string SessionSource)
    {
        public PatientPortalSessionResponse ToResponse(
            bool authenticated,
            string? failureReason,
            string pubpid,
            string displayName) => new(
            Authenticated: authenticated,
            SessionId: SessionId,
            Username: Username,
            PortalUsername: PortalUsername,
            CanonicalId: CanonicalId,
            LegacyPid: LegacyPid,
            Pubpid: pubpid,
            DisplayName: displayName,
            CreatedAt: CreatedAt,
            LastSeenAt: LastSeenAt,
            ExpiresAt: ExpiresAt,
            EndedAt: EndedAt,
            FailureReason: failureReason,
            SessionSource: SessionSource);
    }
}
