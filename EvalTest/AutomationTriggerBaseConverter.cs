using System.Text.Json.Serialization;
using System.Text.Json;

namespace EvalTest;

/// <summary>
/// Converter class that handles polymorphic serialization of <see cref="AutomationTriggerBase"/> objects.
/// </summary>
public class AutomationTriggerBaseConverter : JsonConverter<AutomationTriggerBase>
{
    public override AutomationTriggerBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty("Type", out var typeProperty))
            throw new JsonException("Missing 'Type' discriminator field.");

        string type = typeProperty.GetString()!;
        var json = root.GetRawText();

        return type switch
        {
            "Simple" => JsonSerializer.Deserialize<SimpleTrigger>(json, options)!,
            "Composite" => JsonSerializer.Deserialize<CompositeTrigger>(json, options)!,
            _ => throw new JsonException($"Unknown condition type: {type}")
        };
    }

    public override void Write(Utf8JsonWriter writer, AutomationTriggerBase value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Type", value switch
        {
            SimpleTrigger => "Simple",
            CompositeTrigger => "Composite",
            _ => throw new NotSupportedException($"Unknown type {value.GetType()}")
        });

        foreach (var property in JsonSerializer.SerializeToElement(value, value.GetType(), options).EnumerateObject())
        {
            property.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
