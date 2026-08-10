using System.Windows;

namespace BossDamageLogger.Services
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    public static class ThemeService
    {
        private const string LightThemeUri = "Themes/LightTheme.xaml";
        private const string DarkThemeUri = "Themes/DarkTheme.xaml";

        public static void Apply(AppTheme theme)
        {
            var app = Application.Current;
            if (app is null)
                return;

            string targetUri = theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri;
            var newDictionary = new ResourceDictionary { Source = new Uri(targetUri, UriKind.Relative) };

            var merged = app.Resources.MergedDictionaries;

            var existingThemeDictionaries = merged
                .Where(d => d.Source != null &&
                            (d.Source.OriginalString.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
                             d.Source.OriginalString.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var dictionary in existingThemeDictionaries)
            {
                merged.Remove(dictionary);
            }

            merged.Add(newDictionary);
        }
    }
}
