using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

internal sealed class WorldSimulation
{
  private readonly SimulationConfig _config;
  private readonly CombatManager _combatManager = new();
  private readonly EntityFactory _entityFactory = new();
  private readonly EnemyManager _enemyManager;
  private readonly LevelManager _levelManager;
  private readonly IRandomSource _random;
  private int _destroyedEnemyCount;

  public WorldSimulation(SimulationConfig config, IRandomSource random)
  {
    config.Validate();
    _config = config;
    _random = random;
    _levelManager = new LevelManager(config.Levels);
    _enemyManager = new EnemyManager(config, _entityFactory, State);
  }

  public GameState State { get; } = new();

  public Player AddPlayer(Vector2D position) => AddPlayer(_entityFactory.CreatePlayerId(), position);

  public Player AddPlayer(PlayerId playerId, Vector2D position)
  {
    if (playerId.Value <= 0 || State.Players.ContainsKey(playerId))
    {
      throw new ArgumentException("Player identifier must be positive and unique.", nameof(playerId));
    }

    Player player = _entityFactory.CreatePlayer(
        playerId,
        _config.PlayerBounds.Clamp(position, _config.PlayerRadius),
        _config.PlayerSpeed,
        CreateCircleHitbox(_config.PlayerRadius),
        _config.PlayerStartingLives,
        _config.PlayerMaxHealth);
    State.AddPlayer(player);
    State.MatchStatus = MatchStatus.Running;
    return player;
  }

  public bool RemovePlayer(PlayerId playerId)
  {
    bool removed = State.RemovePlayer(playerId);
    if (removed && State.Players.Count == 0)
    {
      State.MatchStatus = MatchStatus.Lost;
    }

    return removed;
  }

  public Enemy AddEnemy(
      EnemyKind kind,
      Vector2D position,
      int? health = null,
      float? speed = null)
  {
    if (kind == EnemyKind.Boss)
    {
      State.BossPhase = 1;
    }

    Enemy enemy = _enemyManager.Create(
        kind,
        _config.WorldBounds.Clamp(position, _config.EnemyRadius),
        health ?? HealthFor(kind),
        speed ?? SpeedFor(kind),
        AttackIntervalFor(kind));
    return enemy;
  }

  public Enemy AddRandomEnemy(EnemyKind kind)
  {
    int minimumX = (int)MathF.Ceiling(_config.WorldBounds.MinX + _config.EnemyRadius);
    int maximumX = (int)MathF.Floor(_config.WorldBounds.MaxX - _config.EnemyRadius) + 1;
    return AddEnemy(kind, new Vector2D(
        _random.NextInt(minimumX, maximumX),
        _config.WorldBounds.MinY + _config.EnemyRadius));
  }

  public PowerUp AddPowerUp(PowerUpType kind, Vector2D position)
  {
    PowerUp powerUp = _entityFactory.CreatePowerUp(
        kind,
        _config.WorldBounds.Clamp(position, _config.PowerUpRadius),
        _config.PowerUpFallSpeed,
        CreateCircleHitbox(_config.PowerUpRadius));
    State.AddPowerUp(powerUp);
    return powerUp;
  }

  public void StartCampaign()
  {
    if (!_config.CampaignEnabled || State.Wave != 0)
    {
      return;
    }

    State.Wave = 1;
    BeginWaveTransition(completionBonus: 0);
  }

