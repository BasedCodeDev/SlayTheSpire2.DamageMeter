using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Based.Spires2DamageMeter.Hosting;

/// <summary>
/// Snapshot diagnostic DTOs without putting collectible types into the framework's
/// global JSON accessor cache. Neither reflection metadata nor source objects escape.
/// </summary>
public static class LogValue
{
    public static JsonElement Freeze(object? value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, value, new(ReferenceEqualityComparer.Instance), 0);
        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static void Write(Utf8JsonWriter writer, object? value, HashSet<object> path, int depth)
    {
        if (depth >= 64) throw new JsonException("Diagnostic value exceeds maximum depth");
        switch (value)
        {
            case null: writer.WriteNullValue(); return;
            case JsonElement element: element.WriteTo(writer); return;
            case JsonDocument document: document.WriteTo(writer); return;
            case string text: writer.WriteStringValue(text); return;
            case char character: writer.WriteStringValue(character.ToString()); return;
            case bool flag: writer.WriteBooleanValue(flag); return;
            case byte number: writer.WriteNumberValue(number); return;
            case sbyte number: writer.WriteNumberValue(number); return;
            case short number: writer.WriteNumberValue(number); return;
            case ushort number: writer.WriteNumberValue(number); return;
            case int number: writer.WriteNumberValue(number); return;
            case uint number: writer.WriteNumberValue(number); return;
            case long number: writer.WriteNumberValue(number); return;
            case ulong number: writer.WriteNumberValue(number); return;
            case float number: writer.WriteNumberValue(number); return;
            case double number: writer.WriteNumberValue(number); return;
            case decimal number: writer.WriteNumberValue(number); return;
            case DateTime date: writer.WriteStringValue(date); return;
            case DateTimeOffset date: writer.WriteStringValue(date); return;
            case DateOnly date: writer.WriteStringValue(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); return;
            case TimeOnly time: writer.WriteStringValue(time.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture)); return;
            case TimeSpan duration: writer.WriteStringValue(duration.ToString("c", CultureInfo.InvariantCulture)); return;
            case Guid id: writer.WriteStringValue(id); return;
            case Uri uri: writer.WriteStringValue(uri.OriginalString); return;
            case Version version: writer.WriteStringValue(version.ToString()); return;
            case byte[] bytes: writer.WriteBase64StringValue(bytes); return;
            case Enum choice:
                if (Enum.GetUnderlyingType(choice.GetType()) == typeof(ulong)) writer.WriteNumberValue(Convert.ToUInt64(choice));
                else writer.WriteNumberValue(Convert.ToInt64(choice));
                return;
        }
        if (!path.Add(value)) throw new JsonException("Diagnostic value contains a reference cycle");
        try
        {
            if (value is IDictionary dictionary)
            {
                writer.WriteStartObject();
                foreach (DictionaryEntry entry in dictionary)
                {
                    writer.WritePropertyName(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? throw new JsonException("Null dictionary key"));
                    Write(writer, entry.Value, path, depth + 1);
                }
                writer.WriteEndObject();
            }
            else if (value is IEnumerable sequence)
            {
                writer.WriteStartArray();
                foreach (var item in sequence) Write(writer, item, path, depth + 1);
                writer.WriteEndArray();
            }
            else
            {
                writer.WriteStartObject();
                foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (property.GetMethod?.IsPublic != true || property.GetIndexParameters().Length != 0) continue;
                    var ignore = property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition;
                    if (ignore == JsonIgnoreCondition.Always) continue;
                    var member = property.GetValue(value);
                    if (ignore == JsonIgnoreCondition.WhenWritingNull && member == null) continue;
                    if (ignore == JsonIgnoreCondition.WhenWritingDefault && (member == null ||
                        property.PropertyType.IsValueType && Equals(member, Activator.CreateInstance(property.PropertyType)))) continue;
                    writer.WritePropertyName(property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name);
                    Write(writer, member, path, depth + 1);
                }
                writer.WriteEndObject();
            }
        }
        finally { path.Remove(value); }
    }
}
