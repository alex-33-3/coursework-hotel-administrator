using Hotel.Views;

namespace Hotel.Forms;

internal sealed class LoginForm : DialogForm, ILoginView
{
    private readonly TextBox _login = new() { Name = "login" };
    private readonly TextBox _password = new() { Name = "password", UseSystemPasswordChar = true };
    private readonly Label _error = new() { AutoSize = true, ForeColor = Palette.Error, MaximumSize = new Size(380, 0) };

    public LoginForm()
    {
        Text = "Администратор гостиницы — вход";
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;

        var title = new Label
        {
            Text = "Вход в программу",
            Font = new Font(Font.FontFamily, 13, FontStyle.Bold),
            ForeColor = Palette.Accent,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 8)
        };
        var fields = Ui.Fields();
        Ui.AddField(fields, "Логин", _login);
        Ui.AddField(fields, "Пароль", _password);
        fields.RowCount++;
        fields.Controls.Add(_error, 1, fields.RowCount - 1);

        Compose(title, fields, OkCancel("Войти", () => LoginClicked?.Invoke(this, EventArgs.Empty), "Выход"));
    }

    public event EventHandler? LoginClicked;

    public string Login => _login.Text;

    public string Password => _password.Text;

    public void ShowError(string message)
    {
        _error.Text = message;
        _password.SelectAll();
        _password.Focus();
    }
}
