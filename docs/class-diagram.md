# Диаграмма классов

Диаграмма в формате Mermaid отображается на GitHub и в VS Code (расширение Markdown Preview Mermaid).

Обозначения: `+` — public, `~` — internal (меняет состояние, доступно только сервисам ядра), `-` — private.

## Предметная область (`Hotel.Core.Models`)

```mermaid
classDiagram
    direction LR

    class RoomClass {
        <<enumeration>>
        Economy
        Standard
        Superior
        JuniorSuite
        Suite
    }

    class Room {
        <<record>>
        +string Number
        +int Floor
        +RoomClass Class
        +int Places
        +string? Description
    }

    class Tariff {
        <<record>>
        +RoomClass Class
        +decimal PricePerPlace
    }

    class Guest {
        <<record>>
        +int Id
        +string LastName
        +string FirstName
        +string? MiddleName
        +DateOnly BirthDate
        +string Citizenship
        +string PassportNumber
        +string PassportIssuedBy
        +DateOnly PassportIssueDate
        +string? Phone
        +string FullName
        +bool IsBelarusCitizen
    }

    class ReservationStatus {
        <<enumeration>>
        Reserved
        CheckedIn
        Cancelled
        NoShow
    }

    class Reservation {
        <<aggregate root>>
        +int Number
        +string ContactName
        +string Phone
        +string RoomNumber
        +DateOnly Arrival
        +DateOnly Departure
        +int Places
        +decimal PricePerPlace
        +ReservationStatus Status
        +int Nights
        +decimal Total
        +HoldsPlaces(DateOnly today) bool
        +CoversNight(DateOnly night) bool
        ~Update(ReservationDetails, decimal)
        ~SetStatus(ReservationStatus)
    }

    class StayStatus {
        <<enumeration>>
        Active
        CheckedOut
    }

    class Stay {
        <<aggregate root>>
        +int Id
        +int GuestId
        +string RoomNumber
        +int? ReservationNumber
        +DateOnly Arrival
        +DateOnly Departure
        +DateTime? CheckedOutAt
        +decimal PricePerPlace
        +StayStatus Status
        +int PaidNights
        +IsOverdue(DateOnly today) bool
        +CoversNight(DateOnly night, DateOnly today) bool
        ~Extend(DateOnly)
        ~CheckOut(DateTime)
    }

    class ReceiptKind {
        <<enumeration>>
        Accommodation
        Extension
        Surcharge
        Refund
    }

    class Receipt {
        <<record>>
        +int Number
        +int StayId
        +ReceiptKind Kind
        +IReadOnlyList~ReceiptLine~ Lines
        +DateTime IssuedAt
        +string IssuedBy
        +decimal Total
        +decimal SignedTotal
    }

    class ReceiptLine {
        <<record>>
        +string Description
        +decimal Quantity
        +decimal Price
        +decimal Amount
    }

    class UserAccount {
        <<record>>
        +string Login
        +string FullName
        +string PasswordHash
        +string PasswordSalt
        +UserRole Role
    }

    Room --> RoomClass
    Tariff --> RoomClass
    Reservation --> Room : RoomNumber
    Reservation --> ReservationStatus
    Stay --> Guest : GuestId
    Stay --> Room : RoomNumber
    Stay --> Reservation : ReservationNumber
    Stay --> StayStatus
    Receipt --> Stay : StayId
    Receipt *-- ReceiptLine
    Receipt --> ReceiptKind
```

## Логика и хранение (`Hotel.Core`)

