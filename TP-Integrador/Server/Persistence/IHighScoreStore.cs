using SpaceShooter.Protocol;

namespace SpaceShooter.Server.Persistence;

public interface IHighScoreStore
{
    ValueTask<ScoreEntry[]> UpdateAsync(
        IReadOnlyCollection<ScoreEntry> scores,
        CancellationToken cancellationToken);
}

public sealed class InMemoryHighScoreStore : IHighScoreStore
{
    private readonly Dictionary<string, ScoreEntry> _records = new(StringComparer.OrdinalIgnoreCase);

    public ValueTask<ScoreEntry[]> UpdateAsync(
        IReadOnlyCollection<ScoreEntry> scores,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var score in scores)
        {
            if (!_records.TryGetValue(score.PlayerName, out var current) || score.Score > current.Score)
            {
                _records[score.PlayerName] = score;
            }
        }

        return ValueTask.FromResult(Rank(_records.Values));
    }

    internal static ScoreEntry[] Rank(IEnumerable<ScoreEntry> scores) =>
        scores.OrderByDescending(score => score.Score)
            .ThenBy(score => score.PlayerName, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
}
