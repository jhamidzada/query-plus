using System.Security.Cryptography;
using System.Text;

namespace QueryPlus.App.Services;

/// <summary>
/// Encrypts secrets with Windows DPAPI (current user). A protected blob can only be decrypted
/// by the same Windows account on the same machine — so passwords persisted in config.json are
/// never readable as plaintext, nor usable on another machine/user.
/// </summary>
public static class SecretProtector
{
    // Deliberately still the pre-rename value: changing it would make every password saved by
    // earlier builds (as MultiScriptPlus) undecryptable. It's an opaque salt, not a brand name.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MultiScriptPlus.v1");

    /// <summary>Returns a base64 DPAPI blob, or null for empty/failed input.</summary>
    public static string? Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return null;
        try
        {
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plaintext), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Decrypts a base64 DPAPI blob; returns empty string if absent or undecryptable.</summary>
    public static string Unprotect(string? protectedBase64)
    {
        if (string.IsNullOrEmpty(protectedBase64))
            return string.Empty;
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedBase64), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}
