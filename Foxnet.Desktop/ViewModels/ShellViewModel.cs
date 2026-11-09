using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Foxnet.Session;
using System.Media;
using System.Windows;
using System.Windows.Threading;

namespace Foxnet.Desktop.ViewModels;

public enum Page { Profile, Documents, Messages, Contacts, Bank, Devices }

public sealed partial class ShellViewModel(string databasePath, string dataDirectory, IConsoleHost console) : ObservableObject, IDisposable
{
    private DispatcherTimer? _timer;
    private bool _disposed;
    private LocalSession? _session;
    private long _notificationRevision;
    private bool _wasActive;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Title), nameof(Subtitle), nameof(SectionCode))]
    private Page _page;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SignInVisible))] private bool _loading = true;
    [ObservableProperty] private bool _ready;
    [ObservableProperty] private string? _startupError;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SignInVisible))] private SignInViewModel? _signIn;
    [ObservableProperty] private ProfileViewModel? _profile;
    [ObservableProperty] private DocumentsViewModel? _documents;
    [ObservableProperty] private MessagesViewModel? _messages;
    [ObservableProperty] private ContactsViewModel? _contacts;
    [ObservableProperty] private BankViewModel? _bank;
    [ObservableProperty] private DevicesViewModel? _devices;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasUnreadMessages))] private int _unreadMessages;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasBankEvents))] private int _bankEvents;
    [ObservableProperty] private bool _soundEnabled;
    [ObservableProperty] private string? _worldError;

    public bool SignInVisible => !Loading && SignIn != null;
    public bool HasUnreadMessages => UnreadMessages > 0;
    public bool HasBankEvents => BankEvents > 0;

    public string Title => Page switch
    {
        Page.Documents => "Документы",
        Page.Messages => "Сообщения",
        Page.Contacts => "Контакты",
        Page.Bank => "HAESEI Bank",
        Page.Devices => "FOXNET Device",
        _ => "Личный кабинет"
    };

    public string Subtitle => Page switch
    {
        Page.Documents => "Полученные файлы и данные экспедиции.",
        Page.Messages => "Переписки и входящие вложения.",
        Page.Contacts => "Досье собеседников и личные заметки.",
        Page.Bank => "Баланс, платежи и история операций.",
        Page.Devices => "Удаленные устройства",
        _ => "Ваш профиль оператора в сети FOXNET."
    };

    public string SectionCode => Page switch
    {
        Page.Documents => "02  /  БИБЛИОТЕКА",
        Page.Messages => "03  /  ПЕРЕПИСКА",
        Page.Contacts => "04  /  ДОСЬЕ",
        Page.Bank => "05  /  ФИНАНСЫ",
        Page.Devices => "06  /  УСТРОЙСТВА",
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
            var initial = session.GetNotifications();
            SoundEnabled = initial.SoundEnabled;
            _notificationRevision = initial.Revision;
            session.EnterWorkspace();
            _session = session;
            Profile = new ProfileViewModel(session);
            Documents = new DocumentsViewModel(session, dataDirectory);
            Messages = new MessagesViewModel(session, OpenDocument, OpenDossier,
                () => Page == Page.Messages && Application.Current?.MainWindow?.IsActive == true);
            Contacts = new ContactsViewModel(session, OpenChat);
            Bank = new BankViewModel(session);
            Devices = new DevicesViewModel(session, new DeviceConnectionService(session, console), databasePath);
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += OnTick;
            _timer.Start();
            Ready = true;
            PollWorld();
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
        switch (page)
        {
            case Page.Devices: Devices?.Poll(); break;
            case Page.Messages: UpdateWorldView(() => Messages?.Refresh(markRead: true)); break;
            case Page.Contacts: UpdateWorldView(() => Contacts?.Refresh()); break;
            case Page.Bank: UpdateWorldView(() => Bank?.Refresh()); break;
            case Page.Documents: UpdateWorldView(() => Documents?.RefreshOffer()); break;
        }
        PollWorld();
    }

    private void OpenDocument(string eventId)
    {
        Documents!.Open(eventId);
        Navigate(Page.Documents);
    }

    private void OpenDossier(string id)
    {
        Navigate(Page.Contacts);
        UpdateWorldView(() => Contacts?.Refresh(id));
    }

    private void OpenChat(string id)
    {
        Page = Page.Messages;
        UpdateWorldView(() =>
        {
            Messages?.Refresh(markRead: false);
            Messages?.OpenContact(id);
            Messages?.Refresh(markRead: true);
        });
        PollWorld();
    }

    partial void OnSoundEnabledChanged(bool value)
    {
        UpdateWorldView(() => _session?.SetNotificationSound(value));
    }

    private void OnTick(object? sender, EventArgs args)
    {
        Devices?.Poll();
        PollWorld();
    }

    private void PollWorld() => UpdateWorldView(() =>
    {
        if (_session is not { } session)
            return;
        session.AdvanceWorld();
        var state = session.GetNotifications();
        bool active = Application.Current?.MainWindow?.IsActive == true;
        bool newEvent = state.Revision > _notificationRevision;
        if (newEvent && state.SoundEnabled)
            SystemSounds.Asterisk.Play();
        _notificationRevision = state.Revision;

        if (newEvent || active && !_wasActive)
            Messages?.Refresh(markRead: active && Page == Page.Messages);
        if (newEvent && Page == Page.Contacts)
            Contacts?.Refresh();
        if (newEvent && Page == Page.Bank)
            Bank?.Refresh();
        if (newEvent && Page == Page.Documents)
            Documents?.RefreshOffer();

        _wasActive = active;
        state = session.GetNotifications(readBank: active && Page == Page.Bank && state.BankEvents != 0);
        UnreadMessages = state.UnreadMessages;
        BankEvents = state.BankEvents;
    });

    private void UpdateWorldView(Action action)
    {
        try
        {
            action();
            WorldError = null;
        }
        catch (Exception exception) when (ScreenViewModel.IsDisplayable(exception))
        {
            WorldError = ScreenViewModel.Explain(exception);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        Devices?.Dispose();
        if (_timer == null)
            return;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
