namespace BossDamageLogger.Models
{
    public sealed class LogParseResult
    {
        public IReadOnlyList<BossDamageEntry> BossKills { get; }
        public IReadOnlyList<ControllerDeadEvent> ControllerDeadEvents { get; }
        public IReadOnlyList<StartRunEvent> StartRunEvents { get; }
        public IReadOnlyList<BarrelPhaseEntry> BarrelPhases { get; }

        public LogParseResult(
            IReadOnlyList<BossDamageEntry> bossKills,
            IReadOnlyList<ControllerDeadEvent> controllerDeadEvents,
            IReadOnlyList<StartRunEvent> startRunEvents,
            IReadOnlyList<BarrelPhaseEntry> barrelPhases)
        {
            BossKills = bossKills;
            ControllerDeadEvents = controllerDeadEvents;
            StartRunEvents = startRunEvents;
            BarrelPhases = barrelPhases;
        }
    }
}
