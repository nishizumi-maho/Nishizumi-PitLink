namespace NishizumiPitLink.Models;

public class AppState
{
    public bool GlobalEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;

    /// <summary>When on, car+track mappings (MatchKind.CarAndTrack) become available and take priority over plain car/class mappings.</summary>
    public bool PerTrackProfilesEnabled { get; set; } = false;

    public List<CarMapping> Mappings { get; set; } = new();
    public List<DiscoveredCar> DiscoveredCars { get; set; } = new();
}
