// Импорт графики и звука из экспорта AssetRipper в src/KorobokNet/Assets.
// Запуск из корня репозитория: dotnet run --project tools/AssetImport [папка UnityProject]
// По умолчанию экспорт берётся из старого проекта: ../Korobok/recovered/UnityProject.
// Создаёт sprites.json (рамки и опорные точки спрайтов) и animations.json (кадры анимаций),
// копирует PNG и музыку, звуки переводит в WAV через ffmpeg.
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

var root = Directory.GetCurrentDirectory();
var export = args.FirstOrDefault(a => !a.StartsWith("--")) ?? Path.Combine(root, "..", "Korobok", "recovered", "UnityProject");
var unity = Path.GetFullPath(Path.Combine(export, "ExportedProject", "Assets"));
var output = Path.Combine(root, "src", "KorobokNet", "Assets");
if (!Directory.Exists(unity))
{
    Console.Error.WriteLine($"Не найден экспорт Unity: {unity}");
    return 1;
}

var textures = new Dictionary<string, (string Name, int Width, int Height)>();
foreach (var meta in Directory.GetFiles(Path.Combine(unity, "Texture2D"), "*.png.meta"))
{
    var png = meta[..^".meta".Length];
    var (w, h) = PngSize(png);
    textures[Guid(meta)] = (Path.GetFileNameWithoutExtension(png), w, h);
}

var sprites = new SortedDictionary<string, SpriteDto>(StringComparer.Ordinal);
var spriteByGuid = new Dictionary<string, string>();
foreach (var asset in Directory.GetFiles(Path.Combine(unity, "Sprite"), "*.asset"))
{
    var text = File.ReadAllText(asset);
    var name = Match(text, @"m_Name: (.+)").Trim();
    var rect = Regex.Match(text, @"m_Rect:\s+serializedVersion: \d+\s+x: (\S+)\s+y: (\S+)\s+width: (\S+)\s+height: (\S+)");
    var pivot = Regex.Match(text, @"m_Pivot: \{x: (\S+), y: (\S+)\}");
    var ppu = Num(Match(text, @"m_PixelsToUnits: (\S+)"));
    var texture = textures[Match(text, @"m_RD:\s+serializedVersion: \d+\s+texture: \{fileID: \d+, guid: (\w+)")];

    var x = Num(rect.Groups[1].Value);
    var y = Num(rect.Groups[2].Value);
    var w = Num(rect.Groups[3].Value);
    var h = Num(rect.Groups[4].Value);
    // Unity считает y и опорную точку снизу; переводим в координаты "сверху-слева".
    sprites[name] = new SpriteDto(
        texture.Name,
        (int)Math.Round(x),
        (int)Math.Round(texture.Height - y - h),
        (int)Math.Round(w),
        (int)Math.Round(h),
        Round(Num(pivot.Groups[1].Value)),
        Round(1 - Num(pivot.Groups[2].Value)),
        Round(ppu));
    spriteByGuid[Guid(asset + ".meta")] = name;
}

var animations = new SortedDictionary<string, AnimationDto>(StringComparer.Ordinal);
foreach (var anim in Directory.GetFiles(Path.Combine(unity, "AnimationClip"), "*.anim"))
{
    var text = File.ReadAllText(anim);
    var frames = Regex.Matches(text, @"- time: (\S+)\s+value: \{fileID: \d+, guid: (\w+)")
        .Select(m => new FrameDto(Round(Num(m.Groups[1].Value)), spriteByGuid[m.Groups[2].Value]))
        .ToList();
    if (Regex.IsMatch(text, @"m_FloatCurves:\s+- curve"))
    {
        Console.WriteLine($"Внимание: {Path.GetFileName(anim)} содержит float-кривые, они не импортируются.");
    }
    animations[Match(text, @"m_Name: (.+)").Trim()] = new AnimationDto(
        Match(text, @"m_LoopTime: (\d)") == "1",
        Round(Num(Match(text, @"m_StopTime: (\S+)"))),
        frames);
}

var json = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
Directory.CreateDirectory(Path.Combine(output, "Textures"));
Directory.CreateDirectory(Path.Combine(output, "Audio"));
File.WriteAllText(Path.Combine(output, "sprites.json"), JsonSerializer.Serialize(sprites, json));
File.WriteAllText(Path.Combine(output, "animations.json"), JsonSerializer.Serialize(animations, json));

foreach (var (name, _, _) in textures.Values)
{
    File.Copy(Path.Combine(unity, "Texture2D", name + ".png"), Path.Combine(output, "Textures", name + ".png"), overwrite: true);
}

var audio = Path.Combine(unity, "AudioClip");
File.Copy(Path.Combine(audio, "Cheery Monday.ogg"), Path.Combine(output, "Audio", "music.ogg"), overwrite: true);
foreach (var sfx in new[] { "click", "End", "forward" })
{
    var target = Path.Combine(output, "Audio", sfx.ToLowerInvariant() + ".wav");
    using var ffmpeg = Process.Start(new ProcessStartInfo("ffmpeg", ["-v", "error", "-y", "-i", Path.Combine(audio, sfx + ".ogg"), "-ac", "2", "-ar", "44100", "-sample_fmt", "s16", target])
    {
        UseShellExecute = false,
    }) ?? throw new InvalidOperationException("Не удалось запустить ffmpeg.");
    ffmpeg.WaitForExit();
    if (ffmpeg.ExitCode != 0)
    {
        throw new InvalidOperationException($"ffmpeg не смог конвертировать {sfx}.ogg");
    }
}

Console.WriteLine($"Спрайтов: {sprites.Count}, анимаций: {animations.Count}, текстур: {textures.Count}. Готово: {output}");

if (args.Contains("--prefabs"))
{
    foreach (var prefab in Directory.GetFiles(Path.Combine(unity, "GameObject"), "*.prefab").Order())
    {
        Console.WriteLine($"== {Path.GetFileName(prefab)}");
        foreach (var line in File.ReadLines(prefab))
        {
            var t = line.Trim();
            var sprite = Regex.Match(t, @"m_Sprite: \{fileID: \d+, guid: (\w+)");
            if (sprite.Success)
            {
                Console.WriteLine($"   sprite: {spriteByGuid.GetValueOrDefault(sprite.Groups[1].Value, sprite.Groups[1].Value)}");
            }
            else if (Regex.IsMatch(t, @"^(m_Name: \S|m_LocalPosition|m_LocalScale|m_LocalRotation|m_SortingOrder|m_SizeDelta|m_AnchoredPosition|m_Text:|m_FontSize)"))
            {
                Console.WriteLine("   " + t);
            }
        }
    }
}
return 0;

static string Guid(string metaPath) => Match(File.ReadAllText(metaPath), @"guid: (\w+)");

static string Match(string text, string pattern)
{
    var m = Regex.Match(text, pattern);
    return m.Success ? m.Groups[1].Value : throw new FormatException($"Не найдено: {pattern}");
}

static double Num(string s) => double.Parse(s, CultureInfo.InvariantCulture);

static double Round(double v) => Math.Round(v, 4);

static (int Width, int Height) PngSize(string path)
{
    Span<byte> header = stackalloc byte[24];
    using var file = File.OpenRead(path);
    file.ReadExactly(header);
    return (
        (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19],
        (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23]);
}

internal sealed record SpriteDto(string Texture, int X, int Y, int W, int H, double PivotX, double PivotY, double Ppu);

internal sealed record FrameDto(double Time, string Sprite);

internal sealed record AnimationDto(bool Loop, double Length, List<FrameDto> Frames);
