using Windows.Security.Credentials;

namespace OpenClaw.Windows;

/// <summary>
/// Abstracts secret storage so tests can use an in-memory store while production uses Windows PasswordVault.
/// </summary>
public interface IAppCredentialStore
{
    Task<string?> LoadGatewayTokenAsync(CancellationToken cancellationToken = default);

    Task SaveGatewayTokenAsync(string? token, CancellationToken cancellationToken = default);

    Task<string?> LoadDeviceTokenAsync(CancellationToken cancellationToken = default);

    Task SaveDeviceTokenAsync(string? token, CancellationToken cancellationToken = default);

    Task<string?> LoadDevicePrivateKeyAsync(CancellationToken cancellationToken = default);

    Task SaveDevicePrivateKeyAsync(string? privateKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores gateway and device secrets in Windows Credential Locker instead of the JSON preferences file.
/// </summary>
public sealed class PasswordVaultAppCredentialStore : IAppCredentialStore
{
    private const string Resource = "OpenClaw.WindowsCompanion";
    private const string GatewayTokenUserName = "gateway-token";
    private const string DeviceTokenUserName = "device-token";
    private const string DevicePrivateKeyUserName = "device-private-key";
    private readonly PasswordVault vault = new();

    /// <summary>
    /// Returns null when the credential is absent, empty, or inaccessible.
    /// </summary>
    public Task<string?> LoadGatewayTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.Load(GatewayTokenUserName));
    }

    public Task SaveGatewayTokenAsync(string? token, CancellationToken cancellationToken = default)
    {
        this.Save(GatewayTokenUserName, token);
        return Task.CompletedTask;
    }

    public Task<string?> LoadDeviceTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.Load(DeviceTokenUserName));
    }

    public Task SaveDeviceTokenAsync(string? token, CancellationToken cancellationToken = default)
    {
        this.Save(DeviceTokenUserName, token);
        return Task.CompletedTask;
    }

    public Task<string?> LoadDevicePrivateKeyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.Load(DevicePrivateKeyUserName));
    }

    public Task SaveDevicePrivateKeyAsync(string? privateKey, CancellationToken cancellationToken = default)
    {
        this.Save(DevicePrivateKeyUserName, privateKey);
        return Task.CompletedTask;
    }

    private string? Load(string userName)
    {
        try
        {
            var credential = this.vault.Retrieve(Resource, userName);
            credential.RetrievePassword();
            return string.IsNullOrWhiteSpace(credential.Password) ? null : credential.Password;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Removes the previous secret before adding the replacement because PasswordVault does not overwrite.
    /// </summary>
    private void Save(string userName, string? secret)
    {
        this.Remove(userName);
        if (!string.IsNullOrWhiteSpace(secret))
        {
            this.vault.Add(new PasswordCredential(Resource, userName, secret));
        }
    }

    private void Remove(string userName)
    {
        try
        {
            this.vault.Remove(this.vault.Retrieve(Resource, userName));
        }
        catch
        {
        }
    }
}