  public void Step(IReadOnlyDictionary<PlayerId, PlayerInput> inputs)
  {
    if (State.MatchStatus is MatchStatus.Won or MatchStatus.Lost)
    {
      return;
    }

    if (State.WaveTransitionTicksRemaining > 0)
    {
      AdvanceWaveTransition();
      State.Tick++;
      return;
    }

    _combatManager.BeginTick();
    float elapsedSeconds = (float)_config.FixedStep.TotalSeconds;
    UpdatePlayers(inputs, elapsedSeconds);
    UpdateProjectiles(elapsedSeconds);
    UpdatePowerUps(elapsedSeconds);
    ResolvePowerUpCollections();
    UpdateEnemies(elapsedSeconds);
    ResolveEnemyPlayerCollisions();
    AdvanceCampaignOrCompleteMatch();

    State.Tick++;
    if (State.Players.Count > 0 &&
        State.Players.Values.All(player => player.Status == PlayerStatus.Eliminated))
    {
      State.MatchStatus = MatchStatus.Lost;
    }
  }

  public void DamagePlayer(PlayerId playerId, int damage)
  {
    ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(damage, 0);

    if (State.Players.TryGetValue(playerId, out Player? player))
    {
      ApplyDamage(player, damage);
    }
  }

  public void DamageEnemy(EnemyId enemyId, PlayerId ownerId, int damage)
  {
    ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(damage, 0);
    if (!State.Players.ContainsKey(ownerId))
    {
      throw new ArgumentException("Projectile owner must belong to the world.", nameof(ownerId));
    }

    if (State.Enemies.TryGetValue(enemyId, out Enemy? enemy) && enemy.Status == EnemyStatus.Active)
    {
      ApplyEnemyDamage(enemy, ownerId, damage);
    }
  }

  private void UpdatePlayers(IReadOnlyDictionary<PlayerId, PlayerInput> inputs, float elapsedSeconds)
  {
    foreach (Player player in State.Players.Values.OrderBy(player => player.Id.Value))
    {
      UpdatePlayerTimers(player);
      if (player.Status is PlayerStatus.Respawning or PlayerStatus.Eliminated)
      {
        continue;
      }

      if (player.FireCooldownTicks > 0)
      {
        player.FireCooldownTicks--;
      }

      if (!inputs.TryGetValue(player.Id, out PlayerInput input))
      {
        continue;
      }

      Vector2D direction = input.Movement.LengthSquared > 1
          ? input.Movement.NormalizedOrZero()
          : input.Movement;
      player.Position = _config.PlayerBounds.Clamp(
          player.Position + (direction * (player.Speed * elapsedSeconds)),
          _config.PlayerRadius);

      if (input.Fire && player.FireCooldownTicks == 0)
      {
        Fire(player);
      }
    }
  }

  private void UpdatePlayerTimers(Player player)
  {
    PowerUpManager.Update(player);

    if (player.Status == PlayerStatus.Respawning && player.RespawnTicks > 0 && --player.RespawnTicks == 0)
    {
      player.Position = player.SpawnPosition;
      player.Status = PlayerStatus.Invulnerable;
      player.InvulnerabilityTicks = DurationInTicks(_config.InvulnerabilityDuration);
    }
    else if (player.Status == PlayerStatus.Invulnerable &&
             player.InvulnerabilityTicks > 0 &&
             --player.InvulnerabilityTicks == 0)
    {
      player.Status = PlayerStatus.Active;
    }
  }

  private void Fire(Player player)
  {
    AddProjectile(player, new Vector2D(0, -_config.ProjectileSpeed));
    if (player.ProjectileCount > 1)
    {
      AddProjectile(player, new Vector2D(-_config.ProjectileSpeed * 0.2f, -_config.ProjectileSpeed));
      AddProjectile(player, new Vector2D(_config.ProjectileSpeed * 0.2f, -_config.ProjectileSpeed));
    }

    long cooldownTicks = (long)(_config.FireCooldown.Ticks * player.FireCooldownScale);
    TimeSpan cooldown = TimeSpan.FromTicks(cooldownTicks);
    player.FireCooldownTicks = Math.Max(1, DurationInTicks(cooldown));
  }

  private void AddProjectile(Player player, Vector2D velocity)
  {
    Projectile projectile = _entityFactory.CreateProjectile(
        player.Key,
        EntityType.AlliedProjectile,
        player.Position,
        velocity,
        CreateCircleHitbox(_config.ProjectileRadius),
        _config.ProjectileDamage);
    State.AddProjectile(projectile);
  }

