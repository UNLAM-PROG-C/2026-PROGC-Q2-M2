using SpaceShooter.Server;

namespace SpaceShooter.Server.Tests;

public sealed class ServerApplicationTests
{
  [Fact]
  public async Task RunAsyncStopsPromptlyWhenCancellationIsRequested()
  {
    using CancellationTokenSource cancellation = new CancellationTokenSource();
    using ServerApplication application = new ServerApplication(TextWriter.Null, port: 0);

    Task runTask = application.RunAsync(cancellation.Token);
    await Task.Yield();

    Assert.False(runTask.IsCompleted);

    cancellation.Cancel();

    await runTask.WaitAsync(TimeSpan.FromSeconds(1));
  }
}
