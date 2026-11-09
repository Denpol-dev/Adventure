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
            SELECT id, appraisal FROM registered_values WHERE document_id = $id AND owned = 1;
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

        var buyer = _contacts.GetValueOrDefault(buyerId)?.Details;
        if (buyer?.ValueKind is not { } kind || !HasBankAddress(connection, transaction, buyerId) ||
            IsBlocked(connection, transaction, buyerId))
            return new TradeResult(TradeError.Unavailable);

        if (Scalar(connection, transaction, """
            SELECT appraisal FROM registered_values WHERE id = $value AND kind = $kind AND owned = 1;
            """, ("value", valueId), ("kind", (int)kind)) is not long appraisal)
            return new TradeResult(TradeError.Unavailable);
        if (kind == ValueKind.PhysicalItem && _delivery?.IsDelivered(valueId, buyerId) != true)
            return new TradeResult(TradeError.DeliveryRequired);

        var balance = NpcBalance(connection, transaction, buyerId);
        var paid = Math.Min(balance, appraisal);
        var multiplier = _random.Next(buyer.ResaleMin, buyer.ResaleMax + 1);
        var proceeds = checked((long)decimal.Round(appraisal * (decimal)multiplier / 100, 0, MidpointRounding.AwayFromZero));
        var remaining = checked(balance - paid + proceeds);
        var purpose = kind == ValueKind.Document ? "Оплата за материал расследования" : "Оплата за доставленную ценность";
        if (paid > 0)
            ReceiveCredits(connection, transaction, "sale:" + id, buyerId, paid, purpose, now);
        SetNpcBalance(connection, transaction, buyerId, remaining);
        AddNpcEntry(connection, transaction, "resale:" + id, buyerId, proceeds, "resale", now);
        Execute(connection, transaction, """
            INSERT INTO value_sales(id, value_id, buyer_id, appraisal, paid, multiplier, proceeds, settled_at)
            VALUES($id, $value, $buyer, $appraisal, $paid, $multiplier, $proceeds, $now);
            """, ("id", id), ("value", valueId), ("buyer", buyerId), ("appraisal", appraisal),
            ("paid", paid), ("multiplier", multiplier), ("proceeds", proceeds), ("now", now));
        if (kind == ValueKind.PhysicalItem)
            Execute(connection, transaction, "UPDATE registered_values SET owned = 0 WHERE id = $value;", ("value", valueId));
        AddHistory(connection, transaction, "trade:" + id, buyerId,
            $"Передана ценность. Оценка: {appraisal} ¢. Выплата: {paid} ¢.", now);
        if (paid < appraisal && _messages.TryGetValue(buyerId + "-partial", out var reaction))
            Deliver(connection, transaction, reaction, now, reaction.Id + ":" + id);
        return new TradeResult(TradeError.None, appraisal, paid);
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
