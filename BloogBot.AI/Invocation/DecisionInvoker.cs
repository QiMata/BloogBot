using System.Reactive.Linq;
using System.Reactive.Subjects;
using BloogBot.AI.Configuration;
using Microsoft.Extensions.Logging;

namespace BloogBot.AI.Invocation;

/// <summary>
/// Timer-based implementation of decision invocation control.
/// Supports automatic interval-based invocation and ad-hoc requests.
/// </summary>
public sealed class DecisionInvoker : IDecisionInvoker
{
    private readonly DecisionInvocationSettings _settings;
    private readonly Func<CancellationToken, Task> _decisionCallback;
    private readonly ILogger<DecisionInvoker>? _logger;
    private readonly Subject<DecisionInvocationEvent> _invocationsSubject = new();
    private readonly object _lock = new();

    private Timer? _timer;
    private DateTimeOffset _lastInvocation;
    private DateTimeOffset _nextInvocation;
    private bool _isPaused;
    private bool _disposed;

    /// <summary>
    /// Creates a new DecisionInvoker with the specified settings and callback.
    /// </summary>
    /// <param name="settings">Configuration settings for invocation timing.</param>
    /// <param name="decisionCallback">Callback to invoke for each decision cycle.</param>
    /// <param name="logger">Optional logger.</param>
    public DecisionInvoker(
        DecisionInvocationSettings settings,
        Func<CancellationToken, Task> decisionCallback,
        ILogger<DecisionInvoker>? logger = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _decisionCallback = decisionCallback ?? throw new ArgumentNullException(nameof(decisionCallback));
        _logger = logger;

        _lastInvocation = DateTimeOffset.UtcNow;

        if (_settings.EnableAutomaticInvocation)
        {
            StartTimer();
        }
    }

    /// <inheritdoc />
    public TimeSpan CurrentInterval => _settings.DefaultInterval;

    /// <inheritdoc />
    public TimeSpan TimeUntilNextInvocation
    {
        get
        {
            lock (_lock)
            {
                if (!_settings.EnableAutomaticInvocation || _isPaused)
                    return TimeSpan.Zero;

                var remaining = _nextInvocation - DateTimeOffset.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }
    }

    /// <inheritdoc />
    public bool IsAutoInvocationEnabled => _settings.EnableAutomaticInvocation;

    /// <inheritdoc />
    public bool IsPaused
    {
        get
        {
            lock (_lock) { return _isPaused; }
        }
    }

    /// <inheritdoc />
    public IObservable<DecisionInvocationEvent> Invocations =>
        _invocationsSubject.AsObservable();

    /// <inheritdoc />
    public async Task InvokeNowAsync(CancellationToken cancellationToken = default)
    {
        var timeSinceLast = await InvokeInternalAsync(DecisionInvocationType.AdHoc, cancellationToken);

        if (_settings.ResetTimerOnAdHocInvocation)
        {
            ResetTimer();
        }

        _logger?.LogDebug("Ad-hoc decision invoked. Time since last: {TimeSinceLast}", timeSinceLast);
    }

    /// <inheritdoc />
    public void SetInterval(TimeSpan interval)
    {
        lock (_lock)
        {
            // Clamp to valid range
            if (interval < _settings.MinimumInterval)
                interval = _settings.MinimumInterval;
            if (interval > _settings.MaximumInterval)
                interval = _settings.MaximumInterval;

            _settings.DefaultInterval = interval;

            if (_timer != null && !_isPaused)
            {
                ResetTimer();
            }

            _logger?.LogInformation("Decision interval updated to {Interval}", interval);
        }
    }

    /// <inheritdoc />
    public void Pause()
    {
        lock (_lock)
        {
            if (_isPaused) return;
            _isPaused = true;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _logger?.LogInformation("Automatic decision invocation paused");
        }
    }

    /// <inheritdoc />
    public void Resume()
    {
        lock (_lock)
        {
            if (!_isPaused) return;
            _isPaused = false;

            if (_settings.EnableAutomaticInvocation)
            {
                ResetTimer();
            }

            _logger?.LogInformation("Automatic decision invocation resumed");
        }
    }

    private void StartTimer()
    {
        lock (_lock)
        {
            _nextInvocation = DateTimeOffset.UtcNow.Add(_settings.DefaultInterval);
            _timer = new Timer(
                OnTimerElapsed,
                null,
                _settings.DefaultInterval,
                _settings.DefaultInterval);
        }
    }

    private void ResetTimer()
    {
        lock (_lock)
        {
            _nextInvocation = DateTimeOffset.UtcNow.Add(_settings.DefaultInterval);
            _timer?.Change(_settings.DefaultInterval, _settings.DefaultInterval);
        }
    }

    private async void OnTimerElapsed(object? state)
    {
        if (_disposed || _isPaused) return;

        try
        {
            await InvokeInternalAsync(DecisionInvocationType.Automatic, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during automatic decision invocation");
        }
    }

    private async Task<TimeSpan?> InvokeInternalAsync(DecisionInvocationType type, CancellationToken cancellationToken)
    {
        TimeSpan? timeSinceLast;

        lock (_lock)
        {
            if (_disposed) return null;

            var now = DateTimeOffset.UtcNow;
            timeSinceLast = now - _lastInvocation;
            _lastInvocation = now;
        }

        try
        {
            await _decisionCallback(cancellationToken);

            var invocationEvent = new DecisionInvocationEvent(
                DateTimeOffset.UtcNow,
                type,
                timeSinceLast);

            _invocationsSubject.OnNext(invocationEvent);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected cancellation
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during decision callback");
            throw;
        }

        return timeSinceLast;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            _timer?.Dispose();
            _timer = null;

            _invocationsSubject.OnCompleted();
            _invocationsSubject.Dispose();
        }
    }
}
