using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

internal sealed class InMemoryAppCredentialStore : IAppCredentialStore
{
    private string? gatewayToken;
    private string? deviceToken;
    private string? devicePrivateKey;

    public Task<string?> LoadGatewayTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.gatewayToken);
    }

    public Task SaveGatewayTokenAsync(string? token, CancellationToken cancellationToken = default)
    {
        this.gatewayToken = token;
        return Task.CompletedTask;
    }

    public Task<string?> LoadDeviceTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.deviceToken);
    }

    public Task SaveDeviceTokenAsync(string? token, CancellationToken cancellationToken = default)
    {
        this.deviceToken = token;
        return Task.CompletedTask;
    }

    public Task<string?> LoadDevicePrivateKeyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.devicePrivateKey);
    }

    public Task SaveDevicePrivateKeyAsync(string? privateKey, CancellationToken cancellationToken = default)
    {
        this.devicePrivateKey = privateKey;
        return Task.CompletedTask;
    }
}
