using System.Text.Json;

namespace OpenClaw.Windows;

/// <summary>
/// Projects gateway transcript records into human chat rows while leaving operational records for Gateway Events.
/// </summary>
public static class ChatTranscriptProjector
{
    public static ChatMessage? Project(JsonElement element)
    {
        if (TryProjectGatewayEvent(element, out var eventMessage))
        {
            return eventMessage;
        }

        var role = ReadString(element, "role") ?? ReadString(element, "kind") ?? "message";
        if (IsOperationalRole(role))
        {
            return null;
        }

        var text =
            ReadString(element, "text") ??
            ReadTextContent(element, "content") ??
            ReadString(element, "message");
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (TryProjectJsonOrJsonLines(text, out var projected))
        {
            return projected;
        }

        return new ChatMessage(role, text);
    }

    private static bool TryProjectGatewayEvent(JsonElement element, out ChatMessage? message)
    {
        message = null;
        var eventType = ReadString(element, "type") ?? ReadString(element, "event") ?? ReadString(element, "name");
        var stream = ReadString(element, "stream");
        if (string.Equals(eventType, "chat", StringComparison.Ordinal))
        {
            if (TryGetObject(element, out var payload, "payload"))
            {
                message = ProjectChatProjection(payload);
            }
            return true;
        }

        if (IsAssistantEvent(eventType) || string.Equals(stream, "assistant", StringComparison.Ordinal))
        {
            message = ProjectAssistantPayload(element);
            return true;
        }

        if (IsCompletionEvent(eventType))
        {
            message = ProjectCompletionPayload(element);
            return true;
        }

        if (IsOperationalEvent(eventType) || IsOperationalStream(stream))
        {
            return true;
        }

        return false;
    }

    private static ChatMessage? ProjectChatProjection(JsonElement payload)
    {
        var state = ReadString(payload, "state");
        if (state is not ("delta" or "final"))
        {
            return null;
        }

        var text = ReadString(payload, "deltaText");
        if (TryGetObject(payload, out var message, "message"))
        {
            text = ReadTextContent(message, "content") ?? ReadString(message, "text") ?? text;
        }

        return string.IsNullOrWhiteSpace(text) ? null : new ChatMessage("assistant", text);
    }

    private static ChatMessage? ProjectAssistantPayload(JsonElement element)
    {
        var source = TryGetObject(element, out var data, "data")
            ? data
            : TryGetObject(element, out var payload, "payload")
                ? payload
                : element;
        var text =
            ReadString(source, "text") ??
            ReadString(source, "delta") ??
            ReadString(source, "outputText") ??
            ReadTextContent(source, "content");
        if (string.IsNullOrWhiteSpace(text) && TryGetObject(source, out var message, "message"))
        {
            text = ReadTextContent(message, "content") ?? ReadString(message, "text");
        }

        return string.IsNullOrWhiteSpace(text) ? null : new ChatMessage("assistant", text);
    }

    private static ChatMessage? ProjectCompletionPayload(JsonElement element)
    {
        var source = TryGetObject(element, out var data, "data")
            ? data
            : TryGetObject(element, out var payload, "payload")
                ? payload
                : element;
        var text = ReadString(source, "outputText");
        return string.IsNullOrWhiteSpace(text) ? null : new ChatMessage("assistant", text);
    }

    private static bool TryProjectJsonOrJsonLines(string text, out ChatMessage? message)
    {
        message = null;
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0 || !LooksLikeJson(lines[0]))
        {
            return false;
        }

        var projected = new List<ChatMessage>();
        var recognizedGatewayRecords = false;
        foreach (var line in lines)
        {
            if (!LooksLikeJson(line))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                if (TryProjectGatewayEvent(document.RootElement, out var row))
                {
                    recognizedGatewayRecords = true;
                    if (row is not null)
                    {
                        projected.Add(row);
                    }
                    continue;
                }

                var role = ReadString(document.RootElement, "role") ?? ReadString(document.RootElement, "kind");
                if (role is not null && IsOperationalRole(role))
                {
                    recognizedGatewayRecords = true;
                    continue;
                }

                if (role is not null && Project(document.RootElement) is { } chatRow)
                {
                    recognizedGatewayRecords = true;
                    projected.Add(chatRow);
                    continue;
                }

                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        if (!recognizedGatewayRecords)
        {
            return false;
        }

        if (projected.Count > 0)
        {
            message = new ChatMessage(
                projected[0].Role,
                string.Join(Environment.NewLine, projected.Select(static row => row.Text)));
        }
        return true;
    }

    private static string? ReadTextContent(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Array => ReadTextParts(value),
            _ => null,
        };
    }

    private static string? ReadTextParts(JsonElement array)
    {
        var text = string.Concat(array.EnumerateArray().Select(static part =>
        {
            var type = ReadString(part, "type");
            if (type is not ("text" or "output_text" or "input_text"))
            {
                return "";
            }
            return ReadString(part, "text") ?? "";
        }));
        return text.Length == 0 ? null : text;
    }

    private static bool TryGetObject(JsonElement root, out JsonElement value, string propertyName)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(propertyName, out value) &&
            value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
        return false;
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private static bool LooksLikeJson(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static bool IsAssistantEvent(string? eventType)
    {
        return eventType is "assistant.delta" or "assistant.message";
    }

    private static bool IsCompletionEvent(string? eventType)
    {
        return eventType is "run.completed";
    }

    private static bool IsOperationalEvent(string? eventType)
    {
        return eventType is not null &&
            (eventType.StartsWith("tool.", StringComparison.Ordinal) ||
                eventType.StartsWith("thinking.", StringComparison.Ordinal) ||
                eventType.StartsWith("approval.", StringComparison.Ordinal) ||
                eventType.StartsWith("artifact.", StringComparison.Ordinal) ||
                eventType.StartsWith("session.", StringComparison.Ordinal) ||
                eventType.StartsWith("question.", StringComparison.Ordinal) ||
                eventType is "run.created" or "run.queued" or "run.started" or "run.failed" or "run.cancelled" or "run.timed_out");
    }

    private static bool IsOperationalStream(string? stream)
    {
        return stream is "thinking" or "plan" or "tool" or "item" or "command_output" or "approval" or "patch";
    }

    private static bool IsOperationalRole(string role)
    {
        return role is "tool" or "toolResult" or "function" or "system";
    }
}
