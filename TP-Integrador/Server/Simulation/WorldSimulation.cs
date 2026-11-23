using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Coordinates deterministic simulation systems without duplicating their responsibilities.
/// </summary>
internal sealed class WorldSimulation
{
  private readonly CampaignController _campaign;
  private readonly CombatManager _combat = new();
  private readonly SimulationContext _context;
  private readonly PowerUpDropPolicy _dropPolicy;
  private readonly EnemyAttackManager _enemyAttacks;
  private readonly EnemyManager _enemies;
  private readonly PlayerManager _players;
  private readonly ProjectileManager _projectiles;
  private readonly IRandomSource _random;

  public WorldSimulation(SimulationConfig config, IRandomSource random)
  {
    config.Validate();
    _random = random;
    _context = new SimulationContext(config, new EntityFactory(), State);
    LevelManager levels = new(config.Levels);
    _projectiles = new ProjectileManager(_context);
    _players = new PlayerManager(_context, _combat, _projectiles);
    _enemies = new EnemyManager(config, _context.Factory, State);
    _enemyAttacks = new EnemyAttackManager(_context, levels, _projectiles);
    _dropPolicy = new PowerUpDropPolicy(config, random, levels);
    _campaign = new CampaignController(_context, levels, SpawnEnemy);
  }

  public GameState State { get; } = new();

  public Player AddPlayer(Vector2D position)
  {
    return AddPlayer(_context.Factory.CreatePlayerId(), position);
  }

  public Player AddPlayer(PlayerId playerId, Vector2D position)
  {
    return _players.Add(playerId, position);
  }

  public bool RemovePlayer(PlayerId playerId)
  {
    return _players.Remove(playerId);
  }

  public Enemy AddEnemy(
      EnemyKind kind,
      Vector2D position,
      int? health = null,
      float? speed = null)
  {
    SetInitialBossPhase(kind);
    Vector2D spawn = _enemies.ClampSpawn(kind, position);
    return _enemies.Create(
        kind,
        spawn,
        health ?? HealthFor(kind),
        speed ?? SpeedFor(kind),
        AttackIntervalFor(kind));
  }

  public Enemy AddRandomEnemy(EnemyKind kind)
  {
    float margin = HorizontalMargin(kind);
    int minimumX = (int)MathF.Ceiling(_context.Config.WorldBounds.MinX + margin);
    int maximumX = (int)MathF.Floor(_context.Config.WorldBounds.MaxX - margin) + 1;
    int x = _random.NextInt(minimumX, maximumX);
    return AddEnemy(kind, _enemies.CreateTopSpawn(kind, x));
  }

  public PowerUp AddPowerUp(PowerUpType kind, Vector2D position)
  {
    SimulationConfig config = _context.Config;
    Vector2D spawn = config.WorldBounds.Clamp(position, config.PowerUpRadius);
    Hitbox hitbox = new(Vector2D.Zero, new CircleShape(config.PowerUpRadius));
    PowerUp powerUp = _context.Factory.CreatePowerUp(kind, spawn, config.PowerUpFallSpeed, hitbox);
    State.AddPowerUp(powerUp);
    return powerUp;
  }

  public void StartCampaign()
  {
    _campaign.Start();
  }

  public void Step(IReadOnlyDictionary<PlayerId, PlayerInput> inputs)
  {
    if (IsEnded() || AdvanceTransition())
    {
      return;
    }

    RunActiveTick(inputs);
    State.Tick++;
    UpdateDefeatStatus();
  }

  public void DamagePlayer(PlayerId playerId, int damage)
  {
    ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(damage, 0);
    if (State.Players.TryGetValue(playerId, out Player? player))
    {
      _players.Damage(player, damage);
    }
  }

  public void DamageEnemy(EnemyId enemyId, PlayerId ownerId, int damage)
  {
    ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(damage, 0);
    ValidateOwner(ownerId);
    if (State.Enemies.TryGetValue(enemyId, out Enemy? enemy) && IsActive(enemy))
    {
      ApplyEnemyDamage(enemy, ownerId, damage);
    }
  }

  private bool IsEnded()
  {
    return State.MatchStatus is MatchStatus.Won or MatchStatus.Lost;
  }

  private bool AdvanceTransition()
  {
    if (!_campaign.AdvanceTransition())
    {
      return false;
    }

    State.Tick++;
    return true;
  }

