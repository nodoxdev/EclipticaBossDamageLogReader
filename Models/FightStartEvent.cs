namespace BossDamageLogger.Models
{
    public sealed class FightStartEvent
    {
        public DateTime Timestamp { get; }

        public string BossName { get; }

        public double PhaseValue { get; }

        public FightStartEvent(DateTime timestamp, string bossName, double phaseValue)
        {
            Timestamp = timestamp;
            BossName = bossName;
            PhaseValue = phaseValue;
        }
    }
}
