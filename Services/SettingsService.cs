using BossDamageLogger.Models;
using System.IO;
using System.Text.Json;

namespace BossDamageLogger.Services
{
    public sealed class SettingsService : ISettingsService
    {
        private readonly string _settingsFilePath;

        public SettingsService()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(localAppData, "EVRCBossLogReader");
            Directory.CreateDirectory(folder);
            _settingsFilePath = Path.Combine(folder, "settings.json");
        }

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                        return settings;
                }
            }
            catch
            {
                // Corrupt or unreadable settings file - fall back to defaults
                // rather than crashing the app over a preferences file.
            }

            return new AppSettings();
        }

        public void Save(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch
            {
                // Best-effort persistence only; a failed save shouldn't
                // interrupt the user's session.
            }
        }
    }
}
