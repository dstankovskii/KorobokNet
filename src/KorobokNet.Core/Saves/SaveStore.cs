using System.Text.Json;
using System.Text.Json.Serialization;

namespace KorobokNet.Core.Saves;

/// <summary>Чтение и запись сохранений в JSON-файл.</summary>
public sealed class SaveStore(string filePath)
{
    public string FilePath { get; } = filePath;

    public SaveData Load()
    {
        if (!File.Exists(FilePath))
        {
            return new SaveData();
        }
        var json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize(json, SaveJsonContext.Default.SaveData) ?? new SaveData();
    }

    /// <summary>Пишет через временный файл, чтобы сбой посреди записи не испортил сохранение.</summary>
    public void Save(SaveData data)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(FilePath));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(data, SaveJsonContext.Default.SaveData));
        File.Move(temp, FilePath, overwrite: true);
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SaveData))]
internal sealed partial class SaveJsonContext : JsonSerializerContext;
