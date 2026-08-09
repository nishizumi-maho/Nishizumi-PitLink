using System.IO;
using System.Text.Json;
using NishizumiPitLink.Models;

namespace NishizumiPitLink.Services;

public class ProfileStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public ProfileStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NishizumiPitLink");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "state.json");
    }

    public AppState Load()
    {
        if (!File.Exists(_filePath))
            return new AppState();

        try
        {
            var json = File.ReadAllText(_filePath);
            var state = JsonSerializer.Deserialize<AppState>(json, JsonOptions);
            return state ?? new AppState();
        }
        catch
        {
            // Corrupt or unreadable state file: back it up and start fresh rather than crash the app.
            try
            {
                File.Copy(_filePath, _filePath + $".corrupt-{DateTime.Now:yyyyMMddHHmmss}.bak", overwrite: true);
            }
            catch { /* best effort */ }
            return new AppState();
        }
    }

    /// <summary>Writes the state file. Returns null on success, or a message describing why it failed.</summary>
    public string? Save(AppState state)
    {
        try
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);
            var tmpPath = _filePath + ".tmp";
            File.WriteAllText(tmpPath, json);
            // Move is atomic on the same volume, so an interrupted save can't leave a half-written
            // state.json behind the way an overwriting copy can.
            File.Move(tmpPath, _filePath, overwrite: true);
            return null;
        }
        catch (Exception ex)
        {
            // Saving settings must never take the app down - it's called from ordinary UI actions.
            return ex.Message;
        }
    }
}
