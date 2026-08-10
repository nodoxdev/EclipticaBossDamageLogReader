using BossDamageLogger.Models;

namespace BossDamageLogger.Services
{
    public interface ILogParserService
    {
        string GetDefaultLogFolder();

        LogParseResult ParseFolder(string folderPath);
    }
}
