using System;
using WoWSharpClient.Networking.Abstractions;

namespace WoWSharpClient.Networking.Implementation
{
    /// <summary>
    /// Exponential backoff reconnection policy with maximum attempts and delays.
    /// </summary>
    public sealed class ExponentialBackoffPolicy : IReconnectPolicy
    {
        private readonly int _maxAttempts;
        private readonly TimeSpan _initialDelay;
        private readonly TimeSpan _maxDelay;
        private readonly double _backoffMultiplier;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExponentialBackoffPolicy"/> class.
        /// </summary>
        /// <param name="maxAttempts">Maximum number of reconnection attempts (0 = unlimited).</param>
        /// <param name="initialDelay">Initial delay before first reconnection attempt.</param>
        /// <param name="maxDelay">Maximum delay between attempts.</param>
        /// <param name="backoffMultiplier">Multiplier for exponential backoff.</param>
        public ExponentialBackoffPolicy(
            int maxAttempts = 10,
            TimeSpan? initialDelay = null,
            TimeSpan? maxDelay = null,
            double backoffMultiplier = 2.0)
        {
            _maxAttempts = maxAttempts;
            _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
            _maxDelay = maxDelay ?? TimeSpan.FromMinutes(5);
            _backoffMultiplier = backoffMultiplier;

            if (_backoffMultiplier <= 1.0)
                throw new ArgumentException("Backoff multiplier must be greater than 1.0", nameof(backoffMultiplier));
        }

        public TimeSpan? GetDelay(int attempt, Exception? lastError)
        {
            if (_maxAttempts > 0 && attempt > _maxAttempts)
                return null; // Stop reconnecting

            var delay = TimeSpan.FromTicks((long)(_initialDelay.Ticks * Math.Pow(_backoffMultiplier, attempt - 1)));
            
            if (delay > _maxDelay)
                delay = _maxDelay;

            return delay;
        }
    }

    /// <summary>
    /// A simple reconnection policy that always returns a fixed delay.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="FixedDelayPolicy"/> class.
    /// </remarks>
    /// <param name="delay">Fixed delay between reconnection attempts.</param>
    /// <param name="maxAttempts">Maximum number of reconnection attempts (0 = unlimited).</param>
    public sealed class FixedDelayPolicy(TimeSpan delay, int maxAttempts = 0) : IReconnectPolicy
    {
        private readonly int _maxAttempts = maxAttempts;
        private readonly TimeSpan _delay = delay;

        public TimeSpan? GetDelay(int attempt, Exception? lastError)
        {
            if (_maxAttempts > 0 && attempt > _maxAttempts)
                return null;

            return _delay;
        }
    }
}