```mermaid
classDiagram
    direction TB

    class OccupancyCalculator {
        +OccupiedPlaces(string room, DateOnly night, OccupancyExclusion?) int
        +FreePlaces(Room, DateOnly from, DateOnly to, OccupancyExclusion?) int
        +MaxFutureOccupancy(string room) int
        +Today(Room) RoomOccupancy
    }

    class StayCalculator {
        <<static>>
        +TimeOnly CheckOutHour = 12:00
        +TimeOnly LateHalfDayUntil = 18:00
        +Nights(DateOnly, DateOnly) int
        +LateFraction(TimeOnly) decimal
        +Settle(Stay, DateTime) Settlement
    }

    class Settlement {
        <<record>>
        +int PaidNights
        +int ChargedNights
        +decimal LateFraction
        +decimal Paid
        +decimal Charged
        +decimal Difference
        +bool IsEarly
        +ReceiptKind? ReceiptKind
        +ReceiptLines() IReadOnlyList~ReceiptLine~
    }

    class IRoomService {
        <<interface>>
        +GetOccupancyToday(RoomClass?) IReadOnlyList~RoomOccupancy~
        +FindAvailable(RoomQuery) IReadOnlyList~RoomAvailability~
        +Add(Room)
        +Update(Room)
        +Delete(string)
    }

    class IReservationService {
        <<interface>>
        +Find(ReservationFilter) IReadOnlyList~Reservation~
        +GetArrivalsToday() IReadOnlyList~Reservation~
        +Create(ReservationDetails) Reservation
        +Update(int, ReservationDetails)
        +Cancel(int)
        +ExpireNoShows() int
    }

    class IStayService {
        <<interface>>
        +GetDeparturesToday() IReadOnlyList~GuestRow~
        +CheckIn(CheckInRequest) CheckInResult
        +Extend(int stayId, DateOnly) Receipt
        +PreviewCheckOut(int stayId) Settlement
        +CheckOut(int stayId) CheckOutResult
    }

    class IGuestService {
        <<interface>>
        +FindByPassport(string) Guest?
        +GetRows(GuestListFilter) IReadOnlyList~GuestRow~
        +Register(Guest) Guest
        +Update(Guest)
        +Delete(int)
    }

    class GuestSearch {
        <<static>>
        +Search(rows, GuestField, string?) IReadOnlyList~GuestRow~
    }

    class ITariffService {
        <<interface>>
        +GetPrice(RoomClass) decimal
        +Update(RoomClass, decimal)
    }

    class IReceiptService {
        <<interface>>
        +GetCard(int) ReceiptCard
        +GetForGuest(int) IReadOnlyList~Receipt~
        +Find(DateOnly, DateOnly) IReadOnlyList~Receipt~
        +Summarize(receipts) ReceiptSummary
    }

    class IAuthService {
        <<interface>>
        +UserAccount? CurrentUser
        +bool IsManager
        +Login(string, string) UserAccount
        +AddUser(...) UserAccount
        +ChangePassword(string, string)
    }

    class HotelContext {
        +List~Room~ Rooms
        +List~Tariff~ Tariffs
        +List~Guest~ Guests
        +List~Reservation~ Reservations
        +List~Stay~ Stays
        +List~Receipt~ Receipts
        +List~UserAccount~ Users
        +event Changed
        +Load()
        +SaveChanges()
        +ResetStorage(string)
    }

    class IRepository~T~ {
        <<interface>>
        +Load() List~T~
        +Save(IEnumerable~T~)
    }

    class JsonRepository~T~
    class IClock {
        <<interface>>
        +DateTime Now
        +DateOnly Today
    }

    StayCalculator ..> Settlement : создает
    IStayService ..> StayCalculator : Settle
    IStayService ..> OccupancyCalculator
    IReservationService ..> OccupancyCalculator
    IRoomService ..> OccupancyCalculator
    IRoomService ..> ITariffService
    OccupancyCalculator --> HotelContext
    OccupancyCalculator --> IClock
    HotelContext o-- IRepository~T~
    JsonRepository~T~ ..|> IRepository~T~
```

## Представление (`Hotel`) — Model-View-Presenter

```mermaid
classDiagram
    direction TB

    class Presenter {
        <<abstract>>
        #AppServices Services
        #IDialogService Dialogs
        #Try(Action, IValidatingView?) bool
    }

    class MainPresenter {
        +bool LogoutRequested
        +Refresh()
    }
    class CheckInPresenter {
        +IReadOnlyList~int~ IssuedReceipts
    }
    class CheckOutPresenter {
        +IReadOnlyList~int~ IssuedReceipts
        +string? Notice
    }
    class RoomSearchPresenter
    class GuestsTabPresenter

    class IMainView {
        <<interface>>
    }
    class ICheckInView {
        <<interface>>
    }
    class ICheckOutView {
        <<interface>>
    }

    class MainForm
    class CheckInForm
    class CheckOutForm

    class INavigator {
        <<interface>>
        +FindRoom(bool)
        +CheckIn(CheckInTarget) bool
        +CheckOut(int) bool
        +ShowReceipts(IReadOnlyList~int~)
    }
    class Navigator {
        -Run(form, createPresenter) TPresenter?
    }
    class IIssuesReceipts {
        <<interface>>
    }

    Presenter <|-- MainPresenter
    Presenter <|-- CheckInPresenter
    Presenter <|-- CheckOutPresenter
    Presenter <|-- RoomSearchPresenter
    Presenter <|-- GuestsTabPresenter
    MainPresenter *-- GuestsTabPresenter : вкладки
    MainPresenter --> IMainView
    CheckInPresenter --> ICheckInView
    CheckOutPresenter --> ICheckOutView
    MainForm ..|> IMainView
    CheckInForm ..|> ICheckInView
    CheckOutForm ..|> ICheckOutView
    CheckInPresenter ..|> IIssuesReceipts
    CheckOutPresenter ..|> IIssuesReceipts
    Navigator ..|> INavigator
    Navigator ..> CheckInForm : создает с презентером
    MainPresenter --> INavigator
```

Остальные формы устроены так же: интерфейс представления в `Views/IViews.cs`, презентер в `Presenters`, форма в `Forms`, открытие — в `Navigator`.
