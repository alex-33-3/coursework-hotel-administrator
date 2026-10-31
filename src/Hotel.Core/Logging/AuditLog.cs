using System.Diagnostics;
using System.Text;
using Hotel.Core.Services;

namespace Hotel.Core.Logging;

public interface IAuditLog
{
    void Write(string message);
    IReadOnlyList<string> ReadAll();
}

public sealed class FileAuditLog : IAuditLog
{
    private readonly string _filePath;
    private readonly IClock _clock;

    public FileAuditLog(string filePath, IClock clock)
    {
        _filePath = filePath;
        _clock = clock;
    }

    public void Write(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var line = $"{_clock.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}";
            File.AppendAllText(_filePath, line, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Не удалось записать в журнал: {ex.Message}");
        }
    }

    public IReadOnlyList<string> ReadAll()
    {
        try
        {
            return File.Exists(_filePath) ? File.ReadAllLines(_filePath, Encoding.UTF8) : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [$"Не удалось прочитать журнал: {ex.Message}"];
        }
    }
}
