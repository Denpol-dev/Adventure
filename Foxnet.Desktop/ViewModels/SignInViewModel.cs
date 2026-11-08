using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Foxnet.Session;

namespace Foxnet.Desktop.ViewModels;

public sealed class PasswordInput(string password, string confirmation)
{
    public string Password { get; } = password;
    public string Confirmation { get; } = confirmation;
}

public sealed partial class SignInViewModel(LocalSession session, SignInState state, Action enterWorkspace) : ScreenViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(ShowLogin), nameof(ShowConfirmation), nameof(PasswordLabel), nameof(ButtonLabel))]
    private SignInMode _mode = state.Mode;

    [ObservableProperty] private string _login = state.SuggestedLogin ?? "";

    public string Title => ShowConfirmation ? "Восстановление доступа" : "Авторизация FOXNET";
    public bool ShowLogin => Mode != SignInMode.Password;
    public bool ShowConfirmation => Mode == SignInMode.Recovery;
    public string PasswordLabel => ShowConfirmation ? "Новый пароль" : "Пароль";
    public string ButtonLabel => ShowConfirmation ? "Сменить пароль" : "Войти";

    [RelayCommand]
    private Task SubmitAsync(PasswordInput? input)
    {
        if (input == null)
            return Task.CompletedTask;

        string login = Login;
        return RunAsync(async () =>
        {
            switch (Mode)
            {
                case SignInMode.FirstSignIn:
                    await Task.Run(() => session.CreateAccount(login, input.Password));
                    break;
                case SignInMode.Password:
                    var result = await Task.Run(() => session.CheckPassword(input.Password));
                    if (result != PasswordResult.Accepted)
                    {
                        if (result == PasswordResult.RecoveryRequired)
                        {
                            Login = "";
                            Mode = SignInMode.Recovery;
                        }
                        Error = "Неверный пароль.";
                        return;
                    }
                    break;
                case SignInMode.Recovery:
                    await Task.Run(() => session.RecoverAccount(login, input.Password, input.Confirmation));
                    break;
            }
            enterWorkspace();
        });
    }
}
