using System.Text;
using Microsoft.Data.Sqlite;
using static Foxnet.Session.SessionDatabase;

namespace Foxnet.Session;

public enum SecurityHypothesis { None, JournalistIsOperator, SeparateCollaborators }

public sealed partial class LocalSession
{
    public Publication? GetPublication(string documentId)
    {
        using var connection = _database.Open();
        using var query = Command(connection, null, """
            SELECT d.title, p.status, p.published_at FROM evidence_publications p
            JOIN registered_values r ON r.id = p.value_id JOIN documents d ON d.event_id = r.document_id
            WHERE d.event_id = $id;
            """, ("id", documentId));
        using var reader = query.ExecuteReader();
        return reader.Read() ? new(documentId, reader.GetString(0), (PublicationStatus)reader.GetInt32(1),
            reader.IsDBNull(2) ? null : At(reader.GetInt64(2))) : null;
    }

    private void SubmitEvidence(SqliteConnection connection, SqliteTransaction transaction, string id, string buyer, long now)
    {
        var evidence = ReadEvidence(connection, transaction, id);
        Execute(connection, transaction, "INSERT INTO evidence_publications(value_id, sent_at) VALUES($id, $now);",
            ("id", id), ("now", now));
        string message = "submitted:" + id;
        Execute(connection, transaction, """
            INSERT INTO messages(id, contact_id, text, sent_at, outgoing, is_read) VALUES($message, $buyer, $text, $now, 1, 1);
            INSERT INTO attachments(event_id, message_id, title, file_name, format, content)
            SELECT $attachment, $message, title, file_name, format, content FROM documents WHERE event_id = $document;
            """, ("message", message), ("buyer", buyer), ("text", "Передаю материал: " + evidence.Title), ("now", now),
            ("attachment", message + ":file"), ("document", evidence.Document));
        Observe(connection, transaction, buyer, "received:" + id, $"Игрок передал материал «{evidence.Title}».",
            "Полученное вложение", FactConfidence.Confirmed);
        DeliverMessagePart(connection, transaction, "review:" + id,
            new(buyer, "Материал получен. Я сверю источник и хронологию, прежде чем решать вопрос о публикации."), now);
        Schedule(connection, transaction, "verify:" + id, "review-evidence", id, After(now, 2));
    }

    private static void ReviewEvidence(SqliteConnection connection, SqliteTransaction transaction, string id, long now)
    {
        var evidence = ReadEvidence(connection, transaction, id);
        if (evidence.Signal == null)
        {
            Execute(connection, transaction, "UPDATE evidence_publications SET status = $held WHERE value_id = $id;",
                ("held", (int)PublicationStatus.Held), ("id", id));
            DeliverMessagePart(connection, transaction, "held:" + id,
                new(evidence.Buyer, $"«{evidence.Title}» оставлю в архиве. Пока этих записей недостаточно для отдельной публикации."), now);
            return;
        }
        Observe(connection, transaction, evidence.Buyer, "verified:" + id, $"Происхождение материала «{evidence.Title}» подтверждено.",
            "Проверка архивного источника", FactConfidence.Confirmed);
        Schedule(connection, transaction, "publish:" + id, "publish-evidence", id, After(now, 1));
    }

    private void PublishEvidence(SqliteConnection connection, SqliteTransaction transaction, string id, long now)
    {
        var evidence = ReadEvidence(connection, transaction, id);
        Execute(connection, transaction, """
            UPDATE evidence_publications SET status = $status, published_at = $now WHERE value_id = $id;
            """, ("status", (int)PublicationStatus.Published), ("id", id), ("now", now));
        string message = "published:" + id;
        string article = evidence.Signal switch
        {
            "restricted" => "Архивная запись противоречит официальному изложению событий. Ниже выдержка из проверенного материала.",
            "operator-trace" => "Технический журнал уточняет происхождение событий. Выдержку публикую без сведений о передавшем её источнике.",
            "identity-separation" => "Сверка с журналами подтверждает отдельный канал передачи сведений. Личность оператора редакция не раскрывает.",
            _ => "Записи о повторяющихся командах требуют отдельной проверки. Причинная связь с катастрофой по ним не установлена."
        };
        DeliverMessagePart(connection, transaction, message, new(evidence.Buyer,
            $"Публикация вышла: «{evidence.Title}». Прикладываю редакционную версию."), now);
        string content = $"{evidence.Title}\n\n{article}\n\n{evidence.Text}";
        Execute(connection, transaction, """
            INSERT INTO attachments(event_id, message_id, title, file_name, format, content)
            VALUES($file, $message, $title, 'publication.txt', $format, $content);
            """, ("file", message + ":article"), ("message", message), ("title", "Публикация · " + evidence.Title),
            ("format", (int)DocumentFormat.Text), ("content", Encoding.UTF8.GetBytes(content)));
        if (evidence.Signal is "restricted" or "operator-trace" or "identity-separation" && _contacts.ContainsKey("security"))
            Schedule(connection, transaction, "security-publication:" + id, "analyze-publication", id,
                After(now, _scenario.Introduction!.SecurityMinutes));
    }

