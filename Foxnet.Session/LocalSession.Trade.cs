using Microsoft.Data.Sqlite;
using static Foxnet.Session.SessionDatabase;

namespace Foxnet.Session;

public sealed partial class LocalSession
{
    public bool RegisterValue(string valueId, ValueKind kind, long appraisal, string? documentId = null)
    {
        if (string.IsNullOrWhiteSpace(valueId) || !Enum.IsDefined(kind) || appraisal <= 0 ||
            (kind == ValueKind.Document ? string.IsNullOrWhiteSpace(documentId) : documentId != null))
            throw new ArgumentException("Некорректное описание ценности.");

        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        if (documentId != null && Scalar(connection, transaction,
                "SELECT 1 FROM documents WHERE event_id = $id;", ("id", documentId)) == null)
            return false;
        Execute(connection, transaction, """
            INSERT INTO registered_values(id, kind, appraisal, document_id) VALUES($id, $kind, $appraisal, $document)
            ON CONFLICT DO NOTHING;
            """, ("id", valueId), ("kind", (int)kind), ("appraisal", appraisal), ("document", documentId));

        using var query = Command(connection, transaction, """
            SELECT 1 FROM registered_values WHERE id = $id AND kind = $kind AND appraisal = $appraisal AND document_id IS $document;
            """, ("id", valueId), ("kind", (int)kind), ("appraisal", appraisal), ("document", documentId));
        var matches = query.ExecuteScalar() != null;
        transaction.Commit();
        return matches;
    }

    public TradeOffer? GetDocumentOffer(string documentId)
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        using var query = Command(connection, transaction, """
            SELECT r.id, r.appraisal FROM registered_values r
            WHERE r.document_id = $id AND r.owned = 1
                AND NOT EXISTS (SELECT 1 FROM evidence_publications p WHERE p.value_id = r.id);
            """, ("id", documentId));
        using var reader = query.ExecuteReader();
        if (!reader.Read())
            return null;
        var id = reader.GetString(0);
        var appraisal = reader.GetInt64(1);
        reader.Close();

