namespace KorobokNet.Engine;

/// <summary>Длительность текущего кадра (для плавных движений внутри корутин).</summary>
public static class Frame
{
    public static float Dt { get; set; } = 1f / 60f;
}
