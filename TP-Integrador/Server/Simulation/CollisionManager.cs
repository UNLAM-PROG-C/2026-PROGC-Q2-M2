namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Enumerates intersecting entity pairs in a stable order.
/// </summary>
internal static class CollisionManager
{
  public static IReadOnlyList<CollisionEvent> Detect(GameState state)
  {
    Entity[] entities = state.GetEntities().ToArray();
    List<CollisionEvent> collisions = [];
    for (int firstIndex = 0; firstIndex < entities.Length; firstIndex++)
    {
      DetectForEntity(entities, firstIndex, collisions);
    }

    return collisions;
  }

  public static IReadOnlyList<CollisionEvent> Detect(
      Entity source,
      IEnumerable<Entity> candidates)
  {
    return candidates
        .OrderBy(candidate => candidate.Key)
        .Where(candidate => CollisionDetector.Intersects(source, candidate))
        .Select(candidate => CreateEvent(source, candidate))
        .ToArray();
  }

  private static void DetectForEntity(
      Entity[] entities,
      int firstIndex,
      List<CollisionEvent> collisions)
  {
    Entity first = entities[firstIndex];
    for (int secondIndex = firstIndex + 1; secondIndex < entities.Length; secondIndex++)
    {
      Entity second = entities[secondIndex];
      if (CollisionDetector.Intersects(first, second))
      {
        collisions.Add(CreateEvent(first, second));
      }
    }
  }

  private static CollisionEvent CreateEvent(Entity first, Entity second)
  {
    return first.Key <= second.Key
        ? new CollisionEvent(first, second)
        : new CollisionEvent(second, first);
  }
}
