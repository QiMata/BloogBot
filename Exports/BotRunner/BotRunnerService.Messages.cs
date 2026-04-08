using Serilog;
using System;
using System.Linq;

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
                    Log.Warning("[BOT RUNNER] EventHandler is null -- cannot subscribe to message events");
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
            lock (_recentChatMessages)
            {
                while (_recentChatMessages.Count > 0)
                    _activitySnapshot.RecentChatMessages.Add(_recentChatMessages.Dequeue());

                if (_activitySnapshot.RecentChatMessages.Count > MaxBufferedMessages)
                {
                    var kept = _activitySnapshot.RecentChatMessages.Skip(
                        _activitySnapshot.RecentChatMessages.Count - MaxBufferedMessages).ToList();
                    _activitySnapshot.RecentChatMessages.Clear();
                    _activitySnapshot.RecentChatMessages.Add(kept);
                }
            }

            lock (_recentErrors)
            {
                while (_recentErrors.Count > 0)
                    _activitySnapshot.RecentErrors.Add(_recentErrors.Dequeue());

                if (_activitySnapshot.RecentErrors.Count > MaxBufferedMessages)
                {
                    var kept = _activitySnapshot.RecentErrors.Skip(
                        _activitySnapshot.RecentErrors.Count - MaxBufferedMessages).ToList();
                    _activitySnapshot.RecentErrors.Clear();
                    _activitySnapshot.RecentErrors.Add(kept);
                }
            }
        }

        private void EnqueueDiagnosticMessage(string message)
        {
            lock (_recentChatMessages)
            {
                _recentChatMessages.Enqueue(message);
                while (_recentChatMessages.Count > MaxBufferedMessages)
                    _recentChatMessages.Dequeue();
            }
        }
    }
}