  private void UpdateProjectiles(float elapsedSeconds)
  {
    foreach (Projectile projectile in State.Projectiles.Values
                 .Where(projectile => projectile.Status == ProjectileStatus.Active)
                 .OrderBy(projectile => projectile.Id.Value))
    {
      projectile.Position += projectile.Velocity * elapsedSeconds;
      if (!_config.WorldBounds.Contains(projectile.Position))
      {
        projectile.Status = ProjectileStatus.Consumed;
        continue;
      }

      ResolveProjectileCollision(projectile);
    }

    foreach (ProjectileId projectileId in State.Projectiles
                 .Where(pair => pair.Value.Status == ProjectileStatus.Consumed)
                 .Select(pair => pair.Key)
                 .ToArray())
    {
      State.RemoveProjectile(projectileId);
    }
  }

  private void UpdatePowerUps(float elapsedSeconds)
  {
    foreach (PowerUp powerUp in State.PowerUps.Values
                 .Where(powerUp => powerUp.Status == PowerUpStatus.Available))
    {
      powerUp.Position += new Vector2D(0, _config.PowerUpFallSpeed * elapsedSeconds);
      if (powerUp.Position.Y > _config.WorldBounds.MaxY)
      {
        powerUp.Status = PowerUpStatus.Collected;
      }
    }
  }

  private void ResolvePowerUpCollections()
  {
    foreach (PowerUp powerUp in State.PowerUps.Values
                 .Where(powerUp => powerUp.Status == PowerUpStatus.Available)
                 .OrderBy(powerUp => powerUp.Id.Value))
    {
      Player? player = FindCollisionTarget<Player>(
          powerUp,
          ActivePlayers(),
          CollisionType.Pickup);
      if (player is null)
      {
        continue;
      }

      powerUp.Status = PowerUpStatus.Collected;
      ApplyPowerUp(player, powerUp.Kind);
    }

    RemoveCollectedPowerUps();
  }

  private void RemoveCollectedPowerUps()
  {
    PowerUpId[] collectedIds = State.PowerUps
        .Where(pair => pair.Value.Status == PowerUpStatus.Collected)
        .Select(pair => pair.Key)
        .ToArray();
    foreach (PowerUpId id in collectedIds)
    {
      State.RemovePowerUp(id);
    }
  }

  private void ApplyPowerUp(Player player, PowerUpType kind)
  {
    int duration = DurationInTicks(_config.PowerUpDuration);
    PowerUpManager.Collect(player, kind, duration, _config.SpeedBoostMultiplier);
  }

  private void UpdateEnemies(float elapsedSeconds)
  {
    foreach (Enemy enemy in State.Enemies.Values
                 .Where(enemy => enemy.Status == EnemyStatus.Active)
                 .OrderBy(enemy => enemy.Id.Value))
    {
      float speedMultiplier = enemy.Kind == EnemyKind.Boss
          ? CurrentBossSpeedMultiplier()
          : 1f;
      bool reachedBottom = _enemyManager.UpdateMovement(
          enemy,
          elapsedSeconds,
          speedMultiplier);

      if (enemy.Kind == EnemyKind.Boss)
      {
        UpdateBossAttack(enemy);
      }
      else if (enemy.Kind == EnemyKind.Tank)
      {
        UpdateTankAttack(enemy);
      }

      if (!reachedBottom)
      {
        continue;
      }

      Player? target = State.Players.Values
          .Where(player => player.Status != PlayerStatus.Eliminated)
          .OrderBy(player => player.Id.Value)
          .FirstOrDefault();
      if (target is not null)
      {
        ApplyDamage(target, _config.EnemyCollisionDamage);
      }

      enemy.Position = new Vector2D(enemy.Position.X, _config.WorldBounds.MinY + _config.EnemyRadius);
    }
  }

