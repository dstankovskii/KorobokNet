namespace KorobokNet;

/// <summary>Где игра хранит сохранения и журнал результатов.</summary>
public static class GamePaths
{
    /// <summary>%APPDATA%\KorobokNet - сохранения игроков.</summary>
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KorobokNet");

    public static string SaveFile { get; } = Path.Combine(DataDirectory, "save.json");

    /// <summary>Документы\KorobokNet\Результаты - журнал прохождений для учителя (по файлу на игрока).</summary>
    public static string ResultsDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KorobokNet", "Результаты");

    /// <summary>Изображения\KorobokNet - скриншоты по F12.</summary>
    public static string ScreenshotsDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "KorobokNet");
}
