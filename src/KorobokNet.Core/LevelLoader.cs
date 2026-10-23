using System.Text.Json;
using System.Text.Json.Serialization;
using KorobokNet.Core.Programs;

namespace KorobokNet.Core;

public static class LevelLoader
{
    private const string ResourceName = "KorobokNet.Core.Levels.levels.json";

    /// <summary>Уровни из встроенного файла Levels/levels.json.</summary>
    public static IReadOnlyList<LevelDefinition> LoadBuiltIn() => LoadCatalog().Levels;

    /// <summary>Уровни и обучающее поле (для демонстраций в сюжете) из Levels/levels.json.</summary>
    public static LevelCatalog LoadCatalog()
    {
        var file = Read(ReadResource());
        if (file.Tutorial is null)
        {
            throw new FormatException("В levels.json нет обучающего уровня (tutorial).");
        }
        return new LevelCatalog(ToLevel(file.Tutorial, 0), ToLevels(file));
    }

    public static IReadOnlyList<LevelDefinition> Parse(string json) => ToLevels(Read(json));

    private static string ReadResource()
    {
        using var stream = typeof(LevelLoader).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Не найден ресурс {ResourceName}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static LevelFileDto Read(string json) =>
        JsonSerializer.Deserialize(json, LevelJsonContext.Default.LevelFileDto)
            ?? throw new FormatException("Пустой файл уровней.");

    private static List<LevelDefinition> ToLevels(LevelFileDto file) =>
        file.Levels.Select((dto, index) => ToLevel(dto, index + 1)).ToList();

    private static LevelDefinition ToLevel(LevelDto dto, int number) =>
        LevelDefinition.FromRows(
            number,
            DirectionExtensions.Parse(dto.Heading),
            dto.Rows,
            dto.Stars?.Three ?? new StarRule(),
            dto.Stars?.Two ?? new StarRule(),
            dto.Solution is null ? null : RobotProgram.Parse(dto.Solution));
}

public sealed record LevelCatalog(LevelDefinition Tutorial, IReadOnlyList<LevelDefinition> Levels);

internal sealed record LevelFileDto(List<LevelDto> Levels, LevelDto? Tutorial);

internal sealed record LevelDto(string Heading, List<string> Rows, StarsDto? Stars, string? Solution);

internal sealed record StarsDto(StarRule Three, StarRule Two);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip)]
[JsonSerializable(typeof(LevelFileDto))]
internal sealed partial class LevelJsonContext : JsonSerializerContext;