  private void ResolveEnemyPlayerCollisions()
  {
    foreach (Enemy enemy in State.Enemies.Values
                 .Where(enemy => enemy.Status == EnemyStatus.Active && enemy.Kind != EnemyKind.Boss)
                 .OrderBy(enemy => enemy.Id.Value))
    {
      IEnumerable<Player> activePlayers = State.Players.Values
          .Where(player => player.Status == PlayerStatus.Active);
      Player? player = FindCollisionTarget<Player>(
          enemy,
          activePlayers,
          CollisionType.Damage);
      if (player is null)
      {
        continue;
      }

      enemy.Status = EnemyStatus.Destroyed;
      EnemyStateMachine.Destroy(enemy);
      ApplyDamage(player, _config.EnemyCollisionDamage);
    }
  }

  private void ApplyDamage(Player player, int damage)
  {
    if (player.Status is PlayerStatus.Invulnerable or PlayerStatus.Respawning or PlayerStatus.Eliminated)
    {
      return;
    }

    _combatManager.ApplyDamage(player, damage);
    if (player.Health > 0)
    {
      return;
    }

    player.Lives--;
    if (player.Lives <= 0)
    {
      player.Lives = 0;
      player.Health = 0;
      player.Status = PlayerStatus.Eliminated;
      return;
    }

    player.Health = _config.PlayerMaxHealth;
    player.Status = PlayerStatus.Respawning;
    player.RespawnTicks = Math.Max(1, DurationInTicks(_config.RespawnDelay));
  }

  private void AdvanceCampaignOrCompleteMatch()
  {
    if (State.Enemies.Count == 0 ||
        State.Enemies.Values.Any(enemy => enemy.Status == EnemyStatus.Active))
    {
      return;
    }

    if (!_config.CampaignEnabled)
    {
      State.MatchStatus = MatchStatus.Won;
      return;
    }

    if (State.Wave >= _levelManager.LevelCount)
    {
      State.MatchStatus = MatchStatus.Won;
      return;
    }

    int completionBonus = _levelManager.GetLevel(State.Wave).CompletionBonus;
    foreach (Player player in State.Players.Values
                 .Where(player => player.Status != PlayerStatus.Eliminated))
    {
      player.Score += completionBonus;
    }

    State.Wave++;
    BeginWaveTransition(completionBonus);
  }

  private void BeginWaveTransition(int completionBonus)
  {
    ClearTransientEntities();
    State.WaveCompletionBonus = completionBonus;
    State.WaveTransitionTicksRemaining = WaveTransitionDurationInTicks();
    if (State.WaveTransitionTicksRemaining == 0)
    {
      CompleteWaveTransition();
    }
  }

  private void AdvanceWaveTransition()
  {
    State.WaveTransitionTicksRemaining--;
    if (State.WaveTransitionTicksRemaining == 0)
    {
      CompleteWaveTransition();
    }
  }

  private void CompleteWaveTransition()
  {
    State.WaveCompletionBonus = 0;
    SpawnWave(State.Wave);
  }

  private void ClearTransientEntities()
  {
    foreach (ProjectileId projectileId in State.Projectiles.Keys.ToArray())
    {
      State.RemoveProjectile(projectileId);
    }

    foreach (PowerUpId powerUpId in State.PowerUps.Keys.ToArray())
    {
      State.RemovePowerUp(powerUpId);
    }
  }

  private void SpawnWave(int wave)
  {
    IReadOnlyList<EnemySpawn> spawns = _levelManager.CreateSpawns(wave, _config.WorldBounds);
    if (spawns.Any(spawn => spawn.Kind == EnemyKind.Boss))
    {
      State.BossPhase = 1;
    }

    foreach (EnemySpawn spawn in spawns)
    {
      AddEnemy(spawn.Kind, spawn.Position, spawn.Health, spawn.Speed);
    }
  }

