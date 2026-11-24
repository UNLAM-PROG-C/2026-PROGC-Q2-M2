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

  /// <summary>Serializes one validated protocol envelope to UTF-8 JSON.</summary>
  /// <param name="message">The envelope to serialize.</param>
  /// <returns>The serialized UTF-8 payload.</returns>
  public static byte[] Serialize(ProtocolMessage message)
  {
    ArgumentNullException.ThrowIfNull(message);

    using MemoryStream stream = new MemoryStream();
    using (Utf8JsonWriter writer = new Utf8JsonWriter(stream))
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

  /// <summary>Deserializes and validates one UTF-8 protocol envelope.</summary>
  /// <param name="utf8Json">The complete JSON frame payload.</param>
  /// <returns>The validated protocol message.</returns>
  /// <exception cref="ProtocolException">The envelope or payload violates the protocol.</exception>
  public static ProtocolMessage Deserialize(ReadOnlyMemory<byte> utf8Json)
  {
    try
    {
      return DeserializeCore(utf8Json);
    }
    catch (ProtocolException)
    {
      throw;
    }
    catch (JsonException exception)
    {
      throw new ProtocolException(
          ProtocolError.InvalidJson,
          "The message contains invalid JSON or an invalid payload.",
          exception);
    }
  }

  private static ProtocolMessage DeserializeCore(ReadOnlyMemory<byte> utf8Json)
  {
    using JsonDocument document = JsonDocument.Parse(utf8Json);
    JsonElement root = document.RootElement;
    ValidateRoot(root);
    ValidateVersion(GetRequiredInt32(root, "version"));
    string typeName = GetRequiredString(root, "type");
    long sequence = GetRequiredInt64(root, "sequence");
    ValidateSequence(sequence);
    IProtocolPayload payload = DeserializePayload(root, typeName);
    ValidatePayload(payload);
    return new ProtocolMessage(sequence, payload);
  }

  private static void ValidateRoot(JsonElement root)
  {
    if (root.ValueKind != JsonValueKind.Object)
    {
      throw new ProtocolException(ProtocolError.InvalidJson, "The protocol envelope must be a JSON object.");
    }
  }

  private static void ValidateVersion(int version)
  {
    if (version != ProtocolVersion.Current)
    {
      throw new ProtocolException(
          ProtocolError.IncompatibleVersion,
          $"Protocol version {version} is incompatible with version {ProtocolVersion.Current}.");
    }
  }

  private static void ValidateSequence(long sequence)
  {
    if (sequence < 0)
    {
      throw new ProtocolException(ProtocolError.InvalidSequence, "Sequence cannot be negative.");
    }
  }

  private static IProtocolPayload DeserializePayload(JsonElement root, string typeName)
  {
    if (!root.TryGetProperty("payload", out JsonElement element) ||
        element.ValueKind != JsonValueKind.Object)
    {
      throw new ProtocolException(ProtocolError.MissingField, "Required object field 'payload' is missing.");
    }

    Type payloadType = ProtocolTypes.GetPayloadType(typeName);
    return element.Deserialize(payloadType, SerializerOptions) as IProtocolPayload
        ?? throw new ProtocolException(ProtocolError.InvalidPayload, $"Payload for '{typeName}' is invalid.");
  }

  private static int GetRequiredInt32(JsonElement root, string propertyName)
  {
    if (!root.TryGetProperty(propertyName, out JsonElement property) || !property.TryGetInt32(out int value))
    {
      throw new ProtocolException(ProtocolError.MissingField, $"Required integer field '{propertyName}' is missing.");
    }

    return value;
  }

  private static long GetRequiredInt64(JsonElement root, string propertyName)
  {
    if (!root.TryGetProperty(propertyName, out JsonElement property) || !property.TryGetInt64(out long value))
    {
      throw new ProtocolException(ProtocolError.MissingField, $"Required integer field '{propertyName}' is missing.");
    }

    return value;
  }

  private static string GetRequiredString(JsonElement root, string propertyName)
  {
    if (!root.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
    {
      throw new ProtocolException(ProtocolError.MissingField, $"Required string field '{propertyName}' is missing.");
    }

    return property.GetString()!;
  }

  private static void ValidatePayload(IProtocolPayload payload)
  {
    switch (payload)
    {
      case HelloPayload hello:
        ValidateHello(hello);
        break;
      case SelectShipPayload ship:
        ValidateShip(ship);
        break;
      case InputStatePayload input:
        ValidateInput(input);
        break;
      case PauseStatePayload pause:
        ValidatePauseState(pause);
        break;
      case PingResponsePayload response when response.PingId < 0:
      case PingPayload ping when ping.PingId < 0:
        throw new ProtocolException(ProtocolError.InvalidPayload, "Ping identifier cannot be negative.");
    }
  }

  private static void ValidateHello(HelloPayload hello)
  {
    bool invalid = string.IsNullOrWhiteSpace(hello.ClientVersion) ||
        string.IsNullOrWhiteSpace(hello.PlayerName) || hello.PlayerName.Length > 32 ||
        hello.RequestedPlayers is < 1 or > 4;
    if (invalid)
    {
      throw new ProtocolException(
          ProtocolError.InvalidPayload,
          "Hello requires a version, a name of 1 to 32 characters, and 1 to 4 players.");
    }
  }

  private static void ValidateShip(SelectShipPayload ship)
  {
    if (string.IsNullOrWhiteSpace(ship.ShipId))
    {
      throw new ProtocolException(ProtocolError.InvalidPayload, "Ship identifier is required.");
    }
  }

  private static void ValidateInput(InputStatePayload input)
  {
    bool invalid = !float.IsFinite(input.Horizontal) || !float.IsFinite(input.Vertical) ||
        input.Horizontal is < -1 or > 1 || input.Vertical is < -1 or > 1 || input.LocalTick < 0;
    if (invalid)
    {
      throw new ProtocolException(ProtocolError.InvalidPayload, "Input values are outside their valid range.");
    }
  }

  private static void ValidatePauseState(PauseStatePayload pause)
  {
    bool ownerMismatch = pause.Paused != (pause.PausedBy is not null);
    if (ownerMismatch || pause.ServerTick < 0)
    {
      throw new ProtocolException(
          ProtocolError.InvalidPayload,
          "Pause state requires a non-negative tick and an owner only while paused.");
    }
  }
}
