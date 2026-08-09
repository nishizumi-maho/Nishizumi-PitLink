using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using mozaAPI;
using NishizumiPitLink.Models;
using NishizumiPitLink.Services;

namespace NishizumiPitLink.ViewModels;

public record ProfilePickResult(string PitHousePresetPath, string DisplayName);

public class MainViewModel : ObservableObject, IDisposable
{
    private readonly ProfileStore _store;
    private readonly MozaSdkService _moza;
    private readonly IRacingCarWatcher _iracing;

    public Array MatchKinds => Enum.GetValues(typeof(MatchKind));

    public ObservableCollection<CarMapping> Mappings { get; } = new();
    public ObservableCollection<DiscoveredCar> DiscoveredCars { get; } = new();
    public ObservableCollection<string> StatusLog { get; } = new();

    private bool _globalEnabled = true;
    public bool GlobalEnabled
    {
        get => _globalEnabled;
        set
        {
            if (SetField(ref _globalEnabled, value))
            {
                Persist();
                Log(value ? "Automatic switching enabled." : "Automatic switching disabled — nothing will be changed on the FFB.");
            }
        }
    }

    private bool _startWithWindows;
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (SetField(ref _startWithWindows, value))
            {
                AutoStartService.SetEnabled(value);
                Persist();
            }
        }
    }

    private bool _perTrackProfilesEnabled;
    public bool PerTrackProfilesEnabled
    {
        get => _perTrackProfilesEnabled;
        set
        {
            if (SetField(ref _perTrackProfilesEnabled, value))
            {
                Persist();
                Log(value ? "Per-track profiles enabled — car+track mappings now take priority." : "Per-track profiles disabled.");
                MapCurrentCarForTrackCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private bool _iRacingConnected;
    public bool IRacingConnected
    {
        get => _iRacingConnected;
        private set => SetField(ref _iRacingConnected, value);
    }

    private string _currentCarDisplay = "(iRacing not connected)";
    public string CurrentCarDisplay
    {
        get => _currentCarDisplay;
        private set => SetField(ref _currentCarDisplay, value);
    }

    private string _currentTrackDisplay = "-";
    public string CurrentTrackDisplay
    {
        get => _currentTrackDisplay;
        private set => SetField(ref _currentTrackDisplay, value);
    }

    private string _currentProfileDisplay = "-";
    public string CurrentProfileDisplay
    {
        get => _currentProfileDisplay;
        private set => SetField(ref _currentProfileDisplay, value);
    }

    private string _wheelbaseStatus = "Checking...";
    public string WheelbaseStatus
    {
        get => _wheelbaseStatus;
        private set => SetField(ref _wheelbaseStatus, value);
    }

    public RelayCommand AddMappingCommand { get; }
    public RelayCommand DeleteMappingCommand { get; }
    public RelayCommand ChooseMappingTargetCommand { get; }
    public RelayCommand AssignDiscoveredCarCommand { get; }
    public RelayCommand AssignDiscoveredCarForTrackCommand { get; }
    public RelayCommand DismissDiscoveredCarCommand { get; }
    public RelayCommand MapCurrentCarForTrackCommand { get; }
    public RelayCommand ForceReapplyCommand { get; }
    public RelayCommand RefreshWheelbaseStatusCommand { get; }
    public RelayCommand OpenPresetsFolderCommand { get; }

    /// <summary>Raised when the UI should offer a MOZA Pit House preset picker for a car mapping; the View owns the actual dialog.</summary>
    public event Func<ProfilePickResult?>? RequestProfilePick;

    private CarInfo? _currentCar;

    public MainViewModel()
    {
        _store = new ProfileStore();
        _moza = new MozaSdkService();
        _iracing = new IRacingCarWatcher();

        var state = _store.Load();
        foreach (var m in state.Mappings) Mappings.Add(m);
        foreach (var d in state.DiscoveredCars) DiscoveredCars.Add(d);
        _globalEnabled = state.GlobalEnabled;
        _startWithWindows = state.StartWithWindows;
        _perTrackProfilesEnabled = state.PerTrackProfilesEnabled;

        AddMappingCommand = new RelayCommand(AddMapping);
        DeleteMappingCommand = new RelayCommand(o => { if (o is CarMapping m) DeleteMapping(m); });
        ChooseMappingTargetCommand = new RelayCommand(o => { if (o is CarMapping m) ChooseMappingTarget(m); });
        AssignDiscoveredCarCommand = new RelayCommand(o => { if (o is DiscoveredCar d) AssignDiscoveredCar(d); });
        AssignDiscoveredCarForTrackCommand = new RelayCommand(o => { if (o is DiscoveredCar d) AssignDiscoveredCarForTrack(d); }, o => PerTrackProfilesEnabled && o is DiscoveredCar d && !string.IsNullOrEmpty(d.TrackKey));
        DismissDiscoveredCarCommand = new RelayCommand(o => { if (o is DiscoveredCar d) { DiscoveredCars.Remove(d); Persist(); } });
        MapCurrentCarForTrackCommand = new RelayCommand(MapCurrentCarForTrack, () => PerTrackProfilesEnabled && _currentCar is not null);
        ForceReapplyCommand = new RelayCommand(() => ApplyForCar(_currentCar, forceLog: true));
        RefreshWheelbaseStatusCommand = new RelayCommand(RefreshWheelbaseStatus);
        OpenPresetsFolderCommand = new RelayCommand(() =>
        {
            Directory_CreateIfMissing(PitHousePresetImporter.DefaultPresetsFolder);
            Process.Start(new ProcessStartInfo(PitHousePresetImporter.DefaultPresetsFolder) { UseShellExecute = true });
        });

        if (_moza.TryInitialize(out var mozaError))
        {
            Log("MOZA SDK initialized.");
        }
        else
        {
            Log($"Failed to initialize the MOZA SDK: {mozaError}");
        }
        RefreshWheelbaseStatus();
        _ = RetryWheelbaseStatusSoonAsync();

        _iracing.ConnectionChanged += connected => RunOnUi(() =>
        {
            IRacingConnected = connected;
            Log(connected ? "iRacing connected." : "iRacing disconnected.");
            if (!connected)
            {
                CurrentCarDisplay = "(iRacing not connected)";
                CurrentTrackDisplay = "-";
                CurrentProfileDisplay = "-";
                _currentCar = null;
                MapCurrentCarForTrackCommand.RaiseCanExecuteChanged();
            }
        });
        _iracing.CarChanged += info => RunOnUi(() => OnCarChanged(info));
        _iracing.SdkException += ex => RunOnUi(() => Log($"iRacing SDK: {ex.Message}"));
        _iracing.Start();

        Log("Ready. Waiting for iRacing...");
    }

    private static void Directory_CreateIfMissing(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }

    private void OnCarChanged(CarInfo? info)
    {
        _currentCar = info;
        MapCurrentCarForTrackCommand.RaiseCanExecuteChanged();

        if (info is null)
        {
            CurrentCarDisplay = "(no car selected)";
            CurrentTrackDisplay = "-";
            CurrentProfileDisplay = "-";
            return;
        }

        CurrentCarDisplay = string.IsNullOrEmpty(info.CarClassShortName)
            ? info.CarScreenName
            : $"{info.CarScreenName}  [{info.CarClassShortName}]";
        CurrentTrackDisplay = string.IsNullOrEmpty(info.TrackDisplayName) ? "-" : info.TrackDisplayName;

        var hasExactMapping = Mappings.Any(m => m.Kind == MatchKind.Car && m.Key == info.CarPath);
        var hasClassMapping = !string.IsNullOrEmpty(info.CarClassShortName)
            && Mappings.Any(m => m.Kind == MatchKind.CarClass && m.Key == info.CarClassShortName);
        var hasTrackMapping = PerTrackProfilesEnabled && !string.IsNullOrEmpty(info.TrackKey)
            && Mappings.Any(m => m.Kind == MatchKind.CarAndTrack && m.Key == info.CarPath && m.TrackKey == info.TrackKey);

        if (!hasExactMapping && !hasClassMapping && !hasTrackMapping)
        {
            var existing = DiscoveredCars.FirstOrDefault(d => d.CarPath == info.CarPath);
            if (existing is null)
            {
                DiscoveredCars.Insert(0, new DiscoveredCar
                {
                    CarPath = info.CarPath,
                    CarScreenName = info.CarScreenName,
                    CarClassShortName = info.CarClassShortName,
                    TrackKey = info.TrackKey,
                    TrackDisplayName = info.TrackDisplayName,
                    LastSeenUtc = DateTime.UtcNow,
                });
                Persist();
            }
            else
            {
                existing.LastSeenUtc = DateTime.UtcNow;
                existing.TrackKey = info.TrackKey;
                existing.TrackDisplayName = info.TrackDisplayName;
            }
        }

        ApplyForCar(info, forceLog: false);
    }

    private void ApplyForCar(CarInfo? info, bool forceLog)
    {
        if (info is null)
        {
            if (forceLog) Log("No car currently selected.");
            return;
        }

        if (!GlobalEnabled)
        {
            CurrentProfileDisplay = "(automatic switching disabled)";
            if (forceLog) Log("Automatic switching is disabled — nothing applied.");
            return;
        }

        CarMapping? mapping = null;
        if (PerTrackProfilesEnabled && !string.IsNullOrEmpty(info.TrackKey))
            mapping = Mappings.FirstOrDefault(m => m.Enabled && m.Kind == MatchKind.CarAndTrack && m.Key == info.CarPath && m.TrackKey == info.TrackKey);
        mapping ??= Mappings.FirstOrDefault(m => m.Enabled && m.Kind == MatchKind.Car && m.Key == info.CarPath);
        mapping ??= Mappings.FirstOrDefault(m => m.Enabled && m.Kind == MatchKind.CarClass && m.Key == info.CarClassShortName);

        if (mapping is null || !mapping.HasTarget)
        {
            CurrentProfileDisplay = "(no preset for this car)";
            if (forceLog) Log($"No mapping for \"{info.CarScreenName}\".");
            return;
        }

        if (!File.Exists(mapping.PitHousePresetPath))
        {
            CurrentProfileDisplay = "(Pit House preset no longer exists)";
            Log($"The preset linked to \"{info.CarScreenName}\" was not found on disk. Choose another one in the Cars tab.");
            return;
        }

        MotorSettings motor;
        string targetName;
        try
        {
            // Read fresh every time: whatever is currently saved in Pit House is what gets applied.
            (motor, targetName) = PitHousePresetImporter.ImportWithName(mapping.PitHousePresetPath!);
        }
        catch (Exception ex)
        {
            CurrentProfileDisplay = "(failed to read Pit House preset)";
            Log($"Failed to read the Pit House preset for \"{info.CarScreenName}\": {ex.Message}");
            return;
        }

        var outcome = _moza.Apply(motor);
        if (outcome.Success)
        {
            CurrentProfileDisplay = targetName;
            Log($"\"{targetName}\" applied for \"{info.CarScreenName}\".");
        }
        else
        {
            CurrentProfileDisplay = $"{targetName} (with warnings)";
            Log($"\"{targetName}\" applied with warnings: {string.Join("; ", outcome.Failures)}");
        }

        RefreshWheelbaseStatus();
    }

    /// <summary>
    /// Right after installMozaSDK() the wheelbase binding (device discovery through Pit House) can
    /// take a moment, so the very first probe can come back as "no device" even when one is
    /// connected. Retry a few times in the background so the status settles without user action.
    /// </summary>
    private async Task RetryWheelbaseStatusSoonAsync()
    {
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(1000);
            var err = _moza.Probe();
            if (err == ERRORCODE.NORMAL)
            {
                RunOnUi(RefreshWheelbaseStatus);
                return;
            }
        }
        RunOnUi(RefreshWheelbaseStatus);
    }

    private void RefreshWheelbaseStatus()
    {
        var err = _moza.Probe();
        WheelbaseStatus = err switch
        {
            ERRORCODE.NORMAL => "Wheelbase connected",
            ERRORCODE.PITHOUSENOTREADY => "MOZA Pit House is not ready (open Pit House)",
            ERRORCODE.NODEVICES => "No MOZA wheelbase detected",
            ERRORCODE.NOINSTALLSDK => "MOZA SDK not initialized",
            _ => $"Status: {err}",
        };
    }

    private void AddMapping(object? _)
    {
        var mapping = new CarMapping
        {
            Kind = MatchKind.CarClass,
            Key = string.Empty,
            DisplayName = "(set the class or car)",
            Enabled = true,
        };
        Mappings.Add(mapping);
        Persist();
    }

    private void DeleteMapping(CarMapping mapping)
    {
        Mappings.Remove(mapping);
        Persist();
    }

    private void ChooseMappingTarget(CarMapping mapping)
    {
        var result = RequestPick();
        if (result is null) return;

        mapping.PitHousePresetPath = result.PitHousePresetPath;
        mapping.TargetDisplayName = $"Preset: {result.DisplayName}";

        RefreshMappingsView();
        Persist();

        // The mapping the player is currently in might just have gained a target - re-apply immediately.
        if (_currentCar is not null) ApplyForCar(_currentCar, forceLog: false);
    }

    private void RefreshMappingsView()
        => System.Windows.Data.CollectionViewSource.GetDefaultView(Mappings)?.Refresh();

    private void AssignDiscoveredCar(DiscoveredCar discovered)
        => AssignMapping(MatchKind.Car, discovered.CarPath, discovered.CarScreenName, string.Empty, string.Empty, discovered);

    private void AssignDiscoveredCarForTrack(DiscoveredCar discovered)
        => AssignMapping(MatchKind.CarAndTrack, discovered.CarPath, discovered.CarScreenName, discovered.TrackKey, discovered.TrackDisplayName, discovered);

    private void MapCurrentCarForTrack()
    {
        if (_currentCar is null || string.IsNullOrEmpty(_currentCar.TrackKey)) return;
        AssignMapping(MatchKind.CarAndTrack, _currentCar.CarPath, _currentCar.CarScreenName, _currentCar.TrackKey, _currentCar.TrackDisplayName, discovered: null);
    }

    /// <summary>Finds or creates a mapping for the given (kind, car, track) and opens the preset picker for it. A brand-new mapping is discarded if the user cancels the picker.</summary>
    private void AssignMapping(MatchKind kind, string carKey, string carDisplayName, string trackKey, string trackDisplayName, DiscoveredCar? discovered)
    {
        var mapping = Mappings.FirstOrDefault(m => m.Kind == kind && m.Key == carKey && m.TrackKey == trackKey);
        var isNew = mapping is null;
        mapping ??= new CarMapping
        {
            Kind = kind,
            Key = carKey,
            DisplayName = carDisplayName,
            TrackKey = trackKey,
            TrackDisplayName = trackDisplayName,
            Enabled = true,
        };

        var result = RequestPick();
        if (result is null) return; // user cancelled - leave any existing mapping untouched, and never create a dangling one

        mapping.PitHousePresetPath = result.PitHousePresetPath;
        mapping.TargetDisplayName = $"Preset: {result.DisplayName}";

        if (isNew) Mappings.Add(mapping);
        if (discovered is not null) DiscoveredCars.Remove(discovered);
        Persist();

        var scope = kind == MatchKind.CarAndTrack ? $"\"{carDisplayName}\" at \"{trackDisplayName}\" only" : $"\"{carDisplayName}\"";
        Log($"{scope} mapped to \"{result.DisplayName}\".");

        if (_currentCar is not null && _currentCar.CarPath == carKey)
            ApplyForCar(_currentCar, forceLog: false);
    }

    private ProfilePickResult? RequestPick()
    {
        try
        {
            return RequestProfilePick?.Invoke();
        }
        catch (Exception ex)
        {
            Log($"Failed to open the preset picker: {ex}");
            return null;
        }
    }

    private void Log(string message)
    {
        StatusLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        while (StatusLog.Count > 200) StatusLog.RemoveAt(StatusLog.Count - 1);
    }

    private void Persist()
    {
        var state = new AppState
        {
            GlobalEnabled = GlobalEnabled,
            StartWithWindows = StartWithWindows,
            PerTrackProfilesEnabled = PerTrackProfilesEnabled,
            Mappings = Mappings.ToList(),
            DiscoveredCars = DiscoveredCars.ToList(),
        };
        _store.Save(state);
    }

    public void Dispose()
    {
        Persist();
        _iracing.Dispose();
        _moza.Dispose();
        GC.SuppressFinalize(this);
    }
}
