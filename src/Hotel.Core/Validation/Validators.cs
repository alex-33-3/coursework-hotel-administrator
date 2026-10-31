using System.Text.RegularExpressions;
using Hotel.Core.Models;

namespace Hotel.Core.Validation;

public sealed record ValidationError(string Field, string Message);

/// <summary>Проверка номера: номер комнаты, этаж, класс, число мест.</summary>
public sealed partial class RoomValidator
{
    public const int MaxFloor = 30;
    public const int MaxPlaces = 6;
    private const int MaxDescriptionLength = 200;

    [GeneratedRegex(@"^\d{1,4}\p{L}?$")]
    private static partial Regex NumberPattern();

    public IReadOnlyList<ValidationError> Validate(Room room)
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(room.Number))
            errors.Add(new(nameof(Room.Number), "Укажите номер комнаты."));
        else if (!NumberPattern().IsMatch(room.Number))
            errors.Add(new(nameof(Room.Number), "Номер комнаты: до 4 цифр и, при необходимости, буква, например 101 или 204а."));

        if (room.Floor is < 1 or > MaxFloor)
            errors.Add(new(nameof(Room.Floor), $"Этаж — от 1 до {MaxFloor}."));
        if (!Enum.IsDefined(room.Class))
            errors.Add(new(nameof(Room.Class), "Выберите класс номера."));
        if (room.Places is < 1 or > MaxPlaces)
            errors.Add(new(nameof(Room.Places), $"Число мест — от 1 до {MaxPlaces}."));
        if (room.Description?.Length > MaxDescriptionLength)
            errors.Add(new(nameof(Room.Description), $"Описание длиннее {MaxDescriptionLength} символов."));

        return errors;
    }
}

/// <summary>Проверка гостя: ФИО, дата рождения, гражданство, паспорт, телефон.</summary>
public sealed partial class GuestValidator
{
    private const int MaxNameLength = 50;
    private const int MaxTextLength = 150;
    private const int MaxAge = 120;

    [GeneratedRegex(@"^\p{L}+(['’ -]\p{L}+)*$")]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^[A-Z]{2}\d{7}$")]
    private static partial Regex BelarusPassportPattern();

    [GeneratedRegex(@"^[\p{L}\d]{5,20}$")]
    private static partial Regex ForeignPassportPattern();

    public IReadOnlyList<ValidationError> Validate(Guest guest, DateOnly today)
    {
        var errors = new List<ValidationError>();
        ValidateName(errors, nameof(Guest.LastName), "Фамилия", guest.LastName, required: true);
        ValidateName(errors, nameof(Guest.FirstName), "Имя", guest.FirstName, required: true);
        ValidateName(errors, nameof(Guest.MiddleName), "Отчество", guest.MiddleName, required: false);

        if (guest.BirthDate == default)
            errors.Add(new(nameof(Guest.BirthDate), "Укажите дату рождения."));
        else if (guest.BirthDate > today)
            errors.Add(new(nameof(Guest.BirthDate), "Дата рождения не может быть в будущем."));
        else if (guest.BirthDate.AddYears(MaxAge) < today)
            errors.Add(new(nameof(Guest.BirthDate), $"Возраст гостя не может быть больше {MaxAge} лет."));

        if (string.IsNullOrWhiteSpace(guest.Citizenship))
            errors.Add(new(nameof(Guest.Citizenship), "Укажите гражданство."));
        else if (guest.Citizenship.Length > MaxTextLength)
            errors.Add(new(nameof(Guest.Citizenship), $"Гражданство длиннее {MaxTextLength} символов."));

        if (string.IsNullOrWhiteSpace(guest.PassportNumber))
            errors.Add(new(nameof(Guest.PassportNumber), "Укажите серию и номер паспорта."));
        else if (guest.IsBelarusCitizen && !BelarusPassportPattern().IsMatch(guest.PassportNumber))
            errors.Add(new(nameof(Guest.PassportNumber), "Паспорт гражданина РБ: 2 латинские буквы и 7 цифр, например MP1234567."));
        else if (!guest.IsBelarusCitizen && !ForeignPassportPattern().IsMatch(guest.PassportNumber))
            errors.Add(new(nameof(Guest.PassportNumber), "Номер паспорта: от 5 до 20 букв и цифр без пробелов."));

        if (string.IsNullOrWhiteSpace(guest.PassportIssuedBy))
            errors.Add(new(nameof(Guest.PassportIssuedBy), "Укажите, кем выдан паспорт."));
        else if (guest.PassportIssuedBy.Length > MaxTextLength)
            errors.Add(new(nameof(Guest.PassportIssuedBy), $"Поле «Кем выдан» длиннее {MaxTextLength} символов."));

        if (guest.PassportIssueDate == default)
            errors.Add(new(nameof(Guest.PassportIssueDate), "Укажите дату выдачи паспорта."));
        else if (guest.PassportIssueDate > today)
            errors.Add(new(nameof(Guest.PassportIssueDate), "Дата выдачи паспорта не может быть в будущем."));
        else if (guest.BirthDate != default && guest.PassportIssueDate < guest.BirthDate)
            errors.Add(new(nameof(Guest.PassportIssueDate), "Дата выдачи паспорта не может быть раньше даты рождения."));

        if (!string.IsNullOrEmpty(guest.Phone) && !PhoneFormat.IsValid(guest.Phone))
            errors.Add(new(nameof(Guest.Phone), PhoneFormat.Hint));

        return errors;
    }