    private void AnalyzePublication(SqliteConnection connection, SqliteTransaction transaction, string id, long now)
    {
        const string security = "security";
        var evidence = ReadEvidence(connection, transaction, id);
        var previous = CurrentHypothesis(connection, transaction);
        Observe(connection, transaction, security, "published:" + id, $"Со Ен-хи опубликовала «{evidence.Title}».",
            "Открытая публикация", FactConfidence.Confirmed);
        if (evidence.Signal is "operator-trace" or "identity-separation")
        {
            var signal = evidence.Signal == "operator-trace"
                ? "Часть журнала могла быть выгружена только с отдельного терминала."
                : "Публикация и технический журнал указывают на разные каналы и разделение ролей.";
            Observe(connection, transaction, security, evidence.Signal, signal, "Сопоставление открытых материалов",
                FactConfidence.Statement);
        }
        bool separate = Knows("operator-trace") && Knows("identity-separation");
        var next = separate ? SecurityHypothesis.SeparateCollaborators : SecurityHypothesis.JournalistIsOperator;
        if (next == previous)
            return;
        SetFlag(connection, transaction, "security-hypothesis", next.ToString());
        if (previous == SecurityHypothesis.None)
            Deliver(connection, transaction, _messages["security-intro"], now);
        string text = next == SecurityHypothesis.JournalistIsOperator
            ? "В публикации появился фрагмент внутреннего архива. Мы проверяем версию о том, что Со Ен-хи могла сама получить доступ."
            : "Сопоставление двух опубликованных журналов не подтверждает, что журналистка сама управляла оборудованием. Проверяем версию о раздельных ролях.";
        DeliverMessagePart(connection, transaction, "hypothesis:" + next, new(security, text), now);
        AddHistory(connection, transaction, "hypothesis:" + next, security, text, now);
        if (separate)
        {
            DeliverMessagePart(connection, transaction, "investigation-pressure", new(evidence.Buyer,
                "HAESEI направила редакции запрос об источниках двух материалов. Я продолжу проверку, но имена и канал передачи останутся закрытыми."), now);
            Schedule(connection, transaction, "director-intro", "message", "director-intro", After(now, 2));
        }

        bool Knows(string observation) => Scalar(connection, transaction, """
            SELECT 1 FROM npc_observations WHERE contact_id = $contact AND id = $id;
            """, ("contact", security), ("id", observation)) != null;
    }

    public SecurityHypothesis GetSecurityHypothesis()
    {
        using var connection = _database.Open();
        return CurrentHypothesis(connection, null);
    }

    private static SecurityHypothesis CurrentHypothesis(SqliteConnection connection, SqliteTransaction? transaction) =>
        Enum.TryParse<SecurityHypothesis>(Flag(connection, transaction, "security-hypothesis"), out var hypothesis)
            ? hypothesis : SecurityHypothesis.None;

    private static void Observe(SqliteConnection connection, SqliteTransaction transaction, string contact, string id,
        string text, string source, FactConfidence confidence) => Execute(connection, transaction, """
            INSERT INTO npc_observations(contact_id, id, text, source, confidence) VALUES($contact, $id, $text, $source, $confidence)
            ON CONFLICT(contact_id, id) DO NOTHING;
            """, ("contact", contact), ("id", id), ("text", text), ("source", source), ("confidence", (int)confidence));

    private static (string Document, string Title, string Text, string Buyer, string? Signal) ReadEvidence(
        SqliteConnection connection, SqliteTransaction transaction, string id)
    {
        using var query = Command(connection, transaction, """
            SELECT d.event_id, d.title, d.content, s.buyer_id, e.signal FROM expedition_evidence e
            JOIN registered_values r ON r.id = e.value_id JOIN documents d ON d.event_id = r.document_id
            JOIN value_sales s ON s.value_id = r.id WHERE e.value_id = $id ORDER BY s.settled_at LIMIT 1;
            """, ("id", id));
        using var reader = query.ExecuteReader();
        if (!reader.Read())
            throw new InvalidDataException("В истории публикаций отсутствует переданный документ.");
        return (reader.GetString(0), reader.GetString(1), Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(2)),
            reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4));
    }
}
