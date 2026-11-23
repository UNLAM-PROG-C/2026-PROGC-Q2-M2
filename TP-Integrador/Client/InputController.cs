using Godot;
using SpaceShooter.Protocol;

namespace SpaceShooter.Client;

/// <summary>
/// Captures local input and rate-limits commands sent to the server.
/// </summary>
internal sealed class InputController(ClientCoordinator coordinator)
{
    private const ulong ResendIntervalMilliseconds = 100;
    private bool _sendInFlight;
    private InputStatePayload? _lastInput;
    private ulong _lastSentAt;
    private bool _enabled = true;
    private Task _sendTask = Task.CompletedTask;

    public void Update()
    {
        if (!_enabled)
        {
            return;
        }

        InputStatePayload input = Capture();
        ulong now = Time.GetTicksMsec();

        if (_sendInFlight || ShouldSuppress(input, now))
        {
            return;
        }

        _lastInput = input;
        _lastSentAt = now;
        _sendInFlight = true;

        _sendTask = SendAsync(input);
    }

    private static InputStatePayload Capture()
    {
        return new InputStatePayload(
            Input.GetAxis("ui_left", "ui_right"),
            Input.GetAxis("ui_up", "ui_down"),
            Input.IsActionPressed("ui_accept"),
            (long)Time.GetTicksMsec());
    }

    private async Task SendAsync(InputStatePayload input)
    {
        try
        {
            if (coordinator.IsConnected)
            {
                await coordinator.SendAsync(input);
            }
        }
        finally
        {
            _sendInFlight = false;
        }
    }

    private bool ShouldSuppress(InputStatePayload input, ulong now)
    {
        return input == _lastInput && now - _lastSentAt < ResendIntervalMilliseconds;
    }

    public async Task PauseAsync()
    {
        _enabled = false;

        // Esperamos a que termine el envío que ya estaba en curso.
        await _sendTask;

        // Enviamos un estado neutral al servidor.
        InputStatePayload neutral = new(
            0f,
            0f,
            false,
            (long)Time.GetTicksMsec());

        await coordinator.SendAsync(neutral);
    }

    public void Resume()
    {
        _lastInput = null;
        _enabled = true;
    }

    public void Enable()
    {
        _enabled = true;
    }

    public void Disable()
    {
        _enabled = false;
    }
}
