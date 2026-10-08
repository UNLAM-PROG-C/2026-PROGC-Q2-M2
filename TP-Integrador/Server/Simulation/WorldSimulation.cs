using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

public sealed class WorldSimulation
{
    private readonly SimulationConfig _config;
    private readonly IRandomSource _random;
    private int _nextPlayerId = 1;
    private int _nextEnemyId = 1;
    private int _nextProjectileId = 1;
    private int _nextPowerUpId = 1;
    private int _destroyedEnemyCount;

    public WorldSimulation(SimulationConfig config, IRandomSource random)
    {
        config.Validate();
        _config = config;
        _random = random;
    }

    public WorldState State { get; } = new();

    public PlayerState AddPlayer(Vector2D position) => AddPlayer(new PlayerId(_nextPlayerId), position);

    public PlayerState AddPlayer(PlayerId playerId, Vector2D position)
    {
        if (playerId.Value <= 0 || State.MutablePlayers.ContainsKey(playerId))
        {
            throw new ArgumentException("Player identifier must be positive and unique.", nameof(playerId));
        }

        var player = new PlayerState(
            playerId,
            _config.PlayerBounds.Clamp(position, _config.PlayerRadius),
            _config.PlayerStartingLives,
            _config.PlayerMaxHealth);
        State.MutablePlayers.Add(player.Id, player);
        _nextPlayerId = Math.Max(_nextPlayerId, playerId.Value + 1);
        State.MatchStatus = MatchStatus.Running;
        return player;
    }

    public bool RemovePlayer(PlayerId playerId)
    {
        var removed = State.MutablePlayers.Remove(playerId);
        if (removed && State.MutablePlayers.Count == 0)
        {
            State.MatchStatus = MatchStatus.Lost;
        }

        return removed;
    }

    public EnemyState AddEnemy(EnemyKind kind, Vector2D position, int? health = null)
    {
        var enemy = new EnemyState(
            new EnemyId(_nextEnemyId++),
            kind,
            _config.WorldBounds.Clamp(position, _config.EnemyRadius),
            health ?? HealthFor(kind));
        if (kind == EnemyKind.Boss)
        {
            State.BossPhase = 1;
            enemy.AttackCooldownTicks = DurationInTicks(_config.BossPhase1AttackInterval);
        }

        State.MutableEnemies.Add(enemy.Id, enemy);
        return enemy;
    }

    public EnemyState AddRandomEnemy(EnemyKind kind)
    {
        var minimumX = (int)MathF.Ceiling(_config.WorldBounds.MinX + _config.EnemyRadius);
        var maximumX = (int)MathF.Floor(_config.WorldBounds.MaxX - _config.EnemyRadius) + 1;
        return AddEnemy(kind, new Vector2D(
            _random.NextInt(minimumX, maximumX),
            _config.WorldBounds.MinY + _config.EnemyRadius));
    }

    public PowerUpState AddPowerUp(PowerUpKind kind, Vector2D position)
    {
        var powerUp = new PowerUpState(
            new PowerUpId(_nextPowerUpId++),
            kind,
            _config.WorldBounds.Clamp(position, _config.PowerUpRadius));
        State.MutablePowerUps.Add(powerUp.Id, powerUp);
        return powerUp;
    }

    public void StartCampaign()
    {
        if (!_config.CampaignEnabled || State.Wave != 0)
        {
            return;
        }

        State.Wave = 1;
        SpawnWave(1);
    }

    public void Step(IReadOnlyDictionary<PlayerId, PlayerInput> inputs)
    {
        if (State.MatchStatus is MatchStatus.Won or MatchStatus.Lost)
        {
            return;
        }

        var elapsedSeconds = (float)_config.FixedStep.TotalSeconds;
        UpdatePlayers(inputs, elapsedSeconds);
        UpdateProjectiles(elapsedSeconds);
        UpdatePowerUps(elapsedSeconds);
        ResolvePowerUpCollections();
        UpdateEnemies(elapsedSeconds);
        ResolveEnemyPlayerCollisions();
        AdvanceCampaignOrCompleteMatch();

        State.Tick++;
        if (State.MutablePlayers.Count > 0 &&
            State.MutablePlayers.Values.All(player => player.Status == PlayerStatus.Eliminated))
        {
            State.MatchStatus = MatchStatus.Lost;
        }
    }

    public void DamagePlayer(PlayerId playerId, int damage)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(damage, 0);