  private void RunActiveTick(IReadOnlyDictionary<PlayerId, PlayerInput> inputs)
  {
    _combat.BeginTick();
    float elapsedSeconds = (float)_context.Config.FixedStep.TotalSeconds;
    _players.Update(inputs, elapsedSeconds);
    _projectiles.Update(elapsedSeconds, ResolveProjectileCollision);
    UpdatePowerUps(elapsedSeconds);
    ResolvePowerUpCollections();
    UpdateEnemies(elapsedSeconds);
    ResolveEnemyPlayerCollisions();
    _campaign.AdvanceOrComplete();
  }

  private void UpdatePowerUps(float elapsedSeconds)
  {
    IEnumerable<PowerUp> available = State.PowerUps.Values
        .Where(powerUp => powerUp.Status == PowerUpStatus.Available);
    foreach (PowerUp powerUp in available)
    {
      AdvancePowerUp(powerUp, elapsedSeconds);
    }
  }

  private void AdvancePowerUp(PowerUp powerUp, float elapsedSeconds)
  {
    powerUp.Position += new Vector2D(0, _context.Config.PowerUpFallSpeed * elapsedSeconds);
    if (powerUp.Position.Y > _context.Config.WorldBounds.MaxY)
    {
      powerUp.Status = PowerUpStatus.Collected;
    }
  }

  private void ResolvePowerUpCollections()
  {
    foreach (PowerUp powerUp in AvailablePowerUps())
    {
      TryCollect(powerUp);
    }

    RemoveCollectedPowerUps();
  }

  private IEnumerable<PowerUp> AvailablePowerUps()
  {
    return State.PowerUps.Values
        .Where(powerUp => powerUp.Status == PowerUpStatus.Available)
        .OrderBy(powerUp => powerUp.Id.Value);
  }

  private void TryCollect(PowerUp powerUp)
  {
    Player? player = FindCollisionTarget<Player>(
        powerUp,
        _players.ActivePlayers(),
        CollisionType.Pickup);
    if (player is null)
    {
      return;
    }

    powerUp.Status = PowerUpStatus.Collected;
    int duration = _context.DurationInTicks(_context.Config.PowerUpDuration);
    PowerUpManager.Collect(player, powerUp.Kind, duration, _context.Config.SpeedBoostMultiplier);
  }

  private void RemoveCollectedPowerUps()
  {
    PowerUpId[] ids = State.PowerUps
        .Where(pair => pair.Value.Status == PowerUpStatus.Collected)
        .Select(pair => pair.Key)
        .ToArray();
    foreach (PowerUpId id in ids)
    {
      State.RemovePowerUp(id);
    }
  }

  private void UpdateEnemies(float elapsedSeconds)
  {
    IEnumerable<Enemy> activeEnemies = State.Enemies.Values
        .Where(IsActive)
        .OrderBy(enemy => enemy.Id.Value);
    foreach (Enemy enemy in activeEnemies)
    {
      UpdateEnemy(enemy, elapsedSeconds);
    }
  }

  private void UpdateEnemy(Enemy enemy, float elapsedSeconds)
  {
    float multiplier = enemy.Kind == EnemyKind.Boss
        ? _enemyAttacks.BossSpeedMultiplier()
        : 1f;
    bool reachedBottom = _enemies.UpdateMovement(enemy, elapsedSeconds, multiplier);
    _enemyAttacks.Update(enemy);
    if (reachedBottom)
    {
      DamageFirstPlayer();
      _enemies.ResetAtTop(enemy);
    }
  }

  private void DamageFirstPlayer()
  {
    Player? target = State.Players.Values
        .Where(player => player.Status != PlayerStatus.Eliminated)
        .OrderBy(player => player.Id.Value)
        .FirstOrDefault();
    if (target is not null)
    {
      _players.Damage(target, _context.Config.EnemyCollisionDamage);
    }
  }

  private void ResolveEnemyPlayerCollisions()
  {
    IEnumerable<Enemy> enemies = State.Enemies.Values
        .Where(enemy => IsActive(enemy) && enemy.Kind != EnemyKind.Boss)
        .OrderBy(enemy => enemy.Id.Value);
    foreach (Enemy enemy in enemies)
    {
      ResolveEnemyPlayerCollision(enemy);
    }
  }

  private void ResolveEnemyPlayerCollision(Enemy enemy)
  {
    IEnumerable<Player> players = State.Players.Values
        .Where(player => player.Status == PlayerStatus.Active);
    Player? player = FindCollisionTarget<Player>(enemy, players, CollisionType.Damage);
    if (player is null)
    {
      return;
    }

    DestroyEnemy(enemy);
    _players.Damage(player, _context.Config.EnemyCollisionDamage);
  }

  private void ResolveProjectileCollision(Projectile projectile)
  {
    if (projectile.Key.EntityType == EntityType.AlliedProjectile)
    {
      ResolveAlliedProjectile(projectile);
      return;
    }

    ResolveEnemyProjectile(projectile);
  }

