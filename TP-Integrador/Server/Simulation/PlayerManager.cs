using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Owns player movement, firing, damage, respawn, and temporary effects.
/// </summary>
internal sealed class PlayerManager(
    SimulationContext context,
    CombatManager combat,
    ProjectileManager projectiles)
{
  public Player Add(PlayerId playerId, Vector2D position)
  {
    ValidatePlayerId(playerId);
    Player player = CreatePlayer(playerId, position);
    context.State.AddPlayer(player);
    context.State.MatchStatus = MatchStatus.Running;
    return player;
  }

  public bool Remove(PlayerId playerId)
  {
    bool removed = context.State.RemovePlayer(playerId);
    if (removed && context.State.Players.Count == 0)
    {
      context.State.MatchStatus = MatchStatus.Lost;
    }

    return removed;
  }

  public void Update(IReadOnlyDictionary<PlayerId, PlayerInput> inputs, float elapsedSeconds)
  {
    foreach (Player player in OrderedPlayers())
    {
      UpdatePlayer(player, inputs, elapsedSeconds);
    }
  }

  public void Damage(Player player, int damage)
  {
    if (!CanTakeDamage(player))
    {
      return;
    }

    combat.ApplyDamage(player, damage);
    HandleDepletedHealth(player);
  }

  public IEnumerable<Player> ActivePlayers()
  {
    return context.State.Players.Values
        .Where(player => player.Status is PlayerStatus.Active or PlayerStatus.Invulnerable)
        .OrderBy(player => player.Id.Value);
  }

  private void ValidatePlayerId(PlayerId playerId)
  {
    if (playerId.Value <= 0 || context.State.Players.ContainsKey(playerId))
    {
      throw new ArgumentException("Player identifier must be positive and unique.", nameof(playerId));
    }
  }

  private Player CreatePlayer(PlayerId playerId, Vector2D position)
  {
    SimulationConfig config = context.Config;
    Vector2D spawn = config.PlayerBounds.Clamp(position, config.PlayerRadius);
    Hitbox hitbox = new(Vector2D.Zero, new CircleShape(config.PlayerRadius));
    return context.Factory.CreatePlayer(
        playerId,
        spawn,
        config.PlayerSpeed,
        hitbox,
        config.PlayerStartingLives,
        config.PlayerMaxHealth);
  }

  private IEnumerable<Player> OrderedPlayers()
  {
    return context.State.Players.Values.OrderBy(player => player.Id.Value);
  }

  private void UpdatePlayer(
      Player player,
      IReadOnlyDictionary<PlayerId, PlayerInput> inputs,
      float elapsedSeconds)
  {
    UpdateTimers(player);
    if (!CanReceiveInput(player))
    {
      return;
    }

    UpdateFireCooldown(player);
    if (!inputs.TryGetValue(player.Id, out PlayerInput input))
    {
      return;
    }

    Move(player, input.Movement, elapsedSeconds);
    TryFire(player, input.Fire);
  }

  private void UpdateTimers(Player player)
  {
    PowerUpManager.Update(player);
    if (player.Status == PlayerStatus.Respawning)
    {
      UpdateRespawn(player);
    }
    else if (player.Status == PlayerStatus.Invulnerable)
    {
      UpdateInvulnerability(player);
    }
  }

  private static void UpdateFireCooldown(Player player)
  {
    if (player.FireCooldownTicks > 0)
    {
      player.FireCooldownTicks--;
    }
  }

  private void UpdateRespawn(Player player)
  {
    if (player.Status != PlayerStatus.Respawning || player.RespawnTicks <= 0)
    {
      return;
    }

    if (--player.RespawnTicks == 0)
    {
      BeginInvulnerability(player);
    }
  }

  private void BeginInvulnerability(Player player)
  {
    player.Position = player.SpawnPosition;
    player.Status = PlayerStatus.Invulnerable;
    player.InvulnerabilityTicks = context.DurationInTicks(context.Config.InvulnerabilityDuration);
  }

  private static void UpdateInvulnerability(Player player)
  {
    if (player.Status == PlayerStatus.Invulnerable &&
        player.InvulnerabilityTicks > 0 && --player.InvulnerabilityTicks == 0)
    {
      player.Status = PlayerStatus.Active;
    }
  }

  private static bool CanReceiveInput(Player player)
  {
    return player.Status is not PlayerStatus.Respawning and not PlayerStatus.Eliminated;
  }

  private void Move(Player player, Vector2D movement, float elapsedSeconds)
  {
    Vector2D direction = movement.LengthSquared > 1 ? movement.NormalizedOrZero() : movement;
    Vector2D destination = player.Position + (direction * (player.Speed * elapsedSeconds));
    player.Position = context.Config.PlayerBounds.Clamp(destination, context.Config.PlayerRadius);
  }

  private void TryFire(Player player, bool shouldFire)
  {
    if (!shouldFire || player.FireCooldownTicks > 0)
    {
      return;
    }

    FireProjectiles(player);
    player.FireCooldownTicks = CalculateCooldown(player);
  }

  private void FireProjectiles(Player player)
  {
    float speed = context.Config.ProjectileSpeed;
    projectiles.AddAllied(player, new Vector2D(0, -speed));
    if (player.ProjectileCount > 1)
    {
      projectiles.AddAllied(player, new Vector2D(-speed * 0.2f, -speed));
      projectiles.AddAllied(player, new Vector2D(speed * 0.2f, -speed));
    }
  }

  private int CalculateCooldown(Player player)
  {
    long ticks = (long)(context.Config.FireCooldown.Ticks * player.FireCooldownScale);
    return context.DurationInTicks(TimeSpan.FromTicks(ticks));
  }

  private static bool CanTakeDamage(Player player)
  {
    return player.Status is not PlayerStatus.Invulnerable and
        not PlayerStatus.Respawning and not PlayerStatus.Eliminated;
  }

  private void HandleDepletedHealth(Player player)
  {
    if (player.Health > 0)
    {
      return;
    }

    player.Lives--;
    if (player.Lives <= 0)
    {
      Eliminate(player);
      return;
    }

    BeginRespawn(player);
  }

  private static void Eliminate(Player player)
  {
    player.Lives = 0;
    player.Health = 0;
    player.Status = PlayerStatus.Eliminated;
  }

  private void BeginRespawn(Player player)
  {
    player.Health = context.Config.PlayerMaxHealth;
    player.Status = PlayerStatus.Respawning;
    player.RespawnTicks = context.DurationInTicks(context.Config.RespawnDelay);
  }
}
