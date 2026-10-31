using System.ComponentModel;

namespace Hotel.Core.Models;

public enum UserRole
{
    [Description("Администратор")]
    Administrator,

    [Description("Управляющий")]
    Manager
}

/// <summary>
/// Учетная запись. Пароль хранится только в виде хеша PBKDF2 с солью; ФИО печатается в квитанции.
/// </summary>
public sealed record UserAccount
{
    public required string Login { get; init; }
    public required string FullName { get; init; }
    public required string PasswordHash { get; init; }
    public required string PasswordSalt { get; init; }
    public required UserRole Role { get; init; }

    public override string ToString() => $"{Login} — {FullName} ({Role.GetDescription()})";
}
