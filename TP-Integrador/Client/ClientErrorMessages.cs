namespace SpaceShooter.Client;

/// <summary>
/// Maps stable protocol codes to safe, user-facing Spanish messages.
/// </summary>
internal static class ClientErrorMessages
{
  public static string For(string code)
  {
    return code switch
    {
      "serverFull" => "El servidor ya tiene cuatro jugadores conectados.",
      "invalidMatchSize" => "La cantidad de jugadores debe estar entre uno y cuatro.",
      "matchInProgress" => "La partida ya está en curso.",
      "matchSizeMismatch" => "La sala fue creada para otra cantidad de jugadores.",
      "lobbyFull" => "La sala ya está completa.",
      "disconnected" => "El servidor cerró la conexión.",
      "connection" => "Se perdió la conexión con el servidor.",
      _ => "Ocurrió un error de comunicación con el servidor.",
    };
  }
}
