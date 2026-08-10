namespace BossDamageLogger.Models
{
    public sealed class ControllerDeadEvent
    {
        public DateTime Timestamp { get; }

        public ControllerDeadEvent(DateTime timestamp)
        {
            Timestamp = timestamp;
        }
    }
}
