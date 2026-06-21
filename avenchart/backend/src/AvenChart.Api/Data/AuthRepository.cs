using System.Security.Cryptography;
using System.Text;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class AuthRepository(NpgsqlDataSource dataSource)
{
    private const string InvalidCredentialsMessage = "Invalid username or password.";

    public async Task<AuthLoginResponse> LoginAsync(AuthLoginRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Failed(username);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select username, display_name, role, staff_id, password_salt, password_hash
            from auth_accounts
            where lower(username) = lower(@username)
              and active = true
            limit 1;
            """;
        command.Parameters.AddWithValue("username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return Failed(username);
        }

        var storedHash = reader.GetString(reader.GetOrdinal("password_hash"));
        var computedHash = HashPassword(
            reader.GetString(reader.GetOrdinal("password_salt")),
            request.Password);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(storedHash),
                Encoding.UTF8.GetBytes(computedHash)))
        {
            return Failed(username);
        }

        return new AuthLoginResponse(
            Authenticated: true,
            Username: reader.GetString(reader.GetOrdinal("username")),
            DisplayName: reader.GetString(reader.GetOrdinal("display_name")),
            Role: reader.GetString(reader.GetOrdinal("role")),
            StaffId: reader.IsDBNull(reader.GetOrdinal("staff_id")) ? null : reader.GetInt32(reader.GetOrdinal("staff_id")),
            FailureReason: null);
    }

    private static AuthLoginResponse Failed(string username)
    {
        return new AuthLoginResponse(
            Authenticated: false,
            Username: username,
            DisplayName: string.Empty,
            Role: string.Empty,
            StaffId: null,
            FailureReason: InvalidCredentialsMessage);
    }

    private static string HashPassword(string salt, string password)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{password}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
