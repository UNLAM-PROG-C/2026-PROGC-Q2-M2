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
            var records = await LoadCoreAsync(cancellationToken);
            foreach (var score in scores)
            {
                var index = records.FindIndex(record =>
                    string.Equals(record.PlayerName, score.PlayerName, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    records.Add(score);
                }
                else if (score.Score > records[index].Score)
                {
                    records[index] = score;
                }
            }

            var ranked = InMemoryHighScoreStore.Rank(records);
            await SaveCoreAsync(ranked, cancellationToken);
            return ranked;
        }
        finally
        {
            _gate.Release();
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
            await using var stream = new FileStream(
                _path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await JsonSerializer.DeserializeAsync<List<ScoreEntry>>(stream, Options, cancellationToken) ?? [];
        }
        catch (JsonException exception)
        {
            var backupPath = $"{_path}.invalid-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
            File.Copy(_path, backupPath);
            await _output.WriteLineAsync($"Invalid records file preserved at '{backupPath}': {exception.Message}");
            return [];
        }
    }

    private async Task SaveCoreAsync(ScoreEntry[] records, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _path + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, records, Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

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
}
