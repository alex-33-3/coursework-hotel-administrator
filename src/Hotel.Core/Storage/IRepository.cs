namespace Hotel.Core.Storage;

/// <summary>Общая часть хранилищ, не зависящая от типа записей.</summary>
public interface IRepositoryBase
{
    string Location { get; }

    bool Exists { get; }

    /// <summary>Отложить поврежденный файл, чтобы начать с пустого.</summary>
    void Reset();
}

public interface IRepository<T> : IRepositoryBase
{
    List<T> Load();

    void Save(IEnumerable<T> items);
}

public sealed class StorageException : Exception
{
    public StorageException(string location, string message, Exception? inner = null)
        : base(message, inner)
    {
        Location = location;
    }

    public string Location { get; }
}
