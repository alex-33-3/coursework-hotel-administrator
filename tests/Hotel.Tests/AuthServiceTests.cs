using Hotel.Core;
using Hotel.Core.Models;

namespace Hotel.Tests;

public class AuthServiceTests
{
    [Fact]
    public void AddUser_StoresOnlyHash_AndLoginWorks()
    {
        var hotel = new TestHotel();

        var user = hotel.Auth.AddUser(" Admin ", "Ковалева Анна", "secret1", UserRole.Administrator);

        Assert.Equal("admin", user.Login);
        Assert.DoesNotContain("secret1", user.PasswordHash, StringComparison.Ordinal);
        Assert.Equal(user, hotel.Auth.Login("ADMIN", "secret1"));
        Assert.Equal("Ковалева Анна", hotel.Auth.CurrentUserName);
        Assert.False(hotel.Auth.IsManager);
        Assert.DoesNotContain(hotel.Log.Lines, l => l.Contains("secret1", StringComparison.Ordinal));
    }

    [Fact]
    public void Login_WrongPassword_Rejected_AndLogged()
    {
        var hotel = new TestHotel();
        hotel.Auth.AddUser("admin", "Ковалева Анна", "secret1", UserRole.Administrator);

        Assert.Throws<HotelException>(() => hotel.Auth.Login("admin", "wrong"));
        Assert.Null(hotel.Auth.CurrentUser);
        Assert.Contains(hotel.Log.Lines, l => l.StartsWith("Неудачная попытка входа", StringComparison.Ordinal));
    }

    [Fact]
    public void AddUser_InvalidData_Rejected()
    {
        var hotel = new TestHotel();
        hotel.Auth.AddUser("admin", "Ковалева Анна", "secret1", UserRole.Administrator);

        var ex = Assert.Throws<DataValidationException>(() => hotel.Auth.AddUser("admin", "", "123", UserRole.Manager));
        Assert.Equal(["Login", "FullName", "Password"], ex.Errors.Select(e => e.Field));
    }

    [Fact]
    public void DeleteUser_CannotDeleteSelfOrLastManager()
    {
        var hotel = new TestHotel();
        hotel.Auth.AddUser("boss", "Литвинов Олег", "secret1", UserRole.Manager);
        hotel.Auth.AddUser("admin", "Ковалева Анна", "secret1", UserRole.Administrator);
        hotel.Auth.Login("admin", "secret1");

        Assert.Throws<HotelException>(() => hotel.Auth.DeleteUser("admin"));
        Assert.Throws<HotelException>(() => hotel.Auth.DeleteUser("boss"));
    }

    [Fact]
    public void ChangePassword_ThenOldPasswordFails()
    {
        var hotel = new TestHotel();
        hotel.Auth.AddUser("admin", "Ковалева Анна", "secret1", UserRole.Administrator);

        hotel.Auth.ChangePassword("admin", "secret2");

        Assert.Throws<HotelException>(() => hotel.Auth.Login("admin", "secret1"));
        hotel.Auth.Login("admin", "secret2");
    }

    [Fact]
    public void EnsureManager_CreatesDefaultOnlyWhenNoUsers()
    {
        var hotel = new TestHotel();

        Assert.True(hotel.Auth.EnsureManager());
        Assert.False(hotel.Auth.EnsureManager());
        Assert.True(hotel.Auth.Login("manager", "manager123").Role == UserRole.Manager);
    }
}
