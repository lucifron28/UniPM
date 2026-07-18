using System.Security.Cryptography;
using System.Text;

namespace UniPM.Api.Features.Auth;

internal sealed class RefreshTokenGenerator
{
    public string Create() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    public string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    public bool IsWellFormed(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length == 43
            && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    public bool HashesMatch(string left, string right)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(left),
            Encoding.ASCII.GetBytes(right));
}
