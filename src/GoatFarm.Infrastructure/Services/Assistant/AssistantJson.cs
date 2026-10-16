using System.Text.Json;

namespace GoatFarm.Infrastructure.Services.Assistant;

internal static class AssistantJson
{
    public static bool TryGetString(JsonElement root, string name, out string? value)
    {
        value = null;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var el))
            return false;
        if (el.ValueKind == JsonValueKind.Null || el.ValueKind == JsonValueKind.Undefined)
            return false;
        if (el.ValueKind == JsonValueKind.String)
        {
            value = el.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        value = el.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }

    public static bool HasProperty(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out _);

    public static JsonElement EmptyObject()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }

    public static bool TryParseArguments(string? json, out JsonElement element, out string? error)
    {
        element = default;
        error = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            element = EmptyObject();
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind is not JsonValueKind.Object and not JsonValueKind.Null)
            {
                error = "Tool arguments must be a JSON object.";
                return false;
            }

            element = doc.RootElement.ValueKind == JsonValueKind.Null
                ? EmptyObject()
                : doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            error = "The model sent invalid tool arguments.";
            return false;
        }
    }
}
