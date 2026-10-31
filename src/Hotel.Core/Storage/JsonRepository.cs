using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hotel.Core.Storage;

public sealed class JsonRepository<T> : IRepository<T>
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },

        // Строгий формат: лишние поля, null в обязательных полях и пропущенные параметры
        // конструктора считаются повреждением файла, а не молча заменяются значениями по умолчанию.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    private readonly string _filePath;

    public JsonRepository(string filePath)
    {
        _filePath = filePath;
    }

    public string Location => _filePath;

    public bool Exists => File.Exists(_filePath);

    public List<T> Load()
    {
        if (!Exists)
            return new List<T>();

        try
        {
            using var stream = File.OpenRead(_filePath);
            var items = JsonSerializer.Deserialize<List<T>>(stream, Options);
            if (items is null || items.Any(i => i is null))
                throw new StorageException(_filePath, "Файл не содержит списка записей.");
            return items;
        }
        catch (JsonException ex)
        {
            throw new StorageException(_filePath, $"Файл поврежден: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException(_filePath, $"Не удалось прочитать файл: {ex.Message}", ex);
        }
    }

    public void Save(IEnumerable<T> items)
    {
        var tempPath = _filePath + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (var stream = File.Create(tempPath))
                JsonSerializer.Serialize(stream, items.ToList(), Options);

            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException(_filePath, $"Не удалось сохранить файл: {ex.Message}", ex);
        }
    }

    public void Reset()
    {
        if (Exists)
            File.Move(_filePath, $"{_filePath}.corrupt-{DateTime.Now:yyyyMMddHHmmss}");
    }
}
