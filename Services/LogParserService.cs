using BossDamageLogger.Models;
using System.Globalization;
using System.IO;

namespace BossDamageLogger.Services
{
    public sealed class LogParserService : ILogParserService
    {
        private const string BossMarker = "Boss ";
        private const string BossSuffix = " dead, personal damage dealt:";
        private const string StrikeMarker = "STRIKE DMG:";
        private const string NonStrikeMarker = "NON-STRIKE DMG:";
        private const string ControllerDeadMarker = "Local controller dead, switching off.";
        private const string FightStartMarker = "now fighting boss: ";
        private const string PhaseMarker = " on phase: ";
        private const string CloneSuffix = "(Clone)";
        private const string StartRunMarker = "ECLIPTICA - now in stage: Stage_Hall of Beginnings on phase: 0 as class:";

        private const string BarrelPhaseName = "JimBarrel";
        private const string BarrelStartMarker = "Initializing Enemy POOL ID0 as ENEMY ID 86";
        private const string BarrelEndMarker = "Retiring Enemy POOL ID0";
        private const string DealingPrefix = "Dealing ";
        private const string DealingNonStrikeSuffix = "NON-STRIKE damage";
        private const string DealingStrikeSuffix = "STRIKE damage";

        private const int TimestampLength = 19; // "yyyy.MM.dd HH:mm:ss"
        private const string TimestampFormat = "yyyy.MM.dd HH:mm:ss";

        private const int LookaheadLines = 6;

        private static readonly TimeSpan BossKillDedupeWindow = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan ControllerDeadDedupeWindow = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan FightStartDedupeWindow = TimeSpan.FromSeconds(10);

        private sealed class BarrelPhaseState
        {
            public bool IsOpen { get; private set; }
            private DateTime _startTimestamp;
            private long _strikeDamage;
            private long _nonStrikeDamage;

            public void Open(DateTime timestamp)
            {
                IsOpen = true;
                _startTimestamp = timestamp;
                _strikeDamage = 0;
                _nonStrikeDamage = 0;
            }

            public void AddDamage(long amount, bool isStrike)
            {
                if (isStrike)
                    _strikeDamage += amount;
                else
                    _nonStrikeDamage += amount;
            }

            public BarrelPhaseEntry Close(DateTime endTimestamp)
            {
                var entry = new BarrelPhaseEntry(BarrelPhaseName, _startTimestamp, endTimestamp, _strikeDamage, _nonStrikeDamage);
                IsOpen = false;
                return entry;
            }
        }

        public string GetDefaultLogFolder()
        {
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string userAppDataRoot = Directory.GetParent(roaming)?.FullName
                                      ?? throw new DirectoryNotFoundException("Could not resolve the AppData root folder.");

            return Path.Combine(userAppDataRoot, "LocalLow", "VRChat", "VRChat");
        }

        public LogParseResult ParseFolder(string folderPath)
        {
            var allBossKills = new List<BossDamageEntry>();
            var allControllerDeadEvents = new List<ControllerDeadEvent>();
            var allFightStartEvents = new List<FightStartEvent>();
            var allStartRunEvents = new List<StartRunEvent>();
            var allBarrelPhases = new List<BarrelPhaseEntry>();
            var barrelState = new BarrelPhaseState();

            if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
            {
                var logFiles = Directory.GetFiles(folderPath, "output_log_*.txt")
                                         .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

                foreach (var file in logFiles)
                {
                    var (bossKills, controllerDeadEvents, fightStartEvents, startRunEvents, barrelPhases) = ParseFile(file, barrelState);
                    allBossKills.AddRange(bossKills);
                    allControllerDeadEvents.AddRange(controllerDeadEvents);
                    allFightStartEvents.AddRange(fightStartEvents);
                    allStartRunEvents.AddRange(startRunEvents);
                    allBarrelPhases.AddRange(barrelPhases);
                }
            }

            var chronologicalBossKills = allBossKills.OrderBy(e => e.Timestamp).ToList();
            var dedupedBossKills = DedupeAndFilterBossKills(chronologicalBossKills);

            var chronologicalControllerDeadEvents = allControllerDeadEvents.OrderBy(e => e.Timestamp).ToList();
            var dedupedControllerDeadEvents = DedupeControllerDeadEvents(chronologicalControllerDeadEvents);

            var chronologicalFightStartEvents = allFightStartEvents.OrderBy(e => e.Timestamp).ToList();
            var dedupedFightStartEvents = DedupeFightStartEvents(chronologicalFightStartEvents);

            var chronologicalStartRunEvents = allStartRunEvents.OrderBy(e => e.Timestamp).ToList();
            var dedupedStartRunEvents = DedupeStartRunEvents(chronologicalStartRunEvents);

            var bossKillsWithDps = AttachFightStartTimestamps(dedupedBossKills, dedupedFightStartEvents);

            var chronologicalBarrelPhases = allBarrelPhases.OrderBy(e => e.EndTimestamp).ToList();

            return new LogParseResult(bossKillsWithDps, dedupedControllerDeadEvents, dedupedStartRunEvents, chronologicalBarrelPhases);
        }

