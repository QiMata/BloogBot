using System;
using GameData.Core.Enums;

namespace GameData.Core.Exceptions;

/// <summary>
/// Thrown at the boundary where a bot task surfaces a failure that needs
/// to become a metric or a log line. <see cref="Reason"/> is the stable
/// label that drives time-series; <see cref="Detail"/> is the
/// human-readable string used in log messages only.
///
/// See <c>docs/Spec/12_ERROR_TAXONOMY.md</c> for mapping rules.
/// </summary>
public sealed class BotTaskFailedException : Exception
{
    public FailureReason Reason { get; }
    public string Detail { get; }

    public BotTaskFailedException(FailureReason reason, string detail)
        : base($"{reason}: {detail}")
    {
        Reason = reason;
        Detail = detail;
    }

    public BotTaskFailedException(FailureReason reason, string detail, Exception inner)
        : base($"{reason}: {detail}", inner)
    {
        Reason = reason;
        Detail = detail;
    }
}