  private void ResolveAlliedProjectile(Projectile projectile)
  {
    IEnumerable<Enemy> enemies = State.Enemies.Values.Where(IsActive);
    Enemy? enemy = FindCollisionTarget<Enemy>(projectile, enemies, CollisionType.Damage);
    if (enemy is not null)
    {
      projectile.Status = ProjectileStatus.Consumed;
      ApplyEnemyDamage(enemy, projectile.OwnerId, projectile.Damage);
    }
  }

  private void ResolveEnemyProjectile(Projectile projectile)
  {
    Player? player = FindCollisionTarget<Player>(
        projectile,
        _players.ActivePlayers(),
        CollisionType.Damage);
    if (player is not null)
    {
      projectile.Status = ProjectileStatus.Consumed;
      _players.Damage(player, projectile.Damage);
    }
  }

  private void ApplyEnemyDamage(Enemy enemy, PlayerId ownerId, int damage)
  {
    if (!_combat.ApplyDamage(enemy, damage))
    {
      _enemyAttacks.UpdateBossPhase(enemy);
      return;
    }

    DestroyEnemy(enemy);
    AwardKill(ownerId, enemy.Kind);
    TryDropPowerUp(enemy);
  }

  private static void DestroyEnemy(Enemy enemy)
  {
    enemy.Status = EnemyStatus.Destroyed;
    EnemyStateMachine.Destroy(enemy);
  }

  private void AwardKill(PlayerId ownerId, EnemyKind kind)
  {
    int score = kind == EnemyKind.Boss
        ? _context.Config.BossKillScore
        : _context.Config.EnemyKillScore;
    State.Players[ownerId].Score += score;
  }

  private void TryDropPowerUp(Enemy enemy)
  {
    if (enemy.Kind != EnemyKind.Boss && _dropPolicy.TryChoose(State.Wave, out PowerUpType type))
    {
      AddPowerUp(type, enemy.Position);
    }
  }

  private void SpawnEnemy(EnemySpawn spawn)
  {
    AddEnemy(spawn.Kind, spawn.Position, spawn.Health, spawn.Speed);
  }

  private void SetInitialBossPhase(EnemyKind kind)
  {
    if (kind == EnemyKind.Boss)
    {
      State.BossPhase = 1;
    }
  }

  private void ValidateOwner(PlayerId ownerId)
  {
    if (!State.Players.ContainsKey(ownerId))
    {
      throw new ArgumentException("Projectile owner must belong to the world.", nameof(ownerId));
    }
  }

  private void UpdateDefeatStatus()
  {
    if (State.Players.Count > 0 &&
        State.Players.Values.All(player => player.Status == PlayerStatus.Eliminated))
    {
      State.MatchStatus = MatchStatus.Lost;
    }
  }

  private float HorizontalMargin(EnemyKind kind)
  {
    return kind == EnemyKind.Boss
        ? _context.Config.BossHitboxWidth / 2f
        : _context.Config.EnemyRadius;
  }

  private int HealthFor(EnemyKind kind)
  {
    return kind switch
    {
      EnemyKind.Diver => _context.Config.DiverHealth,
      EnemyKind.Tank => _context.Config.TankHealth,
      EnemyKind.Boss => _context.Config.BossHealth,
      _ => _context.Config.EnemyHealth,
    };
  }

  private float SpeedFor(EnemyKind kind)
  {
    return kind switch
    {
      EnemyKind.Scout => _context.Config.ScoutSpeed,
      EnemyKind.Diver => _context.Config.DiverSpeed,
      EnemyKind.Tank => _context.Config.TankSpeed,
      EnemyKind.Boss => _context.Config.BossSpeed,
      _ => 0f,
    };
  }

  private TimeSpan? AttackIntervalFor(EnemyKind kind)
  {
    return kind is EnemyKind.Tank or EnemyKind.Boss
        ? _enemyAttacks.AttackInterval(kind)
        : null;
  }

  private static bool IsActive(Enemy enemy)
  {
    return enemy.Status == EnemyStatus.Active;
  }

  private static TEntity? FindCollisionTarget<TEntity>(
      Entity source,
      IEnumerable<TEntity> candidates,
      CollisionType collisionType)
      where TEntity : Entity
  {
    return CollisionManager.Detect(source, candidates)
        .Select(CollisionResolver.Resolve)
        .Where(resolution => resolution?.Type == collisionType)
        .Select(resolution => resolution!.Target)
        .OfType<TEntity>()
        .FirstOrDefault();
  }
}
