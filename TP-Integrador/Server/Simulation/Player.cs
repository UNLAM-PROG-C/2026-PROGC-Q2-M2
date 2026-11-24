using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Represents a player-controlled entity and its authoritative gameplay state.
/// </summary>
internal sealed class Player : Entity, IDamageable, IShooter
{
  public Player(PlayerId id, Vector2D position, float speed, Hitbox hitbox, int lives, int health)
      : base(new EntityKey(EntityType.Player, id.Value), position, speed, hitbox)
  {
    Id = id;
    SpawnPosition = position;
    Lives = lives;
    Health = health;
  }

  public Dictionary<PowerUpType, PowerUpEffect> Effects { get; } = [];

  public int FireCooldownTicks { get; set; }

  public float FireCooldownScale { get; set; } = 1f;

  public int Health { get; set; }

  public PlayerId Id { get; }

  public int Lives { get; set; }

  public int ProjectileCount { get; set; } = 1;

  public int Score { get; set; }

  public bool ShieldActive { get; set; }

  public int InvulnerabilityTicks { get; set; }

  public int RespawnTicks { get; set; }

  public bool Shield => ShieldActive;

  public Vector2D SpawnPosition { get; }

  public PlayerStatus Status { get; set; } = PlayerStatus.Active;
}
