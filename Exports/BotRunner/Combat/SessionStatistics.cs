using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace BotRunner.Combat
{
    /// <summary>
    /// Tracks per-session bot statistics: kills, deaths, XP, gold, loot.
    /// Also monitors for alert conditions (death loops, frequent disconnects, stuck).
    /// Thread-safe — can be updated from any thread.
    /// </summary>
    public class SessionStatistics
    {
        private readonly object _lock = new();
        private readonly DateTime _sessionStart = DateTime.Now;

        // Combat stats
        public int Kills { get; private set; }
        public int Deaths { get; private set; }
        public int MobsLooted { get; private set; }

        // Economy
        public int CopperLooted { get; private set; }
        public int CopperFromVendor { get; private set; }
        public int ItemsLooted { get; private set; }
        public int ItemsSold { get; private set; }
        public int ItemsCrafted { get; private set; }

        // Progression
        public int XpGained { get; private set; }
        public int SkillUps { get; private set; }
        public int QuestsCompleted { get; private set; }
        public int NodesGathered { get; private set; }

        // Movement
        public float DistanceTraveled { get; private set; }

        // Alert tracking
        public int StuckCount { get; private set; }
        public int DisconnectCount { get; private set; }
        private readonly List<DateTime> _deathTimes = new();
        private readonly List<DateTime> _disconnectTimes = new();
        private readonly List<DateTime> _stuckTimes = new();

        // Alert thresholds
        private const int DeathLoopThreshold = 3;
        private static readonly TimeSpan DeathLoopWindow = TimeSpan.FromMinutes(2);
        private const int DisconnectAlertThreshold = 2;
        private static readonly TimeSpan DisconnectAlertWindow = TimeSpan.FromMinutes(5);
        private const int StuckAlertThreshold = 3;
        private static readonly TimeSpan StuckAlertWindow = TimeSpan.FromMinutes(5);

        // Computed rates
        public TimeSpan SessionDuration => DateTime.Now - _sessionStart;
        public double KillsPerHour => SessionDuration.TotalHours > 0 ? Kills / SessionDuration.TotalHours : 0;
        public double DeathsPerHour => SessionDuration.TotalHours > 0 ? Deaths / SessionDuration.TotalHours : 0;
        public double XpPerHour => SessionDuration.TotalHours > 0 ? XpGained / SessionDuration.TotalHours : 0;
        public double GoldPerHour => SessionDuration.TotalHours > 0 ? (CopperLooted + CopperFromVendor) / 10000.0 / SessionDuration.TotalHours : 0;

        public void RecordKill() { lock (_lock) Kills++; }

        public void RecordDeath()
        {
            lock (_lock)
            {
                Deaths++;
                _deathTimes.Add(DateTime.Now);
                PruneTimestamps(_deathTimes, DeathLoopWindow);
            }
        }

        public void RecordLoot(int copperAmount, int itemCount)
        {
            lock (_lock)
            {
                MobsLooted++;
                CopperLooted += copperAmount;
                ItemsLooted += itemCount;
            }
        }
        public void RecordVendorSale(int copperAmount, int itemCount)
        {
            lock (_lock)
            {
                CopperFromVendor += copperAmount;
                ItemsSold += itemCount;
            }
        }
        public void RecordXpGain(int xp) { lock (_lock) XpGained += xp; }
        public void RecordSkillUp() { lock (_lock) SkillUps++; }
        public void RecordQuestComplete() { lock (_lock) QuestsCompleted++; }
        public void RecordGather() { lock (_lock) NodesGathered++; }
        public void RecordCraft(int count) { lock (_lock) ItemsCrafted += count; }
        public void RecordDistance(float distance) { lock (_lock) DistanceTraveled += distance; }

        public void RecordStuck()
        {
            lock (_lock)
            {
                StuckCount++;
                _stuckTimes.Add(DateTime.Now);
                PruneTimestamps(_stuckTimes, StuckAlertWindow);
            }
        }

        public void RecordDisconnect()
        {
            lock (_lock)
            {
                DisconnectCount++;
                _disconnectTimes.Add(DateTime.Now);
                PruneTimestamps(_disconnectTimes, DisconnectAlertWindow);
            }
        }

        /// <summary>
        /// Returns true if the bot has died 3+ times within the last 2 minutes.
        /// </summary>
        public bool IsDeathLooping
        {
            get
            {
                lock (_lock)
                {
                    PruneTimestamps(_deathTimes, DeathLoopWindow);
                    return _deathTimes.Count >= DeathLoopThreshold;
                }
            }
        }

        /// <summary>
        /// Returns true if the bot has disconnected 2+ times within the last 5 minutes.
        /// </summary>
        public bool IsFrequentlyDisconnecting
        {
            get
            {
                lock (_lock)
                {
                    PruneTimestamps(_disconnectTimes, DisconnectAlertWindow);
                    return _disconnectTimes.Count >= DisconnectAlertThreshold;
                }
            }
        }

        /// <summary>
        /// Returns true if the bot has been stuck 3+ times within the last 5 minutes.
        /// </summary>
        public bool IsFrequentlyStuck
        {
            get
            {
                lock (_lock)
                {
                    PruneTimestamps(_stuckTimes, StuckAlertWindow);
                    return _stuckTimes.Count >= StuckAlertThreshold;
                }
            }
        }

        /// <summary>
        /// Checks all alert conditions and logs ERROR-level messages for any active alerts.
        /// Returns true if any alert is active.
        /// </summary>
        public bool CheckAlerts()
        {
            bool hasAlerts = false;

            if (IsDeathLooping)
            {
                Log.Error("[ALERT] Death loop detected — {Count} deaths in last {Window} minutes. Bot may be in a deadly zone.",
                    DeathLoopThreshold, DeathLoopWindow.TotalMinutes);
                hasAlerts = true;
            }

            if (IsFrequentlyDisconnecting)
            {
                Log.Error("[ALERT] Frequent disconnects — {Count} disconnects in last {Window} minutes.",
                    DisconnectAlertThreshold, DisconnectAlertWindow.TotalMinutes);
                hasAlerts = true;
            }

            if (IsFrequentlyStuck)
            {
                Log.Error("[ALERT] Frequently stuck — {Count} stuck events in last {Window} minutes. Bot may need repositioning.",
                    StuckAlertThreshold, StuckAlertWindow.TotalMinutes);
                hasAlerts = true;
            }

            return hasAlerts;
        }

        /// <summary>
        /// Logs a summary of session statistics.
        /// </summary>
        public void LogSummary()
        {
            var duration = SessionDuration;
            Log.Information(
                "[STATS] Session {Duration:hh\\:mm\\:ss} | Kills: {Kills} ({KPH:F0}/hr) | Deaths: {Deaths} | " +
                "XP: {XP} ({XPH:F0}/hr) | Gold: {Gold:F1}g ({GPH:F1}g/hr) | " +
                "Looted: {Looted} items | Gathered: {Gathered} | Crafted: {Crafted}",
                duration, Kills, KillsPerHour, Deaths,
                XpGained, XpPerHour,
                (CopperLooted + CopperFromVendor) / 10000.0, GoldPerHour,
                ItemsLooted, NodesGathered, ItemsCrafted);

            CheckAlerts();
        }

        private static void PruneTimestamps(List<DateTime> timestamps, TimeSpan window)
        {
            var cutoff = DateTime.Now - window;
            timestamps.RemoveAll(t => t < cutoff);
        }
    }
}
