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
/// </summary>
public class CarMapping
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public MatchKind Kind { get; set; } = MatchKind.Car;

    /// <summary>CarPath when Kind is Car or CarAndTrack, CarClassShortName when Kind is CarClass.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Friendly name shown in the UI (e.g. "Dallara iR-18" or "GT3").</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>iRacing's internal track name (WeekendInfo.TrackName). Only used when Kind == CarAndTrack.</summary>
    public string TrackKey { get; set; } = string.Empty;

    /// <summary>Friendly track name shown in the UI. Only used when Kind == CarAndTrack.</summary>
    public string TrackDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Path to a MOZA Pit House Motor preset (.mzpreset). The user keeps editing FFB in Pit House as
    /// usual - this app just re-reads the preset file live every time it applies it, so edits made in
    /// Pit House take effect immediately with no separate "sync" step.
    /// </summary>
    public string? PitHousePresetPath { get; set; }

    /// <summary>Cached label for the grid ("Preset: X"), refreshed whenever the target is (re)chosen.</summary>
    public string TargetDisplayName { get; set; } = "(choose a Pit House preset)";

    public bool Enabled { get; set; } = true;

    public bool HasTarget => !string.IsNullOrEmpty(PitHousePresetPath);
}