        private static (List<BossDamageEntry> BossKills, List<ControllerDeadEvent> ControllerDeadEvents,
            List<FightStartEvent> FightStartEvents, List<StartRunEvent> StartRunEvents, List<BarrelPhaseEntry> BarrelPhases) ParseFile(string filePath, BarrelPhaseState barrelState)
        {
            var bossKills = new List<BossDamageEntry>();
            var controllerDeadEvents = new List<ControllerDeadEvent>();
            var fightStartEvents = new List<FightStartEvent>();
            var startRunEvents = new List<StartRunEvent>();
            var barrelPhases = new List<BarrelPhaseEntry>();

            List<string> lines;
            try
            {
                lines = ReadAllLinesShared(filePath);
            }
            catch (IOException)
            {
                return (bossKills, controllerDeadEvents, fightStartEvents, startRunEvents, barrelPhases);
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (TryParseBossLine(lines[i], out var bossTimestamp, out var bossName))
                {
                    long? strikeDamage = null;
                    long? nonStrikeDamage = null;

                    int end = Math.Min(i + LookaheadLines, lines.Count - 1);
                    for (int j = i + 1; j <= end && (strikeDamage is null || nonStrikeDamage is null); j++)
                    {
                        if (nonStrikeDamage is null && TryParseDamageValue(lines[j], NonStrikeMarker, out var nonStrikeVal))
                        {
                            nonStrikeDamage = nonStrikeVal;
                            continue;
                        }

                        if (strikeDamage is null && TryParseDamageValue(lines[j], StrikeMarker, out var strikeVal))
                        {
                            strikeDamage = strikeVal;
                        }
                    }

                    if (strikeDamage is not null && nonStrikeDamage is not null)
                    {
                        bossKills.Add(new BossDamageEntry(bossTimestamp, bossName, strikeDamage.Value, nonStrikeDamage.Value));
                    }

                    continue;
                }

                if (TryParseControllerDeadLine(lines[i], out var deadTimestamp))
                {
                    controllerDeadEvents.Add(new ControllerDeadEvent(deadTimestamp));
                    continue;
                }

                if (TryParseFightStartLine(lines[i], out var startTimestamp, out var startBossName, out var phaseValue))
                {
                    fightStartEvents.Add(new FightStartEvent(startTimestamp, startBossName, phaseValue));
                    continue;
                }

                if (TryParseStartRunLine(lines[i], out var startRunEvent, out var startRunEventClass))
                {

                    startRunEvents.Add(new StartRunEvent(startRunEvent, startRunEventClass));
                    continue;
                }

                if (!barrelState.IsOpen && TryParseBarrelStartLine(lines[i], out var barrelStartTimestamp))
                {
                    barrelState.Open(barrelStartTimestamp);
                    continue;
                }

                if (barrelState.IsOpen && TryParseBarrelEndLine(lines[i], out var barrelEndTimestamp))
                {
                    barrelPhases.Add(barrelState.Close(barrelEndTimestamp));
                    continue;
                }

                if (barrelState.IsOpen && TryParseDealingDamageLine(lines[i], out var dealtAmount, out var isStrikeHit))
                {
                    barrelState.AddDamage(dealtAmount, isStrikeHit);
                }
            }

            return (bossKills, controllerDeadEvents, fightStartEvents, startRunEvents, barrelPhases);
        }

