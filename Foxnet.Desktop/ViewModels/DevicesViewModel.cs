using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Foxnet.Session;

namespace Foxnet.Desktop.ViewModels;

public sealed partial class DevicesViewModel : ScreenViewModel
{
    private readonly LocalSession _session;
    private readonly DeviceConnectionService _connections;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsEmpty))] private IReadOnlyList<Device> _devices = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDevice), nameof(NeedsAuthorization), nameof(ShowConnect), nameof(ShowDisconnect),
        nameof(ConnectionLabel), nameof(AuthorizationLabel), nameof(KindLabel))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand), nameof(DisconnectCommand))]
    private Device? _selected;
    [ObservableProperty] private string _login = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotBusy))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand), nameof(DisconnectCommand))]
    private bool _busy;

    public DevicesViewModel(LocalSession session, DeviceConnectionService connections)
    {
        _session = session;
        _connections = connections;
        Refresh();
    }

    public bool IsEmpty => Devices.Count == 0;
    public bool HasDevice => Selected != null;
    public bool NotBusy => !Busy;
    public bool NeedsAuthorization => Selected is { Authorized: false };
    public bool ShowConnect => Selected is { Authorized: true, Connected: false };
    public bool ShowDisconnect => Selected is { Connected: true };
    public string ConnectionLabel => Selected?.Connected == true ? "Подключено" : "Отключено";
    public string AuthorizationLabel => Selected?.Authorized == true ? "Доступ подтверждён" : "Требуется авторизация";
    public string KindLabel => Selected?.Kind == "satellite" ? "Спутник" : Selected?.Kind ?? "";

    public void Poll()
    {
        if (Busy)
            return;
        try
        {
            Refresh();
        }
        catch (Exception exception) when (IsDisplayable(exception))
        {
            Error = Explain(exception);
        }
    }

    private bool CanConnect(string? password) => !Busy && Selected is { Connected: false };
    private bool CanDisconnect() => !Busy && Selected is { Connected: true };

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync(string? password)
    {
        if (Selected is not Device device)
            return;
        string login = Login;
        Busy = true;
        try
        {
            await RunAsync(async () =>
            {
                await Task.Run(() =>
                {
                    if (!device.Authorized && !_session.Authorize(device.Id, login.Trim(), password ?? ""))
                        throw new InvalidOperationException("Неверный логин или пароль.");

                    _connections.Connect(device.Id);
                });
            });
            PollAfterAction();
        }
        finally
        {
            Busy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        if (Selected is not Device device)
            return;
        Busy = true;
        try
        {
            await RunAsync(async () =>
            {
                await Task.Run(() => _connections.Disconnect(device.Id));
            });
            PollAfterAction();
        }
        finally
        {
            Busy = false;
        }
    }

    private void PollAfterAction()
    {
        try
        {
            Refresh();
        }
        catch (Exception exception) when (IsDisplayable(exception))
        {
            Error ??= Explain(exception);
        }
    }

    private void Refresh()
    {
        var devices = _connections.GetDevices();
        if (Devices.SequenceEqual(devices))
            return;

        string? selectedId = Selected?.Id;
        Devices = devices;
        Selected = devices.FirstOrDefault(device => device.Id == selectedId) ?? devices.FirstOrDefault();
    }
}
