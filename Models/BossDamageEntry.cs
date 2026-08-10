namespace BossDamageLogger.Models
{
    public sealed class BossDamageEntry
    {
        public DateTime Timestamp { get; }
        public string BossName { get; }
        public long StrikeDamage { get; }
        public long NonStrikeDamage { get; }
        public long TotalDamage => StrikeDamage + NonStrikeDamage;

        public DateTime? FightStartTimestamp { get; }

        public double? StartPhase { get; }

        public TimeSpan? FightDuration => FightStartTimestamp is { } start && start < Timestamp
            ? Timestamp - start
            : null;

        public double? Dps => FightDuration is { } duration && duration.TotalSeconds > 0
            ? TotalDamage / duration.TotalSeconds
            : null;

        public BossDamageEntry(DateTime timestamp, string bossName, long strikeDamage, long nonStrikeDamage,
            DateTime? fightStartTimestamp = null, double? startPhase = null)
        {
            Timestamp = timestamp;
            BossName = bossName;
            StrikeDamage = strikeDamage;
            NonStrikeDamage = nonStrikeDamage;
            FightStartTimestamp = fightStartTimestamp;
            StartPhase = startPhase;
        }
    }
}
