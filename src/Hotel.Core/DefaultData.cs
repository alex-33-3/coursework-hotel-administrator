using Hotel.Core.Models;
using Hotel.Core.Security;
using Hotel.Core.Storage;

namespace Hotel.Core;

/// <summary>
/// Демонстрационные данные при первом запуске: тарифы, 12 номеров на 3 этажах, управляющий и администратор,
/// гости, проживания (в том числе отъезд сегодня и не выехавший в срок), брони (в том числе заезд сегодня) и квитанции.
/// Даты отсчитываются от сегодняшней, поэтому демонстрация всегда «живая».
/// </summary>
public static class DefaultData
{
    public const string ManagerLogin = "manager";
    public const string ManagerPassword = "manager123";
    public const string AdministratorLogin = "admin";
    public const string AdministratorPassword = "admin123";

    private const string AdministratorName = "Ковалева Анна Сергеевна";

    public static void Seed(HotelContext context, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);

        context.Tariffs.AddRange(
        [
            new Tariff { Class = RoomClass.Economy, PricePerPlace = 45m },
            new Tariff { Class = RoomClass.Standard, PricePerPlace = 60m },
            new Tariff { Class = RoomClass.Superior, PricePerPlace = 85m },
            new Tariff { Class = RoomClass.JuniorSuite, PricePerPlace = 120m },
            new Tariff { Class = RoomClass.Suite, PricePerPlace = 180m }
        ]);

        context.Rooms.AddRange(
        [
            Room("101", 1, RoomClass.Economy, 4, "Четырехместный, душ на этаже"),
            Room("102", 1, RoomClass.Economy, 3, null),
            Room("103", 1, RoomClass.Economy, 2, null),
            Room("104", 1, RoomClass.Standard, 2, null),
            Room("201", 2, RoomClass.Standard, 3, "Окна во двор"),
            Room("202", 2, RoomClass.Standard, 2, null),
            Room("203", 2, RoomClass.Standard, 1, "Одноместный"),
            Room("204", 2, RoomClass.Superior, 2, "Балкон"),
            Room("301", 3, RoomClass.Superior, 2, null),
            Room("302", 3, RoomClass.JuniorSuite, 2, "Гостиная зона"),
            Room("303", 3, RoomClass.JuniorSuite, 3, null),
            Room("304", 3, RoomClass.Suite, 2, "Две комнаты, вид на парк")
        ]);

        context.Users.Add(User(ManagerLogin, "Литвинов Олег Петрович", ManagerPassword, UserRole.Manager));
        context.Users.Add(User(AdministratorLogin, AdministratorName, AdministratorPassword, UserRole.Administrator));

        Guest[] guests =
        [
            Guest(1, "Петров", "Иван", "Сергеевич", new(1985, 3, 14), "MP1234567", "Первомайским РУВД г. Минска", new(2019, 6, 2), "+375291112233"),
            Guest(2, "Сидорова", "Ольга", "Николаевна", new(1990, 11, 2), "HB2345678", "Октябрьским РУВД г. Гомеля", new(2020, 1, 15), "+375447654321"),
            Guest(3, "Кузьмич", "Андрей", "Викторович", new(1978, 7, 30), "KH3456789", "Ленинским РОВД г. Гродно", new(2016, 9, 10), null),
            Guest(4, "Новик", "Елена", "Павловна", new(1995, 5, 21), "MC4567890", "Московским РУВД г. Бреста", new(2021, 4, 3), "+375255551122"),
            Guest(5, "Шмидт", "Томас", null, new(1982, 2, 8), "C01X00T47", "Stadt Berlin", new(2018, 10, 1), "+4915112345678", "Германия"),
            Guest(6, "Жук", "Дмитрий", "Олегович", new(2000, 12, 12), "AB5678901", "Центральным РУВД г. Витебска", new(2022, 2, 20), "+375336667788"),
            Guest(7, "Лапицкая", "Мария", "Игоревна", new(1988, 8, 18), "MP6789012", "Фрунзенским РУВД г. Минска", new(2017, 3, 5), "+375296543210")
        ];
        context.Guests.AddRange(guests);

