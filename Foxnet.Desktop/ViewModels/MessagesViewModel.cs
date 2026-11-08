using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Foxnet.Session;
using System.Collections.ObjectModel;

namespace Foxnet.Desktop.ViewModels;

public sealed partial class MessagesViewModel : ScreenViewModel
{
    private readonly LocalSession _session;
    private readonly Action<string> _openDocument;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasSelection))] private ConversationRow? _selected;
    [ObservableProperty] private IReadOnlyList<MessageRow> _messages = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasReplies))] private IReadOnlyList<ReplyOption> _replies = [];

    public MessagesViewModel(LocalSession session, Action<string> openDocument)
    {
        _session = session;
        _openDocument = openDocument;
        RefreshConversations();
    }

    public ObservableCollection<ConversationRow> Conversations { get; } = [];
    public bool IsEmpty => Conversations.Count == 0;
    public bool HasSelection => Selected != null;
    public bool HasReplies => Replies.Count != 0;

    partial void OnSelectedChanged(ConversationRow? value)
    {
        Messages = [];
        Replies = [];
        if (value != null)
            Run(RefreshThread);
    }

    [RelayCommand]
    private void Reply(ReplyOption? option) => Run(() =>
    {
        if (option == null)
            return;
        bool sent = _session.SelectReply(option.MessageId, option.Id);
        RefreshThread();
        Notice = sent ? "Ответ отправлен." : "Этот ответ уже недоступен.";
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

    private void RefreshThread()
    {
        if (Selected == null)
            return;
        var history = _session.OpenConversation(Selected.Id);
        Messages = history.Select(message => new MessageRow(message)).ToArray();
        Replies = history.SelectMany(message => message.Replies).ToArray();
        RefreshConversations();
    }

    private void RefreshConversations()
    {
        var conversations = _session.GetConversations();
        for (int index = 0; index < conversations.Count; index++)
        {
            var conversation = conversations[index];
            var row = Conversations.FirstOrDefault(existing => existing.Id == conversation.Id);
            if (row == null)
                Conversations.Insert(index, new ConversationRow(conversation));
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

public sealed partial class ConversationRow(Conversation model) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastMessage), nameof(UnreadCount), nameof(IsUnread), nameof(Time))]
    private Conversation _model = model;

    public string Id => Model.Id;
    public string Nickname => Model.Nickname;
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
