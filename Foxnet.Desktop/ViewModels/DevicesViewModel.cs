using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Foxnet.Session;
using System.IO;
using System.Text.Json;

namespace Foxnet.Desktop.ViewModels;

public sealed partial class DevicesViewModel : ScreenViewModel, IDisposable
{
    private readonly LocalSession _session;
    private readonly DeviceConnectionService _connections;
    private readonly ExpeditionPipeClient _expedition;
    private ConsoleIdentity? _watched;
    private CancellationTokenSource? _subscription;
    private bool _disposed;

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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpeditionLabel), nameof(HasExpedition))]
    private ExpeditionStatus? _expeditionStatus;

    public DevicesViewModel(LocalSession session, DeviceConnectionService connections, string databasePath)
    {
        _session = session;
        _connections = connections;
        _expedition = new ExpeditionPipeClient(databasePath);
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
    public bool HasExpedition => ExpeditionStatus != null;
    public string ExpeditionLabel => ExpeditionStatus switch
    {
        { GameOver: true } => "Экспедиция завершена",
        { } status => $"{status.SelectedDroneId} · {status.Mode} · " +
            $"{status.Drones.FirstOrDefault(drone => drone.Selected)?.ChargePercent ?? 0:0.#}%",
        _ => ""
    };

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
        if (!Devices.SequenceEqual(devices))
        {
            string? selectedId = Selected?.Id;
            Devices = devices;
            Selected = devices.FirstOrDefault(device => device.Id == selectedId) ?? devices.FirstOrDefault();
        }
        ObserveExpedition(Selected?.Connection);
    }

    private void ObserveExpedition(ConsoleIdentity? connection)
    {
        if (_disposed || connection == _watched && _subscription != null)
            return;

        _subscription?.Cancel();
        _subscription = null;
        _watched = connection;
        ExpeditionStatus = null;
        if (connection != null)
        {
            _subscription = new CancellationTokenSource();
            _ = ReadExpeditionAsync(_subscription);
        }
    }

    private async Task ReadExpeditionAsync(CancellationTokenSource subscription)
    {
        try
        {
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(subscription.Token);
            handshake.CancelAfter(TimeSpan.FromSeconds(2));
            var reply = await _expedition.RequestAsync(new ExpeditionRequest(ExpeditionRequestKind.Initialize), handshake.Token);
            if (!ReferenceEquals(_subscription, subscription))
                return;
            ExpeditionStatus = reply.Status;
            await foreach (var status in _expedition.WatchAsync(subscription.Token))
            {
                if (!ReferenceEquals(_subscription, subscription))
                    break;
                ExpeditionStatus = status;
            }
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or OperationCanceledException or
                                           InvalidDataException or JsonException or UnauthorizedAccessException)
        {
        }
        finally
        {
            if (ReferenceEquals(_subscription, subscription))
            {
                _subscription = null;
                ExpeditionStatus = null;
            }
            subscription.Dispose();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _subscription?.Cancel();
        _subscription = null;
    }
}
