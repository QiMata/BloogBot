using Serilog;
using System;

namespace BotRunner
{
    public partial class BotRunnerService
    {
        private void SubscribeToMessageEvents()
        {
            try
            {
                var eh = _objectManager.EventHandler;
                if (eh == null)
                {
                    Log.Warning("[BOT RUNNER] EventHandler is null — cannot subscribe to message events");
                    return;
                }

                Log.Information("[BOT RUNNER] Subscribing to EventHandler message events (type: {Type})", eh.GetType().Name);
                DiagLog($"SubscribeToMessageEvents: EventHandler type={eh.GetType().FullName}");

                eh.OnChatMessage += (_, args) =>
                {
                    var msg = $"[CHAT:{args.MsgType}] {args.SenderName}: {args.Text}";
                    lock (_recentChatMessages)
                    {
                        _recentChatMessages.Enqueue(msg);
                        while (_recentChatMessages.Count > MaxBufferedMessages)
                            _recentChatMessages.Dequeue();
                    }
                };

                eh.OnErrorMessage += (_, args) =>
                {
                    lock (_recentErrors)
                    {
                        _recentErrors.Enqueue($"[ERROR] {args.Message}");
                        while (_recentErrors.Count > MaxBufferedMessages)
                            _recentErrors.Dequeue();
                    }
                };

                eh.OnUiMessage += (_, args) =>
                {
                    lock (_recentChatMessages)
                    {
                        _recentChatMessages.Enqueue($"[UI] {args.Message}");
                        while (_recentChatMessages.Count > MaxBufferedMessages)
                            _recentChatMessages.Dequeue();
                    }
                };

                eh.OnSystemMessage += (_, args) =>
                {
                    lock (_recentChatMessages)
                    {
                        _recentChatMessages.Enqueue($"[SYSTEM] {args.Message}");
                        while (_recentChatMessages.Count > MaxBufferedMessages)
                            _recentChatMessages.Dequeue();
                    }
                };

                eh.OnSkillMessage += (_, args) =>
                {
                    lock (_recentChatMessages)
                    {
                        _recentChatMessages.Enqueue($"[SKILL] {args.Message}");
                        while (_recentChatMessages.Count > MaxBufferedMessages)
                            _recentChatMessages.Dequeue();
                    }
                };
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Failed to subscribe to message events: {ex.Message}");
            }
        }

        private void FlushMessageBuffers()
        {
            // Copy buffered messages to snapshot — keep a rolling window of the last MaxBufferedMessages.
            // Messages stay in the snapshot until displaced by newer ones (no clear-per-tick).
            lock (_recentChatMessages)
            {
                while (_recentChatMessages.Count > 0)
                {
                    _activitySnapshot.RecentChatMessages.Add(_recentChatMessages.Dequeue());
                    // Trim to rolling window
                    while (_activitySnapshot.RecentChatMessages.Count > MaxBufferedMessages)
                        _activitySnapshot.RecentChatMessages.RemoveAt(0);
                }
            }

            lock (_recentErrors)
            {
                while (_recentErrors.Count > 0)
                {
                    _activitySnapshot.RecentErrors.Add(_recentErrors.Dequeue());
                    while (_activitySnapshot.RecentErrors.Count > MaxBufferedMessages)
                        _activitySnapshot.RecentErrors.RemoveAt(0);
                }
            }
        }
    }
}
