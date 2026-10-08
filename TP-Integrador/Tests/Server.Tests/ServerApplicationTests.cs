using SpaceShooter.Server;

namespace SpaceShooter.Server.Tests;

public sealed class ServerApplicationTests
{
    [Fact]
    public async Task RunAsyncStopsPromptlyWhenCancellationIsRequested()
    {
        using var cancellation = new CancellationTokenSource();
        using var application = new ServerApplication(TextWriter.Null, port: 0);

        var runTask = application.RunAsync(cancellation.Token);
        await Task.Yield();

        Assert.False(runTask.IsCompleted);

        cancellation.Cancel();

        await runTask.WaitAsync(TimeSpan.FromSeconds(1));
    }
}
