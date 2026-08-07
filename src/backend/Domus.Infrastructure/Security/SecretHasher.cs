using System.Security.Cryptography;
using System.Text;
using Domus.Application.Abstractions;

namespace Domus.Infrastructure.Security;

public sealed class Sha256SecretHasher : ISecretHasher
{
    public string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    public bool Verify(string value, string hash) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(value)),
            Encoding.UTF8.GetBytes(hash));
}

public sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    public string GenerateProvisioningCode()
    {
        // High-entropy one-time code shown once to the admin/device installer.
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public string GenerateMqttSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public string GenerateTemporaryPassword()
    {
        // Senha temporária legível o suficiente para compartilhar no convite (mostrada uma vez).
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(12);
        var chars = new char[12];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        }

        return new string(chars) + "!1";
    }

    public string GeneratePasswordResetToken()
    {
        // Token one-time de alta entropia (app / e-mail).
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public string GenerateMqttUsername(Guid deviceId) =>
        $"dev_{deviceId:N}";
}
