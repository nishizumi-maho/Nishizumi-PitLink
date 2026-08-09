using System.Text.Json.Serialization;
using NishizumiPitLink.ViewModels;

namespace NishizumiPitLink.Models;

public enum MatchKind
{
    /// <summary>Match a specific car by its iRacing CarPath (stable internal id).</summary>
    Car,
    /// <summary>Match every car in an iRacing car class (CarClassShortName), e.g. "GT3".</summary>
    CarClass,
    /// <summary>Match one specific car on one specific track. Only offered when per-track profiles are enabled; always takes priority over a plain Car or CarClass match.</summary>
    CarAndTrack,
}

/// <summary>
/// Associates one car (or car class, or car+track combo) with a MOZA Pit House Motor preset.
/// This app never edits FFB itself - it only decides, based on what iRacing reports, which preset
/// Pit House should have loaded. Exact car+track matches take priority over car matches, which take
/// priority over class matches.
///
/// Raises PropertyChanged so the grid picks up edits in place; without it the UI could only be
/// updated by refreshing the whole collection view, which throws if a grid row is mid-edit.
/// </summary>
public class CarMapping : ObservableObject
{
    private MatchKind _kind = MatchKind.Car;
    private string _key = string.Empty;
    private string _displayName = string.Empty;
    private string _trackKey = string.Empty;
    private string _trackDisplayName = string.Empty;
    private string? _pitHousePresetPath;
    private string _targetDisplayName = "(choose a Pit House preset)";
    private bool _enabled = true;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public MatchKind Kind
    {
        get => _kind;
        set => SetField(ref _kind, value);
    }

    /// <summary>CarPath when Kind is Car or CarAndTrack, CarClassShortName when Kind is CarClass.</summary>
    public string Key
    {
        get => _key;
        set => SetField(ref _key, value);
    }

    /// <summary>Friendly name shown in the UI (e.g. "Dallara iR-18" or "GT3").</summary>
    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value);
    }

    /// <summary>iRacing's internal track name (WeekendInfo.TrackName). Only used when Kind == CarAndTrack.</summary>
    public string TrackKey
    {
        get => _trackKey;
        set => SetField(ref _trackKey, value);
    }

    /// <summary>Friendly track name shown in the UI. Only used when Kind == CarAndTrack.</summary>
    public string TrackDisplayName
    {
        get => _trackDisplayName;
        set => SetField(ref _trackDisplayName, value);
    }

    /// <summary>
    /// Path to a MOZA Pit House Motor preset (.mzpreset). The user keeps editing FFB in Pit House as
    /// usual - this app just re-reads the preset file live every time it applies it, so edits made in
    /// Pit House take effect immediately with no separate "sync" step.
    /// </summary>
    public string? PitHousePresetPath
    {
        get => _pitHousePresetPath;
        set
        {
            if (SetField(ref _pitHousePresetPath, value))
                OnPropertyChanged(nameof(HasTarget));
        }
    }

    /// <summary>Cached label for the grid ("Preset: X"), refreshed whenever the target is (re)chosen.</summary>
    public string TargetDisplayName
    {
        get => _targetDisplayName;
        set => SetField(ref _targetDisplayName, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    [JsonIgnore]
    public bool HasTarget => !string.IsNullOrEmpty(PitHousePresetPath);
}
