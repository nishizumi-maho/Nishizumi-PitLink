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

    public void Save(AppState state)
    {
        var json = JsonSerializer.Serialize(state, JsonOptions);
        var tmpPath = _filePath + ".tmp";
        File.WriteAllText(tmpPath, json);
        File.Copy(tmpPath, _filePath, overwrite: true);
        File.Delete(tmpPath);
    }
}
