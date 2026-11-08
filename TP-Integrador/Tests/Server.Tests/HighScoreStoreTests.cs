using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;
using SpaceShooter.Server.Persistence;

namespace SpaceShooter.Server.Tests;

public sealed class HighScoreStoreTests
{
  [Fact]
  public async Task MissingFileIsCreatedAndRecordsSurviveStoreRestart()
  {
    string directory = Path.Combine(Path.GetTempPath(), $"space-shooter-records-{Guid.NewGuid():N}");
    string path = Path.Combine(directory, "records.json");
    try
    {
      using (JsonHighScoreStore firstStore = new JsonHighScoreStore(path))
      {
        ScoreEntry[] firstRecords = await firstStore.UpdateAsync(
            [new ScoreEntry(new PlayerId(1), "Nova", 500)],
            CancellationToken.None);
        Assert.Equal(500, Assert.Single(firstRecords).Score);
      }

      Assert.True(File.Exists(path));
      using JsonHighScoreStore secondStore = new JsonHighScoreStore(path);
      ScoreEntry[] reloaded = await secondStore.UpdateAsync(
          [
              new ScoreEntry(new PlayerId(2), "Nova", 100),
                    new ScoreEntry(new PlayerId(3), "Orion", 300),
                ],
          CancellationToken.None);

      Assert.Collection(
          reloaded,
          score =>
          {
            Assert.Equal("Nova", score.PlayerName);
            Assert.Equal(500, score.Score);
          },
          score =>
          {
            Assert.Equal("Orion", score.PlayerName);
            Assert.Equal(300, score.Score);
          });
    }
    finally
    {
      if (Directory.Exists(directory))
      {
        Directory.Delete(directory, recursive: true);
      }
    }
  }

  [Fact]
  public async Task InvalidFileIsPreservedBeforeStartingFreshTable()
  {
    string directory = Path.Combine(Path.GetTempPath(), $"space-shooter-invalid-records-{Guid.NewGuid():N}");
    string path = Path.Combine(directory, "records.json");
    Directory.CreateDirectory(directory);
    await File.WriteAllTextAsync(path, "not-json");
    try
    {
      using JsonHighScoreStore store = new JsonHighScoreStore(path, TextWriter.Null);

      ScoreEntry[] records = await store.UpdateAsync(
          [new ScoreEntry(new PlayerId(1), "Nova", 200)],
          CancellationToken.None);

      Assert.Equal(200, Assert.Single(records).Score);
      Assert.Single(Directory.GetFiles(directory, "records.json.invalid-*"));
    }
    finally
    {
      Directory.Delete(directory, recursive: true);
    }
  }
}
