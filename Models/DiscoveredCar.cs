namespace NishizumiPitLink.Models;

/// <summary>A car iRacing has reported that has no applicable mapping yet, kept so the UI can offer a quick "assign" action.</summary>
public class DiscoveredCar
{
    public string CarPath { get; set; } = string.Empty;
    public string CarScreenName { get; set; } = string.Empty;
    public string CarClassShortName { get; set; } = string.Empty;

    /// <summary>Track last seen with this car (iRacing's internal TrackName), for the optional "assign for this track only" action.</summary>
    public string TrackKey { get; set; } = string.Empty;
    public string TrackDisplayName { get; set; } = string.Empty;

    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
}
