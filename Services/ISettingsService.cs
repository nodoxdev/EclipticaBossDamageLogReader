using BossDamageLogger.Models;

namespace BossDamageLogger.Services
{
    public interface ISettingsService
    {
        AppSettings Load();
        void Save(AppSettings settings);
    }
}
