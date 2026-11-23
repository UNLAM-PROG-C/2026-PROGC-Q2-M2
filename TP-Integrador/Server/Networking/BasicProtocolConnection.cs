using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;
using SpaceShooter.Transport;

namespace SpaceShooter.Server.Networking;

public static class BasicProtocolConnection
{
  public static async Task HandleAsync(Stream stream, PlayerId playerId, CancellationToken cancellationToken)
  {
    ProtocolMessage helloMessage = await ReadMessageAsync(stream, cancellationToken);
    ValidateHello(helloMessage);
    const long pingId = 1;
    await SendGreetingAsync(stream, playerId, pingId, cancellationToken);
    ProtocolMessage pingResponse = await ReadMessageAsync(stream, cancellationToken);
    ValidatePingResponse(pingResponse, pingId);
  }

  private static void ValidateHello(ProtocolMessage message)
  {
    if (message.Payload is not HelloPayload)
    {
      throw new ProtocolException(
          ProtocolError.UnexpectedMessage,
          "Hello must be the first message of a connection.");
    }
  }

  private static async Task SendGreetingAsync(
      Stream stream,
      PlayerId playerId,
      long pingId,
      CancellationToken cancellationToken)
  {
    WelcomePayload welcome = new(playerId, 1, 4, ProtocolVersion.Current);
    await WriteMessageAsync(stream, new ProtocolMessage(1, welcome), cancellationToken);
    PingPayload ping = new(pingId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    await WriteMessageAsync(stream, new ProtocolMessage(2, ping), cancellationToken);
  }

  private static void ValidatePingResponse(ProtocolMessage message, long pingId)
  {
    if (message.Payload is not PingResponsePayload response || response.PingId != pingId)
    {
      throw new ProtocolException(ProtocolError.UnexpectedMessage, "A matching PingResponse was expected.");
    }
  }

  private static async ValueTask<ProtocolMessage> ReadMessageAsync(Stream stream, CancellationToken cancellationToken)
  {
    byte[] payload = await LengthPrefixedFrame.ReadAsync(stream, cancellationToken)
        ?? throw new ProtocolException(
            ProtocolError.IncompleteFrame,
            "The peer disconnected before sending the expected message.");
    return ProtocolSerializer.Deserialize(payload);
  }

  private static ValueTask WriteMessageAsync(
      Stream stream,
      ProtocolMessage message,
      CancellationToken cancellationToken) =>
      LengthPrefixedFrame.WriteAsync(stream, ProtocolSerializer.Serialize(message), cancellationToken);
}