    /// <summary>Приводит номер паспорта к виду без пробелов, заглавными буквами.</summary>
    public static string NormalizePassport(string passport) =>
        new(passport.Where(c => !char.IsWhiteSpace(c) && c != '-').Select(char.ToUpperInvariant).ToArray());

    internal static void ValidateName(List<ValidationError> errors, string field, string title, string? value, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
                errors.Add(new(field, $"Укажите поле «{title}»."));
            return;
        }

        if (value.Length > MaxNameLength)
            errors.Add(new(field, $"Поле «{title}» длиннее {MaxNameLength} символов."));
        else if (!NamePattern().IsMatch(value))
            errors.Add(new(field, $"Поле «{title}» может содержать только буквы, пробел, дефис и апостроф."));
    }
}

/// <summary>Проверка брони: контакт, телефон, даты и число мест (свободные места проверяет сервис).</summary>
public sealed partial class ReservationValidator
{
    public const int MaxDaysAhead = 365;
    private const int MaxContactLength = 100;
    private const int MaxNoteLength = 300;

    [GeneratedRegex(@"^\p{L}+(['’. -]+\p{L}+)*\.?$")]
    private static partial Regex ContactPattern();

    public IReadOnlyList<ValidationError> Validate(ReservationDetails details, DateOnly today)
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(details.ContactName))
            errors.Add(new(nameof(details.ContactName), "Укажите контактное лицо."));
        else if (details.ContactName.Length > MaxContactLength || !ContactPattern().IsMatch(details.ContactName))
            errors.Add(new(nameof(details.ContactName), "Контактное лицо: ФИО буквами, не длиннее 100 символов."));

        if (!PhoneFormat.IsValid(details.Phone))
            errors.Add(new(nameof(details.Phone), PhoneFormat.Hint));

        if (string.IsNullOrWhiteSpace(details.RoomNumber))
            errors.Add(new(nameof(details.RoomNumber), "Выберите номер."));

        StayDates.Validate(errors, details.Arrival, details.Departure, today, today.AddDays(MaxDaysAhead));

        if (details.Places < 1)
            errors.Add(new(nameof(details.Places), "Число мест — не меньше 1."));
        if (details.Note?.Length > MaxNoteLength)
            errors.Add(new(nameof(details.Note), $"Примечание длиннее {MaxNoteLength} символов."));

        return errors;
    }
}

/// <summary>Правила дат проживания: приезд в допустимом окне, отъезд позже, срок не больше 90 суток.</summary>
public static class StayDates
{
    public static void Validate(List<ValidationError> errors, DateOnly arrival, DateOnly departure, DateOnly earliest, DateOnly latest)
    {
        if (arrival < earliest)
            errors.Add(new("Arrival", $"Дата приезда не может быть раньше {earliest:dd.MM.yyyy}."));
        else if (arrival > latest)
            errors.Add(new("Arrival", $"Дата приезда не может быть позже {latest:dd.MM.yyyy}."));

        if (departure <= arrival)
            errors.Add(new("Departure", "Дата отъезда должна быть позже даты приезда."));
        else if (Billing.StayCalculator.Nights(arrival, departure) > Billing.StayCalculator.MaxNights)
            errors.Add(new("Departure", $"Срок проживания — не больше {Billing.StayCalculator.MaxNights} суток."));
    }
}

/// <summary>Телефон в международном формате: + и 7–15 цифр.</summary>
public static partial class PhoneFormat
{
    public const string Hint = "Телефон: + и от 7 до 15 цифр, например +375291234567.";

    [GeneratedRegex(@"^\+\d{7,15}$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? phone) => phone is not null && Pattern().IsMatch(phone);

    /// <summary>Убирает пробелы, скобки и дефисы: «+375 (29) 123-45-67» → «+375291234567».</summary>
    public static string Normalize(string phone) => new(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
}
