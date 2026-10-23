namespace KorobokNet;

/// <summary>
/// Параметры запуска.
/// <c>--tour &lt;папка&gt;</c> - пройти по всем экранам, сохранить их скриншоты в папку и выйти
/// (сохранения при этом пишутся в ту же папку, настоящий прогресс игроков не трогается).
/// </summary>
public sealed record GameOptions(string SaveFile, string? TourDirectory)
{
    public static GameOptions Parse(string[] args)
    {
        var tourIndex = Array.IndexOf(args, "--tour");
        if (tourIndex >= 0)
        {
            var dir = Path.GetFullPath(tourIndex + 1 < args.Length ? args[tourIndex + 1] : "tour");
            return new GameOptions(Path.Combine(dir, "save.json"), dir);
        }
        return new GameOptions(GamePaths.SaveFile, null);
    }
}
