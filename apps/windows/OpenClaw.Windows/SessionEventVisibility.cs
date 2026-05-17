using System.Text.Json;

namespace OpenClaw.Windows;

public enum SessionEventVisibilityPreset
{
    Custom,
    ShowAll,
    HideOperational,
    ChatOnly,
}

public sealed record SessionEventVisibilityPreferences(
    IReadOnlyDictionary<string, bool> EventTypes,
    SessionEventVisibilityPreset Preset)
{
    public static readonly IReadOnlyList<string> KnownEventTypes =
    [
        "agent",
        "chat",
        "chat.side_result",
        "connect.challenge",
        "cron",
        "device.pair.requested",
        "device.pair.resolved",
        "exec.approval.requested",
        "exec.approval.resolved",
        "health",
        "heartbeat",
        "node.invoke.request",
        "node.pair.requested",
        "node.pair.resolved",
        "plugin.approval.requested",
        "plugin.approval.resolved",
        "presence",
        "session.message",
        "session.tool",
        "sessions.changed",
        "shutdown",
        "talk.event",
        "talk.mode",
        "tick",
        "update.available",
        "voicewake.changed",
        "voicewake.routing.changed",
    ];

    public static SessionEventVisibilityPreferences Default { get; } = From(null);

    public static SessionEventVisibilityPreferences From(
        IReadOnlyDictionary<string, bool>? eventTypes,
        SessionEventVisibilityPreset preset = SessionEventVisibilityPreset.Custom)
    {
        var normalized = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var eventType in KnownEventTypes)
        {
            normalized[eventType] = true;
        }

        if (eventTypes is not null)
        {
            foreach (var (eventType, visible) in eventTypes)
            {
                var normalizedName = NormalizeEventType(eventType);
                if (normalizedName.Length > 0)
                {
                    normalized[normalizedName] = visible;
                }
            }
        }

        return new SessionEventVisibilityPreferences(normalized, preset);
    }

    public bool IsVisible(string eventType)
    {
        var normalizedName = NormalizeEventType(eventType);
        return normalizedName.Length == 0 ||
            !this.EventTypes.TryGetValue(normalizedName, out var visible) ||
            visible;
    }

    public SessionEventVisibilityPreferences WithEventType(string eventType, bool visible)
    {
        var normalizedName = NormalizeEventType(eventType);
        if (normalizedName.Length == 0)
        {
            return this;
        }

        var next = new Dictionary<string, bool>(this.EventTypes, StringComparer.Ordinal)
        {
            [normalizedName] = visible,
        };
        return From(next);
    }

    public SessionEventVisibilityPreferences WithEventTypes(IEnumerable<string> eventTypes, bool visible)
    {
        var next = new Dictionary<string, bool>(this.EventTypes, StringComparer.Ordinal);
        foreach (var eventType in eventTypes)
        {
            var normalizedName = NormalizeEventType(eventType);
            if (normalizedName.Length > 0)
            {
                next[normalizedName] = visible;
            }
        }

        return From(next);
    }

    public SessionEventVisibilityPreferences WithObservedEvents(IEnumerable<GatewayRealtimeEvent> events)
    {
        var next = new Dictionary<string, bool>(this.EventTypes, StringComparer.Ordinal);
        foreach (var @event in events)
        {
            var normalizedName = NormalizeEventType(@event.Name);
            if (normalizedName.Length > 0 && !next.ContainsKey(normalizedName))
            {
                next[normalizedName] = this.Preset switch
                {
                    SessionEventVisibilityPreset.ChatOnly => SessionEventVisibility.IsChatEventType(normalizedName),
                    SessionEventVisibilityPreset.HideOperational => !SessionEventVisibility.IsOperationalEventType(normalizedName),
                    _ => true,
                };
            }
        }

        return From(next, this.Preset);
    }

    private static string NormalizeEventType(string? eventType)
    {
        return string.IsNullOrWhiteSpace(eventType) ? "" : eventType.Trim();
    }
}

public static class SessionEventVisibility
{
    public const int MaxRealtimeEvents = 500;

