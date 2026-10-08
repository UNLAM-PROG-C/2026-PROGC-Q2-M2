using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceShooter.Protocol;

public static class ProtocolSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static byte[] Serialize(ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", ProtocolVersion.Current);
            writer.WriteString("type", message.Type);
            writer.WriteNumber("sequence", message.Sequence);
            writer.WritePropertyName("payload");
            JsonSerializer.Serialize(writer, message.Payload, message.Payload.GetType(), SerializerOptions);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public static ProtocolMessage Deserialize(ReadOnlyMemory<byte> utf8Json)
    {
        try
        {
            using var document = JsonDocument.Parse(utf8Json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ProtocolException(ProtocolError.InvalidJson, "The protocol envelope must be a JSON object.");
            }

            var version = GetRequiredInt32(root, "version");
            if (version != ProtocolVersion.Current)
            {
                throw new ProtocolException(
                    ProtocolError.IncompatibleVersion,
                    $"Protocol version {version} is incompatible with version {ProtocolVersion.Current}.");
            }

            var typeName = GetRequiredString(root, "type");
            var sequence = GetRequiredInt64(root, "sequence");
            if (sequence < 0)
            {
                throw new ProtocolException(ProtocolError.InvalidSequence, "Sequence cannot be negative.");
            }

            if (!root.TryGetProperty("payload", out var payloadElement) || payloadElement.ValueKind != JsonValueKind.Object)
            {
                throw new ProtocolException(ProtocolError.MissingField, "Required object field 'payload' is missing.");
            }

            var payloadType = ProtocolTypes.GetPayloadType(typeName);
            var payload = payloadElement.Deserialize(payloadType, SerializerOptions) as IProtocolPayload
                ?? throw new ProtocolException(ProtocolError.InvalidPayload, $"Payload for '{typeName}' is invalid.");

            ValidatePayload(payload);
            return new ProtocolMessage(sequence, payload);
        }
        catch (ProtocolException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new ProtocolException(ProtocolError.InvalidJson, "The message contains invalid JSON or an invalid payload.", exception);
        }
    }

    private static int GetRequiredInt32(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || !property.TryGetInt32(out var value))
        {
            throw new ProtocolException(ProtocolError.MissingField, $"Required integer field '{propertyName}' is missing.");
        }

        return value;
    }

    private static long GetRequiredInt64(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || !property.TryGetInt64(out var value))
        {
            throw new ProtocolException(ProtocolError.MissingField, $"Required integer field '{propertyName}' is missing.");
        }

        return value;
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new ProtocolException(ProtocolError.MissingField, $"Required string field '{propertyName}' is missing.");
        }

        return property.GetString()!;
    }

    private static void ValidatePayload(IProtocolPayload payload)
    {
        switch (payload)
        {
            case HelloPayload hello when string.IsNullOrWhiteSpace(hello.ClientVersion) ||
                                         string.IsNullOrWhiteSpace(hello.PlayerName) ||
                                         hello.PlayerName.Length > 32 ||
                                         hello.RequestedPlayers is < 1 or > 4:
                throw new ProtocolException(
                    ProtocolError.InvalidPayload,
                    "Hello requires a client version, a player name of 1 to 32 characters, and a requested player count from 1 to 4.");
            case SelectShipPayload ship when string.IsNullOrWhiteSpace(ship.ShipId):
                throw new ProtocolException(ProtocolError.InvalidPayload, "Ship identifier is required.");
            case InputStatePayload input when !float.IsFinite(input.Horizontal) ||
                                              !float.IsFinite(input.Vertical) ||
                                              input.Horizontal is < -1 or > 1 ||
                                              input.Vertical is < -1 or > 1 ||
                                              input.LocalTick < 0:
                throw new ProtocolException(ProtocolError.InvalidPayload, "Input values are outside their valid range.");
            case PingResponsePayload response when response.PingId < 0:
            case PingPayload ping when ping.PingId < 0:
                throw new ProtocolException(ProtocolError.InvalidPayload, "Ping identifier cannot be negative.");
        }
    }
}
