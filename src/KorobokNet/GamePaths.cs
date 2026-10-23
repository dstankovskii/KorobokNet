namespace KorobokNet;

/// <summary>Где игра хранит сохранения и скриншоты.</summary>
public static class GamePaths
{
    /// <summary>%APPDATA%\KorobokNet - сохранения игроков.</summary>
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KorobokNet");

    public static string SaveFile { get; } = Path.Combine(DataDirectory, "save.json");

    /// <summary>Изображения\KorobokNet - скриншоты по F12.</summary>
    public static string ScreenshotsDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "KorobokNet");
}
