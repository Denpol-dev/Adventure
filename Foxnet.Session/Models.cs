namespace Foxnet.Session;

public sealed record PlayerProfile(Guid Id, string Nickname, byte[]? Avatar);

public sealed record Conversation(string Id, string Nickname, string LastMessage, DateTimeOffset LastActivity, int UnreadCount);

public sealed record Message(string Id, string ContactId, string Text, DateTimeOffset SentAt, bool Outgoing,
    IReadOnlyList<Attachment> Attachments, IReadOnlyList<ReplyOption> Replies);

public sealed record ReplyOption(string MessageId, string Id, string Text, bool Silent = false);

public sealed record Attachment(string EventId, string Title, string FileName, DocumentFormat Format);

public enum DocumentFormat { Text, Markdown, Json, Pdf, Image, Other }

public sealed record DocumentInfo(string EventId, string Title, string FileName, DocumentFormat Format, string Source,
    DateTimeOffset ReceivedAt);

public sealed record LibraryDocument(DocumentInfo Info, byte[] Content);

public sealed record ReceivedDocument(string EventId, string Title, string FileName, DocumentFormat Format, string Source,
    byte[] Content);

public sealed record Device(string Id, string Name, string Kind, bool Authorized, ConsoleIdentity? Connection)
{
    public bool Connected => Connection != null;
}

public sealed record ConsoleIdentity(int ProcessId, long StartedAtUtcTicks);
