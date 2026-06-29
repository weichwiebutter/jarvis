using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HermesPaperBot.Models;

/// <summary>
/// Converts paper-only release mode values from cloud package JSON.
/// </summary>
public sealed class ReleaseModeJsonConverter : JsonConverter<ReleaseMode>
{
    /// <inheritdoc />
    public override ReleaseMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.ToLowerInvariant() switch
        {
            "paper_only" => ReleaseMode.PaperOnly,
            "demo_candidate" => ReleaseMode.DemoCandidate,
            "live_forbidden" => ReleaseMode.LiveForbidden,
            _ => ReleaseMode.PaperOnly,
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ReleaseMode value, JsonSerializerOptions options)
    {
        var serialized = value switch
        {
            ReleaseMode.PaperOnly => "paper_only",
            ReleaseMode.DemoCandidate => "demo_candidate",
            ReleaseMode.LiveForbidden => "live_forbidden",
            _ => "paper_only",
        };

        writer.WriteStringValue(serialized);
    }
}
