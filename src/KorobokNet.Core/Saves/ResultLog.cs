using System.Globalization;
using KorobokNet.Core.Programs;

namespace KorobokNet.Core.Saves;

/// <summary>
/// Журнал результатов для учителя, как в оригинале: KorobokResult/&lt;игрок&gt;.txt, одна строка на прохождение.
/// </summary>
public sealed class ResultLog(string directory)
{
    public string Directory { get; } = directory;

    public static string FormatLine(int level, ProgramStats stats, int stars, DateTime time) =>
        $"Уровень: {level} | действий: {stats.Actions} | циклов: {stats.Loops} | условий: {stats.Ifs}"
        + $" | баллы: {stars} | время: {time.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)}";

    public string FilePathFor(string user)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(user.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return Path.Combine(Directory, (safe.Length == 0 ? "_" : safe) + ".txt");
    }

    public void Append(string user, int level, ProgramStats stats, int stars, DateTime time)
    {
        System.IO.Directory.CreateDirectory(Directory);
        File.AppendAllText(FilePathFor(user), FormatLine(level, stats, stars, time) + Environment.NewLine);
    }

    public void Delete(string user)
    {
        var path = FilePathFor(user);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
