using System.Security.Cryptography;
using System.Text;

namespace AvenChart.Api.Security;

public static class PasswordHashing
{
    private const string Pbkdf2Algorithm = "pbkdf2-sha256";
    private const int Pbkdf2Iterations = 600_000;
    private const int MaximumPbkdf2Iterations = 1_000_000;
    private const int SaltLength = 16;
    private const int HashLength = 32;

    public static bool Verify(string storedHash, string legacySalt, string password)
    {
        if (storedHash.StartsWith($"{Pbkdf2Algorithm}$", StringComparison.Ordinal))
        {
            return VerifyPbkdf2(storedHash, password);
        }

        var legacyHash = SHA256.HashData(Encoding.UTF8.GetBytes($"{legacySalt}:{password}"));
        var legacyHashText = Convert.ToHexString(legacyHash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(storedHash),
            Encoding.UTF8.GetBytes(legacyHashText));
    }

    public static bool RequiresUpgrade(string storedHash) =>
        !storedHash.StartsWith($"{Pbkdf2Algorithm}$", StringComparison.Ordinal);

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            HashLength);
        return string.Join(
            '$',
            Pbkdf2Algorithm,
            Pbkdf2Iterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    private static bool VerifyPbkdf2(string storedHash, string password)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4
            || !string.Equals(parts[0], Pbkdf2Algorithm, StringComparison.Ordinal)
            || !int.TryParse(parts[1], out var iterations)
            || iterations < Pbkdf2Iterations
            || iterations > MaximumPbkdf2Iterations)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            if (salt.Length != SaltLength || expectedHash.Length != HashLength)
            {
                return false;
            }

            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
