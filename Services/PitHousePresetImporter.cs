using System.IO;
using System.IO.Compression;
using System.Text.Json;
using NishizumiPitLink.Models;

namespace NishizumiPitLink.Services;

public record PitHousePresetSummary(string FilePath, string Id, string Name, string Devices)
{
    /// <summary>
    /// Wheelbase plus a short id fragment. Pit House happily saves several presets under the same
    /// name, so the name alone can't identify one - and picking a preset saved for a different
    /// wheelbase would push the wrong torque range at the wheel.
    /// </summary>
    public string Detail
    {
        get
        {
            var shortId = Id.Length >= 8 ? Id[..8] : Id;
            return string.IsNullOrEmpty(Devices) ? shortId : $"{Devices} · {shortId}";
        }
    }
}

/// <summary>
/// Reads MOZA Pit House's own Motor presets (.mzpreset files, which are zip archives containing
/// preset.json) so this app can push the exact FFB settings the user already tuned and saved in
/// Pit House to the wheelbase - it never invents or edits FFB values itself.
///
/// Only the fields with an unambiguous match in the MOZA SDK's motor parameter set are read (see
/// MotorSettings). Road sensitivity, the per-frequency equalizer bands, and soft-limit/end-stop
/// tuning are intentionally left out - their exact SDK field mapping could not be confirmed from
/// the preset file format alone, and pushing a wrong value there is worse than leaving it as-is.
/// Those stay exactly as Pit House itself last set them on the wheel.
/// </summary>
public static class PitHousePresetImporter
{
    public static string DefaultPresetsFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MOZA Pit House", "Presets", "Motor");

    public static List<PitHousePresetSummary> ListPresets(string? folder = null)
    {
        folder ??= DefaultPresetsFolder;
        var result = new List<PitHousePresetSummary>();
        if (!Directory.Exists(folder))
            return result;

        foreach (var file in Directory.EnumerateFiles(folder, "*.mzpreset"))
        {
            try
            {
                using var zip = ZipFile.OpenRead(file);
                var entry = zip.GetEntry("preset.json");
                if (entry is null) continue;

                using var stream = entry.Open();
                using var doc = JsonDocument.Parse(stream);
                var root = doc.RootElement;

                var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? Path.GetFileNameWithoutExtension(file) : Path.GetFileNameWithoutExtension(file);
                var devices = "";
                if (root.TryGetProperty("devices", out var devicesEl) && devicesEl.ValueKind == JsonValueKind.Array)
                    devices = string.Join(", ", devicesEl.EnumerateArray().Select(d => d.GetString()));

                result.Add(new PitHousePresetSummary(file, id, name, devices));
            }
            catch
            {
                // Skip unreadable/corrupt preset files rather than failing the whole listing.
            }
        }

        return result.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Reads a .mzpreset file fresh from disk every time it's called - callers should not cache the
    /// result across car changes, so that edits made in Pit House take effect immediately next time
    /// this preset is applied, with no separate "sync" step.
    /// </summary>
    public static (MotorSettings Motor, string Name) ImportWithName(string mzpresetFilePath)
    {
        using var zip = ZipFile.OpenRead(mzpresetFilePath);
        var entry = zip.GetEntry("preset.json") ?? throw new InvalidDataException("preset.json not found inside .mzpreset archive.");

        using var stream = entry.Open();
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? Path.GetFileNameWithoutExtension(mzpresetFilePath) : Path.GetFileNameWithoutExtension(mzpresetFilePath);
        var settings = BuildMotorSettings(root);
        return (settings, name);
    }

    private static MotorSettings BuildMotorSettings(JsonElement root)
    {
        var settings = new MotorSettings();
        if (!root.TryGetProperty("deviceParams", out var p) || p.ValueKind != JsonValueKind.Object)
            return settings;

        int? GetInt(string name) => p.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number ? el.GetInt32() : null;
        bool? GetBool(string name) => p.TryGetProperty(name, out var el) && (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False) ? el.GetBoolean() : null;

        settings.FfbStrength = GetInt("gameForceFeedbackStrength");
        settings.FfbReverse = GetBool("gameForceFeedbackReversal") is { } rev ? (rev ? 1 : 0) : null;
        settings.LimitWheelSpeed = GetInt("maximumSteeringSpeed");
        settings.PeakTorque = GetInt("maximumTorque");
        settings.NaturalDamper = GetInt("mechanicalDamper");
        settings.NaturalFriction = GetInt("mechanicalFriction");
        settings.SpringStrength = GetInt("mechanicalSpringStrength");
        settings.SpeedDamping = GetInt("speedDependentDamping");
        settings.NaturalInertia = GetInt("naturalInertiaV2");
        settings.LimitAngle = GetInt("maximumSteeringAngle");
        settings.GameMaximumAngle = GetInt("maximumGameSteeringAngle");

        return settings;
    }
}
