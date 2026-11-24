using System.Text.Json;
using SpaceShooter.Protocol;

namespace SpaceShooter.Server.Persistence;

public sealed class JsonHighScoreStore(string path, TextWriter? output = null) : IHighScoreStore, IDisposable
{
  private static readonly JsonSerializerOptions Options = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
  };

  private readonly SemaphoreSlim _gate = new(1, 1);
  private readonly string _path = Path.GetFullPath(path);
  private readonly TextWriter _output = output ?? TextWriter.Null;
  private bool _disposed;

  public async ValueTask<ScoreEntry[]> UpdateAsync(
      IReadOnlyCollection<ScoreEntry> scores,
      CancellationToken cancellationToken)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    await _gate.WaitAsync(cancellationToken);
    try
    {
      List<ScoreEntry> records = await LoadCoreAsync(cancellationToken);
      MergeScores(records, scores);
      ScoreEntry[] ranked = InMemoryHighScoreStore.Rank(records);
      await SaveCoreAsync(ranked, cancellationToken);
      return ranked;
    }
    finally
    {
      _gate.Release();
    }
  }

  private static void MergeScores(
      List<ScoreEntry> records,
      IReadOnlyCollection<ScoreEntry> scores)
  {
    foreach (ScoreEntry score in scores)
    {
      MergeScore(records, score);
    }
  }

  private static void MergeScore(List<ScoreEntry> records, ScoreEntry score)
  {
    int index = records.FindIndex(record => string.Equals(
        record.PlayerName,
        score.PlayerName,
        StringComparison.OrdinalIgnoreCase));
    if (index < 0)
    {
      records.Add(score);
    }
    else if (score.Score > records[index].Score)
    {
      records[index] = score;
    }
  }

  public void Dispose()
  {
    _disposed = true;
    _gate.Dispose();
  }

  private async Task<List<ScoreEntry>> LoadCoreAsync(CancellationToken cancellationToken)
  {
    if (!File.Exists(_path))
    {
      return [];
    }

    try
    {
      await using FileStream stream = OpenReadStream();
      return await JsonSerializer.DeserializeAsync<List<ScoreEntry>>(stream, Options, cancellationToken) ?? [];
    }
    catch (JsonException exception)
    {
      await PreserveInvalidFileAsync(exception);
      return [];
    }
  }

  private FileStream OpenReadStream()
  {
    const int BufferSize = 4096;
    return new FileStream(
        _path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        BufferSize,
        FileOptions.Asynchronous | FileOptions.SequentialScan);
  }

  private async Task PreserveInvalidFileAsync(JsonException exception)
  {
    string backupPath = $"{_path}.invalid-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
    File.Copy(_path, backupPath);
    await _output.WriteLineAsync(
        $"Invalid records file preserved at '{backupPath}': {exception.Message}");
  }

  private async Task SaveCoreAsync(ScoreEntry[] records, CancellationToken cancellationToken)
  {
    string directory = Path.GetDirectoryName(_path)!;
    Directory.CreateDirectory(directory);
    string temporaryPath = _path + ".tmp";
    try
    {
      await WriteTemporaryFileAsync(temporaryPath, records, cancellationToken);
      File.Move(temporaryPath, _path, overwrite: true);
    }
    finally
    {
      if (File.Exists(temporaryPath))
      {
        File.Delete(temporaryPath);
      }
    }
  }

  private static async Task WriteTemporaryFileAsync(
      string path,
      ScoreEntry[] records,
      CancellationToken cancellationToken)
  {
    const int BufferSize = 4096;
    await using FileStream stream = new(
        path,
        FileMode.Create,
        FileAccess.Write,
        FileShare.None,
        BufferSize,
        FileOptions.Asynchronous);
    await JsonSerializer.SerializeAsync(stream, records, Options, cancellationToken);
    await stream.FlushAsync(cancellationToken);
  }
}
