using System.Net;
using SpaceShooter.Server.Networking;
using SpaceShooter.Server.Persistence;

namespace SpaceShooter.Server;

public sealed class ServerApplication : IDisposable
{
    private readonly TextWriter _output;
    private readonly JsonHighScoreStore _highScores;
    private readonly LobbyServer _protocolServer;

    public ServerApplication(TextWriter output, int port = 7777, string recordsPath = "records.json")
    {
        _output = output;
        _highScores = new JsonHighScoreStore(recordsPath, output);
        _protocolServer = new LobbyServer(IPAddress.Any, port, output, highScores: _highScores);
    }

    public void Dispose()
    {
        _protocolServer.Dispose();
        _highScores.Dispose();
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await _output.WriteLineAsync("Space Shooter LAN server started.");

        try
        {
            await _protocolServer.RunAsync(cancellationToken);
        }
        finally
        {
            await _output.WriteLineAsync("Space Shooter LAN server stopped.");
        }
    }
}
