using BossDamageLogger.Models;
using BossDamageLogger.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;

namespace BossDamageLogger.ViewModels
{
    public sealed class MainViewModel : ViewModelBase
    {
        private readonly ILogParserService _parserService;
        private readonly ISettingsService _settingsService;
        private readonly Dispatcher _dispatcher;
        private FileSystemWatcher? _watcher;
        private DispatcherTimer? _debounceTimer;

        public ObservableCollection<LogListEntry> Entries { get; } = new();

        private string _logFolder = string.Empty;
        public string LogFolder
        {
            get => _logFolder;
            private set => SetField(ref _logFolder, value);
        }

        private string _statusText = "Ready.";
        public string StatusText
        {
            get => _statusText;
            private set => SetField(ref _statusText, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set => SetField(ref _isBusy, value);
        }

        private int _bossCount;
        public int BossCount
        {
            get => _bossCount;
            private set => SetField(ref _bossCount, value);
        }

        private int _controllerResetCount;
        public int ControllerResetCount
        {
            get => _controllerResetCount;
            private set => SetField(ref _controllerResetCount, value);
        }

        private int _barrelPhaseCount;
        public int BarrelPhaseCount
        {
            get => _barrelPhaseCount;
            private set => SetField(ref _barrelPhaseCount, value);
        }

        private bool _isDarkMode;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (!SetField(ref _isDarkMode, value))
                    return;

                ThemeService.Apply(value ? AppTheme.Dark : AppTheme.Light);
                _settingsService.Save(new AppSettings { IsDarkMode = value });
            }
        }

        public AsyncRelayCommand RefreshCommand { get; }

        public MainViewModel() : this(new LogParserService(), new SettingsService())
        {
        }

        public MainViewModel(ILogParserService parserService, ISettingsService settingsService)
        {
            _parserService = parserService;
            _settingsService = settingsService;
            _dispatcher = Dispatcher.CurrentDispatcher;

            RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);

            LogFolder = _parserService.GetDefaultLogFolder();

            var settings = _settingsService.Load();
            _isDarkMode = settings.IsDarkMode;
            ThemeService.Apply(_isDarkMode ? AppTheme.Dark : AppTheme.Light);

            _ = LoadAsync();

            SetupWatcher();
        }

        public async Task LoadAsync()
        {
            IsBusy = true;
            StatusText = "Reading log files...";

            try
            {
                var folder = LogFolder;

                var result = await Task.Run(() => _parserService.ParseFolder(folder));

                var merged = result.BossKills.Select(LogListEntry.FromBossKill)
                    .Concat(result.ControllerDeadEvents.Select(LogListEntry.FromControllerDead))
                    .Concat(result.StartRunEvents.Select(LogListEntry.FromStartRun))
                    .Concat(result.BarrelPhases.Select(LogListEntry.FromBarrelPhase))
                    .OrderByDescending(e => e.Timestamp);

                Entries.Clear();
                foreach (var entry in merged)
                {
                    Entries.Add(entry);
                }

                BossCount = result.BossKills.Count;

                if (!Directory.Exists(folder))
                {
                    StatusText = $"Log folder not found: {folder}";
                }
                else
                {
                    StatusText = $"Loaded {BossCount} boss encounter(s) as of {DateTime.Now:HH:mm:ss}.";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to read log files: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SetupWatcher()
        {
            try
            {
                if (!Directory.Exists(LogFolder))
                    return;

                _watcher = new FileSystemWatcher(LogFolder, "output_log_*.txt")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _watcher.Changed += OnLogFolderChanged;
                _watcher.Created += OnLogFolderChanged;
                _watcher.Renamed += OnLogFolderChanged;
            }
            catch (IOException)
            {
                // If the watcher can't be created (folder removed, permissions, etc.)
                // the app still functions via the manual Refresh command.
            }
        }

        private void OnLogFolderChanged(object sender, FileSystemEventArgs e)
        {
            // VRChat writes to its log continuously, so debounce refreshes to
            // avoid re-parsing on every single write.
            _dispatcher.Invoke(() =>
            {
                _debounceTimer?.Stop();
                _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _debounceTimer.Tick += async (_, __) =>
                {
                    _debounceTimer!.Stop();
                    await LoadAsync();
                };
                _debounceTimer.Start();
            });
        }
    }
}
