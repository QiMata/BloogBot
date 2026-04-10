using System;
using System.Threading;
using System.Threading.Tasks;

namespace BotCommLayer;

/// <summary>
/// Reusable exponential-backoff retry helpers extracted from ProtobufSocketClient.
/// Covers both count-limited and time-budgeted retry patterns.
/// </summary>
public static class RetryPolicy
{
    /// <summary>
    /// Retries <paramref name="operation"/> up to <paramref name="maxRetries"/> times
    /// with exponential backoff. If the operation succeeds (no exception), returns immediately.
    /// </summary>
    /// <param name="operation">Action to attempt. Throw on failure.</param>
    /// <param name="maxRetries">Maximum number of attempts.</param>
    /// <param name="baseDelayMs">Initial delay before the first retry.</param>
    /// <param name="shouldRetry">
    /// Predicate that decides whether to retry after a caught exception.
    /// Return false to rethrow immediately. Called with (attempt, exception).
    /// </param>
    /// <param name="onRetry">
    /// Optional callback invoked before each retry delay with (attempt, delay, exception).
    /// </param>
    /// <param name="onFinalFailure">
    /// Optional callback to wrap the final exception. Return the exception to throw.
    /// If null, the last exception is rethrown directly.
    /// </param>
    public static void Execute(
        Action operation,
        int maxRetries = 10,
        int baseDelayMs = 500,
        Func<int, Exception, bool>? shouldRetry = null,
        Action<int, int, Exception>? onRetry = null,
        Func<int, Exception, Exception>? onFinalFailure = null)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                operation();
                return;
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    if (onFinalFailure != null)
                        throw onFinalFailure(attempt, ex);
                    throw;
                }

                if (shouldRetry != null && !shouldRetry(attempt, ex))
                    throw;

                int delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                onRetry?.Invoke(attempt, delay, ex);
                Thread.Sleep(delay);
            }
        }
    }

    /// <summary>
    /// Retries <paramref name="operation"/> within a fixed time budget using
    /// exponential backoff with a configurable delay cap.
    /// </summary>
    /// <param name="operation">Action to attempt. Throw on failure.</param>
    /// <param name="budgetMs">Total time budget in milliseconds.</param>
    /// <param name="baseDelayMs">Initial delay before the first retry.</param>
    /// <param name="maxDelayMs">Maximum per-retry delay.</param>
    /// <param name="onRetry">
    /// Optional callback invoked before each retry delay with (attempt, delay, exception, remainingMs).
    /// </param>
    /// <param name="onTimeout">
    /// Factory for the exception thrown when the budget expires. Called with (attempts, lastException).
    /// If null, a <see cref="TimeoutException"/> is thrown.
    /// </param>
    public static void ExecuteWithBudget(
        Action operation,
        int budgetMs,
        int baseDelayMs = 100,
        int maxDelayMs = 1000,
        Action<int, int, Exception?, int>? onRetry = null,
        Func<int, Exception?, Exception>? onTimeout = null)
    {
        var attemptCount = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Exception? lastException = null;

        while (true)
        {
            attemptCount++;
            try
            {
                operation();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            var remainingMs = budgetMs - (int)sw.ElapsedMilliseconds;
            if (remainingMs <= 0)
                break;

            var delay = Math.Min(baseDelayMs * (1 << Math.Min(attemptCount - 1, 3)), maxDelayMs);
            delay = Math.Min(delay, remainingMs);
            onRetry?.Invoke(attemptCount, delay, lastException, remainingMs);
            if (delay > 0)
                Thread.Sleep(delay);
        }

        if (onTimeout != null)
            throw onTimeout(attemptCount, lastException);

        throw new TimeoutException(
            $"Operation exceeded {budgetMs}ms budget after {attemptCount} attempt(s).",
            lastException);
    }

    /// <summary>
    /// Async retry with exponential backoff and optional jitter.
    /// Used for high-concurrency reconnection (avoids thundering herd).
    /// </summary>
    /// <param name="operation">Async action to attempt. Throw on failure.</param>
    /// <param name="maxRetries">Maximum number of attempts.</param>
    /// <param name="baseDelayMs">Initial delay before the first retry.</param>
    /// <param name="shouldRetry">
    /// Predicate that decides whether to retry after a caught exception.
    /// Return false to rethrow immediately. Called with (attempt, exception).
    /// </param>
    /// <param name="jitterMs">
    /// Maximum random jitter added to each delay to prevent thundering herd.
    /// Set to 0 to disable jitter.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="onFinalFailure">
    /// Optional factory to wrap the final exception. If null, rethrows directly.
    /// </param>
    public static async Task ExecuteAsync(
        Func<Task> operation,
        int maxRetries = 8,
        int baseDelayMs = 200,
        Func<int, Exception, bool>? shouldRetry = null,
        int jitterMs = 0,
        CancellationToken ct = default,
        Func<int, Exception, Exception>? onFinalFailure = null)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await operation();
                return;
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    if (onFinalFailure != null)
                        throw onFinalFailure(attempt, ex);
                    throw;
                }

                if (shouldRetry != null && !shouldRetry(attempt, ex))
                    throw;

                var delay = baseDelayMs * (1 << (attempt - 1));
                if (jitterMs > 0)
                    delay += Random.Shared.Next(0, jitterMs);
                await Task.Delay(delay, ct);
            }
        }
    }
}
