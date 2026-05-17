using System.Text.Json;
using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class SessionEventVisibilityTests
{
    [TestMethod]
    public void DefaultsShowKnownAndUnknownEvents()
    {
        var preferences = SessionEventVisibilityPreferences.Default;

        Assert.IsTrue(preferences.IsVisible("tick"));
        Assert.IsTrue(preferences.IsVisible("custom.event"));
    }

    [TestMethod]
    public void ChatOnlyKeepsChatTranscriptEventsVisible()
    {
        var preferences = SessionEventVisibility.ChatOnly(SessionEventVisibilityPreferences.Default);

        Assert.IsTrue(preferences.IsVisible("chat"));
        Assert.IsTrue(preferences.IsVisible("session.message"));
        Assert.IsFalse(preferences.IsVisible("tick"));
        Assert.IsFalse(preferences.IsVisible("session.tool"));
        Assert.AreEqual(SessionEventVisibilityPreset.ChatOnly, preferences.Preset);
    }

    [TestMethod]
    public void ChatOnlyKeepsNewObservedNonChatEventsHidden()
    {
        var preferences = SessionEventVisibility.ChatOnly(SessionEventVisibilityPreferences.Default);
        var next = preferences.WithObservedEvents([new GatewayRealtimeEvent("plugin.custom", null)]);

        Assert.IsFalse(next.IsVisible("plugin.custom"));
        Assert.AreEqual(SessionEventVisibilityPreset.ChatOnly, next.Preset);
    }

    [TestMethod]
    public void HideOperationalKeepsNewObservedOperationalEventsHidden()
    {
        var preferences = SessionEventVisibility.HideOperational(SessionEventVisibilityPreferences.Default);
        var next = preferences.WithObservedEvents([new GatewayRealtimeEvent("tick", null)]);

        Assert.IsFalse(next.IsVisible("tick"));
        Assert.AreEqual(SessionEventVisibilityPreset.HideOperational, next.Preset);
    }

    [TestMethod]
    public void NewlyObservedEventTypesAreAvailableForControls()
    {
        var preferences = SessionEventVisibilityPreferences.Default;
        var events = new[]
        {
            new GatewayRealtimeEvent("custom.event", null),
        };

        var eventTypes = SessionEventVisibility.EventTypesForControls(events, preferences);

        CollectionAssert.Contains(eventTypes.ToArray(), "custom.event");
    }

    [TestMethod]
    public void HiddenEventTypeCanBeRestoredWithoutRefetching()
    {
        var events = new[]
        {
            new GatewayRealtimeEvent("tick", null),
        };
        var hidden = SessionEventVisibilityPreferences.Default.WithEventType("tick", false);

        var hiddenEvents = SessionEventVisibility.Filter(events, hidden, activeSession: "main");
        var restoredEvents = SessionEventVisibility.Filter(events, hidden.WithEventType("tick", true), activeSession: "main");

        Assert.HasCount(0, hiddenEvents);
        Assert.HasCount(1, restoredEvents);
    }

    [TestMethod]
    public void FilterUsesSessionKeyWhenPresent()
    {
        var events = new[]
        {
            new GatewayRealtimeEvent("chat", Payload("""{"sessionKey":"main","text":"shown"}""")),
            new GatewayRealtimeEvent("chat", Payload("""{"sessionKey":"other","text":"hidden"}""")),
            new GatewayRealtimeEvent("health", null),
        };

        var visible = SessionEventVisibility.Filter(events, SessionEventVisibilityPreferences.Default, "main");

        Assert.HasCount(2, visible);
        Assert.AreEqual("chat", visible[0].Name);
        Assert.AreEqual("health", visible[1].Name);
    }

    [TestMethod]
    public void CountHiddenOnlyCountsRelevantEvents()
    {
        var events = new[]
        {
            new GatewayRealtimeEvent("tick", Payload("""{"sessionKey":"main"}""")),
            new GatewayRealtimeEvent("tick", Payload("""{"sessionKey":"other"}""")),
            new GatewayRealtimeEvent("chat", Payload("""{"sessionKey":"main"}""")),
        };
        var preferences = SessionEventVisibilityPreferences.Default.WithEventType("tick", false);

        var hiddenCount = SessionEventVisibility.CountHidden(events, preferences, "main");

        Assert.AreEqual(1, hiddenCount);
    }

    [TestMethod]
    public void AddBoundedDropsOldestEvents()
    {
        var events = new List<GatewayRealtimeEvent>();
        for (var i = 0; i < SessionEventVisibility.MaxRealtimeEvents + 2; i++)
        {
            SessionEventVisibility.AddBounded(events, new GatewayRealtimeEvent($"event.{i}", null));
        }

        Assert.HasCount(SessionEventVisibility.MaxRealtimeEvents, events);
        Assert.AreEqual("event.2", events[0].Name);
    }

    private static JsonElement Payload(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
