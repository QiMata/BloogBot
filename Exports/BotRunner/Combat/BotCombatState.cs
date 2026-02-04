namespace BotRunner.Combat
{
    public class BotCombatState
    {
        private readonly HashSet<ulong> _lootedTargets = new();
        private readonly object _syncRoot = new();
        private ulong? _currentTargetGuid;

        public ulong? CurrentTargetGuid
        {
            get
            {
                lock (_syncRoot)
                {
                    return _currentTargetGuid;
                }
            }
        }

        public bool HasLooted(ulong targetGuid)
        {
            lock (_syncRoot)
            {
                return _lootedTargets.Contains(targetGuid);
            }
        }

        public void SetCurrentTarget(ulong targetGuid)
        {
            lock (_syncRoot)
            {
                _currentTargetGuid = targetGuid;
                _lootedTargets.Remove(targetGuid);
            }
        }

        public void ClearCurrentTarget(ulong targetGuid)
        {
            lock (_syncRoot)
            {
                if (_currentTargetGuid == targetGuid)
                {
                    _currentTargetGuid = null;
                }
            }
        }

        public bool TryMarkLooted(ulong targetGuid)
        {
            lock (_syncRoot)
            {
                if (!_lootedTargets.Add(targetGuid))
                {
                    return false;
                }

                if (_currentTargetGuid == targetGuid)
                {
                    _currentTargetGuid = null;
                }

                return true;
            }
        }
    }
}
