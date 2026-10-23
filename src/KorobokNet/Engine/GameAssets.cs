using System.Text.Json;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KorobokNet.Engine;

/// <summary>Кадр из текстуры с опорной точкой, как Sprite в Unity.</summary>
public sealed class SpriteDef(Texture2D texture, Rectangle source, Vector2 origin, float pixelsPerUnit)
{
    public Texture2D Texture { get; } = texture;
    public Rectangle Source { get; } = source;

    /// <summary>Опорная точка в пикселях исходного кадра (от левого верхнего угла).</summary>
    public Vector2 Origin { get; } = origin;

    /// <summary>Сколько пикселей кадра приходится на одну единицу мира Unity.</summary>
    public float PixelsPerUnit { get; } = pixelsPerUnit;

    public Vector2 Size => new(Source.Width, Source.Height);
}

public sealed class AnimationClip(string name, bool loop, float length, IReadOnlyList<(float Time, SpriteDef Sprite)> frames)
{
    public string Name { get; } = name;
    public bool Loop { get; } = loop;

    /// <summary>Длительность в секундах; для однокадровых клипов - длительность одного кадра.</summary>
    public float Length { get; } = length;

    public SpriteDef FrameAt(float time)
    {
        if (Loop && Length > 0)
        {
            time %= Length;
        }
        var result = frames[0].Sprite;
        foreach (var (t, sprite) in frames)
        {
            if (t > time)
            {
                break;
            }
            result = sprite;
        }
        return result;
    }
}

/// <summary>
/// Графика, анимации и шрифты из папки Assets. Описания спрайтов и анимаций лежат в sprites.json
/// и animations.json - их создаёт tools/AssetImport из восстановленного проекта Unity.
/// </summary>
public sealed class GameAssets : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly Dictionary<string, Texture2D> _textures = [];
    private readonly Dictionary<string, SpriteDef> _sprites = [];
    private readonly Dictionary<string, AnimationClip> _animations = [];
    private readonly Dictionary<string, SpriteDto> _spriteData;
    private readonly FontSystem _regular = new();
    private readonly FontSystem _bold = new();

    public GameAssets(GraphicsDevice device)
    {
        _device = device;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _spriteData = JsonSerializer.Deserialize<Dictionary<string, SpriteDto>>(ReadText("sprites.json"), options)
            ?? throw new InvalidDataException("sprites.json пуст.");
        var animations = JsonSerializer.Deserialize<Dictionary<string, AnimationDto>>(ReadText("animations.json"), options)
            ?? throw new InvalidDataException("animations.json пуст.");
        foreach (var (name, dto) in animations)
        {
            var frames = dto.Frames.Select(f => ((float)f.Time, Sprite(f.Sprite))).ToList();
            _animations[name] = new AnimationClip(name, dto.Loop, (float)dto.Length, frames);
        }
        _regular.AddFont(ReadBytes("Fonts/PT_Sans-Web-Regular.ttf"));
        _bold.AddFont(ReadBytes("Fonts/PT_Sans-Web-Bold.ttf"));
        Pixel = new Texture2D(device, 1, 1);
        Pixel.SetData([Color.White]);
        Circle = CreateCircle(device, 64);
    }

    public Texture2D Pixel { get; }

    /// <summary>Сглаженный белый круг для скруглённых углов и точек.</summary>
    public Texture2D Circle { get; }

    public static Stream Open(string relativePath) =>
        Microsoft.Xna.Framework.TitleContainer.OpenStream(Path.Combine("Assets", relativePath));

    public Texture2D Texture(string name)
    {
        if (!_textures.TryGetValue(name, out var texture))
        {
            using var stream = Open($"Textures/{name}.png");
            texture = Texture2D.FromStream(_device, stream, DefaultColorProcessors.PremultiplyAlpha);
            _textures[name] = texture;
        }
        return texture;
    }

    public SpriteDef Sprite(string name)
    {
        if (_sprites.TryGetValue(name, out var sprite))
        {
            return sprite;
        }
        if (!_spriteData.TryGetValue(name, out var dto))
        {
            throw new KeyNotFoundException($"Нет спрайта '{name}' в sprites.json.");
        }
        var texture = Texture(dto.Texture);
        var source = Rectangle.Intersect(new Rectangle(dto.X, dto.Y, dto.W, dto.H), texture.Bounds);
        var origin = new Vector2((float)dto.PivotX * dto.W, (float)dto.PivotY * dto.H) - new Vector2(source.X - dto.X, source.Y - dto.Y);
        sprite = new SpriteDef(texture, source, origin, (float)dto.Ppu);
        _sprites[name] = sprite;
        return sprite;
    }

    public AnimationClip Animation(string name) =>
        _animations.TryGetValue(name, out var clip) ? clip : throw new KeyNotFoundException($"Нет анимации '{name}'.");

    public SpriteFontBase Font(float size, bool bold = false) => (bold ? _bold : _regular).GetFont(size);

    public void Dispose()
    {
        foreach (var texture in _textures.Values)
        {
            texture.Dispose();
        }
        Pixel.Dispose();
        Circle.Dispose();
        _regular.Dispose();
        _bold.Dispose();
    }

    private static string ReadText(string path)
    {
        using var reader = new StreamReader(Open(path));
        return reader.ReadToEnd();
    }

    private static byte[] ReadBytes(string path)
    {
        using var stream = Open(path);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static Texture2D CreateCircle(GraphicsDevice device, int size)
    {
        var data = new Color[size * size];
        var r = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                data[y * size + x] = Color.White * Math.Clamp(r - d, 0f, 1f);
            }
        }
        var texture = new Texture2D(device, size, size);
        texture.SetData(data);
        return texture;
    }

    private sealed record SpriteDto(string Texture, int X, int Y, int W, int H, double PivotX, double PivotY, double Ppu);

    private sealed record FrameDto(double Time, string Sprite);

    private sealed record AnimationDto(bool Loop, double Length, List<FrameDto> Frames);
}