    private static readonly string[] OperationalEventTypes =
    [
        "health",
        "heartbeat",
        "presence",
        "sessions.changed",
        "tick",
    ];

    private static readonly string[] ChatEventTypes =
    [
        "chat",
        "session.message",
    ];

    public static void AddBounded(IList<GatewayRealtimeEvent> events, GatewayRealtimeEvent @event)
    {
        events.Add(@event);
        while (events.Count > MaxRealtimeEvents)
        {
            events.RemoveAt(0);
        }
    }

    public static IReadOnlyList<GatewayRealtimeEvent> Filter(
        IEnumerable<GatewayRealtimeEvent> events,
        SessionEventVisibilityPreferences preferences,
        string? activeSession)
    {
        return events
            .Where(@event => preferences.IsVisible(@event.Name))
            .Where(@event => IsRelevantToSession(@event, activeSession))
            .ToArray();
    }

    public static int CountHidden(
        IEnumerable<GatewayRealtimeEvent> events,
        SessionEventVisibilityPreferences preferences,
        string? activeSession)
    {
        return events.Count(@event => IsRelevantToSession(@event, activeSession) && !preferences.IsVisible(@event.Name));
    }

    public static IReadOnlyList<string> EventTypesForControls(
        IEnumerable<GatewayRealtimeEvent> events,
        SessionEventVisibilityPreferences preferences)
    {
        var eventTypes = new HashSet<string>(SessionEventVisibilityPreferences.KnownEventTypes, StringComparer.Ordinal);
        foreach (var eventType in preferences.EventTypes.Keys)
        {
            eventTypes.Add(eventType);
        }

        foreach (var @event in events)
        {
            if (!string.IsNullOrWhiteSpace(@event.Name))
            {
                eventTypes.Add(@event.Name.Trim());
            }
        }

        return eventTypes.OrderBy(EventGroupOrder).ThenBy(static eventType => eventType, StringComparer.Ordinal).ToArray();
    }

    public static SessionEventVisibilityPreferences ShowAll(SessionEventVisibilityPreferences preferences)
    {
        return preferences.WithEventTypes(preferences.EventTypes.Keys, true) with { Preset = SessionEventVisibilityPreset.ShowAll };
    }

    public static SessionEventVisibilityPreferences HideOperational(SessionEventVisibilityPreferences preferences)
    {
        return preferences.WithEventTypes(preferences.EventTypes.Keys, true)
            .WithEventTypes(OperationalEventTypes, false) with
        {
            Preset = SessionEventVisibilityPreset.HideOperational,
        };
    }

    public static SessionEventVisibilityPreferences ChatOnly(SessionEventVisibilityPreferences preferences)
    {
        var next = preferences.WithEventTypes(preferences.EventTypes.Keys, false);
        return next.WithEventTypes(ChatEventTypes, true) with { Preset = SessionEventVisibilityPreset.ChatOnly };
    }

    public static bool IsOperationalEventType(string eventType)
    {
        return OperationalEventTypes.Contains(eventType, StringComparer.Ordinal);
    }

    public static bool IsChatEventType(string eventType)
    {
        return ChatEventTypes.Contains(eventType, StringComparer.Ordinal);
    }

    private static bool IsRelevantToSession(GatewayRealtimeEvent @event, string? activeSession)
    {
        if (string.IsNullOrWhiteSpace(activeSession) ||
            @event.Payload is not { } payload ||
            !TryReadString(payload, "sessionKey", out var eventSessionKey))
        {
            return true;
        }

        return string.Equals(eventSessionKey, activeSession.Trim(), StringComparison.Ordinal);
    }

    private static int EventGroupOrder(string eventType)
    {
        return eventType switch
        {
            "chat" or "session.message" => 0,
            "chat.side_result" or "session.tool" => 1,
            "tick" or "health" or "heartbeat" or "presence" or "sessions.changed" => 2,
            _ when eventType.Contains("pair.", StringComparison.Ordinal) => 3,
            _ when eventType.Contains("approval.", StringComparison.Ordinal) => 3,
            _ => 4,
        };
    }

    private static bool TryReadString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }
}
