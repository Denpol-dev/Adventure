using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Foxnet.Session;
using System.Globalization;
using System.Windows;

namespace Foxnet.Desktop.ViewModels;

public sealed partial class BankViewModel(LocalSession session) : ScreenViewModel
{
    [ObservableProperty] private string _balance = "";
    [ObservableProperty] private IReadOnlyList<RequestRow> _incoming = [];
    [ObservableProperty] private IReadOnlyList<RequestRow> _outgoing = [];
    [ObservableProperty] private IReadOnlyList<EntryRow> _history = [];
    [ObservableProperty] private IReadOnlyList<ContactCard> _recipients = [];
    [ObservableProperty] private ContactCard? _recipient;
    [ObservableProperty] private string _amount = "";
    [ObservableProperty] private string _purpose = "";
    [ObservableProperty] private bool _editing;
    [ObservableProperty] private string _formTitle = "";
    private bool _request;
    private Guid _operationId;

    public void Refresh()
    {
        var bank = session.GetBank();
        Balance = Format(bank.Balance);
        Incoming = bank.Requests.Where(request => request.Incoming).Select(request => new RequestRow(request)).ToArray();
        Outgoing = bank.Requests.Where(request => !request.Incoming).Select(request => new RequestRow(request)).ToArray();
        History = bank.History.Select(entry => new EntryRow(entry)).ToArray();
        if (Editing)
            RefreshRecipients();
    }

    [RelayCommand]
    private void StartTransfer() => StartForm(request: false);

    [RelayCommand]
    private void StartRequest() => StartForm(request: true);

    private void StartForm(bool request)
    {
        _request = request;
        _operationId = Guid.NewGuid();
        Amount = "";
        Purpose = "";
        Error = null;
        FormTitle = request ? "Запросить перевод" : "Перевести";
        Editing = true;
        RefreshRecipients();
    }

    private void RefreshRecipients()
    {
        var recipients = session.GetContacts().Where(contact => contact.HasBankAddress && (!_request || !contact.Blocked)).ToArray();
        if (Recipients.SequenceEqual(recipients))
            return;
        var id = Recipient?.Id;
        Recipients = recipients;
        Recipient = Recipients.FirstOrDefault(contact => contact.Id == id);
    }

    [RelayCommand]
    private void CloseForm() => Editing = false;

    [RelayCommand]
    private void Submit() => Run(() =>
    {
        if (Recipient == null || !long.TryParse(Amount, NumberStyles.None, CultureInfo.InvariantCulture, out var amount) ||
            amount <= 0 || string.IsNullOrWhiteSpace(Purpose))
        {
            Error = "Выберите получателя и укажите целую положительную сумму и назначение.";
            return;
        }

        if (!Confirm($"{FormTitle}?\n\n{Recipient.Nickname} · {Format(amount)}\n{Purpose.Trim()}"))
            return;
        var result = _request
            ? session.RequestPayment(_operationId, Recipient.Id, amount, Purpose.Trim())
            : session.Transfer(_operationId, Recipient.Id, amount, Purpose.Trim());
        if (result.Success)
            Editing = false;
        Complete(result);
    });

    [RelayCommand]
    private void Pay(RequestRow? row) => Run(() =>
    {
        if (row is { Pending: true } &&
            Confirm($"Оплатить запрос от {row.Request.Nickname}?\n\n{row.Amount} · {row.Request.Purpose}"))
            Complete(session.PayRequest(row.Request.Id));
    });

    [RelayCommand]
    private void Cancel(RequestRow? row) => Run(() =>
    {
        if (row is { Pending: true } && Confirm($"Отменить запрос для {row.Request.Nickname}?"))
            Complete(session.CancelRequest(row.Request.Id));
    });

    private void Complete(BankResult result)
    {
        Refresh();
        if (result.Success)
        {
            Notice = "Операция выполнена.";
            return;
        }

        if (result.Error == BankError.InsufficientFunds)
        {
            MessageBox.Show(Application.Current.MainWindow,
                $"Недостаточно средств.\n\nНа вашем счете {Format(result.Balance)}.\n\nДля выполнения перевода необходимо еще {Format(result.Required)}.",
                "HAESEI Bank", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Error = result.Error switch
        {
            BankError.AlreadyPending => "У этого получателя уже есть активный запрос.",
            BankError.RateLimited => "С момента последнего запроса прошло слишком мало времени.",
            BankError.Invalid => "Проверьте сумму и назначение.",
            _ => "Операция недоступна."
        };
    }

    private static bool Confirm(string message) => MessageBox.Show(Application.Current.MainWindow, message,
        "HAESEI Bank", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    internal static string Format(long amount) => amount.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ') + " ¢";
}

public sealed record RequestRow(PaymentRequest Request)
{
    public bool Pending => Request.Status == RequestStatus.Pending;
    public string Amount => BankViewModel.Format(Request.Amount);
    public string Status => Request.Status switch
    {
        RequestStatus.Pending => Request.Incoming ? "Ожидает оплаты" : "Ожидает ответа",
        RequestStatus.Paid => "Оплачен",
        RequestStatus.Rejected => "Отклонен",
        RequestStatus.Cancelled => Request.Incoming ? "Отозван" : "Отменен",
        _ => "Истек"
    };
}

public sealed record EntryRow(BankEntry Entry)
{
    public string Amount => (Entry.Amount > 0 ? "+" : "") + BankViewModel.Format(Entry.Amount);
    public string Date => Entry.At.ToLocalTime().ToString("dd.MM.yyyy  HH:mm");
}
