namespace BossDamageLogger.Models
{
    public sealed class StartRunEvent
    {
        public DateTime Timestamp { get; }

        public string ClassName { get; }

        public StartRunEvent(DateTime timestamp, string className)
        {
            Timestamp = timestamp;
            ClassName = className;
        }
    }
}
