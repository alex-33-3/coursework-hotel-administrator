using System.Text.RegularExpressions;
using Hotel.Core.Logging;
using Hotel.Core.Models;
using Hotel.Core.Security;
using Hotel.Core.Storage;
using Hotel.Core.Validation;

namespace Hotel.Core.Services;

/// <summary>Кто сейчас работает: имя печатается в квитанции и брони.</summary>
public interface IUserContext
{
    string CurrentUserName { get; }
}

public interface IAuthService : IUserContext
{
    /// <summary>Пользователь, который сейчас работает в программе; <c>null</c> — вход не выполнен.</summary>
    UserAccount? CurrentUser { get; }

    bool IsManager { get; }

    IReadOnlyList<UserAccount> Users { get; }

    UserAccount Login(string login, string password);

    void Logout();

    UserAccount AddUser(string login, string fullName, string password, UserRole role);

    void ChangePassword(string login, string newPassword);

    /// <summary>Удаляет учетную запись; нельзя удалить себя и последнего управляющего.</summary>
    void DeleteUser(string login);

    /// <summary>
    /// Если учетных записей нет, создает управляющего с паролем по умолчанию, чтобы в программу можно было войти.
    /// </summary>
    bool EnsureManager();
}

public sealed partial class AuthService : IAuthService
{
    public const string DefaultManagerLogin = "manager";
    public const string DefaultManagerPassword = "manager123";
    public const int MinPasswordLength = 6;

    private readonly HotelContext _context;
    private readonly IAuditLog _log;

    public AuthService(HotelContext context, IAuditLog log)
    {
        _context = context;
        _log = log;
    }

    [GeneratedRegex("^[a-z0-9._-]{3,30}$")]
    private static partial Regex LoginPattern();

    public UserAccount? CurrentUser { get; private set; }

    public string CurrentUserName => CurrentUser?.FullName ?? "—";

    public bool IsManager => CurrentUser?.Role == UserRole.Manager;

    public IReadOnlyList<UserAccount> Users =>
        _context.Users.OrderByDescending(u => u.Role).ThenBy(u => u.Login, StringComparer.Ordinal).ToList();

    public UserAccount Login(string login, string password)
    {
        var normalized = Normalize(login);
        var user = _context.Users.FirstOrDefault(u => u.Login == normalized);
        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            _log.Write($"Неудачная попытка входа: {normalized}");
            throw new HotelException("Неверный логин или пароль.");
        }

        CurrentUser = user;
        _log.Write($"Вход: {user.Login} ({user.Role.GetDescription()})");
        return user;
    }

    public void Logout()
    {
        if (CurrentUser is null)
            return;

        _log.Write($"Выход: {CurrentUser.Login}");
        CurrentUser = null;
    }

    public UserAccount AddUser(string login, string fullName, string password, UserRole role)
    {
        var normalized = Normalize(login);
        var errors = new List<ValidationError>();
        if (!LoginPattern().IsMatch(normalized))
            errors.Add(new("Login", "Логин: от 3 до 30 латинских букв, цифр, точек, дефисов или подчеркиваний."));
        else if (_context.Users.Any(u => u.Login == normalized))
            errors.Add(new("Login", $"Логин «{normalized}» уже занят."));

        if (string.IsNullOrWhiteSpace(fullName))
            errors.Add(new("FullName", "Укажите ФИО сотрудника."));
        if (!Enum.IsDefined(role))
            errors.Add(new("Role", "Выберите роль."));
        AddPasswordErrors(errors, password);
        if (errors.Count > 0)
            throw new DataValidationException(errors);

        var (hash, salt) = PasswordHasher.Hash(password);
        var user = new UserAccount
        {
            Login = normalized,
            FullName = fullName.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role
        };

        _context.Users.Add(user);
        _context.SaveChanges();
        _log.Write($"Создана учетная запись {user}");
        return user;
    }

    public void ChangePassword(string login, string newPassword)
    {
        var user = GetUser(login);
        var errors = new List<ValidationError>();
        AddPasswordErrors(errors, newPassword);
        if (errors.Count > 0)
            throw new DataValidationException(errors);

        var (hash, salt) = PasswordHasher.Hash(newPassword);
        var updated = user with { PasswordHash = hash, PasswordSalt = salt };
        _context.Users[_context.Users.IndexOf(user)] = updated;
        if (CurrentUser?.Login == user.Login)
            CurrentUser = updated;

        _context.SaveChanges();
        _log.Write($"Изменен пароль пользователя {user.Login}");
    }

    public void DeleteUser(string login)
    {
        var user = GetUser(login);
        if (CurrentUser?.Login == user.Login)
            throw new HotelException("Нельзя удалить свою учетную запись.");
        if (user.Role == UserRole.Manager && _context.Users.Count(u => u.Role == UserRole.Manager) == 1)
            throw new HotelException("Нельзя удалить последнего управляющего.");

        _context.Users.Remove(user);
        _context.SaveChanges();
        _log.Write($"Удалена учетная запись {user.Login}");
    }

    public bool EnsureManager()
    {
        if (_context.Users.Count > 0)
            return false;

        AddUser(DefaultManagerLogin, "Управляющий", DefaultManagerPassword, UserRole.Manager);
        return true;
    }

    private UserAccount GetUser(string login)
    {
        var normalized = Normalize(login);
        return _context.Users.FirstOrDefault(u => u.Login == normalized)
               ?? throw new HotelException($"Учетная запись «{normalized}» не найдена.");
    }

    private static void AddPasswordErrors(List<ValidationError> errors, string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            errors.Add(new("Password", $"Пароль должен быть не короче {MinPasswordLength} символов."));
    }

    private static string Normalize(string login) => login.Trim().ToLowerInvariant();
}
