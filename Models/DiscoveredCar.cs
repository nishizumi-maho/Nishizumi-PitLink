using NishizumiPitLink.ViewModels;

namespace NishizumiPitLink.Models;

/// <summary>
/// A car iRacing has reported that has no applicable mapping yet, kept so the UI can offer a quick
/// "assign" action. Raises PropertyChanged so the grid reflects re-sightings (track, last seen)
/// without refreshing the whole collection view.
/// </summary>
public class DiscoveredCar : ObservableObject
{
    private string _carPath = string.Empty;
    private string _carScreenName = string.Empty;
    private string _carClassShortName = string.Empty;
    private string _trackKey = string.Empty;
    private string _trackDisplayName = string.Empty;
    private DateTime _lastSeenUtc = DateTime.UtcNow;

    public string CarPath
    {
        get => _carPath;
        set => SetField(ref _carPath, value);
    }

    public string CarScreenName
    {
        get => _carScreenName;
        set => SetField(ref _carScreenName, value);
    }

    public string CarClassShortName
    {
        get => _carClassShortName;
        set => SetField(ref _carClassShortName, value);
    }

    /// <summary>Track last seen with this car (iRacing's internal TrackName), for the optional "assign for this track only" action.</summary>
    public string TrackKey
    {
        get => _trackKey;
        set => SetField(ref _trackKey, value);
    }

    public string TrackDisplayName
    {
        get => _trackDisplayName;
        set => SetField(ref _trackDisplayName, value);
    }

    public DateTime LastSeenUtc
    {
        get => _lastSeenUtc;
        set => SetField(ref _lastSeenUtc, value);
    }
}