  private void UpdateBossPhase(Enemy enemy)
  {
    if (enemy.Kind != EnemyKind.Boss)
    {
      return;
    }

    if (State.Wave <= 0)
    {
      UpdateLegacyBossPhase(enemy);
      return;
    }

    LevelConfig level = _levelManager.GetLevel(State.Wave);
    float healthRatio = (float)enemy.Health / HealthForCurrentBoss(level);
    int phase = FindBossPhase(level.BossPhases, healthRatio);
    State.BossPhase = phase + 1;
    TimeSpan interval = level.BossPhases[phase].AttackInterval;
    enemy.AttackCooldownTicks = Math.Min(enemy.AttackCooldownTicks, DurationInTicks(interval));
  }

  private void UpdateBossAttack(Enemy boss)
  {
    if (boss.AttackCooldownTicks > 0 && --boss.AttackCooldownTicks > 0)
    {
      return;
    }

    Player[] targets = State.Players.Values
        .Where(player => player.Status is PlayerStatus.Active or PlayerStatus.Invulnerable)
        .OrderBy(player => player.Id.Value)
        .ToArray();
    if (State.BossPhase == 1)
    {
      if (targets.Length > 0)
      {
        FireAtPlayer(boss, targets[0], CurrentBossAttackDamage());
      }
    }
    else
    {
      foreach (Player target in targets)
      {
        FireAtPlayer(boss, target, CurrentBossAttackDamage());
      }
    }

    boss.AttackCooldownTicks = DurationInTicks(CurrentBossAttackInterval());
  }

  private void UpdateTankAttack(Enemy tank)
  {
    if (tank.AttackCooldownTicks > 0 && --tank.AttackCooldownTicks > 0)
    {
      return;
    }

    Player? target = ActivePlayers().FirstOrDefault();
    if (target is not null)
    {
      FireAtPlayer(tank, target, _config.EnemyProjectileDamage);
    }

    tank.AttackCooldownTicks = DurationInTicks(_config.EnemyAttackInterval);
  }

  private IEnumerable<Player> ActivePlayers()
  {
    return State.Players.Values
        .Where(player => player.Status is PlayerStatus.Active or PlayerStatus.Invulnerable)
        .OrderBy(player => player.Id.Value);
  }

