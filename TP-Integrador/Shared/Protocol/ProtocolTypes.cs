namespace SpaceShooter.Protocol;

internal static class ProtocolTypes
{
  private static readonly Dictionary<string, Type> TypesByName =
      new Dictionary<string, Type>(StringComparer.Ordinal)
      {
        ["hello"] = typeof(HelloPayload),
        ["selectShip"] = typeof(SelectShipPayload),
        ["setReady"] = typeof(SetReadyPayload),
        ["setPause"] = typeof(SetPausePayload),
        ["inputState"] = typeof(InputStatePayload),
        ["pingResponse"] = typeof(PingResponsePayload),
        ["leave"] = typeof(LeavePayload),
        ["welcome"] = typeof(WelcomePayload),
        ["lobbyState"] = typeof(LobbyStatePayload),
        ["matchStarted"] = typeof(MatchStartedPayload),
        ["pauseState"] = typeof(PauseStatePayload),
        ["worldSnapshot"] = typeof(WorldSnapshotPayload),
        ["gameEvent"] = typeof(GameEventPayload),
        ["matchEnded"] = typeof(MatchEndedPayload),
        ["ping"] = typeof(PingPayload),
        ["error"] = typeof(ErrorPayload),
        ["serverShutdown"] = typeof(ServerShutdownPayload),
      };

  private static readonly Dictionary<Type, string> NamesByType =
      TypesByName.ToDictionary(pair => pair.Value, pair => pair.Key);

  public static Type GetPayloadType(string name) =>
      TypesByName.TryGetValue(name, out Type? type)
          ? type
          : throw new ProtocolException(ProtocolError.UnknownMessageType, $"Unknown message type '{name}'.");

  public static string GetName(Type type) =>
      NamesByType.TryGetValue(type, out string? name)
          ? name
          : throw new ArgumentException($"Payload type '{type.Name}' is not registered.", nameof(type));
}
