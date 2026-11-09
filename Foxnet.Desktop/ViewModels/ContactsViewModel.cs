using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Foxnet.Session;

namespace Foxnet.Desktop.ViewModels;

public sealed partial class ContactsViewModel(LocalSession session, Action<string> openChat) : ScreenViewModel
{
    private bool _loadingNotes;
    private bool _refreshing;
    [ObservableProperty] private IReadOnlyList<ContactCard> _contacts = [];
    [ObservableProperty] private ContactCard? _selected;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasDossier))] private Dossier? _dossier;
    [ObservableProperty] private IReadOnlyList<FactRow> _facts = [];
    [ObservableProperty] private string _notes = "";

    public bool HasDossier => Dossier != null;

    public void Refresh(string? contactId = null)
    {
        var id = contactId ?? Selected?.Id;
        var contacts = session.GetContacts();
        _refreshing = true;
        if (!Contacts.SequenceEqual(contacts))
            Contacts = contacts;
        Selected = Contacts.FirstOrDefault(contact => contact.Id == id) ?? Contacts.FirstOrDefault();
        _refreshing = false;
        ReloadDossier();
    }

    partial void OnSelectedChanged(ContactCard? oldValue, ContactCard? newValue)
    {
        if (!_refreshing && oldValue?.Id != newValue?.Id)
            ReloadDossier();
    }

    private void ReloadDossier()
    {
        Dossier = Selected == null ? null : session.GetDossier(Selected.Id);
        Facts = Dossier?.Facts.Select(fact => new FactRow(fact)).ToArray() ?? [];
        _loadingNotes = true;
        Notes = Dossier?.Notes ?? "";
        _loadingNotes = false;
    }

    partial void OnNotesChanged(string value)
    {
        if (!_loadingNotes && Dossier is { } dossier)
            Run(() => session.SaveContactNotes(dossier.Contact.Id, value));
    }

    [RelayCommand]
    private void OpenChat()
    {
        if (Selected is { } contact)
            openChat(contact.Id);
    }
}

public sealed record FactRow(FactDefinition Fact)
{
    public string Label => $"{Fact.Source} · {Fact.Confidence switch
    {
        FactConfidence.Confirmed => "Подтверждено",
        FactConfidence.Statement => "Со слов персонажа",
        _ => "Предположение"
    }}";
}
