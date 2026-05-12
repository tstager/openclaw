using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace OpenClaw.Windows;

/// <summary>
/// Represents the Windows companion device keypair used for gateway pairing and signed reconnects.
/// </summary>
public sealed record WindowsDeviceIdentity(
    string DeviceId,
    string PublicKeyPem,
    string PrivateKeyPem)
{
    /// <summary>
    /// Signs the canonical gateway auth payload with the persisted P-256 private key.
    /// </summary>
    public string SignPayload(string payload)
    {
        using var key = ECDsa.Create();
        key.ImportFromPem(this.PrivateKeyPem);
        var signature = key.SignData(
            Encoding.UTF8.GetBytes(payload),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);
        return Base64UrlEncode(signature);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

/// <summary>
/// Loads or creates the long-lived Windows device identity stored in the credential store.
/// </summary>
public sealed class DeviceIdentityStore(IAppCredentialStore credentials)
{
    private readonly IAppCredentialStore credentials = credentials;

    /// <summary>
    /// Reuses a valid persisted key or creates and stores a new identity when the key is missing/corrupt.
    /// </summary>
    public async Task<WindowsDeviceIdentity> LoadOrCreateAsync(CancellationToken cancellationToken = default)
    {
        var privateKeyPem = await this.credentials.LoadDevicePrivateKeyAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(privateKeyPem) && TryLoadIdentity(privateKeyPem, out var identity))
        {
            return identity;
        }

        var created = CreateIdentity();
        await this.credentials.SaveDevicePrivateKeyAsync(created.PrivateKeyPem, cancellationToken);
        return created;
    }

    /// <summary>
    /// Clears device pairing material so the next connect must pair again.
    /// </summary>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await this.credentials.SaveDeviceTokenAsync(null, cancellationToken);
        await this.credentials.SaveDevicePrivateKeyAsync(null, cancellationToken);
    }

    /// <summary>
    /// Builds the exact v3 string that the gateway verifies before accepting a Windows device identity.
    /// </summary>
    public static string BuildDeviceAuthPayloadV3(
        string deviceId,
        string clientId,
        string clientMode,
        string role,
        IReadOnlyList<string> scopes,
        long signedAtMs,
        string? token,
        string nonce,
        string platform,
        string? deviceFamily)
    {
        return string.Join("|", new[]
        {
            "v3",
            deviceId,
            clientId,
            clientMode,
            role,
            string.Join(",", scopes),
            signedAtMs.ToString(CultureInfo.InvariantCulture),
            token ?? "",
            nonce,
            NormalizeDeviceMetadata(platform),
            NormalizeDeviceMetadata(deviceFamily),
        });
    }

    private static bool TryLoadIdentity(string privateKeyPem, out WindowsDeviceIdentity identity)
    {
        try
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(privateKeyPem);
            identity = BuildIdentity(key, privateKeyPem);
            return true;
        }
        catch
        {
            identity = null!;
            return false;
        }
    }

    private static WindowsDeviceIdentity CreateIdentity()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privateKeyPem = WritePem("PRIVATE KEY", key.ExportPkcs8PrivateKey());
        return BuildIdentity(key, privateKeyPem);
    }

    private static WindowsDeviceIdentity BuildIdentity(ECDsa key, string privateKeyPem)
    {
        var publicKeyDer = key.ExportSubjectPublicKeyInfo();
        var publicKeyPem = WritePem("PUBLIC KEY", publicKeyDer);
        var deviceId = Convert.ToHexString(SHA256.HashData(publicKeyDer)).ToLowerInvariant();
        return new WindowsDeviceIdentity(deviceId, publicKeyPem, privateKeyPem);
    }

    private static string NormalizeDeviceMetadata(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
    }

    private static string WritePem(string label, byte[] data)
    {
        return new string(PemEncoding.Write(label, data));
    }

}