        if (State.MutablePlayers.TryGetValue(playerId, out var player))
        {
            ApplyDamage(player, damage);
        }
    }

    public void DamageEnemy(EnemyId enemyId, PlayerId ownerId, int damage)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(damage, 0);
        if (!State.MutablePlayers.ContainsKey(ownerId))
        {
            throw new ArgumentException("Projectile owner must belong to the world.", nameof(ownerId));
        }

        if (State.MutableEnemies.TryGetValue(enemyId, out var enemy) && enemy.Status == EnemyStatus.Active)
        {
            ApplyEnemyDamage(enemy, ownerId, damage);
        }
    }

    private void UpdatePlayers(IReadOnlyDictionary<PlayerId, PlayerInput> inputs, float elapsedSeconds)
    {
        foreach (var player in State.MutablePlayers.Values.OrderBy(player => player.Id.Value))
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

            if (!inputs.TryGetValue(player.Id, out var input))
            {
                continue;
            }

            var direction = input.Movement.LengthSquared > 1
                ? input.Movement.NormalizedOrZero()
                : input.Movement;
            player.Position = _config.PlayerBounds.Clamp(
                player.Position + (direction * (_config.PlayerSpeed * elapsedSeconds)),
                _config.PlayerRadius);

            if (input.Fire && player.FireCooldownTicks == 0)
            {
                Fire(player);
            }
        }
    }

    private void UpdatePlayerTimers(PlayerState player)
    {
        if (player.RapidFireTicks > 0)
        {
            player.RapidFireTicks--;
        }

        if (player.MultiShotTicks > 0)
        {
            player.MultiShotTicks--;
        }

        if (player.ShieldTicks > 0 && --player.ShieldTicks == 0)
        {
            player.Shield = 0;
        }

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

    private void Fire(PlayerState player)
    {
        AddProjectile(player, new Vector2D(0, -_config.ProjectileSpeed));
        if (player.MultiShotTicks > 0)
        {
            AddProjectile(player, new Vector2D(-_config.ProjectileSpeed * 0.2f, -_config.ProjectileSpeed));
            AddProjectile(player, new Vector2D(_config.ProjectileSpeed * 0.2f, -_config.ProjectileSpeed));
        }

        var cooldown = player.RapidFireTicks > 0
            ? TimeSpan.FromTicks(_config.FireCooldown.Ticks / 2)
            : _config.FireCooldown;
        player.FireCooldownTicks = Math.Max(1, DurationInTicks(cooldown));
    }

    private void AddProjectile(PlayerState player, Vector2D velocity)
    {
        var projectile = new ProjectileState(
            new ProjectileId(_nextProjectileId++),
            player.Id,
            player.Position,
            velocity,
            _config.ProjectileDamage);
        State.MutableProjectiles.Add(projectile.Id, projectile);
    }

    private void UpdateProjectiles(float elapsedSeconds)
    {
        foreach (var projectile in State.MutableProjectiles.Values
                     .Where(projectile => projectile.Status == ProjectileStatus.Active)
                     .OrderBy(projectile => projectile.Id.Value))
        {
            projectile.Position += projectile.Velocity * elapsedSeconds;
            if (!_config.WorldBounds.Contains(projectile.Position))
            {
                projectile.Status = ProjectileStatus.Consumed;
                continue;
            }

            var enemy = State.MutableEnemies.Values
                .Where(enemy => enemy.Status == EnemyStatus.Active)
                .OrderBy(enemy => enemy.Id.Value)
                .FirstOrDefault(enemy => Intersects(
                    projectile.Position,
                    _config.ProjectileRadius,
                    enemy.Position,
                    _config.EnemyRadius));
            if (enemy is null)
            {
                continue;
            }

            projectile.Status = ProjectileStatus.Consumed;
            ApplyEnemyDamage(enemy, projectile.OwnerId, projectile.Damage);
        }

        foreach (var projectileId in State.MutableProjectiles
                     .Where(pair => pair.Value.Status == ProjectileStatus.Consumed)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            State.MutableProjectiles.Remove(projectileId);
        }
    }

    private void UpdatePowerUps(float elapsedSeconds)
    {
        foreach (var powerUp in State.MutablePowerUps.Values.Where(powerUp => powerUp.Status == PowerUpStatus.Available))
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
        foreach (var powerUp in State.MutablePowerUps.Values
                     .Where(powerUp => powerUp.Status == PowerUpStatus.Available)
                     .OrderBy(powerUp => powerUp.Id.Value))
        {
            var player = State.MutablePlayers.Values
                .Where(player => player.Status is PlayerStatus.Active or PlayerStatus.Invulnerable)
                .OrderBy(player => player.Id.Value)
                .FirstOrDefault(player => Intersects(
                    player.Position,
                    _config.PlayerRadius,
                    powerUp.Position,
                    _config.PowerUpRadius));
            if (player is null)
            {
                continue;
            }

            powerUp.Status = PowerUpStatus.Collected;
            ApplyPowerUp(player, powerUp.Kind);
        }
    }

    private void ApplyPowerUp(PlayerState player, PowerUpKind kind)
    {
        var duration = DurationInTicks(_config.PowerUpDuration);
        switch (kind)
        {
            case PowerUpKind.RapidFire:
                player.RapidFireTicks = duration;
                break;
            case PowerUpKind.MultiShot:
                player.MultiShotTicks = duration;
                break;
            case PowerUpKind.Shield:
                player.Shield = 1;
                player.ShieldTicks = duration;
                break;
        }
    }

    private void UpdateEnemies(float elapsedSeconds)
    {
        foreach (var enemy in State.MutableEnemies.Values
                     .Where(enemy => enemy.Status == EnemyStatus.Active)
                     .OrderBy(enemy => enemy.Id.Value))
        {
            enemy.MovementPhase += elapsedSeconds;
            var direction = (enemy.Id.Value + (int)enemy.MovementPhase) % 2 == 0 ? 1f : -1f;
            var velocity = enemy.Kind switch
            {
                EnemyKind.Scout => new Vector2D(direction * _config.ScoutSpeed, _config.ScoutSpeed * 0.6f),
                EnemyKind.Diver => new Vector2D(direction * _config.DiverSpeed * 0.25f, _config.DiverSpeed),
                EnemyKind.Tank => new Vector2D(direction * _config.TankSpeed * 0.15f, _config.TankSpeed),
                EnemyKind.Boss => new Vector2D(
                    direction * _config.BossSpeed * (State.BossPhase == 2 ? 1.5f : 1f),
                    0),
                _ => Vector2D.Zero,
            };
            enemy.Position += velocity * elapsedSeconds;
            enemy.Position = new Vector2D(
                Math.Clamp(
                    enemy.Position.X,
                    _config.WorldBounds.MinX + _config.EnemyRadius,
                    _config.WorldBounds.MaxX - _config.EnemyRadius),
                enemy.Position.Y);

            if (enemy.Kind == EnemyKind.Boss)
            {
                UpdateBossAttack(enemy);
            }

            if (enemy.Position.Y <= _config.WorldBounds.MaxY - _config.EnemyRadius)
            {
                continue;
            }

            var target = State.MutablePlayers.Values
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
        foreach (var enemy in State.MutableEnemies.Values
                     .Where(enemy => enemy.Status == EnemyStatus.Active && enemy.Kind != EnemyKind.Boss)
                     .OrderBy(enemy => enemy.Id.Value))
        {
            var player = State.MutablePlayers.Values
                .Where(player => player.Status == PlayerStatus.Active)
                .OrderBy(player => player.Id.Value)
                .FirstOrDefault(player => Intersects(
                    enemy.Position,
                    _config.EnemyRadius,
                    player.Position,
                    _config.PlayerRadius));
            if (player is null)
            {
                continue;
            }

            enemy.Status = EnemyStatus.Destroyed;
            ApplyDamage(player, _config.EnemyCollisionDamage);
        }
    }

    private void ApplyDamage(PlayerState player, int damage)
    {
        if (player.Status is PlayerStatus.Invulnerable or PlayerStatus.Respawning or PlayerStatus.Eliminated)
        {
            return;
        }

        if (player.Shield > 0)
        {
            player.Shield = 0;
            player.ShieldTicks = 0;
            return;
        }

        player.Health -= damage;
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
        if (State.MutableEnemies.Count == 0 ||
            State.MutableEnemies.Values.Any(enemy => enemy.Status == EnemyStatus.Active))
        {
            return;
        }

        if (!_config.CampaignEnabled)
        {
            State.MatchStatus = MatchStatus.Won;
            return;
        }

        if (State.Wave >= 4)
        {
            State.MatchStatus = MatchStatus.Won;
            return;
        }

        foreach (var player in State.MutablePlayers.Values.Where(player => player.Status != PlayerStatus.Eliminated))
        {
            player.Score += _config.WaveBonusScore;
        }

        State.Wave++;
        SpawnWave(State.Wave);
    }

    private void SpawnWave(int wave)
    {
        if (wave == 4)
        {
            State.BossPhase = 1;
            AddEnemy(
                EnemyKind.Boss,
                new Vector2D((_config.WorldBounds.MinX + _config.WorldBounds.MaxX) / 2, 100),
                _config.BossHealth);
            return;
        }

        var count = wave switch
        {
            1 => _config.Wave1EnemyCount,
            2 => _config.Wave2EnemyCount,
            _ => _config.Wave3EnemyCount,
        };
        for (var index = 0; index < count; index++)
        {
            var kind = wave switch
            {
                1 => EnemyKind.Scout,
                2 => index % 2 == 0 ? EnemyKind.Scout : EnemyKind.Diver,
                _ => (index % 3) switch
                {
                    0 => EnemyKind.Scout,
                    1 => EnemyKind.Diver,
                    _ => EnemyKind.Tank,
                },
            };
            var columns = Math.Min(count, 5);
            var column = index % columns;
            var row = index / columns;
            var x = _config.WorldBounds.MaxX * (column + 1) / (columns + 1);
            AddEnemy(kind, new Vector2D(x, 80 + (row * 55)));
        }
    }

    private void UpdateBossPhase(EnemyState enemy)
    {
        if (enemy.Kind == EnemyKind.Boss && enemy.Health <= _config.BossHealth / 2)
        {
            State.BossPhase = 2;
            enemy.AttackCooldownTicks = Math.Min(
                enemy.AttackCooldownTicks,
                DurationInTicks(_config.BossPhase2AttackInterval));
        }
    }

    private void UpdateBossAttack(EnemyState boss)
    {
        if (boss.AttackCooldownTicks > 0 && --boss.AttackCooldownTicks > 0)
        {
            return;
        }

        var targets = State.MutablePlayers.Values
            .Where(player => player.Status is PlayerStatus.Active or PlayerStatus.Invulnerable)
            .OrderBy(player => player.Id.Value)
            .ToArray();
        if (State.BossPhase == 1)
        {
            if (targets.Length > 0)
            {
                ApplyDamage(targets[0], _config.BossAttackDamage);
            }
        }
        else
        {
            foreach (var target in targets)
            {
                ApplyDamage(target, _config.BossAttackDamage);
            }
        }

        boss.AttackCooldownTicks = DurationInTicks(
            State.BossPhase == 2
                ? _config.BossPhase2AttackInterval
                : _config.BossPhase1AttackInterval);
    }

    private void ApplyEnemyDamage(EnemyState enemy, PlayerId ownerId, int damage)
    {
        enemy.Health = Math.Max(0, enemy.Health - damage);
        if (enemy.Health > 0)
        {
            UpdateBossPhase(enemy);
            return;
        }

        enemy.Status = EnemyStatus.Destroyed;
        State.MutablePlayers[ownerId].Score +=
            enemy.Kind == EnemyKind.Boss ? _config.BossKillScore : _config.EnemyKillScore;
        if (enemy.Kind != EnemyKind.Boss && ++_destroyedEnemyCount % 2 == 0)
        {
            var kind = (PowerUpKind)((_destroyedEnemyCount / 2 - 1) % 3);
            AddPowerUp(kind, enemy.Position);
        }
    }

    private int HealthFor(EnemyKind kind) => kind switch
    {
        EnemyKind.Diver => _config.DiverHealth,
        EnemyKind.Tank => _config.TankHealth,
        EnemyKind.Boss => _config.BossHealth,
        _ => _config.EnemyHealth,
    };

    private int DurationInTicks(TimeSpan duration) =>
        Math.Max(1, (int)Math.Ceiling(duration / _config.FixedStep));

    private static bool Intersects(Vector2D first, float firstRadius, Vector2D second, float secondRadius)
    {
        var deltaX = first.X - second.X;
        var deltaY = first.Y - second.Y;
        var combinedRadius = firstRadius + secondRadius;
        return (deltaX * deltaX) + (deltaY * deltaY) <= combinedRadius * combinedRadius;
    }
}
