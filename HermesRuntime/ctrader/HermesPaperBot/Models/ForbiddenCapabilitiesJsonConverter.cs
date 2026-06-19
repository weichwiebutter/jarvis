using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HermesPaperBot.Models;

/// <summary>
/// Converts cloud package forbidden capability lists into the paper-only flag model.
/// </summary>
public sealed class ForbiddenCapabilitiesJsonConverter : JsonConverter<ForbiddenCapabilities>
{
    /// <inheritdoc />
    public override ForbiddenCapabilities Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var capability = reader.GetString();
                    if (!string.IsNullOrWhiteSpace(capability))
                    {
                        capabilities.Add(capability);
                    }
                }
            }
        }

        return new ForbiddenCapabilities
        {
            MarketOrderExecutionForbidden = capabilities.Contains("execute_market_order"),
            LimitOrderPlacementForbidden = capabilities.Contains("place_limit_order"),
            StopOrderPlacementForbidden = capabilities.Contains("place_stop_order"),
            PositionModificationForbidden = capabilities.Contains("modify_position") || capabilities.Contains("position_management"),
            PositionClosingForbidden = capabilities.Contains("close_position"),
            PendingOrderCancellationForbidden = capabilities.Contains("cancel_pending_order") || capabilities.Contains("pending_order_management"),
            ExternalNetworkAccessForbidden = capabilities.Contains("external_network_calls") || capabilities.Contains("secrets_access"),
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ForbiddenCapabilities value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue("execute_market_order");
        writer.WriteStringValue("place_limit_order");
        writer.WriteStringValue("place_stop_order");
        writer.WriteStringValue("modify_position");
        writer.WriteStringValue("close_position");
        writer.WriteStringValue("cancel_pending_order");
        writer.WriteStringValue("position_management");
        writer.WriteStringValue("pending_order_management");
        writer.WriteStringValue("account_risk_mutation");
        writer.WriteStringValue("strategy_mutation");
        writer.WriteStringValue("backtesting");
        writer.WriteStringValue("oos_execution");
        writer.WriteStringValue("forward_learning");
        writer.WriteStringValue("release_manifest_mutation");
        writer.WriteStringValue("safety_flag_mutation");
        writer.WriteStringValue("external_network_calls");
        writer.WriteStringValue("secrets_access");
        writer.WriteEndArray();
    }
}
