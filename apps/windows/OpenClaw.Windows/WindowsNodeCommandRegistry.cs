namespace OpenClaw.Windows;

/// <summary>
/// Async handler for a single Windows node command.
/// </summary>
public delegate Task<WindowsCanvasInvokeResponse> WindowsNodeCommandHandler(
    WindowsCanvasInvokeRequest request,
    CancellationToken cancellationToken);

/// <summary>
/// General registry of Windows node commands, capabilities, and permissions.
/// </summary>
/// <remarks>
/// The node transport reads its connect surface (caps/commands/permissions) from this registry and routes
/// <c>node.invoke</c> requests through it, so command behavior — Canvas/A2UI today, native device commands
/// later — lives in handlers rather than being owned by the transport.
/// </remarks>
public sealed class WindowsNodeCommandRegistry
{
    private readonly Dictionary<string, WindowsNodeCommandHandler> handlers = new(StringComparer.Ordinal);
    private readonly List<string> commandOrder = [];
    private readonly List<string> capabilities = [];
    private readonly Dictionary<string, object?> permissions = new(StringComparer.Ordinal);

    /// <summary>
    /// Commands advertised to the gateway, in registration order.
    /// </summary>
    public IReadOnlyList<string> Commands => this.commandOrder;

    /// <summary>
    /// Node capabilities advertised to the gateway, in declaration order.
    /// </summary>
    public IReadOnlyList<string> Capabilities => this.capabilities;

    /// <summary>
    /// Node permission claims advertised to the gateway.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Permissions => this.permissions;

    /// <summary>
    /// Registers (or replaces) the handler for a command and records its advertised order.
    /// </summary>
    public void Register(string command, WindowsNodeCommandHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        this.DeclareCommand(command);
        this.handlers[command] = handler;
    }

    /// <summary>
    /// Advertises a command without attaching a handler yet. Invoking it returns a structured
    /// not-implemented failure until a handler is registered.
    /// </summary>
    public void DeclareCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command must be a non-empty string.", nameof(command));
        }
        if (!this.commandOrder.Contains(command, StringComparer.Ordinal))
        {
            this.commandOrder.Add(command);
        }
    }

    /// <summary>
    /// Declares a node capability once, preserving declaration order.
    /// </summary>
    public void DeclareCapability(string capability)
    {
        if (string.IsNullOrWhiteSpace(capability))
        {
            throw new ArgumentException("Capability must be a non-empty string.", nameof(capability));
        }
        if (!this.capabilities.Contains(capability, StringComparer.Ordinal))
        {
            this.capabilities.Add(capability);
        }
    }

    /// <summary>
    /// Declares (or replaces) a node permission claim.
    /// </summary>
    public void DeclarePermission(string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Permission key must be a non-empty string.", nameof(key));
        }
        this.permissions[key] = value;
    }

    /// <summary>
    /// True when a handler is registered for the command.
    /// </summary>
    public bool Contains(string command) => this.handlers.ContainsKey(command);

    /// <summary>
    /// Dispatches an invoke request to its registered handler, or returns a structured unknown-command failure.
    /// </summary>
    public Task<WindowsCanvasInvokeResponse> InvokeAsync(
        WindowsCanvasInvokeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (this.handlers.TryGetValue(request.Command, out var handler))
        {
            return handler(request, cancellationToken);
        }
        if (this.commandOrder.Contains(request.Command, StringComparer.Ordinal))
        {
            return Task.FromResult(WindowsCanvasInvokeResponse.Failure(
                "UNAVAILABLE",
                $"Node command not implemented yet: {request.Command}"));
        }
        return Task.FromResult(WindowsCanvasInvokeResponse.Failure(
            "INVALID_REQUEST",
            $"Unknown node command: {request.Command}"));
    }
}