        private static bool TryParseBossLine(string line, out DateTime timestamp, out string bossName)
        {
            timestamp = default;
            bossName = string.Empty;

            int suffixIndex = line.IndexOf(BossSuffix, StringComparison.Ordinal);
            if (suffixIndex < 0)
                return false;

            int bossIndex = line.IndexOf(BossMarker, StringComparison.Ordinal);
            if (bossIndex < 0 || bossIndex + BossMarker.Length > suffixIndex)
                return false;

            if (!TryParseLeadingTimestamp(line, out timestamp))
                return false;

            int nameStart = bossIndex + BossMarker.Length;
            bossName = line.Substring(nameStart, suffixIndex - nameStart).Trim();
            return !string.IsNullOrEmpty(bossName);
        }

        private static bool TryParseControllerDeadLine(string line, out DateTime timestamp)
        {
            timestamp = default;

            if (line.IndexOf(ControllerDeadMarker, StringComparison.Ordinal) < 0)
                return false;

            return TryParseLeadingTimestamp(line, out timestamp);
        }

        private static bool TryParseStartRunLine(string line, out DateTime timestamp, out string className)
        {
            timestamp = default;
            className = string.Empty;

            if (!TryParseLeadingTimestamp(line, out timestamp))
                return false;

            int markerIndex = line.IndexOf(StartRunMarker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return false;

            className = line.Substring(markerIndex + StartRunMarker.Length).Trim();

            return true;
        }

        private static bool TryParseFightStartLine(string line, out DateTime timestamp, out string bossName, out double phaseValue)
        {
            timestamp = default;
            bossName = string.Empty;
            phaseValue = 0;

            int markerIndex = line.IndexOf(FightStartMarker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return false;

            int nameStart = markerIndex + FightStartMarker.Length;
            int phaseIndex = line.IndexOf(PhaseMarker, nameStart, StringComparison.Ordinal);
            if (phaseIndex < 0)
                return false;

            if (!TryParseLeadingTimestamp(line, out timestamp))
                return false;

            string rawName = line.Substring(nameStart, phaseIndex - nameStart).Trim();
            bossName = NormalizeBossName(rawName);
            if (string.IsNullOrEmpty(bossName))
                return false;

            var phaseSpan = line.AsSpan(phaseIndex + PhaseMarker.Length).Trim();
            double.TryParse(phaseSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out phaseValue);

            return true;
        }

        private static string NormalizeBossName(string rawName)
        {
            int cloneIndex = rawName.IndexOf(CloneSuffix, StringComparison.Ordinal);
            return cloneIndex >= 0 ? rawName.Substring(0, cloneIndex).Trim() : rawName;
        }

        private static bool TryParseBarrelStartLine(string line, out DateTime timestamp)
        {
            timestamp = default;

            int index = line.IndexOf(BarrelStartMarker, StringComparison.Ordinal);
            if (index < 0)
                return false;

            int afterIndex = index + BarrelStartMarker.Length;
            if (afterIndex < line.Length && char.IsDigit(line[afterIndex]))
                return false;

            return TryParseLeadingTimestamp(line, out timestamp);
        }

        private static bool TryParseBarrelEndLine(string line, out DateTime timestamp)
        {
            timestamp = default;

            int index = line.IndexOf(BarrelEndMarker, StringComparison.Ordinal);
            if (index < 0)
                return false;

            int afterIndex = index + BarrelEndMarker.Length;
            if (afterIndex < line.Length && char.IsDigit(line[afterIndex]))
                return false;

            return TryParseLeadingTimestamp(line, out timestamp);
        }

        private static bool TryParseDealingDamageLine(string line, out long amount, out bool isStrike)
        {
            amount = 0;
            isStrike = false;

            int prefixIndex = line.IndexOf(DealingPrefix, StringComparison.Ordinal);
            if (prefixIndex < 0)
                return false;

            int numberStart = prefixIndex + DealingPrefix.Length;

            int suffixIndex = line.IndexOf(DealingNonStrikeSuffix, numberStart, StringComparison.Ordinal);
            if (suffixIndex >= 0)
            {
                isStrike = false;
            }
            else
            {
                suffixIndex = line.IndexOf(DealingStrikeSuffix, numberStart, StringComparison.Ordinal);
                if (suffixIndex < 0)
                    return false;

                isStrike = true;
            }

            var numberSpan = line.AsSpan(numberStart, suffixIndex - numberStart).Trim();
            return long.TryParse(numberSpan, NumberStyles.None, CultureInfo.InvariantCulture, out amount);
        }

        private static bool TryParseLeadingTimestamp(string line, out DateTime timestamp)
        {
            timestamp = default;

            if (line.Length < TimestampLength)
                return false;

            return DateTime.TryParseExact(line.AsSpan(0, TimestampLength), TimestampFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);
        }

        private static bool TryParseDamageValue(string line, string marker, out long value)
        {
            value = 0;

            int markerIndex = line.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return false;

            var remainder = line.AsSpan(markerIndex + marker.Length).Trim();
            return long.TryParse(remainder, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        private static List<string> ReadAllLinesShared(string filePath)
        {
            var result = new List<string>();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                result.Add(line);
            }
            return result;
        }

        private static List<BossDamageEntry> DedupeAndFilterBossKills(List<BossDamageEntry> chronologicalEntries)
        {
            var result = new List<BossDamageEntry>();
            DateTime? lastKeptTimestamp = null;

            foreach (var entry in chronologicalEntries)
            {
                if (entry.StrikeDamage <= 0)
                    continue;

                if (lastKeptTimestamp is null || entry.Timestamp - lastKeptTimestamp.Value >= BossKillDedupeWindow)
                {
                    result.Add(entry);
                    lastKeptTimestamp = entry.Timestamp;
                }
            }

            return result;
        }

        private static List<ControllerDeadEvent> DedupeControllerDeadEvents(List<ControllerDeadEvent> chronologicalEvents)
        {
            var result = new List<ControllerDeadEvent>();
            DateTime? lastKeptTimestamp = null;

            foreach (var evt in chronologicalEvents)
            {
                if (lastKeptTimestamp is null || evt.Timestamp - lastKeptTimestamp.Value >= ControllerDeadDedupeWindow)
                {
                    result.Add(evt);
                    lastKeptTimestamp = evt.Timestamp;
                }
            }

            return result;
        }

        private static List<FightStartEvent> DedupeFightStartEvents(List<FightStartEvent> chronologicalEvents)
        {
            var result = new List<FightStartEvent>();
            var lastKeptByName = new Dictionary<string, DateTime>(StringComparer.Ordinal);

            foreach (var evt in chronologicalEvents)
            {
                if (lastKeptByName.TryGetValue(evt.BossName, out var lastKept) &&
                    evt.Timestamp - lastKept < FightStartDedupeWindow)
                {
                    continue;
                }

                result.Add(evt);
                lastKeptByName[evt.BossName] = evt.Timestamp;
            }

            return result;
        }

        private static List<StartRunEvent> DedupeStartRunEvents(List<StartRunEvent> chronologicalEvents)
        {
            var result = new List<StartRunEvent>();
            var lastKeptByName = new Dictionary<string, DateTime>(StringComparer.Ordinal);

            foreach (var evt in chronologicalEvents)
            {
                if (lastKeptByName.TryGetValue(evt.ClassName, out var lastKept) &&
                    evt.Timestamp - lastKept < FightStartDedupeWindow)
                {
                    continue;
                }

                result.Add(evt);
                lastKeptByName[evt.ClassName] = evt.Timestamp;
            }

            return result;
        }

        private static List<BossDamageEntry> AttachFightStartTimestamps(List<BossDamageEntry> bossKills, List<FightStartEvent> fightStartEvents)
        {
            var startsByName = fightStartEvents
                .GroupBy(e => e.BossName, StringComparer.Ordinal)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(e => e.Timestamp).ToList(),
                    StringComparer.Ordinal);

            var result = new List<BossDamageEntry>(bossKills.Count);

            foreach (var kill in bossKills)
            {
                FightStartEvent? matchedStart = null;

                if (startsByName.TryGetValue(kill.BossName, out var candidates))
                {
                    for (int i = candidates.Count - 1; i >= 0; i--)
                    {
                        if (candidates[i].Timestamp <= kill.Timestamp)
                        {
                            matchedStart = candidates[i];
                            break;
                        }
                    }
                }

                result.Add(new BossDamageEntry(kill.Timestamp, kill.BossName, kill.StrikeDamage, kill.NonStrikeDamage,
                    matchedStart?.Timestamp, matchedStart?.PhaseValue));
            }

            return result;
        }
    }
}
