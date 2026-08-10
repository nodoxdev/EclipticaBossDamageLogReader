namespace BossDamageLogger.Models
{
    public sealed class BarrelPhaseEntry
    {
        public string Name { get; }
        public DateTime StartTimestamp { get; }
        public DateTime EndTimestamp { get; }
        public long StrikeDamage { get; }
        public long NonStrikeDamage { get; }

        public long TotalDamage => StrikeDamage + NonStrikeDamage;
        public TimeSpan Duration => EndTimestamp - StartTimestamp;

        public double? Dps => Duration.TotalSeconds > 0
            ? TotalDamage / Duration.TotalSeconds
            : null;

        public BarrelPhaseEntry(string name, DateTime startTimestamp, DateTime endTimestamp, long strikeDamage, long nonStrikeDamage)
        {
            Name = name;
            StartTimestamp = startTimestamp;
            EndTimestamp = endTimestamp;
            StrikeDamage = strikeDamage;
            NonStrikeDamage = nonStrikeDamage;
        }
    }
}
