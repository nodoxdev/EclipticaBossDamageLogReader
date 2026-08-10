namespace BossDamageLogger.Models
{
    public enum LogEntryKind
    {
        BossKill,
        ControllerDead,
        BarrelPhase
    }

    public sealed class LogListEntry
    {
        public LogEntryKind Kind { get; }
        public DateTime Timestamp { get; }
        public string Name { get; }
        public long? StrikeDamage { get; }
        public long? NonStrikeDamage { get; }
        public double? Dps { get; }
        public TimeSpan? FightDuration { get; }
        public double? StartPhase { get; }

        public long? TotalDamage => StrikeDamage.HasValue && NonStrikeDamage.HasValue
            ? StrikeDamage.Value + NonStrikeDamage.Value
            : null;

        public string? DurationDisplay => FightDuration is { } d
            ? (d.TotalHours >= 1 ? d.ToString(@"hh\:mm\:ss") : d.ToString(@"mm\:ss"))
            : null;

        public string? StartPhaseDisplay => StartPhase is { } p ? $"{p * 100:0.0}%" : null;

        private LogListEntry(LogEntryKind kind, DateTime timestamp, string name, long? strikeDamage, long? nonStrikeDamage,
            double? dps, TimeSpan? fightDuration, double? startPhase)
        {
            Kind = kind;
            Timestamp = timestamp;
            Name = name;
            StrikeDamage = strikeDamage;
            NonStrikeDamage = nonStrikeDamage;
            Dps = dps;
            FightDuration = fightDuration;
            StartPhase = startPhase;
        }

        public static LogListEntry FromBossKill(BossDamageEntry entry) =>
            new(LogEntryKind.BossKill, entry.Timestamp, entry.BossName, entry.StrikeDamage, entry.NonStrikeDamage,
                entry.Dps, entry.FightDuration, entry.StartPhase);

        public static LogListEntry FromControllerDead(ControllerDeadEvent evt) =>
            new(LogEntryKind.ControllerDead, evt.Timestamp, "Player Dead", null, null, null, null, null);

        public static LogListEntry FromStartRun(StartRunEvent evt) =>
            new(LogEntryKind.ControllerDead, evt.Timestamp, "Starting the run as " + evt.ClassName, null, null, null, null, null);

        public static LogListEntry From(StartRunEvent evt) =>
            new(LogEntryKind.ControllerDead, evt.Timestamp, "Starting the run as " + evt.ClassName, null, null, null, null, null);
        public static LogListEntry FromBarrelPhase(BarrelPhaseEntry entry) =>
            new(LogEntryKind.BarrelPhase, entry.EndTimestamp, entry.Name, entry.StrikeDamage, entry.NonStrikeDamage,
                entry.Dps, entry.Duration, null);
    }
}
