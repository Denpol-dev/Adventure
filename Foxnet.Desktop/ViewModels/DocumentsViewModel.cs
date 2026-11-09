using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Foxnet.Desktop.Services;
using Foxnet.Session;
using System.Windows;
using System.Windows.Documents;

namespace Foxnet.Desktop.ViewModels;

public sealed partial class DocumentsViewModel : ScreenViewModel
{
    private readonly DocumentLibrary _library;
    private readonly LocalSession _session;
    private readonly string _dataDirectory;
    private LibraryDocument? _opened;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsEmpty))] private IReadOnlyList<DocumentRow> _documents = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasSelection))] private DocumentRow? _selected;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasPreview))] private FlowDocument? _preview;
    [ObservableProperty] private bool _canOpenExternally;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasOffer))] private TradeOffer? _offer;

    public DocumentsViewModel(LocalSession session, string dataDirectory)
    {
        _session = session;
        _library = session.Documents;
        _dataDirectory = dataDirectory;
        Refresh();
    }

    public bool IsEmpty => Documents.Count == 0;
    public bool HasSelection => Selected != null;
    public bool HasPreview => Preview != null;
    public bool HasOffer => Offer != null;

    public void Open(string eventId)
    {
        Refresh();
        Selected = Documents.FirstOrDefault(document => document.Info.EventId == eventId);
    }

    private void Refresh() => Documents = _library.List().Select(document => new DocumentRow(document)).ToArray();

    partial void OnSelectedChanged(DocumentRow? value) => Run(() =>
    {
        _opened = null;
        Offer = null;
        Preview = null;
        CanOpenExternally = false;
        if (value == null)
            return;

        _opened = _library.Get(value.Info.EventId) ?? throw new InvalidOperationException("Документ отсутствует в библиотеке.");
        CanOpenExternally = ExternalDocument.CanOpen(_opened.Info);
        (Preview, Notice) = DocumentPreview.Create(_opened);
        RefreshOffer();
    });

    public void RefreshOffer() => Offer = _opened == null ? null : _session.GetDocumentOffer(_opened.Info.EventId);

    [RelayCommand]
    private void SendMaterial() => Run(() =>
    {
        if (Offer is not { } offer)
            return;
        var appraisal = BankViewModel.Format(offer.Appraisal);
        var choice = MessageBox.Show(Application.Current.MainWindow,
            $"Передать материал контакту «{offer.Buyer}»?\n\nОценка: {appraisal}. Выплата может оказаться неполной или нулевой. Передача необратима.",
            "Передача материала", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (choice != MessageBoxResult.Yes)
            return;
        var result = _session.TransferValue(Guid.NewGuid(), offer.Id, offer.BuyerId);
        RefreshOffer();
        if (result.Error == TradeError.None)
            Notice = $"Материал передан. Выплата: {BankViewModel.Format(result.Paid)}.";
        else
            Error = "Передача недоступна.";
    });

    [RelayCommand]
    private void OpenExternally() => Run(() =>
    {
        if (_opened != null)
            ExternalDocument.Open(_dataDirectory, _opened);
    });
}

public sealed record DocumentRow(DocumentInfo Info)
{
    public string Title => Info.Title;
    public string Source => Info.Source;
    public string Date => Info.ReceivedAt.ToLocalTime().ToString("dd.MM.yyyy  HH:mm");
    public string Format => Info.Format switch
    {
        DocumentFormat.Text => "ТЕКСТ",
        DocumentFormat.Markdown => "MARKDOWN",
        DocumentFormat.Json => "JSON",
        DocumentFormat.Pdf => "PDF",
        DocumentFormat.Image => "ИЗОБРАЖЕНИЕ",
        _ => "ФАЙЛ"
    };
}
