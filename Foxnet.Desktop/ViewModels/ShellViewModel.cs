using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Foxnet.Session;
using System.Windows.Threading;

namespace Foxnet.Desktop.ViewModels;

public enum Page { Profile, Documents, Messages, Devices }

public sealed partial class ShellViewModel(string databasePath, string dataDirectory, IConsoleHost console) : ObservableObject, IDisposable
{
    private DispatcherTimer? _timer;
    private bool _disposed;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Title), nameof(Subtitle), nameof(SectionCode))]
    private Page _page;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SignInVisible))] private bool _loading = true;
    [ObservableProperty] private bool _ready;
    [ObservableProperty] private string? _startupError;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SignInVisible))] private SignInViewModel? _signIn;
    [ObservableProperty] private ProfileViewModel? _profile;
    [ObservableProperty] private DocumentsViewModel? _documents;
    [ObservableProperty] private MessagesViewModel? _messages;
    [ObservableProperty] private DevicesViewModel? _devices;

    public bool SignInVisible => !Loading && SignIn != null;

    public string Title => Page switch
    {
        Page.Documents => "Документы",
        Page.Messages => "Сообщения",
        Page.Devices => "FOXNET Device",
        _ => "Личный кабинет"
    };

    public string Subtitle => Page switch
    {
        Page.Documents => "Полученные файлы и данные экспедиции.",
        Page.Messages => "Переписки и входящие вложения.",
        Page.Devices => "Удалённые устройства",
        _ => "Ваш профиль оператора в сети FOXNET."
    };

    public string SectionCode => Page switch
    {
        Page.Documents => "02  /  БИБЛИОТЕКА",
        Page.Messages => "03  /  ПЕРЕПИСКА",
        Page.Devices => "04  /  УСТРОЙСТВА",
        _ => "01  /  ПРОФИЛЬ"
    };

    [RelayCommand]
    private async Task InitializeAsync()
    {
        Loading = true;
        StartupError = null;
        try
        {
            var (session, signInState) = await Task.Run(() =>
            {
                var store = new LocalSession(databasePath, Scenario.LoadDefault());
                store.Initialize();
                return (store, store.GetSignInState());
            });
            if (_disposed)
                return;

            SignIn = new SignInViewModel(session, signInState, () => EnterWorkspace(session));
        }
        catch (Exception exception) when (ScreenViewModel.IsDisplayable(exception))
        {
            StartupError = ScreenViewModel.Explain(exception);
        }
        finally
        {
            Loading = false;
        }
    }

    private void EnterWorkspace(LocalSession session)
    {
        if (_disposed)
            return;

        SignIn = null;
        try
        {
            Profile = new ProfileViewModel(session);
            Documents = new DocumentsViewModel(session.Documents, dataDirectory);
            Messages = new MessagesViewModel(session, OpenDocument);
            Devices = new DevicesViewModel(session, new DeviceConnectionService(session, console));
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += OnTick;
            _timer.Start();
            Ready = true;
        }
        catch (Exception exception) when (ScreenViewModel.IsDisplayable(exception))
        {
            StartupError = ScreenViewModel.Explain(exception);
        }
    }

    [RelayCommand]
    private void Navigate(Page page)
    {
        Page = page;
        if (page == Page.Devices)
            Devices?.Poll();
    }

    private void OpenDocument(string eventId)
    {
        Documents!.Open(eventId);
        Navigate(Page.Documents);
    }

    private void OnTick(object? sender, EventArgs args) => Devices?.Poll();

    public void Dispose()
    {
        _disposed = true;
        if (_timer == null)
            return;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
