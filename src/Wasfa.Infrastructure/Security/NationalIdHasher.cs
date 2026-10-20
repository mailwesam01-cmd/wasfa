using System.Security.Cryptography;
using System.Text;

namespace Wasfa.Infrastructure.Security;

public static class NationalIdHasher
{
    private const string SystemPepper = "WASFA_JORDAN_NATIONAL_HEALTH_PEPPER_2026_SECURE";

    public static string HashNationalId(string cleartextNationalId, string? salt = null)
    {
        var effectiveSalt = salt ?? "JORDAN_ID_SALT_V1";
        var raw = $"{effectiveSalt}:{cleartextNationalId.Trim()}:{SystemPepper}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SystemPepper));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
