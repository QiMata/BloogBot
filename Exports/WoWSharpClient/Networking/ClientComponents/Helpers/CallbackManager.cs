using System;
using System.Collections.Generic;

namespace WoWSharpClient.Networking.ClientComponents.Helpers
{
    /// <summary>
    /// Helper class for managing callback chains and temporary callback subscriptions.
    /// Allows multiple callbacks to be chained together and provides a mechanism for
    /// temporary callback registration that can be safely removed.
    /// </summary>
    /// <typeparam name="T">The type of parameter passed to the callback.</typeparam>
    public class CallbackManager<T>
    {
        private Action<T>? _permanentCallback;
        private readonly List<Action<T>> _temporaryCallbacks = [];
        private readonly object _lock = new();

        /// <summary>
        /// Sets the permanent callback that will always be invoked.
        /// </summary>
        /// <param name="callback">The permanent callback to set.</param>
        public void SetPermanentCallback(Action<T>? callback)
        {
            lock (_lock)
            {
                _permanentCallback = callback;
            }
        }

        /// <summary>
        /// Adds a temporary callback that can be removed later.
        /// </summary>
        /// <param name="callback">The temporary callback to add.</param>
        /// <returns>A disposable that, when disposed, removes the temporary callback.</returns>
        public IDisposable AddTemporaryCallback(Action<T> callback)
        {
            lock (_lock)
            {
                _temporaryCallbacks.Add(callback);
                return new CallbackUnsubscriber(this, callback);
            }
        }

        /// <summary>
        /// Invokes all registered callbacks (permanent and temporary) with the given parameter.
        /// </summary>
        /// <param name="parameter">The parameter to pass to all callbacks.</param>
        public void InvokeCallbacks(T parameter)
        {
            Action<T>? permanentCallback;
            List<Action<T>> temporaryCallbacks;

            lock (_lock)
            {
                permanentCallback = _permanentCallback;
                temporaryCallbacks = new List<Action<T>>(_temporaryCallbacks);
            }

            // Invoke permanent callback first
            permanentCallback?.Invoke(parameter);

            // Then invoke all temporary callbacks
            foreach (var callback in temporaryCallbacks)
            {
                try
                {
                    callback(parameter);
                }
                catch (Exception)
                {
                    // Log error but continue with other callbacks
                    // In a real implementation, you might want to use a logger here
                }
            }
        }

        private void RemoveTemporaryCallback(Action<T> callback)
        {
            lock (_lock)
            {
                _temporaryCallbacks.Remove(callback);
            }
        }

        private class CallbackUnsubscriber(CallbackManager<T> manager, Action<T> callback) : IDisposable
        {
            private readonly CallbackManager<T> _manager = manager;
            private readonly Action<T> _callback = callback;
            private bool _disposed = false;

            public void Dispose()
            {
                if (!_disposed)
                {
                    _manager.RemoveTemporaryCallback(_callback);
                    _disposed = true;
                }
            }
        }
    }

    /// <summary>
    /// Helper class for managing callbacks with no parameters.
    /// </summary>
    public class CallbackManager
    {
        private Action? _permanentCallback;
        private readonly List<Action> _temporaryCallbacks = [];
        private readonly object _lock = new();

        /// <summary>
        /// Sets the permanent callback that will always be invoked.
        /// </summary>
        /// <param name="callback">The permanent callback to set.</param>
        public void SetPermanentCallback(Action? callback)
        {
            lock (_lock)
            {
                _permanentCallback = callback;
            }
        }

        /// <summary>
        /// Adds a temporary callback that can be removed later.
        /// </summary>
        /// <param name="callback">The temporary callback to add.</param>
        /// <returns>A disposable that, when disposed, removes the temporary callback.</returns>
        public IDisposable AddTemporaryCallback(Action callback)
        {
            lock (_lock)
            {
                _temporaryCallbacks.Add(callback);
                return new CallbackUnsubscriber(this, callback);
            }
        }

        /// <summary>
        /// Invokes all registered callbacks (permanent and temporary).
        /// </summary>
        public void InvokeCallbacks()
        {
            Action? permanentCallback;
            List<Action> temporaryCallbacks;

            lock (_lock)
            {
                permanentCallback = _permanentCallback;
                temporaryCallbacks = new List<Action>(_temporaryCallbacks);
            }

            // Invoke permanent callback first
            permanentCallback?.Invoke();

            // Then invoke all temporary callbacks
            foreach (var callback in temporaryCallbacks)
            {
                try
                {
                    callback();
                }
                catch (Exception)
                {
                    // Log error but continue with other callbacks
                }
            }
        }

        private void RemoveTemporaryCallback(Action callback)
        {
            lock (_lock)
            {
                _temporaryCallbacks.Remove(callback);
            }
        }

        private class CallbackUnsubscriber(CallbackManager manager, Action callback) : IDisposable
        {
            private readonly CallbackManager _manager = manager;
            private readonly Action _callback = callback;
            private bool _disposed = false;

            public void Dispose()
            {
                if (!_disposed)
                {
                    _manager.RemoveTemporaryCallback(_callback);
                    _disposed = true;
                }
            }
        }
    }
}