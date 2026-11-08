using SpaceShooter.Server;

using CancellationTokenSource shutdown = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
  eventArgs.Cancel = true;
  shutdown.Cancel();
};

using ServerApplication application = new ServerApplication(Console.Out);
await application.RunAsync(shutdown.Token);
