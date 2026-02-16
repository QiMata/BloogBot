using BotRunner.Interfaces;
using Serilog;
using System;

namespace BotRunner.Tasks;

/// <summary>
/// Task that waits for a specified duration then pops itself.
/// </summary>
public class WaitTask(IBotContext botContext, int durationMs) : BotTask(botContext), IBotTask
{
    private readonly int _durationMs = durationMs;
    private DateTime? _startTime;

    public void Update()
    {
        _startTime ??= DateTime.Now;

        if ((DateTime.Now - _startTime.Value).TotalMilliseconds >= _durationMs)
        {
            Log.Information($"[WAIT] Wait completed ({_durationMs}ms)");
            BotTasks.Pop();
        }
    }
}
