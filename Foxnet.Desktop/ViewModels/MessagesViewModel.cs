using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Foxnet.Session;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace Foxnet.Desktop.ViewModels;

public sealed partial class MessagesViewModel : ScreenViewModel
{
    private readonly LocalSession _session;
    private readonly Action<string> _openDocument;
    private readonly Action<string> _openDossier;
    private readonly Func<bool> _isVisible;
    private readonly DispatcherTimer _typingTimer = new();
    private readonly Dictionary<string, string> _drafts = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasSelection))] private ConversationRow? _selected;
    [ObservableProperty] private IReadOnlyList<MessageRow> _messages = [];
    [ObservableProperty] private bool _isTyping;
    [ObservableProperty] private string _draft = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowReplyOptions))] private IReadOnlyList<ReplyOption> _replies = [];

    public MessagesViewModel(LocalSession session, Action<string> openDocument, Action<string> openDossier, Func<bool> isVisible)
    {
        _session = session;
        _openDocument = openDocument;
        _openDossier = openDossier;
        _isVisible = isVisible;
        _typingTimer.Tick += OnTypingTimerTick;
        RefreshConversations();
    }

    public ObservableCollection<ConversationRow> Conversations { get; } = [];
    public bool IsEmpty => Conversations.Count == 0;
    public bool HasSelection => Selected != null;
    public bool ShowReplyOptions => !_session.HasDialogueProvider && Replies.Count != 0;
    public string DialogueMode => _session.HasDialogueProvider ? "Диалог с моделью · Ollama" : "Сценарный диалог";

    partial void OnSelectedChanging(ConversationRow? oldValue, ConversationRow? newValue)
    {
        if (oldValue != null)
            _drafts[oldValue.Id] = Draft;
    }

    partial void OnSelectedChanged(ConversationRow? value)
    {
        Messages = [];
        Replies = [];
        _typingTimer.Stop();
        IsTyping = false;
        Draft = value == null ? "" : _drafts.GetValueOrDefault(value.Id, "");
        if (value != null)
            Run(() => RefreshThread(markRead: _isVisible()));
    }

    [RelayCommand]
    private Task SendChatAsync() => RunAsync(async () =>
    {
        if (Selected is not { } selected)
            return;
        string text = Draft;
        var sending = _session.SendChatAsync(selected.Id, text);
        if (!sending.IsCompleted)
            RefreshThread(markRead: true);
        var result = await sending;
        bool sent = result is ChatResult.Scripted or ChatResult.Generated or ChatResult.ProviderUnavailable;
        if (sent && _drafts.GetValueOrDefault(selected.Id) == text)
            _drafts.Remove(selected.Id);
        if (Selected?.Id != selected.Id)
            return;
        if (sent && Draft == text)
            Draft = "";
        RefreshThread(markRead: _isVisible());
        Notice = result switch
        {
            ChatResult.Scripted or ChatResult.Generated => "Сообщение отправлено.",
            ChatResult.ProviderUnavailable => "Использован сценарный ответ.",
            ChatResult.WaitingForReply => "Дождитесь предыдущего ответа.",
            ChatResult.Invalid => "Введите сообщение длиной до 1200 символов.",
            _ => "Контакт недоступен для переписки."
        };
    });

    public void OpenContact(string id) => Selected = Conversations.FirstOrDefault(row => row.Id == id);

    public void Refresh(bool markRead)
    {
        if (Selected == null)
            RefreshConversations();
        else
            RefreshThread(markRead);
    }

    [RelayCommand]
    private void OpenDossier()
    {
        if (Selected is { } selected)
            _openDossier(selected.Id);
    }

    [RelayCommand]
    private void Reply(ReplyOption? option) => Run(() =>
    {
        if (option == null)
            return;
        bool sent = _session.SelectReply(option.MessageId, option.Id);
        RefreshThread(markRead: true);
        Notice = !sent ? "Этот ответ уже недоступен." : option.Silent ? "Сообщение оставлено без ответа." : "Ответ отправлен.";
    });

    [RelayCommand]
    private void OpenAttachment(Attachment? attachment) => Run(() =>
    {
        if (attachment == null)
            return;
        var document = _session.Documents.AddFromAttachment(attachment.EventId)
            ?? throw new InvalidOperationException("Вложение отсутствует в локальной сессии.");
        _openDocument(document.Info.EventId);
    });

    private void RefreshThread(bool markRead)
    {
        if (Selected == null)
            return;
        var history = _session.OpenConversation(Selected.Id, markRead);
        RefreshTyping(Selected.Id);
        if (!Messages.Select(row => row.Message.Id).SequenceEqual(history.Select(message => message.Id)))
            Messages = history.Select(message => new MessageRow(message)).ToArray();
        var replies = history.SelectMany(message => message.Replies).ToArray();
        if (!Replies.SequenceEqual(replies))
            Replies = replies;
        RefreshConversations();
    }

    private void RefreshTyping(string contactId)
    {
        _typingTimer.Stop();
        var delay = _session.GetContactTypingDelay(contactId);
        IsTyping = delay == TimeSpan.Zero;
        if (delay is { } remaining && remaining > TimeSpan.Zero)
        {
            _typingTimer.Interval = remaining;
            _typingTimer.Start();
        }
    }

    private void OnTypingTimerTick(object? sender, EventArgs args)
    {
        _typingTimer.Stop();
        IsTyping = true;
    }

    private void RefreshConversations()
    {
        var conversations = _session.GetConversations();
        var contacts = _session.GetContacts().ToDictionary(contact => contact.Id);
        for (int index = 0; index < conversations.Count; index++)
        {
            var conversation = conversations[index];
            var row = Conversations.FirstOrDefault(existing => existing.Id == conversation.Id);
            if (row == null)
                Conversations.Insert(index, new ConversationRow(conversation, contacts.GetValueOrDefault(conversation.Id)?.Avatar));
            else
            {
                row.Model = conversation;
                int previous = Conversations.IndexOf(row);
                if (previous != index)
                    Conversations.Move(previous, index);
            }
        }
        OnPropertyChanged(nameof(IsEmpty));
    }
}

public sealed partial class ConversationRow(Conversation model, string? avatar) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastMessage), nameof(UnreadCount), nameof(IsUnread), nameof(Time))]
    private Conversation _model = model;

    public string Id => Model.Id;
    public string Nickname => Model.Nickname;
    public string? Avatar => avatar;
    public string LastMessage => Model.LastMessage.ReplaceLineEndings(" ");
    public int UnreadCount => Model.UnreadCount;
    public bool IsUnread => UnreadCount > 0;
    public string Time => Model.LastActivity.ToLocalTime().ToString("HH:mm");
}

public sealed record MessageRow(Message Message)
{
    public bool Outgoing => Message.Outgoing;
    public string Text => Message.Text;
    public string Time => Message.SentAt.ToLocalTime().ToString("dd.MM  HH:mm");
    public IReadOnlyList<Attachment> Attachments => Message.Attachments;
}
