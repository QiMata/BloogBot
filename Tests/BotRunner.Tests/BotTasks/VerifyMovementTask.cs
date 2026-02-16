using System;
using Tests.Infrastructure.BotTasks;

namespace BotRunner.Tests.BotTasks;

/// <summary>
/// BotTask that verifies the character can move forward.
/// Starts movement, waits briefly, stops, and checks that position changed.
/// This task requires a live WoW client with an injected bot.
/// </summary>
public class VerifyMovementTask : TestBotTask
{
    private readonly Func<(float X, float Y, float Z)> _getPosition;
    private readonly Action _startMoveForward;
    private readonly Action _stopMoveForward;

    private (float X, float Y, float Z)? _startPos;
    private DateTime? _moveStartTime;
    private bool _movementStopped;

    public VerifyMovementTask(
        Func<(float X, float Y, float Z)> getPosition,
        Action startMoveForward,
        Action stopMoveForward)
        : base("VerifyMovement")
    {
        _getPosition = getPosition;
        _startMoveForward = startMoveForward;
        _stopMoveForward = stopMoveForward;
        Timeout = TimeSpan.FromSeconds(10);
    }

    public override void Update()
    {
        // Phase 1: Start moving
        if (_startPos == null)
        {
            _startPos = _getPosition();
            _moveStartTime = DateTime.UtcNow;
            _startMoveForward();
            return; // Wait for next tick
        }

        // Phase 2: Wait for movement to happen (2 seconds)
        if (!_movementStopped && (DateTime.UtcNow - _moveStartTime!.Value).TotalSeconds >= 2.0)
        {
            _stopMoveForward();
            _movementStopped = true;
            return; // Wait for next tick
        }

        if (!_movementStopped)
            return; // Still waiting

        // Phase 3: Check position changed
        var endPos = _getPosition();
        float dx = endPos.X - _startPos.Value.X;
        float dy = endPos.Y - _startPos.Value.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        if (dist < 0.5f)
        {
            Fail($"Position barely changed after 2s of movement: " +
                 $"start=({_startPos.Value.X:F1},{_startPos.Value.Y:F1}), " +
                 $"end=({endPos.X:F1},{endPos.Y:F1}), dist={dist:F2}y");
            return;
        }

        Complete();
    }
}
