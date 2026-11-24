using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Creates and advances projectiles while preserving stable processing order.
/// </summary>
internal sealed class ProjectileManager(SimulationContext context)
{
  public void AddAllied(Player player, Vector2D velocity)
  {
    Add(player.Key, EntityType.AlliedProjectile, player.Position, velocity, context.Config.ProjectileDamage);
  }

  public void AddEnemy(Enemy enemy, Player player, int damage)
  {
    Vector2D direction = (player.Position - enemy.Position).NormalizedOrZero();
    Vector2D velocity = direction * context.Config.ProjectileSpeed;
    Add(enemy.Key, EntityType.EnemyProjectile, enemy.Position, velocity, damage);
  }

  public void Update(float elapsedSeconds, Action<Projectile> resolveCollision)
  {
    foreach (Projectile projectile in ActiveProjectiles())
    {
      Advance(projectile, elapsedSeconds, resolveCollision);
    }

    RemoveConsumedProjectiles();
  }

  private void Add(
      EntityKey owner,
      EntityType type,
      Vector2D position,
      Vector2D velocity,
      int damage)
  {
    Hitbox hitbox = new(Vector2D.Zero, new CircleShape(context.Config.ProjectileRadius));
    Projectile projectile = context.Factory.CreateProjectile(owner, type, position, velocity, hitbox, damage);
    context.State.AddProjectile(projectile);
  }

  private void Advance(
      Projectile projectile,
      float elapsedSeconds,
      Action<Projectile> resolveCollision)
  {
    projectile.Position += projectile.Velocity * elapsedSeconds;
    if (!context.Config.WorldBounds.Contains(projectile.Position))
    {
      projectile.Status = ProjectileStatus.Consumed;
      return;
    }

    resolveCollision(projectile);
  }

  private IEnumerable<Projectile> ActiveProjectiles()
  {
    return context.State.Projectiles.Values
        .Where(projectile => projectile.Status == ProjectileStatus.Active)
        .OrderBy(projectile => projectile.Id.Value);
  }

  private void RemoveConsumedProjectiles()
  {
    ProjectileId[] ids = context.State.Projectiles
        .Where(pair => pair.Value.Status == ProjectileStatus.Consumed)
        .Select(pair => pair.Key)
        .ToArray();
    foreach (ProjectileId id in ids)
    {
      context.State.RemoveProjectile(id);
    }
  }
}
