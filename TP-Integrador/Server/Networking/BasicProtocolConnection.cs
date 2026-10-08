using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;
using SpaceShooter.Transport;

namespace SpaceShooter.Server.Networking;

public static class BasicProtocolConnection
{
    public static async Task HandleAsync(Stream stream, PlayerId playerId, CancellationToken cancellationToken)
    {
        var helloMessage = await ReadMessageAsync(stream, cancellationToken);
        if (helloMessage.Payload is not HelloPayload)
        {
            throw new ProtocolException(ProtocolError.UnexpectedMessage, "Hello must be the first message of a connection.");
        }

        await WriteMessageAsync(
            stream,
            new ProtocolMessage(1, new WelcomePayload(playerId, MinimumPlayers: 1, MaximumPlayers: 4, ProtocolVersion.Current)),
            cancellationToken);

        const long pingId = 1;
        await WriteMessageAsync(
            stream,
            new ProtocolMessage(2, new PingPayload(pingId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())),
            cancellationToken);

        var pingResponse = await ReadMessageAsync(stream, cancellationToken);
        if (pingResponse.Payload is not PingResponsePayload response || response.PingId != pingId)
        {
            throw new ProtocolException(ProtocolError.UnexpectedMessage, "A matching PingResponse was expected.");
        }
    }

    private static async ValueTask<ProtocolMessage> ReadMessageAsync(Stream stream, CancellationToken cancellationToken)
    {
        var payload = await LengthPrefixedFrame.ReadAsync(stream, cancellationToken)
            ?? throw new ProtocolException(ProtocolError.IncompleteFrame, "The peer disconnected before sending the expected message.");
        return ProtocolSerializer.Deserialize(payload);
    }

    private static ValueTask WriteMessageAsync(
        Stream stream,
        ProtocolMessage message,
        CancellationToken cancellationToken) =>
        LengthPrefixedFrame.WriteAsync(stream, ProtocolSerializer.Serialize(message), cancellationToken);
}
