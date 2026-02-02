using System;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Abstract base class for network client components providing common
    /// operation state tracking and lifetime management.
    /// </summary>
    public abstract class NetworkClientComponent : INetworkClientComponent
    {
        private readonly object _stateLock = new();
        private bool _isOperationInProgress;
        private DateTime? _lastOperationTime;
        private bool _disposed;

        /// <summary>
        /// Gets a value indicating whether an operation is currently in progress.
        /// </summary>
        public bool IsOperationInProgress
        {
            get
            {
                lock (_stateLock)
                {
                    return _isOperationInProgress;
                }
            }
        }

        /// <summary>
        /// Gets the timestamp of the last operation started.
        /// </summary>
        public DateTime? LastOperationTime
        {
            get
            {
                lock (_stateLock)
                {
                    return _lastOperationTime;
                }
            }
        }

        /// <summary>
        /// Sets the operation-in-progress flag and updates the last operation time when starting.
        /// </summary>
        /// <param name="inProgress">True if an operation is starting, false if it has finished.</param>
        protected void SetOperationInProgress(bool inProgress)
        {
            lock (_stateLock)
            {
                _isOperationInProgress = inProgress;
                if (inProgress)
                {
                    _lastOperationTime = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Disposes this component. Derived classes should override to clean up resources
        /// and then call base.Dispose().
        /// </summary>
        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