        foreach (var contact in _scenario.Contacts)
            if (contact.Details?.ValueKind == ValueKind.Document && HasBankAddress(connection, transaction, contact.Id) &&
                !IsBlocked(connection, transaction, contact.Id) &&
                FindSale(connection, transaction, "value_id = $id AND buyer_id = $buyer", ("id", id), ("buyer", contact.Id)) == null)
                return new TradeOffer(id, contact.Id, contact.Nickname, appraisal);
        return null;
    }

    public TradeResult TransferValue(Guid operationId, string valueId, string buyerId)
    {
        if (operationId == Guid.Empty)
            return new TradeResult(TradeError.Unavailable);
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        var now = UtcNow();
        ProcessDue(connection, transaction, now);
        var result = SettleTrade(connection, transaction, operationId.ToString("D"), valueId, buyerId, now);
        transaction.Commit();
        return result;
    }

    public IReadOnlyList<OrbitalOffer> GetOrbitalOffers()
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        var buyer = _scenario.Contacts.FirstOrDefault(contact => contact.Details?.ValueKind == ValueKind.PhysicalItem &&
            HasBankAddress(connection, transaction, contact.Id) && !IsBlocked(connection, transaction, contact.Id));
        if (buyer == null)
            return [];

        using var query = Command(connection, transaction, """
            SELECT c.value_id, r.appraisal, c.title, c.weight, c.condition
            FROM orbital_cargo c JOIN registered_values r ON r.id = c.value_id
            WHERE c.transferred_at IS NULL AND r.owned = 1 ORDER BY c.delivered_at, c.value_id;
            """);
        using var reader = query.ExecuteReader();
        var offers = new List<OrbitalOffer>();
        while (reader.Read())
            offers.Add(new(new TradeOffer(reader.GetString(0), buyer.Id, buyer.Nickname, reader.GetInt64(1)),
                reader.GetString(2), reader.GetInt32(3), reader.GetString(4)));
        return offers;
    }

    private TradeResult SettleTrade(SqliteConnection connection, SqliteTransaction transaction,
        string id, string valueId, string buyerId, long now)
    {
        if (FindSale(connection, transaction, "id = $id", ("id", id)) is { } previous)
            return previous.Value == valueId && previous.Buyer == buyerId
                ? new TradeResult(TradeError.None, previous.Appraisal, previous.Paid)
                : new TradeResult(TradeError.IdConflict);
        if (FindSale(connection, transaction, "value_id = $value AND buyer_id = $buyer",
                ("value", valueId), ("buyer", buyerId)) != null)
            return new TradeResult(TradeError.AlreadyTransferred);
        if (Scalar(connection, transaction, "SELECT 1 FROM evidence_publications WHERE value_id = $id;",
                ("id", valueId)) != null)
            return new TradeResult(TradeError.AlreadyTransferred);

        var buyer = _contacts.GetValueOrDefault(buyerId)?.Details;
        if (buyer?.ValueKind is not { } kind || !HasBankAddress(connection, transaction, buyerId) ||
            IsBlocked(connection, transaction, buyerId))
            return new TradeResult(TradeError.Unavailable);

        if (Scalar(connection, transaction, """
            SELECT appraisal FROM registered_values WHERE id = $value AND kind = $kind AND owned = 1;
            """, ("value", valueId), ("kind", (int)kind)) is not long appraisal)
            return new TradeResult(TradeError.Unavailable);
        bool orbital = kind == ValueKind.PhysicalItem && Scalar(connection, transaction, """
            SELECT 1 FROM orbital_cargo WHERE value_id = $id AND transferred_at IS NULL;
            """, ("id", valueId)) != null;
        if (kind == ValueKind.PhysicalItem && !orbital && _delivery?.IsDelivered(valueId, buyerId) != true)
            return new TradeResult(TradeError.DeliveryRequired);
        bool evidence = kind == ValueKind.Document && Scalar(connection, transaction, """
            SELECT 1 FROM expedition_evidence WHERE value_id = $id;
            """, ("id", valueId)) != null;

        var balance = NpcBalance(connection, transaction, buyerId);
        var paid = Math.Min(balance, appraisal);
        var multiplier = _random.Next(buyer.ResaleMin, buyer.ResaleMax + 1);
        var proceeds = checked((long)decimal.Round(appraisal * (decimal)multiplier / 100, 0, MidpointRounding.AwayFromZero));
        var purpose = kind == ValueKind.Document ? "Оплата за материал расследования" : "Оплата за доставленную ценность";
        if (paid > 0)
            ReceiveCredits(connection, transaction, "sale:" + id, buyerId, paid, purpose, now);
        SetNpcBalance(connection, transaction, buyerId, balance - paid);
        Execute(connection, transaction, """
            INSERT INTO value_sales(id, value_id, buyer_id, appraisal, paid, multiplier, proceeds, settled_at)
            VALUES($id, $value, $buyer, $appraisal, $paid, $multiplier, $proceeds, $now);
            """, ("id", id), ("value", valueId), ("buyer", buyerId), ("appraisal", appraisal),
            ("paid", paid), ("multiplier", multiplier), ("proceeds", proceeds), ("now", now));
        if (kind == ValueKind.PhysicalItem)
            Execute(connection, transaction, "UPDATE registered_values SET owned = 0 WHERE id = $value;", ("value", valueId));
        if (orbital)
            Execute(connection, transaction, "UPDATE orbital_cargo SET transferred_at = $now WHERE value_id = $value;",
                ("now", now), ("value", valueId));
        if (evidence)
            SubmitEvidence(connection, transaction, valueId, buyerId, now);
        if (orbital || evidence)
            Schedule(connection, transaction, "resale:" + id, "resale", id, After(now, 8));
        else
            ResellValue(connection, transaction, id, now);
        AddHistory(connection, transaction, "trade:" + id, buyerId,
            $"Передана ценность. Оценка: {appraisal} ¢. Выплата: {paid} ¢.", now);
        if (paid < appraisal && _messages.TryGetValue(buyerId + "-partial", out var reaction))
            Deliver(connection, transaction, reaction, now, reaction.Id + ":" + id);
        return new TradeResult(TradeError.None, appraisal, paid);
    }

    private static void ResellValue(SqliteConnection connection, SqliteTransaction transaction, string id, long now)
    {
        using var query = Command(connection, transaction, "SELECT buyer_id, proceeds FROM value_sales WHERE id = $id;", ("id", id));
        using var reader = query.ExecuteReader();
        if (!reader.Read())
            return;
        string buyer = reader.GetString(0);
        long proceeds = reader.GetInt64(1);
        reader.Close();
        SetNpcBalance(connection, transaction, buyer, checked(NpcBalance(connection, transaction, buyer) + proceeds));
        AddNpcEntry(connection, transaction, "resale:" + id, buyer, proceeds, "resale", now);
    }

    private static (string Value, string Buyer, long Appraisal, long Paid)? FindSale(SqliteConnection connection,
        SqliteTransaction transaction, string filter, params (string Name, object? Value)[] parameters)
    {
        using var query = Command(connection, transaction,
            "SELECT value_id, buyer_id, appraisal, paid FROM value_sales WHERE " + filter + ";", parameters);
        using var reader = query.ExecuteReader();
        return reader.Read() ? (reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt64(3)) : null;
    }
}