        var receiptNumber = 0;
        var stayId = 0;
        void Live(int guestId, string room, int arrivalOffset, int departureOffset, bool checkedOut = false)
        {
            var roomClass = context.Rooms.First(r => r.Number == room).Class;
            var price = context.Tariffs.First(t => t.Class == roomClass).PricePerPlace;
            var arrival = today.AddDays(arrivalOffset);
            var stay = new Stay(++stayId, guestId, room, null, arrival, today.AddDays(departureOffset), price);
            context.Receipts.Add(new Receipt
            {
                Number = ++receiptNumber,
                StayId = stay.Id,
                Kind = ReceiptKind.Accommodation,
                Lines = [new ReceiptLine($"Проживание в номере {room} ({roomClass.GetDescription()}) с {arrival:dd.MM.yyyy} " +
                                         $"по {stay.Departure:dd.MM.yyyy}, суток", stay.PaidNights, price)],
                IssuedAt = arrival.ToDateTime(new TimeOnly(14, 20)),
                IssuedBy = AdministratorName
            });
            if (checkedOut)
                stay.CheckOut(stay.Departure.ToDateTime(new TimeOnly(11, 30)));
            context.Stays.Add(stay);
        }

        Live(1, "201", -3, 2);
        Live(2, "201", -2, 0);                 // отъезд сегодня
        Live(3, "104", -4, -1);                // не выехал в срок
        Live(4, "302", -1, 3);
        Live(5, "304", -5, 0);                 // отъезд сегодня
        Live(6, "101", -10, -6, checkedOut: true);
        Live(7, "203", -8, -5, checkedOut: true);

        var reservationNumber = 0;
        void Reserve(string contact, string phone, string room, int arrivalOffset, int nights, int places)
        {
            var roomClass = context.Rooms.First(r => r.Number == room).Class;
            var details = new ReservationDetails
            {
                ContactName = contact,
                Phone = phone,
                RoomNumber = room,
                Arrival = today.AddDays(arrivalOffset),
                Departure = today.AddDays(arrivalOffset + nights),
                Places = places
            };
            context.Reservations.Add(new Reservation(++reservationNumber, details,
                context.Tariffs.First(t => t.Class == roomClass).PricePerPlace, now.AddDays(-7), AdministratorName));
        }

        Reserve("Гончаров Павел Ильич", "+375297778899", "202", 0, 2, 2);     // заезд сегодня
        Reserve("Мельник Светлана", "+375441234567", "101", 0, 3, 1);         // заезд сегодня
        Reserve("Бондарь Виктор", "+375295556677", "201", 3, 4, 2);
        Reserve("Ткачук Ирина", "+375333332211", "304", 7, 2, 2);
    }

    private static Room Room(string number, int floor, RoomClass roomClass, int places, string? description) =>
        new() { Number = number, Floor = floor, Class = roomClass, Places = places, Description = description };

    private static UserAccount User(string login, string fullName, string password, UserRole role)
    {
        var (hash, salt) = PasswordHasher.Hash(password);
        return new UserAccount { Login = login, FullName = fullName, PasswordHash = hash, PasswordSalt = salt, Role = role };
    }

    private static Guest Guest(int id, string lastName, string firstName, string? middleName, DateOnly birthDate, string passport,
        string issuedBy, DateOnly issueDate, string? phone, string citizenship = Models.Guest.DefaultCitizenship) =>
        new()
        {
            Id = id,
            LastName = lastName,
            FirstName = firstName,
            MiddleName = middleName,
            BirthDate = birthDate,
            Citizenship = citizenship,
            PassportNumber = passport,
            PassportIssuedBy = issuedBy,
            PassportIssueDate = issueDate,
            Phone = phone
        };
}
