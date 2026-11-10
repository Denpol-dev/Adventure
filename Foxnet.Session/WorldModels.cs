namespace Foxnet.Session;

public enum FactConfidence { Confirmed, Statement, Unverified }
public sealed record FactDefinition(string Id, string Text, string Source, FactConfidence Confidence);
public sealed record ContactInteraction(string Text, DateTimeOffset At);
public sealed record ContactCard(string Id, string Nickname, string? Name, int? Age, string? Organization,
    string Summary, string? Avatar, bool Blocked, bool HasBankAddress, int UnreadCount);
public sealed record Dossier(ContactCard Contact, string Notes, IReadOnlyList<FactDefinition> Facts,
    IReadOnlyList<ContactInteraction> History);

public enum RequestStatus { Pending, Paid, Rejected, Cancelled, Expired }
public sealed record PaymentRequest(string Id, string ContactId, string Nickname, long Amount, string Purpose,
    bool Incoming, RequestStatus Status, DateTimeOffset CreatedAt);
public sealed record BankEntry(string Id, string Nickname, long Amount, string Purpose, DateTimeOffset At);
public sealed record BankSnapshot(long Balance, IReadOnlyList<PaymentRequest> Requests, IReadOnlyList<BankEntry> History);
public enum BankError { None, InsufficientFunds, Unavailable, Invalid, RateLimited, AlreadyPending, IdConflict }
public sealed record BankResult(BankError Error, long Balance = 0, long Required = 0)
{
    public bool Success => Error == BankError.None;
}

public enum ValueKind { PhysicalItem, Document }
public enum TradeError { None, Unavailable, DeliveryRequired, IdConflict, AlreadyTransferred }
public sealed record TradeResult(TradeError Error, long Appraisal = 0, long Paid = 0);
public sealed record TradeOffer(string Id, string BuyerId, string Buyer, long Appraisal);
public sealed record OrbitalOffer(TradeOffer Offer, string Title, int Weight, string Condition);
public enum PublicationStatus { Reviewing, Held, Published }
public sealed record Publication(string DocumentId, string Title, PublicationStatus Status, DateTimeOffset? PublishedAt);
public interface IPhysicalDelivery
{
    bool IsDelivered(string valueId, string buyerId);
}

public sealed record NotificationState(long Revision, int UnreadMessages, int BankEvents, bool SoundEnabled);
