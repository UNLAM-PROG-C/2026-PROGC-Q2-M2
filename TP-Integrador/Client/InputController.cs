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

    public void Update()
    {
        InputStatePayload input = Capture();
        ulong now = Time.GetTicksMsec();
        if (_sendInFlight || ShouldSuppress(input, now))
        {
            return;
        }

        _lastInput = input;
        _lastSentAt = now;
        _sendInFlight = true;
        _ = SendAsync(input);
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
}