  private void FireAtPlayer(Enemy enemy, Player player, int damage)
  {
    Vector2D direction = (player.Position - enemy.Position).NormalizedOrZero();
    Projectile projectile = _entityFactory.CreateProjectile(
        enemy.Key,
        EntityType.EnemyProjectile,
        enemy.Position,
        direction * _config.ProjectileSpeed,
        CreateCircleHitbox(_config.ProjectileRadius),
        damage);
    State.AddProjectile(projectile);
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
    IEnumerable<Enemy> enemies = State.Enemies.Values
        .Where(enemy => enemy.Status == EnemyStatus.Active);
    Enemy? enemy = FindCollisionTarget<Enemy>(
        projectile,
        enemies,
        CollisionType.Damage);
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
        ActivePlayers(),
        CollisionType.Damage);
    if (player is not null)
    {
      projectile.Status = ProjectileStatus.Consumed;
      ApplyDamage(player, projectile.Damage);
    }
  }

  private void ApplyEnemyDamage(Enemy enemy, PlayerId ownerId, int damage)
  {
    bool destroyed = _combatManager.ApplyDamage(enemy, damage);
    if (!destroyed)
    {
      UpdateBossPhase(enemy);
      return;
    }

    enemy.Status = EnemyStatus.Destroyed;
    EnemyStateMachine.Destroy(enemy);
    State.Players[ownerId].Score +=
        enemy.Kind == EnemyKind.Boss ? _config.BossKillScore : _config.EnemyKillScore;
    if (enemy.Kind != EnemyKind.Boss && ++_destroyedEnemyCount % 2 == 0)
    {
      IReadOnlyList<PowerUpType> available = CurrentAvailablePowerUps();
      PowerUpType kind = available[(_destroyedEnemyCount / 2 - 1) % available.Count];
      AddPowerUp(kind, enemy.Position);
    }
  }

  private IReadOnlyList<PowerUpType> CurrentAvailablePowerUps()
  {
    if (State.Wave <= 0)
    {
      return Enum.GetValues<PowerUpType>();
    }

    return _levelManager.GetLevel(State.Wave).AvailablePowerUps;
  }

  private int CurrentBossAttackDamage()
  {
    return State.Wave > 0 ? CurrentBossPhase().AttackDamage : _config.BossAttackDamage;
  }

  private TimeSpan CurrentBossAttackInterval()
  {
    if (State.Wave > 0)
    {
      return CurrentBossPhase().AttackInterval;
    }

    return State.BossPhase == 2
        ? _config.BossPhase2AttackInterval
        : _config.BossPhase1AttackInterval;
  }

  private float CurrentBossSpeedMultiplier()
  {
    if (State.Wave > 0)
    {
      return CurrentBossPhase().SpeedMultiplier;
    }

    return State.BossPhase == 2 ? 1.5f : 1f;
  }

  private BossPhaseConfig CurrentBossPhase()
  {
    LevelConfig level = _levelManager.GetLevel(State.Wave);
    int phaseIndex = Math.Clamp(State.BossPhase - 1, 0, level.BossPhases.Count - 1);
    return level.BossPhases[phaseIndex];
  }

  private static int FindBossPhase(IReadOnlyList<BossPhaseConfig> phases, float healthRatio)
  {
    int selected = 0;
    for (int index = 0; index < phases.Count; index++)
    {
      if (healthRatio <= phases[index].HealthThreshold)
      {
        selected = index;
      }
    }

    return selected;
  }

  private static int HealthForCurrentBoss(LevelConfig level)
  {
    return level.EnemyGroups.Single(group => group.Kind == EnemyKind.Boss).Health;
  }

  private void UpdateLegacyBossPhase(Enemy enemy)
  {
    if (enemy.Health > _config.BossHealth / 2)
    {
      return;
    }

    State.BossPhase = 2;
    enemy.AttackCooldownTicks = Math.Min(
        enemy.AttackCooldownTicks,
        DurationInTicks(_config.BossPhase2AttackInterval));
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

  private int HealthFor(EnemyKind kind) => kind switch
  {
    EnemyKind.Diver => _config.DiverHealth,
    EnemyKind.Tank => _config.TankHealth,
    EnemyKind.Boss => _config.BossHealth,
    _ => _config.EnemyHealth,
  };

  private float SpeedFor(EnemyKind kind) => kind switch
  {
    EnemyKind.Scout => _config.ScoutSpeed,
    EnemyKind.Diver => _config.DiverSpeed,
    EnemyKind.Tank => _config.TankSpeed,
    EnemyKind.Boss => _config.BossSpeed,
    _ => 0f,
  };

  private TimeSpan? AttackIntervalFor(EnemyKind kind)
  {
    return kind switch
    {
      EnemyKind.Tank => _config.EnemyAttackInterval,
      EnemyKind.Boss => State.Wave > 0
          ? CurrentBossAttackInterval()
          : _config.BossPhase1AttackInterval,
      _ => null,
    };
  }

  private static Hitbox CreateCircleHitbox(float radius)
  {
    return new Hitbox(Vector2D.Zero, new CircleShape(radius));
  }

  private int DurationInTicks(TimeSpan duration) =>
      Math.Max(1, (int)Math.Ceiling(duration / _config.FixedStep));

  private int WaveTransitionDurationInTicks() =>
      _config.WaveTransitionDuration == TimeSpan.Zero
          ? 0
          : DurationInTicks(_config.WaveTransitionDuration);

}
