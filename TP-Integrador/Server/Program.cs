using SpaceShooter.Server;

using var shutdown = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

using var application = new ServerApplication(Console.Out);
await application.RunAsync(shutdown.Token);
