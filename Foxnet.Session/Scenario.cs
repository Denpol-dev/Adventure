using System.Text.Json;
using System.Text.Json.Serialization;

namespace Foxnet.Session;

public sealed record Scenario(
    List<ContactDefinition> Contacts, List<DeviceDefinition> Devices, List<IncomingDefinition> Messages,
    IntroductionDefinition? Introduction = null)
{
    public static Scenario LoadDefault()
    {
        using var stream = typeof(Scenario).Assembly.GetManifestResourceStream("Foxnet.Session.Scenarios.start.json")
            ?? throw new InvalidDataException("Стартовый сценарий FOXNET не найден.");
        return Load(stream);
    }

    public static Scenario Load(Stream stream)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
        };
        var scenario = JsonSerializer.Deserialize<Scenario>(stream, options)
            ?? throw new InvalidDataException("Стартовый сценарий FOXNET пуст.");
        scenario.Validate();
        return scenario;
    }

    internal string Render(string text, IncomingDefinition message)
    {
        if (message.CredentialDeviceId == null)
            return text;

        var credentials = Devices.Single(device => device.Id == message.CredentialDeviceId).Credentials;
        return text.Replace("{login}", credentials.Login, StringComparison.Ordinal)
            .Replace("{password}", credentials.Password, StringComparison.Ordinal);
    }

    private void Validate()
    {
        if (Contacts is not { Count: > 0 } || Devices is not { Count: > 0 } || Messages is not { Count: > 0 } ||
            !Unique(Contacts.Select(contact => contact?.Id)) || !Unique(Devices.Select(device => device?.Id)) ||
            !Unique(Messages.Select(message => message?.Id)))
            throw new InvalidDataException("Сценарий содержит пустые или повторяющиеся идентификаторы.");

        if (Contacts.Any(contact => contact.Details is { } npc &&
            (npc.InitialBalance < 0 || npc.CreditsPerMinute < 0 || npc.RequestLimit < 0 || npc.MaxPaidRequests < 0 ||
             npc.ValueKind != null && (npc.ResaleMin <= 0 || npc.ResaleMax < npc.ResaleMin || npc.ResaleMax > 10000))))
            throw new InvalidDataException("Некорректные параметры экономики персонажа.");

        var contacts = Contacts.Select(contact => contact.Id).ToHashSet(StringComparer.Ordinal);
        var devices = Devices.Select(device => device.Id).ToHashSet(StringComparer.Ordinal);
        var messages = Messages.Select(message => message.Id).ToHashSet(StringComparer.Ordinal);
        if (Introduction is { } timing && (timing.ReminderMinutes <= 0 || timing.DeferralMinutes <= 0 ||
            timing.JournalistMinutes <= 0 || timing.IvanMinutes < timing.JournalistMinutes || timing.SecurityMinutes < 0 ||
            timing.RequestDecisionMinutes <= 0 || timing.RequestExpiryMinutes <= timing.RequestDecisionMinutes ||
            timing.RequestCooldownMinutes <= 0 ||
            !new[] { "unknown-relay", "unknown-invoice", "unknown-reminder", "unknown-expired", "unknown-paid", "journalist-intro",
                "ivan-intro", "security-intro", "director-intro" }.All(messages.Contains) ||
            !Contacts.Any(contact => contact.Id == "unknown" && contact.Details != null)))
            throw new InvalidDataException("Некорректные параметры вступления FOXNET.");

        if (Contacts.Any(contact => string.IsNullOrWhiteSpace(contact.Nickname)) ||
            Devices.Any(device => string.IsNullOrWhiteSpace(device.Name) || string.IsNullOrWhiteSpace(device.Kind) ||
                device.Credentials == null || string.IsNullOrWhiteSpace(device.Credentials.Login) ||
                string.IsNullOrWhiteSpace(device.Credentials.Password)))
            throw new InvalidDataException("Сценарий содержит неполное описание контакта или устройства.");

        var attachmentIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var message in Messages)
        {
            if (!contacts.Contains(message.ContactId) || string.IsNullOrWhiteSpace(message.Text) ||
                (message.CredentialDeviceId != null && !devices.Contains(message.CredentialDeviceId)) ||
                message.Attachments == null || message.Replies == null || !Unique(message.Replies.Select(reply => reply?.Id)))
                throw new InvalidDataException($"Некорректное сообщение сценария '{message.Id}'.");

            foreach (var attachment in message.Attachments)
            {
                if (attachment == null || string.IsNullOrWhiteSpace(attachment.EventId) || !attachmentIds.Add(attachment.EventId) ||
                    string.IsNullOrWhiteSpace(attachment.Title) || string.IsNullOrWhiteSpace(attachment.FileName) ||
                    attachment.Text == null || !Enum.IsDefined(attachment.Format))
                    throw new InvalidDataException($"Некорректное вложение сообщения '{message.Id}'.");
            }

            foreach (var reply in message.Replies)
            {
                if (string.IsNullOrWhiteSpace(reply.Text) ||
                    (reply.NextMessageId != null && !messages.Contains(reply.NextMessageId)) ||
                    (reply.UnlockDeviceId != null && !devices.Contains(reply.UnlockDeviceId)))
                    throw new InvalidDataException($"Некорректный вариант ответа '{reply.Id}'.");
            }

            if (message.Facts is { } facts && (!Unique(facts.Select(fact => fact?.Id)) ||
                facts.Any(fact => string.IsNullOrWhiteSpace(fact.Text) || string.IsNullOrWhiteSpace(fact.Source))))
                throw new InvalidDataException($"Некорректное сведение в сообщении '{message.Id}'.");
        }
    }

    private static bool Unique(IEnumerable<string?> ids)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return ids.All(id => !string.IsNullOrWhiteSpace(id) && seen.Add(id));
    }
}

public sealed record ContactDefinition(string Id, string Nickname, NpcDefinition? Details = null);
public sealed record NpcDefinition(string? Name, int? Age, string? Organization, string Summary, string? Avatar,
    long InitialBalance, long CreditsPerMinute, long RequestLimit, ValueKind? ValueKind, int ResaleMin, int ResaleMax,
    int MaxPaidRequests = 1);
public sealed record IntroductionDefinition(int ReminderMinutes, int DeferralMinutes, int JournalistMinutes,
    int IvanMinutes, int SecurityMinutes, int RequestDecisionMinutes, int RequestExpiryMinutes, int RequestCooldownMinutes);
public sealed record DeviceDefinition(string Id, string Name, string Kind, bool InitiallyAvailable, Credentials Credentials);
public sealed record Credentials(string Login, string Password);
public sealed record IncomingDefinition(string Id, string ContactId, bool DeliverOnStart, string? CredentialDeviceId, string Text,
    List<AttachmentDefinition> Attachments, List<ReplyDefinition> Replies, List<FactDefinition>? Facts = null,
    string? History = null);
public sealed record AttachmentDefinition(string EventId, string Title, string FileName, DocumentFormat Format, string Text);
public sealed record ReplyDefinition(string Id, string Text, string? NextMessageId, string? UnlockDeviceId,
    ReplyAction? Action = null, bool Independent = false, string? Relationship = null, bool Silent = false);
public enum ReplyAction { Dispute, Refuse, Defer, Plea }
