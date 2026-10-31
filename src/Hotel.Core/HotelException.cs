using Hotel.Core.Validation;

namespace Hotel.Core;

/// <summary>
/// Нарушение правил предметной области: сообщение показывается пользователю как есть.
/// </summary>
public class HotelException : Exception
{
    public HotelException(string message) : base(message)
    {
    }
}

public sealed class DataValidationException : HotelException
{
    public DataValidationException(IReadOnlyList<ValidationError> errors)
        : base("Данные заполнены с ошибками:" + Environment.NewLine +
               string.Join(Environment.NewLine, errors.Select(e => "– " + e.Message)))
    {
        Errors = errors;
    }

    public IReadOnlyList<ValidationError> Errors { get; }
}
